from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
plot = (root/'BatchPlotPlus.AutoCAD/PlotService.cs').read_text(encoding='utf-8')
assert 'null, 1, true, outputPath' in plot, 'PDF BeginDocument must use one device copy'
assert plot.index('var plotInfos = new List<PlotInfo>()') < plot.index('foreach (var frame in frames) plotInfos.Add(')
assert 'TemporarySetting.Run(' in plot and 'document.Database.TileMode' in plot
font = (root/'BatchPlotPlus.AutoCAD/FontService.cs').read_text(encoding='utf-8')
assert 'AutoCAD 2023' not in font and 'R24.2' not in font and '"cht"' not in font
assert 'File.Replace(pending, SettingsPath, null)' in font
assert 'Scans.Read(path)' in font and 'FindFileHint.FontFile' in font
assert 'Legacy CadFontAuto' in font
scan = (root/'BatchPlotPlus.AutoCAD/FontScanCache.cs').read_text(encoding='utf-8')
assert 'scans.Count >= 64' in scan and 'SaveAs' not in scan
installer=(root/'InstallOrUpdate.ps1').read_text(encoding='utf-8')
assert '$seriesKey.PSChildName -in @("R24.0", "R24.1", "R24.2", "R24.3", "R25.0")' in installer
assert 'MigrateCadFontAuto.ps1' in installer
manifest=ET.parse(root/'Bundle/BatchPlotPlus.bundle/PackageContents.xml').getroot()
assert manifest.get('ProductCode')=='{048F6E30-0EC6-4CE2-8F39-AB52B538411A}'
for entry in manifest.findall('./Components/ComponentEntry'):
    assert entry.find('./Commands/Command[@Global="BATCHFONTS"]') is not None
print('Upgrade integration contract passed (static wiring, not runtime proof)')
