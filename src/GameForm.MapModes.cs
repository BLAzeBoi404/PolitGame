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
            "Текущее владение, включая занятые области.", "Зелёный: свои; красный: война; синий: торговый договор; серый: нейтралитет.",
            "Концентрация боеспособных частей: светлее — больше сил. Значки показывают принадлежность.", "Производственные мощности на миллион жителей: светлее — больше.",
            "Нефть, руда и плодородие: модельная обеспеченность.", "Дороги влияют на выпуск, строительство и скорость движения.", "Спокойствие населения; низкий индекс означает недовольство."
        };
        private Color DiplomaticColor(int owner)
        {
            if (owner == E.S.Player) return Color.FromArgb(143, 180, 139);
            if (E.AtWar(E.S.Player, owner)) return Color.FromArgb(201, 134, 113);
            if (E.S.Agreements[E.Pair(E.S.Player, owner)]) return Color.FromArgb(131, 174, 192);
            return Color.FromArgb(186, 187, 176);
        }
        private void DrawMapLayerPicker(Graphics g)
        {
            float width = Math.Min(mapRect.Width - 36, 610), x = mapRect.X + 18, y = mapRect.Y + 17;
            Round(g, Color.FromArgb(246, 30, 38, 39), new RectangleF(x, y, width, 106), 4);
            Txt(g, "АТЛАС МИРА / " + layer.ToUpperInvariant(), x + 13, y + 10, 10, Palette.Gold, true);
            float tab = (width - 26) / 4;
            for (int i = 0; i < MapLayers.Length; i++) {
                string value = MapLayers[i];
                Button(g, value, x + 10 + i % 4 * (tab + 2), y + 33 + i / 4 * 33, tab, () => { layer = value; Invalidate(); }, layer == value, MapDescriptions[i], 28);
            }
        }
    }
}
