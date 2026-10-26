using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool nationalPolicyPage;
        private int policyTab, policySelection, lastPolicyIncident;
        private Campaign observedPolicyCampaign;
        private void OpenNationalPolicy(int tab = 0)
        {
            Navigate("Политика"); nationalPolicyPage = true; detailPanel = true; policyTab = tab; speed = 0; Invalidate();
        }
        private void ObservePolicyIncident()
        {
            if (ObserveGovernmentConvention()) return;
            if (E.S == null || E.S.Nations == null || E.Player.Politics == null) return;
            if (!ReferenceEquals(observedPolicyCampaign, E.S)) { observedPolicyCampaign = E.S; lastPolicyIncident = 0; }
            var incident = E.Player.Politics.Pending;
            if (incident == null || incident.Id == lastPolicyIncident) return;
            lastPolicyIncident = incident.Id; speed = 0;
            toast = "Кабинет ожидает решения: " + PolicyRules.Events[(int)incident.Kind].Name; toastUntil = DateTime.Now.AddSeconds(12);
            if (menuScreen == "") { OpenNationalPolicy(1); toastUntil = DateTime.MinValue; }
        }
        private static string EffectSummary(PolicyEffects e)
        {
            var items = new List<string>();
            Action<string, double, bool> add = (name, amount, percent) => { if (Math.Abs(amount) > 1e-9) items.Add(name + " " + (percent ? amount * 100 : amount).ToString("+0.#;−0.#;0") + (percent ? "%" : "")); };
            add("Выпуск", e.Output, true); add("Сборка техники", e.Equipment, true); add("Исследования", e.Research, true); add("Сбор налогов", e.Revenue, true); add("Целевое одобрение", e.Approval, false); add("Бытовое потребление", e.Consumption, true);
            return items.Count == 0 ? "Нет временных изменений" : string.Join(" · ", items);
        }
        private void DrawNationalPolitics(Graphics g)
        {
            PageTitle(g, "Кабинет и национальные решения", "Программы, события и временные меры. Стоимость и последствия указаны до принятия решения.");
            string[] tabs = { "Программы кабинета", "События и решения", "Действующие меры" };
            for (int i = 0; i < tabs.Length; i++) {
                int tab = i; Button(g, tabs[i], 28 + i * 211, 223, 199, () => { policyTab = tab; scroll = 0; Invalidate(); }, policyTab == i, tabs[i], 35); hits[hits.Count - 1].Label = "policy:tab:" + i;
            }
            GovernmentLink(g, 661, 224, 190);
            Button(g, "Бюджет и налоги →", VW - 235, 223, 207, () => Navigate("Политика"), false, "Вернуться к бюджетным регуляторам", 35);
            if (policyTab == 0) DrawNationalDecisions(g); else if (policyTab == 1) DrawPolicyIncident(g); else DrawPolicyModifiers(g);
        }
        private void DrawNationalDecisions(Graphics g)
        {
            var n = E.Player; float x = 372, w = VW - x - 30;
            Section(g, "ПРОГРАММЫ / " + n.Politics.Decisions.Count(s => s.ActiveUntil > E.S.Day) + " ИЗ 2", 28, 284, 312);
            for (int i = 0; i < PolicyRules.Decisions.Length; i++) {
                int selectedPolicy = i; var d = PolicyRules.Decisions[i]; var state = n.Politics.Decisions[i]; float y = 326 + i * 68;
                Button(g, d.Name, 28, y, 312, () => { policySelection = selectedPolicy; Invalidate(); }, policySelection == i, d.Description, 34); hits[hits.Count - 1].Label = "policy:select:" + i;
                string status = state.ActiveUntil > E.S.Day ? "Действует · " + (state.ActiveUntil - E.S.Day) + " дн." : state.CooldownUntil > E.S.Day ? "Повтор через " + (state.CooldownUntil - E.S.Day) + " дн." : E.DecisionReason(d.Kind) == null ? "Можно принять" : "Условия не выполнены";
                Txt(g, status, 39, y + 40, 11, state.ActiveUntil > E.S.Day ? Palette.Teal : Palette.Muted, false, 292);
            }
            Para(g, "Кабинет ведёт не более двух программ. После завершения действует отдельный срок до повторного принятия.", 29, 752, 310, 71, 13);
            Line(g, Palette.Line, 352, 284, 352, VH - 63);
            var definition = PolicyRules.Decisions[policySelection];
            Book(g, definition.Name, x, 285, 28, Palette.Text, true, w);
            Para(g, definition.Description, x, 336, w, 91, 15);
            Section(g, "УСЛОВИЯ", x, 444, w);
            Para(g, definition.Conditions, x, 476, w, 52, 14, Palette.Text);
            Section(g, "ПОСЛЕДСТВИЯ", x, 548, w);
            Para(g, definition.Consequences, x, 582, w, 71, 15, Palette.Teal);
            Txt(g, "Срок " + definition.Duration + " дн. · Повтор через " + definition.Cooldown + " дн. после принятия", x, 668, 12, Palette.Muted, false, w);
            string price = definition.Kind == NationalDecision.WarBonds ? "В казну и в долг: +" + Money(PolicyRules.BondAmount(n)) : "Единовременно: " + Money(PolicyRules.DecisionCost(definition, n));
            Txt(g, price, x, 702, 19, Palette.Gold, true, w);
            string reason = E.DecisionReason(definition.Kind);
            Para(g, reason ?? "Все условия выполнены. Стоимость будет списана один раз.", x, 739, w, 39, 12, reason == null ? Palette.Muted : Palette.Red);
            AvailableButton(g, "Принять программу", x, 790, w, () => Message(E.TakeDecision(definition.Kind)), reason == null, reason ?? definition.Consequences, true, 36); hits[hits.Count - 1].Label = "policy:take";
        }
        private void DrawPolicyIncident(Graphics g)
        {
            var incident = E.Player.Politics.Pending; float w = VW - 56;
            if (incident == null) {
                Book(g, "Повестка рассмотрена", 28, 287, 27, Palette.Text, true);
                Txt(g, "Новые события зависят от положения страны. Последние решения кабинета:", 29, 333, 14, Palette.Muted, false, w);
                var history = E.Player.Politics.History;
                if (history.Count == 0) Para(g, "Здесь появятся принятые программы и ответы на события. События проверяются еженедельно; в начале кампании есть не менее 28 дней на подготовку.", 29, 397, Math.Min(780, w), 110, 17);
                int count = Math.Max(1, (int)((VH - 439) / 74)); int offset = Math.Min(scroll, Math.Max(0, history.Count - count));
                for (int i = offset; i < Math.Min(history.Count, offset + count); i++) {
                    var entry = history[i]; float y = 383 + (i - offset) * 74;
                    Fill(g, i % 2 == 0 ? Palette.Card : Palette.Panel, 28, y, w, 70);
                    Txt(g, new DateTime(E.S.StartYear, 1, 1).AddDays(entry.Day).ToString("dd.MM.yyyy"), 40, y + 10, 12, Palette.Gold);
                    Txt(g, entry.Title, 163, y + 9, 14, Palette.Text, true, w - 150);
                    Para(g, entry.Outcome, 163, y + 33, w - 150, 33, 12);
                }
                Txt(g, "Колесо: история последних 20 решений", 29, VH - 65, 11, Palette.Muted); return;
            }
            var definition = PolicyRules.Events[(int)incident.Kind];
            Book(g, definition.Name, 28, 286, 28, Palette.Text, true, w - 270);
            Txt(g, "Осталось " + (incident.DeadlineDay - E.S.Day) + " дн. на решение", VW - 273, 294, 15, Palette.Gold, true, 245);
            Para(g, definition.Description, 29, 334, w, 53, 15);
            Txt(g, "Без ответа: «" + definition.Choices[0].Name + "». Цены зафиксированы на дату события.", 29, 393, 12, Palette.Muted, false, w);
            for (int i = 0; i < 3; i++) {
                int choice = i; var option = definition.Choices[i]; float y = 429 + i * 132; string reason = E.PolicyChoiceReason(incident.Id, i);
                Card(g, 28, y, w, 122);
                AvailableButton(g, option.Name, 40, y + 12, 285, () => Message(E.ResolvePolicyIncident(incident.Id, choice)), reason == null, reason ?? option.Description, false, 35); hits[hits.Count - 1].Label = "policy:choice:" + i;
                string cost = PolicyRules.ChoiceCost(incident, option) == 0 ? "Без расходов казны" : Money(PolicyRules.ChoiceCost(incident, option));
                Txt(g, cost, 49, y + 58, 14, Palette.Gold, true, 274);
                if (option.Goods) Txt(g, "Еда " + Num(incident.Population * .10) + " · товары " + Num(incident.Population * .04), 49, y + 84, 12, Palette.Muted, false, 272);
                Para(g, option.Description, 349, y + 12, w - 334, 44, 14, Palette.Text);
                if (option.Modifier.HasValue) {
                    var effect = PolicyRules.Modifiers[(int)option.Modifier.Value];
                    Para(g, effect.Duration + " дн.: " + effect.Description, 349, y + 60, w - 334, 37, 12, Palette.Teal);
                }
                if (reason != null) Txt(g, reason, 349, y + 99, 11, Palette.Red, false, w - 334);
            }
        }
        private void DrawPolicyModifiers(Graphics g)
        {
            var n = E.Player; float w = VW - 56; var rows = new List<Tuple<string, string, int>>();
            foreach (var d in n.Politics.Decisions.Where(d => d.ActiveUntil > E.S.Day)) rows.Add(Tuple.Create(PolicyRules.Decisions[(int)d.Kind].Name, EffectSummary(PolicyRules.Decisions[(int)d.Kind].Effects), d.ActiveUntil));
            foreach (var m in n.Politics.Modifiers.Where(m => m.UntilDay > E.S.Day)) rows.Add(Tuple.Create(PolicyRules.Modifiers[(int)m.Kind].Name, PolicyRules.Modifiers[(int)m.Kind].Description, m.UntilDay));
            Book(g, "Действующие меры / " + rows.Count, 28, 288, 27, Palette.Text, true);
            Para(g, "Изменения одноимённых показателей складываются. Государственный строй, призыв, бюджет и технологии учитываются отдельно. При окончании срока эффект снимается автоматически.", 29, 333, w, 49, 14);
            int count = Math.Max(1, (int)((VH - 506) / 82)); int offset = Math.Min(scroll, Math.Max(0, rows.Count - count));
            if (rows.Count == 0) Txt(g, "Временных мер пока нет.", 29, 409, 18, Palette.Muted);
            for (int i = offset; i < Math.Min(rows.Count, offset + count); i++) {
                var row = rows[i]; float y = 397 + (i - offset) * 82;
                Card(g, 28, y, w, 76); Txt(g, row.Item1, 42, y + 9, 17, Palette.Text, true, w - 230);
                Txt(g, (row.Item3 - E.S.Day) + " дн. · до " + new DateTime(E.S.StartYear, 1, 1).AddDays(row.Item3).ToString("dd.MM.yyyy"), VW - 237, y + 12, 12, Palette.Gold, false, 195);
                Para(g, row.Item2, 43, y + 39, w - 30, 32, 13, Palette.Teal);
            }
            Para(g, "Суммарно: " + EffectSummary(PolicyRules.Effects(n, E.S.Day)), 29, VH - 105, w, 46, 12, Palette.Gold);
        }
    }
}
