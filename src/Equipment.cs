using System;
using System.Collections.Generic;
using System.Linq;
namespace Sovereign
{
    public class EquipmentDesign
    {
        public int Id;
        public Blueprint Blueprint = new Blueprint();
        public int Stock;
        public bool Retired;
    }
    public class ProductionLine
    {
        public int Id, DesignId;
        public int Factories = 1, Quantity = 1, Completed;
        public double Progress, Efficiency = 0.25;
        public bool Paused;
        public bool Active => !Paused && Completed < Quantity;
    }
    public class EquipmentAttachment
    {
        public int DesignId;
        public Blueprint Blueprint = new Blueprint();
        public double Condition = 100;
    }
    public static class EquipmentRules
    {
        public static readonly string[] Names = { "Танк", "БМП / БТР", "Грузовик", "Артиллерия", "Истребитель", "Бомбардировщик", "Разведчик", "Транспортник" };
        public static readonly string[] Roles = {
            "Броня и прорыв; основа соединения.", "Мобильное соединение с более лёгкой бронёй.",
            "Поддержка: снабжение и скорость марша.", "Огневая поддержка или основа соединения.",
            "Поддержка: защита от вражеской авиации.", "Поддержка: удар по наземным войскам.",
            "Поддержка: разведка и точность огня.", "Поддержка: снабжение на чужой территории."
        };
        public static readonly string[] ModuleNames = { "Броня", "Орудие / нагрузка", "Двигатель", "Электроника", "Корпус / планер", "Ходовая / аэродинамика", "Топливные баки", "Ремонтный модуль" };
        public static int[] Modules(Blueprint d) => new[] { d.Armor, d.Gun, d.Engine, d.Electronics, d.Frame, d.Mobility, d.FuelTank, d.Support };
        public static void SetModule(Blueprint d, int slot, int value)
        {
            switch (slot) {
                case 0: d.Armor = value; break; case 1: d.Gun = value; break;
                case 2: d.Engine = value; break; case 3: d.Electronics = value; break;
                case 4: d.Frame = value; break; case 5: d.Mobility = value; break;
                case 6: d.FuelTank = value; break; case 7: d.Support = value; break;
                default: throw new ArgumentOutOfRangeException("slot");
            }
        }
        public static int Cap(Blueprint d, int slot, Nation n)
        {
            int cap = TechnologyRules.ModuleCap(n, slot);
            if (slot == 0 && d.Air) cap = Math.Min(3, cap);
            if (slot == 0 && d.Kind == EquipmentKind.Truck) cap = Math.Min(2, cap);
            if (slot == 1 && (d.Kind == EquipmentKind.Truck || d.Kind == EquipmentKind.Transport || d.Kind == EquipmentKind.Recon)) cap = 1;
            return cap;
        }
        public static string Validate(Blueprint d, Nation n = null)
        {
            if (d == null || !Enum.IsDefined(typeof(EquipmentKind), d.Kind)) return "Неизвестный класс техники.";
            if (string.IsNullOrWhiteSpace(d.Name) || d.Name.Trim().Length > 40 || d.Name.Any(char.IsControl)) return "Название: от 1 до 40 печатных символов.";
            int[] values = Modules(d);
            if (values.Any(x => x < 1 || x > 8)) return "Уровни модулей должны быть от 1 до 8.";
            if (d.Air && d.Armor > 3) return "Планер допускает броню не выше 3.";
            if (d.Kind == EquipmentKind.Truck && d.Armor > 2) return "Грузовое шасси допускает броню не выше 2.";
            if ((d.Kind == EquipmentKind.Truck || d.Kind == EquipmentKind.Transport || d.Kind == EquipmentKind.Recon) && d.Gun != 1)
                return "Этому классу доступно только лёгкое оборонительное вооружение (1).";
            if (n != null) {
                if (d.Air && n.Tech[5] < 2) return "Авиации нужны военные технологии уровня 2.";
                for (int i = 0; i < values.Length; i++)
                    if (values[i] > Cap(d, i, n)) return ModuleNames[i] + ": требуется " + TechnologyRules.ModuleRequirement(i, values[i]) + ".";
            }
            return null;
        }
        public static Blueprint Balanced(EquipmentKind kind, Nation n, string name)
        {
            var d = new Blueprint { Kind = kind, Name = name };
            for (int i = 0; i < 8; i++) SetModule(d, i, Math.Max(1, Math.Min(4, Cap(d, i, n) - 1)));
            return d;
        }
        public static double Support(Army army, EquipmentKind kind)
        {
            return army.Attachments.Where(a => a.Blueprint.Kind == kind).Sum(a => {
                Blueprint d = a.Blueprint;
                double benefit;
                switch (kind) {
                    case EquipmentKind.Truck: benefit = 0.04 + d.Mobility * 0.015 + d.Engine * 0.006 + d.Support * 0.008; break;
                    case EquipmentKind.Transport: benefit = 0.05 + 0.2 * d.Range / (600 + d.Range) + d.Engine * 0.008; break;
                    case EquipmentKind.Recon: benefit = 0.04 + d.Electronics * 0.025 + d.Speed * 0.012; break;
                    case EquipmentKind.Fighter: benefit = 0.03 + d.Attack / 600 + d.Speed * 0.018 + d.Electronics * 0.008; break;
                    default: benefit = 0.03 + d.Attack / 650 + d.Penetration / 2500 + d.Electronics * 0.008; break;
                }
                return benefit * a.Condition / 100;
            });
        }
        public static double CombatPower(Army army, Army enemy, bool defending)
        {
            double armorFactor = Rules.Clamp(army.Design.Penetration / Math.Max(1, enemy.Design.Protection), 0.6, 1.15);
            double airStrike = Support(army, EquipmentKind.Bomber) * (1 - Math.Min(0.8, Support(enemy, EquipmentKind.Fighter) * 3));
            double fire = 1 + Support(army, EquipmentKind.Artillery) + Support(army, EquipmentKind.Recon) + airStrike;
            return army.Strength * (defending ? army.Design.Defense : army.Design.Attack * armorFactor) / 32
                * (0.3 + army.Supply / 100) * fire * army.Battalions * ManpowerRules.Manning(army) * (.35 + .65 * army.Organization / 100);
        }
    }
}
