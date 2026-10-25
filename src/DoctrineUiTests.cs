using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestDoctrineInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            Action<string, bool> confirmClick = (label, accept) => { confirmationForTests = text => accept; try { click(label); } finally { confirmationForTests = null; } };
            menuCountry = W.ByCode("UKR"); BeginCampaign(); ClientSize = new Size(1600, 900); speed = 0;
            click("nav:Армия"); click("doctrine:open");
            check(doctrinePage && detailPanel && !ResearchCanvasActive, "Army command opens doctrine trees without enabling science canvas gestures");
            E.Player.Treasury = 10000; E.Player.Education = 60; E.Player.Military = 2.5;
            click("doctrine:stage:2"); click("doctrine:start");
            check(E.Player.Doctrines.Training == -1, "Locked doctrine stages cannot start through the interface");
            click("doctrine:required:70");
            check(!doctrinePage && researchSelection == 76 && researchBranch == 5, "Doctrine prerequisite opens the exact network-coordination technology");
            click("doctrine:open"); click("doctrine:foundation");
            check(!doctrinePage && researchSelection == 5 && researchTab == 1, "Doctrine foundation control opens military fundamentals");
            click("doctrine:open"); click("doctrine:stage:0"); double cash = E.Player.Treasury; click("doctrine:start");
            check(E.Player.Doctrines.Training == 0 && E.Player.Treasury < cash, "Doctrine start button orders a real paid staff course");
            E.Tick(); double work = E.Player.Doctrines.Work; click("doctrine:pause"); E.Tick();
            check(E.Player.Doctrines.Paused && E.Player.Doctrines.Work == work, "Doctrine pause control stops actual course progress");
            click("doctrine:pause"); E.Player.Doctrines.Work = 89.5; E.Tick(); cash = E.Player.Treasury; confirmClick("doctrine:adopt", false);
            check(E.Player.Doctrines.Active[0] == -1 && E.Player.Treasury == cash, "Declining adoption leaves the school and treasury unchanged");
            confirmClick("doctrine:adopt", true);
            check(E.Player.Doctrines.Levels[0] == 1 && E.Player.Doctrines.Active[0] == 0 && E.Player.Treasury < cash && DoctrineRules.Effects(E.Player, E.S.Day).Attack == 0, "Adoption control pays for retraining rather than instantly granting combat bonuses");
            click("doctrine:service:1"); click("doctrine:stage:12"); click("doctrine:start"); cash = E.Player.Treasury; confirmClick("doctrine:cancel", false);
            check(E.Player.Doctrines.Training == 12, "Declining cancellation preserves the running course");
            confirmClick("doctrine:cancel", true);
            check(E.Player.Doctrines.Training == -1 && E.Player.Treasury == cash, "Cancelling through the interface clears the course without refunding payment");
            toastUntil = DateTime.MinValue;
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size;
                for (int service = 0; service < 2; service++) {
                    click("doctrine:service:" + service);
                    for (int id = service * 9; id < service * 9 + 9; id++) {
                        click("doctrine:stage:" + id);
                        check(doctrineSelection == id, "Doctrine stage " + id + " is selectable at " + size.Width + "x" + size.Height);
                        using (var frame = RenderFrame()) { if (id % 9 == 8) frame.Save(Path.Combine(directory, "doctrines-" + service + "-" + size.Width + ".png")); }
                        var controls = hits.Where(h => h.Label != null && h.Label.StartsWith("doctrine:")).ToArray();
                        check(controls.All(h => h.Rect.Left >= 0 && h.Rect.Right <= ExpandedWidth && h.Rect.Bottom <= VH - 34) && !controls.Any(h => controls.Any(other => other != h && h.Rect.IntersectsWith(other.Rect))), "Doctrine stage " + id + " controls stay visible and disjoint at " + size.Width);
                    }
                }
            }
            ClientSize = new Size(1600, 900); click("doctrine:research");
            check(!doctrinePage && page == "Исследования", "Return button restores the scientific research workspace");
            click("nav:Карта"); click("nav:Исследования"); click("doctrine:open");
            check(doctrinePage && detailPanel, "Compact science panel also opens doctrine management");
            E.Validate(E.S); speed = 0;
        }
    }
}
