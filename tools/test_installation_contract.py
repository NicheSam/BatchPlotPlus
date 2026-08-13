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
        require(entry.attrib.get("LoadReasons") == "LoadOnAutoCADStartup", "official startup load reason is missing")
        require("LoadOnAutoCADStartup" not in entry.attrib, "startup load reason is written as a nonstandard standalone attribute")
        require("LoadOnCommandInvocation" not in entry.attrib, "command load reason is written as a nonstandard standalone attribute")
        commands = {item.attrib.get("Global") for item in entry.findall("./Commands/Command")}
        require(
            {"BATCHPLOTPLUS", "BATCHPDF", "BATCHWB", "BATCHPLOTDIAG"}.issubset(commands),
            "manifest command declarations are incomplete",
        )

    installer_bat = (ROOT / "InstallOrUpdate.bat").read_text(encoding="utf-8")
    require('InstallOrUpdate.ps1"' in installer_bat, "BAT wrapper does not delegate to PowerShell")
    require("%SOURCE%" not in installer_bat, "BAT wrapper still expands source paths inside cmd blocks")

    installer_path = ROOT / "InstallOrUpdate.ps1"
    require(installer_path.is_file(), "PowerShell installer is missing")
    installer = installer_path.read_text(encoding="utf-8")
    require('Join-Path $env:ProgramData "Autodesk\\ApplicationPlugins"' in installer, "installer target is not ProgramData")
    require("-Verb RunAs" in installer and "-Wait" in installer and "-PassThru" in installer, "elevated process result is not awaited")
    require("$elevatedProcess = Start-Process" in installer, "UAC process variable can collide with the Elevated switch parameter")
    require("$elevatedProcess.ExitCode" in installer, "elevated exit code is not returned")
    require("$elevated = Start-Process" not in installer, "PowerShell's case-insensitive Elevated variable collision is present")
    require("BatchPlotPlus-install.log" in installer, "installer has no fixed diagnostic log")
    require("Backup-AndRemoveLegacyLoaders" in installer, "stale Loader cleanup is missing")
    require("reg.exe" in installer and "export" in installer, "Loader cleanup has no registry backup")
    require("runtime verification is pending" in installer, "installer overclaims AutoCAD runtime success")
    require("ValidateOnly" in installer, "installer has no non-mutating path validation mode")
    require("Verify-BundleUnblocked" in installer, "source, staged, and deployed files are not verified")
    require("Restore-PreviousBundle" in installer, "failed activation cannot restore the previous bundle")

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
    require("InstallOrUpdate.ps1" in build, "PowerShell installer is not included in installer packaging")
    require("test_installer_paths.ps1" in build, "special-character installer path tests are not part of the release build")

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
