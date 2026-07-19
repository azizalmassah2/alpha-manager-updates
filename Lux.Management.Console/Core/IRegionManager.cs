using System;
using System.Threading.Tasks;

namespace Lux.Management.Console.Core;

public interface IRegionManager
{
    void RegisterRegion(string regionName, object regionTarget);
    void NavigateTo(string regionName, object view);
    void NavigateTo<TView>(string regionName) where TView : class;
    void ClearRegion(string regionName);
}
