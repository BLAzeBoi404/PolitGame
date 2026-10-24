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
            Fill(g, Palette.Panel, 0, 0, VW, 99);
            Fill(g, Palette.Card, sidebar, 60, VW - sidebar, 38);
            Line(g, Palette.Line, 0, 98, VW, 98);
            Crest(g, 15, 16, 24, Palette.Gold);
            Book(g, "SOVEREIGN", 45, 18, 15, Palette.Text, true);
            Txt(g, "СТРАТЕГИЧЕСКИЙ ШТАБ", 16, 49, 9, Palette.Muted, true);
            Txt(g, W.Countries[E.S.Player].Name, 16, 72, 14, Palette.Gold, true, sidebar - 28);
            float x = sidebar + 22, width = (VW - sidebar - 300) / 5;
            string[] labels = { "КАЗНА", "ВВП / ГОД", "БЮДЖЕТ / НЕД.", "СТАБИЛЬНОСТЬ", "НАСЕЛЕНИЕ" };
            string[] values = { Money(E.Player.Treasury), Money(E.Player.GDP), (E.Player.Revenue >= E.Player.Spending ? "+" : "") + Money(E.Player.Revenue - E.Player.Spending), Num(E.Player.Stability) + "%", Num(E.Player.Population) + " млн" };
            for (int i = 0; i < 5; i++) {
                int index = i; float sx = x + i * width;
                Txt(g, labels[i], sx, 10, 9, Palette.Muted, true);
                Txt(g, values[i], sx - 1, 29, 18, i == 2 ? (E.Player.Revenue >= E.Player.Spending ? Palette.Teal : Palette.Red) : Palette.Text, true, width - 12);
                if (i > 0) Line(g, Palette.Line, sx - 12, 14, sx - 12, 46);
                hits.Add(new Hit { Rect = new RectangleF(sx - 4, 4, width - 4, 51), Action = () => Navigate(index == 3 ? "Политика" : index == 0 || index == 2 ? "Экономика" : "Сводка"), Tip = labels[i] + ": открыть подробности. Деньги в млрд расчётных долларов." });
            }
            float dateX = VW - 261;
            Txt(g, E.S.Date.ToString("dd MMMM yyyy", new CultureInfo("ru-RU")), dateX, 13, 17, Palette.Gold, true);
            string[] speeds = { "Пауза", "1×", "3×", "7×" };
            for (int i = 0; i < speeds.Length; i++) {
                int val = i; Button(g, speeds[i], dateX + (i == 0 ? 0 : 68 + (i - 1) * 48), 62, i == 0 ? 62 : 43, () => { speed = val; Invalidate(); }, speed == i, "Скорость симуляции", 29);
            }
            int ready = E.S.Armies.Count(a => a.Owner == E.S.Player && a.TrainingDays == 0);
            string research = E.Player.Research < 0 ? "Исследование не выбрано" : Rules.Techs[E.Player.Research] + " · " + E.Player.ResearchProgress.ToString("0") + "%";
            Button(g, research, x - 4, 64, 270, () => Navigate("Исследования"), E.Player.Research < 0, "Открыть исследования", 26);
            Button(g, "Части " + ready + "  ·  фронты " + E.S.Fronts.Count(p => p.Owner == E.S.Player && p.Active), x + 278, 64, 205, () => Navigate("Армия"), false, "Вооружённые силы и приказы", 26);
            Button(g, "Серии " + E.Player.Lines.Count(l => l.Active) + "  /  склад " + E.Player.Designs.Sum(d => d.Stock), x + 495, 64, Math.Min(205, dateX - x - 516), () => { equipmentProduction = true; Navigate("Производство"); }, false, "Производство и оснащение", 26);
        }
        private void DrawNav(Graphics g)
        {
            Fill(g, Palette.Panel, 0, 99, sidebar, VH - 99); Line(g, Palette.Line, sidebar - 1, 99, sidebar - 1, VH);
            for (int i = 0; i < Navigation.Length; i++) {
                string target = Navigation[i]; float y = 142 + i * 40 + (i >= 5 ? 30 : 0);
                if (i == 0 || i == 5) Txt(g, i == 0 ? "УПРАВЛЕНИЕ" : "СТРАТЕГИЯ", 17, y - 27, 9, Palette.Muted, true);
                var rect = new RectangleF(8, y, sidebar - 16, 35); bool active = page == target;
                if (active || rect.Contains(mouse)) Fill(g, active ? Palette.Selected : Palette.Hover, rect.X, rect.Y, rect.Width, rect.Height);
                if (active) Fill(g, Palette.Gold, 0, y + 5, 3, 25);
                NavIcon(g, i == 0 ? 8 : i - 1, 19, y + 9, 16, active ? Palette.Gold : Palette.Muted);
                Txt(g, target, 44, y + 9, 13, active ? Palette.Gold : Palette.Text, active);
                hits.Add(new Hit { Rect = rect, Label = "nav:" + target, Action = () => Navigate(target), Tip = target + (i < 9 ? " · " + prefs.KeyLabel("Page" + i) : "") });
            }
            Line(g, Palette.Line, 17, VH - 207, sidebar - 17, VH - 207);
            Button(g, "Сохранить · " + prefs.KeyLabel("Save"), 12, VH - 188, sidebar - 24, Save, false, "Сохранить кампанию");
            Button(g, "Настройки", 12, VH - 147, sidebar - 24, () => { menuBack = "game"; OpenMenu("settings"); });
            Button(g, "Главное меню", 12, VH - 106, sidebar - 24, () => OpenMenu("main"));
            Button(g, "Справочник · " + prefs.KeyLabel("Help"), 12, VH - 65, sidebar - 24, () => { speed = 0; showHelp = !showHelp; Invalidate(); });
        }
        private void DrawFooter(Graphics g, float y)
        {
            Fill(g, Palette.Panel, sidebar, y, VW - sidebar, 52); Line(g, Palette.Line, sidebar, y, VW, y);
            Txt(g, "ХРОНИКА", sidebar + 20, y + 10, 9, Palette.Gold, true);
            if (E.S.News.Count > 0) {
                var news = E.S.News[0];
                Txt(g, news.Date + "  /  " + news.Text, sidebar + 112, y + 9, 12, news.Type == 2 ? Palette.Red : Palette.Text, false, VW - sidebar - 140);
            }
            Txt(g, (speed == 0 ? "II  ПАУЗА" : "▶  ВРЕМЯ ИДЁТ") + "    ·    " + prefs.KeyLabel("Pause") + ": пауза    /    " + prefs.KeyLabel("Save") + ": сохранить    /    " + prefs.KeyLabel("Help") + ": справочник", sidebar + 20, y + 32, 9, Palette.Muted);
            hits.Add(new Hit { Rect = new RectangleF(sidebar, y, VW - sidebar, 29), Action = () => Navigate("Хроника"), Tip = "Открыть всю хронику событий" });
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
        private void DrawInspector(Graphics g)
        {
            float x = VW - 318, top = 99, w = 318;
            Fill(g, Palette.Panel, x, top, w, VH - top - 52); Line(g, Palette.Line, x, top, x, VH - 52);
            if (selected < 0) { Txt(g, "РАЗВЕДКА ТЕРРИТОРИИ", x + 22, 125, 10, Palette.Gold, true); Para(g, "Выберите область на карте, чтобы увидеть население, промышленность и доступные приказы.", x + 22, 170, w - 44, 160, 16); return; }
            var p = E.S.Provinces[selected]; bool own = p.Owner == E.S.Player;
            Color relation = own ? Palette.Teal : E.AtWar(E.S.Player, p.Owner) ? Palette.Red : E.Allied(E.S.Player, p.Owner) ? Palette.Gold : Palette.Muted;
            Fill(g, Palette.Card, x + 1, 100, w - 1, 155);
            CountrySilhouette(g, p.Owner, new RectangleF(x + 145, 114, 147, 122), Color.FromArgb(60, relation));
            Txt(g, own ? "СВОЯ ТЕРРИТОРИЯ" : E.AtWar(E.S.Player, p.Owner) ? "ВОЕННЫЕ ДЕЙСТВИЯ" : E.Allied(E.S.Player, p.Owner) ? "СОЮЗНИК" : "ИНОСТРАННАЯ ТЕРРИТОРИЯ", x + 22, 122, 9, relation, true);
            Para(g, W.Regions[selected].Name, x + 20, 151, 276, 58, 22, Palette.Text);
            Txt(g, W.Countries[p.Owner].Name, x + 22, 212, 13, Palette.Muted, false, 270);
            Txt(g, "ОБЛАСТЬ " + selected.ToString("0000") + "   /   " + W.Countries[p.Owner].Code, x + 22, 229, 9, Palette.Muted);
            Txt(g, "НАСЕЛЕНИЕ", x + 22, 274, 10, Palette.Muted, true); Txt(g, Num(p.Population) + " млн", x + 21, 294, 27, Palette.Text, true);
            string[] names = { "Инфраструктура", "Спокойствие населения" }; double[] values = { p.Infrastructure, 100 - p.Unrest };
            for (int i = 0; i < 2; i++) {
                float y = 353 + i * 54; Txt(g, names[i], x + 22, y, 12, Palette.Muted);
                Txt(g, values[i].ToString("0") + "%", x + 252, y, 12, Palette.Text, true);
                Progress(g, x + 22, y + 24, 274, values[i] / 100, i == 0 ? Palette.Gold : Palette.Teal);
            }
            Section(g, "СЫРЬЕВАЯ БАЗА", x + 22, 476, 274);
            string[] resource = { "Почва", "Нефть", "Руда" }; double[] potential = { p.Soil, p.Oil, p.Ore };
            for (int i = 0; i < 3; i++) { float xx = x + 22 + i * 94; Txt(g, resource[i], xx, 515, 11, Palette.Muted); Txt(g, Num(potential[i]), xx, 537, 22, Palette.Text, true); }
            Section(g, "ГАРНИЗОН", x + 22, 591, 274);
            var units = E.S.Armies.Where(a => a.Region == selected).ToList();
            Txt(g, units.Count + " соединений в области", x + 22, 625, 13, Palette.Text);
            var chosen = E.S.Armies.FirstOrDefault(a => a.Id == army);
            if (chosen != null && chosen.Owner == E.S.Player) {
                Txt(g, "Выбрана часть №" + chosen.Id + " · снабжение " + chosen.Supply.ToString("0") + "%", x + 22, 652, 11, Palette.Teal, false, 274);
                var plan = E.FrontFor(chosen.Id);
                Txt(g, plan == null ? "ПКМ по области: приказ движения" : plan.Name + " · ПКМ: ручное управление", x + 22, 675, 10, Palette.Muted, false, 274);
            }
            float yAction = VH - 160;
            Button(g, own ? "Развивать область →" : "Дипломатическое досье →", x + 22, yAction, 274, () => { if (own) { equipmentProduction = false; Navigate("Производство"); } else { foreign = p.Owner; Navigate("Дипломатия"); } }, true);
            Button(g, "Управление войсками →", x + 22, yAction + 43, 274, () => { if (!own) foreign = p.Owner; Navigate("Армия"); });
        }
    }
}
