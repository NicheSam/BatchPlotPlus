using System;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using BatchPlotPlus.AutoCAD;

[assembly: CommandClass(typeof(FontRuntimeChecks))]
public sealed class FontRuntimeChecks
{
    [CommandMethod("BPPFONTCORETEST")]
    public void Run()
    {
        var root = Environment.GetEnvironmentVariable("BPP_TEST_ROOT")!;
        Directory.CreateDirectory(root);
        var log = Path.Combine(root, "font-runtime.txt");
        File.WriteAllText(log, "START\r\n");
        try
        {
            var scanner = new FontScanCache();
            var cache = new FontCache(Path.Combine(root, "owned"));
            for (int n = 0; n < 2; n++)
            {
                var path = Path.Combine(root, "font-case-" + n + ".dwg");
                var name = "bpp_fresh_" + n + ".shx";
                using (var db = new Database(true, true))
                {
                    using (var tx = db.TransactionManager.StartTransaction())
                    {
                        var table = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForWrite);
                        var style = new TextStyleTableRecord { Name = "TEST", FileName = "txt.shx", BigFontFileName = name };
                        table.Add(style); tx.AddNewlyCreatedDBObject(style, true); tx.Commit();
                    }
                    db.SaveAs(path, DwgVersion.Current);
                }
                var hash = FontCache.Hash(path);
                var scan = scanner.Read(path);
                if (!scan.Big.Contains(name) || !scan.Small.Contains("txt.shx")) throw new InvalidOperationException("Style discovery failed.");
                if (!ReferenceEquals(scan, scanner.Read(path))) throw new InvalidOperationException("Repeated drawing not cached.");
                File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
                if (ReferenceEquals(scan, scanner.Read(path))) throw new InvalidOperationException("Modified file cache not invalidated.");
                var source = HostApplicationServices.Current.FindFile("chineset.shx", null, FindFileHint.FontFile);
                cache.Ensure(name, source);
                if (!cache.Owns(name) || FontCache.Hash(source) != FontCache.Hash(Path.Combine(cache.Root, name))) throw new InvalidOperationException("Alias hash check failed.");
                if (FontCache.Hash(path) != hash) throw new InvalidOperationException("DWG bytes changed.");
                File.AppendAllText(log, "CASE " + n + " DWG_SCAN REPEAT INVALIDATION ALIAS_HASH SOURCE_HASH PASS\r\n");
            }
            File.AppendAllText(log, "PASS\r\n");
        }
        catch (System.Exception error) { File.AppendAllText(log, "FAIL " + error.GetType().FullName + " " + error.Message + "\r\n"); }
    }
}
