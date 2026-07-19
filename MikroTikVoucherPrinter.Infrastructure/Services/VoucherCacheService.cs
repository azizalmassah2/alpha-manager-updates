using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class VoucherCacheService : IVoucherCacheService
{
    private readonly ISettingsService _settingsService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private List<VoucherDto>? _cachedVouchers;
    private DateTime _cacheTimestamp = DateTime.MinValue;
    private string _cachedRouterHost = "";

    public VoucherCacheService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private TimeSpan GetCacheExpiryDuration()
    {
        var secondsStr = _settingsService.Get("VoucherCacheDurationSeconds", "60");
        if (int.TryParse(secondsStr, out int seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }
        return TimeSpan.FromSeconds(60);
    }

    public async Task<IReadOnlyList<VoucherDto>?> GetCachedVouchersAsync(string routerHost, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (string.Equals(_cachedRouterHost, routerHost, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedVouchers;
            }
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetCachedVouchersAsync(string routerHost, IReadOnlyList<VoucherDto> vouchers, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _cachedVouchers = vouchers.ToList();
            _cachedRouterHost = routerHost;
            _cacheTimestamp = DateTime.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> IsCacheValidAsync(string routerHost, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!string.Equals(_cachedRouterHost, routerHost, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_cachedVouchers == null)
            {
                return false;
            }

            var expiry = GetCacheExpiryDuration();
            return (DateTime.UtcNow - _cacheTimestamp) < expiry;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _cachedVouchers = null;
            _cachedRouterHost = "";
            _cacheTimestamp = DateTime.MinValue;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<VoucherDto>> FetchOrGetCachedAsync(
        string routerHost, 
        Func<CancellationToken, Task<IReadOnlyList<VoucherDto>>> fetchFunc, 
        bool forceRefresh = false, 
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var isHostMatch = string.Equals(_cachedRouterHost, routerHost, StringComparison.OrdinalIgnoreCase);
            var isCacheValid = _cachedVouchers != null && (DateTime.UtcNow - _cacheTimestamp) < GetCacheExpiryDuration();

            if (!forceRefresh && isHostMatch && isCacheValid)
            {
                return _cachedVouchers!;
            }

            // Fetch fresh vouchers
            var freshData = await fetchFunc(cancellationToken);

            // Store in cache
            _cachedVouchers = freshData.ToList();
            _cachedRouterHost = routerHost;
            _cacheTimestamp = DateTime.UtcNow;

            return _cachedVouchers;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateCacheDeltaAsync(string routerHost, IReadOnlyList<VoucherDto> deltaChanges, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!string.Equals(_cachedRouterHost, routerHost, StringComparison.OrdinalIgnoreCase) || _cachedVouchers == null)
            {
                return;
            }

            var cacheDict = _cachedVouchers.ToDictionary(v => v.Username, StringComparer.OrdinalIgnoreCase);

            foreach (var change in deltaChanges)
            {
                if (change.IsDeleted)
                {
                    cacheDict.Remove(change.Username);
                }
                else
                {
                    cacheDict[change.Username] = change;
                }
            }

            _cachedVouchers = cacheDict.Values.OrderByDescending(v => v.CreatedAt).ThenBy(v => v.Username).ToList();
            _cacheTimestamp = DateTime.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}