using System;
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

            // 1. Delete previous system templates if they exist in the database
            var oldIds = new[] 
            { 
                BuiltInTemplateIds.A4HawaeIsp, 
                BuiltInTemplateIds.A4SimpleGrid, 
                BuiltInTemplateIds.Thermal58, 
                BuiltInTemplateIds.Thermal80 
            };
            var oldTemplates = await db.TemplateConfigs.Where(x => oldIds.Contains(x.Id)).ToListAsync(cancellationToken);
            if (oldTemplates.Any())
            {
                db.TemplateConfigs.RemoveRange(oldTemplates);
                logger.LogInformation("Cleaned up old built-in templates from database.");
            }

            // 2. Define the new TXT Template
            var txtTemplate = new TemplateConfig
            {
                Id = BuiltInTemplateIds.TxtTemplate,
                Name = "قالب ملف نصي TXT",
                IsDefault = true,
                IsSystemTemplate = true,
                Kind = TemplateType.A4, // treated as standard but produces txt output in print pipeline
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
            };

            // 3. Ensure txtTemplate is seeded
            var existing = await db.TemplateConfigs.FirstOrDefaultAsync(x => x.Id == txtTemplate.Id, cancellationToken);
            if (existing == null)
            {
                db.TemplateConfigs.Add(txtTemplate);
                logger.LogInformation("Seeded TXT Template successfully.");
            }
            else if (existing.IsSystemTemplate)
            {
                // Force sync details
                if (existing.Name != txtTemplate.Name)
                {
                    existing.Name = txtTemplate.Name;
                    db.TemplateConfigs.Update(existing);
                    logger.LogInformation("Updated TXT Template name in database.");
                }
            }

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(cancellationToken);
        }
    }
}
