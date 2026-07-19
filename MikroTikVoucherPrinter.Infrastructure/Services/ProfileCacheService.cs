using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class ProfileCacheService : IProfileCacheService
{
    private readonly ISettingsService _settingsService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly Dictionary<string, (List<Profile> Profiles, DateTime Timestamp)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ProfileCacheService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private TimeSpan GetCacheExpiryDuration()
    {
        var secondsStr = _settingsService.Get("ProfileCacheDurationSeconds", "60");
        if (int.TryParse(secondsStr, out int seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }
        return TimeSpan.FromSeconds(60);
    }

    private string GetCacheKey(string routerHost, PackageSourceType sourceType)
    {
        return $"{routerHost}||{sourceType}";
    }

    public async Task<IReadOnlyList<Profile>?> GetCachedProfilesAsync(string routerHost, PackageSourceType sourceType, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var key = GetCacheKey(routerHost, sourceType);
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached.Profiles;
            }
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetCachedProfilesAsync(string routerHost, PackageSourceType sourceType, IReadOnlyList<Profile> profiles, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var key = GetCacheKey(routerHost, sourceType);
            _cache[key] = (profiles.ToList(), DateTime.UtcNow);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> IsCacheValidAsync(string routerHost, PackageSourceType sourceType, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var key = GetCacheKey(routerHost, sourceType);
            if (!_cache.TryGetValue(key, out var cached))
            {
                return false;
            }

            var expiry = GetCacheExpiryDuration();
            return (DateTime.UtcNow - cached.Timestamp) < expiry;
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
            _cache.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<Profile>> FetchOrGetCachedAsync(
        string routerHost, 
        PackageSourceType sourceType,
        Func<CancellationToken, Task<IReadOnlyList<Profile>>> fetchFunc, 
        bool forceRefresh = false, 
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var key = GetCacheKey(routerHost, sourceType);
            var hasCache = _cache.TryGetValue(key, out var cached);
            var isCacheValid = hasCache && (DateTime.UtcNow - cached.Timestamp) < GetCacheExpiryDuration();

            if (!forceRefresh && isCacheValid)
            {
                return cached.Profiles;
            }

            var freshData = await fetchFunc(cancellationToken);
            _cache[key] = (freshData.ToList(), DateTime.UtcNow);
            return freshData;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
