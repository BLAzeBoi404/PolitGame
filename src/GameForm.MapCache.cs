using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private Bitmap mapCache, atlasOverview;
        private string mapCacheKey, overviewKey;
        private AtlasScene cachedScene, requestedScene, renderingScene;
        private bool synchronousAtlas, atlasDisposed;
        private const int CameraMargin = 128;
        private readonly Timer cameraRefresh = new Timer();
        private Task atlasWorker;
        private readonly object atlasGate = new object();
        internal int MapCacheBuilds, AtlasFallbackFrames;
        private double atlasAnnotationMs;
        private readonly System.Collections.Generic.HashSet<Bitmap> pendingAtlases = new System.Collections.Generic.HashSet<Bitmap>();
        private string mapRasterProfile => cachedScene == null ? "pending" : cachedScene.Width + "x" + cachedScene.Height + ", background raster " + cachedScene.BuildMilliseconds.ToString("0.00") + " ms; UI annotation " + atlasAnnotationMs.ToString("0.00") + " ms";
        private string AtlasContentKey(bool setup)
        {
            int ownership = 17, theme = 17, diplomacy = 17;
            unchecked {
                if (!setup) foreach (var p in E.S.Provinces) {
                    ownership = ownership * 31 + p.Owner;
                    if (layer == "Ресурсы") theme = theme * 31 + (p.Oil + p.Ore + p.Soil).GetHashCode();
                    else if (layer == "Инфраструктура") theme = theme * 31 + p.Infrastructure.GetHashCode();
                    else if (layer == "Настроения") theme = theme * 31 + p.Unrest.GetHashCode();
                    else if (layer == "Экономика") { double plants = 0; for (int i = 0; i < 8; i++) plants += p.Public[i] + p.Private[i]; theme = theme * 31 + (plants / Math.Max(.001, p.Population)).GetHashCode(); }
                }
                if (!setup && layer == "Армии") foreach (var a in E.S.Armies) theme = theme * 31 + a.Region * 397 + a.Strength.GetHashCode();
                if (!setup && layer == "Дипломатия") for (int i = 0; i < E.S.Nations.Length; i++) { int p = E.Pair(E.S.Player, i); diplomacy = diplomacy * 31 + (E.AtWar(E.S.Player, i) ? 1 : 0) + (E.S.Agreements[p] ? 2 : 0) + (E.Allied(E.S.Player, i) ? 4 : 0) + (E.S.Access[p] ? 8 : 0); }
            }
            return (setup ? "setup" : E.S.GetHashCode().ToString()) + "/" + layer + "/" + ownership + "/" + theme + "/" + diplomacy + "/" + prefs.Labels + "/" + prefs.RegionBorders + "/" + prefs.Texture + "/" + prefs.GraphicsQuality + "/" + prefs.LabelDensity;
        }
        private AtlasScene CaptureAtlas(string key, bool overview)
        {
            bool setup = menuScreen == "new"; float resolution = prefs.GraphicsQuality == 0 ? .65f : prefs.GraphicsQuality == 1 ? .85f : 1;
            if (layer == "Армии" && !setup) { if (militaryIntensity == null) militaryIntensity = new double[W.Regions.Length]; Array.Clear(militaryIntensity, 0, militaryIntensity.Length); foreach (var a in E.S.Armies) militaryIntensity[a.Region] += a.Strength; }
            var scene = new AtlasScene { Countries = W.Countries, Regions = W.Regions, CountryColors = new Color[W.Countries.Length], RegionColors = new Color[W.Regions.Length], FillRegions = new bool[W.Regions.Length], Key = key,
                X = overview ? 0 : cx, Y = overview ? 0 : cy, Zoom = overview ? 4 : scale, PixelScale = overview ? 4 : scale * UiScale * resolution,
                Width = overview ? 1440 : (int)Math.Ceiling(mapRect.Width * UiScale * resolution) + CameraMargin * 2,
                Height = overview ? 720 : (int)Math.Ceiling(mapRect.Height * UiScale * resolution) + CameraMargin * 2,
                Quality = prefs.GraphicsQuality, Labels = prefs.Labels ? prefs.LabelDensity : 0, Borders = prefs.RegionBorders, Texture = prefs.Texture, Overview = overview };
            for (int i = 0; i < scene.CountryColors.Length; i++) scene.CountryColors[i] = CountryColor(i);
            if (!setup) for (int i = 0; i < scene.RegionColors.Length; i++) { scene.RegionColors[i] = RegionColor(i); scene.FillRegions[i] = layer != "Политика" || E.S.Provinces[i].Owner != W.Regions[i].Country; }
            return scene;
        }
        private void PublishAtlas(AtlasScene scene, Bitmap next)
        {
            if (atlasDisposed || scene.Key != overviewKey) { next.Dispose(); return; }
            // Font backends may share native caches. Annotate completed geography on the UI thread.
            var timer = System.Diagnostics.Stopwatch.StartNew(); AtlasRaster.Annotate(next, scene); atlasAnnotationMs = timer.Elapsed.TotalMilliseconds;
            if (mapCache != null) mapCache.Dispose(); mapCache = next; cachedScene = scene; mapCacheKey = scene.Key; MapCacheBuilds++; Invalidate();
        }
        private static bool SameAtlasRequest(AtlasScene a, AtlasScene b) => a != null && a.Key == b.Key && a.X == b.X && a.Y == b.Y && a.Zoom == b.Zoom && a.Width == b.Width && a.Height == b.Height;
        private void RequestAtlas(AtlasScene scene)
        {
            lock (atlasGate) {
                if (SameAtlasRequest(requestedScene, scene) || (requestedScene == null && SameAtlasRequest(renderingScene, scene))) return;
                requestedScene = scene;
                if (atlasWorker != null && !atlasWorker.IsCompleted) return;
                atlasWorker = Task.Run(() => {
                    while (true) {
                        AtlasScene job;
                        lock (atlasGate) { if (atlasDisposed || requestedScene == null) { atlasWorker = null; renderingScene = null; return; } job = requestedScene; renderingScene = job; requestedScene = null; }
                        Bitmap result;
                        try { result = AtlasRaster.Render(job); }
                        catch (Exception ex) { lock (atlasGate) { atlasWorker = null; requestedScene = null; renderingScene = null; } System.Diagnostics.Debug.WriteLine(ex); return; }
                        lock (atlasGate) {
                            if (atlasDisposed) { result.Dispose(); atlasWorker = null; return; }
                            foreach (var obsolete in pendingAtlases) obsolete.Dispose(); pendingAtlases.Clear();
                            pendingAtlases.Add(result);
                            try { BeginInvoke((Action)(() => { lock (atlasGate) { if (!pendingAtlases.Remove(result)) return; } PublishAtlas(job, result); })); }
                            catch (InvalidOperationException) { pendingAtlases.Remove(result); result.Dispose(); }
                        }
                    }
                });
            }
        }
        internal void WaitForAtlas()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 10000) {
                Task worker; lock (atlasGate) worker = atlasWorker;
                if (worker == null) { Application.DoEvents(); break; }
                worker.Wait(5); Application.DoEvents();
            }
        }
        private void DisposeAtlas()
        {
            lock (atlasGate) { atlasDisposed = true; requestedScene = null; foreach (var bitmap in pendingAtlases) bitmap.Dispose(); pendingAtlases.Clear(); }
            if (atlasOverview != null) { atlasOverview.Dispose(); atlasOverview = null; }
        }
        private void DrawCachedMap(Graphics g)
        {
            bool setup = menuScreen == "new"; string key = AtlasContentKey(setup);
            if (atlasOverview == null || overviewKey != key) {
                var next = AtlasRaster.Render(CaptureAtlas(key, true));
                if (atlasOverview != null) atlasOverview.Dispose(); atlasOverview = next; overviewKey = key;
            }
            var view = new RectangleF(cx - mapRect.Width / scale / 2, cy - mapRect.Height / scale / 2, mapRect.Width / scale, mapRect.Height / scale);
            bool covers = mapCache != null && mapCacheKey == key && cachedScene.Coverage.Contains(view);
            bool exact = covers && cachedScene.Zoom == scale && cachedScene.Width == (int)Math.Ceiling(mapRect.Width * UiScale * (prefs.GraphicsQuality == 0 ? .65f : prefs.GraphicsQuality == 1 ? .85f : 1)) + CameraMargin * 2;
            exact = exact && cachedScene.Height == (int)Math.Ceiling(mapRect.Height * UiScale * (prefs.GraphicsQuality == 0 ? .65f : prefs.GraphicsQuality == 1 ? .85f : 1)) + CameraMargin * 2;
            var ahead = view; ahead.Inflate(CameraMargin * .45f / (scale * UiScale), CameraMargin * .45f / (scale * UiScale));
            bool prefetch = exact && !cachedScene.Coverage.Contains(ahead);
            if (!exact || (!synchronousAtlas && prefetch) || mapCacheKey == null) {
                var scene = CaptureAtlas(key, false);
                if (synchronousAtlas) { PublishAtlas(scene, AtlasRaster.Render(scene)); covers = true; }
                else RequestAtlas(scene);
            }
            Fill(g, Color.FromArgb(122, 148, 154), mapRect.X, mapRect.Y, mapRect.Width, mapRect.Height);
            // Draw one complete coverage surface; never expose partial cache strips as ocean.
            if (covers && mapCache != null) {
                float factor = scale / cachedScene.PixelScale;
                float x = mapRect.X + mapRect.Width / 2 + (cachedScene.X - cx) * scale - mapCache.Width * factor / 2;
                float y = mapRect.Y + mapRect.Height / 2 + (cachedScene.Y - cy) * scale - mapCache.Height * factor / 2;
                var state = g.Save();
                if (Math.Abs(factor * UiScale - 1) < .0001) { g.ResetTransform(); g.DrawImageUnscaled(mapCache, (int)Math.Round(x * UiScale), (int)Math.Round(y * UiScale)); }
                else { g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(mapCache, new RectangleF(x, y, mapCache.Width * factor, mapCache.Height * factor)); }
                g.Restore(state);
            } else {
                AtlasFallbackFrames++;
                float x = mapRect.X + mapRect.Width / 2 + (-180 - cx) * scale, y = mapRect.Y + mapRect.Height / 2 + (-90 - cy) * scale;
                g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(atlasOverview, new RectangleF(x, y, 360 * scale, 180 * scale));
            }
        }
    }
}
