using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly object _lock = new();
        public ObservableCollection<LogEntry> Entries { get; } = new();

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            var entry = new LogEntry
            {
                Level = level,
                Message = message
            };

            AddEntry(entry);
        }

        public void LogSuccess(string message) => Log(message, LogLevel.Success);
        public void LogWarning(string message) => Log(message, LogLevel.Warning);
        public void LogError(string message) => Log(message, LogLevel.Error);

        private string MaskSensitiveData(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            try
            {
                // Mask any "password" : "value" or "key" : "value" that looks like a password
                return System.Text.RegularExpressions.Regex.Replace(json, 
                    @"(""password""\s*:\s*"")[^""]*("")", 
                    "$1******$2", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            catch
            {
                return json;
            }
        }

        public void LogUbusRequest(string method, string requestJson)
        {
            var maskedJson = MaskSensitiveData(requestJson);
            var entry = new LogEntry
            {
                Level = LogLevel.Ubus,
                Message = $"UBUS Request -> {method}",
                UbusMethod = method,
                RequestJson = maskedJson
            };
            AddEntry(entry);
        }

        public void LogUbusResponse(string method, int httpStatus, string responseJson)
        {
            var maskedJson = MaskSensitiveData(responseJson);
            var entry = new LogEntry
            {
                Level = LogLevel.Ubus,
                Message = $"UBUS Response <- {method} [HTTP {httpStatus}]",
                UbusMethod = method,
                HttpStatus = httpStatus,
                ResponseJson = maskedJson
            };
            AddEntry(entry);
        }

        public async Task ExportToTxtAsync(string filePath)
        {
            List<LogEntry> snapshot;
            lock (_lock)
            {
                snapshot = new List<LogEntry>(Entries);
            }

            using var writer = new StreamWriter(filePath);
            foreach (var entry in snapshot)
            {
                await writer.WriteLineAsync(entry.DisplayText);
                if (!string.IsNullOrEmpty(entry.RequestJson))
                {
                    await writer.WriteLineAsync($"   Request: {entry.RequestJson}");
                }
                if (!string.IsNullOrEmpty(entry.ResponseJson))
                {
                    await writer.WriteLineAsync($"   Response: {entry.ResponseJson}");
                }
            }
        }

        public async Task ExportToJsonAsync(string filePath)
        {
            List<LogEntry> snapshot;
            lock (_lock)
            {
                snapshot = new List<LogEntry>(Entries);
            }

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        public void Clear()
        {
            lock (_lock)
            {
                Entries.Clear();
            }
        }

        private void AddEntry(LogEntry entry)
        {
            if (Application.Current?.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    lock (_lock)
                    {
                        Entries.Add(entry);
                    }
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        lock (_lock)
                        {
                            Entries.Add(entry);
                        }
                    }));
                }
            }
            else
            {
                lock (_lock)
                {
                    Entries.Add(entry);
                }
            }
        }
    }
}
