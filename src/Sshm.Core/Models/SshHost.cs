namespace Sshm.Core.Models;

public sealed class SshHost
{
    public string Name { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public string Port { get; set; } = "22";

    public string Identity { get; set; } = string.Empty;

    public string ProxyJump { get; set; } = string.Empty;

    public string ProxyCommand { get; set; } = string.Empty;

    public string Options { get; set; } = string.Empty;

    public string RemoteCommand { get; set; } = string.Empty;

    public string RequestTty { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string SourceFile { get; set; } = string.Empty;

    public int LineNumber { get; set; }
}
