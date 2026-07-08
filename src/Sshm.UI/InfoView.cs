using Sshm.Core.Models;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal sealed class InfoView : View
{
    internal event Action? Cancelled;
    internal event Action<string>? EditRequested;

    private readonly string hostName;

    internal InfoView(SshHost host, string hostName)
    {
        this.hostName = hostName;

        FrameView frame = new()
        {
            Title = $"SSH Host Information: {host.Name}",
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = Dim.Percent(80),
            Height = Dim.Percent(70),
        };

        string content = $"""
            Host Name:      {host.Name}
            Config File:    {TuiUtils.FormatConfigFile(host.SourceFile)}
            Hostname/IP:    {host.Hostname}
            User:           {TuiUtils.FormatOptionalValue(host.User)}
            Port:           {TuiUtils.FormatOptionalValue(host.Port)}
            Identity File:  {TuiUtils.FormatOptionalValue(host.Identity)}
            ProxyJump:      {TuiUtils.FormatOptionalValue(host.ProxyJump)}
            ProxyCommand:   {TuiUtils.FormatOptionalValue(host.ProxyCommand)}
            SSH Options:    {TuiUtils.FormatOptionalValue(host.Options)}
            Tags:           {TuiUtils.FormatTags(host.Tags)}

            Actions:
              e/Enter - Switch to edit mode
              q/Esc   - Return to host list
            """;

        Label infoLabel = new()
        {
            Text = content,
            X = 1,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };

        frame.Add(infoLabel);
        Add(frame);
        Width = Dim.Fill();
        Height = Dim.Fill();

        KeyDownNotHandled += HandleKey;
    }

    private void HandleKey(object? sender, Key key)
    {
        if (key == Key.Esc || TuiKeys.IsChar(key, 'q') || TuiKeys.IsCtrlChar(key, 'c'))
        {
            key.Handled = true;
            Cancelled?.Invoke();
            return;
        }

        if (key == Key.Enter || TuiKeys.IsChar(key, 'e'))
        {
            key.Handled = true;
            EditRequested?.Invoke(hostName);
        }
    }
}
