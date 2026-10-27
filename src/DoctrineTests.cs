using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Sovereign
{
    internal static class DoctrineTests
    {
        internal static void Learn(Nation n, int path, int level)
        {
            for (int tier = 0; tier < level; tier++) {
                var stage = DoctrineRules.Stages[path * 3 + tier]; n.Tech[5] = Math.Max(n.Tech[5], stage.Foundation);
                foreach (int tech in stage.Technologies) ResearchTests.Learn(n, tech);
            }
            n.Doctrines.Levels[path] = level;
        }
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); Tests.FillPlayerFormations(e); e.Player.Treasury = 10000; e.Player.Education = 60; e.Player.Military = 2.5; e.Player.Tech[5] = 2; for (int i = 0; i < 8; i++) e.Player.Stock[i] = 1000000; return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            Action<Engine, string> raw = (e, file) => { using (var stream = File.Create(file)) new XmlSerializer(typeof(Campaign)).Serialize(stream, e.S); };
            Action<Engine, int, int> active = (e, path, level) => { Learn(e.Player, path, level); e.AdoptDoctrine(path); e.S.Day = 30; };
            scenario("Doctrine rules and paid course", () => {
                var e = fresh(); var n = e.Player; var s = n.Doctrines; double cash = n.Treasury; uint rng = e.S.RandomState;
                check(e.S.Version == 12 && e.S.Nations.All(c => c.Doctrines.Levels.All(l => l == 0) && c.Doctrines.Active.All(p => p == -1)), "All new countries start with neutral doctrine and no free school");
                check(e.StartDoctrineTraining(-1) != null && e.StartDoctrineTraining(18) != null && e.StartDoctrineTraining(1) != null && e.AdoptDoctrine(0) != null && n.Treasury == cash && s.Training == -1 && e.S.RandomState == rng, "Unknown, skipped and unlearned doctrine orders are rejected atomically");
                n.Treasury = 0; check(e.StartDoctrineTraining(0) != null && s.Training == -1, "Unfunded staff courses cannot start"); n.Treasury = cash;
                check(e.StartDoctrineTraining(0) == null && s.Training == 0 && Math.Abs(cash - n.Treasury - DoctrineRules.TrainingCost(n.GDP, 0)) < 1e-8 && s.Paid > 0, "Starting a course charges its exact quoted cost once");
                double paidCash = n.Treasury; check(e.StartDoctrineTraining(3) != null && n.Treasury == paidCash && s.Training == 0, "A second course cannot overwrite or charge an occupied staff");
                e.SelectResearch(0); e.Player.Science = 10; e.Tick(); check(s.Work == 1 && s.Levels[0] == 0, "A real daily tick advances one funded staff workday without granting a level");
                e.PauseDoctrineTraining(true); e.Tick(); check(s.Work == 1, "Pausing freezes actual daily staff progress");
                e.PauseDoctrineTraining(false); n.Military = 1; e.Tick(); check(s.Work == 1, "Insufficient military budget stops staff progress");
                n.Military = 2.5; n.Education = 19; e.Tick(); check(s.Work == 1, "Insufficient education stops staff progress"); n.Education = 60;
                s.Work = 89.5; e.Tick(); check(s.Levels[0] == 1 && s.Training == -1 && s.Work == 0 && s.Paid == 0 && s.Active[0] == -1, "Completion clears one paid course without auto-adopting a school");
                check(e.StartDoctrineTraining(0) != null && e.StartDoctrineTraining(1) != null, "Completed and technology-locked stages cannot be started");
                e.Tick(); e.Tick(); check(n.ResearchProgress > 0 && s.Levels[0] == 1, "Scientific research continues alongside completed staff preparation"); e.Validate(e.S);
            });
            scenario("All doctrine stages reachable", () => {
                var e = fresh(); var n = e.Player; n.Tech = Enumerable.Repeat(7, 6).ToArray(); foreach (var d in TechnologyRules.Catalog) ResearchTests.Learn(n, d.Id);
                foreach (var stage in DoctrineRules.Stages) {
                    string error = e.StartDoctrineTraining(stage.Id); if (error != null) throw new Exception(stage.Id + ": " + error);
                    n.Doctrines.Work = stage.Duration - .01; e.Tick();
                    check(n.Doctrines.Levels[stage.Path] == stage.Tier + 1 && n.Doctrines.Training == -1, "Doctrine stage " + stage.Id + " completes through the real daily scheduler");
                }
                check(DoctrineRules.Summary(DoctrineRules.Effects(n, e.S.Day)) == "Без доктринальных изменений", "Learning all six schools never stacks inactive doctrine bonuses"); e.Validate(e.S);
            });
            scenario("Adoption and retraining boundaries", () => {
                var e = fresh(); var n = e.Player; Learn(n, 0, 3); Learn(n, 1, 3); Learn(n, 3, 3); double cash = n.Treasury, cost = DoctrineRules.AdoptionCost(n, 0);
                check(e.AdoptDoctrine(0) == null && Math.Abs(cash - n.Treasury - cost) < 1e-8 && DoctrineRules.Effects(n, 0).Attack == 0, "Adoption charges the advertised cost and starts with no land effects");
                e.S.Day = 29; check(DoctrineRules.Effects(n, e.S.Day).Attack == 0, "Doctrine effects remain absent on retraining day twenty-nine");
                e.S.Day = 30; check(Math.Abs(DoctrineRules.Effects(n, e.S.Day).Attack - .16) < 1e-8, "All learned stages of the selected school activate exactly on day thirty");
                check(e.AdoptDoctrine(3) == null && Math.Abs(DoctrineRules.Effects(n, e.S.Day).Attack - .16) < 1e-8 && DoctrineRules.Effects(n, e.S.Day).AirCover == 0, "Air retraining leaves the independently active land school intact");
                e.S.Day = 60; check(Math.Abs(DoctrineRules.Effects(n, e.S.Day).AirCover - .30) < 1e-8, "Air retraining activates only its cumulative selected school");
                e.S.Day = 179; cash = n.Treasury; check(e.AdoptDoctrine(1) != null && n.Treasury == cash && n.Doctrines.Active[0] == 0, "Changing schools before the 180-day boundary is atomic and free of charges");
                e.S.Day = 180; cost = DoctrineRules.AdoptionCost(n, 1); check(e.AdoptDoctrine(1) == null && Math.Abs(cash - n.Treasury - cost) < 1e-8 && DoctrineRules.Effects(n, 180).Attack == 0, "Changing at day 180 charges the switch price and suspends old land effects");
                e.S.Day = 210; var effect = DoctrineRules.Effects(n, e.S.Day);
                check(Math.Abs(effect.Attack - .25) < 1e-8 && Math.Abs(effect.Speed + .07) < 1e-8 && Math.Abs(effect.AirCover - .30) < 1e-8, "The replacement school applies its benefits and penalties without stacking the old school"); e.Validate(e.S);
            });
            scenario("Cancellation and lost territory", () => {
                var e = fresh(); e.StartDoctrineTraining(0); e.Tick(); e.PauseDoctrineTraining(true); double cash = e.Player.Treasury;
                check(e.CancelDoctrineTraining() == null && e.Player.Treasury == cash && e.Player.Doctrines.Work == 0 && !e.Player.Doctrines.Paused, "Cancellation forfeits the paid course and unfinished progress without refunding money");
                e.StartDoctrineTraining(0); check(e.Player.Treasury < cash && e.Player.Doctrines.Work == 0, "Restarting a cancelled course requires a new full payment");
                foreach (var p in e.S.Provinces.Where(p => p.Owner == e.S.Player)) p.Owner = world.ByCode("POL");
                e.Tick(); check(e.Player.Doctrines.Work == 0, "A country with no territory cannot advance its existing staff course");
                e.CancelDoctrineTraining(); check(e.StartDoctrineTraining(0) != null, "A country with no territory cannot order a new course");
            });
            scenario("Real land movement, supply and costs", () => {
                var a = fresh(); var b = fresh(); active(b, 0, 1); a.S.Day = b.S.Day;
                var ua = a.S.Armies.First(u => u.Owner == a.S.Player); var ub = b.S.Armies.First(u => u.Owner == b.S.Player);
                int target = world.Regions[ua.Region].Neighbors.First(r => a.S.Provinces[r].Owner == a.S.Player);
                a.Order(ua.Id, target); b.Order(ub.Id, target);
                check(Math.Abs(ua.TotalDays / ub.TotalDays - 1.08) < 1e-8, "Maneuver school accelerates the duration of actual new ground orders by eight percent");
                double fa = a.Player.Stock[2], fb = b.Player.Stock[2]; a.Tick(); b.Tick();
                check(Math.Abs((fb - b.Player.Stock[2]) / (fa - a.Player.Stock[2]) - 1.08) < 1e-6, "Maneuver school pays eight percent more actual daily ground fuel");
                a = fresh(); b = fresh(); active(b, 1, 1); a.S.Day = b.S.Day; double ca = a.Player.Treasury, cb = b.Player.Treasury;
                a.Tick(); b.Tick(); check(Math.Abs((cb - b.Player.Treasury) / (ca - a.Player.Treasury) - 1.08) < 1e-6, "Firepower school pays eight percent more actual ground maintenance");
                a = fresh(); b = fresh(); active(b, 2, 1); a.S.Day = b.S.Day; a.Tick(); b.Tick();
                check(Math.Abs(b.S.Armies.First(u => u.Owner == b.S.Player).SupplyDays - a.S.Armies.First(u => u.Owner == a.S.Player).SupplyDays - 1) < 1e-8, "Defensive doctrine adds one real day of supplied ground autonomy");
                a = fresh(); b = fresh(); active(b, 2, 2); a.S.Day = b.S.Day; a.Player.Tech = (int[])b.Player.Tech.Clone(); a.Player.ScienceTree.Known = (bool[])b.Player.ScienceTree.Known.Clone();
                ua = a.S.Armies.First(u => u.Owner == a.S.Player); ub = b.S.Armies.First(u => u.Owner == b.S.Player); int pa = ua.Personnel, pb = ub.Personnel;
                a.DamageArmy(ua, 20); b.DamageArmy(ub, 20);
                check(Math.Abs((pb - ub.Personnel + ub.CasualtyRemainder) / (pa - ua.Personnel + ua.CasualtyRemainder) - .95) < 1e-8 && ua.Strength == ub.Strength, "Defensive doctrine reduces real personnel casualties without erasing equipment damage");
                a = fresh(); b = fresh(); active(b, 0, 2); a.S.Day = b.S.Day; a.Player.Tech = (int[])b.Player.Tech.Clone(); a.Player.ScienceTree.Known = (bool[])b.Player.ScienceTree.Known.Clone();
                ua = a.S.Armies.First(u => u.Owner == a.S.Player); ub = b.S.Armies.First(u => u.Owner == b.S.Player); ua.Organization = ub.Organization = 40;
                a.TickManpower(); b.TickManpower(); check(Math.Abs((ub.Organization - 40) / (ua.Organization - 40) - 1.10) < 1e-8, "Maneuver training raises actual funded organization recovery by ten percent");
            });
            scenario("Real doctrine combat", () => {
                int enemy = world.ByCode("POL");
                Func<Engine, Army[]> prepare = e => {
                    e.Diplomacy(enemy, 3); int home = world.Countries[e.S.Player].Regions.First(r => world.Regions[r].Neighbors.Any(t => e.S.Provinces[t].Owner == enemy));
                    int target = world.Regions[home].Neighbors.First(t => e.S.Provinces[t].Owner == enemy);
                    var own = e.S.Armies.First(u => u.Owner == e.S.Player); var other = e.S.Armies.First(u => u.Owner == enemy);
                    foreach (var unit in e.S.Armies.Where(u => u.Region == target && u != other).ToList()) e.DestroyArmy(unit);
                    own.Region = home; other.Region = target; own.Strength = other.Strength = 100; own.Organization = other.Organization = 100;
                    e.S.Nations[enemy].Treasury = 10000; e.S.Nations[enemy].Stock[2] = 10000;
                    string error = e.Order(own.Id, target); if (error != null) throw new Exception(error); own.Days = .01; return new[] { own, other };
                };
                var a = fresh(); var b = fresh(); active(b, 1, 1); a.S.Day = b.S.Day; var ua = prepare(a); var ub = prepare(b); a.Tick(); b.Tick();
                check(ub[1].Strength < ua[1].Strength && ub[0].Strength > ua[0].Strength, "Firepower doctrine changes actual battle losses for attacker and defender");
                a = fresh(); b = fresh(); Learn(b.S.Nations[enemy], 2, 1); b.AdoptDoctrine(2, enemy); a.S.Day = b.S.Day = 30; ua = prepare(a); ub = prepare(b); a.Tick(); b.Tick();
                check(ub[1].Strength > ua[1].Strength && ub[0].Strength < ua[0].Strength, "An enemy defensive school changes real defensive combat independently"); a.Validate(a.S); b.Validate(b.S);
            });
            scenario("Real aviation missions", () => {
                Func<Engine, EquipmentKind, AirWing> ready = (e, kind) => {
                    int region = world.Countries[e.S.Player].Regions.First(r => world.Regions[r].Neighbors.Any(x => e.S.Provinces[x].Owner != e.S.Player)); e.S.Provinces[region].Airfield = 3;
                    e.Adopt(EquipmentRules.Balanced(kind, e.Player, "Doctrine flight")); e.FindDesign(e.S.Player, e.Player.ActiveDesign).Stock = 1;
                    string error = e.DeployAirWing(e.Player.ActiveDesign, region); if (error != null) throw new Exception(error); var wing = e.S.AirWings.Last(); wing.TrainingDays = 0; return wing;
                };
                foreach (var mission in new[] { AirMission.Recon, AirMission.Strike, AirMission.Supply }) {
                    var a = fresh(); var b = fresh(); var kind = mission == AirMission.Recon ? EquipmentKind.Recon : mission == AirMission.Strike ? EquipmentKind.Bomber : EquipmentKind.Transport;
                    var wa = ready(a, kind); var wb = ready(b, kind); active(b, mission == AirMission.Supply ? 5 : 4, 1); a.S.Day = b.S.Day;
                    int target = mission == AirMission.Supply ? wa.BaseRegion : world.Regions[wa.BaseRegion].Neighbors.First(r => a.S.Provinces[r].Owner != a.S.Player), owner = a.S.Provinces[target].Owner;
                    if (mission != AirMission.Supply) { a.Diplomacy(owner, 3); b.Diplomacy(owner, 3); }
                    var ua = a.S.Armies.First(u => u.Owner == owner); var ub = b.S.Armies.First(u => u.Owner == owner);
                    ua.Region = ub.Region = target; ua.Supply = ub.Supply = 20; ua.Strength = ub.Strength = 100;
                    if (a.SetAirMission(wa.Id, mission, target) != null || b.SetAirMission(wb.Id, mission, target) != null) throw new Exception("Invalid mission fixture");
                    double fa = a.Player.Stock[2], fb = b.Player.Stock[2]; a.TickAviation(); b.TickAviation();
                    check(wa.Sorties == 1 && wb.Sorties == 1 && Math.Abs((fb - b.Player.Stock[2]) / (fa - a.Player.Stock[2]) - (mission == AirMission.Supply ? .96 : 1.06)) < 1e-6, "Doctrine pays its actual sortie fuel tradeoff for " + mission);
                    if (mission == AirMission.Recon) check(Math.Abs(b.AirReconBonus(b.S.Player, target) / a.AirReconBonus(a.S.Player, target) - 1.05) < 1e-8, "Army aviation raises actual reconnaissance effect by five percent");
                    if (mission == AirMission.Strike) check(Math.Abs((100 - ub.Strength) / (100 - ua.Strength) - 1.10) < 1e-8, "Army aviation raises actual ground strike damage by ten percent");
                    if (mission == AirMission.Supply) check(Math.Abs((ub.Supply - 20) / (ua.Supply - 20) - 1.15) < 1e-8, "Air logistics raises actual delivered ground supply by fifteen percent");
                    a.Validate(a.S); b.Validate(b.S);
                }
                var ea = fresh(); var eb = fresh(); ready(ea, EquipmentKind.Transport); ready(eb, EquipmentKind.Transport); active(eb, 5, 2); ea.S.Day = eb.S.Day;
                double cashA = ea.Player.Treasury, cashB = eb.Player.Treasury; ea.TickAviation(); eb.TickAviation();
                check(Math.Abs((cashB - eb.Player.Treasury) / (cashA - ea.Player.Treasury) - .96) < 1e-6, "Air logistics reduces actual idle wing maintenance by four percent");
                ea = fresh(); eb = fresh();
                Func<Engine, Army> intercept = e => {
                    var fighter = ready(e, EquipmentKind.Fighter); fighter.Strength = 35; // Keep interception below the existing per-sortie loss cap.
                    int home = fighter.BaseRegion;
                    int enemyBase = world.Regions[home].Neighbors.First(r => e.S.Provinces[r].Owner != e.S.Player), enemy = e.S.Provinces[enemyBase].Owner;
                    e.Diplomacy(enemy, 3); var n = e.S.Nations[enemy]; n.Treasury = 10000; n.Stock[2] = 10000; n.Tech[5] = Math.Max(2, n.Tech[5]); e.S.Provinces[enemyBase].Airfield = 3;
                    string error = e.AdoptDesign(EquipmentRules.Balanced(EquipmentKind.Bomber, n, "Intercepted bomber"), enemy); if (error != null) throw new Exception(error);
                    e.FindDesign(enemy, n.ActiveDesign).Stock = 1; error = e.DeployAirWing(n.ActiveDesign, enemyBase, enemy); if (error != null) throw new Exception(error);
                    var bomber = e.S.AirWings.Last(); bomber.TrainingDays = 0;
                    var unit = e.S.Armies.First(u => u.Owner == e.S.Player); unit.Region = home; unit.Strength = 100;
                    error = e.SetAirMission(fighter.Id, AirMission.Superiority, home); if (error != null) throw new Exception(error);
                    error = e.SetAirMission(bomber.Id, AirMission.Strike, home, enemy); if (error != null) throw new Exception(error); return unit;
                };
                var defendedA = intercept(ea); var defendedB = intercept(eb); active(eb, 3, 1); ea.S.Day = eb.S.Day; ea.TickAviation(); eb.TickAviation();
                check(defendedB.Strength > defendedA.Strength && eb.S.AirWings.Last().Strength < ea.S.AirWings.Last().Strength, "Air superiority doctrine improves actual interception and protects ground troops from enemy bombers"); ea.Validate(ea.S); eb.Validate(eb.S);
            });
            scenario("Doctrine saves, migration and validation", () => {
                var e = fresh(); Learn(e.Player, 0, 1); e.AdoptDoctrine(0); e.StartDoctrineTraining(9); e.Tick(); e.PauseDoctrineTraining(true);
                string file = Path.Combine(directory, "doctrine.xml"), copy = Path.Combine(directory, "doctrine-copy.xml"), bad = Path.Combine(directory, "doctrine-invalid.xml"); e.Save(file);
                var loaded = fresh(); loaded.Load(file); loaded.Save(copy);
                check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(copy)), "Paused doctrine course, payment and retraining round-trip byte-identically");
                e.PauseDoctrineTraining(false); loaded.PauseDoctrineTraining(false); for (int i = 0; i < 150; i++) { e.Tick(); loaded.Tick(); } e.Save(file); loaded.Save(copy);
                check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(copy)), "Doctrine courses, adoption and AI remain deterministic for 150 days after load");
                var original = loaded.S;
                Action<string, Action<DoctrineState>> reject = (name, mutate) => {
                    var invalid = fresh(); mutate(invalid.Player.Doctrines); raw(invalid, bad); bool rejected = false; try { loaded.Load(bad); } catch (InvalidDataException) { rejected = true; }
                    check(rejected && ReferenceEquals(original, loaded.S), name);
                };
                reject("Loading rejects a learned doctrine without its technology prerequisites atomically", s => s.Levels[0] = 3);
                reject("Loading rejects a school assigned to the wrong service atomically", s => { s.Levels[3] = 1; s.Active[0] = 3; s.ChangedDay[0] = 0; });
                reject("Loading rejects an unlearned active school atomically", s => { s.Active[0] = 0; s.ChangedDay[0] = 0; });
                reject("Loading rejects nonfinite staff progress atomically", s => s.Work = double.NaN);
                reject("Loading rejects fabricated unpaid staff courses atomically", s => { s.Training = 0; s.StartedDay = 0; s.QuotedGDP = 100; });
                reject("Loading rejects unknown doctrine arrays atomically", s => s.Levels = new int[7]);
                reject("Loading rejects stale paused state without an active course atomically", s => s.Paused = true);
                e = fresh(); e.SelectResearch(0); e.Player.ResearchProgress = 31.25; double cash = e.Player.Treasury; uint rng = e.S.RandomState;
                e.S.Version = 8; foreach (var n in e.S.Nations) n.Doctrines = null; raw(e, file); loaded.Load(file);
                check(loaded.S.Version == 12 && loaded.Player.Treasury == cash && loaded.S.RandomState == rng && loaded.Player.ResearchProgress == 31.25 && loaded.S.Nations.All(n => n.Doctrines.Active.All(p => p == -1) && n.Doctrines.Levels.All(l => l == 0)), "Version-eight saves migrate without free doctrines, lost research, charges or random changes"); loaded.Validate(loaded.S);
            });
            scenario("AI learns paid doctrines", () => {
                var e = fresh(); int owner = world.ByCode("POL"); var n = e.S.Nations[owner]; n.Treasury = 10000; n.Education = 60; n.Military = 2.5; n.Tech[5] = 2;
                double cash = n.Treasury; e.PlanDoctrines(owner);
                check(n.Doctrines.Training >= 0 && n.Doctrines.Levels.All(l => l == 0) && Math.Abs(cash - n.Treasury - n.Doctrines.Paid) < 1e-8, "AI starts and pays a valid course without receiving free doctrine levels");
                for (int i = 0; i < 730; i++) e.Tick(); e.Validate(e.S);
                check(e.S.Nations.Where((c, i) => i != e.S.Player).Count(c => c.Doctrines.Levels.Any(l => l > 0)) > 10, "Two-year simulation makes multiple AI countries complete real staff courses");
                check(e.S.Nations.Where((c, i) => i != e.S.Player).Count(c => c.Doctrines.Active.Any(p => p >= 0)) > 10 && e.Player.Doctrines.Active.All(p => p == -1), "AI countries adopt their learned schools without choosing for the player");
            });
        }
    }
}
