using System.Collections.Generic;

namespace BatchPlotPlus.AutoCAD
{
    internal enum FrameMode { Polyline, Block, Custom }
    internal enum OperationMode { Pdf, SplitDwg }
    internal enum OutputMode { SeparatePdf, MergedPdf }
    internal enum PendingAction { None, SelectTemplate, SelectRange, SelectSheets, ClearRange, ClearSheets }
    internal enum SortMode { Selection, LeftRightTopBottom, TopBottomLeftRight }

    internal struct RangeBounds
    {
        public RangeBounds(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
    }

    internal sealed class PluginState
    {
        public string DocumentKey { get; set; } = "";
        public OperationMode OperationMode { get; set; } = OperationMode.Pdf;
        public FrameMode FrameMode { get; set; } = FrameMode.Block;
        public string TemplateHandle { get; set; } = "";
        public string TemplateLabel { get; set; } = "尚未指定";
        public string LayerName { get; set; } = "由樣板自動帶入";
        public bool AutoLayer { get; set; } = true;
        public RangeBounds? Range { get; set; }
        public List<string> ExplicitSheetHandles { get; } = new List<string>();
        public int MatchingFrameCount { get; set; } = -1;
        public int MatchingNamedFrameCount { get; set; } = -1;
        public OutputMode OutputMode { get; set; } = OutputMode.MergedPdf;
        public string Device { get; set; } = "DWG To PDF.pc3";
        public string Paper { get; set; } = "A3";
        public string PlotStyle { get; set; } = "";
        public int Copies { get; set; } = 1;
        public bool FitToPaper { get; set; } = true;
        public double FixedScale { get; set; } = 100.0;
        public SortMode SortMode { get; set; } = SortMode.LeftRightTopBottom;
        public bool ReverseOrder { get; set; }
        public bool AutoRotate { get; set; } = true;
        public bool ReverseOrientation { get; set; }
        public bool CenterPlot { get; set; } = true;
        public string OutputDirectory { get; set; } = "";
        public string MergedFileName { get; set; } = "合併圖面";
        public string DwgOutputDirectory { get; set; } = "";
        public string DwgFilePrefix { get; set; } = "圖";
        public bool DwgTestFirstTwo { get; set; } = true;
        public PendingAction PendingAction { get; set; }
        public List<string> AvailableDevices { get; } = new List<string> { "DWG To PDF.pc3" };
        public List<string> AvailablePlotStyles { get; } = new List<string> { "" };

        public void ResetDocumentSelection(string documentKey)
        {
            DocumentKey = documentKey;
            TemplateHandle = "";
            TemplateLabel = "尚未指定";
            Range = null;
            ExplicitSheetHandles.Clear();
            MatchingFrameCount = -1;
            MatchingNamedFrameCount = -1;
        }
    }

}
