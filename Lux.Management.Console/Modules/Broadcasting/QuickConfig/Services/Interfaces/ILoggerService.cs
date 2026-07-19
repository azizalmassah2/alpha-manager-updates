using System.Collections.ObjectModel;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces
{
    public interface ILoggerService
    {
        ObservableCollection<LogEntry> Entries { get; }
        void Log(string message, LogLevel level = LogLevel.Info);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message);
        
        void LogUbusRequest(string method, string requestJson);
        void LogUbusResponse(string method, int httpStatus, string responseJson);
        
        Task ExportToTxtAsync(string filePath);
        Task ExportToJsonAsync(string filePath);
        void Clear();
    }
}
