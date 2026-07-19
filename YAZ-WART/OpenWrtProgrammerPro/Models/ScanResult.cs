namespace OpenWrtProgrammerPro.Models
{
    public class ScanResult
    {
        public string Ssid { get; set; } = string.Empty;
        public int SignalStrength { get; set; } // -100 to 0 (dBm)
        public int Channel { get; set; }
        public double Frequency { get; set; } // GHz, e.g. 5.180
        public string EncryptionType { get; set; } = "مفتوح";
        public string Bssid { get; set; } = string.Empty;

        public string SignalPercentText => $"{SignalStrength} dBm";
    }
}
