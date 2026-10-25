using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private int researchTab, researchBranch, researchSelection = 6;
        private void OpenResearchProject(int project)
        {
            Navigate("Исследования"); detailPanel = true; researchSelection = project; researchBranch = TechnologyRules.Branch(project); researchTab = project < 6 ? 1 : 0; Invalidate();
        }
        private void DrawResearch(Graphics g)
        {
            var n = E.Player; var a = n.ScienceTree;
            PageTitle(g, "Наука и технологическое развитие", "Фундаментальные уровни и прикладные технологии. Смена программы сохраняет накопленный прогресс.");
            string[] tabs = { "Технологии", "Фундаментальная наука", "План / " + a.Queue.Count };
            float[] starts = { 28, 199, 452 }, widths = { 159, 241, 138 };
            for (int i = 0; i < tabs.Length; i++) {
                int tab = i; Button(g, tabs[i], starts[i], 222, widths[i], () => { researchTab = tab; researchSelection = tab == 1 ? researchBranch : researchBranch * 4 + 6; Invalidate(); }, researchTab == i, tabs[i], 35); hits[hits.Count - 1].Label = "research:tab:" + i;
            }
            Txt(g, n.Research < 0 ? "Нет текущей программы" : n.ResearchProgress.ToString("0") + "% · " + TechnologyRules.CurrentName(n), VW - 446, 226, 12, Palette.Teal, false, 288);
            Progress(g, VW - 445, 251, 281, n.ResearchProgress / 100, Palette.Teal);
            AvailableButton(g, a.Paused ? "Продолжить" : "Пауза", VW - 143, 222, 115, () => E.PauseResearch(!a.Paused), a.ActiveProject >= 0 || a.Queue.Count > 0, "Пауза останавливает прогресс и план. Научный бюджет остаётся в расходах.", a.Paused, 35); hits[hits.Count - 1].Label = "research:pause";
            if (researchTab == 2) { DrawResearchQueue(g); return; }
            float step = (VW - 56) / 6;
            for (int i = 0; i < 6; i++) {
                int branch = i; Button(g, Rules.Techs[i], 28 + i * step, 276, step - 7, () => { researchBranch = branch; researchSelection = researchTab == 1 ? branch : branch * 4 + 6; Invalidate(); }, researchBranch == i, Rules.Techs[i], 32); hits[hits.Count - 1].Label = "research:branch:" + i;
            }
            float right = VW - 331, leftWidth = right - 81;
            if (researchTab == 0) {
                var nodes = TechnologyRules.Catalog.Where(t => t.Branch == researchBranch).ToArray();
                foreach (var node in nodes) foreach (int source in node.Prerequisites.Where(id => TechnologyRules.Catalog[id].Branch == researchBranch)) {
                    float y1 = 378 + source % 4 * 120, y2 = 378 + node.Id % 4 * 120, lane = 22 + source % 4 * 6;
                    Color color = a.Known[source] ? Palette.Teal : Palette.Line;
                    Line(g, color, 46, y1, lane, y1); Line(g, color, lane, y1, lane, y2); Line(g, color, lane, y2, 46, y2);
                    Line(g, color, 40, y2 - 4, 46, y2); Line(g, color, 40, y2 + 4, 46, y2);
                }
                for (int i = 0; i < nodes.Length; i++) {
                    var d = nodes[i]; int project = d.Id + 6; float y = 327 + i * 120; bool current = a.ActiveProject == project;
                    Fill(g, researchSelection == project ? Palette.Selected : Palette.Card, 46, y, leftWidth, 102);
                    Line(g, a.Known[d.Id] ? Palette.Teal : Palette.Line, 46, y + 102, 46 + leftWidth, y + 102);
                    Txt(g, d.Name, 61, y + 11, 18, a.Known[d.Id] ? Palette.Teal : Palette.Text, true, leftWidth - 30);
                    string status = a.Known[d.Id] ? "ОСВОЕНО" : current ? E.ResearchStatus(E.S.Player) : E.ResearchReason(project) == null ? "ДОСТУПНО · фундаментальный уровень " + d.Foundation : E.ResearchReason(project);
                    Txt(g, status, 62, y + 40, 11, current ? Palette.Gold : Palette.Muted, false, leftWidth - 30);
                    Para(g, d.Description, 62, y + 61, leftWidth - 30, 33, 12);
                    if (!a.Known[d.Id]) Progress(g, 61, y + 98, leftWidth - 30, (current ? n.ResearchProgress : a.StoredProgress[project]) / 100, Palette.Gold);
                    hits.Add(new Hit { Rect = new RectangleF(46, y, leftWidth, 102), Label = "technology:" + d.Id, Action = () => { researchSelection = project; Invalidate(); }, Tip = d.Description });
                }
                Txt(g, "Освоено " + a.Known.Count(k => k) + " / 24 · Связи других отраслей — в требованиях справа", 47, 812, 11, Palette.Muted, false, leftWidth);
            } else {
                for (int i = 0; i < 6; i++) {
                    int branch = i; float y = 328 + i * 74;
                    Fill(g, researchSelection == i ? Palette.Selected : Palette.Card, 28, y, leftWidth + 18, 66);
                    Txt(g, Rules.Techs[i], 44, y + 10, 18, Palette.Text, true, leftWidth - 120);
                    Txt(g, "Уровень " + n.Tech[i] + " → " + (n.Tech[i] + 1), right - 207, y + 14, 13, Palette.Gold, true, 159);
                    Txt(g, i == a.ActiveProject ? E.ResearchStatus(E.S.Player) : "Сохранено: " + a.StoredProgress[i].ToString("0") + "%", 45, y + 39, 12, Palette.Muted, false, leftWidth - 15);
                    hits.Add(new Hit { Rect = new RectangleF(28, y, leftWidth + 18, 66), Label = "foundation:" + i, Action = () => { researchSelection = branch; researchBranch = branch; Invalidate(); }, Tip = "Фундаментальные уровни открывают прикладные проекты и повышают базовую эффективность." });
                }
                Txt(g, "Старшие модули 7–8 также требуют прикладных открытий", 29, 801, 12, Palette.Muted, false, leftWidth + 18);
            }
            Line(g, Palette.Line, right - 16, 327, right - 16, 834);
            DrawResearchDetail(g, right, researchSelection);
        }
        private void DrawResearchDetail(Graphics g, float x, int project)
        {
            var n = E.Player; var a = n.ScienceTree; float w = 303; bool basic = project < 6, current = a.ActiveProject == project;
            string[] foundations = { "Повышает базовый выпуск продовольствия и открывает аграрные технологии.", "Повышает базовую выработку электричества и открывает энергетические технологии.", "Повышает базовый выпуск промышленности и темп сборки. Открывает уровни двигателей, корпусов и ремонта.", "Ускоряет новые стройки и открывает уровни ходовой и баков.", "Поддерживает рост населения и открывает медицинские технологии.", "Открывает уровни брони, орудий и электроники. Уровень 2 открывает авиационные классы." };
            Para(g, TechnologyRules.Name(project), x, 329, w, 54, 20, Palette.Text);
            Para(g, basic ? foundations[project] : TechnologyRules.Catalog[project - 6].Description, x, 394, w, 94, 14);
            if (!basic) {
                var d = TechnologyRules.Catalog[project - 6];
                Button(g, Rules.Techs[d.Branch] + ": нужно " + d.Foundation + " / есть " + n.Tech[d.Branch], x, 505, w, () => OpenResearchProject(d.Branch), n.Tech[d.Branch] >= d.Foundation, "Перейти к фундаментальной науке", 29); hits[hits.Count - 1].Label = "research:required-foundation";
                Txt(g, "ПРЕДПОСЫЛКИ", x, 545, 10, Palette.Gold, true);
                if (d.Prerequisites.Length == 0) Txt(g, "Прикладных предпосылок нет", x, 573, 12, Palette.Muted);
                for (int i = 0; i < d.Prerequisites.Length; i++) {
                    int id = d.Prerequisites[i]; Button(g, (a.Known[id] ? "✓ " : "→ ") + TechnologyRules.Catalog[id].Name, x, 566 + i * 31, w, () => OpenResearchProject(id + 6), a.Known[id], "Перейти к необходимой технологии", 26); hits[hits.Count - 1].Label = "research:prerequisite:" + id;
                }
            } else {
                Txt(g, "НАУЧНЫЙ БЮДЖЕТ", x, 513, 10, Palette.Gold, true);
                Txt(g, Num(n.Science) + "% ВВП · образование " + Num(n.Education), x, 545, 14, Palette.Text, false, w);
                Para(g, "Одна программа за раз. Нулевой научный бюджет останавливает прогресс. Прикладные технологии изучаются отдельно и не повышают фундаментальный уровень.", x, 586, w, 95, 13);
            }
            double progress = current ? n.ResearchProgress : a.StoredProgress[project], rate = E.WeeklyResearchRate(E.S.Player, project);
            string estimate = rate <= 0 ? "Нет финансирования" : "Около " + Math.Ceiling((100 - progress) / rate) + " нед. при текущем бюджете";
            Txt(g, "Прогресс " + progress.ToString("0") + "% · " + (current && a.Paused ? "Пауза" : estimate), x, 702, 11, Palette.Muted, false, w);
            string reason = E.ResearchReason(project); string queueReason = E.QueueResearchReason(project);
            Para(g, reason ?? "Бюджет и образование определяют темп работы.", x, 724, w, 29, 11, reason == null ? Palette.Muted : Palette.Gold);
            AvailableButton(g, current ? "Уже в работе" : a.ActiveProject >= 0 ? "Переключиться · прогресс сохранится" : "Начать исследование", x, 761, w, () => Message(E.SelectResearch(project)), reason == null && !current, reason ?? "Предыдущий прогресс сохранится; оплата идёт через научный бюджет.", true, 32); hits[hits.Count - 1].Label = "research:start";
            AvailableButton(g, "Добавить в план", x, 804, w, () => Message(E.QueueResearch(project)), queueReason == null, queueReason ?? "Начать после текущей программы и предыдущих пунктов плана", false, 30); hits[hits.Count - 1].Label = "research:enqueue";
        }
        private void DrawResearchQueue(Graphics g)
        {
            var n = E.Player; var a = n.ScienceTree; float w = VW - 56;
            Book(g, "План научной работы", 28, 286, 28, Palette.Text, true);
            Para(g, "Четыре следующих программы. Предпосылки могут быть завершены текущей работой или предыдущим пунктом плана. Если порядок нарушен, первый недоступный пункт будет ждать.", 29, 335, w, 53, 15);
            for (int i = 0; i < a.Queue.Count; i++) {
                int index = i, project = a.Queue[i]; float y = 411 + i * 89;
                Card(g, 28, y, w, 79); Txt(g, (i + 1).ToString("00"), 43, y + 14, 17, Palette.Gold, true);
                Txt(g, TechnologyRules.Name(project), 91, y + 11, 18, Palette.Text, true, w - 218);
                Txt(g, E.ResearchReason(project) ?? "Предпосылки выполнены · ожидает своей очереди", 92, y + 43, 12, Palette.Muted, false, w - 218);
                Button(g, "Убрать", VW - 142, y + 21, 100, () => Message(E.RemoveResearchQueue(index)), false, "Остальные пункты сохранятся; недоступные будут ждать предпосылок", 33); hits[hits.Count - 1].Label = "research:remove:" + i;
            }
            if (a.Queue.Count == 0) Para(g, "План пуст. Выберите технологию или фундаментальную отрасль и нажмите «Добавить в план».", 29, 428, Math.Min(770, w), 90, 19);
            Txt(g, E.ResearchStatus(E.S.Player), 29, 790, 15, Palette.Teal, false, w - 265);
            Button(g, "Научный бюджет →", VW - 269, 784, 241, () => Navigate("Политика"), false, "Настроить науку и образование", 36);
        }
    }
}
