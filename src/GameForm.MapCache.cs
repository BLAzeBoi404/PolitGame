using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Sovereign
{
    public partial class GameForm
    {
        private Bitmap mapCache;
        private double mapGeometryMs, mapTextureMs, mapLabelsMs;
        private string mapCacheKey;
        private Campaign mapCampaign;
        private float cachedCx, cachedCy, cachedScale;
        private RectangleF cachedMapRect;
        private Size cachedClientSize;
        private string cachedLayer;
        private bool deferMapRefresh;
        private const int CameraMargin = 128;
        private readonly System.Windows.Forms.Timer cameraRefresh = new System.Windows.Forms.Timer();
        internal int MapCacheBuilds;

        private void DrawCachedMap(Graphics g)
        {
            int ownership = 17, diplomacy = 17;
            unchecked {
                foreach (var province in E.S.Provinces) ownership = ownership * 31 + province.Owner;
                if (layer == "Дипломатия") for (int i = 0; i < E.S.Nations.Length; i++) {
                    int pair = E.Pair(E.S.Player, i);
                    diplomacy = diplomacy * 31 + (E.AtWar(E.S.Player, i) ? 1 : 0) + (E.S.Agreements[pair] ? 2 : 0)
                        + (E.Allied(E.S.Player, i) ? 4 : 0) + (E.S.Access[pair] ? 8 : 0);
                }
            }
            string key = ClientSize + "/" + mapRect + "/" + scale + "/" + layer + "/" + ownership + "/" + diplomacy + "/" + E.S.Player
                + "/" + prefs.Labels + "/" + prefs.RegionBorders + "/" + prefs.Texture + "/" + ((layer == "Политика" || layer == "Дипломатия") ? 0 : E.S.Day);
            bool compatible = mapCache != null && ReferenceEquals(mapCampaign, E.S) && cachedLayer == layer && cachedClientSize == ClientSize && cachedMapRect == mapRect;
            bool gesture = compatible && (panning || deferMapRefresh);
            bool insideMargin = compatible && cachedScale == scale && Math.Abs(cachedCx - cx) * scale * UiScale <= CameraMargin - 2
                && Math.Abs(cachedCy - cy) * scale * UiScale <= CameraMargin - 2;
            if (!gesture && (!insideMargin || mapCacheKey != key)) {
                var viewport = mapRect;
                int width = (int)Math.Ceiling(viewport.Width * UiScale) + CameraMargin * 2;
                int height = (int)Math.Ceiling(viewport.Height * UiScale) + CameraMargin * 2;
                var next = new Bitmap(width, height);
                try {
                    // Local bitmap coordinates preserve the camera center; the extra
                    // border is real geography, not stretched pixels or blank ocean.
                    mapRect = new RectangleF(0, 0, width / UiScale, height / UiScale);
                    using (var surface = Graphics.FromImage(next)) {
                        surface.ScaleTransform(UiScale, UiScale);
                        surface.SmoothingMode = SmoothingMode.AntiAlias;
                        surface.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                        DrawMapTerrain(surface);
                    }
                } catch { next.Dispose(); throw; }
                finally { mapRect = viewport; }
                if (mapCache != null) mapCache.Dispose(); mapCache = next;
                mapCacheKey = key; mapCampaign = E.S; MapCacheBuilds++;
                cachedCx = cx; cachedCy = cy; cachedScale = scale; cachedMapRect = mapRect; cachedLayer = layer; cachedClientSize = ClientSize;
            }
            float ratio = scale / cachedScale;
            float x = (mapRect.X + mapRect.Width / 2 + (cachedCx - cx) * scale) * UiScale - mapCache.Width * ratio / 2;
            float y = (mapRect.Y + mapRect.Height / 2 + (cachedCy - cy) * scale) * UiScale - mapCache.Height * ratio / 2;
            Fill(g, Color.FromArgb(122, 148, 154), mapRect.X, mapRect.Y, mapRect.Width, mapRect.Height);
            var state = g.Save(); g.ResetTransform();
            if (ratio == 1) g.DrawImageUnscaled(mapCache, (int)Math.Round(x), (int)Math.Round(y));
            else g.DrawImage(mapCache, new RectangleF(x, y, mapCache.Width * ratio, mapCache.Height * ratio));
            g.Restore(state);
        }
    }
}
