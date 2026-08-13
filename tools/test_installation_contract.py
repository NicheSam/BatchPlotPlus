from pathlib import Path
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    manifest_path = ROOT / "Bundle" / "BatchPlotPlus.bundle" / "PackageContents.xml"
    manifest = ET.parse(manifest_path).getroot()
    entries = manifest.findall("./Components/ComponentEntry")
    require(len(entries) == 2, "expected two version-routed components")
    for entry in entries:
        require(entry.attrib.get("LoadOnAutoCADStartup") == "True", "startup load flag is missing")
        require(entry.attrib.get("LoadOnCommandInvocation") == "True", "command fallback cannot trigger loading")
        require("LoadReasons" not in entry.attrib, "nonstandard LoadReasons attribute is still present")
        commands = {item.attrib.get("Global") for item in entry.findall("./Commands/Command")}
        require(
            {"BATCHPLOTPLUS", "BATCHPDF", "BATCHWB", "BATCHPLOTDIAG"}.issubset(commands),
            "manifest command declarations are incomplete",
        )

    installer = (ROOT / "InstallOrUpdate.bat").read_text(encoding="utf-8")
    require("%ProgramFiles%\\Autodesk\\ApplicationPlugins" in installer, "installer target is not implicitly trusted")
    require("-Verb RunAs" in installer, "installer does not self-elevate for the trusted target")
    require('VerifyUnblocked.ps1" -Path "%SOURCE%" -Repair' in installer, "source files are not verified")
    require('VerifyUnblocked.ps1" -Path "%STAGE%" -Repair' in installer, "staged files are not verified")
    require('VerifyUnblocked.ps1" -Path "%TARGET%" -Repair' in installer, "deployed files are not verified")
    require("if errorlevel 1 goto :source_blocked" in installer, "source unblock failure is not fatal")
    require("if errorlevel 1 goto :stage_blocked" in installer, "staged block is not fatal")
    require("if errorlevel 1 goto :target_blocked" in installer, "deployed block is not fatal")
    require("if errorlevel 1 goto :target_verify_failed" in installer, "deployed integrity failure is not fatal")
    require("move \"%BACKUP%\" \"%TARGET%\"" in installer, "failed activation cannot restore the previous bundle")

    verifier = (ROOT / "VerifyUnblocked.ps1").read_text(encoding="utf-8")
    require("Get-ChildItem -LiteralPath $Path -Recurse -File" in verifier, "verifier does not enumerate every bundle file")
    require("Unblock-File -LiteralPath $file.FullName -ErrorAction Stop" in verifier, "per-file unblock failure is ignored")
    require("-Stream * -ErrorAction Stop" in verifier, "alternate data stream inspection can fail open")
    require('$streams.Stream -contains "Zone.Identifier"' in verifier, "verifier does not check Zone.Identifier")
    require("exit 22" in verifier, "blocked files do not fail verification")

    plugin = (ROOT / "BatchPlotPlus.AutoCAD" / "Plugin.cs").read_text(encoding="utf-8")
    require("InitializeRibbonSafely" in plugin, "Ribbon failure can still abort plug-in initialization")
    require('CommandMethod("BATCHPLOTDIAG"' in plugin, "loaded plug-in has no diagnostic command")
    assembly_info = (ROOT / "BatchPlotPlus.AutoCAD" / "AssemblyInfo.cs").read_text(encoding="utf-8")
    require("ExtensionApplication(typeof(BatchPlotPlus.AutoCAD.Plugin))" in assembly_info, "extension class is not explicit")
    require("CommandClass(typeof(BatchPlotPlus.AutoCAD.Plugin))" in assembly_info, "command class is not explicit")

    diagnostic = ROOT / "DiagnoseInstallation.ps1"
    require(diagnostic.is_file(), "standalone installation diagnostic is missing")
    build = (ROOT / "BuildRelease.ps1").read_text(encoding="utf-8")
    require("DiagnoseInstallation.ps1" in build, "diagnostic is not included in installer packaging")
    require("VerifyUnblocked.ps1" in build, "blocked-file verifier is not included in installer packaging")

    diagnostic_text = diagnostic.read_text(encoding="utf-8")
    require("All $($bundleFiles.Count) bundle files are free of Zone.Identifier." in diagnostic_text, "diagnostic does not report full-bundle verification")
    require('Add-Fail "Blocked bundle file:' in diagnostic_text, "diagnostic treats blocked files as non-fatal")

    print("Installation contract passed")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except AssertionError as error:
        print(f"INSTALLATION_CONTRACT_FAILED: {error}")
        sys.exit(1)
