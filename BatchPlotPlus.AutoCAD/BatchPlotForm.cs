using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace BatchPlotPlus.AutoCAD
{
    internal sealed class BatchPlotForm : Form
    {
        private readonly PluginState _state;
        private readonly RadioButton _framePolyline;
        private readonly RadioButton _frameBlock;
        private readonly RadioButton _frameCustom;
        private readonly CheckBox _autoLayer;
        private readonly TextBox _templateText;
        private readonly TextBox _layerText;
        private readonly Label _selectedCount;
        private readonly Label _rangeStatus;
        private readonly Label _matchingStatus;
        private readonly TabControl _tabs;
        private readonly Button _start;

        private readonly RadioButton _separatePdf;
        private readonly RadioButton _mergedPdf;
        private readonly ComboBox _device;
        private readonly ComboBox _paper;
        private readonly ComboBox _style;
        private readonly NumericUpDown _copies;
        private readonly RadioButton _fit;
        private readonly NumericUpDown _scale;
        private readonly RadioButton _sortSelection;
        private readonly RadioButton _sortHorizontal;
        private readonly RadioButton _sortVertical;
        private readonly CheckBox _reverseOrder;
        private readonly CheckBox _autoRotate;
        private readonly CheckBox _reverseOrientation;
        private readonly CheckBox _centerPlot;
        private readonly TextBox _pdfDirectory;
        private readonly TextBox _mergedName;
        private readonly Label _mergedNameLabel;

        private readonly TextBox _dwgDirectory;
        private readonly CheckBox _dwgTestFirstTwo;

        public BatchPlotForm(PluginState state)
        {
            _state = state;
            Text = "批次輸出工具 Plus V1.3.6";
            Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(760, 604);

            var common = Group("共用圖框條件", 8, 8, 744, 174);
            _frameBlock = Radio("圖塊：尋找相同名稱的圖框圖塊", 12, 22, 224, state.FrameMode == FrameMode.Block);
            _framePolyline = Radio("矩形聚合線：限四邊直線圖框", 12, 48, 224, state.FrameMode == FrameMode.Polyline);
            _frameCustom = Radio("封閉聚合線：不限矩形", 12, 74, 224, state.FrameMode == FrameMode.Custom);
            _frameBlock.CheckedChanged += FrameModeChanged;
            _framePolyline.CheckedChanged += FrameModeChanged;
            _frameCustom.CheckedChanged += FrameModeChanged;

            _autoLayer = Check("使用樣板圖層", 250, 22, 116, state.AutoLayer);
            var selectTemplate = Button("選取圖框樣板...", 372, 18, 122, 25, (_, __) => Retry(PendingAction.SelectTemplate));
            _templateText = TextAt(state.TemplateLabel, 326, 49, 168, 23, true);
            _layerText = TextAt(state.LayerName, 326, 76, 168, 23, state.AutoLayer);
            _autoLayer.CheckedChanged += (_, __) => _layerText.ReadOnly = _autoLayer.Checked;

            var selectFrames = Button("指定要處理的圖框...", 512, 18, 216, 25, (_, __) => Retry(PendingAction.SelectSheets));
            _selectedCount = LabelAt(SelectionStatus(state.ExplicitSheetHandles.Count), 512, 49, 128, 21);
            var useAllFrames = Button("使用全部圖框", 642, 48, 86, 24, (_, __) => Retry(PendingAction.ClearSheets));
            _rangeStatus = LabelAt(RangeStatus(state.Range.HasValue), 512, 105, 216, 21);
            var setRange = Button("設定搜尋範圍...", 512, 76, 104, 25, (_, __) => Retry(PendingAction.SelectRange));
            var clearRange = Button("搜尋整個模型", 624, 76, 104, 25, (_, __) => Retry(PendingAction.ClearRange));
            _matchingStatus = LabelAt(MatchingStatus(state.OperationMode == OperationMode.SplitDwg ? state.MatchingNamedFrameCount : state.MatchingFrameCount), 250, 130, 478, 24);

            common.Controls.AddRange(new Control[] {
                _frameBlock, _framePolyline, _frameCustom, _autoLayer, selectTemplate,
                LabelAt("目前樣板：", 250, 51, 76, 20), _templateText,
                LabelAt("搜尋圖層：", 250, 78, 76, 20), _layerText,
                selectFrames, _selectedCount, useAllFrames, setRange, clearRange, _rangeStatus, _matchingStatus
            });

            _tabs = new TabControl { Location = new Point(8, 190), Size = new Size(744, 354) };
            var pdfPage = new TabPage("輸出 PDF");
            var dwgPage = new TabPage("拆分 DWG");
            _tabs.TabPages.AddRange(new[] { pdfPage, dwgPage });

            var outputGroup = Group("PDF 檔案輸出方式", 8, 8, 246, 72);
            _separatePdf = Radio("每張圖紙輸出成個別 PDF 檔案", 10, 20, 226, state.OutputMode == OutputMode.SeparatePdf);
            _mergedPdf = Radio("全部圖紙合併成一個多頁 PDF", 10, 45, 226, state.OutputMode == OutputMode.MergedPdf);
            outputGroup.Controls.AddRange(new Control[] { _separatePdf, _mergedPdf });

            var printGroup = Group("PDF 頁面設定", 262, 8, 466, 144);
            printGroup.Controls.Add(LabelAt("PDF 輸出裝置：", 10, 22, 90, 20));
            _device = Combo(state.AvailableDevices, 104, 18, 352, 23, Math.Max(0, state.AvailableDevices.IndexOf(state.Device)));
            printGroup.Controls.Add(_device);
            printGroup.Controls.Add(LabelAt("紙張大小：", 10, 49, 90, 20));
            _paper = Combo(new[] { "A4", "A3", "A2", "A1", "A0" }, 104, 45, 352, 23, IndexOf(new[] { "A4", "A3", "A2", "A1", "A0" }, state.Paper));
            printGroup.Controls.Add(_paper);
            printGroup.Controls.Add(LabelAt("出圖樣式：", 10, 76, 90, 20));
            var styleLabels = state.AvailablePlotStyles.ConvertAll(value => string.IsNullOrEmpty(value) ? "無" : value);
            _style = Combo(styleLabels, 104, 72, 352, 23, Math.Max(0, state.AvailablePlotStyles.IndexOf(state.PlotStyle)));
            printGroup.Controls.Add(_style);
            printGroup.Controls.Add(LabelAt("每張份數：", 10, 103, 90, 20));
            _copies = new NumericUpDown { Location = new Point(104, 99), Size = new Size(58, 23), Minimum = 1, Maximum = 99, Value = Math.Max(1, state.Copies) };
            printGroup.Controls.Add(_copies);

            var pdfFileGroup = Group("PDF 儲存設定", 8, 86, 246, 126);
            pdfFileGroup.Controls.Add(LabelAt("儲存資料夾：", 10, 23, 90, 20));
            _pdfDirectory = TextAt(state.OutputDirectory, 10, 47, 158, 23, false);
            var browsePdf = Button("選擇...", 174, 46, 62, 24, BrowsePdfFolder);
            _mergedNameLabel = LabelAt("合併 PDF 檔名：", 10, 79, 104, 20);
            _mergedName = TextAt(state.MergedFileName, 116, 76, 120, 23, false);
            pdfFileGroup.Controls.AddRange(new Control[] { _pdfDirectory, browsePdf, _mergedNameLabel, _mergedName });
            _mergedName.Enabled = state.OutputMode == OutputMode.MergedPdf;
            _mergedNameLabel.Enabled = _mergedName.Enabled;
            _mergedPdf.CheckedChanged += (_, __) => { _mergedName.Enabled = _mergedPdf.Checked; _mergedNameLabel.Enabled = _mergedPdf.Checked; };

            var scaleGroup = Group("圖面縮放", 262, 158, 170, 86);
            _fit = Radio("自動配合紙張", 10, 20, 140, state.FitToPaper);
            var fixedScale = Radio("固定比例", 10, 48, 74, !state.FitToPaper);
            _scale = new NumericUpDown { Location = new Point(106, 46), Size = new Size(54, 23), Minimum = 1, Maximum = 100000, Value = (decimal)Math.Max(1, state.FixedScale), Enabled = !state.FitToPaper };
            _fit.CheckedChanged += (_, __) => _scale.Enabled = !_fit.Checked;
            scaleGroup.Controls.AddRange(new Control[] { _fit, fixedScale, LabelAt("1:", 90, 50, 18, 20), _scale });

            var sortGroup = Group("PDF 頁面排列順序", 438, 158, 290, 112);
            _sortSelection = Radio("依手動選取順序", 10, 20, 168, state.SortMode == SortMode.Selection);
            _sortHorizontal = Radio("逐列：左→右、上→下", 10, 47, 180, state.SortMode == SortMode.LeftRightTopBottom);
            _sortVertical = Radio("逐欄：上→下、左→右", 10, 74, 180, state.SortMode == SortMode.TopBottomLeftRight);
            _reverseOrder = Check("反轉順序", 198, 20, 80, state.ReverseOrder);
            sortGroup.Controls.AddRange(new Control[] { _sortSelection, _sortHorizontal, _sortVertical, _reverseOrder });

            var orientation = Group("頁面方向與位置", 262, 250, 466, 56);
            _autoRotate = Check("依圖框自動旋轉", 10, 23, 128, state.AutoRotate);
            _reverseOrientation = Check("再旋轉 180°", 148, 23, 106, state.ReverseOrientation);
            _centerPlot = Check("將圖框置中", 264, 23, 100, state.CenterPlot);
            orientation.Controls.AddRange(new Control[] { _autoRotate, _reverseOrientation, _centerPlot });
            pdfPage.Controls.AddRange(new Control[] { outputGroup, printGroup, pdfFileGroup, scaleGroup, sortGroup, orientation });

            var dwgRules = Group("拆分規則", 8, 8, 350, 186);
            dwgRules.Controls.AddRange(new Control[] {
                LabelAt("每個同名圖框各輸出一個 DWG。", 12, 24, 320, 22),
                LabelAt("檔名：圖名2-圖名1；同名自動加 _2、_3。", 12, 50, 320, 22),
                LabelAt("原點：圖框包圍框左下角設為 (0,0)。", 12, 76, 320, 22),
                LabelAt("內容：圖框內及與邊界相交的模型空間物件。", 12, 102, 326, 22),
                LabelAt("安全：來源物件只讀，不刪除也不移動。", 12, 128, 320, 22)
            });
            _dwgTestFirstTwo = Check("安全測試：這次只拆前 2 張", 12, 154, 230, state.DwgTestFirstTwo);
            dwgRules.Controls.Add(_dwgTestFirstTwo);

            var dwgOutput = Group("DWG 儲存設定", 366, 8, 362, 132);
            dwgOutput.Controls.Add(LabelAt("儲存資料夾：", 12, 26, 90, 20));
            _dwgDirectory = TextAt(state.DwgOutputDirectory, 12, 51, 270, 23, false);
            var browseDwg = Button("選擇...", 288, 50, 62, 24, BrowseDwgFolder);
            dwgOutput.Controls.AddRange(new Control[] { _dwgDirectory, browseDwg,
                LabelAt("完成後會在資料夾建立 BatchWBlock-log.txt。", 12, 85, 330, 22) });

            var dwgNotice = Group("使用限制", 366, 148, 362, 98);
            dwgNotice.Controls.AddRange(new Control[] {
                LabelAt("• 僅支援帶有「圖名1」或「圖名2」屬性的圖框圖塊。", 12, 24, 338, 22),
                LabelAt("• 旋轉圖框會略過並寫入紀錄，不會輸出錯誤範圍。", 12, 49, 338, 22),
                LabelAt("• 建議先保留安全測試，確認 2 張後再批次處理。", 12, 74, 338, 22)
            });
            dwgPage.Controls.AddRange(new Control[] { dwgRules, dwgOutput, dwgNotice });

            _start = Button("開始輸出 PDF", 430, 560, 120, 28, Accept);
            var cancel = Button("取消", 562, 560, 88, 28, (_, __) => { DialogResult = DialogResult.Cancel; Close(); });
            var help = Button("使用說明", 662, 560, 90, 28, ShowHelp);
            Controls.AddRange(new Control[] { common, _tabs, _start, cancel, help });
            AcceptButton = _start;
            CancelButton = cancel;

            _tabs.SelectedIndex = state.OperationMode == OperationMode.SplitDwg ? 1 : 0;
            _tabs.SelectedIndexChanged += (_, __) => UpdateOperationUi();
            UpdateOperationUi();
        }

        private void UpdateOperationUi()
        {
            var split = _tabs.SelectedIndex == 1;
            _start.Text = split ? "開始拆分 DWG" : "開始輸出 PDF";
            if (split && !_frameBlock.Checked) _frameBlock.Checked = true;
            _framePolyline.Enabled = !split;
            _frameCustom.Enabled = !split;
            _matchingStatus.Text = MatchingStatus(split ? _state.MatchingNamedFrameCount : _state.MatchingFrameCount);
        }

        private void FrameModeChanged(object? sender, EventArgs e)
        {
            var selected = sender as RadioButton;
            if (selected == null || !selected.Checked || _templateText == null) return;
            var mode = _frameBlock.Checked ? FrameMode.Block : (_framePolyline.Checked ? FrameMode.Polyline : FrameMode.Custom);
            if (mode == _state.FrameMode) return;
            _state.FrameMode = mode;
            _state.TemplateHandle = "";
            _state.TemplateLabel = "尚未指定";
            _state.ExplicitSheetHandles.Clear();
            _state.MatchingFrameCount = -1;
            _state.MatchingNamedFrameCount = -1;
            _templateText.Text = _state.TemplateLabel;
            _selectedCount.Text = SelectionStatus(0);
            _matchingStatus.Text = MatchingStatus(-1);
            if (_state.AutoLayer)
            {
                _state.LayerName = "由樣板自動帶入";
                _layerText.Text = _state.LayerName;
            }
        }

        private void Accept(object? sender, EventArgs e)
        {
            CaptureState();
            if (string.IsNullOrWhiteSpace(_state.TemplateHandle))
            {
                Warn("尚未選取圖框樣板。請先按「選取圖框樣板...」。");
                return;
            }
            if (!_state.AutoLayer && string.IsNullOrWhiteSpace(_state.LayerName))
            {
                Warn("搜尋圖層不可留白；如需搜尋所有圖層，請輸入 *。");
                return;
            }
            if (_state.OperationMode == OperationMode.Pdf && string.IsNullOrWhiteSpace(_state.OutputDirectory))
            {
                Warn("尚未指定 PDF 儲存資料夾。");
                return;
            }
            if (_state.OperationMode == OperationMode.Pdf && _state.SortMode == SortMode.Selection && _state.ExplicitSheetHandles.Count == 0)
            {
                Warn("若要依手動選取順序排列，請先按「指定要處理的圖框...」。");
                return;
            }
            if (_state.OperationMode == OperationMode.SplitDwg && string.IsNullOrWhiteSpace(_state.DwgOutputDirectory))
            {
                Warn("尚未指定 DWG 儲存資料夾。");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Retry(PendingAction action)
        {
            CaptureState();
            _state.PendingAction = action;
            DialogResult = DialogResult.Retry;
            Close();
        }

        private void CaptureState()
        {
            _state.OperationMode = _tabs.SelectedIndex == 1 ? OperationMode.SplitDwg : OperationMode.Pdf;
            _state.FrameMode = _frameBlock.Checked ? FrameMode.Block : (_framePolyline.Checked ? FrameMode.Polyline : FrameMode.Custom);
            _state.AutoLayer = _autoLayer.Checked;
            _state.LayerName = _layerText.Text.Trim();
            _state.OutputMode = _mergedPdf.Checked ? OutputMode.MergedPdf : OutputMode.SeparatePdf;
            _state.Device = Convert.ToString(_device.SelectedItem, CultureInfo.InvariantCulture) ?? "DWG To PDF.pc3";
            _state.Paper = Convert.ToString(_paper.SelectedItem, CultureInfo.InvariantCulture) ?? "A3";
            _state.PlotStyle = _style.SelectedIndex <= 0 ? "" : Convert.ToString(_style.SelectedItem, CultureInfo.InvariantCulture) ?? "";
            _state.Copies = (int)_copies.Value;
            _state.FitToPaper = _fit.Checked;
            _state.FixedScale = (double)_scale.Value;
            _state.SortMode = _sortSelection.Checked ? SortMode.Selection : (_sortVertical.Checked ? SortMode.TopBottomLeftRight : SortMode.LeftRightTopBottom);
            _state.ReverseOrder = _reverseOrder.Checked;
            _state.AutoRotate = _autoRotate.Checked;
            _state.ReverseOrientation = _reverseOrientation.Checked;
            _state.CenterPlot = _centerPlot.Checked;
            _state.OutputDirectory = _pdfDirectory.Text.Trim();
            _state.MergedFileName = _mergedName.Text.Trim();
            _state.DwgOutputDirectory = _dwgDirectory.Text.Trim();
            _state.DwgTestFirstTwo = _dwgTestFirstTwo.Checked;
        }

        private void BrowsePdfFolder(object? sender, EventArgs e) => BrowseFolder(_pdfDirectory, "選擇 PDF 儲存資料夾");
        private void BrowseDwgFolder(object? sender, EventArgs e) => BrowseFolder(_dwgDirectory, "選擇拆分 DWG 儲存資料夾");

        private void BrowseFolder(TextBox target, string description)
        {
            using (var dialog = new FolderBrowserDialog { Description = description, SelectedPath = target.Text })
                if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
        }

        private void ShowHelp(object? sender, EventArgs e)
        {
            var text = _tabs.SelectedIndex == 1
                ? "先選取帶有圖名屬性的圖框圖塊，設定範圍與資料夾，再按「開始拆分 DWG」。第一次建議只拆前 2 張。"
                : "先設定共用圖框條件，再設定 PDF 檔案、頁面與排列順序，最後按「開始輸出 PDF」。";
            MessageBox.Show(text, "批次輸出工具 Plus");
        }

        private static void Warn(string text) => MessageBox.Show(text, "無法開始", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        private static string SelectionStatus(int count) => count == 0 ? "目前：全部符合條件" : "目前：已指定 " + count + " 個";
        private static string RangeStatus(bool limited) => limited ? "搜尋範圍：已限制" : "搜尋範圍：整個模型空間";
        private static string MatchingStatus(int count) => count < 0
            ? "選取樣板後會顯示符合數量；PDF 與 DWG 共用以上條件。"
            : "符合目前條件：" + count + " 個圖框；PDF 與 DWG 共用以上條件。";
        private static GroupBox Group(string text, int x, int y, int width, int height) => new GroupBox { Text = text, Location = new Point(x, y), Size = new Size(width, height) };
        private static RadioButton Radio(string text, int x, int y, int width, bool check) => new RadioButton { Text = text, Location = new Point(x, y), Size = new Size(width, 21), Checked = check, AutoSize = false };
        private static CheckBox Check(string text, int x, int y, int width, bool check) => new CheckBox { Text = text, Location = new Point(x, y), Size = new Size(width, 21), Checked = check, AutoSize = false };
        private static Label LabelAt(string text, int x, int y, int width, int height) => new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), TextAlign = ContentAlignment.MiddleLeft };
        private static TextBox TextAt(string text, int x, int y, int width, int height, bool readOnly) => new TextBox { Text = text, Location = new Point(x, y), Size = new Size(width, height), ReadOnly = readOnly };
        private static ComboBox Combo(System.Collections.Generic.IList<string> items, int x, int y, int width, int height, int index)
        {
            var combo = new ComboBox { Location = new Point(x, y), Size = new Size(width, height), DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var item in items) combo.Items.Add(item);
            combo.SelectedIndex = items.Count == 0 ? -1 : Math.Max(0, Math.Min(index, items.Count - 1));
            return combo;
        }
        private static Button Button(string text, int x, int y, int width, int height, EventHandler click)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height) };
            button.Click += click;
            return button;
        }
        private static int IndexOf(string[] values, string value)
        {
            var index = Array.IndexOf(values, value);
            return index < 0 ? 1 : index;
        }
    }
}
