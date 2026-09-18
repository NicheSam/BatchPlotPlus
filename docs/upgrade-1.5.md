# BatchPlotPlus 1.5.0 upgrade candidate

This is an upgrade of BatchPlotPlus, retaining its product code, bundle name and existing commands. It is not a new application. Source baseline: `43db0bf`; upgrade branch: `upgrade/1.5-font-tools`.

## User interface

The existing Ribbon gains one **字型管理** button (`BATCHFONTS`). Its window contains enable/disable, owned-cache cleanup, refresh, status and the last 100 log lines. It does not open automatically. Existing PDF and DWG commands remain unchanged.

The font module handles missing **BigFont SHX** references using the host-resolved `chineset.shx`. It does not claim to repair all TTF/SHX fonts, xrefs or DXF imports. Normal fonts take precedence. No font files are distributed in the release and no source DWG style is rewritten or saved.

## Fixes and boundaries

- Removed hardcoded 2023 / R24.2 / Traditional Chinese installation paths. Resolve fonts through AutoCAD and isolate generated caches by Windows user, host and profile.
- Remember enable/disable across restarts with atomic settings-file replacement. Disabling removes this module's support path; reopen existing drawings to refresh fonts already resolved in memory.
- SHA-256 ownership receipts prevent cache cleanup from deleting unowned or modified files. Cross-process file locking fails closed rather than blocking an open event. Real-font discovery and same-drawing small/big-font conflicts remove only owned aliases.
- Cache up to 64 DWG scans in memory, invalidated by last-write time plus length. Cold opens still require an extra DWG read; network latency remains a validation item. Files changed without either metadata value changing require restart or cache reset.
- Isolate font initialization/event/log failures from output commands. Detect the old standalone module to avoid competing hooks.
- Correct PDF `BeginDocument` copies from the number of pages to **1**, as required for file output. User-requested page copies remain handled by the existing frame expansion.
- Protect `BACKGROUNDPLOT` writes, restore on success/failure, read back results, preserve unrelated concurrent values and retain dual failure evidence.
- Dispose already-created plot resources if a later page fails validation. Reject paper-space execution before using the Model layout.
- Sanitize Windows reserved device filenames and fix progress arithmetic overflow.
- Transform WCS frame polygon points into UCS for DWG selection; use a temporary top view and restore the original view. Rotated-UCS actual split output remains a runtime validation gate.
- Limit loader registration to the manifest's R24.0-R24.3 / R25.0 range rather than any future R25 release. Retain a previous-bundle backup through post-deployment work.

## Installation and migration

Use the existing `InstallOrUpdate.bat` as the AutoCAD user, with all AutoCAD windows closed. The installer continues using the existing ProgramData BatchPlotPlus bundle and startup loader mechanism. Do not manually NETLOAD a new version into a process already holding the old DLL.

`MigrateCadFontAuto.ps1` recognizes the known standalone product code and exact loader path, backs up its registration, moves its bundle outside ApplicationPlugins, and removes its loader keys. Unknown products/paths stop migration. Backup files go to LocalAppData/BatchPlotPlus/MigrationBackups. Failure attempts registry and bundle restoration and reports remaining errors.

Existing legacy `FontFallbacks` files and support paths are **preserved**. The new GUI does not own them: disabling or clearing the new module does not remove those older aliases. Review those separately if complete legacy substitution removal is desired. No global security or autoload settings are weakened.

## Verification status (2026-09-18)

| Layer | Result |
|---|---|
| R24 .NET Framework 4.8 build | Passed against AutoCAD.NET 24.0 API |
| R25 .NET 8 build | Passed against AutoCAD.NET 25.0 API |
| Logic / fault checks | 74 passed on each runtime (40 existing/extended logic + 34 upgrade checks) |
| Bundle / command / UI contracts | Passed; these are static checks |
| Installer paths | Passed: spaces, parentheses, ampersand, Chinese, exclamation; missing manifest correctly rejected |
| AutoCAD 2023 font core | Two actual DWGs: scan, repeated-read cache, metadata invalidation, alias content hash and unchanged source bytes passed |
| Complete DLL in Core Console | Failed during loading/entry with 0xC00000FD; not considered a PDF or desktop result |
| Desktop GUI / PDF | AutoCAD 2023: installed 1.5.0 auto-load, real font form open/close, two-page PDF and independent page/text readback passed |
| Normal / failed plot restoration | BACKGROUNDPLOT restored in both cases; monitored live settings and documents matched the pre-test snapshot |
| DWG split | Rotated/translated UCS and output content still require runtime acceptance |
| Other releases | Official SDK/runtime matrix checked; 2021/2022/2024 and 2025 through Update 1.3 have build/document evidence only. 2025 Update 1.4+ uses .NET 10 per current Autodesk docs and remains unverified |
| First launch / Explorer double-click / profile switch | Implementation present where applicable; desktop validation pending |
| Chinese glyph appearance / text width / PDF line breaks | Pending representative visual checks |
| Network, encrypted/corrupt DWG, xrefs | Not certified; read errors are logged and opening is allowed to continue when the API returns an error |

The candidate was installed on 2026-09-18 after the user closed CAD. The old standalone font module was backed up and migrated. Desktop tests used a disposable drawing and the production installed DLL. Temporary trust paths and test template preferences were restored. Full registry comparison still contains startup defaults and UI layout differences; these were preserved rather than resetting the profile. The earlier stuck Core Console worker was terminated with user authorization.

The package remains an **upgrade candidate**, not an all-versions runtime-certified release. See [official documentation review](official-validation-1.5.md) for version, font, coordinate and output boundaries. The Codex `cad-environment-guard` skill remains separate from this in-process implementation.

## Reproducible checks

Run `BuildRelease.ps1`, then `python tools/test_upgrade_contract.py`. `FontRuntimeChecks` links the production scanner/cache sources without UI dependencies for isolated Core Console checks. `RuntimeChecks` is a desktop-capable integration harness; use only a disposable test drawing, as it creates test geometry. Neither harness is shipped in the bundle. `FontUiPreview` renders the real form with sample state without loading AutoCAD.

## API references

- [Autodesk BeginDocument: copies is 1 when plotting to file](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_PlottingServices_PlotEngine_BeginDocument_PlotInfo_string_object_int_modoptIsLong__MarshalAsUnmanagedType_U1__bool_string.html)
- [Autodesk 2025 application compatibility](https://help.autodesk.com/cloudhelp/2025/CHS/AutoCAD-Customization/files/GUID-D54B0935-1638-4F97-8B37-1EC3635A1E71.htm)
