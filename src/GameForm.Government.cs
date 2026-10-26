using System;
using System.Drawing;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool governmentPage, governmentAgenda;
        private int governmentSelection = 1, observedConventionDay = -1;
        private Campaign observedGovernmentCampaign;
        private void OpenGovernment(bool agenda = false)
        {
            Navigate("Политика"); governmentPage = detailPanel = true; governmentAgenda = agenda; speed = 0;
            var s = E.Player.Government; governmentSelection = s.Target >= 0 ? s.Target : s.Form > 0 ? s.Form : 1; Invalidate();
        }
        private bool ObserveGovernmentConvention()
        {
            if (E.S == null || E.Player.Government == null) return false;
            if (!ReferenceEquals(observedGovernmentCampaign, E.S)) { observedGovernmentCampaign = E.S; observedConventionDay = -1; }
            var s = E.Player.Government;
            if (s.Phase != 1 || s.Target < 0 || observedConventionDay == s.ConsultationDay) return false;
            observedConventionDay = s.ConsultationDay; speed = 0;
            if (menuScreen == "") { OpenGovernment(true); toastUntil = DateTime.MinValue; }
            return true;
        }
        private void GovernmentLink(Graphics g, float x, float y, float width)
        {
            bool pending = E.Player.Government.Target >= 0 && E.Player.Government.Phase == 1;
            Button(g, pending ? "Реформа: решение →" : width < 210 ? "Государство →" : "Государственный строй →", x, y, width, () => OpenGovernment(pending), pending, "Формы правления, подготовка реформы и принятие нового порядка", 32);
            hits[hits.Count - 1].Label = "government:open";
        }
        private static string GovernmentSummary(int form)
        {
            var d = GovernmentRules.Forms[form]; string text = EffectSummary(d.Effects);
            return text + (d.StabilityTarget == 0 ? "" : " · Целевая стабильность +" + d.StabilityTarget);
        }
        private void DrawGovernment(Graphics g)
        {
            PageTitle(g, "Государственный строй", "Игровые варианты институтов. Исторические формы правления стран не назначены.");
            Button(g, "Формы правления", 28, 222, 200, () => { governmentAgenda = false; Invalidate(); }, !governmentAgenda, "Сравнить устройство государства", 35); hits[hits.Count - 1].Label = "government:forms";
            Button(g, "Повестка реформы", 240, 222, 218, () => { governmentAgenda = true; Invalidate(); }, governmentAgenda, "Подготовка, решение и внедрение", 35); hits[hits.Count - 1].Label = "government:agenda";
            Button(g, "Кабинет →", VW - 393, 222, 169, () => OpenNationalPolicy(), false, "Программы и события", 35);
            Button(g, "Бюджет →", VW - 212, 222, 184, () => Navigate("Политика"), false, "Налоги, расходы и политическое давление", 35);
            if (governmentAgenda) { DrawGovernmentAgenda(g); return; }
            var n = E.Player; var s = n.Government; float x = 374, w = VW - x - 28;
            Txt(g, "ДЕЙСТВУЕТ", 29, 286, 10, Palette.Gold, true);
            Para(g, GovernmentRules.Forms[s.Form].Name, 29, 310, 308, 45, 18, Palette.Text);
            for (int i = 1; i < GovernmentRules.Forms.Length; i++) {
                int form = i; var d = GovernmentRules.Forms[i]; float y = 374 + (i - 1) * 104;
                Fill(g, governmentSelection == i ? Palette.Selected : Palette.Card, 28, y, 312, 92);
                Fill(g, s.Form == i ? Palette.Teal : Palette.Line, 28, y, 3, 92);
                Para(g, d.Name, 43, y + 12, 282, 45, 17, Palette.Text);
                Txt(g, s.Form == i ? "ДЕЙСТВУЕТ" : s.Target == i ? "РЕФОРМА В РАБОТЕ" : "СРАВНИТЬ УСЛОВИЯ", 43, y + 66, 10, s.Form == i ? Palette.Teal : Palette.Gold, true);
                hits.Add(new Hit { Rect = new RectangleF(28, y, 312, 92), Label = "government:form:" + i, Tip = GovernmentSummary(i), Action = () => { governmentSelection = form; Invalidate(); } });
            }
            Para(g, "До конца реформы действует прежняя система. Бонусы форм не складываются.", 29, 802, 309, 40, 12);
            Line(g, Palette.Line, 353, 281, 353, 842);
            var selectedForm = GovernmentRules.Forms[governmentSelection];
            Para(g, selectedForm.Name, x, 282, w, 50, 26, Palette.Text);
            Para(g, selectedForm.Description, x, 345, w, 65, 15);
            Section(g, "ПОСЛЕ ЗАВЕРШЕНИЯ", x, 429, w);
            Para(g, GovernmentSummary(governmentSelection), x, 462, w, 61, 16, Palette.Teal);
            Section(g, "ПЕРЕХОД К НОВОМУ УСТРОЙСТВУ", x, 544, w);
            Para(g, "Мир, стабильность и одобрение от 50%; давление не выше " + selectedForm.PressureLimit + ". Подготовка 45 дней, затем выбор порядка принятия и внедрение 30 или 60 дней.", x, 577, w, 67, 14, Palette.Text);
            Para(g, "При старте: одобрение −2, стабильность −3. Во время реформы: выпуск −3%, исследования −5%, сбор налогов −4%. Новый проект — через 365 дней после старта, даже при отзыве.", x, 650, w, 66, 13, Palette.Gold);
            Txt(g, "Подготовка: " + Money(GovernmentRules.ReformCost(n.GDP)), x, 732, 19, Palette.Text, true, w);
            string reason = E.GovernmentReformReason(governmentSelection);
            Para(g, reason ?? "Стоимость списывается один раз. Доплата за согласование — по выбору на следующем этапе.", x, 769, w, 35, 12, reason == null ? Palette.Muted : Palette.Gold);
            AvailableButton(g, "Начать реформу", x, 812, w, () => { if (Confirm("Начать проект «" + selectedForm.Name + "» за " + Money(GovernmentRules.ReformCost(n.GDP)) + "? Одобрение −2, стабильность −3; временные издержки до завершения. Оплата не возвращается при отзыве; новый проект через 365 дней.")) { Message(E.StartGovernmentReform(governmentSelection)); governmentAgenda = true; } }, reason == null, reason ?? "Оплатить подготовку проекта", true, 30); hits[hits.Count - 1].Label = "government:start";
        }
        private void DrawGovernmentAgenda(Graphics g)
        {
            var n = E.Player; var s = n.Government; float w = VW - 56;
            if (s.Target < 0) {
                Book(g, "Нет действующего проекта", 28, 290, 28, Palette.Text, true, w);
                Para(g, "Сейчас: " + GovernmentRules.Forms[s.Form].Name + ". Выберите форму правления, чтобы сравнить последствия и условия начала реформы.", 29, 345, w, 75, 17);
                int cooldown = Math.Max(0, s.LastStartedDay + GovernmentRules.Cooldown - E.S.Day);
                Txt(g, cooldown > 0 ? "Новый проект можно начать через " + cooldown + " дн." : "Срок ожидания нового проекта завершён.", 29, 443, 15, Palette.Gold, false, w);
                Button(g, "Сравнить формы →", 28, 500, 290, () => { governmentAgenda = false; Invalidate(); }, true); hits[hits.Count - 1].Label = "government:compare";
                return;
            }
            Para(g, GovernmentRules.Forms[s.Form].Name + " → " + GovernmentRules.Forms[s.Target].Name, 28, 286, w, 58, 24, Palette.Text);
            string[] phases = { "I   Подготовка · 45 дней", "II   Рассмотрение · ответ за 21 день", "III   Внедрение · 30 / 60 дней" };
            float step = w / 3;
            for (int i = 0; i < phases.Length; i++) {
                Fill(g, s.Phase == i ? Palette.Selected : Palette.Card, 28 + i * step, 359, step - 10, 43);
                Txt(g, phases[i], 39 + i * step, 372, 12, s.Phase == i ? Palette.Gold : Palette.Muted, true, step - 31);
            }
            if (s.Phase == 1) {
                Txt(g, "До отзыва проекта: " + (s.ConsultationDay + GovernmentRules.ResponseDays - E.S.Day) + " дн. · пауза кампании останавливает срок", 29, 421, 14, Palette.Gold, true, w);
                string[] names = { "Расширенное согласование", "Ускоренное утверждение", "Отозвать проект" };
                string[] outcomes = { "Внедрение 60 дней. При завершении: одобрение +4, стабильность +3.", "Внедрение 30 дней. При завершении: одобрение −4, стабильность −3.", "Прежняя система сохраняется. Временные издержки снимаются, оплата и подготовка не возвращаются. Годовой срок сохраняется." };
                for (int i = 0; i < 3; i++) {
                    int choice = i, started = s.StartedDay; float y = 466 + i * 120; string reason = E.GovernmentChoiceReason(started, choice);
                    Card(g, 28, y, w, 108);
                    AvailableButton(g, names[i], 40, y + 15, 298, () => { if (Confirm(names[choice] + "? " + outcomes[choice] + (choice == 0 ? " Доплата " + Money(GovernmentRules.ConsultationCost(s.QuotedGDP)) + "." : ""))) Message(E.ResolveGovernmentConvention(started, choice)); }, reason == null, reason ?? outcomes[i], i == 0, 33); hits[hits.Count - 1].Label = "government:choice:" + i;
                    Txt(g, i == 0 ? "Доплата: " + Money(GovernmentRules.ConsultationCost(s.QuotedGDP)) : "Без дополнительной оплаты", 49, y + 63, 13, Palette.Gold, false, 280);
                    Para(g, outcomes[i], 363, y + 15, w - 351, 55, 14, Palette.Text);
                    if (reason != null) Para(g, reason, 363, y + 74, w - 351, 29, 11, Palette.Red);
                }
                return;
            }
            int duration = s.Phase == 0 ? GovernmentRules.DraftDays : GovernmentRules.ImplementationDays(s.Choice);
            Txt(g, "Выполнено " + s.Work + " из " + duration + " дней работы", 29, 430, 20, Palette.Text, true, w);
            Progress(g, 29, 474, w - 2, (double)s.Work / duration, Palette.Gold);
            string block = E.GovernmentProgressReason(E.S.Player);
            Para(g, s.Paused ? "Реформа на ручной паузе." : block ?? "Работа идёт по одному дню за календарный день.", 29, 500, w, 47, 16, block == null ? Palette.Teal : Palette.Gold);
            Para(g, "При войне, потере территории, стабильности ниже 35%, одобрении ниже 30% или превышении предела давления прогресс останавливается. Оплата сохраняется, временные издержки продолжают действовать.", 29, 565, Math.Min(850, w), 83, 15);
            Txt(g, "Оплачено: " + Money(s.Paid + s.ChoicePaid) + " · временно: выпуск −3%, исследования −5%, сбор налогов −4%", 29, 677, 13, Palette.Gold, false, w);
            Para(g, s.Phase == 0 ? "После подготовки появится решение со сроком ответа 21 день. Без ответа проект отзывается без возврата оплаты." : s.Choice == 0 ? "При завершении: новый строй, одобрение +4, стабильность +3." : "При завершении: новый строй, одобрение −4, стабильность −3.", 29, 721, w, 51, 14);
            Button(g, s.Paused ? "Продолжить" : "Приостановить", 28, 807, 200, () => Message(E.PauseGovernmentReform(!s.Paused)), s.Paused, "Пауза сохраняет работу, но не снимает временные издержки", 33); hits[hits.Count - 1].Label = "government:pause";
            Button(g, "Отозвать проект", 240, 807, 219, () => { if (Confirm("Отозвать проект? Оплата и прогресс не возвращаются; годовой срок до нового проекта сохраняется.")) Message(E.CancelGovernmentReform()); }, false, "Прежний строй сохраняется", 33); hits[hits.Count - 1].Label = "government:cancel";
        }
    }
}
