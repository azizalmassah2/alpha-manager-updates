using System;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

public class OperationProgressEventArgs : EventArgs
{
    public Guid JobId { get; set; }
    public double Percentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface IOperationProgressReporter
{
    event EventHandler<OperationProgressEventArgs>? OnProgress;
    void ReportProgress(Guid jobId, double percentage, string message);
}
