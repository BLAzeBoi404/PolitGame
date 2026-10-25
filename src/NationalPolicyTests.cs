using System;
using System.IO;
using System.Linq;

namespace Sovereign
{
    internal static class NationalPolicyTests
    {
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            Action<Engine> rich = e => { e.Player.Treasury = 10000; e.Player.Stability = 70; e.Player.Education = 80; for (int i = 0; i < 8; i++) e.Player.Stock[i] = 1000000; };
            Action<Engine, int> day = (e, d) => { e.S.Day = d; foreach (var n in e.S.Nations) n.Politics.NextEventDay = d + 60; };
            Func<Engine, PolicyEventKind, PolicyIncident> incident = (e, kind) => {
                if (e.S.Day < 28) e.S.Day = 28; e.Player.Politics.NextEventDay = e.S.Day;
                if (!e.OpenPolicyIncident(e.S.Player, kind)) throw new Exception("Expected incident: " + kind);
                return e.Player.Politics.Pending;
            };
            scenario("Decisions and state limits", () => {
                var e = fresh(); var n = e.Player;
                check(e.S.Version == 9 && e.S.Nations.All(c => c.Politics != null && c.Politics.Decisions.Count == 6 && c.Politics.Pending == null), "New campaign initializes a neutral cabinet for every country without free programs");
                n.Stability = 70; n.Treasury = 0; int history = n.Politics.History.Count;
                check(e.TakeDecision(NationalDecision.Industry) != null && n.Treasury == 0 && n.Politics.History.Count == history && n.Politics.Decisions.All(s => s.StartedDay == -1), "Unaffordable national decision changes neither resources nor timers");
                rich(e); double money = n.Treasury, cost = PolicyRules.DecisionCost(PolicyRules.Decisions[1], n);
                check(e.TakeDecision(NationalDecision.Industry) == null && Math.Abs(n.Treasury - money + cost) < 1e-8 && n.Politics.Decisions[1].ActiveUntil == 120, "Industrial program charges its quoted cost once and starts the exact duration");
                money = n.Treasury;
                check(e.TakeDecision(NationalDecision.Industry) != null && n.Treasury == money, "An active decision cannot be purchased twice");
                check(e.TakeDecision(NationalDecision.Grants) != null, "Research grants require an actual active research program");
                e.StartResearch(0); check(e.TakeDecision(NationalDecision.Grants) == null, "Research grants accept an eligible funded program");
                money = n.Treasury;
                check(e.TakeDecision(NationalDecision.Audit) != null && n.Treasury == money && n.Politics.Decisions.Count(s => s.ActiveUntil > e.S.Day) == 2, "The two-program cabinet limit is enforced before charging money");
                day(e, 120); e.TickNationalPolitics();
                check(PolicyRules.Effects(n, e.S.Day).GoodsFactor == 1 && e.DecisionReason(NationalDecision.Industry) != null, "A program stops exactly on its end date while its cooldown remains");
                day(e, 240); e.TickNationalPolitics();
                check(e.TakeDecision(NationalDecision.Industry) == null && n.Politics.Decisions[1].StartedDay == 240, "A completed program becomes available at its cooldown boundary");
                check(e.TakeDecision((NationalDecision)999) != null, "Unknown national decisions are rejected safely"); e.Validate(e.S);
            });
            scenario("Programs affect real production and research", () => {
                var a = fresh(); var b = fresh(); rich(a); rich(b); a.ComputeEconomy(false); double output = a.Player.Production[0];
                b.TakeDecision(NationalDecision.Industry);
                check(output > 0 && Math.Abs(b.Player.Production[0] / output - 1.06) < 1e-8, "Industrial program increases actual unconstrained goods output by six percent");
                a.StartProduction(a.Player.ActiveDesign, 1, 4); b.StartProduction(b.Player.ActiveDesign, 1, 4); a.TickProduction(); b.TickProduction();
                check(Math.Abs(b.Player.Lines[0].Progress / a.Player.Lines[0].Progress - 1.10) < 1e-8, "Industrial program accelerates actual equipment assembly by ten percent");
                check(b.Player.Stock[4] < a.Player.Stock[4] && b.Player.Treasury < a.Player.Treasury, "Faster equipment output still spends extra materials and money");
                a = fresh(); b = fresh(); rich(a); rich(b); a.ComputeEconomy(false); double revenue = a.Player.Revenue; b.TakeDecision(NationalDecision.Audit);
                check(Math.Abs(b.Player.Revenue / revenue - 1.05) < 1e-8 && b.Player.Stability == 67, "Audit changes real tax collection and applies its initial political cost");
                a = fresh(); b = fresh(); rich(a); rich(b); a.StartResearch(0); b.StartResearch(0); b.TakeDecision(NationalDecision.Grants); a.S.Day = b.S.Day = 6; a.Tick(); b.Tick();
                check(Math.Abs(b.Player.ResearchProgress / a.Player.ResearchProgress - 1.20) < 1e-8, "Grants multiply actual weekly research progress by twenty percent");
                a = fresh(); b = fresh(); rich(a); rich(b); a.Player.Approval = b.Player.Approval = 45; b.TakeDecision(NationalDecision.Relief);
                b.Player.Approval = a.Player.Approval; a.S.Day = b.S.Day = 6; a.Tick(); b.Tick();
                check(b.Player.Approval > a.Player.Approval, "Relief affects the weekly approval target beyond its immediate grant");
            });
            scenario("Bond funding and restrictions", () => {
                var e = fresh(); rich(e); var n = e.Player; double money = n.Treasury, debt = n.Debt;
                check(e.TakeDecision(NationalDecision.WarBonds) != null && n.Treasury == money && n.Debt == debt, "War bonds are unavailable in peacetime without side effects");
                e.Diplomacy(world.ByCode("POL"), 3); n.Debt = n.GDP * 1.49;
                check(e.TakeDecision(NationalDecision.WarBonds) != null, "Bond issuance respects the debt ceiling after the proposed loan");
                n.Debt = n.GDP * .45; e.ComputeEconomy(false); money = n.Treasury; debt = n.Debt; double spending = n.Spending, loan = PolicyRules.BondAmount(n);
                check(e.TakeDecision(NationalDecision.WarBonds) == null && Math.Abs(n.Treasury - money - loan) < 1e-8 && Math.Abs(n.Debt - debt - loan) < 1e-8, "Bond proceeds exactly equal the additional government debt");
                check(Math.Abs(n.Spending - spending - loan * n.Interest / 100 / 52) < 1e-8, "New bonds immediately enter ordinary weekly interest spending");
                check(e.TakeDecision(NationalDecision.Dialogue) != null, "Peacetime dialogue cannot start during a war");
            });
            scenario("Incident eligibility and atomic choices", () => {
                var e = fresh(); rich(e); var n = e.Player; n.Shortage = .2; uint rng = e.S.RandomState;
                check(!e.OpenPolicyIncident(e.S.Player, PolicyEventKind.Shortages), "The initial grace period prevents immediate crises");
                var ev = incident(e, PolicyEventKind.Shortages);
                check(ev.DeadlineDay - ev.CreatedDay == 21 && e.S.RandomState == rng && !e.OpenPolicyIncident(e.S.Player, PolicyEventKind.Investment), "Events retain a fixed deadline and never overwrite a pending choice or consume random draws");
                double quoted = PolicyRules.ChoiceCost(ev, PolicyRules.Events[0].Choices[1]); n.GDP *= 2;
                check(PolicyRules.ChoiceCost(ev, PolicyRules.Events[0].Choices[1]) == quoted, "Event costs retain their original economic quote as GDP changes");
                n.Treasury = 0; double food = n.Stock[0];
                check(e.ResolvePolicyIncident(ev.Id, 1) != null && n.Politics.Pending == ev && n.Stock[0] == food && n.Politics.History.Count == 0, "An unaffordable incident choice preserves pending event and all resources");
                n.Treasury = 100; n.Stock[0] = 0; double money = n.Treasury;
                check(e.ResolvePolicyIncident(ev.Id, 1) != null && n.Treasury == money, "Reserve distribution requires real food stocks before charging money");
                n.Stock[0] = 100; n.Stock[6] = 100; double approval = n.Approval;
                check(e.ResolvePolicyIncident(ev.Id, 1) == null && Math.Abs(n.Treasury - money + quoted) < 1e-8 && Math.Abs(n.Stock[0] - (100 - ev.Population * .1)) < 1e-8 && Math.Abs(n.Stock[6] - (100 - ev.Population * .04)) < 1e-8 && n.Approval == approval + 6, "Reserve distribution spends quoted money and actual goods and applies its stated consequence");
                money = n.Treasury; food = n.Stock[0];
                check(e.ResolvePolicyIncident(ev.Id, 1) != null && n.Treasury == money && n.Stock[0] == food, "A resolved event cannot be claimed again");
                check(n.Politics.Pending == null && n.Politics.EventCooldowns[0] == e.S.Day + 180 && n.Politics.NextEventDay == e.S.Day + 60, "Incident resolution starts national and per-event cooldowns");
                e.Validate(e.S);
            });
            scenario("Rationing and automatic default", () => {
                var e = fresh(); rich(e); e.Player.Shortage = .2; var ev = incident(e, PolicyEventKind.Shortages); e.ComputeEconomy(false);
                double demand = e.Player.Demand[0], population = e.Player.Population, food = e.Player.Stock[0]; e.ResolvePolicyIncident(ev.Id, 2);
                check(Math.Abs(demand - e.Player.Demand[0] - population * .72 * .15) < 1e-8 && e.Player.Stock[0] == food, "Rationing reduces actual household food demand without creating or consuming phantom stocks");
                int until = e.Player.Politics.Modifiers[0].UntilDay; day(e, until); e.TickNationalPolitics(); e.ComputeEconomy(false);
                check(e.Player.Politics.Modifiers.Count == 0 && Math.Abs(e.Player.Demand[0] - demand) < 1e-8, "Expired rationing restores ordinary food demand exactly");
                e = fresh(); e.Player.Shortage = .2; ev = incident(e, PolicyEventKind.Shortages); double approval = e.Player.Approval;
                e.Player.Treasury = -1; day(e, ev.DeadlineDay); e.TickNationalPolitics();
                check(e.Player.Politics.Pending == null && e.Player.Treasury == -1 && e.Player.Approval == approval - 4 && e.Player.Politics.History[0].Outcome.StartsWith("Срок истёк"), "An ignored event applies the advertised free default on its deadline without erasing a negative treasury");
                int history = e.Player.Politics.History.Count; e.TickNationalPolitics();
                check(e.Player.Politics.History.Count == history, "A deadline consequence is applied only once"); e.Validate(e.S);
            });
            scenario("All incident families and live restrictions", () => {
                foreach (PolicyEventKind kind in Enum.GetValues(typeof(PolicyEventKind))) {
                    var e = fresh(); rich(e); var n = e.Player; day(e, 90);
                    n.Shortage = .2; n.Approval = 40; n.Stability = 50; n.Military = 5; e.StartResearch(0);
                    if (kind == PolicyEventKind.WarWeariness) { e.Diplomacy(world.ByCode("POL"), 3); e.S.WarStarted[e.Pair(e.S.Player, world.ByCode("POL"))] = e.S.WarStarted[e.Pair(world.ByCode("POL"), e.S.Player)] = 0; }
                    if (kind == PolicyEventKind.Investment) n.Stability = 70;
                    var ev = incident(e, kind);
                    check(e.ResolvePolicyIncident(ev.Id, 1) == null && n.Politics.History.Any(h => h.Title == PolicyRules.Events[(int)kind].Name), "Conditional event resolves through real engine rules: " + kind); e.Validate(e.S);
                }
                var research = fresh(); rich(research); research.StartResearch(0); var proposal = incident(research, PolicyEventKind.ResearchOffer);
                check(research.ResolvePolicyIncident(proposal.Id, 2) != null, "Scientific exchange needs an actual peaceful trade partner");
                int partner = world.ByCode("POL"); research.S.Agreements[research.Pair(research.S.Player, partner)] = research.S.Agreements[research.Pair(partner, research.S.Player)] = true;
                research.Player.Research = -1;
                check(research.ResolvePolicyIncident(proposal.Id, 2) != null, "An event option rechecks research prerequisites at the moment of choice");
                research.Player.Research = 0;
                check(research.ResolvePolicyIncident(proposal.Id, 2) == null && PolicyRules.Effects(research.Player, research.S.Day).ResearchFactor == 1.12, "Eligible exchange installs its limited research modifier");
                var conquered = fresh(); day(conquered, 35); conquered.Player.Shortage = .2; foreach (var p in conquered.S.Provinces.Where(p => p.Owner == conquered.S.Player)) p.Owner = partner;
                conquered.Player.Politics.NextEventDay = 0;
                check(!conquered.OpenPolicyIncident(conquered.S.Player, PolicyEventKind.Shortages) && conquered.TakeDecision(NationalDecision.Relief) != null, "Countries without controlled territory cannot issue new programs or incidents");
            });
            scenario("Persistence, migration and corrupt records", () => {
                var e = fresh(); rich(e); e.TakeDecision(NationalDecision.Industry); e.Player.Shortage = .2; var pending = incident(e, PolicyEventKind.Shortages);
                string path = Path.Combine(directory, "national-policy.xml"); e.Save(path); var loaded = fresh(); loaded.Load(path);
                check(loaded.Player.Politics.Pending.Id == pending.Id && loaded.Player.Politics.Pending.Economy == pending.Economy && loaded.Player.Politics.Decisions[1].ActiveUntil == 120, "Pending quotes, decision deadlines and cabinet history survive saving");
                e.ResolvePolicyIncident(pending.Id, 2); loaded.ResolvePolicyIncident(pending.Id, 2);
                for (int d = 0; d < 150; d++) { e.Tick(); loaded.Tick(); }
                string second = Path.Combine(directory, "national-policy-copy.xml"); e.Save(path); loaded.Save(second);
                check(File.ReadAllText(path) == File.ReadAllText(second), "Full campaign remains byte-identical across saved policy choices and 150 subsequent days");
                string valid = File.ReadAllText(path); var oldCampaign = loaded.S;
                var corrupt = fresh(); rich(corrupt); corrupt.TakeDecision(NationalDecision.Industry); corrupt.Player.Politics.Decisions[1].ActiveUntil++; bool rejected = false;
                try { corrupt.Validate(corrupt.S); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Save validation rejects fabricated program duration");
                File.WriteAllText(path, valid.Replace("<NextIncident>", "<NextIncident>-")); rejected = false;
                try { loaded.Load(path); } catch { rejected = true; }
                check(rejected && ReferenceEquals(oldCampaign, loaded.S), "A corrupt political save cannot replace the running campaign");
                var legacy = fresh(); legacy.S.Version = 5; var cash = legacy.Player.Treasury; var reserve = legacy.Player.Recruits; uint rng = legacy.S.RandomState; legacy.Player.Politics = null; legacy.Save(path); loaded.Load(path);
                check(loaded.S.Version == 9 && loaded.Player.Treasury == cash && loaded.Player.Recruits == reserve && loaded.S.RandomState == rng && loaded.Player.Politics.Decisions.All(s => s.StartedDay == -1), "Version-five migration preserves economy, manpower and random state and grants no free programs");
            });
            scenario("AI uses real cabinet rules", () => {
                var e = fresh(); int owner = world.ByCode("POL"); var n = e.S.Nations[owner]; n.Treasury = 100; n.Stability = 70; n.Approval = 70; n.Shortage = 0; n.Research = -1;
                double before = n.Treasury, cost = PolicyRules.DecisionCost(PolicyRules.Decisions[1], n); e.PlanNationalPolicy(owner);
                check(n.Politics.Decisions[1].StartedDay == 0 && Math.Abs(n.Treasury - before + cost) < 1e-8, "AI funds the same industrial program at the same cost as the player");
                e = fresh(); n = e.S.Nations[owner]; n.Treasury = 0; n.Shortage = .2; day(e, 28); n.Politics.NextEventDay = 0;
                e.TickNationalPolitics();
                check(n.Politics.Pending == null && n.Politics.Modifiers.Any(m => m.Kind == PolicyModifierKind.Rationing) && n.Treasury == 0, "An insolvent AI chooses an affordable crisis response without free emergency goods");
                for (int d = 0; d < 730; d++) { e.Tick(); if (d % 30 == 0) e.Validate(e.S); }
                check(e.S.Nations.All(c => c.Politics.History.Count <= 20 && c.Politics.Decisions.Count(s => s.ActiveUntil > e.S.Day) <= 2) && e.S.Nations.Any(c => c.Politics.History.Count > 0), "Two-year political simulation retains bounded histories and valid cabinet limits for all countries");
            });
        }
    }
}
