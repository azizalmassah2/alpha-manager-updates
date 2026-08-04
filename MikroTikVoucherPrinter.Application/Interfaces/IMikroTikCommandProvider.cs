using System.Collections.Generic;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// مزود الأوامر الموحد للتواصل مع الراوتر بصرف النظر عن إصدار RouterOS.
/// كل تنفيذ (V6 / V7 / Hotspot) يبني الأوامر بالمعاملات الصحيحة لإصداره.
/// عام بما يكفي لدعم User Manager وHotspot وغيره مستقبلاً.
/// </summary>
public interface IMikroTikCommandProvider
{
    /// <summary>نوع النظام المكتشف على الراوتر الحالي</summary>
    RouterSystemType SystemType { get; }

    /// <summary>مسار أمر الاكتشاف/الفحص للتحقق من توفر الخدمة</summary>
    string DiscoveryPrintPath { get; }

    // ── User CRUD ─────────────────────────────────────────────────────────

    /// <summary>
    /// بناء أمر جلب مستخدم بالاسم من الراوتر.
    /// المعامل يختلف بين v6 (username) وv7 (name) وHotspot (name).
    /// </summary>
    RouterCommand BuildUserPrintCommand(string username);

    /// <summary>
    /// بناء أمر إضافة مستخدم جديد.
    /// المسار والمعاملات تختلف بين v6 وv7.
    /// </summary>
    RouterCommand BuildUserAddCommand(string username, string? password, string adminUser);

    /// <summary>
    /// بناء أمر حذف مستخدم واحد بالمعرف الداخلي (مثل *1A).
    /// </summary>
    RouterCommand BuildUserRemoveCommand(string internalId);

    /// <summary>
    /// بناء أمر حذف دُفعة مستخدمين بمعرفاتهم الداخلية.
    /// يستخدم numbers=*1,*2,*3 حيثما يدعمه الراوتر.
    /// </summary>
    RouterCommand BuildUserBulkRemoveCommand(IEnumerable<string> internalIds);

    // ── Profile / Group Assignment ────────────────────────────────────────

    /// <summary>
    /// بناء أمر تفعيل وتعيين الباقة (Profile) للمستخدم.
    /// المسار يختلف جذرياً: v6 يستخدم create-and-activate-profile ،
    /// v7 يستخدم /user-manager/user/profile/add.
    /// </summary>
    RouterCommand BuildAssignProfileCommand(string username, string profileName, string adminUser);
}
