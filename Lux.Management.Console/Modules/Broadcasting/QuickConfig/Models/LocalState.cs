using System;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models
{
    public class LocalState
    {
        public DateTime InstallDate { get; set; }
        public DateTime LastRunDate { get; set; }
        public DateTime MaxSeenDate { get; set; }
    }
}
