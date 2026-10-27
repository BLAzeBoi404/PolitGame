using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Sovereign
{
    public class Land : IDisposable
    {
        public string Name;
        public double X, Y, Population;
        public PointF[][] Rings;
        public GraphicsPath Path;
        public RectangleF Bounds;
        public double Area;
        private RectangleF[] ringBounds;
        private GraphicsPath overviewPath, regionalPath;
        [ThreadStatic] private static List<PointF> clipInput, clipOutput;
        private struct PointRange { public int Start, End; public PointRange(int start, int end) { Start = start; End = end; } }
        private readonly Dictionary<int, PointF[][]> rasterRings = new Dictionary<int, PointF[][]>();

        // Background rasterization owns its GDI paths. Only immutable managed points are shared.
        public GraphicsPath RasterPath(RectangleF clip, float zoom)
        {
            int level = zoom <= 12 ? 0 : zoom <= 40 ? 1 : zoom <= 100 ? 2 : 3;
            PointF[][] rings;
            lock (rasterRings) {
                if (!rasterRings.TryGetValue(level, out rings)) {
                    float tolerance = level == 0 ? .06f : level == 1 ? .018f : level == 2 ? .005f : .001f;
                    rings = new PointF[Rings.Length][];
                    for (int i = 0; i < Rings.Length; i++) rings[i] = Simplify(Rings[i], tolerance);
                    rasterRings[level] = rings;
                }
            }
            var result = new GraphicsPath(FillMode.Winding);
            if (clip.Contains(Bounds)) { foreach (var ring in rings) if (ring.Length >= 3) result.AddPolygon(ring); return result; }
            if (clipInput == null) { clipInput = new List<PointF>(); clipOutput = new List<PointF>(); }
            foreach (var source in rings) {
                clipInput.Clear(); clipInput.AddRange(source);
                for (int edge = 0; edge < 4 && clipInput.Count > 0; edge++) {
                    clipOutput.Clear(); var previous = clipInput[clipInput.Count - 1];
                    foreach (var current in clipInput) {
                        bool inside = Inside(current, clip, edge), wasInside = Inside(previous, clip, edge);
                        if (inside != wasInside) clipOutput.Add(Intersection(previous, current, clip, edge));
                        if (inside) clipOutput.Add(current); previous = current;
                    }
                    var swap = clipInput; clipInput = clipOutput; clipOutput = swap;
                }
                if (clipInput.Count >= 3) result.AddPolygon(clipInput.ToArray());
            }
            return result;
        }

        public void Prepare()
        {
            Path = BuildPath(0);
            Bounds = Path.GetBounds();
            ringBounds = new RectangleF[Rings.Length]; Area = 0;
            for (int k = 0; k < Rings.Length; k++) {
                var ring = Rings[k]; if (ring.Length == 0) continue;
                float minX = ring[0].X, maxX = minX, minY = ring[0].Y, maxY = minY; double area = 0;
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++) {
                    minX = Math.Min(minX, ring[i].X); maxX = Math.Max(maxX, ring[i].X); minY = Math.Min(minY, ring[i].Y); maxY = Math.Max(maxY, ring[i].Y);
                    area += (double)ring[j].X * ring[i].Y - (double)ring[i].X * ring[j].Y;
                }
                ringBounds[k] = RectangleF.FromLTRB(minX, minY, maxX, maxY); Area += Math.Abs(area) / 2;
            }
        }
        // GDI+ IsVisible rasterizes in device pixels: at a scale of one degree per pixel
        // it misses small districts and even selects the neighbouring country. Never use
        // that raster test for geographic picking. The world format stores exterior rings.
        public bool Contains(PointF point)
        {
            if (!Bounds.Contains(point)) return false;
            for (int k = 0; k < Rings.Length; k++) {
                if (!ringBounds[k].Contains(point)) continue;
                var ring = Rings[k]; int winding = 0;
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++) {
                    double ax = ring[j].X, ay = ring[j].Y, bx = ring[i].X, by = ring[i].Y;
                    double side = (bx - ax) * (point.Y - ay) - (point.X - ax) * (by - ay);
                    if (ay <= point.Y && by > point.Y && side > 0) winding++;
                    else if (ay > point.Y && by <= point.Y && side < 0) winding--;
                }
                if (winding != 0) return true;
            }
            return false;
        }
        // At the selected zoom the simplification error is < 0.8 logical pixels.
        // Hit tests always use original geometry. LOD paths are generated once on demand.
        public GraphicsPath DisplayPath(float zoom)
        {
            if (zoom <= 12) return overviewPath ?? (overviewPath = BuildPath(0.06f));
            if (zoom <= 40) return regionalPath ?? (regionalPath = BuildPath(0.018f));
            return Path;
        }
        private GraphicsPath BuildPath(float tolerance)
        {
            var path = new GraphicsPath(FillMode.Winding);
            foreach (PointF[] ring in Rings)
                if (ring.Length >= 3) path.AddPolygon(tolerance > 0 ? Simplify(ring, tolerance) : ring);
            return path;
        }
        private static PointF[] Simplify(PointF[] ring, float tolerance)
        {
            if (ring.Length < 8) return ring;
            var points = new List<PointF>(ring);
            if (points[0] != points[points.Count - 1]) points.Add(points[0]);
            bool[] keep = new bool[points.Count]; keep[0] = keep[points.Count - 1] = true;
            var ranges = new Stack<PointRange>(); ranges.Push(new PointRange(0, points.Count - 1));
            double squared = tolerance * tolerance;
            while (ranges.Count > 0) {
                var range = ranges.Pop(); int start = range.Start, end = range.End, best = -1; double farthest = squared;
                PointF a = points[start], b = points[end]; double dx = b.X - a.X, dy = b.Y - a.Y, length = dx * dx + dy * dy;
                for (int i = start + 1; i < end; i++) {
                    double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((points[i].X - a.X) * dx + (points[i].Y - a.Y) * dy) / length));
                    double x = points[i].X - (a.X + t * dx), y = points[i].Y - (a.Y + t * dy), distance = x * x + y * y;
                    if (distance > farthest) { farthest = distance; best = i; }
                }
                if (best >= 0) { keep[best] = true; ranges.Push(new PointRange(start, best)); ranges.Push(new PointRange(best, end)); }
            }
            var simplified = new List<PointF>();
            for (int i = 0; i < points.Count - 1; i++) if (keep[i]) simplified.Add(points[i]);
            return simplified.Count >= 3 && simplified.Count < ring.Length ? simplified.ToArray() : ring;
        }
        public GraphicsPath ClipDisplayPath(RectangleF clip, float zoom)
        {
            var result = new GraphicsPath(FillMode.Winding);
            var data = DisplayPath(zoom).PathData;
            var ring = new List<PointF>();
            for (int i = 0; i < data.Points.Length; i++) {
                ring.Add(data.Points[i]);
                if ((data.Types[i] & 128) == 0 && i != data.Points.Length - 1) continue;
                for (int edge = 0; edge < 4 && ring.Count > 0; edge++) {
                    var output = new List<PointF>(); PointF previous = ring[ring.Count - 1];
                    foreach (var current in ring) {
                        bool inside = Inside(current, clip, edge), wasInside = Inside(previous, clip, edge);
                        if (inside != wasInside) output.Add(Intersection(previous, current, clip, edge));
                        if (inside) output.Add(current);
                        previous = current;
                    }
                    ring = output;
                }
                if (ring.Count >= 3) result.AddPolygon(ring.ToArray());
                ring = new List<PointF>();
            }
            return result;
        }
        private static bool Inside(PointF point, RectangleF box, int edge)
        {
            switch (edge) {
                case 0: return point.X >= box.Left;
                case 1: return point.X <= box.Right;
                case 2: return point.Y >= box.Top;
                default: return point.Y <= box.Bottom;
            }
        }
        private static PointF Intersection(PointF a, PointF b, RectangleF box, int edge)
        {
            if (edge < 2) {
                float x = edge == 0 ? box.Left : box.Right;
                return new PointF(x, a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X));
            }
            float y = edge == 2 ? box.Top : box.Bottom;
            return new PointF(a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y), y);
        }

        public void Dispose()
        {
            if (Path != null) Path.Dispose();
            if (overviewPath != null) overviewPath.Dispose();
            if (regionalPath != null) regionalPath.Dispose();
        }
    }
}
