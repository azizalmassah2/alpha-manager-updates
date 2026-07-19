using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Providers;

public class MockRouterOsProvider : IRouterOsProvider, IRouterOsTextProvider
{
    private bool _isConnected;

    public bool IsConnected => _isConnected;

    public Task<Result> ConnectAsync(MikroTikConnectionOptions options)
    {
        _isConnected = true;
        return Task.FromResult(Result.Success());
    }

    public Task<Result> DisconnectAsync()
    {
        _isConnected = false;
        return Task.FromResult(Result.Success());
    }

    public Task<Result<MikroTikResponse>> ExecuteAsync(MikroTikCommand command)
    {
        if (!_isConnected) return Task.FromResult(Result<MikroTikResponse>.Failure("Not connected", ErrorType.ExternalService));
        return Task.FromResult(Result<MikroTikResponse>.Success(new MikroTikResponse { Success = true, Message = "Mock execution successful" }));
    }

    public Task<Result<string>> ExecuteTextAsync(MikroTikCommand command)
    {
        if (!_isConnected) return Task.FromResult(Result<string>.Failure("Not connected", ErrorType.ExternalService));
        return Task.FromResult(Result<string>.Success("Mock text execution successful"));
    }
}
