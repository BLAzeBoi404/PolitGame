using System;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        private void InitializeDoctrines(Campaign campaign)
        {
            foreach (var n in campaign.Nations) n.Doctrines = new DoctrineState(); campaign.Version = 9;
        }
        public string DoctrineTrainingReason(int project, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (owner >= S.Nations.Length || !DoctrineRules.Valid(project)) return "Неизвестная ступень или государство.";
            var n = S.Nations[owner]; var state = n.Doctrines; var stage = DoctrineRules.Stages[project];
            if (state.Levels[stage.Path] > stage.Tier) return "Ступень уже освоена.";
            if (state.Levels[stage.Path] < stage.Tier) return "Сначала завершите предыдущую ступень этой школы.";
            if (state.Training >= 0) return "Штаб уже ведёт один курс. Завершите или отмените его.";
            if (!S.Provinces.Any(p => p.Owner == owner)) return "Нет контролируемой территории для подготовки штаба.";
            string reason = DoctrineRules.Prerequisite(n, project); if (reason != null) return reason;
            if (n.Military < 2 || n.Education < 20) return "Нужны военный бюджет от 2% ВВП и образование от 20.";
            return n.Treasury < DoctrineRules.TrainingCost(n.GDP, stage.Tier) ? "Недостаточно денег на штабную подготовку." : null;
        }
        public string StartDoctrineTraining(int project, int owner = -1)
        {
            if (owner < 0) owner = S.Player; string reason = DoctrineTrainingReason(project, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; var state = n.Doctrines; var stage = DoctrineRules.Stages[project];
            state.QuotedGDP = n.GDP; state.Paid = DoctrineRules.TrainingCost(n.GDP, stage.Tier); n.Treasury -= state.Paid;
            state.Training = project; state.StartedDay = S.Day; state.Work = 0; state.Paused = false;
            if (owner == S.Player) Notify("Штабная подготовка: " + stage.Name + ". Оплачено " + state.Paid.ToString("N2") + " млрд."); Raise(); return null;
        }
        private static void ClearDoctrineCourse(DoctrineState state)
        {
            state.Training = state.StartedDay = -1; state.Work = state.Paid = state.QuotedGDP = 0; state.Paused = false;
        }
        public string CancelDoctrineTraining()
        {
            if (Player.Doctrines.Training < 0) return "Нет текущего курса.";
            ClearDoctrineCourse(Player.Doctrines); Notify("Штабной курс отменён. Деньги и незавершённая подготовка не возвращаются."); Raise(); return null;
        }
        public string PauseDoctrineTraining(bool paused)
        {
            if (Player.Doctrines.Training < 0) return "Нет текущего курса.";
            Player.Doctrines.Paused = paused; Raise(); return null;
        }
        public double DoctrineTrainingRate(int owner)
        {
            var n = S.Nations[owner]; return n.Military < 2 || n.Education < 20 ? 0 : Rules.Clamp(n.Military / 2.5, .5, 2) * Rules.Clamp(n.Education / 60, .4, 1.5);
        }
        public string DoctrineAdoptionReason(int path, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (owner >= S.Nations.Length || path < 0 || path >= 6) return "Неизвестная школа или государство.";
            var n = S.Nations[owner]; var state = n.Doctrines; int service = DoctrineRules.Service(path);
            if (state.Levels[path] == 0) return "Сначала завершите первую ступень этой школы.";
            if (state.Active[service] == path) return "Эта школа уже выбрана.";
            if (state.Active[service] >= 0 && S.Day - state.ChangedDay[service] < DoctrineRules.ChangeCooldown) return "Смена школы через " + (DoctrineRules.ChangeCooldown - S.Day + state.ChangedDay[service]) + " дн.";
            if (!S.Provinces.Any(p => p.Owner == owner)) return "Нет контролируемой территории.";
            return n.Treasury < DoctrineRules.AdoptionCost(n, path) ? "Недостаточно денег на переучивание." : null;
        }
        public string AdoptDoctrine(int path, int owner = -1)
        {
            if (owner < 0) owner = S.Player; string reason = DoctrineAdoptionReason(path, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; var state = n.Doctrines; int service = DoctrineRules.Service(path);
            n.Treasury -= DoctrineRules.AdoptionCost(n, path); state.Active[service] = path; state.ChangedDay[service] = S.Day;
            if (owner == S.Player) Notify("Выбрана школа «" + DoctrineRules.Names[path] + "». Переучивание — 30 дней; до его конца эффекты этого рода войск не действуют."); Raise(); return null;
        }
        public string DoctrineStatus(int owner, int service)
        {
            var state = S.Nations[owner].Doctrines; int path = state.Active[service];
            if (path < 0) return "Общий устав · школа не выбрана";
            int remaining = state.ChangedDay[service] + DoctrineRules.AdaptationDays - S.Day;
            return DoctrineRules.Names[path] + " · " + state.Levels[path] + "/3" + (remaining > 0 ? " · переучивание " + remaining + " дн." : " · действует");
        }
        private void TickDoctrines()
        {
            if (!S.Nations.Any(n => n.Doctrines.Training >= 0 && !n.Doctrines.Paused)) return;
            var owned = new bool[S.Nations.Length]; foreach (var province in S.Provinces) owned[province.Owner] = true;
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                var n = S.Nations[owner]; var state = n.Doctrines; if (state.Training < 0 || state.Paused || !owned[owner]) continue;
                var stage = DoctrineRules.Stages[state.Training]; state.Work += DoctrineTrainingRate(owner);
                if (state.Work < stage.Duration) continue;
                state.Levels[stage.Path]++; ClearDoctrineCourse(state);
                if (owner == S.Player) Notify("Штаб освоил: " + stage.Name + ". " + (state.Active[DoctrineRules.Service(stage.Path)] == stage.Path ? "Выбранная школа усилена." : "Примените школу в разделе доктрин."), 1);
            }
        }
        internal void PlanDoctrines(int owner)
        {
            var n = S.Nations[owner]; var state = n.Doctrines;
            int land = n.Treasury < n.GDP * .02 || n.Stock[2] < 1 ? 2 : owner % 3;
            int air = 3 + owner % 3;
            for (int service = 0; service < 2; service++) {
                int preferred = service == 0 ? land : air;
                if (state.Active[service] >= 0) preferred = state.Active[service];
                else for (int path = service * 3; path < service * 3 + 3; path++) if (state.Levels[path] > 0) { preferred = path; break; }
                if (state.Active[service] < 0 && state.Levels[preferred] > 0 && n.Treasury > DoctrineRules.AdoptionCost(n, preferred) * 3) AdoptDoctrine(preferred, owner);
                if (state.Training >= 0 || state.Levels[preferred] >= 3) continue;
                int project = preferred * 3 + state.Levels[preferred];
                if (n.Treasury > DoctrineRules.TrainingCost(n.GDP, state.Levels[preferred]) * 4 && DoctrineTrainingReason(project, owner) == null) StartDoctrineTraining(project, owner);
            }
        }
        public double GroundCombatFactor(int owner, bool defending)
        {
            var effects = DoctrineRules.Effects(S.Nations[owner], S.Day); return GroundTechnologyFactor(owner) * (1 + (defending ? effects.Defense : effects.Attack)) * (1 - ReadinessRules.Exhaustion(S.Nations[owner]) * .002);
        }
        public double GroundUpkeep(Army army) => GroundUpkeep(army, DoctrineRules.Effects(S.Nations[army.Owner], S.Day));
        private static double GroundUpkeep(Army army, DoctrineEffects effects)
        {
            double attachments = 0; foreach (var a in army.Attachments) attachments += a.Blueprint.Maintenance;
            return (army.Design.Maintenance * army.Battalions + attachments) * (1 + effects.Maintenance);
        }
        public double AirUpkeep(AirWing wing) => wing.Design.Maintenance * (1 + DoctrineRules.Effects(S.Nations[wing.Owner], S.Day).AirMaintenance);
        public double AirDoctrineFactor(int owner, AirMission mission)
        {
            var e = DoctrineRules.Effects(S.Nations[owner], S.Day);
            return 1 + (mission == AirMission.Superiority ? e.AirCover : mission == AirMission.Strike ? e.AirStrike : mission == AirMission.Recon ? e.AirRecon : mission == AirMission.Supply ? e.AirSupply : 0);
        }
        private void ValidateDoctrines(Nation n, Campaign campaign)
        {
            var s = n.Doctrines;
            if (s == null || s.Levels == null || s.Levels.Length != 6 || s.Levels.Any(v => v < 0 || v > 3) || s.Active == null || s.Active.Length != 2 || s.ChangedDay == null || s.ChangedDay.Length != 2
                || s.Training < -1 || s.Training >= 18 || !IsFinite(s.Work) || !IsFinite(s.Paid) || !IsFinite(s.QuotedGDP)) throw new InvalidDataException("Повреждена доктринальная подготовка.");
            for (int service = 0; service < 2; service++) {
                int path = s.Active[service];
                if (path == -1 ? s.ChangedDay[service] != -180 : path < service * 3 || path >= service * 3 + 3 || s.Levels[path] == 0 || s.ChangedDay[service] < 0 || s.ChangedDay[service] > campaign.Day) throw new InvalidDataException("Неверная действующая военная школа.");
            }
            for (int path = 0; path < 6; path++) for (int tier = 0; tier < s.Levels[path]; tier++) if (DoctrineRules.Prerequisite(n, path * 3 + tier) != null) throw new InvalidDataException("Нарушены предпосылки военной доктрины.");
            if (s.Training < 0) {
                if (s.StartedDay != -1 || s.Work != 0 || s.Paid != 0 || s.QuotedGDP != 0 || s.Paused) throw new InvalidDataException("Курс завершён, но подготовка не очищена.");
            } else {
                var stage = DoctrineRules.Stages[s.Training];
                if (s.Levels[stage.Path] != stage.Tier || s.StartedDay < 0 || s.StartedDay > campaign.Day || s.Work < 0 || s.Work >= stage.Duration || s.QuotedGDP <= 0 || Math.Abs(s.Paid - DoctrineRules.TrainingCost(s.QuotedGDP, stage.Tier)) > 1e-8 || DoctrineRules.Prerequisite(n, s.Training) != null)
                    throw new InvalidDataException("Невозможный курс штабной подготовки.");
            }
        }
    }
}
