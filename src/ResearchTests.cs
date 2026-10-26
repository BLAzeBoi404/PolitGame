using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Sovereign
{
    internal static class ResearchTests
    {
        // A fixture grants a technology together with its actual prerequisites, never an invalid tree.
        internal static void Learn(Nation n, int id)
        {
            var d = TechnologyRules.Catalog[id];
            foreach (int prerequisite in d.Prerequisites) Learn(n, prerequisite);
            n.Tech[d.Branch] = Math.Max(n.Tech[d.Branch], d.Foundation); n.ScienceTree.Known[id] = true;
        }
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            Action<Engine> rich = e => { e.Player.Treasury = 10000; e.Player.Science = 10; e.Player.Education = 80; for (int i = 0; i < 8; i++) e.Player.Stock[i] = 1000000; };
            Action<Engine> week = e => { e.S.Day += 6 - e.S.Day % 7; e.Tick(); };
            Action<Engine, string> rawSave = (e, p) => { using (var s = File.Create(p)) new XmlSerializer(typeof(Campaign)).Serialize(s, e.S); };
            scenario("Technology catalog and switching", () => {
                var e = fresh(); var n = e.Player; var a = n.ScienceTree;
                check(e.S.Version == 10 && e.S.Nations.All(c => c.ScienceTree != null && !c.ScienceTree.Known.Any(k => k) && c.ScienceTree.LegacyLevels.All(l => l == 0)), "New campaigns initialize independent research agendas without granting applied discoveries");
                var reached = new bool[TechnologyRules.Count]; var levels = Enumerable.Repeat(7, 6).ToArray();
                for (int pass = 0; pass < TechnologyRules.Count; pass++) foreach (var d in TechnologyRules.Catalog) if (TechnologyRules.Requirements(d.Id + 6, levels, reached) == null) reached[d.Id] = true;
                check(reached.All(k => k) && TechnologyRules.Catalog.Select(d => d.Id).SequenceEqual(Enumerable.Range(0, TechnologyRules.Count)), "All 72 technologies are reachable through an acyclic prerequisite graph");
                double cash = n.Treasury; uint rng = e.S.RandomState;
                check(e.SelectResearch(7) != null && e.SelectResearch(29) != null && e.SelectResearch(-1) != null && e.SelectResearch(TechnologyRules.ProjectCount) != null && a.ActiveProject == -1 && n.Treasury == cash && e.S.RandomState == rng, "Unavailable or unknown research is rejected without changing money, random state or active work");
                int level = n.Tech[0]; e.SelectResearch(6); n.ResearchProgress = 36.5; e.SelectResearch(0); n.ResearchProgress = 19.25;
                check(a.StoredProgress[6] == 36.5 && a.ActiveProject == 0, "Switching from an applied project preserves its exact accumulated progress");
                e.SelectResearch(6);
                check(n.ResearchProgress == 36.5 && a.StoredProgress[6] == 0 && a.StoredProgress[0] == 19.25 && n.Tech[0] == level && n.Treasury == cash, "Returning restores progress once without spending money or raising fundamental levels");
                rich(e); e.PauseResearch(true); week(e);
                check(n.ResearchProgress == 36.5 && a.Paused, "Research pause stops actual weekly progress");
                e.PauseResearch(false); n.Science = 0; week(e);
                check(n.ResearchProgress == 36.5 && e.WeeklyResearchRate(e.S.Player, 6) == 0, "Zero science funding halts the selected program");
                n.Science = 10; n.ResearchProgress = 99.9; week(e);
                check(a.Known[0] && n.Tech[0] == level && n.Research == -1 && a.ActiveProject == -1 && n.ResearchProgress == 0, "Applied completion grants one discovery without a fundamental level or leftover active progress");
                check(e.SelectResearch(6) != null && e.QueueResearch(6) != null, "Completed applied technologies cannot be researched or queued again");
                e.SelectResearch(0); check(n.ResearchProgress == 19.25, "Completing another program does not erase stored fundamental progress");
                n.ResearchProgress = 99.9; week(e);
                check(n.Tech[0] == level + 1 && a.StoredProgress[0] == 0, "Fundamental completion increments its branch exactly once"); e.Validate(e.S);
            });
            scenario("Projected research queue", () => {
                var e = fresh(); rich(e); var n = e.Player; var a = n.ScienceTree; e.SelectResearch(6);
                check(e.QueueResearch(7) != null, "Queue rejects an applied technology missing a prerequisite even after current work finishes");
                check(e.QueueResearch(14) == null && e.QueueResearch(7) == null && e.QueueResearch(18) == null && e.QueueResearch(8) == null, "Four-step plan accepts prerequisites provided by current and earlier queued research");
                check(e.QueueResearch(0) != null && a.Queue.Count == 4, "Research plan enforces its four-program limit");
                e.RemoveResearchQueue(3); check(e.QueueResearch(14) != null && a.Queue.Count == 3, "Queue rejects duplicate projects without removing existing work");
                n.ResearchProgress = 99.9; e.PauseResearch(true); week(e);
                check(a.Queue.Count == 3 && !a.Known[0], "Pausing leaves both almost-completed work and the planned queue untouched");
                e.PauseResearch(false); week(e);
                check(a.Known[0] && a.ActiveProject == 14 && n.ResearchProgress == 0 && a.Queue.SequenceEqual(new[] { 7, 18 }), "Completion starts the next available program without granting a second weekly tick");
                e.SelectResearch(1); n.ResearchProgress = 99.9; week(e);
                check(a.ActiveProject == -1 && a.Queue[0] == 7 && !a.Known[1] && e.ResearchStatus(e.S.Player).Contains("Сначала"), "Switching away from a prerequisite leaves the dependent queue safely blocked");
                e.SelectResearch(14); n.ResearchProgress = 99.9; week(e);
                check(a.Known[8] && a.ActiveProject == 7 && a.Queue.Single() == 18, "Completing the missing prerequisite resumes the blocked plan");
                e.PauseResearch(true); e.Validate(e.S);
                e = fresh(); e.SelectResearch(0); check(e.QueueResearch(0) == null && e.QueueResearch(0) != null, "One additional fundamental level may follow the same active branch without duplicate queue entries");
                e = fresh(); e.PauseResearch(true); e.QueueResearch(6); e.QueueResearch(14); e.QueueResearch(7); e.RemoveResearchQueue(0); e.RemoveResearchQueue(0); e.PauseResearch(false);
                check(e.Player.ScienceTree.ActiveProject == -1 && e.Player.ScienceTree.Queue.Single() == 7, "Removing prerequisite queue items never auto-unlocks their dependent discovery"); e.Validate(e.S);
            });
            scenario("Applied economy and construction", () => {
                var a = fresh(); var b = fresh(); rich(a); rich(b); a.ComputeEconomy(false); Learn(b.Player, 0); b.ComputeEconomy(false);
                check(Math.Abs(b.Player.Production[0] / a.Player.Production[0] - 1.05) < 1e-8, "Selection stations raise actual unconstrained food output by five percent");
                Learn(a.Player, 0); Learn(a.Player, 8); Learn(b.Player, 8); Learn(b.Player, 1); a.ComputeEconomy(false); b.ComputeEconomy(false);
                double foodDelta = b.Player.Production[0] - a.Player.Production[0];
                check(foodDelta > 0 && Math.Abs((b.Player.Consumed[2] - a.Player.Consumed[2]) - Rules.Inputs[0, 2] * (b.Player.Production[0] * .9 - a.Player.Production[0])) < 1e-7, "Agrarian mechanization changes actual industrial fuel consumption as well as output");
                Learn(a.Player, 12); Learn(b.Player, 12); a.ComputeEconomy(false); Learn(b.Player, 2); b.ComputeEconomy(false);
                // Give the baseline the same mechanization; cold chains only reduce household demand.
                Learn(a.Player, 1); a.ComputeEconomy(false);
                check(b.Player.Consumed[0] < a.Player.Consumed[0] && b.Player.Production[0] == a.Player.Production[0], "Cold chains reduce real household food consumption without reducing production");
                a = fresh(); b = fresh(); rich(a); rich(b); Learn(b.Player, 12);
                int region = world.Countries[a.S.Player].Regions[0];
                check(a.Build(region, 0) == null && b.Build(region, 0) == null && b.S.Projects.Last().Total < a.S.Projects.Last().Total && a.Player.Treasury == b.Player.Treasury, "Route planning shortens actual new construction while retaining its full price");
                check(a.BuildAirfield(region) == null && b.BuildAirfield(region) == null && b.S.Projects.Last().Total < a.S.Projects.Last().Total, "Construction research also shortens paid airfield projects");
                a = fresh(); b = fresh(); rich(a); rich(b); Learn(b.Player, 11);
                // Equal fundamentals isolate the applied production bonus.
                a.Player.Tech = (int[])b.Player.Tech.Clone();
                a.StartProduction(a.Player.ActiveDesign, 1, 4); b.StartProduction(b.Player.ActiveDesign, 1, 4);
                double steelA = a.Player.Stock[4], steelB = b.Player.Stock[4]; a.TickProduction(); b.TickProduction();
                var la = a.Player.Lines[0]; var lb = b.Player.Lines[0];
                check(Math.Abs(lb.Progress / la.Progress - 1.18) < 1e-8, "Applied machining, automation and additive production accelerate real equipment assembly by eighteen percent");
                check(Math.Abs(((steelB - b.Player.Stock[4]) / lb.Progress) / ((steelA - a.Player.Stock[4]) / la.Progress) - .95) < 1e-6, "Additive production reduces steel actually spent per equipment kit by five percent");
                a = fresh(); b = fresh(); Learn(b.Player, 19); a.Player.Tech = (int[])b.Player.Tech.Clone();
                check(Math.Abs(b.WeeklyResearchRate(b.S.Player, 0) / a.WeeklyResearchRate(a.S.Player, 0) - 1.08) < 1e-8, "Biomedical networks multiply the real funded research rate by eight percent");
            });
            scenario("Energy, materials and demographic consequences", () => {
                var a = fresh(); var b = fresh(); rich(a); rich(b);
                foreach (var d in TechnologyRules.Catalog.Take(24)) Learn(b.Player, d.Id);
                a.Player.Tech = (int[])b.Player.Tech.Clone(); a.ComputeEconomy(false); b.ComputeEconomy(false);
                check(Math.Abs(b.Player.Production[1] / a.Player.Production[1] - 1.21) < 1e-8 && Math.Abs(b.Player.Production[2] / a.Player.Production[2] - 1.08) < 1e-8 && Math.Abs(b.Player.Production[4] / a.Player.Production[4] - 1.06) < 1e-8, "Energy and alloy discoveries change actual electricity, fuel and steel output by their combined stated factors");
                double expected = Enumerable.Range(0, 8).Sum(i => Rules.Inputs[i, 1] * (b.Player.Production[i] * .92 - a.Player.Production[i]));
                check(Math.Abs(b.Player.Consumed[1] - a.Player.Consumed[1] - expected) < 1e-7, "Smart grids reduce actual industrial electricity consumption without applying the discount to households");
                a = fresh(); b = fresh(); rich(a); rich(b); Learn(b.Player, 19); a.Player.Tech = (int[])b.Player.Tech.Clone();
                int region = world.Countries[a.S.Player].Regions[0]; double population = a.S.Provinces[region].Population; week(a); week(b);
                check(Math.Abs((b.S.Provinces[region].Population - a.S.Provinces[region].Population) / population - .0005 / 52) < 1e-8, "Medical discoveries add their annual growth bonus to actual weekly province population");
            });
            scenario("Applied aviation", () => {
                Func<Engine, AirWing> ready = e => {
                    rich(e); int region = world.Countries[e.S.Player].Regions.First(r => world.Regions[r].Neighbors.Any(x => e.S.Provinces[x].Owner != e.S.Player)); e.S.Provinces[region].Airfield = 3;
                    var d = EquipmentRules.Balanced(EquipmentKind.Recon, e.Player, "Research flight"); e.Adopt(d); e.FindDesign(e.S.Player, e.Player.ActiveDesign).Stock = 1;
                    string error = e.DeployAirWing(e.Player.ActiveDesign, region); if (error != null) throw new Exception(error);
                    var wing = e.S.AirWings.Last(); wing.TrainingDays = 0; return wing;
                };
                var a = fresh(); var b = fresh(); var wa = ready(a); var wb = ready(b); Learn(b.Player, 13); Learn(b.Player, 22);
                int target = world.Regions[wa.BaseRegion].Neighbors.First(r => a.S.Provinces[r].Owner != a.S.Player), enemy = a.S.Provinces[target].Owner;
                a.Diplomacy(enemy, 3); b.Diplomacy(enemy, 3);
                check(a.SetAirMission(wa.Id, AirMission.Recon, target) == null && b.SetAirMission(wb.Id, AirMission.Recon, target) == null, "Applied aviation fixture issues actual valid reconnaissance missions");
                double fa = a.Player.Stock[2], fb = b.Player.Stock[2]; a.TickAviation(); b.TickAviation();
                check(wa.Sorties == 1 && wb.Sorties == 1 && Math.Abs((fb - b.Player.Stock[2]) / (fa - a.Player.Stock[2]) - .95) < 1e-6, "Fuel standards reduce fuel actually spent on aircraft sorties by five percent");
                check(Math.Abs(b.AirReconBonus(b.S.Player, target) / a.AirReconBonus(a.S.Player, target) - 1.08) < 1e-8, "Digital avionics increases the real reconnaissance effect by eight percent");
                Learn(b.Player, 17); int pa = wa.Personnel, pb = wb.Personnel; wa.CasualtyRemainder = wb.CasualtyRemainder = 0;
                a.DamageAirWing(wa, 20); b.DamageAirWing(wb, 20);
                check(Math.Abs((pb - wb.Personnel + wb.CasualtyRemainder) / (pa - wa.Personnel + wa.CasualtyRemainder) - .9) < 1e-8, "Field medicine also reduces actual aircrew casualties by ten percent"); a.Validate(a.S); b.Validate(b.S);
            });
            scenario("Medical and military consequences", () => {
                var a = fresh(); var b = fresh(); rich(a); rich(b); Learn(b.Player, 17);
                var ua = a.S.Armies.First(u => u.Owner == a.S.Player); var ub = b.S.Armies.First(u => u.Owner == b.S.Player);
                int pa = ua.Personnel, pb = ub.Personnel; a.DamageArmy(ua, 20); b.DamageArmy(ub, 20);
                check(Math.Abs((pb - ub.Personnel + ub.CasualtyRemainder) / (pa - ua.Personnel + ua.CasualtyRemainder) - .9) < 1e-8 && ua.Strength == ub.Strength, "Field medicine reduces personnel casualties by ten percent while equipment damage remains unchanged");
                b.DestroyArmy(ub); check(b.Player.PersonnelLosses == pb, "Destruction still accounts for every remaining person after medical casualty reduction");
                check(b.WeeklyIntake(b.S.Player) > a.WeeklyIntake(a.S.Player), "Public health raises actual weekly recruit intake within the existing population capacity");
                a = fresh(); b = fresh(); rich(a); rich(b); Learn(b.Player, 18); ua = a.S.Armies.First(u => u.Owner == a.S.Player); ub = b.S.Armies.First(u => u.Owner == b.S.Player);
                ua.Personnel -= 100; ub.Personnel -= 100; a.Player.PersonnelLosses += 100; b.Player.PersonnelLosses += 100;
                pa = ua.Personnel; pb = ub.Personnel; int ra = a.Player.Recruits, rb = b.Player.Recruits;
                a.TickManpower(); b.TickManpower();
                check(ub.Personnel - pb > ua.Personnel - pa && ra - a.Player.Recruits == ua.Personnel - pa && rb - b.Player.Recruits == ub.Personnel - pb && b.Player.Treasury < a.Player.Treasury, "Teaching hospitals speed real replacements while spending the corresponding recruits and money");
                a.Validate(a.S); b.Validate(b.S);
                a = fresh(); b = fresh(); rich(a); rich(b); Learn(b.Player, 13);
                // One day before the weekly economy: compare identical land maintenance directly.
                double fuelA = a.Player.Stock[2], fuelB = b.Player.Stock[2]; a.Tick(); b.Tick();
                check(Math.Abs((fuelB - b.Player.Stock[2]) / (fuelA - a.Player.Stock[2]) - .92) < 1e-7, "Fuel standards reduce actual daily ground-unit fuel use by eight percent");
                a = fresh(); b = fresh(); rich(a); rich(b); Learn(b.Player, 14); a.Tick(); b.Tick();
                check(Math.Abs(b.S.Armies.First(u => u.Owner == b.S.Player).SupplyDays - a.S.Armies.First(u => u.Owner == a.S.Player).SupplyDays - 2) < 1e-8, "Mobile depots add two actual days of autonomy to supplied ground units");
            });
            scenario("Module gates and legacy compatibility", () => {
                var e = fresh(); var n = e.Player; var existing = n.Design.Copy();
                for (int i = 0; i < 6; i++) n.Tech[i] = 6;
                check(Enumerable.Range(0, 8).All(i => TechnologyRules.ModuleCap(n, i) == 6), "Fundamental levels alone cannot bypass advanced applied module requirements");
                Learn(n, 20);
                check(TechnologyRules.ModuleCap(n, 0) == 7 && TechnologyRules.ModuleCap(n, 1) == 6, "Composite armor unlocks armor seven without granting advanced guns");
                Learn(n, 23);
                check(TechnologyRules.ModuleCap(n, 0) == 8 && TechnologyRules.ModuleCap(n, 1) == 8 && TechnologyRules.ModuleCap(n, 3) == 8 && TechnologyRules.ModuleCap(n, 2) == 7, "Integrated protection unlocks only its named level-eight modules");
                check(n.Design.Armor == existing.Armor && n.Design.Attack == existing.Attack, "New discoveries leave previously designed and issued equipment unchanged");
                n.Tech[5] = 4; check(TechnologyRules.ModuleCap(n, 0) == 6, "Applied discoveries still obey the underlying fundamental level cap");
                e = fresh(); n = e.Player; n.Tech[5] = 6; n.Tech[2] = 5; e.SelectResearch(2); n.ResearchProgress = 47.125;
                double money = n.Treasury; uint rng = e.S.RandomState; var caps = Enumerable.Range(0, 8).Select(i => Math.Min(8, 2 + n.Tech[TechnologyRules.ModuleBranch(i)])).ToArray();
                e.S.Version = 6; foreach (var c in e.S.Nations) c.ScienceTree = null;
                string file = Path.Combine(directory, "research-v6.xml"); rawSave(e, file); var loaded = fresh(); loaded.Load(file);
                check(loaded.S.Version == 10 && loaded.Player.ScienceTree.ActiveProject == 2 && loaded.Player.ResearchProgress == 47.125 && loaded.Player.Treasury == money && loaded.S.RandomState == rng, "Version-six migration preserves active fundamental progress, treasury and deterministic random state");
                check(caps.SequenceEqual(Enumerable.Range(0, 8).Select(i => TechnologyRules.ModuleCap(loaded.Player, i))) && !loaded.Player.ScienceTree.Known.Any(k => k), "Legacy migration preserves all previously available module levels without gifting applied technologies");
                loaded.Validate(loaded.S);
            });
            scenario("Research saves and invalid state", () => {
                var e = fresh(); rich(e); e.SelectResearch(0); e.Player.ResearchProgress = 23.75; e.SelectResearch(6); e.Player.ResearchProgress = 41.125; e.QueueResearch(14); e.QueueResearch(7); e.PauseResearch(true);
                string file = Path.Combine(directory, "research.xml"), copy = Path.Combine(directory, "research-copy.xml"); e.Save(file);
                var loaded = fresh(); loaded.Load(file); loaded.Save(copy);
                check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(copy)), "Full research save round-trips byte-identically with paused applied work, queued dependencies and stored progress");
                e.PauseResearch(false); loaded.PauseResearch(false); for (int i = 0; i < 150; i++) { e.Tick(); loaded.Tick(); }
                e.Save(file); loaded.Save(copy);
                check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(copy)), "Research decisions and AI remain byte-identical for 150 days after save/load");
                var original = loaded.S; string bad = Path.Combine(directory, "research-invalid.xml"); e.Player.ScienceTree.StoredProgress[29] = 12; rawSave(e, bad);
                bool rejected = false; try { loaded.Load(bad); } catch (InvalidDataException) { rejected = true; }
                check(rejected && ReferenceEquals(original, loaded.S), "A fabricated advanced cached progress entry is rejected without replacing the running campaign");
                e.Player.ScienceTree.StoredProgress[29] = 0; e.Player.ScienceTree.Queue.Add(99); rawSave(e, bad); rejected = false; try { loaded.Load(bad); } catch (InvalidDataException) { rejected = true; }
                check(rejected && ReferenceEquals(original, loaded.S), "Unknown queued project IDs cannot enter the active campaign through loading");
                e = fresh(); e.Player.ScienceTree.Known[23] = true; rejected = false; try { e.Validate(e.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Save validation rejects learned technologies without their prerequisites");
            });
            scenario("AI follows research prerequisites", () => {
                var e = fresh(); int owner = world.ByCode("POL"); var n = e.S.Nations[owner]; n.Focus = 5; n.Tech[5] = 1; n.Shortage = 0;
                e.PlanResearch(owner); check(n.ScienceTree.ActiveProject == 5 && !n.ScienceTree.Known[20], "AI starts the missing military foundation instead of receiving an advanced technology for free");
                for (int i = 0; i < 730; i++) e.Tick();
                check(e.S.Nations.Where((c, i) => i != e.S.Player).Sum(c => c.ScienceTree.Known.Count(k => k)) > 10, "Two-year simulation makes AI countries complete real applied research"); e.Validate(e.S);
                check(e.S.Nations.All(c => c.ScienceTree.Queue.Count <= 4), "Two-year research simulation retains valid bounded agendas in every country");
            });
        }
    }
}
