using System;
using System.IO;
using System.Linq;

namespace Sovereign
{
    internal static class ExpandedResearchTests
    {
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); e.Player.Treasury = 10000; e.Player.Education = 80; e.Player.Science = 10; for (int i = 0; i < 8; i++) e.Player.Stock[i] = 1000000; return e; };
            Action<string, Action> scenario = (name, test) => { try { test(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            scenario("Expanded graph integrity", () => {
                check(TechnologyRules.Branches.Length == 6 && TechnologyRules.Branches.All(b => b.Length == 12), "Six branches each contain twelve independent applied technologies");
                check(TechnologyRules.Branches.All(b => b.Select(d => d.Column + "/" + d.Lane).Distinct().Count() == b.Length), "Every technology has a unique spatial position within its branch");
                check(TechnologyRules.Catalog.All(d => d.Prerequisites.All(id => TechnologyRules.Catalog[id].Branch != d.Branch || TechnologyRules.Catalog[id].Column < d.Column)), "Every in-branch dependency flows forward across the visible research stages");
                check(TechnologyRules.Catalog.All(d => d.Prerequisites.Length <= 4 && d.Column >= 0 && d.Column < TechnologyRules.StageNames.Length && d.Lane >= 0 && d.Lane < 4), "All nodes fit the graph lanes and full prerequisite dossier");
                var e = fresh(); foreach (var d in TechnologyRules.Catalog) ResearchTests.Learn(e.Player, d.Id); e.Validate(e.S);
                var effects = TechnologyRules.Effects(e.Player);
                check(effects.Food > .6 && effects.Equipment > .5 && effects.Casualties > -1 && effects.PowerInput > -1 && effects.GroundFuel > -1, "Completing all technologies retains positive resource requirements and bounded medical reductions");
                e.ComputeEconomy(true); e.Tick(); e.Validate(e.S);
                check(e.Player.Stock.All(v => v >= 0 && !double.IsNaN(v)) && e.Player.Population > 0, "A fully researched nation remains valid through real economy and daily simulation");
            });
            scenario("Agricultural revolution chain", () => {
                var e = fresh(); var n = e.Player;
                ResearchTests.Learn(n, 1); ResearchTests.Learn(n, 4);
                check(e.SelectResearch(33) != null, "Green revolution remains locked without soil, irrigation and fertilizer development");
                check(e.SelectResearch(30) == null && e.QueueResearch(31) == null && e.QueueResearch(32) == null && e.QueueResearch(33) == null, "Soil science, irrigation, fertilizers and green revolution form a valid four-stage research plan");
                for (int i = 0; i < 3; i++) { n.ResearchProgress = 99.9; e.S.Day = i * 7 + 6; e.Tick(); }
                check(n.ScienceTree.Known[24] && n.ScienceTree.Known[25] && n.ScienceTree.Known[26] && !n.ScienceTree.Known[27] && n.ScienceTree.ActiveProject == 33, "Finishing three real programs starts green revolution without granting it prematurely");
                e.ComputeEconomy(false); double food = n.Production[0], fuel = n.Consumed[2]; var before = TechnologyRules.Effects(n);
                n.ResearchProgress = 99.9; e.S.Day = 27; e.Tick(); e.ComputeEconomy(false); var after = TechnologyRules.Effects(n);
                check(n.ScienceTree.Known[27] && Math.Abs(n.Production[0] / food - (1 + before.Food + .12) / (1 + before.Food)) < .01 && n.Consumed[2] > fuel, "Green revolution increases actual harvest and pays its additional agrarian fuel demand");
                check(Math.Abs(after.FoodFuelInput - before.FoodFuelInput - .05) < 1e-9 && n.Tech[0] == 4, "Green revolution has its explicit fuel tradeoff and never grants a free fundamental level");
                e.Validate(e.S);
            });
            scenario("New medical and coordination effects", () => {
                var a = fresh(); var b = fresh(); ResearchTests.Learn(b.Player, 63);
                var ua = a.S.Armies.First(u => u.Owner == a.S.Player); var ub = b.S.Armies.First(u => u.Owner == b.S.Player);
                int pa = ua.Personnel, pb = ub.Personnel; a.DamageArmy(ua, 20); b.DamageArmy(ub, 20);
                check(Math.Abs((pb - ub.Personnel + ub.CasualtyRemainder) / (pa - ua.Personnel + ua.CasualtyRemainder) - .78) < 1e-8, "Medical revolution and its clinical prerequisites reduce actual injury casualties by twenty-two percent");
                a = fresh(); b = fresh(); ResearchTests.Learn(b.Player, 66); ua = a.S.Armies.First(u => u.Owner == a.S.Player); ub = b.S.Armies.First(u => u.Owner == b.S.Player);
                ua.Organization = ub.Organization = 40; a.TickManpower(); b.TickManpower();
                check(Math.Abs((ub.Organization - 40) / (ua.Organization - 40) - 1.15) < 1e-8, "Tactical radio increases actual funded organization recovery by fifteen percent");
                a.Validate(a.S); b.Validate(b.S);
                ResearchTests.Learn(b.Player, 71);
                check(Math.Abs(b.GroundTechnologyFactor(b.S.Player) - 1.20) < 1e-8, "Joint operations stack only the completed ground-combat prerequisites");
            });
            scenario("Version-seven research migration", () => {
                var e = fresh(); ResearchTests.Learn(e.Player, 8); e.SelectResearch(0); e.Player.ResearchProgress = 17.5; e.SelectResearch(6); e.Player.ResearchProgress = 48.25; e.QueueResearch(7); e.PauseResearch(true);
                var money = e.Player.Treasury; var random = e.S.RandomState;
                e.S.Version = 7; foreach (var n in e.S.Nations) { Array.Resize(ref n.ScienceTree.Known, 24); Array.Resize(ref n.ScienceTree.StoredProgress, 30); }
                string file = Path.Combine(directory, "research-version7.xml"); e.Save(file);
                var loaded = fresh(); loaded.Load(file); var agenda = loaded.Player.ScienceTree;
                check(loaded.S.Version == 8 && agenda.Known.Length == 72 && agenda.StoredProgress.Length == 78 && agenda.Known[8] && !agenda.Known.Skip(24).Any(k => k), "Version-seven migration extends research arrays without replacing old discoveries or gifting new ones");
                check(agenda.ActiveProject == 6 && loaded.Player.ResearchProgress == 48.25 && agenda.StoredProgress[0] == 17.5 && agenda.Paused && agenda.Queue.Single() == 7 && loaded.Player.Treasury == money && loaded.S.RandomState == random, "Migration preserves active and stored progress, queue, pause, treasury and random state exactly");
                loaded.PauseResearch(false); loaded.Player.ResearchProgress = 99.9; loaded.S.Day = 6; loaded.Tick();
                check(agenda.Known[0] && agenda.ActiveProject == 7, "A migrated research queue continues through its original stable project identifiers");
                loaded.SelectResearch(0); ResearchTests.Learn(loaded.Player, 31);
                check(loaded.SelectResearch(6 + 71) != null, "A completed agricultural revolution does not bypass military prerequisites");
                loaded.Save(Path.Combine(directory, "expanded-research.xml")); var roundtrip = fresh(); roundtrip.Load(Path.Combine(directory, "expanded-research.xml"));
                check(roundtrip.Player.ScienceTree.Known.SequenceEqual(agenda.Known), "New revolution discoveries survive format-eight save and load");
            });
            scenario("Combined-arms combat", () => {
                int enemy = world.ByCode("POL");
                Func<Engine, Army[]> prepare = e => {
                    e.Diplomacy(enemy, 3);
                    int home = world.Countries[e.S.Player].Regions.First(r => world.Regions[r].Neighbors.Any(t => e.S.Provinces[t].Owner == enemy));
                    int target = world.Regions[home].Neighbors.First(t => e.S.Provinces[t].Owner == enemy);
                    var own = e.S.Armies.First(u => u.Owner == e.S.Player); var other = e.S.Armies.First(u => u.Owner == enemy);
                    foreach (var unit in e.S.Armies.Where(u => u.Region == target && u != other).ToList()) e.DestroyArmy(unit);
                    own.Region = home; other.Region = target; own.Strength = other.Strength = 100; own.Organization = other.Organization = 100;
                    e.S.Nations[enemy].Treasury = 10000; e.S.Nations[enemy].Stock[2] = 10000;
                    string error = e.Order(own.Id, target); if (error != null) throw new Exception(error); own.Days = .01;
                    return new[] { own, other };
                };
                var a = fresh(); var b = fresh(); var unitsA = prepare(a); var unitsB = prepare(b); ResearchTests.Learn(b.Player, 64);
                a.Tick(); b.Tick();
                check(unitsB[1].Strength < unitsA[1].Strength && unitsB[0].Strength > unitsA[0].Strength, "Combined-arms research changes real combat damage and protects the attacking formation");
                a.Validate(a.S); b.Validate(b.S);
            });
        }
    }
}
