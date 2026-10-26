using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Sovereign
{
    internal static class AviationTests
    {
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            Func<Engine, int, int> baseFor = (e, owner) => world.Countries[owner].Regions.First(r => world.Regions[r].Neighbors.Any(n => e.S.Provinces[n].Owner != owner));
            Func<Engine, int, int, EquipmentKind, AirWing> ready = (e, owner, region, kind) => {
                var n = e.S.Nations[owner]; n.Treasury = 100; n.Stock[2] = n.Stock[7] = n.Stock[0] = 100;
                e.S.Provinces[region].Airfield = 3;
                var d = EquipmentRules.Balanced(kind, n, "Test wing " + n.NextDesign);
                e.AdoptDesign(d, owner); var record = e.FindDesign(owner, n.ActiveDesign); record.Stock = 1;
                string error = e.DeployAirWing(record.Id, region, owner); if (error != null) throw new Exception(error);
                var wing = e.S.AirWings.Last(); wing.TrainingDays = 0; return wing;
            };
            scenario("Airfield and production", () => {
                var e = fresh(); int region = baseFor(e, e.S.Player); e.Player.Treasury = 100;
                double money = e.Player.Treasury;
                check(e.BuildAirfield(region) == null && e.S.Provinces[region].Airfield == 0 && e.Player.Treasury == money - .6, "Airfield enters a paid construction queue without instant capacity");
                check(e.BuildAirfield(region) != null && e.S.Projects.Count(p => p.Sector == 9) == 1, "Duplicate airfield construction is rejected without charging twice");
                var project = e.S.Projects.Single(p => p.Sector == 9); project.Days = 1; e.Tick();
                check(e.AirCapacity(region) == 2, "Completed level-one airfield provides two wing slots");
                var b = EquipmentRules.Balanced(EquipmentKind.Fighter, e.Player, "Production fighter"); e.Adopt(b);
                var design = e.FindDesign(e.S.Player, e.Player.ActiveDesign);
                check(e.DeployAirWing(design.Id, region) != null && e.S.AirWings.Count == 0, "Aircraft cannot deploy before production provides an equipment kit");
                e.S.Provinces[region].Public[7] += 20; e.Player.Stock[4] = e.Player.Stock[5] = e.Player.Stock[7] = 1000;
                e.StartProduction(design.Id, 10, 1);
                for (int i = 0; i < 100 && design.Stock == 0; i++) e.TickProduction();
                money = e.Player.Treasury;
                check(design.Stock == 1 && e.DeployAirWing(design.Id, region) == null && design.Stock == 0 && e.Player.Treasury == money - .15, "Produced aircraft flow from factory stock into a paid standalone wing");
                var wing = e.S.AirWings.Single(); e.SetAirMission(wing.Id, AirMission.Superiority, region);
                e.TickAviation(); check(wing.TrainingDays == 20 && wing.Sorties == 0, "Training blocks sorties and progresses with funded maintenance");
                var second = ready(e, e.S.Player, region, EquipmentKind.Fighter); e.S.Provinces[region].Airfield = 1;
                design.Stock = 1; money = e.Player.Treasury;
                check(e.DeployAirWing(design.Id, region) != null && design.Stock == 1 && e.Player.Treasury == money, "Airfield capacity rejection preserves equipment and money");
                check(e.SetAirMission(second.Id, AirMission.Strike, region) != null && second.Mission == AirMission.Reserve, "Fighter cannot accept bomber missions");
                int far = world.Countries[world.ByCode("AUS")].Regions.First();
                check(e.SetAirMission(second.Id, AirMission.Superiority, far) != null, "Combat radius rejects distant targets");
                check(e.SetAirMission(second.Id, (AirMission)99, region) != null, "Unknown air mission is rejected"); e.Validate(e.S);
            });
            scenario("Air operations and interception", () => {
                var e = fresh(); int me = e.S.Player, region = baseFor(e, me);
                int target = world.Regions[region].Neighbors.First(r => e.S.Provinces[r].Owner != me), enemy = e.S.Provinces[target].Owner;
                var bomber = ready(e, me, region, EquipmentKind.Bomber);
                check(e.SetAirMission(bomber.Id, AirMission.Strike, target) != null && bomber.Target == -1, "Bombing neutral territory is prohibited");
                e.Diplomacy(enemy, 3);
                var defender = e.S.Armies.First(a => a.Owner == enemy); defender.Region = target; defender.Strength = 100;
                e.SetAirMission(bomber.Id, AirMission.Strike, target);
                double fuel = e.Player.Stock[2]; e.TickAviation(); double unopposed = 100 - defender.Strength;
                check(bomber.Sorties == 1 && e.Player.Stock[2] < fuel && unopposed > 0, "Strike consumes fuel and damages actual enemy formations");
                var fighter = ready(e, enemy, target, EquipmentKind.Fighter); e.SetAirMission(fighter.Id, AirMission.Superiority, target, enemy);
                bomber.Strength = defender.Strength = 100; e.TickAviation();
                check(100 - defender.Strength < unopposed && bomber.Strength < 99, "Enemy fighter cover reduces bombing and inflicts aircraft losses");
                e.Player.Stock[2] = 0; int sorties = bomber.Sorties; double health = defender.Strength; e.TickAviation();
                check(bomber.Sorties == sorties && defender.Strength == health, "Fuel shortage grounds the mission without damaging the target");
                e.Player.Stock[2] = 100; e.Player.Treasury = 0; e.TickAviation();
                check(bomber.Sorties == sorties, "Unfunded maintenance grounds aircraft");
                e.Player.Treasury = 100; bomber.Strength = 70; e.SetAirMission(bomber.Id, AirMission.Reserve, -1);
                double arms = e.Player.Stock[7]; e.TickAviation();
                check(bomber.Strength == 71 && e.Player.Stock[7] < arms, "Reserve repair consumes spare equipment resources");
                var recon = ready(e, me, region, EquipmentKind.Recon); e.SetAirMission(recon.Id, AirMission.Recon, target); e.TickAviation();
                check(e.AirReconBonus(me, target) > 0 && e.AirReconBonus(me, target) <= .15, "Reconnaissance feeds a bounded bonus into ground combat");
                e.SetAirMission(recon.Id, AirMission.Reserve, -1); e.TickAviation();
                check(e.AirReconBonus(me, target) == 0, "Reconnaissance bonus expires when sorties stop");
                var transport = ready(e, me, region, EquipmentKind.Transport); var own = e.S.Armies.First(a => a.Owner == me); own.Region = region; own.Supply = 10; own.SupplyDays = 0;
                e.SetAirMission(transport.Id, AirMission.Supply, region); double food = e.Player.Stock[0]; arms = e.Player.Stock[7]; e.TickAviation();
                check(own.Supply > 10 && own.SupplyDays > 0 && e.Player.Stock[0] < food && e.Player.Stock[7] < arms, "Air supply consumes real cargo and replenishes ground formations");
                e.Player.Stock[0] = 0; sorties = transport.Sorties; e.TickAviation();
                check(transport.Sorties == sorties, "Missing cargo prevents a supply sortie");
                e.Diplomacy(enemy, 4); e.SetAirMission(bomber.Id, AirMission.Reserve, -1); e.Validate(e.S);
            });
            scenario("Airbase relocation and capture", () => {
                var e = fresh(); int me = e.S.Player, region = baseFor(e, me);
                var wing = ready(e, me, region, EquipmentKind.Fighter);
                int second = world.Countries[me].Regions.Where(r => r != region).OrderBy(r => e.AirDistance(region, r)).First();
                check(e.RebaseAirWing(wing.Id, second) != null && wing.BaseRegion == region, "Rebasing requires a destination airfield");
                e.S.Provinces[second].Airfield = 1; double fuel = e.Player.Stock[2];
                check(e.RebaseAirWing(wing.Id, second) == null && wing.BaseRegion == second && wing.TrainingDays == 3 && e.Player.Stock[2] < fuel, "Rebasing spends fuel and temporarily suspends readiness");
                wing.TrainingDays = 0; int enemy = world.ByCode("POL"); e.S.Provinces[second].Owner = enemy; e.TickAviation();
                check(wing.BaseRegion == region && wing.Strength == 80 && wing.TrainingDays == 6, "Captured base triggers evacuation with losses and reorganization");
                e.S.Provinces[region].Owner = enemy; e.TickAviation();
                check(!e.S.AirWings.Contains(wing), "Aircraft are lost when no valid evacuation airfield remains");
                e.Validate(e.S);
            });
            scenario("Air persistence", () => {
                var e = fresh(); int region = baseFor(e, e.S.Player); var wing = ready(e, e.S.Player, region, EquipmentKind.Fighter);
                e.SetAirMission(wing.Id, AirMission.Superiority, region); string path = Path.Combine(directory, "air-campaign.xml"); e.Save(path);
                var loaded = fresh(); loaded.Load(path);
                check(loaded.S.Version == 11 && loaded.S.AirWings.Single().Mission == AirMission.Superiority && loaded.S.Provinces[region].Airfield == 3, "Aircraft orders and airbases survive save/load");
                for (int i = 0; i < 35; i++) { e.Tick(); loaded.Tick(); }
                check(e.S.AirWings.Single().Strength == loaded.S.AirWings.Single().Strength && e.Player.Stock[2] == loaded.Player.Stock[2] && e.S.RandomState == loaded.S.RandomState, "Air simulation remains deterministic after a save/load round trip");
                var invalid = loaded.S.AirWings.Single(); invalid.Strength = double.NaN;
                bool rejected = false; try { loaded.Validate(loaded.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Save validator rejects non-finite aircraft readiness");
                invalid.Strength = 100; int airfield = loaded.S.Provinces[invalid.BaseRegion].Airfield; loaded.S.Provinces[invalid.BaseRegion].Airfield = 0;
                rejected = false; try { loaded.Validate(loaded.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Save validator rejects aircraft stationed without an airbase"); loaded.S.Provinces[invalid.BaseRegion].Airfield = airfield;
                invalid.DesignId = int.MaxValue; rejected = false; try { loaded.Validate(loaded.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Save validator rejects orphan aircraft design references");
                var old = fresh(); old.S.Version = 3; old.S.AirWings.Clear(); old.Save(path); loaded.Load(path);
                check(loaded.S.Version == 11 && loaded.S.AirWings.Count == 0 && loaded.S.Provinces.All(p => p.Airfield == 0), "Version-three migration grants no free aircraft or airbases");
            });
            scenario("Air strategy AI", () => {
                var e = fresh(); int owner = world.ByCode("POL"); e.Diplomacy(owner, 3);
                int region = world.Countries[owner].Regions.OrderByDescending(r => e.S.Provinces[r].Population).First();
                e.S.Provinces[region].Public[7] += 10; e.S.Nations[owner].Treasury = 100;
                int active = e.S.Nations[owner].ActiveDesign; e.PlanAviation();
                check(e.S.Projects.Any(p => p.Owner == owner && p.Sector == 9) && e.S.Nations[owner].Lines.Any(l => e.FindDesign(owner, l.DesignId).Blueprint.Air), "Wartime AI funds airbases and aircraft production through the same rules");
                check(e.S.Nations[owner].ActiveDesign == active, "Air planning preserves AI ground recruitment project"); e.Validate(e.S);
            });
        }
    }
}
