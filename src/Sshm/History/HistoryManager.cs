using System.Text.Json.Serialization;
using Sshm.Config;
using Sshm.Core.Models;

namespace Sshm.History;

public sealed class ConnectionHistory
{
    [JsonPropertyName("connections")]
    public Dictionary<string, ConnectionInfo> Connections { get; set; } = new(StringComparer.Ordinal);
}

public sealed class PortForwardConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("local_port")]
    public string LocalPort { get; set; } = string.Empty;

    [JsonPropertyName("remote_host")]
    public string RemoteHost { get; set; } = string.Empty;

    [JsonPropertyName("remote_port")]
    public string RemotePort { get; set; } = string.Empty;

    [JsonPropertyName("bind_address")]
    public string BindAddress { get; set; } = string.Empty;
}

public sealed class ConnectionInfo
{
    [JsonPropertyName("host_name")]
    public string HostName { get; set; } = string.Empty;

    [JsonPropertyName("last_connect")]
    public DateTime LastConnect { get; set; }

    [JsonPropertyName("connect_count")]
    public int ConnectCount { get; set; }

    [JsonPropertyName("port_forwarding")]
    public PortForwardConfig? PortForwarding { get; set; }
}

public sealed class HistoryManager
{
    private readonly string historyPath;
    private ConnectionHistory history = new();

    public HistoryManager(string historyPath, ConnectionHistory history)
    {
        this.historyPath = historyPath;
        this.history = history;
    }

    public static HistoryManager Create()
    {
        string configDir = PlatformPaths.GetSSHMConfigDir();
        Directory.CreateDirectory(configDir);
        string historyPath = Path.Combine(configDir, "sshm_history.json");
        MigrateOldHistoryFile(historyPath);

        ConnectionHistory loaded = new();
        if (File.Exists(historyPath))
        {
            string json = File.ReadAllText(historyPath);
            loaded = System.Text.Json.JsonSerializer.Deserialize<ConnectionHistory>(json) ?? new ConnectionHistory();
        }

        return new HistoryManager(historyPath, loaded);
    }

    private static void MigrateOldHistoryFile(string newHistoryPath)
    {
        if (File.Exists(newHistoryPath))
        {
            return;
        }

        string sshDir = PlatformPaths.GetSSHDirectory();
        string oldHistoryPath = Path.Combine(sshDir, "sshm_history.json");
        if (!File.Exists(oldHistoryPath))
        {
            return;
        }

        File.Copy(oldHistoryPath, newHistoryPath, overwrite: false);
        try
        {
            File.Delete(oldHistoryPath);
        }
        catch
        {
            // 迁移成功即可，旧文件删除失败不阻断
        }
    }

    public void RecordConnection(string hostName)
    {
        DateTime now = DateTime.Now;
        if (history.Connections.TryGetValue(hostName, out ConnectionInfo? conn))
        {
            conn.LastConnect = now;
            conn.ConnectCount++;
            history.Connections[hostName] = conn;
        }
        else
        {
            history.Connections[hostName] = new ConnectionInfo
            {
                HostName = hostName,
                LastConnect = now,
                ConnectCount = 1,
            };
        }

        SaveHistory();
    }

    public bool TryGetLastConnectionTime(string hostName, out DateTime lastConnect)
    {
        if (history.Connections.TryGetValue(hostName, out ConnectionInfo? conn))
        {
            lastConnect = conn.LastConnect;
            return true;
        }

        lastConnect = default;
        return false;
    }

    public int GetConnectionCount(string hostName)
    {
        if (history.Connections.TryGetValue(hostName, out ConnectionInfo? conn))
        {
            return conn.ConnectCount;
        }

        return 0;
    }

    public List<SshHost> SortHostsByLastUsed(List<SshHost> hosts)
    {
        List<SshHost> sorted = [.. hosts];
        sorted.Sort((a, b) =>
        {
            bool existsA = TryGetLastConnectionTime(a.Name, out DateTime timeA);
            bool existsB = TryGetLastConnectionTime(b.Name, out DateTime timeB);

            if (existsA && existsB)
            {
                return timeB.CompareTo(timeA);
            }

            if (existsA)
            {
                return -1;
            }

            if (existsB)
            {
                return 1;
            }

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return sorted;
    }

    public void RecordPortForwarding(string hostName, string forwardType, string localPort, string remoteHost, string remotePort, string bindAddress)
    {
        DateTime now = DateTime.Now;
        PortForwardConfig portForwardConfig = new()
        {
            Type = forwardType,
            LocalPort = localPort,
            RemoteHost = remoteHost,
            RemotePort = remotePort,
            BindAddress = bindAddress,
        };

        if (history.Connections.TryGetValue(hostName, out ConnectionInfo? conn))
        {
            conn.LastConnect = now;
            conn.ConnectCount++;
            conn.PortForwarding = portForwardConfig;
            history.Connections[hostName] = conn;
        }
        else
        {
            history.Connections[hostName] = new ConnectionInfo
            {
                HostName = hostName,
                LastConnect = now,
                ConnectCount = 1,
                PortForwarding = portForwardConfig,
            };
        }

        SaveHistory();
    }

    public PortForwardConfig? GetPortForwardingConfig(string hostName)
    {
        if (history.Connections.TryGetValue(hostName, out ConnectionInfo? conn))
        {
            return conn.PortForwarding;
        }

        return null;
    }

    private void SaveHistory()
    {
        string? dir = Path.GetDirectoryName(historyPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = System.Text.Json.JsonSerializer.Serialize(history, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(historyPath, json);
    }
}
