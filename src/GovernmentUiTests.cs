using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestGovernmentInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            Action<string, bool> confirmClick = (label, accept) => { confirmationForTests = text => accept; try { click(label); } finally { confirmationForTests = null; } };
            Action<int> days = count => { for (int i = 0; i < count; i++) { E.S.Day++; E.TickGovernments(); } };
            menuCountry = W.ByCode("UKR"); BeginCampaign(); ClientSize = new Size(1600, 900); speed = 0;
            E.Player.Treasury = 1000; E.Player.Approval = E.Player.Stability = 70;
            click("nav:Политика"); click("government:open");
            check(governmentPage && detailPanel && !nationalPolicyPage && speed == 0, "Compact politics opens real constitutional management over the map");
            Action<string> bounds = label => {
                using (var frame = RenderFrame()) { }
                var controls = hits.Where(h => h.Label != null && h.Label.StartsWith("government:")).ToArray();
                check(controls.All(h => h.Rect.Left >= 0 && h.Rect.Right <= ExpandedWidth && h.Rect.Bottom <= VH - 34) && !controls.Any(h => controls.Any(other => other != h && h.Rect.IntersectsWith(other.Rect))), label);
            };
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size;
                for (int form = 1; form <= 4; form++) {
                    click("government:form:" + form); check(governmentSelection == form, "Government form " + form + " is selectable at " + size.Width);
                    bounds("Government form " + form + " action bounds remain visible and disjoint at " + size.Width);
                    if (form == 3) using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "government-forms-" + size.Width + ".png"));
                }
            }
            ClientSize = new Size(1600, 900); click("government:form:1"); double cash = E.Player.Treasury;
            confirmClick("government:start", false); check(E.Player.Government.Target == -1 && E.Player.Treasury == cash, "Declining constitutional reform preserves money and government");
            E.Player.Stability = 40; confirmClick("government:start", true); check(E.Player.Government.Target == -1 && E.Player.Treasury == cash, "Disabled reform controls cannot bypass stability requirements"); E.Player.Stability = 70;
            confirmClick("government:start", true);
            check(E.Player.Government.Target == 1 && governmentAgenda && E.Player.Treasury < cash, "Accepting reform starts a real paid project and opens its progress panel");
            days(10); click("government:pause"); days(3);
            check(E.Player.Government.Paused && E.Player.Government.Work == 10, "Reform pause button stops actual constitutional work");
            confirmClick("government:cancel", false); check(E.Player.Government.Target == 1, "Declining withdrawal retains the active reform");
            toastUntil = DateTime.MinValue;
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size; bounds("Paused reform controls remain visible at " + size.Width);
                using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "government-draft-" + size.Width + ".png"));
            }
            ClientSize = new Size(1600, 900); click("government:pause"); days(35); speed = 3; E.Raise();
            check(E.Player.Government.Phase == 1 && governmentAgenda && governmentPage && speed == 0, "A new constitutional convention pauses time and opens its actual decision panel");
            speed = 1; E.Raise(); check(speed == 1, "An already announced constitutional convention does not repeatedly force a pause"); speed = 0;
            click("nav:Карта"); click("government:outliner");
            check(governmentPage && governmentAgenda && speed == 0, "The map outliner returns to an unanswered constitutional convention");
            toastUntil = DateTime.MinValue;
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size; bounds("Convention choices remain visible and disjoint at " + size.Width);
                using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "government-convention-" + size.Width + ".png"));
            }
            ClientSize = new Size(1600, 900); cash = E.Player.Treasury;
            confirmClick("government:choice:0", false); check(E.Player.Government.Phase == 1 && E.Player.Treasury == cash, "Declining paid consultation preserves the unanswered convention");
            confirmClick("government:choice:0", true); check(E.Player.Government.Phase == 2 && E.Player.Government.ChoicePaid > 0 && E.Player.Treasury < cash, "Consultation control pays its quote and starts sixty-day implementation");
            days(15); toastUntil = DateTime.MinValue;
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size; bounds("Implementation controls remain visible at " + size.Width);
                using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "government-implementation-" + size.Width + ".png"));
            }
            days(45); ClientSize = new Size(1600, 900); click("government:compare");
            check(E.Player.Government.Form == 1 && !governmentAgenda, "Completed constitutional process returns to comparison with the new actual government");
            E.Validate(E.S);
            click("Кабинет →"); click("government:open"); check(governmentPage, "Cabinet management links back to constitutional reform");
            menuCountry = W.ByCode("UKR"); BeginCampaign(); E.Player.Treasury = 1000; E.Player.Approval = E.Player.Stability = 70; OpenGovernment();
            confirmClick("government:start", true); cash = E.Player.Treasury; confirmClick("government:cancel", true);
            check(E.Player.Government.Target == -1 && E.Player.Treasury == cash, "Accepting withdrawal through the interface retains its sunk cost and clears the project");
            E.Validate(E.S); speed = 0;
        }
    }
}
