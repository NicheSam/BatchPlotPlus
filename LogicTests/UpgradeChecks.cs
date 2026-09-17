using System;
using System.IO;
using BatchPlotPlus.AutoCAD;

internal static class UpgradeChecks
{
    private static int checks;
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "BatchPlotPlus-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.shx");
            File.WriteAllText(source, "test font content");
            var cache = new FontCache(Path.Combine(root, "cache"));
            Check(FontCache.Name("C:/fonts/new.SHX") == "new.SHX", "absolute source basename");
            Check(FontCache.Name("../new.shx") == "new.shx", "traversal never becomes target path");
            foreach (var value in new[] { "", " ", "NUL.shx", "con.SHX", "COM1.shx", "a.ttf", "a .shx", "a..shx", "bad:font.shx", ".shx" })
                Check(FontCache.Name(value) == null, "reject name " + value);
            Check(cache.Ensure("new.shx", source) == "created", "new missing bigfont copied");
            Check(cache.Owns("new.shx"), "receipt verified");
            Check(cache.Ensure("new.shx", source) == "cached", "repeat idempotent");
            File.WriteAllText(Path.Combine(cache.Root, "new.shx"), "user replacement");
            Check(cache.Ensure("new.shx", source) == "conflict", "changed font preserved");
            Check(cache.Clear() == 0, "clear preserves user replacement");
            File.WriteAllText(Path.Combine(cache.Root, "user.shx"), "unowned");
            Check(cache.Ensure("user.shx", source) == "conflict", "unowned alias not overwritten");
            Check(cache.Ensure("owned.shx", source) == "created", "second alias");
            Check(cache.Clear() == 1, "only owned alias removed");
            Check(File.Exists(Path.Combine(cache.Root, "user.shx")), "unowned remains");
            Check(!File.Exists(Path.Combine(cache.Root, "owned.shx.receipt")), "receipt removed");
            using (var held = new FileStream(Path.Combine(cache.Root, ".lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Throws<IOException>(() => cache.Ensure("race.shx", source), "concurrent cache mutation fails closed");
            var paths = "C:\\normal;D:\\Fonts;" + cache.Root;
            Check(FontCache.SupportPath(paths, cache.Root, false) == "C:\\normal;D:\\Fonts", "disable removes only owned path");
            Check(FontCache.SupportPath(paths, cache.Root, true) == paths, "enable idempotent");
            Check(FontCache.SupportPath(paths + ";" + cache.Root.ToUpperInvariant(), cache.Root, true) == paths, "deduplicate own path");
            object valueNow = 2;
            TemporarySetting.Run(() => valueNow, v => valueNow = v, 0, () => Check((int)valueNow == 0, "temporary applied"), _ => { });
            Check((int)valueNow == 2, "success restores");
            var originalError = new IOException("plot failed");
            try { TemporarySetting.Run(() => valueNow, v => valueNow = v, 0, () => throw originalError, _ => { }); }
            catch (IOException e) { Check(ReferenceEquals(e, originalError), "original failure retained"); }
            Check((int)valueNow == 2, "failure restores");
            bool reported = false;
            Throws<InvalidOperationException>(() => TemporarySetting.Run(() => valueNow, v => valueNow = v, 0, () => valueNow = 3, _ => reported = true), "concurrent change reported");
            Check((int)valueNow == 3 && reported, "concurrent value preserved");
            valueNow = 2;
            Throws<InvalidOperationException>(() => TemporarySetting.Run(() => valueNow, _ => { }, 0, () => throw new Exception("must not execute"), _ => { }), "write readback rejects ignored set");
            valueNow = 2;
            try { TemporarySetting.Run(() => valueNow, v => { if ((int)v == 2) throw new UnauthorizedAccessException(); valueNow = v; }, 0, () => throw originalError, _ => { }); }
            catch (IOException e) { Check(e.Data.Contains("RestorationFailure"), "dual failures recorded"); }
            Console.WriteLine("Upgrade checks passed: " + checks);
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
    private static void Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); } catch (T) { checks++; return; }
        throw new Exception(name);
    }
}
