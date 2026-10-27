using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestExpandedResearchInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            menuCountry = W.ByCode("UKR"); BeginCampaign(); clock.Stop(); ClientSize = new Size(1600, 900); OpenResearchProject(30);
            using (var frame = RenderFrame()) { }
            float mapX = cx; int choice = researchSelection; var point = new Point((int)((researchViewport.X + 330) * UiScale), (int)((researchViewport.Y + 240) * UiScale));
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)); OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, point.X - 130, point.Y, 0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X - 130, point.Y, 0));
            check(researchOffset > 0 && cx == mapX && researchSelection == choice, "Dragging the research canvas pans its graph without moving the map or selecting a node");
            float offset = researchOffset; OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, -120));
            check(researchOffset > offset && cx == mapX, "Mouse wheel moves research stages instead of zooming the map underneath");
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)); OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, point.X - 25, point.Y, 0)); Capture = false;
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, point.X - 25, point.Y, 0));
            check(!researchDragging && researchSelection == choice, "Losing mouse capture during a graph drag cannot turn release into a technology click");
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size;
                for (int branch = 0; branch < 6; branch++) {
                    click("research:branch:" + branch); click("research:fit");
                    using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "tree-branch-" + branch + "-" + size.Width + ".png"));
                    check(hits.Count(h => h.Label != null && h.Label.StartsWith("technology:")) == 12, "Overview exposes all twelve technology nodes in branch " + branch + " at " + size.Width);
                    foreach (var node in TechnologyRules.Branches[branch]) {
                        click("technology:" + node.Id);
                        if (researchSelection != node.Id + 6) throw new Exception("Node click selected the wrong project: " + node.Id);
                    }
                    check(hits.Where(h => h.Label != null && h.Label.StartsWith("technology:")).All(h => researchViewport.Contains(h.Rect)), "All twelve graph cards remain clickable inside the viewport in branch " + branch + " at " + size.Width);
                }
            }
            ClientSize = new Size(1600, 900); OpenResearchProject(33); click("research:focus");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "green-revolution.png"));
            click("research:prerequisite:26");
            check(researchSelection == 32 && researchBranch == 0, "Green-revolution dossier navigates to the actual fertilizer prerequisite");
            click("research:prerequisite:4");
            check(researchBranch == 1 && researchSelection == 10, "Expanded tree follows a cross-branch energy prerequisite and centers it");
            click("research:branch:0"); OpenResearchProject(30); click("research:start");
            check(E.Player.ScienceTree.ActiveProject == 30, "A newly added soil-science node starts a real research program through the graph");
            Navigate("Карта"); layer = "Ресурсы"; using (var frame = RenderFrame()) { }
            int builds = MapCacheBuilds; E.Tick(); using (var frame = RenderFrame()) { }
            check(MapCacheBuilds == builds, "Advancing a day does not rerasterize an unchanged resource map");
            int region = W.Countries[E.S.Player].Regions[0]; E.S.Provinces[region].Soil += .1; using (var frame = RenderFrame()) { }
            check(MapCacheBuilds > builds, "A real resource change still invalidates the resource layer cache");
            layer = "Инфраструктура"; using (var frame = RenderFrame()) { } builds = MapCacheBuilds; E.S.Provinces[region].Infrastructure -= .1; using (var frame = RenderFrame()) { }
            check(MapCacheBuilds > builds, "Construction-related infrastructure changes invalidate their thematic layer");
            int day = E.S.Day; speed = 3; scheduledSpeed = 0; PumpSimulation(10000); PumpSimulation(10100);
            check(E.S.Day == day + 1, "Fast-forward pump advances one day instead of a seven-day blocking burst");
            PumpSimulation(20000); check(E.S.Day == day + 2, "A delayed timer never replays a backlog in a blocking simulation loop");
            speed = 0; PumpSimulation(30000); check(E.S.Day == day + 2, "Paused simulation ignores timer callbacks");
            speed = 1; PumpSimulation(40000); PumpSimulation(40600); check(E.S.Day == day + 2, "Normal speed retains its 650 millisecond day interval");
            PumpSimulation(40650); check(E.S.Day == day + 3, "Normal-speed scheduler advances at the due time"); speed = 0;
            E.Validate(E.S); layer = "Политика";
        }
    }
}
