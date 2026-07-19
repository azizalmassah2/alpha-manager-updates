namespace MikroTikVoucherPrinter.Application.DTOs;

public enum PaperType { A4, Thermal58, Thermal80 }

/// <summary>
/// بيانات إعدادات الطباعة للنسخة التجارية (Commercial)
/// </summary>
public class PrintSettingsDto
{
    public PaperType PaperType { get; set; } = PaperType.A4;
    public string PrinterName { get; set; } = string.Empty;
    public string TemplateName { get; set; } = "HawaeGridDefault"; // System selects IPrintTemplate based on this
    
    // إذا كان القالب مخصصاً من قاعدة البيانات
    public Guid? CustomTemplateId { get; set; }

    // A4 Settings
    public int CardsPerRow { get; set; } = 4;
    public int CardsPerColumn { get; set; } = 5;
    
    // Thermal Calibration (DPI & MM)
    public int ThermalDpi { get; set; } = 203; // Standard standard: 203 or 300
    public double PrintableWidthMm { get; set; } = 48; // الورق 58 مساحته للطباعة 48 عادة
    
    // Generic Measurements
    public double CardHeight { get; set; } = 150;
    public double Padding { get; set; } = 10;
    public int FontSize { get; set; } = 12;

    // QR & Logo Optimization
    public bool ShowQrCode { get; set; } = true;
    public string QrBaseUrl { get; set; } = "http://hotspot.local/login";
    public bool UseTokenForQr { get; set; } = false; // Example: ?token=xyz instead of username/password variables
    public string CompanyName { get; set; } = "لوكس كارد للشبكات";
    public string CompanyLogoPath { get; set; } = string.Empty; // Absolute path to cache
    public string FooterText { get; set; } = "يرجى الاحتفاظ بالكرت";

    // ضغط الملف الناتج (تقليل جودة صورة الخلفية لتقليص حجم PDF)
    public bool CompressOutput { get; set; } = false;
    public int ImageQuality { get; set; } = 40; // جودة JPEG عند الضغط (0-100)
    public int MaxImageSidePx { get; set; } = 400; // الحد الأقصى لأبعاد الصورة عند الضغط
}
