using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        private readonly Dictionary<long, double> airRecon = new Dictionary<long, double>();
        public static readonly string[] AirMissionNames = { "Резерв / ремонт", "Превосходство", "Удар по войскам", "Разведка", "Воздушное снабжение" };
        public void InitializeAviation(Campaign campaign)
        {
            // Old saves receive no free aircraft or airbases.
            campaign.AirWings = new List<AirWing>(); campaign.NextAirWing = 1;
            foreach (var province in campaign.Provinces) province.Airfield = 0;
            campaign.Version = 4;
        }
        public double AirDistance(int from, int to)
        {
            var a = World.Regions[from]; var b = World.Regions[to];
            double lat1 = -a.Y * Math.PI / 180, lat2 = -b.Y * Math.PI / 180;
            double dlat = lat2 - lat1, dlon = (b.X - a.X) * Math.PI / 180;
            double h = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dlon / 2) * Math.Sin(dlon / 2);
            return 6371 * 2 * Math.Asin(Math.Sqrt(Rules.Clamp(h, 0, 1)));
        }
        public int AirCapacity(int region) => S.Provinces[region].Airfield * 2;
        public bool HasAirSpace(int owner, int region, int exclude = -1) => region >= 0 && region < S.Provinces.Length
            && S.Provinces[region].Owner == owner && AirCapacity(region) > S.AirWings.Count(a => a.BaseRegion == region && a.Id != exclude);
        public string DeployAirWing(int designId, int region, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            var n = S.Nations[owner]; var design = FindDesign(owner, designId);
            if (design == null || !design.Blueprint.Air || design.Retired || design.Stock <= 0) return "Нужен готовый комплект самолётов действующего проекта.";
            if (!HasAirSpace(owner, region)) return "Нужен свой аэродром со свободным местом: два крыла на уровень.";
            if (S.AirWings.Count(a => a.Owner == owner) >= 48) return "Достигнут предел: 48 авиакрыльев.";
            int crew = ManpowerRules.AirSize(design.Blueprint.Kind);
            if (n.Recruits < crew) return "Для авиакрыла нужно людей из резерва: " + crew + ".";
            if (n.Treasury < .15) return "Подготовка авиакрыла стоит 0,15 млрд.";
            n.Treasury -= .15; design.Stock--; n.Recruits -= crew;
            S.AirWings.Add(new AirWing { Id = S.NextAirWing++, Owner = owner, BaseRegion = region, DesignId = design.Id, Design = design.Blueprint.Copy(), Personnel = crew });
            if (owner == S.Player) Notify("Авиакрыло сформировано: " + design.Blueprint.Name + ". Подготовка 21 день.", 1);
            Raise(); return null;
        }
        public bool AirMissionCompatible(Blueprint design, AirMission mission)
        {
            return mission == AirMission.Reserve || (mission == AirMission.Superiority && design.Kind == EquipmentKind.Fighter)
                || (mission == AirMission.Strike && design.Kind == EquipmentKind.Bomber)
                || (mission == AirMission.Recon && design.Kind == EquipmentKind.Recon)
                || (mission == AirMission.Supply && design.Kind == EquipmentKind.Transport);
        }
        public string AirTargetReason(AirWing wing, AirMission mission, int target)
        {
            if (!Enum.IsDefined(typeof(AirMission), mission) || !AirMissionCompatible(wing.Design, mission)) return "Эта задача не соответствует классу самолёта.";
            if (mission == AirMission.Reserve) return null;
            if (target < 0 || target >= S.Provinces.Length) return "Выберите целевую область на карте.";
            if (AirDistance(wing.BaseRegion, target) > wing.Design.Range) return "Цель вне боевого радиуса: " + wing.Design.Range.ToString("0") + " км.";
            int owner = S.Provinces[target].Owner;
            bool enemy = AtWar(wing.Owner, owner) || S.Armies.Any(a => a.Region == target && AtWar(wing.Owner, a.Owner));
            if ((mission == AirMission.Strike || mission == AirMission.Recon) && !enemy) return "Для этой задачи требуется военная цель.";
            if (mission == AirMission.Superiority && !enemy && !HasAccess(wing.Owner, owner)) return "Нейтральное воздушное пространство закрыто.";
            if (mission == AirMission.Supply && !S.Armies.Any(a => a.Owner == wing.Owner && a.Region == target)) return "В области нет своих частей для снабжения.";
            return null;
        }
        public string SetAirMission(int id, AirMission mission, int target, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            var wing = S.AirWings.FirstOrDefault(a => a.Id == id && a.Owner == owner);
            if (wing == null) return "Авиакрыло не найдено.";
            string reason = AirTargetReason(wing, mission, target); if (reason != null) return reason;
            wing.Mission = mission; wing.Target = mission == AirMission.Reserve ? -1 : target;
            Raise(); return null;
        }
        public string RebaseAirWing(int id, int region)
        {
            var wing = S.AirWings.FirstOrDefault(a => a.Id == id && a.Owner == S.Player);
            if (wing == null || !HasAirSpace(S.Player, region, id)) return "Выберите свой аэродром со свободным местом.";
            if (wing.BaseRegion == region) return "Крыло уже на этом аэродроме.";
            if (AirDistance(wing.BaseRegion, region) > wing.Design.Range * 2) return "Аэродром вне перегоночной дальности (два боевых радиуса).";
            if (wing.TrainingDays > 0) return "Сначала завершите подготовку.";
            if (Player.Stock[2] < wing.Design.Fuel) return "Недостаточно топлива для перебазирования.";
            Player.Stock[2] -= wing.Design.Fuel; wing.BaseRegion = region; wing.TrainingDays = 3; wing.Mission = AirMission.Reserve; wing.Target = -1;
            Raise(); return null;
        }
        public string AirStatus(AirWing wing)
        {
            if (S.Provinces[wing.BaseRegion].Owner != wing.Owner) return "Аэродром потерян: эвакуация в следующий день";
            if (wing.TrainingDays > 0) return "Подготовка: " + wing.TrainingDays + " дн.";
            if (wing.Mission == AirMission.Reserve) return "Резерв / ремонт за оружие и деньги";
            string reason = AirTargetReason(wing, wing.Mission, wing.Target); if (reason != null) return reason;
            if (ManpowerRules.AirManning(wing) < .5) return "Экипаж укомплектован менее чем на 50%: требуется пополнение в резерве";
            if (wing.Strength < 20) return "Готовность ниже 20%: отправьте на ремонт";
            if (S.Nations[wing.Owner].Treasury < AirUpkeep(wing)) return "Нет средств на обслуживание";
            if (S.Nations[wing.Owner].Stock[2] < AirFuel(wing)) return "Недостаточно топлива для вылета";
            if (wing.Mission == AirMission.Supply && (S.Nations[wing.Owner].Stock[0] < .02 || S.Nations[wing.Owner].Stock[7] < .015)) return "Нет продовольствия или оружия для груза";
            return "Вылеты: " + AirMissionNames[(int)wing.Mission];
        }
        private double AirFuel(AirWing wing) => wing.Design.Fuel * Math.Max(.2, wing.Strength / 100) * (1 + TechnologyRules.Effects(S.Nations[wing.Owner]).AirFuel) * (1 + DoctrineRules.Effects(S.Nations[wing.Owner], S.Day).AirFuel);
        private long AirKey(int owner, int region) => (long)owner * S.Provinces.Length + region;
        public double AirReconBonus(int owner, int region) { double value; return airRecon.TryGetValue(AirKey(owner, region), out value) ? Math.Min(.15, value) : 0; }
        public void TickAviation()
        {
            airRecon.Clear();
            if (S.AirWings.Count == 0) return;
            var flying = new List<AirWing>();
            foreach (var wing in S.AirWings.ToList()) {
                var n = S.Nations[wing.Owner];
                if (S.Provinces[wing.BaseRegion].Owner != wing.Owner) {
                    int destination = Enumerable.Range(0, S.Provinces.Length).Where(r => HasAirSpace(wing.Owner, r, wing.Id) && AirDistance(wing.BaseRegion, r) <= wing.Design.Range * 2)
                        .OrderBy(r => AirDistance(wing.BaseRegion, r)).DefaultIfEmpty(-1).First();
                    if (destination < 0 || n.Stock[2] < wing.Design.Fuel) {
                        DestroyAirWing(wing); if (wing.Owner == S.Player) Notify("Потеряно авиакрыло №" + wing.Id + ": эвакуация с захваченного аэродрома невозможна.", 2); continue;
                    }
                    n.Stock[2] -= wing.Design.Fuel; wing.BaseRegion = destination; DamageAirWing(wing, 20, 1); wing.TrainingDays = Math.Max(7, wing.TrainingDays);
                    wing.Mission = AirMission.Reserve; wing.Target = -1;
                    if (wing.Owner == S.Player) Notify("Авиакрыло №" + wing.Id + " эвакуировано в " + World.Regions[destination].Name, 2);
                }
                bool funded = n.Treasury >= AirUpkeep(wing);
                if (funded) n.Treasury -= AirUpkeep(wing);
                if (wing.TrainingDays > 0) { if (funded) wing.TrainingDays--; continue; }
                if (wing.Mission == AirMission.Reserve) {
                    double repair = Math.Min(1, 100 - wing.Strength), cost = wing.Design.Cost * repair / 100, arms = wing.Design.Arms * repair / 100;
                    if (funded && n.Treasury >= cost && n.Stock[7] >= arms) { n.Treasury -= cost; n.Stock[7] -= arms; wing.Strength += repair; }
                    continue;
                }
                if (AirTargetReason(wing, wing.Mission, wing.Target) != null || wing.Strength < 20 || ManpowerRules.AirManning(wing) < .5 || !funded || n.Stock[2] < AirFuel(wing)) continue;
                if (wing.Mission == AirMission.Supply) {
                    if (n.Stock[0] < .02 || n.Stock[7] < .015) continue;
                    n.Stock[0] -= .02; n.Stock[7] -= .015;
                }
                n.Stock[2] -= AirFuel(wing); flying.Add(wing); wing.Sorties++;
            }
            // Resolve air cover from the same pre-combat snapshot for both sides.
            var cover = new Dictionary<int, Dictionary<int, double>>();
            foreach (var wing in flying.Where(a => a.Mission == AirMission.Superiority)) {
                double power = wing.Design.Attack * wing.Strength / 100 * ManpowerRules.AirManning(wing) * (wing.Design.SpeedKph / 360) * (1 + wing.Design.Electronics * .03) * (1 + TechnologyRules.Effects(S.Nations[wing.Owner]).AirOperations) * AirDoctrineFactor(wing.Owner, AirMission.Superiority);
                foreach (int region in World.Regions[wing.Target].Neighbors.Concat(new[] { wing.Target }).Distinct()) {
                    if (AirDistance(wing.BaseRegion, region) > wing.Design.Range) continue;
                    Dictionary<int, double> owners; if (!cover.TryGetValue(region, out owners)) cover[region] = owners = new Dictionary<int, double>();
                    double prior; owners.TryGetValue(wing.Owner, out prior); owners[wing.Owner] = prior + power;
                }
            }
            var units = S.Armies.GroupBy(a => a.Region).ToDictionary(a => a.Key, a => a.ToList());
            foreach (var wing in flying) {
                Dictionary<int, double> owners; cover.TryGetValue(wing.Target, out owners);
                double enemy = owners == null ? 0 : owners.Where(p => AtWar(wing.Owner, p.Key)).Sum(p => p.Value);
                double escort = owners == null ? 0 : owners.Where(p => p.Key == wing.Owner || Allied(wing.Owner, p.Key)).Sum(p => p.Value);
                double survival = 1 / (1 + enemy / (12 + escort));
                double effect = wing.Strength / 100 * ManpowerRules.AirManning(wing) * survival * (1 + TechnologyRules.Effects(S.Nations[wing.Owner]).AirOperations) * AirDoctrineFactor(wing.Owner, wing.Mission);
                double loss = (100 - wing.Design.Reliability) * .003 + Math.Min(6, enemy / (12 + escort) * 2) / (1 + wing.Design.Protection / 80);
                DamageAirWing(wing, loss);
                List<Army> targets; units.TryGetValue(wing.Target, out targets);
                if (wing.Mission == AirMission.Strike && targets != null) {
                    var enemies = targets.Where(a => AtWar(wing.Owner, a.Owner)).ToList();
                    int targetBattalions = Math.Max(1, enemies.Sum(a => a.Battalions));
                    foreach (var a in enemies) DamageArmy(a, Math.Min(4, wing.Design.Attack / 25) * effect / targetBattalions, .3, 8.1);
                } else if (wing.Mission == AirMission.Supply && targets != null) {
                    var friends = targets.Where(a => a.Owner == wing.Owner).ToList();
                    int suppliedBattalions = Math.Max(1, friends.Sum(a => a.Battalions));
                    foreach (var a in friends) { a.Supply = Math.Min(100, a.Supply + 12 * effect / suppliedBattalions); a.SupplyDays = Math.Max(a.SupplyDays, effect * 2 / suppliedBattalions); }
                } else if (wing.Mission == AirMission.Recon) {
                    long key = AirKey(wing.Owner, wing.Target); double prior; airRecon.TryGetValue(key, out prior);
                    airRecon[key] = Math.Min(.15, prior + effect * (.04 + wing.Design.Electronics * .01));
                }
            }
            foreach (var lost in S.AirWings.Where(a => a.Strength <= 0).ToList()) {
                DestroyAirWing(lost); if (lost.Owner == S.Player) Notify("Авиакрыло №" + lost.Id + " потеряно в воздушной операции.", 2);
            }
        }
        internal void PlanAviation()
        {
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                if (owner == S.Player || !Enumerable.Range(0, S.Nations.Length).Any(c => AtWar(owner, c))) continue;
                var n = S.Nations[owner]; if (n.Treasury < 2 || FactoryCapacity(owner) < 2) continue;
                int region = Enumerable.Range(0, S.Provinces.Length).Where(r => S.Provinces[r].Owner == owner).OrderByDescending(r => S.Provinces[r].Population).DefaultIfEmpty(-1).First();
                if (region < 0) continue;
                if (S.Provinces[region].Airfield == 0 && !S.Projects.Any(p => p.Region == region && p.Sector == 9)) BuildAirfield(region, owner);
                foreach (var kind in new[] { EquipmentKind.Fighter, EquipmentKind.Bomber }) {
                    var wing = S.AirWings.FirstOrDefault(a => a.Owner == owner && a.Design.Kind == kind);
                    if (wing == null) {
                        var design = n.Designs.FirstOrDefault(d => !d.Retired && d.Blueprint.Kind == kind);
                        if (design == null && n.Designs.Count < 128) {
                            var blueprint = EquipmentRules.Balanced(kind, n, "Авиация " + kind + " " + n.NextDesign);
                            int previous = n.ActiveDesign; Blueprint old = n.Design.Copy();
                            if (AdoptDesign(blueprint, owner) == null) design = FindDesign(owner, n.ActiveDesign);
                            n.ActiveDesign = previous; n.Design = old;
                        }
                        if (design == null) continue;
                        if (design.Stock > 0) DeployAirWing(design.Id, region, owner);
                        else if (!n.Lines.Any(l => l.DesignId == design.Id && l.Completed < l.Quantity) && AssignedFactories(owner) < FactoryCapacity(owner)) StartProduction(design.Id, 1, 1, owner);
                        continue;
                    }
                    if (wing.Strength < 45) { SetAirMission(wing.Id, AirMission.Reserve, -1, owner); continue; }
                    var mission = kind == EquipmentKind.Fighter ? AirMission.Superiority : AirMission.Strike;
                    int target = S.Armies.Where(a => AtWar(owner, a.Owner)).Select(a => a.Region).Distinct().Where(r => AirTargetReason(wing, mission, r) == null).OrderBy(r => AirDistance(wing.BaseRegion, r)).DefaultIfEmpty(-1).First();
                    if (target >= 0) SetAirMission(wing.Id, mission, target, owner);
                }
            }
        }
        public string BuildAirfield(int region, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (region < 0 || region >= S.Provinces.Length || S.Provinces[region].Owner != owner) return "Аэродром строится в своей области.";
            var p = S.Provinces[region];
            if (p.Airfield >= 3 || S.Projects.Any(c => c.Region == region && c.Sector == 9)) return "Аэродром уже строится или достиг третьего уровня.";
            if (S.Projects.Count(c => c.Region == region) >= 4) return "В регионе уже четыре стройки.";
            if (S.Nations[owner].Treasury < .6) return "Аэродром стоит 0,60 млрд.";
            S.Nations[owner].Treasury -= .6;
            int days = Math.Max(1, (int)(120 / ((1 + S.Nations[owner].Tech[3] * .06) * (1 + TechnologyRules.Effects(S.Nations[owner]).Construction))));
            S.Projects.Add(new Construction { Owner = owner, Region = region, Sector = 9, Days = days, Total = days });
            if (owner == S.Player) Notify("Строится аэродром: " + World.Regions[region].Name + ". " + days + " дн.");
            Raise(); return null;
        }
    }
}
