using System.Data;
using Sshm.Connectivity;
using Sshm.Core.Enums;
using Sshm.Core.Models;
using Sshm.History;

namespace Sshm.UI;

internal static class HostTableBuilder
{
    private const string SortMarker = " \u2193";

    internal static DataTable BuildTable(
        IReadOnlyList<SshHost> hosts,
        SortMode sortMode,
        HistoryManager? historyManager,
        PingManager? pingManager)
    {
        DataTable table = new();
        string nameTitle = sortMode == SortMode.ByName ? "Name" + SortMarker : "Name";
        string lastLoginTitle = sortMode == SortMode.ByLastUsed ? "Last Login" + SortMarker : "Last Login";

        table.Columns.Add(nameTitle, typeof(string));
        table.Columns.Add("Hostname", typeof(string));
        table.Columns.Add("Tags", typeof(string));
        table.Columns.Add(lastLoginTitle, typeof(string));

        foreach (SshHost host in hosts)
        {
            string statusIndicator = TuiUtils.GetPingStatusIndicator(pingManager, host.Name);
            string nameCell = statusIndicator + " " + host.Name;
            string tagsCell = TuiUtils.FormatTagsForTable(host.Tags);
            string lastLoginCell = TuiUtils.FormatLastLogin(historyManager, host.Name);
            table.Rows.Add(nameCell, host.Hostname, tagsCell, lastLoginCell);
        }

        return table;
    }
}
