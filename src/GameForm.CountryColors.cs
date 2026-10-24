using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private Color[] atlasColors;
        private void InitializeAtlasColors()
        {
            int count = W.Countries.Length;
            var adjacent = Enumerable.Range(0, count).Select(_ => new HashSet<int>()).ToArray();
            foreach (var region in W.Regions) foreach (int neighbor in region.Neighbors) {
                int other = W.Regions[neighbor].Country;
                if (other != region.Country) { adjacent[region.Country].Add(other); adjacent[other].Add(region.Country); }
            }
            atlasColors = new Color[count]; var assigned = new bool[count];
            foreach (int c in Enumerable.Range(0, count).OrderByDescending(i => adjacent[i].Count).ThenBy(i => W.Countries[i].Code, StringComparer.Ordinal)) {
                int preferred = (c * 7 + c / 8) % Palette.Nations.Length;
                double best = -1; Color selectedColor = Palette.Nations[preferred];
                for (int i = 0; i < Palette.Nations.Length; i++) {
                    Color color = Palette.Nations[(preferred + i) % Palette.Nations.Length];
                    double distance = adjacent[c].Where(n => assigned[n]).Select(n => ColorDistance(color, atlasColors[n])).DefaultIfEmpty(100000).Min();
                    if (distance > best) { best = distance; selectedColor = color; }
                }
                atlasColors[c] = selectedColor; assigned[c] = true;
            }
        }
        private static double ColorDistance(Color a, Color b)
        {
            double r = a.R - b.R, g = a.G - b.G, blue = a.B - b.B;
            return r * r * .3 + g * g * .59 + blue * blue * .11;
        }
    }
}
