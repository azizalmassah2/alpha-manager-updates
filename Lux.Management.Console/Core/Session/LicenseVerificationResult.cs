using System;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// نتيجة عملية التحقق من الترخيص بعد الاتصال بالراوتر
/// </summary>
public class LicenseVerificationResult
{
    public LicenseState State { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public bool IsValid => State == LicenseState.Valid;

    public static LicenseVerificationResult NoLicense()
        => new() { State = LicenseState.NoLicense, Message = "لا يوجد ملف ترخيص" };

    public static LicenseVerificationResult Valid(DateTime? expiresAt = null)
        => new() { State = LicenseState.Valid, Message = "الترخيص صالح", ExpiresAt = expiresAt };

    public static LicenseVerificationResult Expired()
        => new() { State = LicenseState.Expired, Message = "⚠️ انتهت صلاحية الترخيص. يرجى تجديده للاستمرار في الوضع الاحترافي." };

    public static LicenseVerificationResult Mismatch()
        => new() { State = LicenseState.RouterMismatch, Message = "❌ الترخيص مرتبط براوتر مختلف. تحقق من الراوتر المتصل." };

    public static LicenseVerificationResult Corrupted()
        => new() { State = LicenseState.Corrupted, Message = "❌ ملف الترخيص تالف أو التوقيع غير صالح." };
}
