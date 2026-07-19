using System.Net;

namespace OpenWrtProgrammerPro.Helpers
{
    public static class HostnameGenerator
    {
        public static string Prefix { get; set; } = "YAZ";

        public static string Generate(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return $"{Prefix}-XX";

            if (IPAddress.TryParse(ipAddress.Trim(), out var parsedIp))
            {
                var bytes = parsedIp.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    return $"{Prefix}-{bytes[3]}";
                }
            }

            // Fallback for incomplete strings or formatting
            var parts = ipAddress.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1].Trim(), out var lastOctet))
            {
                return $"{Prefix}-{lastOctet}";
            }

            return $"{Prefix}-XX";
        }
    }
}
