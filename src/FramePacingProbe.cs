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
    // The same probe can load the previous release: comparisons exercise its original
    // mouse handler, frame scheduler and real WinForms message loop, not RenderFrame().
    internal static class FramePacingProbe
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Get(object instance, string name) => instance.GetType().GetField(name,Members).GetValue(instance);
        private static void Set(object instance, string name, object value) => instance.GetType().GetField(name,Members).SetValue(instance,value);
        private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name,Members).Invoke(instance,args);
        private static int Counter(object instance, string name)
        {
            var field=instance.GetType().GetField(name,Members); if(field!=null)return (int)field.GetValue(instance);
            var prop=instance.GetType().GetProperty(name,Members);return prop==null?0:(int)prop.GetValue(instance,null);
        }
        internal static void Run(Type gameType, object world, string path)
        {
            string settings=(string)gameType.Assembly.GetType("Sovereign.Preferences").GetProperty("PathName").GetValue(null,null);
            byte[] original=File.Exists(settings)?File.ReadAllBytes(settings):null;
            try { using (var form=(Form)Activator.CreateInstance(gameType,new[]{world})) {
                var prefs=Get(form,"prefs"); Set(prefs,"Autosave",false);Set(prefs,"Tutorial",false);Set(prefs,"Fullscreen",false);
                Set(prefs,"GraphicsQuality",2);Set(prefs,"FrameLimit",60);Set(prefs,"Labels",true);Set(prefs,"LabelDensity",2);Set(prefs,"RegionBorders",true);Set(prefs,"Texture",true);
                form.ClientSize=new Size(1600,900); Call(form,"PreviewPage","Карта"); Set(form,"showHelp",false);Set(form,"speed",0);
                ((System.Windows.Forms.Timer)Get(form,"clock")).Stop();((System.Windows.Forms.Timer)Get(form,"cameraRefresh")).Stop();
                Set(form,"selected",-1);Set(form,"hoveredRegion",-1);Set(form,"cx",20f);Set(form,"cy",-50f);Set(form,"scale",45f);
                form.Show(); form.Update();Call(form,"WaitForAtlas"); Application.DoEvents();
                var times=new List<double>();var timer=Stopwatch.StartNew(); bool recording=false;
                form.Paint+=(s,e)=>{if(recording) times.Add(timer.Elapsed.TotalMilliseconds);};
                var engine=Get(form,"E");var campaign=Get(engine,"S");
                var report=new List<string>{"1600x900; high quality; limit 60; medium zoom 45; paused; synthetic requested 125 Hz left/right drag through the real mouse handler and WinForms message loop.",
                    "Runtime: "+Environment.OSVersion+" / "+(Type.GetType("Mono.Runtime")!=null?"Mono":".NET Framework")+". Paint delivery intervals; NOT physical monitor FPS.",
                    "World armies: "+((IList)Get(campaign,"Armies")).Count};
                int pass=0,sent=0,lastX=0,builds=0,fallback=0,gc=0;double began=0;Point start=Point.Empty;
                Action begin = () => {
                    times.Clear(); builds=Counter(form,"MapCacheBuilds");fallback=Counter(form,"AtlasFallbackFrames");gc=GC.CollectionCount(0);sent=0;
                    start=new Point(800,540);
                    for(int x=400;x<1100;x+=13) {
                        bool blocked=false;foreach(var hit in (IList)Get(form,"hits")) if(((RectangleF)Get(hit,"Rect")).Contains(x,540)){blocked=true;break;}
                        if(!blocked){start=new Point(x,540);break;}
                    }
                    Call(form,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,start.X,start.Y,0));
                    if(!(bool)Get(form,"panning"))throw new InvalidOperationException("Probe did not start a real map drag.");
                    began=timer.Elapsed.TotalMilliseconds;recording=true;
                };
                using(var input=new System.Windows.Forms.Timer {Interval=8}) {
                    input.Tick+=(sender,args)=>{
                        double elapsed=timer.Elapsed.TotalMilliseconds-began;
                        if(elapsed>=6000) {
                            recording=false;Call(form,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,lastX,start.Y,0));
                            var intervals=new List<double>();for(int i=1;i<times.Count;i++)intervals.Add(times[i]-times[i-1]);intervals.Sort();
                            if(intervals.Count<10){report.Add("FAIL Too few paint frames");input.Stop();form.Close();return;}
                            report.Add((pass==0?"First traversal":"Repeated traversal")+": paints="+times.Count+"; inputs="+sent+"; interval mean="+intervals.Average().ToString("0.00",CultureInfo.InvariantCulture)+
                                " p95="+intervals[(int)((intervals.Count-1)*.95)].ToString("0.00",CultureInfo.InvariantCulture)+" max="+intervals.Last().ToString("0.00",CultureInfo.InvariantCulture)+
                                " ms; >33ms="+intervals.Count(v=>v>33.34)+"; >50ms="+intervals.Count(v=>v>50)+"; raster builds="+(Counter(form,"MapCacheBuilds")-builds)+"; fallback paints="+(Counter(form,"AtlasFallbackFrames")-fallback)+"; GC0="+(GC.CollectionCount(0)-gc));
                            if(++pass==2){input.Stop();form.Close();return;}begin();return;
                        }
                        lastX=start.X+(int)(850*Math.Sin(elapsed*Math.PI/1500));
                        Call(form,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,lastX,start.Y,0));sent++;
                    };
                    begin();input.Start();Application.Run(form);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllLines(path,report);foreach(var line in report)Console.WriteLine(line);
            } } finally {
                if(original!=null)File.WriteAllBytes(settings,original);
                else if(File.Exists(settings))File.Delete(settings);
            }
        }
#if PROBE_STANDALONE
        [STAThread] public static void Main(string[] args)
        {
            Application.EnableVisualStyles();var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var type=assembly.GetType("Sovereign.World");
            using(var world=(IDisposable)type.GetMethod("Load").Invoke(null,null))Run(assembly.GetType("Sovereign.GameForm"),world,args[1]);
        }
#endif
    }
}
