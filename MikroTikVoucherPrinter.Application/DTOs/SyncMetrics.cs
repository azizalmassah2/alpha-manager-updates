using System.Threading;

namespace MikroTikVoucherPrinter.Application.DTOs;

public class SyncMetrics
{
    private int _success;
    private int _failed;
    private int _retries;
    private int _skipped;

    public int Success => _success;
    public int Failed => _failed;
    public int Retries => _retries;
    public int Skipped => _skipped;

    public void IncrementSuccess() => Interlocked.Increment(ref _success);
    public void IncrementFailed() => Interlocked.Increment(ref _failed);
    public void IncrementRetries() => Interlocked.Increment(ref _retries);
    public void IncrementSkipped() => Interlocked.Increment(ref _skipped);

    /// <summary>
    /// يدمج نتائج عملية مزامنة أخرى مع هذه النتائج.
    /// مطلوب في RetryFailedAsync لدمج additionalMetrics مع metrics الأصلية.
    /// </summary>
    public SyncMetrics Merge(SyncMetrics other)
    {
        if (other is null) return this;
        var merged = new SyncMetrics();
        Interlocked.Add(ref merged._success, _success + other._success);
        Interlocked.Add(ref merged._failed,  _failed  + other._failed);
        Interlocked.Add(ref merged._retries, _retries + other._retries);
        Interlocked.Add(ref merged._skipped, _skipped + other._skipped);
        return merged;
    }

    public override string ToString() => $"نجح: {Success} | فشل: {Failed} | تمت محاولته: {Retries} | تم تخطيه: {Skipped}";
}
