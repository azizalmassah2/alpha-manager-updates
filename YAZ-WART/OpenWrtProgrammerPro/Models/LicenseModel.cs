using System;
using System.Collections.Generic;

namespace OpenWrtProgrammerPro.Models
{
    public class LicenseModel
    {
        public int LicenseVersion { get; set; } = 1;
        public int KeyVersion { get; set; } = 1;
        public string LicenseId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public string CpuIdHash { get; set; } = string.Empty;
        public string BoardSerialHash { get; set; } = string.Empty;
        public string DiskSerialHash { get; set; } = string.Empty;
        public string MachineGuidHash { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int OfflineDays { get; set; }
        public int GracePeriodDays { get; set; }
        public string LicenseType { get; set; } = "Trial"; // Trial, Monthly, Yearly, Lifetime, Custom
        public bool IsRevoked { get; set; }
        public List<string> Features { get; set; } = new();
        public string Notes { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
