using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using Lux.MikroTik.Models;

namespace Lux.MikroTik.Providers;

public interface IRouterOsTextProvider
{
    Task<Result<string>> ExecuteTextAsync(MikroTikCommand command);
}
