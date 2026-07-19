using System;
using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces
{
    public class VoucherImportProgressEventArgs : EventArgs
    {
        public Guid RouterId { get; }
        public int ImportedCount { get; }
        public int TotalCount { get; }
        public int ProgressPercent { get; }
        public bool IsPaused { get; }

        public VoucherImportProgressEventArgs(Guid routerId, int importedCount, int totalCount, bool isPaused)
        {
            RouterId = routerId;
            ImportedCount = importedCount;
            TotalCount = totalCount;
            ProgressPercent = totalCount > 0 ? (int)((double)importedCount / totalCount * 100) : 0;
            IsPaused = isPaused;
        }
    }

    public class VoucherImportErrorEventArgs : EventArgs
    {
        public Guid RouterId { get; }
        public string ErrorMessage { get; }

        public VoucherImportErrorEventArgs(Guid routerId, string errorMessage)
        {
            RouterId = routerId;
            ErrorMessage = errorMessage;
        }
    }

    public class SnapshotMismatchException : Exception
    {
        public int LocalCount { get; }
        public int SnapshotCount { get; }
        public double PercentageDifference { get; }

        public SnapshotMismatchException(int localCount, int snapshotCount, double percentageDifference)
            : base($"Snapshot mismatch detected. Local active count: {localCount}, Snapshot count: {snapshotCount}, Diff: {percentageDifference:F2}%")
        {
            LocalCount = localCount;
            SnapshotCount = snapshotCount;
            PercentageDifference = percentageDifference;
        }
    }

    public interface IVoucherBackgroundImportManager
    {
        event EventHandler<VoucherImportProgressEventArgs>? ProgressChanged;
        event EventHandler<Guid>? ImportCompleted;
        event EventHandler<VoucherImportErrorEventArgs>? ImportError;

        Task<bool> IsImportRequiredAsync(Guid routerId, CancellationToken cancellationToken = default);
        Task<int> GetRouterVoucherCountAsync(Guid routerId, CancellationToken cancellationToken = default);

        void StartImport(Guid routerId);
        void PauseImport(Guid routerId);
        void ResumeImport(Guid routerId);
        void CancelImport(Guid routerId);

        bool IsImporting(Guid routerId);
        bool IsPaused(Guid routerId);
        int GetImportedCount(Guid routerId);
        int GetTotalImportCount(Guid routerId);
        int GetProgressPercent(Guid routerId);

        Task RunTriggeredSyncAsync(Guid routerId, CancellationToken cancellationToken = default);
        Task RunFullSyncAsync(Guid routerId, CancellationToken cancellationToken = default);
        Task RunSnapshotSyncAsync(Guid routerId, bool force = false, CancellationToken cancellationToken = default);
        Task RunSegmentedSweepAsync(Guid routerId, int segmentIndex, int totalSegments, CancellationToken cancellationToken = default);
        Task RestoreVouchersChunkedAsync(Guid routerId, IProgress<(int restored, int total)> progress, CancellationToken cancellationToken = default);
        void InvalidateSweepCache(Guid routerId);

        /// <summary>
        /// يعيد مسار آخر نسخة منظفة (.clean) من قاعدة بيانات User Manager للراوتر المحدد.
        /// تُستخدم من شاشة Sales لقراءة بيانات المبيعات مباشرة.
        /// </summary>
        string? GetCachedCleanDbPath(Guid routerId);

        /// <summary>
        /// يقوم بتحميل قاعدة بيانات User Manager وحفظها في الكاش الموحد فقط (سريع جداً بدون مزامنة EF Core)
        /// </summary>
        Task DownloadAndCacheDbAsync(Guid routerId, CancellationToken cancellationToken = default);
    }
}
