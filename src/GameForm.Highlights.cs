using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Sovereign
{
    public partial class GameForm
    {
        private readonly Dictionary<Land, GraphicsPath> highlightPaths = new Dictionary<Land, GraphicsPath>();
        private RectangleF highlightCoverage;
        private float highlightScale;
        private void ClearHighlights()
        {
            foreach (var path in highlightPaths.Values) path.Dispose(); highlightPaths.Clear();
        }
        private GraphicsPath HighlightPath(Land land)
        {
            var view = new RectangleF(cx - mapRect.Width / scale / 2, cy - mapRect.Height / scale / 2, mapRect.Width / scale, mapRect.Height / scale);
            if (highlightScale != scale || !highlightCoverage.Contains(view) || highlightPaths.Count > 6) {
                ClearHighlights(); highlightScale = scale; highlightCoverage = view; highlightCoverage.Inflate(140 / scale, 140 / scale);
            }
            GraphicsPath path;
            if (!highlightPaths.TryGetValue(land, out path)) {
                path = land.Bounds.IntersectsWith(highlightCoverage) ? land.ClipDisplayPath(highlightCoverage, scale) : new GraphicsPath();
                highlightPaths.Add(land, path);
            }
            return path;
        }
        private void DrawRegionHighlight(Graphics g, int region, bool selectedRegion)
        {
            if (region < 0) return; var path = HighlightPath(W.Regions[region]); if (path.PointCount == 0) return;
            using (var fill = new SolidBrush(selectedRegion ? Color.FromArgb(43, 246, 238, 181) : Color.FromArgb(65, 255, 245, 199))) g.FillPath(fill, path);
            if (selectedRegion) using (var outer = new Pen(Color.FromArgb(120, 100, 98, 64), 4 / scale)) g.DrawPath(outer, path);
            using (var pen = new Pen(selectedRegion ? Color.FromArgb(99, 93, 54) : Color.FromArgb(85, 91, 70), 1.6f / scale)) g.DrawPath(pen, path);
        }
    }
}
