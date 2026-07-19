namespace Lux.MikroTik.Models;

public class MikroTikConnectionOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 8728;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 10;
    public RouterOsProviderType ProviderType { get; set; } = RouterOsProviderType.Mock;
}
