using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public class ResearchAgenda
    {
        public bool[] Known = new bool[24];
        public double[] StoredProgress = new double[30];
        public int[] LegacyLevels = new int[6];
        public int ActiveProject = -1;
        public bool Paused;
        public List<int> Queue = new List<int>();
    }
    public class TechnologyDefinition
    {
        public int Id, Branch, Foundation;
        public string Name, Description;
        public double Work;
        public int[] Prerequisites;
    }
    public struct TechnologyEffects
    {
        public double Food, Electricity, Fuel, Steel, Equipment, Construction, Research, Intake, Replacements, PopulationGrowth, AirOperations;
        public double FoodFuelInput, PowerInput, EquipmentSteel, HouseholdFood, GroundFuel, AirFuel, Casualties, SupplyDays;
    }
    public static class TechnologyRules
    {
        private static TechnologyDefinition T(int id, int branch, int foundation, string name, string effect, double work, params int[] prerequisites)
            => new TechnologyDefinition { Id = id, Branch = branch, Foundation = foundation, Name = name, Description = effect, Work = work, Prerequisites = prerequisites };
        public static readonly TechnologyDefinition[] Catalog = {
            T(0, 0, 1, "Селекционные станции", "Выпуск продовольствия +5%.", .65),
            T(1, 0, 2, "Аграрная механизация", "Выпуск продовольствия +8%; расход топлива агрокомплексами −10%.", .85, 0, 8),
            T(2, 0, 3, "Холодовые цепочки", "Бытовая потребность в продовольствии −5% благодаря снижению потерь. Промышленный расход не меняется.", 1.0, 1, 12),
            T(3, 0, 5, "Управляемое земледелие", "Выпуск продовольствия +10%.", 1.2, 2, 6),
            T(4, 1, 1, "Диспетчеризация сетей", "Выпуск электроэнергии +6%.", .65),
            T(5, 1, 2, "Глубокая переработка", "Выпуск топлива +8%.", .85, 4, 8),
            T(6, 1, 3, "Накопители энергии", "Выпуск электроэнергии +5%.", 1.0, 4, 8),
            T(7, 1, 5, "Интеллектуальные сети", "Выпуск электроэнергии +10%; расход электричества всеми производствами −8%.", 1.25, 6, 10),
            T(8, 2, 1, "Точное машиностроение", "Темп сборки техники +5%. Основа последующих прикладных проектов.", .65),
            T(9, 2, 2, "Высокопрочные сплавы", "Выпуск стали +6%. Корпуса уровня 7 при промышленности 5.", .85, 8, 4),
            T(10, 2, 3, "Промышленная автоматика", "Темп сборки техники +8%. Двигатели уровня 7 при промышленности 5.", 1.0, 8, 6),
            T(11, 2, 5, "Аддитивное производство", "Темп сборки +5%; сталь на комплект техники −5%. Двигатели, корпуса и ремонт уровня 8 при промышленности 6.", 1.25, 9, 10),
            T(12, 3, 1, "Планирование маршрутов", "Новые стройки завершаются быстрее: темп строительства +8%.", .65),
            T(13, 3, 2, "Топливные стандарты", "Топливо наземных частей −8%, авиационных вылетов −5%. Баки уровня 7 при логистике 5.", .85, 12, 8),
            T(14, 3, 3, "Мобильные склады", "Автономность снабжаемых частей +2 дня. Ходовая уровня 7 при логистике 5.", 1.0, 12, 9),
            T(15, 3, 5, "Единая система снабжения", "Темп новых строек +8%, лимит пополнения наземных частей +20%. Ходовая и баки уровня 8 при логистике 6.", 1.25, 14, 22),
            T(16, 4, 1, "Служба общественного здоровья", "Недельный набор в резерв +5%; годовой темп роста населения +0,02 процентного пункта.", .65),
            T(17, 4, 2, "Полевая медицина", "Потери людей при повреждении частей и авиакрыльев −10%. Уничтожение соединения по-прежнему учитывает всех оставшихся людей.", .85, 16, 12),
            T(18, 4, 3, "Учебные госпитали", "Лимит пополнения наземных частей +20%; люди и ресурсы по-прежнему необходимы. Ремонтный модуль уровня 7 при промышленности 5.", 1.0, 17, 8),
            T(19, 4, 5, "Биомедицинские сети", "Скорость исследований +8%; годовой темп роста населения +0,03 процентного пункта.", 1.25, 18, 7),
            T(20, 5, 5, "Композитная защита", "Броня уровня 7. Для уровня 8 нужны интегрированная защита и военная фундаментальная наука 6. Уже выданная техника не меняется.", 1.0, 9),
            T(21, 5, 5, "Управление огнём", "Орудия уровня 7. Существующие машины сохраняют свои характеристики; новые модули требуют нового проекта и производства.", 1.0, 8),
            T(22, 5, 5, "Цифровая авионика", "Эффективность обеспеченных авиационных задач +8%; электроника уровня 7.", 1.1, 6, 10),
            T(23, 5, 6, "Интегрированная защита", "Броня, орудия и электроника уровня 8. Применяются через конструктор и производство.", 1.35, 20, 21, 22, 14)
        };
        private static readonly int[] Seven = { 20, 21, 10, 22, 9, 14, 13, 18 };
        private static readonly int[] Eight = { 23, 23, 11, 23, 11, 15, 15, 11 };
        public static bool Has(Nation n, int id) => n.ScienceTree != null && n.ScienceTree.Known[id];
        public static bool ValidProject(int project) => project >= 0 && project < 30;
        public static int Branch(int project) => project < 6 ? project : Catalog[project - 6].Branch;
        public static string Name(int project) => project < 0 ? "Программа не выбрана" : project < 6 ? Rules.Techs[project] + ": фундаментальная наука" : Catalog[project - 6].Name;
        public static string CurrentName(Nation n) => n.Research < 0 ? "Программа не выбрана" : Name(n.ScienceTree != null ? n.ScienceTree.ActiveProject : n.Research);
        public static int ModuleBranch(int slot) => slot == 2 || slot == 4 || slot == 7 ? 2 : slot == 5 || slot == 6 ? 3 : 5;
        public static int ModuleCap(Nation n, int slot)
        {
            int branch = ModuleBranch(slot), standard = Math.Min(8, 2 + n.Tech[branch]);
            if (n.ScienceTree == null) return standard;
            int unlocked = Has(n, Eight[slot]) ? 8 : Has(n, Seven[slot]) ? 7 : 6;
            return Math.Min(standard, Math.Max(Math.Min(8, 2 + n.ScienceTree.LegacyLevels[branch]), unlocked));
        }
        public static int ModuleTechnology(int slot, int level) => (level >= 8 ? Eight : Seven)[slot];
        public static string ModuleRequirement(int slot, int level)
        {
            return Rules.Techs[ModuleBranch(slot)] + " " + (level - 2) + (level >= 7 ? " и «" + Catalog[(level >= 8 ? Eight : Seven)[slot]].Name + "»" : "");
        }
        public static string Requirements(int project, int[] levels, bool[] known)
        {
            if (!ValidProject(project)) return "Неизвестная программа исследований.";
            if (project < 6) return levels[project] >= 10000 ? "Достигнут предел фундаментальных уровней." : null;
            var d = Catalog[project - 6];
            if (known[d.Id]) return "Технология уже освоена.";
            if (levels[d.Branch] < d.Foundation) return "Нужна фундаментальная наука: " + Rules.Techs[d.Branch] + " " + d.Foundation + ".";
            var missing = d.Prerequisites.Where(id => !known[id]).ToArray();
            return missing.Length == 0 ? null : "Сначала: " + string.Join(", ", missing.Select(id => Catalog[id].Name)) + ".";
        }
        public static TechnologyEffects Effects(Nation n)
        {
            var e = new TechnologyEffects(); if (n.ScienceTree == null) return e; var k = n.ScienceTree.Known;
            if (k[0]) e.Food += .05; if (k[1]) { e.Food += .08; e.FoodFuelInput = -.10; } if (k[2]) e.HouseholdFood = -.05; if (k[3]) e.Food += .10;
            if (k[4]) e.Electricity += .06; if (k[5]) e.Fuel += .08; if (k[6]) e.Electricity += .05; if (k[7]) { e.Electricity += .10; e.PowerInput = -.08; }
            if (k[8]) e.Equipment += .05; if (k[9]) e.Steel += .06; if (k[10]) e.Equipment += .08; if (k[11]) { e.Equipment += .05; e.EquipmentSteel = -.05; }
            if (k[12]) e.Construction += .08; if (k[13]) { e.GroundFuel = -.08; e.AirFuel = -.05; } if (k[14]) e.SupplyDays = 2; if (k[15]) { e.Construction += .08; e.Replacements += .20; }
            if (k[16]) { e.Intake += .05; e.PopulationGrowth += .0002; } if (k[17]) e.Casualties = -.10; if (k[18]) e.Replacements += .20;
            if (k[19]) { e.Research += .08; e.PopulationGrowth += .0003; } if (k[22]) e.AirOperations = .08;
            return e;
        }
        public static double GoodsFactor(TechnologyEffects e, int good) => 1 + (good == 0 ? e.Food : good == 1 ? e.Electricity : good == 2 ? e.Fuel : good == 4 ? e.Steel : 0);
        public static double InputFactor(TechnologyEffects e, int sector, int input) => (input == 1 ? 1 + e.PowerInput : 1) * (sector == 0 && input == 2 ? 1 + e.FoodFuelInput : 1);
    }
}
