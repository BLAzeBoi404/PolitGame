using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        // Drawn above the cached atlas so issuing orders never rebuilds its geometry.
        private void DrawFrontRegions(Graphics g)
        {
            if (scale < 7) return;
            var visible = new RectangleF(cx - mapRect.Width / scale / 2, cy - mapRect.Height / scale / 2, mapRect.Width / scale, mapRect.Height / scale);
            foreach (var plan in E.S.Fronts.Where(p => p.Owner == E.S.Player && p.Active && p.ArmyIds.Count > 0)) {
                using (var shade = new SolidBrush(Color.FromArgb(45, plan.Stance == FrontStance.Advance ? Palette.Red : Palette.Gold)))
                using (var border = new Pen(plan.Stance == FrontStance.Advance ? Palette.Red : Palette.Gold, 1.6f / scale)) {
                    border.DashStyle = DashStyle.Dash;
                    foreach (int region in E.FrontRegions(plan)) {
                        var land = W.Regions[region]; if (!land.Bounds.IntersectsWith(visible)) continue;
                        g.FillPath(shade, land.DisplayPath(scale)); g.DrawPath(border, land.DisplayPath(scale));
                    }
                }
            }
        }
        private void DrawBattleMarkers(Graphics g)
        {
            var enemies = E.S.Armies.Where(a => E.AtWar(E.S.Player, a.Owner)).GroupBy(a => a.Region).ToDictionary(group => group.Key, group => group.ToList());
            foreach (var unit in E.S.Armies.Where(a => a.Owner == E.S.Player && a.Target >= 0 && a.Days <= 1.1)) {
                if (!enemies.ContainsKey(unit.Target)) continue;
                PointF point = Screen(W.Regions[unit.Target].X, W.Regions[unit.Target].Y);
                if (!mapRect.Contains(point)) continue;
                var box = new RectangleF(point.X - 10, point.Y - 37, 20, 20);
                Fill(g, Palette.Panel, box.X, box.Y, box.Width, box.Height);
                Line(g, Palette.Red, box.X + 5, box.Y + 5, box.Right - 5, box.Bottom - 5, 2);
                Line(g, Palette.Red, box.X + 5, box.Bottom - 5, box.Right - 5, box.Y + 5, 2);
                int region = unit.Target;
                hits.Add(new Hit { Rect = box, Label = "battle:" + unit.Id, Action = () => { selected = region; army = unit.Id; Invalidate(); }, Tip = "Бой: соединение №" + unit.Id + " · обороняющихся частей " + enemies[region].Count });
            }
        }
    }
}
