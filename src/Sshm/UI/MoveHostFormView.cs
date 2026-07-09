using Sshm.Config;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal sealed class MoveHostFormView : View
{
    private readonly FileSelectorView fileSelector;
    private readonly string hostName;
    private bool processing;

    internal event Action? Cancelled;
    internal event Action? Submitted;

    internal MoveHostFormView(string hostName, string configFile, IReadOnlyList<string> files)
    {
        this.hostName = hostName;

        List<string> displayNames = BuildDisplayNames(files);
        fileSelector = new FileSelectorView(
            $"Select destination config file for host '{hostName}':",
            files,
            displayNames);
        fileSelector.FileSelected += OnFileSelected;
        fileSelector.Cancelled += () => Cancelled?.Invoke();

        Add(fileSelector);
        Width = Dim.Fill();
        Height = Dim.Fill();

        KeyDownNotHandled += HandleKey;
    }

    internal static MoveHostFormView Create(string hostName, string configFile)
    {
        List<string> files = SshConfigQuery.GetConfigFilesExcludingCurrent(hostName, configFile);
        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                "no includes found in SSH config file - move operation requires multiple config files");
        }

        return new MoveHostFormView(hostName, configFile, files);
    }

    private static List<string> BuildDisplayNames(IReadOnlyList<string> files)
    {
        string homeDir = PlatformPaths.GetSSHDirectory();
        string mainConfig = PlatformPaths.GetDefaultSSHConfigPath();
        List<string> displayNames = [];

        foreach (string file in files)
        {
            if (string.Equals(file, mainConfig, StringComparison.OrdinalIgnoreCase))
            {
                displayNames.Add("Main SSH Config (~/.ssh/config)");
                continue;
            }

            if (file.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                string relPath = Path.GetRelativePath(homeDir, file).Replace('\\', '/');
                displayNames.Add("~/.ssh/" + relPath);
            }
            else
            {
                displayNames.Add(file);
            }
        }

        return displayNames;
    }

    private void HandleKey(object? sender, Key key)
    {
        if (!processing && TuiKeys.IsChar(key, 'q'))
        {
            key.Handled = true;
            Cancelled?.Invoke();
        }
    }

    private void OnFileSelected(string targetFile)
    {
        if (processing)
        {
            return;
        }

        processing = true;
        try
        {
            SshConfigMutations.MoveHostToFile(hostName, targetFile);
            Submitted?.Invoke();
        }
        catch
        {
            processing = false;
            Submitted?.Invoke();
        }
    }
}
