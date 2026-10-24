using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestAtlasInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            Action<Point, MouseButtons> tap = (p, button) => {
                int x = (int)(p.X * UiScale), y = (int)(p.Y * UiScale);
                OnMouseDown(new MouseEventArgs(button, 1, x, y, 0)); OnMouseUp(new MouseEventArgs(button, 1, x, y, 0));
            };
            check(W.Regions.All(r => r.Neighbors.All(n => r.Country == W.Regions[n].Country || atlasColors[r.Country] != atlasColors[W.Regions[n].Country])), "Political palette assigns distinct colors to every pair of adjacent countries");
            ClientSize = new Size(1600, 900); PreviewPage("Карта"); Home();
            using (var frame = RenderFrame()) { }
            var campaign = E.S; int day = E.S.Day; float originalCx = cx, originalCy = cy;
            OpenMenu("new"); using (var frame = RenderFrame()) { }
            int france = W.ByCode("FRA");
            var region = W.Countries[france].Regions.First(r => FindCountry(new PointF((float)W.Regions[r].X, (float)W.Regions[r].Y)) == france);
            cx = (float)W.Regions[region].X; cy = (float)W.Regions[region].Y; scale = 14;
            using (var frame = RenderFrame()) { }
            Point center = new Point((int)(mapRect.X + mapRect.Width / 2), (int)(mapRect.Y + mapRect.Height / 2));
            tap(center, MouseButtons.Left);
            check(menuCountry == france && ReferenceEquals(E.S, campaign) && E.S.Day == day, "Country map click selects France without creating or altering a campaign");
            float selectedCx = cx, selectedCy = cy;
            using (var frame = RenderFrame()) { }
            check(cx == selectedCx && cy == selectedCy, "Country selection does not jump or refocus the atlas camera");
            var anchor = Geo(center); float zoom = scale;
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, (int)(center.X * UiScale), (int)(center.Y * UiScale), 120));
            var after = Geo(center);
            check(scale > zoom && Math.Abs(anchor.X - after.X) < .01 && Math.Abs(anchor.Y - after.Y) < .01, "Setup wheel zoom keeps the geographic cursor anchor");
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, (int)(center.X * UiScale), (int)(center.Y * UiScale), 0));
            for (int i = 1; i <= 30; i++) OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, (int)((center.X + i) * UiScale), (int)(center.Y * UiScale), 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, (int)((center.X + 30) * UiScale), (int)(center.Y * UiScale), 0));
            check(cx != selectedCx && menuCountry == france, "Slow atlas drag pans without accidentally selecting a country on release");
            int selectedCountry = menuCountry; cx = -30; cy = 0; using (var frame = RenderFrame()) { }
            tap(center, MouseButtons.Left);
            check(menuCountry == selectedCountry, "Ocean click preserves the selected starting country");
            ContinueGame(); using (var frame = RenderFrame()) { }
            check(cx == originalCx && cy == originalCy && ReferenceEquals(E.S, campaign), "Leaving setup restores the existing campaign camera and state");
            OpenMenu("new"); using (var frame = RenderFrame()) { }
            FocusSetupCountry(W.ByCode("DEU")); using (var frame = RenderFrame()) { }
            click("Начать игру →"); using (var frame = RenderFrame()) { }
            check(E.S.Player == W.ByCode("DEU") && page == "Карта" && menuScreen == "" && speed == 0, "Atlas start button opens the chosen country directly on the paused map");
            menuCountry = W.ByCode("UKR"); BeginCampaign();
            click("nav:Экономика"); using (var frame = RenderFrame()) { }
            int builds = MapCacheBuilds; float camera = cx; zoom = scale; int selection = selected;
            tap(new Point(10, 530), MouseButtons.Left);
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, (int)(35 * UiScale), (int)(530 * UiScale), -120));
            check(cx == camera && scale == zoom && selected == selection, "Panel background captures clicks and scrolling without affecting the atlas");
            using (var frame = RenderFrame()) { }
            check(MapCacheBuilds == builds && mapRect.Width == VW && !detailPanel, "Opening compact management reuses the full-width atlas cache");
            bool trading = E.Player.AutoTrade; click(trading ? "Автоторговля: включена" : "Автоторговля: выключена");
            check(E.Player.AutoTrade != trading, "Compact economy controls change the real trade policy");
            click("Подробное управление →"); check(detailPanel, "Compact panel expands into the detailed workspace");
            click("Свернуть"); check(!detailPanel, "Detailed workspace collapses back without closing the map");
            click("nav:Политика"); double pressure = E.Player.Repression; click("+");
            check(E.Player.Repression > pressure, "Compact policy control changes actual political spending");
            click("nav:Исследования"); click("Начать"); check(E.Player.Research >= 0, "Compact research starts a real scientific program");
            foreach (var size in new[] { new Size(1280, 720), new Size(1600, 900), new Size(1920, 1080) }) {
                ClientSize = size;
                foreach (var target in new[] { "Экономика", "Армия", "Авиация", "Новая игра", "Конструктор" }) {
                    PreviewPage(target); Application.DoEvents();
                    using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "atlas-" + size.Width + "-" + target + ".png"));
                    check(hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1), "Atlas " + target + " action bounds at " + size.Width + "x" + size.Height);
                }
            }
            ClientSize = new Size(1280, 720);
            foreach (var target in Navigation.Where(p => p != "Карта")) {
                PreviewPage(target); detailPanel = true; using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "detail-" + target + ".png"));
                check(hits.All(h => h.Rect.Left >= 0 && h.Rect.Top >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Bottom <= VH + 1), "Expanded " + target + " remains within 1280x720 viewport");
            }
            PreviewPage("Новая игра"); PreviewPage("Настройки");
            check(!countrySearch.Visible && !startYear.Visible, "Leaving new-campaign preview hides native setup controls on other menus");
            ClientSize = new Size(1600, 900); PreviewPage("Карта"); Home();
            int airbase = OwnRegion(); E.Player.Treasury = 100; OpenAviation(); click("Аэродром +1 · 0,60 млрд");
            check(E.S.Projects.Any(p => p.Region == airbase && p.Sector == 9), "Airbase panel starts actual construction through a mouse click");
            var construction = E.S.Projects.First(p => p.Region == airbase && p.Sector == 9); construction.Days = 1; E.Tick();
            var blueprint = EquipmentRules.Balanced(EquipmentKind.Fighter, E.Player, "Atlas fighter"); E.Adopt(blueprint);
            var design = E.FindDesign(E.S.Player, E.Player.ActiveDesign); design.Stock = 1;
            click("Сформировать крыло · 0,15 млрд");
            check(E.S.AirWings.Count == 1 && design.Stock == 0, "Air panel deploys a real produced equipment kit");
            var wing = E.S.AirWings.Single(); wing.TrainingDays = 0; click("Превосходство →");
            check(wing.Mission == AirMission.Superiority && wing.Target == airbase, "Air panel assigns the map-selected target to the actual wing");
            E.Player.Stock[2] = 100; E.TickAviation();
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "air-operations.png"));
            click("В резерв / ремонт"); check(wing.Mission == AirMission.Reserve, "Air mission stop returns the wing to reserve");
            speed = 0;
        }
    }
}
