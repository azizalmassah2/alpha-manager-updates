using System.Collections.Generic;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface IBackupService
    {
        Task<BackupData> CreateBackupAsync(string ip, string session, string deviceIp);
        Task<List<string>> ListBackupsAsync();
        Task<BackupData> LoadBackupAsync(string filename);
    }
}
