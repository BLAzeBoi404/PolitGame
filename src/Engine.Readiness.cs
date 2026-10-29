using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        private void InitializeReadiness(Campaign campaign)
        {
            foreach (var n in campaign.Nations)
                n.Condition = new MilitaryCondition { AccountedLosses = n.PersonnelLosses };
            foreach (var a in campaign.Armies) { a.Fatigue = 0; a.ExerciseRemaining = 0; }
            campaign.ReadinessDay = -1;
            campaign.Version = 14;
        }

        private bool CountryAtWar(int owner)
        {
            int offset = owner * S.Nations.Length;
            for (int other = 0; other < S.Nations.Length; other++) if (S.Wars[offset + other]) return true;
            return false;
        }

        public double ExerciseDailyCost(Army army) => (ReadinessRules.ExerciseCash(army) + GroundUpkeep(army) * .15) * (1 + Math.Max(0, army.Proficiency - 50) / 20);
        private static bool ExerciseLimitReached(Army army) => army.Experience >= ReadinessRules.ExerciseExperienceLimit && army.Proficiency >= UnitQualityRules.ExerciseTrainingLimit;

        private string ExerciseEnvironmentReason(Army army, bool[] network)
        {
            if (army.TrainingDays > 0) return "Сначала завершите начальную подготовку или перевооружение.";
            if (army.Target >= 0 || S.Provinces[army.Region].Owner != army.Owner) return "Учения требуют стоянки в собственной области.";
            if (!IsSupplied(army.Owner, army.Region, network) || army.Supply < 60) return "Для учений нужны сухопутное снабжение и обеспеченность от 60%.";
            if (army.Strength < 70 || ManpowerRules.Manning(army) < .7 || army.Organization < 50) return "Нужны техника и личный состав от 70%, организация от 50%.";
            foreach (int r in World.Regions[army.Region].Neighbors)
                if (AtWar(army.Owner, S.Provinces[r].Owner)) return "Рядом фронт: часть должна оставаться в боевой готовности.";
            return null;
        }

        public string ExerciseReason(int id, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            var army = S.Armies.FirstOrDefault(a => a.Id == id && a.Owner == owner);
            if (army == null) return "Выберите свою часть.";
            return ExerciseReason(army, SupplyNetwork());
        }

        private string ExerciseReason(Army army, bool[] network)
        {
            if (army.ExerciseRemaining > 0) return "Курс учений уже назначен.";
            if (ExerciseLimitReached(army)) return "Достигнут предел учений: подготовка 90, опыт 35; дальнейший опыт приобретается в бою.";
            if (army.Fatigue > 50) return "Перед новым курсом нужен отдых: усталость не выше 50%.";
            string reason = ExerciseEnvironmentReason(army, network);
            return reason ?? ExerciseFundingReason(army);
        }

        private string ExerciseFundingReason(Army army)
        {
            var n = S.Nations[army.Owner];
            if (n.Treasury < ExerciseDailyCost(army) + GroundUpkeep(army)) return "Не хватает денег на день учений и обычное содержание.";
            if (n.Stock[2] < ReadinessRules.ExerciseFuel(army)) return "Не хватает топлива для учений.";
            if (n.Stock[7] < ReadinessRules.ExerciseArms(army)) return "Не хватает вооружения и учебных боеприпасов.";
            return null;
        }

        public string ExerciseStatus(Army army)
        {
            if (army.ExerciseRemaining == 0) return "Учения не назначены";
            return ExerciseEnvironmentReason(army, SupplyNetwork()) ?? ExerciseFundingReason(army)
                ?? (army.Fatigue >= 65 ? "Пауза для отдыха; курс продолжится после восстановления" : "Учения: осталось " + army.ExerciseRemaining + " оплаченных дней");
        }

        public string StartExercise(int id, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            var army = S.Armies.FirstOrDefault(a => a.Id == id && a.Owner == owner);
            return army == null ? "Выберите свою часть." : StartExercise(army, SupplyNetwork());
        }

        // UI and AI share this exact validation and mutation. During the bounded AI
        // pass ownership/treaties cannot change, so one supply snapshot is sufficient.
        private string StartExercise(Army army, bool[] network)
        {
            string reason = ExerciseReason(army, network); if (reason != null) return reason;
            army.ExerciseRemaining = ReadinessRules.ExerciseLength;
            DetachFromFront(army.Id);
            if (army.Owner == S.Player) { Notify("Часть №" + army.Id + ": назначен курс учений на 21 день. Оплата и расход материалов ежедневно."); Raise(); }
            return null;
        }

        public string StopExercise(int id, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            var army = S.Armies.FirstOrDefault(a => a.Id == id && a.Owner == owner);
            if (army == null || army.ExerciseRemaining == 0) return "Действующий курс не найден.";
            army.ExerciseRemaining = 0;
            if (owner == S.Player) { Notify("Учения части №" + id + " прекращены. Полученный опыт и усталость сохраняются."); Raise(); }
            return null;
        }

        internal void TickReadiness(bool[] network = null)
        {
            if (S.ReadinessDay == S.Day) return;
            S.ReadinessDay = S.Day;
            network = network ?? SupplyNetwork();
            var serving = new long[S.Nations.Length];
            foreach (var army in S.Armies) {
                serving[army.Owner] += army.Personnel;
                bool exercised = false;
                if (army.ExerciseRemaining > 0) {
                    if (ExerciseLimitReached(army)) army.ExerciseRemaining = 0;
                    else if (army.Fatigue < 65 && ExerciseEnvironmentReason(army, network) == null && ExerciseFundingReason(army) == null) {
                        var n = S.Nations[army.Owner];
                        n.Treasury -= ExerciseDailyCost(army);
                        n.Stock[2] -= ReadinessRules.ExerciseFuel(army);
                        n.Stock[7] -= ReadinessRules.ExerciseArms(army);
                        if (army.Experience < ReadinessRules.ExerciseExperienceLimit) army.Experience = Math.Min(ReadinessRules.ExerciseExperienceLimit, army.Experience + .65 * (.7 + .3 * Rules.Clamp(n.Education, 0, 100) / 100));
                        if (army.Proficiency < UnitQualityRules.ExerciseTrainingLimit) army.Proficiency = Math.Min(UnitQualityRules.ExerciseTrainingLimit, army.Proficiency + .45 * (1 - army.Proficiency / 150) * (.7 + .3 * Rules.Clamp(n.Education, 0, 100) / 100));
                        army.ExerciseRemaining--;
                        army.Fatigue = Math.Min(100, army.Fatigue + 1.1);
                        army.Organization = Math.Max(0, army.Organization - 1.5);
                        exercised = true;
                        if (army.ExerciseRemaining == 0 && army.Owner == S.Player) Notify("Часть №" + army.Id + " завершила учения. Для восстановления боеготовности нужен отдых.", 1);
                    }
                }
                if (army.Target >= 0) army.Fatigue = Math.Min(100, army.Fatigue + .35);
                else if (!exercised) {
                    bool supported = IsSupplied(army.Owner, army.Region, network) && army.Supply >= 60;
                    double rest = supported ? 1.5 : .25;
                    if (S.Nations[army.Owner].Treasury < GroundUpkeep(army)) rest *= .5;
                    army.Fatigue = Math.Max(0, army.Fatigue - rest);
                }
            }
            foreach (var wing in S.AirWings) serving[wing.Owner] += wing.Personnel;
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                var n = S.Nations[owner]; var condition = n.Condition;
                long losses = Math.Max(0, n.PersonnelLosses - condition.AccountedLosses);
                condition.AccountedLosses = n.PersonnelLosses;
                double lossPressure = Math.Min(5, losses * 150.0 / Math.Max(1000, serving[owner] + losses));
                double pressure = CountryAtWar(owner) ? .015 + Math.Max(0, n.Shortage) * .03 + Math.Max(0, (int)n.Conscription - 1) * .005 : -.10;
                condition.Exhaustion = Rules.Clamp(condition.Exhaustion + pressure + lossPressure, 0, 100);
            }
        }

        public MilitaryAssessment AssessMilitary(int owner) => AssessMilitary(owner, S.Armies.Where(a => a.Owner == owner).ToList());
        private MilitaryAssessment AssessMilitary(int owner, List<Army> units)
        {
            var n = S.Nations[owner];
            var result = new MilitaryAssessment { Formations = units.Count, AtWar = CountryAtWar(owner) };
            double fuel = 0, arms = 0;
            double fuelFactor = (1 + TechnologyRules.Effects(n).GroundFuel) * (1 + DoctrineRules.Effects(n, S.Day).Fuel);
            foreach (var a in units) {
                result.Battalions += a.Battalions;
                result.DailyUpkeep += GroundUpkeep(a);
                result.Readiness += ReadinessRules.Score(a) * a.Battalions;
                if (a.ExerciseRemaining > 0) result.Exercising++;
                else if (ReadinessRules.Score(a) >= 65) result.Ready++;
                // Estimate an operational reserve, even when formations are parked.
                fuel += ((a.Design.Fuel + .003) * a.Battalions + a.Attachments.Sum(t => t.Blueprint.Fuel * .3)) * fuelFactor;
                arms += .01 * a.Battalions;
            }
            result.Readiness /= Math.Max(1, result.Battalions);
            result.DailyCommitment = result.DailyUpkeep + Math.Max(0, n.Spending - n.Revenue) / 7;
            result.FuelDays = fuel <= 0 ? 365 : Math.Min(365, n.Stock[2] / fuel);
            result.ArmsDays = arms <= 0 ? 365 : Math.Min(365, n.Stock[7] / arms);
            if (n.Treasury < result.DailyCommitment * 7 || result.FuelDays < 7 || result.ArmsDays < 7) {
                result.Posture = "Обеспечение"; result.Reason = "Нужен запас денег, топлива и вооружения хотя бы на семь дней операций.";
            } else if (result.Readiness < 60 || n.Condition.Exhaustion >= 60) {
                result.Posture = result.AtWar ? "Оборона и восстановление" : "Восстановление";
                result.Reason = "Отдых, пополнение и снижение военной усталости важнее наступления.";
            } else {
                result.Posture = result.AtWar ? "Боевые действия" : "Плановая подготовка";
                result.Reason = result.AtWar ? "Готовые части могут наступать при местном перевесе; остальные восстанавливаются." : "Можно обучать часть войск, сохраняя боеспособный резерв.";
            }
            return result;
        }

        internal void PlanMilitaryPreparation()
        {
            var network = SupplyNetwork();
            var units = new List<Army>[S.Nations.Length];
            for (int owner = 0; owner < units.Length; owner++) units[owner] = new List<Army>();
            foreach (var army in S.Armies) units[army.Owner].Add(army);
            for (int owner = 0; owner < units.Length; owner++) {
                if (owner == S.Player || units[owner].Count == 0) continue;
                var n = S.Nations[owner]; var report = AssessMilitary(owner, units[owner]);
                bool emergency = report.AtWar || n.Condition.Exhaustion >= 40 || report.FuelDays < 7 || report.ArmsDays < 7 || n.Treasury < report.DailyCommitment * 14;
                if (emergency) {
                    foreach (var a in units[owner]) if (a.ExerciseRemaining > 0) StopExercise(a.Id, owner);
                    if (report.AtWar) n.Trade.ReserveWeeks = Math.Max(4, n.Trade.ReserveWeeks);
                    continue;
                }
                int capacity = Math.Max(units[owner].Min(a => a.Battalions), (int)Math.Ceiling(report.Battalions * .1 * Rules.Clamp(n.Education / 60, .5, 1.5)));
                double budget = Math.Max(0, n.Treasury - report.DailyCommitment * 14), fuel = n.Stock[2], arms = n.Stock[7];
                foreach (var a in units[owner].Where(a => a.ExerciseRemaining > 0)) {
                    capacity -= a.Battalions; budget -= ExerciseDailyCost(a) * a.ExerciseRemaining;
                    fuel -= ReadinessRules.ExerciseFuel(a) * a.ExerciseRemaining; arms -= ReadinessRules.ExerciseArms(a) * a.ExerciseRemaining;
                }
                foreach (var a in units[owner].Where(a => a.ExerciseRemaining == 0).OrderBy(a => a.Experience).ThenBy(a => a.Id)) {
                    double cashCost = ExerciseDailyCost(a) * ReadinessRules.ExerciseLength * 1.5;
                    double fuelCost = ReadinessRules.ExerciseFuel(a) * ReadinessRules.ExerciseLength, armsCost = ReadinessRules.ExerciseArms(a) * ReadinessRules.ExerciseLength;
                    if (capacity < a.Battalions || budget < cashCost || fuel < fuelCost || arms < armsCost) continue;
                    if (StartExercise(a, network) != null) continue;
                    capacity -= a.Battalions; budget -= cashCost; fuel -= fuelCost; arms -= armsCost;
                }
            }
        }

        private void PrepareAIForNewWar(int attacker, List<int> defenders)
        {
            foreach (var a in S.Armies)
                if (a.Owner != S.Player && a.ExerciseRemaining > 0 && (a.Owner == attacker || defenders.Contains(a.Owner))) StopExercise(a.Id, a.Owner);
            for (int owner = 0; owner < S.Nations.Length; owner++)
                if (owner != S.Player && (owner == attacker || defenders.Contains(owner))) S.Nations[owner].Trade.ReserveWeeks = Math.Max(4, S.Nations[owner].Trade.ReserveWeeks);
        }

        internal bool CanPrepareWar(int owner, int enemy)
        {
            if (CountryAtWar(owner) || Allied(owner, enemy) || S.Agreements[Pair(owner, enemy)] || S.TruceUntil[Pair(owner, enemy)] > S.Day) return false;
            var n = S.Nations[owner]; var report = AssessMilitary(owner);
            if (report.Readiness < 65 || report.Ready < Math.Max(1, report.Formations * .6) || report.FuelDays < 14 || report.ArmsDays < 14
                || n.Condition.Exhaustion >= 30 || n.Treasury < report.DailyCommitment * 30) return false;
            double own = 0, opposition = 0;
            foreach (var a in S.Armies) {
                if (a.Owner == owner && a.ExerciseRemaining == 0) own += PlanningPower(a);
            }
            for (int other = 0; other < S.Nations.Length; other++)
                if (other == enemy || Allied(other, enemy)) opposition += EstimatedPersonnelPower(other, World.Countries[other].StartingGroundPersonnel);
            return own > opposition * 1.3 && own > 0;
        }

        private void ValidateReadiness(Campaign campaign)
        {
            if (campaign.ReadinessDay < -1 || campaign.ReadinessDay > campaign.Day) throw new InvalidDataException("Повреждена дата военной подготовки.");
            foreach (var n in campaign.Nations)
                if (n.Condition == null || !IsFinite(n.Condition.Exhaustion) || n.Condition.Exhaustion < 0 || n.Condition.Exhaustion > 100
                    || n.Condition.AccountedLosses < 0 || n.Condition.AccountedLosses > n.PersonnelLosses)
                    throw new InvalidDataException("Повреждён учёт военной усталости.");
            foreach (var a in campaign.Armies)
                if (!IsFinite(a.Fatigue) || a.Fatigue < 0 || a.Fatigue > 100 || a.ExerciseRemaining < 0 || a.ExerciseRemaining > ReadinessRules.ExerciseLength
                    || (a.ExerciseRemaining > 0 && (a.Target >= 0 || a.TrainingDays > 0))) throw new InvalidDataException("Повреждены учения или усталость части.");
        }
    }
}
