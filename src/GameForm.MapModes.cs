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
            "Концентрация боеспособных частей: светлее — больше сил. Значки показывают принадлежность.", "Производственные мощности на миллион жителей: светлее — больше.",
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
            float x = mapRect.X + 14, y = mapRect.Y + 14, width = mapRect.Width - 28;
            Fill(g, Color.FromArgb(248, 16, 25, 32), x, y, width, 67);
            Txt(g, "АТЛАС  /  " + layer.ToUpperInvariant(), x + 12, y + 9, 9, Palette.Gold, true);
            Txt(g, "МАСШТАБ " + scale.ToString("0.0") + "×", x + width - 115, y + 9, 9, Palette.Muted);
            float tab = (width - 16) / MapLayers.Length;
            for (int i = 0; i < MapLayers.Length; i++) {
                string value = MapLayers[i];
                Button(g, value, x + 8 + i * tab, y + 31, tab - 2, () => { layer = value; Invalidate(); }, layer == value, MapDescriptions[i], 28);
            }
        }
    }
}
