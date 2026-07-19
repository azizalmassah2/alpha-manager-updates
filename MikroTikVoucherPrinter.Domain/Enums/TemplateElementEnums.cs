namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// نوع عنصر القالب — يُستخدم كـ discriminator في JSON Polymorphism.
/// </summary>
public enum TemplateElementType
{
    Text = 0,
    DynamicField = 1,
    Image = 2,
    QrCode = 3,
    Barcode = 4,
    Shape = 5,
    Line = 6,
}

/// <summary>اتجاه الصفحة</summary>
public enum TemplateOrientation
{
    Portrait = 0,
    Landscape = 1,
}

/// <summary>نوع خلفية القالب</summary>
public enum TemplateBackgroundType
{
    Solid = 0,
    Image = 1,
    Gradient = 2,
}

/// <summary>وضع تحجيم الصورة داخل العنصر</summary>
public enum ImageScaleMode
{
    Stretch = 0,
    Fit = 1,
    Fill = 2,
    None = 3,
}

/// <summary>نوع شكل ShapeElement</summary>
public enum ShapeType
{
    Rectangle = 0,
    Ellipse = 1,
    Triangle = 2,
    RoundedRectangle = 3,
}

/// <summary>نمط خط LineElement</summary>
public enum LineStyle
{
    Solid = 0,
    Dashed = 1,
    Dotted = 2,
}

/// <summary>نوع الباركود</summary>
public enum BarcodeType
{
    Code128 = 0,
    Code39 = 1,
    Ean13 = 2,
}

/// <summary>مستوى تصحيح خطأ QR Code</summary>
public enum QrErrorCorrection
{
    Low = 0,
    Medium = 1,
    Quartile = 2,
    High = 3,
}

/// <summary>محاذاة نص أفقية</summary>
public enum TextHorizontalAlignment
{
    Left = 0,
    Center = 1,
    Right = 2,
}
