using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BatchPlotPlus.AutoCAD
{
    // Only files with matching ownership receipts may be removed or reused.
    internal sealed class FontCache
    {
        internal string Root { get; }
        internal FontCache(string root) { Root = Path.GetFullPath(root); }
        internal static string? Name(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return null;
            var colon = reference.IndexOf(':');
            if (colon >= 0 && (colon != 1 || !char.IsLetter(reference[0]) || reference.LastIndexOf(':') != colon)) return null;
            var name = Path.GetFileName(reference.Trim().Replace('/', '\\'));
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !name.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)) return null;
            var stem = Path.GetFileNameWithoutExtension(name);
            if (stem.EndsWith(" ") || stem.EndsWith(".") || stem.Length == 0 ||
                System.Text.RegularExpressions.Regex.IsMatch(stem, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return null;
            return name;
        }
        internal static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }
        internal bool Owns(string name)
        {
            if (Name(name) != name) return false;
            var target = Path.Combine(Root, name);
            return File.Exists(target) && File.Exists(target + ".receipt") &&
                File.ReadAllText(target + ".receipt").Trim() == Hash(target);
        }
        internal string Ensure(string name, string source)
        {
            using (var gate = Lock()) return EnsureLocked(name, source);
        }
        private string EnsureLocked(string name, string source)
        {
            if (Name(name) != name) throw new ArgumentException("Unsafe font name.");
            Directory.CreateDirectory(Root);
            var target = Path.Combine(Root, name);
            if (File.Exists(target)) return Owns(name) && Hash(source) == Hash(target) ? "cached" : "conflict";
            // Receipt first: a crash cannot leave an unidentifiable completed copy.
            File.WriteAllText(target + ".receipt", Hash(source), new UTF8Encoding(false));
            File.Copy(source, target, false);
            return "created";
        }
        internal int Clear()
        {
            using (var gate = Lock()) return ClearLocked();
        }
        internal bool RemoveOwned(string name)
        {
            using (var gate = Lock())
            {
                if (!Owns(name)) return false;
                File.Delete(Path.Combine(Root, name));
                File.Delete(Path.Combine(Root, name) + ".receipt");
                return true;
            }
        }
        private int ClearLocked()
        {
            if (!Directory.Exists(Root)) return 0;
            var count = 0;
            foreach (var path in Directory.GetFiles(Root, "*.shx"))
            {
                var name = Path.GetFileName(path);
                if (!Owns(name)) continue;
                File.Delete(path);
                File.Delete(path + ".receipt");
                count++;
            }
            return count;
        }
        private IDisposable Lock()
        {
            Directory.CreateDirectory(Root);
            // FileShare.None serializes different AutoCAD processes without waiting in open events.
            return new FileStream(Path.Combine(Root, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        internal static string SupportPath(string value, string cache, bool enabled)
        {
            var entries = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !string.Equals(p.Trim().TrimEnd('\\', '/'), cache.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)).ToList();
            if (enabled) entries.Add(cache);
            return string.Join(";", entries);
        }
    }
}
