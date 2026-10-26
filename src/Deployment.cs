using System;
namespace Sovereign
{
    public enum FormationSpecialty { Combined, Assault, Mobile, AntiArmor, FireSupport, Entrenched }
    public class BattleDeployment
    {
        public int Front = 1, Left, Right, Reserve;
        public FormationSpecialty Specialty;
        public BattleDeployment Copy() => (BattleDeployment)MemberwiseClone();
    }
    public readonly struct DeploymentView
    {
        public readonly int Front, Left, Right, Reserve;
        public readonly FormationSpecialty Specialty;
        public DeploymentView(BattleDeployment d, int count)
        {
            Front = d == null ? count : d.Front; Left = d == null ? 0 : d.Left; Right = d == null ? 0 : d.Right;
            Reserve = d == null ? 0 : d.Reserve; Specialty = d == null ? FormationSpecialty.Combined : d.Specialty;
        }
    }
    public static class DeploymentRules
    {
        public static readonly string[] Names = { "Общевойсковая", "Штурмовая", "Манёвренная", "Противотанковая", "Огневая поддержка", "Оборонительная" };
        public static readonly string[] Descriptions = { "Сбалансированный бой без специализации.", "Атака +14%, оборона −10%; риск при обходе.", "Обход +35%; фронтальная оборона −8%.", "Против брони +18%; против лёгких частей −8%.", "Сила приданной артиллерии +40%; без неё атака −8%.", "Оборона +16%, атака −12%." };
        public static BattleDeployment Default(int count) => new BattleDeployment { Front = count };
        public static bool Valid(BattleDeployment d, int count) => d == null || (Enum.IsDefined(typeof(FormationSpecialty), d.Specialty) && d.Front >= 1 && d.Left >= 0 && d.Right >= 0 && d.Reserve >= 0 && d.Front <= 6 && d.Left <= 6 && d.Right <= 6 && d.Reserve <= 6 && d.Front + d.Left + d.Right + d.Reserve == count);
        public static bool Same(BattleDeployment a, BattleDeployment b, int count) { a = a ?? Default(count); b = b ?? Default(count); return a.Front == b.Front && a.Left == b.Left && a.Right == b.Right && a.Reserve == b.Reserve && a.Specialty == b.Specialty; }
        public static DeploymentView For(Army a) => new DeploymentView(a.Deployment, a.Battalions);
        public static double Factor(Army a, Army enemy, bool defending)
        {
            var d = For(a); var other = For(enemy);
            double flank = Math.Max(0, d.Left - other.Right) + Math.Max(0, d.Right - other.Left);
            double exposed = Math.Max(0, other.Left - d.Right) + Math.Max(0, other.Right - d.Left);
            // Reserves cover threatened flanks; committing troops to manoeuvre thins the front.
            exposed = Math.Max(0, exposed - d.Reserve);
            double front = (d.Front + .85 * (d.Left + d.Right) + (defending ? .95 : .55) * d.Reserve) / a.Battalions;
            double maneuver = Math.Min(.25, flank / Math.Max(1, other.Front) * .10) * (d.Specialty == FormationSpecialty.Mobile ? 1.35 : 1);
            double value = front * (1 + maneuver) * (1 - Math.Min(.25, exposed / Math.Max(1, d.Front) * .1));
            switch (d.Specialty) {
                case FormationSpecialty.Assault: value *= defending ? .90 : 1.14; break;
                case FormationSpecialty.Mobile: if (defending) value *= .92; break;
                case FormationSpecialty.AntiArmor: value *= enemy.Design.Kind == EquipmentKind.Tank ? 1.18 : .92; break;
                case FormationSpecialty.FireSupport: value *= a.Attachments.Exists(s => s.Blueprint.Kind == EquipmentKind.Artillery) ? 1 + EquipmentRules.Support(a, EquipmentKind.Artillery) * .4 : defending ? 1 : .92; break;
                case FormationSpecialty.Entrenched: value *= defending ? 1.16 : .88; break;
            }
            // Experience is earned in combat, never inferred from the equipment silhouette.
            return value * (1 + Math.Min(100, Math.Max(0, a.Experience)) * .0015);
        }
        public static int Stars(Army a)
        {
            if (a.TrainingDays > 0 || a.Personnel == 0) return 0;
            double quality = (30 + Math.Min(100, Math.Max(0, a.Experience)) * .7) * Math.Min(a.Strength, a.Organization) / 100 * ManpowerRules.Manning(a) * (.5 + .5 * a.Supply / 100);
            return Math.Max(0, Math.Min(5, (int)Math.Floor(quality / 20)));
        }
    }
}
