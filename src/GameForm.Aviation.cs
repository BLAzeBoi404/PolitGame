using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private int selectedAirWing = -1, airRosterOffset;
        private bool aviationPage;
        private void OpenAviation() { Navigate("Армия"); aviationPage = true; detailPanel = false; Invalidate(); }
        private void DrawAviationPanel(Graphics g)
        {
            float w = DocketWidth - 40; int region = selected >= 0 ? selected : OwnRegion();
            var p = E.S.Provinces[region];
            Txt(g, "ВЫБРАННАЯ ОБЛАСТЬ", 20, 157, 9, Palette.Gold, true);
            Txt(g, W.Regions[region].Name, 20, 179, 17, Palette.Text, true, w);
            Txt(g, p.Airfield == 0 ? "Аэродрома нет" : "Аэродром ур. " + p.Airfield + " · " + E.S.AirWings.Count(a => a.BaseRegion == region) + "/" + E.AirCapacity(region) + " крыльев", 21, 208, 12, Palette.Muted);
            var project = E.S.Projects.FirstOrDefault(c => c.Region == region && c.Sector == 9);
            Button(g, project == null ? "Аэродром +1 · 0,60 млрд" : "Строительство: " + project.Days + " дней", 20, 235, w, () => Message(E.BuildAirfield(region)), false, "Своя область; 120 дней с поправкой на технологии; два крыла на уровень.", 30);
            var design = E.FindDesign(E.S.Player, E.Player.ActiveDesign);
            Txt(g, "ПРОЕКТ / " + design.Blueprint.Name, 20, 283, 10, Palette.Gold, true, w);
            AvailableButton(g, "Сформировать крыло · 0,15 млрд", 20, 305, w, () => Message(E.DeployAirWing(design.Id, region)), design.Blueprint.Air && design.Stock > 0 && !design.Retired && E.HasAirSpace(E.S.Player, region), "Выберите самолёт на складе, произведите комплект и выберите свой аэродром.", true, 31);
            Button(g, "Самолёты: проект и производство →", 20, 344, w, () => { equipmentProduction = true; Navigate("Производство"); detailPanel = true; }, false, "Каталог изготовленной техники", 28);
            var wings = E.S.AirWings.Where(a => a.Owner == E.S.Player).ToList();
            airRosterOffset = Math.Min(airRosterOffset, Math.Max(0, wings.Count - 2));
            CompactSection(g, "АВИАКРЫЛЬЯ / " + wings.Count, 394);
            Button(g, "‹", 301, 385, 32, () => { airRosterOffset = Math.Max(0, airRosterOffset - 2); Invalidate(); }, false, "Предыдущие крылья", 25);
            Button(g, "›", 343, 385, 32, () => { airRosterOffset = Math.Min(Math.Max(0, wings.Count - 2), airRosterOffset + 2); Invalidate(); }, false, "Следующие крылья", 25);
            for (int i = airRosterOffset; i < Math.Min(wings.Count, airRosterOffset + 2); i++) {
                var a = wings[i]; float y = 436 + (i - airRosterOffset) * 41;
                Button(g, "№" + a.Id + " · " + a.Design.Name + " · " + a.Strength.ToString("0") + "%", 20, y, w, () => { selectedAirWing = a.Id; Invalidate(); }, selectedAirWing == a.Id, E.AirStatus(a), 32);
            }
            var wing = wings.FirstOrDefault(a => a.Id == selectedAirWing) ?? wings.FirstOrDefault();
            if (wing == null) { Para(g, "Постройте аэродром, создайте авиационный проект, выпустите серию и сформируйте крыло. Для вылетов нужны топливо и подготовленный экипаж.", 20, 535, w, 135, 14); return; }
            selectedAirWing = wing.Id;
            Txt(g, "База: " + W.Regions[wing.BaseRegion].Name, 20, 532, 12, Palette.Text, false, w);
            Txt(g, "Радиус " + wing.Design.Range.ToString("0") + " км · вылетов " + wing.Sorties, 20, 556, 11, Palette.Gold);
            Para(g, E.AirStatus(wing), 20, 580, w, 43, 12, Palette.Muted);
            var mission = wing.Design.Kind == EquipmentKind.Fighter ? AirMission.Superiority : wing.Design.Kind == EquipmentKind.Bomber ? AirMission.Strike : wing.Design.Kind == EquipmentKind.Recon ? AirMission.Recon : AirMission.Supply;
            string reason = E.AirTargetReason(wing, mission, region);
            Txt(g, "Цель: " + W.Regions[region].Name, 20, 631, 12, Palette.Text, false, w);
            AvailableButton(g, Engine.AirMissionNames[(int)mission] + " →", 20, 655, w, () => Message(E.SetAirMission(wing.Id, mission, region)), reason == null, reason ?? "Задача в выбранной области; выберите другую цель на карте.", true, 31);
            Button(g, "В резерв / ремонт", 20, 699, w / 2 - 5, () => Message(E.SetAirMission(wing.Id, AirMission.Reserve, -1)), wing.Mission == AirMission.Reserve, "Остановить вылеты; ремонт расходует оружие и деньги", 30);
            Button(g, "Перебазировать", 25 + w / 2, 699, w / 2 - 5, () => Message(E.RebaseAirWing(wing.Id, region)), false, "К выбранному своему аэродрому; топливо, дальность и три дня подготовки", 30);
            Button(g, "Показать базу на карте", 20, 743, w, () => { cx = (float)W.Regions[wing.BaseRegion].X; cy = (float)W.Regions[wing.BaseRegion].Y; selected = wing.BaseRegion; Invalidate(); }, false, "Выделить аэродром", 30);
        }
        private void DrawAirOperations(Graphics g)
        {
            foreach (var wing in E.S.AirWings.Where(a => a.Owner == E.S.Player)) {
                var basePoint = Screen(W.Regions[wing.BaseRegion].X, W.Regions[wing.BaseRegion].Y);
                if (wing.Id == selectedAirWing && aviationPage) {
                    using (var radius = new Pen(Color.FromArgb(130, Palette.Gold), 1)) {
                        radius.DashStyle = DashStyle.Dash;
                        var ring = new PointF[73];
                        double lat = -W.Regions[wing.BaseRegion].Y * Math.PI / 180, lon = W.Regions[wing.BaseRegion].X * Math.PI / 180, distance = wing.Design.Range / 6371;
                        for (int i = 0; i < ring.Length; i++) {
                            double bearing = i * Math.PI * 2 / 72;
                            double lat2 = Math.Asin(Math.Sin(lat) * Math.Cos(distance) + Math.Cos(lat) * Math.Sin(distance) * Math.Cos(bearing));
                            double lon2 = lon + Math.Atan2(Math.Sin(bearing) * Math.Sin(distance) * Math.Cos(lat), Math.Cos(distance) - Math.Sin(lat) * Math.Sin(lat2));
                            ring[i] = Screen(lon2 * 180 / Math.PI, -lat2 * 180 / Math.PI);
                        }
                        g.DrawLines(radius, ring);
                    }
                }
                if (wing.Mission != AirMission.Reserve && wing.Target >= 0) {
                    var target = Screen(W.Regions[wing.Target].X, W.Regions[wing.Target].Y);
                    using (var line = new Pen(Palette.Gold, 1.5f)) { line.DashStyle = DashStyle.Dash; g.DrawLine(line, basePoint, target); g.DrawEllipse(line, target.X - 7, target.Y - 7, 14, 14); }
                }
                var rect = new RectangleF(basePoint.X - 16, basePoint.Y - 43, 34, 24);
                if (!mapRect.IntersectsWith(rect)) continue;
                Fill(g, wing.Id == selectedAirWing && aviationPage ? Palette.Selected : Palette.Panel, rect.X, rect.Y, rect.Width, rect.Height);
                // Original top-view aircraft symbol, independent of installed symbol fonts.
                Line(g, Palette.Gold, basePoint.X, rect.Y + 4, basePoint.X, rect.Bottom - 3, 2);
                Line(g, Palette.Gold, basePoint.X - 12, rect.Y + 14, basePoint.X + 12, rect.Y + 14, 2);
                Line(g, Palette.Gold, basePoint.X - 5, rect.Bottom - 4, basePoint.X + 5, rect.Bottom - 4);
                hits.Add(new Hit { Rect = RectangleF.Intersect(rect, mapRect), Label = "air:" + wing.Id, Action = () => { selectedAirWing = wing.Id; selected = wing.BaseRegion; OpenAviation(); }, Tip = wing.Design.Name + " · " + E.AirStatus(wing) });
            }
        }
    }
}
