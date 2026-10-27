using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Sovereign
{
    internal static class CampaignSystemsTests
    {
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            int pol = world.ByCode("POL"), ger = world.ByCode("DEU");
            scenario("Treaties", () => {
                var e = fresh(); int me = e.S.Player;
                e.S.Relations[e.Pair(me, pol)] = e.S.Relations[e.Pair(pol, me)] = 0;
                check(e.Diplomacy(pol, 5) != null && !e.Allied(me, pol), "Alliance requires positive relations and does not partially mutate on rejection");
                e.S.Relations[e.Pair(me, pol)] = e.S.Relations[e.Pair(pol, me)] = 65;
                check(e.Diplomacy(pol, 5) == null && e.Allied(me, pol) && e.Allied(pol, me) && e.HasAccess(me, pol), "Defensive alliance grants reciprocal land access");
                check(e.Diplomacy(pol, 3) != null && !e.AtWar(me, pol), "Cannot attack an ally");
                e.S.Player = ger;
                check(e.Diplomacy(pol, 3) == null && e.AtWar(ger, pol) && e.AtWar(ger, me), "Attacking a country calls its defensive ally into the war");
                check(!e.HasAccess(ger, pol) && !e.S.Agreements[e.Pair(ger, pol)], "War cancels transit and trade");
                e.S.Player = me;
                check(e.Diplomacy(pol, 5) == null && !e.Allied(me, pol), "An alliance can be explicitly dissolved");
                e.Validate(e.S);
            });
            scenario("Transit and revocation", () => {
                var e = fresh(); int me = e.S.Player;
                int border = world.Countries[me].Regions.First(r => world.Regions[r].Neighbors.Any(n => e.S.Provinces[n].Owner == pol));
                int destination = world.Regions[border].Neighbors.First(n => e.S.Provinces[n].Owner == pol);
                var unit = e.S.Armies.First(a => a.Owner == me); unit.Region = border;
                check(e.Order(unit.Id, destination) != null, "Neutral border rejects movement before access treaty");
                e.S.Relations[e.Pair(me, pol)] = e.S.Relations[e.Pair(pol, me)] = 60;
                check(e.Diplomacy(pol, 6) == null && e.HasAccess(me, pol) && !e.HasAccess(pol, me), "Requested access is directional");
                e.Order(unit.Id, destination);
                for (int day = 0; day < 100 && unit.Region != destination; day++) e.Tick();
                check(unit.Region == destination && e.S.Provinces[destination].Owner == pol, "Transit movement never captures friendly territory");
                unit.Supply = 20; unit.SupplyDays = 0;
                for (int day = 0; day < 12; day++) e.Tick();
                check(unit.Supply > 65 && unit.SupplyDays > 0, "Transit treaty supplies troops through connected friendly land");
                check(e.Diplomacy(pol, 6) == null && e.S.Provinces[unit.Region].Owner == me && unit.TrainingDays >= 14 && unit.Supply <= 50,
                    "Revoked access repatriates troops with a recovery delay");
                e.Validate(e.S);
            });
            scenario("Peace terms", () => {
                var e = fresh(); int me = e.S.Player;
                e.Diplomacy(pol, 3);
                check(e.Diplomacy(pol, 8) != null && e.AtWar(me, pol), "Premature white peace can be rejected without ending war");
                int occupied = world.Countries[pol].Regions[0]; e.S.Provinces[occupied].Owner = me;
                e.S.Day = 360;
                check(e.Diplomacy(pol, 8) == null && !e.AtWar(me, pol) && e.S.Provinces[occupied].Owner == pol, "Negotiated return of homeland restores occupied bilateral territory");
                check(e.Diplomacy(pol, 3) != null && e.S.TruceUntil[e.Pair(me, pol)] == 540, "Peace enforces a 180-day truce");
                e.S.Day = 540; check(e.Diplomacy(pol, 3) == null && e.AtWar(me, pol), "War is available when a truce expires");
                e.S.Day = 720; e.S.Provinces[occupied].Owner = me;
                check(e.Diplomacy(pol, 4) == null && e.S.Provinces[occupied].Owner == me, "Peace by control line preserves acquired territory");
                e.Validate(e.S);
            });
            scenario("Front operations", () => {
                var e = fresh(); int me = e.S.Player;
                e.Diplomacy(pol, 3);
                var unit = e.S.Armies.First(a => a.Owner == me);
                int border = world.Countries[me].Regions.First(r => world.Regions[r].Neighbors.Any(n => e.S.Provinces[n].Owner == pol));
                int target = world.Regions[border].Neighbors.First(n => e.S.Provinces[n].Owner == pol);
                foreach (var removed in e.S.Armies.Where(a => a.Region == target).ToList()) e.DestroyArmy(removed); unit.Region = border;
                check(e.Order(unit.Id, target, false, false) != null && unit.Target < 0, "Defensive repositioning forbids hostile transit");
                check(e.CreateFront(pol, new int[0], FrontStance.Hold) != null && e.S.Fronts.Count == 0, "An empty front is rejected");
                check(e.CreateFront(pol, new[] { unit.Id }, FrontStance.Hold) == null && unit.Target < 0, "Hold order occupies a border without launching an attack");
                var plan = e.S.Fronts.Single();
                check(e.ChangeFront(plan.Id, FrontStance.Advance, true) == null && unit.Target >= 0 && e.S.Provinces[unit.Target].Owner == pol,
                    "Advance order attacks an undefended neighboring enemy province");
                check(e.ChangeFront(plan.Id, FrontStance.Hold, false) == null && unit.Target == -1 && unit.Route.Count == 0, "Pausing a front stops its existing routes");
                int friendly = world.Regions[border].Neighbors.First(n => e.S.Provinces[n].Owner == me);
                check(e.Order(unit.Id, friendly) == null && e.FrontFor(unit.Id) == null, "Manual orders detach a formation from automatic command");
                check(e.CreateFront(pol, new[] { unit.Id }, FrontStance.Hold) == null && e.S.Fronts.Sum(p => p.ArmyIds.Count) == 1,
                    "Reassignment cannot give one army conflicting fronts");
                var active = e.S.Fronts.Last();
                string path = Path.Combine(directory, "front-roundtrip.xml"); e.Save(path);
                var loaded = fresh(); loaded.Load(path);
                check(loaded.S.Fronts.Last().ArmyIds.SequenceEqual(active.ArmyIds) && loaded.S.Fronts.Last().Stance == active.Stance, "Save/load retains front membership and stance");
                for (int day = 0; day < 91; day++) { e.Tick(); loaded.Tick(); }
                e.Save(path); string copy = Path.Combine(directory, "front-roundtrip-copy.xml"); loaded.Save(copy);
                check(File.ReadAllText(path) == File.ReadAllText(copy), "War and automatic fronts remain deterministic across save/load");
                e.Diplomacy(pol, 4);
                check(e.S.Fronts.All(p => !p.Active), "Peace stops fronts against the former enemy");
                e.Validate(e.S);
            });
            scenario("Migration and corrupt state", () => {
                var legacy = fresh(); legacy.S.Version = 2; legacy.S.Access = null; legacy.S.Alliances = null; legacy.S.WarStarted = null; legacy.S.TruceUntil = null;
                string path = Path.Combine(directory, "v2-treaties.xml"); using (var file = File.Create(path)) new XmlSerializer(typeof(Campaign)).Serialize(file, legacy.S);
                var e = fresh(); e.Load(path);
                check(e.S.Version == 12 && !e.S.Alliances.Any(x => x) && e.S.Fronts.Count == 0 && e.S.Armies.Count == legacy.S.Armies.Count,
                    "Version 2 migration preserves forces and adds no unsolicited alliances");
                e.S.Alliances[e.Pair(e.S.Player, pol)] = true; bool rejected = false;
                try { e.Validate(e.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Asymmetric alliance data is rejected");
                e.S.Alliances[e.Pair(e.S.Player, pol)] = false;
                e.Diplomacy(pol, 3); e.CreateFront(pol, new[] { e.S.Armies.First(a => a.Owner == e.S.Player).Id }, FrontStance.Hold);
                e.S.Fronts[0].ArmyIds.Add(int.MaxValue); rejected = false;
                try { e.Validate(e.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Dangling front membership is rejected before saving");
            });
            scenario("Control bindings", () => {
                var prefs = new Preferences();
                check(prefs.SetBinding("Pause", Keys.Control | Keys.P) == null && prefs.Binding("Pause") == (Keys.Control | Keys.P), "Controls accept modifier shortcuts");
                check(prefs.SetBinding("Save", Keys.Control | Keys.P) != null && prefs.Binding("Save") == Keys.F5, "Binding conflicts preserve the previous shortcut");
                check(prefs.SetBinding("Pause", Keys.Escape) != null, "Escape remains available for menu navigation and cancellation");
                string path = Path.Combine(directory, "bindings.xml");
                using (var file = File.Create(path)) new XmlSerializer(typeof(Preferences)).Serialize(file, prefs);
                using (var file = File.OpenRead(path)) prefs = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(file);
                prefs.NormalizeBindings(); check(prefs.Binding("Pause") == (Keys.Control | Keys.P), "User controls survive serialization");
                prefs.Bindings.Clear(); prefs.NormalizeBindings(); check(prefs.Binding("Pause") == Keys.Space, "Invalid control settings recover usable defaults");
            });
        }
    }
}
