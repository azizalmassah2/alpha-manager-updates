using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Infrastructure.Data;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public sealed class TemplateService : ITemplateService
{
    private readonly IDbContextFactory<LuxCardDbContext> _factory;

    public TemplateService(IDbContextFactory<LuxCardDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<TemplateConfigDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var list = await db.TemplateConfigs
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.IsSystemTemplate)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return list.Select(Map).ToList();
    }

    public async Task<TemplateConfigDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var e = await db.TemplateConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        return e == null ? null : Map(e);
    }

    public async Task<TemplateConfigDto?> GetDefaultForProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var profile = await db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == profileId, cancellationToken);
        if (profile?.TemplateId is Guid tid)
            return await GetByIdAsync(tid, cancellationToken);

        return await GetByIdAsync(BuiltInTemplateIds.A4HawaeIsp, cancellationToken);
    }

    public async Task<IReadOnlyList<TemplateConfigDto>> GetByKindAsync(TemplateType kind, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var list = await db.TemplateConfigs.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Kind == kind)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return list.Select(Map).ToList();
    }

    public async Task<Guid> GetPrimarySystemTemplateIdAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var exists = await db.TemplateConfigs.AsNoTracking()
            .AnyAsync(x => x.Id == BuiltInTemplateIds.A4HawaeIsp && !x.IsDeleted, cancellationToken);
        if (exists)
            return BuiltInTemplateIds.A4HawaeIsp;

        var first = await db.TemplateConfigs.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.IsSystemTemplate)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return first != Guid.Empty ? first : BuiltInTemplateIds.A4HawaeIsp;
    }

    private static TemplateConfigDto Map(TemplateConfig x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Kind = x.Kind,
        IsSystemTemplate = x.IsSystemTemplate,
        IsDefault = x.IsDefault,
        LegacyRendererKey = x.LegacyRendererKey,
        ThermalPrintableWidthMm = x.ThermalPrintableWidthMm,
        Columns = x.Columns,
        Rows = x.Rows,
        BackgroundImagePath = x.BackgroundImagePath
    };
}
