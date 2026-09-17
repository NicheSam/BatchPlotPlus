using System;
using System.Drawing;
using System.Windows.Forms;

namespace BatchPlotPlus.AutoCAD
{
    internal sealed class FontToolsForm : Form
    {
        private readonly TextBox _report = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, WordWrap = false };
        internal FontToolsForm()
        {
            Text = "BatchPlotPlus - \u5b57\u578b\u7ba1\u7406";
            Size = new Size(860, 560);
            MinimumSize = new Size(620, 400);
            StartPosition = FormStartPosition.CenterParent;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            Add(actions, "\u555f\u7528", () => FontService.SetEnabled(true));
            Add(actions, "\u505c\u7528", () => FontService.SetEnabled(false));
            Add(actions, "\u6e05\u9664\u672c\u5de5\u5177\u66ff\u4ee3\u6a94", () => FontService.Clear());
            Add(actions, "\u91cd\u65b0\u6574\u7406", () => { });
            var note = new Label { Dock = DockStyle.Bottom, AutoSize = true,
                Text = "\u50c5\u66ff\u4ee3\u7f3a\u5931 SHX \u5927\u5b57\u9ad4\u3002\u505c\u7528\u5f8c\u624d\u80fd\u6e05\u9664\uff1b\u5df2\u958b\u5716\u9762\u9700\u91cd\u958b\u624d\u6703\u91cd\u65b0\u89e3\u6790\u5b57\u578b\u3002\r\n\u66ff\u4ee3\u53ef\u6539\u8b8a\u5b57\u5bec\u8207\u63db\u884c\uff0c\u51fa\u5716\u524d\u8acb\u6aa2\u67e5\u4e2d\u6587\u5167\u5bb9\u3002" };
            note.Text += "\r\n\u820a CadFontAuto \u66ff\u4ee3\u6a94\u4e0d\u5c6c\u65bc\u672c\u5feb\u53d6\uff0c\u4e0d\u6703\u88ab\u6b64\u8655\u6e05\u9664\u3002";
            Controls.Add(_report); Controls.Add(actions); Controls.Add(note);
            RefreshReport();
            Shown += (sender, args) => { ActiveControl = actions.Controls[0]; _report.Select(0, 0); };
        }
        private void Add(FlowLayoutPanel panel, string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (sender, args) =>
            {
                try { action(); RefreshReport(); }
                catch (Exception e) { MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            panel.Controls.Add(button);
        }
        private void RefreshReport() { _report.Text = FontService.Report(); }
    }
}
