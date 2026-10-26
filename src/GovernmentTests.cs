using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Sovereign
{
    internal static class GovernmentTests
    {
        // A completed-form fixture isolates permanent effects from the transition's paid costs.
        private static void SetForm(Engine e, int form)
        {
            e.S.Day = 75; e.Player.Government.Form = form; e.Player.Government.ChangedDay = 75; e.Player.Government.LastStartedDay = 0;
        }
        public static void Run(World world, string directory, Action<bool, string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) { AutoSaveEnabled = false }; e.New(world.ByCode("UKR"), 2026, 1); e.Player.Treasury = 10000; e.Player.Stability = e.Player.Approval = 70; e.Player.Education = 60; e.Player.Science = 3; for (int i = 0; i < 8; i++) e.Player.Stock[i] = 1000000; return e; };
            Action<string, Action> scenario = (name, run) => { try { run(); } catch (Exception ex) { check(false, name + ": " + ex); } };
            Action<Engine, int> days = (e, count) => { for (int i = 0; i < count; i++) { e.S.Day++; e.TickGovernments(); } };
            Action<Engine, string> raw = (e, file) => { using (var stream = File.Create(file)) new XmlSerializer(typeof(Campaign)).Serialize(stream, e.S); };
            scenario("Constitutional reform orders", () => {
                var e = fresh(); var n = e.Player; var s = n.Government; double cash = n.Treasury; uint rng = e.S.RandomState;
                check(e.S.Version == 11 && e.S.Nations.All(c => c.Government.Form == 0 && c.Government.Target == -1), "Every new country starts with neutral existing institutions rather than an invented historical government");
                check(e.StartGovernmentReform(0) != null && e.StartGovernmentReform(5) != null && e.StartGovernmentReform(-1) != null && n.Treasury == cash && s.Target == -1 && e.S.RandomState == rng, "Unknown and neutral reform targets are rejected without changing the campaign");
                n.Treasury = 0; check(e.StartGovernmentReform(1) != null, "Unfunded constitutional reform cannot begin"); n.Treasury = cash;
                n.Stability = 49; check(e.StartGovernmentReform(1) != null, "Reform requires fifty stability"); n.Stability = 70;
                n.Approval = 49; check(e.StartGovernmentReform(1) != null, "Reform requires fifty approval"); n.Approval = 70;
                for (int form = 1; form <= 4; form++) { n.Repression = GovernmentRules.Forms[form].PressureLimit + 1; check(e.StartGovernmentReform(form) != null, "Form " + form + " enforces its own pressure limit before charging"); } n.Repression = 0;
                int enemy = world.ByCode("POL"); e.Diplomacy(enemy, 3); check(e.StartGovernmentReform(1) != null, "A country at war cannot begin constitutional reform");
                e = fresh(); n = e.Player; s = n.Government; cash = n.Treasury; rng = e.S.RandomState; double quoted = n.GDP;
                check(e.StartGovernmentReform(1) == null && Math.Abs(cash - n.Treasury - GovernmentRules.ReformCost(quoted)) < 1e-8 && s.Paid == GovernmentRules.ReformCost(quoted) && s.QuotedGDP == quoted, "Starting reform charges and stores the exact quoted cost once");
                check(n.Approval == 68 && n.Stability == 67 && s.Form == 0 && s.Target == 1 && s.LastStartedDay == 0 && e.S.RandomState == rng, "Reform applies its initial political cost without selecting a government or consuming random numbers");
                cash = n.Treasury; check(e.StartGovernmentReform(2) != null && n.Treasury == cash && s.Target == 1, "A parallel reform cannot replace or recharge the active project");
                e.Tick(); check(s.Work == 1 && s.Phase == 0, "An actual world tick advances constitutional preparation by one workday");
                e.PauseGovernmentReform(true); e.Tick(); check(s.Work == 1, "Manual reform pause preserves preparation");
                e.PauseGovernmentReform(false); n.Stability = 34; e.Tick(); check(s.Work == 1, "A stability crisis halts preparation");
                n.Stability = 70; n.Approval = 29; e.Tick(); check(s.Work == 1, "An approval crisis halts preparation");
                n.Approval = 70; n.Repression = 31; e.Tick(); check(s.Work == 1, "Exceeding the target constitution's pressure limit halts preparation");
                n.Repression = 0; e.Diplomacy(enemy, 3); e.Tick(); check(s.Work == 1, "War pauses an existing reform without discarding its paid progress");
                cash = n.Treasury; e.CancelGovernmentReform(); check(s.Target == -1 && s.Work == 0 && n.Treasury == cash && GovernmentRules.Effects(n).Output == 0, "Withdrawal works during war, forfeits payment and removes transitional penalties"); e.Validate(e.S);
            });
            scenario("Consultation deadlines and adoption routes", () => {
                foreach (int choice in new[] { 0, 1 }) {
                    var e = fresh(); var n = e.Player; var s = n.Government; e.StartGovernmentReform(1); double quote = s.QuotedGDP; days(e, 44);
                    check(s.Phase == 0 && s.Work == 44 && s.Form == 0, "Preparation does not finish before its 45th working day, route " + choice);
                    days(e, 1); check(s.Phase == 1 && s.Work == 0 && s.ConsultationDay == 45 && s.Form == 0, "Preparation opens a dated convention without auto-adopting the project, route " + choice);
                    double cash = n.Treasury; check(e.ResolveGovernmentConvention(s.StartedDay + 1, choice) != null && e.ResolveGovernmentConvention(s.StartedDay, 3) != null && n.Treasury == cash, "Stale convention references and unknown answers cannot charge money");
                    check(e.PauseGovernmentReform(true) != null && !s.Paused, "A pending response deadline cannot be frozen by the reform pause control");
                    n.Treasury = 0; check(e.GovernmentChoiceReason(s.StartedDay, 0) != null && e.GovernmentChoiceReason(s.StartedDay, 1) == null && e.GovernmentChoiceReason(s.StartedDay, 2) == null, "An empty treasury blocks paid consultation while retaining fast adoption and withdrawal"); n.Treasury = cash;
                    n.GDP *= 2; cash = n.Treasury;
                    check(e.ResolveGovernmentConvention(s.StartedDay, choice) == null && s.Phase == 2 && s.Work == 0 && Math.Abs(cash - n.Treasury - (choice == 0 ? GovernmentRules.ConsultationCost(quote) : 0)) < 1e-8, "Convention uses the original economic quote and starts an empty implementation stage, route " + choice);
                    cash = n.Treasury; check(e.ResolveGovernmentConvention(0, choice) != null && n.Treasury == cash, "A resolved convention cannot be applied or charged twice");
                    int duration = GovernmentRules.ImplementationDays(choice), leader = n.Leader; double approval = n.Approval, stability = n.Stability;
                    days(e, duration - 1); check(s.Form == 0 && s.Target == 1, "The old government remains until the final implementation day, route " + choice);
                    days(e, 1); check(s.Form == 1 && s.Target == -1 && s.ChangedDay == e.S.Day && n.Leader == leader + 1 && s.Work == 0 && s.Paid == 0 && !s.Paused, "Completion selects one new government, appoints one cabinet and clears transition state, route " + choice);
                    check(n.Approval == approval + (choice == 0 ? 4 : -4) && n.Stability == stability + (choice == 0 ? 3 : -3), "Only completed implementation applies its declared political outcome, route " + choice);
                    check(Math.Abs(GovernmentRules.Effects(n).Research - .08) < 1e-8 && GovernmentRules.Effects(n).Output == 0 && e.StartGovernmentReform(1) != null, "Permanent government effects replace transitional penalties and the same government cannot be readopted"); e.Validate(e.S);
                }
                var expired = fresh(); expired.StartGovernmentReform(2); days(expired, 45); double money = expired.Player.Treasury;
                days(expired, 20); check(expired.Player.Government.Phase == 1 && expired.GovernmentChoiceReason(0, 1) == null, "Convention remains answerable on day twenty of its response window");
                days(expired, 1); check(expired.Player.Government.Target == -1 && expired.Player.Government.Form == 0 && expired.Player.Treasury == money && expired.Player.Politics.History[0].Outcome.Contains("истёк"), "Unanswered convention withdraws exactly at its deadline without refund or forced adoption"); expired.Validate(expired.S);
            });
            scenario("Territory, cooldown and every form", () => {
                var e = fresh(); e.StartGovernmentReform(3); days(e, 10); var s = e.Player.Government;
                foreach (var p in e.S.Provinces.Where(p => p.Owner == e.S.Player)) p.Owner = world.ByCode("POL");
                days(e, 5); check(s.Work == 10, "Losing all territory freezes an active constitutional project"); e.CancelGovernmentReform();
                e = fresh(); e.StartGovernmentReform(2); e.CancelGovernmentReform(); e.Player.Approval = e.Player.Stability = 70; days(e, 364);
                check(e.StartGovernmentReform(3) != null, "Cancelled reforms retain the full 365-day cooldown"); days(e, 1);
                check(e.StartGovernmentReform(3) == null && e.Player.Government.LastStartedDay == 365, "A new project may begin exactly on the cooldown boundary"); e.Validate(e.S);
                for (int form = 1; form <= 4; form++) {
                    e = fresh(); e.StartGovernmentReform(form); days(e, 45); e.ResolveGovernmentConvention(0, 1); days(e, 30); e.Validate(e.S);
                    check(e.Player.Government.Form == form && e.Player.Government.ChangedDay == 75, "Government form " + form + " is reachable through the complete paid process");
                }
                e = fresh(); e.StartGovernmentReform(2); days(e, 45); e.ResolveGovernmentConvention(0, 1); e.PauseGovernmentReform(true); days(e, 365); e.PauseGovernmentReform(false); days(e, 30); e.Player.Stability = e.Player.Approval = 70;
                check(e.StartGovernmentReform(3) == null, "A long-paused reform can be followed by a new project when its original cooldown has elapsed"); e.Validate(e.S);
            });
            scenario("Government changes real economic systems", () => {
                var a = fresh(); var b = fresh(); SetForm(b, 1); a.S.Day = b.S.Day; a.ComputeEconomy(false); b.ComputeEconomy(false);
                check(Math.Abs(b.Player.Revenue / a.Player.Revenue - .97) < 1e-8, "Parliamentary institutions reduce actual tax collection by three percent");
                a.SelectResearch(0); b.SelectResearch(0); a.S.Day = b.S.Day = 76; a.Tick(); b.Tick();
                check(Math.Abs(b.Player.ResearchProgress / a.Player.ResearchProgress - 1.08) < 1e-8, "Parliamentary institutions accelerate real funded weekly research by eight percent");
                check(Math.Abs((b.Player.Approval - a.Player.Approval) - .28) < 1e-7, "Parliamentary approval changes the real weekly target rather than granting an instant bonus");
                a = fresh(); b = fresh(); SetForm(b, 3); a.S.Day = b.S.Day; a.ComputeEconomy(false); b.ComputeEconomy(false);
                check(Math.Abs(b.Player.Production[0] / a.Player.Production[0] - 1.03) < 1e-8 && Math.Abs(b.Player.Revenue / a.Player.Revenue - .96) < 1e-8, "Constitutional monarchy changes actual goods output and tax collection together");
                a.S.Day = b.S.Day = 76; a.Tick(); b.Tick();
                check(Math.Abs(b.Player.Stability - a.Player.Stability - (b.Player.Approval - a.Player.Approval + 4) * .06) < 1e-7, "Constitutional monarchy raises the real weekly stability target by four points");
                foreach (int form in new[] { 2, 4 }) {
                    a = fresh(); b = fresh(); SetForm(b, form); a.S.Day = b.S.Day;
                    a.StartProduction(a.Player.ActiveDesign, 1, 4); b.StartProduction(b.Player.ActiveDesign, 1, 4); a.TickProduction(); b.TickProduction();
                    check(Math.Abs(b.Player.Lines[0].Progress / a.Player.Lines[0].Progress - (form == 2 ? 1.06 : 1.08)) < 1e-8, "Government form " + form + " accelerates actual equipment assembly by its declared factor");
                    check(b.Player.Stock[4] < a.Player.Stock[4], "Faster government-backed assembly pays for its additional steel, form " + form);
                }
                a = fresh(); b = fresh(); SetForm(b, 4); a.S.Day = b.S.Day; a.SelectResearch(0); b.SelectResearch(0); a.S.Day = b.S.Day = 76; a.Tick(); b.Tick();
                check(Math.Abs(b.Player.ResearchProgress / a.Player.ResearchProgress - .94) < 1e-8, "Centralized institutions pay their six-percent research penalty in real weekly progress");
                a = fresh(); b = fresh(); a.TakeDecision(NationalDecision.Industry); b.TakeDecision(NationalDecision.Industry); b.StartGovernmentReform(1); a.ComputeEconomy(false); b.ComputeEconomy(false);
                check(Math.Abs(b.Player.Production[0] / a.Player.Production[0] - 1.03 / 1.06) < 1e-8 && Math.Abs(b.Player.Revenue / a.Player.Revenue - .96) < 1e-8, "Transitional output and tax costs combine with real cabinet programs while preparation runs");
                a.SelectResearch(0); b.SelectResearch(0); check(Math.Abs(b.WeeklyResearchRate(b.S.Player, 0) / a.WeeklyResearchRate(a.S.Player, 0) - .95) < 1e-8, "Reform bureaucracy reduces actual research speed during preparation");
                b.PauseGovernmentReform(true); b.ComputeEconomy(false); check(Math.Abs(b.Player.Production[0] / a.Player.Production[0] - 1.03 / 1.06) < 1e-8, "Pausing reform does not remove its ongoing economic penalties"); b.CancelGovernmentReform();
                check(b.Player.Production[0] == a.Player.Production[0], "Withdrawal immediately removes transitional output costs from the economy preview");
            });
            scenario("Government persistence and invalid state", () => {
                string file = Path.Combine(directory, "government.xml"), copy = Path.Combine(directory, "government-copy.xml"), invalid = Path.Combine(directory, "government-invalid.xml");
                foreach (int phase in new[] { 0, 1, 2 }) {
                    var e = fresh(); e.StartGovernmentReform(3); days(e, phase == 0 ? 12 : 45);
                    if (phase == 2) { e.ResolveGovernmentConvention(0, 0); days(e, 8); }
                    if (phase != 1) e.PauseGovernmentReform(true); e.Save(file); var loaded = fresh(); loaded.Load(file); loaded.Save(copy);
                    check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(copy)), "Government phase " + phase + " round-trips with exact payment, dates and pause state");
                    var before = loaded.S; int originalPhase = e.Player.Government.Phase; e.Player.Government.Phase = 5; raw(e, invalid); bool rejected = false; try { loaded.Load(invalid); } catch (InvalidDataException) { rejected = true; }
                    check(rejected && ReferenceEquals(before, loaded.S), "Invalid government phase cannot replace the running campaign"); e.Player.Government.Phase = originalPhase;
                    e.Player.Government.Paid += .10; raw(e, invalid); rejected = false; try { loaded.Load(invalid); } catch (InvalidDataException) { rejected = true; }
                    check(rejected && ReferenceEquals(before, loaded.S), "Inconsistent government preparation payment is rejected atomically");
                }
                var continued = fresh(); continued.StartGovernmentReform(1); days(continued, 45); continued.ResolveGovernmentConvention(0, 0); continued.Save(file);
                var other = fresh(); other.Load(file); for (int i = 0; i < 150; i++) { continued.Tick(); other.Tick(); } continued.Save(file); other.Save(copy);
                check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(copy)), "Reform completion and AI remain byte-identical for 150 actual world days after save/load");
                Action<string, Action<GovernmentState>> rejectState = (label, mutate) => {
                    var e = fresh(); mutate(e.Player.Government); raw(e, invalid); var before = other.S; bool rejected = false; try { other.Load(invalid); } catch (InvalidDataException) { rejected = true; } check(rejected && ReferenceEquals(before, other.S), label);
                };
                rejectState("Unknown government forms are rejected before loading", s => s.Form = 99);
                rejectState("A fabricated adopted government without dates is rejected", s => s.Form = 1);
                rejectState("Nonfinite constitutional payment is rejected", s => s.Paid = double.NaN);
                rejectState("Stale idle reform progress is rejected", s => s.Work = 1);
                rejectState("Future reform cooldown dates are rejected", s => s.LastStartedDay = 2);
                var legacy = fresh(); DoctrineTests.Learn(legacy.Player, 0, 1); legacy.AdoptDoctrine(0); legacy.SelectResearch(0); legacy.Player.ResearchProgress = 37.25;
                double cash = legacy.Player.Treasury; uint rng = legacy.S.RandomState; legacy.S.Version = 9; foreach (var n in legacy.S.Nations) n.Government = null; raw(legacy, file); other.Load(file);
                check(other.S.Version == 11 && other.Player.Treasury == cash && other.S.RandomState == rng && other.Player.ResearchProgress == 37.25 && other.Player.Doctrines.Active[0] == 0 && other.S.Nations.All(n => n.Government.Form == 0 && n.Government.Target == -1), "Version-nine migration preserves money, research, doctrine adoption and RNG without inventing governments"); other.Validate(other.S);
            });
            scenario("AI constitutional reform", () => {
                var e = fresh(); int owner = world.ByCode("POL"); var n = e.S.Nations[owner]; n.Treasury = 10000; n.Stability = n.Approval = 70; n.Repression = 0;
                double cash = n.Treasury; e.PlanGovernment(owner);
                check(n.Government.Target > 0 && n.Government.Form == 0 && Math.Abs(cash - n.Treasury - n.Government.Paid) < 1e-8, "AI orders and pays for constitutional preparation through the same rules");
                days(e, 45); check(n.Government.Phase == 1, "AI reaches a real convention after paid preparation"); days(e, 1);
                check(n.Government.Phase == 2 && n.Government.Choice == 0 && n.Government.ChoicePaid > 0 && n.Government.Work == 0, "AI pays for consultation without receiving a free implementation day"); days(e, 60);
                check(n.Government.Form > 0 && n.Government.Target == -1 && e.Player.Government.Form == 0, "AI completes its constitutional project without choosing a government for the player");
                for (int i = 0; i < 730; i++) e.Tick(); e.Validate(e.S);
                check(e.S.Nations.Count(c => c.Government.Form > 0) > 10 && e.S.Nations.All(c => c.Politics.History.Count <= 20), "Two-year simulation completes multiple AI government reforms and preserves bounded political histories");
            });
        }
    }
}
