using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Infrastructure.Data;
using tik4net;
using Lux.Platform.Abstractions.Interfaces;
using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MikroTikVoucherPrinter.Infrastructure.Services
{
    public class VoucherBackgroundImportManager : IVoucherBackgroundImportManager
    {
        private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
        private readonly IDbContextFactory<PlatformDbContext> _platformDbFactory;
        private readonly ISecureStorageService _secureStorageService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<VoucherBackgroundImportManager> _logger;

        private readonly ConcurrentDictionary<Guid, ImportState> _activeImports = new();
        private readonly ConcurrentDictionary<Guid, bool> _activeSweeps = new();
        private static readonly ConcurrentDictionary<Guid, string> _routerDbPathCache = new();

        // ─────────────────────────────────────────────────────────────────────
        // Sweep Cache: يحتوي على كامل قائمة مستخدمي الراوتر مقسمة بالبادئات
        // يُجلب مرة واحدة في بداية كل دورة Sweep (segmentIndex == 0)
        // ─────────────────────────────────────────────────────────────────────
        private readonly ConcurrentDictionary<Guid, SweepCache> _sweepCache = new();

        private sealed class SweepCache
        {
            public DateTime FetchedAt { get; init; }
            public bool IsHotspot { get; init; }
            /// <summary>Key = first character of username (lowercase), Value = list of router sentences</summary>
            public Dictionary<string, List<ITikSentence>> SegmentedUsers { get; init; } = new();
            public int TotalFetched { get; init; }
        }

        public event EventHandler<VoucherImportProgressEventArgs>? ProgressChanged;
        public event EventHandler<Guid>? ImportCompleted;
        public event EventHandler<VoucherImportErrorEventArgs>? ImportError;

        public VoucherBackgroundImportManager(
            IDbContextFactory<LuxCardDbContext> dbFactory,
            IDbContextFactory<PlatformDbContext> platformDbFactory,
            ISecureStorageService secureStorageService,
            ISettingsService settingsService,
            ILogger<VoucherBackgroundImportManager> logger)
        {
            _dbFactory = dbFactory;
            _platformDbFactory = platformDbFactory;
            _secureStorageService = secureStorageService;
            _settingsService = settingsService;
            _logger = logger;

            // Check if there was an active import when the app was closed last time
            _ = Task.Run(async () =>
            {
                try
                {
                    await _settingsService.LoadAsync();
                    var isActive = _settingsService.Get<bool>("BackgroundImport_IsActive", false);
                    if (isActive)
                    {
                        var routerIdStr = _settingsService.Get<string>("BackgroundImport_RouterId");
                        if (Guid.TryParse(routerIdStr, out var routerId))
                        {
                            // Resume import automatically
                            StartImport(routerId);
                        }
                    }
                }
                catch
                {
                    // Ignore startup load errors
                }
            });
        }

        private class ImportState
        {
            public Guid RouterId { get; }
            public int ImportedCount { get; set; }
            public int TotalCount { get; set; }
            public bool IsPaused { get; set; }
            public CancellationTokenSource Cts { get; } = new();

            public ImportState(Guid routerId)
            {
                RouterId = routerId;
            }
        }

        public async Task<bool> IsImportRequiredAsync(Guid routerId, CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var localCount = await db.Vouchers
                .IgnoreQueryFilters()
                .CountAsync(v => v.RouterId == routerId && !v.IsDeleted, cancellationToken);
            return localCount == 0;
        }

        public async Task<int> GetRouterVoucherCountAsync(Guid routerId, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
                var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
                if (router == null) return 0;

                var pass = "";
                if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
                {
                    pass = _secureStorageService.Decrypt(router.EncryptedPassword);
                }

                return await Task.Run(() =>
                {
                    using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                    connection.SendTimeout = 8000;
                    connection.ReceiveTimeout = 8000;
                    connection.Open(router.Host, router.Username, pass);

                    IEnumerable<ITikSentence> users;
                    try
                    {
                        users = connection.CreateCommandAndParameters("/tool/user-manager/user/print").ExecuteList().Cast<ITikSentence>();
                    }
                    catch
                    {
                        try
                        {
                            users = connection.CreateCommandAndParameters("/user-manager/user/print").ExecuteList().Cast<ITikSentence>();
                        }
                        catch
                        {
                            users = connection.CreateCommandAndParameters("/ip/hotspot/user/print").ExecuteList().Cast<ITikSentence>();
                        }
                    }
                    return users.Count();
                }, cancellationToken);
            }
            catch
            {
                return 0;
            }
        }

        public void StartImport(Guid routerId)
        {
            if (_activeImports.ContainsKey(routerId))
                return; // Already importing for this router

            var state = new ImportState(routerId);
            _activeImports[routerId] = state;

            // Run the background task
            _ = Task.Run(() => RunImportLifecycleAsync(state));
        }

        public void PauseImport(Guid routerId)
        {
            if (_activeImports.TryGetValue(routerId, out var state))
            {
                state.IsPaused = true;
                NotifyProgress(state);
            }
        }

        public void ResumeImport(Guid routerId)
        {
            if (_activeImports.TryGetValue(routerId, out var state))
            {
                state.IsPaused = false;
                NotifyProgress(state);
            }
        }

        public void CancelImport(Guid routerId)
        {
            if (_activeImports.TryGetValue(routerId, out var state))
            {
                state.Cts.Cancel();
                _activeImports.TryRemove(routerId, out _);
                ClearSettingsAsync().Wait();
            }
        }

        public bool IsImporting(Guid routerId) => _activeImports.ContainsKey(routerId);
        public bool IsPaused(Guid routerId) => _activeImports.TryGetValue(routerId, out var state) && state.IsPaused;
        public int GetImportedCount(Guid routerId) => _activeImports.TryGetValue(routerId, out var state) ? state.ImportedCount : 0;
        public int GetTotalImportCount(Guid routerId) => _activeImports.TryGetValue(routerId, out var state) ? state.TotalCount : 0;
        public int GetProgressPercent(Guid routerId) => _activeImports.TryGetValue(routerId, out var state) ? (state.TotalCount > 0 ? (int)((double)state.ImportedCount / state.TotalCount * 100) : 0) : 0;

        private void NotifyProgress(ImportState state)
        {
            ProgressChanged?.Invoke(this, new VoucherImportProgressEventArgs(state.RouterId, state.ImportedCount, state.TotalCount, state.IsPaused));
        }

        private async Task SaveSettingsAsync(Guid routerId)
        {
            try
            {
                _settingsService.Set("BackgroundImport_RouterId", routerId.ToString());
                _settingsService.Set("BackgroundImport_IsActive", true);
                await _settingsService.SaveAsync();
            }
            catch { }
        }

        private async Task ClearSettingsAsync()
        {
            try
            {
                _settingsService.Set("BackgroundImport_IsActive", false);
                await _settingsService.SaveAsync();
            }
            catch { }
        }

        private async Task RunImportLifecycleAsync(ImportState state)
        {
            var routerId = state.RouterId;
            var token = state.Cts.Token;

            try
            {
                await SaveSettingsAsync(routerId);

                // 1. Get connection options
                await using var platformDb = await _platformDbFactory.CreateDbContextAsync(token);
                var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, token);
                if (router == null)
                    throw new InvalidOperationException("الراوتر المحدد غير موجود بقاعدة البيانات");

                var pass = "";
                if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
                {
                    pass = _secureStorageService.Decrypt(router.EncryptedPassword);
                }

                // 2. Load local profiles for price lookup
                await using var db = await _dbFactory.CreateDbContextAsync(token);
                var localProfiles = await db.Profiles
                    .Where(p => p.RouterId == routerId)
                    .AsNoTracking()
                    .ToListAsync(token);

                var profilePriceLookup = localProfiles
                    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToDictionary(p => p.Name, p => p.Price, StringComparer.OrdinalIgnoreCase);

                // 3. Create or reuse Legacy Import Batch
                var legacyBatchName = $"LEGACY-IMPORT-{DateTime.Now:yyyyMMdd-HHmm}";
                var batchId = Guid.NewGuid();
                var legacyBatch = new Batch
                {
                    Id = batchId,
                    Name = legacyBatchName,
                    ProfileName = "Legacy",
                    TotalCount = 0,
                    RouterId = routerId
                };
                
                db.Batches.Add(legacyBatch);
                await db.SaveChangesAsync(token);

                // Check if the router has a UserManager SQLite database
                _logger.LogInformation("🔄 [InitialImport] Checking if router has a UserManager SQLite database...");
                string? scannedPath = null;
                if (_routerDbPathCache.TryGetValue(routerId, out var cachedPath))
                {
                    scannedPath = cachedPath;
                }
                else
                {
                    scannedPath = FindUserManagerDbPath(router.Host, router.Username, pass);
                    if (!string.IsNullOrEmpty(scannedPath))
                    {
                        _routerDbPathCache[routerId] = scannedPath;
                    }
                }
                if (!string.IsNullOrEmpty(scannedPath))
                {
                    _logger.LogInformation("✅ [InitialImport] UserManager database found at: {Path}. Using Snapshot Sync for initial import.", scannedPath);
                    await RunImportLifecycleFromSnapshotAsync(state, scannedPath, pass, token);
                    return;
                }

                // 4. Fetch Users list from MikroTik Router
                List<ITikSentence> rawUsers = new();
                bool isHotspot = false;

                using (var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api))
                {
                    connection.SendTimeout = 30000;
                    connection.ReceiveTimeout = 30000;
                    connection.Open(router.Host, router.Username, pass);

                    try
                    {
                        rawUsers = connection.CreateCommandAndParameters("/tool/user-manager/user/print").ExecuteList().Cast<ITikSentence>().ToList();
                    }
                    catch
                    {
                        try
                        {
                            rawUsers = connection.CreateCommandAndParameters("/user-manager/user/print").ExecuteList().Cast<ITikSentence>().ToList();
                        }
                        catch
                        {
                            rawUsers = connection.CreateCommandAndParameters("/ip/hotspot/user/print").ExecuteList().Cast<ITikSentence>().ToList();
                            isHotspot = true;
                        }
                    }
                }

                if (!rawUsers.Any())
                {
                    _activeImports.TryRemove(routerId, out _);
                    await ClearSettingsAsync();
                    ImportCompleted?.Invoke(this, routerId);
                    return;
                }

                // Get existing local usernames to skip duplicates
                var existingUsernames = new HashSet<string>(
                    await db.Vouchers
                        .IgnoreQueryFilters()
                        .Where(v => v.RouterId == routerId)
                        .Select(v => v.Username)
                        .ToListAsync(token),
                    StringComparer.OrdinalIgnoreCase
                );

                // Filter out already imported users
                var newUsers = rawUsers
                    .Where(sentence => {
                        var username = GetWord(sentence, isHotspot ? "name" : "username");
                        return !string.IsNullOrWhiteSpace(username) && !existingUsernames.Contains(username);
                    })
                    .ToList();

                // Setup state count
                state.TotalCount = rawUsers.Count;
                state.ImportedCount = rawUsers.Count - newUsers.Count;

                NotifyProgress(state);

                // Process in chunks of 500
                const int ChunkSize = 500;
                var chunk = new List<Voucher>();

                await using var dbWrite = await _dbFactory.CreateDbContextAsync(token);
                dbWrite.ChangeTracker.AutoDetectChangesEnabled = false;

                using var transaction = await dbWrite.Database.BeginTransactionAsync(token);
                try
                {
                    for (int i = 0; i < newUsers.Count; i++)
                    {
                        // Check cancellation or pause
                        while (state.IsPaused && !token.IsCancellationRequested)
                        {
                            await Task.Delay(500, token);
                        }

                        token.ThrowIfCancellationRequested();

                        var sentence = newUsers[i];
                        var username = GetWord(sentence, isHotspot ? "name" : "username")!;
                        var password = GetWord(sentence, "password") ?? "";
                        var profile = GetWord(sentence, "profile") ?? GetWord(sentence, "actual-profile") ?? "";

                        decimal? inferredPrice = null;
                        if (!string.IsNullOrWhiteSpace(profile) && profilePriceLookup.TryGetValue(profile, out var price))
                        {
                            inferredPrice = price;
                        }

                        var calculatedStatus = InferVoucherStatusFromSentence(sentence, isHotspot);

                        var voucher = new Voucher
                        {
                            Id = Guid.NewGuid(),
                            Username = username,
                            Password = password,
                            Price = inferredPrice ?? 0,
                            ProfileName = profile,
                            BatchId = batchId,
                            CredentialMode = CredentialMode.UsernameAndPassword,
                            Status = calculatedStatus,
                            PrintStatus = VoucherPrintStatus.Reserved,
                            AgentId = null,
                            RouterId = routerId,
                            VoucherSource = VoucherSource.ImportedFromRouter,
                            ImportDate = DateTime.UtcNow,
                            CreatedBy = "System Import",
                            Comment = GetWord(sentence, "comment")
                        };

                        var mikroTikUserId = GetWord(sentence, ".id");
                        if (!string.IsNullOrWhiteSpace(mikroTikUserId))
                        {
                            voucher.MarkAsSynced(mikroTikUserId);
                        }

                        chunk.Add(voucher);

                        if (chunk.Count >= ChunkSize || i == newUsers.Count - 1)
                        {
                            dbWrite.Vouchers.AddRange(chunk);
                            var batch = await dbWrite.Batches.FirstOrDefaultAsync(b => b.Id == batchId, token);
                            if (batch != null)
                            {
                                batch.TotalCount += chunk.Count;
                            }
                            await dbWrite.SaveChangesAsync(token);

                            state.ImportedCount += chunk.Count;
                            chunk.Clear();

                            NotifyProgress(state);
                        }
                    }

                    await transaction.CommitAsync(token);
                }
                catch
                {
                    await transaction.RollbackAsync(token);
                    throw;
                }
                finally
                {
                    dbWrite.ChangeTracker.AutoDetectChangesEnabled = true;
                }

                // Import Finished
                _activeImports.TryRemove(routerId, out _);
                await ClearSettingsAsync();
                ImportCompleted?.Invoke(this, routerId);
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception ex)
            {
                _activeImports.TryRemove(routerId, out _);
                await ClearSettingsAsync();
                var msg = GetFullExceptionMessage(ex);
                ImportError?.Invoke(this, new VoucherImportErrorEventArgs(routerId, msg));
            }
        }

        private async Task RunImportLifecycleFromSnapshotAsync(ImportState state, string scannedPath, string pass, CancellationToken token)
        {
            var routerId = state.RouterId;
            string tempMain = Path.Combine(Path.GetTempPath(), $"sqldb_{Guid.NewGuid()}.tmp");
            string tempWal = tempMain + "-wal";
            string tempShm = tempMain + "-shm";
            string cleanDb = tempMain + ".clean";

            try
            {
                await using var platformDb = await _platformDbFactory.CreateDbContextAsync(token);
                var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, token);
                if (router == null) return;

                // Download files
                _logger.LogInformation("📥 [InitialImport] Downloading UserManager database from {Path}...", scannedPath);
                FtpDownload($"ftp://{router.Host}/{scannedPath}", tempMain, router.Username, pass);
                FtpDownloadOptional($"ftp://{router.Host}/{scannedPath}-wal", tempWal, router.Username, pass);
                FtpDownloadOptional($"ftp://{router.Host}/{scannedPath}-shm", tempShm, router.Username, pass);

                // Online Backup merge
                _logger.LogInformation("🔄 [InitialImport] Merging WAL/SHM files via SQLite Backup API...");
                using (var src = new SqliteConnection($"Data Source={tempMain}"))
                using (var dst = new SqliteConnection($"Data Source={cleanDb}"))
                {
                    src.Open();
                    dst.Open();
                    src.BackupDatabase(dst);
                }

                SqliteConnection.ClearAllPools();
                TryDelete(tempMain); TryDelete(tempWal); TryDelete(tempShm);

                // Save to UserManagerCache
                SaveCleanDbCache(routerId, cleanDb);

                // Read snapshot users
                var snapshotUsers = new List<SnapshotUser>();
                using (var conn = new SqliteConnection($"Data Source={cleanDb}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT id, userName, password, disabled, regDate, downloadUsed, uploadUsed, uptimeUsed, lastSeenAt, actualProfileName FROM [user]";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        snapshotUsers.Add(new SnapshotUser
                        {
                            Id = reader.GetInt64(0),
                            Username = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Password = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Disabled = !reader.IsDBNull(3) && reader.GetInt32(3) == 1,
                            RegDate = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4),
                            DownloadUsed = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                            UploadUsed = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                            UptimeUsed = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                            LastSeenAt = reader.IsDBNull(8) ? (long?)null : reader.GetInt64(8),
                            ActualProfileName = reader.IsDBNull(9) ? "" : reader.GetString(9)
                        });
                    }
                }

                if (snapshotUsers.Count == 0)
                {
                    _activeImports.TryRemove(routerId, out _);
                    await ClearSettingsAsync();
                    ImportCompleted?.Invoke(this, routerId);
                    return;
                }

                await using var db = await _dbFactory.CreateDbContextAsync(token);

                // Create or reuse Legacy Import Batch
                var legacyBatchName = $"LEGACY-IMPORT-{DateTime.Now:yyyyMMdd-HHmm}";
                var batchId = Guid.NewGuid();
                var legacyBatch = new Batch
                {
                    Id = batchId,
                    Name = legacyBatchName,
                    ProfileName = "Legacy",
                    TotalCount = 0,
                    RouterId = routerId
                };
                db.Batches.Add(legacyBatch);
                await db.SaveChangesAsync(token);

                // Profile Price lookup
                var localProfiles = await db.Profiles
                    .Where(p => p.RouterId == routerId)
                    .AsNoTracking()
                    .ToListAsync(token);

                var profilePriceLookup = localProfiles
                    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToDictionary(p => p.Name, p => p.Price, StringComparer.OrdinalIgnoreCase);

                // Existing local usernames
                var existingUsernames = new HashSet<string>(
                    await db.Vouchers
                        .IgnoreQueryFilters()
                        .Where(v => v.RouterId == routerId)
                        .Select(v => v.Username)
                        .ToListAsync(token),
                    StringComparer.OrdinalIgnoreCase
                );

                var newUsers = snapshotUsers
                    .Where(u => !string.IsNullOrWhiteSpace(u.Username) && !existingUsernames.Contains(u.Username))
                    .ToList();

                state.TotalCount = snapshotUsers.Count;
                state.ImportedCount = snapshotUsers.Count - newUsers.Count;

                NotifyProgress(state);

                // Insert in chunks of 500
                const int ChunkSize = 500;
                var chunk = new List<Voucher>();

                await using var dbWrite = await _dbFactory.CreateDbContextAsync(token);
                dbWrite.ChangeTracker.AutoDetectChangesEnabled = false;

                using var transaction = await dbWrite.Database.BeginTransactionAsync(token);
                try
                {
                    for (int i = 0; i < newUsers.Count; i++)
                    {
                        while (state.IsPaused && !token.IsCancellationRequested)
                        {
                            await Task.Delay(500, token);
                        }

                        token.ThrowIfCancellationRequested();

                        var snap = newUsers[i];

                        decimal? inferredPrice = null;
                        if (!string.IsNullOrWhiteSpace(snap.ActualProfileName) && profilePriceLookup.TryGetValue(snap.ActualProfileName, out var price))
                        {
                            inferredPrice = price;
                        }

                        var calculatedStatus = (snap.UptimeUsed > 0 || snap.DownloadUsed > 0)
                            ? (string.IsNullOrWhiteSpace(snap.ActualProfileName) ? VoucherStatus.Expired : VoucherStatus.Used)
                            : VoucherStatus.Unused;

                        var voucher = new Voucher
                        {
                            Id = Guid.NewGuid(),
                            Username = snap.Username,
                            Password = snap.Password,
                            Price = inferredPrice ?? 0,
                            ProfileName = snap.ActualProfileName,
                            BatchId = batchId,
                            CredentialMode = CredentialMode.UsernameAndPassword,
                            Status = calculatedStatus,
                            PrintStatus = VoucherPrintStatus.Reserved,
                            BytesUsed = snap.DownloadUsed + snap.UploadUsed,
                            DownloadUsedBytes = snap.DownloadUsed,
                            UploadUsedBytes = snap.UploadUsed,
                            UptimeUsedSeconds = snap.UptimeUsed,
                            AgentId = null,
                            RouterId = routerId,
                            VoucherSource = VoucherSource.ImportedFromRouter,
                            ImportDate = DateTime.UtcNow,
                            CreatedBy = "System Initial Import",
                            Comment = ""
                        };

                        voucher.MarkAsSynced("*" + snap.Id.ToString("x"));

                        chunk.Add(voucher);

                        if (chunk.Count >= ChunkSize || i == newUsers.Count - 1)
                        {
                            dbWrite.Vouchers.AddRange(chunk);
                            var batch = await dbWrite.Batches.FirstOrDefaultAsync(b => b.Id == batchId, token);
                            if (batch != null)
                            {
                                batch.TotalCount += chunk.Count;
                            }
                            await dbWrite.SaveChangesAsync(token);

                            state.ImportedCount += chunk.Count;
                            chunk.Clear();

                            NotifyProgress(state);
                        }
                    }

                    await transaction.CommitAsync(token);
                }
                catch
                {
                    await transaction.RollbackAsync(token);
                    throw;
                }
                finally
                {
                    dbWrite.ChangeTracker.AutoDetectChangesEnabled = true;
                }

                _activeImports.TryRemove(routerId, out _);
                await ClearSettingsAsync();
                ImportCompleted?.Invoke(this, routerId);
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception ex)
            {
                _activeImports.TryRemove(routerId, out _);
                await ClearSettingsAsync();
                var msg = GetFullExceptionMessage(ex);
                ImportError?.Invoke(this, new VoucherImportErrorEventArgs(routerId, msg));
            }
            finally
            {
                TryDelete(cleanDb);
            }
        }

        private static string? GetWord(ITikSentence sentence, string key)
        {
            if (sentence.Words.TryGetValue(key, out var val))
                return val;
            return null;
        }

        private static long? TryLong(string? val)
        {
            if (long.TryParse(val, out var res))
                return res;
            return null;
        }

        private static bool InferExpiredFromSentence(ITikSentence sentence, bool isHotspot)
        {
            if (isHotspot)
            {
                var limit = GetWord(sentence, "limit-uptime");
                var uptime = GetWord(sentence, "uptime");
                if (!string.IsNullOrEmpty(limit) && !string.IsNullOrEmpty(uptime) && limit == uptime)
                    return true;
            }
            else
            {
                var profile = GetWord(sentence, "actual-profile-name");
                var download = TryLong(GetWord(sentence, "download-used")) ?? 0;
                var upload = TryLong(GetWord(sentence, "upload-used")) ?? 0;
                var lastSeen = GetWord(sentence, "last-seen");
                var uptimeUsed = GetWord(sentence, "uptime-used");

                bool hasUsage = download > 0 || upload > 0
                    || (!string.IsNullOrEmpty(lastSeen) && !lastSeen.Equals("never", StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrEmpty(uptimeUsed) && uptimeUsed != "0s");

                if (hasUsage && string.IsNullOrWhiteSpace(profile))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// يستنتج حالة الكرت (Unused / Used / Expired) من بيانات الـ API مباشرة.
        /// </summary>
        private static VoucherStatus InferVoucherStatusFromSentence(ITikSentence sentence, bool isHotspot)
        {
            if (InferExpiredFromSentence(sentence, isHotspot))
                return VoucherStatus.Expired;

            long download = isHotspot
                ? TryLong(GetWord(sentence, "bytes-out")) ?? 0
                : TryLong(GetWord(sentence, "download-used")) ?? 0;
            long upload = isHotspot
                ? TryLong(GetWord(sentence, "bytes-in")) ?? 0
                : TryLong(GetWord(sentence, "upload-used")) ?? 0;

            var lastSeen = GetWord(sentence, "last-seen");
            var uptimeUsed = GetWord(sentence, "uptime-used");

            bool hasUsage = download > 0 || upload > 0
                || (!string.IsNullOrEmpty(lastSeen) && !lastSeen.Equals("never", StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(uptimeUsed) && uptimeUsed != "0s");

            return hasUsage ? VoucherStatus.Used : VoucherStatus.Unused;
        }


        public async Task RunTriggeredSyncAsync(Guid routerId, CancellationToken cancellationToken = default)
        {
            await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
            var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
            if (router == null) return;

            var pass = "";
            if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
                pass = _secureStorageService.Decrypt(router.EncryptedPassword);

            // ─────────────────────────────────────────────────────────────────────
            // FIX: استخدام كاش الـ Sweep إن كان متاحاً (أقل من 5 دقائق)
            // بدلاً من .proplist الذي أثبتنا أنه يعيد 0 نتائج مع User Manager
            // ─────────────────────────────────────────────────────────────────────
            int routerCount = 0;
            if (_sweepCache.TryGetValue(routerId, out var existingCache) &&
                (DateTime.UtcNow - existingCache.FetchedAt).TotalMinutes < 5)
            {
                routerCount = existingCache.TotalFetched;
                _logger.LogDebug("[TriggeredSync] Using sweep cache ({Total} users, age {Age:F1}min).",
                    existingCache.TotalFetched,
                    (DateTime.UtcNow - existingCache.FetchedAt).TotalMinutes);
            }
            else
            {
                // Full Fetch بدون .proplist — الطريقة الوحيدة الموثوقة
                using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                connection.SendTimeout = 30000;
                connection.ReceiveTimeout = 30000;
                connection.Open(router.Host, router.Username, pass);

                List<ITikSentence> names;
                try
                {
                    names = connection.CreateCommand("/tool/user-manager/user/print")
                        .ExecuteList().Cast<ITikSentence>().ToList();
                }
                catch
                {
                    try
                    {
                        names = connection.CreateCommand("/user-manager/user/print")
                            .ExecuteList().Cast<ITikSentence>().ToList();
                    }
                    catch
                    {
                        try
                        {
                            names = connection.CreateCommand("/ip/hotspot/user/print")
                                .ExecuteList().Cast<ITikSentence>().ToList();
                        }
                        catch
                        {
                            return;
                        }
                    }
                }
                routerCount = names.Count;
            }

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            int localCount = await db.Vouchers
                .Where(v => v.RouterId == routerId && !v.IsDeleted)
                .CountAsync(cancellationToken);

            if (routerCount > 0 && routerCount == localCount)
            {
                _logger.LogDebug("[TriggeredSync] Counts match ({Count}). No import needed.", localCount);
                return;
            }

            _logger.LogInformation("[TriggeredSync] Router={RouterCount}, Local={LocalCount}. Triggering import.",
                routerCount, localCount);
            StartImport(routerId);
        }

        public async Task RunFullSyncAsync(Guid routerId, CancellationToken cancellationToken = default)
        {
            if (!_activeSweeps.TryAdd(routerId, true))
            {
                _logger.LogWarning("⚠️ [Sync Overlap] A sweep/sync is already running for router {RouterId}. Skipping.", routerId);
                return;
            }

            try
            {
                await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
                var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
                if (router == null) return;

                var pass = "";
                if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
                    pass = _secureStorageService.Decrypt(router.EncryptedPassword);

                // 1. Invalidate current cache to force fresh fetch
                InvalidateSweepCache(routerId);

                // 2. Fetch fresh cache
                _logger.LogInformation("🔄 [FullSync] Fetching fresh user list from MikroTik...");
                await FetchAndCacheAllUsersAsync(routerId, router.Host, router.Username, pass, cancellationToken);

                if (!_sweepCache.TryGetValue(routerId, out var cache))
                {
                    _logger.LogWarning("⚠️ [FullSync] No cache available after fetch attempt. Sync aborted.");
                    return;
                }

                // 3. Process all segments sequentially
                var prefixes = "abcdefghijklmnopqrstuvwxyz0123456789".Select(c => c.ToString()).ToList();
                for (int i = 0; i < prefixes.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var prefix = prefixes[i];
                    var routerUsersForSegment = cache.SegmentedUsers.TryGetValue(prefix, out var seg)
                        ? seg
                        : new List<ITikSentence>();

                    await ProcessSegmentAsync(routerId, prefix, routerUsersForSegment, cache.IsHotspot, cache.TotalFetched, cancellationToken);
                }

                _logger.LogInformation("   [FullSync] Reconciled all segments successfully for router {RouterId}.", routerId);
            }
            finally
            {
                _activeSweeps.TryRemove(routerId, out _);
            }
        }

        private class SnapshotUser
        {
            public long Id { get; set; }
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public bool Disabled { get; set; }
            public long? RegDate { get; set; }
            public long DownloadUsed { get; set; }
            public long UploadUsed { get; set; }
            public long UptimeUsed { get; set; }
            public long? LastSeenAt { get; set; }
            public string ActualProfileName { get; set; } = "";
        }

        public async Task RunSnapshotSyncAsync(Guid routerId, bool force = false, CancellationToken cancellationToken = default)
        {
            if (!_activeSweeps.TryAdd(routerId, true))
            {
                throw new InvalidOperationException("⚠️ يوجد عملية مزامنة أو فحص أخرى بالخلفية قيد التشغيل حالياً على هذا الراوتر. يرجى الانتظار والمحاولة مرة أخرى بعد قليل.");
            }

            string tempMain = Path.Combine(Path.GetTempPath(), $"sqldb_{Guid.NewGuid()}.tmp");
            string tempWal = tempMain + "-wal";
            string tempShm = tempMain + "-shm";
            string cleanDb = tempMain + ".clean";

            try
            {
                await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
                var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
                if (router == null) return;

                var pass = "";
                if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
                    pass = _secureStorageService.Decrypt(router.EncryptedPassword);

                // ─────────────────────────────────────────────────────────────
                // Step 1: FTP download with automatic dynamic path scanning
                // ─────────────────────────────────────────────────────────────
                bool downloadSucceeded = false;
                string baseRemote = "";

                // 1.0 Try reading from cache first
                if (_routerDbPathCache.TryGetValue(routerId, out var cachedPath) && !string.IsNullOrEmpty(cachedPath))
                {
                    _logger.LogInformation("🎯 [SnapshotSync] Found cached database path: {Path}. Downloading directly...", cachedPath);
                    try
                    {
                        FtpDownload($"ftp://{router.Host}/{cachedPath}", tempMain, router.Username, pass);
                        baseRemote = cachedPath;
                        downloadSucceeded = true;
                    }
                    catch (WebException ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [SnapshotSync] Failed to download from cached path: {Path}. Clearing cache and rescanning...", cachedPath);
                        _routerDbPathCache.TryRemove(routerId, out _);
                    }
                }

                if (!downloadSucceeded)
                {
                    // 1.1 Try scanning dynamically first
                    _logger.LogInformation("🔄 [SnapshotSync] Scanning router FTP directories recursively for 'sqldb' file...");
                    var scannedPath = FindUserManagerDbPath(router.Host, router.Username, pass);
                    if (!string.IsNullOrEmpty(scannedPath))
                    {
                        try
                        {
                            _logger.LogInformation("✅ [SnapshotSync] Found database dynamically at path: {Path}. Downloading...", scannedPath);
                            FtpDownload($"ftp://{router.Host}/{scannedPath}", tempMain, router.Username, pass);
                            baseRemote = scannedPath;
                            downloadSucceeded = true;
                            _routerDbPathCache[routerId] = scannedPath; // Cache it!
                        }
                        catch (WebException)
                        {
                            // Fallback to manual list if download failed
                        }
                    }
                }

                // 1.2 Fallback to manual probe list if dynamic scan failed/didn't find it
                if (!downloadSucceeded)
                {
                    var possiblePaths = new[]
                    {
                        "user-manager/sqldb",
                        "disk1/user-manager/sqldb",
                        "userman1/sqldb",
                        "disk1/userman1/sqldb"
                    };

                    foreach (var path in possiblePaths)
                    {
                        try
                        {
                            _logger.LogInformation("🔄 [SnapshotSync Fallback] Probing path: ftp://{Host}/{Path} ...", router.Host, path);
                            FtpDownload($"ftp://{router.Host}/{path}", tempMain, router.Username, pass);
                            baseRemote = path;
                            downloadSucceeded = true;
                            _logger.LogInformation("✅ [SnapshotSync Fallback] Downloaded successfully from path: {Path}", path);
                            _routerDbPathCache[routerId] = path; // Cache it!
                            break;
                        }
                        catch (WebException)
                        {
                            // Try next path
                        }
                    }
                }

                if (!downloadSucceeded)
                {
                    throw new FileNotFoundException("لم يتم العثور على قاعدة بيانات User Manager (sqldb) على الراوتر. يرجى التأكد من تشغيل الخدمة وصلاحيات المستخدم.");
                }

                // Download optional WAL and SHM files
                FtpDownloadOptional($"ftp://{router.Host}/{baseRemote}-wal", tempWal, router.Username, pass);
                FtpDownloadOptional($"ftp://{router.Host}/{baseRemote}-shm", tempShm, router.Username, pass);

                // ─────────────────────────────────────────────────────────────
                // Step 2: Validate magic bytes header
                // ─────────────────────────────────────────────────────────────
                var magic = new byte[16];
                using (var fs = new FileStream(tempMain, FileMode.Open, FileAccess.Read))
                {
                    fs.Read(magic, 0, 16);
                }
                var header = Encoding.ASCII.GetString(magic, 0, 15);
                if (header != "SQLite format 3")
                {
                    throw new InvalidDataException($"Downloaded file is not a valid SQLite database! Header: '{header}'");
                }

                // ─────────────────────────────────────────────────────────────
                // Step 3: SQLite Online Backup to merge WAL/SHM
                // ─────────────────────────────────────────────────────────────
                _logger.LogInformation("🔄 [SnapshotSync] Merging WAL/SHM files via SQLite Backup API...");
                using (var src = new SqliteConnection($"Data Source={tempMain}"))
                using (var dst = new SqliteConnection($"Data Source={cleanDb}"))
                {
                    src.Open();
                    dst.Open();
                    src.BackupDatabase(dst);
                }

                SqliteConnection.ClearAllPools();
                TryDelete(tempMain); TryDelete(tempWal); TryDelete(tempShm);
                TryDelete(tempMain + "-wal"); TryDelete(tempMain + "-shm");

                // Save to UserManagerCache
                SaveCleanDbCache(routerId, cleanDb);

                // ─────────────────────────────────────────────────────────────
                // Step 4: Extract users from SQLite Snapshot
                // ─────────────────────────────────────────────────────────────
                var snapshotUsers = new Dictionary<string, SnapshotUser>(StringComparer.OrdinalIgnoreCase);
                using (var conn = new SqliteConnection($"Data Source={cleanDb}"))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT id, userName, password, disabled, regDate, downloadUsed, uploadUsed, uptimeUsed, lastSeenAt, actualProfileName FROM [user]";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var snapUser = new SnapshotUser
                        {
                            Id = reader.GetInt64(0),
                            Username = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Password = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Disabled = !reader.IsDBNull(3) && reader.GetInt32(3) == 1,
                            RegDate = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4),
                            DownloadUsed = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                            UploadUsed = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                            UptimeUsed = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                            LastSeenAt = reader.IsDBNull(8) ? (long?)null : reader.GetInt64(8),
                            ActualProfileName = reader.IsDBNull(9) ? "" : reader.GetString(9)
                        };

                        if (!string.IsNullOrEmpty(snapUser.Username))
                        {
                            snapshotUsers[snapUser.Username] = snapUser;
                        }
                    }
                }

                _logger.LogInformation("✅ [SnapshotSync] Read {Count} users from Snapshot.", snapshotUsers.Count);

                // ─────────────────────────────────────────────────────────────
                // Step 5: Reconciliation with Safety Check Lockout
                // ─────────────────────────────────────────────────────────────
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
                var localVouchers = await db.Vouchers
                    .IgnoreQueryFilters()
                    .Where(v => v.RouterId == routerId)
                    .ToListAsync(cancellationToken);

                int localActiveCount = localVouchers.Count(v => !v.IsDeleted);
                int snapshotCount = snapshotUsers.Count;

                if (!force && localActiveCount > 0)
                {
                    double diff = Math.Abs((double)snapshotCount - localActiveCount);
                    double pct = (diff / localActiveCount) * 100.0;
                    if (pct > 30.0)
                    {
                        _logger.LogError("🚫 [SnapshotSync Safety] Snapshot mismatch detected (Diff: {Pct:F2}%). Sync aborted for safety.", pct);
                        throw new SnapshotMismatchException(localActiveCount, snapshotCount, pct);
                    }
                }

                // Start db transaction
                using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Price lookup dictionary
                    var localProfiles = await db.Profiles
                        .Where(p => p.RouterId == routerId)
                        .AsNoTracking()
                        .ToListAsync(cancellationToken);

                    var profilePriceLookup = localProfiles
                        .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToDictionary(p => p.Name, p => p.Price, StringComparer.OrdinalIgnoreCase);

                    // Import batch ID
                    var importBatch = await db.Batches
                        .FirstOrDefaultAsync(b => b.RouterId == routerId && b.Name.StartsWith("LEGACY-IMPORT-"), cancellationToken);
                    if (importBatch == null)
                    {
                        importBatch = new Batch
                        {
                            Id = Guid.NewGuid(),
                            Name = $"LEGACY-IMPORT-{DateTime.UtcNow:yyyyMMdd}",
                            ProfileName = "Legacy",
                            TotalCount = 0,
                            RouterId = routerId
                        };
                        db.Batches.Add(importBatch);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    var batchId = importBatch.Id;

                    var localLookup = localVouchers.ToDictionary(v => v.Username, v => v, StringComparer.OrdinalIgnoreCase);
                    
                    // Case 1 & Case 3: Reconcile existing local database records
                    foreach (var localVoucher in localVouchers)
                    {
                        if (snapshotUsers.TryGetValue(localVoucher.Username, out var snap))
                        {
                            // Case 1: Voucher exists in snapshot
                            bool modified = false;

                            if (localVoucher.IsDeleted)
                            {
                                // Auto-Restore
                                localVoucher.IsDeleted = false;
                                localVoucher.DeletedDate = null;
                                localVoucher.DeletedSource = null;
                                localVoucher.MarkAsSyncedForDelete(); // Reset sync flags
                                _logger.LogInformation("♻️ [SnapshotSync Auto-Restore] Restored '{Username}' since it exists in snapshot.", localVoucher.Username);
                                modified = true;
                            }

                            long totalBytes = snap.DownloadUsed + snap.UploadUsed;
                            if (localVoucher.BytesUsed != totalBytes) { localVoucher.BytesUsed = totalBytes; modified = true; }
                            if (localVoucher.DownloadUsedBytes != snap.DownloadUsed) { localVoucher.DownloadUsedBytes = snap.DownloadUsed; modified = true; }
                            if (localVoucher.UploadUsedBytes != snap.UploadUsed) { localVoucher.UploadUsedBytes = snap.UploadUsed; modified = true; }
                            if (localVoucher.UptimeUsedSeconds != snap.UptimeUsed) { localVoucher.UptimeUsedSeconds = snap.UptimeUsed; modified = true; }
                            if (localVoucher.ProfileName != snap.ActualProfileName && !string.IsNullOrEmpty(snap.ActualProfileName)) 
                            { 
                                localVoucher.ProfileName = snap.ActualProfileName; 
                                modified = true; 
                            }
                            if (localVoucher.IsDisabled != snap.Disabled) { localVoucher.IsDisabled = snap.Disabled; modified = true; }
                            if (localVoucher.Password != snap.Password && !string.IsNullOrEmpty(snap.Password))
                            {
                                localVoucher.Password = snap.Password;
                                modified = true;
                            }

                            var newStatus = (snap.UptimeUsed > 0 || snap.DownloadUsed > 0)
                                ? (string.IsNullOrWhiteSpace(snap.ActualProfileName) ? VoucherStatus.Expired : VoucherStatus.Used)
                                : VoucherStatus.Unused;
                            if (localVoucher.Status != newStatus) { localVoucher.Status = newStatus; modified = true; }

                            var expectedId = "*" + snap.Id.ToString("x");
                            if (localVoucher.MikroTikUserId != expectedId)
                            {
                                localVoucher.MarkAsSynced(expectedId);
                                modified = true;
                            }

                            if (modified)
                            {
                                db.Entry(localVoucher).State = EntityState.Modified;
                            }
                        }
                        else
                        {
                            // Case 3: Voucher disappeared from snapshot -> Soft Delete
                            if (!localVoucher.IsDeleted)
                            {
                                localVoucher.IsDeleted = true;
                                localVoucher.DeletedDate = DateTime.UtcNow;
                                localVoucher.DeletedSource = VoucherDeletedSource.SnapshotSync;
                                localVoucher.MarkAsPendingForDeleteOrRestore();

                                db.Entry(localVoucher).State = EntityState.Modified;
                                _logger.LogInformation("🗑️ [SnapshotSync Soft-Delete] Soft deleted '{Username}' since it is missing in snapshot.", localVoucher.Username);
                            }
                        }
                    }

                    // Case 2: Import new users from snapshot
                    foreach (var snapName in snapshotUsers.Keys)
                    {
                        if (!localLookup.ContainsKey(snapName))
                        {
                            var snap = snapshotUsers[snapName];

                            decimal? inferredPrice = null;
                            if (profilePriceLookup.TryGetValue(snap.ActualProfileName, out var pr))
                                inferredPrice = pr;

                            var newVoucher = new Voucher
                            {
                                Id = Guid.NewGuid(),
                                Username = snap.Username,
                                Password = snap.Password,
                                Price = inferredPrice ?? 0,
                                ProfileName = snap.ActualProfileName,
                                BatchId = batchId,
                                CredentialMode = CredentialMode.UsernameAndPassword,
                                Status = snap.UptimeUsed > 0 || snap.DownloadUsed > 0 ? VoucherStatus.Used : VoucherStatus.Unused,
                                PrintStatus = VoucherPrintStatus.Reserved,
                                BytesUsed = snap.DownloadUsed + snap.UploadUsed,
                                DownloadUsedBytes = snap.DownloadUsed,
                                UploadUsedBytes = snap.UploadUsed,
                                UptimeUsedSeconds = snap.UptimeUsed,
                                AgentId = null,
                                RouterId = routerId,
                                VoucherSource = VoucherSource.ImportedFromRouter,
                                ImportDate = DateTime.UtcNow,
                                CreatedBy = "System Snapshot Sync",
                                Comment = ""
                            };

                            newVoucher.MarkAsSynced("*" + snap.Id.ToString("x"));

                            db.Vouchers.Add(newVoucher);
                            _logger.LogInformation("📥 [SnapshotSync Import] Imported new user '{Username}' from snapshot.", snap.Username);
                        }
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("✅ [SnapshotSync] Database reconciled and transaction committed.");
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            finally
            {
                TryDelete(cleanDb);
                _activeSweeps.TryRemove(routerId, out _);
            }
        }

        private static void FtpDownload(string ftpUrl, string localPath, string user, string pass)
        {
            var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.Credentials = new NetworkCredential(user, pass);
            req.UsePassive = true; req.UseBinary = true; req.KeepAlive = false; req.Timeout = 30000;

            using var resp = (FtpWebResponse)req.GetResponse();
            using var stream = resp.GetResponseStream();
            using var file = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.CopyTo(file);
        }

        private static void FtpDownloadOptional(string ftpUrl, string localPath, string user, string pass)
        {
            try 
            { 
                FtpDownload(ftpUrl, localPath, user, pass); 
            }
            catch (Exception)
            {
                // Swallowing any exception when downloading optional WAL/SHM files
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        public async Task RunSegmentedSweepAsync(Guid routerId, int segmentIndex, int totalSegments, CancellationToken cancellationToken = default)
        {
            if (!_activeSweeps.TryAdd(routerId, true))
            {
                _logger.LogWarning("⚠️ [Sweep Overlap] A sweep is already running for router {RouterId}. Skipping.", routerId);
                return;
            }

            try
            {
                await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
                var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
                if (router == null) return;

                var pass = "";
                if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
                    pass = _secureStorageService.Decrypt(router.EncryptedPassword);

                var prefixes = "abcdefghijklmnopqrstuvwxyz0123456789".Select(c => c.ToString()).ToList();
                if (segmentIndex < 0 || segmentIndex >= prefixes.Count) return;
                var prefix = prefixes[segmentIndex];

                // ─────────────────────────────────────────────────────────────────
                // PHASE 1: أول شريحة في الدورة → جلب كامل القائمة وتحديث الكاش
                // ─────────────────────────────────────────────────────────────────
                if (segmentIndex == 0 || !_sweepCache.ContainsKey(routerId))
                {
                    await FetchAndCacheAllUsersAsync(routerId, router.Host, router.Username, pass, cancellationToken);
                }

                // ─────────────────────────────────────────────────────────────────
                // PHASE 2: قراءة شريحة الحرف الحالي من الكاش
                // ─────────────────────────────────────────────────────────────────
                if (!_sweepCache.TryGetValue(routerId, out var cache))
                {
                    _logger.LogWarning("⚠️ [Sweep] No cache available after fetch attempt for router {RouterId}. " +
                        "Skipping segment '{Prefix}' — no data to act on.", routerId, prefix);
                    return; // Safety: لا تمضِ بدون بيانات موثوقة من الراوتر
                }

                var routerUsersForSegment = cache.SegmentedUsers.TryGetValue(prefix, out var seg)
                    ? seg
                    : new List<ITikSentence>();

                await ProcessSegmentAsync(routerId, prefix, routerUsersForSegment,
                    cache.IsHotspot, cache.TotalFetched, cancellationToken);
            }
            finally
            {
                _activeSweeps.TryRemove(routerId, out _);
            }
        }

        /// <summary>
        /// يجلب جميع مستخدمي الراوتر دفعة واحدة بدون فلاتر (الطريقة الوحيدة الموثوقة)
        /// ويقسمهم داخل C# حسب أول حرف في الاسم، ثم يخزنهم في _sweepCache.
        /// SAFETY GATE L0: إذا أعاد الراوتر 0 مستخدمين → لا يُحدَّث الكاش → كل الشرائح تُتجاوز.
        /// </summary>
        private async Task FetchAndCacheAllUsersAsync(
            Guid routerId, string host, string username, string pass,
            CancellationToken cancellationToken)
        {
            List<ITikSentence> allUsers = new();
            bool isHotspot = false;

            try
            {
                using var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                connection.SendTimeout = 60000;   // مهلة أطول للجلب الكامل (30k+ records)
                connection.ReceiveTimeout = 60000;
                connection.Open(host, username, pass);

                // ──────────────────────────────────────────────────────────────────
                // Fetch بدون أي فلاتر أو .proplist — الطريقة الوحيدة المعتمدة والمثبتة
                // ──────────────────────────────────────────────────────────────────
                try
                {
                    allUsers = connection.CreateCommand("/tool/user-manager/user/print")
                                        .ExecuteList().Cast<ITikSentence>().ToList();
                    _logger.LogDebug("[SweepCache] Fetched {Count} via /tool/user-manager/user/print.", allUsers.Count);
                }
                catch
                {
                    try
                    {
                        allUsers = connection.CreateCommand("/user-manager/user/print")
                                            .ExecuteList().Cast<ITikSentence>().ToList();
                        _logger.LogDebug("[SweepCache] Fetched {Count} via /user-manager/user/print.", allUsers.Count);
                    }
                    catch
                    {
                        allUsers = connection.CreateCommand("/ip/hotspot/user/print")
                                            .ExecuteList().Cast<ITikSentence>().ToList();
                        isHotspot = true;
                        _logger.LogDebug("[SweepCache] Fetched {Count} via /ip/hotspot/user/print (Hotspot).", allUsers.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚫 [SweepCache Safety Gate L0] Connection failed during full fetch for router {RouterId}. " +
                    "Cache NOT updated. All segments will be skipped this cycle.", routerId);
                return; // لا تُحدِّث الكاش عند فشل الاتصال
            }

            // ─────────────────────────────────────────────────────────────
            // SAFETY GATE L0: Strict Empty Response Guard
            // ─────────────────────────────────────────────────────────────
            if (allUsers.Count == 0)
            {
                _logger.LogError(
                    "🚫 [SweepCache Safety Gate L0] Router returned 0 users for router {RouterId}. " +
                    "This is unexpected. Cache NOT updated. All 36 segments will be skipped this cycle.",
                    routerId);
                // مهم: لا تُحدِّث الكاش — يبقى الكاش السابق محفوظاً إن وُجد
                return;
            }

            // تقسيم المستخدمين بالبادئة داخل C# — بديل Wildcard الصحيح والمثبت
            var segmented = new Dictionary<string, List<ITikSentence>>(StringComparer.OrdinalIgnoreCase);
            var nameKey = isHotspot ? "name" : "username";
            foreach (var sentence in allUsers)
            {
                var name = GetWord(sentence, nameKey) ?? "";
                if (name.Length == 0) continue;
                var key = name[0].ToString().ToLowerInvariant();
                if (!segmented.ContainsKey(key))
                    segmented[key] = new List<ITikSentence>();
                segmented[key].Add(sentence);
            }

            _sweepCache[routerId] = new SweepCache
            {
                FetchedAt = DateTime.UtcNow,
                IsHotspot = isHotspot,
                SegmentedUsers = segmented,
                TotalFetched = allUsers.Count
            };

            _logger.LogInformation(
                "✅ [SweepCache] Built cache for router {RouterId}: {Total} users across {Segs} prefix segments.",
                routerId, allUsers.Count, segmented.Count);
        }

        /// <summary>
        /// يعالج شريحة حرف واحدة باستخدام بيانات الكاش — مع حوائز أمان متعددة الطبقات
        /// </summary>
        private async Task ProcessSegmentAsync(
            Guid routerId, string prefix,
            List<ITikSentence> routerUsersForSegment,
            bool isHotspot, int totalFetched,
            CancellationToken cancellationToken)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var localVouchers = await db.Vouchers
                .IgnoreQueryFilters()
                .Where(v => v.RouterId == routerId && v.Username.StartsWith(prefix))
                .ToListAsync(cancellationToken);

            var localActive = localVouchers.Count(v => !v.IsDeleted);

            // ─────────────────────────────────────────────────────────────
            // SAFETY GATE L2: Router Total Integrity Check
            // إذا إجمالي ما جلبناه من الراوتر أقل من 50% من الكروت المحلية
            // → شيء خاطئ → أوقف هذه الشريحة بالكامل
            // ─────────────────────────────────────────────────────────────
            var totalLocalActive = await db.Vouchers
                .Where(v => v.RouterId == routerId && !v.IsDeleted)
                .CountAsync(cancellationToken);

            if (totalLocalActive > 100 && totalFetched < (int)(totalLocalActive * 0.5))
            {
                _logger.LogError(
                    "🚫 [Sweep Guard L2] Router total fetched ({RouterTotal}) is less than 50% of local active ({LocalTotal}). " +
                    "Segment '{Prefix}' BLOCKED. Possible connection or data integrity issue.",
                    totalFetched, totalLocalActive, prefix);
                return;
            }

            // ─────────────────────────────────────────────────────────────
            // SAFETY GATE L3: Segment Empty Guard
            // إذا الشريحة فارغة من الراوتر لكن لدينا كروت نشطة محلياً
            // → هذا يعني غالباً خطأ في الجلب، لا حذف فعلي → تجاوز Case 3
            // ─────────────────────────────────────────────────────────────
            bool segmentEmptyGuardTriggered = routerUsersForSegment.Count == 0 && localActive > 0;
            if (segmentEmptyGuardTriggered)
            {
                _logger.LogWarning(
                    "🛡️ [Sweep Guard L3] Segment '{Prefix}' has 0 users from router but {LocalActive} active locally. " +
                    "Case 3 (Soft Delete) BLOCKED for this segment. Skipping entirely.",
                    prefix, localActive);
                return;
            }

            // بناء lookup من بيانات الراوتر
            var routerLookup = new Dictionary<string, ITikSentence>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in routerUsersForSegment)
            {
                var uName2 = GetWord(u, isHotspot ? "name" : "username");
                if (!string.IsNullOrEmpty(uName2))
                    routerLookup[uName2] = u;
            }

            var localLookup = localVouchers.ToDictionary(v => v.Username, v => v, StringComparer.OrdinalIgnoreCase);
            bool hasChanges = false;

            // Load profiles for price lookup (Case 2)
            var localProfiles = await db.Profiles
                .Where(p => p.RouterId == routerId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var profilePriceLookup = localProfiles
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToDictionary(p => p.Name, p => p.Price, StringComparer.OrdinalIgnoreCase);

            // Get or create import batch
            var importBatch = await db.Batches
                .FirstOrDefaultAsync(b => b.RouterId == routerId && b.Name.StartsWith("LEGACY-IMPORT-"), cancellationToken);
            if (importBatch == null)
            {
                importBatch = new Batch
                {
                    Id = Guid.NewGuid(),
                    Name = $"LEGACY-IMPORT-{DateTime.UtcNow:yyyyMMdd}",
                    ProfileName = "Legacy",
                    TotalCount = 0,
                    RouterId = routerId
                };
                db.Batches.Add(importBatch);
                await db.SaveChangesAsync(cancellationToken);
            }
            var batchId = importBatch.Id;

            // Case 1 & Case 2: Iterate over router users in this segment
            foreach (var uName in routerLookup.Keys)
            {
                var sentence = routerLookup[uName];
                var disabledStr = GetWord(sentence, "disabled") ?? "false";
                var disabled = disabledStr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                               disabledStr.Equals("yes", StringComparison.OrdinalIgnoreCase);

                // FIX 2: actual-profile أولاً (الباقة المُطبَّقة فعلاً) ثم profile كاحتياط
                var profile = GetWord(sentence, "actual-profile")
                           ?? GetWord(sentence, "profile")
                           ?? GetWord(sentence, "group")
                           ?? "";
                var comment = GetWord(sentence, "comment") ?? "";
                var routerIdValue = GetWord(sentence, ".id") ?? "";

                if (localLookup.TryGetValue(uName, out var voucher))
                {
                    // ─────────────────────────────────────────────────────────────
                    // AUTO-RESTORE: كرت موجود على الراوتر لكنه محذوف محلياً بسبب
                    // خطأ الـ RouterOS DeletedSource → استعادة تلقائية
                    // ─────────────────────────────────────────────────────────────
                    if (voucher.IsDeleted && voucher.DeletedSource == VoucherDeletedSource.RouterOS)
                    {
                        voucher.IsDeleted = false;
                        voucher.DeletedDate = null;
                        voucher.DeletedSource = null;
                        if (!string.IsNullOrEmpty(routerIdValue))
                            voucher.MarkAsSynced(routerIdValue);
                        else
                            voucher.MarkAsSyncedForDelete(); // fallback: mark as synced anyway

                        _logger.LogInformation(
                            "♻️ [Sweep Auto-Restore] Voucher '{Username}' was incorrectly soft-deleted " +
                            "(DeletedSource=RouterOS). Router confirms it exists. Restored automatically.",
                            uName);

                        db.Entry(voucher).State = EntityState.Modified;
                        hasChanges = true;
                        continue;
                    }

                    // Case 1: Reconcile active vouchers — تحديث Profile/Comment/Disabled/SyncId/Usage
                    if (!voucher.IsDeleted)
                    {
                        bool modified = false;
                        if (voucher.IsDisabled != disabled) { voucher.IsDisabled = disabled; modified = true; }
                        // لا نتجاوز فوق باقة فارغة تعود بالكاش
                        if (!string.IsNullOrEmpty(profile) && voucher.ProfileName != profile) { voucher.ProfileName = profile; modified = true; }
                        if (voucher.Comment != comment) { voucher.Comment = comment; modified = true; }
                        if (voucher.MikroTikUserId != routerIdValue && !string.IsNullOrEmpty(routerIdValue))
                        {
                            voucher.MarkAsSynced(routerIdValue);
                            modified = true;
                        }

                        // FIX 1: تحديث بيانات الاستخدام (BytesUsed / UptimeUsed / Status)
                        long downloadUsed = isHotspot
                            ? TryLong(GetWord(sentence, "bytes-out")) ?? 0
                            : TryLong(GetWord(sentence, "download-used")) ?? 0;
                        long uploadUsed = isHotspot
                            ? TryLong(GetWord(sentence, "bytes-in")) ?? 0
                            : TryLong(GetWord(sentence, "upload-used")) ?? 0;
                        long totalBytes = downloadUsed + uploadUsed;
                        if (voucher.BytesUsed != totalBytes) { voucher.BytesUsed = totalBytes; modified = true; }
                        if (voucher.DownloadUsedBytes != downloadUsed) { voucher.DownloadUsedBytes = downloadUsed; modified = true; }
                        if (voucher.UploadUsedBytes != uploadUsed) { voucher.UploadUsedBytes = uploadUsed; modified = true; }

                        var uptimeStr = GetWord(sentence, "uptime-used");
                        if (!string.IsNullOrEmpty(uptimeStr))
                        {
                            var uptimeSecs = ParseDurationToSeconds(uptimeStr);
                            if (voucher.UptimeUsedSeconds != uptimeSecs) { voucher.UptimeUsedSeconds = uptimeSecs; modified = true; }
                        }

                        var newStatus = InferVoucherStatusFromSentence(sentence, isHotspot);
                        if (voucher.Status != newStatus) { voucher.Status = newStatus; modified = true; }

                        if (modified)
                        {
                            hasChanges = true;
                            db.Entry(voucher).State = EntityState.Modified;
                        }
                    }
                }
                else
                {
                    // Case 2: Import new active voucher from router
                    decimal? inferredPrice = null;
                    if (profilePriceLookup.TryGetValue(profile, out var pr))
                        inferredPrice = pr;

                    // FIX 1: قراءة بيانات الاستخدام وكلمة السر للكرت الجديد
                    long dlUsed = isHotspot
                        ? TryLong(GetWord(sentence, "bytes-out")) ?? 0
                        : TryLong(GetWord(sentence, "download-used")) ?? 0;
                    long ulUsed = isHotspot
                        ? TryLong(GetWord(sentence, "bytes-in")) ?? 0
                        : TryLong(GetWord(sentence, "upload-used")) ?? 0;
                    long bytesTotal = dlUsed + ulUsed;

                    var uptimeUsedStr = GetWord(sentence, "uptime-used");
                    long uptimeSecs2 = string.IsNullOrEmpty(uptimeUsedStr) ? 0 : ParseDurationToSeconds(uptimeUsedStr);

                    var password = GetWord(sentence, "password") ?? uName;

                    var newVoucher = new Voucher
                    {
                        Id = Guid.NewGuid(),
                        Username = uName,
                        Password = password,
                        Price = inferredPrice ?? 0,
                        ProfileName = profile,
                        BatchId = batchId,
                        CredentialMode = CredentialMode.UsernameAndPassword,
                        Status = InferVoucherStatusFromSentence(sentence, isHotspot),
                        PrintStatus = VoucherPrintStatus.Reserved,
                        BytesUsed = bytesTotal,
                        DownloadUsedBytes = dlUsed,
                        UploadUsedBytes = ulUsed,
                        UptimeUsedSeconds = uptimeSecs2,
                        AgentId = null,
                        RouterId = routerId,
                        VoucherSource = VoucherSource.ImportedFromRouter,
                        ImportDate = DateTime.UtcNow,
                        CreatedBy = "System Sweep",
                        Comment = comment
                    };

                    if (!string.IsNullOrEmpty(routerIdValue))
                        newVoucher.MarkAsSynced(routerIdValue);

                    db.Vouchers.Add(newVoucher);
                    hasChanges = true;
                }
            }

            // Case 3: Soft-delete locally active vouchers missing from router
            var missingVouchers = localVouchers
                .Where(v => !v.IsDeleted && !routerLookup.ContainsKey(v.Username))
                .ToList();

            var totalActiveVouchers = localVouchers.Count(v => !v.IsDeleted);

            if (missingVouchers.Any())
            {
                // ─────────────────────────────────────────────────────────────
                // SAFETY GATE L4: MassDelete Lockout (خط الدفاع الاحتياطي)
                // معاملات مخففة بعد أن أصبحت طبقات L0-L3 تتكفل بالحالات الخطيرة
                // ─────────────────────────────────────────────────────────────
                int floor = _settingsService.Get<int>("Sync.MassDelete.Floor", 3);          // خُفِّض من 10 إلى 3
                int ceiling = _settingsService.Get<int>("Sync.MassDelete.Ceiling", 200);     // خُفِّض من 500 إلى 200
                double percentage = _settingsService.Get<double>("Sync.MassDelete.Percentage", 5.0); // خُفِّض من 10% إلى 5%

                bool massDeleteLocked = false;
                if (missingVouchers.Count >= floor)
                {
                    double ratio = totalActiveVouchers > 0
                        ? ((double)missingVouchers.Count / totalActiveVouchers * 100.0)
                        : 0.0;
                    if (missingVouchers.Count >= ceiling || ratio >= percentage)
                    {
                        massDeleteLocked = true;
                        _logger.LogWarning(
                            "⚠️ [Sweep Guard L4 - MassDelete Lockout] Case 3 aborted for segment '{Prefix}'. " +
                            "Missing: {MissingCount}/{TotalActive} ({Ratio:F1}%). Thresholds: Floor={Floor}, Ceiling={Ceiling}, %={Percentage}%",
                            prefix, missingVouchers.Count, totalActiveVouchers, ratio, floor, ceiling, percentage);
                    }
                }

                if (!massDeleteLocked)
                {
                    foreach (var voucher in missingVouchers)
                    {
                        voucher.IsDeleted = true;
                        voucher.DeletedDate = DateTime.UtcNow;
                        voucher.DeletedSource = VoucherDeletedSource.RouterOS;
                        voucher.MarkAsSyncedForDelete();

                        db.Entry(voucher).State = EntityState.Modified;
                        hasChanges = true;

                        _logger.LogInformation(
                            "🗑️ [Sweep Case 3] Soft-deleted '{Username}' — not found in router segment '{Prefix}'.",
                            voucher.Username, prefix);
                    }
                }
            }

            if (hasChanges)
                await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// يمسح كاش الـ Sweep لراوتر محدد — يُستدعى عند تغيير الراوتر النشط
        /// </summary>
        public void InvalidateSweepCache(Guid routerId)
        {
            _sweepCache.TryRemove(routerId, out _);
            _logger.LogDebug("[SweepCache] Cache invalidated for router {RouterId}.", routerId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GetCachedCleanDbPath — يُستخدم من شاشة Sales للقراءة المباشرة
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// يعيد مسار آخر نسخة منظفة (.clean) من قاعدة بيانات User Manager للراوتر المحدد.
        /// تُستخدم من شاشة Sales لقراءة بيانات المبيعات مباشرة.
        /// </summary>
        public string? GetCachedCleanDbPath(Guid routerId)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var cacheDir = Path.Combine(appData, "Lux Platform", "UserManagerCache");
                var destPath = Path.Combine(cacheDir, $"userman_{routerId}.db");
                if (System.IO.File.Exists(destPath))
                {
                    return destPath;
                }
            }
            catch
            {
                // Ignored
            }
            return null;
        }

        private void SaveCleanDbCache(Guid routerId, string cleanDbPath)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var cacheDir = Path.Combine(appData, "Lux Platform", "UserManagerCache");
                Directory.CreateDirectory(cacheDir);
                var destPath = Path.Combine(cacheDir, $"userman_{routerId}.db");
                
                // Copy the file to the destination path
                File.Copy(cleanDbPath, destPath, true);
                _logger.LogInformation("💾 [UserManagerCache] Saved clean DB cache for router {RouterId} at {Path}", routerId, destPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [UserManagerCache] Failed to save clean DB cache for router {RouterId}", routerId);
            }
        }



        public async Task RestoreVouchersChunkedAsync(Guid routerId, IProgress<(int restored, int total)> progress, CancellationToken cancellationToken = default)
        {
            await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
            var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
            if (router == null) return;

            var pass = "";
            if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
            {
                pass = _secureStorageService.Decrypt(router.EncryptedPassword);
            }

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var activeVouchers = await db.Vouchers
                .IgnoreQueryFilters()
                .Where(v => v.RouterId == routerId && !v.IsDeleted && v.Status != VoucherStatus.Expired && v.SyncStatus != SyncStatus.Synced)
                .ToListAsync(cancellationToken);

            if (!activeVouchers.Any()) return;

            var localProfiles = await db.Profiles
                .Where(p => p.RouterId == routerId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var profileLookup = localProfiles.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            using (var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api))
            {
                connection.SendTimeout = 30000;
                connection.ReceiveTimeout = 30000;
                connection.Open(router.Host, router.Username, pass);

                bool isHotspot = false;
                try
                {
                    connection.CreateCommandAndParameters("/ip/hotspot/user/print", ".proplist", "name", "?name", "nonexistentuser").ExecuteList();
                    isHotspot = true;
                }
                catch {}

                const int ChunkSize = 2000;
                for (int i = 0; i < activeVouchers.Count; i += ChunkSize)
                {
                    var chunk = activeVouchers.Skip(i).Take(ChunkSize).ToList();

                    foreach (var voucher in chunk)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        long remainingUptime = 0;
                        long remainingBytes = 0;

                        if (profileLookup.TryGetValue(voucher.ProfileName, out var profile))
                        {
                            var totalUptime = ParseDurationToSeconds(profile.Uptime);
                            var totalBytes = ParseTransferToBytes(profile.Transfer);

                            remainingUptime = totalUptime > 0 ? Math.Max(0, totalUptime - voucher.UptimeUsedSeconds) : 0;
                            remainingBytes = totalBytes > 0 ? Math.Max(0, totalBytes - voucher.BytesUsed) : 0;
                        }

                        try
                        {
                            if (isHotspot)
                            {
                                var args = new List<string> { "name", voucher.Username };
                                if (!string.IsNullOrEmpty(voucher.EffectivePassword))
                                {
                                    args.Add("password");
                                    args.Add(voucher.EffectivePassword);
                                }
                                args.Add("profile");
                                args.Add(voucher.ProfileName);
                                if (remainingUptime > 0)
                                {
                                    args.Add("limit-uptime");
                                    args.Add($"{remainingUptime}");
                                }
                                if (remainingBytes > 0)
                                {
                                    args.Add("limit-bytes-total");
                                    args.Add($"{remainingBytes}");
                                }
                                if (!string.IsNullOrEmpty(voucher.Comment))
                                {
                                    args.Add("comment");
                                    args.Add(voucher.Comment);
                                }
                                var cmd = connection.CreateCommandAndParameters("/ip/hotspot/user/add", args.ToArray());
                                var resList = cmd.ExecuteList();
                                var id = (resList != null && resList.Any()) ? (resList.First().Words.TryGetValue(".id", out var idVal) ? idVal : voucher.Username) : voucher.Username;
                                voucher.MarkAsSynced(id);
                            }
                            else
                            {
                                var args = new List<string> { "username", voucher.Username };
                                if (!string.IsNullOrEmpty(voucher.EffectivePassword))
                                {
                                    args.Add("password");
                                    args.Add(voucher.EffectivePassword);
                                }
                                if (!string.IsNullOrEmpty(voucher.Comment))
                                {
                                    args.Add("comment");
                                    args.Add(voucher.Comment);
                                }
                                var cmd = connection.CreateCommandAndParameters("/user-manager/user/add", args.ToArray());
                                var resList = cmd.ExecuteList();
                                var id = (resList != null && resList.Any()) ? (resList.First().Words.TryGetValue(".id", out var idVal) ? idVal : voucher.Username) : voucher.Username;

                                if (!string.IsNullOrEmpty(voucher.ProfileName))
                                {
                                    try
                                    {
                                        var createProfCmd = connection.CreateCommandAndParameters("/user-manager/user-profile/add", "user", voucher.Username, "profile", voucher.ProfileName);
                                        createProfCmd.ExecuteNonQuery();
                                    }
                                    catch {}
                                }

                                voucher.MarkAsSynced(id);
                            }
                        }
                        catch (Exception ex)
                        {
                            voucher.MarkAsFailed(ex.Message);
                        }
                    }

                    await using var dbWrite = await _dbFactory.CreateDbContextAsync(cancellationToken);
                    foreach (var v in chunk)
                    {
                        dbWrite.Entry(v).State = EntityState.Modified;
                    }
                    await dbWrite.SaveChangesAsync(cancellationToken);

                    progress?.Report((i + chunk.Count, activeVouchers.Count));
                }
            }
        }

        private static long ParseDurationToSeconds(string duration)
        {
            if (string.IsNullOrWhiteSpace(duration)) return 0;
            duration = duration.Trim().ToLowerInvariant();
            long totalSeconds = 0;
            var numberStr = "";
            foreach (var c in duration)
            {
                if (char.IsDigit(c) || c == '.')
                {
                    numberStr += c;
                }
                else if (numberStr.Length > 0 && c != ' ')
                {
                    if (double.TryParse(numberStr, out var num))
                    {
                        numberStr = "";
                        switch (c)
                        {
                            case 'w': totalSeconds += (long)(num * 7 * 24 * 3600); break;
                            case 'd': totalSeconds += (long)(num * 24 * 3600); break;
                            case 'h': totalSeconds += (long)(num * 3600); break;
                            case 'm': totalSeconds += (long)(num * 60); break;
                            case 's': totalSeconds += (long)num; break;
                        }
                    }
                }
            }
            return totalSeconds;
        }

        private static long ParseTransferToBytes(string transfer)
        {
            if (string.IsNullOrWhiteSpace(transfer)) return 0;
            transfer = transfer.Trim().ToUpperInvariant();
            var numberStr = "";
            foreach (var c in transfer)
            {
                if (char.IsDigit(c) || c == '.')
                {
                    numberStr += c;
                }
                else if (c != ' ')
                {
                    if (double.TryParse(numberStr, out var num))
                    {
                        switch (c)
                        {
                            case 'T': return (long)(num * 1024L * 1024 * 1024 * 1024);
                            case 'G': return (long)(num * 1024L * 1024 * 1024);
                            case 'M': return (long)(num * 1024L * 1024);
                            case 'K': return (long)(num * 1024L);
                            case 'B': return (long)num;
                        }
                    }
                    break;
                }
            }
            if (double.TryParse(numberStr, out var plainNum)) return (long)plainNum;
            return 0;
        }

        private string? FindUserManagerDbPath(string host, string user, string pass)
        {
            var candidates = new[]
            {
                "disk1/user-manager/sqldb",
                "user-manager/sqldb",
                "flash/user-manager/sqldb",
                "userman1/sqldb",
                "disk1/userman1/sqldb"
            };

            var found = new List<(string path, int userCount)>();

            foreach (var path in candidates)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"probe_sqldb_{Guid.NewGuid()}");
                try
                {
                    _logger.LogInformation("🔄 [FindUserManagerDbPath] Probing candidate path: ftp://{Host}/{Path} ...", host, path);
                    FtpDownload($"ftp://{host}/{path}", tempPath, user, pass);
                    if (File.Exists(tempPath))
                    {
                        using (var conn = new SqliteConnection($"Data Source={tempPath}"))
                        {
                            conn.Open();
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "SELECT COUNT(*) FROM [user];";
                            var count = Convert.ToInt32(cmd.ExecuteScalar());
                            _logger.LogInformation("✅ [FindUserManagerDbPath] Candidate {Path} downloaded successfully. User count: {Count}", path, count);
                            found.Add((path, count));
                        }
                        SqliteConnection.ClearAllPools();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("❌ [FindUserManagerDbPath] Candidate {Path} skipped: {Message}", path, ex.Message);
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch {}
                }
            }

            if (found.Count > 0)
            {
                // Prefer the database with the most users
                var best = found.OrderByDescending(x => x.userCount).First();
                _logger.LogInformation("🎯 [FindUserManagerDbPath] Selected database path: {Path} with {Count} users.", best.path, best.userCount);
                return best.path;
            }

            _logger.LogWarning("⚠️ [FindUserManagerDbPath] No candidate paths succeeded or contained tables. Falling back to dynamic scanning...");

            var foundDatabases = new List<(string path, long size)>();
            ScanDirectory($"ftp://{host}/", "", user, pass, foundDatabases);
            
            if (foundDatabases.Count == 0) return null;
            
            // Sort by file size descending to pick the largest sqldb (which is the active user-manager database)
            return foundDatabases.OrderByDescending(db => db.size).First().path;
        }

        private static void ScanDirectory(string ftpBaseUrl, string currentPath, string user, string pass, List<(string path, long size)> foundDatabases)
        {
            try
            {
                var url = ftpBaseUrl + currentPath;
                if (!url.EndsWith("/")) url += "/";

                var req = (FtpWebRequest)WebRequest.Create(url);
                req.Method = WebRequestMethods.Ftp.ListDirectory;
                req.Credentials = new NetworkCredential(user, pass);
                req.UsePassive = true; req.KeepAlive = false; req.Timeout = 10000;

                var items = new List<string>();

                using (var resp = (FtpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                using (var sr = new StreamReader(stream))
                {
                    string line;
                    while ((line = sr.ReadLine()!) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var name = line.Trim();
                        if (name == "." || name == "..") continue;
                        
                        if (name.Contains("/"))
                            name = name.Substring(name.LastIndexOf('/') + 1);
                        items.Add(name);
                    }
                }

                foreach (var item in items)
                {
                    if (string.Equals(item, "sqldb", StringComparison.OrdinalIgnoreCase))
                    {
                        var fullPath = currentPath + item;
                        var size = GetFtpFileSize(ftpBaseUrl + fullPath, user, pass);
                        foundDatabases.Add((fullPath, size));
                    }
                    else
                    {
                        if (!item.Contains(".") && 
                            item != "pub" && 
                            !item.EndsWith(".tar") && 
                            !item.EndsWith(".backup") && 
                            !item.EndsWith(".clean") && 
                            !item.EndsWith(".tmp") && 
                            !item.EndsWith("-wal") && 
                            !item.EndsWith("-shm"))
                        {
                            var subPath = currentPath + item + "/";
                            ScanDirectory(ftpBaseUrl, subPath, user, pass, foundDatabases);
                        }
                    }
                }
            }
            catch
            {
                // Ignore directory listing failures
            }
        }

        private static long GetFtpFileSize(string ftpUrl, string user, string pass)
        {
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Method = WebRequestMethods.Ftp.GetFileSize;
                req.Credentials = new NetworkCredential(user, pass);
                req.UsePassive = true; req.KeepAlive = false; req.Timeout = 5000;

                using var resp = (FtpWebResponse)req.GetResponse();
                return resp.ContentLength;
            }
            catch
            {
                return 0;
            }
        }

        public async Task DownloadAndCacheDbAsync(Guid routerId, CancellationToken cancellationToken = default)
        {
            string tempMain = Path.Combine(Path.GetTempPath(), $"sqldb_{Guid.NewGuid()}.tmp");
            string tempWal = tempMain + "-wal";
            string tempShm = tempMain + "-shm";
            string cleanDb = tempMain + ".clean";

            try
            {
                await using var platformDb = await _platformDbFactory.CreateDbContextAsync(cancellationToken);
                var router = await platformDb.Routers.FirstOrDefaultAsync(r => r.Id == routerId, cancellationToken);
                if (router == null) return;

                var pass = "";
                if (!string.IsNullOrWhiteSpace(router.EncryptedPassword))
                    pass = _secureStorageService.Decrypt(router.EncryptedPassword);

                bool downloadSucceeded = false;
                string baseRemote = "";

                // 1.0 Try cached path
                if (_routerDbPathCache.TryGetValue(routerId, out var cachedPath) && !string.IsNullOrEmpty(cachedPath))
                {
                    try
                    {
                        FtpDownload($"ftp://{router.Host}/{cachedPath}", tempMain, router.Username, pass);
                        baseRemote = cachedPath;
                        downloadSucceeded = true;
                    }
                    catch (WebException)
                    {
                        _routerDbPathCache.TryRemove(routerId, out _);
                    }
                }

                if (!downloadSucceeded)
                {
                    // 1.1 Try scanning dynamically
                    var scannedPath = FindUserManagerDbPath(router.Host, router.Username, pass);
                    if (!string.IsNullOrEmpty(scannedPath))
                    {
                        try
                        {
                            FtpDownload($"ftp://{router.Host}/{scannedPath}", tempMain, router.Username, pass);
                            baseRemote = scannedPath;
                            downloadSucceeded = true;
                            _routerDbPathCache[routerId] = scannedPath;
                        }
                        catch (WebException) { }
                    }
                }

                if (!downloadSucceeded)
                {
                    // 1.2 Probe candidates
                    var possiblePaths = new[]
                    {
                        "user-manager/sqldb",
                        "disk1/user-manager/sqldb",
                        "userman1/sqldb",
                        "disk1/userman1/sqldb"
                    };

                    foreach (var path in possiblePaths)
                    {
                        try
                        {
                            FtpDownload($"ftp://{router.Host}/{path}", tempMain, router.Username, pass);
                            baseRemote = path;
                            downloadSucceeded = true;
                            _routerDbPathCache[routerId] = path;
                            break;
                        }
                        catch (WebException) { }
                    }
                }

                if (!downloadSucceeded)
                {
                    throw new FileNotFoundException("لم يتم العثور على قاعدة بيانات User Manager (sqldb) على الراوتر.");
                }

                // Download WAL/SHM
                FtpDownloadOptional($"ftp://{router.Host}/{baseRemote}-wal", tempWal, router.Username, pass);
                FtpDownloadOptional($"ftp://{router.Host}/{baseRemote}-shm", tempShm, router.Username, pass);

                // Validate SQLite format 3
                var magic = new byte[16];
                using (var fs = new FileStream(tempMain, FileMode.Open, FileAccess.Read))
                {
                    fs.Read(magic, 0, MagicHeaderSize < fs.Length ? MagicHeaderSize : (int)fs.Length);
                }
                var header = Encoding.ASCII.GetString(magic, 0, 15);
                if (header != "SQLite format 3")
                {
                    throw new InvalidDataException("Downloaded file is not a valid SQLite database!");
                }

                // Merge WAL/SHM
                using (var src = new SqliteConnection($"Data Source={tempMain}"))
                using (var dst = new SqliteConnection($"Data Source={cleanDb}"))
                {
                    src.Open();
                    dst.Open();
                    src.BackupDatabase(dst);
                }

                SqliteConnection.ClearAllPools();
                TryDelete(tempMain); TryDelete(tempWal); TryDelete(tempShm);
                TryDelete(tempMain + "-wal"); TryDelete(tempMain + "-shm");

                // Save to UserManagerCache
                SaveCleanDbCache(routerId, cleanDb);
            }
            finally
            {
                TryDelete(tempMain); TryDelete(tempWal); TryDelete(tempShm);
                TryDelete(cleanDb);
                TryDelete(tempMain + "-wal"); TryDelete(tempMain + "-shm");
            }
        }

        private const int MagicHeaderSize = 16;

        private static string GetFullExceptionMessage(Exception ex)
        {
            var msg = ex.Message;
            var inner = ex.InnerException;
            while (inner != null)
            {
                msg += $" -> {inner.Message}";
                inner = inner.InnerException;
            }
            return msg;
        }
    }
}
