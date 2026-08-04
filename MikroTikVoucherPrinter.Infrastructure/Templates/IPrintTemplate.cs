using System;
using System.Collections.Generic;
using iText.Kernel.Font;
using iText.Layout;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Infrastructure.Templates;

public interface IPrintTemplate
{
    string TemplateName { get; }
    void LayoutDocument(
        Document document,
        List<VoucherDto> vouchers,
        PrintSettingsDto settings,
        PdfFont arabicFont,
        IProgress<(int currentPage, int totalPages, string statusText)>? progress = null);
}
