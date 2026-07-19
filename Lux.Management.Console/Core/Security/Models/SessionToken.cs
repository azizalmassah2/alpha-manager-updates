using System;

namespace Lux.Management.Console.Core.Security.Models;

/// <summary>
/// يمثل كائن رمز الجلسة المحلل والموقع لبيئة التشغيل — Value Object غير قابل للتعديل.
/// </summary>
public sealed record SessionToken(
    string RawToken,
    string RouterSerialNumber,
    bool IsPro,
    DateTime IssuedAt
);
