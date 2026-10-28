using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestReadinessInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            E.New(W.ByCode("UKR"), 2026, 1); speed = 0; showHelp = false; menuScreen = "";
            var a = E.S.Armies.First(u => u.Owner == E.S.Player); army = a.Id; a.Experience = 0;
            E.Player.Treasury = 100; E.Player.Stock[2] = E.Player.Stock[7] = 100;
            Navigate("Армия"); click("readiness:open");
            check(readinessPage && detailPanel, "The army panel opens the real readiness and exercises desk");
            double money = E.Player.Treasury; click("readiness:exercise");
            check(a.ExerciseRemaining == 21 && E.Player.Treasury == money, "Mouse input schedules the selected formation without an advance payment");
            E.S.Day++; E.TickReadiness(); click("readiness:exercise");
            check(a.ExerciseRemaining == 0 && a.Experience > 0 && a.Fatigue > 0, "Stopping exercises from the UI preserves earned experience and recovery costs");
            E.Player.Stock[2] = 0; click("readiness:exercise");
            check(a.ExerciseRemaining == 0, "The disabled training control cannot bypass a real fuel shortage");
            E.Player.Stock[2] = 100; click("readiness:exercise");
            foreach (var size in new[] { new Size(1280,720), new Size(1600,900), new Size(1920,1080) }) {
                ClientSize = size; toastUntil = DateTime.MinValue;
                using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "readiness-" + size.Width + ".png"));
                var targets = hits.Where(h => h.Label != null && h.Label.StartsWith("readiness:")).ToArray();
                check(targets.Length >= 11 && targets.All(h => h.Rect.Left >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Top >= 0 && h.Rect.Bottom <= VH + 1), "Readiness controls remain reachable at " + size.Width + "x" + size.Height);
                bool overlap = false;
                for (int i = 0; i < targets.Length; i++) for (int j = i + 1; j < targets.Length; j++) { var r = RectangleF.Intersect(targets[i].Rect, targets[j].Rect); if (r.Width > 1 && r.Height > 1) overlap = true; }
                check(!overlap, "Readiness controls do not overlap at " + size.Width + "x" + size.Height);
            }
            click("readiness:next"); check(readinessOffset > 0, "The readiness list pages through the country's actual formations");
            click("readiness:trade"); check(tradePage && !readinessPage, "The readiness desk links directly to supply procurement");
            OpenReadiness(); click("readiness:back"); check(!readinessPage && page == "Армия", "Returning to command restores the ordinary army panel");
            ClientSize = new Size(1600,900);
        }
    }
}
