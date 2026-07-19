using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// عنصر صورة ثابتة أو شعار ديناميكي.
/// إذا كان UseRouterLogo=true، يتم ربطه تلقائياً بشعار الراوتر النشط.
/// </summary>
public class ImageElement : TemplateElement
{
    /// <summary>
    /// مسار الصورة المطلق على الجهاز.
    /// يُتجاهل إذا كان UseRouterLogo=true.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>وضع تحجيم الصورة داخل مساحة العنصر</summary>
    public ImageScaleMode ScaleMode { get; set; } = ImageScaleMode.Fit;

    /// <summary>نصف قطر زوايا الصورة (mm) — 0 يعني زوايا حادة</summary>
    public float CornerRadius { get; set; } = 0f;

    /// <summary>
    /// إذا كانت true، يتم تجاهل ImagePath والاستبدال بشعار الراوتر النشط تلقائياً.
    /// مفيد لقوالب مشتركة بين رواترات متعددة.
    /// </summary>
    public bool UseRouterLogo { get; set; } = false;
}
