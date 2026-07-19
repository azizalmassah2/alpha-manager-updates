using System.Text.Json.Serialization;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Domain.Models.TemplateElements;

/// <summary>
/// الكلاس الأساسي لجميع عناصر القالب.
/// يُخزَّن كـ JSON داخل LuxTemplate.ElementsJson.
/// يستخدم System.Text.Json Polymorphism مع $type discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextElement),         typeDiscriminator: "Text")]
[JsonDerivedType(typeof(DynamicFieldElement), typeDiscriminator: "DynamicField")]
[JsonDerivedType(typeof(ImageElement),        typeDiscriminator: "Image")]
[JsonDerivedType(typeof(QrCodeElement),       typeDiscriminator: "QrCode")]
[JsonDerivedType(typeof(BarcodeElement),      typeDiscriminator: "Barcode")]
[JsonDerivedType(typeof(ShapeElement),        typeDiscriminator: "Shape")]
[JsonDerivedType(typeof(LineElement),         typeDiscriminator: "Line")]
public abstract class TemplateElement
{
    /// <summary>معرف فريد للعنصر داخل القالب</summary>
    public Guid ElementId { get; set; } = Guid.NewGuid();

    /// <summary>اسم العنصر في محرر القالب (اختياري، للتوضيح فقط)</summary>
    public string? Name { get; set; }

    // ══ الموضع والأبعاد (بالملليمتر، نسبة لزاوية الكرت العليا اليسرى) ══

    /// <summary>المسافة من الحافة اليسرى للكرت (mm)</summary>
    public float X { get; set; }

    /// <summary>المسافة من الحافة العلوية للكرت (mm)</summary>
    public float Y { get; set; }

    /// <summary>عرض العنصر (mm)</summary>
    public float Width { get; set; }

    /// <summary>ارتفاع العنصر (mm)</summary>
    public float Height { get; set; }

    // ══ خصائص الظهور ══

    /// <summary>درجة الدوران (0-360)</summary>
    public float Rotation { get; set; } = 0f;

    /// <summary>الشفافية (0.0 شفاف كامل — 1.0 معتم كامل)</summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>هل العنصر مرئي؟</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>هل العنصر مقفل (لا يمكن تعديله في المحرر)؟</summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>ترتيب العنصر في ترتيب الرسم (الأعلى يُرسم فوق الأقل)</summary>
    public int ZIndex { get; set; } = 0;
}
