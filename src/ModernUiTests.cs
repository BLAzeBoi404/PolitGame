using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestModernInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            E.New(W.ByCode("UKR"), 2026, 1); speed = 0; clock.Stop(); showHelp = false; menuScreen = "";
            ClientSize = new Size(1600, 900); prefs.OutlinerCollapsed = false; prefs.Tooltips = true;
            Navigate("Карта"); Home(); selected = hoveredRegion = -1; scale = 14;
            Action draw = () => { using (var frame = RenderFrame()) { } };
            Action<Keys> key = value => KeysDown(this, new KeyEventArgs(value));
            draw(); var original = armyMapGroups.ToArray();
            check(original.All(g => g.Overview && g.Units.All(a => a.Owner == E.S.Player)) && original.Length < E.S.Armies.Count(a => a.Owner == E.S.Player), "Overview sectors reduce badge density without showing hidden foreign troops");
            check(original.Sum(g => g.Personnel) == E.S.Armies.Where(a => a.Owner == E.S.Player).Sum(a => a.Personnel), "Overview grouping preserves every soldier exactly once");
            cx += 1; draw();
            check(original.SequenceEqual(armyMapGroups), "Camera panning reuses stable sector groups instead of reclustering markers");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "modern-map.png"));
            string marker = hits.First(h => h.Label != null && h.Label.StartsWith("army-group:")).Label;
            click(marker); draw();
            check(scale == 45 && army >= 0 && armyMapGroups.All(g => !g.Overview) && E.S.Armies.Any(a => a.Id == army && a.Region == selected), "Clicking a sector zooms to an actual formation and restores detailed markers");
            Home(); scale = 14; draw();
            float oldScale = scale, oldCx = cx; int oldSelection = selected;
            click("map:layers"); draw();
            check(mapLayersExpanded && MapLayers.All(name => hits.Any(h => h.Label == name)) && !MapInputAllowed(new Point(700, 450)), "Layer picker exposes all seven named modes and blocks map interaction underneath");
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 700, 450, 0));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 740, 450, 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 740, 450, 0));
            check(!mapLayersExpanded && cx == oldCx && selected == oldSelection, "Clicking outside the layer picker dismisses it without dragging or selecting the map");
            click("map:layers"); OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 700, 450, -120));
            check(!mapLayersExpanded && scale == oldScale, "Wheel dismissal does not accidentally zoom the map behind the popup");
            click("map:layers"); draw(); key(Keys.Tab); draw();
            check(FocusedHit() != null && FocusedHit().Label == MapLayers[0], "Keyboard focus starts on the first visible popup option, excluding covered controls");
            key(Keys.Tab); draw(); key(Keys.Enter); draw();
            check(layer == "Дипломатия" && !mapLayersExpanded, "Tab and Enter choose the actual diplomatic map layer");
            click("map:layers"); key(Keys.Escape);
            check(menuScreen == "" && !mapLayersExpanded, "Escape closes the layer picker before opening the pause menu");
            click("map:layers"); click("nav:Армия");
            check(page == "Армия" && !mapLayersExpanded, "Navigation remains usable while the layer picker is open");
            Navigate("Карта"); draw(); key(Keys.Tab); draw(); string first = keyboardFocusKey;
            key(Keys.Tab); draw(); key(Keys.Shift | Keys.Tab); draw();
            check(keyboardFocusKey == first && FocusedHit() != null, "Shift-Tab reverses keyboard navigation through the command bar");
            prefs.SetBinding("Pause", Keys.Tab); var message = new System.Windows.Forms.Message();
            ProcessCmdKey(ref message, Keys.Tab);
            check(speed == 1, "An explicitly rebound Tab command takes priority over default focus navigation");
            speed = 0; prefs.Bindings = Preferences.DefaultBindings();
            Navigate("Армия"); check(keyboardFocusKey == null, "Changing a page drops stale keyboard focus");
            Navigate("Карта"); selected = E.S.Armies.First(a => a.Owner == E.S.Player).Region; draw();
            var freed = new Point((int)OutlinerRect.X + 30, (int)OutlinerRect.Y + 100);
            check(!MapInputAllowed(freed), "Expanded operational summary protects its controls from map clicks");
            click("hud:outliner-toggle"); draw();
            check(prefs.OutlinerCollapsed && Preferences.Read().OutlinerCollapsed && MapInputAllowed(freed), "Collapsing the summary persists the preference and releases the covered map area");
            click("hud:outliner-toggle");
            var own = E.S.Armies.Where(a => a.Owner == E.S.Player).ToArray();
            foreach (var a in own) { a.Strength = a.Organization = a.Supply = 100; a.Fatigue = 0; a.Personnel = ManpowerRules.Establishment(a); a.TrainingDays = a.ExerciseRemaining = 0; }
            own[1].Supply = 15; own[2].Fatigue = 85; own[3].TrainingDays = 15;
            OpenReadiness(); click("readiness:filter:1"); draw();
            check(ReadinessUnits().Select(a => a.Id).OrderBy(id => id).SequenceEqual(new[] { own[1].Id, own[2].Id, own[3].Id }.OrderBy(id => id)), "Attention filter finds genuinely undersupplied, exhausted or untrained own formations");
            click("readiness:filter:2"); draw();
            check(ReadinessUnits().Length == 1 && ReadinessUnits()[0] == own[3], "Training filter shows only an actual training formation");
            own[3].TrainingDays = 0; draw();
            check(!hits.Any(h => h.Label == "readiness:exercise") && hits.Any(h => h.Label == "readiness:filter:0"), "Empty training results leave a way back without a stale exercise action");
            click("readiness:filter:0"); click("readiness:sort"); draw();
            var sorted = ReadinessUnits();
            check(sorted.Zip(sorted.Skip(1), (a,b) => ReadinessRules.Score(a) <= ReadinessRules.Score(b)).All(v => v), "Readiness sorting puts the weakest formations first");
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, (int)(300 * UiScale), (int)(500 * UiScale), -120)); draw();
            check(readinessOffset == 3 && hits.Any(h => h.Label == "readiness:unit:" + sorted[3].Id), "Mouse wheel scrolls the readiness roster by three actual entries");
            int beforeArmy = army; double money = E.Player.Treasury;
            click("readiness:summary:0");
            check(army == beforeArmy && E.Player.Treasury == money, "Clicking a read-only summary neither throws nor changes campaign state");
            E.Player.Stock[2] = 0; draw();
            var exercise = hits.Single(h => h.Label == "readiness:exercise"); keyboardFocusKey = FocusKey(exercise); key(Keys.Enter); draw();
            check(E.S.Armies.First(a => a.Id == army).ExerciseRemaining == 0 && E.Player.Treasury == money && !string.IsNullOrEmpty(toast), "Keyboard activation of an unavailable course explains the reason without bypassing its cost checks");
            keyboardFocusKey = null; hoveredTip = hits.Single(h => h.Label == "quality:detail").Tip;
            hoverSince = DateTime.Now.AddSeconds(-1); mouse = new Point((int)VW - 8, (int)VH - 8); toastUntil = DateTime.MinValue;
            draw();
            check(lastTooltipBounds.Height > 110 && lastTooltipBounds.Right <= VW && lastTooltipBounds.Bottom <= VH - 34 && lastTooltipBounds.Top >= 99, "Long quality explanations expand to fit and stay inside the viewport");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "modern-tooltip.png"));
            hoveredTip = ""; army = own[1].Id; click("readiness:filter:1");
            foreach (var size in new[] { new Size(1280,720), new Size(1600,900), new Size(1920,1080) }) {
                ClientSize = size; using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "modern-readiness-" + size.Width + ".png"));
                var controls = hits.Where(h => h.FocusTarget && h.Label != null && h.Label.StartsWith("readiness:")).ToArray();
                check(controls.All(h => h.Rect.Right <= ExpandedWidth && h.Rect.Bottom <= VH - 34) && !controls.Any(a => controls.Any(b => a != b && RectangleF.Intersect(a.Rect,b.Rect).Width > 1 && RectangleF.Intersect(a.Rect,b.Rect).Height > 1)), "Filtered roster controls fit without overlap at " + size.Width + "x" + size.Height);
                Navigate("Карта"); Home(); scale = 14; click("map:layers");
                using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "modern-layers-" + size.Width + ".png"));
                check(MapLayerPopupRect.Bottom < MapPickerRect.Top && MapLayerPopupRect.Right <= VW && MapLayerPopupRect.Top > 99, "Layer picker fits the map at " + size.Width + "x" + size.Height);
                key(Keys.Escape); OpenReadiness(); click("readiness:filter:1");
            }
            ClientSize = new Size(1600,900); prefs.OutlinerCollapsed = false; prefs.Save(); Navigate("Карта"); Home();
        }
    }
}
