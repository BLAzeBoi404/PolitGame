using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private float researchOffset, researchZoom = 1;
        private bool researchFocusPending, researchDragging, researchMoved;
        private Point researchLast, researchOrigin;
        private RectangleF researchViewport;
        private const float TechColumn = 214, TechRow = 100, TechWidth = 184, TechHeight = 84;
        private bool ResearchCanvasActive => menuScreen == "" && page == "Исследования" && detailPanel && researchTab == 0;
        private float ResearchContentWidth => (TechnologyRules.Branches[researchBranch].Max(d => d.Column) + 1) * TechColumn;
        private void ClampResearchOffset() { researchOffset = Math.Max(0, Math.Min(Math.Max(0, ResearchContentWidth * researchZoom - researchViewport.Width), researchOffset)); }
        private void FocusResearchSelection()
        {
            if (researchSelection < 6) return;
            researchOffset = (TechnologyRules.Catalog[researchSelection - 6].Column * TechColumn + TechWidth / 2) * researchZoom - researchViewport.Width / 2;
            ClampResearchOffset();
        }
        private void ZoomResearch(float next)
        {
            float center = (researchOffset + researchViewport.Width / 2) / researchZoom;
            researchZoom = Math.Max(.45f, Math.Min(1f, (float)Math.Round(next * 20) / 20)); researchOffset = center * researchZoom - researchViewport.Width / 2; ClampResearchOffset(); Invalidate();
        }
        private RectangleF TechnologyRect(TechnologyDefinition d)
        {
            return new RectangleF(researchViewport.X + d.Column * TechColumn * researchZoom - researchOffset,
                researchViewport.Y + 32 + d.Lane * TechRow * researchZoom, TechWidth * researchZoom, TechHeight * researchZoom);
        }
        private bool TechnologyMilestone(int id) => id == 27 || id == 31 || id == 39 || id == 47 || id == 55 || id == 63 || id == 71;
        private void DrawTechnologyTree(Graphics g, RectangleF viewport)
        {
            researchViewport = viewport; ClampResearchOffset();
            if (researchFocusPending) { FocusResearchSelection(); researchFocusPending = false; }
            float x = viewport.X, controlsY = 318;
            Button(g, "←", x, controlsY, 34, () => { researchOffset -= 250; ClampResearchOffset(); Invalidate(); }, false, "Предыдущие этапы", 28); hits.Last().Label = "research:pan:left";
            Button(g, "→", x + 40, controlsY, 34, () => { researchOffset += 250; ClampResearchOffset(); Invalidate(); }, false, "Следующие этапы", 28); hits.Last().Label = "research:pan:right";
            Button(g, "−", x + 86, controlsY, 30, () => ZoomResearch(researchZoom / 1.15f), false, "Уменьшить дерево", 28); hits.Last().Label = "research:zoom:out";
            Button(g, "+", x + 122, controlsY, 30, () => ZoomResearch(researchZoom * 1.15f), false, "Увеличить дерево", 28); hits.Last().Label = "research:zoom:in";
            Button(g, "Обзор", x + 165, controlsY, 86, () => { ZoomResearch((float)Math.Floor(viewport.Width / ResearchContentWidth * 20) / 20); researchOffset = 0; }, false, "Показать всю ветку", 28); hits.Last().Label = "research:fit";
            Button(g, "К выбору", x + 259, controlsY, 104, () => { researchZoom = 1; FocusResearchSelection(); Invalidate(); }, false, "К выбранной технологии в обычном масштабе", 28); hits.Last().Label = "research:focus";
            Txt(g, (researchZoom * 100).ToString("0") + "% · перетаскивание / колесо", x + 376, controlsY + 6, 11, Palette.Muted, false, viewport.Width - 380);
            var state = g.Save(); g.SetClip(viewport, CombineMode.Intersect);
            Fill(g, Color.FromArgb(19, 28, 26), viewport.X, viewport.Y, viewport.Width, viewport.Height);
            var nodes = TechnologyRules.Branches[researchBranch]; var n = E.Player; var a = n.ScienceTree;
            for (int col = 0; col <= nodes.Max(d => d.Column); col++) {
                float columnX = x + col * TechColumn * researchZoom - researchOffset;
                if (col % 2 == 0) Fill(g, Color.FromArgb(24, 34, 30), columnX, viewport.Y, TechColumn * researchZoom, viewport.Height);
                Txt(g, (col + 1).ToString("00") + "  " + TechnologyRules.StageNames[col], columnX + 8, viewport.Y + 8, 10 * researchZoom, Palette.Gold, true, TechWidth * researchZoom);
            }
            foreach (var node in nodes) foreach (int id in node.Prerequisites) {
                var previous = TechnologyRules.Catalog[id]; if (previous.Branch != researchBranch) continue;
                var from = TechnologyRect(previous); var to = TechnologyRect(node); float y1 = from.Y + from.Height / 2, y2 = to.Y + to.Height / 2;
                bool selectedEdge = researchSelection == node.Id + 6 || researchSelection == id + 6;
                Color color = a.Known[id] ? Palette.Teal : selectedEdge ? Palette.Gold : Color.FromArgb(68, 83, 72);
                float elbow = from.Right + 14 * researchZoom;
                using (var pen = new Pen(color, selectedEdge ? 2 : 1)) {
                    if (node.Column - previous.Column > 1) {
                        float corridor = to.Top - 8 * researchZoom, arrival = to.Left - 12 * researchZoom;
                        g.DrawLines(pen, new[] { new PointF(from.Right, y1), new PointF(elbow, y1), new PointF(elbow, corridor), new PointF(arrival, corridor), new PointF(arrival, y2), new PointF(to.Left - 4, y2) });
                    } else g.DrawLines(pen, new[] { new PointF(from.Right, y1), new PointF(elbow, y1), new PointF(elbow, y2), new PointF(to.Left - 4, y2) });
                    g.DrawLines(pen, new[] { new PointF(to.Left - 8, y2 - 3), new PointF(to.Left - 4, y2), new PointF(to.Left - 8, y2 + 3) });
                }
            }
            foreach (var d in nodes) {
                int project = d.Id + 6; var box = TechnologyRect(d); if (!viewport.IntersectsWith(box)) continue;
                bool known = a.Known[d.Id], active = a.ActiveProject == project, chosen = researchSelection == project, available = E.ResearchReason(project) == null;
                int queued = a.Queue.IndexOf(project); Color accent = known ? Palette.Teal : chosen || active || TechnologyMilestone(d.Id) ? Palette.Gold : available ? Palette.Text : Palette.Muted;
                Fill(g, chosen ? Palette.Selected : available || known ? Palette.Card : Color.FromArgb(28, 37, 33), box.X, box.Y, box.Width, box.Height);
                using (var pen = new Pen(accent, chosen ? 2 : 1)) g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                float z = researchZoom;
                if (TechnologyMilestone(d.Id)) {
                    float center = box.X + 14 * z, top = box.Y + 14 * z;
                    using (var brush = new SolidBrush(accent)) g.FillPolygon(brush, new[] { new PointF(center, top - 6 * z), new PointF(center + 6 * z, top), new PointF(center, top + 6 * z), new PointF(center - 6 * z, top) });
                } else TechnologyGlyph(g, d.Branch, box.X + 7 * z, box.Y + 7 * z, 14 * z, accent);
                Txt(g, "УР. " + d.Foundation, box.Right - 48 * z, box.Y + 8 * z, 9 * z, accent, true, 43 * z);
                Para(g, d.Name, box.X + 8 * z, box.Y + 29 * z, box.Width - 16 * z, 32 * z, 12 * z, known || available || chosen ? Palette.Text : Palette.Muted);
                string status = known ? "ОСВОЕНО" : active ? (a.Paused ? "ПАУЗА · " : "В РАБОТЕ · ") + n.ResearchProgress.ToString("0") + "%" : queued >= 0 ? "ПЛАН · " + (queued + 1) : available ? "ДОСТУПНО" : "ПРЕДПОСЫЛКИ";
                Txt(g, status, box.X + 8 * z, box.Bottom - 20 * z, 9 * z, accent, true, box.Width - 16 * z);
                double progress = active ? n.ResearchProgress : a.StoredProgress[project];
                if (progress > 0 && !known) Progress(g, box.X + 2, box.Bottom - 3, box.Width - 4, progress / 100, Palette.Teal);
                hits.Add(new Hit { Rect = RectangleF.Intersect(box, viewport), Label = "technology:" + d.Id, Tip = d.Name + ": " + d.Description + "\n" + (E.ResearchReason(project) ?? "Можно исследовать"), Action = () => { researchSelection = project; Invalidate(); } });
            }
            g.Restore(state);
            float trackY = viewport.Bottom + 6, extent = ResearchContentWidth * researchZoom;
            Fill(g, Palette.Line, x, trackY, viewport.Width, 4);
            Fill(g, Palette.Gold, x + researchOffset / extent * viewport.Width, trackY, Math.Min(viewport.Width, viewport.Width * viewport.Width / extent), 4);
            Txt(g, "Освоено " + a.Known.Count(k => k) + " / " + TechnologyRules.Count + " · " + nodes.Count(d => a.Known[d.Id]) + "/12 в отрасли · ◇ смена технологического уклада", x, 814, 11, Palette.Muted, false, viewport.Width);
        }
        private void TechnologyGlyph(Graphics g, int branch, float x, float y, float size, Color color)
        {
            var state = g.Save(); g.TranslateTransform(x, y); g.ScaleTransform(size / 16, size / 16);
            using (var pen = new Pen(color, 1.2f)) using (var brush = new SolidBrush(color)) {
                if (branch == 0) {
                    g.DrawLine(pen, 8, 1, 8, 16);
                    for (int i = 0; i < 3; i++) { float yy = 3 + i * 4; g.DrawLine(pen, 8, yy + 3, 3, yy); g.DrawLine(pen, 8, yy + 3, 13, yy); }
                } else if (branch == 1) g.DrawPolygon(pen, new[] { new PointF(10, 0), new PointF(3, 9), new PointF(8, 9), new PointF(6, 16), new PointF(14, 6), new PointF(9, 6) });
                else if (branch == 2) { g.DrawEllipse(pen, 4, 4, 8, 8); for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(pen, 8 + (float)Math.Cos(a) * 5, 8 + (float)Math.Sin(a) * 5, 8 + (float)Math.Cos(a) * 8, 8 + (float)Math.Sin(a) * 8); } }
                else if (branch == 3) { g.DrawRectangle(pen, 1, 2, 14, 11); g.DrawLine(pen, 6, 2, 6, 13); g.DrawLine(pen, 10, 2, 10, 13); g.FillEllipse(brush, 2, 13, 3, 3); g.FillEllipse(brush, 11, 13, 3, 3); }
                else if (branch == 4) { g.FillRectangle(brush, 6, 1, 4, 14); g.FillRectangle(brush, 1, 6, 14, 4); }
                else { g.DrawPolygon(pen, new[] { new PointF(2, 1), new PointF(14, 1), new PointF(13, 10), new PointF(8, 16), new PointF(3, 10) }); g.DrawLine(pen, 8, 4, 8, 12); g.DrawLine(pen, 5, 7, 11, 7); }
            }
            g.Restore(state);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { researchDragging = false; panning = false; }
        }
        private bool ResearchMouseDown(MouseEventArgs e)
        {
            var point = Logical(e.Location); if (!ResearchCanvasActive || !researchViewport.Contains(point) || e.Button != MouseButtons.Left) return false;
            researchDragging = true; researchMoved = false; researchOrigin = researchLast = point; Capture = true; return true;
        }
        private bool ResearchMouseMove(MouseEventArgs e)
        {
            if (!researchDragging) return false; var point = Logical(e.Location);
            if (Math.Abs(point.X - researchOrigin.X) + Math.Abs(point.Y - researchOrigin.Y) > 4) researchMoved = true;
            if (researchMoved) { researchOffset -= point.X - researchLast.X; ClampResearchOffset(); Invalidate(); }
            researchLast = point; return true;
        }
        private bool ResearchMouseUp()
        {
            if (!researchDragging && !researchMoved) return false;
            bool movedTree = researchMoved; researchDragging = false; researchMoved = false; Capture = false; return movedTree;
        }
        private bool ResearchMouseWheel(MouseEventArgs e)
        {
            if (!ResearchCanvasActive || !researchViewport.Contains(Logical(e.Location))) return false;
            if ((ModifierKeys & Keys.Control) != 0) ZoomResearch(researchZoom * (e.Delta > 0 ? 1.15f : 1 / 1.15f));
            else { researchOffset += e.Delta < 0 ? 180 : -180; ClampResearchOffset(); Invalidate(); }
            return true;
        }
    }
}
