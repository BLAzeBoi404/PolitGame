using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Sovereign
{
    public partial class GameForm
    {
        private readonly Dictionary<Land, GraphicsPath> highlightPaths = new Dictionary<Land, GraphicsPath>();
        private sealed class HighlightSurface { public Bitmap Image; public RectangleF Bounds; }
        private readonly Dictionary<int, HighlightSurface> highlightSurfaces = new Dictionary<int, HighlightSurface>();
        private float highlightPixels;
        private RectangleF highlightCoverage;
        private float highlightScale;
        private void ClearHighlights()
        {
            foreach (var path in highlightPaths.Values) path.Dispose(); highlightPaths.Clear();
            foreach (var surface in highlightSurfaces.Values) surface.Image.Dispose(); highlightSurfaces.Clear();
        }
        private GraphicsPath HighlightPath(Land land)
        {
            var view = new RectangleF(cx - mapRect.Width / scale / 2, cy - mapRect.Height / scale / 2, mapRect.Width / scale, mapRect.Height / scale);
            if (highlightPixels != UiScale || highlightScale != scale || !highlightCoverage.Contains(view) || highlightPaths.Count > 6) {
                ClearHighlights(); highlightPixels = UiScale; highlightScale = scale; highlightCoverage = view; highlightCoverage.Inflate(256 / scale, 256 / scale);
            }
            GraphicsPath path;
            if (!highlightPaths.TryGetValue(land, out path)) {
                path = land.Bounds.IntersectsWith(highlightCoverage) ? land.RasterPath(highlightCoverage, scale) : new GraphicsPath();
                highlightPaths.Add(land, path);
            }
            return path;
        }
        private void DrawRegionHighlight(Graphics g, int region, bool selectedRegion)
        {
            if (region < 0) return; var path = HighlightPath(W.Regions[region]); if (path.PointCount == 0) return;
            int key = region * 2 + (selectedRegion ? 1 : 0); HighlightSurface cached;
            if (!highlightSurfaces.TryGetValue(key, out cached)) {
                var bounds = RectangleF.Intersect(highlightCoverage, W.Regions[region].Bounds); bounds.Inflate(5 / scale, 5 / scale);
                float pixels = scale * UiScale;
                var bitmap = new Bitmap(System.Math.Max(1, (int)System.Math.Ceiling(bounds.Width * pixels)), System.Math.Max(1, (int)System.Math.Ceiling(bounds.Height * pixels)), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var ink = Graphics.FromImage(bitmap)) using (var transform = new Matrix(pixels, 0, 0, pixels, -bounds.X * pixels, -bounds.Y * pixels)) {
                    ink.Transform = transform; ink.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var fill = new SolidBrush(selectedRegion ? Color.FromArgb(43, 246, 238, 181) : Color.FromArgb(65, 255, 245, 199))) ink.FillPath(fill, path);
                    if (selectedRegion) using (var outer = new Pen(Color.FromArgb(120, 100, 98, 64), 4 / scale)) ink.DrawPath(outer, path);
                    using (var pen = new Pen(selectedRegion ? Color.FromArgb(99, 93, 54) : Color.FromArgb(85, 91, 70), 1.6f / scale)) ink.DrawPath(pen, path);
                }
                cached = new HighlightSurface { Image = bitmap, Bounds = bounds }; highlightSurfaces[key] = cached;
            }
            if(gpuRecordingWorld){var point=Screen(cached.Bounds.X,cached.Bounds.Y);RecordImage(cached.Image,new RectangleF(point.X*UiScale,point.Y*UiScale,cached.Bounds.Width*scale*UiScale,cached.Bounds.Height*scale*UiScale),false);return;}
            var state = g.Save(); g.ResetTransform(); PointF position = Screen(cached.Bounds.X, cached.Bounds.Y);
            g.DrawImageUnscaled(cached.Image, (int)System.Math.Round(position.X * UiScale), (int)System.Math.Round(position.Y * UiScale)); g.Restore(state);
        }
    }
}
