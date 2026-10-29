using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool atlasCamera;
        private float gameCx, gameCy, gameScale, setupCx = 18, setupCy = -32, setupScale = 6;
        private int hoveredCountry = -1;
        private float ExpandedWidth => Math.Max(1100, VW - 330);
        private RectangleF OutlinerRect => new RectangleF(VW - OutlinerWidth - 12, 115, OutlinerWidth, prefs.OutlinerCollapsed ? 46 : Math.Min(selected >= 0 ? 623 : 410, VH - 260));
        private void SynchronizeAtlasCamera()
        {
            bool next = menuScreen == "new";
            if (next == atlasCamera) return;
            if (next) { gameCx = cx; gameCy = cy; gameScale = scale; cx = setupCx; cy = setupCy; scale = setupScale; }
            else { setupCx = cx; setupCy = cy; setupScale = scale; cx = gameCx; cy = gameCy; scale = gameScale; }
            atlasCamera = next; hoveredCountry = -1; hoveredRegion = -1;
            panning = false; moved = false; CapturePointer(false);
        }
        private bool MapInputAllowed(Point point)
        {
            if (showHelp || !mapRect.Contains(point)) return false;
            if (menuScreen == "new") return point.X > 314 && point.X < VW - 314 && point.Y > 82 && point.Y < VH - 42;
            if (menuScreen != "") return false;
            if (page != "Карта" && point.X < (detailPanel ? ExpandedWidth : DocketWidth)) return false;
            if ((!detailPanel || page == "Карта") && OutlinerRect.Contains(point)) return false;
            return !mapLayersExpanded && !MapPickerRect.Contains(point);
        }
        private void ClampCamera() { cx = Math.Max(-180, Math.Min(180, cx)); cy = Math.Max(-84, Math.Min(84, cy)); }
        private int FindCountry(PointF point)
        {
            return W.FindCountry(point);
        }
        private void FocusSetupCountry(int id)
        {
            menuCountry = id;
            var c = W.Countries[id]; cx = (float)c.X; cy = (float)c.Y;
            scale = Math.Max(4, Math.Min(45, 550 / Math.Max(12, c.Bounds.Width)));
            ClampCamera(); Invalidate();
        }
        private void DrawSelectionTerrain(Graphics g)
        {
            Fill(g, Color.FromArgb(130, 155, 158), mapRect.X, mapRect.Y, mapRect.Width, mapRect.Height);
            for (int i = -180; i <= 180; i += 15) { var p = Screen(i, 0); Line(g, Color.FromArgb(32, 44, 73, 78), p.X, mapRect.Top, p.X, mapRect.Bottom); }
            for (int i = -75; i <= 75; i += 15) { var p = Screen(0, i); Line(g, Color.FromArgb(32, 44, 73, 78), mapRect.Left, p.Y, mapRect.Right, p.Y); }
            var view = new RectangleF(cx - mapRect.Width / scale / 2, cy - mapRect.Height / scale / 2, mapRect.Width / scale, mapRect.Height / scale);
            var state = g.Save();
            g.TranslateTransform(mapRect.X + mapRect.Width / 2, mapRect.Y + mapRect.Height / 2);
            g.ScaleTransform(scale, scale); g.TranslateTransform(-cx, -cy);
            for (int i = 0; i < W.Countries.Length; i++) if (W.Countries[i].Bounds.IntersectsWith(view)) {
                using (var path = W.Countries[i].ClipDisplayPath(view, scale)) {
                    if (path.PointCount == 0) continue;
                    using (var brush = new SolidBrush(CountryColor(i))) g.FillPath(brush, path);
                    using (var pen = new Pen(Color.FromArgb(112, 88, 78), 0.85f / scale)) g.DrawPath(pen, path);
                }
            }
            g.Restore(state);
            if (prefs.Texture) using (var texture = new TextureBrush(terrainTexture)) g.FillRectangle(texture, mapRect);
            var occupied = new System.Collections.Generic.List<RectangleF>();
            foreach (var c in W.Countries.OrderByDescending(c => c.Bounds.Width * c.Bounds.Height)) {
                var p = Screen(c.X, c.Y);
                if (!mapRect.Contains(p) || c.Bounds.Width * scale < 42) continue;
                float font = Math.Max(10, Math.Min(19, c.Bounds.Width * scale / 15));
                string name = c.Name.ToUpperInvariant(); var size = g.MeasureString(name, BookFont(font, true));
                var box = new RectangleF(p.X - size.Width / 2 - 5, p.Y - 2, size.Width + 10, size.Height + 4);
                if (size.Width > c.Bounds.Width * scale * 1.35 || occupied.Any(b => b.IntersectsWith(box))) continue;
                occupied.Add(box);
                Book(g, name, p.X - size.Width / 2, p.Y, font, Color.FromArgb(66, 61, 49), true);
            }
        }
        private void DrawCampaignSetup(Graphics g)
        {
            mapRect = new RectangleF(0, 82, VW, VH - 124);
            if(!gpuCapturingHud) {
            var state = g.Save(); g.SetClip(mapRect); DrawCachedMap(g);
            g.TranslateTransform(mapRect.X + mapRect.Width / 2, mapRect.Y + mapRect.Height / 2);
            g.ScaleTransform(scale, scale); g.TranslateTransform(-cx, -cy);
            foreach (int id in new[] { hoveredCountry, menuCountry }.Distinct()) if (id >= 0) {
                var path = HighlightPath(W.Countries[id]); if (path.PointCount == 0) continue;
                using (var brush = new SolidBrush(Color.FromArgb(id == menuCountry ? 65 : 35, 255, 243, 173))) g.FillPath(brush, path);
                using (var pen = new Pen(id == menuCountry ? Color.FromArgb(96, 66, 28) : Color.FromArgb(220, 225, 200), (id == menuCountry ? 2.4f : 1.5f) / scale)) g.DrawPath(pen, path);
            }
            g.Restore(state);
            }
            Fill(g, Palette.Panel, 0, 0, VW, 82); Line(g, Palette.Gold, 0, 81, VW, 81);
            Button(g, "← Главное меню", 18, 23, 170, () => OpenMenu("main"));
            Book(g, "АТЛАС ГОСУДАРСТВ", 220, 20, 25, Palette.Text, true);
            Txt(g, "Выберите страну на карте · перемещайте карту мышью · приближайте колесом", 222, 55, 12, Palette.Muted);
            DocketFrame(g, new RectangleF(12, 98, 302, VH - 156), "Найти государство");
            Txt(g, "НАЗВАНИЕ ИЛИ КОД СТРАНЫ", 25, 184, 9, Palette.Muted, true);
            var list = countryList.Where(c => W.Countries[c].Name.IndexOf(countryQuery, StringComparison.OrdinalIgnoreCase) >= 0 || W.Countries[c].Code.IndexOf(countryQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            int count = (int)((VH - 290) / 33), offset = Math.Min(menuScroll, Math.Max(0, list.Count - count));
            for (int i = offset; i < Math.Min(list.Count, offset + count); i++) {
                int id = list[i]; float y = 211 + (i - offset) * 33;
                Button(g, W.Countries[id].Name, 24, y, 278, () => FocusSetupCountry(id), id == menuCountry, "Выбрать и найти на карте · " + W.Countries[id].Code, 30);
            }
            Txt(g, list.Count == 0 ? "Страны не найдены" : (offset + 1) + "–" + Math.Min(list.Count, offset + count) + " из " + list.Count, 25, VH - 82, 11, Palette.Muted);
            float x = VW - 310;
            DocketFrame(g, new RectangleF(x, 98, 298, VH - 156), "Ваша кампания");
            var country = W.Countries[menuCountry];
            CountrySilhouette(g, menuCountry, new RectangleF(x + 35, 157, 228, 85), Color.FromArgb(140, Palette.Gold));
            Book(g, country.Name, x + 20, 254, 23, Palette.Text, true, 258);
            Txt(g, country.Code + "  /  " + country.Regions.Count + " областей", x + 20, 289, 11, Palette.Gold);
            Txt(g, "Население " + country.PopulationYear + " · " + Num(country.Population / 1000000) + " млн", x + 20, 319, 14, Palette.Text);
            Txt(g, "ВВП базы     " + Money(country.GDP / 1000), x + 20, 344, 14, Palette.Text);
            Txt(g, "НАЧАЛЬНЫЙ ГОД", x + 20, 371, 10, Palette.Muted, true);
            Button(g, experimentalYear ? "✓ Экспериментальная эпоха" : "○ Экспериментальная эпоха", x + 20, 435, 258, () => {
                experimentalYear = !experimentalYear; if (!experimentalYear) { newYear = 2026; startYear.Value = 2026; } LayoutMenuControls(); Invalidate();
            }, experimentalYear, "Меняет дату и технологии; границы остаются современными.", 30);
            Para(g, "Современные границы. Региональное население, состав и размещение войск — оценки сценария; численность ВС основана на открытых данных.", x + 20, 479, 258, 53, 12, Palette.Muted);
            Txt(g, "ПОВЕДЕНИЕ ДРУГИХ СТРАН", x + 20, 544, 10, Palette.Gold, true);
            string[] difficulty = { "Спокойное", "Стратегическое", "Конкурентное" };
            for (int i = 0; i < 3; i++) { int v = i; Button(g, difficulty[i], x + 20, 570 + 38 * i, 258, () => { newDifficulty = v; Invalidate(); }, newDifficulty == i, "Поведение ИИ", 32); }
            Button(g, prefs.Tutorial ? "✓ Руководство при старте" : "○ Руководство при старте", x + 20, 702, 258, () => { prefs.Tutorial = !prefs.Tutorial; Invalidate(); }, prefs.Tutorial, "Показать начальную справку", 30);
            Button(g, "Начать игру →", x + 20, VH - 115, 258, BeginCampaign, true, "Начать за " + country.Name, 41);
            Fill(g, Palette.Panel, 0, VH - 42, VW, 42);
            Txt(g, "ЛКМ: выбрать государство    /    Потяните: переместить карту    /    Колесо: масштаб", 24, VH - 28, 12, Palette.Text);
            Button(g, "−", VW - 175, VH - 37, 40, () => { scale = Math.Max(2, scale / 1.3f); Invalidate(); }, false, "Отдалить", 30);
            Button(g, "+", VW - 125, VH - 37, 40, () => { scale = Math.Min(400, scale * 1.3f); Invalidate(); }, false, "Приблизить", 30);
            Button(g, "⌂", VW - 75, VH - 37, 40, () => FocusSetupCountry(menuCountry), false, "К выбранной стране", 30);
        }
    }
}
