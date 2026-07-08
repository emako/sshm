using Sshm.Core.Models;

namespace Sshm.Config;

public static class SshConfigParser
{
    public static List<SshHost> ParseSSHConfig()
    {
        string configPath = PlatformPaths.GetDefaultSSHConfigPath();
        return ParseSSHConfigFile(configPath);
    }

    public static List<SshHost> ParseSSHConfigFile(string configPath)
    {
        Dictionary<string, bool> processedFiles = new(StringComparer.OrdinalIgnoreCase);
        return ParseSSHConfigFileWithProcessedFiles(configPath, processedFiles);
    }

    internal static List<SshHost> ParseSSHConfigFileWithProcessedFiles(
        string configPath,
        Dictionary<string, bool> processedFiles)
    {
        string absPath = Path.GetFullPath(configPath);

        if (processedFiles.ContainsKey(absPath))
        {
            return [];
        }

        processedFiles[absPath] = true;

        if (!File.Exists(configPath))
        {
            if (absPath == SshConfigInternals.GetMainConfigPath())
            {
                SshConfigInternals.EnsureSSHDirectory();
                File.WriteAllText(configPath, string.Empty);
                FilePermissions.SetSecureFilePermissions(configPath);
            }

            return [];
        }

        List<SshHost> hosts = [];
        SshHost? currentHost = null;
        List<string> currentAliasNames = [];
        List<string> pendingTags = [];
        int lineNumber = 0;

        using StreamReader reader = new(configPath);
        while (true)
        {
            string? rawLine = reader.ReadLine();
            if (rawLine == null)
            {
                break;
            }

            lineNumber++;
            string line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("# Tags:", StringComparison.Ordinal))
            {
                string tagsStr = line["# Tags:".Length..].Trim();
                if (tagsStr.Length > 0)
                {
                    string[] tagParts = tagsStr.Split(',');
                    foreach (string rawTag in tagParts)
                    {
                        string tag = rawTag.Trim();
                        if (tag.Length > 0)
                        {
                            pendingTags.Add(tag);
                        }
                    }
                }

                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string key = parts[0].ToLowerInvariant();
            string value = string.Join(' ', parts.Skip(1));

            switch (key)
            {
                case "include":
                    List<SshHost> includeHosts = ProcessIncludeDirective(value, configPath, processedFiles);
                    hosts.AddRange(includeHosts);
                    break;

                case "host":
                    FinalizeCurrentHost(hosts, ref currentHost, ref currentAliasNames);

                    List<string> validHostNames = SshConfigInternals.ParseHostNamesFromLine("Host " + value);
                    if (validHostNames.Count == 0)
                    {
                        currentHost = null;
                        currentAliasNames = [];
                        pendingTags = [];
                        break;
                    }

                    currentHost = new SshHost
                    {
                        Name = validHostNames[0],
                        Port = "22",
                        Tags = [.. pendingTags],
                        SourceFile = absPath,
                        LineNumber = lineNumber,
                    };

                    currentAliasNames = validHostNames.Count > 1
                        ? validHostNames.Skip(1).ToList()
                        : [];

                    pendingTags = [];
                    break;

                case "hostname":
                    if (currentHost != null)
                    {
                        currentHost.Hostname = value;
                    }

                    break;

                case "user":
                    if (currentHost != null)
                    {
                        currentHost.User = value;
                    }

                    break;

                case "port":
                    if (currentHost != null)
                    {
                        currentHost.Port = value;
                    }

                    break;

                case "identityfile":
                    if (currentHost != null)
                    {
                        currentHost.Identity = value;
                    }

                    break;

                case "proxyjump":
                    if (currentHost != null)
                    {
                        currentHost.ProxyJump = value;
                    }

                    break;

                case "proxycommand":
                    if (currentHost != null)
                    {
                        currentHost.ProxyCommand = value;
                    }

                    break;

                case "remotecommand":
                    if (currentHost != null)
                    {
                        currentHost.RemoteCommand = value;
                    }

                    break;

                case "requesttty":
                    if (currentHost != null)
                    {
                        currentHost.RequestTty = value;
                    }

                    break;

                default:
                    if (currentHost != null && line.Trim().Length > 0)
                    {
                        string optionLine = parts[0] + " " + value;
                        if (string.IsNullOrEmpty(currentHost.Options))
                        {
                            currentHost.Options = optionLine;
                        }
                        else
                        {
                            currentHost.Options += '\n' + optionLine;
                        }
                    }

                    break;
            }
        }

        FinalizeCurrentHost(hosts, ref currentHost, ref currentAliasNames);
        return hosts;
    }

    public static List<SshHost> FilterVisibleHosts(IReadOnlyList<SshHost> hosts)
    {
        List<SshHost> visible = [];
        foreach (SshHost host in hosts)
        {
            if (!SshConfigInternals.HostHasTag(host.Tags, "hidden"))
            {
                visible.Add(host);
            }
        }

        return visible;
    }

    public static List<string> GetAllConfigFiles()
    {
        string configPath = PlatformPaths.GetDefaultSSHConfigPath();
        Dictionary<string, bool> processedFiles = new(StringComparer.OrdinalIgnoreCase);
        ParseSSHConfigFileWithProcessedFiles(configPath, processedFiles);
        return [.. processedFiles.Keys];
    }

    public static List<string> GetAllConfigFilesFromBase(string baseConfigPath)
    {
        if (string.IsNullOrEmpty(baseConfigPath))
        {
            return GetAllConfigFiles();
        }

        Dictionary<string, bool> processedFiles = new(StringComparer.OrdinalIgnoreCase);
        ParseSSHConfigFileWithProcessedFiles(baseConfigPath, processedFiles);
        return [.. processedFiles.Keys];
    }

    private static void FinalizeCurrentHost(
        List<SshHost> hosts,
        ref SshHost? currentHost,
        ref List<string> currentAliasNames)
    {
        if (currentHost == null)
        {
            return;
        }

        hosts.Add(currentHost);

        if (currentAliasNames.Count > 0)
        {
            foreach (string aliasName in currentAliasNames)
            {
                SshHost aliasHost = SshConfigInternals.CloneHost(currentHost);
                aliasHost.Name = aliasName;
                hosts.Add(aliasHost);
            }
        }

        currentAliasNames = [];
        currentHost = null;
    }

    private static List<SshHost> ProcessIncludeDirective(
        string pattern,
        string baseConfigPath,
        Dictionary<string, bool> processedFiles)
    {
        if (pattern.StartsWith('~'))
        {
            string homeDir = PlatformPaths.GetHomeDir();
            pattern = Path.Combine(homeDir, pattern[1..].TrimStart('/', '\\'));
        }

        if (!Path.IsPathRooted(pattern))
        {
            string baseDir = Path.GetDirectoryName(baseConfigPath) ?? ".";
            pattern = Path.Combine(baseDir, pattern);
        }

        List<string> matches = GlobHelper.Glob(pattern);
        List<SshHost> allHosts = [];

        foreach (string match in matches)
        {
            if (Directory.Exists(match))
            {
                continue;
            }

            if (match.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (SshConfigInternals.IsNonSSHConfigFile(match))
            {
                continue;
            }

            try
            {
                List<SshHost> includedHosts = ParseSSHConfigFileWithProcessedFiles(match, processedFiles);
                allHosts.AddRange(includedHosts);
            }
            catch
            {
                // Skip files that cannot be parsed.
            }
        }

        return allHosts;
    }
}
