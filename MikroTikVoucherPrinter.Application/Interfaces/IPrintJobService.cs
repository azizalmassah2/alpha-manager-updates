using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IPrintJobService
{
    Task<PrintJob> CreateJobAsync(VoucherGenerationRequest request, Guid templateId, int count, CancellationToken cancellationToken = default);
    
    Task ExecuteJobAsync(Guid jobId, IProgress<(int success, int failed, int total, string phase, PrintJobStep step)>? progress = null, CancellationToken cancellationToken = default);
    
    Task ResumeJobAsync(Guid jobId, IProgress<(int success, int failed, int total, string phase, PrintJobStep step)>? progress = null, CancellationToken cancellationToken = default);
    
    Task CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    
    Task RebuildPdfOnlyAsync(Guid jobId, CancellationToken cancellationToken = default);
    
    Task LogEventAsync(Guid jobId, string level, string message, string? details = null, CancellationToken cancellationToken = default);
    
    Task<List<PrintJob>> GetActiveJobsAsync(CancellationToken cancellationToken = default);
    
    Task<List<PrintJob>> GetJobHistoryAsync(CancellationToken cancellationToken = default);
    
    Task<List<PrintJobEvent>> GetJobEventsAsync(Guid jobId, CancellationToken cancellationToken = default);
}
