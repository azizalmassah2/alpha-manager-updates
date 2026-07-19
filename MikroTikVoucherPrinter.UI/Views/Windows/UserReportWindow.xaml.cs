using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MikroTikVoucherPrinter.UI.Views.Windows;

public partial class UserReportWindow : Window
{
    private readonly string _userName;
    private string _dbPath;
    
    // بيانات التقرير
    private string _profile = "—";
    private string _status = "—";
    private string _regDate = "—";
    private string _lastSeen = "—";
    private string _download = "0 MB";
    private string _upload = "0 MB";
    private string _uptime = "0 دقيقة";
    private string _sessionsCount = "0";
    private string _ips = "لا يوجد";
    private string _macs = "لا يوجد";

    public UserReportWindow(string userName)
    {
        InitializeComponent();
        QuestPDF.Settings.License = LicenseType.Community;
        
        _userName = userName;
        TxtUserName.Text = userName;
        
        _dbPath = @"d:\LUXCARD\desktop\user-manager\sqldb";

        LoadData();
    }

    private void LoadData()
    {
        if (!File.Exists(_dbPath))
        {
            MessageBox.Show($"عذراً، لم يتم العثور على قاعدة البيانات في المسار:\n{_dbPath}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            conn.Open();

            // 1. معلومات المستخدم
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, actualProfileName, disabled, uptimeUsed, downloadUsed, regDate, lastSeenAt FROM [user] WHERE CAST(userName AS TEXT) = @u LIMIT 1";
                cmd.Parameters.AddWithValue("@u", _userName); 
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    long userId = reader.GetInt64(0);
                    _profile = reader.IsDBNull(1) ? "—" : reader.GetString(1);
                    
                    int disabled = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    long upUsed = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                    long dlUsed = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                    
                    if (disabled == 1) _status = "مُعطَّل ❌";
                    else if (upUsed > 0 || dlUsed > 0) _status = "مستخدم 🟢";
                    else _status = "جاري الانتظار ⏳";

                    if (!reader.IsDBNull(5)) _regDate = GetRelativeTime(reader.GetInt64(5));
                    if (!reader.IsDBNull(6)) _lastSeen = GetRelativeTime(reader.GetInt64(6));
                    
                    // 2. الإحصائيات من ucounters
                    using var statCmd = conn.CreateCommand();
                    statCmd.CommandText = "SELECT download, upload, uptime FROM [ucounters] WHERE userId = @uid";
                    statCmd.Parameters.AddWithValue("@uid", userId);
                    using var statReader = statCmd.ExecuteReader();
                    long totalDl = 0, totalUl = 0, totalUp = 0;
                    while (statReader.Read())
                    {
                        if (!statReader.IsDBNull(0)) totalDl += statReader.GetInt64(0);
                        if (!statReader.IsDBNull(1)) totalUl += statReader.GetInt64(1);
                        if (!statReader.IsDBNull(2)) totalUp += statReader.GetInt64(2);
                    }
                    if (totalDl > 0) _download = FormatBytes(totalDl);
                    if (totalUl > 0) _upload = FormatBytes(totalUl);
                    if (totalUp > 0) _uptime = FormatUptime(totalUp);

                    // 3. الجلسات (العناوين والماكات)
                    using var sessCmd = conn.CreateCommand();
                    sessCmd.CommandText = "SELECT ipUser, callingStationId FROM [session] WHERE userId = @uid";
                    sessCmd.Parameters.AddWithValue("@uid", userId);
                    using var sessReader = sessCmd.ExecuteReader();
                    
                    int sCount = 0;
                    var ipSet = new HashSet<string>();
                    var macSet = new HashSet<string>();
                    
                    while (sessReader.Read())
                    {
                        sCount++;
                        if (!sessReader.IsDBNull(0))
                        {
                            var ipBytes = sessReader.GetValue(0) as byte[];
                            if (ipBytes != null)
                            {
                                if (ipBytes.Length == 4 || ipBytes.Length == 16)
                                    ipSet.Add(new System.Net.IPAddress(ipBytes).ToString());
                                else
                                    ipSet.Add(Encoding.UTF8.GetString(ipBytes));
                            }
                            else
                            {
                                ipSet.Add(sessReader.GetString(0));
                            }
                        }
                        if (!sessReader.IsDBNull(1))
                        {
                            var macBytes = sessReader.GetValue(1) as byte[];
                            if (macBytes != null) macSet.Add(Encoding.UTF8.GetString(macBytes));
                            else macSet.Add(sessReader.GetString(1));
                        }
                    }
                    _sessionsCount = $"{sCount} جلسة";
                    if (ipSet.Count > 0) _ips = string.Join("\n", ipSet);
                    if (macSet.Count > 0) _macs = string.Join("\n", macSet);
                }
            }

            // تحديث الواجهة
            TxtProfile.Text = _profile;
            TxtStatus.Text = _status;
            TxtRegDate.Text = _regDate;
            TxtLastSeen.Text = _lastSeen;
            TxtDownload.Text = _download;
            TxtUpload.Text = _upload;
            TxtUptime.Text = _uptime;
            TxtSessionsCount.Text = _sessionsCount;
            TxtIPs.Text = _ips;
            TxtMACs.Text = _macs;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطأ في قراءة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));
                        page.ContentFromRightToLeft();

                        // الهيدر (ترويسة التقرير)
                        page.Header().BorderBottom(2).BorderColor(Colors.Blue.Lighten2).PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("LuxCard Enterprise").FontSize(26).FontColor(Colors.Blue.Darken2).Bold();
                                col.Item().Text("نظام إدارة شبكات الإنترنت - ISP Operations Center").FontSize(12).FontColor(Colors.Grey.Darken1);
                            });
                            row.ConstantItem(150).AlignRight().Column(col =>
                            {
                                col.Item().Text($"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd}").FontSize(10).FontColor(Colors.Grey.Darken2);
                                col.Item().Text($"وقت الإصدار: {DateTime.Now:HH:mm:ss}").FontSize(10).FontColor(Colors.Grey.Darken2);
                                col.Item().Text("النظام: لوكس كارد V2").FontSize(10).FontColor(Colors.Blue.Medium);
                            });
                        });

                        page.Content().PaddingVertical(20).Column(col =>
                        {
                            col.Spacing(20);

                            // عنوان التقرير
                            col.Item().PaddingBottom(10).AlignCenter().Text($"تقرير المشترك المٌفصّل: {_userName}")
                                .FontSize(22).Bold().FontColor(Colors.Blue.Darken3);

                            // القسم الأول: معلومات عامة
                            col.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                            {
                                inner.Item().PaddingBottom(10).Text("معلومات البطاقة الأساسية").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                inner.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                    table.Cell().PaddingBottom(5).Text($"الباقة المشتركة: {_profile}").FontSize(13).FontColor(Colors.Grey.Darken3);
                                    table.Cell().PaddingBottom(5).Text($"حالة الكرت: {_status}").FontSize(13).FontColor(Colors.Grey.Darken3);
                                    table.Cell().Text($"تاريخ التسجيل: {_regDate}").FontSize(13).FontColor(Colors.Grey.Darken3);
                                    table.Cell().Text($"آخر ظهور: {_lastSeen}").FontSize(13).FontColor(Colors.Grey.Darken3);
                                });
                            });

                            // القسم الثاني: إحصائيات الاستهلاك
                            col.Item().Background(Colors.Blue.Lighten5).Padding(15).Column(inner =>
                            {
                                inner.Item().PaddingBottom(10).Text("إحصائيات الاستهلاك والبيانات").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                inner.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                    table.Cell().PaddingBottom(8).Text(text => {
                                        text.Span("إجمالي التنزيل: ").FontSize(13).FontColor(Colors.Grey.Darken3);
                                        text.Span(_download).FontSize(13).FontColor(Colors.Green.Darken2).Bold();
                                    });
                                    table.Cell().PaddingBottom(8).Text(text => {
                                        text.Span("إجمالي الرفع: ").FontSize(13).FontColor(Colors.Grey.Darken3);
                                        text.Span(_upload).FontSize(13).FontColor(Colors.Purple.Darken2).Bold();
                                    });
                                    table.Cell().Text(text => {
                                        text.Span("إجمالي وقت الاتصال: ").FontSize(13).FontColor(Colors.Grey.Darken3);
                                        text.Span(_uptime).FontSize(13).FontColor(Colors.Orange.Darken3).Bold();
                                    });
                                    table.Cell().Text(text => {
                                        text.Span("عدد الجلسات (سجل الاتصال): ").FontSize(13).FontColor(Colors.Grey.Darken3);
                                        text.Span(_sessionsCount).FontSize(13).FontColor(Colors.Blue.Darken2).Bold();
                                    });
                                });
                            });

                            // القسم الثالث: العناوين والأمان
                            col.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                            {
                                inner.Item().PaddingBottom(10).Text("السجل الأمني والأجهزة المرتبطة").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                                
                                inner.Item().PaddingBottom(4).Text("عناوين IP التي حصل عليها المشترك أثناء الجلسات:").Bold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                inner.Item().Text(_ips.Replace("\n", "  •  ")).FontSize(11).FontColor(Colors.Grey.Darken3).LineHeight(1.5f);
                                
                                inner.Item().PaddingTop(15).PaddingBottom(4).Text("عناوين MAC (الأجهزة التي اتصلت بالكرت):").Bold().FontSize(12).FontColor(Colors.Grey.Darken2);
                                inner.Item().Text(_macs.Replace("\n", "  •  ")).FontSize(11).FontColor(Colors.Grey.Darken3).LineHeight(1.5f);
                            });

                            // ملاحظة ذيلية
                            col.Item().PaddingTop(20).AlignCenter().Text("هذا التقرير مُصدّر آلياً من نظام LuxCard ولا يحتاج إلى ختم أو توقيع.").FontSize(11).FontColor(Colors.Grey.Medium);
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("الصفحة ").FontColor(Colors.Grey.Medium);
                            x.CurrentPageNumber().FontColor(Colors.Grey.Medium);
                            x.Span(" من ").FontColor(Colors.Grey.Medium);
                            x.TotalPages().FontColor(Colors.Grey.Medium);
                        });
                    });
                })
                .GeneratePdf(dialog.FileName);

                MessageBox.Show("تم حفظ التقرير بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء إنشاء PDF: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
