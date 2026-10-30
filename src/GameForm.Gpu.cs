using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private sealed class WorldCommand
        {
            internal Bitmap Image;
            internal RectangleF Bounds;
            internal PointF[] Points;
            internal Color Color;
            internal float Thickness;
            internal bool FixedSize,Dashed;
        }
        private sealed class WorldHit { internal Hit Hit; internal PointF Anchor; }
        private GpuCanvas gpu;
        private Bitmap gpuHud;
        private bool gpuHudDirty=true,gpuWorldDirty=true,gpuCapturingHud,gpuRecordingWorld,gpuPaintingMap;
        private readonly List<WorldCommand> gpuWorld=new List<WorldCommand>();
        private readonly List<WorldHit> gpuWorldHits=new List<WorldHit>();
        private readonly List<Hit> gpuHudHits=new List<Hit>();
        private readonly List<Bitmap> gpuFrontImages=new List<Bitmap>();
        private RectangleF gpuWorldCoverage;
        private float gpuWorldScale,gpuWorldUiScale,gpuHudUiScale;
        private string gpuError;
        private bool forceGpu,gpuFailed;
        private double gpuDiagnosticAt;
        private int monitorFrequency=60;
        private int[] gpuProvinceOwners;
        internal int GpuHudBuilds,GpuWorldBuilds;
        internal double GpuLongestWorkMs,GpuLongestSwapMs;
        internal bool GpuActive => gpu!=null;
        internal long GpuUploads => gpu==null?0:gpu.Uploads;
        internal long GpuPresents => gpu==null?0:gpu.Presents;
        internal string GpuDriver => gpu==null?"GDI+":gpu.Driver;
        internal event Action FramePresented;
        private PointF WorldPoint(float x,float y) => new PointF(cx+(x/UiScale-mapRect.X-mapRect.Width/2)/scale,cy+(y/UiScale-mapRect.Y-mapRect.Height/2)/scale);
        private RectangleF Physical(RectangleF r) => new RectangleF(r.X*UiScale,r.Y*UiScale,r.Width*UiScale,r.Height*UiScale);
        private PointF Physical(PointF p) => new PointF(p.X*UiScale,p.Y*UiScale);
        private RectangleF CommandBox(WorldCommand command)
        {
            if(command.FixedSize){var p=Physical(Screen(command.Bounds.X,command.Bounds.Y));return new RectangleF(p.X-command.Bounds.Width/2,p.Y-command.Bounds.Height/2,command.Bounds.Width,command.Bounds.Height);}
            var a=Physical(Screen(command.Bounds.X,command.Bounds.Y));return new RectangleF(a.X,a.Y,command.Bounds.Width*scale*UiScale,command.Bounds.Height*scale*UiScale);
        }
        internal void EnableGpuForTests(){forceGpu=true;if(IsHandleCreated)StartGpu();}
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if(forceGpu||(prefs.Renderer==0&&Environment.OSVersion.Platform==PlatformID.Win32NT))StartGpu();
        }
        private void UpdateMonitorFrequency()
        {
            try {
                var center=PointToScreen(new Point(ClientSize.Width/2,ClientSize.Height/2));
                for(int i=0;i<6;i++){var display=OpenTK.DisplayDevice.GetDisplay((OpenTK.DisplayIndex)i);if(display!=null&&display.Bounds.Contains(center)){monitorFrequency=Math.Max(30,Math.Min(360,(int)Math.Round(display.RefreshRate>0?display.RefreshRate:60)));break;}}
            } catch {monitorFrequency=60;}
        }
        protected override void OnLocationChanged(EventArgs e){base.OnLocationChanged(e);if(gpu!=null)UpdateMonitorFrequency();}
        private int RegionOwnerForView(int region) => simulationBusy&&gpuProvinceOwners!=null?gpuProvinceOwners[region]:E.S.Provinces[region].Owner;
        private bool PointerCaptured => gpu!=null?gpu.Capture:Capture;
        private void CapturePointer(bool value){if(gpu!=null)gpu.Capture=value;else Capture=value;}
        private void StartGpu()
        {
            if(gpu!=null)return;GpuCanvas canvas=null;
            try {
                canvas=new GpuCanvas();Controls.Add(canvas);canvas.SendToBack();canvas.CreateControl();canvas.Initialize();
                canvas.MouseMove+=(s,e)=>OnMouseMove(e);canvas.MouseDown+=(s,e)=>OnMouseDown(e);canvas.MouseUp+=(s,e)=>OnMouseUp(e);canvas.MouseWheel+=(s,e)=>{var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;OnMouseWheel(e);};
                canvas.MouseCaptureChanged+=(s,e)=>{if(!canvas.Capture){panning=false;researchDragging=false;}};
                canvas.Render=PresentGpu;gpu=canvas;gpuError=null;gpuFailed=false;UpdateMonitorFrequency();gpuHudDirty=gpuWorldDirty=true;Invalidate();
            } catch(Exception e) {
                if(canvas!=null){Controls.Remove(canvas);canvas.Dispose();}
                gpuError=e.Message;gpu=null;base.Invalidate();
            }
        }
        private void StopGpu()
        {
            if(gpu==null)return;gpu.Render=null;Controls.Remove(gpu);gpu.Dispose();gpu=null;
            if(gpuHud!=null){gpuHud.Dispose();gpuHud=null;}
            gpuWorld.Clear();gpuWorldHits.Clear();gpuHudHits.Clear();foreach(var image in gpuFrontImages)image.Dispose();gpuFrontImages.Clear();base.Invalidate();
        }
        private void ChangeRenderer()
        {
            prefs.Renderer=1-prefs.Renderer;
            if(prefs.Renderer==0)StartGpu();else StopGpu();
            Invalidate();
        }
        private void RebuildGpuHud()
        {
            if(gpuHud!=null){gpu.Remove(gpuHud);gpuHud.Dispose();}
            gpuHud=new Bitmap(ClientSize.Width,ClientSize.Height,PixelFormat.Format32bppPArgb);
            gpuCapturingHud=true;
            try {using(var g=Graphics.FromImage(gpuHud)){g.Clear(Color.Transparent);OnPaint(new PaintEventArgs(g,ClientRectangle));}}
            finally {gpuCapturingHud=false;}
            gpuHudHits.Clear();gpuHudHits.AddRange(hits);gpuHudUiScale=UiScale;gpuHudDirty=false;GpuHudBuilds++;
        }
        private bool GpuWorldVisible(PointF point)
        {
            var region=mapRect;region.Inflate(256,256);return region.Contains(point);
        }
        private void RecordImage(Bitmap image,RectangleF destination,bool fixedSize)
        {
            RectangleF bounds;
            if(fixedSize){var p=WorldPoint(destination.X+destination.Width/2,destination.Y+destination.Height/2);bounds=new RectangleF(p.X,p.Y,destination.Width,destination.Height);}
            else {var p=WorldPoint(destination.X,destination.Y);bounds=new RectangleF(p.X,p.Y,destination.Width/(scale*UiScale),destination.Height/(scale*UiScale));}
            gpuWorld.Add(new WorldCommand {Image=image,Bounds=bounds,FixedSize=fixedSize});
        }
        private void RecordFill(Color color,RectangleF logical)
        {
            var rect=Physical(logical);var p=WorldPoint(rect.X+rect.Width/2,rect.Y+rect.Height/2);
            gpuWorld.Add(new WorldCommand {Color=color,FixedSize=true,Bounds=new RectangleF(p.X,p.Y,rect.Width,rect.Height)});
        }
        private void MapPolyline(Graphics g,Color color,float width,PointF[] points,bool dashed=false,bool arrow=false)
        {
            if(gpuRecordingWorld){gpuWorld.Add(new WorldCommand {Points=points.Select(p=>WorldPoint(p.X*UiScale,p.Y*UiScale)).ToArray(),Color=color,Thickness=width*UiScale,Dashed=dashed});
                if(arrow&&points.Length>=2){var tip=points[points.Length-1];var from=points[points.Length-2];double angle=Math.Atan2(tip.Y-from.Y,tip.X-from.X);var head=new[]{new PointF(tip.X-7*(float)Math.Cos(angle-.5),tip.Y-7*(float)Math.Sin(angle-.5)),tip,new PointF(tip.X-7*(float)Math.Cos(angle+.5),tip.Y-7*(float)Math.Sin(angle+.5))};MapPolyline(g,color,width,head);}
                return;}
            using(var pen=new Pen(color,width)){if(dashed)pen.DashStyle=DashStyle.Dash;g.DrawLines(pen,points);}
        }
        private void MapEllipse(Graphics g,Color color,float width,RectangleF rect)
        {
            var points=new PointF[33];for(int i=0;i<points.Length;i++){double a=i*Math.PI/16;points[i]=new PointF(rect.X+rect.Width/2+(float)Math.Cos(a)*rect.Width/2,rect.Y+rect.Height/2+(float)Math.Sin(a)*rect.Height/2);}
            MapPolyline(g,color,width,points);
        }
        private void RecordFronts()
        {
            if(scale<7)return;
            foreach(var plan in E.S.Fronts.Where(p=>p.Owner==E.S.Player&&p.Active&&p.ArmyIds.Count>0))foreach(int id in E.FrontRegions(plan)) {
                var land=W.Regions[id];var bounds=RectangleF.Intersect(land.Bounds,gpuWorldCoverage);if(bounds.Width<=0||bounds.Height<=0)continue;
                bounds.Inflate(4/scale,4/scale);float pixels=scale*UiScale;
                var image=new Bitmap(Math.Max(1,(int)Math.Ceiling(bounds.Width*pixels)),Math.Max(1,(int)Math.Ceiling(bounds.Height*pixels)),PixelFormat.Format32bppPArgb);
                using(var g=Graphics.FromImage(image))using(var path=land.RasterPath(gpuWorldCoverage,scale))using(var matrix=new Matrix(pixels,0,0,pixels,-bounds.X*pixels,-bounds.Y*pixels)) {
                    g.Transform=matrix;Color color=plan.Stance==FrontStance.Advance?Palette.Red:Palette.Gold;
                    using(var brush=new SolidBrush(Color.FromArgb(45,color)))g.FillPath(brush,path);
                    using(var pen=new Pen(color,1.6f/scale)){pen.DashStyle=DashStyle.Dash;g.DrawPath(pen,path);}
                }
                gpuFrontImages.Add(image);gpuWorld.Add(new WorldCommand {Image=image,Bounds=bounds});
            }
        }
        private void RecordSetupHighlights()
        {
            foreach(int id in new[]{hoveredCountry,menuCountry}.Distinct())if(id>=0) {
                var land=W.Countries[id];var bounds=RectangleF.Intersect(land.Bounds,gpuWorldCoverage);if(bounds.Width<=0||bounds.Height<=0)continue;
                bounds.Inflate(4/scale,4/scale);float pixels=scale*UiScale;
                var image=new Bitmap(Math.Max(1,(int)Math.Ceiling(bounds.Width*pixels)),Math.Max(1,(int)Math.Ceiling(bounds.Height*pixels)),PixelFormat.Format32bppPArgb);
                using(var g=Graphics.FromImage(image))using(var path=land.RasterPath(gpuWorldCoverage,scale))using(var matrix=new Matrix(pixels,0,0,pixels,-bounds.X*pixels,-bounds.Y*pixels)) {
                    g.Transform=matrix;g.SmoothingMode=SmoothingMode.AntiAlias;
                    using(var brush=new SolidBrush(Color.FromArgb(id==menuCountry?65:35,255,243,173)))g.FillPath(brush,path);
                    using(var pen=new Pen(id==menuCountry?Color.FromArgb(96,66,28):Color.FromArgb(220,225,200),(id==menuCountry?2.4f:1.5f)/scale))g.DrawPath(pen,path);
                }
                gpuFrontImages.Add(image);gpuWorld.Add(new WorldCommand {Image=image,Bounds=bounds});
            }
        }
        private void RebuildGpuWorld()
        {
            if(menuScreen!="new"){if(gpuProvinceOwners==null)gpuProvinceOwners=new int[W.Regions.Length];for(int i=0;i<gpuProvinceOwners.Length;i++)gpuProvinceOwners[i]=E.S.Provinces[i].Owner;}
            gpuWorld.Clear();gpuWorldHits.Clear();
            if(unitBadges.Count>=1024)ClearUnitBadges();if(highlightPaths.Count>4)ClearHighlights();
            foreach(var image in gpuFrontImages){gpu.Remove(image);image.Dispose();}gpuFrontImages.Clear();
            gpuWorldCoverage=AtlasView;gpuWorldCoverage.Inflate(256/scale,256/scale);gpuWorldScale=scale;gpuWorldUiScale=UiScale;
            var saved=hits;hits=new List<Hit>();gpuRecordingWorld=true;
            try {
                using(var bitmap=new Bitmap(1,1))using(var g=Graphics.FromImage(bitmap)) {
                    if(menuScreen=="new")RecordSetupHighlights();else {
                    if(hoveredRegion!=selected)DrawRegionHighlight(g,hoveredRegion,false);DrawRegionHighlight(g,selected,true);RecordFronts();
                    DrawArmyMap(g);DrawBattleMarkers(g);DrawAirOperations(g);
                    }
                }
                foreach(var hit in hits){var p=WorldPoint((hit.Rect.X+hit.Rect.Width/2)*UiScale,(hit.Rect.Y+hit.Rect.Height/2)*UiScale);gpuWorldHits.Add(new WorldHit {Hit=hit,Anchor=p});}
            } finally {gpuRecordingWorld=false;hits=saved;}
            gpuWorldDirty=false;GpuWorldBuilds++;
        }
        private void AddGpuHudHits()
        {
            if(gpuHud==null)return;
            float x=(float)ClientSize.Width/gpuHud.Width*gpuHudUiScale/UiScale,y=(float)ClientSize.Height/gpuHud.Height*gpuHudUiScale/UiScale;
            if(Math.Abs(x-1)<.0001&&Math.Abs(y-1)<.0001){hits.AddRange(gpuHudHits);return;}
            foreach(var h in gpuHudHits)hits.Add(new Hit {Rect=new RectangleF(h.Rect.X*x,h.Rect.Y*y,h.Rect.Width*x,h.Rect.Height*y),Action=h.Action,Label=h.Label,Tip=h.Tip,FocusTarget=h.FocusTarget});
        }
        private void PresentGpu()
        {
            if(gpu==null||gpuFailed||ClientSize.Width<=0||ClientSize.Height<=0)return;
            try {
                double began=uiTime.Elapsed.TotalMilliseconds;gpu.Begin(prefs.FrameLimit==-1);
                bool setup=menuScreen=="new",menu=menuScreen!=""&&!setup;
                if(prefs.PerformanceOverlay&&began-gpuDiagnosticAt>=500&&!simulationBusy){gpuHudDirty=true;gpuDiagnosticAt=began;}
                if(!simulationBusy&&(gpuHudDirty||gpuHud==null||gpuHud.Width!=ClientSize.Width||gpuHud.Height!=ClientSize.Height))RebuildGpuHud();
                if(!menu) {
                    mapRect=setup?new RectangleF(0,82,VW,VH-124):new RectangleF(0,99,VW,VH-34-99);
                    gpu.Clip(Physical(mapRect));
                    gpuPaintingMap=true;
                    try {using(var bitmap=new Bitmap(1,1))using(var graphics=Graphics.FromImage(bitmap))DrawCachedMap(graphics);}
                    finally {gpuPaintingMap=false;}
                    if(!simulationBusy&&(gpuWorldDirty||gpuWorldScale!=scale||gpuWorldUiScale!=UiScale||!gpuWorldCoverage.Contains(AtlasView)))RebuildGpuWorld();
                    foreach(var command in gpuWorld) {
                        if(command.Points!=null){gpu.Line(command.Color,command.Thickness,command.Points.Select(p=>Physical(Screen(p.X,p.Y))).ToArray(),command.Dashed);continue;}
                        var box=CommandBox(command);if(!box.IntersectsWith(Physical(mapRect)))continue;
                        if(command.Image!=null)gpu.Image(command.Image,box,new RectangleF(0,0,command.Image.Width,command.Image.Height));else gpu.Fill(command.Color,box);
                    }
                    hits.Clear();
                    if(!showHelp)foreach(var entry in gpuWorldHits) {
                        var position=Screen(entry.Anchor.X,entry.Anchor.Y);var source=entry.Hit;
                        float ratio=gpuWorldUiScale/UiScale;var size=new SizeF(source.Rect.Width*ratio,source.Rect.Height*ratio);
                        var rect=new RectangleF(position.X-size.Width/2,position.Y-size.Height/2,size.Width,size.Height);
                        if(!rect.IntersectsWith(mapRect))continue;hits.Add(new Hit {Rect=RectangleF.Intersect(rect,mapRect),Label=source.Label,Tip=source.Tip,Action=source.Action});
                    }
                    AddGpuHudHits();
                } else {hits.Clear();AddGpuHudHits();}
                gpu.Clip(null);DrawGpuMiniMap();if(gpuHud!=null)gpu.Image(gpuHud,new RectangleF(0,0,ClientSize.Width,ClientSize.Height),new RectangleF(0,0,gpuHud.Width,gpuHud.Height),true);
                double swapBegan=uiTime.Elapsed.TotalMilliseconds;GpuLongestWorkMs=Math.Max(GpuLongestWorkMs,swapBegan-began);
                gpu.End();GpuLongestSwapMs=Math.Max(GpuLongestSwapMs,uiTime.Elapsed.TotalMilliseconds-swapBegan);RecordFrameTiming(began);
                if(FramePresented!=null)FramePresented();if(gpu.NeedsUpload||panning)InvalidateCamera();
            } catch(Exception e) {
                gpuError=e.Message;gpuFailed=true;speed=0;
                Action fallback=()=>{StopGpu();Message("Видеорендер недоступен; включена совместимая отрисовка. "+e.Message);};
                if(simulationBusy)pendingSimulationActions.Enqueue(fallback);else fallback();
            }
        }
    }
}
