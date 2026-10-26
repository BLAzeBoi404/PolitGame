using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        public void ExpandPreview() { detailPanel = true; }
        private void DrawCommandPanel(Graphics g)
        {
            if (page == "Карта") return;
            float width = detailPanel ? ExpandedWidth : DocketWidth;
            if (detailPanel) {
                DocketFrame(g, new RectangleF(0, 99, width, VH - 133), "");
                var state = g.Save(); g.SetClip(new RectangleF(0, 99, width, VH - 133));
                workspaceWidth = width;
                try { DrawPage(g); } finally { workspaceWidth = 0; g.Restore(state); }
                Button(g, "Свернуть", width - 162, 111, 94, () => { detailPanel = false; Invalidate(); }, false, "Вернуть компактную панель", 28);
            } else {
                DocketFrame(g, new RectangleF(0, 99, width, VH - 133), aviationPage ? "Воздушные силы" : manpowerPage ? "Личный состав" : page);
                if (aviationPage) DrawAviationPanel(g); else if (manpowerPage) DrawManpowerPanel(g); else DrawCompactPanel(g);
                Button(g, (aviationPage || manpowerPage) ? "Наземные войска →" : "Подробное управление →", 18, VH - 83, width - 36, () => { if (aviationPage || manpowerPage) Navigate("Армия"); else detailPanel = true; Invalidate(); }, false, "Развернуть ведомость, сохранив карту на экране", 32);
            }
            Button(g, "×", width - 48, 107, 32, () => Navigate("Карта"), false, "Закрыть панель", 29);
        }
        private void LedgerRow(Graphics g, string label, string value, float y, Color? color = null)
        {
            if (((int)y / 29) % 2 == 0) Fill(g, Palette.Card, 13, y - 3, DocketWidth - 26, 28);
            Txt(g, label, 23, y + 2, 13, Palette.Muted, false, 220);
            Txt(g, value, 248, y + 1, 14, color ?? Palette.Text, true, 130);
        }
        private void CompactSection(Graphics g, string text, float y) { Section(g, text, 20, y, DocketWidth - 40); }
        private void DrawCompactPanel(Graphics g)
        {
            var n = E.Player; float w = DocketWidth - 40;
            if (page == "Экономика") {
                Txt(g, "НЕДЕЛЬНЫЙ БАЛАНС", 20, 159, 10, Palette.Muted, true);
                Book(g, (n.Revenue >= n.Spending ? "+" : "") + Money(n.Revenue - n.Spending), 19, 179, 28, n.Revenue >= n.Spending ? Palette.Teal : Palette.Red, true);
                Txt(g, "Динамика ВВП", 20, 226, 10, Palette.Muted);
                Spark(g, n.History, new RectangleF(21, 250, w - 2, 67), Palette.Gold);
                if (n.History.Count < 2) Txt(g, "Первые данные появятся через неделю", 23, 279, 12, Palette.Muted);
                LedgerRow(g, "Доходы", Money(n.Revenue), 335, Palette.Teal);
                LedgerRow(g, "Расходы", Money(n.Spending), 364, Palette.Red);
                LedgerRow(g, "Государственный долг", Money(n.Debt), 393);
                LedgerRow(g, "Импорт / экспорт", Num(n.Imports) + " / " + Num(n.Exports), 422);
                CompactSection(g, "РЫНОК / НЕДЕЛЬНЫЙ ВЫПУСК", 465);
                Txt(g, "ТОВАР", 24, 507, 9, Palette.Muted, true); Txt(g, "ВЫПУСК", 183, 507, 9, Palette.Muted, true); Txt(g, "ЗАПАС", 293, 507, 9, Palette.Muted, true);
                for (int i = 0; i < 8; i++) {
                    int good = i; float y = 531 + i * 27;
                    if (i % 2 == 0) Fill(g, Palette.Card, 14, y - 3, w + 12, 27);
                    Txt(g, Rules.Goods[i], 24, y, 12, Palette.Text, false, 152);
                    Txt(g, Num(n.Production[i]), 184, y, 12, Palette.Teal, false, 95);
                    Txt(g, Num(n.Stock[i]), 294, y, 12, n.Stock[i] < n.Demand[i] ? Palette.Red : Palette.Text, false, 80);
                    hits.Add(new Hit { Rect = new RectangleF(14, y - 3, w + 12, 27), Label = "market:" + i, Action = () => { marketGood = good; detailPanel = true; Invalidate(); }, Tip = Rules.Goods[i] + ": спрос " + Num(n.Demand[i]) + ", цена " + Num(n.Price[i] * 1000) + " млн/ед. Нажмите для производственной цепочки." });
                }
                Button(g, n.AutoTrade ? "Автоторговля: включена" : "Автоторговля: выключена", 20, 761, w, () => { n.AutoTrade = !n.AutoTrade; Invalidate(); }, n.AutoTrade, "Торговля использует капитал бизнеса", 31);
            } else if (page == "Исследования") {
                Txt(g, "НАУЧНАЯ ПРОГРАММА", 20, 160, 10, Palette.Gold, true);
                DoctrineLink(g, 202, 151, 174);
                Para(g, n.Research < 0 ? "Выберите фундаментальную отрасль или прикладную технологию." : E.ResearchStatus(E.S.Player), 20, 186, w, 56, 16, Palette.Text);
                Progress(g, 20, 249, w, n.Research < 0 ? 0 : n.ResearchProgress / 100, Palette.Gold);
                Button(g, "Технологии и план →", 20, 278, w, () => OpenResearchProject(researchSelection), true, "72 прикладные технологии, предпосылки и план работы", 32);
                string[] effect = { "Урожайность", "Выработка энергии", "Двигатели и корпуса", "Ходовая, баки, строительство", "Уровень жизни", "Броня, оружие, электроника" };
                for (int i = 0; i < 6; i++) {
                    int tech = i; float y = 326 + i * 67;
                    Txt(g, Rules.Techs[i] + "  /  " + n.Tech[i], 21, y, 14, Palette.Text, true, 245);
                    Txt(g, effect[i], 21, y + 26, 11, Palette.Muted, false, 245);
                    AvailableButton(g, n.ScienceTree.ActiveProject == i ? "В работе" : "Начать", 281, y + 4, 95, () => Message(E.StartResearch(tech)), n.Research < 0, "Одна программа за раз; скорость зависит от научного бюджета.", n.ScienceTree.ActiveProject == i, 30);
                    Line(g, Palette.Line, 20, y + 61, 376, y + 61);
                }
                Button(g, "Научный бюджет: " + Num(n.Science) + "% ВВП →", 20, 761, w, () => Navigate("Политика"), false, "Изменить финансирование", 31);
            } else if (page == "Политика") {
                LedgerRow(g, "Стабильность", Num(n.Stability) + "%", 166, Palette.Teal);
                LedgerRow(g, "Одобрение", Num(n.Approval) + "%", 195);
                LedgerRow(g, "Уровень жизни", Num(n.Living) + " / 100", 224);
                Button(g, "Решения и события →", 20, 254, 166, () => OpenNationalPolicy(), false, "Программы кабинета, повестка и действующие меры", 32);
                GovernmentLink(g, 194, 254, 182);
                CompactSection(g, "БЮДЖЕТ И ГОСУДАРСТВО", 303);
                CompactPolicy(g, "Налоги", n.Tax, 350, v => n.Tax = v, 5, 50, 1, "Высокие налоги сдерживают инвестиции.");
                CompactPolicy(g, "Социальные расходы", n.Social, 433, v => n.Social = v, 0, 15, .5, "Поддерживают уровень жизни и одобрение.");
                CompactPolicy(g, "Наука и образование", n.Science, 516, v => n.Science = v, 0, 10, .5, "Ускоряют исследования и рост образования.");
                CompactPolicy(g, "Военный бюджет", n.Military, 599, v => n.Military = v, .5, 15, .5, "Снабжение и восстановление соединений.");
                CompactPolicy(g, "Политическое давление", n.Repression, 682, v => n.Repression = v, 0, 100, 5, "Контроль ценой одобрения и выпуска.");
                Button(g, "Призыв и резерв →", 20, 758, w, OpenManpower, false, "Мобилизация влияет на промышленность и одобрение", 30);
            } else if (page == "Производство") {
                CompactSection(g, "ВОЕННО-ПРОМЫШЛЕННЫЙ КОМПЛЕКС", 160);
                LedgerRow(g, "Занято / мощность", E.AssignedFactories(E.S.Player) + " / " + E.FactoryCapacity(E.S.Player), 205);
                LedgerRow(g, "Готовая техника", n.Designs.Sum(d => d.Stock) + " компл.", 237, Palette.Teal);
                Button(g, "Каталог и заказ серии →", 20, 283, w, () => { equipmentProduction = true; detailPanel = true; Invalidate(); }, true);
                Button(g, "Государственные закупки →", 20, 371, w, OpenProcurement, false, "Товары и транспорт от частных предприятий");
                Button(g, "Строительство в области →", 20, 327, w, () => { equipmentProduction = false; detailPanel = true; Invalidate(); });
                CompactSection(g, "ДЕЙСТВУЮЩИЕ СЕРИИ", 427);
                var lines = n.Lines.Where(l => l.Completed < l.Quantity).ToList();
                int offset = Math.Min(scroll, Math.Max(0, lines.Count - 3));
                if (lines.Count == 0) Para(g, "Заказов пока нет. Создайте проект в конструкторе и назначьте заводы для выпуска техники.", 20, 476, w, 110, 15);
                for (int i = offset; i < Math.Min(lines.Count, offset + 3); i++) {
                    var line = lines[i]; float y = 475 + (i - offset) * 100;
                    Txt(g, E.FindDesign(E.S.Player, line.DesignId).Blueprint.Name, 21, y, 15, Palette.Text, true, 255);
                    Txt(g, E.ProductionStatus(E.S.Player, line), 21, y + 28, 11, Palette.Muted, false, w);
                    Progress(g, 21, y + 57, w, (line.Completed + line.Progress) / line.Quantity, Palette.Teal);
                    Button(g, line.Paused ? "Пуск" : "Пауза", 290, y - 3, 86, () => Message(E.ChangeLine(line.Id, line.Factories, !line.Paused)), line.Paused, "Остановить / возобновить серию", 28);
                    Txt(g, line.Completed + "/" + line.Quantity + " компл. · " + line.Factories + " заводов", 21, y + 72, 11, Palette.Gold);
                }
            } else if (page == "Армия") {
                var units = E.S.Armies.Where(a => a.Owner == E.S.Player).ToList();
                CompactSection(g, "ЧАСТИ / " + units.Count, 160);
                Button(g, "Резерв →", 168, 151, 92, OpenManpower, false, "Личный состав, мобилизация и штаты", 27);
                Button(g, "Авиация →", 270, 151, 106, OpenAviation, false, "Аэродромы, крылья и вылеты", 27);
                int offset = Math.Min(scroll, Math.Max(0, units.Count - 6));
                for (int i = offset; i < Math.Min(units.Count, offset + 6); i++) {
                    var a = units[i]; float y = 209 + (i - offset) * 80;
                    Button(g, "№" + a.Id + " · " + a.Design.Name, 20, y, w, () => FocusArmy(a), a.Id == army, "Выбрать на карте; ПКМ по области — приказ", 30);
                    Txt(g, W.Regions[a.Region].Name, 28, y + 36, 11, Palette.Muted, false, 190);
                    Txt(g, People(a.Personnel) + " чел. / орг. " + a.Organization.ToString("0") + "%", 208, y + 36, 11, a.Organization < 40 ? Palette.Red : Palette.Teal);
                    Progress(g, 28, y + 61, w - 16, a.Strength / 100, Palette.Teal);
                }
                Button(g, "Фронты и оснащение →", 20, 739, w, () => { detailPanel = true; Invalidate(); }, true);
                DoctrineLink(g, 20, 782, w);
            } else if (page == "Дипломатия") {
                if (foreign < 0 || foreign == E.S.Player) foreign = Enumerable.Range(0, W.Countries.Length).First(c => c != E.S.Player);
                var other = W.Countries[foreign]; int pair = E.Pair(E.S.Player, foreign);
                CountrySilhouette(g, foreign, new RectangleF(80, 170, 230, 135), Palette.Gold);
                Book(g, other.Name, 20, 329, 25, Palette.Text, true, w);
                LedgerRow(g, "Отношения", E.S.Relations[pair].ToString("+0;-0;0"), 385);
                LedgerRow(g, "Статус", E.AtWar(E.S.Player, foreign) ? "ВОЙНА" : E.Allied(E.S.Player, foreign) ? "Союз" : "Мир", 419);
                LedgerRow(g, "Торговый договор", E.S.Agreements[pair] ? "Есть" : "Нет", 453);
                Para(g, "Выберите государство щелчком по карте. Переговоры, доступ, союзы и условия мира — в подробном досье.", 20, 511, w, 100, 14);
                Button(g, "Найти государство…", 20, 636, w, ChooseForeign);
                Button(g, "Переговоры и договоры →", 20, 688, w, () => { detailPanel = true; Invalidate(); }, true);
            } else if (page == "Хроника") {
                var news = E.S.News; int offset = Math.Min(scroll, Math.Max(0, news.Count - 6));
                for (int i = offset; i < Math.Min(news.Count, offset + 6); i++) {
                    var item = news[i]; float y = 164 + (i - offset) * 101;
                    Txt(g, item.Date, 20, y, 10, Palette.Gold, true);
                    Para(g, item.Text, 20, y + 20, w, 62, 13, item.Type == 2 ? Palette.Red : Palette.Text);
                    Line(g, Palette.Line, 20, y + 90, 376, y + 90);
                }
            } else {
                CountrySilhouette(g, E.S.Player, new RectangleF(70, 167, 258, 131), Color.FromArgb(160, Palette.Gold));
                Book(g, W.Countries[E.S.Player].Name, 20, 320, 25, Palette.Text, true, w);
                Txt(g, "ГОСУДАРСТВЕННАЯ ВЕДОМОСТЬ", 21, 358, 10, Palette.Gold, true);
                LedgerRow(g, "Казна", Money(n.Treasury), 401);
                LedgerRow(g, "Население", Num(n.Population) + " млн", 435);
                LedgerRow(g, "Уровень жизни", Num(n.Living) + " / 100", 469);
                LedgerRow(g, "Одобрение", Num(n.Approval) + "%", 503);
                LedgerRow(g, "Безработица", Num(n.Unemployment) + "%", 537);
                LedgerRow(g, "Инфляция", Num(n.Inflation) + "%", 571);
                CompactSection(g, "ПРИОРИТЕТЫ", 622);
                Button(g, n.Research < 0 ? "Выбрать научную программу →" : "Исследования: " + TechnologyRules.CurrentName(n), 20, 669, w, () => Navigate("Исследования"), n.Research < 0);
                Button(g, "Производство и строительство →", 20, 716, w, () => Navigate("Производство"));
            }
        }
        private void CompactPolicy(Graphics g, string label, double value, float y, Action<double> set, double min, double max, double step, string tip)
        {
            Txt(g, label, 21, y, 14, Palette.Text, true, 260);
            Txt(g, Num(value) + "%", 298, y, 17, Palette.Gold, true);
            Progress(g, 21, y + 30, 237, (value - min) / (max - min), Palette.Gold);
            Button(g, "−", 274, y + 26, 44, () => { set(Rules.Clamp(value - step, min, max)); E.ComputeEconomy(false); Invalidate(); }, false, tip, 28);
            Button(g, "+", 330, y + 26, 44, () => { set(Rules.Clamp(value + step, min, max)); E.ComputeEconomy(false); Invalidate(); }, false, tip, 28);
        }
        private void FocusArmy(Army unit)
        {
            army = unit.Id; selected = unit.Region; cx = (float)W.Regions[unit.Region].X; cy = (float)W.Regions[unit.Region].Y;
            scale = Math.Max(18, scale); Invalidate();
        }
        private void DrawOutliner(Graphics g)
        {
            var box = OutlinerRect; float x = box.X, y = box.Y, w = box.Width;
            DocketFrame(g, box, "Текущие дела");
            var n = E.Player;
            Txt(g, "ИССЛЕДОВАНИЯ", x + 14, y + 57, 9, Palette.Gold, true);
            Button(g, n.Research < 0 ? "Выбрать программу →" : TechnologyRules.CurrentName(n) + "  " + n.ResearchProgress.ToString("0") + "%", x + 12, y + 76, w - 24, () => Navigate("Исследования"), false, "Технологическая программа", 28);
            Progress(g, x + 14, y + 109, w - 28, n.Research < 0 ? 0 : n.ResearchProgress / 100, Palette.Gold);
            Txt(g, "ПРОИЗВОДСТВО", x + 14, y + 132, 9, Palette.Gold, true);
            var lines = n.Lines.Where(l => l.Active).Take(2).ToList();
            if (lines.Count == 0) Txt(g, "Нет действующих серий", x + 14, y + 158, 12, Palette.Muted);
            for (int i = 0; i < lines.Count; i++) {
                var line = lines[i]; var b = E.FindDesign(E.S.Player, line.DesignId).Blueprint;
                Button(g, b.Name + "  " + line.Completed + "/" + line.Quantity, x + 12, y + 150 + i * 29, w - 24, () => { equipmentProduction = true; Navigate("Производство"); }, false, E.ProductionStatus(E.S.Player, line), 25);
            }
            bool convention = n.Politics.Pending == null && n.Government.Target >= 0 && n.Government.Phase == 1;
            Button(g, convention ? "Реформа ждёт решения →" : n.Politics.Pending == null ? "Программы кабинета →" : "Событие: " + PolicyRules.Events[(int)n.Politics.Pending.Kind].Name, x + 12, y + 208, w - 24, () => { if (convention) OpenGovernment(true); else OpenNationalPolicy(n.Politics.Pending == null ? 0 : 1); }, convention || n.Politics.Pending != null, "Национальные решения, события и государственная реформа", 28); hits[hits.Count - 1].Label = convention ? "government:outliner" : "policy:outliner";
            Txt(g, "СОЕДИНЕНИЯ", x + 14, y + 246, 9, Palette.Gold, true);
            var units = E.S.Armies.Where(a => a.Owner == E.S.Player).Take(3).ToList();
            for (int i = 0; i < units.Count; i++) {
                var a = units[i]; Button(g, "№" + a.Id + " · " + W.Regions[a.Region].Name, x + 12, y + 266 + i * 31, w - 24, () => FocusArmy(a), a.Id == army, a.Design.Name + "; готовность " + a.Strength.ToString("0") + "%, снабжение " + a.Supply.ToString("0") + "%", 27);
            }
            Button(g, "Все войска и фронты →", x + 12, y + 365, w - 24, () => Navigate("Армия"), false, "Командование", 27);
            Line(g, Palette.Line, x + 12, y + 393, x + w - 12, y + 393);
            if (selected >= 0) {
                var p = E.S.Provinces[selected];
                Txt(g, "ВЫБРАННАЯ ОБЛАСТЬ", x + 14, y + 407, 9, Palette.Gold, true);
                Para(g, W.Regions[selected].Name, x + 14, y + 429, w - 28, 45, 18, Palette.Text);
                Txt(g, W.Countries[p.Owner].Name, x + 14, y + 481, 12, Palette.Muted, false, w - 28);
                Txt(g, Num(p.Population) + " млн · дороги " + p.Infrastructure.ToString("0") + "%", x + 14, y + 508, 12, Palette.Text);
                Txt(g, "Спокойствие " + (100 - p.Unrest).ToString("0") + "%", x + 14, y + 535, 12, Palette.Teal);
                Button(g, p.Owner == E.S.Player ? "Развивать область →" : "Дипломатия →", x + 12, y + 570, w - 24, () => { foreign = p.Owner; Navigate(p.Owner == E.S.Player ? "Производство" : "Дипломатия"); }, false, "Управление выбранной территорией", 30);
            }
        }
    }
}
