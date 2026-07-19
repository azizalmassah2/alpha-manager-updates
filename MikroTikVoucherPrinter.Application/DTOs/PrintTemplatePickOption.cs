namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// عنصر قائمة اختيار قالب للطباعة (يشمل خيار &quot;افتراضي الباقة&quot; في شاشة التوليد).
/// </summary>
public sealed class PrintTemplatePickOption
{
    /// <summary>قيمة فارغة تعني &quot;استخدم TemplateId المرتبط بالباقة ثم الافتراضي النظامي&quot;.</summary>
    public Guid? TemplateId { get; init; }

    public bool IsProfileDefaultChoice { get; init; }

    public string Title { get; init; } = "";

    public string Subtitle { get; init; } = "";

    public string? ThumbnailPath { get; init; }

    public TemplateConfigDto? Source { get; init; }
}
