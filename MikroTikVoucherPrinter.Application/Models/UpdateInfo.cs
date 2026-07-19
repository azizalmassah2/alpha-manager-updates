using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MikroTikVoucherPrinter.Application.Models;

/// <summary>
/// نموذج بيانات التحديث المجلوب من سيرفر التحديثات.
/// يدعم قراءة update.json البسيط القديم (backward compatible)
/// وكذلك البنية الموسّعة الجديدة.
///
/// بنية update.json المدعومة:
/// {
///   "version":                "1.1.0",
///   "updateType":             "optional|recommended|mandatory|security",
///   "mandatory":              false,
///   "minimumSupportedVersion": "1.0.0",
///   "downloadUrl":            "https://...",
///   "sha256":                 "",           // محجوز للتحقق المستقبلي
///   "fileSize":               0,            // بالبايت، 0 = غير محدد
///   "releaseDate":            "2026-07-16",
///   "releaseNotes":           ["سطر 1", "سطر 2"],  // أو "نص" للتوافق القديم
///   "message":                "",           // رسالة إدارية
///   "enabled":                true          // false = تجاهل هذا الإصدار
/// }
/// </summary>
public class UpdateInfo
{
    // ══════════════════════════════════════════════════════════════════════
    // الحقول الأساسية
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>رقم الإصدار الجديد (مثال: 1.1.0)</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// نوع التحديث — يتحكم في سلوك نافذة التحديث وأزرارها.
    /// القيمة الافتراضية: Optional (لضمان التوافق مع الإصدارات القديمة)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UpdateType UpdateType { get; set; } = UpdateType.Optional;

    /// <summary>
    /// هل التحديث إجباري؟ — يُخفي زر "لاحقاً" ويمنع التجاوز.
    /// يكون true تلقائياً إذا كان UpdateType = Mandatory.
    /// </summary>
    public bool Mandatory { get; set; }

    /// <summary>
    /// الحد الأدنى للإصدار المدعوم.
    /// إذا كان إصدار العميل أقل من هذه القيمة يُعامَل التحديث كإجباري
    /// بغض النظر عن قيمة Mandatory.
    /// مثال: "1.0.5"
    /// </summary>
    public string MinimumSupportedVersion { get; set; } = string.Empty;

    // ══════════════════════════════════════════════════════════════════════
    // حقول التنزيل والتحقق
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>رابط تنزيل ملف التثبيت</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash للملف — محجوز للتحقق من سلامة الملف مستقبلاً.
    /// حالياً لا يُستخدم في التحقق، لكن البنية جاهزة.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>حجم الملف بالبايت. 0 = غير محدد</summary>
    public long FileSize { get; set; }

    // ══════════════════════════════════════════════════════════════════════
    // حقول العرض
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// تاريخ الإصدار (مثال: "2026-07-16").
    /// يُعرض في نافذة التحديث.
    /// </summary>
    public string ReleaseDate { get; set; } = string.Empty;

    /// <summary>
    /// ملاحظات الإصدار — مصفوفة نصوص تُعرض كنقاط منفصلة.
    /// يدعم أيضاً النص الفردي (backward compat) عبر StringOrArrayConverter.
    /// </summary>
    [JsonConverter(typeof(StringOrArrayJsonConverter))]
    public List<string> ReleaseNotes { get; set; } = new();

    /// <summary>
    /// رسالة إدارية تظهر داخل نافذة التحديث.
    /// مثال: "يوجد تحديث أمني مهم يؤثر على بيانات الترخيص."
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// هل هذا الإصدار نشط؟
    /// false = يتم تجاهله بالكامل (مفيد عند سحب إصدار معطوب).
    /// الافتراضي: true (للتوافق مع الملفات القديمة التي لا تحتوي هذا الحقل).
    /// </summary>
    public bool Enabled { get; set; } = true;

    // ══════════════════════════════════════════════════════════════════════
    // حقول تُحسب وقت الفحص (لا تُقرأ من JSON)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// يُضبط true من UpdateService إذا كان إصدار العميل أقل من MinimumSupportedVersion.
    /// في هذه الحالة يُعامَل التحديث كإجباري حتى لو Mandatory = false.
    /// </summary>
    [JsonIgnore]
    public bool IsForcedByMinVersion { get; set; }

    // ══════════════════════════════════════════════════════════════════════
    // Computed Properties
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// هل يجب منع المستخدم من تجاوز هذا التحديث؟
    /// يكون true في حالات:
    ///   - Mandatory = true
    ///   - UpdateType = Mandatory أو Security
    ///   - إصدار العميل أقل من MinimumSupportedVersion
    /// </summary>
    [JsonIgnore]
    public bool CannotSkip =>
        Mandatory
        || UpdateType == UpdateType.Mandatory
        || UpdateType == UpdateType.Security
        || IsForcedByMinVersion;

    /// <summary>حجم الملف مُنسَّق (KB / MB) لعرضه في الواجهة</summary>
    [JsonIgnore]
    public string FileSizeFormatted
    {
        get
        {
            if (FileSize <= 0) return "غير محدد";
            if (FileSize < 1024)           return $"{FileSize} B";
            if (FileSize < 1024 * 1024)    return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>نوع التحديث بالعربية لعرضه في الواجهة</summary>
    [JsonIgnore]
    public string UpdateTypeLabel => UpdateType switch
    {
        UpdateType.Optional    => "اختياري",
        UpdateType.Recommended => "موصى به",
        UpdateType.Mandatory   => "إجباري",
        UpdateType.Security    => "أمني 🔒",
        _                      => "اختياري"
    };

    /// <summary>لون شارة نوع التحديث (Hex) لعرضه في الواجهة</summary>
    [JsonIgnore]
    public string UpdateTypeBadgeColor => UpdateType switch
    {
        UpdateType.Optional    => "#3A7BD5",
        UpdateType.Recommended => "#27AE60",
        UpdateType.Mandatory   => "#E94560",
        UpdateType.Security    => "#E67E22",
        _                      => "#3A7BD5"
    };

    /// <summary>
    /// هل هذا الإصدار أحدث من النسخة الحالية للبرنامج؟
    /// </summary>
    public bool IsNewerThan(Version current)
        => System.Version.TryParse(Version, out var remote) && remote > current;
}
