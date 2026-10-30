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
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Land[],float[]> textWidths = new System.Runtime.CompilerServices.ConditionalWeakTable<Land[],float[]>();
        internal static void PrepareText(Land[] countries,Land[] regions)
        {
            PrepareText(countries,true);PrepareText(regions,false);
        }
        private static float[] PrepareText(Land[] lands,bool countries)
        {
            return textWidths.GetValue(lands,key=>{
                var widths=new float[key.Length];
                using(var b=new Bitmap(1,1))using(var g=Graphics.FromImage(b))using(var font=new Font(countries?"Georgia":"Segoe UI",countries?12:11,countries?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel)) {
                    g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                    for(int i=0;i<key.Length;i++)widths[i]=g.MeasureString(countries?key[i].Name.ToUpperInvariant():key[i].Name,font).Width;
                }
                return widths;
            });
        }
        public static Bitmap Render(AtlasScene scene)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var bitmap = new Bitmap(scene.Width, scene.Height, PixelFormat.Format32bppPArgb);
            try {
                using (var g = Graphics.FromImage(bitmap)) {
                    g.Clear(Palette.Sea);
                    var view = scene.Coverage; var clip = view; clip.Inflate(2 / scene.PixelScale, 2 / scene.PixelScale);
                    using (var matrix = new Matrix(scene.PixelScale, 0, 0, scene.PixelScale, scene.Width / 2f - scene.X * scene.PixelScale, scene.Height / 2f - scene.Y * scene.PixelScale)) {
                        g.Transform = matrix;
                        using (var sea = new LinearGradientBrush(new RectangleF(-180,-90,360,180), Color.FromArgb(12,22,44), Color.FromArgb(23,43,66), 90)) g.FillRectangle(sea, view);
                        g.SmoothingMode = scene.Quality == 2 ? SmoothingMode.AntiAlias : SmoothingMode.None;
                        using (var countries = new GraphicsPath()) using (var regions = new GraphicsPath()) {
                            for (int i = 0; i < scene.Countries.Length; i++) if (scene.Countries[i].Bounds.IntersectsWith(view)) {
                                using (var path = scene.Countries[i].RasterPath(clip, scene.Overview ? 4 : scene.Zoom)) {
                                    if (path.PointCount == 0) continue;
                                    FillLand(g, path, scene.Countries[i].Bounds, scene.CountryColors[i], scene.Overview);
                                    if (!scene.Overview) countries.AddPath(path, false);
                                }
                            }
                            for (int i = 0; i < scene.Regions.Length; i++) {
                                var land = scene.Regions[i]; if (!land.Bounds.IntersectsWith(view)) continue;
                                bool border = !scene.Overview && scene.Borders && scene.Zoom >= 10 && land.Bounds.Width * land.Bounds.Height * scene.Zoom * scene.Zoom >= 35;
                                if (!scene.FillRegions[i] && !border) continue;
                                using (var path = land.RasterPath(clip, scene.Overview ? 4 : scene.Zoom)) {
                                    if (path.PointCount == 0) continue;
                                    if (scene.FillRegions[i]) FillLand(g, path, land.Bounds, scene.RegionColors[i], scene.Overview);
                                    if (border) regions.AddPath(path, false);
                                }
                            }
                            g.SmoothingMode = SmoothingMode.None;
                            using (var pen = new Pen(Color.FromArgb(160, 21, 25, 34), 1.05f / scene.PixelScale)) if (regions.PointCount > 0) g.DrawPath(pen, regions);
                            if (countries.PointCount > 0) {
                                using (var outer = new Pen(Color.FromArgb(125, 6, 12, 23), 5.2f / scene.PixelScale)) { outer.LineJoin = LineJoin.Round; g.DrawPath(outer, countries); }
                                using (var pen = new Pen(Color.FromArgb(12, 16, 24), 1.7f / scene.PixelScale)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, countries); }
                            }
                        }
                    }
                    g.ResetTransform();
                    if (scene.Texture && !scene.Overview) {
                        using (var tile = new Bitmap(32, 32)) {
                            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) tile.SetPixel(x, y, Color.FromArgb((x * 17 + y * 13) % 9, 35, 30, 20));
                            using (var brush = new TextureBrush(tile)) { brush.TranslateTransform(-scene.Coverage.X*scene.PixelScale,-scene.Coverage.Y*scene.PixelScale); g.FillRectangle(brush, 0, 0, scene.Width, scene.Height); }
                        }
                    }

                }
                scene.BuildMilliseconds = timer.Elapsed.TotalMilliseconds;
                return bitmap;
            } catch { bitmap.Dispose(); throw; }
        }
        private static void FillLand(Graphics g, GraphicsPath path, RectangleF bounds, Color color, bool overview)
        {
            if (overview || bounds.Width <= 0 || bounds.Height <= 0) { using(var brush = new SolidBrush(color)) g.FillPath(brush,path); return; }
            Color light = Color.FromArgb(Math.Min(255,color.R+12),Math.Min(255,color.G+12),Math.Min(255,color.B+12));
            Color dark = Color.FromArgb((int)(color.R*.82),(int)(color.G*.82),(int)(color.B*.82));
            // World-anchored shading is identical on either side of a tile boundary.
            using(var brush = new LinearGradientBrush(bounds,light,dark,60)) g.FillPath(brush,path);
        }
        // Choose labels once in world-pixel space. Per-tile collision filtering could
        // keep a label in one tile and reject its other half in the neighbouring tile.
        internal static AtlasLabel[] PlanLabels(AtlasScene s)
        {
            if(s.Labels==0 || s.Overview)return new AtlasLabel[0];
            var labels=new List<AtlasLabel>();var cells=new Dictionary<long,List<RectangleF>>();float ratio=s.PixelScale/s.Zoom;
            {
                for(int pass=0;pass<(s.Labels==2&&s.Zoom>25?2:1);pass++) {
                    var lands=pass==0?s.Countries:s.Regions;
                    var widths=PrepareText(lands,pass==0);
                    for(int index=0;index<lands.Length;index++) {
                        var land=lands[index];
                        if(land.Bounds.Width*s.Zoom<(pass==0?74:100)||land.Bounds.Height*s.Zoom<(pass==0?27:44))continue;
                        string text=pass==0?land.Name.ToUpperInvariant():land.Name;
                        float width=Math.Min((widths[index]+6)*ratio,190*ratio);
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
            using(var ink=new SolidBrush(Color.FromArgb(240,234,219)))
            using(var halo=new SolidBrush(Color.FromArgb(235,13,17,27)))
            using(var format=new StringFormat {Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}) {
                g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                foreach(var label in labels) {
                    var box=label.Bounds;box.Offset(s.Width/2f-s.X*s.PixelScale,s.Height/2f-s.Y*s.PixelScale);
                    if(box.Right<0||box.Bottom<0||box.Left>s.Width||box.Top>s.Height)continue;
                    var shadow=box;shadow.Offset(ratio,ratio);var font=label.Country?countryFont:regionFont;
                    g.DrawString(label.Text,font,halo,shadow,format);
                    shadow.Offset(-2*ratio,-2*ratio);g.DrawString(label.Text,font,halo,shadow,format);
                    g.DrawString(label.Text,font,ink,box,format);
                }
            }
        }
    }
}
