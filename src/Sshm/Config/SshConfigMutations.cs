using Sshm.Core.Models;

namespace Sshm.Config;

public static class SshConfigMutations
{
    public static void AddSSHHost(SshHost host)
    {
        string configPath = PlatformPaths.GetDefaultSSHConfigPath();
        AddSSHHostToFile(host, configPath);
    }

    public static void AddSSHHostToFile(SshHost host, string configPath)
    {
        lock (ConfigLock.SyncRoot)
        {
            if (File.Exists(configPath))
            {
                SshConfigInternals.BackupConfig(configPath);
            }
            else
            {
                string? directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            if (SshConfigQuery.HostExistsInFile(host.Name, configPath))
            {
                throw new InvalidOperationException($"host '{host.Name}' already exists");
            }

            using StreamWriter writer = new(configPath, append: true);
            writer.Write('\n');
            writer.Write(SshConfigInternals.BuildHostBlockText(host));
        }
    }

    public static void UpdateSSHHostInFile(string oldName, SshHost newHost, string configPath)
    {
        lock (ConfigLock.SyncRoot)
        {
            SshConfigInternals.BackupConfig(configPath);

            (bool isMultiHost, List<string> hostNames) =
                SshConfigQuery.IsPartOfMultiHostDeclaration(oldName, configPath);

            string[] lines = SshConfigInternals.ReadConfigLines(configPath);
            List<string> newLines = [];
            int index = 0;
            bool hostFound = false;

            while (index < lines.Length)
            {
                string line = lines[index].Trim();

                if (line.StartsWith("# Tags:", StringComparison.Ordinal) && index + 1 < lines.Length)
                {
                    string nextLine = lines[index + 1].Trim();
                    if (nextLine.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> foundHostNames = SshConfigInternals.ParseRawHostNamesFromLine(nextLine);
                        int targetHostIndex = foundHostNames.IndexOf(oldName);
                        if (targetHostIndex >= 0)
                        {
                            hostFound = true;
                            HandleTaggedHostUpdate(
                                lines,
                                ref index,
                                newLines,
                                oldName,
                                newHost,
                                isMultiHost,
                                hostNames,
                                foundHostNames,
                                targetHostIndex,
                                hasTagsLine: true);
                            continue;
                        }
                    }
                }

                if (line.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                {
                    List<string> foundHostNames = SshConfigInternals.ParseRawHostNamesFromLine(line);
                    int targetHostIndex = foundHostNames.IndexOf(oldName);
                    if (targetHostIndex >= 0)
                    {
                        hostFound = true;
                        HandleTaggedHostUpdate(
                            lines,
                            ref index,
                            newLines,
                            oldName,
                            newHost,
                            isMultiHost,
                            hostNames,
                            foundHostNames,
                            targetHostIndex,
                            hasTagsLine: false);
                        continue;
                    }
                }

                newLines.Add(lines[index]);
                index++;
            }

            if (!hostFound)
            {
                throw new InvalidOperationException($"host '{oldName}' not found");
            }

            SshConfigInternals.WriteConfigLines(configPath, newLines);
        }
    }

    public static void UpdateMultiHostBlock(
        IReadOnlyList<string> originalHosts,
        IReadOnlyList<string> newHosts,
        SshHost commonProperties,
        string configPath)
    {
        lock (ConfigLock.SyncRoot)
        {
            SshConfigInternals.BackupConfig(configPath);

            string[] lines = SshConfigInternals.ReadConfigLines(configPath);
            List<string> newLines = [];
            int index = 0;
            bool blockFound = false;

            while (index < lines.Length)
            {
                string line = lines[index].Trim();

                if (line.StartsWith("# Tags:", StringComparison.Ordinal) && index + 1 < lines.Length)
                {
                    string nextLine = lines[index + 1].Trim();
                    if (nextLine.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> foundHostNames = SshConfigInternals.ParseRawHostNamesFromLine(nextLine);
                        if (ContainsAnyOriginalHost(originalHosts, foundHostNames))
                        {
                            blockFound = true;
                            ReplaceMultiHostBlock(lines, ref index, newLines, newHosts, commonProperties, skipTaggedHeader: true);
                            continue;
                        }
                    }
                }

                if (line.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                {
                    List<string> foundHostNames = SshConfigInternals.ParseRawHostNamesFromLine(line);
                    if (ContainsAnyOriginalHost(originalHosts, foundHostNames))
                    {
                        blockFound = true;
                        ReplaceMultiHostBlock(lines, ref index, newLines, newHosts, commonProperties, skipTaggedHeader: false);
                        continue;
                    }
                }

                newLines.Add(lines[index]);
                index++;
            }

            if (!blockFound)
            {
                throw new InvalidOperationException("multi-host block not found");
            }

            SshConfigInternals.WriteConfigLines(configPath, newLines);
        }
    }

    public static void DeleteSSHHostWithLine(SshHost host)
    {
        DeleteSSHHostFromFileWithLine(host.Name, host.SourceFile, host.LineNumber);
    }

    public static void DeleteSSHHostFromFileWithLine(string hostName, string configPath, int targetLineNumber)
    {
        lock (ConfigLock.SyncRoot)
        {
            SshConfigInternals.BackupConfig(configPath);

            (bool isMultiHost, List<string> hostNames) =
                SshConfigQuery.IsPartOfMultiHostDeclaration(hostName, configPath);

            string[] lines = SshConfigInternals.ReadConfigLines(configPath);
            List<string> newLines = [];
            int index = 0;
            bool hostFound = false;

            while (index < lines.Length)
            {
                int currentLineNumber = index + 1;
                string line = lines[index].Trim();

                if (line.StartsWith("# Tags:", StringComparison.Ordinal) && index + 1 < lines.Length)
                {
                    string nextLine = lines[index + 1].Trim();
                    int nextLineNumber = index + 2;

                    if (nextLine.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> foundHostNames = SshConfigInternals.ParseRawHostNamesFromLine(nextLine);
                        int targetHostIndex = foundHostNames.IndexOf(hostName);
                        if (targetHostIndex >= 0 &&
                            (targetLineNumber == 0 || nextLineNumber == targetLineNumber))
                        {
                            hostFound = true;
                            HandleHostDelete(
                                lines,
                                ref index,
                                newLines,
                                hostName,
                                isMultiHost,
                                hostNames,
                                foundHostNames,
                                targetHostIndex,
                                hasTagsLine: true);
                            break;
                        }
                    }
                }

                if (line.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                {
                    List<string> foundHostNames = SshConfigInternals.ParseRawHostNamesFromLine(line);
                    int targetHostIndex = foundHostNames.IndexOf(hostName);
                    if (targetHostIndex >= 0 &&
                        (targetLineNumber == 0 || currentLineNumber == targetLineNumber))
                    {
                        hostFound = true;
                        HandleHostDelete(
                            lines,
                            ref index,
                            newLines,
                            hostName,
                            isMultiHost,
                            hostNames,
                            foundHostNames,
                            targetHostIndex,
                            hasTagsLine: false);
                        break;
                    }
                }

                newLines.Add(lines[index]);
                index++;
            }

            if (!hostFound)
            {
                throw new InvalidOperationException($"host '{hostName}' not found");
            }

            SshConfigInternals.WriteConfigLines(configPath, newLines);
        }
    }

    public static void MoveHostToFile(string hostName, string targetConfigFile)
    {
        SshHost host = SshConfigQuery.FindHostInAllConfigs(hostName);

        if (string.Equals(host.SourceFile, targetConfigFile, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"host '{hostName}' is already in the target config file '{targetConfigFile}'");
        }

        try
        {
            AddSSHHostToFile(host, targetConfigFile);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"failed to add host to target file: {ex.Message}", ex);
        }

        try
        {
            DeleteSSHHostFromFileWithLine(hostName, host.SourceFile, host.LineNumber);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"failed to remove host from source file: {ex.Message}", ex);
        }
    }

    private static void HandleTaggedHostUpdate(
        string[] lines,
        ref int index,
        List<string> newLines,
        string oldName,
        SshHost newHost,
        bool isMultiHost,
        List<string> declarationHostNames,
        List<string> foundHostNames,
        int targetHostIndex,
        bool hasTagsLine)
    {
        if (isMultiHost && declarationHostNames.Count > 1)
        {
            List<string> remainingHosts = [];
            for (int hostIndex = 0; hostIndex < foundHostNames.Count; hostIndex++)
            {
                if (hostIndex != targetHostIndex)
                {
                    remainingHosts.Add(foundHostNames[hostIndex]);
                }
            }

            if (hasTagsLine)
            {
                newLines.Add(lines[index]);
            }

            if (remainingHosts.Count > 0)
            {
                newLines.Add("Host " + string.Join(' ', remainingHosts));
                index += hasTagsLine ? 2 : 1;
                while (index < lines.Length && !SshConfigInternals.IsHostBlockEnd(lines[index]))
                {
                    newLines.Add(lines[index]);
                    index++;
                }
            }
            else
            {
                index += hasTagsLine ? 2 : 1;
                SshConfigInternals.SkipHostBlockBody(lines, ref index);
            }

            InsertNewHostBlock(newLines, newHost, leadingBlankLine: true, trailingBlankLine: true);
            return;
        }

        if (hasTagsLine)
        {
            index += 2;
        }
        else
        {
            index++;
        }

        SshConfigInternals.SkipHostBlockBody(lines, ref index);
        SshConfigInternals.SkipTrailingEmptyLines(lines, ref index);

        InsertNewHostBlock(
            newLines,
            newHost,
            leadingBlankLine: newLines.Count > 0 && newLines[^1].Trim().Length > 0,
            trailingBlankLine: true);
    }

    private static void InsertNewHostBlock(
        List<string> newLines,
        SshHost newHost,
        bool leadingBlankLine,
        bool trailingBlankLine)
    {
        if (leadingBlankLine)
        {
            newLines.Add(string.Empty);
        }

        SshConfigInternals.AppendHostBlock(newLines, newHost);

        if (trailingBlankLine)
        {
            newLines.Add(string.Empty);
        }
    }

    private static void HandleHostDelete(
        string[] lines,
        ref int index,
        List<string> newLines,
        string hostName,
        bool isMultiHost,
        List<string> declarationHostNames,
        List<string> foundHostNames,
        int targetHostIndex,
        bool hasTagsLine)
    {
        if (isMultiHost && declarationHostNames.Count > 1)
        {
            List<string> remainingHosts = [];
            for (int hostIndex = 0; hostIndex < foundHostNames.Count; hostIndex++)
            {
                if (hostIndex != targetHostIndex)
                {
                    remainingHosts.Add(foundHostNames[hostIndex]);
                }
            }

            if (hasTagsLine)
            {
                newLines.Add(lines[index]);
            }

            if (remainingHosts.Count > 0)
            {
                newLines.Add("Host " + string.Join(' ', remainingHosts));
                index += hasTagsLine ? 2 : 1;
                while (index < lines.Length && !SshConfigInternals.IsHostBlockEnd(lines[index]))
                {
                    newLines.Add(lines[index]);
                    index++;
                }
            }
            else
            {
                index += hasTagsLine ? 2 : 1;
                SshConfigInternals.SkipHostBlockBody(lines, ref index);
            }

            SshConfigInternals.SkipTrailingEmptyLines(lines, ref index);
            AppendRemainingLines(lines, ref index, newLines);
            return;
        }

        index += hasTagsLine ? 2 : 1;
        SshConfigInternals.SkipHostBlockBody(lines, ref index);
        SshConfigInternals.SkipTrailingEmptyLines(lines, ref index);
        AppendRemainingLines(lines, ref index, newLines);
    }

    private static void AppendRemainingLines(string[] lines, ref int index, List<string> newLines)
    {
        while (index < lines.Length)
        {
            newLines.Add(lines[index]);
            index++;
        }
    }

    private static bool ContainsAnyOriginalHost(IReadOnlyList<string> originalHosts, IReadOnlyList<string> foundHostNames)
    {
        foreach (string originalHost in originalHosts)
        {
            foreach (string foundHost in foundHostNames)
            {
                if (foundHost == originalHost)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ReplaceMultiHostBlock(
        string[] lines,
        ref int index,
        List<string> newLines,
        IReadOnlyList<string> newHosts,
        SshHost commonProperties,
        bool skipTaggedHeader)
    {
        index += skipTaggedHeader ? 2 : 1;
        SshConfigInternals.SkipHostBlockBody(lines, ref index);
        SshConfigInternals.SkipTrailingEmptyLines(lines, ref index);

        if (newLines.Count > 0 && newLines[^1].Trim().Length > 0)
        {
            newLines.Add(string.Empty);
        }

        if (commonProperties.Tags.Count > 0)
        {
            newLines.Add("# Tags: " + string.Join(", ", commonProperties.Tags));
        }

        newLines.Add("Host " + string.Join(' ', newHosts));
        SshConfigInternals.AppendHostProperties(newLines, commonProperties);
        newLines.Add(string.Empty);
    }
}
