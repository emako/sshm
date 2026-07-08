using Sshm.Core.Models;

namespace Sshm.Config;

internal static class SshConfigInternals
{
    internal static string GetMainConfigPath()
    {
        return Path.GetFullPath(PlatformPaths.GetDefaultSSHConfigPath());
    }

    internal static void EnsureSSHDirectory()
    {
        string sshDir = PlatformPaths.GetSSHDirectory();
        if (!Directory.Exists(sshDir))
        {
            Directory.CreateDirectory(sshDir);
        }
    }

    internal static void BackupConfig(string configPath)
    {
        string backupDir = PlatformPaths.GetSSHMBackupDir();
        Directory.CreateDirectory(backupDir);

        string filename = Path.GetFileName(configPath);
        string backupPath = Path.Combine(backupDir, filename + ".backup");
        File.Copy(configPath, backupPath, overwrite: true);
        FilePermissions.SetSecureFilePermissions(backupPath);
    }

    internal static string FormatSSHConfigValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Contains(' '))
        {
            return $"\"{value}\"";
        }

        return value;
    }

    internal static bool HostHasTag(IReadOnlyList<string> tags, string target)
    {
        foreach (string tag in tags)
        {
            if (string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static SshHost CloneHost(SshHost source)
    {
        return new SshHost
        {
            Name = source.Name,
            Hostname = source.Hostname,
            User = source.User,
            Port = source.Port,
            Identity = source.Identity,
            ProxyJump = source.ProxyJump,
            ProxyCommand = source.ProxyCommand,
            Options = source.Options,
            RemoteCommand = source.RemoteCommand,
            RequestTty = source.RequestTty,
            Tags = [.. source.Tags],
            SourceFile = source.SourceFile,
            LineNumber = source.LineNumber,
        };
    }

    internal static List<string> ParseRawHostNamesFromLine(string hostLine)
    {
        string hostPart = hostLine.StartsWith("Host ", StringComparison.OrdinalIgnoreCase)
            ? hostLine[5..].Trim()
            : hostLine.Trim();
        return [.. hostPart.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)];
    }

    internal static void AppendHostProperties(List<string> lines, SshHost host)
    {
        lines.Add("    HostName " + host.Hostname);

        if (!string.IsNullOrEmpty(host.User))
        {
            lines.Add("    User " + host.User);
        }

        if (!string.IsNullOrEmpty(host.Port) && host.Port != "22")
        {
            lines.Add("    Port " + host.Port);
        }

        if (!string.IsNullOrEmpty(host.Identity))
        {
            lines.Add("    IdentityFile " + FormatSSHConfigValue(host.Identity));
        }

        if (!string.IsNullOrEmpty(host.ProxyJump))
        {
            lines.Add("    ProxyJump " + host.ProxyJump);
        }

        if (!string.IsNullOrEmpty(host.ProxyCommand))
        {
            lines.Add("    ProxyCommand=" + host.ProxyCommand);
        }

        if (!string.IsNullOrEmpty(host.RemoteCommand))
        {
            lines.Add("    RemoteCommand " + host.RemoteCommand);
        }

        if (!string.IsNullOrEmpty(host.RequestTty))
        {
            lines.Add("    RequestTTY " + host.RequestTty);
        }

        if (!string.IsNullOrEmpty(host.Options))
        {
            string[] optionLines = host.Options.Split('\n');
            foreach (string rawOption in optionLines)
            {
                string option = rawOption.Trim();
                if (option.Length > 0)
                {
                    lines.Add("    " + option);
                }
            }
        }
    }

    internal static void AppendHostBlock(List<string> lines, SshHost host)
    {
        if (host.Tags.Count > 0)
        {
            lines.Add("# Tags: " + string.Join(", ", host.Tags));
        }

        lines.Add("Host " + host.Name);
        AppendHostProperties(lines, host);
    }

    internal static string BuildHostBlockText(SshHost host, bool leadingBlankLine = false, bool trailingBlankLine = false)
    {
        List<string> lines = [];
        if (leadingBlankLine)
        {
            lines.Add(string.Empty);
        }

        AppendHostBlock(lines, host);

        if (trailingBlankLine)
        {
            lines.Add(string.Empty);
        }

        return string.Join('\n', lines);
    }

    internal static bool IsNonSSHConfigFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath).ToLowerInvariant();

        if (fileName == "readme" || fileName == "readme.txt")
        {
            return true;
        }

        string[] excludedExtensions =
        [
            ".txt", ".md", ".rst", ".doc", ".docx", ".pdf",
            ".log", ".tmp", ".bak", ".old", ".orig",
            ".json", ".xml", ".yaml", ".yml", ".toml",
            ".sh", ".bash", ".zsh", ".fish", ".ps1", ".bat", ".cmd",
            ".py", ".pl", ".rb", ".js", ".php", ".go", ".c", ".cpp",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg",
            ".zip", ".tar", ".gz", ".bz2", ".xz",
        ];

        foreach (string extension in excludedExtensions)
        {
            if (fileName.EndsWith(extension, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (fileName.StartsWith('.'))
        {
            return true;
        }

        return HasNonSSHContent(filePath);
    }

    internal static bool HasNonSSHContent(string filePath)
    {
        try
        {
            using FileStream stream = File.OpenRead(filePath);
            byte[] buffer = new byte[2048];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
            {
                return false;
            }

            string content = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead).ToLowerInvariant();
            string[] nonSshIndicators =
            [
                "<!doctype", "<html>", "<xml>", "<?xml",
                "#!/bin/", "#!/usr/bin/",
                "# readme", "# documentation", "# license",
                "package main", "function ", "class ", "def ",
                "import ", "require ", "#include",
                "select ", "insert ", "update ", "delete ",
            ];

            foreach (string indicator in nonSshIndicators)
            {
                if (content.Contains(indicator, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    internal static string[] ReadConfigLines(string configPath)
    {
        string content = File.ReadAllText(configPath);
        return content.Split('\n');
    }

    internal static void WriteConfigLines(string configPath, List<string> lines)
    {
        string newContent = string.Join('\n', lines);
        File.WriteAllText(configPath, newContent);
    }

    internal static bool IsHostBlockEnd(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Length == 0 || trimmed.StartsWith("Host ", StringComparison.OrdinalIgnoreCase);
    }

    internal static void SkipHostBlockBody(string[] lines, ref int index)
    {
        while (index < lines.Length && !IsHostBlockEnd(lines[index]))
        {
            index++;
        }
    }

    internal static void SkipTrailingEmptyLines(string[] lines, ref int index)
    {
        while (index < lines.Length && lines[index].Trim().Length == 0)
        {
            index++;
        }
    }

    internal static List<string> ParseHostNamesFromLine(string hostLine)
    {
        string hostPart = hostLine[5..].Trim();
        string[] hostNames = hostPart.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        List<string> validHostNames = [];

        foreach (string rawName in hostNames)
        {
            string hostName = rawName.Trim('"');
            if (hostName.Contains('*') || hostName.Contains('?'))
            {
                continue;
            }

            validHostNames.Add(hostName);
        }

        return validHostNames;
    }
}
