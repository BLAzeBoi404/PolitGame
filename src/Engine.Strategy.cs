using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        private void StrategicOrders(int owner, List<int> controlled)
        {
            var borders = controlled.Where(r => World.Regions[r].Neighbors.Any(n => AtWar(owner, S.Provinces[n].Owner))).ToList();
            if (borders.Count == 0) return;
            foreach (Army unit in S.Armies.Where(a => a.Owner == owner && a.Target < 0 && a.TrainingDays == 0).ToList()) {
                if (unit.Strength < 35 || unit.Supply < 25) continue; // Recover before an offensive.
                var targets = World.Regions[unit.Region].Neighbors.Where(r => AtWar(owner, S.Provinces[r].Owner))
                    .OrderBy(r => S.Armies.Where(a => a.Region == r && AtWar(owner, a.Owner)).Sum(a => a.Strength * a.Design.Power)).ToList();
                if (targets.Count > 0) {
                    int target = targets[0];
                    var defenders = S.Armies.Where(a => a.Region == target && AtWar(owner, a.Owner)).ToList();
                    if (defenders.Count == 0 || EquipmentRules.CombatPower(unit, defenders[0], false) > defenders.Sum(a => EquipmentRules.CombatPower(a, unit, true)) * 1.1)
                        Order(unit.Id, target, false);
                    continue;
                }
                // Reinforce a nearby border, preferring sectors without a friendly garrison.
                var destinations = borders.OrderBy(r => StrategicDistance(unit.Region, r) + S.Armies.Count(a => a.Owner == owner && (a.Region == r || a.Target == r)) * 8).Take(4);
                foreach (int target in destinations) if (Order(unit.Id, target, false) == null) break;
            }
        }
        private double StrategicDistance(int from, int to)
        {
            double dx = (World.Regions[from].X - World.Regions[to].X) * Math.Cos(World.Regions[from].Y * Math.PI / 180);
            double dy = World.Regions[from].Y - World.Regions[to].Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
