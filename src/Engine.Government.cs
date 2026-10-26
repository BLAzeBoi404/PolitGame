using System;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        private void InitializeGovernments(Campaign campaign)
        {
            foreach (var n in campaign.Nations) n.Government = new GovernmentState(); campaign.Version = 10;
        }
        public string GovernmentReformReason(int target, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (owner >= S.Nations.Length || !GovernmentRules.ValidTarget(target)) return "Неизвестный проект государственного устройства.";
            var n = S.Nations[owner]; var state = n.Government;
            if (state.Target >= 0) return "Сначала завершите или отзовите текущую реформу.";
            if (state.Form == target) return "Эта форма правления уже действует.";
            if (S.Day < state.LastStartedDay + GovernmentRules.Cooldown) return "Новый проект через " + (state.LastStartedDay + GovernmentRules.Cooldown - S.Day) + " дн.";
            if (!HasPolicyTerritory(owner)) return "Нет контролируемой территории.";
            if (NationAtWar(owner)) return "Начало реформы требует мира.";
            if (n.Stability < 50 || n.Approval < 50) return "Нужны стабильность и одобрение от 50%.";
            if (n.Repression > GovernmentRules.Forms[target].PressureLimit) return "Снизьте политическое давление до " + GovernmentRules.Forms[target].PressureLimit + " или ниже.";
            return n.Treasury < GovernmentRules.ReformCost(n.GDP) ? "Недостаточно денег на подготовку реформы." : null;
        }
        public string StartGovernmentReform(int target, int owner = -1)
        {
            if (owner < 0) owner = S.Player; string reason = GovernmentReformReason(target, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; var s = n.Government; s.Target = target; s.StartedDay = s.LastStartedDay = S.Day;
            s.QuotedGDP = Math.Max(0, n.GDP); s.Paid = GovernmentRules.ReformCost(s.QuotedGDP); n.Treasury -= s.Paid;
            n.Approval = Math.Max(0, n.Approval - 2); n.Stability = Math.Max(0, n.Stability - 3);
            RecordPolicy(owner, "Реформа: " + GovernmentRules.Forms[target].Name, "Проект начат. Оплачено " + s.Paid.ToString("N2") + " млрд. Одобрение −2, стабильность −3. До завершения: выпуск −3%, исследования −5%, сбор налогов −4%.");
            if (owner == S.Player) ComputeEconomy(false); Raise(); return null;
        }
        private static void ClearGovernmentReform(GovernmentState s)
        {
            s.Target = s.StartedDay = s.Choice = s.ConsultationDay = s.ChoiceDay = -1; s.Phase = s.Work = 0;
            s.Paid = s.QuotedGDP = s.ChoicePaid = 0; s.Paused = false;
        }
        private void WithdrawGovernmentReform(int owner, bool expired)
        {
            var s = S.Nations[owner].Government; string title = GovernmentRules.Forms[s.Target].Name; ClearGovernmentReform(s);
            RecordPolicy(owner, "Проект отозван: " + title, (expired ? "Срок рассмотрения истёк. " : "") + "Оплата и подготовка не возвращаются. Прежний строй сохранён; временные издержки сняты. Годовой срок до нового проекта сохраняется.");
        }
        public string CancelGovernmentReform(int owner = -1)
        {
            if (owner < 0) owner = S.Player; if (owner >= S.Nations.Length || S.Nations[owner].Government.Target < 0) return "Нет действующего проекта.";
            WithdrawGovernmentReform(owner, false); if (owner == S.Player) ComputeEconomy(false); Raise(); return null;
        }
        public string PauseGovernmentReform(bool paused)
        {
            var s = Player.Government; if (s.Target < 0 || s.Phase == 1) return "Пауза доступна на подготовке и внедрении; срок ответа на повестку не останавливается.";
            s.Paused = paused; Raise(); return null;
        }
        public string GovernmentProgressReason(int owner, bool? territory = null)
        {
            var n = S.Nations[owner]; var s = n.Government;
            if (s.Target < 0) return "Реформа не ведётся.";
            if (!(territory ?? HasPolicyTerritory(owner))) return "Нет контролируемой территории.";
            if (NationAtWar(owner)) return "Война приостановила реформу.";
            if (n.Stability < 35 || n.Approval < 30) return "Для продолжения нужны стабильность от 35% и одобрение от 30%.";
            if (n.Repression > GovernmentRules.Forms[s.Target].PressureLimit) return "Давление выше предела выбранного проекта.";
            return null;
        }
        public string GovernmentChoiceReason(int startedDay, int choice, int owner = -1)
        {
            if (owner < 0) owner = S.Player; if (owner >= S.Nations.Length) return "Неизвестное государство.";
            var n = S.Nations[owner]; var s = n.Government;
            if (s.Target < 0 || s.Phase != 1 || s.StartedDay != startedDay) return "Повестка уже разрешена или изменилась.";
            if (S.Day >= s.ConsultationDay + GovernmentRules.ResponseDays) return "Срок ответа истёк.";
            if (choice < 0 || choice > 2) return "Неизвестное решение.";
            if (choice == 2) return null;
            string reason = GovernmentProgressReason(owner); if (reason != null) return reason;
            return choice == 0 && n.Treasury < GovernmentRules.ConsultationCost(s.QuotedGDP) ? "Недостаточно средств на расширенное согласование." : null;
        }
        public string ResolveGovernmentConvention(int startedDay, int choice, int owner = -1)
        {
            if (owner < 0) owner = S.Player; string reason = GovernmentChoiceReason(startedDay, choice, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; var s = n.Government;
            if (choice == 2) WithdrawGovernmentReform(owner, false);
            else {
                s.Choice = choice; s.ChoiceDay = S.Day; s.ChoicePaid = choice == 0 ? GovernmentRules.ConsultationCost(s.QuotedGDP) : 0; n.Treasury -= s.ChoicePaid;
                s.Phase = 2; s.Work = 0; s.Paused = false;
                RecordPolicy(owner, "Принят порядок реформы", (choice == 0 ? "Расширенное согласование: внедрение 60 дней, при завершении одобрение +4 и стабильность +3. Оплачено " + s.ChoicePaid.ToString("N2") + " млрд." : "Ускоренное утверждение: внедрение 30 дней, при завершении одобрение −4 и стабильность −3."));
            }
            if (owner == S.Player) ComputeEconomy(false); Raise(); return null;
        }
        internal void TickGovernments()
        {
            if (!S.Nations.Any(n => n.Government.Target >= 0)) return;
            var owned = new bool[S.Nations.Length]; foreach (var p in S.Provinces) owned[p.Owner] = true;
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                var n = S.Nations[owner]; var s = n.Government; if (s.Target < 0) continue;
                if (s.Phase == 1) {
                    if (S.Day >= s.ConsultationDay + GovernmentRules.ResponseDays) { WithdrawGovernmentReform(owner, true); if (owner == S.Player) ComputeEconomy(false); continue; }
                    if (owner != S.Player) {
                        int choice = n.Treasury >= GovernmentRules.ConsultationCost(s.QuotedGDP) * 3 ? 0 : 1;
                        if (GovernmentChoiceReason(s.StartedDay, choice, owner) == null) ResolveGovernmentConvention(s.StartedDay, choice, owner);
                    }
                    continue;
                }
                if (s.Paused || GovernmentProgressReason(owner, owned[owner]) != null) continue;
                s.Work++;
                if (s.Phase == 0 && s.Work >= GovernmentRules.DraftDays) {
                    s.Phase = 1; s.Work = 0; s.ConsultationDay = S.Day;
                    RecordPolicy(owner, "Реформа готова к рассмотрению", "Проект «" + GovernmentRules.Forms[s.Target].Name + "». Выберите согласование или ускоренное утверждение. Срок — 21 день; без ответа проект отзывается.");
                } else if (s.Phase == 2 && s.Work >= GovernmentRules.ImplementationDays(s.Choice)) {
                    int choice = s.Choice; s.Form = s.Target; s.ChangedDay = S.Day; n.Leader++;
                    n.Approval = Rules.Clamp(n.Approval + (choice == 0 ? 4 : -4), 0, 100); n.Stability = Rules.Clamp(n.Stability + (choice == 0 ? 3 : -3), 0, 100);
                    ClearGovernmentReform(s);
                    RecordPolicy(owner, "Новый государственный строй", GovernmentRules.Forms[s.Form].Name + ". Переход завершён, временные издержки сняты; назначен кабинет №" + n.Leader + ".");
                    if (owner == S.Player) ComputeEconomy(false);
                }
            }
        }
        internal void PlanGovernment(int owner)
        {
            var n = S.Nations[owner]; if (n.Government.Target >= 0 || n.Government.Form != 0 || n.Stability < 60 || n.Approval < 55) return;
            int target = n.Repression > 45 ? 4 : n.Focus == 5 ? 2 : n.Science >= 3 ? 1 : 3;
            if (n.Treasury >= GovernmentRules.ReformCost(n.GDP) * 4 && GovernmentReformReason(target, owner) == null) StartGovernmentReform(target, owner);
        }
        private void ValidateGovernment(Nation n, Campaign campaign)
        {
            var s = n.Government; int day = campaign.Day;
            if (s == null || s.Form < 0 || s.Form >= GovernmentRules.Forms.Length || s.ChangedDay < -1 || s.ChangedDay > day || (s.Form == 0 ? s.ChangedDay != -1 : s.ChangedDay < 0)
                || s.LastStartedDay < -365 || s.LastStartedDay > day || (s.LastStartedDay < 0 && s.LastStartedDay != -365) || (s.Form > 0 && (s.LastStartedDay < 0 || s.ChangedDay < 75))
                || !IsFinite(s.Paid) || !IsFinite(s.QuotedGDP) || !IsFinite(s.ChoicePaid)) throw new InvalidDataException("Повреждён государственный строй.");
            if (s.Target == -1) {
                if (s.StartedDay != -1 || s.Phase != 0 || s.Work != 0 || s.Choice != -1 || s.ConsultationDay != -1 || s.ChoiceDay != -1 || s.Paid != 0 || s.QuotedGDP != 0 || s.ChoicePaid != 0 || s.Paused) throw new InvalidDataException("Не очищено состояние реформы.");
                return;
            }
            if (!GovernmentRules.ValidTarget(s.Target) || s.Target == s.Form || s.StartedDay < 0 || s.StartedDay != s.LastStartedDay || s.StartedDay > day || s.Phase < 0 || s.Phase > 2 || s.Work < 0 || s.QuotedGDP < 0
                || Math.Abs(s.Paid - GovernmentRules.ReformCost(s.QuotedGDP)) > 1e-8 || (s.Form > 0 && s.StartedDay < s.ChangedDay)) throw new InvalidDataException("Неверный проект реформы.");
            if (s.Phase == 0) {
                if (s.Work >= GovernmentRules.DraftDays || s.Work > day - s.StartedDay || s.Choice != -1 || s.ConsultationDay != -1 || s.ChoiceDay != -1 || s.ChoicePaid != 0) throw new InvalidDataException("Повреждена подготовка реформы.");
            } else {
                if (s.ConsultationDay < s.StartedDay + GovernmentRules.DraftDays || s.ConsultationDay > day) throw new InvalidDataException("Повреждена дата рассмотрения реформы.");
                if (s.Phase == 1) {
                    if (s.Work != 0 || s.Choice != -1 || s.ChoiceDay != -1 || s.ChoicePaid != 0 || s.Paused || day >= s.ConsultationDay + GovernmentRules.ResponseDays) throw new InvalidDataException("Повреждена повестка реформы.");
                } else if (s.Choice < 0 || s.Choice > 1 || s.ChoiceDay < s.ConsultationDay || s.ChoiceDay >= s.ConsultationDay + GovernmentRules.ResponseDays || s.ChoiceDay > day
                    || s.Work >= GovernmentRules.ImplementationDays(s.Choice) || s.Work > day - s.ChoiceDay || Math.Abs(s.ChoicePaid - (s.Choice == 0 ? GovernmentRules.ConsultationCost(s.QuotedGDP) : 0)) > 1e-8) throw new InvalidDataException("Повреждено внедрение реформы.");
            }
        }
    }
}
