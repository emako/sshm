using Sshm.Connectivity;
using Sshm.History;

namespace Sshm.UI;

internal static class TuiUtils
{
    private const string UnknownIndicator = "\u26AB";
    private const string OnlineIndicator = "\U0001F7E2";
    private const string OfflineIndicator = "\U0001F534";
    private const string ConnectingIndicator = "\U0001F7E1";

    internal static string GetPingStatusIndicator(PingManager? pingManager, string hostName)
    {
        if (pingManager == null)
        {
            return UnknownIndicator;
        }

        return pingManager.GetStatus(hostName) switch
        {
            PingStatus.Online => OnlineIndicator,
            PingStatus.Offline => OfflineIndicator,
            PingStatus.Connecting => ConnectingIndicator,
            _ => UnknownIndicator,
        };
    }

    internal static string ExtractHostNameFromTableRow(string firstColumn)
    {
        string[] parts = firstColumn.Split((char[]?)[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return string.Join(' ', parts.Skip(1));
        }

        return firstColumn;
    }

    internal static string FormatConfigFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return "Unknown";
        }

        string normalized = filePath.Replace('\\', '/');
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $".../{parts[^2]}/{parts[^1]}";
        }

        return filePath;
    }

    internal static string FormatTags(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return "Not set";
        }

        return string.Join(", ", tags);
    }

    internal static string FormatOptionalValue(string value)
    {
        return string.IsNullOrEmpty(value) ? "Not set" : value;
    }

    internal static string FormatTagsForTable(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        List<string> formatted = [];
        foreach (string tag in tags)
        {
            formatted.Add("#" + tag);
        }

        return string.Join(' ', formatted);
    }

    internal static string FormatLastLogin(HistoryManager? historyManager, string hostName)
    {
        if (historyManager == null)
        {
            return string.Empty;
        }

        if (historyManager.TryGetLastConnectionTime(hostName, out DateTime lastConnect))
        {
            return TimeAgoFormatter.Format(lastConnect);
        }

        return string.Empty;
    }
}
