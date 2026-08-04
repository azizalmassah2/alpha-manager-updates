using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.MikroTik.Models;
using tik4net;

namespace Lux.MikroTik.Providers;

public class RouterOsApiClient : IRouterOsApiClient, IDisposable
{
    private ITikConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private MikroTikConnectionOptions? _lastOptions;

    private static readonly string LogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mikrotik_log.txt");

    public bool IsConnected => _connection != null && _connection.IsOpened;

    public async Task ConnectAsync(MikroTikConnectionOptions options)
    {
        await _lock.WaitAsync();
        try
        {
            _lastOptions = options;
            var st = Environment.StackTrace;
            var msg1 = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [RouterOsApiClient] ConnectAsync called. Host: {options.Host}, User: {options.Username}\nStackTrace:\n{st}\n";
            System.IO.File.AppendAllText(LogPath, msg1);
            
            var connectionType = options.UseSsl ? TikConnectionType.ApiSsl : TikConnectionType.Api;
            _connection = ConnectionFactory.CreateConnection(connectionType);
            
            var port = options.Port > 0 ? options.Port : (options.UseSsl ? 8729 : 8728);
            
            await _connection.OpenAsync(options.Host, port, options.Username, options.Password);
            
            var msg3 = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [RouterOsApiClient] Connection Opened. IsOpened: {_connection.IsOpened}\n";
            System.IO.File.AppendAllText(LogPath, msg3);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        var st = Environment.StackTrace;
        var msg1 = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [RouterOsApiClient] DisconnectAsync CALLED BY:\n{st}\n";
        System.IO.File.AppendAllText(LogPath, msg1);

        await _lock.WaitAsync();
        try
        {
            _lastOptions = null;
            if (_connection != null)
            {
                if (_connection.IsOpened)
                {
                    _connection.Close();
                    var msg2 = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [RouterOsApiClient] Connection Closed.\n";
                    System.IO.File.AppendAllText(LogPath, msg2);
                }
                
                var msg3 = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [RouterOsApiClient] Connection Disposed/Cleared.\n";
                System.IO.File.AppendAllText(LogPath, msg3);
                
                _connection = null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ReconnectInternalAsync()
    {
        if (_lastOptions == null) return;
        
        try
        {
            if (_connection != null)
            {
                if (_connection.IsOpened) _connection.Close();
            }
        }
        catch {}
        
        var connectionType = _lastOptions.UseSsl ? TikConnectionType.ApiSsl : TikConnectionType.Api;
        _connection = ConnectionFactory.CreateConnection(connectionType);
        
        var port = _lastOptions.Port > 0 ? _lastOptions.Port : (_lastOptions.UseSsl ? 8729 : 8728);
        await _connection.OpenAsync(_lastOptions.Host, port, _lastOptions.Username, _lastOptions.Password);
    }

    private bool IsConnectionException(Exception ex)
    {
        if (ex is System.IO.IOException || ex is System.Net.Sockets.SocketException || ex is tik4net.TikConnectionException)
            return true;
        if (ex.InnerException != null)
            return IsConnectionException(ex.InnerException);
        return false;
    }

    private bool IsRouterCommandException(Exception ex)
    {
        // TikCommandException means the router understood our command but returned an error (e.g. "no such command or directory")
        // This is NOT a connection drop — it is a command-level error.
        if (ex is tik4net.TikCommandException)
            return true;
        if (ex.InnerException != null)
            return IsRouterCommandException(ex.InnerException);
        return false;
    }

    private static bool IsWriteCommand(string command)
    {
        // Write commands need NameValue parameter format (=key=value) instead of filter format (?key=value).
        // Read commands (print, getall) do NOT need NameValue — their parameters are filters.
        return command.EndsWith("/add", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/set", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/remove", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/enable", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/disable", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/unset", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/move", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/import", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/export", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/save", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/load", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/reboot", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/shutdown", StringComparison.OrdinalIgnoreCase)
            || command.EndsWith("/run", StringComparison.OrdinalIgnoreCase);  // /system/script/run requires NameValue for .id/number
    }

    private async Task<IEnumerable<ITikSentence>> ExecuteWithRetryAsync(string command, string[] parameters)
    {
        if (_connection == null || !_connection.IsOpened)
        {
            if (_lastOptions != null)
            {
                try
                {
                    await ReconnectInternalAsync();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Not connected to MikroTik and auto-reconnect failed.", ex);
                }
            }
            else
            {
                throw new InvalidOperationException("Not connected to MikroTik.");
            }
        }

        try
        {
            return await Task.Run(() =>
            {
                var cmd = parameters == null || parameters.Length == 0 
                    ? _connection.CreateCommand(command) 
                    : _connection.CreateCommandAndParameters(command, parameters);
                
                // Apply NameValue format for all write commands (add/set/remove/enable/disable/unset)
                // Without this, tik4net sends parameters as filter queries (?key=value) instead of
                // write parameters (=key=value), which causes RouterOS errors like 'vlan-id bad'.
                if (IsWriteCommand(command))
                {
                    cmd.DefaultParameterFormat = tik4net.TikCommandParameterFormat.NameValue;
                }
                
                return cmd.ExecuteList();
            });
        }
        catch (Exception ex) when (IsRouterCommandException(ex))
        {
            // The router returned a command-level error (e.g. "no such command or directory").
            // tik4net closes the TCP socket after this — silently re-open it so subsequent commands work.
            if (_lastOptions != null)
            {
                try { await ReconnectInternalAsync(); } catch { }
            }
            // Re-throw the original exception so callers that wrap in try/catch can handle it gracefully.
            throw;
        }
        catch (Exception ex) when (IsConnectionException(ex))
        {
            if (_lastOptions != null)
            {
                try
                {
                    await ReconnectInternalAsync();
                    return await Task.Run(() =>
                    {
                        var cmd = parameters == null || parameters.Length == 0 
                            ? _connection.CreateCommand(command) 
                            : _connection.CreateCommandAndParameters(command, parameters);
                        
                        if (IsWriteCommand(command))
                        {
                            cmd.DefaultParameterFormat = tik4net.TikCommandParameterFormat.NameValue;
                        }
                        
                        return cmd.ExecuteList();
                    });
                }
                catch (Exception retryEx)
                {
                    throw new InvalidOperationException("MikroTik connection dropped and auto-reconnect retry failed.", retryEx);
                }
            }
            throw;
        }
    }

    public Task<IEnumerable<IDictionary<string, string>>> ExecuteAsync(string command)
    {
        return ExecuteAsync(command, Array.Empty<string>());
    }

    public async Task<IEnumerable<IDictionary<string, string>>> ExecuteAsync(string command, params string[] parameters)
    {
        await _lock.WaitAsync();
        try
        {
            var results = await ExecuteWithRetryAsync(command, parameters);
            
            var list = new List<IDictionary<string, string>>();
            foreach (var sentence in results)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in sentence.Words)
                {
                    dict[kvp.Key] = kvp.Value;
                }
                list.Add(dict);
            }
            return (IEnumerable<IDictionary<string, string>>)list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<string> ExecuteTextAsync(string command)
    {
        return ExecuteTextAsync(command, Array.Empty<string>());
    }

    public async Task<string> ExecuteTextAsync(string command, params string[] parameters)
    {
        await _lock.WaitAsync();
        try
        {
            var results = await ExecuteWithRetryAsync(command, parameters);
            
            var sb = new System.Text.StringBuilder();
            foreach (var sentence in results)
            {
                foreach (var kvp in sentence.Words)
                {
                    if (string.Equals(kvp.Key, "ret", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(kvp.Key))
                    {
                        sb.AppendLine(kvp.Value);
                    }
                }
            }
            return sb.ToString();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_connection != null)
        {
            if (_connection.IsOpened)
            {
                _connection.Close();
            }
            _connection.Dispose();
        }
        _lock.Dispose();
    }
}
