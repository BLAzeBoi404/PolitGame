using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Sovereign
{
    internal static class EquipmentTests
    {
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex.GetType().Name + " " + ex.Message); } };
            scenario("Map clipping", () => {
                using (var land = new Land { Rings = new[] { new[] { new System.Drawing.PointF(-10, -10), new System.Drawing.PointF(10, -10), new System.Drawing.PointF(10, 10), new System.Drawing.PointF(-10, 10) } } }) {
                    land.Prepare(); var original = land.Path.GetBounds();
                    using (var clipped = land.ClipDisplayPath(new System.Drawing.RectangleF(-2, -3, 4, 6), 50))
                        check(clipped.GetBounds() == new System.Drawing.RectangleF(-2, -3, 4, 6) && clipped.IsVisible(0, 0), "Map clipping preserves the visible polygon interior");
                    check(land.Path.GetBounds() == original && land.Path.IsVisible(9, 9), "Rendering simplification never mutates exact selection geometry");
                }
            });
            scenario("Designer", () => {
                var e = fresh();
                foreach (EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind))) {
                    Blueprint d = EquipmentRules.Balanced(kind, e.Player, kind.ToString());
                    double money = e.Player.Treasury;
                    check(e.Adopt(d) == null && Math.Abs(money - e.Player.Treasury - 2 * d.Cost) < 1e-10, "Paid design adoption: " + kind);
                    d.Engine = 8;
                    check(e.Player.Design.Engine != 8 && e.Player.Designs.Last().Blueprint.Engine != 8, "Adoption snapshots modules: " + kind);
                }
                var b = e.Player.Design.Copy(); b.Name = "Locked"; b.Kind = EquipmentKind.Tank; b.Engine = 8;
                double balance = e.Player.Treasury; int count = e.Player.Designs.Count;
                check(e.Adopt(b) != null && e.Player.Treasury == balance && e.Player.Designs.Count == count, "Locked module does not charge or mutate state");
                b = new Blueprint { Name = "Invalid truck", Kind = EquipmentKind.Truck, Gun = 4 };
                check(e.Adopt(b) != null, "Incompatible weapons rejected");
                b = new Blueprint { Name = "\n" };
                check(e.Adopt(b) != null && e.Adopt(null) != null, "Invalid names and null designs rejected");
                b = new Blueprint { Name = "Modern", Kind = EquipmentKind.Fighter }; e.Player.Tech[5] = 0;
                check(e.Adopt(b) != null, "Aircraft gated by military research");
                e.Player.Tech[5] = 2;
                check(e.Adopt(b) == null, "Military research unlocks aircraft");
                var light = new Blueprint(); var heavy = light.Copy(); heavy.Armor++;
                check(heavy.Protection > light.Protection && heavy.Weight > light.Weight && heavy.Speed < light.Speed && heavy.Cost > light.Cost && heavy.Fuel > light.Fuel, "Armor tradeoff is consistent across five stats");
                var engine = light.Copy(); engine.Engine++;
                check(engine.Speed > light.Speed && engine.Fuel > light.Fuel && engine.Cost > light.Cost, "Engine gains speed at a material and fuel cost");
                var tank = light.Copy(); tank.FuelTank++;
                check(tank.Range > light.Range && tank.Weight > light.Weight && tank.Speed < light.Speed, "Fuel tanks trade mobility for real supply endurance");
                check(e.Adopt(e.Player.Design.Copy()) != null, "Duplicate names rejected without replacing existing designs");
            });
            scenario("Production", () => {
                var e = fresh(); var n = e.Player; var d = n.Designs[0]; int id = d.Id;
                int capacity = e.FactoryCapacity(e.S.Player);
                check(e.StartProduction(id, capacity + 1, 3) != null && n.Lines.Count == 0, "Factory over-allocation is rejected");
                check(e.StartProduction(id, 0, 1) != null && e.StartProduction(id, 1, 0) != null && e.StartProduction(-1, 1, 1) != null, "Invalid production requests are rejected");
                check(e.StartProduction(id, 1, 2) == null, "Production line can be created");
                var line = n.Lines[0]; n.Treasury = 20; n.Stock[4] = 20; n.Stock[5] = 20; n.Stock[7] = 20;
                double money = n.Treasury, steel = n.Stock[4], machines = n.Stock[5], arms = n.Stock[7];
                e.ChangeLine(line.Id, 1, true); e.TickProduction();
                check(line.Progress == 0 && n.Treasury == money && e.AssignedFactories(e.S.Player) == 0, "Pause preserves progress and releases capacity without spending");
                e.ChangeLine(line.Id, 1, false);
                n.Stock[4] = 0; e.TickProduction();
                check(line.Progress == 0 && n.Stock[5] == machines && n.Treasury == money && e.ProductionStatus(e.S.Player, line).Contains("Сталь"), "Missing steel blocks all associated costs and explains the shortage");
                n.Stock[4] = steel;
                for (int i = 0; i < 500 && line.Active; i++) e.TickProduction();
                check(line.Completed == 2 && d.Stock == 2 && line.Progress < 1e-8, "Serial production completes exact ordered quantity");
                check(Math.Abs(money - n.Treasury - 2 * d.Blueprint.Cost) < 1e-8 && Math.Abs(steel - n.Stock[4] - 2 * d.Blueprint.Steel) < 1e-8
                    && Math.Abs(machines - n.Stock[5] - 2 * d.Blueprint.Machines) < 1e-8 && Math.Abs(arms - n.Stock[7] - 2 * d.Blueprint.Arms) < 1e-8, "Completed kits conserve money and all three material inputs");
                e.TickProduction(); check(d.Stock == 2 && e.AssignedFactories(e.S.Player) == 0, "Completed line cannot duplicate stock and frees capacity");
                check(e.CancelLine(line.Id) == null && d.Stock == 2, "Removing completed line retains manufactured stock");
                e.StartProduction(id, 1, 1); line = n.Lines[0]; e.TickProduction(); double invested = n.Treasury;
                e.CancelLine(line.Id); check(n.Treasury == invested && d.Stock == 2, "Cancelling unfinished kit neither refunds spent inputs nor creates stock");
                e.StartProduction(id, 1, 1); line = n.Lines[0];
                foreach (var p in e.S.Provinces.Where(p => p.Owner == e.S.Player)) { p.Public[7] = 0; p.Private[7] = 0; }
                e.TickProduction(); check(line.Progress == 0 && e.ProductionStatus(e.S.Player, line).Contains("утрачены"), "Losing factories stops existing production");
                int home = world.Countries[e.S.Player].Regions[0]; e.S.Provinces[home].Public[7] = 1;
                e.TickProduction(); check(line.Progress > 0, "Rebuilt capacity resumes the original line");
            });
            scenario("Deployment", () => {
                var e = fresh(); var d = e.Player.Designs[0]; int home = world.Countries[e.S.Player].Regions[0];
                int before = e.S.Armies.Count; double money = e.Player.Treasury;
                check(e.Recruit(home) != null && e.S.Armies.Count == before && e.Player.Treasury == money, "Recruitment cannot invent equipment");
                d.Stock = 2;
                check(e.Recruit(world.Countries[world.ByCode("POL")].Regions[0]) != null && d.Stock == 2, "Cannot recruit in a foreign province");
                check(e.Recruit(home) == null && d.Stock == 1, "Recruitment consumes exactly one stored kit");
                var a = e.S.Armies.Last(); int target = world.Regions[home].Neighbors.First(r => e.S.Provinces[r].Owner == e.S.Player);
                check(a.TrainingDays == 30 && e.Order(a.Id, target) != null, "New formations cannot move during training");
                for (int i = 0; i < 30; i++) e.Tick();
                check(a.TrainingDays == 0 && e.Order(a.Id, target) == null, "Trained formation accepts movement orders");
                e.Adopt(new Blueprint { Name = "Refit", Armor = 3 }); var upgrade = e.Player.Designs.Last(); upgrade.Stock = 1;
                check(e.Refit(a.Id, upgrade.Id) != null && upgrade.Stock == 1, "Moving formation cannot refit");
                a.Target = -1; a.Route.Clear(); a.Strength = 100;
                check(e.Refit(a.Id, upgrade.Id) == null && upgrade.Stock == 0 && d.Stock == 2 && a.TrainingDays == 14 && a.Design.Armor == 3, "Refit swaps inventory, returns intact old equipment and requires training");
                var truck = EquipmentRules.Balanced(EquipmentKind.Truck, e.Player, "Support truck"); e.Adopt(truck); var record = e.Player.Designs.Last(); record.Stock = 2;
                a.TrainingDays = 0;
                check(e.Refit(a.Id, record.Id, true) == null && record.Stock == 1 && a.Attachments.Count == 1, "Support installation consumes a produced kit");
                a.TrainingDays = 0;
                check(e.Refit(a.Id, record.Id, true) != null && record.Stock == 1, "Duplicate support classes cannot stack");
                var enemy = new Army(); double bare = EquipmentRules.CombatPower(a, enemy, false);
                a.Attachments.Add(new EquipmentAttachment { Blueprint = EquipmentRules.Balanced(EquipmentKind.Bomber, e.Player, "Bomber") });
                double bombing = EquipmentRules.CombatPower(a, enemy, false);
                enemy.Attachments.Add(new EquipmentAttachment { Blueprint = EquipmentRules.Balanced(EquipmentKind.Fighter, e.Player, "Fighter") });
                check(bombing > bare && EquipmentRules.CombatPower(a, enemy, false) < bombing, "Bomber support improves attacks; enemy fighters counter it");
                check(EquipmentRules.Support(a, EquipmentKind.Truck) > 0, "Truck loadout contributes to movement and supply calculations");
            });
            scenario("Persistence", () => {
                var e = fresh(); e.StartProduction(e.Player.ActiveDesign, 1, 3);
                for (int i = 0; i < 11; i++) e.Tick();
                string path = Path.Combine(directory, "equipment-campaign.xml"); e.Save(path);
                var restored = fresh(); restored.Load(path);
                check(restored.Player.Lines[0].Progress == e.Player.Lines[0].Progress && restored.Player.Lines[0].Efficiency == e.Player.Lines[0].Efficiency, "Save/load retains exact partial production and efficiency");
                for (int i = 0; i < 120; i++) { e.Tick(); restored.Tick(); }
                string other = Path.Combine(directory, "equipment-restored.xml"); e.Save(path); restored.Save(other);
                check(File.ReadAllText(path) == File.ReadAllText(other), "Entire campaign remains bit-for-bit deterministic after save/load");
                check(File.Exists(path + ".bak"), "Atomic save retains previous backup");
                string invalid = Path.Combine(directory, "invalid.xml"); File.WriteAllText(invalid, File.ReadAllText(path).Replace("<Stock>\n", "<Stock>\n"));
                string text = File.ReadAllText(path); int start = text.IndexOf("<Treasury>") + "<Treasury>".Length; int end = text.IndexOf("</Treasury>", start);
                File.WriteAllText(invalid, text.Substring(0, start) + "NaN" + text.Substring(end));
                var original = restored.S; bool rejected = false;
                try { restored.Load(invalid); } catch (InvalidDataException) { rejected = true; }
                check(rejected && ReferenceEquals(original, restored.S), "Nonfinite save is rejected atomically without losing the active campaign");
                var badLine = e.Player.Lines[0]; int saved = badLine.DesignId; badLine.DesignId = -5; rejected = false;
                try { e.Validate(e.S); } catch (InvalidDataException) { rejected = true; }
                badLine.DesignId = saved; check(rejected, "Dangling production references rejected");
                var legacy = fresh(); legacy.S.Version = 1;
                foreach (var n in legacy.S.Nations) { n.Designs.Clear(); n.ActiveDesign = 0; n.NextDesign = 1; }
                foreach (var a in legacy.S.Armies) a.DesignId = 0;
                string legacyPath = Path.Combine(directory, "legacy.xml");
                using (var stream = File.Create(legacyPath)) new XmlSerializer(typeof(Campaign)).Serialize(stream, legacy.S);
                restored.Load(legacyPath);
                check(restored.S.Version == 4 && restored.Player.Designs.Count == 1 && restored.S.Armies.All(a => a.DesignId > 0), "Version 1 migration creates catalogs and retains existing armies");
                int stock = restored.Player.Designs[0].Stock; restored.Save(path); restored.Load(path);
                check(restored.Player.Designs.Count == 1 && restored.Player.Designs[0].Stock == stock, "Repeated loading cannot duplicate migrated designs or equipment");
            });
            scenario("Logistics and combat", () => {
                var e = fresh();
                int player = e.S.Player, other = world.ByCode("POL");
                int home = world.Countries[player].Regions.OrderByDescending(r => e.S.Provinces[r].Population).First();
                bool[] network = e.SupplyNetwork();
                check(network[home], "Largest controlled home region is a supply hub");
                int island = world.Countries[player].Regions.First(r => r != home);
                foreach (int neighbor in world.Regions[island].Neighbors) e.S.Provinces[neighbor].Owner = other;
                network = e.SupplyNetwork();
                check(!network[island], "Hostile territory cuts off an isolated province from supply");
                var a = e.S.Armies.First(x => x.Owner == player); a.Region = island; a.SupplyDays = 0;
                e.Player.Stock[2] = 100; e.Player.Stock[7] = 100;
                double strength = a.Strength;
                for (int i = 0; i < 12; i++) e.Tick();
                check(a.Supply < 40 && a.Strength < strength, "Encircled formation loses supply and suffers attrition");
                check(e.Diplomacy(-1, 3) != null && e.Diplomacy(other, 99) != null && e.Subsidize(e.S.Provinces.Length) != null, "Out-of-range strategic actions are rejected");
                var attacker = new Army { Design = new Blueprint { Gun = 1 }, Supply = 100 };
                var defender = new Army { Design = new Blueprint { Armor = 6 } };
                double weak = EquipmentRules.CombatPower(attacker, defender, false);
                attacker.Design.Gun = 6;
                check(EquipmentRules.CombatPower(attacker, defender, false) > weak, "Penetration and attack improve performance against heavy armor");
            });
            scenario("Long campaign", () => {
                var e = fresh(); e.StartProduction(e.Player.ActiveDesign, 1, 3); var watch = Stopwatch.StartNew();
                for (int i = 0; i < 1825; i++) e.Tick();
                e.Validate(e.S);
                check(e.S.Day == 1825 && e.Player.Designs[0].Stock > 0, "Five-year simulation produces player equipment and passes full state validation");
                check(e.S.Nations.Where((n, i) => i != e.S.Player).Any(n => n.Lines.Count > 0 || n.Designs.Any(d => d.Stock > 0)), "AI operates real production lines and inventory");
                check(e.S.Nations.All(n => n.Stock.All(v => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0)), "Five-year economy retains finite nonnegative material stocks");
                File.WriteAllText(Path.Combine(directory, "simulation-performance.txt"), "1825 days, " + e.S.Nations.Length + " countries, " + e.S.Provinces.Length + " provinces, " + e.S.Armies.Count + " formations: " + watch.Elapsed.TotalSeconds.ToString("0.00") + " seconds\n");
            });
        }
    }
}
