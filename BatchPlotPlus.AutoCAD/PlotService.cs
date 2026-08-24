using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BatchPlotPlus.AutoCAD
{
    internal static class PlotService
    {
        private const int MaxPreviewSegmentsPerSheet = 1200;

        public static string DescribeTemplate(Database database, ObjectId id, out string layer)
        {
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var entity = (Entity)transaction.GetObject(id, OpenMode.ForRead);
                layer = entity.Layer;
                if (entity is BlockReference block)
                    return "圖塊: " + EffectiveName(block, transaction);
                if (entity is Polyline polyline)
                    return "聚合線: " + polyline.Layer;
                return entity.GetType().Name;
            }
        }

        internal static bool TryValidateTemplate(Database database, ObjectId id, FrameMode mode, out string error)
        {
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead) as Entity;
                if (mode == FrameMode.Block && entity is BlockReference)
                {
                    error = "";
                    return true;
                }
                if (!(entity is Polyline polyline) || !polyline.Closed)
                {
                    error = "選取的聚合線未封閉，不能作為圖框樣板。";
                    return false;
                }
                if (mode == FrameMode.Polyline && !IsRectangle(polyline))
                {
                    error = "矩形聚合線模式只接受四邊直線且封閉的圖框。";
                    return false;
                }
                error = "";
                return true;
            }
        }

        public static void Execute(Document document, PluginState state)
        {
            if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                throw new InvalidOperationException("AutoCAD 目前正在執行其他出圖工作，請稍後再試。");
            Directory.CreateDirectory(state.OutputDirectory);

            var frames = CollectFrames(document.Database, state);
            if (frames.Count == 0) throw new InvalidOperationException("找不到符合圖框樣板、搜尋圖層與範圍的圖框。");
            frames = SortFrames(frames, state);
            if (state.Copies > 1)
                frames = Enumerable.Range(0, state.Copies).SelectMany(_ => frames).ToList();

            var oldBackgroundPlot = Convert.ToInt32(AcApp.GetSystemVariable("BACKGROUNDPLOT"), CultureInfo.InvariantCulture);
            AcApp.SetSystemVariable("BACKGROUNDPLOT", 0);
            var totalTimer = Stopwatch.StartNew();
            try
            {
                var media = ResolveMedia(document.Database, state);
                if (state.OutputMode == OutputMode.MergedPdf)
                {
                    var name = BatchLogic.SafeFileName(string.IsNullOrWhiteSpace(state.MergedFileName) ? "合併圖面" : state.MergedFileName);
                    var output = UniquePath(state.OutputDirectory, name, ".pdf");
                    PlotPages(document, state, frames, output, media);
                    document.Editor.WriteMessage("\n多頁 PDF 已完成：" + output);
                }
                else
                {
                    var number = 0;
                    foreach (var frame in frames)
                    {
                        number++;
                        var output = UniquePath(state.OutputDirectory, BatchLogic.SafeFileName(frame.FileBase), ".pdf");
                        PlotPages(document, state, new List<FrameInfo> { frame }, output, media);
                    }
                    document.Editor.WriteMessage("\n已完成 " + number + " 個個別 PDF 檔案。");
                }
            }
            finally
            {
                AcApp.SetSystemVariable("BACKGROUNDPLOT", oldBackgroundPlot);
                document.Editor.WriteMessage("\nPDF 輸出總耗時：" + totalTimer.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒。");
            }
        }

        internal static List<FrameInfo> CollectFrames(Database database, PluginState state, bool ignoreSelectionAndRange = false)
        {
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var templateId = ResolveHandle(database, state.TemplateHandle);
                if (templateId.IsNull || !templateId.IsValid)
                    throw new InvalidOperationException("圖框樣板已失效，請重新指定。");
                var sample = transaction.GetObject(templateId, OpenMode.ForRead) as Entity
                    ?? throw new InvalidOperationException("無法讀取圖框樣板。");
                var sampleBlockName = sample is BlockReference sampleBlock ? EffectiveName(sampleBlock, transaction) : "";
                var ids = !ignoreSelectionAndRange && state.ExplicitSheetHandles.Count > 0
                    ? state.ExplicitSheetHandles.Select(handle => ResolveHandle(database, handle)).Where(id => !id.IsNull).ToArray()
                    : ReadModelSpaceIds(database, transaction);
                var result = new List<FrameInfo>();
                foreach (var id in ids)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null || !Matches(entity, sample, sampleBlockName, state, transaction)) continue;
                    Extents3d extents;
                    try { extents = entity.GeometricExtents; }
                    catch { continue; }
                    if (!ignoreSelectionAndRange && state.Range.HasValue && !Intersects(extents, state.Range.Value)) continue;
                    result.Add(new FrameInfo {
                        Id = id,
                        Extents = extents,
                        FileBase = BuildFileBase(entity, transaction, result.Count + 1, database),
                        HasDrawingName = entity is BlockReference namedBlock && HasDrawingName(namedBlock, transaction),
                        Rotation = entity is BlockReference rotatedBlock ? rotatedBlock.Rotation : 0.0
                    });
                }
                return result;
            }
        }

        private static ObjectId[] ReadModelSpaceIds(Database database, Transaction transaction)
        {
            var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)transaction.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            return model.Cast<ObjectId>().ToArray();
        }

        private static bool Matches(Entity entity, Entity sample, string sampleBlockName, PluginState state, Transaction transaction)
        {
            var targetLayer = state.AutoLayer ? sample.Layer : state.LayerName;
            if (state.FrameMode == FrameMode.Block)
                return entity is BlockReference candidate &&
                    string.Equals(EffectiveName(candidate, transaction), sampleBlockName, StringComparison.OrdinalIgnoreCase) &&
                    BatchLogic.WildcardMatch(entity.Layer, targetLayer);
            if (!(entity is Polyline polyline) || !polyline.Closed) return false;
            if (!BatchLogic.WildcardMatch(entity.Layer, targetLayer)) return false;
            if (state.FrameMode == FrameMode.Polyline) return IsRectangle(polyline);
            return true;
        }

        private static bool IsRectangle(Polyline polyline)
        {
            if (!polyline.Closed || polyline.NumberOfVertices != 4) return false;
            for (var index = 0; index < 4; index++)
                if (Math.Abs(polyline.GetBulgeAt(index)) > 1e-9) return false;
            return true;
        }

        private static bool Intersects(Extents3d extents, RangeBounds range) =>
            extents.MaxPoint.X >= range.MinX && extents.MinPoint.X <= range.MaxX &&
            extents.MaxPoint.Y >= range.MinY && extents.MinPoint.Y <= range.MaxY;

        private static ObjectId ResolveHandle(Database database, string text)
        {
            if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) return ObjectId.Null;
            try { return database.GetObjectId(false, new Handle(value), 0); }
            catch { return ObjectId.Null; }
        }

        private static string EffectiveName(BlockReference block, Transaction transaction)
        {
            var recordId = block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord;
            return ((BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead)).Name;
        }

        private static string BuildFileBase(Entity entity, Transaction transaction, int index, Database database)
        {
            if (entity is BlockReference block)
            {
                var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId attributeId in block.AttributeCollection)
                {
                    var attribute = transaction.GetObject(attributeId, OpenMode.ForRead) as AttributeReference;
                    if (attribute != null) attributes[attribute.Tag] = attribute.TextString.Trim();
                }
                attributes.TryGetValue("圖名1", out var first);
                attributes.TryGetValue("圖名2", out var second);
                if (!string.IsNullOrWhiteSpace(first) || !string.IsNullOrWhiteSpace(second))
                    return string.Join("-", new[] { second, first }.Where(value => !string.IsNullOrWhiteSpace(value)));
            }
            var drawing = Path.GetFileNameWithoutExtension(database.Filename);
            if (string.IsNullOrWhiteSpace(drawing)) drawing = "圖面";
            return drawing + "-" + index.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static bool HasDrawingName(BlockReference block, Transaction transaction)
        {
            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(attributeId, OpenMode.ForRead) as AttributeReference;
                if (attribute != null &&
                    (string.Equals(attribute.Tag, "圖名1", StringComparison.OrdinalIgnoreCase) || string.Equals(attribute.Tag, "圖名2", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(attribute.TextString)) return true;
            }
            return false;
        }

        internal static List<FrameInfo> SortFrames(List<FrameInfo> frames, PluginState state)
            => SortFrames(frames, state.PdfSortMode, state.PdfReverseOrder);

        internal static List<FrameInfo> SortFrames(List<FrameInfo> frames, SortMode mode, bool reverse)
        {
            var byKey = frames.Select((frame, index) => new { Key = index.ToString(CultureInfo.InvariantCulture), Frame = frame }).ToDictionary(item => item.Key, item => item.Frame);
            var items = byKey.Select(pair => new SheetOrderItem
            {
                Key = pair.Key,
                MinX = pair.Value.Extents.MinPoint.X,
                MinY = pair.Value.Extents.MinPoint.Y,
                MaxX = pair.Value.Extents.MaxPoint.X,
                MaxY = pair.Value.Extents.MaxPoint.Y
            }).ToList();
            return BatchLogic.Sort(items, mode, reverse).Select(item => byKey[item.Key]).ToList();
        }

        internal static void ShowPreview(Document document, PluginState state)
        {
            var frames = CollectFrames(document.Database, state);
            if (frames.Count == 0) throw new InvalidOperationException("找不到符合圖框樣板、搜尋圖層與範圍的圖框。");
            frames = SortFrames(frames, state);
            if (state.Copies > 1)
                frames = Enumerable.Range(0, state.Copies).SelectMany(_ => frames).ToList();
            var items = BuildPreviewItems(document, state, frames);
            using (var form = new PlotPreviewForm(items))
                AcApp.ShowModalDialog(form);
        }

        internal static List<PlotPreviewItem> BuildPreviewItems(Document document, PluginState state, IList<FrameInfo> frames)
        {
            var worldToDisplay = GetWorldToDisplay(document);
            BatchLogic.TryGetPaperMillimeters(state.Paper, out var paperWidth, out var paperHeight);
            var frameIds = new HashSet<ObjectId>(frames.Select(frame => frame.Id));
            var previewSources = BuildPreviewSources(document.Database, worldToDisplay, frameIds);
            var items = new List<PlotPreviewItem>();
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                var plotExtents = frame.Extents;
                plotExtents.TransformBy(worldToDisplay);
                var min = plotExtents.MinPoint;
                var max = plotExtents.MaxPoint;
                var window = BatchLogic.NormalizePlotWindow(min.X, min.Y, max.X, max.Y);
                var frameWidth = window.MaxX - window.MinX;
                var frameHeight = window.MaxY - window.MinY;
                var frameIsLandscape = frameWidth >= frameHeight;
                var behavior = BatchLogic.ResolvePlotBehavior(state, frameIsLandscape);
                var pageLandscape = behavior.RotationDegrees == 90 || behavior.RotationDegrees == 270;
                var previewItem = new PlotPreviewItem
                {
                    PageNumber = index + 1,
                    FileBase = frame.FileBase,
                    Device = state.Device,
                    Paper = state.Paper,
                    PlotStyle = state.PlotStyle,
                    OrientationLabel = pageLandscape ? "橫向" : "直向",
                    RotationDegrees = behavior.RotationDegrees,
                    ScaleLabel = state.FitToPaper ? "自動配合紙張" : "1:" + state.FixedScale.ToString("0.###", CultureInfo.InvariantCulture),
                    FrameSizeLabel = frameWidth.ToString("0.###", CultureInfo.InvariantCulture) + "×" + frameHeight.ToString("0.###", CultureInfo.InvariantCulture),
                    WindowLabel = FormatWindow(window),
                    WindowMinX = window.MinX,
                    WindowMinY = window.MinY,
                    FrameWidth = frameWidth,
                    FrameHeight = frameHeight,
                    PageWidth = pageLandscape ? paperHeight : paperWidth,
                    PageHeight = pageLandscape ? paperWidth : paperHeight,
                    PrintLineweights = behavior.PrintLineweights,
                    PlotTransparency = behavior.PlotTransparency
                };
                AddContentPreview(previewItem, window, previewSources);
                items.Add(previewItem);
            }
            return items;
        }

        private static List<PreviewSource> BuildPreviewSources(Database database, Matrix3d worldToDisplay, ISet<ObjectId> frameIds)
        {
            var sources = new List<PreviewSource>();
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var ids = ReadModelSpaceIds(database, transaction);
                foreach (var id in ids)
                {
                    if (frameIds.Contains(id)) continue;
                    var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null) continue;
                    Extents3d extents;
                    try { extents = entity.GeometricExtents; }
                    catch { continue; }
                    extents.TransformBy(worldToDisplay);
                    var bounds = NormalizePreviewBounds(extents.MinPoint.X, extents.MinPoint.Y, extents.MaxPoint.X, extents.MaxPoint.Y);
                    var colorArgb = ResolvePreviewColor(entity, transaction);
                    var segments = BuildEntityPreviewSegments(entity, worldToDisplay, bounds, colorArgb);
                    if (segments.Count == 0) AddBoundsSegments(segments, bounds, colorArgb);
                    sources.Add(new PreviewSource(id, bounds, segments));
                }
            }
            return sources;
        }

        private static PlotWindowBounds NormalizePreviewBounds(double x1, double y1, double x2, double y2)
        {
            if (!IsFinite(x1) || !IsFinite(y1) || !IsFinite(x2) || !IsFinite(y2))
                return new PlotWindowBounds(0.0, 0.0, 1.0, 1.0);

            var minX = Math.Min(x1, x2);
            var minY = Math.Min(y1, y2);
            var maxX = Math.Max(x1, x2);
            var maxY = Math.Max(y1, y2);
            var width = maxX - minX;
            var height = maxY - minY;
            var pad = Math.Max(Math.Max(Math.Abs(width), Math.Abs(height)) * 0.001, 1e-4);
            if (Math.Abs(width) < 1e-8)
            {
                minX -= pad;
                maxX += pad;
            }
            if (Math.Abs(height) < 1e-8)
            {
                minY -= pad;
                maxY += pad;
            }
            return new PlotWindowBounds(minX, minY, maxX, maxY);
        }

        private static List<PlotPreviewSegment> BuildEntityPreviewSegments(Entity entity, Matrix3d worldToDisplay, PlotWindowBounds bounds, int colorArgb)
        {
            var segments = new List<PlotPreviewSegment>();
            var line = entity as Line;
            if (line != null)
            {
                AddSegment(segments, line.StartPoint, line.EndPoint, worldToDisplay, colorArgb);
                return segments;
            }
            var polyline = entity as Polyline;
            if (polyline != null)
            {
                var count = Math.Min(polyline.NumberOfVertices, 250);
                for (var i = 1; i < count; i++) AddSegment(segments, polyline.GetPoint3dAt(i - 1), polyline.GetPoint3dAt(i), worldToDisplay, colorArgb);
                if (polyline.Closed && count > 1) AddSegment(segments, polyline.GetPoint3dAt(count - 1), polyline.GetPoint3dAt(0), worldToDisplay, colorArgb);
                return segments;
            }
            var circle = entity as Circle;
            if (circle != null)
            {
                AddArcSegments(segments, circle.Center, circle.Radius, 0.0, Math.PI * 2.0, worldToDisplay, 48, colorArgb);
                return segments;
            }
            var arc = entity as Arc;
            if (arc != null)
            {
                var start = arc.StartAngle;
                var end = arc.EndAngle;
                while (end < start) end += Math.PI * 2.0;
                AddArcSegments(segments, arc.Center, arc.Radius, start, end, worldToDisplay, 32, colorArgb);
                return segments;
            }
            AddBoundsSegments(segments, bounds, colorArgb);
            return segments;
        }

        private static void AddArcSegments(ICollection<PlotPreviewSegment> segments, Point3d center, double radius, double startAngle, double endAngle, Matrix3d transform, int steps, int colorArgb)
        {
            var sweep = Math.Max(1e-9, endAngle - startAngle);
            var segmentCount = Math.Max(4, Math.Min(steps, (int)Math.Ceiling(sweep / (Math.PI / 12.0))));
            Point3d? previous = null;
            for (var i = 0; i <= segmentCount; i++)
            {
                var angle = startAngle + sweep * i / segmentCount;
                var point = new Point3d(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius, center.Z);
                if (previous.HasValue) AddSegment(segments, previous.Value, point, transform, colorArgb);
                previous = point;
            }
        }

        private static void AddSegment(ICollection<PlotPreviewSegment> segments, Point3d start, Point3d end, Matrix3d transform, int colorArgb)
        {
            start = start.TransformBy(transform);
            end = end.TransformBy(transform);
            if (!IsFinite(start.X) || !IsFinite(start.Y) || !IsFinite(end.X) || !IsFinite(end.Y)) return;
            segments.Add(new PlotPreviewSegment(start.X, start.Y, end.X, end.Y, colorArgb));
        }

        private static void AddBoundsSegments(ICollection<PlotPreviewSegment> segments, PlotWindowBounds bounds, int colorArgb)
        {
            segments.Add(new PlotPreviewSegment(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MinY, colorArgb));
            segments.Add(new PlotPreviewSegment(bounds.MaxX, bounds.MinY, bounds.MaxX, bounds.MaxY, colorArgb));
            segments.Add(new PlotPreviewSegment(bounds.MaxX, bounds.MaxY, bounds.MinX, bounds.MaxY, colorArgb));
            segments.Add(new PlotPreviewSegment(bounds.MinX, bounds.MaxY, bounds.MinX, bounds.MinY, colorArgb));
        }

        private static int ResolvePreviewColor(Entity entity, Transaction transaction)
        {
            try
            {
                var color = entity.Color;
                if (color != null && color.ColorMethod == ColorMethod.ByColor)
                    return color.ColorValue.ToArgb();
                if (color != null && color.ColorMethod == ColorMethod.ByAci && color.ColorIndex > 0 && color.ColorIndex < 256)
                    return AciToArgb(color.ColorIndex);
                if (!entity.LayerId.IsNull)
                {
                    var layer = transaction.GetObject(entity.LayerId, OpenMode.ForRead, false) as LayerTableRecord;
                    var layerColor = layer?.Color;
                    if (layerColor != null && layerColor.ColorMethod == ColorMethod.ByColor)
                        return layerColor.ColorValue.ToArgb();
                    if (layerColor != null && layerColor.ColorIndex > 0 && layerColor.ColorIndex < 256)
                        return AciToArgb(layerColor.ColorIndex);
                }
            }
            catch (System.Exception)
            {
                return AciToArgb(7);
            }
            return AciToArgb(7);
        }

        private static int AciToArgb(int index)
        {
            switch (index)
            {
                case 1: return unchecked((int)0xFFFF0000);
                case 2: return unchecked((int)0xFFFFFF00);
                case 3: return unchecked((int)0xFF00FF00);
                case 4: return unchecked((int)0xFF00FFFF);
                case 5: return unchecked((int)0xFF0000FF);
                case 6: return unchecked((int)0xFFFF00FF);
                case 8: return unchecked((int)0xFF606060);
                case 9: return unchecked((int)0xFFB0B0B0);
                default: return unchecked((int)0xFF000000);
            }
        }

        private static void AddContentPreview(PlotPreviewItem item, PlotWindowBounds window, IEnumerable<PreviewSource> sources)
        {
            foreach (var source in sources)
            {
                if (!Intersects(source.Bounds, window)) continue;
                item.ContentEntityCount++;
                foreach (var segment in source.Segments)
                {
                    if (!Intersects(segment, window)) continue;
                    if (item.ContentSegments.Count >= MaxPreviewSegmentsPerSheet)
                    {
                        item.ContentPreviewTruncated = true;
                        return;
                    }
                    item.ContentSegments.Add(segment);
                }
            }
        }

        private static bool Intersects(PlotWindowBounds left, PlotWindowBounds right)
        {
            return left.MinX <= right.MaxX && left.MaxX >= right.MinX && left.MinY <= right.MaxY && left.MaxY >= right.MinY;
        }

        private static bool Intersects(PlotPreviewSegment segment, PlotWindowBounds window)
        {
            var minX = Math.Min(segment.X1, segment.X2);
            var maxX = Math.Max(segment.X1, segment.X2);
            var minY = Math.Min(segment.Y1, segment.Y2);
            var maxY = Math.Max(segment.Y1, segment.Y2);
            return minX <= window.MaxX && maxX >= window.MinX && minY <= window.MaxY && maxY >= window.MinY;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class PreviewSource
        {
            public PreviewSource(ObjectId id, PlotWindowBounds bounds, List<PlotPreviewSegment> segments)
            {
                Id = id;
                Bounds = bounds;
                Segments = segments;
            }
            public ObjectId Id { get; }
            public PlotWindowBounds Bounds { get; }
            public List<PlotPreviewSegment> Segments { get; }
        }

        private static Matrix3d GetWorldToDisplay(Document document)
        {
            using (var view = document.Editor.GetCurrentView())
            {
                var worldToDisplay = Matrix3d.PlaneToWorld(view.ViewDirection);
                worldToDisplay = Matrix3d.Displacement(view.Target - Point3d.Origin) * worldToDisplay;
                worldToDisplay = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * worldToDisplay;
                return worldToDisplay.Inverse();
            }
        }

        internal static void PlotPages(Document document, PluginState state, IList<FrameInfo> frames, string outputPath, string media)
        {
            Transaction? monochromeOverride = null;
            try
            {
                var worldToDisplay = GetWorldToDisplay(document);

                using (var transaction = document.Database.TransactionManager.StartTransaction())
                {
                var layoutDictionary = (DBDictionary)transaction.GetObject(document.Database.LayoutDictionaryId, OpenMode.ForRead);
                var layoutId = layoutDictionary.GetAt("Model");
                var layout = (Layout)transaction.GetObject(layoutId, OpenMode.ForRead);
                var infoValidators = new List<PlotInfoValidator>();
                var plotInfos = frames.Select(frame => CreatePlotInfo(layout, frame, state, media, worldToDisplay, infoValidators)).ToList();
                try
                {
                    monochromeOverride = BeginMonochromeOverride(document, state);
                    using (var engine = PlotFactory.CreatePublishEngine())
                    using (var progress = new PlotProgressDialog(false, frames.Count, true))
                    {
                        progress.set_PlotMsgString(PlotMessageIndex.DialogTitle, "批次輸出 PDF Plus");
                        progress.set_PlotMsgString(PlotMessageIndex.CancelJobButtonMessage, "取消全部輸出");
                        progress.set_PlotMsgString(PlotMessageIndex.CancelSheetButtonMessage, "略過這一頁");
                        progress.set_PlotMsgString(PlotMessageIndex.SheetSetProgressCaption, "整批進度");
                        progress.set_PlotMsgString(PlotMessageIndex.SheetProgressCaption, "目前頁面");
                        progress.LowerPlotProgressRange = 0;
                        progress.UpperPlotProgressRange = 100;
                        progress.PlotProgressPos = 0;
                        progress.OnBeginPlot();
                        progress.IsVisible = true;
                        engine.BeginPlot(progress, null);
                        engine.BeginDocument(plotInfos[0], document.Name, null, frames.Count, true, outputPath);
                        for (var index = 0; index < plotInfos.Count; index++)
                        {
                            var sheetTimer = Stopwatch.StartNew();
                            progress.set_PlotMsgString(PlotMessageIndex.Status, "正在輸出第 " + (index + 1).ToString(CultureInfo.InvariantCulture) + "/" + plotInfos.Count.ToString(CultureInfo.InvariantCulture) + " 頁：" + frames[index].FileBase);
                            progress.PlotProgressPos = BatchLogic.ProgressPercent(index, plotInfos.Count);
                            progress.OnBeginSheet();
                            progress.LowerSheetProgressRange = 0;
                            progress.UpperSheetProgressRange = 100;
                            progress.SheetProgressPos = 0;
                            using (var page = new PlotPageInfo())
                                engine.BeginPage(page, plotInfos[index], index == plotInfos.Count - 1, null);
                            engine.BeginGenerateGraphics(null);
                            engine.EndGenerateGraphics(null);
                            engine.EndPage(null);
                            progress.SheetProgressPos = 100;
                            progress.OnEndSheet();
                            progress.PlotProgressPos = BatchLogic.ProgressPercent(index + 1, plotInfos.Count);
                            document.Editor.WriteMessage("\n[PDF " + (index + 1).ToString(CultureInfo.InvariantCulture) + "/" + plotInfos.Count.ToString(CultureInfo.InvariantCulture) + "] " + frames[index].FileBase + " - " + sheetTimer.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒");
                        }
                        engine.EndDocument(null);
                        progress.PlotProgressPos = 100;
                        progress.OnEndPlot();
                        engine.EndPlot(null);
                    }
                }
                finally
                {
                    monochromeOverride?.Dispose();
                    monochromeOverride = null;
                    foreach (var info in plotInfos)
                    {
                        info.OverrideSettings?.Dispose();
                        info.Dispose();
                    }
                    foreach (var validator in infoValidators)
                        validator.Dispose();
                }
                }
            }
            finally
            {
                monochromeOverride?.Dispose();
            }
        }

        private static Transaction? BeginMonochromeOverride(Document document, PluginState state)
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(state.PlotStyle), "monochrome", StringComparison.OrdinalIgnoreCase))
                return null;

            var database = document.Database;
            var transaction = database.TransactionManager.StartTransaction();
            var changed = 0;
            var skipped = 0;
            try
            {
                var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                if (database.PlotStyleMode)
                {
                    foreach (ObjectId layerId in layerTable)
                    {
                        try
                        {
                            var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
                            if (ConvertTrueColorToAci(layer.Color, color => layer.Color = color)) changed++;
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { skipped++; }
                    }
                }
                else
                {
                    foreach (ObjectId layerId in layerTable)
                    {
                        try
                        {
                            var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
                            layer.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                            layer.PlotStyleName = "Normal";
                            changed++;
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { skipped++; }
                    }
                }

                var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId recordId in blockTable)
                {
                    BlockTableRecord record;
                    try { record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead); }
                    catch (Autodesk.AutoCAD.Runtime.Exception) { skipped++; continue; }
                    if (record.IsFromExternalReference) continue;
                    foreach (ObjectId entityId in record)
                    {
                        try
                        {
                            var entity = transaction.GetObject(entityId, OpenMode.ForWrite, false) as Entity;
                            if (entity == null) continue;
                            if (database.PlotStyleMode)
                            {
                                if (ConvertTrueColorToAci(entity.Color, color => entity.Color = color)) changed++;
                            }
                            else
                            {
                                entity.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                                entity.PlotStyleName = "Normal";
                                changed++;
                            }
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { skipped++; }
                    }
                }
                document.Editor.WriteMessage("\nmonochrome 出圖暫時覆寫：" + changed.ToString(CultureInfo.InvariantCulture) + " 個物件／圖層，略過 " + skipped.ToString(CultureInfo.InvariantCulture) + " 個不支援項目；來源 DWG 不會儲存變更。");
                return transaction;
            }
            catch
            {
                transaction.Dispose();
                throw;
            }
        }

        private static bool ConvertTrueColorToAci(Color source, Action<Color> assign)
        {
            if (source.ColorMethod != ColorMethod.ByColor) return false;
            var index = EntityColor.LookUpAci(source.Red, source.Green, source.Blue);
            assign(Color.FromColorIndex(ColorMethod.ByAci, index));
            return true;
        }

        internal static string ResolveMedia(Database database, PluginState state)
        {
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var layoutDictionary = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
                var layout = (Layout)transaction.GetObject(layoutDictionary.GetAt("Model"), OpenMode.ForRead);
                using (var settings = new PlotSettings(layout.ModelType))
                {
                    settings.CopyFrom(layout);
                    var validator = PlotSettingsValidator.Current;
                    validator.SetPlotConfigurationName(settings, state.Device, null);
                    // Autodesk documents RefreshLists as expensive and unnecessary more than once per batch.
                    // https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_PlotSettingsValidator.html
                    validator.RefreshLists(settings);
                    var media = FindMedia(validator.GetCanonicalMediaNameList(settings).Cast<string>(), state.Paper);
                    if (media == null) throw new InvalidOperationException("選取的 PDF 輸出裝置不支援 " + state.Paper + " 紙張大小。");
                    return media;
                }
            }
        }

        private static PlotInfo CreatePlotInfo(Layout layout, FrameInfo frame, PluginState state, string media, Matrix3d worldToDisplay, ICollection<PlotInfoValidator> infoValidators)
        {
            var settings = new PlotSettings(layout.ModelType);
            settings.CopyFrom(layout);
            var validator = PlotSettingsValidator.Current;
            validator.SetPlotConfigurationName(settings, state.Device, media);
            var plotExtents = frame.Extents;
            plotExtents.TransformBy(worldToDisplay);
            var min = plotExtents.MinPoint;
            var max = plotExtents.MaxPoint;
            var window = BatchLogic.NormalizePlotWindow(min.X, min.Y, max.X, max.Y);
            try
            {
                validator.SetPlotWindowArea(settings, new Extents2d(window.MinX, window.MinY, window.MaxX, window.MaxY));
                validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);
            }
            catch (System.Exception exception)
            {
                throw new InvalidOperationException("\u8a2d\u5b9a\u51fa\u5716\u8996\u7a97\u7bc4\u570d\u5931\u6557\u3002\u5716\u6846\uff1a" + frame.FileBase + "\uff1b\u7bc4\u570d\uff1a" + FormatWindow(window) + "\u3002", exception);
            }
            validator.SetPlotCentered(settings, state.CenterPlot);
            validator.SetUseStandardScale(settings, state.FitToPaper);
            if (state.FitToPaper)
                validator.SetStdScaleType(settings, StdScaleType.ScaleToFit);
            else
                validator.SetCustomPrintScale(settings, new CustomScale(1.0, state.FixedScale));
            if (!string.IsNullOrWhiteSpace(state.PlotStyle))
            {
                var requiredExtension = BatchLogic.RequiredPlotStyleExtension(layout.Database.PlotStyleMode);
                if (!BatchLogic.IsPlotStyleCompatible(layout.Database.PlotStyleMode, state.PlotStyle))
                    throw new InvalidOperationException("目前圖面只能使用 " + requiredExtension.ToUpperInvariant() + " 出圖樣式，不能使用 " + state.PlotStyle + "。");
                try
                {
                    validator.SetCurrentStyleSheet(settings, state.PlotStyle);
                    settings.PlotPlotStyles = true;
                }
                catch (System.Exception exception) { throw new InvalidOperationException("無法套用出圖樣式 " + state.PlotStyle + "。", exception); }
            }
            else
            {
                settings.PlotPlotStyles = false;
            }
            var frameIsLandscape = (window.MaxX - window.MinX) >= (window.MaxY - window.MinY);
            var behavior = BatchLogic.ResolvePlotBehavior(state, frameIsLandscape);
            try
            {
                settings.PrintLineweights = behavior.PrintLineweights;
                settings.PlotTransparency = behavior.PlotTransparency;
            }
            catch (System.Exception exception)
            {
                throw new InvalidOperationException("\u5957\u7528\u5217\u5370\u5167\u5bb9\u8a2d\u5b9a\u5931\u6557\u3002\u7dda\u7c97\uff1a" + state.PrintLineweights.ToString(CultureInfo.InvariantCulture) + "\uff1b\u900f\u660e\u5ea6\uff1a" + state.PlotTransparency.ToString(CultureInfo.InvariantCulture) + "\u3002", exception);
            }
            var rotation = behavior.RotationDegrees == 90
                ? PlotRotation.Degrees090
                : behavior.RotationDegrees == 180
                    ? PlotRotation.Degrees180
                    : behavior.RotationDegrees == 270
                        ? PlotRotation.Degrees270
                        : PlotRotation.Degrees000;
            try
            {
                validator.SetPlotRotation(settings, rotation);
            }
            catch (System.Exception exception)
            {
                throw new InvalidOperationException("\u8a2d\u5b9a\u5716\u7d19\u65b9\u5411\u5931\u6557\u3002\u65cb\u8f49\u89d2\u5ea6\uff1a" + behavior.RotationDegrees.ToString(CultureInfo.InvariantCulture) + "\u3002", exception);
            }
            var info = new PlotInfo { Layout = layout.ObjectId, OverrideSettings = settings };
            PlotInfoValidator? infoValidator = null;
            try
            {
                infoValidator = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled };
                infoValidator.Validate(info);
                infoValidators.Add(infoValidator);
                return info;
            }
            catch (System.Exception exception)
            {
                infoValidator?.Dispose();
                info.OverrideSettings?.Dispose();
                info.Dispose();
                var style = string.IsNullOrWhiteSpace(state.PlotStyle) ? "\u7121" : state.PlotStyle;
                throw new InvalidOperationException("AutoCAD \u9a57\u8b49\u51fa\u5716\u8a2d\u5b9a\u5931\u6557\u3002\u5716\u6846\uff1a" + frame.FileBase + "\uff1b\u88dd\u7f6e\uff1a" + state.Device + "\uff1b\u7d19\u5f35\uff1a" + state.Paper + "\uff1b\u6a23\u5f0f\uff1a" + style + "\uff1b\u7bc4\u570d\uff1a" + FormatWindow(window) + "\u3002", exception);
            }
        }

        private static string FormatWindow(PlotWindowBounds window)
        {
            return window.MinX.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                window.MinY.ToString("0.###", CultureInfo.InvariantCulture) + " - " +
                window.MaxX.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                window.MaxY.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string? FindMedia(IEnumerable<string> names, string paper)
        {
            var upper = paper.ToUpperInvariant();
            return names.FirstOrDefault(name => name.ToUpperInvariant().Contains("FULL_BLEED") && Regex.IsMatch(name.ToUpperInvariant(), "(^|_)" + Regex.Escape(upper) + "(_|$)"))
                ?? names.FirstOrDefault(name => Regex.IsMatch(name.ToUpperInvariant(), "(^|_)" + Regex.Escape(upper) + "(_|$)"));
        }

        private static string UniquePath(string directory, string baseName, string extension)
        {
            var path = Path.Combine(directory, baseName + extension);
            var number = 2;
            while (File.Exists(path)) path = Path.Combine(directory, baseName + "_" + number++.ToString(CultureInfo.InvariantCulture) + extension);
            return path;
        }

        internal sealed class FrameInfo
        {
            public ObjectId Id { get; set; }
            public Extents3d Extents { get; set; }
            public string FileBase { get; set; } = "圖面";
            public bool HasDrawingName { get; set; }
            public double Rotation { get; set; }
        }
    }
}
