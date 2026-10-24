using System;
namespace Sovereign
{
    public enum EquipmentKind { Tank, IFV, Truck, Artillery, Fighter, Bomber, Recon, Transport }
    // A blueprint is a value: adoption, production and deployment always copy it.
    public class Blueprint
    {
        public string Name = "Стандарт-1";
        public EquipmentKind Kind;
        public int Armor = 2, Gun = 2, Engine = 2, Electronics = 1;
        public int Frame = 1, Mobility = 1, FuelTank = 1, Support = 1;
        public bool Air => Kind >= EquipmentKind.Fighter;
        public bool Formation => Kind == EquipmentKind.Tank || Kind == EquipmentKind.IFV || Kind == EquipmentKind.Artillery;
        public double BaseWeight => (new double[] { 18, 10, 4, 12, 4, 12, 3, 16 })[(int)Kind];
        public double Weight => BaseWeight + Armor * (Air ? 0.35 : 2.1) + Gun * (Air ? 0.4 : 1.1)
            + Engine * 0.45 + Electronics * 0.12 + Frame * 0.6 + Mobility * 0.3 + FuelTank * 0.45 + Support * 0.25;
        public double Protection => Armor * (Air ? 6 : 22) * (1 + Frame * 0.04);
        public double Penetration => Gun * (Kind == EquipmentKind.Artillery ? 17 : 26) * (1 + Electronics * 0.04);
        public double Attack => (Formation ? 32 : (Kind == EquipmentKind.Bomber ? 36 : (Kind == EquipmentKind.Fighter ? 24 : 4)))
            * (0.65 + Gun * 0.22) * (1 + Electronics * 0.07);
        public double Defense => 30 * (1 + Protection / 110) * (1 + Electronics * 0.035);
        public double Power => Math.Sqrt(Attack * Defense) / 32;
        public double Speed => Rules.Clamp(Math.Sqrt((BaseWeight + 15) / Weight) * (0.65 + Engine * 0.14)
            * (1 + Mobility * 0.045) * (Kind == EquipmentKind.Truck ? 1.3 : 1), 0.35, 3);
        public double SpeedKph => Speed * (Air ? 360 : 40);
        public double Fuel => 0.025 * (Weight / 25) * (0.7 + Engine * 0.17) * (Air ? 2.3 : 1);
        public double Range => (Air ? 450 : 140) * (1 + FuelTank * 0.4) / (Fuel / (Air ? 0.07 : 0.04));
        public double Reliability => Rules.Clamp(96 - Armor * 1.5 - Gun * 2 - Electronics * 1.8
            - Engine * 0.8 + Support * 2.5 + Mobility * 0.9, 40, 99);
        public double Cost => (0.025 + Weight * 0.0025 + Gun * 0.008 + Engine * 0.006
            + Electronics * 0.014 + Frame * 0.006 + Mobility * 0.004 + Support * 0.005) * (Air ? 1.5 : 1);
        public double Maintenance => Cost * (1 + (100 - Reliability) / 50) / 1500;
        public double WorkDays => (8 + Weight * 0.45 + Gun + Electronics * 2 + Support) * (Air ? 1.3 : 1);
        public double Steel => Weight * 0.045;
        public double Machines => 0.08 + Engine * 0.025 + Electronics * 0.04;
        public double Arms => 0.15 + Gun * 0.09;
        public Blueprint Copy() => (Blueprint)MemberwiseClone();
    }
}
