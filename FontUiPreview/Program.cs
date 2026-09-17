using System;
using System.Drawing;
using System.Windows.Forms;
namespace BatchPlotPlus.AutoCAD
{
    // View-only sample data; this executable never references or starts AutoCAD.
    internal static class FontService
    {
        internal static bool Enabled = true;
        internal static void SetEnabled(bool enabled) { Enabled = enabled; }
        internal static int Clear() { return 0; }
        internal static string Report() => "Enabled (missing BigFont SHX only)\r\nCache: [per-user / host / profile]\r\nLog: fonts.log\r\n\r\nexample.shx [owned]\r\n\r\n2026-09-17 created: example.shx -> chineset.shx";
    }
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            using (var form = new FontToolsForm())
            {
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.Show();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(args[0]); }
            }
        }
    }
}
