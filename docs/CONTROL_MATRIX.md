# BatchPlotPlus 1.4.21 control-to-behavior and wording review

Every enabled input below has a state write and a downstream consumer. Informational labels are marked as display-only. Unsupported legacy controls were removed instead of being left disabled or unconnected.

The common frame conditions are shared by both tabs. The PDF tab owns page/file settings; the Split DWG tab owns its destination, fallback filename prefix, sequence order, and safety-test limit.

| Window item | State/action | Downstream behavior | Review |
|---|---|---|---|
| AutoCAD ribbon: Output PDF | Reads `BATCHPDF` from `RibbonButton.CommandParameter` | Opens the integrated window on the PDF tab | Connected |
| AutoCAD ribbon: Split DWG | Reads `BATCHWB` from `RibbonButton.CommandParameter` | Opens the integrated window on the Split DWG tab | Connected |
| Polyline / block / custom frame | `FrameMode` | Block matches the same block name; polyline requires a four-sided straight rectangle; custom accepts other closed polylines on the chosen layer | Connected |
| Auto-select layer | `AutoLayer` | Uses the selected template layer; otherwise enables an editable wildcard layer rule | Connected |
| Select block or layer | `SelectTemplate` | Prompts AutoCAD for the correct entity type and stores its handle | Connected |
| Template text | Display-only | Shows the chosen block or polyline | Informational |
| Layer text | `LayerName` | Exact or `*` / `?` wildcard filter in `WildcardMatch` | Connected |
| Each sheet as an individual PDF / all sheets as one multi-page PDF | `OutputMode` | Chooses one PDF file per frame or one native multi-page PDF document | Connected; wording states the file result |
| Specify frames to output | `SelectSheets` | Limits candidates to the explicit frame selection and preserves selection order | Connected |
| Use all frames | Clears handles | Returns frame discovery to all matching model-space candidates | Connected |
| PDF device | `Device` | Passed to `SetPlotConfigurationName`; list comes from AutoCAD and is PDF-filtered | Connected |
| Paper size | `Paper` | Resolves a matching canonical media name | Connected |
| Plot style | `PlotStyle` | Applies CTB/STB; a failure is reported instead of ignored | Connected |
| Copies | `Copies` | Repeats pages/files in the requested count | Connected |
| Range status | Display-only | Shows whether the model-space range filter is active | Informational |
| Auto-fit paper / fixed scale | `FitToPaper`, `FixedScale` | Uses `ScaleToFit` or `CustomScale` | Connected |
| Manual selection order | `SortMode.Selection` | Uses explicit selection order; blocked if no selection exists | Connected |
| By row: left to right, top to bottom | `SortMode.LeftRightTopBottom` | Groups rows with tolerance, then sorts each row left to right | Connected |
| By column: top to bottom, left to right | `SortMode.TopBottomLeftRight` | Groups columns with tolerance, then sorts each column top to bottom | Connected |
| Reverse order | `ReverseOrder` | Reverses the final PDF page sequence | Connected |
| Rotate to match frame | `AutoRotate` | Selects portrait/landscape rotation from frame proportions | Connected |
| Rotate another 180 degrees | `ReverseOrientation` | Adds 180 degrees to the selected orientation | Connected |
| Center the frame | `CenterPlot` | Controls `SetPlotCentered` | Connected |
| Output directory / browse | `OutputDirectory` | Creates the directory and writes output files there | Connected |
| Merged filename | `MergedFileName` | Used only for merged mode and disabled in separate mode | Connected |
| Set search range / search entire model | `Range` | Adds or removes an extents-intersection filter | Connected |
| Start PDF output | Validation + WCS-to-DCS plot-window conversion + execute | Requires template, directory, a manual layer rule when applicable, and explicit selection when manual order is chosen | Connected |
| Cancel | Dialog cancellation | Exits without plotting | Connected |
| Help | Guidance | Explains the supported flow | Connected |
| PDF / Split DWG tabs | `OperationMode` | Dispatches to `PlotService.Execute` or `DwgSplitService.Execute` | Connected |
| DWG output directory | `DwgOutputDirectory` | Creates and saves individual DWG files in this directory | Connected |
| DWG sequence prefix | `DwgFilePrefix` | Names frames without drawing-name attributes as prefix + sorted one-based index | Connected |
| DWG sequence order | `SortMode`, `ReverseOrder` | Applies manual, row-first, column-first, or reversed order before assigning fallback numbers | Connected |
| Test first two frames | `DwgTestFirstTwo` | Limits the sorted DWG frame list to two items | Connected |
| Start Split DWG | Validation + execute | Requires a matching block template, model space, and a DWG directory; drawing-name attributes are optional | Connected |

## Removed after review

- Preset configuration, Settings, Properties, Edit style.
- Print-to-file, archive split, generate layouts, print existing layouts.
- Multi-document, Preview.
- Direct implementation and layout-proportion placeholders.
- Block auto-match checkbox that had no state consumer.

These were visible but did not have complete behavior. Keeping them would have made the window misleading.

## Runtime gate

Static review, compilation, and offline UI rendering do not prove AutoCAD plotting behavior. The remaining runtime checks are: bundle command loading, block/polyline prompting, plot device/media resolution, one separate PDF sample, one 2-3 page merged PDF sample, page order, source drawing preservation, and plot-state restoration after success or failure.
