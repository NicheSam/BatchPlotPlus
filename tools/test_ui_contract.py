from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
FORM = (ROOT / "BatchPlotPlus.AutoCAD" / "BatchPlotForm.cs").read_text(encoding="utf-8")
PLOT = (ROOT / "BatchPlotPlus.AutoCAD" / "PlotService.cs").read_text(encoding="utf-8")
DWG = (ROOT / "BatchPlotPlus.AutoCAD" / "DwgSplitService.cs").read_text(encoding="utf-8")
PLUGIN = (ROOT / "BatchPlotPlus.AutoCAD" / "Plugin.cs").read_text(encoding="utf-8")
MODELS = (ROOT / "BatchPlotPlus.AutoCAD" / "Models.cs").read_text(encoding="utf-8")
PREVIEW = (ROOT / "BatchPlotPlus.AutoCAD" / "PlotPreviewForm.cs").read_text(encoding="utf-8")
RIBBON = (ROOT / "BatchPlotPlus.AutoCAD" / "RibbonService.cs").read_text(encoding="utf-8")
DIAG = (ROOT / "BatchPlotPlus.AutoCAD" / "PluginDiagnostics.cs").read_text(encoding="utf-8")


def require(needle: str, text: str, message: str) -> None:
    if needle not in text:
        raise AssertionError(message)


def main() -> None:
    for label, action in (
        ("選取圖框樣板...", "PendingAction.SelectTemplate"),
        ("指定要處理的圖框...", "PendingAction.SelectSheets"),
        ("使用全部圖框", "PendingAction.ClearSheets"),
        ("設定搜尋範圍...", "PendingAction.SelectRange"),
        ("搜尋整個模型", "PendingAction.ClearRange"),
        ("輸出預覽", "PendingAction.PreviewPdf"),
    ):
        require(f'Button("{label}"', FORM, f"missing button: {label}")
        require(action, FORM, f"button is not connected: {label}")

    for needle, message in (
        ("BrowsePdfFolder", "PDF folder button is not connected"),
        ("BrowseDwgFolder", "DWG folder button is not connected"),
        ("Button(\"開始輸出 PDF\"", "start button is missing"),
        ("Button(\"取消\"", "cancel button is missing"),
        ("ShowHelp", "help button is not connected"),
        ("_tabs.SelectedIndexChanged", "tab switch is not connected"),
        ("_start.Text = split ? \"開始拆分 DWG\" : \"開始輸出 PDF\"", "primary action does not follow the active tab"),
        ("_state.PrintLineweights = _printLineweights.Checked", "lineweight selection is not captured"),
        ("_state.PlotTransparency = _plotTransparency.Checked", "transparency selection is not captured"),
        ("_state.PageOrientation", "orientation selection is not captured"),
        ("_state.PdfSortMode", "PDF order is not captured independently"),
        ("_state.DwgSortMode", "DWG order is not captured independently"),
        ("合併 PDF 檔名不可留白", "merged filename validation is missing"),
    ):
        require(needle, FORM, message)

    require("RibbonService.EnsureVisible();", PLUGIN, "commands do not self-heal the Ribbon tab")
    require("AcApp.Idle += OnIdle", RIBBON, "Ribbon startup retry does not subscribe to AutoCAD idle")
    require("MaxDeferredAttempts", RIBBON, "Ribbon startup retry has no bounded retry limit")
    require("Ribbon tab: " + "", DIAG, "diagnostics do not report Ribbon status")
    require("PlotService.ShowPreview", PLUGIN, "preview action does not call PlotService")
    require("internal static void ShowPreview", PLOT, "PDF preview service is missing")
    require("BuildPreviewItems", PLOT, "PDF preview data builder is missing")
    require("TryGetPaperMillimeters", PLOT, "preview does not use paper dimensions")
    require("OrientationLabel", PREVIEW, "preview form does not display orientation")
    require("ScaleLabel", PREVIEW, "preview form does not display scale")
    require("statusText", PREVIEW, "preview does not compute an orientation verdict")
    require("SourceOrientation", PREVIEW, "preview does not show source frame orientation")
    require("DrawLongEdgeArrow", PREVIEW, "preview does not mark the frame long edge")
    require("DrawTitleBlockHint", PREVIEW, "preview does not include a title-block direction hint")
    require("orientationMatches", PREVIEW, "preview does not compare frame and paper orientation")
    require("DrawContentPreview", PREVIEW, "preview does not draw frame content")
    require("ContentSegments", MODELS, "preview item does not carry content segments")
    require("BuildPreviewSources", PLOT, "plot preview does not collect model-space content")
    require("AddContentPreview(previewItem", PLOT, "preview item is not populated with frame content")
    require("NormalizePreviewBounds", PLOT, "preview entity bounds still use strict plot-window validation")
    require("InitialClientSize", PREVIEW, "preview window initial size is not screen-aware")
    require("PrintableMarginPixels", PREVIEW, "preview does not keep fit-to-paper close to printable area")
    require("ResolvePreviewSegmentColor", PREVIEW, "preview does not apply plot-style color behavior")
    require("ResolvePreviewColor", PLOT, "preview does not sample CAD entity or layer colors")
    require("MaxPreviewSegmentsPerSheet", PLOT, "content preview has no segment cap")
    require("settings.PrintLineweights = behavior.PrintLineweights", PLOT, "lineweight selection does not reach AutoCAD plot settings")
    require("settings.PlotTransparency = behavior.PlotTransparency", PLOT, "transparency selection does not reach AutoCAD plot settings")
    require("SetPlotRotation(settings, rotation)", PLOT, "orientation does not reach AutoCAD plot settings")
    require("settings.PlotPlotStyles = false", PLOT, "the no-style choice does not explicitly disable plot styles")
    require("state.PdfSortMode", PLOT, "PDF service does not use PDF order")
    require("state.DwgSortMode", DWG, "DWG service does not use DWG order")
    require("PDF 輸出失敗", PLUGIN, "PDF failure title is missing")
    require("DWG 拆分失敗", PLUGIN, "DWG failure title is missing")
    require("PdfSortMode", MODELS, "PDF order state is missing")
    require("DwgSortMode", MODELS, "DWG order state is missing")
    if "AutoRotate" in FORM + PLOT + MODELS:
        raise AssertionError("obsolete AutoRotate setting is still present")
    print("UI and workflow contract passed")


if __name__ == "__main__":
    main()
