using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// طلب إنشاء دفعة جديدة — يُمرر من GenerateVoucherPage إلى IBatchService
/// </summary>
public class CreateBatchRequest
{
    // ─── بيانات الدفعة ──────────────────────────────────────
    /// <summary>اسم الدفعة المعروض للمستخدم</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>وصف اختياري</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>المستخدم أو القسم المنشئ</summary>
    public string CreatedBy { get; set; } = "Lux System";

    // ─── إعدادات التوليد ───────────────────────────────────
    /// <summary>طلب التوليد المفصل (عدد الكروت، الباقة، نوع الأحرف...)</summary>
    public VoucherGenerationRequest GenerationSettings { get; set; } = new();

    // ─── بعد التوليد ───────────────────────────────────────
    /// <summary>هل تُزامَن الدفعة تلقائياً بعد التوليد؟</summary>
    public bool AutoSync { get; set; }

    /// <summary>هل يُولَّد PDF تلقائياً بعد التوليد؟</summary>
    public bool AutoPrint { get; set; }

    /// <summary>معرف قالب الطباعة (اختياري)</summary>
    public Guid? PrintTemplateId { get; set; }
}
