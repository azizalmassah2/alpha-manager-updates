using System;

namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// يمثل لقطة أمان شاملة لحالة الجلسة والترخيص في لحظة معينة — غير قابل للتعديل.
/// </summary>
public sealed record SecuritySnapshot(
    Guid SessionId,
    bool IsAuthenticated,
    bool IsProMode,
    DateTime? Timestamp
);
