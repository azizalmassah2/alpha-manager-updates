namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models
{
    public class DeviceInfo
    {
        public string Hostname { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string OpenWrtVersion { get; set; } = "غير معروف";
        public string SessionId { get; set; } = string.Empty;
        public string SessionStatus { get; set; } = "غير متصل";
        public bool IsConnected { get; set; }

        // ── صلاحيات ACL الفعلية من session.login ─────────────────────────────
        /// <summary>
        /// الصلاحيات المُستخرجة فعلياً من استجابة session.login.
        /// تعكس الواقع الحقيقي — وجود دالة في ubus list لا يعني صلاحية تنفيذها.
        /// </summary>
        public DeviceAcls Acls { get; set; } = DeviceAcls.FullPermissions();

        // ── اختصارات للقراءة السريعة ───────────────────────────────────────────
        public bool CanCommit => Acls.CanCommit;
        public bool CanApply  => Acls.CanApply;

        /// <summary>
        /// وضع البرمجة المُكتشف (مشتق من ACL الفعلي).
        /// </summary>
        public string ProgrammingMode => Acls.ProgrammingMode;
    }
}
