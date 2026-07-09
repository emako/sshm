namespace Sshm.Config;

internal static class GlobHelper
{
    internal static List<string> Glob(string pattern)
    {
        try
        {
            pattern = ExpandTilde(pattern);
            pattern = Path.GetFullPath(pattern.Replace('/', Path.DirectorySeparatorChar));

            if (!ContainsGlobMeta(pattern))
            {
                if (File.Exists(pattern))
                {
                    return [Path.GetFullPath(pattern)];
                }

                return [];
            }

            int metaIndex = IndexOfGlobMeta(pattern);
            int slashIndex = pattern.LastIndexOf(Path.DirectorySeparatorChar, metaIndex);
            if (slashIndex < 0)
            {
                slashIndex = pattern.LastIndexOf('/', metaIndex);
            }

            string directory = slashIndex >= 0 ? pattern[..slashIndex] : ".";
            string filePattern = slashIndex >= 0 ? pattern[(slashIndex + 1)..] : pattern;

            if (ContainsGlobMeta(directory))
            {
                return GlobWithWildcardDirectory(pattern);
            }

            if (!Directory.Exists(directory))
            {
                return [];
            }

            List<string> matches = [];
            foreach (string file in Directory.EnumerateFiles(directory, filePattern))
            {
                matches.Add(Path.GetFullPath(file));
            }

            return matches;
        }
        catch
        {
            return [];
        }
    }

    private static string ExpandTilde(string pattern)
    {
        if (!pattern.StartsWith('~'))
        {
            return pattern;
        }

        string homeDir = PlatformPaths.GetHomeDir();
        if (pattern.Length == 1)
        {
            return homeDir;
        }

        if (pattern[1] == '/' || pattern[1] == '\\')
        {
            return Path.Combine(homeDir, pattern[2..]);
        }

        return Path.Combine(homeDir, pattern[1..]);
    }

    private static bool ContainsGlobMeta(string value)
    {
        return value.Contains('*') || value.Contains('?') || value.Contains('[');
    }

    private static int IndexOfGlobMeta(string value)
    {
        int star = value.IndexOf('*');
        int question = value.IndexOf('?');
        int bracket = value.IndexOf('[');

        int index = int.MaxValue;
        if (star >= 0)
        {
            index = Math.Min(index, star);
        }

        if (question >= 0)
        {
            index = Math.Min(index, question);
        }

        if (bracket >= 0)
        {
            index = Math.Min(index, bracket);
        }

        return index == int.MaxValue ? -1 : index;
    }

    private static List<string> GlobWithWildcardDirectory(string pattern)
    {
        List<string> results = [];
        string root = Path.GetPathRoot(pattern) ?? string.Empty;
        if (string.IsNullOrEmpty(root))
        {
            return results;
        }

        string relative = pattern[root.Length..].TrimStart(Path.DirectorySeparatorChar, '/');
        string[] segments = relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        CollectMatches(root, segments, 0, results);
        return results;
    }

    private static void CollectMatches(string currentDir, string[] segments, int segmentIndex, List<string> results)
    {
        if (segmentIndex >= segments.Length)
        {
            return;
        }

        string segment = segments[segmentIndex];
        bool isLast = segmentIndex == segments.Length - 1;

        if (!ContainsGlobMeta(segment))
        {
            string nextDir = Path.Combine(currentDir, segment);
            if (isLast)
            {
                if (File.Exists(nextDir))
                {
                    results.Add(Path.GetFullPath(nextDir));
                }

                return;
            }

            if (Directory.Exists(nextDir))
            {
                CollectMatches(nextDir, segments, segmentIndex + 1, results);
            }

            return;
        }

        if (!Directory.Exists(currentDir))
        {
            return;
        }

        if (isLast)
        {
            foreach (string file in Directory.EnumerateFiles(currentDir, segment))
            {
                results.Add(Path.GetFullPath(file));
            }

            return;
        }

        foreach (string directory in Directory.EnumerateDirectories(currentDir, segment))
        {
            CollectMatches(directory, segments, segmentIndex + 1, results);
        }
    }
}
