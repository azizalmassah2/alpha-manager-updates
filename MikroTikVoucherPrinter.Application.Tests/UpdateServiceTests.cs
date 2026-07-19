using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Models;
using MikroTikVoucherPrinter.Infrastructure.Services;
using Moq;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests
{
    public class UpdateServiceTests
    {
        private readonly Mock<ILogger<UpdateService>> _mockLogger = new();

        private (HttpListener listener, string baseUrl) StartLocalServer()
        {
            var listener = new HttpListener();
            var port = 20000 + Random.Shared.Next(1, 9000);
            var baseUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(baseUrl);
            listener.Start();
            return (listener, baseUrl);
        }

        private string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(data);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        [Fact]
        public async Task UpdateService_WithValidSha256_Succeeds()
        {
            var (listener, baseUrl) = StartLocalServer();
            var testBinaryContent = Encoding.UTF8.GetBytes("Valid executable binary payload for testing");
            var validHash = ComputeSha256(testBinaryContent);

            var updateInfo = new UpdateInfo
            {
                Version = "9.9.9",
                Enabled = true,
                DownloadUrl = $"{baseUrl}download/AlphaManager_Update.exe",
                Sha256 = validHash
            };

            var jsonManifest = JsonSerializer.Serialize(updateInfo);

            _ = Task.Run(() =>
            {
                try
                {
                    // 1. Respond to manifest check
                    var context1 = listener.GetContext();
                    var response1 = context1.Response;
                    var buffer1 = Encoding.UTF8.GetBytes(jsonManifest);
                    response1.ContentLength64 = buffer1.Length;
                    response1.OutputStream.Write(buffer1, 0, buffer1.Length);
                    response1.OutputStream.Close();

                    // 2. Respond to file download
                    var context2 = listener.GetContext();
                    var response2 = context2.Response;
                    response2.ContentLength64 = testBinaryContent.Length;
                    response2.OutputStream.Write(testBinaryContent, 0, testBinaryContent.Length);
                    response2.OutputStream.Close();
                }
                catch { }
            });

            try
            {
                var service = new UpdateService(_mockLogger.Object)
                {
                    UpdateManifestUrl = $"{baseUrl}update.json",
                    ExitProcessOnComplete = false, // Prevent test exit
                    CurrentVersionOverride = new Version(1, 0, 0)
                };

                var checkResult = await service.CheckForUpdateAsync();
                Assert.True(checkResult.HasUpdate);

                var progress = new Progress<int>();
                await service.DownloadAndInstallAsync(checkResult.Update!, progress);

                // Verify file downloaded to Temp and exists
                var fileName = $"AlphaManager_Update_{checkResult.Update!.Version}.exe";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                Assert.True(File.Exists(tempPath));

                // Clean up
                File.Delete(tempPath);
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        [Fact]
        public async Task UpdateService_WithInvalidSha256_ThrowsCryptographicExceptionAndDeletesTempFile()
        {
            var (listener, baseUrl) = StartLocalServer();
            var testBinaryContent = Encoding.UTF8.GetBytes("Valid executable binary payload for testing");
            // Manifest has a completely different hash
            var wrongHash = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f61234";

            var updateInfo = new UpdateInfo
            {
                Version = "9.9.8",
                Enabled = true,
                DownloadUrl = $"{baseUrl}download/AlphaManager_Update.exe",
                Sha256 = wrongHash
            };

            var jsonManifest = JsonSerializer.Serialize(updateInfo);

            _ = Task.Run(() =>
            {
                try
                {
                    var context1 = listener.GetContext();
                    var response1 = context1.Response;
                    var buffer1 = Encoding.UTF8.GetBytes(jsonManifest);
                    response1.ContentLength64 = buffer1.Length;
                    response1.OutputStream.Write(buffer1, 0, buffer1.Length);
                    response1.OutputStream.Close();

                    var context2 = listener.GetContext();
                    var response2 = context2.Response;
                    response2.ContentLength64 = testBinaryContent.Length;
                    response2.OutputStream.Write(testBinaryContent, 0, testBinaryContent.Length);
                    response2.OutputStream.Close();
                }
                catch { }
            });

            try
            {
                var service = new UpdateService(_mockLogger.Object)
                {
                    UpdateManifestUrl = $"{baseUrl}update.json",
                    ExitProcessOnComplete = false,
                    CurrentVersionOverride = new Version(1, 0, 0)
                };

                var checkResult = await service.CheckForUpdateAsync();
                Assert.True(checkResult.HasUpdate);

                var progress = new Progress<int>();
                var fileName = $"AlphaManager_Update_{checkResult.Update!.Version}.exe";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                // Should throw CryptographicException
                await Assert.ThrowsAsync<CryptographicException>(() =>
                    service.DownloadAndInstallAsync(checkResult.Update!, progress));

                // Verify temp file was deleted
                Assert.False(File.Exists(tempPath));
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        [Fact]
        public async Task UpdateService_WithEmptySha256_SucceedsForBackwardCompatibility()
        {
            var (listener, baseUrl) = StartLocalServer();
            var testBinaryContent = Encoding.UTF8.GetBytes("Valid executable binary payload for testing");

            var updateInfo = new UpdateInfo
            {
                Version = "9.9.7",
                Enabled = true,
                DownloadUrl = $"{baseUrl}download/AlphaManager_Update.exe",
                Sha256 = "" // Empty SHA256
            };

            var jsonManifest = JsonSerializer.Serialize(updateInfo);

            _ = Task.Run(() =>
            {
                try
                {
                    var context1 = listener.GetContext();
                    var response1 = context1.Response;
                    var buffer1 = Encoding.UTF8.GetBytes(jsonManifest);
                    response1.ContentLength64 = buffer1.Length;
                    response1.OutputStream.Write(buffer1, 0, buffer1.Length);
                    response1.OutputStream.Close();

                    var context2 = listener.GetContext();
                    var response2 = context2.Response;
                    response2.ContentLength64 = testBinaryContent.Length;
                    response2.OutputStream.Write(testBinaryContent, 0, testBinaryContent.Length);
                    response2.OutputStream.Close();
                }
                catch { }
            });

            try
            {
                var service = new UpdateService(_mockLogger.Object)
                {
                    UpdateManifestUrl = $"{baseUrl}update.json",
                    ExitProcessOnComplete = false,
                    CurrentVersionOverride = new Version(1, 0, 0)
                };

                var checkResult = await service.CheckForUpdateAsync();
                Assert.True(checkResult.HasUpdate);

                var progress = new Progress<int>();
                await service.DownloadAndInstallAsync(checkResult.Update!, progress);

                var fileName = $"AlphaManager_Update_{checkResult.Update!.Version}.exe";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                Assert.True(File.Exists(tempPath));

                // Clean up
                File.Delete(tempPath);
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        [Fact]
        public async Task UpdateService_WithMalformedSha256_ThrowsCryptographicExceptionAndDeletesTempFile()
        {
            var (listener, baseUrl) = StartLocalServer();
            var testBinaryContent = Encoding.UTF8.GetBytes("Valid executable binary payload for testing");
            // Not hex, and invalid length (too short)
            var malformedHash = "invalid-sha-hash";

            var updateInfo = new UpdateInfo
            {
                Version = "9.9.6",
                Enabled = true,
                DownloadUrl = $"{baseUrl}download/AlphaManager_Update.exe",
                Sha256 = malformedHash
            };

            var jsonManifest = JsonSerializer.Serialize(updateInfo);

            _ = Task.Run(() =>
            {
                try
                {
                    var context1 = listener.GetContext();
                    var response1 = context1.Response;
                    var buffer1 = Encoding.UTF8.GetBytes(jsonManifest);
                    response1.ContentLength64 = buffer1.Length;
                    response1.OutputStream.Write(buffer1, 0, buffer1.Length);
                    response1.OutputStream.Close();

                    var context2 = listener.GetContext();
                    var response2 = context2.Response;
                    response2.ContentLength64 = testBinaryContent.Length;
                    response2.OutputStream.Write(testBinaryContent, 0, testBinaryContent.Length);
                    response2.OutputStream.Close();
                }
                catch { }
            });

            try
            {
                var service = new UpdateService(_mockLogger.Object)
                {
                    UpdateManifestUrl = $"{baseUrl}update.json",
                    ExitProcessOnComplete = false,
                    CurrentVersionOverride = new Version(1, 0, 0)
                };

                var checkResult = await service.CheckForUpdateAsync();
                Assert.True(checkResult.HasUpdate);

                var progress = new Progress<int>();
                var fileName = $"AlphaManager_Update_{checkResult.Update!.Version}.exe";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                await Assert.ThrowsAsync<CryptographicException>(() =>
                    service.DownloadAndInstallAsync(checkResult.Update!, progress));

                // Verify temp file was deleted
                Assert.False(File.Exists(tempPath));
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        [Fact]
        public async Task UpdateService_WithDownloadFailure_DeletesTempFile()
        {
            var (listener, baseUrl) = StartLocalServer();

            var updateInfo = new UpdateInfo
            {
                Version = "9.9.5",
                Enabled = true,
                DownloadUrl = $"{baseUrl}non-existent-url-forcing-404.exe",
                Sha256 = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f61234"
            };

            var jsonManifest = JsonSerializer.Serialize(updateInfo);

            _ = Task.Run(() =>
            {
                try
                {
                    var context1 = listener.GetContext();
                    var response1 = context1.Response;
                    var buffer1 = Encoding.UTF8.GetBytes(jsonManifest);
                    response1.ContentLength64 = buffer1.Length;
                    response1.OutputStream.Write(buffer1, 0, buffer1.Length);
                    response1.OutputStream.Close();

                    var context2 = listener.GetContext();
                    var response2 = context2.Response;
                    response2.StatusCode = (int)HttpStatusCode.NotFound; // HTTP 404
                    response2.OutputStream.Close();
                }
                catch { }
            });

            try
            {
                var service = new UpdateService(_mockLogger.Object)
                {
                    UpdateManifestUrl = $"{baseUrl}update.json",
                    ExitProcessOnComplete = false,
                    CurrentVersionOverride = new Version(1, 0, 0)
                };

                var checkResult = await service.CheckForUpdateAsync();
                Assert.True(checkResult.HasUpdate);

                var progress = new Progress<int>();
                var fileName = $"AlphaManager_Update_{checkResult.Update!.Version}.exe";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                // Should throw HttpRequestException due to 404
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    service.DownloadAndInstallAsync(checkResult.Update!, progress));

                // Verify temp file was deleted/does not exist
                Assert.False(File.Exists(tempPath));
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }
    }
}
