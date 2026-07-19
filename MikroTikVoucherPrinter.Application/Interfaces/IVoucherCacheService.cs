using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IVoucherCacheService
{
    /// <summary>
    /// استرجاع الكروت المخزنة مؤقتاً لراوتر معين.
    /// </summary>
    Task<IReadOnlyList<VoucherDto>?> GetCachedVouchersAsync(string routerHost, CancellationToken cancellationToken = default);

    /// <summary>
    /// حفظ الكروت لراوتر معين وتحديث طابع الوقت.
    /// </summary>
    Task SetCachedVouchersAsync(string routerHost, IReadOnlyList<VoucherDto> vouchers, CancellationToken cancellationToken = default);

    /// <summary>
    /// التحقق مما إذا كان الكاش صالحاً للراوتر الحالي.
    /// </summary>
    Task<bool> IsCacheValidAsync(string routerHost, CancellationToken cancellationToken = default);

    /// <summary>
    /// مسح الكاش يدوياً وإجبار النظام على تحديث البيانات.
    /// </summary>
    Task ClearCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// جلب البيانات إما من الكاش إذا كان صالحاً، أو استخدام دالة الجلب الممررة مع القفل لمنع الجلب المتوازي.
    /// </summary>
    Task<IReadOnlyList<VoucherDto>> FetchOrGetCachedAsync(
        string routerHost, 
        Func<CancellationToken, Task<IReadOnlyList<VoucherDto>>> fetchFunc, 
        bool forceRefresh = false, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// نقطة توسعة لتحديث الكاش جزئياً (Delta Update) في المستقبل دون إعادة تحميل كامل الكروت.
    /// </summary>
    Task UpdateCacheDeltaAsync(string routerHost, IReadOnlyList<VoucherDto> deltaChanges, CancellationToken cancellationToken = default);
}
