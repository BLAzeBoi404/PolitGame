using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        public FrontPlan FrontFor(int armyId) => S.Fronts.FirstOrDefault(p => p.ArmyIds.Contains(armyId));
        private void DetachFromFront(int armyId) { foreach (var plan in S.Fronts) plan.ArmyIds.Remove(armyId); }
        public string CreateFront(int enemy, IEnumerable<int> armyIds, FrontStance stance)
        {
            if (enemy < 0 || enemy >= S.Nations.Length || !AtWar(S.Player, enemy)) return "Выберите государство, с которым идёт война.";
            if (S.Fronts.Count(p => p.Owner == S.Player) >= 24) return "Лимит: 24 фронта на страну.";
            if (!Enum.IsDefined(typeof(FrontStance), stance) || armyIds == null) return "Некорректный приказ.";
            var ids = armyIds.Distinct().ToList();
            if (ids.Count == 0 || ids.Any(id => !S.Armies.Any(a => a.Id == id && a.Owner == S.Player))) return "Отметьте свои соединения в списке.";
            foreach (int id in ids) {
                DetachFromFront(id);
                var unit = S.Armies.First(a => a.Id == id); unit.Target = -1; unit.Route.Clear();
            }
            var plan = new FrontPlan { Id = S.NextFront++, Owner = S.Player, Enemy = enemy, Stance = stance, ArmyIds = ids,
                Name = "Фронт " + World.Countries[enemy].Name };
            S.Fronts.Add(plan); ExecuteFront(plan);
            Notify(plan.Name + ": " + ids.Count + " соединений. " + (stance == FrontStance.Hold ? "Занять и удерживать границу." : "Наступать при достаточных силах."), 1);
            Raise(); return null;
        }
        public string ChangeFront(int id, FrontStance stance, bool active)
        {
            var plan = S.Fronts.FirstOrDefault(p => p.Id == id && p.Owner == S.Player);
            if (plan == null || !Enum.IsDefined(typeof(FrontStance), stance)) return "Фронт не найден.";
            if (active && !AtWar(plan.Owner, plan.Enemy)) return "Для выполнения приказа нужна война с выбранной страной.";
            plan.Stance = stance; plan.Active = active;
            foreach (var unit in S.Armies.Where(a => plan.ArmyIds.Contains(a.Id))) { unit.Target = -1; unit.Route.Clear(); }
            if (active) ExecuteFront(plan);
            Raise(); return null;
        }
        public void DeleteFront(int id)
        {
            var plan = S.Fronts.FirstOrDefault(p => p.Id == id && p.Owner == S.Player);
            if (plan == null) return;
            foreach (var unit in S.Armies.Where(a => plan.ArmyIds.Contains(a.Id))) { unit.Target = -1; unit.Route.Clear(); }
            S.Fronts.Remove(plan); Raise();
        }
        private void PruneFronts()
        {
            if (S.Fronts.Count == 0) return;
            var live = new HashSet<int>(S.Armies.Select(a => a.Id));
            foreach (var plan in S.Fronts) {
                plan.ArmyIds.RemoveAll(id => !live.Contains(id));
                if (!AtWar(plan.Owner, plan.Enemy)) plan.Active = false;
            }
        }
        public string FrontStatus(FrontPlan plan)
        {
            if (!plan.Active) return "Приказ приостановлен";
            if (plan.ArmyIds.Count == 0) return "Нет назначенных частей";
            if (!AtWar(plan.Owner, plan.Enemy)) return "Война завершена";
            if (FrontRegions(plan).Count == 0) return "Нет доступной сухопутной границы";
            var members = new HashSet<int>(plan.ArmyIds);
            var units = S.Armies.Where(a => members.Contains(a.Id)).ToList();
            if (units.All(a => a.TrainingDays > 0)) return "Части проходят подготовку";
            if (units.All(a => a.Supply < 45 || a.Strength < 45 || a.TrainingDays > 0 || a.Organization < 40 || ManpowerRules.Manning(a) < .4)) return "Пополнение, организация и снабжение";
            return plan.Stance == FrontStance.Hold ? "Занять и удерживать границу" : "Наступать при перевесе сил";
        }
        private void ExecuteFronts() { foreach (var plan in S.Fronts) ExecuteFront(plan); }
        public List<int> FrontRegions(FrontPlan plan)
        {
            var result = new List<int>();
            for (int r = 0; r < S.Provinces.Length; r++)
                if (HasAccess(plan.Owner, S.Provinces[r].Owner) && World.Regions[r].Neighbors.Any(n => S.Provinces[n].Owner == plan.Enemy)) result.Add(r);
            return result;
        }
        private void ExecuteFront(FrontPlan plan)
        {
            if (!plan.Active || !AtWar(plan.Owner, plan.Enemy)) return;
            var border = FrontRegions(plan); if (border.Count == 0) return;
            var borderSet = new HashSet<int>(border);
            var assigned = new HashSet<int>(plan.ArmyIds);
            var units = S.Armies.Where(a => assigned.Contains(a.Id)).ToList();
            var occupancy = S.Armies.GroupBy(a => a.Region).ToDictionary(g => g.Key, g => g.ToList());
            var stationed = new Dictionary<int, int>();
            foreach (var unit in units) {
                int r = unit.Route.Count > 0 ? unit.Route[unit.Route.Count - 1] : unit.Target >= 0 ? unit.Target : unit.Region;
                if (!stationed.ContainsKey(r)) stationed[r] = 0; stationed[r]++;
            }
            foreach (var unit in units) {
                if (unit.Target >= 0 || unit.TrainingDays > 0 || unit.Strength < 45 || unit.Supply < 45 || unit.Organization < 40 || ManpowerRules.Manning(unit) < .4) continue;
                if (borderSet.Contains(unit.Region)) {
                    if (plan.Stance == FrontStance.Hold) continue;
                    foreach (int target in World.Regions[unit.Region].Neighbors.Where(r => S.Provinces[r].Owner == plan.Enemy)) {
                        List<Army> defenders;
                        occupancy.TryGetValue(target, out defenders);
                        defenders = defenders == null ? new List<Army>() : defenders.Where(a => AtWar(unit.Owner, a.Owner)).ToList();
                        if (defenders.Count > 0 && EquipmentRules.CombatPower(unit, defenders[0], false) * GroundTechnologyFactor(unit.Owner) < defenders.Sum(a => EquipmentRules.CombatPower(a, unit, true) * GroundTechnologyFactor(a.Owner)) * 1.2) continue;
                        Order(unit.Id, target, false); break;
                    }
                    continue;
                }
                foreach (int destination in border.OrderBy(r => StrategicDistance(unit.Region, r) + (stationed.ContainsKey(r) ? stationed[r] * 8 : 0)).Take(6)) {
                    if (Order(unit.Id, destination, false, false) != null) continue;
                    if (!stationed.ContainsKey(destination)) stationed[destination] = 0;
                    stationed[destination]++; break;
                }
            }
        }
    }
}
