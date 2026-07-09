using Sshm.Core.Models;

namespace Sshm.Config;

public static class SshConfigQuery
{
    public static bool HostExistsInFile(string hostName, string configPath)
    {
        return HostExistsInSpecificFile(hostName, configPath);
    }

    public static bool HostExistsInSpecificFile(string hostName, string configPath)
    {
        if (!File.Exists(configPath))
        {
            return false;
        }

        using StreamReader reader = new(configPath);
        while (true)
        {
            string? rawLine = reader.ReadLine();
            if (rawLine == null)
            {
                break;
            }

            string line = rawLine.Trim();
            if (!line.StartsWith("host ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            List<string> hostNames = SshConfigInternals.ParseRawHostNamesFromLine(line);
            foreach (string name in hostNames)
            {
                if (name == hostName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static SshHost GetSSHHost(string hostName)
    {
        List<SshHost> hosts = SshConfigParser.ParseSSHConfig();
        foreach (SshHost host in hosts)
        {
            if (host.Name == hostName)
            {
                return host;
            }
        }

        throw new InvalidOperationException($"host '{hostName}' not found");
    }

    public static SshHost GetSSHHostFromFile(string hostName, string configPath)
    {
        List<SshHost> hosts = SshConfigParser.ParseSSHConfigFile(configPath);
        foreach (SshHost host in hosts)
        {
            if (host.Name == hostName)
            {
                return host;
            }
        }

        throw new InvalidOperationException($"host '{hostName}' not found");
    }

    public static SshHost FindHostInAllConfigs(string hostName)
    {
        List<SshHost> hosts = SshConfigParser.ParseSSHConfig();
        foreach (SshHost host in hosts)
        {
            if (host.Name == hostName)
            {
                return host;
            }
        }

        throw new InvalidOperationException($"host '{hostName}' not found in any configuration file");
    }

    public static bool QuickHostExists(string hostName)
    {
        string configPath = PlatformPaths.GetDefaultSSHConfigPath();
        return QuickHostExistsInFile(hostName, configPath);
    }

    public static bool QuickHostExistsInFile(string hostName, string configPath)
    {
        Dictionary<string, bool> processedFiles = new(StringComparer.OrdinalIgnoreCase);
        return QuickHostSearchInFile(hostName, configPath, processedFiles);
    }

    public static (bool IsMultiHost, List<string> HostNames) IsPartOfMultiHostDeclaration(
        string hostName,
        string configPath)
    {
        string[] lines = SshConfigInternals.ReadConfigLines(configPath);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("host ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            List<string> hostNames = SshConfigInternals.ParseRawHostNamesFromLine(line);
            foreach (string name in hostNames)
            {
                if (name == hostName)
                {
                    return (hostNames.Count > 1, hostNames);
                }
            }
        }

        return (false, []);
    }

    public static List<string> GetConfigFilesExcludingCurrent(string hostName, string baseConfigFile)
    {
        List<string> allFiles = string.IsNullOrEmpty(baseConfigFile)
            ? SshConfigParser.GetAllConfigFiles()
            : SshConfigParser.GetAllConfigFilesFromBase(baseConfigFile);

        SshHost host = FindHostInAllConfigs(hostName);
        List<string> filteredFiles = [];
        foreach (string file in allFiles)
        {
            if (!string.Equals(file, host.SourceFile, StringComparison.OrdinalIgnoreCase))
            {
                filteredFiles.Add(file);
            }
        }

        return filteredFiles;
    }

    private static bool QuickHostSearchInFile(
        string hostName,
        string configPath,
        Dictionary<string, bool> processedFiles)
    {
        string absPath = Path.GetFullPath(configPath);
        if (processedFiles.ContainsKey(absPath))
        {
            return false;
        }

        processedFiles[absPath] = true;

        if (!File.Exists(configPath))
        {
            return false;
        }

        using StreamReader reader = new(configPath);
        while (true)
        {
            string? rawLine = reader.ReadLine();
            if (rawLine == null)
            {
                break;
            }

            string line = rawLine.Trim();
            if (line.Length == 0 || (line.StartsWith('#') && !line.StartsWith("# Tags:", StringComparison.Ordinal)))
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
                    if (QuickSearchInclude(hostName, value, configPath, processedFiles))
                    {
                        return true;
                    }

                    break;

                case "host":
                    List<string> hostNames = SshConfigInternals.ParseHostNamesFromLine("Host " + value);
                    foreach (string candidateHostName in hostNames)
                    {
                        if (candidateHostName == hostName)
                        {
                            return true;
                        }
                    }

                    break;
            }
        }

        return false;
    }

    private static bool QuickSearchInclude(
        string hostName,
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
        foreach (string match in matches)
        {
            if (Directory.Exists(match))
            {
                continue;
            }

            if (SshConfigInternals.IsNonSSHConfigFile(match))
            {
                continue;
            }

            if (QuickHostSearchInFile(hostName, match, processedFiles))
            {
                return true;
            }
        }

        return false;
    }
}
