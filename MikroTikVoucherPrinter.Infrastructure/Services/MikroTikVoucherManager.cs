using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lux.MikroTik.Interfaces;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;
using Lux.MikroTik.Exceptions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using Polly;
using Polly.CircuitBreaker;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class MikroTikVoucherManager : IMikroTikVoucherManager
{
    private readonly IRouterOsProvider _provider;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MikroTikVoucherManager> _logger;

    private static readonly AsyncCircuitBreakerPolicy _circuitBreaker = Policy
        .Handle<MikroTikConnectionException>()
        .Or<OperationCanceledException>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 3, 
            durationOfBreak: TimeSpan.FromSeconds(15)
        );

    public MikroTikVoucherManager(
        IRouterOsProvider provider,
        ISettingsService settingsService,
        ILogger<MikroTikVoucherManager> logger)
    {
        _provider = provider;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<Result<MikroTikUserResult>> CreateUserAsync(string username, string? password, string profileName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                var host = _settingsService.Get("MikroTik.Host", "192.168.88.1");
                var user = _settingsService.Get("MikroTik.Username", "admin");
                var pass = _settingsService.Get("MikroTik.Password", "");

                try
                {
                    bool isHotspot = false;
                    bool exists = false;
                    string id = "";
                    IReadOnlyDictionary<string, string>? rawUser = null;

                    // Check if User Manager is available
                    try
                    {
                        var printCmd = new MikroTikCommand
                        {
                            Command = "/tool/user-manager/user/print",
                            Arguments = new[] { "username", username }
                        };
                        var existing = await _provider.ExecuteAsync(printCmd);
                        if (existing.IsSuccess && existing.Value.RawData != null && existing.Value.RawData.Any())
                        {
                            exists = true;
                            rawUser = existing.Value.RawData.First();
                            if (rawUser.TryGetValue(".id", out var val))
                            {
                                id = val;
                            }
                        }
                    }
                    catch (MikroTikCommandException)
                    {
                        // Fallback to Hotspot
                        isHotspot = true;
                        var printCmd = new MikroTikCommand
                        {
                            Command = "/ip/hotspot/user/print",
                            Arguments = new[] { "name", username }
                        };
                        var existing = await _provider.ExecuteAsync(printCmd);
                        if (existing.IsSuccess && existing.Value.RawData != null && existing.Value.RawData.Any())
                        {
                            exists = true;
                            rawUser = existing.Value.RawData.First();
                            if (rawUser.TryGetValue(".id", out var val))
                            {
                                id = val;
                            }
                        }
                    }

                    if (exists && rawUser != null)
                    {
                        bool disabled = false;
                        if (rawUser.TryGetValue("disabled", out var disabledVal))
                        {
                            disabled = disabledVal.Equals("true", StringComparison.OrdinalIgnoreCase) || disabledVal.Equals("yes", StringComparison.OrdinalIgnoreCase);
                        }

                        string profile = "";
                        if (rawUser.TryGetValue("profile", out var profVal)) profile = profVal;
                        else if (rawUser.TryGetValue("actual-profile", out var actProfVal)) profile = actProfVal;

                        _logger.LogInformation("Idempotency: User {Username} already exists on MikroTik.", username);
                        return Result<MikroTikUserResult>.Success(new MikroTikUserResult
                        {
                            Id = id,
                            Username = username,
                            WasAlreadyPresent = true,
                            ProfileName = profile,
                            IsDisabled = disabled
                        });
                    }

                    if (isHotspot)
                    {
                        var args = new List<string> { "name", username, "profile", profileName, "server", "all" };
                        if (password != null)
                        {
                            args.Add("password");
                            args.Add(password);
                        }

                        var addCmd = new MikroTikCommand
                        {
                            Command = "/ip/hotspot/user/add",
                            Arguments = args.ToArray()
                        };
                        await _provider.ExecuteAsync(addCmd);
                    }
                    else
                    {
                        // Try RouterOS 7 first
                        try
                        {
                            var argsV7 = new List<string> { "username", username, "owner", user };
                            if (password != null)
                            {
                                argsV7.Add("password");
                                argsV7.Add(password);
                            }

                            var addCmdV7 = new MikroTikCommand
                            {
                                Command = "/tool/user-manager/user/add",
                                Arguments = argsV7.ToArray()
                            };
                            await _provider.ExecuteAsync(addCmdV7);

                            try
                            {
                                var profileCmdV7 = new MikroTikCommand
                                {
                                    Command = "/tool/user-manager/user/create-and-activate-profile",
                                    Arguments = new[]
                                    {
                                        "customer", user,
                                        "profile", profileName,
                                        "user", username
                                    }
                                };
                                await _provider.ExecuteAsync(profileCmdV7);
                            }
                            catch (Exception profileEx)
                            {
                                _logger.LogWarning(profileEx, "⚠️ create-and-activate-profile failed, trying to assign group directly...");
                                try
                                {
                                    var setCmdV7 = new MikroTikCommand
                                    {
                                        Command = "/tool/user-manager/user/set",
                                        Arguments = new[]
                                        {
                                            "numbers", username,
                                            "group", profileName
                                        }
                                    };
                                    await _provider.ExecuteAsync(setCmdV7);
                                }
                                catch { /* Created user without profile, will assign later */ }
                            }
                        }
                        catch (MikroTikCommandException v7ex)
                        {
                            _logger.LogInformation("ℹ️ RouterOS 7 syntax failed ({Msg}), trying RouterOS 6...", v7ex.Message);

                            var argsV6 = new List<string> { "customer", user, "username", username };
                            if (password != null)
                            {
                                argsV6.Add("password");
                                argsV6.Add(password);
                            }

                            var addCmdV6 = new MikroTikCommand
                            {
                                Command = "/tool/user-manager/user/add",
                                Arguments = argsV6.ToArray()
                            };
                            await _provider.ExecuteAsync(addCmdV6);

                            try
                            {
                                var profileCmdV6 = new MikroTikCommand
                                {
                                    Command = "/tool/user-manager/user/create-and-activate-profile",
                                    Arguments = new[]
                                    {
                                        "customer", user,
                                        "profile", profileName,
                                        "user", username
                                    }
                                };
                                await _provider.ExecuteAsync(profileCmdV6);
                            }
                            catch (Exception profileV6Ex)
                            {
                                _logger.LogWarning(profileV6Ex, "⚠️ Activating profile in v6 failed too.");
                            }
                        }
                    }

                    // Query new ID
                    string newId = $"generated_{Guid.NewGuid()}";
                    var queryCmd = isHotspot 
                        ? new MikroTikCommand { Command = "/ip/hotspot/user/print", Arguments = new[] { "name", username } }
                        : new MikroTikCommand { Command = "/tool/user-manager/user/print", Arguments = new[] { "username", username } };
                    
                    var queryResult = await _provider.ExecuteAsync(queryCmd);
                    if (queryResult.IsSuccess && queryResult.Value.RawData != null && queryResult.Value.RawData.Any())
                    {
                        if (queryResult.Value.RawData.First().TryGetValue(".id", out var val2))
                        {
                            newId = val2;
                        }
                    }

                    _logger.LogInformation("🔥 [Success] Created and activated user {Username} successfully.", username);
                    return Result<MikroTikUserResult>.Success(new MikroTikUserResult
                    {
                        Id = newId,
                        Username = username,
                        WasAlreadyPresent = false
                    });
                }
                finally
                {
                    // Connection is managed globally
                }
            });
        }
        catch (BrokenCircuitException)
        {
            _logger.LogCritical("🚫 [Circuit Breaker] Circuit breaker active! Requests blocked temporarily.");
            return Result<MikroTikUserResult>.Failure("System is temporarily rate-limiting calls to MikroTik due to repeated timeouts.", ErrorType.ExternalService);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⚠️ [Timeout] Connection to MikroTik timed out for user {Username}.", username);
            return Result<MikroTikUserResult>.Failure("Network Timeout - No response from MikroTik", ErrorType.ExternalService);
        }
        catch (MikroTikCommandException ex)
        {
            _logger.LogError(ex, "❌ [MikroTik Logic Error] RouterOS rejected the command.");
            return Result<MikroTikUserResult>.Failure($"RouterOS rejected operation: {ex.Message}", ErrorType.Conflict);
        }
        catch (MikroTikConnectionException ex)
        {
            _logger.LogError(ex, "❌ [Network Failure] Physical connection failed.");
            return Result<MikroTikUserResult>.Failure($"Network connection failure: {ex.Message}", ErrorType.ExternalService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [Fatal Error] Unexpected error during connection.");
            return Result<MikroTikUserResult>.Failure($"Unexpected error: {ex.Message}", ErrorType.ExternalService);
        }
    }

    public async Task<Dictionary<string, Result<MikroTikUserResult>>> CreateUsersBulkAsync(
        IEnumerable<(string username, string? password, string profileName)> users,
        IProgress<(int success, int failed, int total)>? progress = null,
        int initialSuccess = 0,
        int initialFailed = 0,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, Result<MikroTikUserResult>>();
        int total = users.Count() + initialSuccess + initialFailed;
        int success = initialSuccess;
        int failed = initialFailed;

        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                var host = _settingsService.Get("MikroTik.Host", "192.168.88.1");
                var adminUser = _settingsService.Get("MikroTik.Username", "admin");
                var pass = _settingsService.Get("MikroTik.Password", "");

                try
                {
                    bool isHotspot = false;
                    bool isV7 = false;
                    bool? useOwner = null;
                    string[]? cachedProfileCommand = null;

                    // 1. Determine Environment Once
                    try
                    {
                        var resCommand = new MikroTikCommand { Command = "/system/resource/print" };
                        var res = await _provider.ExecuteAsync(resCommand);
                        if (res.IsSuccess && res.Value.RawData != null && res.Value.RawData.Any())
                        {
                            var dict = res.Value.RawData.First();
                            if (dict.TryGetValue("version", out var vWord) && vWord.StartsWith("7."))
                                isV7 = true;
                        }
                    }
                    catch { }

                    try
                    {
                        string testCmd = isV7 ? "/user-manager/user/print" : "/tool/user-manager/user/print";
                        var checkCmd = new MikroTikCommand { Command = testCmd, Arguments = new[] { "username", "dummy_check_123" } };
                        await _provider.ExecuteAsync(checkCmd);
                        isHotspot = false;
                    }
                    catch
                    {
                        isHotspot = true;
                    }

                    foreach (var u in users)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            if (isHotspot)
                            {
                                var args = new List<string> { "name", u.username, "profile", u.profileName, "server", "all" };
                                if (!string.IsNullOrEmpty(u.password)) { args.Add("password"); args.Add(u.password); }
                                
                                var addCmd = new MikroTikCommand { Command = "/ip/hotspot/user/add", Arguments = args.ToArray() };
                                await _provider.ExecuteAsync(addCmd);
                            }
                            else
                            {
                                if (isV7)
                                {
                                    var args = new List<string> { "name", u.username, "group", u.profileName };
                                    if (!string.IsNullOrEmpty(u.password)) { args.Add("password"); args.Add(u.password); }
                                    
                                    var addCmdV7 = new MikroTikCommand { Command = "/user-manager/user/add", Arguments = args.ToArray() };
                                    await _provider.ExecuteAsync(addCmdV7);
                                }
                                else
                                {
                                    bool added = false;
                                    if (useOwner == null || useOwner == true)
                                    {
                                        try
                                        {
                                            var argsV6Owner = new List<string> { "username", u.username, "owner", adminUser };
                                            if (!string.IsNullOrEmpty(u.password)) { argsV6Owner.Add("password"); argsV6Owner.Add(u.password); }
                                            
                                            var addCmdV6Owner = new MikroTikCommand { Command = "/tool/user-manager/user/add", Arguments = argsV6Owner.ToArray() };
                                            await _provider.ExecuteAsync(addCmdV6Owner);
                                            useOwner = true;
                                            added = true;
                                        }
                                        catch (MikroTikCommandException ex)
                                        {
                                            if (ex.Message.Contains("already")) throw;
                                            useOwner = false;
                                        }
                                    }
                                    
                                    if (!added && useOwner == false)
                                    {
                                        var argsV6Cust = new List<string> { "username", u.username, "customer", adminUser };
                                        if (!string.IsNullOrEmpty(u.password)) { argsV6Cust.Add("password"); argsV6Cust.Add(u.password); }
                                        
                                        var addCmdV6Cust = new MikroTikCommand { Command = "/tool/user-manager/user/add", Arguments = argsV6Cust.ToArray() };
                                        await _provider.ExecuteAsync(addCmdV6Cust);
                                    }

                                    // Profile Assignment for V6
                                    if (cachedProfileCommand != null)
                                    {
                                        var args = cachedProfileCommand.ToArray();
                                        for (int i = 0; i < args.Length; i++)
                                        {
                                            if (args[i] == "{user}") args[i] = u.username;
                                            if (args[i] == "{profile}") args[i] = u.profileName;
                                        }
                                        try
                                        {
                                            var cachedCmd = new MikroTikCommand { Command = args[0], Arguments = args.Skip(1).ToArray() };
                                            await _provider.ExecuteAsync(cachedCmd);
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        var commands = new List<string[]>
                                        {
                                            new[] { "/tool/user-manager/user/create-and-activate-profile", "numbers", u.username, "profile", u.profileName, "customer", adminUser },
                                            new[] { "/tool/user-manager/user/create-and-activate-profile", "user", u.username, "profile", u.profileName, "customer", adminUser },
                                            new[] { "/tool/user-manager/user/create-and-activate-profile", "numbers", u.username, "profile", u.profileName, "owner", adminUser },
                                            new[] { "/tool/user-manager/user/create-and-activate-profile", "user", u.username, "profile", u.profileName, "owner", adminUser },
                                            new[] { "/tool/user-manager/user/set", "numbers", u.username, "group", u.profileName }
                                        };

                                        foreach (var cmdArgs in commands)
                                        {
                                            try
                                            {
                                                var tryCmd = new MikroTikCommand { Command = cmdArgs[0], Arguments = cmdArgs.Skip(1).ToArray() };
                                                await _provider.ExecuteAsync(tryCmd);
                                                cachedProfileCommand = cmdArgs.Select(a => a == u.username ? "{user}" : (a == u.profileName ? "{profile}" : a)).ToArray();
                                                break;
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }

                            results[u.username] = Result<MikroTikUserResult>.Success(new MikroTikUserResult { Id = $"generated_{Guid.NewGuid()}", Username = u.username, WasAlreadyPresent = false });
                            success++;
                        }
                        catch (MikroTikCommandException ex) when (ex.Message.Contains("already"))
                        {
                            results[u.username] = Result<MikroTikUserResult>.Success(new MikroTikUserResult { Id = $"existing_{Guid.NewGuid()}", Username = u.username, WasAlreadyPresent = true });
                            success++;
                        }
                        catch (Exception ex)
                        {
                            results[u.username] = Result<MikroTikUserResult>.Failure($"Failed: {ex.Message}", ErrorType.ExternalService);
                            failed++;
                        }

                        progress?.Report((success, failed, total));
                    }
                }
                finally
                {
                    // Connection is managed globally
                }
                
                return results;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Bulk execution failed.");
            foreach (var u in users)
            {
                if (!results.ContainsKey(u.username))
                {
                    results[u.username] = Result<MikroTikUserResult>.Failure($"General error: {ex.Message}", ErrorType.ExternalService);
                    failed++;
                }
            }
            progress?.Report((success, failed, total));
            return results;
        }
    }

    public async Task<Dictionary<string, Result>> DeleteUsersBulkAsync(
        IEnumerable<(string username, string? externalId)> users,
        IProgress<(int success, int failed, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var userList = users.ToList();
        var results = new Dictionary<string, Result>();
        int total = userList.Count;
        int success = 0;
        int failed = 0;

        if (total == 0) return results;

        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                // ── 1. كشف نوع الراوتر مرة واحدة فقط ──────────────────────────
                bool isHotspot = false;
                bool isV7     = false;

                try
                {
                    var resCmd = new MikroTikCommand { Command = "/system/resource/print" };
                    var res = await _provider.ExecuteAsync(resCmd);
                    if (res.IsSuccess && res.Value.RawData != null && res.Value.RawData.Any())
                        if (res.Value.RawData.First().TryGetValue("version", out var v) && v.StartsWith("7."))
                            isV7 = true;
                }
                catch (Exception resEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[DETECT-WARN] (Modern) /system/resource/print failed: {resEx.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                    _logger.LogWarning(resEx, "⚠️ /system/resource/print failed");
                }

                try
                {
                    string testCmd = isV7 ? "/user-manager/user/print" : "/tool/user-manager/user/print";
                    var test = await _provider.ExecuteAsync(new MikroTikCommand
                    {
                        Command = testCmd,
                        Arguments = new[] { "username", "dummy_check_123" }
                    });
                    isHotspot = !test.IsSuccess;
                }
                catch (Exception testEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[DETECT-WARN] (Modern) User Manager test command failed (Falling back to Hotspot): {testEx.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                    _logger.LogWarning(testEx, "⚠️ User Manager print test failed, using Hotspot fallback.");
                    isHotspot = true;
                }

                string queryCmdPath  = isHotspot ? "/ip/hotspot/user/print"  : (isV7 ? "/user-manager/user/print"  : "/tool/user-manager/user/print");
                string removeCmdPath = isHotspot ? "/ip/hotspot/user/remove" : (isV7 ? "/user-manager/user/remove" : "/tool/user-manager/user/remove");
                string filterProp    = isHotspot ? "name" : "username";

                // ── 2. تحديد الـ IDs: استخدم externalId المخزّن أولاً (أسرع)
                //       وللكروت بدون ID صالح، نجلب الـ ID من الراوتر بـ query منفردة
                var toDelete = new List<(string username, string internalId)>();

                foreach (var u in userList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string? internalId = u.externalId;
                    if (!string.IsNullOrEmpty(internalId) && internalId.StartsWith("*"))
                    {
                        toDelete.Add((u.username, internalId));
                        continue;
                    }

                    // fallback: نجلب الـ ID من الراوتر
                    try
                    {
                        var queryCmd = new MikroTikCommand
                        {
                            Command = queryCmdPath,
                            Arguments = new[] { filterProp, u.username }
                        };
                        var existing = await _provider.ExecuteAsync(queryCmd);
                        if (!existing.IsSuccess || existing.Value.RawData == null || !existing.Value.RawData.Any())
                        {
                            // غير موجود على الراوتر، يُعدّ ناجحاً (Idempotent)
                            results[u.username] = Result.Success();
                            success++;
                            progress?.Report((success, failed, total));
                            continue;
                        }

                        var first = existing.Value.RawData.First();
                        if (!first.TryGetValue(".id", out internalId) || string.IsNullOrEmpty(internalId))
                        {
                            results[u.username] = Result.Failure("فشل تحديد المعرف الداخلي على الراوتر.", ErrorType.Conflict);
                            failed++;
                            progress?.Report((success, failed, total));
                            continue;
                        }

                        toDelete.Add((u.username, internalId));
                    }
                    catch (Exception ex)
                    {
                        results[u.username] = Result.Failure($"خطأ أثناء البحث: {ex.Message}", ErrorType.ExternalService);
                        failed++;
                        progress?.Report((success, failed, total));
                    }
                }

                // ── 3. حذف كل الكروت الموجودة دفعة واحدة (IDs مجمّعة) ───────
                //       المايكروتك يدعم "numbers=*1,*2,*3" — أسرع من حذف فردي
                const int batchSize = 50; // حدّ آمن لطول الأمر
                var batches = toDelete
                    .Select((item, idx) => (item, idx))
                    .GroupBy(x => x.idx / batchSize)
                    .Select(g => g.Select(x => x.item).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var ids = string.Join(",", batch.Select(b => b.internalId));
                    try
                    {
                        var removeCmd = new MikroTikCommand
                        {
                            Command = removeCmdPath,
                            Arguments = new[] { "numbers", ids }
                        };
                        System.Diagnostics.Debug.WriteLine($"[DELETE-05] Router Request Sent (Modern) | Command: {removeCmdPath} | Parameters: numbers={ids} | Time: {DateTime.Now:HH:mm:ss.fff}");
                        var removeResult = await _provider.ExecuteAsync(removeCmd);
                        System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received (Modern) | Success: {removeResult.IsSuccess} | Error: {removeResult.ErrorMessage} | Time: {DateTime.Now:HH:mm:ss.fff}");

                        if (removeResult.IsSuccess)
                        {
                            foreach (var item in batch)
                            {
                                results[item.username] = Result.Success();
                                success++;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received (Modern Batch Failed, trying fallback) | Time: {DateTime.Now:HH:mm:ss.fff}");
                            // إذا فشل الـ batch، نحاول كل كرت بشكل منفرد كـ fallback
                            foreach (var item in batch)
                            {
                                try
                                {
                                    var singleRemove = new MikroTikCommand
                                    {
                                        Command = removeCmdPath,
                                        Arguments = new[] { "numbers", item.internalId }
                                    };
                                    System.Diagnostics.Debug.WriteLine($"[DELETE-05] Router Request Sent (Modern Fallback) | Command: {removeCmdPath} | Parameters: numbers={item.internalId} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                    var singleResult = await _provider.ExecuteAsync(singleRemove);
                                    System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received (Modern Fallback) | Success: {singleResult.IsSuccess} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                    
                                    if (singleResult.IsSuccess)
                                    {
                                        results[item.username] = Result.Success();
                                        success++;
                                    }
                                    else
                                    {
                                        if (singleResult.ErrorMessage != null && (singleResult.ErrorMessage.Contains("no such item") || singleResult.ErrorMessage.Contains("not found")))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[DELETE-WARN] Card '{item.username}' not found on router (already deleted). Treating as success. | Time: {DateTime.Now:HH:mm:ss.fff}");
                                            results[item.username] = Result.Success();
                                            success++;
                                        }
                                        else
                                        {
                                            // Requirement 6: Detailed failure reporting
                                            var routerIdSetting = _settingsService.Get("LastConnectedRouterId", "unknown");
                                            var fullDetail = $"[DELETE-FAIL-DETAIL-LIVE-MODERN] Username: '{item.username}' | RouterId: '{routerIdSetting}' | Command: '{removeCmdPath} numbers={item.internalId}' | Response: '{singleResult.ErrorMessage}'";
                                            System.Diagnostics.Debug.WriteLine(fullDetail);
                                            _logger.LogError(fullDetail);

                                            results[item.username] = Result.Failure($"فشل حذف {item.username}: {singleResult.ErrorMessage}", ErrorType.ExternalService);
                                            failed++;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (ex.Message.Contains("no such item") || ex.Message.Contains("not found"))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DELETE-WARN] Card '{item.username}' not found on router (already deleted). Treating as success. | Time: {DateTime.Now:HH:mm:ss.fff}");
                                        results[item.username] = Result.Success();
                                        success++;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received (Modern Fallback Error) | Success: False | Username: {item.username} | Error: {ex.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                        
                                        var routerIdSetting = _settingsService.Get("LastConnectedRouterId", "unknown");
                                        var fullDetail = $"[DELETE-FAIL-DETAIL-LIVE-MODERN] Username: '{item.username}' | RouterId: '{routerIdSetting}' | Command: '{removeCmdPath} numbers={item.internalId}' | Exception: '{ex.Message}'";
                                        System.Diagnostics.Debug.WriteLine(fullDetail);
                                        _logger.LogError(fullDetail);

                                        results[item.username] = Result.Failure($"خطأ: {ex.Message}", ErrorType.ExternalService);
                                        failed++;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received (Modern Batch Catch) | Success: False | Error: {ex.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                        foreach (var item in batch)
                        {
                            results[item.username] = Result.Failure($"خطأ في الـ Batch: {ex.Message}", ErrorType.ExternalService);
                            failed++;
                        }
                    }

                    progress?.Report((success, failed, total));
                }

                _logger.LogInformation("🗑️ [BulkDelete] انتهى: نجح {S} | فشل {F} من أصل {T}", success, failed, total);
                return results;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DELETE-ERROR] Exception inside Modern DeleteUsersBulkAsync: {ex} | Time: {DateTime.Now:HH:mm:ss.fff}");
            _logger.LogError(ex, "❌ [BulkDelete] خطأ عام أثناء الحذف الجماعي");
            foreach (var u in userList)
            {
                if (!results.ContainsKey(u.username))
                {
                    results[u.username] = Result.Failure($"خطأ عام: {ex.Message}", ErrorType.ExternalService);
                    failed++;
                }
            }
            progress?.Report((success, failed, total));
            throw; // إعادة رمي الاستثناء للتتبع ومنع الابتلاع
        }
    }

    public async Task<Result> DeleteUserAsync(string username, string? externalId = null, CancellationToken cancellationToken = default)

    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                var host = _settingsService.Get("MikroTik.Host", "192.168.88.1");
                var user = _settingsService.Get("MikroTik.Username", "admin");
                var pass = _settingsService.Get("MikroTik.Password", "");

                bool isHotspot = false;
                bool isV7 = false;

                // 1. Detect Router Type
                try
                {
                    var resCmd = new MikroTikCommand { Command = "/system/resource/print" };
                    var res = await _provider.ExecuteAsync(resCmd);
                    if (res.IsSuccess && res.Value.RawData != null && res.Value.RawData.Any())
                    {
                        if (res.Value.RawData.First().TryGetValue("version", out var vWord) && vWord.StartsWith("7."))
                            isV7 = true;
                    }
                }
                catch { }

                try
                {
                    string testCmd = isV7 ? "/user-manager/user/print" : "/tool/user-manager/user/print";
                    var test = await _provider.ExecuteAsync(new MikroTikCommand { Command = testCmd, Arguments = new[] { "username", "dummy_check_123" } });
                    isHotspot = !test.IsSuccess;
                }
                catch
                {
                    isHotspot = true;
                }

                string? internalId = externalId;
                if (string.IsNullOrEmpty(internalId) || !internalId.StartsWith("*"))
                {
                    // Fallback to query print
                    string queryCmdPath = isHotspot ? "/ip/hotspot/user/print" : (isV7 ? "/user-manager/user/print" : "/tool/user-manager/user/print");
                    string filterProp = isHotspot ? "name" : "username";

                    var queryCmd = new MikroTikCommand
                    {
                        Command = queryCmdPath,
                        Arguments = new[] { filterProp, username }
                    };

                    var existing = await _provider.ExecuteAsync(queryCmd);
                    if (!existing.IsSuccess || existing.Value.RawData == null || !existing.Value.RawData.Any())
                    {
                        _logger.LogInformation("✅ [DeleteUser] User {Username} does not exist on MikroTik (already deleted).", username);
                        return Result.Success();
                    }

                    if (existing.Value.RawData.Count > 1)
                    {
                        _logger.LogWarning("⚠️ [DeleteUser] Duplicate users found for {Username} on MikroTik. Aborting delete.", username);
                        return Result.Failure("تعارض في البيانات: يوجد أكثر من حساب بنفس الاسم على الراوتر.", ErrorType.Conflict);
                    }

                    var first = existing.Value.RawData.First();
                    if (!first.TryGetValue(".id", out internalId) || string.IsNullOrEmpty(internalId))
                    {
                        return Result.Failure("فشل في تحديد المعرف الداخلي للمستخدم على الراوتر.", ErrorType.Conflict);
                    }
                }

                // 3. Remove by internal ID
                string removeCmdPath = isHotspot ? "/ip/hotspot/user/remove" : (isV7 ? "/user-manager/user/remove" : "/tool/user-manager/user/remove");
                var removeCmd = new MikroTikCommand
                {
                    Command = removeCmdPath,
                    Arguments = new[] { "numbers", internalId }
                };
                var removeResult = await _provider.ExecuteAsync(removeCmd);

                if (!removeResult.IsSuccess)
                {
                    return Result.Failure($"فشل حذف المستخدم: {removeResult.ErrorMessage}", ErrorType.ExternalService);
                }

                _logger.LogInformation("🔥 [DeleteUser] Successfully deleted user {Username} (ID: {Id}) from MikroTik.", username, internalId);
                return Result.Success();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DeleteUser] Error deleting user {Username} from MikroTik.", username);
            return Result.Failure($"فشل في حذف المستخدم من الراوتر: {ex.Message}", ErrorType.ExternalService);
        }
    }
}
