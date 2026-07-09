using Sshm.Core.Enums;
using Sshm.Core.Models;
using Sshm.History;

namespace Sshm.UI;

internal static class HostFilter
{
    internal static List<SshHost> FilterHosts(
        string query,
        IReadOnlyList<SshHost> visibleHosts,
        SortMode sortMode,
        HistoryManager? historyManager)
    {
        string[] subqueries = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (subqueries.Length == 0)
        {
            return HostSorter.SortHosts(visibleHosts, sortMode, historyManager);
        }

        List<List<SshHost>> subfilteredHosts = [];
        foreach (string subquery in subqueries)
        {
            subfilteredHosts.Add(FilterHostsByWord(subquery, visibleHosts, sortMode, historyManager));
        }

        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        List<SshHost> result = [];
        foreach (List<SshHost> hosts in subfilteredHosts)
        {
            foreach (SshHost host in hosts)
            {
                if (!counts.TryGetValue(host.Name, out int count))
                {
                    counts[host.Name] = 1;
                }
                else
                {
                    counts[host.Name] = count + 1;
                }

                if (counts[host.Name] == subqueries.Length)
                {
                    result.Add(host);
                }
            }
        }

        return result;
    }

    private static List<SshHost> FilterHostsByWord(
        string word,
        IReadOnlyList<SshHost> visibleHosts,
        SortMode sortMode,
        HistoryManager? historyManager)
    {
        List<SshHost> filtered = [];

        if (string.IsNullOrEmpty(word))
        {
            return HostSorter.SortHosts(visibleHosts, sortMode, historyManager);
        }

        string lowerWord = word.ToLowerInvariant();
        foreach (SshHost host in visibleHosts)
        {
            if (host.Name.Contains(lowerWord, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(host);
                continue;
            }

            if (host.Hostname.Contains(lowerWord, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(host);
                continue;
            }

            if (host.User.Contains(lowerWord, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(host);
                continue;
            }

            bool tagMatched = false;
            foreach (string tag in host.Tags)
            {
                if (tag.Contains(lowerWord, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(host);
                    tagMatched = true;
                    break;
                }
            }

            if (tagMatched)
            {
                continue;
            }
        }

        return HostSorter.SortHosts(filtered, sortMode, historyManager);
    }
}
