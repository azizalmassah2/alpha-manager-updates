using System.Collections.Generic;

namespace Lux.Management.Console.Modules._Migration;

public interface ILegacyScreenMigrationService
{
    void TrackScreen(string screenName, string originalModule, MigrationStatus status, string notes = "");
    void UpdateStatus(string screenName, MigrationStatus status, string notes = "");
    IEnumerable<LegacyScreenInfo> GetMigrationStatus();
    void GenerateReport();
}
