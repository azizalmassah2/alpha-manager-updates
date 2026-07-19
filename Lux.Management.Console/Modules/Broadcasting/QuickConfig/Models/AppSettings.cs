namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models
{
    /// <summary>
    /// إعدادات التطبيق التي يتم حفظها واستعادتها تلقائياً عند إغلاق/فتح البرنامج.
    /// </summary>
    public class AppSettings
    {
        // ── إعدادات الاتصال ──────────────────────────────────────────────────
        public string ConnectIp { get; set; } = "192.168.1.1";
        public string ConnectUsername { get; set; } = "root";
        public string ConnectPassword { get; set; } = "";

        // ── إعدادات الشبكة المستهدفة ─────────────────────────────────────────
        public string TargetIpsInput { get; set; } = "192.168.1.20";
        public string Gateway { get; set; } = "192.168.1.1";
        public string SubnetMask { get; set; } = "255.255.255.0";
        public int VlanId { get; set; } = 10;

        // ── إعدادات الشبكة الأساسية (تُعدل مرة واحدة) ──────────────────────
        public string BaseSsid24G { get; set; } = "LUX-4G";
        public string BaseSsid5G { get; set; } = "LUX-5G";
        public string BaseGateway { get; set; } = "10.0.0.1";
        public string BaseSubnet { get; set; } = "255.255.0.0";
        public string HostnamePrefix { get; set; } = "YAZ";
        public int LastProgrammedModemNumber { get; set; } = 0;
        public string NetworkPrefix { get; set; } = "10.0.0";
        public int StartingModemNumber { get; set; } = 2;

        // ── إعدادات الواي فاي ────────────────────────────────────────────────
        public string Ssid24Ghz { get; set; } = "OpenWrt_2.4G";
        public string Ssid5Ghz { get; set; } = "OpenWrt_5G";
        public string WifiPassword { get; set; } = "";
        public bool IsWifiEncrypted { get; set; } = false;

        // ── وضع التشغيل ──────────────────────────────────────────────────────
        public string SelectedMode { get; set; } = "AccessPoint";  // AccessPoint | ClientWds
        public string RemoteSsid { get; set; } = "";
        public string RemotePassword { get; set; } = "";

        // ── إعدادات كلمة المرور النهائية ─────────────────────────────────────────
        public string NewPassword { get; set; } = "";
        public bool ChangePasswordAfterProgramming { get; set; } = false;
        public bool TryNetworkPasswordFirst { get; set; } = false;
    }
}
