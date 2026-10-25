using System;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        public void InitializeNationalPolitics(Campaign campaign)
        {
            for (int c = 0; c < campaign.Nations.Length; c++) campaign.Nations[c].Politics = new NationalPolitics {
                NextEventDay = campaign.Day + 28 + c % 5 * 7,
                Decisions = PolicyRules.Decisions.Select(d => new DecisionState { Kind = d.Kind }).ToList()
            };
            campaign.Version = 6;
        }
        public bool NationAtWar(int owner) => Enumerable.Range(0, S.Nations.Length).Any(c => AtWar(owner, c));
        private bool HasPolicyTerritory(int owner) => S.Provinces.Any(p => p.Owner == owner);
        public string DecisionReason(NationalDecision kind, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (owner >= S.Nations.Length || !Enum.IsDefined(typeof(NationalDecision), kind)) return "Неизвестное решение или государство.";
            var n = S.Nations[owner]; var d = PolicyRules.Decisions[(int)kind]; var state = n.Politics.Decisions[(int)kind];
            if (!HasPolicyTerritory(owner)) return "Нет контролируемых областей.";
            if (state.ActiveUntil > S.Day) return "Программа действует ещё " + (state.ActiveUntil - S.Day) + " дн.";
            if (state.CooldownUntil > S.Day) return "Повторное принятие через " + (state.CooldownUntil - S.Day) + " дн.";
            if (n.Politics.Decisions.Count(s => s.ActiveUntil > S.Day) >= 2) return "Кабинет может вести не более двух программ одновременно.";
            if (kind == NationalDecision.Relief && n.Approval >= 60 && n.Shortage < .10) return "Помощь доступна при одобрении ниже 60% или дефиците от 10%.";
            if (kind == NationalDecision.Industry && n.Stability < 45) return "Для промышленного заказа нужна стабильность от 45%.";
            if (kind == NationalDecision.Grants && (n.Research < 0 || n.Science < 1 || n.Education < 40)) return "Нужны исследование, научный бюджет от 1% и образование от 40.";
            if (kind == NationalDecision.Audit && n.Stability < 35) return "Для аудита нужна стабильность от 35%.";
            if (kind == NationalDecision.WarBonds) {
                if (!NationAtWar(owner)) return "Военные облигации выпускаются только во время войны.";
                if (n.Stability < 40) return "Для займа нужна стабильность от 40%.";
                if (n.Debt + PolicyRules.BondAmount(n) > n.GDP * 1.5) return "Долг после займа превысит 150% ВВП.";
            }
            if (kind == NationalDecision.Dialogue && (NationAtWar(owner) || n.Approval >= 65 || n.Repression > 40)) return "Нужны мир, одобрение ниже 65% и давление не выше 40.";
            double cost = PolicyRules.DecisionCost(d, n);
            if (cost > 0 && n.Treasury < cost) return "Недостаточно денег: нужно " + cost.ToString("N2") + " млрд.";
            return null;
        }
        public string TakeDecision(NationalDecision kind, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            string reason = DecisionReason(kind, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; var d = PolicyRules.Decisions[(int)kind]; var state = n.Politics.Decisions[(int)kind];
            double cost = PolicyRules.DecisionCost(d, n); n.Treasury -= cost;
            if (kind == NationalDecision.WarBonds) { double loan = PolicyRules.BondAmount(n); n.Treasury += loan; n.Debt += loan; }
            n.Approval = Rules.Clamp(n.Approval + d.Approval, 0, 100); n.Stability = Rules.Clamp(n.Stability + d.Stability, 0, 100);
            state.StartedDay = S.Day; state.ActiveUntil = S.Day + d.Duration; state.CooldownUntil = S.Day + d.Cooldown;
            RecordPolicy(owner, d.Name, "Программа принята на " + d.Duration + " дней. " + (kind == NationalDecision.WarBonds ? "Получен заём " + PolicyRules.BondAmount(n).ToString("N2") : "Оплачено " + cost.ToString("N2")) + " млрд.");
            if (owner == S.Player) ComputeEconomy(false); Raise(); return null;
        }
        private void RecordPolicy(int owner, string title, string outcome)
        {
            var history = S.Nations[owner].Politics.History;
            history.Insert(0, new PolicyRecord { Day = S.Day, Title = title, Outcome = outcome });
            if (history.Count > 20) history.RemoveAt(20);
            if (owner == S.Player) Notify(title + ": " + outcome);
        }
        internal bool PolicyTrigger(PolicyEventKind kind, int owner)
        {
            var n = S.Nations[owner];
            switch (kind) {
                case PolicyEventKind.Shortages: return n.Shortage >= .15;
                case PolicyEventKind.LaborDispute: return n.Approval < 45 && n.Stability < 55;
                case PolicyEventKind.WarWeariness: return Enumerable.Range(0, S.Nations.Length).Any(c => AtWar(owner, c) && S.Day - S.WarStarted[Pair(owner, c)] >= 60);
                case PolicyEventKind.Procurement: return n.Repression >= 30 || n.Military >= 5;
                case PolicyEventKind.ResearchOffer: return n.Research >= 0 && n.Education >= 50;
                case PolicyEventKind.Investment: return !NationAtWar(owner) && n.Stability >= 60;
                default: return false;
            }
        }
        internal bool OpenPolicyIncident(int owner, PolicyEventKind kind, bool[] territories = null)
        {
            var n = S.Nations[owner]; var p = n.Politics;
            if (!Enum.IsDefined(typeof(PolicyEventKind), kind) || p.Pending != null || S.Day < p.NextEventDay || S.Day < p.EventCooldowns[(int)kind] || !PolicyTrigger(kind, owner) || !(territories == null ? HasPolicyTerritory(owner) : territories[owner])) return false;
            p.Pending = new PolicyIncident { Id = p.NextIncident++, Kind = kind, CreatedDay = S.Day, DeadlineDay = S.Day + 21, Economy = Math.Max(.1, n.GDP), Population = Math.Max(.001, n.Population) };
            if (owner == S.Player) Notify("Кабинет ожидает решения: " + PolicyRules.Events[(int)kind].Name + ". Срок — 21 день.", 2);
            return true;
        }
        public string PolicyChoiceReason(int incidentId, int choice, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (owner >= S.Nations.Length) return "Неизвестное государство.";
            var n = S.Nations[owner]; var incident = n.Politics.Pending;
            if (incident == null || incident.Id != incidentId) return "Событие уже разрешено или изменилось.";
            if (S.Day >= incident.DeadlineDay) return "Срок решения истёк.";
            if (choice < 0 || choice >= 3) return "Неизвестный вариант решения.";
            return ChoiceResourcesReason(owner, incident, choice);
        }
        private string ChoiceResourcesReason(int owner, PolicyIncident incident, int choice)
        {
            var n = S.Nations[owner]; var option = PolicyRules.Events[(int)incident.Kind].Choices[choice];
            double cost = PolicyRules.ChoiceCost(incident, option);
            if (cost > 0 && n.Treasury < cost) return "Недостаточно денег: нужно " + cost.ToString("N2") + " млрд.";
            if (option.Goods && (n.Stock[0] < incident.Population * .10 || n.Stock[6] < incident.Population * .04)) return "Не хватает резервов продовольствия или потребительских товаров.";
            if (option.Research && n.Research < 0) return "Сначала запустите исследование.";
            if (option.Partner && !Enumerable.Range(0, S.Nations.Length).Any(c => c != owner && S.Agreements[Pair(owner, c)] && !AtWar(owner, c))) return "Нужен торговый договор с мирным партнёром.";
            if (option.Pressure && n.Repression < 20) return "Нужно политическое давление от 20.";
            return null;
        }
        public string ResolvePolicyIncident(int incidentId, int choice, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            string reason = PolicyChoiceReason(incidentId, choice, owner); if (reason != null) return reason;
            ApplyPolicyChoice(owner, choice, false); Raise(); return null;
        }
        private void ApplyPolicyChoice(int owner, int choice, bool expired)
        {
            var n = S.Nations[owner]; var p = n.Politics; var incident = p.Pending;
            var definition = PolicyRules.Events[(int)incident.Kind]; var option = definition.Choices[choice];
            n.Treasury -= PolicyRules.ChoiceCost(incident, option);
            if (option.Goods) { n.Stock[0] -= incident.Population * .10; n.Stock[6] -= incident.Population * .04; }
            n.Approval = Rules.Clamp(n.Approval + option.Approval, 0, 100); n.Stability = Rules.Clamp(n.Stability + option.Stability, 0, 100);
            n.Living = Rules.Clamp(n.Living + option.Living, 0, 100); n.Repression = Rules.Clamp(n.Repression + option.Repression, 0, 100);
            if (option.Modifier.HasValue) {
                var kind = option.Modifier.Value; var m = p.Modifiers.FirstOrDefault(x => x.Kind == kind);
                if (m == null) { m = new NationalModifier { Kind = kind }; p.Modifiers.Add(m); }
                m.StartedDay = S.Day; m.UntilDay = S.Day + PolicyRules.Modifiers[(int)kind].Duration;
            }
            p.EventCooldowns[(int)incident.Kind] = S.Day + 180; p.NextEventDay = S.Day + 60; p.Pending = null;
            RecordPolicy(owner, definition.Name, (expired ? "Срок истёк. " : "") + option.Name + ". " + option.Description);
            if (owner == S.Player) ComputeEconomy(false);
        }
        public void TickNationalPolitics()
        {
            bool[] territories = null;
            if (S.Day % 7 == 0) { territories = new bool[S.Nations.Length]; foreach (var region in S.Provinces) territories[region.Owner] = true; }
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                var p = S.Nations[owner].Politics;
                p.Modifiers.RemoveAll(m => m.UntilDay <= S.Day);
                foreach (var state in p.Decisions) if (state.ActiveUntil == S.Day) RecordPolicy(owner, PolicyRules.Decisions[(int)state.Kind].Name, "Срок программы завершён.");
                if (p.Pending != null && S.Day >= p.Pending.DeadlineDay) ApplyPolicyChoice(owner, 0, true);
                if (S.Day % 7 == 0 && p.Pending == null && S.Day >= p.NextEventDay) {
                    if (territories[owner]) for (int kind = 0; kind < PolicyRules.Events.Length; kind++) if (OpenPolicyIncident(owner, (PolicyEventKind)kind, territories)) break;
                }
                if (owner != S.Player && p.Pending != null) {
                    int choice = new[] { 1, 2, 0 }.First(c => PolicyChoiceReason(p.Pending.Id, c, owner) == null);
                    ApplyPolicyChoice(owner, choice, false);
                }
            }
        }
        internal void PlanNationalPolicy(int owner)
        {
            var n = S.Nations[owner];
            NationalDecision[] preferences = n.Shortage >= .15 || n.Approval < 45
                ? new[] { NationalDecision.Relief, NationalDecision.Dialogue, NationalDecision.Audit }
                : new[] { NationalDecision.Grants, NationalDecision.Industry, NationalDecision.Audit };
            if (NationAtWar(owner) && n.Treasury < n.GDP * .01 && DecisionReason(NationalDecision.WarBonds, owner) == null) { TakeDecision(NationalDecision.WarBonds, owner); return; }
            foreach (var kind in preferences) {
                double cost = PolicyRules.DecisionCost(PolicyRules.Decisions[(int)kind], n);
                if (n.Treasury >= cost * 2 && DecisionReason(kind, owner) == null) { TakeDecision(kind, owner); return; }
            }
        }
        private void ValidateNationalPolitics(Nation n, Campaign campaign)
        {
            var p = n.Politics; int day = campaign.Day;
            if (p == null || p.Decisions == null || p.Decisions.Count != PolicyRules.Decisions.Length || p.Decisions.Where((s, i) => s == null || (int)s.Kind != i).Any()
                || p.Modifiers == null || p.Modifiers.Count > PolicyRules.Modifiers.Length || p.Modifiers.Any(m => m == null || !Enum.IsDefined(typeof(PolicyModifierKind), m.Kind))
                || p.Modifiers.Select(m => m.Kind).Distinct().Count() != p.Modifiers.Count || p.NextIncident < 1 || p.NextIncident > day + 2
                || p.NextEventDay < 0 || p.NextEventDay > day + 60 || p.EventCooldowns == null || p.EventCooldowns.Length != 6 || p.EventCooldowns.Any(d => d < 0 || d > day + 180)
                || p.History == null || p.History.Count > 20 || p.History.Any(h => h == null || h.Day < 0 || h.Day > day || !PolicyText(h.Title, 100) || !PolicyText(h.Outcome, 600))) throw new InvalidDataException("Повреждена национальная политика.");
            foreach (var state in p.Decisions) {
                var d = PolicyRules.Decisions[(int)state.Kind];
                if (state.StartedDay < -1 || state.StartedDay > day || (state.StartedDay == -1 ? state.ActiveUntil != 0 || state.CooldownUntil != 0 : state.ActiveUntil != state.StartedDay + d.Duration || state.CooldownUntil != state.StartedDay + d.Cooldown)) throw new InvalidDataException("Повреждены сроки решений.");
            }
            if (p.Decisions.Count(d => d.ActiveUntil > day) > 2) throw new InvalidDataException("Слишком много программ кабинета.");
            foreach (var m in p.Modifiers) if (m.StartedDay < 0 || m.StartedDay > day || m.UntilDay != m.StartedDay + PolicyRules.Modifiers[(int)m.Kind].Duration) throw new InvalidDataException("Повреждены сроки государственных мер.");
            var e = p.Pending;
            if (e != null && (!Enum.IsDefined(typeof(PolicyEventKind), e.Kind) || e.Id < 1 || e.Id != p.NextIncident - 1 || e.CreatedDay < 0 || e.CreatedDay > day || e.DeadlineDay != e.CreatedDay + 21 || day >= e.DeadlineDay
                || !IsFinite(e.Economy) || e.Economy < .1 || !IsFinite(e.Population) || e.Population < .001)) throw new InvalidDataException("Повреждено ожидающее политическое событие.");
        }
        private static bool PolicyText(string text, int max) => !string.IsNullOrWhiteSpace(text) && text.Length <= max && !text.Any(char.IsControl);
    }
}
