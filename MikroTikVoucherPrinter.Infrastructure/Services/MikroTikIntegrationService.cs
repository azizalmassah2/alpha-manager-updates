using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Polly;
    using Polly.CircuitBreaker;
    using tik4net;

    public class LegacyMikroTikIntegrationService : IMikroTikIntegrationService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<LegacyMikroTikIntegrationService> _logger;

        private AsyncCircuitBreakerPolicy _circuitBreaker;

        public LegacyMikroTikIntegrationService(ISettingsService settingsService, ILogger<LegacyMikroTikIntegrationService> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
            ResetCircuitBreaker();
        }

        public void ResetCircuitBreaker()
        {
            _circuitBreaker = Policy
                .Handle<TikConnectionException>()
                .Or<OperationCanceledException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(5)
                );
        }

        public async Task<Result<MikroTikUserResult>> CreateUserAsync(string username, string? password, string profileName, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _circuitBreaker.ExecuteAsync(async () =>
                {
                    return await Task.Run(() =>
                    {
                        using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                        var host = _settingsService.Get("MikroTik.Host", "192.168.88.1");
                        var user = _settingsService.Get("MikroTik.Username", "admin");
                        var pass = _settingsService.Get("MikroTik.Password", "");

                        connection.SendTimeout = 3000;
                        connection.ReceiveTimeout = 3000;
                        connection.Open(host, user, pass);

                        bool isHotspot = false;
                        IEnumerable<ITikSentence> existingUsers = null;
                        ITikCommand printCmd = null;

                        try
                        {
                            printCmd = connection.CreateCommandAndParameters("/tool/user-manager/user/print", "username", username);
                            existingUsers = printCmd.ExecuteList();
                        }
                        catch (TikCommandException)
                        {
                            isHotspot = true;
                            printCmd = connection.CreateCommandAndParameters("/ip/hotspot/user/print", "name", username);
                            existingUsers = printCmd.ExecuteList();
                        }

                        if (existingUsers != null && existingUsers.Any())
                        {
                            var existingUser = existingUsers.First();
                            string id = "";
                            if (existingUser.Words.TryGetValue(".id", out var val)) id = val;
                            
                            bool disabled = false;
                            if (existingUser.Words.TryGetValue("disabled", out var disabledVal))
                            {
                                disabled = disabledVal.Equals("true", StringComparison.OrdinalIgnoreCase) || disabledVal.Equals("yes", StringComparison.OrdinalIgnoreCase);
                            }

                            string profile = "";
                            if (existingUser.Words.TryGetValue("profile", out var profVal)) profile = profVal;
                            else if (existingUser.Words.TryGetValue("actual-profile", out var actProfVal)) profile = actProfVal;

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

                            var addCmd = connection.CreateCommandAndParameters("/ip/hotspot/user/add", args.ToArray());
                            addCmd.ExecuteNonQuery();
                        }
                        else
                        {
                            try
                            {
                                var argsV7 = new List<string> { "username", username, "owner", user };
                                if (password != null)
                                {
                                    argsV7.Add("password");
                                    argsV7.Add(password);
                                }

                                var addCmdV7 = connection.CreateCommandAndParameters("/tool/user-manager/user/add", argsV7.ToArray());
                                addCmdV7.ExecuteNonQuery();

                                try
                                {
                                    var profileCmdV7 = connection.CreateCommandAndParameters(
                                        "/tool/user-manager/user/create-and-activate-profile",
                                        "customer", user,
                                        "profile", profileName,
                                        "user", username);
                                    profileCmdV7.ExecuteNonQuery();
                                }
                                catch (Exception profileEx)
                                {
                                    _logger.LogWarning(profileEx, "create-and-activate-profile failed, trying fallback to group set...");
                                    try
                                    {
                                        var setCmdV7 = connection.CreateCommandAndParameters(
                                            "/tool/user-manager/user/set",
                                            "numbers", username,
                                            "group", profileName);
                                        setCmdV7.ExecuteNonQuery();
                                    }
                                    catch { }
                                }
                            }
                            catch (TikCommandException v7ex)
                            {
                                _logger.LogInformation("RouterOS 7 syntax failed ({Msg}), trying RouterOS 6...", v7ex.Message);

                                var argsV6 = new List<string> { "customer", user, "username", username };
                                if (password != null)
                                {
                                    argsV6.Add("password");
                                    argsV6.Add(password);
                                }

                                var addCmdV6 = connection.CreateCommandAndParameters("/tool/user-manager/user/add", argsV6.ToArray());
                                addCmdV6.ExecuteNonQuery();

                                try
                                {
                                    var profileCmdV6 = connection.CreateCommandAndParameters(
                                        "/tool/user-manager/user/create-and-activate-profile",
                                        "customer", user,
                                        "profile", profileName,
                                        "user", username);
                                    profileCmdV6.ExecuteNonQuery();
                                }
                                catch (Exception profileV6Ex)
                                {
                                    _logger.LogWarning(profileV6Ex, "Failed to activate profile in v6");
                                }
                            }
                        }

                        var newUsers = printCmd.ExecuteList();
                        string newId = $"generated_{Guid.NewGuid()}";
                        if (newUsers.Any())
                        {
                            if (newUsers.First().Words.TryGetValue(".id", out var val2)) newId = val2;
                        }

                        _logger.LogInformation("Success: User {Username} created and activated successfully.", username);

                        return Result<MikroTikUserResult>.Success(new MikroTikUserResult
                        {
                            Id = newId,
                            Username = username,
                            WasAlreadyPresent = false
                        });

                    }, cancellationToken);
                });
            }
            catch (BrokenCircuitException)
            {
                return Result<MikroTikUserResult>.Failure("System circuit breaker active", ErrorType.ExternalService);
            }
            catch (OperationCanceledException)
            {
                return Result<MikroTikUserResult>.Failure("Network Timeout", ErrorType.ExternalService);
            }
            catch (TikConnectionException ex)
            {
                _logger.LogError(ex, "Network Failure during MikroTik communication.");
                return Result<MikroTikUserResult>.Failure($"Network Failure: {ex.Message}", ErrorType.ExternalService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during MikroTik communication.");
                return Result<MikroTikUserResult>.Failure($"Unexpected error: {ex.Message}", ErrorType.ExternalService);
            }
        }

        private void AssignProfile(ITikConnection connection, string username, string profileName, string adminUser)
        {
            var commands = new List<string[]>
            {
                new[] { "/tool/user-manager/user/create-and-activate-profile", "numbers", username, "profile", profileName, "customer", adminUser },
                new[] { "/tool/user-manager/user/create-and-activate-profile", "user", username, "profile", profileName, "customer", adminUser },
                new[] { "/tool/user-manager/user/create-and-activate-profile", "numbers", username, "profile", profileName, "owner", adminUser },
                new[] { "/tool/user-manager/user/create-and-activate-profile", "user", username, "profile", profileName, "owner", adminUser },
                new[] { "/tool/user-manager/user/set", "numbers", username, "group", profileName }
            };

            foreach (var args in commands)
            {
                try
                {
                    var cmd = connection.CreateCommandAndParameters(args[0], args.Skip(1).ToArray());
                    cmd.ExecuteNonQuery();
                    return;
                }
                catch { }
            }
            
            _logger.LogWarning("⚠️ فشل تفعيل الباقة {ProfileName} للمستخدم {Username} بجميع المحاولات المتاحة.", profileName, username);
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
                    return await Task.Run(() =>
                    {
                        using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                        var host = _settingsService.Get("MikroTik.Host", "192.168.88.1");
                        var adminUser = _settingsService.Get("MikroTik.Username", "admin");
                        var pass = _settingsService.Get("MikroTik.Password", "");

                        connection.SendTimeout = 10000;
                        connection.ReceiveTimeout = 10000;
                        connection.Open(host, adminUser, pass);

                        bool isHotspot = false;
                        bool isV7 = false;
                        bool? useOwner = null;
                        string[] cachedProfileCommand = null;

                        try
                        {
                            var res = connection.CreateCommandAndParameters("/system/resource/print").ExecuteList().FirstOrDefault();
                            if (res != null && res.Words.TryGetValue("version", out var vWord) && vWord.StartsWith("7."))
                                isV7 = true;
                        }
                        catch { }

                        try
                        {
                            string testCmd = isV7 ? "/user-manager/user/print" : "/tool/user-manager/user/print";
                            connection.CreateCommandAndParameters(testCmd, "username", "dummy_check_123").ExecuteList();
                            isHotspot = false;
                        }
                        catch
                        {
                            isHotspot = true;
                        }

                        bool connectionBroken = false;
                        foreach (var u in users)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (connectionBroken)
                            {
                                results[u.username] = Result<MikroTikUserResult>.Failure("انقطع الاتصال بالراوتر أثناء عملية الرفع الجماعية", ErrorType.ExternalService);
                                failed++;
                                progress?.Report((success, failed, total));
                                continue;
                            }

                            bool wasAlreadyPresent = false;
                            bool userFailed = false;
                            string failureMessage = "";

                            if (isHotspot == true)
                            {
                                try
                                {
                                    var args = new List<string> { "name", u.username, "profile", u.profileName, "server", "all" };
                                    if (!string.IsNullOrEmpty(u.password)) { args.Add("password"); args.Add(u.password); }
                                    connection.CreateCommandAndParameters("/ip/hotspot/user/add", args.ToArray()).ExecuteNonQuery();
                                }
                                catch (Exception ex)
                                {
                                    if (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
                                    {
                                        wasAlreadyPresent = true;
                                    }
                                    else
                                    {
                                        userFailed = true;
                                        failureMessage = ex.Message;
                                        if (ex is System.Net.Sockets.SocketException || ex is System.IO.IOException || ex is TikConnectionException)
                                        {
                                            connectionBroken = true;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (isV7)
                                {
                                    try
                                    {
                                        var args = new List<string> { "name", u.username, "group", u.profileName };
                                        if (!string.IsNullOrEmpty(u.password)) { args.Add("password"); args.Add(u.password); }
                                        connection.CreateCommandAndParameters("/user-manager/user/add", args.ToArray()).ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        if (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
                                        {
                                            wasAlreadyPresent = true;
                                        }
                                        else
                                        {
                                            userFailed = true;
                                            failureMessage = ex.Message;
                                            if (ex is System.Net.Sockets.SocketException || ex is System.IO.IOException || ex is TikConnectionException)
                                            {
                                                connectionBroken = true;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var argsV6Cust = new List<string> { "customer", adminUser, "username", u.username };
                                    if (!string.IsNullOrEmpty(u.password)) { argsV6Cust.Add("password"); argsV6Cust.Add(u.password); }

                                    try
                                    {
                                        connection.CreateCommandAndParameters("/tool/user-manager/user/add", argsV6Cust.ToArray()).ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        if (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
                                        {
                                            wasAlreadyPresent = true;
                                        }
                                        else
                                        {
                                            var argsSimple = new List<string> { "username", u.username };
                                            if (!string.IsNullOrEmpty(u.password)) { argsSimple.Add("password"); argsSimple.Add(u.password); }
                                            try
                                            {
                                                connection.CreateCommandAndParameters("/tool/user-manager/user/add", argsSimple.ToArray()).ExecuteNonQuery();
                                            }
                                            catch (Exception simpleEx)
                                            {
                                                if (simpleEx.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    wasAlreadyPresent = true;
                                                }
                                                else
                                                {
                                                    userFailed = true;
                                                    failureMessage = simpleEx.Message;
                                                    if (simpleEx is System.Net.Sockets.SocketException || simpleEx is System.IO.IOException || simpleEx is TikConnectionException)
                                                    {
                                                        connectionBroken = true;
                                                    }
                                                    _logger.LogWarning("Fallback add failed: {Msg}", simpleEx.Message);
                                                }
                                            }
                                        }
                                    }

                                    if (!userFailed)
                                    {
                                        if (cachedProfileCommand != null)
                                        {
                                            var args = cachedProfileCommand.ToArray();
                                            for (int i = 0; i < args.Length; i++)
                                            {
                                                if (args[i] == "{user}") args[i] = u.username;
                                                if (args[i] == "{profile}") args[i] = u.profileName;
                                            }
                                            try { connection.CreateCommandAndParameters(args[0], args.Skip(1).ToArray()).ExecuteNonQuery(); } catch { }
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
                                                    connection.CreateCommandAndParameters(cmdArgs[0], cmdArgs.Skip(1).ToArray()).ExecuteNonQuery();
                                                    cachedProfileCommand = cmdArgs.Select(a => a == u.username ? "{user}" : (a == u.profileName ? "{profile}" : a)).ToArray();
                                                    break;
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }

                            if (userFailed)
                            {
                                results[u.username] = Result<MikroTikUserResult>.Failure(failureMessage, ErrorType.ExternalService);
                                failed++;
                            }
                            else
                            {
                                results[u.username] = Result<MikroTikUserResult>.Success(new MikroTikUserResult
                                {
                                    Id = wasAlreadyPresent ? $"existing_{Guid.NewGuid()}" : $"generated_{Guid.NewGuid()}",
                                    Username = u.username,
                                    WasAlreadyPresent = wasAlreadyPresent
                                });
                                success++;
                            }

                            progress?.Report((success, failed, total));
                        }

                        return results;
                    }, cancellationToken);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Bulk Error");
                foreach (var u in users)
                {
                    if (!results.ContainsKey(u.username))
                    {
                        results[u.username] = Result<MikroTikUserResult>.Failure($"خطأ عام: {ex.Message}", ErrorType.ExternalService);
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
                    return await Task.Run(() =>
                    {
                        using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                        var host = _settingsService.Get("MikroTik.Host", "192.168.88.1");
                        var user = _settingsService.Get("MikroTik.Username", "admin");
                        var pass = _settingsService.Get("MikroTik.Password", "");

                        connection.SendTimeout = 10000;
                        connection.ReceiveTimeout = 10000;
                        connection.Open(host, user, pass);

                        // ── 1. كشف نوع الراوتر مرة واحدة فقط ────────────────
                        bool isHotspot = false;
                        bool isV7 = false;

                        try
                        {
                            var res = connection.CreateCommand("/system/resource/print").ExecuteList().FirstOrDefault();
                            if (res != null && res.Words.TryGetValue("version", out var vWord) && vWord.StartsWith("7."))
                                isV7 = true;
                        }
                        catch (Exception resEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DETECT-WARN] /system/resource/print failed: {resEx.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                            _logger.LogWarning(resEx, "⚠️ /system/resource/print failed");
                        }

                        try
                        {
                            string testCmd = isV7 ? "/user-manager/router/print" : "/tool/user-manager/customer/print";
                            connection.CreateCommand(testCmd).ExecuteList();
                            isHotspot = false;
                        }
                        catch (Exception testEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DETECT-WARN] User Manager test command failed (Falling back to Hotspot): {testEx.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                            _logger.LogWarning(testEx, "⚠️ User Manager print test failed, using Hotspot fallback.");
                            isHotspot = true;
                        }

                        string queryCmdPath  = isHotspot ? "/ip/hotspot/user/print"  : (isV7 ? "/user-manager/user/print"  : "/tool/user-manager/user/print");
                        string removeCmdPath = isHotspot ? "/ip/hotspot/user/remove" : (isV7 ? "/user-manager/user/remove" : "/tool/user-manager/user/remove");
                        string filterProp    = isHotspot ? "name" : "username";

                        // ── 2. تحديد الـ IDs ──────────────────────────────────
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

                            try
                            {
                                var queryCmd = connection.CreateCommand(queryCmdPath);
                                queryCmd.AddParameter(filterProp, u.username);
                                var existing = queryCmd.ExecuteList();

                                if (!existing.Any())
                                {
                                    results[u.username] = Result.Success();
                                    success++;
                                    progress?.Report((success, failed, total));
                                    continue;
                                }

                                var first = existing.First();
                                if (!first.Words.TryGetValue(".id", out internalId) || string.IsNullOrEmpty(internalId))
                                {
                                    results[u.username] = Result.Failure("فشل تحديد المعرف الداخلي.", ErrorType.Conflict);
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

                        // ── 3. حذف Batch بـ numbers=*1,*2,*3 ────────────────
                        const int batchSize = 50;
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
                                System.Diagnostics.Debug.WriteLine($"[DELETE-05] Router Request Sent | Command: {removeCmdPath} | Parameters: numbers={ids} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                var removeCmd = connection.CreateCommandAndParameters(removeCmdPath, "numbers", ids);
                                removeCmd.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received | Success: True | Time: {DateTime.Now:HH:mm:ss.fff}");

                                foreach (var item in batch)
                                {
                                    results[item.username] = Result.Success();
                                    success++;
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received | Success: False (Batch remove failed, will retry individually) | Error: {ex.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                
                                // Fallback: حذف كل كرت منفرداً
                                foreach (var item in batch)
                                {
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DELETE-05] Router Request Sent (Fallback) | Command: {removeCmdPath} | Parameters: numbers={item.internalId} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                        var single = connection.CreateCommandAndParameters(removeCmdPath, "numbers", item.internalId);
                                        single.ExecuteNonQuery();
                                        System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received (Fallback) | Success: True | Username: {item.username} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                        results[item.username] = Result.Success();
                                        success++;
                                    }
                                    catch (Exception ex2)
                                    {
                                        if (ex2.Message.Contains("no such item") || ex2.Message.Contains("not found"))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[DELETE-WARN] Card '{item.username}' not found on router (already deleted). Treating as success. | Time: {DateTime.Now:HH:mm:ss.fff}");
                                            results[item.username] = Result.Success();
                                            success++;
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[DELETE-06] Router Response Received (Fallback) | Success: False | Username: {item.username} | Error: {ex2.Message} | Time: {DateTime.Now:HH:mm:ss.fff}");
                                            
                                            // Requirement 6: Detailed failure reporting
                                            var routerIdSetting = _settingsService.Get("LastConnectedRouterId", "unknown");
                                            var fullDetail = $"[DELETE-FAIL-DETAIL-LIVE] Username: '{item.username}' | RouterId: '{routerIdSetting}' | Command: '{removeCmdPath} numbers={item.internalId}' | Response: '{ex2.Message}'";
                                            System.Diagnostics.Debug.WriteLine(fullDetail);
                                            _logger.LogError(fullDetail);

                                            results[item.username] = Result.Failure($"فشل: {ex2.Message}", ErrorType.ExternalService);
                                            failed++;
                                        }
                                    }
                                }
                            }

                            progress?.Report((success, failed, total));
                        }

                        _logger.LogInformation("🗑️ [BulkDelete Legacy] انتهى: نجح {S} | فشل {F} من أصل {T}", success, failed, total);
                        return results;

                    }, cancellationToken);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DELETE-ERROR] Exception inside DeleteUsersBulkAsync: {ex} | Time: {DateTime.Now:HH:mm:ss.fff}");
                _logger.LogError(ex, "❌ [BulkDelete Legacy] خطأ عام");
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
                    return await Task.Run(() =>
                    {
                        using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                        var host = _settingsService.Get("MikroTik.Host", "192.168.88.1");
                        var user = _settingsService.Get("MikroTik.Username", "admin");
                        var pass = _settingsService.Get("MikroTik.Password", "");

                        connection.SendTimeout = 5000;
                        connection.ReceiveTimeout = 5000;
                        connection.Open(host, user, pass);

                        bool isHotspot = false;
                        bool isV7 = false;
                        
                        try
                        {
                            var res = connection.CreateCommand("/system/resource/print").ExecuteList().FirstOrDefault();
                            if (res != null && res.Words.TryGetValue("version", out var vWord) && vWord.StartsWith("7."))
                                isV7 = true;
                        }
                        catch { }

                        try
                        {
                            string testCmd = isV7 ? "/user-manager/user/print" : "/tool/user-manager/user/print";
                            connection.CreateCommandAndParameters(testCmd, "username", "dummy_check_123").ExecuteList();
                            isHotspot = false;
                        }
                        catch
                        {
                            isHotspot = true;
                        }

                        string? internalId = externalId;
                        if (string.IsNullOrEmpty(internalId) || !internalId.StartsWith("*"))
                        {
                            // Fallback to name-based query
                            string queryCmdPath = isHotspot ? "/ip/hotspot/user/print" : (isV7 ? "/user-manager/user/print" : "/tool/user-manager/user/print");
                            string filterProp = isHotspot ? "name" : "username";
                            
                            var queryCmd = connection.CreateCommand(queryCmdPath);
                            queryCmd.AddParameter(filterProp, username);
                            
                            var existing = queryCmd.ExecuteList();
                            if (!existing.Any())
                            {
                                _logger.LogInformation("✅ [DeleteUser] User {Username} does not exist on MikroTik (already deleted).", username);
                                return Result.Success();
                            }

                            if (existing.Count() > 1)
                            {
                                _logger.LogWarning("⚠️ [DeleteUser] Duplicate users found for {Username} on MikroTik. Aborting delete.", username);
                                return Result.Failure("تعارض في البيانات: يوجد أكثر من حساب بنفس الاسم على الراوتر.", ErrorType.Conflict);
                            }

                            var first = existing.First();
                            if (!first.Words.TryGetValue(".id", out internalId) || string.IsNullOrEmpty(internalId))
                            {
                                return Result.Failure("فشل في تحديد المعرف الداخلي للمستخدم على الراوتر.", ErrorType.Conflict);
                            }
                        }

                        string removeCmdPath = isHotspot ? "/ip/hotspot/user/remove" : (isV7 ? "/user-manager/user/remove" : "/tool/user-manager/user/remove");
                        var removeCmd = connection.CreateCommand(removeCmdPath);
                        removeCmd.AddParameter("numbers", internalId);
                        removeCmd.ExecuteNonQuery();

                        _logger.LogInformation("🔥 [DeleteUser] Successfully deleted user {Username} (ID: {Id}) from MikroTik.", username, internalId);
                        return Result.Success();

                    }, cancellationToken);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [DeleteUser] Error deleting user {Username} from MikroTik.", username);
                return Result.Failure($"فشل في حذف المستخدم من الراوتر: {ex.Message}", ErrorType.ExternalService);
            }
        }
    }
}
