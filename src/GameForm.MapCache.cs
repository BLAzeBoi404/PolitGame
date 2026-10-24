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
        private string cachedLayer;
        private bool deferMapRefresh;
        private readonly System.Windows.Forms.Timer cameraRefresh = new System.Windows.Forms.Timer();
        internal int MapCacheBuilds;

        private void DrawCachedMap(Graphics g)
        {
            int ownership = 17, diplomacy = 17;
            unchecked {
                foreach (var province in E.S.Provinces) ownership = ownership * 31 + province.Owner;
                if (layer == "Дипломатия") for (int i = 0; i < E.S.Nations.Length; i++)
                    diplomacy = diplomacy * 31 + (E.AtWar(E.S.Player, i) ? 2 : E.S.Agreements[E.Pair(E.S.Player, i)] ? 1 : 0);
            }
            // Static political geometry survives daily simulation updates. Data layers
            // refresh when the date changes; camera and preference changes always rebuild.
            string key = ClientSize + "/" + mapRect + "/" + cx + "/" + cy + "/" + scale + "/" + layer
                + "/" + ownership + "/" + diplomacy + "/" + E.S.Player + "/" + prefs.Labels + "/" + prefs.RegionBorders + "/" + prefs.Texture
                + "/" + ((layer == "Политика" || layer == "Дипломатия") ? 0 : E.S.Day);
            bool cameraGesture = (panning || deferMapRefresh) && mapCache != null && ReferenceEquals(mapCampaign, E.S)
                && cachedLayer == layer && mapCache.Size == ClientSize && cachedMapRect == mapRect;
            if (cameraGesture) {
                float ratio = scale / cachedScale;
                float dx = (mapRect.X + mapRect.Width / 2 - (cachedMapRect.X + cachedMapRect.Width / 2) * ratio + (cachedCx - cx) * scale) * UiScale;
                float dy = (mapRect.Y + mapRect.Height / 2 - (cachedMapRect.Y + cachedMapRect.Height / 2) * ratio + (cachedCy - cy) * scale) * UiScale;
                Fill(g, Color.FromArgb(122, 148, 154), mapRect.X, mapRect.Y, mapRect.Width, mapRect.Height);
                var gestureState = g.Save(); g.ResetTransform();
                if (ratio == 1) g.DrawImageUnscaled(mapCache, (int)dx, (int)dy);
                else g.DrawImage(mapCache, new RectangleF(dx, dy, mapCache.Width * ratio, mapCache.Height * ratio));
                g.Restore(gestureState);
                return;
            }
            if (mapCache == null || mapCacheKey != key || !ReferenceEquals(mapCampaign, E.S)) {
                if (mapCache != null) mapCache.Dispose();
                mapCache = new Bitmap(ClientSize.Width, ClientSize.Height);
                using (var surface = Graphics.FromImage(mapCache)) {
                    surface.ScaleTransform(UiScale, UiScale);
                    surface.SmoothingMode = SmoothingMode.AntiAlias;
                    surface.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    DrawMapTerrain(surface);
                }
                mapCacheKey = key; mapCampaign = E.S; MapCacheBuilds++;
                cachedCx = cx; cachedCy = cy; cachedScale = scale; cachedMapRect = mapRect; cachedLayer = layer;
            }
            var state = g.Save(); g.ResetTransform(); g.DrawImageUnscaled(mapCache, 0, 0); g.Restore(state);
        }
    }
}
