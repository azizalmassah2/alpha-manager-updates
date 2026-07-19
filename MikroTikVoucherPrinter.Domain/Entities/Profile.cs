using System;
using MikroTikVoucherPrinter.Domain.Common;

namespace MikroTikVoucherPrinter.Domain.Entities;

/// <summary>
/// باقة الإنترنت — يتم جلبها من المايكروتيك مباشرة ويُحفظ نسخة محلية كـ Cache للعرض عند الأوف لاين
/// </summary>
public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string? MikroTikProfileId { get; set; }
    public string Duration { get; set; } = string.Empty;   // مثال: 30d, 1h
    public string RateLimit { get; set; } = string.Empty;  // مثال: 2M/2M
    public decimal Price { get; set; }

    public string Transfer { get; set; } = string.Empty;   // مثال: 1G, 500M
    public string Uptime { get; set; } = string.Empty;     // مثال: 1d6h
    public string SharedUsers { get; set; } = "1";

    // بيانات الكاش
    public string RouterHost { get; set; } = string.Empty; // لتمييز البيانات لو كان هناك أكثر من راوتر
    public Guid RouterId { get; set; }
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
    public bool IsFromCache { get; set; } = false;          // يُضبط true عند القراءة من الكاش
    public string? SystemType { get; set; }

    // ربط بالقالب المخصص (اختياري)
    public Guid? TemplateId { get; set; }
    public virtual TemplateConfig? Template { get; set; }

    // خاصية مساعدة للـ UI
    public string DisplayName => string.IsNullOrEmpty(Duration)
        ? Name
        : $"{Name}  ({Duration})";
}
