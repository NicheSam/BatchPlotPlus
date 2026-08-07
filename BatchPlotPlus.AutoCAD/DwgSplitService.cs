using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace BatchPlotPlus.AutoCAD
{
    internal static class DwgSplitService
    {
        public static void Execute(Document document, PluginState state)
        {
            if (state.FrameMode != FrameMode.Block)
                throw new InvalidOperationException("拆分 DWG 目前只支援帶圖名屬性的圖框圖塊。");
            if (!document.Database.TileMode)
                throw new InvalidOperationException("請先切換到模型空間，再執行拆分 DWG。");

            Directory.CreateDirectory(state.DwgOutputDirectory);
            var selectedFrames = PlotService.CollectFrames(document.Database, state);
            var frames = selectedFrames.Where(frame => frame.HasDrawingName).ToList();
            frames = PlotService.SortFrames(frames, SortMode.LeftRightTopBottom, false);
            if (state.DwgTestFirstTwo) frames = frames.Take(2).ToList();
            if (frames.Count == 0)
                throw new InvalidOperationException("找不到帶有「圖名1」或「圖名2」內容的圖框。");

            var allFrameIds = new HashSet<ObjectId>(PlotService.CollectFrames(document.Database, state, true).Select(frame => frame.Id));
            var log = new List<string>
            {
                "BatchPlotPlus DWG split " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                "來源=" + document.Name,
                "圖框數=" + frames.Count.ToString(CultureInfo.InvariantCulture)
            };
            var succeeded = 0;
            var failed = 0;
            using (var originalView = document.Editor.GetCurrentView())
            {
                try
                {
                    for (var index = 0; index < frames.Count; index++)
                    {
                        var frame = frames[index];
                        var frameTimer = Stopwatch.StartNew();
                        try
                        {
                            if (Math.Abs(NormalizeRotation(frame.Rotation)) > 1e-8)
                                throw new InvalidOperationException("圖框有旋轉角度，為避免擷取錯誤範圍已略過。");
                            ZoomToFrame(document.Editor, frame.Extents);
                            var ids = CollectWindowObjects(document.Editor, document.Database, frame, allFrameIds);
                            if (ids.Count == 0) throw new InvalidOperationException("圖框範圍內沒有可輸出的模型空間物件。");
                            var output = UniquePath(state.DwgOutputDirectory, BatchLogic.SafeFileName(frame.FileBase), ".dwg");
                            using (var outputDatabase = document.Database.Wblock(ids, frame.Extents.MinPoint))
                                outputDatabase.SaveAs(output, DwgVersion.Current);
                            succeeded++;
                            log.Add("成功|" + frame.FileBase + "|" + output);
                        }
                        catch (System.Exception exception)
                        {
                            failed++;
                            log.Add("失敗|" + frame.FileBase + "|" + exception.Message);
                        }
                        document.Editor.WriteMessage("\n[DWG " + (index + 1) + "/" + frames.Count + "] " + frame.FileBase + " - " + frameTimer.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒");
                    }
                }
                finally
                {
                    document.Editor.SetCurrentView(originalView);
                }
            }

            var logPath = Path.Combine(state.DwgOutputDirectory, "BatchWBlock-log.txt");
            File.WriteAllLines(logPath, log, new UTF8Encoding(false));
            document.Editor.WriteMessage("\n拆分完成：成功 " + succeeded + "，失敗 " + failed + "。紀錄：" + logPath);
            if (succeeded == 0) throw new InvalidOperationException("沒有成功建立 DWG，請查看 BatchWBlock-log.txt。");
        }

        private static ObjectIdCollection CollectWindowObjects(Editor editor, Database database, PlotService.FrameInfo frame, ISet<ObjectId> frameIds)
        {
            var result = new ObjectIdCollection();
            var min = frame.Extents.MinPoint;
            var max = frame.Extents.MaxPoint;
            var diagonal = Math.Sqrt(Math.Pow(max.X - min.X, 2) + Math.Pow(max.Y - min.Y, 2));
            var padding = Math.Max(1e-6, diagonal * 1e-5);
            var polygon = new Point3dCollection
            {
                new Point3d(min.X - padding, min.Y - padding, 0), new Point3d(max.X + padding, min.Y - padding, 0),
                new Point3d(max.X + padding, max.Y + padding, 0), new Point3d(min.X - padding, max.Y + padding, 0)
            };
            var selection = editor.SelectCrossingPolygon(polygon);
            if (selection.Status != PromptStatus.OK || selection.Value == null) return result;
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)transaction.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (var id in selection.Value.GetObjectIds())
                {
                    if (frameIds.Contains(id) && id != frame.Id) continue;
                    var entity = transaction.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity != null && entity.OwnerId == model.ObjectId) result.Add(id);
                }
            }
            return result;
        }

        private static void ZoomToFrame(Editor editor, Extents3d extents)
        {
            using (var view = editor.GetCurrentView())
            {
                var width = Math.Max(1e-6, extents.MaxPoint.X - extents.MinPoint.X) * 1.25;
                var height = Math.Max(1e-6, extents.MaxPoint.Y - extents.MinPoint.Y) * 1.25;
                var aspect = Math.Max(1e-6, view.Width / Math.Max(1e-6, view.Height));
                if (width / height > aspect) height = width / aspect;
                else width = height * aspect;
                view.CenterPoint = new Point2d((extents.MinPoint.X + extents.MaxPoint.X) / 2.0, (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0);
                view.Width = width;
                view.Height = height;
                editor.SetCurrentView(view);
                editor.Regen();
            }
        }

        private static double NormalizeRotation(double value)
        {
            var twoPi = Math.PI * 2.0;
            value %= twoPi;
            if (value > Math.PI) value -= twoPi;
            if (value < -Math.PI) value += twoPi;
            return value;
        }

        private static string UniquePath(string directory, string baseName, string extension)
        {
            var path = Path.Combine(directory, baseName + extension);
            var number = 2;
            while (File.Exists(path))
                path = Path.Combine(directory, baseName + "_" + number++.ToString(CultureInfo.InvariantCulture) + extension);
            return path;
        }
    }
}
