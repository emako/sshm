using Sshm.Config;
using Sshm.Core.Models;
using Sshm.Validation;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal sealed class EditHostFormView : View
{
    private const int FocusAreaHosts = 0;
    private const int FocusAreaProperties = 1;

    private readonly string originalName;
    private readonly string actualConfigFile;
    private readonly List<string> originalHosts;
    private int focusArea = FocusAreaHosts;
    private int currentTab;
    private int focusedIndex;
    private readonly Label errorLabel;
    private readonly Label tabLabel;
    private readonly List<TextField> hostInputs;
    private readonly TextField[] propertyInputs;

    internal event Action? Cancelled;
    internal event Action? Submitted;

    internal EditHostFormView(SshHost host, string hostName, string configFile)
    {
        originalName = hostName;
        actualConfigFile = string.IsNullOrEmpty(configFile) ? host.SourceFile : configFile;

        (bool isMulti, List<string> hostNames) = SshConfigQuery.IsPartOfMultiHostDeclaration(hostName, actualConfigFile);
        if (!isMulti)
        {
            hostNames = [hostName];
        }

        originalHosts = [.. hostNames];

        Label title = new()
        {
            Text = "Edit SSH Host",
            X = 0,
            Y = 0,
        };

        Label configLabel = new()
        {
            Text = "Config file: " + TuiUtils.FormatConfigFile(host.SourceFile),
            X = 0,
            Y = Pos.Bottom(title),
        };

        hostInputs = [];
        foreach (string name in hostNames)
        {
            hostInputs.Add(TuiViewHelper.CreateTextField(name));
        }

        propertyInputs = new TextField[10];
        propertyInputs[0] = TuiViewHelper.CreateTextField(host.Hostname);
        propertyInputs[1] = TuiViewHelper.CreateTextField(host.User);
        propertyInputs[2] = TuiViewHelper.CreateTextField(host.Port);
        propertyInputs[3] = TuiViewHelper.CreateTextField(host.Identity);
        propertyInputs[4] = TuiViewHelper.CreateTextField(host.ProxyJump);
        propertyInputs[5] = TuiViewHelper.CreateTextField(host.ProxyCommand);
        propertyInputs[6] = TuiViewHelper.CreateTextField(SshOptions.FormatSSHOptionsForCommand(host.Options));
        propertyInputs[7] = TuiViewHelper.CreateTextField(string.Join(", ", host.Tags));
        propertyInputs[8] = TuiViewHelper.CreateTextField(host.RemoteCommand);
        propertyInputs[9] = TuiViewHelper.CreateTextField(host.RequestTty);

        tabLabel = new Label
        {
            Text = RenderTabs(),
            X = 0,
            Y = Pos.Bottom(configLabel),
        };

        errorLabel = new Label
        {
            Text = string.Empty,
            X = 0,
            Y = 20,
            Width = Dim.Fill(),
        };

        Label helpLabel = new()
        {
            Text = "Tab/Shift+Tab/Enter: navigate \u2022 Ctrl+J/K: switch tabs \u2022 Ctrl+A: add host \u2022 Ctrl+D: delete host \u2022 Ctrl+S: save \u2022 Ctrl+C/Esc: cancel",
            X = 0,
            Y = Pos.Bottom(errorLabel),
            Width = Dim.Fill(),
        };

        Add(title, configLabel, tabLabel, errorLabel, helpLabel);
        LayoutFields();
        FocusCurrent();
        Width = Dim.Fill();
        Height = Dim.Fill();

        KeyDownNotHandled += HandleKey;
    }

    private void HandleKey(object? sender, Key key)
    {
        if (key == Key.Esc || TuiKeys.IsCtrlChar(key, 'c')) { key.Handled = true; Cancelled?.Invoke(); return; }
        if (TuiKeys.IsCtrlChar(key, 's')) { key.Handled = true; SubmitForm(); return; }
        if (TuiKeys.IsCtrlChar(key, 'j')) { key.Handled = true; SwitchTab(1); return; }
        if (TuiKeys.IsCtrlChar(key, 'k')) { key.Handled = true; SwitchTab(-1); return; }
        if (TuiKeys.IsCtrlChar(key, 'a')) { key.Handled = true; AddHostInput(); return; }
        if (TuiKeys.IsCtrlChar(key, 'd')) { key.Handled = true; DeleteHostInput(); return; }
        if (key == Key.Tab) { key.Handled = true; MoveFocus(1); return; }
        if (TuiKeys.IsShiftTab(key)) { key.Handled = true; MoveFocus(-1); return; }
        if (key == Key.Enter) { key.Handled = true; HandleEnter(); return; }
        if (key == Key.CursorDown) { key.Handled = true; MoveFocus(1); return; }
        if (key == Key.CursorUp) { key.Handled = true; MoveFocus(-1); }
    }

    private string RenderTabs()
    {
        return currentTab == 0 ? "[ General ]    Advanced" : "  General    [ Advanced ]";
    }

    private int[] GetPropertyTabFields()
    {
        return currentTab == 0 ? [0, 1, 2, 3, 4, 5, 7] : [6, 8, 9];
    }

    private string[] GetPropertyTabLabels()
    {
        return currentTab == 0
            ?
            [
                "Hostname/IP *",
                "User",
                "Port",
                "Identity File",
                "Proxy Jump",
                "Proxy Command",
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
            if (subView is Label label
                && label.Text.StartsWith("Host Name", StringComparison.Ordinal))
            {
                Remove(subView);
            }

            if (subView is TextField)
            {
                Remove(subView);
            }
        }

        int y = 3;
        Label hostSection = new()
        {
            Text = "Host Names",
            X = 0,
            Y = y,
        };
        Add(hostSection);
        y += 2;

        for (int i = 0; i < hostInputs.Count; i++)
        {
            Label hostLabel = new()
            {
                Text = $"Host Name {i + 1} *",
                X = 0,
                Y = y,
            };
            TextField hostInput = hostInputs[i];
            hostInput.X = 0;
            hostInput.Y = y + 1;
            Add(hostLabel, hostInput);
            y += 3;
        }

        Label propertiesSection = new()
        {
            Text = "Common Properties",
            X = 0,
            Y = y,
        };
        Add(propertiesSection);
        y += 2;

        tabLabel.Y = y;
        tabLabel.Text = RenderTabs();
        y += 2;

        int[] indices = GetPropertyTabFields();
        string[] labels = GetPropertyTabLabels();
        for (int i = 0; i < indices.Length; i++)
        {
            Label fieldLabel = new()
            {
                Text = labels[i],
                X = 0,
                Y = y,
            };
            TextField field = propertyInputs[indices[i]];
            field.X = 0;
            field.Y = y + 1;
            Add(fieldLabel, field);
            y += 3;
        }
    }

    private void FocusCurrent()
    {
        if (focusArea == FocusAreaHosts)
        {
            hostInputs[focusedIndex].SetFocus();
        }
        else
        {
            propertyInputs[focusedIndex].SetFocus();
        }

        LayoutFields();
    }

    private void MoveFocus(int delta)
    {
        if (focusArea == FocusAreaHosts)
        {
            focusedIndex += delta;
            if (focusedIndex >= hostInputs.Count)
            {
                focusArea = FocusAreaProperties;
                int[] props = GetPropertyTabFields();
                focusedIndex = props[0];
            }
            else if (focusedIndex < 0)
            {
                focusedIndex = 0;
            }
        }
        else
        {
            int[] tabFields = GetPropertyTabFields();
            int pos = Array.IndexOf(tabFields, focusedIndex);
            pos += delta;

            if (pos >= tabFields.Length)
            {
                if (currentTab == 0)
                {
                    currentTab = 1;
                    tabFields = GetPropertyTabFields();
                    focusedIndex = tabFields[0];
                }
                else
                {
                    focusArea = FocusAreaHosts;
                    focusedIndex = 0;
                }
            }
            else if (pos < 0)
            {
                if (currentTab == 1)
                {
                    currentTab = 0;
                    tabFields = GetPropertyTabFields();
                    focusedIndex = tabFields[^1];
                }
                else
                {
                    focusArea = FocusAreaHosts;
                    focusedIndex = hostInputs.Count - 1;
                }
            }
            else
            {
                focusedIndex = tabFields[pos];
            }
        }

        FocusCurrent();
    }

    private void SwitchTab(int direction)
    {
        currentTab = direction > 0 ? (currentTab + 1) % 2 : (currentTab - 1 + 2) % 2;
        if (focusArea == FocusAreaProperties)
        {
            int[] props = GetPropertyTabFields();
            focusedIndex = props[0];
            FocusCurrent();
        }
    }

    private void HandleEnter()
    {
        if (focusArea == FocusAreaProperties)
        {
            int[] tabFields = GetPropertyTabFields();
            int pos = Array.IndexOf(tabFields, focusedIndex);
            if (currentTab == 1 && pos == tabFields.Length - 1)
            {
                SubmitForm();
                return;
            }
        }

        MoveFocus(1);
    }

    private void AddHostInput()
    {
        TextField newInput = TuiViewHelper.CreateTextField(string.Empty);
        hostInputs.Add(newInput);
        focusArea = FocusAreaHosts;
        focusedIndex = hostInputs.Count - 1;
        LayoutFields();
        FocusCurrent();
    }

    private void DeleteHostInput()
    {
        if (hostInputs.Count <= 1 || focusArea != FocusAreaHosts)
        {
            return;
        }

        hostInputs.RemoveAt(focusedIndex);
        if (focusedIndex >= hostInputs.Count)
        {
            focusedIndex = hostInputs.Count - 1;
        }

        LayoutFields();
        FocusCurrent();
    }

    internal void SubmitForm()
    {
        try
        {
            List<string> hostNames = [];
            foreach (TextField input in hostInputs)
            {
                string name = input.Text.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(name))
                {
                    hostNames.Add(name);
                }
            }

            if (hostNames.Count == 0)
            {
                throw new InvalidOperationException("at least one host name is required");
            }

            string hostname = propertyInputs[0].Text.ToString()?.Trim() ?? string.Empty;
            string user = propertyInputs[1].Text.ToString()?.Trim() ?? string.Empty;
            string port = propertyInputs[2].Text.ToString()?.Trim() ?? string.Empty;
            string identity = propertyInputs[3].Text.ToString()?.Trim() ?? string.Empty;
            string proxyJump = propertyInputs[4].Text.ToString()?.Trim() ?? string.Empty;
            string proxyCommand = propertyInputs[5].Text.ToString()?.Trim() ?? string.Empty;
            string options = propertyInputs[6].Text.ToString()?.Trim() ?? string.Empty;
            string tagsStr = propertyInputs[7].Text.ToString()?.Trim() ?? string.Empty;
            string remoteCommand = propertyInputs[8].Text.ToString()?.Trim() ?? string.Empty;
            string requestTty = propertyInputs[9].Text.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(port))
            {
                port = "22";
            }

            if (string.IsNullOrEmpty(hostname))
            {
                throw new InvalidOperationException("hostname is required");
            }

            foreach (string hostName in hostNames)
            {
                SshHostValidator.ValidateHost(hostName, hostname, port, identity);
            }

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

            SshHost commonHost = new()
            {
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

            if (hostNames.Count == 1 && originalHosts.Count == 1)
            {
                commonHost.Name = hostNames[0];
                SshConfigMutations.UpdateSSHHostInFile(originalName, commonHost, actualConfigFile);
            }
            else
            {
                SshConfigMutations.UpdateMultiHostBlock(originalHosts, hostNames, commonHost, actualConfigFile);
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
