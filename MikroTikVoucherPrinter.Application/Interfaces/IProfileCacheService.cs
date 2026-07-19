using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IProfileCacheService
{
    /// <summary>
    /// استرجاع الباقات المخزنة مؤقتاً لراوتر معين ونوع معين.
    /// </summary>
    Task<IReadOnlyList<Profile>?> GetCachedProfilesAsync(string routerHost, PackageSourceType sourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// حفظ الباقات لراوتر معين ونوع معين وتحديث طابع الوقت.
    /// </summary>
    Task SetCachedProfilesAsync(string routerHost, PackageSourceType sourceType, IReadOnlyList<Profile> profiles, CancellationToken cancellationToken = default);

    /// <summary>
    /// التحقق مما إذا كان الكاش صالحاً للراوتر والنوع الحالي.
    /// </summary>
    Task<bool> IsCacheValidAsync(string routerHost, PackageSourceType sourceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// مسح الكاش يدوياً وإجبار النظام على تحديث البيانات.
    /// </summary>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// جلب البيانات إما من الكاش إذا كان صالحاً، أو استخدام دالة الجلب الممررة مع القفل لمنع الجلب المتوازي.
    /// </summary>
    Task<IReadOnlyList<Profile>> FetchOrGetCachedAsync(
        string routerHost, 
        PackageSourceType sourceType,
        Func<CancellationToken, Task<IReadOnlyList<Profile>>> fetchFunc, 
        bool forceRefresh = false, 
        CancellationToken cancellationToken = default);
}
