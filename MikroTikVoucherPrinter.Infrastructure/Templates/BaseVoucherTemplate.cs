using System.IO;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using QRCoder;

namespace MikroTikVoucherPrinter.Infrastructure.Templates;

public abstract class BaseVoucherTemplate : IPrintTemplate
{
    public abstract string TemplateName { get; }

    public abstract void LayoutDocument(Document document, System.Collections.Generic.List<VoucherDto> vouchers, PrintSettingsDto settings, PdfFont arabicFont);

    protected ImageData GetCachedLogo(PrintSettingsDto settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CompanyLogoPath) || !File.Exists(settings.CompanyLogoPath))
            return null;

        try
        {
            // ط¥ط¹ط§ط¯ط© ط§ط³طھط؛ظ„ط§ظ„ ط§ظ„ظ€ ImageData ظٹط­ظ…ظٹ ط§ظ„ط°ط§ظƒط±ط© ظˆظٹط¬ط¹ظ„ ط§ظ„ظ€ PDF ط®ظپظٹظپط§ظ‹ ط¬ط¯ط§ظ‹ ظ…ظ‡ظ…ط§ طھظƒط±ط±طھ ط§ظ„طµظˆط±ط©
            return ImageDataFactory.Create(settings.CompanyLogoPath);
        }
        catch
        {
            return null; // Fallback ط¥ط°ط§ ظƒط§ظ† ظ…ظ„ظپ ط§ظ„طµظˆط±ط© ظ…ط¹ط·ظˆط¨ط§ظ‹
        }
    }

    protected void BuildVoucherContent(object container, VoucherDto v, PrintSettingsDto settings, ImageData cachedLogoData)
    {
        // ط¥ط¶ط§ظپط© ط§ظ„ط´ط¹ط§ط± ط¥ط°ط§ ظˆظڈط¬ط¯
        if (cachedLogoData != null)
        {
            // ط¥ظ†ط´ط§ط، instance ط¬ط¯ظٹط¯ط© طھط¹طھظ…ط¯ ط¹ظ„ظ‰ ظ†ظپط³ ×”ظ€ ImageData ظ„طھظˆظپط± ط§ظ„ط°ط§ظƒط±ط©
            var img = new Image(cachedLogoData)
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetWidth(60); // Optimize size
            AddElement(container, img);
        }
        else if (!string.IsNullOrEmpty(settings.CompanyName))
        {
            var header = new Paragraph(settings.CompanyName)
                .SetFontSize(settings.FontSize + 2)
                .SetMarginTop(2)
                .SetMarginBottom(2);
            AddElement(container, header);
        }

        // ط§ظ„طھظپط§طµظٹظ„ ط§ظ„ط£ط³ط§ط³ظٹط©
        var userText = new Paragraph()
            .SetMarginTop(5)
            .SetMarginBottom(5)
            .SetFontSize(settings.FontSize);

        if (v.CredentialMode == CredentialMode.UsernameOnly)
        {
            userText.Add(new Text($"ط§ظ„ظ…ط³طھط®ط¯ظ…: {v.Username}\n"))
                    .Add(new Text($"(ط¨ط¯ظˆظ† ظƒظ„ظ…ط© ط³ط±)\n"));
        }
        else if (v.CredentialMode == CredentialMode.UsernameEqualsPassword)
        {
            userText.Add(new Text($"ط§ظ„ظ…ط³طھط®ط¯ظ…: {v.Username}\n"))
                    .Add(new Text($"ط§ظ„ظ…ط±ظˆط± = ط§ظ„ظ…ط³طھط®ط¯ظ…\n"));
        }
        else
        {
            userText.Add(new Text($"ط§ظ„ظ…ط³طھط®ط¯ظ…: {v.Username}\n"))
                    .Add(new Text($"ط§ظ„ظ…ط±ظˆط±: {v.Password}\n"));
        }
        
        userText.Add(new Text($"ط§ظ„ط³ط±ط¹ط©: {v.Profile}\n"))
                .Add(new Text($"ط§ظ„ط³ط¹ط±: {v.Price}"));
        AddElement(container, userText);

        // ط¥ط¶ط§ظپط© ط±ظ…ط² ط§ظ„ظ€ QR ظ…ط¹ ط¯ط¹ظ…ظ‡ ظ„ظ„ظ€ Token-based login ظˆ CredentialMode
        if (settings.ShowQrCode)
        {
            string loginUri;
            if (settings.UseTokenForQr)
            {
                loginUri = $"{settings.QrBaseUrl.TrimEnd('/')}?token={v.Username}";
            }
            else
            {
                if (v.CredentialMode == CredentialMode.UsernameOnly)
                {
                    loginUri = $"{settings.QrBaseUrl.TrimEnd('/')}?username={v.Username}";
                }
                else
                {
                    string effPass = v.CredentialMode == CredentialMode.UsernameEqualsPassword ? v.Username : v.Password;
                    loginUri = $"{settings.QrBaseUrl.TrimEnd('/')}?username={v.Username}&password={effPass}";
                }
            }

            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(loginUri, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(3);

            var img = new Image(ImageDataFactory.Create(qrCodeImage))
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetWidth(65)
                .SetHeight(65);
            AddElement(container, img);
        }

        // ط§ظ„طھط°ظٹظٹظ„ ط§ظ„ظ…ط®طµطµ
        if (!string.IsNullOrEmpty(settings.FooterText))
        {
            var footer = new Paragraph(settings.FooterText)
                .SetFontSize(settings.FontSize - 3)
                .SetMarginTop(3);
            AddElement(container, footer);
        }
    }

    private void AddElement(object container, object element)
    {
        if (container is Cell cell)
        {
            if (element is IBlockElement block) cell.Add(block);
            else if (element is Image img) cell.Add(img);
        }
        else if (container is Div div)
        {
            if (element is IBlockElement block) div.Add(block);
            else if (element is Image img) div.Add(img);
        }
    }
}
