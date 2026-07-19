using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// تنفيذ خدمة إدارة قوالب Lux Template Engine.
/// تعمل موازياً لـ TemplateService القديم دون تعارض.
/// </summary>
public class LuxTemplateService : ILuxTemplateService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly ILogger<LuxTemplateService> _logger;

    public LuxTemplateService(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        ILogger<LuxTemplateService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // ══ عمليات القراءة ══

    public async Task<IReadOnlyList<LuxTemplateDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var templates = await db.LuxTemplates
            .AsNoTracking()
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return templates.Select(MapToListDto).ToList();
    }

    public async Task<IReadOnlyList<LuxTemplateDto>> GetByCategoryAsync(
        TemplateCategory category, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var templates = await db.LuxTemplates
            .AsNoTracking()
            .Where(t => t.Category == category)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return templates.Select(MapToListDto).ToList();
    }

    public async Task<LuxTemplateDetailDto?> GetDetailByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var template = await db.LuxTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return template is null ? null : MapToDetailDto(template);
    }

    public async Task<LuxTemplateDto?> GetDefaultForCategoryAsync(
        TemplateCategory category, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var template = await db.LuxTemplates
            .AsNoTracking()
            .Where(t => t.Category == category && t.IsDefault)
            .FirstOrDefaultAsync(ct);

        return template is null ? null : MapToListDto(template);
    }

    // ══ عمليات الكتابة ══

    public async Task<LuxTemplateDto> CreateAsync(LuxTemplateDetailDto dto, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = MapToEntity(dto);
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.NewGuid();

        db.LuxTemplates.Add(entity);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("LuxTemplate created: {Name} (Id={Id})", entity.Name, entity.Id);
        return MapToListDto(entity);
    }

    public async Task UpdateAsync(LuxTemplateDetailDto dto, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.LuxTemplates.FindAsync(new object[] { dto.Id }, ct)
            ?? throw new InvalidOperationException($"LuxTemplate not found: {dto.Id}");

        // نحدث فقط الخصائص القابلة للتعديل
        entity.Name                = dto.Name;
        entity.Description         = dto.Description;
        entity.Category            = dto.Category;
        entity.OutputType          = dto.OutputType;
        entity.Orientation         = dto.Orientation;
        entity.PageWidthMm         = dto.PageWidthMm;
        entity.PageHeightMm        = dto.PageHeightMm;
        entity.CardsPerRow         = dto.CardsPerRow;
        entity.CardsPerColumn      = dto.CardsPerColumn;
        entity.CardWidthMm         = dto.CardWidthMm;
        entity.CardHeightMm        = dto.CardHeightMm;
        entity.HorizontalGapMm     = dto.HorizontalGapMm;
        entity.VerticalGapMm       = dto.VerticalGapMm;
        entity.MarginTopMm         = dto.MarginTopMm;
        entity.MarginBottomMm      = dto.MarginBottomMm;
        entity.MarginLeftMm        = dto.MarginLeftMm;
        entity.MarginRightMm       = dto.MarginRightMm;
        entity.BackgroundType      = dto.BackgroundType;
        entity.BackgroundColorHex  = dto.BackgroundColorHex;
        entity.BackgroundImagePath = dto.BackgroundImagePath;
        entity.ElementsJson        = dto.ElementsJson;
        entity.LinkedProfileName   = dto.LinkedProfileName;
        entity.Version++;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("LuxTemplate updated: {Name} (Id={Id}, v{Version})", entity.Name, entity.Id, entity.Version);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.LuxTemplates.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"LuxTemplate not found: {id}");

        if (entity.IsSystemTemplate)
            throw new InvalidOperationException("القوالب النظامية لا يمكن حذفها.");

        // Soft delete — يتم عبر آلية ChangeTracker في DbContext
        db.LuxTemplates.Remove(entity);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("LuxTemplate deleted (soft): {Name} (Id={Id})", entity.Name, id);
    }

    public async Task<LuxTemplateDto> DuplicateAsync(Guid id, string newName, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var source = await db.LuxTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException($"LuxTemplate not found: {id}");

        var copy = new LuxTemplate
        {
            Id                 = Guid.NewGuid(),
            Name               = newName,
            Description        = source.Description,
            Category           = source.Category,
            OutputType         = source.OutputType,
            Orientation        = source.Orientation,
            PageWidthMm        = source.PageWidthMm,
            PageHeightMm       = source.PageHeightMm,
            CardsPerRow        = source.CardsPerRow,
            CardsPerColumn     = source.CardsPerColumn,
            CardWidthMm        = source.CardWidthMm,
            CardHeightMm       = source.CardHeightMm,
            HorizontalGapMm    = source.HorizontalGapMm,
            VerticalGapMm      = source.VerticalGapMm,
            MarginTopMm        = source.MarginTopMm,
            MarginBottomMm     = source.MarginBottomMm,
            MarginLeftMm       = source.MarginLeftMm,
            MarginRightMm      = source.MarginRightMm,
            BackgroundType     = source.BackgroundType,
            BackgroundColorHex = source.BackgroundColorHex,
            BackgroundImagePath= source.BackgroundImagePath,
            ElementsJson       = source.ElementsJson,
            LinkedProfileName  = source.LinkedProfileName,
            IsSystemTemplate   = false, // النسخة ليست نظامية أبداً
            IsDefault          = false, // النسخة ليست افتراضية
            Version            = 1,
        };

        db.LuxTemplates.Add(copy);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("LuxTemplate duplicated: {Source} → {Copy}", source.Name, newName);
        return MapToListDto(copy);
    }

    public async Task SetDefaultAsync(Guid id, TemplateCategory category, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // ألغِ الافتراضي الحالي
            var currentDefault = await db.LuxTemplates
                .Where(t => t.Category == category && t.IsDefault)
                .ToListAsync(ct);

            foreach (var t in currentDefault)
                t.IsDefault = false;

            // اضبط الجديد
            var target = await db.LuxTemplates.FindAsync(new object[] { id }, ct)
                ?? throw new InvalidOperationException($"LuxTemplate not found: {id}");

            target.IsDefault = true;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("LuxTemplate set as default: {Name} (Category={Category})", target.Name, category);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ══ Mapping Helpers ══

    private static LuxTemplateDto MapToListDto(LuxTemplate e)
    {
        // احسب عدد العناصر بسرعة من JSON بدون deserialization كامل
        int elemCount = 0;
        try
        {
            if (!string.IsNullOrWhiteSpace(e.ElementsJson) && e.ElementsJson != "[]")
            {
                using var doc = JsonDocument.Parse(e.ElementsJson);
                elemCount = doc.RootElement.GetArrayLength();
            }
        }
        catch { /* تجاهل خطأ التحليل */ }

        return new LuxTemplateDto
        {
            Id                = e.Id,
            Name              = e.Name,
            Description       = e.Description,
            Category          = e.Category,
            OutputType        = e.OutputType,
            Orientation       = e.Orientation,
            CardsPerRow       = e.CardsPerRow,
            CardsPerColumn    = e.CardsPerColumn,
            CardWidthMm       = e.CardWidthMm,
            CardHeightMm      = e.CardHeightMm,
            IsDefault         = e.IsDefault,
            IsSystemTemplate  = e.IsSystemTemplate,
            Version           = e.Version,
            LinkedProfileName = e.LinkedProfileName,
            ElementsCount     = elemCount,
        };
    }

    private static LuxTemplateDetailDto MapToDetailDto(LuxTemplate e) => new()
    {
        Id                 = e.Id,
        Name               = e.Name,
        Description        = e.Description,
        Category           = e.Category,
        OutputType         = e.OutputType,
        Orientation        = e.Orientation,
        PageWidthMm        = e.PageWidthMm,
        PageHeightMm       = e.PageHeightMm,
        CardsPerRow        = e.CardsPerRow,
        CardsPerColumn     = e.CardsPerColumn,
        CardWidthMm        = e.CardWidthMm,
        CardHeightMm       = e.CardHeightMm,
        HorizontalGapMm    = e.HorizontalGapMm,
        VerticalGapMm      = e.VerticalGapMm,
        MarginTopMm        = e.MarginTopMm,
        MarginBottomMm     = e.MarginBottomMm,
        MarginLeftMm       = e.MarginLeftMm,
        MarginRightMm      = e.MarginRightMm,
        BackgroundType     = e.BackgroundType,
        BackgroundColorHex = e.BackgroundColorHex,
        BackgroundImagePath= e.BackgroundImagePath,
        ElementsJson       = e.ElementsJson,
        LinkedProfileName  = e.LinkedProfileName,
        Version            = e.Version,
        IsSystemTemplate   = e.IsSystemTemplate,
        IsDefault          = e.IsDefault,
    };

    private static LuxTemplate MapToEntity(LuxTemplateDetailDto dto) => new()
    {
        Id                 = dto.Id,
        Name               = dto.Name,
        Description        = dto.Description,
        Category           = dto.Category,
        OutputType         = dto.OutputType,
        Orientation        = dto.Orientation,
        PageWidthMm        = dto.PageWidthMm,
        PageHeightMm       = dto.PageHeightMm,
        CardsPerRow        = dto.CardsPerRow,
        CardsPerColumn     = dto.CardsPerColumn,
        CardWidthMm        = dto.CardWidthMm,
        CardHeightMm       = dto.CardHeightMm,
        HorizontalGapMm    = dto.HorizontalGapMm,
        VerticalGapMm      = dto.VerticalGapMm,
        MarginTopMm        = dto.MarginTopMm,
        MarginBottomMm     = dto.MarginBottomMm,
        MarginLeftMm       = dto.MarginLeftMm,
        MarginRightMm      = dto.MarginRightMm,
        BackgroundType     = dto.BackgroundType,
        BackgroundColorHex = dto.BackgroundColorHex,
        BackgroundImagePath= dto.BackgroundImagePath,
        ElementsJson       = dto.ElementsJson,
        LinkedProfileName  = dto.LinkedProfileName,
        Version            = dto.Version,
        IsSystemTemplate   = dto.IsSystemTemplate,
        IsDefault          = dto.IsDefault,
    };
}
