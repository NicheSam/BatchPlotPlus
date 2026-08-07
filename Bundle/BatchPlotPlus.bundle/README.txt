BatchPlotPlus 1.3.6

AutoCAD command: BATCHPLOTPLUS
PDF shortcut: BATCHPDF opens the same window on the PDF tab.
Legacy DWG shortcut: BATCHWB opens the same window on the Split DWG tab.
Supported baseline: AutoCAD 2023 or newer, Windows 64-bit.

The original protected BatchPlot.VLX is not modified or replaced.
The command opens a native .NET Framework 4.8 Traditional Chinese WinForms window with PDF and DWG tabs.
Supported frame sources: matching attributed blocks, rectangular closed polylines, or closed custom polylines.
Sheets may be limited by a two-corner range or an explicit selection.
Page order supports selection order, left-right/top-bottom, top-bottom/left-right, and reverse order.
PDF output can create one file per sheet or combine all sheets into one native multi-page document.
The Traditional Chinese UI distinguishes CAD frames, PDF pages, and PDF files and describes each option by its actual effect.

The Split DWG tab ports the BatchWBlock v1.2 workflow into the same .NET window:
- attributed block filenames use 圖名2-圖名1;
- each output origin is the frame bounding-box lower-left corner;
- crossing model-space objects are copied with Database.Wblock and the source drawing is not erased or moved;
- duplicate filenames receive _2, _3, and so on;
- a BatchWBlock-log.txt report is written;
- the safety-test option limits one run to the first two frames.
Attributed block filenames use 圖名2-圖名1 when those tags exist.

The component loads when AutoCAD starts and creates the Batch Plot Tools ribbon tab.
The ribbon contains Output PDF and Split DWG buttons; commands remain available as fallbacks.
Ribbon command handlers read the command from AutoCAD's RibbonButton event parameter.
Model-space frame extents are converted from WCS to the DCS coordinates required by AutoCAD's plot-window API.
Landscape frames use a 90-degree plot rotation when automatic rotation is enabled.
Selecting a plot style table enables PlotPlotStyles so CTB/STB overrides are applied to PDF output.
PDF plot-device and paper lists are refreshed once per batch rather than once per page.
The native progress dialog reports the current page and both PDF and DWG operations write per-item elapsed time to the command line.
No DCL, AutoLISP, external merger, or LISPSYS setting is required.
