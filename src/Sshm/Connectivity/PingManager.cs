using System.Diagnostics;
using System.Net.Sockets;
using Renci.SshNet;
using Sshm.Core.Models;

namespace Sshm.Connectivity;

public enum PingStatus
{
    Unknown,
    Connecting,
    Online,
    Offline,
}

public sealed class HostPingResult
{
    public string HostName { get; init; } = string.Empty;

    public PingStatus Status { get; init; }

    public Exception? Error { get; init; }

    public TimeSpan Duration { get; init; }
}

public sealed class PingManager
{
    private readonly Dictionary<string, HostPingResult> results = new(StringComparer.Ordinal);
    private readonly object syncRoot = new();
    private readonly TimeSpan timeout;
    private readonly string configFile;

    public PingManager(TimeSpan timeout, string configFile)
    {
        this.timeout = timeout;
        this.configFile = configFile;
    }

    public PingStatus GetStatus(string hostName)
    {
        lock (syncRoot)
        {
            if (results.TryGetValue(hostName, out HostPingResult? result))
            {
                return result.Status;
            }
        }

        return PingStatus.Unknown;
    }

    public bool TryGetResult(string hostName, out HostPingResult? result)
    {
        lock (syncRoot)
        {
            return results.TryGetValue(hostName, out result);
        }
    }

    public async Task<HostPingResult> PingHostAsync(SshHost host, CancellationToken cancellationToken = default)
    {
        DateTime start = DateTime.UtcNow;
        UpdateStatus(host.Name, PingStatus.Connecting, null, TimeSpan.Zero);

        if (!string.IsNullOrEmpty(host.ProxyJump) || !string.IsNullOrEmpty(host.ProxyCommand))
        {
            return await PingWithExternalCommandAsync(host, start, cancellationToken).ConfigureAwait(false);
        }

        string hostname = string.IsNullOrEmpty(host.Hostname) ? host.Name : host.Hostname;
        string port = string.IsNullOrEmpty(host.Port) ? "22" : host.Port;

        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeout);

            using TcpClient client = new();
            await client.ConnectAsync(hostname, int.Parse(port), linked.Token).ConfigureAwait(false);

            try
            {
                using SshClient sshClient = new(new ConnectionInfo(hostname, int.Parse(port), host.User, new NoneAuthenticationMethod(host.User)));
                sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(2);
                sshClient.Connect();
                sshClient.Disconnect();
            }
            catch
            {
                // TCP 可达即视为在线
            }

            TimeSpan duration = DateTime.UtcNow - start;
            HostPingResult online = new()
            {
                HostName = host.Name,
                Status = PingStatus.Online,
                Duration = duration,
            };
            UpdateStatus(host.Name, PingStatus.Online, null, duration);
            return online;
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - start;
            HostPingResult offline = new()
            {
                HostName = host.Name,
                Status = PingStatus.Offline,
                Error = ex,
                Duration = duration,
            };
            UpdateStatus(host.Name, PingStatus.Offline, ex, duration);
            return offline;
        }
    }

    public async Task PingAllHostsAsync(IEnumerable<SshHost> hosts, CancellationToken cancellationToken = default)
    {
        List<Task> tasks = [];
        foreach (SshHost host in hosts)
        {
            tasks.Add(PingHostAsync(host, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<HostPingResult> PingWithExternalCommandAsync(SshHost host, DateTime start, CancellationToken cancellationToken)
    {
        int timeoutSec = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
        List<string> args =
        [
            "-q",
            "-o", "BatchMode=yes",
            "-o", "StrictHostKeyChecking=no",
            "-o", $"ConnectTimeout={timeoutSec}",
        ];

        if (!string.IsNullOrEmpty(configFile))
        {
            args.Add("-F");
            args.Add(configFile);
        }

        args.Add(host.Name);
        args.Add("exit");

        ProcessStartInfo psi = new()
        {
            FileName = "ssh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ssh process");
            using CancellationTokenRegistration reg = cancellationToken.Register(() =>
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            });

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            TimeSpan duration = DateTime.UtcNow - start;
            PingStatus status = process.ExitCode == 0 ? PingStatus.Online : PingStatus.Offline;
            HostPingResult result = new()
            {
                HostName = host.Name,
                Status = status,
                Duration = duration,
                Error = process.ExitCode == 0 ? null : new InvalidOperationException($"ssh exit code {process.ExitCode}"),
            };
            UpdateStatus(host.Name, status, result.Error, duration);
            return result;
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - start;
            HostPingResult result = new()
            {
                HostName = host.Name,
                Status = PingStatus.Offline,
                Error = ex,
                Duration = duration,
            };
            UpdateStatus(host.Name, PingStatus.Offline, ex, duration);
            return result;
        }
    }

    private void UpdateStatus(string hostName, PingStatus status, Exception? error, TimeSpan duration)
    {
        lock (syncRoot)
        {
            results[hostName] = new HostPingResult
            {
                HostName = hostName,
                Status = status,
                Error = error,
                Duration = duration,
            };
        }
    }
}
