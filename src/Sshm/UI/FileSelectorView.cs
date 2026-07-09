using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal sealed class FileSelectorView : View
{
    private readonly List<string> files;
    private int selectedIndex;
    private readonly ListView listView;

    internal event Action<string>? FileSelected;
    internal event Action? Cancelled;

    internal FileSelectorView(string title, IReadOnlyList<string> configFiles, IReadOnlyList<string> displayNames)
    {
        files = [.. configFiles];
        selectedIndex = 0;

        Label titleLabel = new()
        {
            Text = title,
            X = 0,
            Y = 0,
        };

        ObservableCollection<string> source = new(displayNames);
        listView = new ListView
        {
            Source = new ListWrapper<string>(source),
            X = 0,
            Y = Pos.Bottom(titleLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
        };
        listView.SelectedItem = 0;
        listView.ValueChanged += (_, args) =>
        {
            if (args.NewValue.HasValue)
            {
                selectedIndex = args.NewValue.Value;
            }
        };

        Label helpLabel = new()
        {
            Text = "Up/Down or j/k: navigate | Enter: select | Esc: cancel",
            X = 0,
            Y = Pos.Bottom(listView),
            Width = Dim.Fill(),
        };

        Add(titleLabel, listView, helpLabel);
        Width = Dim.Fill();
        Height = Dim.Fill();

        KeyDownNotHandled += HandleKey;
    }

    private void HandleKey(object? sender, Key key)
    {
        if (key == Key.Esc)
        {
            key.Handled = true;
            Cancelled?.Invoke();
            return;
        }

        if (key == Key.Enter)
        {
            key.Handled = true;
            ConfirmSelection();
            return;
        }

        if (key == Key.CursorDown || TuiKeys.IsChar(key, 'j'))
        {
            key.Handled = true;
            MoveSelection(1);
            return;
        }

        if (key == Key.CursorUp || TuiKeys.IsChar(key, 'k'))
        {
            key.Handled = true;
            MoveSelection(-1);
        }
    }

    private void MoveSelection(int delta)
    {
        if (files.Count == 0)
        {
            return;
        }

        selectedIndex += delta;
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        if (selectedIndex >= files.Count)
        {
            selectedIndex = files.Count - 1;
        }

        listView.SelectedItem = selectedIndex;
    }

    private void ConfirmSelection()
    {
        if (selectedIndex >= 0 && selectedIndex < files.Count)
        {
            FileSelected?.Invoke(files[selectedIndex]);
        }
    }
}
