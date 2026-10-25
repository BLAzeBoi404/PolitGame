using System;
using System.Collections.Generic;

namespace Sovereign
{
    public class DoctrineState
    {
        public int[] Levels = new int[6];
        public int[] Active = { -1, -1 };
        public int[] ChangedDay = { -180, -180 };
        public int Training = -1, StartedDay = -1;
        public double Work, Paid, QuotedGDP;
        public bool Paused;
    }
    public struct DoctrineEffects
    {
        public double Attack, Defense, Speed, Fuel, Maintenance, SupplyDays, Organization, Casualties;
        public double AirCover, AirStrike, AirRecon, AirSupply, AirFuel, AirMaintenance;
        public void Add(DoctrineEffects e)
        {
            Attack += e.Attack; Defense += e.Defense; Speed += e.Speed; Fuel += e.Fuel; Maintenance += e.Maintenance;
            SupplyDays += e.SupplyDays; Organization += e.Organization; Casualties += e.Casualties;
            AirCover += e.AirCover; AirStrike += e.AirStrike; AirRecon += e.AirRecon; AirSupply += e.AirSupply; AirFuel += e.AirFuel; AirMaintenance += e.AirMaintenance;
        }
    }
    public class DoctrineStage
    {
        public int Id, Foundation;
        public string Name;
        public int[] Technologies;
        public DoctrineEffects Effects;
        public int Path => Id / 3;
        public int Tier => Id % 3;
        public int Duration => 90 + Tier * 60;
    }
    public static class DoctrineRules
    {
        public const int AdaptationDays = 30, ChangeCooldown = 180;
        public static readonly string[] Names = { "Манёвренная война", "Огневое превосходство", "Глубокая оборона", "Господство в воздухе", "Армейская авиация", "Воздушная логистика" };
        public static readonly string[] Descriptions = {
            "Темп наступления и быстрые переходы. Повышенный расход топлива требует надёжного снабжения.",
            "Сосредоточение огня и подготовленные удары. Содержание дороже, марши медленнее.",
            "Удержание позиций, автономность и сохранение людей. Наступательный темп ниже.",
            "Истребительное прикрытие и борьба за воздух. Удары слабее, топлива требуется больше.",
            "Разведка и поддержка сухопутных атак. Усиленные ударные задачи ослабляют воздушное прикрытие.",
            "Снабжение войск с воздуха и экономичные вылеты. Истребительное прикрытие слабее."
        };
        private static DoctrineStage D(int id, string name, DoctrineEffects effects, params int[] technologies)
            => new DoctrineStage { Id = id, Name = name, Foundation = id % 3 == 0 ? 2 : id % 3 == 1 ? 4 : 6, Effects = effects, Technologies = technologies };
        public static readonly DoctrineStage[] Stages = {
            D(0, "Подвижные группы", new DoctrineEffects { Attack = .06, Speed = .08, Fuel = .08 }),
            D(1, "Оперативный манёвр", new DoctrineEffects { Attack = .04, Speed = .04, Organization = .10, Fuel = .04 }, 64, 66),
            D(2, "Развитие прорыва", new DoctrineEffects { Attack = .06, Speed = .06, SupplyDays = .5, Fuel = .03 }, 70, 13),
            D(3, "Огневая подготовка", new DoctrineEffects { Attack = .10, Maintenance = .08, Speed = -.04 }),
            D(4, "Согласованные батареи", new DoctrineEffects { Attack = .07, Defense = .04, Maintenance = .06 }, 65, 9),
            D(5, "Система огневого поражения", new DoctrineEffects { Attack = .08, Defense = .05, Maintenance = .06, Speed = -.03 }, 69),
            D(6, "Эшелонированные позиции", new DoctrineEffects { Defense = .10, SupplyDays = 1, Speed = -.04 }),
            D(7, "Сохранение боеспособности", new DoctrineEffects { Defense = .07, Casualties = -.05, Attack = -.03 }, 14, 17),
            D(8, "Устойчивая оборона", new DoctrineEffects { Defense = .08, Fuel = -.06, Organization = .10, Speed = -.04 }, 23, 60),
            D(9, "Истребительное прикрытие", new DoctrineEffects { AirCover = .12, AirStrike = -.05, AirFuel = .05 }),
            D(10, "Согласованный перехват", new DoctrineEffects { AirCover = .08, AirRecon = .05 }, 68),
            D(11, "Контроль воздушного пространства", new DoctrineEffects { AirCover = .10, AirFuel = .05 }, 22, 67),
            D(12, "Непосредственная поддержка", new DoctrineEffects { AirStrike = .10, AirRecon = .05, AirFuel = .06 }),
            D(13, "Передовые авианаводчики", new DoctrineEffects { AirStrike = .08, AirCover = -.05 }, 64, 67),
            D(14, "Совместная ударная операция", new DoctrineEffects { AirStrike = .08, AirRecon = .05, AirFuel = .04, AirCover = -.05 }, 69, 70),
            D(15, "Транспортное планирование", new DoctrineEffects { AirSupply = .15, AirFuel = -.04, AirCover = -.05 }),
            D(16, "Распределённые маршруты", new DoctrineEffects { AirSupply = .10, AirRecon = .08, AirMaintenance = -.04 }, 53, 14),
            D(17, "Воздушный мост", new DoctrineEffects { AirSupply = .10, AirFuel = -.04, AirCover = -.05 }, 55, 22)
        };
        private static readonly DoctrineEffects[,] accumulated = new DoctrineEffects[6, 4];
        static DoctrineRules()
        {
            for (int path = 0; path < 6; path++) for (int level = 1; level <= 3; level++) {
                var effects = accumulated[path, level - 1]; effects.Add(Stages[path * 3 + level - 1].Effects); accumulated[path, level] = effects;
            }
        }
        public static bool Valid(int project) => project >= 0 && project < Stages.Length;
        public static int Service(int path) => path / 3;
        public static double TrainingCost(double gdp, int tier) => Math.Max(.12, gdp * .001) * (tier + 1);
        public static double AdoptionCost(Nation n, int path) => Math.Max(.05, n.GDP * (n.Doctrines.Active[Service(path)] < 0 ? .0005 : .001));
        public static DoctrineEffects Cumulative(int path, int level) => accumulated[path, level];
        public static DoctrineEffects Effects(Nation n, int day)
        {
            var effects = new DoctrineEffects(); var state = n.Doctrines; if (state == null) return effects;
            for (int service = 0; service < 2; service++) if (state.Active[service] >= 0 && day >= state.ChangedDay[service] + AdaptationDays)
                effects.Add(accumulated[state.Active[service], state.Levels[state.Active[service]]]);
            return effects;
        }
        public static string Prerequisite(Nation n, int project)
        {
            if (!Valid(project)) return "Неизвестная ступень доктрины.";
            var stage = Stages[project];
            if (n.Tech[5] < stage.Foundation) return "Нужны военные технологии уровня " + stage.Foundation + ".";
            foreach (int id in stage.Technologies) if (!TechnologyRules.Has(n, id)) return "Нужна технология: " + TechnologyRules.Catalog[id].Name + ".";
            return null;
        }
        public static string Summary(DoctrineEffects e)
        {
            var result = new List<string>();
            Action<string, double> add = (name, value) => { if (Math.Abs(value) > 1e-9) result.Add(name + " " + (value * 100).ToString("+0.#;−0.#;0") + "%"); };
            add("Атака", e.Attack); add("Оборона", e.Defense); add("Скорость новых переходов", e.Speed); add("Наземное топливо", e.Fuel); add("Содержание частей", e.Maintenance);
            if (e.SupplyDays > 0) result.Add("Автономность +" + e.SupplyDays.ToString("0.#") + " дн.");
            add("Восстановление организации", e.Organization); add("Людские потери частей", e.Casualties);
            add("Воздушное прикрытие", e.AirCover); add("Удары авиации", e.AirStrike); add("Разведка", e.AirRecon); add("Воздушное снабжение", e.AirSupply); add("Топливо вылетов", e.AirFuel); add("Содержание крыльев", e.AirMaintenance);
            return result.Count == 0 ? "Без доктринальных изменений" : string.Join(" · ", result);
        }
    }
}
