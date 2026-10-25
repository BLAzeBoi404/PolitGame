using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestNationalPolicyInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            menuCountry = W.ByCode("UKR"); BeginCampaign(); ClientSize = new Size(1600, 900);
            E.Player.Treasury = 100; E.Player.Stability = 70;
            click("nav:Политика"); click("Решения и события →");
            check(nationalPolicyPage && detailPanel && page == "Политика" && speed == 0, "Politics navigation opens the cabinet over the map and pauses simulation");
            click("policy:select:1"); double cash = E.Player.Treasury, cost = PolicyRules.DecisionCost(PolicyRules.Decisions[1], E.Player);
            click("policy:take");
            check(E.Player.Politics.Decisions[1].StartedDay == E.S.Day && Math.Abs(cash - E.Player.Treasury - cost) < 1e-8, "Cabinet button starts the selected program and charges the actual quoted price");
            cash = E.Player.Treasury; click("policy:take");
            check(E.Player.Treasury == cash && E.Player.Politics.History.Count == 1, "A disabled active-program button cannot charge the player twice");
            click("policy:tab:2"); check(policyTab == 2 && PolicyRules.Effects(E.Player, E.S.Day).GoodsFactor == 1.06, "Active-measures tab represents the real industrial modifier");
            E.S.Day = 28; E.Player.Shortage = .2; E.Player.Politics.NextEventDay = 28;
            E.OpenPolicyIncident(E.S.Player, PolicyEventKind.Shortages); speed = 3; E.Raise();
            check(speed == 0 && policyTab == 1 && nationalPolicyPage, "A new incident pauses fast-forward and opens its actual choice panel");
            speed = 1; E.Raise(); check(speed == 1, "Reviewing an already announced incident does not repeatedly force a pause"); speed = 0;
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size;
                for (int tab = 0; tab < 3; tab++) {
                    policyTab = tab;
                    using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "cabinet-" + tab + "-" + size.Width + ".png"));
                    check(hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1), "Cabinet tab " + tab + " stays within the viewport at " + size.Width + "x" + size.Height);
                }
            }
            policyTab = 1; int id = E.Player.Politics.Pending.Id;
            E.Player.Treasury = 0; click("policy:choice:1");
            check(E.Player.Politics.Pending.Id == id && E.Player.Treasury == 0, "An unaffordable event option is disabled in the actual interface");
            E.Player.Treasury = 100; E.Player.Stock[0] = E.Player.Stock[6] = 100; var ev = E.Player.Politics.Pending; cost = PolicyRules.ChoiceCost(ev, PolicyRules.Events[0].Choices[1]);
            click("policy:choice:1");
            check(E.Player.Politics.Pending == null && Math.Abs(E.Player.Treasury - (100 - cost)) < 1e-8 && E.Player.Politics.Modifiers.Any(m => m.Kind == PolicyModifierKind.EmergencyRelief), "A real event click pays for reserve distribution and installs its time-limited result");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "cabinet-history.png"));
            check(E.Player.Politics.History[0].Title == "Перебои с товарами", "Resolved choices appear in persistent cabinet history");
            foreach (var kind in new[] { PolicyModifierKind.Guarantees, PolicyModifierKind.ResearchFund, PolicyModifierKind.HomeSupport }) E.Player.Politics.Modifiers.Add(new NationalModifier { Kind = kind, StartedDay = E.S.Day, UntilDay = E.S.Day + PolicyRules.Modifiers[(int)kind].Duration });
            ClientSize = new Size(1280, 720); click("policy:tab:2");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "cabinet-measures-many.png"));
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, (int)(500 * UiScale), (int)(500 * UiScale), -120));
            check(scroll == 1 && E.Player.Politics.Modifiers.Count == 4, "Mouse wheel scrolls a long active-measures list without changing campaign effects");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "cabinet-measures-scroll.png"));
            click("Бюджет и налоги →"); check(!nationalPolicyPage && !detailPanel && page == "Политика", "Cabinet navigation returns to working budget controls");
            Navigate("Карта"); E.S.Provinces[W.Countries[E.S.Player].Regions[0]].Public[7] += 2;
            for (int i = 0; i < 2; i++) { string error = E.StartProduction(E.Player.ActiveDesign, 1, 2); if (error != null) throw new InvalidOperationException("Outliner fixture: " + error); }
            using (var frame = RenderFrame()) { }
            var policyHit = hits.Single(h => h.Label == "policy:outliner");
            var series = hits.Where(h => h.Label == E.Player.Design.Name + "  0/2").ToList();
            check(series.Count == 2 && series.All(h => !h.Rect.IntersectsWith(policyHit.Rect)), "Cabinet outliner shortcut does not overlap either of two production rows");
            click("policy:outliner"); check(nationalPolicyPage && policyTab == 0, "The current-affairs outliner opens national programs");
            E.Validate(E.S); speed = 0;
        }
    }
}
