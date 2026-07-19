using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Models.TemplateElements;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// محرك الطباعة الجديد — يحول قوالب LuxTemplate + بيانات الكروت إلى PDF.
/// يستخدم iText7 (مُثبَّتة بالفعل في المشروع) لتفادي تبعية جديدة.
/// </summary>
public class TemplateEngineService : ITemplateEngine
{
    // ممتد الطباعة للحراري (mm → point): 1 mm = 2.8346 pt
    private const float MmToPt = 2.8346f;

    private readonly ILogger<TemplateEngineService> _logger;

    public TemplateEngineService(ILogger<TemplateEngineService> logger)
    {
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 1. Data Resolution — حل FieldTokens إلى قيم حقيقية
    // ══════════════════════════════════════════════════════════════════════

    public Dictionary<FieldToken, string> ResolveVoucherData(
        VoucherDto voucher,
        PrintContextDto context,
        int cardIndex = 1)
    {
        return new Dictionary<FieldToken, string>
        {
            // بيانات الكرت
            [FieldToken.Username]      = voucher.Username ?? string.Empty,
            [FieldToken.Password]      = voucher.Password ?? string.Empty,
            [FieldToken.Price]         = voucher.Price > 0 ? $"{voucher.Price:N0}" : string.Empty,
            [FieldToken.Quota]         = voucher.QuotaLimitBytes.HasValue && voucher.QuotaLimitBytes > 0
                                            ? FormatBytes(voucher.QuotaLimitBytes.Value)
                                            : string.Empty,
            [FieldToken.Duration]      = string.Empty, // يُحدد من الباقة عند ربط Profile
            [FieldToken.Validity]      = string.Empty,
            [FieldToken.DownloadSpeed] = string.Empty,
            [FieldToken.UploadSpeed]   = string.Empty,

            // بيانات الشبكة
            [FieldToken.NetworkName]   = context.NetworkName,
            [FieldToken.RouterName]    = context.RouterName,
            [FieldToken.RouterIp]      = context.RouterIp,

            // بيانات الوكيل
            [FieldToken.AgentName]     = voucher.AgentName ?? context.AgentName ?? string.Empty,
            [FieldToken.AgentPhone]    = context.AgentPhone ?? string.Empty,

            // التواريخ والأوقات
            [FieldToken.PrintDate]     = context.PrintDateDisplay,
            [FieldToken.PrintTime]     = context.PrintTimeDisplay,
            [FieldToken.ExpiryDate]    = string.Empty,

            // أرقام تسلسلية
            [FieldToken.SerialNumber]  = $"#{cardIndex:D4}",
            [FieldToken.BatchNumber]   = context.BatchNumber ?? voucher.BatchId.ToString()[..8],
            [FieldToken.CardIndex]     = cardIndex.ToString(),
            [FieldToken.TotalCards]    = context.TotalCards.ToString(),

            // مستقبلية
            [FieldToken.CustomerName]  = string.Empty,
            [FieldToken.InvoiceNumber] = string.Empty,
            [FieldToken.Total]         = string.Empty,
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    // 2. PDF Rendering — تحويل القالب + الكروت إلى PDF
    // ══════════════════════════════════════════════════════════════════════

    public async Task<byte[]> RenderToPdfAsync(
        LuxTemplateDetailDto template,
        IReadOnlyList<VoucherDto> vouchers,
        PrintContextDto context,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var elements = ParseElements(template.ElementsJson);

            using var ms = new MemoryStream();
            using var writer = new PdfWriter(ms);
            using var pdf = new PdfDocument(writer);

            // أبعاد الصفحة بالـ Points
            float pageW = template.PageWidthMm * MmToPt;
            float pageH = template.PageHeightMm * MmToPt;
            var pageSize = new PageSize(pageW, pageH);

            // أبعاد الكرت بالـ Points
            float cardW  = template.CardWidthMm  * MmToPt;
            float cardH  = template.CardHeightMm * MmToPt;
            float gapX   = template.HorizontalGapMm * MmToPt;
            float gapY   = template.VerticalGapMm   * MmToPt;
            float marginL = template.MarginLeftMm   * MmToPt;
            float marginT = template.MarginTopMm    * MmToPt;

            int cols = Math.Max(1, template.CardsPerRow);
            int rows = Math.Max(1, template.CardsPerColumn);

            // احسب عدد الكروت في الصفحة
            int cardsPerPage = cols * rows;
            int totalCards   = vouchers.Count;
            int totalPages   = (int)Math.Ceiling((double)totalCards / cardsPerPage);

            for (int pageIdx = 0; pageIdx < totalPages; pageIdx++)
            {
                ct.ThrowIfCancellationRequested();

                var page = pdf.AddNewPage(pageSize);
                var canvas = new PdfCanvas(page);
                var doc    = new Document(pdf, pageSize);

                for (int slot = 0; slot < cardsPerPage; slot++)
                {
                    int voucherIdx = pageIdx * cardsPerPage + slot;
                    if (voucherIdx >= totalCards)
                        break;

                    var voucher  = vouchers[voucherIdx];
                    var resolved = ResolveVoucherData(voucher, context, voucherIdx + 1);

                    int col = slot % cols;
                    int row = slot / cols;

                    // موضع الكرت (iText: Y من الأسفل)
                    float cardX = marginL + col * (cardW + gapX);
                    float cardY = pageH - marginT - (row + 1) * cardH - row * gapY;

                    RenderCard(canvas, pdf, doc, template, elements, resolved, cardX, cardY, cardW, cardH);
                }
            }

            pdf.Close();
            return ms.ToArray();

        }, ct);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 3. Preview Rendering — صورة PNG لمعاينة كرت واحد
    // ══════════════════════════════════════════════════════════════════════

    public async Task<byte[]> RenderPreviewAsync(
        LuxTemplateDetailDto template,
        Dictionary<FieldToken, string>? sampleData = null,
        CancellationToken ct = default)
    {
        // في v1.0: نرسم PDF بكرت واحد ثم نعيده كـ PDF bytes
        // المعاينة الحقيقية كـ PNG تحتاج SkiaSharp (v2.0)
        var previewVoucher = BuildSampleVoucher(sampleData);
        var ctx = new PrintContextDto
        {
            NetworkName  = sampleData?.GetValueOrDefault(FieldToken.NetworkName) ?? "شبكة لوكس",
            RouterName   = sampleData?.GetValueOrDefault(FieldToken.RouterName)  ?? "Router-01",
            RouterIp     = sampleData?.GetValueOrDefault(FieldToken.RouterIp)    ?? "192.168.1.1",
            TotalCards   = 1,
        };

        // نبني قالباً مؤقتاً بكرت واحد فقط
        var singleCardTemplate = new LuxTemplateDetailDto
        {
            Id              = template.Id,
            Name            = template.Name,
            Category        = template.Category,
            OutputType      = template.OutputType,
            Orientation     = template.Orientation,
            PageWidthMm     = template.CardWidthMm + template.MarginLeftMm + template.MarginRightMm,
            PageHeightMm    = template.CardHeightMm + template.MarginTopMm + template.MarginBottomMm,
            CardsPerRow     = 1,
            CardsPerColumn  = 1,
            CardWidthMm     = template.CardWidthMm,
            CardHeightMm    = template.CardHeightMm,
            MarginTopMm     = template.MarginTopMm,
            MarginBottomMm  = template.MarginBottomMm,
            MarginLeftMm    = template.MarginLeftMm,
            MarginRightMm   = template.MarginRightMm,
            BackgroundType  = template.BackgroundType,
            BackgroundColorHex  = template.BackgroundColorHex,
            BackgroundImagePath = template.BackgroundImagePath,
            ElementsJson    = template.ElementsJson,
        };

        return await RenderToPdfAsync(singleCardTemplate, new[] { previewVoucher }, ctx, ct);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Private — رسم كرت واحد
    // ══════════════════════════════════════════════════════════════════════

    private void RenderCard(
        PdfCanvas canvas,
        PdfDocument pdf,
        Document doc,
        LuxTemplateDetailDto template,
        IReadOnlyList<TemplateElement> elements,
        Dictionary<FieldToken, string> resolved,
        float cardX, float cardY, float cardW, float cardH)
    {
        // 1. الخلفية
        RenderBackground(canvas, template, cardX, cardY, cardW, cardH);

        // 2. العناصر مرتبة حسب ZIndex
        foreach (var element in elements.OrderBy(e => e.ZIndex))
        {
            if (!element.IsVisible) continue;

            // حساب موضع العنصر داخل الكرت (iText: Y من الأسفل)
            float elemX = cardX + element.X * MmToPt;
            float elemY = cardY + cardH - element.Y * MmToPt - element.Height * MmToPt;
            float elemW = element.Width  * MmToPt;
            float elemH = element.Height * MmToPt;

            switch (element)
            {
                case DynamicFieldElement dynField:
                    RenderDynamicField(canvas, pdf, dynField, resolved, elemX, elemY, elemW, elemH);
                    break;
                case TextElement text:
                    RenderText(canvas, pdf, text, text.Content, elemX, elemY, elemW, elemH);
                    break;
                case QrCodeElement qr:
                    RenderQrCode(canvas, qr, resolved, elemX, elemY, elemW, elemH);
                    break;
                case ImageElement img:
                    RenderImage(canvas, img, elemX, elemY, elemW, elemH);
                    break;
                case ShapeElement shape:
                    RenderShape(canvas, shape, elemX, elemY, elemW, elemH);
                    break;
                case LineElement line:
                    RenderLine(canvas, line, elemX, elemY, elemW, elemH);
                    break;
            }
        }

        // 3. إطار الكرت (اختياري — للتطوير)
        // canvas.SetStrokeColor(ColorConstants.LIGHT_GRAY).SetLineWidth(0.3f)
        //        .Rectangle(cardX, cardY, cardW, cardH).Stroke();
    }

    private static void RenderBackground(
        PdfCanvas canvas,
        LuxTemplateDetailDto template,
        float cardX, float cardY, float cardW, float cardH)
    {
        switch (template.BackgroundType)
        {
            case TemplateBackgroundType.Solid:
                if (!string.IsNullOrWhiteSpace(template.BackgroundColorHex))
                {
                    var color = HexToDeviceRgb(template.BackgroundColorHex);
                    canvas.SaveState()
                          .SetFillColor(color)
                          .Rectangle(cardX, cardY, cardW, cardH)
                          .Fill()
                          .RestoreState();
                }
                break;

            case TemplateBackgroundType.Image:
                if (!string.IsNullOrWhiteSpace(template.BackgroundImagePath)
                    && File.Exists(template.BackgroundImagePath))
                {
                    try
                    {
                        var imgData = iText.IO.Image.ImageDataFactory.Create(template.BackgroundImagePath);
                        canvas.AddImageFittedIntoRectangle(imgData,
                            new Rectangle(cardX, cardY, cardW, cardH), false);
                    }
                    catch (Exception)
                    {
                        // إذا فشل تحميل الصورة، نرسم خلفية بيضاء
                        canvas.SaveState()
                              .SetFillColor(ColorConstants.WHITE)
                              .Rectangle(cardX, cardY, cardW, cardH)
                              .Fill()
                              .RestoreState();
                    }
                }
                break;
        }
    }

    private void RenderDynamicField(
        PdfCanvas canvas, PdfDocument pdf,
        DynamicFieldElement element,
        Dictionary<FieldToken, string> resolved,
        float x, float y, float w, float h)
    {
        var value = resolved.TryGetValue(element.Token, out var v) && !string.IsNullOrEmpty(v)
            ? ApplyFormat(v, element.FormatString)
            : element.FallbackText;

        RenderText(canvas, pdf, element, value, x, y, w, h);
    }

    private void RenderText(
        PdfCanvas canvas, PdfDocument pdf,
        TextElement element, string text,
        float x, float y, float w, float h)
    {
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            var font = LoadArabicFont();
            float fontSize = element.FontSizePt;
            var color = HexToDeviceRgb(element.ColorHex);

            canvas.BeginText()
                  .SetFontAndSize(font, fontSize)
                  .SetFillColor(color)
                  .MoveText(x, y + h / 2 - fontSize / 3)
                  .ShowText(text)
                  .EndText();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render text element: {Text}", text);
        }
    }

    private static void RenderQrCode(
        PdfCanvas canvas,
        QrCodeElement element,
        Dictionary<FieldToken, string> resolved,
        float x, float y, float w, float h)
    {
        var data = element.StaticData;
        if (string.IsNullOrEmpty(data))
        {
            resolved.TryGetValue(element.DataToken, out data);
        }

        if (string.IsNullOrWhiteSpace(data)) return;

        if (!string.IsNullOrEmpty(element.UrlPrefix))
            data = element.UrlPrefix + data;

        try
        {
            // iText7: BarcodeQRCode.CreateFormXObject takes PdfDocument only
            var qrCode = new iText.Barcodes.BarcodeQRCode(data);
            var qrImage = qrCode.CreateFormXObject(canvas.GetDocument());

            float size = Math.Min(w, h);
            canvas.AddXObjectFittedIntoRectangle(qrImage, new Rectangle(x, y, size, size));
        }
        catch (Exception)
        {
            // رسم مربع placeholder إذا فشل QR
            canvas.SaveState()
                  .SetStrokeColor(ColorConstants.GRAY)
                  .SetLineWidth(0.5f)
                  .Rectangle(x, y, w, h)
                  .Stroke()
                  .RestoreState();
        }
    }

    private static void RenderImage(
        PdfCanvas canvas,
        ImageElement element,
        float x, float y, float w, float h)
    {
        var path = element.ImagePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            var imgData = iText.IO.Image.ImageDataFactory.Create(path);
            canvas.AddImageFittedIntoRectangle(imgData, new Rectangle(x, y, w, h), false);
        }
        catch (Exception)
        {
            // تجاهل الصور التي لا يمكن تحميلها
        }
    }

    private static void RenderShape(
        PdfCanvas canvas,
        ShapeElement element,
        float x, float y, float w, float h)
    {
        canvas.SaveState();

        if (!string.IsNullOrEmpty(element.FillColorHex))
            canvas.SetFillColor(HexToDeviceRgb(element.FillColorHex));

        if (element.StrokeWidth > 0)
        {
            canvas.SetStrokeColor(HexToDeviceRgb(element.StrokeColorHex));
            canvas.SetLineWidth(element.StrokeWidth * MmToPt);
        }

        switch (element.ShapeType)
        {
            case ShapeType.Rectangle:
            case ShapeType.RoundedRectangle:
                canvas.Rectangle(x, y, w, h);
                break;
            case ShapeType.Ellipse:
                canvas.Ellipse(x, y, x + w, y + h);
                break;
        }

        if (!string.IsNullOrEmpty(element.FillColorHex) && element.StrokeWidth > 0)
            canvas.FillStroke();
        else if (!string.IsNullOrEmpty(element.FillColorHex))
            canvas.Fill();
        else if (element.StrokeWidth > 0)
            canvas.Stroke();

        canvas.RestoreState();
    }

    private static void RenderLine(
        PdfCanvas canvas,
        LineElement element,
        float x, float y, float w, float h)
    {
        canvas.SaveState()
              .SetStrokeColor(HexToDeviceRgb(element.ColorHex))
              .SetLineWidth(element.Thickness * MmToPt);

        if (element.IsHorizontal)
            canvas.MoveTo(x, y + h / 2).LineTo(x + w, y + h / 2);
        else
            canvas.MoveTo(x + w / 2, y).LineTo(x + w / 2, y + h);

        canvas.Stroke().RestoreState();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Utilities
    // ══════════════════════════════════════════════════════════════════════

    private static IReadOnlyList<TemplateElement> ParseElements(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return Array.Empty<TemplateElement>();

        try
        {
            return JsonSerializer.Deserialize<List<TemplateElement>>(
                json, LuxTemplateJsonOptions.Default) ?? new List<TemplateElement>();
        }
        catch
        {
            return Array.Empty<TemplateElement>();
        }
    }

    private PdfFont? _arabicFont;
    private PdfFont LoadArabicFont()
    {
        if (_arabicFont is not null) return _arabicFont;

        try
        {
            // محاولة تحميل خط عربي من النظام
            var fontPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                "arial.ttf");

            if (System.IO.File.Exists(fontPath))
            {
                _arabicFont = PdfFontFactory.CreateFont(fontPath,
                    PdfEncodings.IDENTITY_H,
                    PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
                return _arabicFont;
            }
        }
        catch { /* fallback */ }

        // Fallback إلى Helvetica
        _arabicFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        return _arabicFont;
    }

    private static DeviceRgb HexToDeviceRgb(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return new DeviceRgb(r / 255f, g / 255f, b / 255f);
    }

    private static iText.Kernel.Colors.Color HexToColor(string hex)
        => HexToDeviceRgb(hex);

    private static (int r, int g, int b) ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            return (
                Convert.ToInt32(hex[..2], 16),
                Convert.ToInt32(hex[2..4], 16),
                Convert.ToInt32(hex[4..6], 16)
            );
        }
        return (0, 0, 0);
    }

    private static string ApplyFormat(string value, string? format)
    {
        if (string.IsNullOrEmpty(format)) return value;
        if (double.TryParse(value, out var num))
            return num.ToString(format);
        return value;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return string.Empty;
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F0} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F0} KB";
        return $"{bytes} B";
    }

    private static VoucherDto BuildSampleVoucher(Dictionary<FieldToken, string>? data)
    {
        return new VoucherDto
        {
            Username        = data?.GetValueOrDefault(FieldToken.Username) ?? "LUXCARD-0001",
            Password        = data?.GetValueOrDefault(FieldToken.Password) ?? "pass1234",
            Price           = 5000,
            Profile         = data?.GetValueOrDefault(FieldToken.Duration) ?? "1h",
            QuotaLimitBytes = 1_073_741_824L, // 1 GB
            AgentName       = data?.GetValueOrDefault(FieldToken.AgentName),
        };
    }
}
