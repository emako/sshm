namespace Sshm.Config;

public static class SshOptions
{
    public static string ParseSSHOptionsFromCommand(string options)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            return string.Empty;
        }

        options = options.Trim();

        if (!options.Contains("-o"))
        {
            string[] lines = options.Split('\n');
            List<string> result = [];
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    result.Add(string.Join(' ', parts));
                }
            }

            return string.Join('\n', result);
        }

        List<string> commandResult = [];
        string[] partsByOption = options.Split("-o");
        foreach (string part in partsByOption)
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            string option = trimmed.Replace("=", " ");
            commandResult.Add(option);
        }

        return string.Join('\n', commandResult);
    }

    public static string FormatSSHOptionsForCommand(string options)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            return string.Empty;
        }

        string trimmed = options.Trim();
        if (trimmed.StartsWith("-o ", StringComparison.Ordinal))
        {
            return trimmed;
        }

        List<string> result = [];
        string[] lines = options.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int spaceIndex = line.IndexOf(' ');
            if (spaceIndex >= 0)
            {
                string key = line[..spaceIndex];
                string value = line[(spaceIndex + 1)..];
                result.Add($"-o {key}={value}");
            }
            else
            {
                result.Add($"-o {line}");
            }
        }

        return string.Join(' ', result);
    }
}
