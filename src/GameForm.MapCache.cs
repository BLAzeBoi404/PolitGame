using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private Bitmap atlasOverview;
        private System.Threading.Tasks.Task<Bitmap> overviewTask;
        private string overviewTaskKey;
        private bool atlasStateDirty=true;
        private AtlasScene cachedScene;
        private AtlasTiles atlasTiles;
        private string mapCacheKey, overviewKey, tileContentKey, stableAtlasKey;
        private bool synchronousAtlas, atlasDisposed;
        private int atlasWakeQueued;
        private const int CameraMargin = 256;
        private readonly Timer cameraRefresh = new Timer();
        private List<AtlasTileId> currentTileRequests = new List<AtlasTileId>();
        internal int MapCacheBuilds => atlasTiles == null ? 0 : atlasTiles.Builds;
        internal int AtlasFallbackFrames, AtlasMissingTiles;
        internal int AtlasTileHits => atlasTiles == null ? 0 : atlasTiles.Hits;
        internal int AtlasResidentTiles => atlasTiles == null ? 0 : atlasTiles.Count;
        internal float AtlasPixelScale;
        private bool AtlasBusy => overviewTask!=null || (atlasTiles != null && atlasTiles.Busy);
        private string mapRasterProfile => "tiles=" + AtlasResidentTiles + "/" + AtlasTiles.Limit + "; background tile=" + (atlasTiles == null ? "0" : atlasTiles.LastBuildMilliseconds.ToString("0.00")) + " ms; tile annotations cached";
        private string AtlasContentKey(bool setup)
        {
            if(stableAtlasKey!=null&&(simulationBusy||(gpuPaintingMap&&!atlasStateDirty)))return stableAtlasKey;
            atlasStateDirty=false;
            int ownership = 17, theme = 17, diplomacy = 17;
            unchecked {
                if (!setup) for (int regionId=0;regionId<E.S.Provinces.Length;regionId++) {
                    var p=E.S.Provinces[regionId];
                    ownership = ownership * 31 + p.Owner;
                    if (layer == "Население") theme = theme * 31 + PopulationColor(regionId).ToArgb();
                    else if (layer == "Ресурсы") theme = theme * 31 + (p.Oil + p.Ore + p.Soil).GetHashCode();
                    else if (layer == "Инфраструктура") theme = theme * 31 + p.Infrastructure.GetHashCode();
                    else if (layer == "Настроения") theme = theme * 31 + p.Unrest.GetHashCode();
                    else if (layer == "Экономика") { double plants = 0; for (int i = 0; i < 8; i++) plants += p.Public[i] + p.Private[i]; theme = theme * 31 + (plants / Math.Max(.001, p.Population)).GetHashCode(); }
                }
                if (!setup && layer == "Армии") {
                    UpdateMilitaryIntensity();
                    // Hash the rendered aggregate, not every changing troop count.
                    // Small replacements in an already saturated area do not change
                    // a pixel and must not invalidate thousands of cached tiles.
                    for (int region = 0; region < W.Regions.Length; region++) theme = theme * 31 + RegionColor(region).ToArgb();
                }
                if (!setup && layer == "Дипломатия") for (int i = 0; i < E.S.Nations.Length; i++) { int p = E.Pair(E.S.Player, i); diplomacy = diplomacy * 31 + (E.AtWar(E.S.Player, i) ? 1 : 0) + (E.S.Agreements[p] ? 2 : 0) + (E.Allied(E.S.Player, i) ? 4 : 0) + (E.S.Access[p] ? 8 : 0); }
            }
            return stableAtlasKey = (setup ? "setup" : E.S.GetHashCode().ToString()) + "/" + layer + "/" + ownership + "/" + theme + "/" + diplomacy + "/" + prefs.Labels + "/" + prefs.RegionBorders + "/" + prefs.Texture + "/" + prefs.GraphicsQuality + "/" + prefs.LabelDensity;
        }
        private void UpdateMilitaryIntensity()
        {
            if (militaryIntensity == null) militaryIntensity = new double[W.Regions.Length];
            Array.Clear(militaryIntensity, 0, militaryIntensity.Length);
            foreach (var a in E.S.Armies) if (E.SharesMilitaryIntel(E.S.Player, a.Owner)) militaryIntensity[a.Region] += a.Personnel / 8.0;
            foreach (var c in E.KnownMilitaryContacts(E.S.Player)) militaryIntensity[c.Region] += ((double)c.LowerPersonnel + c.UpperPersonnel) / 16.0;
        }
        private AtlasScene CaptureAtlas(string key, bool overview)
        {
            bool setup = menuScreen == "new"; float resolution = prefs.GraphicsQuality == 0 ? .65f : prefs.GraphicsQuality == 1 ? .85f : 1;
            if (layer == "Армии" && !setup && militaryIntensity == null) UpdateMilitaryIntensity();
            var scene = new AtlasScene { Countries = W.Countries, Regions = W.Regions, CountryColors = new Color[W.Countries.Length], RegionColors = new Color[W.Regions.Length], FillRegions = new bool[W.Regions.Length], Key = key,
                X = overview ? 0 : cx, Y = overview ? 0 : cy, Zoom = overview ? 4 : scale, PixelScale = overview ? 4 : scale * UiScale * resolution,
                Width = overview ? 1440 : (int)Math.Ceiling(mapRect.Width * UiScale * resolution) + CameraMargin * 2,
                Height = overview ? 720 : (int)Math.Ceiling(mapRect.Height * UiScale * resolution) + CameraMargin * 2,
                Quality = prefs.GraphicsQuality, Labels = prefs.Labels ? prefs.LabelDensity : 0, Borders = prefs.RegionBorders, Texture = prefs.Texture, Overview = overview };
            for (int i = 0; i < scene.CountryColors.Length; i++) scene.CountryColors[i] = CountryColor(i);
            if (!setup) for (int i = 0; i < scene.RegionColors.Length; i++) { scene.RegionColors[i] = RegionColor(i); scene.FillRegions[i] = layer != "Политика" || E.S.Provinces[i].Owner != W.Regions[i].Country; }
            return scene;
        }
        private void WakeAtlas()
        {
            if (atlasDisposed || !IsHandleCreated || System.Threading.Interlocked.Exchange(ref atlasWakeQueued, 1) != 0) return;
            try { BeginInvoke((Action)(() => { System.Threading.Interlocked.Exchange(ref atlasWakeQueued, 0); if (!atlasDisposed) InvalidateCamera(); })); }
            catch (InvalidOperationException) { System.Threading.Interlocked.Exchange(ref atlasWakeQueued, 0); }
        }
        internal void WaitForAtlas()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!atlasDisposed && atlasTiles != null && watch.ElapsedMilliseconds < 10000) {
                atlasTiles.Collect(); atlasTiles.Request(currentTileRequests, false);
                if(overviewTask!=null&&overviewTask.IsCompleted){if(gpu!=null)PresentGpu();else using(var bitmap=RenderFrame()){};}
                if (!AtlasBusy) break;
                Application.DoEvents(); System.Threading.Thread.Sleep(1);
            }
            if (atlasTiles != null) atlasTiles.Collect();
        }
        private void DisposeAtlas()
        {
            atlasDisposed = true;
            if (atlasTiles != null) atlasTiles.Dispose();
            if (atlasOverview != null) atlasOverview.Dispose();
            if(overviewTask!=null){try {overviewTask.GetAwaiter().GetResult().Dispose();}catch{}overviewTask=null;}
        }
        private RectangleF AtlasView => new RectangleF(cx - mapRect.Width / scale / 2, cy - mapRect.Height / scale / 2, mapRect.Width / scale, mapRect.Height / scale);
        // Crop the SOURCE before scaling. GDI+ otherwise resamples a world-sized image
        // into tens of thousands of pixels at close zoom, even with a viewport clip.
        private void DrawAtlasSurface(Graphics g, Bitmap image, RectangleF coverage, RectangleF part, RectangleF view)
        {
            var visible = RectangleF.Intersect(RectangleF.Intersect(coverage, part), view);
            if (visible.Width <= 0 || visible.Height <= 0) return;
            var origin = Screen(visible.X, visible.Y);
            var destination = new RectangleF(origin.X * UiScale, origin.Y * UiScale, visible.Width * scale * UiScale, visible.Height * scale * UiScale);
            var source = new RectangleF((visible.X - coverage.X) / coverage.Width * image.Width, (visible.Y - coverage.Y) / coverage.Height * image.Height,
                visible.Width / coverage.Width * image.Width, visible.Height / coverage.Height * image.Height);
            if(gpuPaintingMap){gpu.Image(image,destination,source,image==atlasOverview);return;}
            float imagePixels = image.Width / coverage.Width;
            if (Math.Abs(imagePixels - scale * UiScale) < .003) {
                var position = Screen(coverage.X, coverage.Y);
                var clip = Rectangle.FromLTRB((int)Math.Round(destination.Left), (int)Math.Round(destination.Top), (int)Math.Round(destination.Right), (int)Math.Round(destination.Bottom));
                var saved = g.Save(); g.SetClip(clip, CombineMode.Intersect);
                g.DrawImageUnscaled(image, (int)Math.Round(position.X * UiScale), (int)Math.Round(position.Y * UiScale)); g.Restore(saved);
            } else g.DrawImage(image, destination, source, GraphicsUnit.Pixel);
        }
        private List<AtlasTileId> VisibleTiles(int level, RectangleF view, int margin)
        {
            float width = AtlasTiles.Size / AtlasTiles.Pixels(level);
            int x0 = Math.Max(0, (int)Math.Floor((view.Left + 180) / width) - margin), x1 = Math.Min((int)Math.Ceiling(360 / width) - 1, (int)Math.Floor((view.Right + 180) / width) + margin);
            int y0 = Math.Max(0, (int)Math.Floor((view.Top + 90) / width) - margin), y1 = Math.Min((int)Math.Ceiling(180 / width) - 1, (int)Math.Floor((view.Bottom + 90) / width) + margin);
            var list = new List<AtlasTileId>();
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) list.Add(new AtlasTileId(level, x, y));
            list.Sort((a,b) => {
                var aa = AtlasTiles.Bounds(a); var bb = AtlasTiles.Bounds(b);
                int visible = (bb.IntersectsWith(view) ? 1 : 0) - (aa.IntersectsWith(view) ? 1 : 0); if (visible != 0) return visible;
                return (Math.Abs(aa.X + aa.Width/2 - cx) + Math.Abs(aa.Y + aa.Height/2 - cy)).CompareTo(Math.Abs(bb.X + bb.Width/2 - cx) + Math.Abs(bb.Y + bb.Height/2 - cy));
            });
            return list;
        }
        private void DrawCachedMap(Graphics g)
        {
            bool setup = menuScreen == "new"; string key = AtlasContentKey(setup);
            if(overviewTask!=null&&overviewTask.IsCompleted) {
                if(overviewTask.Status==System.Threading.Tasks.TaskStatus.RanToCompletion) {
                    var next=overviewTask.Result;
                    if(overviewTaskKey==key){if(gpu!=null)gpu.Remove(atlasOverview);atlasOverview.Dispose();atlasOverview=next;overviewKey=key;}
                    else next.Dispose();
                }
                overviewTask=null;
            }
            if (atlasOverview == null || (overviewKey != key && synchronousAtlas)) {
                var next = AtlasRaster.Render(CaptureAtlas(key, true));
                if (atlasOverview != null) {if(gpu!=null)gpu.Remove(atlasOverview);atlasOverview.Dispose();} atlasOverview = next; overviewKey = key;
            } else if(overviewKey!=key&&overviewTask==null&&!simulationBusy) {
                var snapshot=CaptureAtlas(key,true);overviewTaskKey=key;
                overviewTask=System.Threading.Tasks.Task.Run(()=>{try{return AtlasRaster.Render(snapshot);}finally{WakeAtlas();}});
            }
            if (atlasTiles == null) atlasTiles = new AtlasTiles(WakeAtlas);
            string content = key + "/" + UiScale + (scale >= 10 ? "/regions" : "/countries") + (scale > 25 ? "/labels" : "/country-labels");
            if (tileContentKey != content || mapCacheKey == null) {
                if(!simulationBusy)cachedScene = CaptureAtlas(key, false);
                atlasTiles.Reset(cachedScene); tileContentKey = content; mapCacheKey = key;
            }
            atlasTiles.Collect();
            var view = AtlasView; float quality = prefs.GraphicsQuality == 0 ? .65f : prefs.GraphicsQuality == 1 ? .85f : 1;
            int level = AtlasTiles.Level(scale * UiScale * quality); AtlasPixelScale = AtlasTiles.Pixels(level);
            currentTileRequests = VisibleTiles(level, view, 1);
            atlasTiles.Request(currentTileRequests, synchronousAtlas);
            var state = g.Save(); g.ResetTransform();
            g.InterpolationMode = InterpolationMode.Bilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
            if(gpuPaintingMap)gpu.Fill(Palette.Sea,Physical(mapRect));
            else using (var sea = new SolidBrush(Palette.Sea)) g.FillRectangle(sea, mapRect.X * UiScale, mapRect.Y * UiScale, mapRect.Width * UiScale, mapRect.Height * UiScale);
            int missing = 0;
            foreach (var id in currentTileRequests) if (AtlasTiles.Bounds(id).IntersectsWith(view)) {
                var tile=atlasTiles.Find(id);if(tile==null||(gpuPaintingMap&&!gpu.HasTexture(tile.Image)))missing++;
            }
            if (missing > 0) DrawAtlasSurface(g, atlasOverview, new RectangleF(-180,-90,360,180), view, view);
            // Keep already detailed geography while a new zoom level or edge is loading.
            // Only missing tiles use the overview; a cache miss never blurs the entire screen.
            if (missing > 0) foreach (var tile in atlasTiles.Ready) if (tile.Id.Level != level && tile.Id.Level > level * .5 && tile.Id.Level < level * 2)
                DrawAtlasSurface(g, tile.Image, tile.Scene.Coverage, AtlasTiles.Bounds(tile.Id), view);
            foreach (var id in currentTileRequests) {
                var bounds = AtlasTiles.Bounds(id); if (!bounds.IntersectsWith(view)) continue;
                var tile = atlasTiles.Find(id);
                if (tile == null) continue;
                DrawAtlasSurface(g, tile.Image, tile.Scene.Coverage, bounds, view);
            }
            // Upload the nearby margin within the same budget. No visible tile waits
            // behind prefetch and a CPU-ready/GPU-pending tile still has a backdrop.
            if(gpuPaintingMap)foreach(var id in currentTileRequests)if(!AtlasTiles.Bounds(id).IntersectsWith(view)){var tile=atlasTiles.Find(id);if(tile!=null)gpu.Warm(tile.Image);}
            AtlasMissingTiles = missing; if (missing > 0) AtlasFallbackFrames++;
            g.Restore(state);
        }
    }
}
