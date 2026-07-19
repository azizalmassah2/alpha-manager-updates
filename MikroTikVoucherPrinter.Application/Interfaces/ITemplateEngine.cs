using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// محرك الطباعة الجديد — يحول بيانات الكروت + قالب LuxTemplate إلى مستند قابل للطباعة.
/// مستقل تماماً عن <see cref="IPrintService"/> القديم.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// يحل جميع FieldTokens لكرت واحد إلى قيم نصية حقيقية.
    /// يجمع بيانات الكرت (VoucherDto) مع سياق الطباعة (PrintContextDto).
    /// </summary>
    /// <param name="voucher">بيانات الكرت</param>
    /// <param name="context">سياق الطباعة (شبكة، وكيل، تاريخ...)</param>
    /// <param name="cardIndex">ترتيب الكرت في الدفعة (يبدأ من 1)</param>
    Dictionary<FieldToken, string> ResolveVoucherData(
        DTOs.VoucherDto voucher,
        DTOs.PrintContextDto context,
        int cardIndex = 1);

    /// <summary>
    /// يُحوِّل قالب + قائمة كروت إلى PDF bytes.
    /// يعيد ملف PDF جاهزاً للطباعة أو الحفظ.
    /// </summary>
    Task<byte[]> RenderToPdfAsync(
        DTOs.LuxTemplateDetailDto template,
        IReadOnlyList<DTOs.VoucherDto> vouchers,
        DTOs.PrintContextDto context,
        CancellationToken ct = default);

    /// <summary>
    /// يُولِّد صورة PNG لمعاينة القالب بكرت واحد تجريبي.
    /// إذا كانت sampleData فارغة، يستخدم قيماً تجريبية افتراضية.
    /// </summary>
    Task<byte[]> RenderPreviewAsync(
        DTOs.LuxTemplateDetailDto template,
        Dictionary<FieldToken, string>? sampleData = null,
        CancellationToken ct = default);
}
