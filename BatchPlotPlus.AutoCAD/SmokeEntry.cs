#if BATCHPLOT_SMOKE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;

namespace BatchPlotPlus.AutoCAD
{
    public sealed class SmokeEntry
    {
        [CommandMethod("BATCHPLOTTEST", CommandFlags.Modal)]
        public void Run()
        {
            var log = Environment.GetEnvironmentVariable("BATCHPLOT_SMOKE_LOG");
            try
            {
                if (!string.IsNullOrWhiteSpace(log)) File.WriteAllText(log, "RUN_START\n");
                var document = Application.DocumentManager.MdiActiveDocument;
                if (document == null) throw new InvalidOperationException("No active document.");

            var output = Environment.GetEnvironmentVariable("BATCHPLOT_SMOKE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Path.GetTempPath(), "BatchPlotPlus-smoke.pdf");

            var state = new PluginState
            {
                Device = "DWG To PDF.pc3",
                Paper = "A3",
                PlotStyle = document.Database.PlotStyleMode ? "monochrome.ctb" : "monochrome.stb",
                FitToPaper = true,
                CenterPlot = true,
                PageOrientation = PageOrientation.Auto
            };
            var frames = new List<PlotService.FrameInfo>
            {
                new PlotService.FrameInfo
                {
                    Extents = new Extents3d(
                        new Autodesk.AutoCAD.Geometry.Point3d(1000000.0, 2000000.0, 0.0),
                        new Autodesk.AutoCAD.Geometry.Point3d(1001000.0, 2000700.0, 0.0)),
                    FileBase = "smoke"
                },
                new PlotService.FrameInfo
                {
                    Extents = new Extents3d(
                        new Autodesk.AutoCAD.Geometry.Point3d(1001500.0, 2000000.0, 0.0),
                        new Autodesk.AutoCAD.Geometry.Point3d(1002500.0, 2000700.0, 0.0)),
                    FileBase = "smoke-2"
                },
                new PlotService.FrameInfo
                {
                    Extents = new Extents3d(
                        new Autodesk.AutoCAD.Geometry.Point3d(1003000.0, 2000000.0, 0.0),
                        new Autodesk.AutoCAD.Geometry.Point3d(1004000.0, 2000700.0, 0.0)),
                    FileBase = "smoke-3"
                }
            };
                if (!string.IsNullOrWhiteSpace(log)) File.AppendAllText(log, "MODE=" + (document.Database.PlotStyleMode ? "CTB" : "STB") + ";STYLE=" + state.PlotStyle + "\n");
                if (!string.IsNullOrWhiteSpace(log) && !document.Database.PlotStyleMode)
                {
                    using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
                    {
                        var dictionary = (DBDictionary)transaction.GetObject(document.Database.PlotStyleNameDictionaryId, OpenMode.ForRead);
                        var styleNames = new List<string>();
                        foreach (DBDictionaryEntry entry in dictionary) styleNames.Add(entry.Key);
                        File.AppendAllText(log, "PLOT_STYLE_NAMES=" + string.Join(",", styleNames) + "\n");
                        var layouts = (DBDictionary)transaction.GetObject(document.Database.LayoutDictionaryId, OpenMode.ForRead);
                        var model = (Layout)transaction.GetObject(layouts.GetAt("Model"), OpenMode.ForRead);
                        File.AppendAllText(log, "CURRENT_LAYOUT=" + LayoutManager.Current.CurrentLayout + ";MODEL_TYPE=" + model.ModelType + ";MODEL_PLOT_TYPE=" + model.PlotType + "\n");
                    }
                }
                var media = PlotService.ResolveMedia(document.Database, state);
                if (!string.IsNullOrWhiteSpace(log)) File.AppendAllText(log, "MEDIA=" + media + "\n");
                var nonBlackBefore = CountNonBlackEntities(document.Database);
                PlotService.PlotPages(document, state, frames, output, media);
                var nonBlackAfter = CountNonBlackEntities(document.Database);
                if (!string.IsNullOrWhiteSpace(log)) File.AppendAllText(log, "ROLLBACK_NONBLACK_BEFORE=" + nonBlackBefore + ";AFTER=" + nonBlackAfter + "\n");
                if (nonBlackAfter != nonBlackBefore) throw new InvalidOperationException("Temporary monochrome override was not rolled back.");
                if (!string.IsNullOrWhiteSpace(log)) File.AppendAllText(log, "RUN_OK\n");
                document.Editor.WriteMessage("\nBATCHPLOTTEST_OK=" + output);
            }
            catch (System.Exception exception)
            {
                if (!string.IsNullOrWhiteSpace(log)) File.AppendAllText(log, "RUN_ERROR=" + exception + "\n");
                throw;
            }
        }

        private static int CountNonBlackEntities(Database database)
        {
            var count = 0;
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId recordId in blockTable)
                {
                    var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
                    if (record.IsFromExternalReference) continue;
                    foreach (ObjectId entityId in record)
                    {
                        var entity = transaction.GetObject(entityId, OpenMode.ForRead, false) as Entity;
                        if (entity == null) continue;
                        if (entity.Color.ColorMethod != Autodesk.AutoCAD.Colors.ColorMethod.ByAci || entity.Color.ColorIndex != 7) count++;
                    }
                }
            }
            return count;
        }

        [CommandMethod("BATCHPLOTOFFICIAL", CommandFlags.Modal)]
        public void RunOfficial()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) throw new InvalidOperationException("No active document.");
            var output = Environment.GetEnvironmentVariable("BATCHPLOT_CONTROL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Path.GetTempPath(), "BatchPlotPlus-control.pdf");

            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                var layoutManager = LayoutManager.Current;
                var layout = (Layout)transaction.GetObject(layoutManager.GetLayoutId(layoutManager.CurrentLayout), OpenMode.ForRead);
                using (var info = new PlotInfo { Layout = layout.ObjectId })
                using (var settings = new PlotSettings(layout.ModelType))
                {
                    settings.CopyFrom(layout);
                    var settingsValidator = PlotSettingsValidator.Current;
                    settingsValidator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Extents);
                    settingsValidator.SetUseStandardScale(settings, true);
                    settingsValidator.SetStdScaleType(settings, StdScaleType.ScaleToFit);
                    settingsValidator.SetPlotCentered(settings, true);
                    var state = new PluginState { Device = "DWG To PDF.pc3", Paper = "A3" };
                    settingsValidator.SetPlotConfigurationName(settings, state.Device, PlotService.ResolveMedia(document.Database, state));
                    info.OverrideSettings = settings;
                    using (var infoValidator = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled })
                    {
                        infoValidator.Validate(info);
                        using (var engine = PlotFactory.CreatePublishEngine())
                        using (var progress = new PlotProgressDialog(false, 1, true))
                        {
                            progress.OnBeginPlot();
                            engine.BeginPlot(progress, null);
                            engine.BeginDocument(info, document.Name, null, 1, true, output);
                            progress.OnBeginSheet();
                            using (var page = new PlotPageInfo())
                                engine.BeginPage(page, info, true, null);
                            engine.BeginGenerateGraphics(null);
                            engine.EndGenerateGraphics(null);
                            engine.EndPage(null);
                            progress.OnEndSheet();
                            engine.EndDocument(null);
                            progress.OnEndPlot();
                            engine.EndPlot(null);
                        }
                    }
                }
            }
            document.Editor.WriteMessage("\nBATCHPLOTOFFICIAL_OK=" + output);
        }

        [CommandMethod("BATCHPLOTREALTEST", CommandFlags.Modal)]
        public void RunRealDrawing()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) throw new InvalidOperationException("No active document.");
            var log = Environment.GetEnvironmentVariable("BATCHPLOT_SMOKE_LOG");
            var output = Environment.GetEnvironmentVariable("BATCHPLOT_REAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Path.GetTempPath(), "BatchPlotPlus-real-smoke.pdf");

            ObjectId templateId = ObjectId.Null;
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
                var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in modelSpace)
                {
                    var block = transaction.GetObject(id, OpenMode.ForRead, false) as BlockReference;
                    if (block == null) continue;
                    var hasDrawingName = block.AttributeCollection.Cast<ObjectId>()
                        .Select(attributeId => transaction.GetObject(attributeId, OpenMode.ForRead, false) as AttributeReference)
                        .Any(attribute => attribute != null &&
                            (string.Equals(attribute.Tag, "\u5716\u540d1", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(attribute.Tag, "\u5716\u540d2", StringComparison.OrdinalIgnoreCase)));
                    if (!hasDrawingName) continue;
                    templateId = id;
                    break;
                }
            }
            if (templateId.IsNull) throw new InvalidOperationException("No attributed title block was found.");

            var state = new PluginState
            {
                FrameMode = FrameMode.Block,
                TemplateHandle = templateId.Handle.ToString(),
                AutoLayer = true,
                Device = "DWG To PDF.pc3",
                Paper = "A3",
                PlotStyle = document.Database.PlotStyleMode ? "monochrome.ctb" : "monochrome.stb",
                FitToPaper = true,
                CenterPlot = true,
                PageOrientation = PageOrientation.Auto
            };
            PlotService.DescribeTemplate(document.Database, templateId, out var layer);
            state.LayerName = layer;
            var frames = PlotService.SortFrames(PlotService.CollectFrames(document.Database, state), state);
            if (frames.Count == 0) throw new InvalidOperationException("No matching title blocks were found.");
            frames = frames.Take(1).ToList();
            var media = PlotService.ResolveMedia(document.Database, state);
            if (!string.IsNullOrWhiteSpace(log)) File.WriteAllText(log, "MODE=" + (document.Database.PlotStyleMode ? "CTB" : "STB") + ";STYLE=" + state.PlotStyle + ";FRAMES=" + frames.Count + "\n");
            var nonBlackBefore = CountNonBlackEntities(document.Database);
            PlotService.PlotPages(document, state, frames, output, media);
            var nonBlackAfter = CountNonBlackEntities(document.Database);
            if (!string.IsNullOrWhiteSpace(log)) File.AppendAllText(log, "ROLLBACK_NONBLACK_BEFORE=" + nonBlackBefore + ";AFTER=" + nonBlackAfter + "\n");
            if (nonBlackAfter != nonBlackBefore) throw new InvalidOperationException("Temporary monochrome override was not rolled back.");
            if (!string.IsNullOrWhiteSpace(log)) File.AppendAllText(log, "RUN_OK\n");
            document.Editor.WriteMessage("\nBATCHPLOTREALTEST_OK=" + output + ";PAGES=" + frames.Count);
        }

        [CommandMethod("BATCHPLOTSTYLEDIAG", CommandFlags.Modal)]
        public void DiagnosePlotStyles()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) throw new InvalidOperationException("No active document.");
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var dictionary = (DBDictionary)transaction.GetObject(document.Database.PlotStyleNameDictionaryId, OpenMode.ForRead);
                document.Editor.WriteMessage("\nPLOT_STYLE_MODE=" + (document.Database.PlotStyleMode ? "CTB" : "STB"));
                var styleNames = new List<string>();
                foreach (DBDictionaryEntry entry in dictionary) styleNames.Add(entry.Key);
                document.Editor.WriteMessage("\nPLOT_STYLE_NAMES=" + string.Join(",", styleNames));
                var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId recordId in blockTable)
                {
                    var record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);
                    foreach (ObjectId id in record)
                    {
                        var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                        if (entity == null) continue;
                        var name = entity.PlotStyleName ?? "<null>";
                        counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
                    }
                }
            }
            foreach (var pair in counts.OrderByDescending(pair => pair.Value))
                document.Editor.WriteMessage("\nENTITY_STYLE=" + pair.Key + ";COUNT=" + pair.Value);
        }
    }
}
#endif
