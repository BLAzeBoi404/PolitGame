using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Sovereign
{
    internal sealed class AtlasScene
    {
        public Land[] Countries, Regions;
        public Color[] CountryColors, RegionColors;
        public bool[] FillRegions;
        public string Key;
        public float X, Y, Zoom, PixelScale;
        public int Width, Height, Quality, Labels;
        public bool Borders, Texture, Overview;
        public double BuildMilliseconds;
        public RectangleF Coverage => new RectangleF(X - Width / PixelScale / 2, Y - Height / PixelScale / 2, Width / PixelScale, Height / PixelScale);
    }
    internal sealed class AtlasLabel
    {
        internal string Text;
        internal RectangleF Bounds; // Stable world-pixel coordinates, independent of tile/camera.
        internal bool Country;
    }
    internal static class AtlasRaster
    {
        public static Bitmap Render(AtlasScene scene)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var bitmap = new Bitmap(scene.Width, scene.Height, PixelFormat.Format32bppPArgb);
            try {
                using (var g = Graphics.FromImage(bitmap)) {
                    g.Clear(Color.FromArgb(122, 148, 154));
                    var view = scene.Coverage; var clip = view; clip.Inflate(2 / scene.PixelScale, 2 / scene.PixelScale);
                    using (var matrix = new Matrix(scene.PixelScale, 0, 0, scene.PixelScale, scene.Width / 2f - scene.X * scene.PixelScale, scene.Height / 2f - scene.Y * scene.PixelScale)) {
                        g.Transform = matrix; g.SmoothingMode = scene.Quality == 2 ? SmoothingMode.AntiAlias : SmoothingMode.None;
                        using (var countries = new GraphicsPath()) using (var regions = new GraphicsPath()) {
                            for (int i = 0; i < scene.Countries.Length; i++) if (scene.Countries[i].Bounds.IntersectsWith(view)) {
                                using (var path = scene.Countries[i].RasterPath(clip, scene.Overview ? 4 : scene.Zoom)) {
                                    if (path.PointCount == 0) continue;
                                    using (var brush = new SolidBrush(scene.CountryColors[i])) g.FillPath(brush, path);
                                    if (!scene.Overview) countries.AddPath(path, false);
                                }
                            }
                            for (int i = 0; i < scene.Regions.Length; i++) {
                                var land = scene.Regions[i]; if (!land.Bounds.IntersectsWith(view)) continue;
                                bool border = !scene.Overview && scene.Borders && scene.Zoom >= 10 && land.Bounds.Width * land.Bounds.Height * scene.Zoom * scene.Zoom >= 35;
                                if (!scene.FillRegions[i] && !border) continue;
                                using (var path = land.RasterPath(clip, scene.Overview ? 4 : scene.Zoom)) {
                                    if (path.PointCount == 0) continue;
                                    if (scene.FillRegions[i]) using (var brush = new SolidBrush(scene.RegionColors[i])) g.FillPath(brush, path);
                                    if (border) regions.AddPath(path, false);
                                }
                            }
                            g.SmoothingMode = SmoothingMode.None;
                            using (var pen = new Pen(Color.FromArgb(64, 64, 79, 76), .65f / scene.PixelScale)) if (regions.PointCount > 0) g.DrawPath(pen, regions);
                            using (var pen = new Pen(Color.FromArgb(91, 103, 91), 1.1f / scene.PixelScale)) if (countries.PointCount > 0) g.DrawPath(pen, countries);
                        }
                    }
                    g.ResetTransform();
                    if (scene.Texture && !scene.Overview) {
                        using (var tile = new Bitmap(32, 32)) {
                            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) tile.SetPixel(x, y, Color.FromArgb((x * 17 + y * 13) % 9, 35, 30, 20));
                            using (var brush = new TextureBrush(tile)) g.FillRectangle(brush, 0, 0, scene.Width, scene.Height);
                        }
                    }

                }
                scene.BuildMilliseconds = timer.Elapsed.TotalMilliseconds;
                return bitmap;
            } catch { bitmap.Dispose(); throw; }
        }
        // Choose labels once in world-pixel space. Per-tile collision filtering could
        // keep a label in one tile and reject its other half in the neighbouring tile.
        internal static AtlasLabel[] PlanLabels(AtlasScene s)
        {
            if(s.Labels==0 || s.Overview)return new AtlasLabel[0];
            var labels=new List<AtlasLabel>();var cells=new Dictionary<long,List<RectangleF>>();float ratio=s.PixelScale/s.Zoom;
            using(var bitmap=new Bitmap(1,1))using(var g=Graphics.FromImage(bitmap))
            using(var countryFont=new Font("Georgia",12*ratio,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var regionFont=new Font("Segoe UI",11*ratio,FontStyle.Regular,GraphicsUnit.Pixel)) {
                g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                for(int pass=0;pass<(s.Labels==2&&s.Zoom>25?2:1);pass++) {
                    var lands=pass==0?s.Countries:s.Regions;var font=pass==0?countryFont:regionFont;
                    foreach(var land in lands) {
                        if(land.Bounds.Width*s.Zoom<(pass==0?74:100)||land.Bounds.Height*s.Zoom<(pass==0?27:44))continue;
                        string text=pass==0?land.Name.ToUpperInvariant():land.Name;
                        float width=Math.Min(g.MeasureString(text,font).Width+6,190*ratio);
                        var box=new RectangleF((float)land.X*s.PixelScale-width/2,(float)land.Y*s.PixelScale,width,20*ratio);
                        int x0=(int)Math.Floor(box.Left/256),x1=(int)Math.Floor(box.Right/256),y0=(int)Math.Floor(box.Top/256),y1=(int)Math.Floor(box.Bottom/256);
                        bool overlap=false;
                        for(int x=x0;x<=x1&&!overlap;x++)for(int y=y0;y<=y1&&!overlap;y++) {
                            List<RectangleF> nearby;if(!cells.TryGetValue(((long)x<<32)^(uint)y,out nearby))continue;
                            foreach(var prior in nearby)if(prior.IntersectsWith(box)){overlap=true;break;}
                        }
                        if(overlap)continue;
                        labels.Add(new AtlasLabel {Text=text,Bounds=box,Country=pass==0});
                        for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++) {
                            long key=((long)x<<32)^(uint)y;List<RectangleF> nearby;
                            if(!cells.TryGetValue(key,out nearby))cells[key]=nearby=new List<RectangleF>();nearby.Add(box);
                        }
                    }
                }
            }
            return labels.ToArray();
        }
        internal static void Annotate(Bitmap bitmap, AtlasScene s, AtlasLabel[] labels)
        {
            if(labels.Length==0)return;float ratio=s.PixelScale/s.Zoom;
            using(var g=Graphics.FromImage(bitmap))
            using(var countryFont=new Font("Georgia",12*ratio,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var regionFont=new Font("Segoe UI",11*ratio,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var ink=new SolidBrush(Color.FromArgb(52,66,59)))
            using(var halo=new SolidBrush(Color.FromArgb(150,246,243,221)))
            using(var format=new StringFormat {Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}) {
                g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                foreach(var label in labels) {
                    var box=label.Bounds;box.Offset(s.Width/2f-s.X*s.PixelScale,s.Height/2f-s.Y*s.PixelScale);
                    if(box.Right<0||box.Bottom<0||box.Left>s.Width||box.Top>s.Height)continue;
                    var shadow=box;shadow.Offset(ratio,ratio);var font=label.Country?countryFont:regionFont;
                    g.DrawString(label.Text,font,halo,shadow,format);g.DrawString(label.Text,font,ink,box,format);
                }
            }
        }
    }
}
