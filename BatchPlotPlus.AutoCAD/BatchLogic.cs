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

    internal sealed class PlotBehavior
    {
        public int RotationDegrees { get; set; }
        public bool PrintLineweights { get; set; }
        public bool PlotTransparency { get; set; }
    }
    internal sealed class PlotWindowBounds
    {
        public PlotWindowBounds(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
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
            if (Regex.IsMatch(result.Split('.')[0].TrimEnd(), @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase))
                result = "_" + result;
            return string.IsNullOrWhiteSpace(result) ? "圖面" : result;
        }

        public static string NumberedFileBase(string prefix, int oneBasedIndex)
        {
            var effectivePrefix = string.IsNullOrWhiteSpace(prefix) ? "圖" : SafeFileName(prefix);
            return effectivePrefix + Math.Max(1, oneBasedIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string DwgFileBase(string attributeFileBase, bool hasDrawingName, string prefix, int oneBasedIndex)
        {
            return hasDrawingName && !string.IsNullOrWhiteSpace(attributeFileBase)
                ? attributeFileBase
                : NumberedFileBase(prefix, oneBasedIndex);
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
            return (int)((long)completed * 100 / total);
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

        public static int ResolvePlotRotationDegrees(PageOrientation orientation, bool frameIsLandscape, bool reverse)
        {
            var landscape = orientation == PageOrientation.Landscape ||
                (orientation == PageOrientation.Auto && frameIsLandscape);
            if (landscape) return reverse ? 270 : 90;
            return reverse ? 180 : 0;
        }

        public static PlotBehavior ResolvePlotBehavior(PluginState state, bool frameIsLandscape)
        {
            return new PlotBehavior
            {
                RotationDegrees = ResolvePlotRotationDegrees(state.PageOrientation, frameIsLandscape, state.ReverseOrientation),
                PrintLineweights = state.PrintLineweights,
                PlotTransparency = state.PlotTransparency
            };
        }

        public static bool TryGetPaperMillimeters(string paper, out double width, out double height)
        {
            switch ((paper ?? "").Trim().ToUpperInvariant())
            {
                case "A0": width = 841; height = 1189; return true;
                case "A1": width = 594; height = 841; return true;
                case "A2": width = 420; height = 594; return true;
                case "A3": width = 297; height = 420; return true;
                case "A4": width = 210; height = 297; return true;
                default: width = 297; height = 420; return false;
            }
        }
        public static PlotWindowBounds NormalizePlotWindow(double x1, double y1, double x2, double y2)
        {
            if (!IsFinite(x1) || !IsFinite(y1) || !IsFinite(x2) || !IsFinite(y2))
                throw new InvalidOperationException("\u51fa\u5716\u7bc4\u570d\u5305\u542b\u7121\u6548\u5ea7\u6a19\uff0c\u8acb\u91cd\u65b0\u6307\u5b9a\u5716\u6846\u6216\u51fa\u5716\u7bc4\u570d\u3002");

            var minX = Math.Min(x1, x2);
            var minY = Math.Min(y1, y2);
            var maxX = Math.Max(x1, x2);
            var maxY = Math.Max(y1, y2);
            if (Math.Abs(maxX - minX) < 1e-8 || Math.Abs(maxY - minY) < 1e-8)
                throw new InvalidOperationException("\u51fa\u5716\u7bc4\u570d\u5bec\u5ea6\u6216\u9ad8\u5ea6\u70ba 0\uff0c\u8acb\u91cd\u65b0\u6307\u5b9a\u6709\u6548\u5716\u6846\u3002");

            return new PlotWindowBounds(minX, minY, maxX, maxY);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
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
