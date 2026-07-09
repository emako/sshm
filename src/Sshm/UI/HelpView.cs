using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal sealed class HelpView : View
{
    internal event Action? Closed;

    internal HelpView()
    {
        Label title = new()
        {
            Text = "\U0001f4d6 SSHM - Commands",
            X = Pos.Center(),
            Y = 1,
        };

        Label leftColumn = new()
        {
            Text = """
                Navigation & Connection

                Enter  connect to selected host
                i   show host information
                /   search hosts
                Tab switch focus

                Host Management

                a   add new host
                e   edit selected host
                m   move host to another config
                d   delete selected host
                """,
            X = 2,
            Y = 3,
            Width = 36,
        };

        Label rightColumn = new()
        {
            Text = """
                Advanced Features

                p   ping all hosts
                H   toggle hidden hosts visibility
                f   setup port forwarding
                s   cycle sort modes
                n   sort by name
                r   sort by recent connection

                System

                h   show this help
                q   quit application
                ESC exit current view
                """,
            X = Pos.Right(leftColumn) + 2,
            Y = 3,
            Width = 36,
        };

        Label footer = new()
        {
            Text = "Press ESC, h, q or Enter to close",
            X = Pos.Center(),
            Y = Pos.Bottom(rightColumn) + 1,
        };

        Add(title, leftColumn, rightColumn, footer);
        Width = Dim.Fill();
        Height = Dim.Fill();
        BorderStyle = LineStyle.Rounded;

        KeyDownNotHandled += HandleKey;
    }

    private void HandleKey(object? sender, Key key)
    {
        if (key == Key.Esc || key == Key.Enter || TuiKeys.IsChar(key, 'q') || TuiKeys.IsChar(key, 'h') || TuiKeys.IsCtrlChar(key, 'c'))
        {
            key.Handled = true;
            Closed?.Invoke();
        }
    }
}
