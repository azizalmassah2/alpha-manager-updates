using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces
{
    public interface IBackupService
    {
        Task<BackupData> CreateBackupAsync(string ip, string session, string deviceIp);
        Task<List<string>> ListBackupsAsync();
        Task<BackupData> LoadBackupAsync(string filename);
    }
}
