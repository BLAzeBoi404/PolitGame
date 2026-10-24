using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private static readonly string[] Navigation = { "Сводка", "Карта", "Экономика", "Производство", "Исследования", "Дипломатия", "Армия", "Конструктор", "Политика", "Хроника" };
        private void DrawHeader(Graphics g)
        {
            Fill(g, Palette.Panel, 0, 0, VW, 58);
            Line(g, Palette.Gold, 0, 1, VW, 1);
            Crest(g, 17, 14, 28, Palette.Gold);
            Book(g, W.Countries[E.S.Player].Name, 58, 10, 19, Palette.Text, true, 210);
            Txt(g, "S O V E R E I G N   /   " + W.Countries[E.S.Player].Code, 59, 37, 9, Palette.Gold);
            float x = 285, width = (VW - 555) / 5;
            string[] labels = { "КАЗНА", "ВВП / ГОД", "БЮДЖЕТ / НЕД.", "СТАБИЛЬНОСТЬ", "НАСЕЛЕНИЕ" };
            string[] values = { Money(E.Player.Treasury), Money(E.Player.GDP), (E.Player.Revenue >= E.Player.Spending ? "+" : "") + Money(E.Player.Revenue - E.Player.Spending), Num(E.Player.Stability) + "%", Num(E.Player.Population) + " млн" };
            for (int i = 0; i < 5; i++) {
                int index = i; float sx = x + i * width;
                Line(g, Palette.Line, sx - 12, 11, sx - 12, 46);
                Txt(g, labels[i], sx, 10, 9, Palette.Muted, true);
                Txt(g, values[i], sx, 27, 16, i == 2 ? (E.Player.Revenue >= E.Player.Spending ? Palette.Teal : Palette.Red) : Palette.Text, true, width - 18);
                hits.Add(new Hit { Rect = new RectangleF(sx - 4, 4, width - 6, 49), Action = () => Navigate(index == 3 ? "Политика" : index == 0 || index == 2 ? "Экономика" : "Сводка"), Tip = labels[i] + ": открыть ведомость. Деньги в млрд расчётных долларов." });
            }
            float dateX = VW - 250;
            Txt(g, E.S.Date.ToString("dd MMMM yyyy", new CultureInfo("ru-RU")), dateX, 10, 16, Palette.Gold, true);
            string[] speeds = { "II", "1×", "3×", "7×" };
            for (int i = 0; i < 4; i++) {
                int val = i; Button(g, speeds[i], dateX + i * 47, 33, 41, () => { speed = val; Invalidate(); }, speed == i, i == 0 ? "Пауза · " + prefs.KeyLabel("Pause") : "Скорость симуляции", 22);
            }
            Button(g, "≡", VW - 44, 11, 33, () => OpenMenu("main"), false, "Главное меню · Esc", 35);
        }
        private void DrawNav(Graphics g)
        {
            Fill(g, Palette.Card, 0, 58, VW, 41); Line(g, Palette.Line, 0, 58, VW, 58); Line(g, Palette.Gold, 0, 98, VW, 98);
            float tab = (VW - 177) / Navigation.Length;
            for (int i = 0; i < Navigation.Length; i++) {
                string target = Navigation[i]; float x = 8 + i * tab;
                bool active = page == target; var rect = new RectangleF(x, 61, tab - 2, 34);
                if (active || rect.Contains(mouse)) Fill(g, active ? Palette.Selected : Palette.Hover, x, 61, tab - 2, 34);
                NavIcon(g, i == 0 ? 8 : i - 1, x + 8, 68, 19, active ? Palette.Gold : Palette.Muted);
                Txt(g, target, x + 35, 72, 12, active ? Palette.Gold : Palette.Text, active, tab - 38);
                if (active) Line(g, Palette.Gold, x + 5, 95, x + tab - 7, 95, 2);
                hits.Add(new Hit { Rect = rect, Label = "nav:" + target, Action = () => Navigate(page == target && target != "Карта" ? "Карта" : target), Tip = target + (i < 9 ? " · " + prefs.KeyLabel("Page" + i) : "") });
            }
            Button(g, "Сохранить", VW - 164, 64, 94, Save, false, "Сохранить · " + prefs.KeyLabel("Save"), 29);
            Button(g, "?", VW - 60, 64, 44, () => { speed = 0; showHelp = !showHelp; Invalidate(); }, false, "Справочник", 29);
        }
        private void DrawFooter(Graphics g, float y)
        {
            Fill(g, Palette.Panel, 0, y, VW, 34); Line(g, Palette.Line, 0, y, VW, y);
            Txt(g, speed == 0 ? "II  ПАУЗА" : "▶  ВРЕМЯ ИДЁТ", 16, y + 10, 10, Palette.Gold, true);
            if (E.S.News.Count > 0) {
                var news = E.S.News[0];
                Txt(g, news.Date + "  /  " + news.Text, 138, y + 9, 12, news.Type == 2 ? Palette.Red : Palette.Text, false, VW - 390);
            }
            Txt(g, prefs.KeyLabel("Pause") + ": пауза   ·   ЛКМ: выбор   ·   ПКМ: приказ", VW - 285, y + 11, 9, Palette.Muted);
            hits.Add(new Hit { Rect = new RectangleF(130, y, VW - 425, 34), Action = () => Navigate("Хроника"), Tip = "Открыть всю хронику событий" });
            DrawMapLayerPicker(g);
        }
        // Opaque frames also consume input in their empty areas, so the atlas never
        // receives clicks or drag starts through a management panel.
        private void DocketFrame(Graphics g, RectangleF box, string title)
        {
            Fill(g, Color.FromArgb(65, 0, 0, 0), box.X + 5, box.Y + 5, box.Width, box.Height);
            Fill(g, Palette.Panel, box.X, box.Y, box.Width, box.Height);
            Fill(g, Palette.Card, box.X + 1, box.Y + 1, box.Width - 2, 42);
            Line(g, Palette.Gold, box.X, box.Y, box.Right, box.Y);
            Line(g, Palette.Line, box.Right, box.Y, box.Right, box.Bottom);
            Line(g, Palette.Line, box.X, box.Bottom, box.Right, box.Bottom);
            Book(g, title, box.X + 17, box.Y + 12, 18, Palette.Text, true, box.Width - 75);
            Line(g, Palette.Line, box.X + 10, box.Y + 42, box.Right - 10, box.Y + 42);
            hits.Add(new Hit { Rect = box, Action = () => {}, Tip = "" });
        }
        private void CountrySilhouette(Graphics g, int country, RectangleF box, Color color)
        {
            var land = W.Countries[country];
            // Focus the main landmass rather than shrinking overseas territories
            // and the date-line extent into an unreadable world-wide thumbnail.
            var main = land.Rings.OrderByDescending(ring => {
                double area = 0; for (int i = 0; i < ring.Length; i++) { var a = ring[i]; var b = ring[(i + 1) % ring.Length]; area += a.X * b.Y - b.X * a.Y; } return Math.Abs(area);
            }).First();
            var bounds = RectangleF.FromLTRB(main.Min(p => p.X), main.Min(p => p.Y), main.Max(p => p.X), main.Max(p => p.Y));
            float zoom = Math.Min(box.Width / Math.Max(1, bounds.Width), box.Height / Math.Max(1, bounds.Height));
            var state = g.Save(); g.SetClip(box, CombineMode.Intersect);
            g.TranslateTransform(box.X + box.Width / 2, box.Y + box.Height / 2);
            g.ScaleTransform(zoom, zoom); g.TranslateTransform(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2);
            using (var brush = new SolidBrush(color)) g.FillPath(brush, land.DisplayPath(zoom));
            g.Restore(state);
        }
    }
}
