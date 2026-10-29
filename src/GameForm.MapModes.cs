using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private int hoveredRegion = -1;
        private double[] militaryIntensity;
        private static readonly string[] MapLayers = { "Политика", "Дипломатия", "Армии", "Экономика", "Ресурсы", "Инфраструктура", "Настроения" };
        private static readonly string[] MapDescriptions = {
            "Текущее владение, включая занятые области.", "Зелёный: свои; золотой: союз; бирюзовый: доступ; красный: война; синий: торговля; серый: нейтралитет.",
            "Свои и переданные партнёром силы; приблизительные наблюдения противника. Неизвестные войска скрыты при любом масштабе.", "Производственные мощности на миллион жителей: светлее — больше.",
            "Нефть, руда и плодородие: модельная обеспеченность.", "Дороги влияют на выпуск, строительство и скорость движения.", "Спокойствие населения; низкий индекс означает недовольство."
        };
        private Color DiplomaticColor(int owner)
        {
            if (owner == E.S.Player) return Color.FromArgb(143, 180, 139);
            if (E.Allied(E.S.Player, owner)) return Color.FromArgb(196, 184, 127);
            if (E.S.Access[E.Pair(E.S.Player, owner)]) return Color.FromArgb(145, 183, 173);
            if (E.AtWar(E.S.Player, owner)) return Color.FromArgb(201, 134, 113);
            if (E.S.Agreements[E.Pair(E.S.Player, owner)]) return Color.FromArgb(131, 174, 192);
            return Color.FromArgb(186, 187, 176);
        }
        private bool mapLayersExpanded;
        private RectangleF MapPickerRect => new RectangleF(VW - 314, VH - 118, 302, 73);
        private RectangleF MapLayerPopupRect => new RectangleF(VW - 334, VH - 497, 322, 367);
        private static readonly string[] MapLayerTitles = { "Политическая", "Дипломатия", "Военные силы", "Экономика", "Ресурсы", "Инфраструктура", "Настроения" };
        private static readonly string[] MapLayerHints = { "Границы и контроль территорий", "Союзы, конфликты и соглашения", "Доступные позиции и наблюдения", "Производственная плотность", "Сырьё и плодородие", "Дорожная сеть областей", "Спокойствие населения" };
        private void DrawMapLayerPicker(Graphics g)
        {
            var box = MapPickerRect; float x = box.X, y = box.Y;
            Round(g, Palette.Panel, box, 6); RoundBorder(g, Palette.Line, box, 6, 1);
            hits.Add(new Hit { Rect = box, Label = "map:picker-surface" });
            Txt(g, "СЛОЙ КАРТЫ", x + 12, y + 8, 10, Palette.Muted, true);
            int selectedLayer = Math.Max(0, Array.IndexOf(MapLayers, layer));
            Button(g, MapLayerTitles[selectedLayer] + "  ⋯", x + 8, y + 29, 177, () => { mapLayersExpanded = !mapLayersExpanded; keyboardFocusKey = null; InvalidateHud(); }, mapLayersExpanded, "Выбрать слой карты · полные названия и пояснения", 34);
            hits[hits.Count - 1].Label = "map:layers";
            Button(g, "+", x + 192, y + 29, 29, () => { scale = Math.Min(400, scale * 1.3f); Invalidate(); }, false, "Приблизить", 34); hits[hits.Count - 1].Label = "map:zoom-in";
            Button(g, "−", x + 228, y + 29, 29, () => { scale = Math.Max(2, scale / 1.3f); Invalidate(); }, false, "Отдалить", 34); hits[hits.Count - 1].Label = "map:zoom-out";
            Button(g, "⌂", x + 264, y + 29, 29, Home, false, "К своей стране", 34); hits[hits.Count - 1].Label = "map:home";
            if (!mapLayersExpanded) return;
            var popup = MapLayerPopupRect;
            Round(g, Palette.Panel, popup, 6); RoundBorder(g, Palette.Line, popup, 6, 1);
            hits.Add(new Hit { Rect = popup, Label = "map:layer-surface" });
            Txt(g, "ПРЕДСТАВЛЕНИЕ КАРТЫ", popup.X + 15, popup.Y + 13, 11, Palette.Muted, true);
            for (int i = 0; i < MapLayers.Length; i++) {
                string value = MapLayers[i]; var row = new RectangleF(popup.X + 8, popup.Y + 44 + i * 45, popup.Width - 16, 42);
                bool active = layer == value;
                if (active || row.Contains(mouse)) Round(g, active ? Palette.Selected : Palette.Hover, row, 4);
                if (active) Fill(g, Palette.Gold, row.X, row.Y + 6, 3, row.Height - 12);
                Txt(g, MapLayerTitles[i], row.X + 12, row.Y + 3, 14, active ? Palette.Gold : Palette.Text, true, row.Width - 28);
                Txt(g, MapLayerHints[i], row.X + 12, row.Y + 24, 10, Palette.Muted, false, row.Width - 28);
                hits.Add(new Hit { Rect = row, Label = value, FocusTarget = true, Tip = MapDescriptions[i], Action = () => { layer = value; mapLayersExpanded = false; keyboardFocusKey = null; Invalidate(); } });
            }
        }
    }
}
