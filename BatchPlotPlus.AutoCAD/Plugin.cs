using System;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BatchPlotPlus.AutoCAD
{
    public sealed class Plugin : IExtensionApplication
    {
        private static readonly PluginState State = new PluginState();

        public void Initialize()
        {
            InitializeRibbonSafely();
            try { FontService.Initialize(); }
            catch (System.Exception exception) { PluginDiagnostics.Write("Font module initialization failed; output commands remain available.", exception); }
        }

        public void Terminate()
        {
            try { FontService.Terminate(); }
            catch (System.Exception exception) { PluginDiagnostics.Write("Font module termination failed.", exception); }
            try
            {
                RibbonService.Terminate();
            }
            catch (System.Exception exception)
            {
                PluginDiagnostics.Write("Ribbon termination failed.", exception);
            }
        }

        [CommandMethod("BATCHFONTS", CommandFlags.Modal)]
        public void FontTools()
        {
            try { using (var form = new FontToolsForm()) AcApp.ShowModalDialog(form); }
            catch (System.Exception exception) { PluginDiagnostics.Write("Font tools failed.", exception); }
        }

        private static void InitializeRibbonSafely()
        {
            try
            {
                RibbonService.Initialize();
                PluginDiagnostics.Write("Plug-in initialized; Ribbon initialization did not throw.");
            }
            catch (System.Exception exception)
            {
                PluginDiagnostics.Write("Plug-in commands loaded, but Ribbon initialization failed.", exception);
            }
        }

        [CommandMethod("BATCHPLOTPLUS", CommandFlags.Modal)]
        public void BatchPlotPlus()
        {
            Run();
        }

        [CommandMethod("BATCHPDF", CommandFlags.Modal)]
        public void BatchPdf()
        {
            State.OperationMode = OperationMode.Pdf;
            Run();
        }

        [CommandMethod("BATCHWB", CommandFlags.Modal)]
        public void BatchWBlock()
        {
            State.OperationMode = OperationMode.SplitDwg;
            Run();
        }

        [CommandMethod("BATCHPLOTDIAG", CommandFlags.Modal)]
        public void DiagnoseInstallation()
        {
            RibbonService.EnsureVisible();
            var document = AcApp.DocumentManager.MdiActiveDocument;
            var report = PluginDiagnostics.BuildLoadedReport();
            PluginDiagnostics.Write("Loaded diagnostic requested.\n" + report);
            if (document != null)
                document.Editor.WriteMessage("\n" + report.Replace(Environment.NewLine, "\n"));
        }

        private static void Run()
        {
            RibbonService.EnsureVisible();
            var document = AcApp.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var documentKey = document.Database.FingerprintGuid.ToString();
            if (!string.Equals(State.DocumentKey, documentKey, StringComparison.OrdinalIgnoreCase))
                State.ResetDocumentSelection(documentKey);
            RefreshPlotChoices(document.Database);
            if (string.IsNullOrWhiteSpace(State.OutputDirectory))
                State.OutputDirectory = System.IO.Path.GetDirectoryName(document.Name) ?? "";
            if (string.IsNullOrWhiteSpace(State.DwgOutputDirectory))
                State.DwgOutputDirectory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(document.Name) ?? "", "SplitDWG");

            while (true)
            {
                RefreshFrameCount(document.Database);
                using (var form = new BatchPlotForm(State))
                {
                    var result = AcApp.ShowModalDialog(form);
                    if (result == DialogResult.Retry)
                    {
                        HandlePendingAction(document);
                        continue;
                    }
                    if (result != DialogResult.OK) return;
                }

                try
                {
                    if (State.OperationMode == OperationMode.SplitDwg) DwgSplitService.Execute(document, State);
                    else PlotService.Execute(document, State);
                }
                catch (System.Exception exception)
                {
                    var title = State.OperationMode == OperationMode.SplitDwg ? "DWG 拆分失敗" : "PDF 輸出失敗";
                    PluginDiagnostics.Write(title, exception);
                    MessageBox.Show(FormatExceptionMessage(exception), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }
        }

        private static string FormatExceptionMessage(System.Exception exception)
        {
            var message = exception.Message;
            var inner = exception.InnerException;
            while (inner != null)
            {
                if (!string.IsNullOrWhiteSpace(inner.Message) &&
                    message.IndexOf(inner.Message, StringComparison.OrdinalIgnoreCase) < 0)
                    message += Environment.NewLine + "\u539f\u56e0\uff1a" + inner.Message;
                inner = inner.InnerException;
            }
            return message;
        }

        private static void RefreshPlotChoices(Database database)
        {
            try
            {
                var validator = PlotSettingsValidator.Current;
                State.AvailableDevices.Clear();
                foreach (var item in validator.GetPlotDeviceList())
                {
                    var name = item as string;
                    if (!string.IsNullOrWhiteSpace(name) && name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                        State.AvailableDevices.Add(name);
                }
                if (State.AvailableDevices.Count == 0) State.AvailableDevices.Add("DWG To PDF.pc3");
                if (!State.AvailableDevices.Contains(State.Device))
                    State.Device = State.AvailableDevices.Contains("DWG To PDF.pc3") ? "DWG To PDF.pc3" : State.AvailableDevices[0];
                State.AvailablePlotStyles.Clear();
                State.AvailablePlotStyles.Add("");
                foreach (var item in validator.GetPlotStyleSheetList())
                {
                    var name = item as string;
                    if (!string.IsNullOrWhiteSpace(name) &&
                        BatchLogic.IsPlotStyleCompatible(database.PlotStyleMode, name) &&
                        !State.AvailablePlotStyles.Contains(name)) State.AvailablePlotStyles.Add(name);
                }
                State.PlotStyle = BatchLogic.SelectCompatiblePlotStyle(database.PlotStyleMode, State.PlotStyle, State.AvailablePlotStyles);
            }
            catch
            {
                State.AvailableDevices.Clear();
                State.AvailableDevices.Add("DWG To PDF.pc3");
                State.AvailablePlotStyles.Clear();
                State.AvailablePlotStyles.Add("");
                State.PlotStyle = "";
            }
        }

        private static void RefreshFrameCount(Database database)
        {
            State.MatchingFrameCount = -1;
            State.MatchingNamedFrameCount = -1;
            if (string.IsNullOrWhiteSpace(State.TemplateHandle)) return;
            try
            {
                var frames = PlotService.CollectFrames(database, State);
                State.MatchingFrameCount = frames.Count;
                State.MatchingNamedFrameCount = frames.FindAll(frame => frame.HasDrawingName).Count;
            }
            catch { State.MatchingFrameCount = -1; }
        }

        private static void HandlePendingAction(Autodesk.AutoCAD.ApplicationServices.Document document)
        {
            var editor = document.Editor;
            var database = document.Database;
            var action = State.PendingAction;
            State.PendingAction = PendingAction.None;
            if (action == PendingAction.ClearRange)
            {
                State.Range = null;
            }
            else if (action == PendingAction.ClearSheets)
            {
                State.ExplicitSheetHandles.Clear();
            }
            else if (action == PendingAction.PreviewPdf)
            {
                try
                {
                    PlotService.ShowPreview(document, State);
                }
                catch (System.Exception exception)
                {
                    PluginDiagnostics.Write("輸出預覽失敗", exception);
                    MessageBox.Show(FormatExceptionMessage(exception), "輸出預覽失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (action == PendingAction.SelectTemplate)
            {
                var prompt = State.FrameMode == FrameMode.Block
                    ? "\n請選取一個代表性的圖框圖塊："
                    : State.FrameMode == FrameMode.Polyline
                        ? "\n請選取一個四邊直線且封閉的圖框聚合線："
                        : "\n請選取一個封閉的圖框聚合線：";
                var options = new PromptEntityOptions(prompt);
                if (State.FrameMode == FrameMode.Block)
                {
                    options.SetRejectMessage("\n這個模式只接受圖塊物件。");
                    options.AddAllowedClass(typeof(BlockReference), false);
                }
                else
                {
                    options.SetRejectMessage("\n這個模式只接受聚合線物件。");
                    options.AddAllowedClass(typeof(Polyline), false);
                }
                var result = editor.GetEntity(options);
                if (result.Status != PromptStatus.OK) return;
                if (!PlotService.TryValidateTemplate(database, result.ObjectId, State.FrameMode, out var error))
                {
                    editor.WriteMessage("\n" + error);
                    return;
                }
                State.TemplateHandle = result.ObjectId.Handle.ToString();
                State.TemplateLabel = PlotService.DescribeTemplate(database, result.ObjectId, out var layer);
                if (State.AutoLayer) State.LayerName = layer;
            }
            else if (action == PendingAction.SelectRange)
            {
                var first = editor.GetPoint("\n指定圖框搜尋範圍的第一個角點：");
                if (first.Status != PromptStatus.OK) return;
                var secondOptions = new PromptCornerOptions("\n指定搜尋範圍的另一個角點：", first.Value);
                var second = editor.GetCorner(secondOptions);
                if (second.Status != PromptStatus.OK) return;
                State.Range = new RangeBounds(
                    Math.Min(first.Value.X, second.Value.X), Math.Min(first.Value.Y, second.Value.Y),
                    Math.Max(first.Value.X, second.Value.X), Math.Max(first.Value.Y, second.Value.Y));
            }
            else if (action == PendingAction.SelectSheets)
            {
                editor.WriteMessage(State.OperationMode == OperationMode.SplitDwg
                    ? "\n請選取這次要拆分的圖框；按 Enter 完成："
                    : "\n請依照希望的 PDF 頁面順序選取圖框；按 Enter 完成：");
                var result = editor.GetSelection();
                if (result.Status != PromptStatus.OK) return;
                State.ExplicitSheetHandles.Clear();
                foreach (var id in result.Value.GetObjectIds()) State.ExplicitSheetHandles.Add(id.Handle.ToString());
            }
        }
    }
}
