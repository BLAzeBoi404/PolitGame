using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Sovereign
{
    // Also loads previous release assemblies. Input is independently scheduled and
    // coalesced: WinForms.Timer's coarse cadence must not become the measured limit.
    internal static class FramePacingProbe
    {
        private const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static object Get(object o,string name)=>o.GetType().GetField(name,Members).GetValue(o);
        private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Members).SetValue(o,value);
        private static void Optional(object o,string name,object value){var field=o.GetType().GetField(name,Members);if(field!=null)field.SetValue(o,value);}
        private static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Members).Invoke(o,args);
        private static long Counter(object o,string name)
        {
            var field=o.GetType().GetField(name,Members);if(field!=null)return Convert.ToInt64(field.GetValue(o));
            var property=o.GetType().GetProperty(name,Members);return property==null?0:Convert.ToInt64(property.GetValue(o,null));
        }
        internal static void Run(Type gameType,object world,string path,bool useGpu=false,bool live=false,int frameLimit=60)
        {
            string settings=(string)gameType.Assembly.GetType("Sovereign.Preferences").GetProperty("PathName").GetValue(null,null);
            byte[] original=File.Exists(settings)?File.ReadAllBytes(settings):null;
            try {using(var form=(Form)Activator.CreateInstance(gameType,new[]{world})) {
                var prefs=Get(form,"prefs");Set(prefs,"Autosave",false);Optional(prefs,"PerformanceOverlay",false);Set(prefs,"Tutorial",false);Set(prefs,"Fullscreen",false);Optional(prefs,"Renderer",1);
                Set(prefs,"GraphicsQuality",2);Set(prefs,"FrameLimit",frameLimit);Set(prefs,"Labels",true);Set(prefs,"LabelDensity",2);Set(prefs,"RegionBorders",true);Set(prefs,"Texture",true);
                form.ClientSize=new Size(1600,900);Call(form,"PreviewPage","Карта");Set(form,"showHelp",false);Set(form,"speed",0);
                var clock=(System.Windows.Forms.Timer)Get(form,"clock");clock.Stop();((System.Windows.Forms.Timer)Get(form,"cameraRefresh")).Stop();
                Set(form,"selected",-1);Set(form,"hoveredRegion",-1);Set(form,"cx",20f);Set(form,"cy",-50f);Set(form,"scale",45f);
                if(useGpu)Call(form,"EnableGpuForTests");form.Show();form.Update();Call(form,"WaitForAtlas");Application.DoEvents();
                if(useGpu&&!(bool)gameType.GetProperty("GpuActive",Members).GetValue(form,null))throw new InvalidOperationException("The requested OpenGL renderer failed to initialize");
                if(useGpu)for(int i=0;i<16;i++)Call(form,"PresentGpu");
                var times=new List<double>();var timer=Stopwatch.StartNew();bool recording=false;int movedFrames=0;float priorX=float.NaN;
                Action frame=()=>{if(!recording)return;times.Add(timer.Elapsed.TotalMilliseconds);float x=(float)Get(form,"cx");if(x!=priorX)movedFrames++;priorX=x;};
                var present=gameType.GetEvent("FramePresented",Members);
                if(useGpu)present.GetAddMethod(true).Invoke(form,new object[]{frame});else form.Paint+=(s,e)=>frame();
                var engine=Get(form,"E");Set(engine,"AutoSaveEnabled",false);var campaign=Get(engine,"S");
                var report=new List<string>{"1600x900; high quality; frame limit "+frameLimit+"; medium zoom 45; "+(live?"simulation 7x":"paused")+"; requested 500 Hz input, at most one queued callback; original mouse handler and real message loop.",
                    "Runtime: "+Environment.OSVersion+" / "+(Type.GetType("Mono.Runtime")!=null?"Mono":".NET Framework")+". Present/paint delivery intervals; NOT physical monitor FPS.",
                    "Renderer: "+(useGpu?gameType.GetProperty("GpuDriver",Members).GetValue(form,null):"GDI+")+"; starting armies: "+((IList)Get(campaign,"Armies")).Count};
                int pass=0,sent=0,lastX=0,gc=0,queued=0,resumedIncidents=0;long builds=0,fallback=0,hud=0,uploads=0;double began=0;Point start=Point.Empty;bool closed=false, transitioning=false;Exception inputError=null;
                Action resume=()=>{
                    // Political events legitimately open a panel and pause the calendar.
                    // Return to the map as a player would; keep the real event/state.
                    if(live&&(string)Get(form,"page")!="Карта") {
                        Call(form,"Navigate","Карта");Set(form,"speed",3);resumedIncidents++;
                        if(useGpu)Call(form,"PresentGpu");else form.Refresh();
                    }
                };
                Action begin=()=>{
                    resume();
                    Optional(form,"GpuLongestWorkMs",0.0);Optional(form,"GpuLongestSwapMs",0.0);times.Clear();movedFrames=0;priorX=float.NaN;builds=Counter(form,"MapCacheBuilds");fallback=Counter(form,"AtlasFallbackFrames");hud=Counter(form,"GpuHudBuilds");uploads=Counter(form,"GpuUploads");gc=GC.CollectionCount(0);sent=0;
                    start=new Point(800,540);
                    for(int x=400;x<1100;x+=13){bool blocked=false;foreach(var hit in (IList)Get(form,"hits"))if(((RectangleF)Get(hit,"Rect")).Contains(x,540)){blocked=true;break;}if(!blocked){start=new Point(x,540);break;}}
                    Call(form,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,start.X,start.Y,0));if(!(bool)Get(form,"panning"))throw new InvalidOperationException("Probe did not start a map drag");
                    began=timer.Elapsed.TotalMilliseconds;recording=true;
                };
                Action input=()=>{
                    Interlocked.Exchange(ref queued,0);if(closed||transitioning)return;
                    try {
                    resume();
                    double elapsed=timer.Elapsed.TotalMilliseconds-began;
                    if(elapsed>=6000) {
                        transitioning=true;recording=false;Call(form,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,lastX,start.Y,0));
                        var gaps=new List<double>();for(int i=1;i<times.Count;i++)gaps.Add(times[i]-times[i-1]);gaps.Sort();
                        if(gaps.Count<10)throw new InvalidOperationException("Too few presented frames");
                        Func<double,string> number=v=>v.ToString("0.00",CultureInfo.InvariantCulture);
                        report.Add((pass==0?"First traversal":"Repeated traversal")+": frames="+times.Count+"; camera-changing frames="+movedFrames+"; inputs="+sent+"; interval mean="+number(gaps.Average())+" p95="+number(gaps[(int)((gaps.Count-1)*.95)])+" max="+number(gaps.Last())+" ms; >33ms="+gaps.Count(v=>v>33.34)+"; >50ms="+gaps.Count(v=>v>50)+"; raster builds="+(Counter(form,"MapCacheBuilds")-builds)+"; fallback paints="+(Counter(form,"AtlasFallbackFrames")-fallback)+"; HUD builds="+(Counter(form,"GpuHudBuilds")-hud)+"; GPU uploads="+(Counter(form,"GpuUploads")-uploads)+"; GC0="+(GC.CollectionCount(0)-gc));
                        if(useGpu)report.Add("Longest GPU submission="+number((double)Get(form,"GpuLongestWorkMs"))+" ms; longest buffer swap="+number((double)Get(form,"GpuLongestSwapMs"))+" ms");
                        if(++pass==2){closed=true;Set(form,"speed",0);clock.Stop();form.Close();return;}form.BeginInvoke((Action)(()=>{try {begin();transitioning=false;}catch(Exception e){inputError=e;closed=true;form.Close();}}));return;
                    }
                    if(!(bool)Get(form,"panning"))throw new InvalidOperationException("Drag lost pointer capture; timing sample is invalid");
                    lastX=start.X+(int)(850*Math.Sin(elapsed*Math.PI/1500));Call(form,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,lastX,start.Y,0));sent++;
                    }catch(Exception e){inputError=e;closed=true;recording=false;Set(form,"speed",0);clock.Stop();form.Close();}
                };
                begin();if(live){Set(form,"speed",3);clock.Start();}
                using(var feeder=new System.Threading.Timer(_=>{
                    if(closed||Interlocked.Exchange(ref queued,1)!=0)return;
                    try {form.BeginInvoke(input);}catch(InvalidOperationException){Interlocked.Exchange(ref queued,0);}
                },null,0,2)) {Application.Run(form);closed=true;}
                if(inputError!=null)throw new InvalidOperationException("Frame probe failed",inputError);
                report.Add("Campaign days advanced: "+Get(campaign,"Day")+"; event panels returned to map: "+resumedIncidents);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllLines(path,report);foreach(var line in report)Console.WriteLine(line);
            }} finally {if(original!=null)File.WriteAllBytes(settings,original);else if(File.Exists(settings))File.Delete(settings);}
        }
#if PROBE_STANDALONE
        [STAThread]public static void Main(string[] args)
        {
            Application.EnableVisualStyles();var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=assembly.GetType("Sovereign.World");
            using(var world=(IDisposable)type.GetMethod("Load").Invoke(null,null))Run(assembly.GetType("Sovereign.GameForm"),world,args[1],false,args.Contains("--live"),args.Contains("--165")?165:60);
        }
#endif
    }
}
