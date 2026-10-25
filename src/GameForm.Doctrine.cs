using System;
using System.Drawing;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool doctrinePage;
        private int doctrineService, doctrineSelection;
        private void OpenDoctrines(int service = 0)
        {
            Navigate("Исследования"); doctrinePage = detailPanel = true; doctrineService = service;
            int path = E.Player.Doctrines.Active[service]; if (path < 0) path = service * 3;
            doctrineSelection = path * 3 + Math.Min(2, E.Player.Doctrines.Levels[path]); Invalidate();
        }
        private void DoctrineLink(Graphics g, float x, float y, float width)
        {
            Button(g, "Военные доктрины →", x, y, width, () => OpenDoctrines(), false, "Шесть школ, штабная подготовка и переучивание войск", 27);
            hits[hits.Count - 1].Label = "doctrine:open";
        }
        private void DrawDoctrines(Graphics g)
        {
            var n = E.Player; var state = n.Doctrines;
            PageTitle(g, "Военные доктрины", "Штабная подготовка идёт отдельно от науки. На каждый род войск действует одна школа.");
            Button(g, "Технологии →", VW - 202, 178, 174, () => OpenResearchProject(researchSelection), false, "Вернуться к научной программе", 26);
            hits[hits.Count - 1].Label = "doctrine:research";
            for (int service = 0; service < 2; service++) {
                int s = service;
                Button(g, service == 0 ? "Сухопутные войска" : "Воздушные силы", 28 + service * 205, 222, 193, () => { doctrineService = s; doctrineSelection = s * 9; Invalidate(); }, doctrineService == service, "Три альтернативные школы", 35);
                hits[hits.Count - 1].Label = "doctrine:service:" + service;
            }
            float right = VW - 347, width = right - 56, column = width / 3;
            Txt(g, E.DoctrineStatus(E.S.Player, doctrineService), 29, 271, 12, Palette.Gold, false, width);
            for (int i = 0; i < 3; i++) {
                int path = doctrineService * 3 + i; float x = 28 + i * column, cw = column - 12;
                Para(g, DoctrineRules.Names[path], x, 305, cw, 45, 18, Palette.Text);
                Para(g, DoctrineRules.Descriptions[path], x, 354, cw, 59, 12);
                for (int tier = 0; tier < 3; tier++) {
                    int id = path * 3 + tier; var stage = DoctrineRules.Stages[id]; float y = 421 + tier * 99;
                    bool learned = state.Levels[path] > tier, current = state.Training == id;
                    Color accent = learned ? Palette.Teal : current ? Palette.Gold : Palette.Line;
                    if (tier > 0) Line(g, accent, x + cw / 2, y - 11, x + cw / 2, y);
                    Fill(g, doctrineSelection == id ? Palette.Selected : Palette.Card, x, y, cw, 88);
                    Fill(g, accent, x, y, 3, 88);
                    Txt(g, (tier == 0 ? "I" : tier == 1 ? "II" : "III") + "   /   " + (learned ? "ОСВОЕНО" : current ? "ПОДГОТОВКА" : "ВОЕННЫЕ ТЕХ. " + stage.Foundation), x + 13, y + 10, 9, accent == Palette.Line ? Palette.Muted : accent, true, cw - 24);
                    Para(g, stage.Name, x + 13, y + 30, cw - 24, 48, 15, Palette.Text);
                    hits.Add(new Hit { Rect = new RectangleF(x, y, cw, 88), Label = "doctrine:stage:" + id, Action = () => { doctrineSelection = id; Invalidate(); }, Tip = DoctrineRules.Names[path] + ": " + stage.Name + ". " + DoctrineRules.Summary(stage.Effects) });
                }
            }
            Line(g, Palette.Line, right - 15, 275, right - 15, 842);
            DrawDoctrineDetail(g, right);
            Section(g, "РАБОТА ШТАБА", 28, 733, width - 12);
            string course = state.Training < 0 ? "Курс не назначен. Выберите ступень в одной из школ." : DoctrineRules.Stages[state.Training].Name + " · " + (state.Paused ? "пауза" : E.DoctrineTrainingRate(E.S.Player) <= 0 ? "нет условий для подготовки" : Math.Ceiling(state.Work) + " / " + DoctrineRules.Stages[state.Training].Duration);
            Txt(g, course, 29, 767, 12, Palette.Text, false, width - 12);
            Progress(g, 29, 793, width - 12, state.Training < 0 ? 0 : state.Work / DoctrineRules.Stages[state.Training].Duration, Palette.Gold);
            AvailableButton(g, state.Paused ? "Продолжить" : "Пауза", 28, 810, 128, () => Message(E.PauseDoctrineTraining(!state.Paused)), state.Training >= 0, "При паузе прогресс сохраняется; военный бюджет остаётся в расходах.", state.Paused, 30); hits[hits.Count - 1].Label = "doctrine:pause";
            AvailableButton(g, "Отменить курс", 166, 810, 151, () => { if (Confirm("Отменить штабной курс? Оплата и незавершённая подготовка не возвращаются.")) Message(E.CancelDoctrineTraining()); }, state.Training >= 0, "Без возврата денег и прогресса текущего курса", false, 30); hits[hits.Count - 1].Label = "doctrine:cancel";
            Button(g, "Военный бюджет →", 327, 810, Math.Min(185, width - 339), () => Navigate("Политика"), false, "Для подготовки нужны бюджет от 2% ВВП и образование от 20", 30);
        }
        private void DrawDoctrineDetail(Graphics g, float x)
        {
            var n = E.Player; var state = n.Doctrines; var stage = DoctrineRules.Stages[doctrineSelection]; float w = 319;
            Para(g, stage.Name, x, 281, w, 52, 21, Palette.Text);
            Txt(g, "СУММАРНО НА СТУПЕНИ " + (stage.Tier + 1), x, 344, 10, Palette.Gold, true);
            Para(g, DoctrineRules.Summary(DoctrineRules.Cumulative(stage.Path, stage.Tier + 1)), x, 369, w, 117, 13, Palette.Text);
            Txt(g, "Эффекты — только у выбранной школы.", x, 490, 11, Palette.Muted, false, w);
            Button(g, "Военные технологии: " + n.Tech[5] + " / нужно " + stage.Foundation, x, 520, w, () => OpenResearchProject(5), n.Tech[5] >= stage.Foundation, "Перейти к фундаментальной военной науке", 27); hits[hits.Count - 1].Label = "doctrine:foundation";
            for (int i = 0; i < stage.Technologies.Length; i++) {
                int id = stage.Technologies[i];
                Button(g, (TechnologyRules.Has(n, id) ? "✓ " : "→ ") + TechnologyRules.Catalog[id].Name, x, 554 + i * 32, w, () => OpenResearchProject(id + 6), TechnologyRules.Has(n, id), "Открыть необходимую технологию", 27); hits[hits.Count - 1].Label = "doctrine:required:" + id;
            }
            bool current = state.Training == stage.Id; double rate = E.DoctrineTrainingRate(E.S.Player);
            Txt(g, "Подготовка: " + Money(DoctrineRules.TrainingCost(n.GDP, stage.Tier)) + " · " + (rate <= 0 ? "приостановлена" : "≈ " + Math.Ceiling((stage.Duration - (current ? state.Work : 0)) / rate) + " дн."), x, 625, 12, Palette.Text, false, w);
            string reason = E.DoctrineTrainingReason(stage.Id);
            Para(g, reason ?? "Разовая оплата. Темп зависит от военного бюджета и образования.", x, 651, w, 43, 12, reason == null ? Palette.Muted : Palette.Gold);
            AvailableButton(g, current ? "Подготовка идёт" : "Начать подготовку", x, 700, w, () => Message(E.StartDoctrineTraining(stage.Id)), reason == null, reason ?? "Оплатить и начать штабной курс", true, 32); hits[hits.Count - 1].Label = "doctrine:start";
            string adoption = E.DoctrineAdoptionReason(stage.Path);
            Para(g, "Применить освоенные ступени: " + state.Levels[stage.Path] + "/3 · " + Money(DoctrineRules.AdoptionCost(n, stage.Path)) + ". Переучивание 30 дней без эффектов. Смена раз в 180 дней.", x, 746, w, 57, 12);
            AvailableButton(g, state.Active[doctrineService] == stage.Path ? "Школа выбрана" : "Применить школу", x, 810, w, () => { if (Confirm("Применить школу «" + DoctrineRules.Names[stage.Path] + "» за " + Money(DoctrineRules.AdoptionCost(n, stage.Path)) + "? 30 дней переучивания без эффектов этого рода войск; смена через 180 дней.")) Message(E.AdoptDoctrine(stage.Path)); }, adoption == null, adoption ?? "Оплатить переучивание и применить только освоенные ступени", false, 30); hits[hits.Count - 1].Label = "doctrine:adopt";
        }
    }
}
