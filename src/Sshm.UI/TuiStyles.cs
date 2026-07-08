using System.Globalization;
using Terminal.Gui.Drawing;

namespace Sshm.UI;

internal static class TuiStyles
{
    internal static Color PrimaryColor { get; } = Color.Parse(TuiConstants.PrimaryColorHex);

    internal static Scheme Primary { get; } = new(new Terminal.Gui.Drawing.Attribute(PrimaryColor, Color.Black));

    internal static Scheme Secondary { get; } = new(new Terminal.Gui.Drawing.Attribute(Color.Gray, Color.Black));

    internal static Scheme Error { get; } = new(new Terminal.Gui.Drawing.Attribute(Color.White, Color.Red));

    internal static Scheme Success { get; } = new(new Terminal.Gui.Drawing.Attribute(Color.Green, Color.Black));

    internal static Scheme Warning { get; } = new(new Terminal.Gui.Drawing.Attribute(Color.Yellow, Color.Black));
}
