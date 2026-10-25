using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        internal void Benchmark(string path)
        {
            clock.Stop(); cameraRefresh.Stop(); prefs.Autosave = false; prefs.Tutorial = false; prefs.Fullscreen = false;
            E.AutoSaveEnabled = false; ClientSize = new Size(1600, 900); menuCountry = W.ByCode("UKR"); BeginCampaign(); speed = 0;
            var report = new List<string> { "1600x900, " + Environment.OSVersion.Platform + " / " + (Type.GetType("Mono.Runtime") != null ? "Mono" : ".NET Framework") + ", fixed camera; CPU input + paint times, not display FPS. Autosave disabled." };
            using (var target = new Bitmap(1600, 900)) using (var g = Graphics.FromImage(target)) {
                Action paint = () => { g.ResetTransform(); OnPaint(new PaintEventArgs(g, ClientRectangle)); };
                Action<string, int, Action<int>> measure = (name, count, step) => {
                    var values = new List<double>();
                    for (int i = 0; i < count; i++) { var watch = Stopwatch.StartNew(); step(i); paint(); values.Add(watch.Elapsed.TotalMilliseconds); }
                    values.Sort(); report.Add(name + ": mean=" + values.Average().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " p95=" + values[(int)Math.Floor((count - 1) * .95)].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " max=" + values.Last().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms; samples=" + count);
                };
                cx = 20; cy = -50; scale = 18; selected = hoveredRegion = -1; paint(); paint();
                measure("Cached political map", 60, i => { });
                measure("Hover and exact hit testing", 60, i => OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 420 + i * 9, 370 + i % 5, 0)));
                selected = W.Countries[W.ByCode("RUS")].Regions.OrderByDescending(r => W.Regions[r].Path.PointCount).First();
                cx = (float)W.Regions[selected].X; cy = (float)W.Regions[selected].Y; scale = 30; paint();
                measure("Complex selected province", 30, i => { });
                selected = hoveredRegion = -1; cx = 20; cy = -50; scale = 18; paint(); panning = true;
                measure("Camera drag", 60, i => cx += .03f); panning = false; paint();
                measure("Camera rebuild", 12, i => { mapCacheKey = null; cx += .15f; });
                report.Add("Raster stages: " + mapRasterProfile); report.Add("Geometry=" + mapGeometryMs.ToString("0.0") + " texture=" + mapTextureMs.ToString("0.0") + " labels=" + mapLabelsMs.ToString("0.0") + " ms");
                layer = "Ресурсы"; paint();
                measure("Resource map advancing daily", 20, i => E.Tick());
                layer = "Политика"; Navigate("Карта"); paint();
                measure("Seven-day simulation burst", 20, i => { for (int day = 0; day < 7; day++) E.Tick(); });
                Navigate("Исследования"); detailPanel = true; paint();
                measure("Research workspace", 40, i => { });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllLines(path, report); foreach (var line in report) Console.WriteLine(line);
        }
    }
}
