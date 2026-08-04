using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.Application.Services;

public class VoucherGenerationService : IVoucherGenerationService
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly IGenericRepository<Batch> _batchRepo;
    private readonly ISyncService _syncService;
    private readonly IPrintService _printService;
    private readonly IPrintPreviewService _printPreviewService;
    private readonly IVoucherQueryService _queryService;

    // Characters for generation
    private const string DIGITS = "0123456789";
    private const string DIGITS_SAFE = "23456789";
    private const string LETTERS_UPPER = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LETTERS_LOWER = "abcdefghjkmnpqrstuvwxyz";
    private const string MIXED = LETTERS_UPPER + DIGITS_SAFE;
    private const string LOWERCASE_MIXED = LETTERS_LOWER + DIGITS_SAFE;

    public VoucherGenerationService(
        IVoucherRepository voucherRepository,
        IGenericRepository<Batch> batchRepo,
        ISyncService syncService,
        IPrintService printService,
        IPrintPreviewService printPreviewService,
        IVoucherQueryService queryService)
    {
        _voucherRepository = voucherRepository;
        _batchRepo = batchRepo;
        _syncService = syncService;
        _printService = printService;
        _printPreviewService = printPreviewService;
        _queryService = queryService;
    }

    public async Task<VoucherGenerationResult> GenerateAsync(
        VoucherGenerationRequest request,
        IProgress<(int success, int failed, int total, string phase)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new VoucherGenerationResult();
        int count = request.Mode == GenerationMode.Bulk ? request.Count : 1;

        // Phase 1: Generation
        progress?.Report((0, 0, count, "Generating items"));
        var batchId = Guid.NewGuid();
        var newBatch = new Batch
        {
            Id          = batchId,
            Name        = request.Mode == GenerationMode.Bulk
                ? $"Batch {DateTime.Now:yyyy-MM-dd HH:mm}"
                : $"Single {DateTime.Now:yyyy-MM-dd HH:mm}",
            ProfileName = request.ProfileName,
            TotalCards  = count,
            Status      = Domain.Enums.BatchStatus.Generating,
            SyncStatus  = Domain.Enums.BatchSyncStatus.Pending,
            StartedAt   = DateTime.UtcNow,
        };
        await _batchRepo.AddAsync(newBatch, cancellationToken);
        result.BatchId = batchId;

        var list = new List<Voucher>();
        var rnd = new Random();
        string userPool = GetCharacterPool(request.CharacterMode);
        string passPool = GetCharacterPool(request.PasswordCharacterMode);

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string user = request.Mode == GenerationMode.Single && !string.IsNullOrEmpty(request.SingleUsername)
                ? request.SingleUsername
                : request.Prefix + GenerateRandomString(rnd, request.UsernameLength, userPool);

            string pass = request.CredentialMode switch
            {
                CredentialMode.UsernameOnly => "",
                CredentialMode.UsernameEqualsPassword => user,
                CredentialMode.UsernameAndPassword =>
                    request.Mode == GenerationMode.Single && !string.IsNullOrEmpty(request.SinglePassword)
                        ? request.SinglePassword
                        : request.PasswordPrefix + GenerateRandomString(rnd, request.PasswordLength, passPool),
                _ => ""
            };

            list.Add(new Voucher
            {
                Username = user,
                Password = pass,
                ProfileName = request.ProfileName,
                BatchId = batchId,
                Price = request.Price,
                CredentialMode = request.CredentialMode,
                AgentId = request.AgentId
            });

            progress?.Report((i + 1, 0, count, "Generating items"));
        }
        result.GeneratedCount = list.Count;

        // Phase 2: Save to DB
        progress?.Report((0, 0, 1, "Saving to database"));
        var dbResult = await _voucherRepository.BulkInsertSafelyAsync(list, cancellationToken);
        result.DbSuccessCount = dbResult.SuccessCount;
        result.DbFailedCount = dbResult.FailedCount;
        progress?.Report((1, 0, 1, "Saving to database"));

        // Phase 3: Sync to MikroTik
        if (request.AutoSync && result.DbSuccessCount > 0)
        {
            progress?.Report((0, 0, result.DbSuccessCount, "Syncing with MikroTik"));
            
            var syncProgress = new Progress<(int s, int f, int t)>(update =>
            {
                progress?.Report((update.s, update.f, update.t, "Syncing with MikroTik"));
            });
            
            var syncResult = await _syncService.ProcessBatchAsync(batchId, syncProgress, cancellationToken);
            result.SyncSuccessCount = syncResult.Success;
            result.SyncFailedCount = syncResult.Failed;
        }

        // Phase 4: Print Preview
        if (request.AutoPrint && result.DbSuccessCount > 0)
        {
            progress?.Report((0, 0, 1, "Preparing print file"));
            
            var vouchers = await _queryService.GetVouchersByBatchIdAsync(batchId, cancellationToken);
            if (vouchers.Count > 0)
            {
                var settings = new PrintSettingsDto();
                if (request.PrintTemplateId.HasValue)
                {
                    settings.CustomTemplateId = request.PrintTemplateId.Value;
                }

                var pdfResult = await _printService.GeneratePdfAsync(new List<VoucherDto>(vouchers), settings, cancellationToken: cancellationToken);
                
                if (pdfResult.IsSuccess)
                {
                    string tempFileName = $"luxcard_batch_{DateTime.Now:HHmmss}.pdf";
                    await _printPreviewService.PreviewPdfAsync(pdfResult.Value, tempFileName, cancellationToken);
                    result.AutoPrintInvoked = true;
                }
            }
            progress?.Report((1, 0, 1, "Preparing print file"));
        }

        return result;
    }

    private string GetCharacterPool(CharacterMode mode) => mode switch
    {
        CharacterMode.DigitsOnly => DIGITS,
        CharacterMode.LettersOnly => LETTERS_UPPER,
        CharacterMode.Mixed => MIXED,
        CharacterMode.LowercaseMixed => LOWERCASE_MIXED,
        _ => MIXED
    };

    private string GenerateRandomString(Random rnd, int length, string pool)
    {
        return new string(Enumerable.Repeat(pool, length)
            .Select(s => s[rnd.Next(s.Length)]).ToArray());
    }
}
