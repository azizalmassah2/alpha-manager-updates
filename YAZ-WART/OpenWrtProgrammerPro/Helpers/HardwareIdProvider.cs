using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace OpenWrtProgrammerPro.Helpers
{
    public static class HardwareIdProvider
    {
        public static string GetHardwareId()
        {
            string cpu = GetCpuId().Trim();
            string board = GetBoardSerial().Trim();
            string disk = GetDiskSerial().Trim();
            string guid = GetMachineGuid().Trim();

            string combined = $"{cpu}{board}{disk}{guid}";
            
            // If all WMI fails and returns empty, fall back to basic system properties
            if (string.IsNullOrWhiteSpace(combined))
            {
                combined = $"{Environment.MachineName}{Environment.UserName}{Environment.ProcessorCount}";
            }

            return HashSha256(combined);
        }

        public static string GetCpuIdHash()
        {
            return HashSha256(GetCpuId().Trim());
        }

        public static string GetBoardSerialHash()
        {
            return HashSha256(GetBoardSerial().Trim());
        }

        public static string GetDiskSerialHash()
        {
            return HashSha256(GetDiskSerial().Trim());
        }

        public static string GetMachineGuidHash()
        {
            return HashSha256(GetMachineGuid().Trim());
        }

        private static string GetCpuId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var item in searcher.Get())
                {
                    return item["ProcessorId"]?.ToString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        private static string GetBoardSerial()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (var item in searcher.Get())
                {
                    return item["SerialNumber"]?.ToString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        private static string GetDiskSerial()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
                foreach (var item in searcher.Get())
                {
                    var sn = item["SerialNumber"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(sn))
                    {
                        return sn;
                    }
                }
            }
            catch { }

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia");
                foreach (var item in searcher.Get())
                {
                    var sn = item["SerialNumber"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(sn))
                    {
                        return sn;
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private static string GetMachineGuid()
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return subKey?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
            }
            catch
            {
                try
                {
                    using var subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                    return subKey?.GetValue("MachineGuid")?.ToString() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private static string HashSha256(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
