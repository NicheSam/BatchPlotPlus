using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatchPlotPlus.AutoCAD
{
    internal sealed class SheetOrderItem
    {
        public string Key { get; set; } = "";
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    internal static class BatchLogic
    {
        public static bool WildcardMatch(string value, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern == "*") return true;
            var expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(value, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public static string SafeFileName(string value)
        {
            var result = value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_');
            result = result.Trim('.', ' ');
            return string.IsNullOrWhiteSpace(result) ? "圖面" : result;
        }

        public static List<SheetOrderItem> Sort(IList<SheetOrderItem> items, SortMode mode, bool reverse)
        {
            List<SheetOrderItem> result;
            if (mode == SortMode.LeftRightTopBottom) result = GroupAndSort(items, true);
            else if (mode == SortMode.TopBottomLeftRight) result = GroupAndSort(items, false);
            else result = items.ToList();
            if (reverse) result.Reverse();
            return result;
        }

        public static int ProgressPercent(int completed, int total)
        {
            if (total <= 0 || completed <= 0) return 0;
            if (completed >= total) return 100;
            return completed * 100 / total;
        }

        public static string RequiredPlotStyleExtension(bool colorDependent) => colorDependent ? ".ctb" : ".stb";

        public static bool IsPlotStyleCompatible(bool colorDependent, string name)
        {
            return string.Equals(Path.GetExtension(name), RequiredPlotStyleExtension(colorDependent), StringComparison.OrdinalIgnoreCase);
        }

        public static string SelectCompatiblePlotStyle(bool colorDependent, string current, IList<string> available)
        {
            var exact = available.FirstOrDefault(name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exact) && IsPlotStyleCompatible(colorDependent, exact)) return exact;
            var equivalent = Path.GetFileNameWithoutExtension(current) + RequiredPlotStyleExtension(colorDependent);
            return available.FirstOrDefault(name => string.Equals(name, equivalent, StringComparison.OrdinalIgnoreCase)) ?? "";
        }

        private static List<SheetOrderItem> GroupAndSort(IList<SheetOrderItem> items, bool rowsFirst)
        {
            var sizes = items.Select(item => rowsFirst ? item.MaxY - item.MinY : item.MaxX - item.MinX).OrderBy(value => value).ToList();
            var tolerance = sizes.Count == 0 ? 1.0 : Math.Max(1e-6, sizes[sizes.Count / 2] * 0.45);
            var pending = (rowsFirst ? items.OrderByDescending(CenterY).ThenBy(CenterX) : items.OrderBy(CenterX).ThenByDescending(CenterY)).ToList();
            var result = new List<SheetOrderItem>();
            while (pending.Count > 0)
            {
                var anchorValue = rowsFirst ? CenterY(pending[0]) : CenterX(pending[0]);
                var group = pending.Where(item => Math.Abs((rowsFirst ? CenterY(item) : CenterX(item)) - anchorValue) <= tolerance).ToList();
                foreach (var item in group) pending.Remove(item);
                result.AddRange(rowsFirst ? group.OrderBy(CenterX) : group.OrderByDescending(CenterY));
            }
            return result;
        }

        private static double CenterX(SheetOrderItem item) => (item.MinX + item.MaxX) / 2.0;
        private static double CenterY(SheetOrderItem item) => (item.MinY + item.MaxY) / 2.0;
    }
}
