using System;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Entities.Platform;

/// <summary>
/// كيان يمثل أجهزة البث (المودمات والانتينات) المضافة لقائمة الصيانة والإعداد
/// </summary>
public class BroadcastingDevice : BaseEntity
{
    /// <summary>اسم الجهاز التعريفي</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>عنوان IP الخاص بالجهاز</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>عنوان MAC الخاص بالجهاز</summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>نوع الجهاز (Modem / Antenna)</summary>
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>الشركة المصنعة (Ubiquiti, TP-Link, MikroTik, Cambium, etc.)</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>اسم مستخدم لوحة التحكم</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>كلمة مرور لوحة التحكم</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>ملاحظات وسجل الصيانة للجهاز</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>معرف الراوتر المرتبط به هذا الجهاز</summary>
    public Guid RouterId { get; set; }
}
