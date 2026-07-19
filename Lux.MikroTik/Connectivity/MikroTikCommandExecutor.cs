using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;

using Lux.MikroTik.Providers;
using Lux.MikroTik.Exceptions;

namespace Lux.MikroTik.Connectivity;

public class MikroTikCommandExecutor : IMikroTikCommandExecutor
{
    private readonly IRouterOsProvider _provider;

    public MikroTikCommandExecutor(IRouterOsProvider provider)
    {
        _provider = provider;
    }

    public async Task<MikroTikResponse> ExecuteAsync(MikroTikCommand command, CancellationToken cancellationToken = default)
    {
        var result = await _provider.ExecuteAsync(command);

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\n[COMMAND EXECUTED]");
            sb.AppendLine($"COMMAND: {command.Command}");
            
            if (command.Parameters != null && command.Parameters.Count > 0)
            {
                var pList = new System.Collections.Generic.List<string>();
                foreach (var p in command.Parameters) pList.Add($"{p.Key}={p.Value}");
                sb.AppendLine($"PARAMS: {string.Join(", ", pList)}");
            }
            if (command.Arguments != null && command.Arguments.Length > 0)
            {
                sb.AppendLine($"ARGS: {string.Join(" ", command.Arguments)}");
            }

            sb.AppendLine("\n[RAW RESPONSE]");
            if (result.IsFailure)
            {
                sb.AppendLine($"FAILURE: {result.ErrorMessage}");
            }
            else if (result.Value?.RawData != null)
            {
                foreach (var dict in result.Value.RawData)
                {
                    sb.AppendLine("{");
                    foreach (var kvp in dict)
                    {
                        sb.AppendLine($"  \"{kvp.Key}\": \"{kvp.Value}\"");
                    }
                    sb.AppendLine("}");
                }
            }
            else
            {
                sb.AppendLine("NO RAW DATA OR NULL RESPONSE");
            }
            sb.AppendLine("====================================================");
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lux_api_trace.txt"), sb.ToString());
        }
        catch { }

        if (result.IsFailure)
        {
            throw new MikroTikCommandException($"Command execution failed: {result.ErrorMessage}");
        }
        
        return result.Value;
    }
}
