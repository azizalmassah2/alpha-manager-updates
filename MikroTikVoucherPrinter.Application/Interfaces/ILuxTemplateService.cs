using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// خدمة إدارة القوالب الجديدة (LuxTemplate Engine).
/// موازية لـ <see cref="ITemplateService"/> ولا تستبدلها.
/// </summary>
public interface ILuxTemplateService
{
    // ══ عمليات القراءة ══

    /// <summary>جلب جميع القوالب المتاحة للراوتر النشط</summary>
    Task<IReadOnlyList<DTOs.LuxTemplateDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>جلب القوالب مصفاة حسب التصنيف</summary>
    Task<IReadOnlyList<DTOs.LuxTemplateDto>> GetByCategoryAsync(TemplateCategory category, CancellationToken ct = default);

    /// <summary>جلب تفاصيل قالب كامل (مع ElementsJson) بالمعرف</summary>
    Task<DTOs.LuxTemplateDetailDto?> GetDetailByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>جلب القالب الافتراضي لتصنيف معين</summary>
    Task<DTOs.LuxTemplateDto?> GetDefaultForCategoryAsync(TemplateCategory category, CancellationToken ct = default);

    // ══ عمليات الكتابة ══

    /// <summary>إنشاء قالب جديد</summary>
    Task<DTOs.LuxTemplateDto> CreateAsync(DTOs.LuxTemplateDetailDto template, CancellationToken ct = default);

    /// <summary>تحديث قالب موجود</summary>
    Task UpdateAsync(DTOs.LuxTemplateDetailDto template, CancellationToken ct = default);

    /// <summary>حذف قالب (Soft Delete)</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>نسخ قالب موجود باسم جديد</summary>
    Task<DTOs.LuxTemplateDto> DuplicateAsync(Guid id, string newName, CancellationToken ct = default);

    /// <summary>
    /// تعيين قالب كافتراضي لتصنيفه.
    /// يلغي الافتراضي القديم تلقائياً (Atomic في Transaction).
    /// </summary>
    Task SetDefaultAsync(Guid id, TemplateCategory category, CancellationToken ct = default);
}
