using System.Collections.Generic;

namespace BatchPlotPlus.AutoCAD
{
    internal enum FrameMode { Polyline, Block, Custom }
    internal enum OperationMode { Pdf, SplitDwg }
    internal enum OutputMode { SeparatePdf, MergedPdf }
    internal enum PendingAction { None, SelectTemplate, SelectRange, SelectSheets, ClearRange, ClearSheets, PreviewPdf }
    internal enum SortMode { Selection, LeftRightTopBottom, TopBottomLeftRight }
    internal enum PageOrientation { Auto, Landscape, Portrait }

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



    internal sealed class PlotPreviewSegment
    {
        public PlotPreviewSegment(double x1, double y1, double x2, double y2, int colorArgb)
        {
            X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; ColorArgb = colorArgb;
        }
        public double X1 { get; }
        public double Y1 { get; }
        public double X2 { get; }
        public double Y2 { get; }
        public int ColorArgb { get; }
    }

    internal sealed class PlotPreviewItem
    {
        public int PageNumber { get; set; }
        public string FileBase { get; set; } = "";
        public string Device { get; set; } = "";
        public string Paper { get; set; } = "";
        public string PlotStyle { get; set; } = "";
        public string OrientationLabel { get; set; } = "";
        public int RotationDegrees { get; set; }
        public string ScaleLabel { get; set; } = "";
        public string FrameSizeLabel { get; set; } = "";
        public string WindowLabel { get; set; } = "";
        public double WindowMinX { get; set; }
        public double WindowMinY { get; set; }
        public double FrameWidth { get; set; }
        public double FrameHeight { get; set; }
        public double PageWidth { get; set; }
        public double PageHeight { get; set; }
        public bool PrintLineweights { get; set; }
        public bool PlotTransparency { get; set; }
        public List<PlotPreviewSegment> ContentSegments { get; } = new List<PlotPreviewSegment>();
        public int ContentEntityCount { get; set; }
        public bool ContentPreviewTruncated { get; set; }
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
        public SortMode PdfSortMode { get; set; } = SortMode.LeftRightTopBottom;
        public bool PdfReverseOrder { get; set; }
        public SortMode DwgSortMode { get; set; } = SortMode.LeftRightTopBottom;
        public bool DwgReverseOrder { get; set; }
        public PageOrientation PageOrientation { get; set; } = PageOrientation.Auto;
        public bool PrintLineweights { get; set; } = true;
        public bool PlotTransparency { get; set; }
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
