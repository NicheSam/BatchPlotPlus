using System;
using BatchPlotPlus.AutoCAD;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2) throw new ArgumentException("Expected output PNG path and optional dwg mode.");
        UiPreview.Render(args[0], args.Length == 2 && string.Equals(args[1], "dwg", StringComparison.OrdinalIgnoreCase));
    }
}
