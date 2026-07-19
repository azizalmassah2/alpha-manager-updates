namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models
{
    /// <summary>
    /// صلاحيات UCI المُمنوحة فعلياً من خلال ACL الجلسة (session.login).
    /// هذه تعكس الواقع الحقيقي — وجود الدالة في ubus list لا يعني الإذن بتنفيذها.
    /// </summary>
    public class DeviceAcls
    {
        // ── صلاحيات UCI الأساسية ────────────────────────────────────────────────
        public bool CanGet    { get; set; }
        public bool CanSet    { get; set; }
        public bool CanAdd    { get; set; }
        public bool CanDelete { get; set; }
        public bool CanApply  { get; set; }
        public bool CanCommit { get; set; }

        // ── صلاحيات إضافية ──────────────────────────────────────────────────────
        public bool CanOrder  { get; set; }
        public bool CanRename { get; set; }
        public bool CanChanges{ get; set; }
        public bool CanConfirm{ get; set; }

        /// <summary>
        /// وضع البرمجة المُحدَّد بناءً على الصلاحيات الفعلية.
        /// </summary>
        public string ProgrammingMode
        {
            get
            {
                if (CanSet && CanCommit && CanApply)
                    return "وضع كامل (Mode A: set + commit + apply)";
                if (CanSet && CanApply && !CanCommit)
                    return "وضع جزئي (Mode B: set + apply بدون commit)";
                if (CanSet && !CanCommit && !CanApply)
                    return "وضع مقيّد (Mode C: set only — runtime فقط)";
                return "وضع محدود";
            }
        }

        /// <summary>
        /// يُنشئ كائن ACL آمن بكل الصلاحيات (للتوافق الخلفي عند تعذّر القراءة).
        /// </summary>
        public static DeviceAcls FullPermissions() => new()
        {
            CanGet = true, CanSet = true, CanAdd = true,
            CanDelete = true, CanApply = true, CanCommit = true,
            CanOrder = true, CanRename = true, CanChanges = true, CanConfirm = true
        };

        /// <summary>
        /// يُنشئ كائن ACL من المصفوفة المُعادة في session.login → acls.uci
        /// </summary>
        public static DeviceAcls FromUciAclArray(System.Collections.Generic.IEnumerable<string> grantedMethods)
        {
            var set = new System.Collections.Generic.HashSet<string>(
                grantedMethods, System.StringComparer.OrdinalIgnoreCase);

            return new DeviceAcls
            {
                CanGet     = set.Contains("get"),
                CanSet     = set.Contains("set"),
                CanAdd     = set.Contains("add"),
                CanDelete  = set.Contains("delete"),
                CanApply   = set.Contains("apply"),
                CanCommit  = set.Contains("commit"),
                CanOrder   = set.Contains("order"),
                CanRename  = set.Contains("rename"),
                CanChanges = set.Contains("changes"),
                CanConfirm = set.Contains("confirm")
            };
        }
    }
}
