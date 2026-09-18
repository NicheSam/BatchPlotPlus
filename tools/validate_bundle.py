from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
BUNDLE = ROOT / "Bundle" / "BatchPlotPlus.bundle"
MANIFEST = BUNDLE / "PackageContents.xml"
DLL_R24 = BUNDLE / "Contents" / "R24" / "BatchPlotPlus.AutoCAD.dll"
DLL_R25 = BUNDLE / "Contents" / "R25" / "BatchPlotPlus.AutoCAD.dll"
LEGACY_DLL = BUNDLE / "Contents" / "BatchPlotPlus.AutoCAD.dll"
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

    if manifest.attrib.get("AppVersion") != "1.5.0":
        raise ValueError("Manifest AppVersion is not 1.5.0")
    require(manifest_text, [
        'SeriesMin="R24.0" SeriesMax="R24.3"',
        'ModuleName="./Contents/R24/BatchPlotPlus.AutoCAD.dll"',
        'SeriesMin="R25.0" SeriesMax="R25.0"',
        'ModuleName="./Contents/R25/BatchPlotPlus.AutoCAD.dll"',
        'LoadOnAutoCADStartup="True" LoadOnCommandInvocation="True"',
        '<Commands GroupName="BatchPlotPlus.Commands">',
        '<Command Global="BATCHPLOTDIAG" Local="BATCHPLOTDIAG"',
    ], "manifest")
    if manifest_text.count('AppType=".Net"') != 2:
        raise ValueError("Manifest must contain exactly two version-routed .NET components")
    if "LoadReasons=" in manifest_text:
        raise ValueError("Manifest must use explicit startup and command load attributes, not ambiguous LoadReasons")
    require(project_text, [
        "<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>",
        "<Version>1.5.0</Version>",
        "<UseWindowsForms>true</UseWindowsForms>",
        "<UseWPF>true</UseWPF>",
        '<PackageReference Include="AutoCAD.NET" Version="24.0.0"',
        '<PackageReference Include="AutoCAD.NET" Version="25.0.1"',
    ], "project")
    require(plugin_text, ["IExtensionApplication", "InitializeRibbonSafely", "PluginDiagnostics.Write", 'CommandMethod("BATCHPLOTPLUS"', 'CommandMethod("BATCHPDF"', 'CommandMethod("BATCHWB"', 'CommandMethod("BATCHPLOTDIAG"', "AcApp.ShowModalDialog", "PendingAction.SelectTemplate", "PendingAction.SelectRange", "ResetDocumentSelection", "DwgSplitService.Execute", "AddAllowedClass(typeof(BlockReference)", "AddAllowedClass(typeof(Polyline)"], "command")
    require((ROOT / "BatchPlotPlus.AutoCAD" / "BatchPlotForm.cs").read_text(encoding="utf-8"), ["列印物件線粗", "列印物件透明度", "自動（依圖框）", "橫向", "直向", "PageOrientation"], "PDF settings UI")
    require((ROOT / "BatchPlotPlus.AutoCAD" / "PlotService.cs").read_text(encoding="utf-8"), ["ResolvePlotBehavior", "PrintLineweights", "PlotTransparency", "SetPlotRotation"], "plot settings wiring")
    require(ribbon_text, ["ComponentManager.ItemInitialized", "RibbonTab", "RibbonPanel", "RibbonButton", "BatchPlotPlus.RibbonTab", "BATCHPDF ", "BATCHWB ", "return true;", "parameter is RibbonButton button", "button.CommandParameter as string", "SendStringToExecute", "Ribbon tab created successfully.", "Deferred Ribbon creation failed."], "ribbon")
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
    require(logic_text, ["WildcardMatch", "GroupAndSort", "SafeFileName", "NumberedFileBase", "DwgFileBase", "ProgressPercent", "IsPlotStyleCompatible", "SelectCompatiblePlotStyle"], "batch logic")
    require(dwg_text, ["SelectCrossingPolygon", "Wblock(ids", "SaveAs(output", "DwgTestFirstTwo", "DwgFilePrefix", "DwgFileBase", "BatchWBlock-log.txt", "SetCurrentView(originalView)", "frameTimer.Elapsed"], "DWG split")
    if ".Where(frame => frame.HasDrawingName)" in dwg_text:
        raise ValueError("DWG split still excludes frames without drawing-name attributes")
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
    for label, dll in (("R24", DLL_R24), ("R25", DLL_R25)):
        if not dll.is_file() or dll.stat().st_size < 20_000:
            raise ValueError(f"{label} BatchPlotPlus.AutoCAD.dll is missing or unexpectedly small")
        if dll.read_bytes()[:2] != b"MZ":
            raise ValueError(f"{label} BatchPlotPlus.AutoCAD.dll is not a PE file")
    r24_bytes = DLL_R24.read_bytes()
    r25_bytes = DLL_R25.read_bytes()
    if b".NETFramework,Version=v4.8" not in r24_bytes or b".NETCoreApp,Version=v8.0" in r24_bytes:
        raise ValueError("R24 assembly does not target .NET Framework 4.8")
    if b".NETCoreApp,Version=v8.0" not in r25_bytes or b".NETFramework,Version=v4.8" in r25_bytes:
        raise ValueError("R25 assembly does not target .NET 8")
    if r24_bytes == r25_bytes:
        raise ValueError("R24 and R25 assemblies must be independently compiled")
    if LEGACY_DLL.exists():
        raise ValueError("Obsolete single-version DLL remains in the bundle root")
    print("Bundle validation passed")
    print(f"R24 DLL bytes: {DLL_R24.stat().st_size}")
    print(f"R25 DLL bytes: {DLL_R25.stat().st_size}")
    print("Build targets: AutoCAD 2021-2024 (.NET Framework 4.8), 2025 through Update 1.3 (.NET 8); 2025 Update 1.4+ host compatibility pending")
    print("UI: Traditional Chinese WinForms")
    print("Output: separate PDF, native multi-page PDF, and copy-safe split DWG")
    return 0


if __name__ == "__main__":
    sys.exit(main())
