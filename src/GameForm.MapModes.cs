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
            "Концентрация сил и все иностранные части. На политической карте мирные иностранные части видны только вблизи.", "Производственные мощности на миллион жителей: светлее — больше.",
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
        private void DrawMapLayerPicker(Graphics g)
        {
            float x = VW - 314, y = VH - 118;
            Fill(g, Palette.Panel, x, y, 302, 73);
            Txt(g, layer.ToUpperInvariant(), x + 10, y + 10, 10, Palette.Gold, true, 185);
            Button(g, "+", x + 192, y + 4, 29, () => { scale = Math.Min(400, scale * 1.3f); Invalidate(); }, false, "Приблизить", 26); hits[hits.Count - 1].Label = "map:zoom-in";
            Button(g, "−", x + 228, y + 4, 29, () => { scale = Math.Max(2, scale / 1.3f); Invalidate(); }, false, "Отдалить", 26); hits[hits.Count - 1].Label = "map:zoom-out";
            Button(g, "⌂", x + 264, y + 4, 29, Home, false, "К своей стране", 26);
            string[] abbreviations = { "ПЛ", "ДП", "ВЧ", "ЭК", "РС", "ИН", "НС" };
            for (int i = 0; i < MapLayers.Length; i++) {
                string value = MapLayers[i];
                Button(g, abbreviations[i], x + 7 + i * 41, y + 37, 38, () => { layer = value; Invalidate(); }, layer == value, value + ": " + MapDescriptions[i], 28);
                hits[hits.Count - 1].Label = value;
            }
        }
    }
}
