using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sovereign
{
    internal static class IntelligenceTests
    {
        internal static int RunSuite(World world, string directory)
        {
            Directory.CreateDirectory(directory); var rows = new List<string>(); int failures = 0;
            Run(world, directory, (ok, label) => { rows.Add((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; Console.WriteLine(rows.Last()); });
            rows.Add("Failures: " + failures); File.WriteAllLines(Path.Combine(directory, "intelligence-results.txt"), rows); return failures == 0 ? 0 : 1;
        }
        internal static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            scenario("Treaties and local contact", () => {
                var e = fresh(); int me = e.S.Player, other = world.ByCode("POL"), third = world.ByCode("DEU"), enemy = world.ByCode("RUS");
                check(e.S.Armies.Where(a => e.SharesMilitaryIntel(me, a.Owner)).All(a => a.Owner == me) && e.KnownMilitaryContacts(me).Count == 0, "A new peaceful campaign exposes only the player's own forces");
                e.S.Access[e.Pair(me, other)] = e.S.Agreements[e.Pair(me, other)] = true;
                check(!e.SharesMilitaryIntel(me, other), "Military access and trade alone grant no troop intelligence");
                check(e.Diplomacy(other, 9) != null && !e.SharesMilitaryIntel(me, other), "A low-relation intelligence treaty is rejected without revealing anything");
                e.S.Relations[e.Pair(me, other)] = e.S.Relations[e.Pair(other, me)] = 80;
                check(e.Diplomacy(other, 9) == null && e.SharesMilitaryIntel(me, other) && e.SharesMilitaryIntel(other, me), "An accepted intelligence treaty grants bilateral access to actual partner positions");
                int region = world.Countries[enemy].CapitalRegion;
                e.S.MilitaryContacts.Add(IntelligenceRules.Observe(other, enemy, region, 0, 1234, IntelSource.AirRecon));
                e.S.Alliances[e.Pair(other, third)] = e.S.Alliances[e.Pair(third, other)] = true;
                e.S.MilitaryContacts.Add(IntelligenceRules.Observe(third, enemy, world.Countries[enemy].Regions.First(r => r != region), 0, 4000, IntelSource.GroundContact));
                check(e.KnownMilitaryContacts(me).Count == 1 && !e.SharesMilitaryIntel(me, third), "Sharing includes first-hand partner observations but never chains through a partner's ally");
                e.Diplomacy(other, 9);
                check(!e.SharesMilitaryIntel(me, other) && e.KnownMilitaryContacts(me).Count == 0, "Revoking intelligence access removes partner positions and reports immediately");
                e.S.Alliances[e.Pair(me, other)] = e.S.Alliances[e.Pair(other, me)] = true;
                check(e.SharesMilitaryIntel(me, other) && e.KnownMilitaryContacts(me).Count == 1, "Defensive alliances include first-hand military information");
                e.Diplomacy(other, 5);
                check(!e.SharesMilitaryIntel(me, other) && e.KnownMilitaryContacts(me).Count == 0, "Breaking an alliance revokes its implicit information access");
                e.S.Relations[e.Pair(me, other)] = e.S.Relations[e.Pair(other, me)] = 80; e.Diplomacy(other, 9); e.Diplomacy(other, 3);
                check(e.AtWar(me, other) && !e.S.IntelligenceSharing[e.Pair(me, other)] && !e.S.IntelligenceSharing[e.Pair(other, me)], "Declaring war cancels the bilateral intelligence treaty");
                e.Validate(e.S);
            });
            scenario("Fog and observation lifetime", () => {
                var e = fresh(); int me = e.S.Player, enemy = world.ByCode("RUS");
                var ours = e.S.Armies.First(a => a.Owner == me); var theirs = e.S.Armies.First(a => a.Owner == enemy);
                int home = world.Countries[me].Regions.First(r => world.Regions[r].Neighbors.Any(n => world.Regions[n].Country == enemy));
                int border = world.Regions[home].Neighbors.First(r => world.Regions[r].Country == enemy);
                int hidden = world.Countries[enemy].CapitalRegion;
                foreach (var a in e.S.Armies.Where(a => a.Owner == me)) a.Region = home;
                foreach (var a in e.S.Armies.Where(a => a.Owner == enemy)) a.Region = hidden;
                theirs.Region = border; theirs.Personnel = 1234;
                e.Diplomacy(enemy, 3);
                var seen = e.KnownMilitaryContacts(me);
                check(!e.SharesMilitaryIntel(me, enemy) && seen.Count == 1 && seen[0].Region == border && seen.All(c => c.Region != hidden), "War reveals local enemy contact, never the enemy's complete force or rear deployment");
                check(seen[0].LowerPersonnel <= 1234 && seen[0].UpperPersonnel > 1234 && seen[0].LowerPersonnel != 1234, "Contact strength is a bounded estimate, not an exact formation count");
                double estimate = e.KnownEnemyPower(me)[border]; theirs.Personnel = 10; theirs.Proficiency = 0; theirs.Strength = 5;
                check(e.KnownEnemyPower(me)[border] == estimate, "Planning uses the observation and cannot read hidden live personnel or quality");
                theirs.Region = hidden;
                check(e.KnownMilitaryContacts(me)[0].Region == border && e.KnownMilitaryContacts(me)[0].UpperPersonnel == seen[0].UpperPersonnel, "A report stays at the observed position and never tracks a hidden movement");
                // Remove coverage without looking at the old area again.
                foreach (var a in e.S.Armies.Where(a => a.Owner == me)) a.Region = world.Countries[me].Regions.First(r => r != home && !world.Regions[r].Neighbors.Contains(border));
                e.S.Day = 2; e.RefreshGroundIntelligence();
                check(e.KnownMilitaryContacts(me).Any(c => c.Region == border && c.Day == 0), "Unobserved contacts retain their original date while aging");
                e.S.Day = 4; e.RefreshGroundIntelligence();
                check(!e.KnownMilitaryContacts(me).Any(c => c.Region == border), "Contacts expire after three days without confirmation");
                ours.Region = home; theirs.Region = border; e.RefreshGroundIntelligence(); theirs.Region = hidden; e.S.Day++; e.RefreshGroundIntelligence();
                check(!e.KnownMilitaryContacts(me).Any(c => c.Region == border), "Revisiting an empty area removes a disproven report before its expiry");
                theirs.Region = ours.Region; ours.TrainingDays = 1; e.RefreshGroundIntelligence();
                check(e.KnownMilitaryContacts(me).Any(c => c.Region == ours.Region), "Hostile troops inside controlled territory are detected by domestic surveillance");
            });
            scenario("Actual reconnaissance sorties", () => {
                var e = fresh(); int me = e.S.Player;
                int home = world.Countries[me].Regions.First(r => world.Regions[r].Neighbors.Any(n => world.Regions[n].Country != me));
                int target = world.Regions[home].Neighbors.First(r => world.Regions[r].Country != me), enemy = e.S.Provinces[target].Owner;
                e.Player.Treasury = 100; e.Player.Stock[2] = e.Player.Stock[7] = 100; e.S.Provinces[home].Airfield = 3;
                e.AdoptDesign(EquipmentRules.Balanced(EquipmentKind.Recon, e.Player, "Recon test"), me); var design = e.FindDesign(me, e.Player.ActiveDesign); design.Stock = 1;
                if (e.DeployAirWing(design.Id, home) != null) throw new Exception("Could not deploy recon wing");
                var wing = e.S.AirWings.Last(); wing.TrainingDays = 0;
                e.Diplomacy(enemy, 3); e.S.MilitaryContacts.Clear(); e.S.Armies.First(a => a.Owner == enemy).Region = target;
                check(e.SetAirMission(wing.Id, AirMission.Recon, target) == null && e.KnownMilitaryContacts(me).Count == 0, "Assigning reconnaissance does not instantly reveal troops");
                e.Player.Stock[2] = 0; e.TickAviation();
                check(wing.Sorties == 0 && e.KnownMilitaryContacts(me).Count == 0, "An unfueled reconnaissance wing discovers nothing");
                e.Player.Stock[2] = 100; e.Player.Treasury = 0; e.TickAviation();
                check(wing.Sorties == 0 && e.KnownMilitaryContacts(me).Count == 0, "An unfunded reconnaissance wing discovers nothing");
                e.Player.Treasury = 100; double fuel = e.Player.Stock[2]; e.TickAviation();
                check(wing.Sorties == 1 && e.Player.Stock[2] < fuel && e.KnownMilitaryContacts(me).Any(c => c.Region == target && c.Source == IntelSource.AirRecon), "A real funded sortie consumes fuel and records an aerial observation");
                int neutral = world.Countries[world.ByCode("MDA")].CapitalRegion;
                var hostile = e.S.Armies.First(a => a.Owner == enemy); hostile.Region = neutral;
                string reason = e.AirTargetReason(wing, AirMission.Recon, neutral); hostile.Region = target;
                check(reason == e.AirTargetReason(wing, AirMission.Recon, neutral), "Mission validation cannot be used to probe hidden enemies in neutral territory");
            });
            scenario("Quality and paid improvement", () => {
                var e = fresh(); var a = e.S.Armies.First(u => u.Owner == e.S.Player); var enemy = new Army();
                a.Proficiency = 40; a.Experience = 100; double regular = EquipmentRules.CombatPower(a, enemy, false);
                check(UnitQualityRules.Grade(a) == 2, "Experience alone cannot turn an untrained unit into elite troops");
                a.Proficiency = 90; a.Experience = 10;
                check(UnitQualityRules.Grade(a) == 4, "Elite status requires experience as well as professional training");
                a.Experience = 35;
                check(UnitQualityRules.Grade(a) == 5 && EquipmentRules.CombatPower(a, enemy, false) > regular, "Elite qualification has a real combat effect rather than cosmetic stars");
                double score = ReadinessRules.Score(a); a.Fatigue = 90;
                check(UnitQualityRules.Grade(a) == 5 && ReadinessRules.Score(a) < score, "Fatigue reduces readiness without erasing permanent qualification");
                a.Proficiency = 80; a.Experience = 60; a.Personnel = 1000; UnitQualityRules.IncorporateRecruits(a, 1000);
                check(a.Proficiency == 55 && a.Experience == 30, "Equal numbers of new recruits dilute training and experience by their actual personnel weight");
                e = fresh(); Tests.SmallPlayerForce(e); a = e.S.Armies.First(u => u.Owner == e.S.Player); a.Proficiency = 80; a.Experience = 60;
                e.Player.Treasury = e.Player.Stock[2] = e.Player.Stock[7] = 100;
                check(e.StartExercise(a.Id) == null, "Veterans can improve training beyond the exercise experience cap");
                e.S.Day = 1; double skill = a.Proficiency, cash = e.Player.Treasury; e.TickReadiness();
                check(a.Proficiency > skill && a.Experience == 60 && e.Player.Treasury < cash, "Advanced exercises charge money, increase training and never reduce combat-earned experience");
                double advanced = e.ExerciseDailyCost(a); a.Proficiency = 30;
                check(advanced > e.ExerciseDailyCost(a) * 2, "Maintaining an advanced training course costs more than basic instruction");
                int kenya = world.ByCode("KEN"); var african = e.S.Armies.First(u => u.Owner == kenya); african.Proficiency = 90; african.Experience = 35;
                check(UnitQualityRules.Grade(african) == 5 && e.S.Armies.Where(u => u.Owner == kenya).Select(u => u.Proficiency).Distinct().Count() > 1, "An African country can field elite troops and its own formations have different qualification levels");
                check(world.Countries[world.ByCode("USA")].TrainingBaseline > world.Countries[world.ByCode("SOM")].TrainingBaseline
                    && world.Countries.All(c => c.TrainingBaseline >= 30 && c.TrainingBaseline <= 70), "Scenario resource differences create bounded national baselines rather than universal identical forces");
            });
            scenario("Persistence and hostile input validation", () => {
                var e = fresh(); int me = e.S.Player, ally = world.ByCode("POL"), enemy = world.ByCode("RUS");
                e.S.Relations[e.Pair(me, ally)] = e.S.Relations[e.Pair(ally, me)] = 80; e.Diplomacy(ally, 9);
                e.S.MilitaryContacts.Add(IntelligenceRules.Observe(ally, enemy, world.Countries[enemy].CapitalRegion, 0, 4000, IntelSource.AirRecon));
                var army = e.S.Armies.First(a => a.Owner == me); army.Proficiency = 83.25;
                string path = Path.Combine(directory, "intelligence-save.xml"); e.Save(path); var loaded = new Engine(world) { AutoSaveEnabled = false }; loaded.Load(path);
                check(loaded.SharesMilitaryIntel(me, ally) && loaded.KnownMilitaryContacts(me).Count == 1 && loaded.S.Armies.First(a => a.Id == army.Id).Proficiency == 83.25, "Saving preserves treaties, observation snapshots and exact training progress");
                for (int i = 0; i < 10; i++) { e.Tick(); loaded.Tick(); }
                string again = Path.Combine(directory, "intelligence-reloaded.xml"); e.Save(path); loaded.Save(again);
                check(File.ReadAllText(path) == File.ReadAllText(again), "Intelligence and training continue deterministically after reload");
                e.S.Version = 14; double money = e.Player.Treasury, experience = army.Experience; int position = army.Region, count = e.S.Armies.Count;
                e.Save(path); loaded.Load(path);
                check(loaded.S.Version == Campaign.CurrentVersion && loaded.Player.Treasury == money && loaded.S.Armies.Count == count
                    && loaded.S.Armies.First(a => a.Id == army.Id).Experience == experience && loaded.S.Armies.First(a => a.Id == army.Id).Region == position
                    && loaded.S.MilitaryContacts.Count == 0 && !loaded.S.IntelligenceSharing.Any(v => v), "Format fourteen migrates without reshuffling armies, changing money or inventing treaties and observations");
                Action<Action> rejected = mutate => { loaded.Load(path); mutate(); bool failed = false; try { loaded.Validate(loaded.S); } catch (InvalidDataException) { failed = true; } check(failed, "Malformed military intelligence or training is rejected before use"); };
                rejected(() => loaded.S.IntelligenceSharing[e.Pair(me, ally)] = true);
                rejected(() => loaded.S.Armies[0].Proficiency = double.NaN);
                rejected(() => loaded.S.Armies[0].Proficiency = 101);
                rejected(() => loaded.S.MilitaryContacts.Add(IntelligenceRules.Observe(me, enemy, -1, 0, 10, IntelSource.GroundContact)));
                rejected(() => loaded.S.MilitaryContacts.Add(IntelligenceRules.Observe(me, enemy, 0, loaded.S.Day + 1, 10, IntelSource.GroundContact)));
                rejected(() => { var c = IntelligenceRules.Observe(me, enemy, 0, 0, 10, IntelSource.GroundContact); loaded.S.MilitaryContacts.Add(c); loaded.S.MilitaryContacts.Add(c); });
            });
        }
    }
}
