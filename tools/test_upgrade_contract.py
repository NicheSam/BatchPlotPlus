from pathlib import Path
import hashlib
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
plot = (root/'BatchPlotPlus.AutoCAD/PlotService.cs').read_text(encoding='utf-8')
# Output behavior is frozen to the user-approved 1.4.4 baseline.
# Digests are generated from Git commit 43db0bf, normalizing line endings only.
import json
baseline = json.loads((root/'tools/output-baseline.json').read_text(encoding='utf-8'))
for relative, digest in baseline['sha256'].items():
    content = (root/relative).read_text(encoding='utf-8')
    assert hashlib.sha256(content.encode('utf-8')).hexdigest() == digest, 'Output behavior changed: ' + relative
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
