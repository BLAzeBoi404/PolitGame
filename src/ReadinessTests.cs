using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sovereign
{
    internal static class ReadinessTests
    {
        internal static int RunSuite(World world, string directory)
        {
            Directory.CreateDirectory(directory); var rows = new List<string>(); int failures = 0;
            Run(world, directory, (ok, name) => { string line = (ok ? "PASS " : "FAIL ") + name; rows.Add(line); Console.WriteLine(line); if (!ok) failures++; });
            rows.Add("Failures: " + failures); File.WriteAllLines(Path.Combine(directory, "readiness-results.txt"), rows); return failures == 0 ? 0 : 1;
        }
        internal static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => {
                var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); Tests.SmallPlayerForce(e);
                e.Player.Treasury = 100; e.Player.Stock[2] = e.Player.Stock[7] = 100;
                return e;
            };
            Func<Engine, Army> first = e => e.S.Armies.First(a => a.Owner == e.S.Player);
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            scenario("Paid exercises", () => {
                var e = fresh(); var a = first(e); double cash = e.Player.Treasury, fuel = e.Player.Stock[2], arms = e.Player.Stock[7]; long people = e.ServicePersonnel(e.S.Player);
                check(e.StartExercise(a.Id) == null && a.ExerciseRemaining == 21 && e.Player.Treasury == cash && a.Experience == 0, "Starting exercises creates no instant experience and makes no advance charge");
                check(e.StartExercise(a.Id) != null && a.ExerciseRemaining == 21, "Repeated training commands cannot reset an active course or grant experience");
                double cost = e.ExerciseDailyCost(a); e.S.Day = 1; e.TickReadiness();
                check(Math.Abs(e.Player.Treasury - (cash - cost)) < 1e-9 && Math.Abs(e.Player.Stock[2] - (fuel - ReadinessRules.ExerciseFuel(a))) < 1e-9 && Math.Abs(e.Player.Stock[7] - (arms - ReadinessRules.ExerciseArms(a))) < 1e-9, "One actual exercise day debits the quoted money, fuel and arms exactly once");
                check(a.Experience > 0 && a.Fatigue > 0 && a.Organization < 100 && a.ExerciseRemaining == 20 && e.ServicePersonnel(e.S.Player) == people, "Paid training increases proficiency while reducing immediate readiness without inventing personnel");
                cash = e.Player.Treasury; double experience = a.Experience; e.TickReadiness();
                check(cash == e.Player.Treasury && a.Experience == experience && a.ExerciseRemaining == 20, "A repeated readiness tick on the same day cannot duplicate training or charges");
                double fatigue = a.Fatigue; check(e.StopExercise(a.Id) == null && e.Player.Treasury == cash && a.Experience == experience && a.Fatigue == fatigue, "Cancelling exercises preserves spent money, earned experience and fatigue");
                check(e.StopExercise(a.Id) != null && e.Player.Treasury == cash, "Repeated cancellation grants no refund"); e.Validate(e.S);
            });
            scenario("Shortages and limits", () => {
                var e = fresh(); var a = first(e); e.StartExercise(a.Id); double xp = a.Experience;
                e.Player.Stock[2] = 0; e.S.Day = 1; e.TickReadiness();
                check(a.ExerciseRemaining == 21 && a.Experience == xp && e.Player.Treasury == 100, "A fuel shortage pauses the course without charging or granting experience");
                e.PrepareTradeNeeds(); double plannedFuel = e.Player.Trade.Operations[2], plannedArms = e.Player.Trade.Operations[7]; e.StopExercise(a.Id); e.PrepareTradeNeeds();
                check(plannedFuel > e.Player.Trade.Operations[2] && plannedArms > e.Player.Trade.Operations[7], "A stalled exercise course requests its real additional inputs from the international market");
                e.Player.Stock[2] = 100; e.StartExercise(a.Id); e.Player.Treasury = 0; e.S.Day++; e.TickReadiness();
                check(a.ExerciseRemaining == 21 && a.Experience == xp && e.Player.Stock[2] == 100, "An unfunded course consumes neither materials nor training time");
                e.Player.Treasury = 100; e.Player.Stock[7] = 0; e.S.Day++; e.TickReadiness();
                check(a.ExerciseRemaining == 21 && a.Experience == xp && e.Player.Treasury == 100, "An arms shortage also pauses a fully fueled course atomically");
                e.Player.Stock[7] = 100; a.Proficiency = 90; a.Experience = 34.9; e.S.Day++; e.TickReadiness(); e.S.Day++; e.TickReadiness();
                check(a.Experience == 35 && a.ExerciseRemaining == 0 && e.StartExercise(a.Id) != null, "Exercises stop once both training 90 and experience 35 are reached");
                a.Experience = 0; a.Fatigue = 51; check(e.StartExercise(a.Id) != null, "Exhausted formations must rest before starting another course");
                a.Fatigue = 0; a.TrainingDays = 14; check(e.StartExercise(a.Id) != null, "Initial formation and refit training cannot overlap exercises"); a.TrainingDays = 0;
                var foreign = e.S.Armies.First(x => x.Owner != e.S.Player); check(e.StartExercise(foreign.Id) != null && foreign.ExerciseRemaining == 0, "A player cannot assign exercises to another country's formation");
                e.Validate(e.S);
            });
            scenario("Complete course, rest and orders", () => {
                var e = fresh(); var a = first(e); e.StartExercise(a.Id);
                for (int day = 1; day <= 21; day++) { e.S.Day = day; e.TickReadiness(); }
                check(a.ExerciseRemaining == 0 && a.Experience > 10 && a.Experience < 35 && a.Fatigue > 20, "A complete course requires twenty-one paid days and leaves a real recovery requirement");
                double xp = a.Experience; for (int day = 0; day < 16; day++) { e.S.Day++; e.TickReadiness(); e.TickManpower(); }
                check(a.Fatigue == 0 && a.Experience == xp && a.Organization > 90, "Supplied rest restores fatigue and organization while retaining learned proficiency");
                e.StartExercise(a.Id); int target = world.Regions[a.Region].Neighbors.First(r => e.S.Provinces[r].Owner == e.S.Player);
                check(e.Order(a.Id, a.Region) != null && a.ExerciseRemaining == 21, "A rejected movement command does not silently cancel exercises");
                check(e.Order(a.Id, target) == null && a.ExerciseRemaining == 0, "An accepted movement order stops exercises through the ordinary command path");
                e.S.Day++; e.TickReadiness(); check(a.Fatigue > 0, "Marching generates fatigue rather than counting as restorative rest"); e.Validate(e.S);
            });
            scenario("Battle readiness", () => {
                var e = fresh(); var a = first(e); var enemy = e.S.Armies.First(x => x.Owner == world.ByCode("POL"));
                double rested = EquipmentRules.CombatPower(a, enemy, false), score = ReadinessRules.Score(a); a.Fatigue = 90;
                check(EquipmentRules.CombatPower(a, enemy, false) < rested * .7 && ReadinessRules.Score(a) < score, "Fatigue reduces actual combat power and the displayed readiness score");
                int other = world.ByCode("POL"); e.Diplomacy(other, 3);
                int border = world.Countries[e.S.Player].Regions.First(r => world.Regions[r].Neighbors.Any(n => e.S.Provinces[n].Owner == other));
                int target = world.Regions[border].Neighbors.First(r => e.S.Provinces[r].Owner == other); a.Region = border;
                check(e.Order(a.Id, target) != null, "Severely fatigued troops cannot start a fresh offensive through manual orders");
                a.Fatigue = 0; check(e.StartExercise(a.Id) != null, "A formation beside an enemy-controlled front cannot start exercises");
                foreach(var otherUnit in e.S.Armies.Where(x => x.Region == target && x.Id != enemy.Id).ToArray())e.DestroyArmy(otherUnit); enemy.Region = target; enemy.Target = -1;
                e.Order(a.Id, target); a.Days = .01; e.Tick();
                check(a.Fatigue > .35 && enemy.Fatigue > 0, "A real battle tires both attacker and defender, independently of casualties");
            });
            scenario("Exhausted ongoing attack", () => {
                var e = fresh(); var a = first(e); int enemy = world.ByCode("POL"); e.Diplomacy(enemy, 3);
                int border = world.Countries[e.S.Player].Regions.First(r => world.Regions[r].Neighbors.Any(n => e.S.Provinces[n].Owner == enemy));
                int target = world.Regions[border].Neighbors.First(r => e.S.Provinces[r].Owner == enemy); a.Region = border;
                e.Order(a.Id, target); a.Fatigue = 95; e.Tick();
                check(a.Target == -1 && a.Route.Count == 0, "An ongoing offensive stops when the formation reaches critical fatigue, allowing recovery");
                e.Validate(e.S);
            });
            scenario("War exhaustion and recovery", () => {
                var e = fresh(); var a = first(e); int other = world.ByCode("POL"); e.Diplomacy(other, 3);
                for (int day = 1; day <= 100; day++) { e.S.Day = day; e.TickReadiness(); }
                check(e.Player.Condition.Exhaustion > 1, "A prolonged war accumulates national exhaustion even without new casualties");
                double before = e.Player.Condition.Exhaustion; e.DamageArmy(a, 20); e.S.Day++; e.TickReadiness(); double afterLoss = e.Player.Condition.Exhaustion;
                check(afterLoss > before + .1 && e.Player.Condition.AccountedLosses == e.Player.PersonnelLosses, "Actual personnel losses add exhaustion through the conserved casualty ledger");
                e.S.Day++; e.TickReadiness(); check(e.Player.Condition.Exhaustion - afterLoss < .1, "The same casualties are not charged again on following days");
                e.Player.Condition.Exhaustion = 0; e.ComputeEconomy(false); double output = e.Player.Production.Sum(), power = e.GroundCombatFactor(e.S.Player, false); int intake = e.WeeklyIntake(e.S.Player);
                e.Player.Condition.Exhaustion = 80; e.ComputeEconomy(false);
                check(e.Player.Production.Sum() < output && e.WeeklyIntake(e.S.Player) < intake && e.GroundCombatFactor(e.S.Player, false) < power, "War exhaustion constrains real production, recruitment throughput and combat effectiveness");
                e.Diplomacy(other, 4); before = e.Player.Condition.Exhaustion; e.S.Day++; e.TickReadiness();
                check(e.Player.Condition.Exhaustion < before && e.Player.Condition.Exhaustion > 0, "Peace starts gradual recovery instead of instantly erasing the costs of war"); e.Validate(e.S);
            });
            scenario("Preparation AI", () => {
                var e = fresh(); int owner = world.ByCode("POL"); var n = e.S.Nations[owner];
                n.Treasury = 10000; n.Stock[2] = n.Stock[7] = 10000; var troops = e.S.Armies.Where(a => a.Owner == owner).ToList();
                foreach (var a in troops) { a.Experience = 0; a.Fatigue = 0; a.Organization = a.Supply = a.Strength = 100; }
                double money = n.Treasury; e.PlanMilitaryPreparation();
                check(troops.Any(a => a.ExerciseRemaining > 0) && troops.Any(a => a.ExerciseRemaining == 0) && n.Treasury == money, "AI schedules affordable exercises for part of its force while keeping an operational reserve");
                var trained = troops.First(a => a.ExerciseRemaining > 0); e.S.Day = 1; e.TickReadiness();
                check(n.Treasury < money && trained.Experience > 0 && trained.Fatigue > 0, "AI exercises pay the same daily costs and suffer the same fatigue as the player's");
                e.Diplomacy(owner, 3);
                check(troops.All(a => a.ExerciseRemaining == 0), "An attacked AI stops exercises immediately rather than waiting for the weekly planning date");
                e.PlanMilitaryPreparation();
                check(troops.All(a => a.ExerciseRemaining == 0) && n.Trade.ReserveWeeks >= 4, "A new war cancels AI exercises and raises its real import reserve target");
                n.Stock[2] = 0; check(e.AssessMilitary(owner).Posture == "Обеспечение" && !e.CanPrepareWar(owner, world.ByCode("DEU")), "AI identifies operational shortages and cannot authorize an unfunded new war");
                e = fresh(); owner = world.ByCode("POL"); n = e.S.Nations[owner]; n.Treasury = 0; e.PlanMilitaryPreparation();
                check(e.S.Armies.Where(a => a.Owner == owner).All(a => a.ExerciseRemaining == 0), "An insolvent AI does not acquire free military training");
            });
            scenario("Material war preparation", () => {
                var e = fresh(); int owner = world.ByCode("POL"), enemy = world.ByCode("LUX"); var n = e.S.Nations[owner];
                n.Treasury = 10000; n.Stock[2] = n.Stock[7] = 10000;
                foreach(var a in e.S.Armies.Where(a => a.Owner == owner)) { a.Organization = a.Strength = a.Supply = 100; a.Fatigue = 0; }
                check(e.CanPrepareWar(owner, enemy), "A funded, supplied and ready army with a real force advantage can pass the AI preparation assessment");
                n.Stock[2] = 0; check(!e.CanPrepareWar(owner, enemy), "A fuel-starved army cannot pass war preparation even when GDP and money are high"); n.Stock[2] = 10000;
                n.Stock[7] = 0; check(!e.CanPrepareWar(owner, enemy), "An army without its arms reserve cannot pass war preparation"); n.Stock[7] = 10000;
                n.Condition.Exhaustion = 40; check(!e.CanPrepareWar(owner, enemy), "National war exhaustion blocks AI plans for a new offensive war"); n.Condition.Exhaustion = 0;
                double spending = n.Spending; n.Spending = n.Revenue + 1000000;
                check(!e.CanPrepareWar(owner, enemy), "War preparation accounts for the running national budget deficit as well as unit upkeep"); n.Spending = spending;
                int ally = world.ByCode("USA"); e.S.Alliances[e.Pair(enemy,ally)] = e.S.Alliances[e.Pair(ally,enemy)] = true;
                check(!e.CanPrepareWar(owner, enemy), "AI force assessment includes the target's actual defensive allies");
            });
            scenario("Persistence and migration", () => {
                var e = fresh(); var a = first(e); e.StartExercise(a.Id); e.S.Day = 1; e.TickReadiness(); e.Player.Condition.Exhaustion = 12;
                string path = Path.Combine(directory, "readiness-save.xml"); e.Save(path); var loaded = new Engine(world) { AutoSaveEnabled = false }; loaded.Load(path);
                check(first(loaded).ExerciseRemaining == a.ExerciseRemaining && first(loaded).Fatigue == a.Fatigue && loaded.Player.Condition.Exhaustion == 12, "Saving preserves exercise progress, fatigue and national exhaustion");
                for (int day = 0; day < 35; day++) { e.Tick(); loaded.Tick(); }
                string path2 = Path.Combine(directory, "readiness-reloaded.xml"); e.Save(path); loaded.Save(path2);
                check(File.ReadAllText(path) == File.ReadAllText(path2), "Training, fatigue, trade and AI continue bit-for-bit identically after loading");
                e = fresh(); a = first(e); e.DamageArmy(a, 10); long losses = e.Player.PersonnelLosses; double treasury = e.Player.Treasury, xp = a.Experience; int region = a.Region;
                e.S.Version = 13; e.Player.Condition = null; e.Save(path); loaded.Load(path);
                check(loaded.S.Version == Campaign.CurrentVersion && loaded.Player.Condition.AccountedLosses == losses && loaded.Player.Condition.Exhaustion == 0 && loaded.Player.Treasury == treasury && first(loaded).Region == region && first(loaded).Experience == xp, "Format thirteen migrates without charging historic casualties or changing money, experience and deployments");
                loaded.Player.Condition.Exhaustion = double.NaN; bool rejected = false; try { loaded.Validate(loaded.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Nonfinite military exhaustion is rejected before replacing a campaign");
                loaded.Player.Condition.Exhaustion = 0; first(loaded).ExerciseRemaining = 22; rejected = false; try { loaded.Validate(loaded.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Invalid course duration cannot enter a saved campaign");
            });
        }
    }
}
