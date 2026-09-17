using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using BatchPlotPlus.AutoCAD;

[assembly: CommandClass(typeof(RuntimeChecks))]
public sealed class RuntimeChecks
{
    [CommandMethod("BPPUPGRADETEST")]
    public void Run()
    {
        var path = Path.Combine(Environment.GetEnvironmentVariable("BPP_TEST_ROOT")!, "bootstrap.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "BOOTSTRAP\r\n");
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name!.IndexOf("BatchPlot", StringComparison.OrdinalIgnoreCase) >= 0)
                File.AppendAllText(path, assembly.FullName + " " + assembly.Location + "\r\n");
        try { RunChecks(); }
        catch (System.Exception e) { File.AppendAllText(path, e.GetType().FullName + " " + e.Message + "\r\n"); }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void RunChecks()
    {
        var root = Environment.GetEnvironmentVariable("BPP_TEST_ROOT");
        if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("BPP_TEST_ROOT is required.");
        Directory.CreateDirectory(root);
        var log = Path.Combine(root, "runtime.txt");
        File.WriteAllText(log, "START\r\n");
        try
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var state = new PluginState { FrameMode = FrameMode.Polyline, LayerName = "0", AutoLayer = true, OutputDirectory = root, MergedFileName = "runtime", DwgOutputDirectory = root };
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var blocks = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tx.GetObject(blocks[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                for (int n = 0; n < 2; n++)
                {
                    var border = new Polyline();
                    border.AddVertexAt(0, new Point2d(n * 500, 0), 0, 0, 0);
                    border.AddVertexAt(1, new Point2d(n * 500 + 400, 0), 0, 0, 0);
                    border.AddVertexAt(2, new Point2d(n * 500 + 400, 280), 0, 0, 0);
                    border.AddVertexAt(3, new Point2d(n * 500, 280), 0, 0, 0);
                    border.Closed = true;
                    model.AppendEntity(border); tx.AddNewlyCreatedDBObject(border, true);
                    if (n == 0) state.TemplateHandle = border.Handle.ToString();
                    var text = new DBText { TextString = "BatchPlotPlus 1.5 - " + n, Height = 12, Position = new Point3d(n * 500 + 30, 100, 0) };
                    model.AppendEntity(text); tx.AddNewlyCreatedDBObject(text, true);
                }
                tx.Commit();
            }
            var before = Convert.ToInt32(AcApp.GetSystemVariable("BACKGROUNDPLOT"));
            state.Device = "nonexistent-device.pc3";
            bool failed = false;
            try { PlotService.Execute(doc, state); } catch (System.Exception) { failed = true; }
            if (!failed || Convert.ToInt32(AcApp.GetSystemVariable("BACKGROUNDPLOT")) != before) throw new InvalidOperationException("Failure restoration check failed.");
            File.AppendAllText(log, "INVALID_DEVICE_RESTORED\r\n");
            state.Device = "DWG To PDF.pc3";
            PlotService.Execute(doc, state);
            if (!File.Exists(Path.Combine(root, "runtime.pdf")) || Convert.ToInt32(AcApp.GetSystemVariable("BACKGROUNDPLOT")) != before) throw new InvalidOperationException("PDF output check failed.");
            File.AppendAllText(log, "PDF_CREATED_SETTINGS_RESTORED\r\n");
            var input = Path.Combine(root, "font-fixture.dwg");
            using (var fixture = new Database(true, true))
            {
                using (var tx = fixture.TransactionManager.StartTransaction())
                {
                    var styles = (TextStyleTable)tx.GetObject(fixture.TextStyleTableId, OpenMode.ForWrite);
                    var style = new TextStyleTableRecord { Name = "TEST", FileName = "txt.shx", BigFontFileName = "bpp_runtime_missing.shx" };
                    styles.Add(style); tx.AddNewlyCreatedDBObject(style, true); tx.Commit();
                }
                fixture.SaveAs(input, DwgVersion.Current);
            }
            var fontRoot = Path.Combine(root, "font-state"); Directory.CreateDirectory(fontRoot);
            var cache = new FontCache(Path.Combine(fontRoot, "Cache"));
            var type = typeof(FontService);
            type.GetField("_root", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, fontRoot);
            type.GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, cache);
            var originalHash = FontCache.Hash(input);
            var prepare = type.GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!;
            prepare.Invoke(null, new object[] { input });
            if (!cache.Owns("bpp_runtime_missing.shx")) throw new InvalidOperationException("Missing bigfont was not cached.");
            prepare.Invoke(null, new object[] { input });
            if (FontCache.Hash(input) != originalHash) throw new InvalidOperationException("Source DWG changed.");
            File.AppendAllText(log, "FONT_SCAN_AND_REPEAT_PASSED_SOURCE_UNCHANGED\r\nPASS\r\n");
        }
        catch (System.Exception error) { File.AppendAllText(log, "FAIL " + error + "\r\n"); }
    }
}
