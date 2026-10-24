using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestManpowerInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            menuCountry = W.ByCode("UKR"); BeginCampaign(); ClientSize = new Size(1600, 900);
            click("nav:Армия"); click("Резерв →");
            check(manpowerPage && !detailPanel && page == "Армия", "Army navigation opens the actual reserve and template panel over the map");
            double money = E.Player.Treasury; int people = E.Player.Recruits;
            click("conscription:1");
            check(E.Player.Conscription == ConscriptionLaw.Limited && E.Player.Treasury < money && E.Player.Recruits == people, "Conscription UI applies the paid law without inventing instant recruits");
            click("template:next"); check(E.SelectedTemplate(E.S.Player).Battalions == 2, "Template selector changes the next formation's actual organization size");
            templateDraftName = "UI Formation"; templateDraftBattalions = 2; click("template:more"); click("Сохранить штат");
            check(E.Player.Templates.Any(t => t.Name == "UI Formation" && t.Battalions == 3) && E.SelectedTemplate(E.S.Player).Battalions == 3, "Template editor saves and selects an immutable three-battalion organization");
            var design = E.FindDesign(E.S.Player, E.Player.ActiveDesign); design.Stock = 3; E.Player.Treasury = 100;
            click("Наземные войска →"); click("Фронты и оснащение →");
            people = E.Player.Recruits; int before = E.S.Armies.Count; click(RecruitButtonText()); var unit = E.S.Armies.Last();
            check(E.S.Armies.Count == before + 1 && unit.Battalions == 3 && unit.Personnel == 2000 && design.Stock == 0 && E.Player.Recruits == people - 2000, "Recruit UI consumes three equipment kits and 2000 people for the selected template");
            E.Validate(E.S);
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size; OpenManpower(); using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "manpower-" + size.Width + ".png"));
                check(hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1), "Manpower panel remains within viewport at " + size.Width + "x" + size.Height);
                Navigate("Армия"); detailPanel = true; using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "formations-" + size.Width + ".png"));
                check(hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1), "Expanded formations remain within viewport at " + size.Width + "x" + size.Height);
            }
            speed = 0;
        }
    }
}
