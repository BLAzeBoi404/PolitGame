using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace Sovereign
{
    internal struct AtlasTileId : IEquatable<AtlasTileId>
    {
        public int Level, X, Y;
        public AtlasTileId(int level, int x, int y) { Level = level; X = x; Y = y; }
        public bool Equals(AtlasTileId other) => Level == other.Level && X == other.X && Y == other.Y;
        public override bool Equals(object value) => value is AtlasTileId && Equals((AtlasTileId)value);
        public override int GetHashCode() => unchecked((Level * 397 ^ X) * 397 ^ Y);
    }
    internal sealed class AtlasTile : IDisposable
    {
        internal AtlasTileId Id;
        internal Bitmap Image;
        internal AtlasScene Scene;
        internal long Used;
        public void Dispose() { Image.Dispose(); }
    }
    // Bitmap ownership transfers from one raster worker to the UI thread. There are no
    // fonts, campaign reads or Control calls on the worker and at most eight pending images.
    internal sealed class AtlasTiles : IDisposable
    {
        internal const int Size = 512, Padding = 96, Limit = 96;
        private readonly object gate = new object();
        private readonly Dictionary<AtlasTileId, AtlasTile> ready = new Dictionary<AtlasTileId, AtlasTile>();
        private readonly Dictionary<int, AtlasLabel[]> labelPlans = new Dictionary<int, AtlasLabel[]>();
        private readonly HashSet<AtlasTileId> inProgress = new HashSet<AtlasTileId>();
        private readonly Queue<AtlasTile> completed = new Queue<AtlasTile>();
        private List<AtlasTileId> wanted = new List<AtlasTileId>();
        private Task worker;
        private AtlasScene source;
        private int generation;
        private bool disposed;
        private long use;
        private readonly Action wake;
        internal int Builds, Hits;
        internal double LastBuildMilliseconds;
        internal string LastError;
        internal int Count => ready.Count;
        internal bool Busy { get { lock (gate) return worker != null || completed.Count > 0; } }
        internal IEnumerable<AtlasTile> Ready => ready.Values;
        internal static float Pixels(int level) => (float)(level / 256.0);
        internal static int Level(float pixels) => Math.Max(256, Math.Min(256000, (int)Math.Round(pixels * 256)));
        internal static RectangleF Bounds(AtlasTileId id)
        {
            float size = Size / Pixels(id.Level); return new RectangleF(-180 + id.X * size, -90 + id.Y * size, size, size);
        }
        internal AtlasTiles(Action invalidate) { wake = invalidate; }
        internal void Reset(AtlasScene scene)
        {
            lock (gate) {
                generation++; source = scene; wanted.Clear(); inProgress.Clear();
                while (completed.Count > 0) completed.Dequeue().Dispose();
            }
            foreach (var tile in ready.Values) tile.Dispose(); ready.Clear(); labelPlans.Clear(); LastError = null;
        }
        internal AtlasTile Find(AtlasTileId id)
        {
            AtlasTile tile; if (!ready.TryGetValue(id, out tile)) return null;
            tile.Used = ++use; Hits++; return tile;
        }
        private void Annotate(AtlasTile tile)
        {
            AtlasLabel[] labels;
            if(!labelPlans.TryGetValue(tile.Id.Level,out labels)) {
                // Zoom sweeps cannot accumulate unlimited label plans either.
                if(labelPlans.Count>=12)labelPlans.Clear();
                labels=AtlasRaster.PlanLabels(tile.Scene);labelPlans.Add(tile.Id.Level,labels);
            }
            AtlasRaster.Annotate(tile.Image,tile.Scene,labels);
        }
        internal void Collect()
        {
            lock (gate) {
                int collected = 0;
                while (completed.Count > 0 && collected++ < 2) {
                    var tile = completed.Dequeue(); 
                    AtlasTile old; if (ready.TryGetValue(tile.Id, out old)) old.Dispose();
                    Annotate(tile);
                    tile.Used = ++use; ready[tile.Id] = tile; Builds++; LastBuildMilliseconds = tile.Scene.BuildMilliseconds;
                }
            }
            bool more; lock (gate) more = completed.Count > 0;
            if (more) wake(); // Keep draining bounded batches even after the raster worker stops.
            while (ready.Count > Limit) {
                AtlasTile oldest = null; foreach (var tile in ready.Values) if (oldest == null || tile.Used < oldest.Used) oldest = tile;
                ready.Remove(oldest.Id); lock (gate) inProgress.Remove(oldest.Id); oldest.Dispose();
            }
        }
        private AtlasScene Scene(AtlasTileId id, AtlasScene style)
        {
            var b = Bounds(id);
            return new AtlasScene { Countries = style.Countries, Regions = style.Regions, CountryColors = style.CountryColors, RegionColors = style.RegionColors,
                FillRegions = style.FillRegions, Key = style.Key, X = b.X + b.Width / 2, Y = b.Y + b.Height / 2,
                Width = Size + Padding * 2, Height = Size + Padding * 2, PixelScale = Pixels(id.Level), Zoom = Pixels(id.Level) / (style.PixelScale / style.Zoom),
                Quality = style.Quality, Labels = style.Labels, Borders = style.Borders, Texture = style.Texture };
        }
        internal void Request(List<AtlasTileId> ids, bool synchronous)
        {
            if (disposed) return;
            var missing = new List<AtlasTileId>();
            foreach (var id in ids) if (Find(id) == null) missing.Add(id);
            if (synchronous) {
                // Deterministic screenshots only. The interactive path never rasterizes tiles here.
                foreach (var id in missing) {
                    var scene = Scene(id, source); var tile = new AtlasTile { Id = id, Scene = scene, Image = AtlasRaster.Render(scene), Used = ++use };
                    Annotate(tile); ready[id] = tile; Builds++; LastBuildMilliseconds = scene.BuildMilliseconds;
                }
                Collect(); return;
            }
            lock (gate) {
                wanted = missing;
                if (worker != null || wanted.Count == 0 || completed.Count >= 8) return;
                worker = Task.Run((Action)Run);
            }
        }
        private void Run()
        {
            while (true) {
                AtlasTileId id = default(AtlasTileId); AtlasScene style; int revision; bool found = false;
                lock (gate) {
                    if (!disposed && completed.Count < 8) foreach (var candidate in wanted) if (inProgress.Add(candidate)) { id = candidate; found = true; break; }
                    if (!found) { worker = null; return; }
                    style = source; revision = generation;
                }
                AtlasTile tile = null;
                try { var scene = Scene(id, style); tile = new AtlasTile { Id = id, Scene = scene, Image = AtlasRaster.Render(scene) }; }
                catch (Exception e) { lock (gate) { LastError = e.Message; inProgress.Remove(id); worker = null; } wake(); return; }
                lock (gate) {
                    if (disposed || revision != generation) tile.Dispose();
                    else completed.Enqueue(tile);
                }
                wake();
            }
        }
        public void Dispose()
        {
            lock (gate) { disposed = true; generation++; wanted.Clear(); while (completed.Count > 0) completed.Dequeue().Dispose(); }
            foreach (var tile in ready.Values) tile.Dispose(); ready.Clear();
        }
    }
}
