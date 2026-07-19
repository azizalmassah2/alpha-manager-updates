using System;
using System.Threading.Tasks;

namespace Lux.Management.Console.Core;

public interface IAutoRefreshService
{
    void Start();
    void Stop();
    bool IsRunning { get; }
    
    /// <summary>
    /// Refresh interval in seconds (default 30)
    /// </summary>
    int IntervalSeconds { get; set; }
    
    void RegisterCallback(Func<Task> callback);
    void UnregisterCallback(Func<Task> callback);
}
