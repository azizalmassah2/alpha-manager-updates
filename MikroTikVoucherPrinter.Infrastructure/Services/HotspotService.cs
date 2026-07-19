using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services
{
    public class HotspotService : IHotspotService
    {
        private readonly ISettingsService _settingsService;
        private const string SettingKey = "Hotspot_ConfigJson";
        private const string KeyString = "ALFA_HOTSPOT_SECURE_KEY_2026_@!";

        public HotspotService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public HotspotConfig LoadConfig()
        {
            var json = _settingsService.Get<string>(SettingKey, null!);
            if (string.IsNullOrEmpty(json))
            {
                var cfg = new HotspotConfig();
                
                cfg.SpeedOptions.Add(new SpeedOptionDto { Value = "", Label = "كرت هوتسبوت", Selected = false });
                cfg.SpeedOptions.Add(new SpeedOptionDto { Value = "2M/2M", Label = "سرعة اقتصادية", Selected = false });
                cfg.SpeedOptions.Add(new SpeedOptionDto { Value = "4M/4M", Label = "سرعة عادية", Selected = false });
                cfg.SpeedOptions.Add(new SpeedOptionDto { Value = "4M/8M", Label = "سرعة متوسطة", Selected = false });
                cfg.SpeedOptions.Add(new SpeedOptionDto { Value = "4M/16M", Label = "سرعة عالية", Selected = true });
                
                cfg.Packages.Add(new HotspotPackageDto { Vl = "يوم", Time = "3 ساعات", Size = "400 ميجا", Price = "100 ريال" });
                cfg.Packages.Add(new HotspotPackageDto { Vl = "3 أيام", Time = "6 ساعات", Size = "1 جيجا", Price = "200 ريال" });
                cfg.Packages.Add(new HotspotPackageDto { Vl = "8 أيام", Time = "8 ساعة", Size = "1.5 جيجا", Price = "300 ريال" });
                cfg.Packages.Add(new HotspotPackageDto { Vl = "12 أيام", Time = "20 ساعة", Size = "3 جيجا", Price = "500 ريال" });
                cfg.Packages.Add(new HotspotPackageDto { Vl = "باقة 7 يوم", Time = "45 ساعة", Size = "6 جيجا", Price = "1000 ريال" });
                cfg.Packages.Add(new HotspotPackageDto { Vl = "شهري", Time = "مفتوح", Size = "16 جيجا", Price = "3000 ريال" });
                cfg.Packages.Add(new HotspotPackageDto { Vl = "شهري", Time = "شهري", Size = "35 جيجا", Price = "5000 ريال" });
                
                cfg.SalesPoints.Add("تتوفر الكروت في كل البقالات المجاورة لتغطية الشبكة");

                return cfg;
            }

            try
            {
                return JsonSerializer.Deserialize<HotspotConfig>(json) ?? new HotspotConfig();
            }
            catch
            {
                return new HotspotConfig();
            }
        }

        public void SaveConfig(HotspotConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = false });
            _settingsService.Set(SettingKey, json);
        }

        public async Task<string> PreparePreviewFolderAsync(HotspotConfig config)
        {
            return await PrepareFolderLocalAsync(config);
        }

        public async Task<Result> UploadHotspotAsync(
            string host, 
            string username, 
            string password, 
            HotspotConfig config, 
            string destinationPath, 
            IProgress<double> progress, 
            CancellationToken token)
        {
            string localDir = string.Empty;
            try
            {
                localDir = await PrepareFolderLocalAsync(config);
                var alfaDir = Path.Combine(localDir, "ALFA");
                if (!Directory.Exists(alfaDir))
                {
                    return Result.Failure("فشل استخراج ملفات القالب المشفرة.", ErrorType.Unexpected);
                }

                var files = Directory.GetFiles(alfaDir, "*", SearchOption.AllDirectories);
                int totalFiles = files.Length;
                if (totalFiles == 0)
                {
                    return Result.Failure("لا توجد ملفات لرفعها.", ErrorType.Validation);
                }

                var context = new UploadContext();
                var destFolder = destinationPath.Trim('/', '\\');

                await UploadDirectoryFtpAsync(host, username, password, alfaDir, destFolder, totalFiles, context, progress, token);

                return Result.Success();
            }
            catch (OperationCanceledException ex)
            {
                return Result.Failure("تم إلغاء عملية الرفع بطلب من المستخدم.", ErrorType.Unexpected, ex);
            }
            catch (Exception ex)
            {
                return Result.Failure($"حدث خطأ أثناء رفع الملفات: {ex.Message}", ErrorType.ExternalService, ex);
            }
            finally
            {
                if (!string.IsNullOrEmpty(localDir) && Directory.Exists(localDir))
                {
                    try { Directory.Delete(localDir, true); } catch { }
                }
            }
        }

        private async Task<string> PrepareFolderLocalAsync(HotspotConfig config)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"hotspot_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            var tempZip = Path.Combine(tempDir, "temp_alfa.zip");

            // Extract embedded ALFA.enc
            var assembly = Assembly.GetEntryAssembly() ?? typeof(HotspotService).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Lux.Management.Console.Assets.ALFA.enc"))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException("لم يتم العثور على المورد المدمج المشفر ALFA.enc داخل ملفات البرنامج.");
                }

                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    var encryptedBytes = ms.ToArray();

                    // Decrypt bytes using repeating XOR key
                    var keyBytes = Encoding.UTF8.GetBytes(KeyString);
                    var decryptedBytes = new byte[encryptedBytes.Length];
                    for (int i = 0; i < encryptedBytes.Length; i++)
                    {
                        decryptedBytes[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyBytes.Length]);
                    }

                    await File.WriteAllBytesAsync(tempZip, decryptedBytes);
                }
            }

            // Unzip file
            ZipFile.ExtractToDirectory(tempZip, tempDir);
            File.Delete(tempZip);

            // Generate config.js
            var serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var configJson = JsonSerializer.Serialize(config, serializerOptions);
            var jsContent = $"window.siteConfig = {configJson};";

            var mainConfigPath = Path.Combine(tempDir, "ALFA", "config.js");
            var cssConfigPath = Path.Combine(tempDir, "ALFA", "css", "config.js");

            if (File.Exists(mainConfigPath))
            {
                await File.WriteAllTextAsync(mainConfigPath, jsContent, Encoding.UTF8);
            }
            if (File.Exists(cssConfigPath))
            {
                await File.WriteAllTextAsync(cssConfigPath, jsContent, Encoding.UTF8);
            }

            return tempDir;
        }

        private async Task UploadDirectoryFtpAsync(
            string host, 
            string username, 
            string password, 
            string localDirPath, 
            string remoteDirPath, 
            int totalFiles, 
            UploadContext context, 
            IProgress<double> progress, 
            CancellationToken token)
        {
            // Create remote dir if not exists
            await CreateFtpDirectoryOptionalAsync(host, username, password, remoteDirPath);

            var files = Directory.GetFiles(localDirPath);
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(file);
                var remoteFilePath = $"{remoteDirPath}/{fileName}";

                var ftpUrl = $"ftp://{host}/{remoteFilePath}";
                await UploadFileFtpAsync(ftpUrl, file, username, password, token);

                context.UploadedCount++;
                progress.Report((double)context.UploadedCount / totalFiles * 100);
            }

            var subDirs = Directory.GetDirectories(localDirPath);
            foreach (var subDir in subDirs)
            {
                var dirName = Path.GetFileName(subDir);
                var remoteSubDirPath = $"{remoteDirPath}/{dirName}";
                await UploadDirectoryFtpAsync(host, username, password, subDir, remoteSubDirPath, totalFiles, context, progress, token);
            }
        }

        private async Task CreateFtpDirectoryOptionalAsync(string host, string username, string password, string remoteDirPath)
        {
            try
            {
                var ftpUrl = $"ftp://{host}/{remoteDirPath}";
                var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Method = WebRequestMethods.Ftp.MakeDirectory;
                req.Credentials = new NetworkCredential(username, password);
                req.UsePassive = true;
                req.KeepAlive = false;
                req.Timeout = 15000;

                using (var resp = (FtpWebResponse)await req.GetResponseAsync())
                {
                    // Success or directory already exists
                }
            }
            catch
            {
                // Swallowing exceptions as directory may already exist
            }
        }

        private async Task UploadFileFtpAsync(string ftpUrl, string localPath, string username, string password, CancellationToken token)
        {
            var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
            req.Method = WebRequestMethods.Ftp.UploadFile;
            req.Credentials = new NetworkCredential(username, password);
            req.UsePassive = true;
            req.UseBinary = true;
            req.KeepAlive = false;
            req.Timeout = 20000;

            using (var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ftpStream = await req.GetRequestStreamAsync())
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    await ftpStream.WriteAsync(buffer, 0, bytesRead, token);
                }
            }
        }

        public async Task<System.Collections.Generic.Dictionary<string, byte[]>> GetPreviewFilesAsync(HotspotConfig config)
        {
            var files = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            var assembly = Assembly.GetEntryAssembly() ?? typeof(HotspotService).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Lux.Management.Console.Assets.ALFA.enc"))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException("لم يتم العثور على المورد المدمج المشفر ALFA.enc.");
                }

                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    var encryptedBytes = ms.ToArray();

                    var keyBytes = Encoding.UTF8.GetBytes(KeyString);
                    var decryptedBytes = new byte[encryptedBytes.Length];
                    for (int i = 0; i < encryptedBytes.Length; i++)
                    {
                        decryptedBytes[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyBytes.Length]);
                    }

                    using (var zipStream = new MemoryStream(decryptedBytes))
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name)) continue;

                            var relativePath = entry.FullName.Replace('\\', '/');
                            
                            using (var entryStream = entry.Open())
                            using (var entryMs = new MemoryStream())
                            {
                                await entryStream.CopyToAsync(entryMs);
                                files[relativePath] = entryMs.ToArray();
                            }
                        }
                    }
                }
            }

            var serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var configJson = JsonSerializer.Serialize(config, serializerOptions);
            var jsContent = $"window.siteConfig = {configJson};";
            var jsBytes = Encoding.UTF8.GetBytes(jsContent);

            files["ALFA/config.js"] = jsBytes;
            files["ALFA/css/config.js"] = jsBytes;

            return files;
        }

        public async Task<string?> DownloadFileFtpAsync(string host, string username, string password, string remoteFilePath)
        {
            try
            {
                var ftpUrl = $"ftp://{host}/{remoteFilePath.TrimStart('/')}";
                var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.Credentials = new NetworkCredential(username, password);
                req.UsePassive = true;
                req.UseBinary = true;
                req.KeepAlive = false;
                req.Timeout = 10000;

                using (var resp = (FtpWebResponse)await req.GetResponseAsync())
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> UploadFileFtpAsync(string host, string username, string password, byte[] fileBytes, string remoteFilePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(remoteFilePath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir))
                {
                    await CreateFtpDirectoryOptionalAsync(host, username, password, dir);
                }

                var ftpUrl = $"ftp://{host}/{remoteFilePath.Replace('\\', '/').TrimStart('/')}";
                var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Method = WebRequestMethods.Ftp.UploadFile;
                req.Credentials = new NetworkCredential(username, password);
                req.UsePassive = true;
                req.UseBinary = true;
                req.KeepAlive = false;
                req.Timeout = 10000;

                using (var ftpStream = await req.GetRequestStreamAsync())
                {
                    await ftpStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
 
        private class UploadContext
        {
            public int UploadedCount { get; set; }
        }
    }
}
