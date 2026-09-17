using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;

namespace BatchPlotPlus.AutoCAD
{
    internal sealed class FontScan
    {
        internal string Stamp = "";
        internal HashSet<string> Big = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal HashSet<string> Small = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
    internal sealed class FontScanCache
    {
        private readonly Dictionary<string, FontScan> scans = new Dictionary<string, FontScan>(StringComparer.OrdinalIgnoreCase);
        internal void Clear() => scans.Clear();
        internal FontScan Read(string path)
        {
            path = Path.GetFullPath(path);
            var file = new FileInfo(path);
            var stamp = file.LastWriteTimeUtc.Ticks + ":" + file.Length;
            if (scans.TryGetValue(path, out var prior) && prior.Stamp == stamp) return prior;
            var scan = new FontScan { Stamp = stamp };
            using (var db = new Database(false, true))
            {
                db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
                using (var tx = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var table = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in table)
                    {
                        var style = (TextStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);
                        if (!string.IsNullOrWhiteSpace(style.BigFontFileName)) scan.Big.Add(style.BigFontFileName);
                        var small = FontCache.Name(style.FileName);
                        if (small != null) scan.Small.Add(small);
                    }
                }
            }
            if (scans.Count >= 64) scans.Clear();
            scans[path] = scan;
            return scan;
        }
    }
}
