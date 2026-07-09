using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Sshm.UI;

internal static class TuiViewHelper
{
    internal static TextField CreateTextField(string text)
    {
        TextField field = new()
        {
            Width = Dim.Fill(),
            Text = text,
        };
        return field;
    }

    internal static void Style(View view, Scheme scheme)
    {
        view.SetScheme(scheme);
    }
}
