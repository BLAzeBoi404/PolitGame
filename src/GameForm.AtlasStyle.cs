using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace Sovereign
{
    public partial class GameForm
    {
        private Bitmap flagSheet;
        private readonly Dictionary<string, Rectangle> flagCells = new Dictionary<string, Rectangle>();
        private void CountryFlag(Graphics g, int country, RectangleF box)
        {
            if (flagSheet == null) {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("flags.png")) using (var source = new Bitmap(stream)) flagSheet = new Bitmap(source);
                using (var reader = new StreamReader(assembly.GetManifestResourceStream("flag-codes.tsv"))) {
                    reader.ReadLine(); string line; int index = 0;
                    while ((line = reader.ReadLine()) != null) {
                        var cells = line.Split('\t');
                        if (cells.Length > 1 && cells[1].Length > 0) flagCells[cells[0]] = new Rectangle(index % 16 * 64, index / 16 * 48, 64, 48);
                        index++;
                    }
                }
            }
            Rectangle sourceBox;
            Fill(g, Palette.Bg, box.X, box.Y, box.Width, box.Height);
            if (flagCells.TryGetValue(W.Countries[country].Code, out sourceBox)) g.DrawImage(flagSheet, box, sourceBox, GraphicsUnit.Pixel);
            else Txt(g, W.Countries[country].Code, box.X + 2, box.Y + box.Height / 2 - 6, 10, Palette.Text, true, box.Width - 4);
            RoundBorder(g, Color.FromArgb(180, Palette.Line), box, 1, 1);
        }
        private void PanelPlate(Graphics g, RectangleF box, bool raised = false)
        {
            using (var brush = new LinearGradientBrush(box, raised ? Color.FromArgb(40,43,54) : Color.FromArgb(26,28,37), Palette.Panel, 90)) g.FillRectangle(brush, box);
            Line(g, Color.FromArgb(91,94,107), box.X, box.Y, box.Right, box.Y);
            Line(g, Color.FromArgb(6,8,12), box.X, box.Bottom, box.Right, box.Bottom, 2);
            Line(g, Palette.Line, box.Right, box.Y, box.Right, box.Bottom);
        }
        private double[] regionAreas;
        private double RegionAreaKm2(int region)
        {
            if (regionAreas == null) regionAreas = new double[W.Regions.Length];
            if (regionAreas[region] > 0) return regionAreas[region];
            return regionAreas[region] = SphericalAreaKm2(W.Regions[region]);
        }
        private static double SphericalAreaKm2(Land land)
        {
            double total = 0, radians = Math.PI / 180;
            foreach (var ring in land.Rings) {
                double area = 0;
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++) {
                    double longitude = (ring[i].X - ring[j].X) * radians;
                    if (longitude > Math.PI) longitude -= Math.PI * 2;
                    if (longitude < -Math.PI) longitude += Math.PI * 2;
                    area += longitude * (Math.Sin(-ring[i].Y * radians) + Math.Sin(-ring[j].Y * radians));
                }
                total += Math.Abs(area) * 6371.0088 * 6371.0088 / 2;
            }
            return Math.Max(.01, total);
        }
        private Color PopulationColor(int region)
        {
            double density = E.S.Provinces[region].Population * 1000000 / RegionAreaKm2(region);
            return Mix(Color.FromArgb(38,60,91), Color.FromArgb(229,179,85), (float)Rules.Clamp(Math.Log10(1 + density) / 4, 0, 1));
        }
    }
}
