using System;
using System.Collections.Generic;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models
{
    public class BackupData
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string DeviceIp { get; set; } = string.Empty;
        public Dictionary<string, object> Configs { get; set; } = new();
    }
}
