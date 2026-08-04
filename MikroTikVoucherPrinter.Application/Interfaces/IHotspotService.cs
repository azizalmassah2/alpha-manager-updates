using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Application.DTOs;

namespace MikroTikVoucherPrinter.Application.Interfaces
{
    public interface IHotspotService
    {
        HotspotConfig LoadConfig();
        void SaveConfig(HotspotConfig config);
        Task<string> PreparePreviewFolderAsync(HotspotConfig config);
        Task<Dictionary<string, byte[]>> GetPreviewFilesAsync(HotspotConfig config);
        Task<string?> DownloadFileFtpAsync(string host, string username, string password, string remoteFilePath);
        Task<bool> UploadFileFtpAsync(string host, string username, string password, byte[] fileBytes, string remoteFilePath);
        Task<List<string>> GetRouterFoldersFtpAsync(string host, string username, string password);
        Task<Result> UploadConfigOnlyAsync(
            string host, 
            string username, 
            string password, 
            HotspotConfig config, 
            string destinationPath, 
            CancellationToken token);
        Task<Result> UploadHotspotAsync(
            string host, 
            string username, 
            string password, 
            HotspotConfig config, 
            string destinationPath, 
            IProgress<double> progress, 
            CancellationToken token);
        void SaveAdImage(int index, string sourceFilePath);
        void DeleteAdImage(int index);
        string? GetAdImagePath(int index);
    }
}
