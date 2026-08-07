from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
BUNDLE = ROOT / "Bundle" / "BatchPlotPlus.bundle"
MANIFEST = BUNDLE / "PackageContents.xml"
DLL = BUNDLE / "Contents" / "BatchPlotPlus.AutoCAD.dll"
PROJECT = ROOT / "BatchPlotPlus.AutoCAD" / "BatchPlotPlus.AutoCAD.csproj"
FORM = ROOT / "BatchPlotPlus.AutoCAD" / "BatchPlotForm.cs"
PLUGIN = ROOT / "BatchPlotPlus.AutoCAD" / "Plugin.cs"
PLOT = ROOT / "BatchPlotPlus.AutoCAD" / "PlotService.cs"
LOGIC = ROOT / "BatchPlotPlus.AutoCAD" / "BatchLogic.cs"
DWG = ROOT / "BatchPlotPlus.AutoCAD" / "DwgSplitService.cs"
RIBBON = ROOT / "BatchPlotPlus.AutoCAD" / "RibbonService.cs"


def require(text: str, tokens: list[str], source: str) -> None:
    for token in tokens:
        if token not in text:
            raise ValueError(f"Missing {source} token: {token}")


def main() -> int:
    manifest_text = MANIFEST.read_text(encoding="utf-8")
    manifest = ET.parse(MANIFEST).getroot()
    project_text = PROJECT.read_text(encoding="utf-8")
    form_text = FORM.read_text(encoding="utf-8")
    plugin_text = PLUGIN.read_text(encoding="utf-8")
    plot_text = PLOT.read_text(encoding="utf-8")
    logic_text = LOGIC.read_text(encoding="utf-8")
    dwg_text = DWG.read_text(encoding="utf-8")
    ribbon_text = RIBBON.read_text(encoding="utf-8")

    if manifest.attrib.get("AppVersion") != "1.3.6":
        raise ValueError("Manifest AppVersion is not 1.3.6")
    require(manifest_text, ['AppType=".Net"', 'ModuleName="./Contents/BatchPlotPlus.AutoCAD.dll"', 'LoadReasons="LoadOnAutoCADStartup"'], "manifest")
    if "<Commands" in manifest_text or "<Command " in manifest_text or "LoadOnCommandInvocation" in manifest_text:
        raise ValueError("Startup-loaded component must not include command-invocation manifest entries")
    require(project_text, ["<TargetFramework>net48</TargetFramework>", "<Version>1.3.6</Version>", "<UseWindowsForms>true</UseWindowsForms>", "<Reference Include=\"AdWindows\">"], "project")
    require(plugin_text, ["IExtensionApplication", "RibbonService.Initialize", 'CommandMethod("BATCHPLOTPLUS"', 'CommandMethod("BATCHPDF"', 'CommandMethod("BATCHWB"', "AcApp.ShowModalDialog", "PendingAction.SelectTemplate", "PendingAction.SelectRange", "ResetDocumentSelection", "DwgSplitService.Execute", "AddAllowedClass(typeof(BlockReference)", "AddAllowedClass(typeof(Polyline)"], "command")
    require(ribbon_text, ["ComponentManager.ItemInitialized", "RibbonTab", "RibbonPanel", "RibbonButton", "BatchPlotPlus.RibbonTab", "BATCHPDF ", "BATCHWB ", "return true;", "parameter is RibbonButton button", "button.CommandParameter as string", "SendStringToExecute"], "ribbon")
    parameter_index = ribbon_text.index("CommandParameter = command")
    handler_index = ribbon_text.index("button.CommandHandler = CommandHandler")
    if parameter_index >= handler_index or "CommandHandler = CommandHandler," in ribbon_text:
        raise ValueError("Ribbon command parameter must be assigned before the command handler")
    require(form_text, ["批次輸出工具 Plus", "TabControl", "輸出 PDF", "拆分 DWG", "共用圖框條件", "全部圖紙合併成一個多頁 PDF", "指定要處理的圖框", "安全測試：這次只拆前 2 張", "CenterPlot", "LayerName"], "UI")
    for phrase in ("打印", "保存位置", "自動居中", "布滿圖紙", "輸出選項", "選擇要處理的圖紙"):
        if phrase in form_text or phrase in plugin_text or phrase in plot_text:
            raise ValueError(f"Obsolete or unclear UI wording remains: {phrase}")
    require(plot_text, ["CreatePublishEngine", "BeginDocument", "BeginPage", "ResolveMedia", "SheetSetProgressCaption", "SheetProgressCaption", "ProgressPercent", "PDF 輸出總耗時", "圖名1", "圖名2", "Matrix3d.PlaneToWorld", "worldToDisplay.Inverse", "plotExtents.TransformBy(worldToDisplay)", "SetPlotWindowArea", "SetCustomPrintScale", "SetPlotCentered(settings, state.CenterPlot)", "settings.PlotPlotStyles = true", "BeginMonochromeOverride", "monochromeOverride?.Dispose()"], "plot engine")
    if plot_text.count("validator.RefreshLists(settings);") != 1:
        raise ValueError("Expensive plot-device list refresh must occur exactly once per batch")
    if plot_text.count("ResolveMedia(document.Database, state)") != 1:
        raise ValueError("Plot media must be resolved exactly once in the batch entry point")
    require(plot_text, ["PlotPages(document, state, frames, output, media)", "PlotPages(document, state, new List<FrameInfo> { frame }, output, media)"], "shared plot media")
    require(logic_text, ["WildcardMatch", "GroupAndSort", "SafeFileName", "ProgressPercent", "IsPlotStyleCompatible", "SelectCompatiblePlotStyle"], "batch logic")
    require(dwg_text, ["SelectCrossingPolygon", "Wblock(ids", "SaveAs(output", "DwgTestFirstTwo", "BatchWBlock-log.txt", "SetCurrentView(originalView)", "frameTimer.Elapsed"], "DWG split")
    if ".Erase(" in dwg_text or "Erase(true" in dwg_text:
        raise ValueError("DWG split must not erase source entities")
    if ".Enabled = false" in form_text or re.search(r"Button\([^\n]+, null\)", form_text):
        raise ValueError("Visible UI still contains disabled or no-op controls")
    if "catch { }" in plot_text:
        raise ValueError("Plot engine still silently ignores an error")
    if 'AppType=".lsp"' in manifest_text or "BatchPlotPlus.dcl" in manifest_text:
        raise ValueError("Legacy LISP/DCL component is still active in the manifest")
    for text in (project_text, form_text, plugin_text, plot_text, logic_text, dwg_text, ribbon_text, manifest_text):
        for marker in ("\ufffd", "Ã", "Â", "å¤", "å¥", "ä¸"):
            if marker in text:
                raise ValueError(f"Possible mojibake marker: {marker}")
    if not DLL.is_file() or DLL.stat().st_size < 20_000:
        raise ValueError("BatchPlotPlus.AutoCAD.dll is missing or unexpectedly small")
    if DLL.read_bytes()[:2] != b"MZ":
        raise ValueError("BatchPlotPlus.AutoCAD.dll is not a PE file")
    print("Bundle validation passed")
    print(f"DLL bytes: {DLL.stat().st_size}")
    print("UI: Traditional Chinese WinForms")
    print("Output: separate PDF, native multi-page PDF, and copy-safe split DWG")
    return 0


if __name__ == "__main__":
    sys.exit(main())
