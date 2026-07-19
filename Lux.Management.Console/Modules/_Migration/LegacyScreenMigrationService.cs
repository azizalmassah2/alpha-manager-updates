using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Lux.Management.Console.Modules._Migration;

public class LegacyScreenMigrationService : ILegacyScreenMigrationService
{
    private readonly ConcurrentDictionary<string, LegacyScreenInfo> _screens = new();

    public void TrackScreen(string screenName, string originalModule, MigrationStatus status, string notes = "")
    {
        _screens[screenName] = new LegacyScreenInfo
        {
            ScreenName = screenName,
            OriginalModule = originalModule,
            Status = status,
            Notes = notes
        };
    }

    public void UpdateStatus(string screenName, MigrationStatus status, string notes = "")
    {
        if (_screens.TryGetValue(screenName, out var info))
        {
            info.Status = status;
            if (!string.IsNullOrEmpty(notes))
            {
                info.Notes = notes;
            }
        }
    }

    public IEnumerable<LegacyScreenInfo> GetMigrationStatus()
    {
        return _screens.Values.ToList();
    }

    public void GenerateReport()
    {
        var screens = GetMigrationStatus().ToList();
        int total = screens.Count;
        int completed = screens.Count(s => s.Status == MigrationStatus.Completed);
        int inProgress = screens.Count(s => s.Status == MigrationStatus.InProgress);

        Debug.WriteLine("=== Legacy Screen Migration Report ===");
        Debug.WriteLine($"Total Screens Tracked: {total}");
        Debug.WriteLine($"Completed: {completed} | In Progress: {inProgress} | Pending: {total - completed - inProgress}");
        Debug.WriteLine("--------------------------------------");

        foreach (var screen in screens)
        {
            Debug.WriteLine($"[{screen.Status}] {screen.ScreenName} (from {screen.OriginalModule}) - {screen.Notes}");
        }
        Debug.WriteLine("======================================");
    }
}
