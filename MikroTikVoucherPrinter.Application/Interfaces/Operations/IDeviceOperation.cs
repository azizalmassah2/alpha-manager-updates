using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces.Operations;

public class OperationExecutionContext
{
    public System.Guid JobId { get; set; }
    public System.Guid? DeviceId { get; set; }
}

public class OperationResult
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    
    public static OperationResult Success(string msg = "") => new OperationResult { IsSuccess = true, Message = msg };
    public static OperationResult Failure(string error) => new OperationResult { IsSuccess = false, Message = error };
}

public interface IDeviceOperation
{
    Task<OperationResult> ExecuteAsync(OperationExecutionContext context, CancellationToken cancellationToken);
}
