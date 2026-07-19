namespace MikroTikVoucherPrinter.Domain.Common;

/// <summary>
/// الكيان الأساسي - كل الكيانات ترث منه
/// تم تحديثه لاستخدام Guid لتسهيل دعم العمل Offline دون تضارب الـ IDs
/// وإضافة الـ Soft Delete
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Concurrency Token
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
