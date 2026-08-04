using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Data.Sqlite;
using MikroTikVoucherPrinter.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.Views;

public partial class UserReportWindow : Window
{
    private readonly string _userName;
    private string _dbPath;
    private string _routerName = "—";
    private readonly VoucherDto? _voucher;
    
    // بيانات التقرير الشاملة
    private string _profile = "—";
    private string _status = "—";
    private string _regDate = "—";
    private string _firstUse = "—";
    private string _expiryDate = "—";
    private string _validityHours = "—";
    private string _lastSeen = "—";
    private string _download = "0 MB";
    private string _upload = "0 MB";
    private string _remainingQuota = "—";
    private string _uptime = "0 دقيقة";
    private string _sessionsCount = "0";
    private string _lastIp = "—";
    private string _lastMac = "—";
    private string _lastVlan = "—";
    private string _ips = "لا يوجد";
    private string _macs = "لا يوجد";
    
    private readonly List<UserSessionReportItem> _sessionsList = new();
    private readonly Dictionary<string, string> _leases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _uniqueMacs = new();

    public UserReportWindow(string userName) : this(userName, @"d:\LUXCARD\desktop\user-manager\sqldb")
    {
    }

    public UserReportWindow(string userName, string dbPath, string routerName = "—", Dictionary<string, string>? leases = null, VoucherDto? voucher = null)
    {
        InitializeComponent();
        QuestPDF.Settings.License = LicenseType.Community;
        
        _userName = userName;
        TxtUserName.Text = userName;
        _dbPath = dbPath;
        _routerName = routerName;
        _voucher = voucher;

        if (_voucher != null)
        {
            if (!string.IsNullOrWhiteSpace(_voucher.Profile)) _profile = _voucher.Profile;
            _status = _voucher.StatusText;
            _regDate = _voucher.CreatedAt.ToString("yyyy/MM/dd HH:mm");
            if (_voucher.DownloadUsedBytes.HasValue) _download = FormatBytes(_voucher.DownloadUsedBytes.Value);
            if (_voucher.UploadUsedBytes.HasValue) _upload = FormatBytes(_voucher.UploadUsedBytes.Value);
            if (_voucher.UsedAt.HasValue) _firstUse = _voucher.UsedAt.Value.ToString("yyyy/MM/dd HH:mm");
            if (!string.IsNullOrEmpty(_voucher.QuotaSummaryText)) _remainingQuota = _voucher.QuotaSummaryText;
        }

        if (leases != null)
        {
            foreach (var kv in leases)
            {
                _leases[kv.Key] = kv.Value;
            }
        }

        LoadData();
    }

    private void LoadData()
    {
        long? userId = null;
        long totalDl = 0, totalUl = 0, totalUp = 0;
        long limitBytes = 0;
        string validityStr = string.Empty;
        long? tillTimeTs = null;

        if (File.Exists(_dbPath))
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
                conn.Open();

                // 1. معلومات المستخدم الأساسية
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, actualProfileName, disabled, uptimeUsed, downloadUsed, regDate, lastSeenAt FROM [user] WHERE CAST(userName AS TEXT) = @u LIMIT 1";
                    cmd.Parameters.AddWithValue("@u", _userName); 
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        userId = reader.GetInt64(0);
                        if (!reader.IsDBNull(1) && !string.IsNullOrWhiteSpace(reader.GetString(1))) 
                            _profile = reader.GetString(1);
                        
                        int disabled = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        long upUsed = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                        long dlUsed = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                        
                        if (disabled == 1) _status = "مُعطَّل ❌";
                        else if (upUsed > 0 || dlUsed > 0) _status = "مُستخدم 🟢";
                        else _status = "غير مستخدم ⏳";

                        if (!reader.IsDBNull(5)) _regDate = FormatTimestamp(reader.GetInt64(5));
                        if (!reader.IsDBNull(6)) _lastSeen = FormatTimestamp(reader.GetInt64(6));
                    }
                }

                // 2. الإحصائيات من ucounters
                if (userId.HasValue)
                {
                    using var statCmd = conn.CreateCommand();
                    statCmd.CommandText = "SELECT download, upload, uptime FROM [ucounters] WHERE userId = @uid";
                    statCmd.Parameters.AddWithValue("@uid", userId.Value);
                    using var statReader = statCmd.ExecuteReader();
                    while (statReader.Read())
                    {
                        if (!statReader.IsDBNull(0)) totalDl += statReader.GetInt64(0);
                        if (!statReader.IsDBNull(1)) totalUl += statReader.GetInt64(1);
                        if (!statReader.IsDBNull(2)) totalUp += statReader.GetInt64(2);
                    }
                    if (totalDl > 0) _download = FormatBytes(totalDl);
                    if (totalUl > 0) _upload = FormatBytes(totalUl);
                    if (totalUp > 0) _uptime = FormatUptime(totalUp);

                    // 3. الجلسات (العناوين والماكات والتفاصيل)
                    using var sessCmd = conn.CreateCommand();
                    sessCmd.CommandText = "SELECT id, fromTime, tillTime, upTime, bytesDownload, bytesUpload, CAST(nasPortId AS TEXT), ipUser, callingStationId FROM [session] WHERE userId = @uid ORDER BY fromTime DESC";
                    sessCmd.Parameters.AddWithValue("@uid", userId.Value);
                    using var sessReader = sessCmd.ExecuteReader();
                    
                    int sCount = 0;
                    long minFromTs = long.MaxValue;
                    var ipSet = new HashSet<string>();
                    var macSet = new HashSet<string>();
                    _sessionsList.Clear();
                    
                    while (sessReader.Read())
                    {
                        sCount++;
                        var item = new UserSessionReportItem();
                        
                        // Start Time
                        if (!sessReader.IsDBNull(1))
                        {
                            var fromTs = sessReader.GetInt64(1);
                            if (fromTs > 0 && fromTs < minFromTs) minFromTs = fromTs;
                            item.StartTimeFormatted = DateTimeOffset.FromUnixTimeSeconds(fromTs).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
                        }
                        else
                        {
                            item.StartTimeFormatted = "—";
                        }
                        
                        // VLAN/Port
                        if (!sessReader.IsDBNull(6))
                        {
                            item.Vlan = sessReader.GetString(6);
                        }
                        else
                        {
                            item.Vlan = "—";
                        }
                        
                        // IP Address
                        if (!sessReader.IsDBNull(7))
                        {
                            var ipBytes = sessReader.GetValue(7) as byte[];
                            if (ipBytes != null)
                            {
                                if (ipBytes.Length == 4 || ipBytes.Length == 16)
                                    item.IpAddress = new System.Net.IPAddress(ipBytes).ToString();
                                else
                                    item.IpAddress = Encoding.UTF8.GetString(ipBytes);
                            }
                            else
                            {
                                item.IpAddress = sessReader.GetString(7);
                            }
                            if (!string.IsNullOrEmpty(item.IpAddress)) ipSet.Add(item.IpAddress);
                        }
                        else
                        {
                            item.IpAddress = "—";
                        }
                        
                        // MAC Address
                        if (!sessReader.IsDBNull(8))
                        {
                            var macBytes = sessReader.GetValue(8) as byte[];
                            if (macBytes != null)
                            {
                                item.MacAddress = string.Join(":", macBytes.Select(b => b.ToString("X2")));
                                try
                                {
                                    var asciiMac = Encoding.ASCII.GetString(macBytes).Trim();
                                    if (asciiMac.Length >= 12)
                                        item.MacAddress = asciiMac;
                                }
                                catch {}
                            }
                            else
                            {
                                item.MacAddress = sessReader.GetString(8);
                            }
                            if (!string.IsNullOrEmpty(item.MacAddress)) macSet.Add(item.MacAddress);
                        }
                        else
                        {
                            item.MacAddress = "—";
                        }
                        
                        // Uptime/Duration
                        if (!sessReader.IsDBNull(3))
                        {
                            long durationSec = sessReader.GetInt64(3);
                            item.DurationFormatted = FormatUptime(durationSec);
                        }
                        else
                        {
                            item.DurationFormatted = "—";
                        }
                        
                        // Download/Upload bytes
                        if (!sessReader.IsDBNull(4))
                        {
                            item.DownloadFormatted = FormatBytes(sessReader.GetInt64(4));
                        }
                        else
                        {
                            item.DownloadFormatted = "0 B";
                        }
                        if (!sessReader.IsDBNull(5))
                        {
                            item.UploadFormatted = FormatBytes(sessReader.GetInt64(5));
                        }
                        else
                        {
                            item.UploadFormatted = "0 B";
                        }
                        
                        // احتساب أول جلسة للجلسة الأحدث
                        if (sCount == 1)
                        {
                            _lastIp = item.IpAddress != "—" ? item.IpAddress : _lastIp;
                            _lastMac = item.MacAddress != "—" ? item.MacAddress : _lastMac;
                            _lastVlan = item.Vlan != "—" ? item.Vlan : _lastVlan;
                        }

                        _sessionsList.Add(item);
                    }

                    _sessionsCount = $"{sCount} جلسة";
                    _uniqueMacs.Clear();
                    if (ipSet.Count > 0) _ips = string.Join("\n", ipSet);
                    if (macSet.Count > 0)
                    {
                        _uniqueMacs.AddRange(macSet);
                        _macs = string.Join("\n", macSet);
                    }

                    if (minFromTs < long.MaxValue)
                    {
                        _firstUse = DateTimeOffset.FromUnixTimeSeconds(minFromTs).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
                    }
                }

                // 4. استعلام الصلاحية والحدود (validity & limitations) من الباقة
                if (userId.HasValue || (!string.IsNullOrEmpty(_profile) && _profile != "—"))
                {
                    try
                    {
                        using var profCmd = conn.CreateCommand();
                        profCmd.CommandText = @"
                            SELECT 
                                pr.validity,
                                MAX(COALESCE(lim.downloadLimit, 0)) AS dlLimit,
                                MAX(COALESCE(lim.uploadLimit, 0)) AS ulLimit,
                                MAX(COALESCE(lim.transferLimit, 0)) AS trLimit
                            FROM [profile] pr
                            LEFT JOIN [pparts] pp ON pp.profileId = pr.id
                            LEFT JOIN [limitation] lim ON lim.id = pp.limitId
                            WHERE pr.name = @p OR pr.nameForUser = @p OR (@uid IS NOT NULL AND pr.id IN (SELECT profileId FROM userprofile WHERE userId = @uid))
                            GROUP BY pr.id
                            LIMIT 1";
                        profCmd.Parameters.AddWithValue("@p", _profile);
                        profCmd.Parameters.AddWithValue("@uid", (object?)userId ?? DBNull.Value);

                        using var profReader = profCmd.ExecuteReader();
                        if (profReader.Read())
                        {
                            if (!profReader.IsDBNull(0)) validityStr = profReader.GetValue(0)?.ToString() ?? "";
                            long dlLim = profReader.IsDBNull(1) ? 0 : profReader.GetInt64(1);
                            long ulLim = profReader.IsDBNull(2) ? 0 : profReader.GetInt64(2);
                            long trLim = profReader.IsDBNull(3) ? 0 : profReader.GetInt64(3);

                            if (trLim > 0) limitBytes = trLim;
                            else if (dlLim > 0) limitBytes = dlLim;
                            else if (ulLim > 0) limitBytes = ulLim;
                        }
                    }
                    catch { }

                    // استعلام tillTime المباشر من userprofile إن وجد
                    if (userId.HasValue)
                    {
                        try
                        {
                            using var upCmd = conn.CreateCommand();
                            upCmd.CommandText = "SELECT tillTime FROM userprofile WHERE userId = @uid AND tillTime > 0 ORDER BY id DESC LIMIT 1";
                            upCmd.Parameters.AddWithValue("@uid", userId.Value);
                            var tillObj = upCmd.ExecuteScalar();
                            if (tillObj != null && tillObj != DBNull.Value)
                            {
                                tillTimeTs = Convert.ToInt64(tillObj);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error querying report db: {ex.Message}");
            }
        }

        // 5. حساب تاريخ الانتهاء وساعات الصلاحية المتبقية
        double hours = ParseTimeSpanToHours(validityStr);

        if (tillTimeTs.HasValue && tillTimeTs.Value > 0)
        {
            var expiry = DateTimeOffset.FromUnixTimeSeconds(tillTimeTs.Value).LocalDateTime;
            _expiryDate = expiry.ToString("yyyy/MM/dd HH:mm");
            double remHours = (expiry - DateTime.Now).TotalHours;
            if (remHours > 0)
            {
                int wholeH = (int)remHours;
                int d = wholeH / 24;
                int h = wholeH % 24;
                _validityHours = d > 0 ? $"{wholeH} ساعة متبقية ({d} يوم و {h} ساعة)" : $"{wholeH} ساعة متبقية";
            }
            else
            {
                _validityHours = $"منتهي (تاريخ الانتهاء: {expiry:yyyy/MM/dd})";
            }
        }
        else if (hours > 0)
        {
            DateTime? firstUseDate = null;
            if (DateTime.TryParse(_firstUse, out var parsedFirst))
            {
                firstUseDate = parsedFirst;
            }
            else if (_voucher != null && _voucher.UsedAt.HasValue)
            {
                firstUseDate = _voucher.UsedAt.Value;
            }

            if (firstUseDate.HasValue)
            {
                var expiry = firstUseDate.Value.AddHours(hours);
                _expiryDate = expiry.ToString("yyyy/MM/dd HH:mm");
                double remHours = (expiry - DateTime.Now).TotalHours;
                if (remHours > 0)
                {
                    int wholeH = (int)remHours;
                    int d = wholeH / 24;
                    int h = wholeH % 24;
                    _validityHours = d > 0 ? $"{wholeH} ساعة متبقية ({d} يوم و {h} ساعة)" : $"{wholeH} ساعة متبقية";
                }
                else
                {
                    _validityHours = $"منتهي (إجمالي الصلاحية: {(int)hours} ساعة)";
                }
            }
            else
            {
                _expiryDate = "لم يُفعل بعد (تحسب عند أول استخدام)";
                _validityHours = $"{(int)hours} ساعة (تبدأ من أول اتصال)";
            }
        }

        // 6. حساب الرصيد المتبقي
        if (limitBytes == 0 && _voucher != null && _voucher.QuotaLimitBytes.HasValue && _voucher.QuotaLimitBytes.Value > 0)
        {
            limitBytes = _voucher.QuotaLimitBytes.Value;
        }

        if (limitBytes > 0)
        {
            long used = totalDl + totalUl;
            if (used == 0 && _voucher != null && _voucher.QuotaUsedBytes.HasValue) 
                used = _voucher.QuotaUsedBytes.Value;

            long rem = limitBytes - used;
            if (rem < 0) rem = 0;
            _remainingQuota = $"{FormatBytes(rem)} (من أصل {FormatBytes(limitBytes)})";
        }
        else
        {
            _remainingQuota = "غير محدود (مفتوح)";
        }

        // فحص العناوين من جدول الـ Leases إذا لم توجد في الجلسات
        if ((_lastMac == "—" || string.IsNullOrEmpty(_lastMac)) && _uniqueMacs.Count > 0)
        {
            _lastMac = _uniqueMacs.First();
        }
        if ((_lastIp == "—" || string.IsNullOrEmpty(_lastIp)) && _leases.Count > 0)
        {
            if (!string.IsNullOrEmpty(_lastMac) && _leases.TryGetValue(_lastMac, out var leasedIp))
            {
                _lastIp = leasedIp;
            }
            else
            {
                _lastIp = _leases.Values.FirstOrDefault() ?? "—";
            }
        }

        // تحديث عناصر الواجهة
        TxtProfile.Text = _profile;
        TxtStatus.Text = _status;
        TxtRegDate.Text = _regDate;
        TxtFirstUse.Text = _firstUse;
        TxtExpiryDate.Text = _expiryDate;
        TxtValidityHours.Text = _validityHours;
        TxtLastSeen.Text = _lastSeen;
        TxtDownload.Text = _download;
        TxtUpload.Text = _upload;
        TxtRemainingQuota.Text = _remainingQuota;
        TxtUptime.Text = _uptime;
        TxtSessionsCount.Text = _sessionsCount;
        TxtLastIp.Text = _lastIp;
        TxtLastMac.Text = _lastMac;
        TxtLastVlan.Text = _lastVlan;
        TxtIPs.Text = _ips;
        TxtMACs.Text = _macs;

        SessionsDataGrid.ItemsSource = null;
        SessionsDataGrid.ItemsSource = _sessionsList;
    }

    private double ParseTimeSpanToHours(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        val = val.Trim();

        // 1. أرقام بالثواني المباشرة (مثل 86400 أو 2592000)
        if (long.TryParse(val, out long totalSeconds) && totalSeconds > 0)
        {
            return totalSeconds / 3600.0;
        }

        // 2. تنسيق TimeSpan القياسي (مثل 01:00:00 أو 1.02:00:00)
        if (TimeSpan.TryParse(val, out TimeSpan ts))
        {
            return ts.TotalHours;
        }

        // 3. تنسيقات الميكروتك المرنة (مثل 3d, 3d00:00:00, 1w2d, 1d2h, 12h, 4w)
        try
        {
            double totalH = 0;
            string s = val.ToLower().Replace(" ", "");

            // استخراج الأسابيع w
            int wIndex = s.IndexOf('w');
            if (wIndex >= 0)
            {
                string wPart = s.Substring(0, wIndex);
                if (double.TryParse(wPart, out double w)) totalH += w * 168.0;
                s = s.Substring(wIndex + 1);
            }

            // استخراج الأيام d
            int dIndex = s.IndexOf('d');
            if (dIndex >= 0)
            {
                string dPart = s.Substring(0, dIndex);
                if (double.TryParse(dPart, out double d)) totalH += d * 24.0;
                s = s.Substring(dIndex + 1);
            }

            // استخراج الساعات h
            int hIndex = s.IndexOf('h');
            if (hIndex >= 0)
            {
                string hPart = s.Substring(0, hIndex);
                if (double.TryParse(hPart, out double h)) totalH += h;
                s = s.Substring(hIndex + 1);
            }

            // استخراج الدقائق m
            int mIndex = s.IndexOf('m');
            if (mIndex >= 0)
            {
                string mPart = s.Substring(0, mIndex);
                if (double.TryParse(mPart, out double m)) totalH += m / 60.0;
                s = s.Substring(mIndex + 1);
            }

            // أي وقت متبقي بتنسيق HH:mm:ss بعد الأيام (مثل 00:00:00 في 3d00:00:00)
            if (!string.IsNullOrEmpty(s) && TimeSpan.TryParse(s, out TimeSpan remTs))
            {
                totalH += remTs.TotalHours;
            }

            if (totalH > 0) return totalH;
        }
        catch { }

        return 0;
    }

    private string FormatTimestamp(long ts)
    {
        if (ts < 1000000000) return "—";
        try
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
            var rel = GetRelativeTime(ts);
            return $"{date:yyyy/MM/dd HH:mm} ({rel})";
        }
        catch { return "—"; }
    }

    private string GetRelativeTime(long ts)
    {
        if (ts < 1000000000) return "—";
        try
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
            var tsSpan = DateTime.Now - date;
            
            if (tsSpan.TotalSeconds < 0) return "الآن";
            
            if (tsSpan.TotalSeconds < 60)
                return "قبل لحظات";
            if (tsSpan.TotalMinutes < 60)
                return $"قبل {(int)tsSpan.TotalMinutes} دقيقة";
            if (tsSpan.TotalHours < 24)
                return $"قبل {(int)tsSpan.TotalHours} ساعة";
            if (tsSpan.TotalDays < 30)
                return $"قبل {(int)tsSpan.TotalDays} يوم";
            if (tsSpan.TotalDays < 365)
                return $"قبل {(int)(tsSpan.TotalDays / 30)} شهر";
            
            return $"قبل {(int)(tsSpan.TotalDays / 365)} سنة";
        }
        catch { return "—"; }
    }

    private string FormatBytes(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        decimal n = bytes;
        while (Math.Round(n / 1024) >= 1) { n /= 1024; i++; }
        return $"{n:N2} {suf[i]}";
    }

    private string FormatUptime(long totalSeconds)
    {
        var t = TimeSpan.FromSeconds(totalSeconds);
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays} يوم و {t.Hours} ساعة";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours} ساعة و {t.Minutes} دقيقة";
        return $"{(int)t.TotalMinutes} دقيقة";
    }

    private void BtnSavePdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "ملفات PDF (*.pdf)|*.pdf",
            FileName = $"تقرير_المستخدم_{_userName}.pdf"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                byte[]? logoBytes = null;
                try
                {
                    var uri = new Uri("pack://application:,,,/Lux.Management.Console;component/Resources/img/logo2.png");
                    var resourceStream = System.Windows.Application.GetResourceStream(uri);
                    if (resourceStream != null)
                    {
                        using var ms = new MemoryStream();
                        resourceStream.Stream.CopyTo(ms);
                        logoBytes = ms.ToArray();
                    }
                }
                catch {}

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));
                        page.ContentFromRightToLeft();

                        // Header
                        page.Header().BorderBottom(2).BorderColor(Colors.Blue.Lighten2).PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Row(r =>
                            {
                                if (logoBytes != null)
                                {
                                    r.ConstantItem(50).Height(50).Image(logoBytes);
                                }
                                r.RelativeItem().PaddingRight(10).Column(col =>
                                {
                                    col.Item().Text("Alpha Manager").FontSize(24).FontColor(Colors.Blue.Darken2).Bold();
                                    col.Item().Text("نظام إدارة شبكات الإنترنت").FontSize(11).FontColor(Colors.Grey.Darken1);
                                });
                            });
                            
                            row.ConstantItem(180).AlignLeft().Column(colLeft =>
                            {
                                colLeft.Item().Text($"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd}").FontSize(10).FontColor(Colors.Grey.Darken2);
                                colLeft.Item().Text($"وقت الإصدار: {DateTime.Now:HH:mm:ss}").FontSize(10).FontColor(Colors.Grey.Darken2);
                                colLeft.Item().Text($"اسم الشبكة: {_routerName}").FontSize(10).FontColor(Colors.Grey.Darken2);
                            });
                        });

                        page.Content().PaddingVertical(20).Column(col =>
                        {
                            col.Spacing(20);

                            col.Item().PaddingBottom(10).AlignCenter().Text($"تقرير المشترك التفصيلي: {_userName}")
                                .FontSize(22).Bold().FontColor(Colors.Blue.Darken3);

                            // Section 1: Card details
                            col.Item().Row(mainRow =>
                            {
                                mainRow.RelativeItem(3).Column(rightCol =>
                                {
                                    rightCol.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                                    {
                                        inner.Item().PaddingBottom(10).Text("معلومات الكرت والباقة").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                        inner.Item().Table(table =>
                                        {
                                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                            table.Cell().PaddingBottom(5).Text($"الباقة: {_profile}").FontSize(12).FontColor(Colors.Grey.Darken3);
                                            table.Cell().PaddingBottom(5).Text($"حالة الكرت: {_status}").FontSize(12).FontColor(Colors.Grey.Darken3);
                                            table.Cell().PaddingBottom(5).Text($"تاريخ أول استخدام: {_firstUse}").FontSize(12).FontColor(Colors.Grey.Darken3);
                                            table.Cell().PaddingBottom(5).Text($"تاريخ الانتهاء: {_expiryDate}").FontSize(12).FontColor(Colors.Grey.Darken3);
                                            table.Cell().PaddingBottom(5).Text($"الصلاحية: {_validityHours}").FontSize(12).FontColor(Colors.Grey.Darken3);
                                            table.Cell().PaddingBottom(5).Text($"آخر اتصال: {_lastSeen}").FontSize(12).FontColor(Colors.Grey.Darken3);
                                        });
                                    });
                                });

                                mainRow.ConstantItem(15);

                                mainRow.RelativeItem(2).Column(leftCol =>
                                {
                                    leftCol.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                                    {
                                        inner.Item().PaddingBottom(10).Text("موديل الجهاز المتوقع").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                        
                                        if (_uniqueMacs.Count > 0)
                                        {
                                            foreach (var mac in _uniqueMacs)
                                            {
                                                var model = GetDeviceModelFromMac(mac);
                                                inner.Item().PaddingBottom(8).Column(deviceCol =>
                                                {
                                                    deviceCol.Item().Text(mac).FontSize(10).FontColor(Colors.Grey.Darken2);
                                                    deviceCol.Item().Text(model != "—" ? model : "غير محدد").FontSize(12).Bold().FontColor(Colors.Blue.Darken3);
                                                });
                                            }
                                        }
                                        else
                                        {
                                            inner.Item().Text("لا توجد عناوين MAC").FontSize(12).FontColor(Colors.Grey.Medium);
                                        }
                                    });
                                });
                            });

                            // Section 2: Usage statistics
                            col.Item().Background(Colors.Blue.Lighten5).Padding(15).Column(inner =>
                            {
                                inner.Item().PaddingBottom(10).Text("إحصائيات الاستهلاك والرصيد").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                inner.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                    table.Cell().PaddingBottom(8).Text(text => {
                                        text.Span("التنزيل: ").FontSize(12).FontColor(Colors.Grey.Darken3);
                                        text.Span(_download).FontSize(12).FontColor(Colors.Green.Darken2).Bold();
                                    });
                                    table.Cell().PaddingBottom(8).Text(text => {
                                        text.Span("الرفع: ").FontSize(12).FontColor(Colors.Grey.Darken3);
                                        text.Span(_upload).FontSize(12).FontColor(Colors.Purple.Darken2).Bold();
                                    });
                                    table.Cell().PaddingBottom(8).Text(text => {
                                        text.Span("الرصيد المتبقي: ").FontSize(12).FontColor(Colors.Grey.Darken3);
                                        text.Span(_remainingQuota).FontSize(12).FontColor(Colors.Blue.Darken3).Bold();
                                    });
                                    table.Cell().PaddingBottom(8).Text(text => {
                                        text.Span("إجمالي وقت الاتصال: ").FontSize(12).FontColor(Colors.Grey.Darken3);
                                        text.Span(_uptime).FontSize(12).FontColor(Colors.Orange.Darken3).Bold();
                                    });
                                });
                            });

                            // Section 3: Connection addresses
                            col.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                            {
                                inner.Item().PaddingBottom(10).Text("آخر بيانات اتصال والمنافذ").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                inner.Item().Text($"آخر IP: {_lastIp}   |   آخر MAC: {_lastMac}   |   آخر فيلان/منفذ: {_lastVlan}").FontSize(12).FontColor(Colors.Grey.Darken3);
                                
                                inner.Item().PaddingTop(12).PaddingBottom(4).Text("عناوين IP المرتبطة:").Bold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                inner.Item().Text(_ips.Replace("\n", "  •  ")).FontSize(11).FontColor(Colors.Grey.Darken3);
                                
                                inner.Item().PaddingTop(10).PaddingBottom(4).Text("عناوين MAC المرتبطة:").Bold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                inner.Item().Text(_macs.Replace("\n", "  •  ")).FontSize(11).FontColor(Colors.Grey.Darken3);
                            });

                            // Section 4: Session history table
                            if (_sessionsList != null && _sessionsList.Count > 0)
                            {
                                col.Item().Background(Colors.White).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(inner =>
                                {
                                    inner.Item().PaddingBottom(8).Text("سجل الجلسات التفصيلي").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                                    inner.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn(3); // Start Time
                                            c.RelativeColumn(2); // VLAN
                                            c.RelativeColumn(3); // IP Address
                                            c.RelativeColumn(3); // MAC Address
                                            c.RelativeColumn(2); // Duration
                                            c.RelativeColumn(2); // Download
                                            c.RelativeColumn(2); // Upload
                                        });

                                        table.Header(h =>
                                        {
                                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("وقت البدء").Bold().FontSize(10);
                                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("الفيلان").Bold().FontSize(10);
                                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("عنوان IP").Bold().FontSize(10);
                                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("عنوان MAC").Bold().FontSize(10);
                                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("المدة").Bold().FontSize(10);
                                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("التنزيل").Bold().FontSize(10);
                                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("الرفع").Bold().FontSize(10);
                                        });

                                        foreach (var s in _sessionsList.Take(100))
                                        {
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(s.StartTimeFormatted).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(s.Vlan).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(s.IpAddress).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(s.MacAddress).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(s.DurationFormatted).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(s.DownloadFormatted).FontSize(9);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(s.UploadFormatted).FontSize(9);
                                        }
                                    });
                                });
                            }
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("صفحة ").FontSize(10).FontColor(Colors.Grey.Medium);
                            x.CurrentPageNumber().FontSize(10).FontColor(Colors.Grey.Medium);
                            x.Span(" من ").FontSize(10).FontColor(Colors.Grey.Medium);
                            x.TotalPages().FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    });
                }).GeneratePdf(dialog.FileName);

                MessageBox.Show("تم حفظ التقرير بنجاح كملف PDF!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ الملف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private string GetDeviceModelFromMac(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return "—";

        var cleanMac = mac.Replace(":", "").Replace("-", "").ToUpper();
        if (cleanMac.Length < 6) return "—";
        
        var prefix = cleanMac.Substring(0, 6);
        
        if (new[] {
            "001CB3", "002500", "00254B", "FCFC48", "000393", "000502", "000A27", "000A95", "0010FA", "001451", "0016CB", "0017F2", "0019E3", "001B63", "001D4F", "001E52", "001EC2", "001F5B", "001FF3", "0021E9",
            "002241", "002312", "002332", "00236C", "002436", "002608", "00264A", "0026B0", "0026BB", "18AF61", "24A074", "28E347", "34159E", "38CADA", "3C0754", "3CD0F8", "403004", "40D32D", "442A60", "444C0C",
            "48437C", "48D705", "4C38CC", "4C8D79", "50EAE5", "542696", "542A1B", "5433CB", "5855CA", "5C5948", "5C95AE", "5C97F3", "600308", "60334B", "609217", "60A44C", "60C547", "60FACD", "64200C", "6470EC",
            "64B9E8", "64E682", "680908", "685B35", "68A86D", "68AE20", "68D93C", "6C3E6D", "6C4008", "6C709F", "6C8DC1", "6C96CF", "6CAB31", "701124", "7014A6", "703EAC", "705681", "70700D", "70A2B3", "70CD60",
            "70DEE2", "748114", "74A34A", "74B587", "74E1B6", "74E2E5", "74F612", "7831C1", "783A84", "784F43", "787B8A", "78886D", "789F70", "78A3E4", "78FD94", "7C11BE", "7C6D62", "7CC3A1", "7CC537", "7CD1C3",
            "7CF90E", "80006E", "804971", "805A04", "80929F", "80B655", "80EA96", "842999", "843835", "84788B", "848E0C", "84A134", "84B153", "84FCFE", "881FA1", "88308A", "885395", "8863DF", "886417", "88C663",
            "8C2937", "8C2DAA", "8C5877", "8C7A3D", "8C7C92", "8C8590", "8CFABA", "9027E4", "9035EB", "907240", "90840D", "90B21F", "90C1C6", "90E17B", "90FD03", "949426", "94E96A", "94E979", "94F6A3", "9801A7",
            "985AEB", "98B8E3", "98D6BB", "98E0D9", "98FE94", "9C04EB", "9C207B", "9C35EB", "9C4F5E", "9C8B7F", "9CE65E", "9CF387", "A01828", "A03B8F", "A0999B", "A0EDCD", "A43135", "A45E60", "A46706", "A4772E",
            "A4B197", "A4C361", "A4D18C", "A4D1D2", "A4E975", "A4F1E8", "A82066", "A85B78", "A8667F", "A886DD", "A88808", "A8967A", "A8BBCF", "A8FAD8", "AC162D", "AC293A", "AC3C0B", "AC7F3E", "AC87A3", "ACBC32",
            "ACCF85", "ACE010", "ACFDEC", "B019C6", "B03495", "B0481A", "B065BD", "B0702D", "B09FBA", "B0C554", "B418D1", "B48B19", "B49CDF", "B4F065", "B4F61C", "B8098A", "B8782E", "B88D12", "B8C75D", "B8E856",
            "B8F6B1", "BC3BAF", "BC4CC4", "BC52B7", "BC5436", "BC6778", "BC9FEF", "BCA920", "BCEC5D", "BCFED9", "C01ADA", "C06394", "C0847A", "C09F42", "C0A0BB", "C0D012", "C0E862", "C0F2FB", "C42C03", "C48508",
            "C4E90F", "C81EE7", "C82A14", "C8334B", "C869CD", "C88550", "C8B5B7", "C8D2C4", "C8E0EB", "CC088D", "CC20E8", "CC25EF", "CC29F5", "CC4463", "CC52AF", "CC785F", "CCCC81", "D0034B", "D022BE", "D023DB",
            "D02599", "D03311", "D04F7E", "D0817A", "D0A637", "D0C5F3", "D0D2B9", "D0E140", "D4258B", "D435E8", "D4619D", "D4909C", "D49A20", "D4A33D", "D4DCCD", "D4F46F", "D8004D", "D81C79", "D83062", "D89695",
            "D8A25E", "D8BB2C", "D8CF9C", "D8D1CB", "DC2B61", "DC3714", "DC415F", "DC86D8", "DCA4CA", "DCD3A2", "E05F45", "E06678", "E0B52F", "E0B9BA", "E0C97A", "E0CB4E", "E0DB55", "E0F5C6", "E0F847", "E425E9",
            "E450EB", "E48B7F", "E498D5", "E49ADC", "E4B2FB", "E4C72F", "E4E4AB", "E8040B", "E80688", "E8802E", "E88D28", "E8B1FC", "EC3586", "EC852F", "ECADB8", "F01898", "F02475", "F0761C", "F099BF", "F0A44A",
            "F0B479", "F0C1F1", "F0DBE2", "F0DCE2", "F0F612", "F409D8", "F41B5F", "F431C3", "F437B7", "F45C89", "F4F15A", "F4F951", "F80377", "F81EDF", "F82793", "F8387F", "F86214", "F88FCA", "F8E079", "FC253F",
            "FC7516", "FCD815", "FCE998", "FCFC48"
        }.Contains(prefix)) return "Apple 🍏";

        if (new[] {
            "0000F0", "000278", "0007AB", "0009A7", "001247", "0012FB", "0015B9", "001632", "00166C", "0017C9", "0017D5", "0018AF", "001A8A", "001CC6", "001D25", "001E7D", "001FCC", "002119", "0021D2", "0022F5",
            "00233D", "0023D6", "002490", "002538", "002637", "007A93", "04180F", "04FEA1", "080027", "083E8E", "08ECA9", "08FC88", "0C1420", "0C54A5", "0C715D", "0C8910", "0C9152", "0CDFA4", "103047", "1077B1",
            "10D07A", "10F96F", "1436C6", "147DC5", "148FC6", "14F43E", "181E78", "18227E", "183BD2", "184617", "185936", "187A93", "1883BF", "189EFC", "1C35A6", "1C5A3E", "1C62B8", "1CE286", "2047ED", "20D5BF",
            "20E52A", "241B7A", "244B03", "2473F2", "24D3F2", "24E314", "24EC99", "24FCE5", "281878", "2827BF", "287A93", "28987B", "28A183", "28B2BD", "28C2DD", "28CC01", "28E02C", "2C0E3D", "2C26C5", "2C3033",
            "2C4401", "2C5A0F", "2CD05A", "30074D", "301966", "30636B", "307512", "3078C8", "3085A9", "30918F", "30A8DB", "30C7AE", "30D855", "342387", "342D0D", "34A395", "34A8EB", "34C059", "34E6AD", "34FC95",
            "380197", "382DD1", "3895F6", "38A4ED", "3C5A37", "3C6238", "3C6A2C", "3C8BCD", "3CE1A1", "3CF7A4", "400E85", "401B5F", "40270B", "4035E5", "40516C", "406C8F", "4083DE", "4098AD", "40CCF4", "40D855",
            "40FC89", "445072", "44650D", "4480EB", "44913C", "44F43E", "44F459", "482CA0", "483FE9", "484487", "4844F7", "4852B5", "485A3F", "48C1AC", "48D63D", "4C1AC0", "4C3C16", "4C4E35", "4C55CC", "4C7C5F",
            "4CBCA5", "4CD1A1", "4CE676", "5001D9", "500731", "501878", "502E5C", "503275", "505025", "5056A8", "508569", "50A4C8", "50CCF8", "50F5DA", "5440AD", "544415", "54880E", "549A11", "54B121", "54C121",
            "54E4BD", "54FA3E", "580876", "581F28", "58A2F5", "58C876", "58D9C5", "58E28F", "58F10F", "5C0A5B", "5C3A45", "5C70A3", "5CA86A", "5CC5D4", "5CF8A1", "600194", "601D91", "6021C0", "604415", "606D3C",
            "60AF6D", "60D0A9", "60E327", "60E701", "60F189", "642149", "646E97", "647791", "64A769", "64B0A6", "64C2DE", "64C667", "64C6EC", "64D4BD", "64E7D8", "64FBCC", "680571", "681878", "683A7E", "683DB5",
            "687A93", "689CE2", "68C44D", "68DBF2", "6C2990", "6C71D9", "6C8336", "6C8DF1", "6C9AED", "6CA75F", "6CF15E", "702C1F", "703EB5", "704D7B", "70700D", "708D09", "70A8E3", "70B035", "70B121", "70D4F2",
            "70D690", "7405A5", "74458A", "745A6A", "745E1C", "747990", "749A3C", "74B9EB", "74D02B", "74D435", "781DBC", "7825AD", "7831C1", "783F15", "78471D", "784B87", "78521A", "785AC8", "787A93", "789ED0",
            "78A82F", "78ABBB", "78D6F0", "78E7D1", "78F7BE", "7C03D8", "7C38AD", "7C78B2", "7CA7B5", "7CC709", "7CD30A", "7CF7CF", "8018A7", "805AB4", "805E4F", "80656D", "80B655", "80CF41", "80E650", "84119E",
            "8425DB", "843838", "845181", "8455A5", "84742A", "84871C", "84C121", "84DB2F", "84E0F4", "84F0EC", "84FCE6", "88124E", "88308A", "88797E", "88C9D0", "88D7F6", "88E603", "8C1ABF", "8C2DA5", "8C3AE3",
            "8C7712", "8CC8CD", "8CF5A3", "9000DB", "90187C", "906549", "907F61", "908C50", "90B686", "90C682", "90D0ED", "90E7C4", "90F15C", "90FDAC", "9405BB", "94103F", "94350A", "94652D", "946EB5", "948BC1",
            "94B10A", "94E979", "94F4AC", "94FB29", "98002A", "980C82", "980D2E", "9810E8", "984FEE", "9852B5", "988389", "989C57", "98B3B1", "98E7F5", "98F170", "98FFD0", "9C0298", "9C0FC4", "9C1FDD", "9C2A70",
            "9C3AAF", "9C431E", "9C6C15", "9CE6E7", "9CFC01", "A00798", "A01828", "A0270A", "A0698E", "A07591", "A0821F", "A091C8", "A0B4F5", "A0C5F2", "A0C9A0", "A0CBFD", "A0E6F8", "A408EA", "A4370B", "A45C27",
            "A47076", "A47733", "A49A58", "A4D1D2", "A4E975", "A80600", "A816D0", "A82279", "A82BB9", "A854B2", "A85B78", "A87B39", "A887ED", "A89FBA", "A8C009", "A8D578", "A8E3EE", "AC1B1C", "AC3A7A", "AC5775",
            "AC5F3E", "AC7A4D", "ACAFB9", "ACC115", "ACE215", "B01401", "B05ADA", "B0702D", "B0C4E7", "B0D59D", "B0EC71", "B407F9", "B41C30", "B4430D", "B4527D", "B46293", "B479A7", "B47C9C", "B8098A", "B85001",
            "B857D8", "B86CE8", "B8782E", "B8E856", "B8F6B1", "BC14EF", "BC15AC", "BC20A4", "BC4760", "BC5436", "BC720D", "BC7CB0", "BCA58D", "BCA8A6", "BCB32B", "BCD11F", "BCF4D4", "C0143D", "C07E19", "C09727",
            "C0A13E", "C0BDC8", "C0D012", "C0F8DA", "C42C03", "C43A35", "C4438F", "C46E13", "C4731E", "C48508", "C49A02", "C4D0E3", "C4FC56", "C81451", "C87DF3", "C88550", "C8979F", "C8C2C4", "C8D2C4", "C8F733",
            "CC07AB", "CC3A61", "CCA3E2", "CCB8A8", "CCC3EA", "CCF3A5", "D0034B", "D017C2", "D022BE", "D03742", "D059E4", "D087E2", "D0A225", "D0C5F3", "D0D2B9", "D0E140", "D428B2", "D4389C", "D487D8", "D4909C",
            "D49EC0", "D4A33D", "D4C1FC", "D4E63E", "D80F99", "D831CF", "D85775", "D890E8", "D8A25E", "D8B190", "D8C0A6", "D8D1CB", "D8E56D", "DC2B61", "DC3A5E", "DC712F", "DC74A5", "DC85DE", "DCA8EB", "DCC3A2",
            "E05163", "E0AA96", "E0B52F", "E0B9BA", "E0C97A", "E0D083", "E41218", "E43022", "E44790", "E470B8", "E47CF9", "E4907A", "E4B2FB", "E4E0A6", "E8039A", "E807BF", "E83A12", "E8508B", "E89309", "E8E5D6",
            "EC1F72", "EC3586", "EC852F", "ECE555", "F008F1", "F01898", "F02475", "F025B7", "F05B7B", "F05D8C", "F06BCA", "F0728F", "F0A225", "F0A44A", "F0B479", "F0C1F1", "F0E77E", "F0EB14", "F0ECAF", "F0F57E",
            "F0F612", "F409D8", "F44B2A", "F45C89", "F47B5E", "F49EC2", "F4A810", "F4D488", "F4F951", "F80473", "F81EDF", "F82793", "F8387F", "F884F2", "F8CFC5", "FC1910", "FC7516", "FC8F90", "FCA135", "FCC2DE",
            "FCE998"
        }.Contains(prefix)) return "Samsung 📱";

        if (new[] {
            "009EC8", "185936", "286C07", "3480B3", "3CBD3E", "50642B", "584498", "640980", "745A25", "7C1DD9", "8035C1", "8CBEBE", "9C2EA1", "ACC1EE", "B03829", "C40BCB", "E446DA", "F023B9", "FC1990", "00EC0A",
            "1C5216", "28E31F", "38A28C", "3CA82A", "4C11AE", "50EC50", "58A2B7", "64B5C6", "7811DC", "7CA7B0", "80AD1A", "90A4DE", "9CF03C", "B03CA7", "C80E14", "E49C20", "F0B4D2", "FC63F6", "0C29BB", "2034FB",
            "30107A", "4459E3", "4CB9EA", "5448AD", "5CE0F6", "687AB4", "781F4A", "7CF90E", "80EACA", "9810E8", "A45055", "A89CED", "B43A28", "CC78AB", "EC6C9F", "F45C89", "FCA89A"
        }.Contains(prefix)) return "Xiaomi 📱";

        return "—";
    }
}

public class UserSessionReportItem
{
    public string StartTimeFormatted { get; set; } = string.Empty;
    public string Vlan { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string DurationFormatted { get; set; } = string.Empty;
    public string DownloadFormatted { get; set; } = string.Empty;
    public string UploadFormatted { get; set; } = string.Empty;
}
