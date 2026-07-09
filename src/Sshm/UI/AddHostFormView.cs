using Sshm.Config;
using Sshm.Core.Models;
using Sshm.Validation;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal sealed class AddHostFormView : View
{
    private const int TabGeneral = 0;
    private const int TabAdvanced = 1;

    private readonly string configFile;
    private int currentTab = TabGeneral;
    private int focusedIndex;
    private readonly Label errorLabel;
    private readonly Label tabLabel;
    private readonly TextField[] fields;

    internal event Action? Cancelled;
    internal event Action? Submitted;

    internal AddHostFormView(string configFile, string? initialName)
    {
        this.configFile = configFile;

        string defaultUser = Environment.UserName;
        string homeDir = PlatformPaths.GetHomeDir();
        string defaultIdentity = Path.Combine(homeDir, ".ssh", "id_rsa");
        string[] keyTypes = ["id_ed25519", "id_ecdsa", "id_rsa"];
        foreach (string keyType in keyTypes)
        {
            string keyPath = Path.Combine(homeDir, ".ssh", keyType);
            if (File.Exists(keyPath))
            {
                defaultIdentity = keyPath;
                break;
            }
        }

        Label title = new()
        {
            Text = "Add SSH Host Configuration",
            X = 0,
            Y = 0,
        };

        tabLabel = new Label
        {
            Text = RenderTabs(),
            X = 0,
            Y = Pos.Bottom(title),
        };

        fields = new TextField[11];
        fields[0] = CreateField(initialName ?? string.Empty);
        fields[1] = CreateField(string.Empty);
        fields[2] = CreateField(defaultUser);
        fields[3] = CreateField("22");
        fields[4] = CreateField(string.Empty);
        fields[5] = CreateField(string.Empty);
        fields[6] = CreateField(string.Empty);
        fields[7] = CreateField(string.Empty);
        fields[8] = CreateField(string.Empty);
        fields[9] = CreateField(string.Empty);
        fields[10] = CreateField(string.Empty);

        errorLabel = new Label
        {
            Text = string.Empty,
            X = 0,
            Y = 18,
            Width = Dim.Fill(),
        };

        Label helpLabel = new()
        {
            Text = "Tab/Shift+Tab: navigate | Ctrl+J/K: switch tabs | Enter on last field: submit | Ctrl+S: save | Ctrl+C/Esc: cancel | * Required",
            X = 0,
            Y = Pos.Bottom(errorLabel),
            Width = Dim.Fill(),
        };

        Add(title, tabLabel, errorLabel, helpLabel);
        LayoutFields();
        FocusField(0);
        Width = Dim.Fill();
        Height = Dim.Fill();

        KeyDownNotHandled += HandleKey;
    }

    private void HandleKey(object? sender, Key key)
    {
        if (key == Key.Esc || TuiKeys.IsCtrlChar(key, 'c'))
        {
            key.Handled = true;
            Cancelled?.Invoke();
            return;
        }

        if (TuiKeys.IsCtrlChar(key, 's'))
        {
            key.Handled = true;
            SubmitForm();
            return;
        }

        if (TuiKeys.IsCtrlChar(key, 'j'))
        {
            key.Handled = true;
            SwitchTab(1);
            return;
        }

        if (TuiKeys.IsCtrlChar(key, 'k'))
        {
            key.Handled = true;
            SwitchTab(-1);
            return;
        }

        if (key == Key.Tab)
        {
            key.Handled = true;
            MoveFocus(1);
            return;
        }

        if (TuiKeys.IsShiftTab(key))
        {
            key.Handled = true;
            MoveFocus(-1);
            return;
        }

        if (key == Key.Enter)
        {
            key.Handled = true;
            HandleEnter();
            return;
        }

        if (key == Key.CursorDown)
        {
            key.Handled = true;
            MoveFocus(1);
            return;
        }

        if (key == Key.CursorUp)
        {
            key.Handled = true;
            MoveFocus(-1);
        }
    }

    private static TextField CreateField(string text)
    {
        return TuiViewHelper.CreateTextField(text);
    }

    private string RenderTabs()
    {
        if (currentTab == TabGeneral)
        {
            return "[ General ]    Advanced";
        }

        return "  General    [ Advanced ]";
    }

    private int[] GetCurrentTabFields()
    {
        return currentTab == TabGeneral
            ? [0, 1, 2, 3, 4, 5, 6, 7]
            : [8, 9, 10];
    }

    private string[] GetCurrentTabLabels()
    {
        return currentTab == TabGeneral
            ?
            [
                "Host Name *",
                "Hostname/IP *",
                "User",
                "Port",
                "Identity File",
                "ProxyJump",
                "ProxyCommand",
                "Tags (comma-separated)",
            ]
            :
            [
                "SSH Options",
                "Remote Command",
                "Request TTY",
            ];
    }

    private void LayoutFields()
    {
        foreach (View subView in SubViews.ToArray())
        {
            if (subView is Label label && label != tabLabel && label != errorLabel)
            {
                if (label.Text.StartsWith("Host Name", StringComparison.Ordinal)
                    || label.Text.StartsWith("Hostname", StringComparison.Ordinal)
                    || label.Text.StartsWith("User", StringComparison.Ordinal)
                    || label.Text.StartsWith("Port", StringComparison.Ordinal)
                    || label.Text.StartsWith("Identity", StringComparison.Ordinal)
                    || label.Text.StartsWith("Proxy", StringComparison.Ordinal)
                    || label.Text.StartsWith("Tags", StringComparison.Ordinal)
                    || label.Text.StartsWith("SSH", StringComparison.Ordinal)
                    || label.Text.StartsWith("Remote", StringComparison.Ordinal)
                    || label.Text.StartsWith("Request", StringComparison.Ordinal))
                {
                    Remove(subView);
                }
            }

            if (subView is TextField)
            {
                Remove(subView);
            }
        }

        int y = 4;
        int[] indices = GetCurrentTabFields();
        string[] labels = GetCurrentTabLabels();
        for (int i = 0; i < indices.Length; i++)
        {
            Label fieldLabel = new()
            {
                Text = labels[i],
                X = 0,
                Y = y,
            };
            TextField field = fields[indices[i]];
            field.X = 0;
            field.Y = y + 1;
            field.Width = Dim.Fill();
            Add(fieldLabel, field);
            y += 3;
        }

        tabLabel.Text = RenderTabs();
    }

    private void FocusField(int fieldIndex)
    {
        focusedIndex = fieldIndex;
        foreach (TextField field in fields)
        {
            field.ReadOnly = false;
        }

        fields[fieldIndex].SetFocus();
        LayoutFields();
    }

    private void MoveFocus(int delta)
    {
        int[] tabFields = GetCurrentTabFields();
        int pos = Array.IndexOf(tabFields, focusedIndex);
        if (pos < 0)
        {
            pos = 0;
        }

        pos += delta;
        if (pos >= tabFields.Length)
        {
            if (currentTab == TabGeneral)
            {
                currentTab = TabAdvanced;
                pos = 0;
            }
            else
            {
                pos = tabFields.Length - 1;
            }
        }
        else if (pos < 0)
        {
            if (currentTab == TabAdvanced)
            {
                currentTab = TabGeneral;
                tabFields = GetCurrentTabFields();
                pos = tabFields.Length - 1;
            }
            else
            {
                pos = 0;
            }
        }

        LayoutFields();
        FocusField(GetCurrentTabFields()[pos]);
    }

    private void SwitchTab(int direction)
    {
        currentTab = direction > 0
            ? (currentTab + 1) % 2
            : (currentTab - 1 + 2) % 2;
        int[] tabFields = GetCurrentTabFields();
        FocusField(tabFields[0]);
    }

    private void HandleEnter()
    {
        int[] tabFields = GetCurrentTabFields();
        int pos = Array.IndexOf(tabFields, focusedIndex);
        if (currentTab == TabAdvanced && pos == tabFields.Length - 1)
        {
            SubmitForm();
            return;
        }

        MoveFocus(1);
    }

    internal void SubmitForm()
    {
        try
        {
            string name = fields[0].Text.ToString()?.Trim() ?? string.Empty;
            string hostname = fields[1].Text.ToString()?.Trim() ?? string.Empty;
            string user = fields[2].Text.ToString()?.Trim() ?? string.Empty;
            string port = fields[3].Text.ToString()?.Trim() ?? string.Empty;
            string identity = fields[4].Text.ToString()?.Trim() ?? string.Empty;
            string proxyJump = fields[5].Text.ToString()?.Trim() ?? string.Empty;
            string proxyCommand = fields[6].Text.ToString()?.Trim() ?? string.Empty;
            string tagsStr = fields[7].Text.ToString()?.Trim() ?? string.Empty;
            string options = fields[8].Text.ToString()?.Trim() ?? string.Empty;
            string remoteCommand = fields[9].Text.ToString()?.Trim() ?? string.Empty;
            string requestTty = fields[10].Text.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(user))
            {
                user = Environment.UserName;
            }

            if (string.IsNullOrEmpty(port))
            {
                port = "22";
            }

            SshHostValidator.ValidateHost(name, hostname, port, identity);

            List<string> tags = [];
            if (!string.IsNullOrEmpty(tagsStr))
            {
                foreach (string part in tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string tag = part.Trim();
                    if (!string.IsNullOrEmpty(tag))
                    {
                        tags.Add(tag);
                    }
                }
            }

            SshHost host = new()
            {
                Name = name,
                Hostname = hostname,
                User = user,
                Port = port,
                Identity = identity,
                ProxyJump = proxyJump,
                ProxyCommand = proxyCommand,
                Options = SshOptions.ParseSSHOptionsFromCommand(options),
                RemoteCommand = remoteCommand,
                RequestTty = requestTty,
                Tags = tags,
            };

            if (!string.IsNullOrEmpty(configFile))
            {
                SshConfigMutations.AddSSHHostToFile(host, configFile);
            }
            else
            {
                SshConfigMutations.AddSSHHost(host);
            }

            errorLabel.Text = string.Empty;
            Submitted?.Invoke();
        }
        catch (Exception ex)
        {
            errorLabel.Text = "Error: " + ex.Message;
        }
    }
}
