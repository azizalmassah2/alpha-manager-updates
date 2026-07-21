using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System;

namespace Lux.Management.Console.Tests;

public class UnitTest1
{
    [Fact]
    public async Task RunTracer()
    {
        var services = new ServiceCollection();
        
        // Add Logging
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add application and infrastructure services
        MikroTikVoucherPrinter.Application.DependencyInjection.AddApplicationServices(services);
        MikroTikVoucherPrinter.Infrastructure.DependencyInjection.AddInfrastructureServices(services);
        Lux.MikroTik.DependencyInjection.AddMikroTikServices(services, useMockProvider: false);

        // Add dispatcher service stub
        var dispatcherMock = new Moq.Mock<Lux.Platform.Abstractions.Interfaces.IDispatcherService>();
        services.AddSingleton(dispatcherMock.Object);

        var sp = services.BuildServiceProvider();

        // Switch to the last active router
        var activeRouterContext = sp.GetRequiredService<IActiveRouterContext>();
        using (var scope = sp.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MikroTikVoucherPrinter.Infrastructure.Data.LuxCardDbContext>>();
            using var luxDb = await dbFactory.CreateDbContextAsync();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<UnitTest1>>();
            await MikroTikVoucherPrinter.Infrastructure.Data.LuxCardSqliteSchemaUpgrade.ApplyAsync(luxDb, logger);

            var db = scope.ServiceProvider.GetRequiredService<MikroTikVoucherPrinter.Infrastructure.Data.PlatformDbContext>();
            var router = await db.Routers.FirstOrDefaultAsync(r => r.Host == "10.0.0.1");
            if (router == null)
            {
                router = await db.Routers.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
            }
            
            if (router != null)
            {
                await activeRouterContext.SwitchRouterAsync(router);
                var queryService = scope.ServiceProvider.GetRequiredService<IVoucherQueryService>();
                var list = await queryService.GetAllVouchersFromMikroTikAsync(CancellationToken.None);
                
                // Write a tag to confirm execution
                var tagPath = @"C:\Users\MrAziz\.gemini\antigravity\brain\6bb8795a-6087-4e0f-984f-b7e2636f66c8\scratch\tracer_success.txt";
                System.IO.File.WriteAllText(tagPath, $"Fetched {list.Count} items at {DateTime.Now}");
            }
        }
    }

    [Fact]
    public async Task DumpCustomersAndIdentity()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        MikroTikVoucherPrinter.Application.DependencyInjection.AddApplicationServices(services);
        MikroTikVoucherPrinter.Infrastructure.DependencyInjection.AddInfrastructureServices(services);
        Lux.MikroTik.DependencyInjection.AddMikroTikServices(services, useMockProvider: false);
        var dispatcherMock = new Moq.Mock<Lux.Platform.Abstractions.Interfaces.IDispatcherService>();
        services.AddSingleton(dispatcherMock.Object);
        var sp = services.BuildServiceProvider();

        var activeRouterContext = sp.GetRequiredService<IActiveRouterContext>();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MikroTikVoucherPrinter.Infrastructure.Data.PlatformDbContext>();
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ROUTERS IN DB ===");
            var routers = await db.Routers.ToListAsync();
            foreach (var r in routers)
            {
                sb.AppendLine($"Host: {r.Host}, User: {r.Username}, Port: {r.Port}, CreatedAt: {r.CreatedAt}");
            }

            var router = routers.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            if (router != null)
            {
                await activeRouterContext.SwitchRouterAsync(router);
                var executor = scope.ServiceProvider.GetRequiredService<Lux.MikroTik.Connectivity.IMikroTikCommandExecutor>();
                
                try
                {
                    var res = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/customer/print" });
                    sb.AppendLine("=== CUSTOMERS ===");
                    foreach (var d in res.RawData)
                    {
                        sb.AppendLine("{");
                        foreach (var kvp in d) sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                        sb.AppendLine("}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed to print customers: {ex.Message}");
                }
                
                try
                {
                    var resRouters = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/router/print" });
                    sb.AppendLine("=== USER MANAGER ROUTERS ===");
                    foreach (var d in resRouters.RawData)
                    {
                        sb.AppendLine("{");
                        foreach (var kvp in d) sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                        sb.AppendLine("}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed to print routers: {ex.Message}");
                }

                try
                {
                    var userRes = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/user/print" });
                    sb.AppendLine("=== ROUTER USERS ===");
                    foreach (var d in userRes.RawData)
                    {
                        sb.AppendLine("{");
                        foreach (var kvp in d) sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                        sb.AppendLine("}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed to print users: {ex.Message}");
                }

                try
                {
                    var groupRes = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/user/group/print" });
                    sb.AppendLine("=== ROUTER GROUPS ===");
                    foreach (var d in groupRes.RawData)
                    {
                        sb.AppendLine("{");
                        foreach (var kvp in d) sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                        sb.AppendLine("}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed to print groups: {ex.Message}");
                }

                try
                {
                    var logRes = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/log/print" });
                    sb.AppendLine("=== ROUTER LOGS ===");
                    foreach (var d in logRes.RawData.TakeLast(30))
                    {
                        sb.AppendLine($"{d.GetValueOrDefault("time")} - {d.GetValueOrDefault("topics")}: {d.GetValueOrDefault("message")}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed to print logs: {ex.Message}");
                }

                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lux_customers_dump.txt"), sb.ToString());
            }
        }
    }

    [Fact]
    public async Task TryAddProfile()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        MikroTikVoucherPrinter.Application.DependencyInjection.AddApplicationServices(services);
        MikroTikVoucherPrinter.Infrastructure.DependencyInjection.AddInfrastructureServices(services);
        Lux.MikroTik.DependencyInjection.AddMikroTikServices(services, useMockProvider: false);
        var dispatcherMock = new Moq.Mock<Lux.Platform.Abstractions.Interfaces.IDispatcherService>();
        services.AddSingleton(dispatcherMock.Object);
        var sp = services.BuildServiceProvider();

        var activeRouterContext = sp.GetRequiredService<IActiveRouterContext>();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MikroTikVoucherPrinter.Infrastructure.Data.PlatformDbContext>();
            var router = await db.Routers.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
            if (router != null)
            {
                await activeRouterContext.SwitchRouterAsync(router);
                var executor = scope.ServiceProvider.GetRequiredService<Lux.MikroTik.Connectivity.IMikroTikCommandExecutor>();
                
                var sb = new System.Text.StringBuilder();

                // Test 1: Add profile without owner
                try
                {
                    sb.AppendLine("--- Test 1: /tool/user-manager/profile/add without owner ---");
                    var cmd = new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/add" };
                    cmd.Parameters.Add("name", "Diag1");
                    cmd.Parameters.Add("name-for-users", "Diag1");
                    cmd.Parameters.Add("starts-at", "logon");
                    cmd.Parameters.Add("validity", "1d");
                    cmd.Parameters.Add("price", "100.00");
                    var res = await executor.ExecuteAsync(cmd);
                    sb.AppendLine("Success!");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed: {ex.Message}");
                }

                // Test 2: Add profile with owner=admin
                try
                {
                    sb.AppendLine("--- Test 2: /tool/user-manager/profile/add with owner=admin ---");
                    var cmd = new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/add" };
                    cmd.Parameters.Add("name", "Diag2");
                    cmd.Parameters.Add("name-for-users", "Diag2");
                    cmd.Parameters.Add("owner", "admin");
                    cmd.Parameters.Add("starts-at", "logon");
                    cmd.Parameters.Add("validity", "1d");
                    cmd.Parameters.Add("price", "100.00");
                    var res = await executor.ExecuteAsync(cmd);
                    sb.AppendLine("Success!");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed: {ex.Message}");
                }

                // Test 3: Add profile with customer=admin
                try
                {
                    sb.AppendLine("--- Test 3: /tool/user-manager/profile/add with customer=admin ---");
                    var cmd = new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/add" };
                    cmd.Parameters.Add("name", "Diag3");
                    cmd.Parameters.Add("name-for-users", "Diag3");
                    cmd.Parameters.Add("customer", "admin");
                    cmd.Parameters.Add("starts-at", "logon");
                    cmd.Parameters.Add("validity", "1d");
                    cmd.Parameters.Add("price", "100.00");
                    var res = await executor.ExecuteAsync(cmd);
                    sb.AppendLine("Success!");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed: {ex.Message}");
                }

                // Test 5: Add profile with owner=*1 (Customer ID)
                try
                {
                    sb.AppendLine("--- Test 5: /tool/user-manager/profile/add with owner=*1 ---");
                    var cmd = new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/add" };
                    cmd.Parameters.Add("name", "Diag5");
                    cmd.Parameters.Add("name-for-users", "Diag5");
                    cmd.Parameters.Add("owner", "*1");
                    cmd.Parameters.Add("starts-at", "logon");
                    cmd.Parameters.Add("validity", "1d");
                    cmd.Parameters.Add("price", "100.00");
                    var res = await executor.ExecuteAsync(cmd);
                    sb.AppendLine("Success!");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed: {ex.Message}");
                }

                // Test 8: Original four parameters EXACTLY as in initial commit
                try
                {
                    sb.AppendLine("--- Test 8: /tool/user-manager/profile/add with ONLY original 4 parameters ---");
                    var cmd = new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/add" };
                    cmd.Parameters.Add("name", "Diag8");
                    cmd.Parameters.Add("validity", "1d");
                    cmd.Parameters.Add("price", "100.00");
                    cmd.Parameters.Add("shared-users", "2");
                    var res = await executor.ExecuteAsync(cmd);
                    sb.AppendLine("Success!");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed: {ex.Message}");
                }

                // Test 7: Add custom customer first, then add profile owned by them
                try
                {
                    sb.AppendLine("--- Test 7: Add customer testcust, then add profile ---");
                    
                    var addCust = new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/customer/add" };
                    addCust.Parameters.Add("login", "testcust");
                    addCust.Parameters.Add("password", "123456");
                    addCust.Parameters.Add("permissions", "owner");
                    await executor.ExecuteAsync(addCust);
                    sb.AppendLine("Customer testcust added successfully.");

                    var cmd = new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/add" };
                    cmd.Parameters.Add("name", "Diag7");
                    cmd.Parameters.Add("name-for-users", "Diag7");
                    cmd.Parameters.Add("owner", "testcust");
                    cmd.Parameters.Add("starts-at", "logon");
                    cmd.Parameters.Add("validity", "1d");
                    cmd.Parameters.Add("price", "100.00");
                    var res = await executor.ExecuteAsync(cmd);
                    sb.AppendLine("Profile Diag7 added successfully.");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Failed in Test 7: {ex.Message}");
                }

                // Clean up Diag profiles if created
                try
                {
                    await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/remove", Arguments = new[] { ".id", "Diag1" } });
                } catch {}
                try
                {
                    await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/remove", Arguments = new[] { ".id", "Diag2" } });
                } catch {}
                try
                {
                    await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/remove", Arguments = new[] { ".id", "Diag3" } });
                } catch {}
                try
                {
                    await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/remove", Arguments = new[] { ".id", "Diag5" } });
                } catch {}
                try
                {
                    await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/remove", Arguments = new[] { ".id", "Diag7" } });
                } catch {}
                try
                {
                    await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/customer/remove", Arguments = new[] { ".id", "testcust" } });
                } catch {}

                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lux_add_profile_diag.txt"), sb.ToString());
            }
        }
    }

    [Fact]
    public async Task TryAddProfileViaFtpImport()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        MikroTikVoucherPrinter.Application.DependencyInjection.AddApplicationServices(services);
        MikroTikVoucherPrinter.Infrastructure.DependencyInjection.AddInfrastructureServices(services);
        Lux.MikroTik.DependencyInjection.AddMikroTikServices(services, useMockProvider: false);
        var dispatcherMock = new Moq.Mock<Lux.Platform.Abstractions.Interfaces.IDispatcherService>();
        services.AddSingleton(dispatcherMock.Object);
        var sp = services.BuildServiceProvider();

        var activeRouterContext = sp.GetRequiredService<IActiveRouterContext>();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MikroTikVoucherPrinter.Infrastructure.Data.PlatformDbContext>();
            var router = await db.Routers.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
            if (router != null)
            {
                await activeRouterContext.SwitchRouterAsync(router);
                var executor = scope.ServiceProvider.GetRequiredService<Lux.MikroTik.Connectivity.IMikroTikCommandExecutor>();

                var sb = new System.Text.StringBuilder();

                // Step 1: Create the .rsc script content
                string profileName = "FtpDiag1";
                string rscContent = $"/tool user-manager profile add name={profileName} name-for-users={profileName} owner=admin starts-at=logon validity=1d price=200.00\r\n";
                string rscFileName = "lux_profile_cmd.rsc";

                // Step 2: Upload via FTP
                try
                {
                    sb.AppendLine("--- Step 1: Upload .rsc via FTP ---");
                    string plainPassword = "";
                    try
                    {
                        var secureStorage = scope.ServiceProvider.GetRequiredService<Lux.Platform.Abstractions.Interfaces.IDispatcherService>() != null ?
                            scope.ServiceProvider.GetRequiredService<Lux.Platform.Abstractions.Interfaces.ISecureStorageService>() : null;
                        if (secureStorage != null)
                            plainPassword = secureStorage.Decrypt(router.EncryptedPassword);
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"Decryption failed: {ex.Message}");
                    }

                    string ftpUrl = $"ftp://{router.Host}/{rscFileName}";
                    var ftpRequest = (System.Net.FtpWebRequest)System.Net.WebRequest.Create(ftpUrl);
                    ftpRequest.Method = System.Net.WebRequestMethods.Ftp.UploadFile;
                    ftpRequest.Credentials = new System.Net.NetworkCredential(router.Username, plainPassword);
                    ftpRequest.UseBinary = true;
                    ftpRequest.UsePassive = true;
                    ftpRequest.KeepAlive = false;

                    byte[] fileContents = System.Text.Encoding.UTF8.GetBytes(rscContent);
                    ftpRequest.ContentLength = fileContents.Length;

                    using (var requestStream = await ftpRequest.GetRequestStreamAsync())
                    {
                        await requestStream.WriteAsync(fileContents, 0, fileContents.Length);
                    }

                    using (var response = (System.Net.FtpWebResponse)await ftpRequest.GetResponseAsync())
                    {
                        sb.AppendLine($"FTP Upload Status: {response.StatusDescription}");
                    }
                    sb.AppendLine("FTP Upload completed.");

                    var filesPrint = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/file/print" });
                    sb.AppendLine("=== FILES ON ROUTER ===");
                    foreach (var f in filesPrint.RawData)
                    {
                        sb.AppendLine($"Name: {f.GetValueOrDefault("name")}, Size: {f.GetValueOrDefault("size")}, Type: {f.GetValueOrDefault("type")}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"FTP Upload failed: {ex.Message}");
                    if (ex.InnerException != null) sb.AppendLine($"  Inner: {ex.InnerException.Message}");
                }

                // Step 3: Import the .rsc file via API
                try
                {
                    sb.AppendLine("--- Step 2: Import .rsc via API (ExecuteTextAsync) ---");
                    var importCmd = new Lux.MikroTik.Models.MikroTikCommand { Command = "/import" };
                    importCmd.Parameters.Add("file-name", rscFileName);
                    
                    var textProvider = scope.ServiceProvider.GetRequiredService<Lux.MikroTik.Providers.IRouterOsTextProvider>();
                    var importResText = await textProvider.ExecuteTextAsync(importCmd);
                    sb.AppendLine("Import completed. Success? " + importResText.IsSuccess);
                    sb.AppendLine("Output text:");
                    sb.AppendLine(importResText.Value);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Import failed: {ex.Message}");
                    if (ex.InnerException != null) sb.AppendLine($"  Inner: {ex.InnerException.Message}");
                }

                // Log print
                try
                {
                    var logRes = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/log/print" });
                    sb.AppendLine("=== ROUTER LOGS AFTER IMPORT ===");
                    foreach (var d in logRes.RawData.TakeLast(15))
                    {
                        sb.AppendLine($"{d.GetValueOrDefault("time")} - {d.GetValueOrDefault("topics")}: {d.GetValueOrDefault("message")}");
                    }
                }
                catch {}

                // Step 4: Verify profile was created
                try
                {
                    sb.AppendLine("--- Step 3: Verify profile ---");
                    var checkProfile = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand { Command = "/tool/user-manager/profile/print" });
                    sb.AppendLine("=== PROFILES ===");
                    foreach (var d in checkProfile.RawData)
                    {
                        sb.AppendLine("{");
                        foreach (var kvp in d) sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                        sb.AppendLine("}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Print profiles failed: {ex.Message}");
                }

                // Step 5: Clean up
                try
                {
                    // Remove the profile
                    var profiles = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand 
                    { 
                        Command = "/tool/user-manager/profile/print",
                        Arguments = new[] { "?name", profileName }
                    });
                    foreach (var d in profiles.RawData)
                    {
                        var id = d.GetValueOrDefault(".id");
                        if (!string.IsNullOrEmpty(id))
                        {
                            await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand 
                            { 
                                Command = "/tool/user-manager/profile/remove",
                                Arguments = new[] { ".id", id }
                            });
                            sb.AppendLine($"Cleaned up profile {profileName} (id={id}).");
                        }
                    }
                } catch (Exception ex) { sb.AppendLine($"Profile cleanup: {ex.Message}"); }

                try
                {
                    // Remove the .rsc file from router
                    var files = await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand 
                    { 
                        Command = "/file/print",
                        Arguments = new[] { "?name", rscFileName }
                    });
                    foreach (var d in files.RawData)
                    {
                        var id = d.GetValueOrDefault(".id");
                        if (!string.IsNullOrEmpty(id))
                        {
                            await executor.ExecuteAsync(new Lux.MikroTik.Models.MikroTikCommand 
                            { 
                                Command = "/file/remove",
                                Arguments = new[] { ".id", id }
                            });
                            sb.AppendLine($"Cleaned up file {rscFileName}.");
                        }
                    }
                } catch (Exception ex) { sb.AppendLine($"File cleanup: {ex.Message}"); }

                System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lux_ftp_import_diag.txt"), sb.ToString());
            }
        }
    }

    [Fact]
    public async Task DiagnoseApiConnection()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        MikroTikVoucherPrinter.Application.DependencyInjection.AddApplicationServices(services);
        MikroTikVoucherPrinter.Infrastructure.DependencyInjection.AddInfrastructureServices(services);
        Lux.MikroTik.DependencyInjection.AddMikroTikServices(services, useMockProvider: false);
        var dispatcherMock = new Moq.Mock<Lux.Platform.Abstractions.Interfaces.IDispatcherService>();
        services.AddSingleton(dispatcherMock.Object);
        var sp = services.BuildServiceProvider();

        var activeRouterContext = sp.GetRequiredService<IActiveRouterContext>();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MikroTikVoucherPrinter.Infrastructure.Data.PlatformDbContext>();
            var router = await db.Routers.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
            if (router != null)
            {
                await activeRouterContext.SwitchRouterAsync(router);
                var executor = scope.ServiceProvider.GetRequiredService<Lux.MikroTik.Connectivity.IMikroTikCommandExecutor>();
                var sb = new System.Text.StringBuilder();

            }
        }
    }

    // Raw RouterOS API client class for C#
    private class RawRosApiClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private System.Net.Sockets.TcpClient _client;
        private System.IO.Stream _stream;

        public RawRosApiClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            _client = new System.Net.Sockets.TcpClient(_host, _port);
            _stream = _client.GetStream();
        }

        private void WriteWord(string word)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(word);
            int length = bytes.Length;
            if (length < 0x80)
            {
                _stream.WriteByte((byte)length);
            }
            else if (length < 0x4000)
            {
                length |= 0x8000;
                _stream.WriteByte((byte)((length >> 8) & 0xFF));
                _stream.WriteByte((byte)(length & 0xFF));
            }
            else
            {
                throw new ArgumentException("Word too long");
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        private string ReadWord()
        {
            int b1 = _stream.ReadByte();
            if (b1 == -1) return null;
            
            int length;
            if ((b1 & 0x80) == 0x00)
            {
                length = b1;
            }
            else if ((b1 & 0xC0) == 0x80)
            {
                int b2 = _stream.ReadByte();
                if (b2 == -1) return null;
                length = ((b1 & 0x3F) << 8) + b2;
            }
            else
            {
                throw new Exception("Unsupported length prefix");
            }
            
            if (length == 0) return "";
            
            byte[] buffer = new byte[length];
            int read = 0;
            while (read < length)
            {
                int r = _stream.Read(buffer, read, length - read);
                if (r <= 0) break;
                read += r;
            }
            return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
        }

        public List<List<string>> SendCmd(string cmd, Dictionary<string, string> parameters = null)
        {
            WriteWord(cmd);
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    WriteWord($"={kvp.Key}={kvp.Value}");
                }
            }
            WriteWord("");

            var response = new List<List<string>>();
            while (true)
            {
                var sentence = new List<string>();
                while (true)
                {
                    string w = ReadWord();
                    if (string.IsNullOrEmpty(w)) break;
                    sentence.Add(w);
                }
                if (sentence.Count > 0)
                {
                    response.Add(sentence);
                    if (sentence[0] == "!done" || sentence[0] == "!fatal") break;
                }
            }
            return response;
        }

        public bool Login(string username, string password)
        {
            var res = SendCmd("/login", new Dictionary<string, string> { { "name", username }, { "password", password } });
            if (res.Count > 0 && res[0].Count > 0 && res[0][0] == "!done")
            {
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}