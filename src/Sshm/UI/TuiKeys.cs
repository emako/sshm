using System.Globalization;
using Terminal.Gui.Input;

namespace Sshm.UI;

internal static class TuiKeys
{
    internal static bool IsChar(Key key, char ch)
    {
        return !key.IsCtrl && !key.IsAlt && key.AsRune == new System.Text.Rune(ch);
    }

    internal static bool IsCtrlChar(Key key, char ch)
    {
        return key.IsCtrl && !key.IsAlt && key.AsRune == new System.Text.Rune(ch);
    }

    internal static bool IsShiftTab(Key key)
    {
        return key == Key.Tab && key.IsShift;
    }
}
