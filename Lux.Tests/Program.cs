using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Lux.Management.Console;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace Lux.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var app = new App();
            // App constructor creates _host and configures DI!
            var serviceProvider = app.ServiceProvider;
            
            var profileService = serviceProvider.GetRequiredService<IProfileService>();
            
            Console.WriteLine("Fetching UserManager Profiles...");
            try {
                var profiles = await profileService.GetAllProfilesAsync(MikroTikVoucherPrinter.Domain.Enums.PackageSourceType.UserManager, CancellationToken.None);
                Console.WriteLine($"Got {profiles.Count} profiles.");
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
            
            Console.WriteLine("Done.");
            
            if (File.Exists(@"C:\temp\lux_trace.txt")) {
                Console.WriteLine(File.ReadAllText(@"C:\temp\lux_trace.txt"));
            }
        }
    }
}
