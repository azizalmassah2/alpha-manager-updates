using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Infrastructure.Data
{
    public static class BuiltInTemplateSeeder
    {
        public static async Task EnsureSeedAsync(LuxCardDbContext db, ILogger logger, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var rv = Guid.NewGuid().ToByteArray();

            var templates = new List<TemplateConfig>
            {
                new TemplateConfig
                {
                    Id = BuiltInTemplateIds.A4HawaeIsp,
                    Name = "افتراضي النظام (A4 - 21 صف × 4 أعمدة)",
                    IsDefault = true,
                    IsSystemTemplate = true,
                    Kind = TemplateType.A4,
                    Columns = 4,
                    Rows = 21,
                    MarginX = 1.0f,
                    MarginY = 1.0f,
                    UsernameX = 20.0f,
                    UsernameY = 4.3f,
                    PasswordX = 5.0f,
                    PasswordY = 12.0f,
                    PriceX = 5.0f,
                    PriceY = 20.0f,
                    QrX = 40.0f,
                    QrY = 5.0f,
                    ValidityX = 5.0f,
                    ValidityY = 28.0f,
                    TimeX = 5.0f,
                    TimeY = 36.0f,
                    SerialNumberX = 5.0f,
                    SerialNumberY = 44.0f,
                    PrintDateX = 40.0f,
                    PrintDateY = 44.0f,
                    BarcodeX = 30.0f,
                    BarcodeY = 20.0f,
                    FontSize = 5.0f,
                    FontFamily = "Arial",
                    FontColorHex = "#000000",
                    FrameColorHex = "#000000",
                    FrameSize = 0,
                    CreatedAt = now,
                    RowVersion = rv
                },
                new TemplateConfig
                {
                    Id = BuiltInTemplateIds.A4SimpleGrid,
                    Name = "شبكة بسيطة (A4 - 10 صفوف × 3 أعمدة)",
                    IsDefault = false,
                    IsSystemTemplate = true,
                    Kind = TemplateType.A4,
                    Columns = 3,
                    Rows = 10,
                    MarginX = 2.0f,
                    MarginY = 2.0f,
                    UsernameX = 25.0f,
                    UsernameY = 6.0f,
                    PasswordX = 10.0f,
                    PasswordY = 15.0f,
                    PriceX = 10.0f,
                    PriceY = 25.0f,
                    QrX = 45.0f,
                    QrY = 6.0f,
                    ValidityX = 10.0f,
                    ValidityY = 32.0f,
                    TimeX = 10.0f,
                    TimeY = 40.0f,
                    SerialNumberX = 10.0f,
                    SerialNumberY = 48.0f,
                    PrintDateX = 45.0f,
                    PrintDateY = 48.0f,
                    BarcodeX = 35.0f,
                    BarcodeY = 25.0f,
                    FontSize = 6.0f,
                    FontFamily = "Arial",
                    FontColorHex = "#000000",
                    FrameColorHex = "#000000",
                    FrameSize = 0,
                    CreatedAt = now,
                    RowVersion = rv
                },
                new TemplateConfig
                {
                    Id = BuiltInTemplateIds.Thermal58,
                    Name = "طباعة حرارية 58 مم",
                    IsDefault = false,
                    IsSystemTemplate = true,
                    Kind = TemplateType.Thermal58,
                    ThermalPrintableWidthMm = 48,
                    Columns = 1,
                    Rows = 1,
                    MarginX = 0,
                    MarginY = 0,
                    UsernameX = 15.0f,
                    UsernameY = 5.0f,
                    PasswordX = 5.0f,
                    PasswordY = 12.0f,
                    PriceX = 5.0f,
                    PriceY = 20.0f,
                    QrX = 30.0f,
                    QrY = 5.0f,
                    FontSize = 5.0f,
                    FontFamily = "Arial",
                    FontColorHex = "#000000",
                    FrameColorHex = "#000000",
                    FrameSize = 0,
                    CreatedAt = now,
                    RowVersion = rv
                },
                new TemplateConfig
                {
                    Id = BuiltInTemplateIds.Thermal80,
                    Name = "طباعة حرارية 80 مم",
                    IsDefault = false,
                    IsSystemTemplate = true,
                    Kind = TemplateType.Thermal80,
                    ThermalPrintableWidthMm = 72,
                    Columns = 1,
                    Rows = 1,
                    MarginX = 0,
                    MarginY = 0,
                    UsernameX = 20.0f,
                    UsernameY = 5.0f,
                    PasswordX = 5.0f,
                    PasswordY = 12.0f,
                    PriceX = 5.0f,
                    PriceY = 20.0f,
                    QrX = 45.0f,
                    QrY = 5.0f,
                    FontSize = 5.0f,
                    FontFamily = "Arial",
                    FontColorHex = "#000000",
                    FrameColorHex = "#000000",
                    FrameSize = 0,
                    CreatedAt = now,
                    RowVersion = rv
                },
                new TemplateConfig
                {
                    Id = BuiltInTemplateIds.TxtTemplate,
                    Name = "قالب ملف نصي TXT",
                    IsDefault = false,
                    IsSystemTemplate = true,
                    Kind = TemplateType.A4,
                    Columns = 4,
                    Rows = 21,
                    MarginX = 1.0f,
                    MarginY = 1.0f,
                    UsernameX = 20.0f,
                    UsernameY = 4.3f,
                    PasswordX = 5.0f,
                    PasswordY = 12.0f,
                    PriceX = 5.0f,
                    PriceY = 20.0f,
                    QrX = 40.0f,
                    QrY = 5.0f,
                    ValidityX = 5.0f,
                    ValidityY = 28.0f,
                    TimeX = 5.0f,
                    TimeY = 36.0f,
                    SerialNumberX = 5.0f,
                    SerialNumberY = 44.0f,
                    PrintDateX = 40.0f,
                    PrintDateY = 44.0f,
                    BarcodeX = 30.0f,
                    BarcodeY = 20.0f,
                    FontSize = 5.0f,
                    FontFamily = "Arial",
                    FontColorHex = "#000000",
                    FrameColorHex = "#000000",
                    FrameSize = 0,
                    CreatedAt = now,
                    RowVersion = rv
                }
            };

            foreach (var tpl in templates)
            {
                var existing = await db.TemplateConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tpl.Id, cancellationToken);
                if (existing == null)
                {
                    db.TemplateConfigs.Add(tpl);
                    logger.LogInformation("Seeded system template: {Name}", tpl.Name);
                }
                else
                {
                    existing.Name = tpl.Name;
                    existing.IsSystemTemplate = true;
                    existing.Kind = tpl.Kind;
                    db.TemplateConfigs.Update(existing);
                }
            }

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(cancellationToken);
        }
    }
}
