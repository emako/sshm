using Sshm.Core.Enums;
using Sshm.History;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal sealed class PortForwardFormView : View
{
    private readonly string hostName;
    private readonly string configFile;
    private readonly HistoryManager? historyManager;
    private PortForwardType forwardType = PortForwardType.Local;
    private int focusedIndex;
    private readonly Label errorLabel;
    private readonly Label typeLabel;
    private readonly TextField typeField;
    private readonly TextField localPortField;
    private readonly TextField remoteHostField;
    private readonly TextField remotePortField;
    private readonly TextField bindAddressField;

    internal event Action? Cancelled;
    internal event Action<IReadOnlyList<string>>? Submitted;

    internal PortForwardFormView(string hostName, string configFile, HistoryManager? historyManager)
    {
        this.hostName = hostName;
        this.configFile = configFile;
        this.historyManager = historyManager;

        Label title = new()
        {
            Text = "Port Forwarding Setup",
            X = Pos.Center(),
            Y = 0,
        };

        Label hostInfo = new()
        {
            Text = "Host: " + hostName,
            X = 0,
            Y = Pos.Bottom(title),
        };

        typeLabel = new Label
        {
            Text = "Forward Type:",
            X = 0,
            Y = Pos.Bottom(hostInfo),
        };

        typeField = TuiViewHelper.CreateTextField(forwardType.ToDisplayString());
        typeField.X = 0;
        typeField.Y = Pos.Bottom(typeLabel);
        typeField.Width = 40;
        typeField.ReadOnly = true;

        localPortField = TuiViewHelper.CreateTextField(string.Empty);
        remoteHostField = TuiViewHelper.CreateTextField("localhost");
        remotePortField = TuiViewHelper.CreateTextField(string.Empty);
        bindAddressField = TuiViewHelper.CreateTextField(string.Empty);

        errorLabel = new Label
        {
            Text = string.Empty,
            X = 0,
            Y = 22,
            Width = Dim.Fill(),
        };

        Label helpLabel = new()
        {
            Text = "<-/-> change type | Tab/down: next field | Shift+Tab/up: previous field | Enter: connect | Esc: cancel",
            X = 0,
            Y = Pos.Bottom(errorLabel),
            Width = Dim.Fill(),
        };

        Add(title, hostInfo, typeLabel, typeField, errorLabel, helpLabel);
        LoadPreviousConfig();
        LayoutFields();
        FocusField(0);
        Width = Dim.Fill();
        Height = Dim.Fill();

        KeyDownNotHandled += HandleKey;
    }

    private void HandleKey(object? sender, Key key)
    {
        if (key == Key.Esc || TuiKeys.IsCtrlChar(key, 'c')) { key.Handled = true; Cancelled?.Invoke(); return; }
        if (key == Key.CursorLeft) { key.Handled = true; ChangeType(-1); return; }
        if (key == Key.CursorRight) { key.Handled = true; ChangeType(1); return; }
        if (key == Key.Tab) { key.Handled = true; MoveFocus(1); return; }
        if (TuiKeys.IsShiftTab(key)) { key.Handled = true; MoveFocus(-1); return; }
        if (key == Key.CursorDown) { key.Handled = true; MoveFocus(1); return; }
        if (key == Key.CursorUp) { key.Handled = true; MoveFocus(-1); return; }
        if (key == Key.Enter) { key.Handled = true; HandleEnter(); }
    }

    private int[] GetValidFields()
    {
        return forwardType == PortForwardType.Dynamic
            ? [0, 1, 4]
            : [0, 1, 2, 3, 4];
    }

    private void LayoutFields()
    {
        foreach (View subView in SubViews.ToArray())
        {
            if (subView is Label label
                && (label.Text.StartsWith("Local", StringComparison.Ordinal)
                    || label.Text.StartsWith("Remote", StringComparison.Ordinal)
                    || label.Text.StartsWith("SOCKS", StringComparison.Ordinal)
                    || label.Text.StartsWith("Bind", StringComparison.Ordinal)
                    || label.Text.Contains("forwarding", StringComparison.OrdinalIgnoreCase)))
            {
                Remove(subView);
            }

            if (subView is TextField field
                && field != typeField
                && field != localPortField
                && field != remoteHostField
                && field != remotePortField
                && field != bindAddressField)
            {
                Remove(subView);
            }
        }

        int y = 6;
        Label hint = new()
        {
            Text = forwardType switch
            {
                PortForwardType.Remote => "Remote forwarding: ssh -R [bind_address:]remote_port:local_host:local_port",
                PortForwardType.Dynamic => "Dynamic forwarding (SOCKS proxy): ssh -D [bind_address:]port",
                _ => "Local forwarding: ssh -L [bind_address:]local_port:remote_host:remote_port",
            },
            X = 0,
            Y = y,
        };
        Add(hint);
        y += 2;

        if (forwardType == PortForwardType.Local)
        {
            AddField("Local Port:", localPortField, ref y, 1);
            AddField("Remote Host:", remoteHostField, ref y, 2);
            AddField("Remote Port:", remotePortField, ref y, 3);
        }
        else if (forwardType == PortForwardType.Remote)
        {
            AddField("Remote Port:", localPortField, ref y, 1);
            AddField("Local Host:", remoteHostField, ref y, 2);
            AddField("Local Port:", remotePortField, ref y, 3);
        }
        else
        {
            AddField("SOCKS Port:", localPortField, ref y, 1);
        }

        AddField("Bind Address (optional):", bindAddressField, ref y, 4);
        typeField.Text = forwardType.ToDisplayString();
    }

    private void AddField(string labelText, TextField field, ref int y, int fieldIndex)
    {
        Label label = new()
        {
            Text = labelText,
            X = 0,
            Y = y,
        };
        field.X = 0;
        field.Y = y + 1;
        field.Width = Dim.Fill();
        Add(label, field);
        y += 3;
    }

    private void FocusField(int fieldIndex)
    {
        focusedIndex = fieldIndex;
        TextField field = fieldIndex switch
        {
            0 => typeField,
            1 => localPortField,
            2 => remoteHostField,
            3 => remotePortField,
            4 => bindAddressField,
            _ => typeField,
        };
        field.SetFocus();
        LayoutFields();
    }

    private void MoveFocus(int delta)
    {
        int[] valid = GetValidFields();
        int pos = Array.IndexOf(valid, focusedIndex);
        pos += delta;
        if (pos < 0)
        {
            pos = 0;
        }

        if (pos >= valid.Length)
        {
            pos = valid.Length - 1;
        }

        FocusField(valid[pos]);
    }

    private void HandleEnter()
    {
        int[] valid = GetValidFields();
        int pos = Array.IndexOf(valid, focusedIndex);
        if (pos == valid.Length - 1)
        {
            SubmitForm();
            return;
        }

        MoveFocus(1);
    }

    private void ChangeType(int delta)
    {
        if (focusedIndex != 0)
        {
            return;
        }

        int next = (int)forwardType + delta;
        if (next < 0)
        {
            next = (int)PortForwardType.Dynamic;
        }

        if (next > (int)PortForwardType.Dynamic)
        {
            next = (int)PortForwardType.Local;
        }

        forwardType = (PortForwardType)next;
        LayoutFields();
        int[] valid = GetValidFields();
        if (!valid.Contains(focusedIndex))
        {
            FocusField(valid[0]);
        }
    }

    private void LoadPreviousConfig()
    {
        PortForwardConfig? config = historyManager?.GetPortForwardingConfig(hostName);
        if (config == null)
        {
            return;
        }

        forwardType = PortForwardTypeExtensions.FromHistoryType(config.Type);
        localPortField.Text = config.LocalPort;
        remoteHostField.Text = string.IsNullOrEmpty(config.RemoteHost) ? "localhost" : config.RemoteHost;
        remotePortField.Text = config.RemotePort;
        bindAddressField.Text = config.BindAddress;
    }

    internal void SubmitForm()
    {
        try
        {
            string localPort = localPortField.Text.ToString()?.Trim() ?? string.Empty;
            string remoteHost = remoteHostField.Text.ToString()?.Trim() ?? string.Empty;
            string remotePort = remotePortField.Text.ToString()?.Trim() ?? string.Empty;
            string bindAddress = bindAddressField.Text.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(localPort))
            {
                throw new InvalidOperationException("port is required");
            }

            if (!int.TryParse(localPort, out _))
            {
                throw new InvalidOperationException("invalid port number");
            }

            List<string> sshArgs = [];
            if (!string.IsNullOrEmpty(configFile))
            {
                sshArgs.Add("-F");
                sshArgs.Add(configFile);
            }

            string forwardTypeStr = forwardType.ToHistoryType();
            switch (forwardType)
            {
                case PortForwardType.Local:
                    if (string.IsNullOrEmpty(remoteHost))
                    {
                        remoteHost = "localhost";
                    }

                    if (string.IsNullOrEmpty(remotePort))
                    {
                        throw new InvalidOperationException("remote port is required for local forwarding");
                    }

                    if (!int.TryParse(remotePort, out _))
                    {
                        throw new InvalidOperationException("invalid remote port number");
                    }

                    sshArgs.Add("-L");
                    sshArgs.Add(string.IsNullOrEmpty(bindAddress)
                        ? $"{localPort}:{remoteHost}:{remotePort}"
                        : $"{bindAddress}:{localPort}:{remoteHost}:{remotePort}");
                    break;

                case PortForwardType.Remote:
                    if (string.IsNullOrEmpty(remoteHost))
                    {
                        remoteHost = "localhost";
                    }

                    if (string.IsNullOrEmpty(remotePort))
                    {
                        throw new InvalidOperationException("local port is required for remote forwarding");
                    }

                    if (!int.TryParse(remotePort, out _))
                    {
                        throw new InvalidOperationException("invalid local port number");
                    }

                    sshArgs.Add("-R");
                    sshArgs.Add(string.IsNullOrEmpty(bindAddress)
                        ? $"{localPort}:{remoteHost}:{remotePort}"
                        : $"{bindAddress}:{localPort}:{remoteHost}:{remotePort}");
                    break;

                case PortForwardType.Dynamic:
                    sshArgs.Add("-D");
                    sshArgs.Add(string.IsNullOrEmpty(bindAddress) ? localPort : $"{bindAddress}:{localPort}");
                    break;
            }

            historyManager?.RecordPortForwarding(hostName, forwardTypeStr, localPort, remoteHost, remotePort, bindAddress);
            sshArgs.Add(hostName);
            errorLabel.Text = string.Empty;
            Submitted?.Invoke(sshArgs);
        }
        catch (Exception ex)
        {
            errorLabel.Text = "Error: " + ex.Message;
        }
    }
}
