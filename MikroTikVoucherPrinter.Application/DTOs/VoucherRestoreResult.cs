using System;

namespace MikroTikVoucherPrinter.Application.DTOs;

public enum RestoreStatus
{
    Success,
    AlreadyExistsReconciled,
    ConflictDetected,
    ValidationFailed,
    RouterError,
    UnexpectedError
}

public class VoucherRestoreResult
{
    public Guid VoucherId { get; set; }
    public string Username { get; set; } = string.Empty;
    public RestoreStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ConflictReason { get; set; }
    public long DurationMs { get; set; }

    public static VoucherRestoreResult Succeeded(Guid voucherId, string username, long durationMs)
    {
        return new VoucherRestoreResult
        {
            VoucherId = voucherId,
            Username = username,
            Status = RestoreStatus.Success,
            DurationMs = durationMs
        };
    }

    public static VoucherRestoreResult Reconciled(Guid voucherId, string username, long durationMs)
    {
        return new VoucherRestoreResult
        {
            VoucherId = voucherId,
            Username = username,
            Status = RestoreStatus.AlreadyExistsReconciled,
            DurationMs = durationMs
        };
    }

    public static VoucherRestoreResult Conflict(Guid voucherId, string username, string reason, long durationMs)
    {
        return new VoucherRestoreResult
        {
            VoucherId = voucherId,
            Username = username,
            Status = RestoreStatus.ConflictDetected,
            ConflictReason = reason,
            ErrorMessage = reason,
            DurationMs = durationMs
        };
    }

    public static VoucherRestoreResult Failed(Guid voucherId, string username, RestoreStatus status, string error, long durationMs)
    {
        return new VoucherRestoreResult
        {
            VoucherId = voucherId,
            Username = username,
            Status = status,
            ErrorMessage = error,
            DurationMs = durationMs
        };
    }
}
