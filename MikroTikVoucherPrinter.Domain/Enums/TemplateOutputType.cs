namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// نوع ورق / حجم مخرج القالب.
/// </summary>
public enum TemplateOutputType
{
    /// <summary>ورق A4 قياسي (210×297 mm)</summary>
    A4 = 0,

    /// <summary>ورق A5 (148×210 mm)</summary>
    A5 = 1,

    /// <summary>طابعة حرارية عرض 58mm</summary>
    Thermal58 = 2,

    /// <summary>طابعة حرارية عرض 80mm</summary>
    Thermal80 = 3,

    /// <summary>بطاقة قياسية بحجم CR80 (85.6×54 mm)</summary>
    Card85x54 = 4,
}
