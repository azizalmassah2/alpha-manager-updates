namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// تصنيف نوع القالب للطباعة (ورقي / حراري / مخصص بالشبكة).
/// </summary>
public enum TemplateType
{
    A4 = 0,
    Thermal58 = 1,
    Thermal80 = 2,
    Custom = 3
}
