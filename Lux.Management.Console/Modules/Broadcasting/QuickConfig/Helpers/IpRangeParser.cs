using System.Net;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers
{
    public static class IpRangeParser
    {
        public static List<string> Parse(string input)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(input))
                return result;

            // Split by newlines, commas, or semicolons
            var lines = input.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine)) continue;

                // Check if it's a range like 192.168.1.20-192.168.1.50
                if (cleanLine.Contains('-'))
                {
                    var rangeParts = cleanLine.Split('-');
                    if (rangeParts.Length == 2)
                    {
                        var startStr = rangeParts[0].Trim();
                        var endStr = rangeParts[1].Trim();

                        if (IPAddress.TryParse(startStr, out var startIp) && IPAddress.TryParse(endStr, out var endIp))
                        {
                            var startBytes = startIp.GetAddressBytes();
                            var endBytes = endIp.GetAddressBytes();

                            if (startBytes.Length == 4 && endBytes.Length == 4)
                            {
                                // Ensure they share the first 3 octets for safety/sanity in typical subnet ranges
                                if (startBytes[0] == endBytes[0] && startBytes[1] == endBytes[1] && startBytes[2] == endBytes[2])
                                {
                                    int startVal = startBytes[3];
                                    int endVal = endBytes[3];

                                    // Swap if they entered it backwards
                                    if (startVal > endVal)
                                    {
                                        var temp = startVal;
                                        startVal = endVal;
                                        endVal = temp;
                                    }

                                    for (int i = startVal; i <= endVal; i++)
                                    {
                                        result.Add($"{startBytes[0]}.{startBytes[1]}.{startBytes[2]}.{i}");
                                    }
                                    continue;
                                }
                            }
                        }
                    }
                }

                // If not range, treat as single IP
                if (IPAddress.TryParse(cleanLine, out _))
                {
                    result.Add(cleanLine);
                }
            }

            return result.Distinct().ToList();
        }
    }
}
