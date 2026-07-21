using System;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.MikroTik.Models;
using Lux.MikroTik.Exceptions;

namespace Lux.MikroTik.Providers;

public class RouterOsApiProvider : IRouterOsProvider, IRouterOsTextProvider
{
    private readonly IRouterOsApiClient _apiClient;

    public RouterOsApiProvider(IRouterOsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public bool IsConnected => _apiClient.IsConnected;

    public async Task<Result> ConnectAsync(MikroTikConnectionOptions options)
    {
        try
        {
            await _apiClient.ConnectAsync(options);
            return Result.Success();
        }
        catch (Exception ex)
        {
            throw new MikroTikConnectionException($"Failed to connect: {ex.Message}", ex);
        }
    }

    public async Task<Result> DisconnectAsync()
    {
        try
        {
            await _apiClient.DisconnectAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            throw new MikroTikConnectionException($"Failed to disconnect: {ex.Message}", ex);
        }
    }

    public async Task<Result<MikroTikResponse>> ExecuteAsync(MikroTikCommand command)
    {
        if (!IsConnected)
        {
            return Result<MikroTikResponse>.Failure("Not connected to MikroTik device.", ErrorType.ExternalService);
        }

        try
        {
            string[] paramArray;
            if (command.Arguments != null && command.Arguments.Length > 0)
            {
                paramArray = command.Arguments;
            }
            else
            {
                var paramList = new System.Collections.Generic.List<string>();
                foreach (var kvp in command.Parameters)
                {
                    paramList.Add(kvp.Key);
                    paramList.Add(kvp.Value);
                }
                paramArray = paramList.ToArray();
            }

            var rawData = await _apiClient.ExecuteAsync(command.Command, paramArray);
            
            var response = new MikroTikResponse
            {
                Success = true,
                Message = "Success"
            };

            var list = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>();
            foreach (var item in rawData)
            {
                var dict = new System.Collections.Generic.Dictionary<string, string>();
                foreach (var kvp in item)
                {
                    dict[kvp.Key] = kvp.Value;
                }
                list.Add(dict);
            }

            response.RawData = list;
            return Result<MikroTikResponse>.Success(response);
        }
        catch (Exception ex)
        {
            try
            {
                var errorPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lux_command_errors.txt");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR executing command: {command.Command}");
                if (command.Parameters != null)
                {
                    foreach (var p in command.Parameters)
                    {
                        sb.AppendLine($"  Param: {p.Key} = {p.Value}");
                    }
                }
                if (command.Arguments != null)
                {
                    sb.AppendLine($"  Args: {string.Join(" ", command.Arguments)}");
                }
                sb.AppendLine($"Exception: {ex.Message}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"Inner Exception: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                    {
                        sb.AppendLine($"Inner-Inner Exception: {ex.InnerException.InnerException.Message}");
                    }
                }
                sb.AppendLine(ex.StackTrace);
                sb.AppendLine("--------------------------------------------------");
                System.IO.File.AppendAllText(errorPath, sb.ToString());
            }
            catch {}
            throw new MikroTikCommandException($"Command execution failed: {ex.Message}", ex);
        }
    }

    public async Task<Result<string>> ExecuteTextAsync(MikroTikCommand command)
    {
        if (!IsConnected)
        {
            return Result<string>.Failure("Not connected to RouterOS.", ErrorType.ExternalService);
        }

        try
        {
            string[] paramArray;
            if (command.Arguments != null && command.Arguments.Length > 0)
            {
                paramArray = command.Arguments;
            }
            else
            {
                var paramList = new System.Collections.Generic.List<string>();
                foreach (var kvp in command.Parameters)
                {
                    paramList.Add(kvp.Key);
                    paramList.Add(kvp.Value);
                }
                paramArray = paramList.ToArray();
            }

            var rawText = await _apiClient.ExecuteTextAsync(command.Command, paramArray);
            return Result<string>.Success(rawText);
        }
        catch (Exception ex)
        {
            throw new MikroTikCommandException($"Command execution failed: {ex.Message}", ex);
        }
    }
}
