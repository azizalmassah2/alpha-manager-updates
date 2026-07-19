using System.Collections.Generic;
using iText.Kernel.Font;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Infrastructure.Templates;

public class ThermalTemplate : BaseVoucherTemplate
{
    public override string TemplateName => "ThermalDefault";

    public override void LayoutDocument(Document document, List<VoucherDto> vouchers, PrintSettingsDto settings, PdfFont arabicFont)
    {
        // بناء קالب الرول الحراري (صفحة لكل فاتورة/كرت)
        bool isFirst = true;
        var cachedLogo = GetCachedLogo(settings);

        foreach (var v in vouchers)
        {
            if (!isFirst) document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
            isFirst = false;

            Div div = new Div()
                .SetPadding((float)settings.Padding)
                .SetTextAlignment(TextAlignment.CENTER);

            BuildVoucherContent(div, v, settings, cachedLogo);
            document.Add(div);
        }
    }
}
