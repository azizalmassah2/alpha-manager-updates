using System;
using System.Collections.Generic;
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

    public abstract void LayoutDocument(
        Document document,
        List<VoucherDto> vouchers,
        PrintSettingsDto settings,
        PdfFont arabicFont,
        IProgress<(int currentPage, int totalPages, string statusText)>? progress = null);

    protected ImageData? GetCachedLogo(PrintSettingsDto settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CompanyLogoPath) || !File.Exists(settings.CompanyLogoPath))
            return null;

        try
        {
            return ImageDataFactory.Create(settings.CompanyLogoPath);
        }
        catch
        {
            return null;
        }
    }

    protected void BuildVoucherContent(object container, VoucherDto v, PrintSettingsDto settings, ImageData? cachedLogoData)
    {
        if (cachedLogoData != null)
        {
            var img = new Image(cachedLogoData)
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetWidth(60);
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

        var userText = new Paragraph()
            .SetMarginTop(5)
            .SetMarginBottom(5)
            .SetFontSize(settings.FontSize);

        if (v.CredentialMode == CredentialMode.UsernameOnly)
        {
            userText.Add(new Text($"المستخدم: {v.Username}\n"))
                    .Add(new Text($"(بدون كلمة سر)\n"));
        }
        else if (v.CredentialMode == CredentialMode.UsernameEqualsPassword)
        {
            userText.Add(new Text($"المستخدم: {v.Username}\n"))
                    .Add(new Text($"المرور = المستخدم\n"));
        }
        else
        {
            userText.Add(new Text($"المستخدم: {v.Username}\n"))
                    .Add(new Text($"المرور: {v.Password}\n"));
        }
        
        userText.Add(new Text($"السرعة: {v.Profile}\n"))
                .Add(new Text($"السعر: {v.Price}"));
        AddElement(container, userText);

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
