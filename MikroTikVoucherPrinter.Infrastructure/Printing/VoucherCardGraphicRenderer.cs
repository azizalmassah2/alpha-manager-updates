using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using QRCoder;

namespace MikroTikVoucherPrinter.Infrastructure.Printing;

/// <summary>
/// محرك الرسم الموحد للكرت (Unified Voucher Card Graphic Engine)
/// يعتمد على الهندسة الفيزيائية المليمترية (1mm = 2.83465pt).
/// يقوم برسم الكرت بالكامل بمساحة العمل فقط وإرجاع صورة مطابقة 100% للمعاينة والطباعة.
/// </summary>
public static class VoucherCardGraphicRenderer
{
    private static readonly ConcurrentDictionary<string, Image> ImageCache = new();

    /// <summary>
    /// توليد الكرت كمصفوفة بايتات PNG لمعاينة شاشات WPF.
    /// </summary>
    public static byte[] RenderCardToPngBytes(TemplateConfig config, VoucherDto voucher, double dpi = 150)
    {
        using var bitmap = RenderCardInternal(config, voucher, (float)dpi);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// توليد الكرت كمصفوفة بايتات JPEG عالية الدقة (300 DPI) للطباعة في PDF.
    /// </summary>
    public static byte[] RenderCardToJpegBytes(TemplateConfig config, VoucherDto voucher, double dpi = 300, int quality = 90)
    {
        using var bitmap = RenderCardInternal(config, voucher, (float)dpi);
        using var ms = new MemoryStream();

        var encoder = GetEncoder(ImageFormat.Jpeg);
        if (encoder != null)
        {
            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Clamp(quality, 10, 95));
            bitmap.Save(ms, encoder, encoderParams);
        }
        else
        {
            bitmap.Save(ms, ImageFormat.Jpeg);
        }
        return ms.ToArray();
    }

    public static Bitmap RenderCardInternal(TemplateConfig config, VoucherDto voucher, float dpi)
    {
        float mmToPx = dpi / 25.4f;

        // حساب أبعاد مساحة العمل الفعلية للكرت بالمليمتر والبيكسل
        float cols = config.Columns > 0 ? config.Columns : 3;
        float rows = config.Rows > 0 ? config.Rows : 7;

        float cardWMm = (210.0f - (config.MarginX * cols)) / cols;
        float cardHMm = (297.0f - (config.MarginY * rows)) / rows;

        if (cardWMm <= 0) cardWMm = 70f;
        if (cardHMm <= 0) cardHMm = 40f;

        int widthPx = Math.Max(50, (int)Math.Round(cardWMm * mmToPx));
        int heightPx = Math.Max(30, (int)Math.Round(cardHMm * mmToPx));

        var bitmap = new Bitmap(widthPx, heightPx);
        bitmap.SetResolution(dpi, dpi);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 1. رسم خلفية الكرت (مقيدة بمساحة العمل widthPx x heightPx فقط وتجاهل طول الصورة الأصلي)
            DrawBackground(g, config, widthPx, heightPx);

            // 2. رسم إطار الكرت
            DrawFrame(g, config, widthPx, heightPx, mmToPx);

            // 3. رسم الشعار
            DrawLogo(g, config, widthPx, heightPx);

            // 4. إعداد ألوان وخط النصوص (1 مم في الحجم = 2.83465pt في الخط)
            var fontColor = ParseColor(config.FontColorHex, Color.Black);
            var fontStyle = FontStyle.Regular;
            if (config.IsBold) fontStyle |= FontStyle.Bold;
            if (config.IsItalic) fontStyle |= FontStyle.Italic;

            string fontName = string.IsNullOrWhiteSpace(config.FontFamily) ? "Arial" : config.FontFamily;
            float fontSizePt = Math.Max(6f, config.FontSize * 2.83465f);

            using var font = new Font(fontName, fontSizePt, fontStyle, GraphicsUnit.Point);
            using var brush = new SolidBrush(fontColor);

            // 5. رسم العناصر النصية في مواضعها المليمترية الدقيقة (Top-Left Origin)
            if (config.ShowUsername)
                DrawTextElement(g, voucher.Username, config.UsernameX, config.UsernameY, mmToPx, font, brush);

            if (config.ShowPassword)
            {
                string passText = voucher.CredentialMode == CredentialMode.UsernameOnly ? "(بدون كلمة سر)"
                    : voucher.CredentialMode == CredentialMode.UsernameEqualsPassword ? "السر = المستخدم"
                    : voucher.Password;
                DrawTextElement(g, passText, config.PasswordX, config.PasswordY, mmToPx, font, brush);
            }

            if (config.ShowPrice && voucher.Price > 0)
                DrawTextElement(g, $"{voucher.Price:0} ريال", config.PriceX, config.PriceY, mmToPx, font, brush);

            if (config.ShowValidity && !string.IsNullOrEmpty(voucher.Profile))
                DrawTextElement(g, voucher.Profile, config.ValidityX, config.ValidityY, mmToPx, font, brush);

            if (config.ShowSerialNumber)
                DrawTextElement(g, voucher.Id.ToString()[..Math.Min(8, voucher.Id.ToString().Length)].ToUpper(), config.SerialNumberX, config.SerialNumberY, mmToPx, font, brush);

            if (config.ShowPrintDate)
                DrawTextElement(g, DateTime.Now.ToString("yyyy/MM/dd"), config.PrintDateX, config.PrintDateY, mmToPx, font, brush);

            if (config.ShowTime && !string.IsNullOrEmpty(voucher.Profile))
                DrawTextElement(g, $"⏱ {voucher.Profile}", config.TimeX, config.TimeY, mmToPx, font, brush);

            // 6. رسم الـ QR Code
            if (config.ShowQr)
            {
                DrawQrCode(g, config, voucher, mmToPx);
            }
        }

        return bitmap;
    }

    private static void DrawBackground(Graphics g, TemplateConfig config, int widthPx, int heightPx)
    {
        if (!string.IsNullOrEmpty(config.BackgroundImagePath) && File.Exists(config.BackgroundImagePath))
        {
            var bgImg = GetCachedImage(config.BackgroundImagePath);
            if (bgImg != null)
            {
                // توحيد الحجم: رسم الصورة بمساحة العمل الفعلية (widthPx x heightPx) وتجاهل طولها الأصلي
                g.DrawImage(bgImg, new Rectangle(0, 0, widthPx, heightPx));
                return;
            }
        }
        g.Clear(Color.White);
    }

    private static void DrawFrame(Graphics g, TemplateConfig config, int widthPx, int heightPx, float mmToPx)
    {
        if (config.FrameSize > 0.05f)
        {
            float penThickness = config.FrameSize * mmToPx;
            var frameColor = ParseColor(config.FrameColorHex, Color.Black);
            using var pen = new Pen(frameColor, penThickness);
            float inset = penThickness / 2.0f;
            g.DrawRectangle(pen, inset, inset, widthPx - penThickness, heightPx - penThickness);
        }
    }

    private static void DrawLogo(Graphics g, TemplateConfig config, int widthPx, int heightPx)
    {
        if (!string.IsNullOrEmpty(config.LogoImagePath) && File.Exists(config.LogoImagePath))
        {
            var logoImg = GetCachedImage(config.LogoImagePath);
            if (logoImg != null)
            {
                float logoW = Math.Min(widthPx * 0.25f, 120f);
                g.DrawImage(logoImg, new RectangleF(10f, 10f, logoW, logoW));
            }
        }
    }

    private static void DrawTextElement(Graphics g, string text, float mmX, float mmY, float mmToPx, Font font, Brush brush)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        float posX = mmX * mmToPx;
        float posY = mmY * mmToPx;
        g.DrawString(text, font, brush, posX, posY);
    }

    private static void DrawQrCode(Graphics g, TemplateConfig config, VoucherDto voucher, float mmToPx)
    {
        try
        {
            string qrText = $"u={voucher.Username}";
            using var qrGen = new QRCodeGenerator();
            var qrData = qrGen.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrData);
            byte[] qrBytes = qrCode.GetGraphic(3);

            using var ms = new MemoryStream(qrBytes);
            using var qrBitmap = Image.FromStream(ms);

            float qrX = config.QrX * mmToPx;
            float qrY = config.QrY * mmToPx;
            float qrSize = config.QrSize * mmToPx;

            g.DrawImage(qrBitmap, new RectangleF(qrX, qrY, qrSize, qrSize));
        }
        catch { /* Quiet fallback */ }
    }

    private static Image? GetCachedImage(string path)
    {
        return ImageCache.GetOrAdd(path, p =>
        {
            try
            {
                return Image.FromFile(p);
            }
            catch
            {
                return null!;
            }
        });
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return fallback;
        }
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        var decoders = ImageCodecInfo.GetImageDecoders();
        foreach (var codec in decoders)
        {
            if (codec.FormatID == format.Guid)
                return codec;
        }
        return null;
    }
}
