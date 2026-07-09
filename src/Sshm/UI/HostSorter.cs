using Sshm.Core.Enums;
using Sshm.Core.Models;
using Sshm.History;

namespace Sshm.UI;

internal static class HostSorter
{
    internal static List<SshHost> SortHosts(
        IReadOnlyList<SshHost> hosts,
        SortMode sortMode,
        HistoryManager? historyManager)
    {
        if (historyManager == null || sortMode == SortMode.ByName)
        {
            return SortHostsByName(hosts);
        }

        return historyManager.SortHostsByLastUsed([.. hosts]);
    }

    private static List<SshHost> SortHostsByName(IReadOnlyList<SshHost> hosts)
    {
        List<SshHost> sorted = [.. hosts];
        sorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return sorted;
    }
}
