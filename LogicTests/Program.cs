using System;
using System.Collections.Generic;
using System.Linq;
using BatchPlotPlus.AutoCAD;

internal static class Program
{
    private static int _checks;

    private static void Main()
    {
        Check(BatchLogic.WildcardMatch("A-FRAME", "A-*"), "wildcard star");
        Check(BatchLogic.WildcardMatch("FRAME-01", "FRAME-??"), "wildcard question mark");
        Check(!BatchLogic.WildcardMatch("OTHER", "FRAME-*"), "wildcard rejection");

        var frames = new List<SheetOrderItem>
        {
            Frame("bottom-right", 100, -1), Frame("top-right", 100, 101),
            Frame("bottom-left", 0, 0), Frame("top-left", 0, 100)
        };
        var state = new PluginState { SortMode = SortMode.LeftRightTopBottom };
        Check(Order(BatchLogic.Sort(frames, state.SortMode, false)) == "top-left,top-right,bottom-left,bottom-right", "row-first ordering");
        state.SortMode = SortMode.TopBottomLeftRight;
        Check(Order(BatchLogic.Sort(frames, state.SortMode, false)) == "top-left,bottom-left,top-right,bottom-right", "column-first ordering");
        Check(Order(BatchLogic.Sort(frames, state.SortMode, true)) == "bottom-right,top-right,bottom-left,top-left", "reverse ordering");

        var safe = BatchLogic.SafeFileName("A:B/C*D?");
        Check(safe.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0, "filename sanitization");

        var documentState = new PluginState { TemplateHandle = "AB", TemplateLabel = "old", MatchingFrameCount = 9, MatchingNamedFrameCount = 8 };
        documentState.ExplicitSheetHandles.Add("CD");
        documentState.Range = new RangeBounds(0, 0, 1, 1);
        documentState.ResetDocumentSelection("new-document");
        Check(documentState.TemplateHandle == "" && documentState.ExplicitSheetHandles.Count == 0 && !documentState.Range.HasValue && documentState.MatchingFrameCount == -1 && documentState.MatchingNamedFrameCount == -1, "document-state reset");

        Check(BatchLogic.ProgressPercent(0, 9) == 0, "progress start");
        Check(BatchLogic.ProgressPercent(4, 9) == 44, "progress middle");
        Check(BatchLogic.ProgressPercent(9, 9) == 100, "progress complete");
        Check(BatchLogic.ProgressPercent(1, 0) == 0, "progress empty");

        var styles = new List<string> { "", "monochrome.ctb", "monochrome.stb", "office.ctb" };
        Check(BatchLogic.IsPlotStyleCompatible(true, "monochrome.ctb"), "CTB compatibility");
        Check(!BatchLogic.IsPlotStyleCompatible(true, "monochrome.stb"), "reject STB for CTB drawing");
        Check(BatchLogic.SelectCompatiblePlotStyle(true, "monochrome.stb", styles) == "monochrome.ctb", "map STB selection to CTB equivalent");
        Check(BatchLogic.SelectCompatiblePlotStyle(false, "office.ctb", styles) == "", "clear unavailable STB equivalent");

        Console.WriteLine("Logic tests passed: " + _checks);
    }

    private static SheetOrderItem Frame(string name, double x, double y) => new SheetOrderItem
    {
        Key = name,
        MinX = x,
        MinY = y,
        MaxX = x + 80,
        MaxY = y + 50
    };

    private static string Order(IEnumerable<SheetOrderItem> frames) => string.Join(",", frames.Select(frame => frame.Key));

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Failed: " + name);
        _checks++;
    }
}
