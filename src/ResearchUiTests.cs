using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestResearchInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            menuCountry = W.ByCode("UKR"); BeginCampaign(); ClientSize = new Size(1600, 900); speed = 0;
            click("nav:Исследования"); click("Технологии и план →");
            check(page == "Исследования" && detailPanel, "Compact science command panel opens the actual technology workspace");
            click("research:tab:0"); click("research:branch:0"); click("technology:0"); click("research:start");
            check(E.Player.ScienceTree.ActiveProject == 6, "A technology card and start button select real applied research");
            E.Player.ResearchProgress = 31.5; click("research:pause");
            check(E.Player.ScienceTree.Paused, "Research pause button changes the actual research agenda"); click("research:pause");
            click("research:tab:1"); click("foundation:2"); click("research:start");
            check(E.Player.ScienceTree.ActiveProject == 2 && E.Player.ScienceTree.StoredProgress[6] == 31.5, "Fundamental tab switches the active program and retains applied progress");
            click("research:tab:0"); click("research:branch:0"); click("technology:0"); click("research:start");
            check(E.Player.ResearchProgress == 31.5 && E.Player.ScienceTree.ActiveProject == 6, "Returning through the interface restores the exact stored applied progress");
            click("technology:1"); click("research:prerequisite:8");
            check(researchBranch == 2 && researchSelection == 14 && researchTab == 0, "Cross-branch prerequisite button navigates to the required technology card");
            click("research:enqueue"); click("research:branch:0"); click("technology:1"); click("research:enqueue");
            check(E.Player.ScienceTree.Queue.SequenceEqual(new[] { 14, 7 }), "UI plan admits dependencies supplied by the active and preceding queued programs");
            click("research:tab:2"); click("research:remove:0");
            check(E.Player.ScienceTree.Queue.Single() == 7, "Removing a prerequisite from the visible plan retains its dependent work for later");
            click("research:tab:0"); click("research:branch:5"); click("technology:23");
            var active = E.Player.ScienceTree.ActiveProject; click("research:start");
            check(E.Player.ScienceTree.ActiveProject == active && !E.Player.ScienceTree.Known[23], "Locked advanced technology cannot start through its disabled interface button");
            toastUntil = DateTime.MinValue;
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size;
                for (int branch = 0; branch < 6; branch++) {
                    OpenResearchProject(branch * 4 + 9);
                    using (var frame = RenderFrame()) { if (branch == 5) frame.Save(Path.Combine(directory, "research-tree-" + size.Width + ".png")); }
                    check(hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1), "Technology branch " + branch + " stays within the viewport at " + size.Width + "x" + size.Height);
                    var start = hits.Single(h => h.Label == "research:start");
                    check(hits.Where(h => h.Label != null && h.Label.StartsWith("research:prerequisite:")).All(h => !h.Rect.IntersectsWith(start.Rect)), "Prerequisite controls do not overlap the research action at " + size.Width + " branch " + branch);
                }
                for (int tab = 1; tab < 3; tab++) {
                    researchTab = tab; researchSelection = tab == 1 ? 5 : 29;
                    using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "research-tab-" + tab + "-" + size.Width + ".png"));
                    check(hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1), "Research tab " + tab + " stays within the viewport at " + size.Width + "x" + size.Height);
                }
            }
            ClientSize = new Size(1600, 900); Navigate("Конструктор"); detailPanel = true; focusedModule = 0; draft.Kind = EquipmentKind.Tank;
            click("Исследования модулей →");
            check(page == "Исследования" && detailPanel && researchSelection == 26, "Designer links its capped armor module to the actual composite-armor technology");
            click("research:required-foundation");
            check(researchTab == 1 && researchSelection == 5, "Technology foundation requirement opens the correct fundamental branch");
            E.Validate(E.S); speed = 0;
        }
    }
}
