using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace BatchPlotPlus.AutoCAD
{
    internal static class DwgBoundary
    {
        internal static Point3dCollection Read(Database db, PlotService.FrameInfo frame)
        {
            using (var tx = db.TransactionManager.StartOpenCloseTransaction())
            {
                var entity = tx.GetObject(frame.Id, OpenMode.ForRead);
                var points = new Point3dCollection();
                if (entity is Polyline poly)
                {
                    if (!poly.Closed || poly.NumberOfVertices < 3 || Math.Abs(poly.Normal.Z) < 0.999999)
                        throw new InvalidOperationException("DWG boundary must be a closed XY polyline: " + frame.Id.Handle);
                    for (var i = 0; i < poly.NumberOfVertices; i++)
                    {
                        // Approximate arcs with angular steps no larger than 0.04 radians.
                        var bulge = Math.Abs(poly.GetBulgeAt(i));
                        var angle = 4 * Math.Atan(bulge);
                        var steps = bulge < 1e-12 ? 1 : Math.Max(2, (int)Math.Ceiling(angle / 0.04));
                        for (var n = 0; n < steps; n++)
                        {
                            var p = poly.GetPointAtParameter(i + (double)n / steps);
                            points.Add(new Point3d(p.X, p.Y, 0));
                        }
                    }
                }
                else
                {
                    var min = frame.Extents.MinPoint; var max = frame.Extents.MaxPoint;
                    points.Add(new Point3d(min.X, min.Y, 0)); points.Add(new Point3d(max.X, min.Y, 0));
                    points.Add(new Point3d(max.X, max.Y, 0)); points.Add(new Point3d(min.X, max.Y, 0));
                }
                double area = 0;
                for (int i = 0; i < points.Count; i++)
                {
                    var a = points[i]; var b = points[(i + 1) % points.Count];
                    if (a.DistanceTo(b) < 1e-9) throw new InvalidOperationException("Duplicate boundary vertex: " + frame.Id.Handle);
                    area += a.X * b.Y - b.X * a.Y;
                    for (int j = i + 2; j < points.Count; j++)
                    {
                        if (i == 0 && j == points.Count - 1) continue;
                        if (Crosses(a, b, points[j], points[(j + 1) % points.Count]))
                            throw new InvalidOperationException("Self-intersecting boundary: " + frame.Id.Handle);
                    }
                }
                if (Math.Abs(area) < 1e-8) throw new InvalidOperationException("Zero-area boundary: " + frame.Id.Handle);
                return points;
            }
        }

        private static double Cross(Point3d a, Point3d b, Point3d c) => (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        private static bool Crosses(Point3d a, Point3d b, Point3d c, Point3d d) => Cross(a,b,c)*Cross(a,b,d)<0 && Cross(c,d,a)*Cross(c,d,b)<0;
    }
}
