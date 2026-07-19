using System;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Ubus
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        
        // Advanced UBUS info
        public string? UbusMethod { get; set; }
        public int? HttpStatus { get; set; }
        public string? RequestJson { get; set; }
        public string? ResponseJson { get; set; }

        public string DisplayText => $"[{Timestamp:HH:mm:ss}] [{LevelText}] {Message}";

        private string LevelText => Level switch
        {
            LogLevel.Info => "معلومات",
            LogLevel.Success => "نجاح",
            LogLevel.Warning => "تنبيه",
            LogLevel.Error => "خطأ",
            LogLevel.Ubus => "UBUS",
            _ => "سجل"
        };
    }
}
