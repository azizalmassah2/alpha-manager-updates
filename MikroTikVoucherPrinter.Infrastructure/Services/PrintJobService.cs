using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Infrastructure.Data;
using System.Diagnostics;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class PrintJobService : IPrintJobService
{
    private readonly IDbContextFactory<LuxCardDbContext> _dbFactory;
    private readonly IMikroTikIntegrationService _mikroTikIntegrationService;
    private readonly IPrintService _printService;
    private readonly IPrintPreviewService _printPreviewService;
    private readonly IVoucherRepository _voucherRepository;
    private readonly ILogger<PrintJobService> _logger;

    private const string DIGITS = "0123456789";
    private const string DIGITS_SAFE = "23456789";
    private const string LETTERS_UPPER = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LETTERS_LOWER = "abcdefghjkmnpqrstuvwxyz";
    private const string MIXED = LETTERS_UPPER + DIGITS_SAFE;
    private const string LOWERCASE_MIXED = LETTERS_LOWER + DIGITS_SAFE;

    public PrintJobService(
        IDbContextFactory<LuxCardDbContext> dbFactory,
        IMikroTikIntegrationService mikroTikIntegrationService,
        IPrintService printService,
        IPrintPreviewService printPreviewService,
        IVoucherRepository voucherRepository,
        ILogger<PrintJobService> logger)
    {
        _dbFactory = dbFactory;
        _mikroTikIntegrationService = mikroTikIntegrationService;
        _printService = printService;
        _printPreviewService = printPreviewService;
        _voucherRepository = voucherRepository;
        _logger = logger;
    }

    public async Task<PrintJob> CreateJobAsync(VoucherGenerationRequest request, Guid templateId, int count, CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        // 1. Create and Save Batch
        var batch = new Batch
        {
            Id = batchId,
            Name = request.Mode == GenerationMode.Bulk ? $"Batch {DateTime.Now:yyyy-MM-dd HH:mm}" : $"Single {DateTime.Now:yyyy-MM-dd HH:mm}",
            ProfileName = request.ProfileName,
            TotalCount = count
        };

        // 2. Generate Vouchers with Reserved status (Local allocation)
        var list = new List<Voucher>();
        var rnd = new Random();
        string userPool = GetCharacterPool(request.CharacterMode);
        string passPool = GetCharacterPool(request.PasswordCharacterMode);

        for (int i = 0; i < count; i++)
        {
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
                AgentId = request.AgentId,
                PrintStatus = VoucherPrintStatus.Reserved
            });
        }

        // Save Batch to DB
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await db.Batches.AddAsync(batch, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var bulkResult = await _voucherRepository.BulkInsertSafelyAsync(list, cancellationToken);
        if (bulkResult.SuccessCount == 0)
        {
            throw new InvalidOperationException("فشل توليد وحفظ الكروت محلياً (تضارب في الأسماء).");
        }

        // 3. Create PrintJob
        var parametersJson = JsonSerializer.Serialize(request);
        var printJob = new PrintJob
        {
            Id = jobId,
            TemplateId = templateId,
            PrintedAt = DateTime.UtcNow,
            CardCount = count,
            ReservedCount = bulkResult.SuccessCount,
            SyncedCount = 0,
            PdfCount = 0,
            PrintedCount = 0,
            BatchId = batchId,
            Status = PrintJobStatus.Pending,
            CurrentStep = PrintJobStep.GeneratingVouchers,
            JobParametersJson = parametersJson,
            JobVersion = 1,
            TemplateVersion = 1
        };

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await db.PrintJobs.AddAsync(printJob, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await LogEventAsync(jobId, "Info", "تم إنشاء مهمة الطباعة وحجز الكروت محلياً.", $"إجمالي الكروت المحجوزة: {bulkResult.SuccessCount}", cancellationToken);

        return printJob;
    }

    public async Task ExecuteJobAsync(Guid jobId, IProgress<(int success, int failed, int total, string phase, PrintJobStep step)>? progress = null, CancellationToken cancellationToken = default)
    {
        await RunJobPipelineAsync(jobId, progress, cancellationToken);
    }

    public async Task ResumeJobAsync(Guid jobId, IProgress<(int success, int failed, int total, string phase, PrintJobStep step)>? progress = null, CancellationToken cancellationToken = default)
    {
        await LogEventAsync(jobId, "Info", "تم طلب استئناف مهمة الطباعة.", null, cancellationToken);
        await RunJobPipelineAsync(jobId, progress, cancellationToken);
    }

    public async Task CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
        if (job != null)
        {
            job.Status = PrintJobStatus.Cancelled;
            await db.SaveChangesAsync(cancellationToken);
            await LogEventAsync(jobId, "Warning", "تم إلغاء المهمة من قبل المستخدم.", null, cancellationToken);
        }
    }

    public async Task RebuildPdfOnlyAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await LogEventAsync(jobId, "Info", "تم طلب إعادة بناء ملف الـ PDF فقط.", null, cancellationToken);
        
        PrintJob? job;
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            job = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
        }

        if (job == null) throw new ArgumentException("المهمة غير موجودة.", nameof(jobId));

        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var dbJob = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
            if (dbJob != null)
            {
                dbJob.CurrentStep = PrintJobStep.BuildingPdf;
                dbJob.Status = PrintJobStatus.Running;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await RunJobPipelineAsync(jobId, null, cancellationToken);
    }

    public async Task LogEventAsync(Guid jobId, string level, string message, string? details = null, CancellationToken cancellationToken = default)
    {
        var evt = new PrintJobEvent
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Details = details
        };

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.PrintJobEvents.AddAsync(evt, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<PrintJob>> GetActiveJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PrintJobs
            .AsNoTracking()
            .Where(j => j.Status == PrintJobStatus.Running || j.Status == PrintJobStatus.Pending || j.Status == PrintJobStatus.Failed)
            .OrderByDescending(j => j.PrintedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PrintJob>> GetJobHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PrintJobs
            .AsNoTracking()
            .OrderByDescending(j => j.PrintedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PrintJobEvent>> GetJobEventsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PrintJobEvents
            .AsNoTracking()
            .Where(e => e.JobId == jobId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    private async Task RunJobPipelineAsync(Guid jobId, IProgress<(int success, int failed, int total, string phase, PrintJobStep step)>? progress, CancellationToken cancellationToken)
    {
        PrintJob? job = null;
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            job = await db.PrintJobs.Include(j => j.Batch).FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        }

        if (job == null)
            throw new ArgumentException("لم يتم العثور على مهمة الطباعة المحددة.", nameof(jobId));

        if (job.Status == PrintJobStatus.Completed || job.Status == PrintJobStatus.Cancelled)
            return;

        // Transition job status to Running
        await UpdateJobStatusAsync(jobId, PrintJobStatus.Running, null, cancellationToken);

        try
        {
            // Stage 1 to 2 transition
            if (job.CurrentStep == PrintJobStep.GeneratingVouchers)
            {
                await UpdateJobStepAsync(jobId, PrintJobStep.SyncingRouter, cancellationToken);
                job.CurrentStep = PrintJobStep.SyncingRouter;
            }

            // Stage 2: Sync to Router
            if (job.CurrentStep == PrintJobStep.SyncingRouter)
            {
                await LogEventAsync(jobId, "Info", "بدء مزامنة الحسابات مع راوتر مايكروتك.", null, cancellationToken);
                
                List<Voucher> reservedVouchers;
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    reservedVouchers = await db.Vouchers
                        .Where(v => v.BatchId == job.BatchId && v.PrintStatus == VoucherPrintStatus.Reserved)
                        .ToListAsync(cancellationToken);
                }

                int totalCount = job.CardCount;
                int batchSize = 100;
                int processed = 0;

                for (int i = 0; i < reservedVouchers.Count; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chunk = reservedVouchers.Skip(i).Take(batchSize).ToList();
                    var usersToSync = chunk.Select(v => (v.Username, v.EffectivePassword, v.ProfileName)).ToList();

                    progress?.Report((processed, 0, totalCount, $"مزامنة الدفعة {i / batchSize + 1}...", PrintJobStep.SyncingRouter));

                    var results = await _mikroTikIntegrationService.CreateUsersBulkAsync(
                        usersToSync, 
                        null, 
                        0, 
                        0, 
                        cancellationToken);

                    await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                    {
                        foreach (var v in chunk)
                        {
                            var dbVoucher = await db.Vouchers.FindAsync(new object[] { v.Id }, cancellationToken);
                            if (dbVoucher != null)
                            {
                                if (results.TryGetValue(v.Username, out var res))
                                {
                                    if (res.IsSuccess)
                                    {
                                        dbVoucher.MarkAsSynced(res.Value.Id);
                                        dbVoucher.PrintStatus = VoucherPrintStatus.Synced;
                                    }
                                    else
                                    {
                                        dbVoucher.MarkAsFailed($"[{res.ErrorType}] {res.ErrorMessage}");
                                        dbVoucher.PrintStatus = VoucherPrintStatus.Failed;
                                    }
                                }
                                else
                                {
                                    dbVoucher.MarkAsFailed("[Unexpected] لم يتم العثور على نتيجة المزامنة.");
                                    dbVoucher.PrintStatus = VoucherPrintStatus.Failed;
                                }
                            }
                        }
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    int syncedCount = 0;
                    int failedCount = 0;
                    await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                    {
                        syncedCount = await db.Vouchers.CountAsync(v => v.BatchId == job.BatchId && v.PrintStatus == VoucherPrintStatus.Synced, cancellationToken);
                        failedCount = await db.Vouchers.CountAsync(v => v.BatchId == job.BatchId && v.PrintStatus == VoucherPrintStatus.Failed, cancellationToken);

                        var dbJob = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
                        if (dbJob != null)
                        {
                            dbJob.SyncedCount = syncedCount;
                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }

                    processed = syncedCount + failedCount;
                    await LogEventAsync(jobId, "Info", $"تمت مزامنة دفعة {chunk.Count} كارت.", $"الناجح: {syncedCount}، الفاشل: {failedCount}", cancellationToken);
                    progress?.Report((syncedCount, failedCount, totalCount, "جاري المزامنة مع مايكروتك...", PrintJobStep.SyncingRouter));
                }

                int finalSyncedCount = 0;
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    finalSyncedCount = await db.Vouchers.CountAsync(v => v.BatchId == job.BatchId && v.PrintStatus == VoucherPrintStatus.Synced, cancellationToken);
                }

                if (finalSyncedCount == 0)
                {
                    throw new InvalidOperationException("لم يتم مزامنة أي كارت بنجاح على المايكروتك، لا يمكن إنشاء ملف الـ PDF.");
                }

                await UpdateJobStepAsync(jobId, PrintJobStep.BuildingPdf, cancellationToken);
                job.CurrentStep = PrintJobStep.BuildingPdf;
            }

            // Stage 3: Build PDF
            if (job.CurrentStep == PrintJobStep.BuildingPdf)
            {
                progress?.Report((0, 0, 1, "جاري بناء ملف الـ PDF...", PrintJobStep.BuildingPdf));
                await LogEventAsync(jobId, "Info", "بدء إنشاء ملف الـ PDF من سجل قاعدة البيانات المحلية.", null, cancellationToken);

                List<Voucher> syncedVouchers;
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    syncedVouchers = await db.Vouchers
                        .Where(v => v.BatchId == job.BatchId && (v.PrintStatus == VoucherPrintStatus.Synced || v.PrintStatus == VoucherPrintStatus.PdfGenerated))
                        .ToListAsync(cancellationToken);
                }

                var originalRequest = JsonSerializer.Deserialize<VoucherGenerationRequest>(job.JobParametersJson ?? "{}");
                
                var dtoList = syncedVouchers.Select(v => new VoucherDto
                {
                    Id = v.Id,
                    Username = v.Username,
                    Password = v.Password,
                    Profile = v.ProfileName,
                    Status = v.Status,
                    SyncStatus = v.SyncStatus,
                    CreatedAt = v.CreatedAt,
                    BatchId = v.BatchId,
                    Price = v.Price
                }).ToList();

                var printSettings = new PrintSettingsDto();
                if (job.TemplateId != Guid.Empty)
                {
                    printSettings.CustomTemplateId = job.TemplateId;
                }

                var pdfResult = await _printService.GeneratePdfAsync(dtoList, printSettings, cancellationToken);
                if (!pdfResult.IsSuccess)
                {
                    throw new InvalidOperationException($"فشل بناء الـ PDF: {pdfResult.ErrorMessage}");
                }

                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string outputDir = Path.Combine(docPath, "LuxCard", "Prints");
                Directory.CreateDirectory(outputDir);
                string finalPath = Path.Combine(outputDir, $"job_{jobId}.pdf");
                string tempPath = Path.Combine(outputDir, $"job_{jobId}.tmp.pdf");

                await File.WriteAllBytesAsync(tempPath, pdfResult.Value, cancellationToken);

                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
                File.Move(tempPath, finalPath);

                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    var ids = dtoList.Select(d => d.Id).ToList();
                    var dbVouchers = await db.Vouchers.Where(v => ids.Contains(v.Id)).ToListAsync(cancellationToken);
                    foreach (var v in dbVouchers)
                    {
                        v.PrintStatus = VoucherPrintStatus.PdfGenerated;
                    }
                    
                    var dbJob = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
                    if (dbJob != null)
                    {
                        dbJob.OutputFilePath = finalPath;
                        dbJob.PdfCount = dtoList.Count;
                    }
                    await db.SaveChangesAsync(cancellationToken);
                }

                await LogEventAsync(jobId, "Info", "تم بناء ملف الـ PDF بنجاح وحفظه محلياً.", $"المسار: {finalPath}", cancellationToken);
                progress?.Report((1, 0, 1, "تم بناء ملف الـ PDF.", PrintJobStep.BuildingPdf));

                await UpdateJobStepAsync(jobId, PrintJobStep.Printing, cancellationToken);
                job.CurrentStep = PrintJobStep.Printing;
            }

            // Stage 4: Printing (Spooling)
            if (job.CurrentStep == PrintJobStep.Printing)
            {
                progress?.Report((0, 0, 1, "جاري إرسال الكروت إلى مسبع الطباعة...", PrintJobStep.Printing));
                await LogEventAsync(jobId, "Info", "بدء فتح المعاينة وإرسال الكروت للطباعة.", null, cancellationToken);

                string? finalPath = null;
                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    var dbJob = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
                    finalPath = dbJob?.OutputFilePath;
                }

                if (string.IsNullOrEmpty(finalPath) || !File.Exists(finalPath))
                {
                    throw new FileNotFoundException("ملف الـ PDF النهائي غير موجود لإرساله للطباعة.");
                }

                Process.Start(new ProcessStartInfo(finalPath) { UseShellExecute = true });

                await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
                {
                    var dbVouchers = await db.Vouchers
                        .Where(v => v.BatchId == job.BatchId && v.PrintStatus == VoucherPrintStatus.PdfGenerated)
                        .ToListAsync(cancellationToken);
                    foreach (var v in dbVouchers)
                    {
                        v.PrintStatus = VoucherPrintStatus.Printed;
                    }

                    var dbJob = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
                    if (dbJob != null)
                    {
                        dbJob.PrintedCount = dbJob.PdfCount;
                        dbJob.Status = PrintJobStatus.Completed;
                        dbJob.CurrentStep = PrintJobStep.Completed;
                    }
                    await db.SaveChangesAsync(cancellationToken);
                }

                await LogEventAsync(jobId, "Info", "اكتملت عملية التوليد والطباعة للمهمة بنجاح.", null, cancellationToken);
                progress?.Report((1, 0, 1, "اكتملت العملية بنجاح.", PrintJobStep.Completed));
            }
        }
        catch (Exception ex)
        {
            await UpdateJobStatusAsync(jobId, PrintJobStatus.Failed, ex.Message, cancellationToken);
            await LogEventAsync(jobId, "Error", $"حدث خطأ أثناء معالجة المهمة: {ex.Message}", ex.ToString(), cancellationToken);
            throw;
        }
    }

    private async Task UpdateJobStatusAsync(Guid jobId, PrintJobStatus status, string? errorMessage, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var dbJob = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
        if (dbJob != null)
        {
            dbJob.Status = status;
            if (errorMessage != null)
                dbJob.ErrorMessage = errorMessage;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task UpdateJobStepAsync(Guid jobId, PrintJobStep step, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var dbJob = await db.PrintJobs.FindAsync(new object[] { jobId }, cancellationToken);
        if (dbJob != null)
        {
            dbJob.CurrentStep = step;
            await db.SaveChangesAsync(cancellationToken);
        }
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
