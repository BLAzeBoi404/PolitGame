using System;
using System.Collections.Generic;

namespace Sovereign
{
    public enum ConscriptionLaw { Volunteers, Limited, Extensive, Total }
    public class FormationTemplate
    {
        public int Id;
        public string Name;
        public int Battalions = 1;
    }
    public static class ManpowerRules
    {
        public static readonly string[] LawNames = { "Добровольная служба", "Ограниченный призыв", "Расширенный призыв", "Всеобщая мобилизация" };
        public static readonly double[] PopulationShare = { .005, .02, .05, .10 };
        public static readonly double[] OutputPenalty = { 0, .03, .08, .16 };
        public const double ReplacementCost = .000003;
        public const double ReplacementArms = .0002;
        public static List<FormationTemplate> Defaults() => new List<FormationTemplate> {
            new FormationTemplate { Id = 1, Name = "Боевая группа", Battalions = 1 },
            new FormationTemplate { Id = 2, Name = "Бригада", Battalions = 2 },
            new FormationTemplate { Id = 3, Name = "Дивизия", Battalions = 4 }
        };
        public static int GroundSize(EquipmentKind kind, int battalions, int supports = 0) => 200 + battalions * (kind == EquipmentKind.IFV ? 900 : kind == EquipmentKind.Artillery ? 500 : 600) + supports * 100;
        public static int Establishment(Army army) => GroundSize(army.Design.Kind, army.Battalions, army.Attachments.Count);
        public static int AirSize(EquipmentKind kind) => kind == EquipmentKind.Bomber ? 220 : kind == EquipmentKind.Transport ? 200 : kind == EquipmentKind.Recon ? 80 : 120;
        public static double Manning(Army army) => Math.Min(1, (double)army.Personnel / Establishment(army));
        public static double AirManning(AirWing wing) => Math.Min(1, (double)wing.Personnel / AirSize(wing.Design.Kind));
        public static double Output(Nation n) => 1 - OutputPenalty[(int)n.Conscription];
        public static int TrainingDays(int battalions) => 30 + 6 * (battalions - 1);
    }
}
