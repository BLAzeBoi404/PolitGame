using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    internal static class UiTests
    {
        public static int Run(World world, string directory)
        {
            Directory.CreateDirectory(directory);
            using (var form = new GameForm(world)) return form.TestInterface(directory);
        }
    }
    public partial class GameForm
    {
        public Bitmap RenderFrame()
        {
            var frame = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var graphics = Graphics.FromImage(frame)) {
                graphics.Clear(BackColor);
                OnPaint(new PaintEventArgs(graphics, ClientRectangle));
            }
            foreach (Control child in Controls) if (child.Visible) {
                using (var part = new Bitmap(child.Width, child.Height)) {
                    child.DrawToBitmap(part, new Rectangle(0, 0, child.Width, child.Height));
                    using (var g = Graphics.FromImage(frame)) g.DrawImageUnscaled(part, child.Location);
                }
            }
            return frame;
        }
        internal int TestInterface(string directory)
        {
            var results = new List<string>(); int failed = 0;
            Action<bool, string> check = (ok, name) => { results.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failed++; };
            Action<string> preview = p => { PreviewPage(p); detailPanel = menuScreen == "" && page != "Карта"; };
            prefs.Autosave = false; prefs.Tutorial = false; prefs.Fullscreen = false; prefs.Bindings = Preferences.DefaultBindings();
            E.AutoSaveEnabled = false;
            Show(); Application.DoEvents();
            menuCountry = W.ByCode("FRA"); BeginCampaign();
            check(E.S.Player == W.ByCode("FRA") && speed == 0, "Country selection starts a paused campaign");
            menuCountry = W.ByCode("UKR"); BeginCampaign();
            check(E.S.Player == W.ByCode("UKR") && E.S.Day == 0, "Starting another country resets campaign state");
            preview("Конструктор");
            using (var image = RenderFrame()) { }
            Action<string> click = label => {
                using (var image = RenderFrame()) { }
                Hit hit = hits.LastOrDefault(h => h.Label == label);
                if (hit == null) throw new InvalidOperationException("UI button not found: " + label);
                Point position = new Point((int)((hit.Rect.X + hit.Rect.Width / 2) * UiScale), (int)((hit.Rect.Y + hit.Rect.Height / 2) * UiScale));
                OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, position.X, position.Y, 0));
            };
            click("БМП / БТР"); check(draft.Kind == EquipmentKind.IFV, "Designer category responds to a mouse click");
            int previous = draft.Support; click("+"); check(draft.Support == previous + 1, "Module control changes the actual blueprint");
            draft.Name = "UI Expedition"; double treasury = E.Player.Treasury;
            using (var frame = RenderFrame()) { }
            click("Испытать и сохранить · " + Money(draft.Cost * 2));
            check(E.Player.Designs.Any(d => d.Blueprint.Name == "UI Expedition") && E.Player.Treasury < treasury, "Designer saves a paid project through the UI");
            preview("Серии"); click("Заказать " + orderQuantity + " компл. · всего " + Money(E.Player.Design.Cost * orderQuantity));
            check(E.Player.Lines.Count == 1, "Production order button creates a real line");
            var line = E.Player.Lines[0]; click("Пауза"); check(line.Paused, "Production pause button releases the line");
            click("Пуск"); check(!line.Paused, "Production resume button restarts the line");
            for (int i = 0; i < 250 && E.FindDesign(E.S.Player, E.Player.ActiveDesign).Stock == 0; i++) E.Tick();
            preview("Армия"); int armyCount = E.S.Armies.Count;
            click("Набрать часть · 0,30 млрд");
            check(E.S.Armies.Count == armyCount + 1 && E.S.Armies.Last().TrainingDays == 30, "Recruit button consumes manufactured equipment");
            preview("Карта");
            using (var frame = RenderFrame()) { }
            Point center = new Point((int)(mapRect.X + mapRect.Width / 2), (int)(mapRect.Y + mapRect.Height / 2));
            float before = scale;
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, (int)(center.X * UiScale), (int)(center.Y * UiScale), 120));
            check(scale > before, "Mouse wheel zooms the map");
            float oldCx = cx;
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, (int)(center.X * UiScale), (int)(center.Y * UiScale), 0));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, (int)((center.X + 40) * UiScale), (int)(center.Y * UiScale), 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, (int)((center.X + 40) * UiScale), (int)(center.Y * UiScale), 0));
            check(cx != oldCx, "Mouse drag pans the map");
            var own = E.S.Armies.First(a => a.Owner == E.S.Player); selected = own.Region;
            cx = (float)W.Regions[own.Region].X; cy = (float)W.Regions[own.Region].Y;
            using (var frame = RenderFrame()) { }
            check(FindRegion(new PointF(cx, cy)) >= 0, "Territory hit testing resolves real geography");
            speed = 0; KeysDown(this, new KeyEventArgs(Keys.Space)); check(speed == 1, "Space resumes time");
            KeysDown(this, new KeyEventArgs(Keys.Escape)); check(speed == 0 && menuScreen == "main", "Menu pauses simulation");
            string[] pages = { "Сводка", "Карта", "Экономика", "Производство", "Серии", "Исследования", "Дипломатия", "Армия", "Конструктор", "Политика", "Меню", "Новая игра", "Настройки", "Клавиши" };
            var sizes = new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) };
            for (int s = 0; s < sizes.Length; s++) {
                ClientSize = sizes[s];
                for (int p = 0; p < pages.Length; p++) {
                    if (pages[p] == "Производство") equipmentProduction = false;
                    PreviewPage(pages[p]); if (pages[p] == "Клавиши") OpenMenu("controls"); Application.DoEvents();
                    using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "screen-" + s + "-" + p + ".png"));
                    bool contained = hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1);
                    check(contained, pages[p] + " has visible action bounds at " + sizes[s].Width + "x" + sizes[s].Height);
                    if (menuScreen == "") check(!countrySearch.Visible && !startYear.Visible, "Setup controls hidden during " + pages[p]);
                }
            }
            preview("Карта"); ClientSize = new Size(1600, 900);
            using (var frame = RenderFrame()) { }
            int cached = MapCacheBuilds;
            selected = selected == 0 ? 1 : 0;
            using (var frame = RenderFrame()) { }
            check(MapCacheBuilds == cached, "Changing selection reuses cached geography");
            cx += 0.25f;
            using (var frame = RenderFrame()) { }
            check(MapCacheBuilds == cached, "Small camera movements reuse accurate overscanned geography");
            cx += CameraMargin / scale / UiScale + 2;
            using (var frame = RenderFrame()) { }
            check(MapCacheBuilds == cached + 1, "Leaving the overscan margin rebuilds geography");
            cx -= CameraMargin / scale / UiScale + 2;
            foreach (string mode in MapLayers) {
                click(mode);
                using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "map-" + Array.IndexOf(MapLayers, mode) + ".png"));
                check(layer == mode, "Map mode click selects " + mode);
            }
            layer = "Политика";
            using (var frame = RenderFrame()) { }
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 30; i++) using (var frame = RenderFrame()) { }
            results.Add("Map rendering mean: " + (watch.Elapsed.TotalMilliseconds / 30).ToString("0.0") + " ms, 1600x900, 30 frames, Mono/Xvfb");
            watch.Restart();
            for (int i = 0; i < 12; i++) { mapCacheKey = null; cx += 0.15f; using (var frame = RenderFrame()) { } }
            results.Add("Forced camera rebuild mean (including overscan): " + (watch.Elapsed.TotalMilliseconds / 12).ToString("0.0") + " ms, 1600x900, 12 frames, Mono/Xvfb");
            panning = true; int buildsBeforeDrag = MapCacheBuilds; watch.Restart();
            for (int i = 0; i < 20; i++) { cx += 0.1f; using (var frame = RenderFrame()) { } }
            results.Add("Drag frame mean: " + (watch.Elapsed.TotalMilliseconds / 20).ToString("0.0") + " ms, 1600x900, 20 frames, Mono/Xvfb");
            check(MapCacheBuilds == buildsBeforeDrag, "Dragging translates cached geometry without blocking on a rebuild");
            panning = false; using (var frame = RenderFrame()) { }
            check(MapCacheBuilds == buildsBeforeDrag, "Releasing a short drag retains accurate overscanned geography");
            results.Add("Raster stages: " + mapRasterProfile);
            results.Add("Terrain stages ms: geometry=" + mapGeometryMs.ToString("0.0") + " texture=" + mapTextureMs.ToString("0.0") + " labels=" + mapLabelsMs.ToString("0.0"));
            check(W.Countries.Sum(c => c.DisplayPath(6).PointCount) < W.Countries.Sum(c => c.Path.PointCount), "Overview geometry reduces vertices while retaining exact hit-test paths");
            // Exercise new systems through the real hit regions and keyboard handler.
            foreign = W.ByCode("POL"); preview("Дипломатия");
            while (E.S.Relations[E.Pair(E.S.Player, foreign)] < 65) click("Отправить");
            click("Предложить союз");
            check(E.Allied(E.S.Player, foreign), "Diplomatic action establishes an actual defensive alliance");
            click("Запросить");
            check(E.S.Access[E.Pair(E.S.Player, foreign)], "Access treaty can be requested from the dossier");
            E.Diplomacy(foreign, 5); E.Diplomacy(foreign, 3);
            preview("Армия"); click("Выбрать все"); click("Удерживать");
            check(E.S.Fronts.Count == 1 && E.S.Fronts[0].ArmyIds.Count == E.S.Armies.Count(a => a.Owner == E.S.Player), "Roster selection assigns actual formations to a front");
            click("Наступление"); check(E.S.Fronts[0].Stance == FrontStance.Advance, "Front stance button changes the operational plan");
            click("Остановить"); check(!E.S.Fronts[0].Active && E.S.Armies.Where(a => E.S.Fronts[0].ArmyIds.Contains(a.Id)).All(a => a.Target < 0), "Stopping a front cancels movement through the UI");
            click("Продолжить");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "front-command.png"));
            preview("Карта"); Home(); selected = own.Region;
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "front-map.png"));
            preview("Дипломатия");
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "dossier-war.png"));
            preview("Настройки"); click("Настроить клавиши →"); click("Пробел");
            KeysDown(this, new KeyEventArgs(Keys.Control | Keys.P));
            check(prefs.Binding("Pause") == (Keys.Control | Keys.P) && pendingBinding == null, "Binding editor captures a new modifier shortcut");
            preview("Карта"); speed = 0; KeysDown(this, new KeyEventArgs(Keys.Space));
            check(speed == 0, "Old pause key no longer triggers after reassignment");
            KeysDown(this, new KeyEventArgs(Keys.Control | Keys.P));
            check(speed == 1, "Reassigned pause key controls the live simulation");
            speed = 0; prefs.Bindings = Preferences.DefaultBindings(); prefs.Save();
            TestAtlasInterface(directory, check, click);
            results.Add("Failures: " + failed); File.WriteAllLines(Path.Combine(directory, "ui-results.txt"), results);
            Console.WriteLine(string.Join(Environment.NewLine, results));
            return failed == 0 ? 0 : 2;
        }
    }
}
