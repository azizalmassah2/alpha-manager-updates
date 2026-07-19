using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// ظٹط·ط¨ظ‘ظ‚ ط¥ط¹ط¯ط§ط¯ط§طھ ط§ظ„ظ‚ط§ظ„ط¨ ط¹ظ„ظ‰ <see cref="PrintSettingsDto"/> ظ‚ط¨ظ„ ط§ظ„ط±ظ†ط¯ط± (ظˆط±ظ‚طŒ ط´ط¨ظƒط©طŒ ط§ط³ظ… ط§ظ„ظ‚ط§ظ„ط¨ ط§ظ„ط§ط­طھظٹط§ط·ظٹ).
/// </summary>
public static class TemplatePrintSettingsOverlay
{
    public static void ApplyFromEntity(TemplateConfig cfg, PrintSettingsDto s)
    {
        if (cfg.Columns > 0)
            s.CardsPerRow = cfg.Columns;
        if (cfg.Rows > 0)
            s.CardsPerColumn = cfg.Rows;

        switch (cfg.Kind)
        {
            case TemplateType.A4:
                s.PaperType = PaperType.A4;
                break;
            case TemplateType.Thermal58:
                s.PaperType = PaperType.Thermal58;
                if (cfg.ThermalPrintableWidthMm is > 0)
                    s.PrintableWidthMm = cfg.ThermalPrintableWidthMm.Value;
                break;
            case TemplateType.Thermal80:
                s.PaperType = PaperType.Thermal80;
                if (cfg.ThermalPrintableWidthMm is > 0)
                    s.PrintableWidthMm = cfg.ThermalPrintableWidthMm.Value;
                break;
            case TemplateType.Custom:
            default:
                s.PaperType = PaperType.A4;
                break;
        }

        if (!string.IsNullOrWhiteSpace(cfg.LegacyRendererKey))
            s.TemplateName = cfg.LegacyRendererKey!;
        else
            s.TemplateName = "CustomGridDefault";
    }
}
