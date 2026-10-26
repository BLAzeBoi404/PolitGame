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
            var windowHandle = Handle;
            prefs.GraphicsQuality = 2; prefs.Labels = true; prefs.LabelDensity = 2; prefs.RegionBorders = true; prefs.Texture = true; prefs.FrameLimit = 60; prefs.PerformanceOverlay = false;
            E.AutoSaveEnabled = false; ClientSize = new Size(1600, 900); menuCountry = W.ByCode("UKR"); BeginCampaign(); speed = 0;
            var report = new List<string> { "1600x900, " + Environment.OSVersion.Platform + " / " + (Type.GetType("Mono.Runtime") != null ? "Mono" : ".NET Framework") + ", fixed camera; high quality / native raster resolution; CPU input + paint times, not display FPS. Autosave disabled." };
            using (var target = new Bitmap(1600, 900)) using (var g = Graphics.FromImage(target)) {
                Action paint = () => { g.ResetTransform(); OnPaint(new PaintEventArgs(g, ClientRectangle)); };
                Action warm = () => { paint(); WaitForAtlas(); paint(); WaitForAtlas(); };
                Action<string, int, Action<int>> measure = (name, count, step) => {
                    var values = new List<double>(); int gc0=GC.CollectionCount(0), gc1=GC.CollectionCount(1);
                    for (int i = 0; i < count; i++) { var watch = Stopwatch.StartNew(); step(i); paint(); values.Add(watch.Elapsed.TotalMilliseconds); }
                    values.Sort(); report.Add(name + ": mean=" + values.Average().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " p95=" + values[(int)Math.Floor((count - 1) * .95)].ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " max=" + values.Last().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms; samples=" + count + "; GC0=" + (GC.CollectionCount(0)-gc0) + "; GC1=" + (GC.CollectionCount(1)-gc1));
                };
                cx = 20; cy = -50; scale = 18; selected = hoveredRegion = -1; warm();
                measure("Cached political map", 60, i => { });
                measure("Hover and exact hit testing", 60, i => OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 420 + i * 9, 370 + i % 5, 0)));
                selected = W.Countries[W.ByCode("RUS")].Regions.OrderByDescending(r => W.Regions[r].Path.PointCount).First();
                cx = (float)W.Regions[selected].X; cy = (float)W.Regions[selected].Y; scale = 30; warm();
                measure("Complex selected province", 30, i => { });
                selected = hoveredRegion = -1; cx = 20; cy = -50; scale = 18; warm(); panning = true;
                measure("Camera drag", 60, i => cx += .03f); panning = false; warm();
                measure("Camera rebuild", 12, i => { mapCacheKey = null; cx += .15f; });
                WaitForAtlas(); selected = hoveredRegion = -1; cx = 20; cy = -50; scale = 45; warm();
                measure("Medium zoom cached / many provinces", 60, i => { });
                panning = true;
                measure("Medium zoom long drag / async detail", 90, i => { cx += .35f; if (i % 3 == 0) Application.DoEvents(); });
                panning = false; var resolve = Stopwatch.StartNew(); warm();
                report.Add("Latest camera detail settled in " + resolve.Elapsed.TotalMilliseconds.ToString("0.00") + " ms; fallback frames=" + AtlasFallbackFrames);
                report.Add("Raster stages: " + mapRasterProfile);
                layer = "Ресурсы"; warm();
                measure("Resource map advancing daily", 20, i => E.Tick());
                layer = "Политика"; Navigate("Карта"); warm();
                measure("Seven-day simulation burst", 20, i => { for (int day = 0; day < 7; day++) E.Tick(); });
                Navigate("Исследования"); detailPanel = true; warm();
                measure("Research workspace", 40, i => { });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllLines(path, report); foreach (var line in report) Console.WriteLine(line);
        }
    }
}
