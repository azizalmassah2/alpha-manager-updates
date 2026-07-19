using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// ╔══════════════════════════════════════════════════════════╗
/// ║         أداة توليد كود ترخيص LuxCard — للمشرف فقط       ║
/// ║  لا تُوزَّع هذه الأداة — استخدمها لتوليد الأكواد فقط   ║
/// ╚══════════════════════════════════════════════════════════╝
///
/// الاستخدام:
///   LicenseKeyGenerator.exe
///   ثم أدخل الـ Serial Number للراوتر عند الطلب
///
/// كيفية الحصول على الـ Serial:
///   سجّل دخولك للراوتر → Terminal → /system/routerboard/print
///   ابحث عن: serial-number: XXXXXXXXXXXX
/// </summary>

// ─── السر الخاص — يجب أن يطابق LicenseService.LicenseHmacSecret ──────────
const string HmacSecret = "LuxCard-Admin-2024-Secret";
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = Encoding.UTF8;
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║      LuxCard — License Key Generator v1.0            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("🔌 أدخل Serial Number للراوتر (أو 'q' للخروج): ");
    Console.ResetColor();

    var serial = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(serial)) continue;
    if (serial.Equals("q", StringComparison.OrdinalIgnoreCase)) break;

    var key = GenerateLicenseKey(serial);

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✅ الراوتر:       {serial.ToUpperInvariant()}");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"🔑 كود الترخيص:  ");
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"   {key}");
    Console.ResetColor();
    Console.WriteLine(new string('─', 55));
    Console.WriteLine();
}

Console.WriteLine("وداعاً!");

static string GenerateLicenseKey(string routerSerial)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HmacSecret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(routerSerial.Trim().ToUpperInvariant()));
    var hex = Convert.ToHexString(hash)[..16].ToUpperInvariant();
    return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
}
