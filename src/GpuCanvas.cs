using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using GLPixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace Sovereign
{
    // A retained GPU compositor. Bitmap rasterization is a resource update, never a
    // prerequisite for moving the camera. All GL calls/ownership stay on the UI thread.
    internal sealed class GpuCanvas : GLControl
    {
        private sealed class Texture { internal int Id, Width, Height; internal long Used, Bytes; }
        private readonly Dictionary<Bitmap,Texture> textures = new Dictionary<Bitmap,Texture>();
        private long frame, residentBytes, uploadBudget;
        internal long UploadBytes, Uploads, Presents;
        internal int TextureCount => textures.Count;
        internal long ResidentBytes => residentBytes;
        internal Action Render;
        private bool captureNext;
        private bool? requestedSync;
        private Bitmap captured;
        internal string Driver;
        private int maxTextureSize;
        internal bool NeedsUpload;
        internal const long MemoryLimit = 256L * 1024 * 1024;
        internal GpuCanvas() : base(new GraphicsMode(new ColorFormat(32),0,0,0),2,1,GraphicsContextFlags.Default)
        { Dock=DockStyle.Fill; TabStop=false; }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if(textures!=null)textures.Clear();residentBytes=0;requestedSync=null;base.OnHandleDestroyed(e);
        }
        protected override void OnPaint(PaintEventArgs e) { if(Render!=null)Render(); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        internal void Initialize()
        {
            MakeCurrent();Driver=GL.GetString(StringName.Renderer)+" / "+GL.GetString(StringName.Version);
            GL.GetInteger(GetPName.MaxTextureSize,out maxTextureSize);
            if(string.IsNullOrEmpty(GL.GetString(StringName.Version)))throw new InvalidOperationException("OpenGL context is unavailable");
        }
        internal void Begin(bool sync)
        {
            MakeCurrent();if(requestedSync!=sync){try {VSync=sync;}catch(NotSupportedException){}requestedSync=sync;}
            frame++;uploadBudget=4L*1024*1024;NeedsUpload=false;
            GL.Viewport(0,0,Width,Height);GL.Disable(EnableCap.DepthTest);GL.Disable(EnableCap.ScissorTest);
            GL.MatrixMode(MatrixMode.Projection);GL.LoadIdentity();GL.Ortho(0,Width,Height,0,-1,1);
            GL.MatrixMode(MatrixMode.Modelview);GL.LoadIdentity();
            GL.Enable(EnableCap.Blend);GL.BlendFunc(BlendingFactor.One,BlendingFactor.OneMinusSrcAlpha);
            GL.ClearColor(24/255f,31/255f,28/255f,1);GL.Clear(ClearBufferMask.ColorBufferBit);
        }
        internal void Clip(RectangleF? box)
        {
            if(!box.HasValue){GL.Disable(EnableCap.ScissorTest);return;}
            var b=box.Value;GL.Enable(EnableCap.ScissorTest);
            int left=(int)Math.Floor(b.Left),right=(int)Math.Ceiling(b.Right),top=(int)Math.Floor(b.Top),bottom=(int)Math.Ceiling(b.Bottom);
            GL.Scissor(left,Height-bottom,Math.Max(0,right-left),Math.Max(0,bottom-top));
        }
        internal bool HasTexture(Bitmap bitmap) => textures.ContainsKey(bitmap);
        internal void Warm(Bitmap bitmap){Bind(bitmap,false);}
        private Texture Bind(Bitmap bitmap,bool urgent)
        {
            Texture t;
            if(textures.TryGetValue(bitmap,out t)){t.Used=frame;GL.BindTexture(TextureTarget.Texture2D,t.Id);return t;}
            if(bitmap.Width>maxTextureSize||bitmap.Height>maxTextureSize)throw new InvalidOperationException("Размер слоя превышает предел видеодрайвера");
            long bytes=(long)bitmap.Width*bitmap.Height*4;
            if(bytes>MemoryLimit)throw new InvalidOperationException("Слой превышает бюджет видеопамяти");
            if(!urgent&&bytes>uploadBudget&&uploadBudget<4L*1024*1024){NeedsUpload=true;return null;}
            while(residentBytes+bytes>MemoryLimit&&textures.Count>0) {
                Bitmap key=null;long oldest=long.MaxValue;
                foreach(var entry in textures)if(entry.Value.Used<oldest&&entry.Value.Used<frame){oldest=entry.Value.Used;key=entry.Key;}
                if(key==null)break;Remove(key);
            }
            if(residentBytes+bytes>MemoryLimit)throw new InvalidOperationException("Не хватает бюджета видеопамяти для видимых слоёв");
            t=new Texture {Id=GL.GenTexture(),Width=bitmap.Width,Height=bitmap.Height,Bytes=bytes,Used=frame};
            try {
                GL.BindTexture(TextureTarget.Texture2D,t.Id);
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.ClampToEdge);
                var bits=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try {GL.PixelStore(PixelStoreParameter.UnpackAlignment,4);GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Rgba,bitmap.Width,bitmap.Height,0,GLPixelFormat.Bgra,PixelType.UnsignedByte,bits.Scan0);}
                finally {bitmap.UnlockBits(bits);}
                var error=GL.GetError();if(error!=ErrorCode.NoError)throw new InvalidOperationException("OpenGL texture upload: "+error);
                textures.Add(bitmap,t);residentBytes+=bytes;uploadBudget-=bytes;UploadBytes+=bytes;Uploads++;
                return t;
            } catch {GL.DeleteTexture(t.Id);throw;}
        }
        internal bool Image(Bitmap bitmap,RectangleF destination,RectangleF source,bool urgent=false)
        {
            if(destination.Right<=0||destination.Bottom<=0||destination.Left>=Width||destination.Top>=Height)return true;
            var t=Bind(bitmap,urgent);if(t==null)return false;
            GL.Enable(EnableCap.Texture2D);GL.Color4(1f,1f,1f,1f);
            float u=source.Left/t.Width,v=source.Top/t.Height,r=source.Right/t.Width,b=source.Bottom/t.Height;
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(u,v);GL.Vertex2(destination.Left,destination.Top);
            GL.TexCoord2(r,v);GL.Vertex2(destination.Right,destination.Top);
            GL.TexCoord2(r,b);GL.Vertex2(destination.Right,destination.Bottom);
            GL.TexCoord2(u,b);GL.Vertex2(destination.Left,destination.Bottom);GL.End();return true;
        }
        internal void Fill(Color c,RectangleF box)
        {
            GL.Disable(EnableCap.Texture2D);float a=c.A/255f;GL.Color4(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);
            GL.Begin(PrimitiveType.Quads);GL.Vertex2(box.Left,box.Top);GL.Vertex2(box.Right,box.Top);GL.Vertex2(box.Right,box.Bottom);GL.Vertex2(box.Left,box.Bottom);GL.End();
        }
        internal void Line(Color c,float width,PointF[] points,bool dashed=false)
        {
            if(points.Length<2)return;GL.Disable(EnableCap.Texture2D);float a=c.A/255f;GL.Color4(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);
            if(dashed){GL.Enable(EnableCap.LineStipple);GL.LineStipple(1,0x00ff);}else GL.Disable(EnableCap.LineStipple);
            GL.LineWidth(Math.Max(1,width));GL.Begin(PrimitiveType.LineStrip);foreach(var p in points)GL.Vertex2(p.X,p.Y);GL.End();
        }
        internal void Remove(Bitmap bitmap)
        {
            Texture t;if(!textures.TryGetValue(bitmap,out t))return;
            GL.DeleteTexture(t.Id);residentBytes-=t.Bytes;textures.Remove(bitmap);
        }
        internal void End()
        {
            Clip(null);if(captureNext){captured=ReadBack();captureNext=false;}SwapBuffers();Presents++;
            // Expired source images can be disposed by their owners without a GL call.
            if(frame%120==0){var expired=new List<Bitmap>();foreach(var p in textures)if(p.Value.Used<frame-240)expired.Add(p.Key);foreach(var key in expired)Remove(key);}
        }
        internal Bitmap CaptureFrame()
        {
            captureNext=true;Render();var result=captured;captured=null;
            if(result==null)throw new InvalidOperationException("GPU frame capture failed");return result;
        }
        private Bitmap ReadBack()
        {
            MakeCurrent();var image=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data=image.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try {GL.ReadBuffer(ReadBufferMode.Back);GL.ReadPixels(0,0,Width,Height,GLPixelFormat.Bgra,PixelType.UnsignedByte,data.Scan0);}
            finally {image.UnlockBits(data);}
            image.RotateFlip(RotateFlipType.RotateNoneFlipY);return image;
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&IsHandleCreated&&!IsDisposed) {
                try {MakeCurrent();foreach(var t in textures.Values)GL.DeleteTexture(t.Id);}catch(GraphicsContextException){}catch(InvalidOperationException){}
                textures.Clear();residentBytes=0;
            }
            base.Dispose(disposing);
        }
    }
}
