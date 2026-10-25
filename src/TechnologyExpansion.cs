using System;
using System.Linq;

namespace Sovereign
{
    public static partial class TechnologyRules
    {
        public const int Count = 72, ProjectCount = Count + 6;
        public static readonly string[] StageNames = { "ОСНОВЫ", "МЕХАНИЗАЦИЯ", "МАССОВОЕ ВНЕДРЕНИЕ", "АВТОМАТИЗАЦИЯ", "ЦИФРОВОЙ ПЕРЕХОД", "НОВЫЙ УКЛАД", "ПЕРСПЕКТИВА" };
        static TechnologyRules()
        {
            int[,] positions = { {0, 1}, {1, 1}, {2, 2}, {4, 2}, {0, 1}, {2, 3}, {2, 1}, {3, 1}, {0, 1}, {1, 2}, {2, 1}, {4, 2}, {0, 1}, {1, 2}, {2, 3}, {4, 2}, {0, 1}, {1, 2}, {2, 3}, {3, 3}, {2, 1}, {2, 0}, {2, 2}, {4, 1}, {0, 0}, {1, 0}, {2, 0}, {3, 1}, {2, 3}, {3, 0}, {4, 1}, {5, 1}, {1, 0}, {1, 2}, {2, 0}, {3, 0}, {3, 3}, {3, 2}, {4, 2}, {5, 1}, {1, 0}, {2, 0}, {3, 0}, {3, 3}, {4, 0}, {4, 3}, {5, 1}, {6, 1}, {1, 0}, {2, 0}, {3, 0}, {3, 2}, {4, 0}, {3, 3}, {5, 0}, {6, 1}, {1, 0}, {2, 0}, {3, 1}, {2, 2}, {4, 1}, {3, 0}, {5, 2}, {6, 1}, {0, 0}, {1, 0}, {0, 2}, {1, 3}, {1, 2}, {3, 0}, {3, 2}, {5, 1} };
            for (int i = 0; i < Catalog.Length; i++) { Catalog[i].Column = positions[i, 0]; Catalog[i].Lane = positions[i, 1]; }
            Branches = Enumerable.Range(0, 6).Select(b => Catalog.Where(t => t.Branch == b).ToArray()).ToArray();
        }
        public static readonly TechnologyDefinition[][] Branches;
        private static void ApplyExpandedEffects(bool[] known, ref TechnologyEffects e)
        {
            if (known[24]) { e.Food += .03; }
            if (known[25]) { e.Food += .04; }
            if (known[26]) { e.Food += .06; e.FoodPowerInput += .05; }
            if (known[27]) { e.Food += .12; e.FoodFuelInput += .05; }
            if (known[28]) { e.Food += .04; }
            if (known[29]) { e.Food += .04; e.FoodFuelInput -= .03; }
            if (known[30]) { e.Food += .06; e.FoodFuelInput -= .04; }
            if (known[31]) { e.Food += .10; e.HouseholdFood -= .02; }
            if (known[32]) { e.Electricity += .04; }
            if (known[33]) { e.PowerInput -= .03; }
            if (known[34]) { e.Electricity += .06; }
            if (known[35]) { e.Electricity += .10; }
            if (known[36]) { e.Electricity += .05; e.Fuel += .04; }
            if (known[37]) { e.Electricity += .06; }
            if (known[38]) { e.PowerInput -= .04; }
            if (known[39]) { e.Electricity += .08; e.PowerInput -= .03; }
            if (known[40]) { e.Equipment += .04; }
            if (known[41]) { e.Equipment += .04; }
            if (known[42]) { e.Equipment += .08; }
            if (known[43]) { e.EquipmentSteel -= .03; }
            if (known[44]) { e.Equipment += .08; }
            if (known[45]) { e.Research += .04; }
            if (known[46]) { e.Steel += .04; e.PowerInput -= .03; }
            if (known[47]) { e.Equipment += .10; e.Construction += .04; }
            if (known[48]) { e.Construction += .04; }
            if (known[49]) { e.HouseholdFood -= .02; }
            if (known[50]) { e.Construction += .06; }
            if (known[51]) { e.GroundFuel -= .04; e.SupplyDays += .5; }
            if (known[52]) { e.Construction += .06; }
            if (known[53]) { e.GroundFuel -= .03; e.AirFuel -= .03; }
            if (known[54]) { e.Replacements += .10; }
            if (known[55]) { e.GroundFuel -= .03; e.SupplyDays += 1; }
            if (known[56]) { e.PopulationGrowth += .0002; }
            if (known[57]) { e.Intake += .04; e.PopulationGrowth += .0002; }
            if (known[58]) { e.Casualties -= .04; }
            if (known[59]) { e.Replacements += .10; e.Casualties -= .02; }
            if (known[60]) { e.Casualties -= .03; }
            if (known[61]) { e.Intake += .04; }
            if (known[62]) { e.PopulationGrowth += .0003; }
            if (known[63]) { e.Intake += .05; e.Casualties -= .03; }
            if (known[64]) { e.GroundCombat += .04; }
            if (known[65]) { e.GroundCombat += .04; }
            if (known[66]) { e.OrganizationRecovery += .15; }
            if (known[67]) { e.AirOperations += .05; }
            if (known[68]) { e.AirOperations += .05; }
            if (known[69]) { e.GroundCombat += .06; e.AirOperations += .03; }
            if (known[70]) { e.GroundCombat += .05; e.OrganizationRecovery += .10; }
            if (known[71]) { e.GroundCombat += .05; e.AirOperations += .04; }
        }
    }
}
