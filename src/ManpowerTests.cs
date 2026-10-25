using System;
using System.IO;
using System.Linq;

namespace Sovereign
{
    internal static class ManpowerTests
    {
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            Func<Engine, bool> conserved = e => Enumerable.Range(0, e.S.Nations.Length).All(c => {
                var n = e.S.Nations[c]; return n.Mobilized - n.Discharged == n.Recruits + e.ServicePersonnel(c) + n.PersonnelLosses;
            });
            Action<Nation, int> reserve = (n, value) => { n.Mobilized += value - n.Recruits; n.Recruits = value; };
            Func<Engine, int> home = e => world.Countries[e.S.Player].Regions.First(r => e.SupplyNetwork()[r]);
            scenario("Personnel accounting and recruitment", () => {
                var e = fresh(); int region = home(e); var design = e.Player.Designs[0]; design.Stock = 10; e.Player.Treasury = 100;
                check(conserved(e) && e.Player.Recruits > 0, "New campaign initializes a conserved reserve, service and loss ledger");
                e.SelectTemplate(3); int kits = design.Stock, people = e.Player.Recruits; double money = e.Player.Treasury;
                reserve(e.Player, 0); var snapshot = e.S.Armies.Count;
                check(e.Recruit(region) != null && e.S.Armies.Count == snapshot && design.Stock == kits && e.Player.Treasury == money, "Recruitment without personnel preserves money, equipment and armies");
                reserve(e.Player, people); design.Stock = 3;
                check(e.Recruit(region) != null && e.Player.Recruits == people && e.Player.Treasury == money, "Large formation cannot be recruited with fewer kits than battalions");
                design.Stock = kits; e.Player.Treasury = 0;
                check(e.Recruit(region) != null && design.Stock == kits && e.Player.Recruits == people, "Recruitment without money does not consume people or equipment");
                e.Player.Treasury = money;
                check(e.Recruit(region) == null && design.Stock == kits - 4 && e.Player.Recruits == people - 2600 && Math.Abs(e.Player.Treasury - (money - 1.2)) < 1e-8, "Four-battalion division consumes four kits, 2600 people and the scaled recruitment budget");
                var army = e.S.Armies.Last();
                check(army.Battalions == 4 && army.TrainingDays == 48 && army.Strength == 100 && army.Organization == 20 && conserved(e), "Training is distinct from intact equipment and preserves personnel accounting");
                check(e.CreateTemplate("Экспедиционная", 6) == null && army.Battalions == 4 && e.SelectedTemplate(e.S.Player).Battalions == 6, "New named template does not silently alter existing formations");
                int templates = e.Player.Templates.Count;
                check(e.CreateTemplate("Экспедиционная", 2) != null && e.CreateTemplate("Слишком большая", 7) != null && e.Player.Templates.Count == templates, "Invalid or duplicate templates are rejected atomically");
                e.Player.Treasury = 0; e.S.Day = 1; e.Tick(); check(army.TrainingDays == 48, "Unfunded ground training does not advance");
                e.Validate(e.S);
            });
            scenario("Mobilization and economy", () => {
                var e = fresh(); e.Player.Treasury = 100; long beforeCap = e.ManpowerCapacity(e.S.Player); int available = e.Player.Recruits;
                double approval = e.Player.Approval; e.ComputeEconomy(false); double output = e.Player.Production.Sum();
                check(e.SetConscription(ConscriptionLaw.Extensive) != null && e.Player.Conscription == ConscriptionLaw.Volunteers, "Wartime conscription is unavailable in peace");
                check(e.SetConscription(ConscriptionLaw.Limited) == null && e.ManpowerCapacity(e.S.Player) > beforeCap && e.Player.Recruits == available && e.Player.Approval < approval, "Conscription expands eligibility with political cost but creates no instant reserve");
                check(e.Player.Production.Sum() < output && ManpowerRules.Output(e.Player) == .97, "Higher conscription reduces real industrial output");
                check(e.SetConscription(ConscriptionLaw.Volunteers) != null, "Ninety-day cooldown prevents instant mobilization toggling");
                e.S.Day = 7; e.TickManpower();
                check(e.Player.Recruits > available && conserved(e), "Weekly mustering transfers people into the accounted reserve");
                long homeCap = e.ManpowerCapacity(e.S.Player); int foreign = world.Countries[world.ByCode("POL")].Regions[0]; e.S.Provinces[foreign].Owner = e.S.Player;
                check(e.ManpowerCapacity(e.S.Player) == homeCap, "Occupied foreign populations do not become free recruits");
                int province = home(e); int oldOwner = e.S.Provinces[province].Owner; e.S.Provinces[province].Owner = world.ByCode("POL");
                check(e.ManpowerCapacity(e.S.Player) < homeCap, "Losing home territory reduces the available mobilization base"); e.S.Provinces[province].Owner = oldOwner;
                reserve(e.Player, 300000); e.S.Day = 90; long discharged = e.Player.Discharged;
                check(e.SetConscription(ConscriptionLaw.Volunteers) == null && e.Player.Recruits < 300000 && e.Player.Discharged > discharged && conserved(e), "Demobilization releases excess reserves without deleting active soldiers");
                e.Validate(e.S);
            });
            scenario("Losses, reinforcement and disbanding", () => {
                var e = fresh(); int region = home(e); var a = e.S.Armies.First(u => u.Owner == e.S.Player); a.Region = region;
                e.Player.Treasury = 100; e.Player.Stock[7] = 100; int people = a.Personnel; double organization = a.Organization;
                e.DamageArmy(a, 25);
                check(a.Personnel < people && a.Organization < organization && e.Player.PersonnelLosses == people - a.Personnel && conserved(e), "Battle losses remove real soldiers and reduce organization without changing total accounting");
                e.S.Day = 1; people = a.Personnel; int pool = e.Player.Recruits; double money = e.Player.Treasury, arms = e.Player.Stock[7];
                e.TickManpower();
                check(a.Personnel > people && e.Player.Recruits < pool && e.Player.Treasury < money && e.Player.Stock[7] < arms && conserved(e), "Stationary supplied formation receives paid replacements from the finite reserve");
                people = a.Personnel; a.Target = world.Regions[region].Neighbors.First(); e.TickManpower();
                check(a.Personnel == people, "Moving formations cannot receive replacements"); a.Target = -1;
                a.Supply = 10; e.TickManpower(); check(a.Personnel == people, "Poorly supplied formations cannot receive replacements"); a.Supply = 100;
                reserve(e.Player, 0); e.TickManpower(); check(a.Personnel == people, "Repairs and organization recovery cannot invent missing soldiers");
                reserve(e.Player, 1000); e.Player.Stock[7] = 0; e.TickManpower(); check(a.Personnel == people, "Replacements require actual equipment resources");
                e.Player.Stock[7] = 100; e.Player.Treasury = -1; pool = e.Player.Recruits; e.TickManpower();
                check(a.Personnel == people && e.Player.Recruits == pool && e.Player.Treasury == -1 && e.Player.Stock[7] == 100, "Unfunded replacements preserve a negative treasury and consume no resources");
                e.Player.Treasury = 100;
                e.Player.Stock[7] = 100; a.Organization = 10; int enemy = world.ByCode("POL"); e.Diplomacy(enemy, 3);
                int border = world.Countries[e.S.Player].Regions.First(r => world.Regions[r].Neighbors.Any(n => e.S.Provinces[n].Owner == enemy));
                int target = world.Regions[border].Neighbors.First(n => e.S.Provinces[n].Owner == enemy); a.Region = border;
                check(e.Order(a.Id, target) != null, "Disorganized formations cannot launch a new attack");
                a.Region = region; a.Organization = 100; a.Strength = 100;
                var design = e.FindDesign(a.Owner, a.DesignId); int stock = design.Stock; pool = e.Player.Recruits; people = a.Personnel;
                check(e.DisbandArmy(a.Id) == null && !e.S.Armies.Contains(a) && e.Player.Recruits == pool + people && design.Stock == stock + 1 && conserved(e), "Disbanding returns surviving soldiers and intact equipment exactly once");
                check(e.DisbandArmy(a.Id) != null && design.Stock == stock + 1, "Repeated disbanding cannot duplicate equipment");
                var other = e.S.Armies.First(u => u.Owner == e.S.Player); long losses = e.Player.PersonnelLosses; people = other.Personnel;
                e.DestroyArmy(other); e.DestroyArmy(other);
                check(e.Player.PersonnelLosses == losses + people && conserved(e), "Destroyed formation accounts for remaining personnel only once");
                e.Validate(e.S);
            });
            scenario("Refit and crew accounting", () => {
                var e = fresh(); var a = e.S.Armies.First(u => u.Owner == e.S.Player); a.Region = home(e); e.Player.Treasury = 100;
                e.Adopt(EquipmentRules.Balanced(EquipmentKind.IFV, e.Player, "Personnel carrier")); var design = e.Player.Designs.Last(); design.Stock = 1;
                int pool = e.Player.Recruits, people = a.Personnel;
                check(e.Refit(a.Id, design.Id) == null && a.Personnel == people + 300 && e.Player.Recruits == pool - 300 && conserved(e), "Refitting into a more manpower-intensive class consumes the additional crew");
                a.TrainingDays = 0; e.Adopt(EquipmentRules.Balanced(EquipmentKind.Truck, e.Player, "Staff support")); design = e.Player.Designs.Last(); design.Stock = 1;
                pool = e.Player.Recruits; check(e.Refit(a.Id, design.Id, true) == null && e.Player.Recruits == pool - 100 && conserved(e), "Support company draws its own personnel from the reserve");
                e.Adopt(EquipmentRules.Balanced(EquipmentKind.Fighter, e.Player, "Crew fighter")); design = e.Player.Designs.Last(); design.Stock = 1; int region = home(e); e.S.Provinces[region].Airfield = 1;
                pool = e.Player.Recruits; reserve(e.Player, 0);
                check(e.DeployAirWing(design.Id, region) != null && design.Stock == 1, "Aircraft cannot deploy without pilots and ground crews");
                reserve(e.Player, pool); e.DeployAirWing(design.Id, region); var wing = e.S.AirWings.Single(); wing.TrainingDays = 0;
                check(e.Player.Recruits == pool - 120 && wing.Personnel == 120 && conserved(e), "Aircraft deployment consumes an accounted crew");
                e.DamageAirWing(wing, 60); int crew = wing.Personnel; e.S.Day = 1; e.TickManpower();
                check(wing.Personnel > crew && conserved(e), "Aircraft crews replenish from the same reserve while grounded");
                e.DestroyAirWing(wing); check(conserved(e), "Destroyed aircraft account for surviving crew as losses"); e.Validate(e.S);
            });
            scenario("Reorganization", () => {
                var e = fresh(); int region = home(e); var a = e.S.Armies.First(u => u.Owner == e.S.Player); a.Region = region;
                var design = e.FindDesign(e.S.Player, a.DesignId); design.Stock = 2; e.Player.Treasury = 100; reserve(e.Player, 5000);
                int pool = e.Player.Recruits; double money = e.Player.Treasury;
                check(e.ReorganizeArmy(a.Id, 3) != null && design.Stock == 2 && e.Player.Recruits == pool && e.Player.Treasury == money, "Reorganization with insufficient equipment preserves all resources");
                design.Stock = 3; int original = a.Personnel;
                check(e.ReorganizeArmy(a.Id, 3) == null && a.Battalions == 4 && a.Personnel == original + 1800 && design.Stock == 0 && a.TrainingDays == 23 && a.Organization == 40 && conserved(e), "Existing formation expansion consumes missing battalions and personnel and requires retraining");
                check(e.ReorganizeArmy(a.Id, 1) != null && a.Battalions == 4, "A formation cannot bypass ongoing retraining by changing templates again");
                a.TrainingDays = 0; pool = e.Player.Recruits;
                check(e.ReorganizeArmy(a.Id, 1) == null && a.Personnel == original && design.Stock == 3 && e.Player.Recruits == pool + 1800 && conserved(e), "Shrinking an intact formation returns its actual excess personnel and equipment");
                a.TrainingDays = 0; e.ReorganizeArmy(a.Id, 3); a.TrainingDays = 0; e.DamageArmy(a, 60); int stock = design.Stock;
                e.ReorganizeArmy(a.Id, 1);
                check(design.Stock == stock + 1 && conserved(e), "Reorganizing a damaged formation does not recreate destroyed equipment"); e.Validate(e.S);
            });
            scenario("Scale, save and strategy", () => {
                var e = fresh(); int region = home(e); var design = e.Player.Designs[0]; e.Player.Treasury = 100; design.Stock = 10; reserve(e.Player, 10000);
                e.SelectTemplate(3); e.Recruit(region); var division = e.S.Armies.Last(); var enemy = e.S.Armies.First(a => a.Owner != e.S.Player);
                division.Organization = 100; var group = e.S.Armies.First(a => a.Owner == e.S.Player);
                double ratio = EquipmentRules.CombatPower(division, enemy, false) / EquipmentRules.CombatPower(group, enemy, false);
                check(Math.Abs(ratio - 4) < 1e-8, "Four fully manned battalions provide four times the same-model combat power");
                division.TrainingDays = 0; division.Target = world.Regions[region].Neighbors.First(r => e.S.Provinces[r].Owner == e.S.Player); division.Days = division.TotalDays = 100;
                e.Player.Stock[2] = 100; double fuel = e.Player.Stock[2], money = e.Player.Treasury; e.S.Day = 1; e.Tick();
                check(Math.Abs(fuel - e.Player.Stock[2] - (division.Design.Fuel * 4 + .009)) < 1e-8, "Moving division consumes four battalions of fuel in the real daily simulation");
                check(Math.Abs(money - e.Player.Treasury - division.Design.Maintenance * 7) < 1e-8, "Formation upkeep scales with every fielded battalion");
                division.Target = -1;
                e.DamageArmy(division, 7.25); string path = Path.Combine(directory, "personnel.xml"); e.Save(path);
                var loaded = fresh(); loaded.Load(path);
                for (int i = 0; i < 91; i++) { e.Tick(); loaded.Tick(); }
                string copy = Path.Combine(directory, "personnel-copy.xml"); e.Save(path); loaded.Save(copy);
                check(File.ReadAllText(path) == File.ReadAllText(copy) && conserved(loaded), "Personnel, fractional casualties, templates and reserves remain deterministic across save/load");
                loaded.Player.Recruits++; bool rejected = false; try { loaded.Validate(loaded.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Save validator rejects a duplicated or unaccounted recruit"); loaded.Player.Recruits--;
                var unit = loaded.S.Armies.First(a => a.Owner == loaded.S.Player); unit.TemplateId = int.MaxValue; rejected = false;
                try { loaded.Validate(loaded.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Save validator rejects a broken formation template reference");
                var legacy = fresh(); legacy.S.Version = 4; legacy.Save(path); loaded.Load(path);
                check(loaded.S.Version == 6 && loaded.S.Armies.Count == legacy.S.Armies.Count && loaded.Player.Designs[0].Stock == legacy.Player.Designs[0].Stock && conserved(loaded), "Version-four migration preserves units and equipment while initializing a balanced personnel ledger");
                int pol = world.ByCode("POL"); e = fresh(); e.Diplomacy(pol, 3); var n = e.S.Nations[pol]; reserve(n, 0); n.Treasury = 100; n.Stability = 80;
                e.PlanManpower(pol); check(n.Conscription == ConscriptionLaw.Limited && conserved(e), "Wartime AI raises conscription under the same costs and restrictions");
                for (int i = 0; i < 730; i++) { e.Tick(); if (i % 30 == 0) e.Validate(e.S); }
                check(conserved(e), "Two-year wartime simulation preserves every nation's personnel balance"); e.Validate(e.S);
            });
        }
    }
}
