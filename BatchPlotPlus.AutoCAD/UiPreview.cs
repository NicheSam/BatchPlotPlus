using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace BatchPlotPlus.AutoCAD
{
    public static class UiPreview
    {
        public static void Render(string path, bool splitDwg = false)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var state = new PluginState
            {
                OperationMode = splitDwg ? OperationMode.SplitDwg : OperationMode.Pdf,
                OutputDirectory = @"C:\Output\PDF",
                DwgOutputDirectory = @"C:\Output\SplitDWG",
                MatchingFrameCount = 9,
                MatchingNamedFrameCount = 8
            };
            using (var form = new BatchPlotForm(state))
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-10000, -10000);
                form.Show();
                Application.DoEvents();
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(path, ImageFormat.Png);
                form.Hide();
            }
        }
    }
}
