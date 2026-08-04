using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Enums;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Properties;
using MikroTikVoucherPrinter.Infrastructure.Templates;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services
{
    public class PrintService : IPrintService
    {
        private readonly IEnumerable<IPrintTemplate> _printTemplates;
        private readonly ILogger<PrintService> _logger;
        private readonly IDbContextFactory<Data.LuxCardDbContext> _dbFactory;

        public PrintService(IEnumerable<IPrintTemplate> printTemplates, ILogger<PrintService> logger, IDbContextFactory<Data.LuxCardDbContext> dbFactory)
        {
            _printTemplates = printTemplates;
            _logger = logger;
            _dbFactory = dbFactory;
        }

        public async Task<Result<byte[]>> GeneratePdfAsync(
            List<VoucherDto> vouchers,
            PrintSettingsDto settings,
            IProgress<(int currentPage, int totalPages, string statusText)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (vouchers == null || vouchers.Count == 0)
                return Result<byte[]>.Failure("لا يوجد كروت للطباعة.", ErrorType.Validation);

            try
            {
                Domain.Entities.TemplateConfig? customConfig = null;
                if (settings.CustomTemplateId.HasValue)
                {
                    await using var dbCtx = await _dbFactory.CreateDbContextAsync(cancellationToken);
                    customConfig = await dbCtx.TemplateConfigs.FindAsync(
                        new object[] { settings.CustomTemplateId.Value }, cancellationToken);
                }

                // If TXT text template is selected, generate TXT byte output
                if (settings.CustomTemplateId == BuiltInTemplateIds.TxtTemplate || (customConfig != null && customConfig.Id == BuiltInTemplateIds.TxtTemplate))
                {
                    var sb = new StringBuilder();
                    foreach (var v in vouchers)
                    {
                        if (v.CredentialMode == CredentialMode.UsernameOnly)
                        {
                            sb.AppendLine($"رمز الدخول: {v.Username}");
                        }
                        else
                        {
                            sb.AppendLine($"رمز الدخول: {v.Username}");
                            sb.AppendLine($"كلمة المرور: {v.Password}");
                        }
                        sb.AppendLine(); // empty line between cards
                    }
                    var txtBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    return Result<byte[]>.Success(txtBytes);
                }

                return await Task.Run(() =>
                {
                    using var ms = new MemoryStream();
                    var writer = new PdfWriter(ms);
                    writer.SetCloseStream(false);
                    using var pdf = new PdfDocument(writer);

                    if (customConfig != null)
                        TemplatePrintSettingsOverlay.ApplyFromEntity(customConfig, settings);

                    PageSize pageSize = PageSize.A4;
                    if (settings.PaperType == PaperType.Thermal58 || settings.PaperType == PaperType.Thermal80)
                    {
                        double pointsPerMm = 72.0 / 25.4; 
                        float widthInPoints = (float)(settings.PrintableWidthMm * pointsPerMm);
                        pageSize = new PageSize(widthInPoints, (float)settings.CardHeight);
                    }

                    var document = new Document(pdf, pageSize);
                    document.SetMargins(2, 2, 2, 2);
                    
                    PdfFont arabicFont = TryGetArabicFont();
                    document.SetBaseDirection(BaseDirection.RIGHT_TO_LEFT);
                    document.SetTextAlignment(TextAlignment.RIGHT);
                    document.SetFont(arabicFont);
                    document.SetFontSize(settings.FontSize);

                    IPrintTemplate? template = null;

                    if (customConfig != null)
                    {
                        if (!string.IsNullOrWhiteSpace(customConfig.LegacyRendererKey))
                        {
                            template = _printTemplates.FirstOrDefault(t =>
                                t.TemplateName == customConfig.LegacyRendererKey);
                            if (template == null)
                            {
                                template = new CustomGridTemplate(customConfig);
                            }
                        }
                        else
                        {
                            template = new CustomGridTemplate(customConfig);
                        }
                    }
                    else
                    {
                        template = _printTemplates.FirstOrDefault(t => t.TemplateName == settings.TemplateName);
                        if (template == null)
                        {
                            template = settings.PaperType == PaperType.A4 
                                ? _printTemplates.FirstOrDefault(t => t.TemplateName == "HawaeGridDefault")
                                : _printTemplates.FirstOrDefault(t => t.TemplateName == "ThermalDefault");
                        }
                    }

                    if (template != null)
                    {
                        template.LayoutDocument(document, vouchers, settings, arabicFont, progress);
                    }

                    document.Close();
                    writer.Close();

                    byte[] pdfBytes = ms.ToArray();
                    return Result<byte[]>.Success(pdfBytes);
                    
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Result<byte[]>.Failure("تم إلغاء عملية بناء ملف الطباعة", ErrorType.Unexpected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ فادح في عملية الطباعة.");
                return Result<byte[]>.Failure($"فشل في محرك الطباعة: {ex.Message}", ErrorType.Unexpected);
            }
        }

        private PdfFont TryGetArabicFont()
        {
            try { return PdfFontFactory.CreateFont("c:\\windows\\fonts\\tahoma.ttf", PdfEncodings.IDENTITY_H); }
            catch {
                try { return PdfFontFactory.CreateFont("c:\\windows\\fonts\\arial.ttf", PdfEncodings.IDENTITY_H); }
                catch { return PdfFontFactory.CreateFont(StandardFonts.HELVETICA); }
            }
        }
    }
}
