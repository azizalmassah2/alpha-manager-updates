using System;
using System.Collections.Generic;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Renderer;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Infrastructure.Templates;

public class HawaeGridTemplate : IPrintTemplate
{
    public string TemplateName => "HawaeGridDefault";

    public void LayoutDocument(Document document, List<VoucherDto> vouchers, PrintSettingsDto settings, PdfFont arabicFont)
    {
        int cols = settings.CardsPerRow > 0 ? settings.CardsPerRow : 4;
        Table table = new Table(cols).UseAllAvailableWidth().SetHorizontalAlignment(HorizontalAlignment.CENTER);

        // A4 height ~842 pt. With 10 rows â†’ ~82 pt each
        float cardHeight = 82f;

        foreach (var v in vouchers)
        {
            Cell cell = new Cell()
                .SetHeight(cardHeight)
                .SetPadding(0)
                .SetBorder(new SolidBorder(ColorConstants.WHITE, 1));

            cell.SetNextRenderer(new HawaeCellRenderer(cell));

            // Inner 3-column layout: Left (price) | Middle (credentials) | Right (company)
            Table innerTable = new Table(new float[] { 1, 2, 1 }).UseAllAvailableWidth().SetHeight(cardHeight);
            innerTable.SetMargin(0).SetPadding(0);

            // â”€â”€â”€ Left Red Area â€” ط§ظ„ط³ط¹ط± â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Cell leftCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(2).SetTextAlignment(TextAlignment.CENTER);
            var leftStack = new Paragraph().SetFont(arabicFont).SetFontColor(ColorConstants.WHITE);

            string priceValue = v.Price > 0 ? v.Price.ToString("0") : "0";
            leftStack.Add(new Text(priceValue).SetFontSize(18)).Add(new Text("\n"));
            leftStack.Add(new Text("ط±ظٹط§ظ„").SetFontSize(8)).Add(new Text("\n"));

            // ظ…ط¹ظ„ظˆظ…ط© ط§ظ„ط¨ط§ظ‚ط© (ط¥ط°ط§ ظƒط§ظ†طھ ظپظٹ ط§ط³ظ… ط§ظ„ط¨ط§ظ‚ط©)
            string profileHint = v.Profile ?? "";
            if (!string.IsNullOrEmpty(profileHint))
            {
                leftStack.Add(new Text(profileHint).SetFontSize(6)).Add(new Text("\n"));
            }
            leftCell.Add(leftStack);

            // â”€â”€â”€ Middle White Area â€” ط¨ظٹط§ظ†ط§طھ ط§ظ„ط§ط¹طھظ…ط§ط¯ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Cell midCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(4).SetTextAlignment(TextAlignment.CENTER);
            var midStack = new Paragraph().SetFont(arabicFont).SetFontColor(ColorConstants.BLACK);

            BuildCredentialContent(midStack, v);

            midCell.Add(midStack);

            // â”€â”€â”€ Right Red Area â€” ط§ط³ظ… ط§ظ„ط´ط±ظƒط© â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Cell rightCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(2).SetTextAlignment(TextAlignment.CENTER);
            var rightStack = new Paragraph().SetFont(arabicFont).SetFontColor(ColorConstants.WHITE);

            string companyName = string.IsNullOrEmpty(settings.CompanyName)
                ? "ط´ط¨ظƒط© ط§ظ„ط¹ط§طµظ… ظ„ظ„ط¥ظ†طھط±ظ†طھ"
                : settings.CompanyName;

            var parts = companyName.Split(' ', 3);
            if (parts.Length >= 1) rightStack.Add(new Text(parts[0]).SetFontSize(8)).Add(new Text("\n"));
            if (parts.Length >= 2) rightStack.Add(new Text(parts[1]).SetFontSize(12)).Add(new Text("\n"));
            if (parts.Length >= 3) rightStack.Add(new Text(parts[2]).SetFontSize(8));
            else rightStack.Add(new Text("ظ„ظ„ط¥ظ†طھط±ظ†طھ").SetFontSize(8));

            rightCell.Add(rightStack);

            innerTable.AddCell(leftCell);
            innerTable.AddCell(midCell);
            innerTable.AddCell(rightCell);

            cell.Add(innerTable);
            table.AddCell(cell);
        }

        document.Add(table);
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  ظ…ط­ط±ظƒ ط·ط¨ط§ط¹ط© ط¨ظٹط§ظ†ط§طھ ط§ظ„ط§ط¹طھظ…ط§ط¯ â€” ظٹطھط­ظƒظ… ظپظٹ ط§ظ„ظ…ط­طھظˆظ‰ ط§ظ„ظˆط³ط·ظٹ
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    private static void BuildCredentialContent(Paragraph p, VoucherDto v)
    {
        var mode = v.CredentialMode;

        switch (mode)
        {
            case CredentialMode.UsernameOnly:
                // ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… ظپظ‚ط· â€” ط¨ط¯ظˆظ† ظƒظ„ظ…ط© ط³ط±
                p.Add(new Text(v.Username).SetFontSize(13)).Add(new Text("\n"));
                p.Add(new Text("â–¶ ط§ظ„ط§ط³ظ… ظ‡ظˆ ط§ظ„ط±ظ…ط² â—€").SetFontSize(7)
                    .SetFontColor(new DeviceRgb(80, 80, 80)));
                break;

            case CredentialMode.UsernameEqualsPassword:
                // ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… + طھظˆط¶ظٹط­ ط£ظ† ط§ظ„ط±ظ…ط² = ط§ظ„ط§ط³ظ…
                p.Add(new Text(v.Username).SetFontSize(13)).Add(new Text("\n"));
                p.Add(new Text("â”€â”€â”€â”€â”€â”€â”€â”€â”€").SetFontSize(6)
                    .SetFontColor(new DeviceRgb(150, 150, 150))).Add(new Text("\n"));
                p.Add(new Text("ط§ظ„ط±ظ…ط² = ط§ظ„ط§ط³ظ…").SetFontSize(8)
                    .SetFontColor(new DeviceRgb(60, 60, 180)));
                break;

            case CredentialMode.UsernameAndPassword:
            default:
                // ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… + ظƒظ„ظ…ط© ط§ظ„ط³ط± ط¨ظˆط¶ظˆط­
                p.Add(new Text("â†“ ط§ظ„ط§ط³ظ… â†“").SetFontSize(7)
                    .SetFontColor(new DeviceRgb(100, 100, 100))).Add(new Text("\n"));
                p.Add(new Text(v.Username).SetFontSize(12)).Add(new Text("\n"));
                p.Add(new Text("â”€â”€â”€â”€â”€â”€â”€â”€â”€").SetFontSize(6)
                    .SetFontColor(new DeviceRgb(150, 150, 150))).Add(new Text("\n"));
                p.Add(new Text("â†“ ط§ظ„ط±ظ…ط² â†“").SetFontSize(7)
                    .SetFontColor(new DeviceRgb(100, 100, 100))).Add(new Text("\n"));
                // ظƒظ„ظ…ط© ط§ظ„ط³ط± â€” ظ…ط±ط¦ظٹط© ظˆظˆط§ط¶ط­ط©
                string effectivePass = !string.IsNullOrEmpty(v.Password) ? v.Password : v.Username;
                p.Add(new Text(effectivePass).SetFontSize(11)
                    .SetFontColor(new DeviceRgb(20, 20, 100)));
                break;
        }
    }
}

// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
//  Custom Cell Renderer â€” ط®ظ„ظپظٹط© ط¨طھطµظ…ظٹظ… ظ‡ظˆط§ط¦ظٹ
// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
public class HawaeCellRenderer : CellRenderer
{
    public HawaeCellRenderer(Cell modelElement) : base(modelElement) { }

    public override IRenderer GetNextRenderer()
        => new HawaeCellRenderer((Cell)GetModelElement());

    public override void DrawBackground(DrawContext drawContext)
    {
        base.DrawBackground(drawContext);

        PdfCanvas canvas = drawContext.GetCanvas();
        Rectangle rect = GetOccupiedAreaBBox();

        float x = rect.GetLeft();
        float y = rect.GetBottom();
        float w = rect.GetWidth();
        float h = rect.GetHeight();

        // 1. ط®ظ„ظپظٹط© ط­ظ…ط±ط§ط، ط¯ط§ظƒظ†ط© ظ„ظ„ظƒط±طھ ظƒظ„ظ‡
        Color darkRed = new DeviceRgb(179, 27, 32); // #B31B20
        canvas.SaveState();
        canvas.SetFillColor(darkRed);
        canvas.Rectangle(x, y, w, h);
        canvas.Fill();

        // 2. ط§ظ„ظ…ظ†ط·ظ‚ط© ط§ظ„ط¨ظٹط¶ط§ط، ط§ظ„ظ…ط§ط¦ظ„ط© ظپظٹ ط§ظ„ظ…ظ†طھطµظپ (ط´ظƒظ„ ظ…طھظˆط§ط²ظٹ ط§ظ„ط£ط¶ظ„ط§ط¹)
        float slant = w * 0.08f;
        float wLeft  = w * 0.28f;
        float wRight = w * 0.28f;

        canvas.SetFillColor(ColorConstants.WHITE);
        canvas.MoveTo(x + wLeft,              y);
        canvas.LineTo(x + wLeft  + slant,     y + h);
        canvas.LineTo(x + w - wRight + slant, y + h);
        canvas.LineTo(x + w - wRight,         y);
        canvas.ClosePath();
        canvas.Fill();

        // 3. ط®ط· ط¸ظ„ ط±ظپظٹط¹ ط¹ظ„ظ‰ ط§ظ„ط­ط§ظپط© ط§ظ„ظٹط³ط±ظ‰ ظ„ظ„ظ…ظ†ط·ظ‚ط© ط§ظ„ط¨ظٹط¶ط§ط،
        canvas.SetStrokeColor(new DeviceRgb(100, 0, 0));
        canvas.SetLineWidth(1f);
        canvas.MoveTo(x + wLeft,          y);
        canvas.LineTo(x + wLeft + slant,  y + h);
        canvas.Stroke();

        canvas.RestoreState();
    }
}
