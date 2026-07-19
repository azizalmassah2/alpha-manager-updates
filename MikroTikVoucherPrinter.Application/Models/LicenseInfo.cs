using System;
using System.Collections.Generic;

namespace MikroTikVoucherPrinter.Application.Models;

/// <summary>
/// نموذج ملف الترخيص — متوافق مع LicenseModel في YAZ-WART/LicenseGenerator.
/// يُحفظ كـ license.dat (JSON موقَّع بـ RSA-SHA256).
/// </summary>
public class LicenseInfo
{
    public int LicenseVersion       { get; set; } = 1;
    public int KeyVersion           { get; set; } = 1;
    public string LicenseId         { get; set; } = string.Empty;
    public string CustomerName      { get; set; } = string.Empty;
    public string HardwareId        { get; set; } = string.Empty;

    // بصمات الأجهزة الفرعية (للمطابقة الجزئية Fuzzy Match)
    public string CpuIdHash         { get; set; } = string.Empty;
    public string BoardSerialHash   { get; set; } = string.Empty;
    public string DiskSerialHash    { get; set; } = string.Empty;
    public string MachineGuidHash   { get; set; } = string.Empty;

    public DateTime IssueDate       { get; set; }
    public DateTime ExpiryDate      { get; set; }
    public int OfflineDays          { get; set; }
    public int GracePeriodDays      { get; set; }
    public string LicenseType       { get; set; } = "Trial"; // Trial, Monthly, Yearly, Lifetime, Custom
    public bool IsRevoked           { get; set; }
    public List<string> Features    { get; set; } = new();
    public string Notes             { get; set; } = string.Empty;

    // تحقق من سلامة الحمولة (SHA-256)
    public string PayloadHash       { get; set; } = string.Empty;
    // التوقيع الرقمي (RSA-SHA256 بمفتاح خاص)
    public string Signature         { get; set; } = string.Empty;
}
