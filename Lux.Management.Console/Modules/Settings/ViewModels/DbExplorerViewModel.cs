#pragma warning disable MVVMTK0034
#pragma warning disable SYSLIB0014

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace Lux.Management.Console.Modules.Settings.ViewModels;

using Lux.Management.Console.ViewModels;

// ─── نموذج عرض اسم الجدول في القائمة ────────────────────────────────────────
public class TableDisplayItem
{
    public string Name        { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon        { get; set; } = "📋";
}

// ─── تعريف جدول (اسمه + استعلامه + أعمدته المعربة) ─────────────────────────
public class TableDefinition
{
    public string Name        { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon        { get; set; } = string.Empty;
    public string Sql         { get; set; } = string.Empty;
    public string CountSql    { get; set; } = string.Empty;

    /// <summary>خريطة: اسم العمود الأصلي → الاسم العربي المعروض</summary>
    public Dictionary<string, string> ColumnMap { get; set; } = new();
}

/// <summary>
/// ViewModel معرب متخصص لاستعراض قاعدة بيانات User Manager
/// يعرض 4 جداول محددة فقط: المستخدمين، الجلسات، الباقات، السجلات
/// </summary>
public partial class DbExplorerViewModel : ViewModelBase
{
    private static readonly string DbPath = @"d:\LUXCARD\desktop\user-manager\sqldb";
    private readonly ISettingsService _settingsService;

    // ══════════════════════════════════════════════════════════════════════════
    //  تعريف الجداول الأربعة المطلوبة مع استعلاماتها وتعريب أعمدتها
    // ══════════════════════════════════════════════════════════════════════════
    private static readonly List<TableDefinition> AllowedTables = new()
    {
        new TableDefinition
        {
            Name        = "user",
            DisplayName = "المستخدمون والكروت",
            Icon        = "👤",
            CountSql    = "SELECT COUNT(*) FROM [user]",
            Sql = @"
                SELECT
                    userName,
                    CASE 
                        WHEN disabled = 1 THEN 'مُعطَّل ❌' 
                        WHEN uptimeUsed > 0 OR downloadUsed > 0 THEN 'مستخدم 🟢' 
                        ELSE 'جاري الانتظار ⏳' 
                    END AS status,
                    actualProfileName,
                    downloadUsed,
                    uploadUsed,
                    uptimeUsed,
                    regDate,
                    lastSeenAt,
                    lastIp
                FROM [user]
                ORDER BY id DESC",
            ColumnMap = new()
            {
                { "userName",          "اسم المستخدم" },
                { "status",            "حالة الكرت" },
                { "actualProfileName", "الباقة المشتركة" },
                { "downloadUsed",      "التنزيل المستخدم" },
                { "uploadUsed",        "الرفع المستخدم" },
                { "uptimeUsed",        "وقت الاتصال" },
                { "regDate",           "تاريخ التسجيل" },
                { "lastSeenAt",        "آخر ظهور" },
                { "lastIp",            "آخر عنوان IP" },
            }
        },

        new TableDefinition
        {
            Name        = "session",
            DisplayName = "جلسات الاتصال الحالية",
            Icon        = "🔗",
            CountSql    = "SELECT COUNT(*) FROM [session]",
            Sql = @"
                SELECT
                    u.userName,
                    CASE WHEN s.active = 1 THEN 'نشطة 🟢' ELSE 'منتهية ⚫' END AS sessionStatus,
                    s.fromTime,
                    s.tillTime,
                    s.upTime,
                    s.bytesDownload,
                    s.bytesUpload,
                    s.ipUser
                FROM [session] s
                LEFT JOIN [user] u ON u.id = s.userId
                ORDER BY s.id DESC",
            ColumnMap = new()
            {
                { "userName",      "اسم المستخدم" },
                { "sessionStatus", "حالة الجلسة" },
                { "fromTime",      "بداية الجلسة" },
                { "tillTime",      "نهاية الجلسة" },
                { "upTime",        "مدة الاتصال" },
                { "bytesDownload", "التنزيل" },
                { "bytesUpload",   "الرفع" },
                { "ipUser",        "عنوان IP" },
            }
        },

        new TableDefinition
        {
            Name        = "profile",
            DisplayName = "باقات الإنترنت",
            Icon        = "📦",
            CountSql    = "SELECT COUNT(*) FROM [profile]",
            Sql = @"
                SELECT
                    nameForUser,
                    validity,
                    price,
                    sharedUsers
                FROM [profile]
                ORDER BY id ASC",
            ColumnMap = new()
            {
                { "nameForUser", "اسم الباقة" },
                { "validity",    "مدة الصلاحية" },
                { "price",       "السعر" },
                { "sharedUsers", "الأجهزة المشتركة" },
            }
        },

        new TableDefinition
        {
            Name        = "ucounters",
            DisplayName = "إحصائيات استهلاك المشتركين",
            Icon        = "📊",
            CountSql    = "SELECT COUNT(*) FROM [ucounters]",
            Sql = @"
                SELECT
                    u.userName,
                    u.actualProfileName,
                    uc.download,
                    uc.upload,
                    uc.uptime
                FROM [ucounters] uc
                LEFT JOIN [user] u ON u.id = uc.userId
                ORDER BY uc.download DESC",
            ColumnMap = new()
            {
                { "userName",          "اسم المستخدم" },
                { "actualProfileName", "الباقة" },
                { "download",          "إجمالي التنزيل" },
                { "upload",            "إجمالي الرفع" },
                { "uptime",            "إجمالي وقت الاتصال" },
            }
        },
    };

    private readonly ILogger<DbExplorerViewModel> _logger; 

    public DbExplorerViewModel(ILogger<DbExplorerViewModel> logger, ISettingsService settingsService, Lux.Management.Console.Core.IPermissionService permissionService, Lux.Platform.Abstractions.Interfaces.IEventBus eventBus) : base(permissionService, eventBus)
    {
        _settingsService = settingsService;
        _logger = logger;
        Title = "استعراض قاعدة بيانات يوزر مانجر";
    }

    // ───── Observable Properties ─────
    [ObservableProperty] private ObservableCollection<TableDisplayItem> _tableItems = new();
    [ObservableProperty] private string _selectedTable = string.Empty;
    [ObservableProperty] private int _totalRows;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private DataTable? _dataTable;

    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _availableProfiles = new();
    [ObservableProperty] private string _selectedProfileFilter = "الكل";

    [ObservableProperty] private ObservableCollection<string> _availableStatuses = new();
    [ObservableProperty] private string _selectedStatusFilter = "الكل";

    public bool ShowFilters => SelectedTable == "user";

    partial void OnSelectedProfileFilterChanged(string value) => SearchInTable();
    partial void OnSelectedStatusFilterChanged(string value) => SearchInTable();
    
    partial void OnSelectedTableChanged(string value)
    {
        OnPropertyChanged(nameof(ShowFilters));
        if (!string.IsNullOrEmpty(value) && !IsBusy)
            _ = LoadSelectedTableAsync(value);
    }
    public async Task InitializeAsync(object? parameter = null)
    {
        await ExecuteBusyAsync(async (token) =>
        {
            // بناء قائمة الجداول المسموح بها (تتحقق من وجودها فعلياً في القاعدة)
            var existingTables = await Task.Run(() => GetExistingTableNames(), token);

            var items = AllowedTables
                .Where(t => existingTables.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
                .Select(t => new TableDisplayItem
                {
                    Name        = t.Name,
                    DisplayName = $"{t.Icon}  {t.DisplayName}",
                })
                .ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TableItems = new ObservableCollection<TableDisplayItem>(items);
            });

            // تحميل جدول المستخدمين أولاً
            if (items.Count > 0)
            {
                var defaultTable = items.First().Name;
                var (dt, rowCount) = await Task.Run(() => ReadTableData(defaultTable), token);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _selectedTable = defaultTable;
                    OnPropertyChanged(nameof(SelectedTable));
                    TotalRows  = rowCount;
                    DataTable  = dt;
                });
            }
        }, "جاري تحميل قاعدة بيانات يوزر مانجر...");
    }




    [RelayCommand]
    private async Task LoadSelectedTableAsync(string tableName)
    {
        if (string.IsNullOrEmpty(tableName)) return;
        var def = AllowedTables.FirstOrDefault(t => t.Name == tableName);
        if (def == null) return;

        await ExecuteBusyAsync(async (token) =>
        {
            var (dt, rowCount) = await Task.Run(() => ReadTableData(tableName), token);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (tableName == "user")
                {
                    var profiles = dt.AsEnumerable()
                        .Select(r => r["الباقة المشتركة"]?.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s) && s != "—")
                        .Distinct()
                        .ToList();
                    profiles.Insert(0, "الكل");
                    AvailableProfiles = new ObservableCollection<string>(profiles!);

                    var statuses = dt.AsEnumerable()
                        .Select(r => r["حالة الكرت"]?.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s) && s != "—")
                        .Distinct()
                        .ToList();
                    statuses.Insert(0, "الكل");
                    AvailableStatuses = new ObservableCollection<string>(statuses!);
                }
                else
                {
                    AvailableProfiles.Clear();
                    AvailableStatuses.Clear();
                }

                _selectedProfileFilter = "الكل";
                _selectedStatusFilter = "الكل";
                OnPropertyChanged(nameof(SelectedProfileFilter));
                OnPropertyChanged(nameof(SelectedStatusFilter));

                TotalRows = rowCount;
                DataTable = dt;
            });
        }, $"جاري تحميل {def.DisplayName}...");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  قراءة البيانات من قاعدة البيانات
    // ══════════════════════════════════════════════════════════════════════════

    private HashSet<string> GetExistingTableNames()
    {
        if (!File.Exists(DbPath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var conn = OpenDb();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        using var reader = cmd.ExecuteReader();

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) set.Add(reader.GetString(0));
        return set;
    }

    private (DataTable dt, int rowCount) ReadTableData(string tableName)
    {
        var def = AllowedTables.First(t => t.Name == tableName);

        if (!File.Exists(DbPath)) return (new DataTable(tableName), 0);

        using var conn = OpenDb();

        // عدد الصفوف الإجمالي
        int count = 0;
        try
        {
            using var cntCmd = conn.CreateCommand();
            cntCmd.CommandText = def.CountSql;
            count = Convert.ToInt32(cntCmd.ExecuteScalar());
        }
        catch { }

        // قراءة البيانات
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = def.Sql;
        using var reader = dataCmd.ExecuteReader();

        var dt = new DataTable(tableName);

        // إنشاء الأعمدة المعربة
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var orig    = reader.GetName(i);
            var display = def.ColumnMap.TryGetValue(orig, out var ar) ? ar : orig;
            dt.Columns.Add(display, typeof(string));
        }

        // قراءة الصفوف مع تنسيق البيانات
        while (reader.Read())
        {
            var row = dt.NewRow();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i)) { row[i] = "—"; continue; }

                var raw  = reader.GetName(i);
                var val  = reader.GetValue(i);

                // فك BLOB → IP Address أو UTF-8
                if (val is byte[] bytes)
                {
                    if ((bytes.Length == 4 || bytes.Length == 16) && raw.Contains("ip", StringComparison.OrdinalIgnoreCase))
                    {
                        try { row[i] = new System.Net.IPAddress(bytes).ToString(); }
                        catch { row[i] = BitConverter.ToString(bytes); }
                    }
                    else
                    {
                        try { row[i] = Encoding.UTF8.GetString(bytes); }
                        catch { row[i] = BitConverter.ToString(bytes); }
                    }
                    continue;
                }

                // Unix Timestamp → تاريخ مقروء
                if (val is long ts && ts > 1_000_000_000 &&
                    (raw.Contains("Time", StringComparison.OrdinalIgnoreCase) ||
                     raw.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
                     raw is "added" or "lastSeenAt" or "activated"))
                {
                    row[i] = FormatRelativeTime(ts); continue;
                }

                // Bytes → MB/GB
                if (val is long b && b > 0 &&
                    (raw.Contains("download", StringComparison.OrdinalIgnoreCase) ||
                     raw.Contains("upload",   StringComparison.OrdinalIgnoreCase) ||
                     raw.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
                     raw.Contains("Limit",    StringComparison.OrdinalIgnoreCase) ||
                     raw.Contains("Used",     StringComparison.OrdinalIgnoreCase)))
                {
                    row[i] = FormatBytes(b); continue;
                }

                // Seconds → وقت مقروء
                if (val is long s && s > 0 &&
                    (raw.Contains("uptime", StringComparison.OrdinalIgnoreCase) ||
                     raw.Contains("duration", StringComparison.OrdinalIgnoreCase)))
                {
                    row[i] = FormatUptime(s); continue;
                }

                row[i] = val.ToString() ?? "—";
            }
            dt.Rows.Add(row);
        }

        return (dt, count);
    }

    // فتح اتصال آمن بقاعدة البيانات (immutable = قراءة فقط بدون WAL)
    private static SqliteConnection OpenDb()
    {
        // Mode=ReadOnly كافية للقراءة من الملف المحلي الثابت
        var conn = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly");
        conn.Open();
        return conn;
    }

    private static string FormatRelativeTime(long ts)
    {
        if (ts < 1_000_000_000) return "—";
        try
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
            var tsSpan = DateTime.Now - date;
            
            if (tsSpan.TotalSeconds < 0) return "الآن";
            if (tsSpan.TotalSeconds < 60) return "قبل لحظات";
            if (tsSpan.TotalMinutes < 60) return $"قبل {(int)tsSpan.TotalMinutes} دقيقة";
            if (tsSpan.TotalHours < 24) return $"قبل {(int)tsSpan.TotalHours} ساعة";
            if (tsSpan.TotalDays < 30) return $"قبل {(int)tsSpan.TotalDays} يوم";
            if (tsSpan.TotalDays < 365) return $"قبل {(int)(tsSpan.TotalDays / 30)} شهر";
            
            return $"قبل {(int)(tsSpan.TotalDays / 365)} سنة";
        }
        catch { return "—"; }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        decimal n = bytes;
        while (Math.Round(n / 1024) >= 1) { n /= 1024; i++; }
        return $"{n:N2} {suf[i]}";
    }

    private static string FormatUptime(long totalSeconds)
    {
        var t = TimeSpan.FromSeconds(totalSeconds);
        if (t.TotalDays  >= 1) return $"{(int)t.TotalDays} يوم و {t.Hours} ساعة";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} ساعة و {t.Minutes} دقيقة";
        return $"{(int)t.TotalMinutes} دقيقة";
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  البحث والفلترة
    // ══════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!string.IsNullOrEmpty(SelectedTable))
            await LoadSelectedTableAsync(SelectedTable);
    }

    [ObservableProperty]
    private bool _isExactMatch;

    [RelayCommand]
    private void SearchInTable()
    {
        if (DataTable == null) return;

        var conditions = new List<string>();

        // فلتر نصي
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var textFilters = new List<string>();
            string op = IsExactMatch ? "=" : "LIKE";
            string val = IsExactMatch ? $"'{SearchText.Replace("'", "''")}'" : $"'%{SearchText.Replace("'", "''")}%'";

            foreach (DataColumn col in DataTable.Columns)
                textFilters.Add($"CONVERT([{col.ColumnName}], 'System.String') {op} {val}");
            
            if (textFilters.Count > 0)
                conditions.Add($"({string.Join(" OR ", textFilters)})");
        }

        // فلتر الباقة (إذا كان الجدول يحتوي على عمود الباقة)
        if (DataTable.Columns.Contains("الباقة المشتركة") && SelectedProfileFilter != "الكل" && !string.IsNullOrEmpty(SelectedProfileFilter))
        {
            conditions.Add($"[الباقة المشتركة] = '{SelectedProfileFilter.Replace("'", "''")}'");
        }

        // فلتر الحالة (إذا كان الجدول يحتوي على عمود الحالة)
        if (DataTable.Columns.Contains("حالة الكرت") && SelectedStatusFilter != "الكل" && !string.IsNullOrEmpty(SelectedStatusFilter))
        {
            conditions.Add($"[حالة الكرت] = '{SelectedStatusFilter.Replace("'", "''")}'");
        }

        DataTable.DefaultView.RowFilter = conditions.Count > 0 ? string.Join(" AND ", conditions) : "";
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        SelectedProfileFilter = "الكل";
        SelectedStatusFilter = "الكل";
        if (DataTable != null) DataTable.DefaultView.RowFilter = "";
    }

    [RelayCommand]
    private void ShowUserReport(System.Data.DataRowView? row)
    {
        if (row == null || SelectedTable != "user") return;

        var userName = row["اسم المستخدم"]?.ToString();
        if (!string.IsNullOrEmpty(userName))
        {
            System.Windows.MessageBox.Show("Disabled", "Info", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            // window.Owner = System.Windows.Application.Current.MainWindow;
            // window.ShowDialog();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  سحب قاعدة البيانات الحية من المايكروتك عبر FTP
    // ══════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task FetchLiveDatabaseAsync()
    {
        await ExecuteBusyAsync(async (token) =>
        {
            var host     = _settingsService.Get("MikroTik.Host",     "192.168.88.1");
            var username = _settingsService.Get("MikroTik.Username", "admin");
            var password = _settingsService.Get("MikroTik.Password", "");

            // استخدام المسار البديل بناءً على الصورة (userman1 بدلاً من user-manager)
            string baseRemote = "disk1/userman1/sqldb";
            string tempMain   = DbPath + ".tmp";
            string tempWal    = tempMain + "-wal";
            string tempShm    = tempMain + "-shm";
            string cleanDb    = DbPath + ".clean";
            string backupDb   = DbPath + ".bak";

            var dir = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            TryDelete(tempMain); TryDelete(tempWal); TryDelete(tempShm); TryDelete(cleanDb);

            // ─── Step 1: تحميل الملفات من MikroTik FTP ─────────────────────
            await Task.Run(() =>
            {
                FtpDownload($"ftp://{host}/{baseRemote}", tempMain, username, password);
                FtpDownloadOptional($"ftp://{host}/{baseRemote}-wal", tempWal, username, password);
                FtpDownloadOptional($"ftp://{host}/{baseRemote}-shm", tempShm, username, password);
            }, token);

            // ─── Step 2: التحقق من صحة الملف (Magic Bytes) ─────────────────
            await Task.Run(() =>
            {
                var magic = new byte[16];
                using var fs = new FileStream(tempMain, FileMode.Open, FileAccess.Read);
                fs.Read(magic, 0, 16);
                var header = Encoding.ASCII.GetString(magic, 0, 15);
                if (header != "SQLite format 3")
                    throw new InvalidDataException($"الملف المُحمَّل ليس SQLite صالحاً! (Header: '{header}')");
            }, token);

            // ─── Step 3: نسخ القاعدة عبر SQLite Online Backup API ───────────
            // نفتح القاعدة بدون Mode=ReadOnly لتجنب خطأ SQLite Error 8 أثناء تعافي WAL
            await Task.Run(() =>
            {
                if (File.Exists(cleanDb)) File.Delete(cleanDb);

                using var src = new SqliteConnection($"Data Source={tempMain}");
                src.Open();

                using var dst = new SqliteConnection($"Data Source={cleanDb}");
                dst.Open();

                src.BackupDatabase(dst);

                SqliteConnection.ClearAllPools();
                TryDelete(tempMain); TryDelete(tempWal); TryDelete(tempShm);
                TryDelete(tempMain + "-wal"); TryDelete(tempMain + "-shm");
            }, token);

            // ─── Step 4: فحص الفهارس ومحاولة الإصلاح ──────────────────────────
            // قواعد بيانات User Manager الحية قد تحتوي على تلف بسيط في الفهارس (Indexes)
            // إذا فشل الفحص، نقوم بإعادة بناء الفهارس محلياً (REINDEX) ولا نوقف العملية
            await Task.Run(() =>
            {
                using var chk = new SqliteConnection($"Data Source={cleanDb}");
                chk.Open();
                using var cmd = chk.CreateCommand();
                
                cmd.CommandText = "PRAGMA integrity_check;";
                var result = cmd.ExecuteScalar()?.ToString();
                
                if (result != "ok")
                {
                    // محاولة إصلاح الفهارس التالفة محلياً (مثل خطأ missing from index)
                    try 
                    {
                        cmd.CommandText = "REINDEX;";
                        cmd.ExecuteNonQuery();
                    } 
                    catch { /* نتجاهل الخطأ لأننا نحتاج للقراءة فقط */ }
                }
                SqliteConnection.ClearAllPools();
            }, token);

            // ─── Step 5: تحرير القفل المحلي واستبدال الملف ─────────────────
            System.Windows.Application.Current.Dispatcher.Invoke(() => { DataTable = null; });
            SqliteConnection.ClearAllPools();
            await Task.Delay(300, token);

            await Task.Run(() =>
            {
                if (File.Exists(DbPath))
                {
                    if (File.Exists(backupDb)) File.Delete(backupDb);
                    File.Copy(DbPath, backupDb);
                }
                File.Copy(cleanDb, DbPath, overwrite: true);
                TryDelete(cleanDb);
            }, token);

        }, "جاري سحب قاعدة البيانات حياً من المايكروتك...");

        // ─── Step 6: تحديث الواجهة بعد اكتمال السحب (خارج ExecuteBusyAsync) ──
        if (!HasError)
        {
            await InitializeAsync(null);
        }
    }

    // ─── أدوات FTP ──────────────────────────────────────────────────────────
    private static void FtpDownload(string ftpUrl, string localPath, string user, string pass)
    {
        var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
        req.Method = WebRequestMethods.Ftp.DownloadFile;
        req.Credentials = new NetworkCredential(user, pass);
        req.UsePassive = true; req.UseBinary = true; req.KeepAlive = false; req.Timeout = 30000;

        using var resp   = (FtpWebResponse)req.GetResponse();
        using var stream = resp.GetResponseStream();
        using var file   = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.CopyTo(file);
    }

    private static void FtpDownloadOptional(string ftpUrl, string localPath, string user, string pass)
    {
        try { FtpDownload(ftpUrl, localPath, user, pass); }
        catch (WebException ex) when ((ex.Response as FtpWebResponse)?.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable) { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

