using System.Data;
using System.Diagnostics;
using Sshm.Core;
using Sshm.Config;
using Sshm.Connectivity;
using Sshm.Core.Enums;
using Sshm.Core.Models;
using Sshm.History;
using Sshm.Version;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

public static class TuiApplication
{
    public static void RunInteractiveMode(
        IReadOnlyList<SshHost> hosts,
        string configFile,
        bool searchMode,
        string currentVersion,
        bool noUpdateCheck)
    {
        ConsoleEncoding.EnsureUtf8();

        TuiShell shell = new(hosts, configFile, searchMode, currentVersion, noUpdateCheck);
        shell.Run();
    }
}

internal sealed class TuiShell
{
    private readonly IApplication app;
    private readonly Window topWindow;
    private readonly string configFile;
    private readonly string currentVersion;
    private readonly AppConfig appConfig;
    private readonly HistoryManager? historyManager;
    private readonly PingManager pingManager;

    private List<SshHost> allHosts;
    private List<SshHost> visibleHosts;
    private List<SshHost> filteredHosts;
    private SortMode sortMode = SortMode.ByName;
    private ViewMode viewMode = ViewMode.List;
    private bool searchModeActive;
    private bool showHidden;
    private bool deleteMode;
    private SshHost? deleteHost;
    private UpdateInfo? updateInfo;
    private string errorMessage = string.Empty;
    private bool showingError;

    private readonly Label titleLabel;
    private readonly Label updateBanner;
    private readonly Label hiddenBanner;
    private readonly Label errorBanner;
    private readonly TextField searchField;
    private readonly TableView hostTable;
    private readonly DataTableSource tableSource;
    private readonly Label helpLabel;
    private readonly FrameView deleteDialog;
    private readonly Label deleteQuestionLabel;

    private View? currentSubView;

    internal TuiShell(
        IReadOnlyList<SshHost> hosts,
        string configFile,
        bool searchMode,
        string currentVersion,
        bool noUpdateCheck)
    {
        this.configFile = configFile;
        this.currentVersion = currentVersion;
        allHosts = [.. hosts];

        try
        {
            appConfig = AppConfigService.LoadAppConfig();
        }
        catch
        {
            appConfig = AppConfigService.GetDefaultAppConfig();
        }

        if (noUpdateCheck)
        {
            appConfig.CheckForUpdates = false;
        }

        try
        {
            historyManager = HistoryManager.Create();
        }
        catch
        {
            historyManager = null;
        }

        pingManager = new PingManager(TimeSpan.FromSeconds(5), configFile);
        visibleHosts = HostSorter.SortHosts(SshConfigParser.FilterVisibleHosts(allHosts), sortMode, historyManager);
        filteredHosts = [.. visibleHosts];
        searchModeActive = searchMode;

        app = Application.Create();
        app.Init();

        topWindow = new Window
        {
            Title = string.Empty,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        titleLabel = new Label
        {
            Text = TuiConstants.AsciiTitle.TrimEnd(),
            X = Pos.Center(),
            Y = 0,
        };

        updateBanner = new Label
        {
            Text = string.Empty,
            X = Pos.Center(),
            Y = Pos.Bottom(titleLabel),
            Visible = false,
        };

        errorBanner = new Label
        {
            Text = string.Empty,
            X = Pos.Center(),
            Y = Pos.Bottom(updateBanner),
            Visible = false,
        };

        hiddenBanner = new Label
        {
            Text = "  [showing hidden hosts \u2014 press H to hide]",
            X = 0,
            Y = Pos.Bottom(errorBanner),
            Visible = false,
        };

        searchField = TuiViewHelper.CreateTextField(string.Empty);
        searchField.X = 0;
        searchField.Y = Pos.Bottom(hiddenBanner);
        searchField.TextChanged += (_, _) => OnSearchChanged();

        DataTable table = HostTableBuilder.BuildTable(filteredHosts, sortMode, historyManager, pingManager);
        tableSource = new DataTableSource(table);
        hostTable = new TableView(tableSource)
        {
            X = 0,
            Y = Pos.Bottom(searchField),
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
        };

        helpLabel = new Label
        {
            Text = GetHelpText(),
            X = 0,
            Y = Pos.Bottom(hostTable),
            Width = Dim.Fill(),
        };

        deleteDialog = new FrameView
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 50,
            Height = 10,
            Visible = false,
        };

        Label deleteTitle = new()
        {
            Text = " DELETE SSH HOST ",
            X = Pos.Center(),
            Y = 0,
        };

        deleteQuestionLabel = new Label
        {
            Text = string.Empty,
            X = 1,
            Y = 2,
            Width = Dim.Fill(2),
        };
        deleteDialog.Add(deleteTitle, deleteQuestionLabel);

        topWindow.Add(titleLabel, updateBanner, errorBanner, hiddenBanner, searchField, hostTable, helpLabel, deleteDialog);

        app.Keyboard.KeyDown += HandleAppKeyDown;
        RefreshTable();

        if (searchModeActive)
        {
            searchField.SetFocus();
        }
        else
        {
            hostTable.SetFocus();
        }

        if (!string.IsNullOrEmpty(currentVersion) && appConfig.IsUpdateCheckEnabled())
        {
            _ = CheckUpdatesAsync();
        }
    }

    internal void Run()
    {
        app.Run(topWindow);
        if (app is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void HandleAppKeyDown(object? sender, Key key)
    {
        if (viewMode != ViewMode.List)
        {
            return;
        }

        if (TuiKeys.IsChar(key, '/')) { key.Handled = true; EnterSearchMode(); return; }
        if (TuiKeys.IsCtrlChar(key, 'f')) { key.Handled = true; EnterSearchMode(); return; }
        if (TuiKeys.IsChar(key, 'a')) { key.Handled = true; HandleAdd(); return; }
        if (TuiKeys.IsChar(key, 'e')) { key.Handled = true; HandleEdit(); return; }
        if (TuiKeys.IsChar(key, 'i')) { key.Handled = true; HandleInfo(); return; }
        if (TuiKeys.IsChar(key, 'm')) { key.Handled = true; HandleMove(); return; }
        if (TuiKeys.IsChar(key, 'd')) { key.Handled = true; HandleDelete(); return; }
        if (TuiKeys.IsChar(key, 'f')) { key.Handled = true; HandlePortForward(); return; }
        if (TuiKeys.IsChar(key, 'p')) { key.Handled = true; HandlePingAll(); return; }
        if (TuiKeys.IsChar(key, 'h')) { key.Handled = true; HandleHelp(); return; }
        if (TuiKeys.IsChar(key, 'H')) { key.Handled = true; ToggleHiddenHosts(); return; }
        if (TuiKeys.IsChar(key, 's')) { key.Handled = true; CycleSortMode(); return; }
        if (TuiKeys.IsChar(key, 'n')) { key.Handled = true; SetSortByName(); return; }
        if (TuiKeys.IsChar(key, 'r')) { key.Handled = true; SetSortByRecent(); return; }
        if (TuiKeys.IsChar(key, 'q')) { key.Handled = true; HandleQuit("q"); return; }
        if (TuiKeys.IsChar(key, 'j')) { key.Handled = true; MoveTableSelection(1); return; }
        if (TuiKeys.IsChar(key, 'k')) { key.Handled = true; MoveTableSelection(-1); return; }
        if (key == Key.Esc) { key.Handled = true; HandleEscape(); return; }
        if (TuiKeys.IsCtrlChar(key, 'c')) { key.Handled = true; HandleQuit("ctrl+c"); return; }
        if (key == Key.Tab) { key.Handled = true; ToggleSearchFocus(); return; }
        if (key == Key.Enter) { key.Handled = true; HandleEnter(); }
    }

    private string GetHelpText()
    {
        return searchModeActive
            ? " Type to filter \u2022 Enter: validate \u2022 Tab: switch \u2022 ESC: quit"
            : " \u2191/\u2193: navigate \u2022 Enter: connect \u2022 p: ping all \u2022 i: info \u2022 h: help \u2022 q: quit";
    }

    private void OnSearchChanged()
    {
        string query = searchField.Text ?? string.Empty;
        filteredHosts = string.IsNullOrEmpty(query)
            ? HostSorter.SortHosts(visibleHosts, sortMode, historyManager)
            : HostFilter.FilterHosts(query, visibleHosts, sortMode, historyManager);
        RefreshTable();
    }

    private void RefreshTable()
    {
        DataTable table = HostTableBuilder.BuildTable(filteredHosts, sortMode, historyManager, pingManager);
        hostTable.Table = new DataTableSource(table);
        hostTable.RefreshContentSize();
        helpLabel.Text = GetHelpText();
        hiddenBanner.Visible = showHidden;
    }

    private void EnterSearchMode()
    {
        if (viewMode != ViewMode.List || deleteMode)
        {
            return;
        }

        searchModeActive = true;
        searchField.SetFocus();
        helpLabel.Text = GetHelpText();
    }

    private void ToggleSearchFocus()
    {
        if (viewMode != ViewMode.List || deleteMode)
        {
            return;
        }

        searchModeActive = !searchModeActive;
        if (searchModeActive)
        {
            searchField.SetFocus();
        }
        else
        {
            hostTable.SetFocus();
        }

        helpLabel.Text = GetHelpText();
    }

    private void HandleEnter()
    {
        if (viewMode != ViewMode.List)
        {
            return;
        }

        if (searchModeActive)
        {
            searchModeActive = false;
            hostTable.SetFocus();
            helpLabel.Text = GetHelpText();
            return;
        }

        if (deleteMode)
        {
            ConfirmDelete();
            return;
        }

        SshHost? host = GetSelectedHost();
        if (host != null)
        {
            ConnectToHost(host.Name);
        }
    }

    private void HandleEscape()
    {
        if (viewMode != ViewMode.List)
        {
            return;
        }

        if (deleteMode)
        {
            deleteMode = false;
            deleteHost = null;
            deleteDialog.Visible = false;
            hostTable.SetFocus();
            return;
        }

        HandleQuit("esc");
    }

    private void HandleQuit(string key)
    {
        if (viewMode != ViewMode.List)
        {
            return;
        }

        if (searchModeActive && key != "esc" && key != "ctrl+c")
        {
            return;
        }

        if (deleteMode)
        {
            return;
        }

        if (appConfig.KeyBindings.ShouldQuitOnKey(key))
        {
            app.RequestStop();
        }
    }

    private void MoveTableSelection(int delta)
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        TableSelection? selection = hostTable.Value;
        int row = selection == null ? 0 : selection.SelectedCell.Y;
        row += delta;
        if (row < 0)
        {
            row = 0;
        }

        if (row >= filteredHosts.Count)
        {
            row = Math.Max(0, filteredHosts.Count - 1);
        }

        hostTable.Value = new TableSelection(new System.Drawing.Point(0, row));
    }

    private SshHost? GetSelectedHost()
    {
        TableSelection? selection = hostTable.Value;
        int row = selection == null ? 0 : selection.SelectedCell.Y;
        if (row >= 0 && row < filteredHosts.Count)
        {
            return filteredHosts[row];
        }

        return null;
    }

    private void ConnectToHost(string hostName)
    {
        historyManager?.RecordConnection(hostName);
        LaunchSsh(hostName);
    }

    private void LaunchSsh(string hostName, IReadOnlyList<string>? extraArgs = null)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "ssh",
            UseShellExecute = true,
        };

        if (!string.IsNullOrEmpty(configFile))
        {
            psi.ArgumentList.Add("-F");
            psi.ArgumentList.Add(configFile);
        }

        if (extraArgs != null)
        {
            foreach (string arg in extraArgs)
            {
                psi.ArgumentList.Add(arg);
            }
        }

        psi.ArgumentList.Add(hostName);
        Process.Start(psi);
        app.RequestStop();
    }

    private void HandleAdd()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        List<string> configFiles = string.IsNullOrEmpty(configFile)
            ? SshConfigParser.GetAllConfigFiles()
            : SshConfigParser.GetAllConfigFilesFromBase(configFile);

        if (configFiles.Count <= 1)
        {
            string target = configFiles.Count == 1 ? configFiles[0] : configFile;
            ShowAddForm(target);
            return;
        }

        ShowFileSelector(
            "Select config file to add host to:",
            configFiles,
            selectedFile =>             ShowAddForm(selectedFile));
    }

    private void ShowAddForm(string targetConfigFile)
    {
        AddHostFormView form = new(targetConfigFile, null);
        form.Cancelled += () => ShowListView();
        form.Submitted += () =>
        {
            ReloadHosts();
            ShowListView();
        };
        SwitchToSubView(form, ViewMode.Add);
    }

    private void HandleEdit()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        SshHost? host = GetSelectedHost();
        if (host == null)
        {
            return;
        }

        try
        {
            SshHost fullHost = string.IsNullOrEmpty(configFile)
                ? SshConfigQuery.GetSSHHost(host.Name)
                : SshConfigQuery.GetSSHHostFromFile(host.Name, configFile);
            EditHostFormView form = new(fullHost, host.Name, configFile);
            form.Cancelled += () => ShowListView();
            form.Submitted += () =>
            {
                ReloadHosts();
                ShowListView();
            };
            SwitchToSubView(form, ViewMode.Edit);
        }
        catch
        {
        }
    }

    private void HandleInfo()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        SshHost? host = GetSelectedHost();
        if (host == null)
        {
            return;
        }

        try
        {
            SshHost fullHost = string.IsNullOrEmpty(configFile)
                ? SshConfigQuery.GetSSHHost(host.Name)
                : SshConfigQuery.GetSSHHostFromFile(host.Name, configFile);
            InfoView info = new(fullHost, host.Name);
            info.Cancelled += () => ShowListView();
            info.EditRequested += name => ShowEditFromInfo(name);
            SwitchToSubView(info, ViewMode.Info);
        }
        catch
        {
        }
    }

    private void ShowEditFromInfo(string hostName)
    {
        try
        {
            SshHost fullHost = string.IsNullOrEmpty(configFile)
                ? SshConfigQuery.GetSSHHost(hostName)
                : SshConfigQuery.GetSSHHostFromFile(hostName, configFile);
            EditHostFormView form = new(fullHost, hostName, configFile);
            form.Cancelled += () => ShowListView();
            form.Submitted += () =>
            {
                ReloadHosts();
                ShowListView();
            };
            SwitchToSubView(form, ViewMode.Edit);
        }
        catch
        {
            ShowListView();
        }
    }

    private void HandleMove()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        SshHost? host = GetSelectedHost();
        if (host == null)
        {
            return;
        }

        try
        {
            MoveHostFormView form = MoveHostFormView.Create(host.Name, configFile);
            form.Cancelled += () => ShowListView();
            form.Submitted += () =>
            {
                ReloadHosts();
                ShowListView();
            };
            SwitchToSubView(form, ViewMode.Move);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void HandleDelete()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        SshHost? host = GetSelectedHost();
        if (host == null)
        {
            return;
        }

        deleteMode = true;
        deleteHost = host;
        deleteQuestionLabel.Text = $"Are you sure you want to delete host '{host.Name}'?\n\nThis action cannot be undone.\n\nEnter: confirm | Esc: cancel";

        deleteDialog.Visible = true;
    }

    private void ConfirmDelete()
    {
        if (deleteHost == null)
        {
            deleteMode = false;
            deleteDialog.Visible = false;
            return;
        }

        try
        {
            SshConfigMutations.DeleteSSHHostWithLine(deleteHost);
            ReloadHosts();
        }
        catch
        {
        }

        deleteMode = false;
        deleteHost = null;
        deleteDialog.Visible = false;
        hostTable.SetFocus();
    }

    private void HandlePortForward()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        SshHost? host = GetSelectedHost();
        if (host == null)
        {
            return;
        }

        PortForwardFormView form = new(host.Name, configFile, historyManager);
        form.Cancelled += () => ShowListView();
        form.Submitted += args =>
        {
            historyManager?.RecordConnection(host.Name);
            ProcessStartInfo psi = new()
            {
                FileName = "ssh",
                UseShellExecute = true,
            };
            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            Process.Start(psi);
            app.RequestStop();
        };
        SwitchToSubView(form, ViewMode.PortForward);
    }

    private async void HandlePingAll()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        List<Task> tasks = [];
        foreach (SshHost host in visibleHosts)
        {
            tasks.Add(pingManager.PingHostAsync(host));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        app.Invoke(() => RefreshTable());
    }

    private void HandleHelp()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        HelpView help = new();
        help.Closed += () => ShowListView();
        SwitchToSubView(help, ViewMode.Help);
    }

    private void ToggleHiddenHosts()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        showHidden = !showHidden;
        visibleHosts = HostSorter.SortHosts(
            showHidden ? allHosts : SshConfigParser.FilterVisibleHosts(allHosts),
            sortMode,
            historyManager);
        ApplyCurrentFilter();
        RefreshTable();
    }

    private void CycleSortMode()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        sortMode = sortMode == SortMode.ByName ? SortMode.ByLastUsed : SortMode.ByName;
        ApplyCurrentFilter();
        RefreshTable();
    }

    private void SetSortByName()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        sortMode = SortMode.ByName;
        ApplyCurrentFilter();
        RefreshTable();
    }

    private void SetSortByRecent()
    {
        if (viewMode != ViewMode.List || searchModeActive || deleteMode)
        {
            return;
        }

        sortMode = SortMode.ByLastUsed;
        ApplyCurrentFilter();
        RefreshTable();
    }

    private void ApplyCurrentFilter()
    {
        string query = searchField.Text ?? string.Empty;
        filteredHosts = string.IsNullOrEmpty(query)
            ? HostSorter.SortHosts(visibleHosts, sortMode, historyManager)
            : HostFilter.FilterHosts(query, visibleHosts, sortMode, historyManager);
    }

    private void ReloadHosts()
    {
        allHosts = string.IsNullOrEmpty(configFile)
            ? SshConfigParser.ParseSSHConfig()
            : SshConfigParser.ParseSSHConfigFile(configFile);
        visibleHosts = HostSorter.SortHosts(
            showHidden ? allHosts : SshConfigParser.FilterVisibleHosts(allHosts),
            sortMode,
            historyManager);
        ApplyCurrentFilter();
        RefreshTable();
    }

    private void ShowFileSelector(string title, IReadOnlyList<string> files, Action<string> onSelected)
    {
        List<string> displayNames = [];
        string homeDir = PlatformPaths.GetSSHDirectory();
        string mainConfig = PlatformPaths.GetDefaultSSHConfigPath();
        foreach (string file in files)
        {
            if (string.Equals(file, mainConfig, StringComparison.OrdinalIgnoreCase))
            {
                displayNames.Add("Main SSH Config (~/.ssh/config)");
            }
            else if (file.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                displayNames.Add("~/.ssh/" + Path.GetRelativePath(homeDir, file).Replace('\\', '/'));
            }
            else
            {
                displayNames.Add(file);
            }
        }

        FileSelectorView selector = new(title, files, displayNames);
        selector.Cancelled += () => ShowListView();
        selector.FileSelected += onSelected;
        SwitchToSubView(selector, ViewMode.FileSelector);
    }

    private void SwitchToSubView(View subView, ViewMode mode)
    {
        HideListControls();
        if (currentSubView != null)
        {
            topWindow.Remove(currentSubView);
        }

        currentSubView = subView;
        viewMode = mode;
        subView.X = 0;
        subView.Y = 0;
        subView.Width = Dim.Fill();
        subView.Height = Dim.Fill();
        topWindow.Add(subView);
        subView.SetFocus();
    }

    private void ShowListView()
    {
        if (currentSubView != null)
        {
            topWindow.Remove(currentSubView);
            currentSubView = null;
        }

        viewMode = ViewMode.List;
        ShowListControls();
        if (searchModeActive)
        {
            searchField.SetFocus();
        }
        else
        {
            hostTable.SetFocus();
        }
    }

    private void HideListControls()
    {
        titleLabel.Visible = false;
        updateBanner.Visible = false;
        errorBanner.Visible = false;
        hiddenBanner.Visible = false;
        searchField.Visible = false;
        hostTable.Visible = false;
        helpLabel.Visible = false;
        deleteDialog.Visible = false;
    }

    private void ShowListControls()
    {
        titleLabel.Visible = true;
        updateBanner.Visible = updateInfo?.Available == true;
        errorBanner.Visible = showingError;
        hiddenBanner.Visible = showHidden;
        searchField.Visible = true;
        hostTable.Visible = true;
        helpLabel.Visible = true;
        deleteDialog.Visible = deleteMode;
    }

    private void ShowError(string message)
    {
        errorMessage = message;
        showingError = true;
        errorBanner.Text = "\u274c " + message;
        errorBanner.Visible = true;
        app.AddTimeout(TimeSpan.FromSeconds(3), () =>
        {
            showingError = false;
            errorBanner.Visible = false;
            errorMessage = string.Empty;
            return false;
        });
    }

    private async Task CheckUpdatesAsync()
    {
        try
        {
            UpdateInfo info = await UpdateChecker.CheckForUpdatesAsync(currentVersion).ConfigureAwait(false);
            app.Invoke(() =>
            {
                updateInfo = info;
                if (info.Available)
                {
                    updateBanner.Text = $"\U0001f680 Update available: {info.CurrentVer} \u2192 {info.LatestVer}";
                    updateBanner.Visible = true;
                }
            });
        }
        catch
        {
        }
    }
}
