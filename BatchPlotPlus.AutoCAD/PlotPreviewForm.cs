using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace BatchPlotPlus.AutoCAD
{
    internal sealed class PlotPreviewForm : Form
    {
        private readonly IList<PlotPreviewItem> _items;
        private readonly DataGridView _grid;
        private readonly Panel _canvas;
        private readonly Label _summary;
        private readonly Button _closeButton;

        public PlotPreviewForm(IList<PlotPreviewItem> items)
        {
            _items = items;
            Text = "\u8f38\u51fa\u9810\u89bd";
            Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = true;
            MinimumSize = new Size(960, 600);
            ClientSize = InitialClientSize();

            _summary = new Label { TextAlign = ContentAlignment.MiddleLeft };
            _grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            _grid.Columns.Add(Column("PageNumber", "\u9801", 44));
            _grid.Columns.Add(Column("FileBase", "\u5716\u6846", 150));
            _grid.Columns.Add(Column("Paper", "\u7d19\u5f35", 58));
            _grid.Columns.Add(Column("OrientationLabel", "\u8f38\u51fa\u65b9\u5411", 76));
            _grid.Columns.Add(Column("RotationDegrees", "\u65cb\u8f49", 58));
            _grid.Columns.Add(Column("ScaleLabel", "\u6bd4\u4f8b", 110));
            _grid.Columns.Add(Column("FrameSizeLabel", "\u5716\u6846\u5c3a\u5bf8", 92));

            _canvas = new Panel { BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            _grid.SelectionChanged += (_, __) => _canvas.Invalidate();
            _canvas.Paint += DrawPreview;
            _closeButton = new Button { Text = "\u95dc\u9589", Size = new Size(88, 28) };
            _closeButton.Click += (_, __) => Close();
            Controls.AddRange(new Control[] { _summary, _grid, _canvas, _closeButton });
            Resize += (_, __) => LayoutPreviewControls();
            Load += (_, __) => { LayoutPreviewControls(); Populate(); };
        }

        private static Size InitialClientSize()
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            var width = Math.Min(1180, Math.Max(960, area.Width - 80));
            var height = Math.Min(760, Math.Max(600, area.Height - 120));
            return new Size(width, height);
        }

        private void LayoutPreviewControls()
        {
            const int margin = 12;
            const int top = 42;
            const int gap = 12;
            var buttonY = ClientSize.Height - margin - _closeButton.Height;
            var mainHeight = Math.Max(260, buttonY - top - gap);
            var availableWidth = Math.Max(760, ClientSize.Width - margin * 2 - gap);
            var gridWidth = Math.Min(548, Math.Max(390, (int)(availableWidth * 0.48)));
            _summary.Location = new Point(margin, 10);
            _summary.Size = new Size(Math.Max(200, ClientSize.Width - margin * 2), 24);
            _grid.Location = new Point(margin, top);
            _grid.Size = new Size(gridWidth, mainHeight);
            _canvas.Location = new Point(_grid.Right + gap, top);
            _canvas.Size = new Size(Math.Max(340, ClientSize.Width - _canvas.Left - margin), mainHeight);
            _closeButton.Location = new Point(ClientSize.Width - margin - _closeButton.Width, buttonY);
        }

        private void Populate()
        {
            _summary.Text = "\u5171 " + _items.Count.ToString(CultureInfo.InvariantCulture) + " \u9801\uff1b\u9ede\u9078\u5de6\u5074\u9801\u9762\u53ef\u6aa2\u67e5\u539f\u59cb\u5716\u6846\u65b9\u5411\u3001\u9810\u671f PDF \u65b9\u5411\u8207\u5716\u6846\u5167\u5bb9\u662f\u5426\u8f49\u6b63\u3002";
            foreach (var item in _items)
            {
                var row = _grid.Rows.Add(
                    item.PageNumber.ToString(CultureInfo.InvariantCulture),
                    item.FileBase,
                    item.Paper,
                    item.OrientationLabel,
                    item.RotationDegrees.ToString(CultureInfo.InvariantCulture) + "\u00b0",
                    item.ScaleLabel,
                    item.FrameSizeLabel);
                _grid.Rows[row].Tag = item;
            }
            if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
        }

        private void DrawPreview(object? sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            var item = SelectedItem();
            if (item == null)
            {
                DrawCentered(e.Graphics, "\u6c92\u6709\u53ef\u9810\u89bd\u7684\u9801\u9762", _canvas.ClientRectangle, Brushes.Black);
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var sourceLandscape = item.FrameWidth >= item.FrameHeight;
            var outputLandscape = item.PageWidth >= item.PageHeight;
            var orientationMatches = sourceLandscape == outputLandscape;
            var statusText = orientationMatches ? "\u65b9\u5411\u5224\u65b7\uff1a\u5716\u6846\u8207 PDF \u7d19\u5f35\u65b9\u5411\u4e00\u81f4" : "\u65b9\u5411\u5224\u65b7\uff1a\u5716\u6846\u8207 PDF \u7d19\u5f35\u65b9\u5411\u4e0d\u4e00\u81f4";
            var statusBack = orientationMatches ? Color.FromArgb(221, 245, 229) : Color.FromArgb(255, 238, 210);
            var statusFore = orientationMatches ? Color.FromArgb(20, 120, 56) : Color.FromArgb(178, 91, 0);
            using (var brush = new SolidBrush(statusBack)) e.Graphics.FillRectangle(brush, 14, 12, _canvas.Width - 28, 38);
            using (var pen = new Pen(statusFore, 1)) e.Graphics.DrawRectangle(pen, 14, 12, _canvas.Width - 28, 38);
            using (var brush = new SolidBrush(statusFore)) e.Graphics.DrawString(statusText, new Font(Font, FontStyle.Bold), brush, 24, 22);

            DrawKeyValue(e.Graphics, "\u539f\u59cb\u5716\u6846", SourceOrientation(item), 24, 64);
            DrawKeyValue(e.Graphics, "\u9810\u671f PDF", item.Paper + " / " + item.OrientationLabel + " / " + item.RotationDegrees.ToString(CultureInfo.InvariantCulture) + "\u00b0", 24, 88);
            DrawKeyValue(e.Graphics, "\u6bd4\u4f8b\u8a2d\u5b9a", item.ScaleLabel, 24, 112);
            DrawKeyValue(e.Graphics, "\u51fa\u5716\u6a23\u5f0f", string.IsNullOrWhiteSpace(item.PlotStyle) ? "\u672a\u6307\u5b9a" : item.PlotStyle, 24, 136);
            DrawKeyValue(e.Graphics, "\u7dda\u7c97/\u900f\u660e\u5ea6", OnOff(item.PrintLineweights) + " / " + OnOff(item.PlotTransparency), 24, 160);
            DrawKeyValue(e.Graphics, "\u5167\u5bb9\u7dda\u7a3f", item.ContentSegments.Count.ToString(CultureInfo.InvariantCulture) + " \u7dda\u6bb5 / " + item.ContentEntityCount.ToString(CultureInfo.InvariantCulture) + " \u7269\u4ef6", 24, 184);

            var footerTop = Math.Max(440, _canvas.Height - 92);
            var area = new RectangleF(24, 184, Math.Max(240, _canvas.Width - 48), Math.Max(220, footerTop - 196));
            var paperRatio = item.PageWidth / Math.Max(1e-9, item.PageHeight);
            var paper = FitRect(area, paperRatio);
            using (var paperBrush = new SolidBrush(Color.FromArgb(252, 252, 252))) e.Graphics.FillRectangle(paperBrush, paper);
            using (var paperPen = new Pen(Color.FromArgb(70, 70, 70), 2)) e.Graphics.DrawRectangle(paperPen, paper.X, paper.Y, paper.Width, paper.Height);
            DrawPaperDimension(e.Graphics, paper, item);

            var printableMargin = PrintableMarginPixels(paper);
            var contentBounds = new RectangleF(paper.X + printableMargin, paper.Y + printableMargin, paper.Width - printableMargin * 2, paper.Height - printableMargin * 2);
            var frameRatio = item.FrameWidth / Math.Max(1e-9, item.FrameHeight);
            var frame = FitRect(contentBounds, frameRatio);
            using (var frameBrush = new SolidBrush(Color.White)) e.Graphics.FillRectangle(frameBrush, frame);
            using (var framePen = new Pen(Color.FromArgb(0, 102, 178), 2)) e.Graphics.DrawRectangle(framePen, frame.X, frame.Y, frame.Width, frame.Height);
            DrawContentPreview(e.Graphics, item, frame);
            if (item.ContentSegments.Count == 0)
            {
                DrawTitleBlockHint(e.Graphics, frame);
                DrawLongEdgeArrow(e.Graphics, frame);
                DrawCentered(e.Graphics, item.FileBase, frame, Brushes.Black);
            }
            else
            {
                DrawFrameNameBadge(e.Graphics, item.FileBase, frame);
            }

            var warning = orientationMatches
                ? "\u53ef\u5224\u65b7\uff1a\u539f\u5716\u9577\u908a\u548c PDF \u7d19\u5f35\u9577\u908a\u540c\u5411\u3002"
                : "\u8acb\u6ce8\u610f\uff1a\u9577\u908a\u4e0d\u540c\u5411\uff0c\u53ef\u80fd\u9700\u8981\u6539\u6210\u81ea\u52d5\u3001\u6a6b\u5411/\u76f4\u5411\u6216\u518d\u65cb\u8f49 180\u00b0\u3002";
            using (var brush = new SolidBrush(orientationMatches ? Color.FromArgb(20, 120, 56) : Color.FromArgb(178, 91, 0)))
                e.Graphics.DrawString(warning, Font, brush, 24, footerTop);
            e.Graphics.DrawString("\u8996\u7a97\u7bc4\u570d\uff1a" + item.WindowLabel, Font, Brushes.Black, 24, footerTop + 24);
            e.Graphics.DrawString("\u5716\u6846\u5c3a\u5bf8\uff1a" + item.FrameSizeLabel + "\uff1b\u7d19\u5f35\u7b26\u5408\uff1a" + item.Paper + " " + item.OrientationLabel + "\uff1b\u8a2d\u5099\uff1a" + item.Device, Font, Brushes.Black, 24, footerTop + 48);
            if (item.ContentPreviewTruncated) e.Graphics.DrawString("\u5716\u9762\u5167\u5bb9\u9810\u89bd\u5df2\u7c21\u5316\uff1a\u5716\u9762\u7269\u4ef6\u904e\u591a\uff0c\u50c5\u986f\u793a\u524d\u9762\u4e00\u90e8\u5206\u7dda\u7a3f\u3002", Font, Brushes.DarkOrange, 24, footerTop + 72);
        }

        private void DrawContentPreview(Graphics graphics, PlotPreviewItem item, RectangleF frame)
        {
            if (item.ContentSegments.Count == 0)
            {
                using (var brush = new SolidBrush(Color.FromArgb(120, 80, 80, 80)))
                    DrawCentered(graphics, "\u5716\u6846\u5167\u672a\u64f7\u53d6\u5230\u53ef\u9810\u89bd\u7dda\u7a3f", frame, brush);
                return;
            }
            var previousClip = graphics.Clip;
            graphics.SetClip(frame);
            foreach (var segment in item.ContentSegments)
            {
                using (var pen = new Pen(ResolvePreviewSegmentColor(item, segment), item.PrintLineweights ? 1.2f : 1.0f))
                {
                    graphics.DrawLine(pen,
                        MapX(segment.X1, item, frame),
                        MapY(segment.Y1, item, frame),
                        MapX(segment.X2, item, frame),
                        MapY(segment.Y2, item, frame));
                }
            }
            graphics.Clip = previousClip;
        }

        private static float PrintableMarginPixels(RectangleF paper)
        {
            return Math.Max(6.0f, Math.Min(paper.Width, paper.Height) * 0.035f);
        }

        private static Color ResolvePreviewSegmentColor(PlotPreviewItem item, PlotPreviewSegment segment)
        {
            var color = Color.FromArgb(segment.ColorArgb);
            if (IsMonochromePlotStyle(item.PlotStyle))
                color = Color.Black;
            else if (IsGrayscalePlotStyle(item.PlotStyle))
                color = ToGrayscale(color);
            var alpha = item.PlotTransparency ? 150 : 225;
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static bool IsMonochromePlotStyle(string plotStyle)
        {
            return (plotStyle ?? string.Empty).IndexOf("monochrome", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsGrayscalePlotStyle(string plotStyle)
        {
            return (plotStyle ?? string.Empty).IndexOf("grayscale", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Color ToGrayscale(Color color)
        {
            var gray = (int)Math.Round(color.R * 0.299 + color.G * 0.587 + color.B * 0.114);
            gray = Math.Max(0, Math.Min(255, gray));
            return Color.FromArgb(gray, gray, gray);
        }

        private PlotPreviewItem? SelectedItem()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as PlotPreviewItem;
        }

        private static DataGridViewTextBoxColumn Column(string name, string header, int width)
        {
            return new DataGridViewTextBoxColumn { Name = name, HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable };
        }

        private static RectangleF FitRect(RectangleF bounds, double ratio)
        {
            var width = bounds.Width;
            var height = (float)(width / ratio);
            if (height > bounds.Height)
            {
                height = bounds.Height;
                width = (float)(height * ratio);
            }
            return new RectangleF(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
        }

        private static float MapX(double x, PlotPreviewItem item, RectangleF frame)
        {
            return frame.Left + (float)((x - item.WindowMinX) / Math.Max(1e-9, item.FrameWidth) * frame.Width);
        }

        private static float MapY(double y, PlotPreviewItem item, RectangleF frame)
        {
            return frame.Bottom - (float)((y - item.WindowMinY) / Math.Max(1e-9, item.FrameHeight) * frame.Height);
        }

        private void DrawCentered(Graphics graphics, string text, RectangleF bounds, Brush brush)
        {
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                graphics.DrawString(text, Font, brush, bounds, format);
        }

        private void DrawKeyValue(Graphics graphics, string key, string value, int x, int y)
        {
            using (var keyBrush = new SolidBrush(Color.FromArgb(82, 82, 82))) graphics.DrawString(key + "\uff1a", Font, keyBrush, x, y);
            graphics.DrawString(value, new Font(Font, FontStyle.Bold), Brushes.Black, x + 86, y);
        }

        private static string SourceOrientation(PlotPreviewItem item)
        {
            var source = item.FrameWidth >= item.FrameHeight ? "\u6a6b\u5f0f\u5716\u6846" : "\u76f4\u5f0f\u5716\u6846";
            return source + " / " + item.FrameSizeLabel;
        }

        private void DrawPaperDimension(Graphics graphics, RectangleF paper, PlotPreviewItem item)
        {
            using (var arrowPen = new Pen(Color.FromArgb(100, 100, 100), 1) { CustomEndCap = new AdjustableArrowCap(4, 4) })
            {
                graphics.DrawLine(arrowPen, paper.Left, paper.Top - 12, paper.Right, paper.Top - 12);
                graphics.DrawLine(arrowPen, paper.Left - 12, paper.Bottom, paper.Left - 12, paper.Top);
            }
            graphics.DrawString("\u7d19\u5f35\u9577\u908a / " + item.OrientationLabel, Font, Brushes.Black, paper.Left + 8, paper.Top - 32);
            graphics.DrawString(item.Paper, new Font(Font, FontStyle.Bold), Brushes.Black, paper.Right - 42, paper.Bottom + 6);
        }

        private void DrawLongEdgeArrow(Graphics graphics, RectangleF frame)
        {
            var y = frame.Bottom - 18;
            using (var arrowPen = new Pen(Color.FromArgb(0, 102, 178), 2) { CustomEndCap = new AdjustableArrowCap(5, 5) })
                graphics.DrawLine(arrowPen, frame.Left + 18, y, frame.Right - 18, y);
            graphics.DrawString("\u5716\u6846\u9577\u908a", Font, Brushes.Black, frame.Left + 22, y - 24);
        }

        private void DrawFrameNameBadge(Graphics graphics, string text, RectangleF frame)
        {
            var badge = new RectangleF(frame.Left + 8, frame.Top + 8, Math.Min(120, frame.Width - 16), 22);
            using (var brush = new SolidBrush(Color.FromArgb(238, 255, 255, 255))) graphics.FillRectangle(brush, badge);
            using (var pen = new Pen(Color.FromArgb(150, 0, 102, 178), 1)) graphics.DrawRectangle(pen, badge.X, badge.Y, badge.Width, badge.Height);
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                graphics.DrawString(text, Font, Brushes.Black, badge, format);
        }

        private void DrawTitleBlockHint(Graphics graphics, RectangleF frame)
        {
            var title = new RectangleF(frame.Right - Math.Max(42, frame.Width * 0.18f), frame.Bottom - Math.Max(22, frame.Height * 0.16f), Math.Max(38, frame.Width * 0.16f), Math.Max(18, frame.Height * 0.12f));
            using (var brush = new SolidBrush(Color.FromArgb(255, 250, 214))) graphics.FillRectangle(brush, title);
            using (var pen = new Pen(Color.FromArgb(168, 123, 0), 1)) graphics.DrawRectangle(pen, title.X, title.Y, title.Width, title.Height);
            DrawCentered(graphics, "\u6a19\u984c\u6b04", title, Brushes.Black);
        }

        private static string OnOff(bool value) => value ? "\u958b" : "\u95dc";
    }
}
