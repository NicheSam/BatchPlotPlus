using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace BatchPlotPlus.AutoCAD
{
    internal static class FontService
    {
        private static bool _started, _handling;
        private static string _root = "";
        private static string _profile = "";
        private static FontCache? _cache;
        private static readonly FontScanCache Scans = new FontScanCache();
        internal static bool Enabled { get; private set; }
        internal static string Status { get; private set; } = "Not initialized";
        internal static string LogPath => Path.Combine(_root, "fonts.log");
        private static string SettingsPath => Path.Combine(_root, "enabled.txt");

        internal static void Initialize()
        {
            if (_started) return;
            // Version and language independent; separate host generations and profiles.
            var version = Convert.ToString(AcApp.GetSystemVariable("ACADVER")) ?? "unknown";
            var profile = Convert.ToString(AcApp.GetSystemVariable("CPROFILE")) ?? "default";
            _profile = profile;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatchPlotPlus", "Fonts", version.Split(' ')[0],
                    BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(profile))).Replace("-", "").Substring(0, 16));
            _cache = new FontCache(Path.Combine(_root, "Cache"));
            Directory.CreateDirectory(_root);
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "CadFontAuto"))
            {
                Status = "Legacy CadFontAuto loaded: migrate and restart AutoCAD before enabling integrated fonts.";
                Log(Status);
                return;
            }
            Enabled = !File.Exists(SettingsPath) || File.ReadAllText(SettingsPath).Trim() == "on";
            try { UpdateSupport(Enabled); }
            catch { Enabled = false; Status = "Support path unavailable; font automation is disabled"; throw; }
            AcApp.DocumentManager.DocumentCreateStarted += Creating;
            foreach (Document document in AcApp.DocumentManager) Subscribe(document);
            _started = true;
            Status = Enabled ? "Enabled (missing BigFont SHX only)" : "Disabled";
            Log(Status);
        }
        internal static void Terminate()
        {
            if (!_started) return;
            AcApp.DocumentManager.DocumentCreateStarted -= Creating;
            foreach (Document document in AcApp.DocumentManager) document.BeginDwgOpen -= Opening;
            _started = false;
        }
        private static void Creating(object sender, DocumentCollectionEventArgs args)
        {
            try { if (args.Document != null) Subscribe(args.Document); }
            catch (System.Exception e) { Log("subscribe: " + e.Message); }
        }
        private static void Subscribe(Document doc) { doc.BeginDwgOpen -= Opening; doc.BeginDwgOpen += Opening; }
        private static void Opening(object sender, DrawingOpenEventArgs args)
        {
            if (_handling) return;
            _handling = true;
            var timer = Stopwatch.StartNew();
            try
            {
                if (_profile != Convert.ToString(AcApp.GetSystemVariable("CPROFILE"))) { Terminate(); Scans.Clear(); Initialize(); }
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "CadFontAuto"))
                {
                    UpdateSupport(false); Enabled = false;
                    Status = "Legacy CadFontAuto detected; integrated automation paused until migration and restart.";
                    Log(Status); return;
                }
                if (Enabled) Prepare(args.FileName);
            }
            catch (System.Exception e) { Log("open skipped: " + e.Message); }
            finally { _handling = false; Log("scan ms=" + timer.ElapsedMilliseconds); }
        }
        private static string Resolve(string name, Database? database)
        {
            try { return HostApplicationServices.Current.FindFile(name, database, FindFileHint.FontFile); }
            catch (System.Exception) { return ""; }
        }
        private static void Prepare(string path)
        {
            if (_cache == null || !path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return;
            var scan = Scans.Read(path);
            var source = Resolve("chineset.shx", null);
            if (!File.Exists(source)) { Log("chineset.shx unavailable; no substitutions created"); return; }
            string support = Convert.ToString(AcApp.GetSystemVariable("ACADPREFIX")) ?? "";
            var directories = FontCache.SupportPath(support, _cache.Root, false).Split(';')
                .Concat(new[] { Path.GetDirectoryName(path) ?? "", Path.GetDirectoryName(source) ?? "" });
            // A later drawing may use an earlier BigFont alias as its ordinary font.
            // Remove our alias even when this drawing has no BigFont reference at all.
            foreach (var small in scan.Small) if (_cache.Owns(small)) _cache.RemoveOwned(small);
            foreach (var reference in scan.Big)
            {
                var name = FontCache.Name(reference);
                if (name == null || name.Equals("chineset.shx", StringComparison.OrdinalIgnoreCase)) continue;
                if (scan.Small.Contains(name)) { _cache.RemoveOwned(name); Log("ambiguous small/big font: " + name); continue; }
                var resolved = Resolve(reference, null);
                if ((File.Exists(reference) && !InsideCache(reference)) ||
                    (File.Exists(resolved) && !InsideCache(resolved)) ||
                    directories.Any(d => !string.IsNullOrWhiteSpace(d) && File.Exists(Path.Combine(d, name))))
                { _cache.RemoveOwned(name); continue; }
                Log(_cache.Ensure(name, source) + ": " + name + " -> chineset.shx | " + path);
            }
        }
        private static bool InsideCache(string path) => _cache != null && Path.GetFullPath(path).StartsWith(_cache.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static void UpdateSupport(bool enabled)
        {
            if (_cache == null) throw new InvalidOperationException("Font module unavailable.");
            Directory.CreateDirectory(_cache.Root);
            dynamic preferences = Autodesk.AutoCAD.ApplicationServices.Application.Preferences;
            string current = preferences.Files.SupportPath;
            var next = FontCache.SupportPath(current, _cache.Root, enabled);
            if (current != next) preferences.Files.SupportPath = next;
            string actual = preferences.Files.SupportPath;
            if (!string.Equals(actual.TrimEnd(';'), next.TrimEnd(';'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Support path readback failed.");
        }
        internal static void SetEnabled(bool enabled)
        {
            if (!_started) throw new InvalidOperationException(Status);
            if (enabled && AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "CadFontAuto"))
                throw new InvalidOperationException("Migrate legacy CadFontAuto and restart before enabling integrated fonts.");
            var previous = Enabled;
            UpdateSupport(enabled);
            var pending = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(pending, enabled ? "on" : "off", new UTF8Encoding(false));
                if (File.Exists(SettingsPath)) File.Replace(pending, SettingsPath, null);
                else File.Move(pending, SettingsPath);
            }
            catch
            {
                UpdateSupport(previous);
                throw;
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
            Enabled = enabled;
            Status = enabled ? "Enabled (missing BigFont SHX only)" : "Disabled; reopen drawings to refresh resolved fonts";
            Log(Status);
        }
        internal static int Clear()
        {
            if (Enabled) throw new InvalidOperationException("Disable automatic substitution before clearing the cache.");
            Scans.Clear();
            var count = _cache?.Clear() ?? 0;
            Log("Removed owned aliases: " + count);
            return count;
        }
        internal static string Report()
        {
            var lines = new StringBuilder().AppendLine(Status).AppendLine("Cache: " + _cache?.Root).AppendLine("Log: " + LogPath);
            if (_cache != null && Directory.Exists(_cache.Root))
                foreach (var p in Directory.GetFiles(_cache.Root, "*.shx")) lines.AppendLine(Path.GetFileName(p) + (_cache.Owns(Path.GetFileName(p)) ? " [owned]" : " [unowned/conflict]"));
            if (File.Exists(LogPath)) lines.AppendLine(string.Join(Environment.NewLine, File.ReadLines(LogPath).Reverse().Take(100).Reverse()));
            return lines.ToString();
        }
        private static void Log(string message)
        {
            try
            {
                if (_root.Length == 0) return;
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1024 * 1024) File.WriteAllText(LogPath, "Log rotated\r\n");
                File.AppendAllText(LogPath, DateTimeOffset.Now.ToString("o") + " " + message + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (System.Exception) { /* Font diagnostics must never block opening or plotting. */ }
        }
    }
}
