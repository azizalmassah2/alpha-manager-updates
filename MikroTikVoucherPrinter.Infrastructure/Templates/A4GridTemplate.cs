using System.Collections.Generic;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Infrastructure.Templates;

public class A4GridTemplate : BaseVoucherTemplate
{
    public override string TemplateName => "A4GridDefault";

    public override void LayoutDocument(Document document, List<VoucherDto> vouchers, PrintSettingsDto settings, PdfFont arabicFont)
    {
        int cols = settings.CardsPerRow > 0 ? settings.CardsPerRow : 4;
        Table table = new Table(cols).UseAllAvailableWidth().SetHorizontalAlignment(HorizontalAlignment.CENTER);

        // تجهيز השعار مرة واحدة (Optimization Cache)
        var cachedLogo = GetCachedLogo(settings);

        foreach (var v in vouchers)
        {
            Cell cell = new Cell()
                .SetPadding((float)settings.Padding)
                .SetBorder(new SolidBorder(ColorConstants.GRAY, 1))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetHeight((float)settings.CardHeight);

            BuildVoucherContent(cell, v, settings, cachedLogo);
            table.AddCell(cell);
        }

        document.Add(table);
    }
}
