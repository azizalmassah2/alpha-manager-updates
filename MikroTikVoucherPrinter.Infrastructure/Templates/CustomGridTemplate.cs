using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Renderer;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using QRCoder;
// aliases ظ„ط­ظ„ ط§ظ„طھط¹ط§ط±ط¶ ط¨ظٹظ† System.Drawing ظˆ iText7
using PdfColor        = iText.Kernel.Colors.Color;
using PdfColorConsts  = iText.Kernel.Colors.ColorConstants;
using DeviceRgb       = iText.Kernel.Colors.DeviceRgb;
using PdfRect         = iText.Kernel.Geom.Rectangle;
using SysBitmap       = System.Drawing.Bitmap;
using SysImage        = System.Drawing.Image;
using MikroTikVoucherPrinter.Infrastructure.Printing;

namespace MikroTikVoucherPrinter.Infrastructure.Templates;

public class CustomGridTemplate : IPrintTemplate
{
    public string TemplateName => "CustomGridDefault";

    private readonly TemplateConfig _config;

    public CustomGridTemplate(TemplateConfig config)
    {
        _config = config;
    }

    public void LayoutDocument(
        Document document,
        List<VoucherDto> vouchers,
        PrintSettingsDto settings,
        PdfFont arabicFont,
        IProgress<(int currentPage, int totalPages, string statusText)>? progress = null)
    {
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Absolute Grid Layout (A4) â€” ظٹط·ط§ط¨ظ‚ ظ…ط®ط±ط¬ط§طھ ط§ظ„ط·ط¨ط§ط¹ط© ط§ظ„ظ†ظ‡ط§ط¦ظٹط©
        //  ط§ظ„ظ‡ط¯ظپ: ط´ط¨ظƒط© ط«ط§ط¨طھط© (Rows/Columns) + ط£ط¨ط¹ط§ط¯ ط¨ط§ظ„ظ…ظ„ظٹظ…طھط± + ظ‡ظˆط§ظ…ط´/طھط¨ط§ط¹ط¯
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var pdfDoc = document.GetPdfDocument();

        int cols = _config.Columns > 0 ? _config.Columns : 3;
        int rows = _config.Rows > 0 ? _config.Rows : 7;
        int perPage = cols * rows;

        float mmToPt = 2.83465f;
        float gapXPt = Math.Max(0, _config.MarginX) * mmToPt;
        float gapYPt = Math.Max(0, _config.MarginY) * mmToPt;

        PageSize pageSize = pdfDoc.GetDefaultPageSize();
        float left = document.GetLeftMargin();
        float right = document.GetRightMargin();
        float topMargin = document.GetTopMargin();
        float bottomMargin = document.GetBottomMargin();

        float printableWidthPt = pageSize.GetWidth() - left - right;
        float printableHeightPt = pageSize.GetHeight() - topMargin - bottomMargin;

        float cellWidth = printableWidthPt / cols;
        float cellHeight = printableHeightPt / rows;

        float cardWidthPt = Math.Max(1f, cellWidth - gapXPt);
        float cardHeightPt = Math.Max(1f, cellHeight - gapYPt);

        // طھط¬ظ‡ظٹط² ط§ظ„طµظˆط±ط© ط§ظ„ط®ظ„ظپظٹط© ظ…ط±ط© ظˆط§ط­ط¯ط©
        ImageData? bgImageData = null;
        if (!string.IsNullOrEmpty(_config.BackgroundImagePath) && File.Exists(_config.BackgroundImagePath))
        {
            byte[] imgBytes = settings.CompressOutput
                ? CompressImage(_config.BackgroundImagePath, settings.MaxImageSidePx, settings.ImageQuality)
                : File.ReadAllBytes(_config.BackgroundImagePath);
            bgImageData = ImageDataFactory.Create(imgBytes);
        }

        // طھط¬ظ‡ظٹط² ط§ظ„ط´ط¹ط§ط± ظ…ط±ط© ظˆط§ط­ط¯ط©
        ImageData? logoImageData = null;
        if (!string.IsNullOrEmpty(_config.LogoImagePath) && File.Exists(_config.LogoImagePath))
        {
            byte[] logoBytes = settings.CompressOutput
                ? CompressImage(_config.LogoImagePath, 150, settings.ImageQuality)
                : File.ReadAllBytes(_config.LogoImagePath);
            logoImageData = ImageDataFactory.Create(logoBytes);
        }

        // طھظپط±ظٹط؛ ظ„ظˆظ† ط§ظ„ط®ط·
        var fontColor = CustomGridTemplateDrawing.ParseHexColor(_config.FontColorHex, PdfColorConsts.BLACK);
        var frameColor = CustomGridTemplateDrawing.ParseHexColor(_config.FrameColorHex, PdfColorConsts.BLACK);
        float frameSizePt = Math.Max(0, _config.FrameSize) * mmToPt;

        // ظ…ط³ط§ط¹ط¯ط© ظ„ط­ط³ط§ط¨ ظ†ظ‚ط·ط© ط¨ط¯ط§ظٹط© ط§ظ„ط´ط¨ظƒط© ط¯ط§ط®ظ„ ط§ظ„طµظپط­ط© (ظ…ظ† ط£ط¹ظ„ظ‰ ظٹط³ط§ط±)
        float top = pageSize.GetTop() - topMargin;

        // إضافة الصفحة الأولى يدويّاً
        pdfDoc.AddNewPage(pageSize);

        int totalPages = (int)Math.Max(1, Math.Ceiling((double)vouchers.Count / perPage));

        for (int i = 0; i < vouchers.Count; i++)
        {
            int currentPage = (i / perPage) + 1;
            int indexOnPage = i % perPage;

            if (indexOnPage == 0)
            {
                progress?.Report((currentPage, totalPages, $"جاري رسم صفحة الكروت ({currentPage} من {totalPages})..."));
            }

            if (i > 0 && indexOnPage == 0)
            {
                pdfDoc.AddNewPage(pageSize);
            }
            int r = indexOnPage / cols;
            int c = indexOnPage % cols;

            float x = left + c * cellWidth + (gapXPt / 2f);
            float yTop = top - r * cellHeight - (gapYPt / 2f);
            float y = yTop - cardHeightPt;

            if (y < bottomMargin - 1f)
                continue;

            var rect = new PdfRect(x, y, cardWidthPt, cardHeightPt);
            var canvas = new PdfCanvas(pdfDoc.GetLastPage());
            var renderCanvas = new Canvas(canvas, rect);

            // [NEW ARCHITECTURE] توليد صورة الكرت بدقة 300 DPI عبر محرك الرسم الموحد وإدراجها بالـ PDF
            byte[] cardJpegBytes = VoucherCardGraphicRenderer.RenderCardToJpegBytes(_config, vouchers[i], dpi: 300, quality: settings.ImageQuality);
            var cardImage = new Image(ImageDataFactory.Create(cardJpegBytes))
                .SetFixedPosition(x, y)
                .SetWidth(cardWidthPt)
                .SetHeight(cardHeightPt);

            renderCanvas.Add(cardImage);
            renderCanvas.Close();
        }
    }

    /// <summary>
    /// ظٹط¶ط؛ط· طµظˆط±ط© ط¨طھظ‚ظ„ظٹظ„ ط§ظ„ط¯ظ‚ط© ظˆط¬ظˆط¯ط© JPEG ظ„طھظ‚ظ„ظٹطµ ط­ط¬ظ… ظ…ظ„ظپ PDF ط§ظ„ظ†ط§طھط¬
    /// </summary>
    private static byte[] CompressImage(string imagePath, int maxSidePx, int quality)
    {
        try
        {
            using var original = SysImage.FromFile(imagePath);
            int w = original.Width;
            int h = original.Height;

            if (w > maxSidePx || h > maxSidePx)
            {
                float scale = Math.Min((float)maxSidePx / w, (float)maxSidePx / h);
                w = Math.Max(1, (int)(w * scale));
                h = Math.Max(1, (int)(h * scale));
            }

            using var resized = new SysBitmap(original, w, h);
            using var ms = new MemoryStream();

            var jpegEncoder = GetEncoder(ImageFormat.Jpeg)!;
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Clamp(quality, 10, 95));

            resized.Save(ms, jpegEncoder, encoderParams);
            return ms.ToArray();
        }
        catch
        {
            return File.ReadAllBytes(imagePath);
        }
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
        => ImageCodecInfo.GetImageDecoders().FirstOrDefault(c => c.FormatID == format.Guid);
}

public class CustomCardCellRenderer : CellRenderer
{
    private readonly VoucherDto _voucher;
    private readonly TemplateConfig _config;
    private readonly ImageData? _bgImageData;
    private readonly PdfColor _fontColor;
    private readonly PdfFont _arabicFont;
    private readonly PrintSettingsDto _settings;

    public CustomCardCellRenderer(Cell modelElement, VoucherDto voucher, TemplateConfig config, ImageData? bgImageData, PdfColor fontColor, PdfFont arabicFont, PrintSettingsDto settings)
        : base(modelElement)
    {
        _voucher = voucher;
        _config = config;
        _bgImageData = bgImageData;
        _fontColor = fontColor;
        _arabicFont = arabicFont;
        _settings = settings;
    }

    public override IRenderer GetNextRenderer() => new CustomCardCellRenderer((Cell)GetModelElement(), _voucher, _config, _bgImageData, _fontColor, _arabicFont, _settings);

    public override void Draw(DrawContext drawContext)
    {
        base.Draw(drawContext); // ظٹط±ط³ظ… ط­ط¯ظˆط¯ ط§ظ„ط®ظ„ظٹط© ط¥ط°ط§ ظˆط¬ط¯طھ

        PdfCanvas canvas = drawContext.GetCanvas();
        PdfRect rect = GetOccupiedAreaBBox();

        float x = rect.GetLeft();
        float y = rect.GetBottom();
        float w = rect.GetWidth();
        float h = rect.GetHeight();

        // 2. ط¥ط¹ط¯ط§ط¯ ط§ظ„ط±ط³ظ… ظ„ظ„ظ†طµظˆطµ
        var pdfDoc = drawContext.GetDocument();
        Canvas renderCanvas = new Canvas(canvas, rect); // ظ„ط·ط¨ط§ط¹ط© ط§ظ„ظ€ Text/Image ط¯ط§ط®ظ„ ط§ظ„ظ€ Cell ط¨ط³ظ‡ظˆظ„ط©

        // 1. ط±ط³ظ… طµظˆط±ط© ط§ظ„ط®ظ„ظپظٹط©
        if (_bgImageData != null)
        {
            var bgImg = new Image(_bgImageData)
                .SetFixedPosition(x, y)
                .SetWidth(w)
                .SetHeight(h);
            renderCanvas.Add(bgImg);
        }

        // ظ…ط¹ط§ظ…ظ„ ط§ظ„طھط­ظˆظٹظ„ ظ…ظ† ط§ظ„ظ…ظ„ظٹظ…طھط± ط¥ظ„ظ‰ ظ†ظ‚ط§ط·
        float mmToPt = 2.83465f;

        // 3. ط±ط³ظ… ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ…
        if (_config.ShowUsername)
        {
            float ux = x + (_config.UsernameX * mmToPt);
            float uy = y + (_config.UsernameY * mmToPt);
            DrawText(renderCanvas, _voucher.Username, ux, uy, _config.FontSize, _fontColor, _arabicFont);
        }

        // 4. ط±ط³ظ… ظƒظ„ظ…ط© ط§ظ„ط³ط±
        if (_config.ShowPassword)
        {
            float px = x + (_config.PasswordX * mmToPt);
            float py = y + (_config.PasswordY * mmToPt);
            
            // طھط­ط¯ظٹط¯ ظ‚ظٹظ…ط© ظƒظ„ظ…ط© ط§ظ„ط³ط± ط§ظ„ظ…ط¹ط±ظˆط¶ط© ط¨ظ†ط§ط،ظ‹ ط¹ظ„ظ‰ CredentialMode
            string passText = "";
            if (_voucher.CredentialMode == CredentialMode.UsernameOnly)
                passText = "(ط¨ط¯ظˆظ† ظƒظ„ظ…ط© ط³ط±)";
            else if (_voucher.CredentialMode == CredentialMode.UsernameEqualsPassword)
                passText = "ط§ظ„ظ…ط±ظˆط± = ط§ظ„ظ…ط³طھط®ط¯ظ…";
            else
                passText = _voucher.Password;

            DrawText(renderCanvas, passText, px, py, _config.FontSize, _fontColor, _arabicFont);
        }

        // 5. ط±ط³ظ… ط§ظ„ط³ط¹ط±
        if (_config.ShowPrice)
        {
            float priceX = x + (_config.PriceX * mmToPt);
            float priceY = y + (_config.PriceY * mmToPt);
            string priceStr = _voucher.Price > 0 ? _voucher.Price.ToString("0") : "0";
            DrawText(renderCanvas, $"{priceStr} ط±ظٹط§ظ„", priceX, priceY, _config.FontSize, _fontColor, _arabicFont);
        }

        // 6. ط±ط³ظ… ط§ظ„ظ€ QR Code
        if (_config.ShowQr)
        {
            float qx = x + (_config.QrX * mmToPt);
            float qy = y + (_config.QrY * mmToPt);
            float qSize = _config.QrSize * mmToPt;

            string loginUri;
            if (_settings.UseTokenForQr)
            {
                loginUri = $"{_settings.QrBaseUrl.TrimEnd('/')}?token={_voucher.Username}";
            }
            else
            {
                if (_voucher.CredentialMode == CredentialMode.UsernameOnly)
                    loginUri = $"{_settings.QrBaseUrl.TrimEnd('/')}?username={_voucher.Username}";
                else
                {
                    string effPass = _voucher.CredentialMode == CredentialMode.UsernameEqualsPassword ? _voucher.Username : _voucher.Password;
                    loginUri = $"{_settings.QrBaseUrl.TrimEnd('/')}?username={_voucher.Username}&password={effPass}";
                }
            }

            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(loginUri, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(3);

            var img = new Image(ImageDataFactory.Create(qrCodeImage))
                .SetFixedPosition(qx, qy)
                .SetWidth(qSize)
                .SetHeight(qSize);
            renderCanvas.Add(img);
        }
    }

    private void DrawText(Canvas canvas, string text, float x, float y, float size, PdfColor PdfColor, PdfFont font)
    {
        // Text is drawn at Absolute Position with Left alignment
        var p = new Paragraph(text)
            .SetFont(font)
            .SetFontSize(size)
            .SetFontColor(PdfColor)
            .SetFixedPosition(x, y, 200f); // 200f width limit

        canvas.Add(p);
    }
}

internal static class CustomGridTemplateDrawing
{
    internal static PdfColor ParseHexColor(string? hex, PdfColor fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            if (!hex.StartsWith("#") || hex.Length != 7) return fallback;
            var r = Convert.ToInt32(hex.Substring(1, 2), 16);
            var g = Convert.ToInt32(hex.Substring(3, 2), 16);
            var b = Convert.ToInt32(hex.Substring(5, 2), 16);
            return new DeviceRgb(r, g, b);
        }
        catch
        {
            return fallback;
        }
    }

    internal static void DrawCard(
        Canvas renderCanvas,
        PdfRect rect,
        VoucherDto voucher,
        ImageData? bgImageData,
        ImageData? logoImageData,
        PdfColor fontColor,
        PdfColor frameColor,
        float frameSizePt,
        PdfFont arabicFont,
        PrintSettingsDto settings,
        MikroTikVoucherPrinter.Domain.Entities.TemplateConfig config)
    {
        float x = rect.GetLeft();
        float y = rect.GetBottom();
        float w = rect.GetWidth();
        float h = rect.GetHeight();
        float mmToPt = 2.83465f;

        if (bgImageData != null)
            renderCanvas.Add(new Image(bgImageData).SetFixedPosition(x, y).SetWidth(w).SetHeight(h));

        // 2) الإطار
        if (frameSizePt > 0.1f)
        {
            var c = renderCanvas.GetPdfCanvas();
            c.SaveState();
            c.SetStrokeColor(frameColor);
            c.SetLineWidth(frameSizePt);
            float inset = frameSizePt / 2f;
            c.Rectangle(x + inset, y + inset, w - frameSizePt, h - frameSizePt);
            c.Stroke();
            c.RestoreState();
        }

        // 3) الشعار
        if (logoImageData != null)
        {
            float logoW = Math.Min(w * 0.18f, 45f);
            renderCanvas.Add(new Image(logoImageData)
                .SetFixedPosition(x + w - logoW - 6f, y + h - logoW - 6f)
                .SetWidth(logoW).SetHeight(logoW));
        }

        // [FIX I-03] معامل خط 2.25f لتحويل حجم الخط من مم إلى نقاط PDF لتطابق المعاينة
        float fs = Math.Max(4f, config.FontSize * 2.25f);

        // إحداثيات سين وصاد تحسب مباشرةً من أعلى ويسار الكرت بنفس طريقة WPF Canvas
        float PdfX(float mmX) => x + (mmX * mmToPt);
        float PdfY(float mmY) => y + h - (mmY * mmToPt) - fs;

        float GetAvailableWidth(float mmX) => Math.Max(30f, w - (mmX * mmToPt));

        // 4) رسم العناصر حسب إعدادات القالب
        if (config.ShowUsername)
            DrawText(renderCanvas, voucher.Username,
                PdfX(config.UsernameX), PdfY(config.UsernameY), GetAvailableWidth(config.UsernameX), fs, fontColor, arabicFont);

        if (config.ShowPassword)
        {
            string passText = voucher.CredentialMode == CredentialMode.UsernameOnly ? "(بدون كلمة سر)"
                : voucher.CredentialMode == CredentialMode.UsernameEqualsPassword ? "السر = المستخدم"
                : voucher.Password;
            DrawText(renderCanvas, passText,
                PdfX(config.PasswordX), PdfY(config.PasswordY), GetAvailableWidth(config.PasswordX), fs, fontColor, arabicFont);
        }

        if (config.ShowPrice && voucher.Price > 0)
            DrawText(renderCanvas, $"{voucher.Price:0} ريال",
                PdfX(config.PriceX), PdfY(config.PriceY), GetAvailableWidth(config.PriceX), fs, fontColor, arabicFont);

        if (config.ShowValidity && !string.IsNullOrEmpty(voucher.Profile))
            DrawText(renderCanvas, voucher.Profile,
                PdfX(config.ValidityX), PdfY(config.ValidityY), GetAvailableWidth(config.ValidityX), fs, fontColor, arabicFont);

        if (config.ShowSerialNumber)
            DrawText(renderCanvas, voucher.Id.ToString()[..8].ToUpper(),
                PdfX(config.SerialNumberX), PdfY(config.SerialNumberY), GetAvailableWidth(config.SerialNumberX), fs, fontColor, arabicFont);

        if (config.ShowPrintDate)
            DrawText(renderCanvas, DateTime.Now.ToString("yyyy/MM/dd"),
                PdfX(config.PrintDateX), PdfY(config.PrintDateY), GetAvailableWidth(config.PrintDateX), fs, fontColor, arabicFont);

        if (config.ShowTime && !string.IsNullOrEmpty(voucher.Profile))
            DrawText(renderCanvas, $"⏱ {voucher.Profile}",
                PdfX(config.TimeX), PdfY(config.TimeY), GetAvailableWidth(config.TimeX), fs, fontColor, arabicFont);

        // QR Code
        if (config.ShowQr)
        {
            try
            {
                string qrText = $"{settings.QrBaseUrl.TrimEnd('/')}?u={voucher.Username}";
                using var qrGen = new QRCoder.QRCodeGenerator();
                var qrData = qrGen.CreateQrCode(qrText, QRCoder.QRCodeGenerator.ECCLevel.Q);
                var qrCode = new QRCoder.PngByteQRCode(qrData);
                byte[] qrBytes = qrCode.GetGraphic(3);
                float qrSize = config.QrSize * mmToPt;
                
                // حساب X و Y مباشرةً من أعلى ويسار الكرت (مطابق لـ Canvas)
                float qrPdfX = x + (config.QrX * mmToPt);
                float qrPdfY = y + h - (config.QrY * mmToPt) - qrSize;
                
                renderCanvas.Add(new Image(ImageDataFactory.Create(qrBytes))
                    .SetFixedPosition(qrPdfX, qrPdfY)
                    .SetWidth(qrSize).SetHeight(qrSize));
            }
            catch { /* QR rendering failed silently */ }
        }
    }

    private static void DrawText(Canvas canvas, string text,
        float px, float py, float width, float fontSize, PdfColor PdfColor, PdfFont font)
    {
        if (string.IsNullOrEmpty(text)) return;
        canvas.Add(new Paragraph(text)
            .SetFont(font)
            .SetFontSize(fontSize)
            .SetFontColor(PdfColor)
            .SetTextAlignment(TextAlignment.LEFT)
            .SetFixedPosition(px, py, width));
    }
}


