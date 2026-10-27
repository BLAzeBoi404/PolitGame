using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Sovereign
{
    internal static class GpuTests
    {
        internal static int Run(World world,string directory)
        {
            byte[] original=File.Exists(Preferences.PathName)?File.ReadAllBytes(Preferences.PathName):null;
            try {Directory.CreateDirectory(directory);using(var f=new GameForm(world))return f.TestGpu(directory);}
            finally {if(original!=null)File.WriteAllBytes(Preferences.PathName,original);else if(File.Exists(Preferences.PathName))File.Delete(Preferences.PathName);}
        }
    }
    public partial class GameForm
    {
        internal int TestGpu(string directory)
        {
            var lines=new System.Collections.Generic.List<string>();int failed=0;
            Action<bool,string> check=(ok,label)=>{lines.Add((ok?"PASS ":"FAIL ")+label);Console.WriteLine(lines[lines.Count-1]);if(!ok)failed++;};
            prefs.Renderer=0;prefs.Fullscreen=prefs.PerformanceOverlay=false;prefs.Autosave=prefs.Tutorial=false;E.AutoSaveEnabled=false;prefs.FrameLimit=0;prefs.GraphicsQuality=2;prefs.LabelDensity=2;prefs.Labels=prefs.RegionBorders=prefs.Texture=true;clock.Stop();cameraRefresh.Stop();ClientSize=new Size(1600,900);
            PreviewPage("Карта");showHelp=false;scale=45;cx=20;cy=-50;selected=hoveredRegion=-1;speed=0;
            forceGpu=true;Show();Application.DoEvents();
            check(GpuActive,"OpenGL context and compositor start: "+(GpuActive?GpuDriver:gpuError));if(!GpuActive)return 1;
            Action settle=()=>{WaitForAtlas();for(int i=0;i<12;i++)PresentGpu();};settle();
            using(var reference=RenderFrame())using(var frame=gpu.CaptureFrame()) {
                frame.Save(Path.Combine(directory,"gpu-map.png"));reference.Save(Path.Combine(directory,"cpu-map.png"));
                // Compare local color averages: GL's continuous subpixel filtering differs
                // from libgdiplus's quantized edge/texture sampling. Do not compare
                // isolated antialiased text or border pixels as if they were flat fills.
                int close=0,total=0;for(int y=130;y<760;y+=31)for(int x=30;x<1250;x+=37){int[] a=new int[3],b=new int[3];for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){var c=reference.GetPixel(x+dx,y+dy);var d=frame.GetPixel(x+dx,y+dy);a[0]+=c.R;a[1]+=c.G;a[2]+=c.B;b[0]+=d.R;b[1]+=d.G;b[2]+=d.B;}if((Math.Abs(a[0]-b[0])+Math.Abs(a[1]-b[1])+Math.Abs(a[2]-b[2]))/25<24)close++;total++;}
                check(close>total*.97,"GPU framebuffer preserves geography, colors and overlays (5x5 color averages, "+close+"/"+total+")");
            }
            Invalidate();settle();int hud=GpuHudBuilds;long uploads=GpuUploads;
            for(int i=0;i<60;i++){cx+=i%2==0?.02f:-.02f;InvalidateCamera();PresentGpu();}
            check(GpuHudBuilds==hud,"Camera motion reuses the HUD without rerasterizing its text and panels");
            check(GpuUploads==uploads,"Warm camera motion performs zero bitmap uploads to the GPU");
            check(gpu.ResidentBytes<=GpuCanvas.MemoryLimit,"GPU texture cache remains within its memory budget");
            string originalLayer=layer;float originalScale=scale;layer="Армии";scale=4;Invalidate();settle();
            check(GpuActive&&gpuWorldHits.Count>100,"World-scale military layer retains many markers without disposing in-use badges");
            layer=originalLayer;scale=originalScale;Invalidate();settle();
            var worldHit=hits.FirstOrDefault(h=>h.Label.StartsWith("army-group:"));
            if(worldHit!=null){worldHit.Action();PresentGpu();check(army>=0,"Retained world hit targets still select actual formations");}
            var baseline=new Engine(W){AutoSaveEnabled=false};baseline.New(E.S.Player,E.S.StartYear,E.S.Difficulty);baseline.Tick();
            string queuedSave=Path.Combine(directory,"queued-save.xml");if(File.Exists(queuedSave))File.Delete(queuedSave);
            int day=E.S.Day;string originalPage=page;
            using(var gate=new System.Threading.ManualResetEventSlim(false)) {
                BeforeSimulationStep=()=>gate.Wait();StartSimulationDay();check(simulationBusy,"Simulation runs off the window thread");
                try {
                    long frames=GpuPresents;
                    var economy=hits.Last(h=>h.Label=="nav:Экономика");var r=economy.Rect;
                    OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,(int)((r.X+r.Width/2)*UiScale),(int)((r.Y+r.Height/2)*UiScale),0));
                    hits.Add(new Hit {Rect=new RectangleF(1,1,8,8),Label="test:queued-save",Action=()=>E.Save(queuedSave)});
                    OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,(int)(4*UiScale),(int)(4*UiScale),0));
                    check(!File.Exists(queuedSave),"Saving waits until the worker finishes the current day");
                    for(int i=0;i<10;i++){cx+=.02f;InvalidateCamera();PresentGpu();}
                    check(GpuPresents>=frames+10&&E.S.Day==day,"A deliberately blocked simulation worker cannot block ten camera presentations");
                    check(page==originalPage,"UI actions wait while the worker owns mutable campaign state");
                } finally {gate.Set();}
                var watch=System.Diagnostics.Stopwatch.StartNew();while(simulationBusy&&watch.ElapsedMilliseconds<10000){Application.DoEvents();System.Threading.Thread.Sleep(1);}
            }
            BeforeSimulationStep=null;
            check(!simulationBusy&&E.S.Day==day+1&&page=="Экономика","Completed day publishes state, then executes queued UI actions in order (day="+E.S.Day+", page="+page+", pending="+pendingSimulationActions.Count+")");E.Validate(E.S);settle();
            var serializer=new System.Xml.Serialization.XmlSerializer(typeof(Campaign));
            using(var expected=new StringWriter())using(var actual=new StringWriter()){serializer.Serialize(expected,baseline.S);serializer.Serialize(actual,E.S);check(expected.ToString()==actual.ToString(),"Worker day produces exactly the same campaign as the synchronous engine");}
            var saved=new Engine(W){AutoSaveEnabled=false};saved.Load(queuedSave);check(saved.S.Day==day+1,"Queued save contains the completed day and passes validation");File.Delete(queuedSave);
            // Use the real child-control event forwarding, not just an action delegate.
            Action<string,MouseEventArgs> pointer=(name,args)=>typeof(Control).GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(gpu,new object[]{args});
            OpenMenu("new");Invalidate();PresentGpu();int poland=W.ByCode("POL");FocusSetupCountry(poland);menuCountry=W.ByCode("UKR");Invalidate();settle();
            var pol=Physical(Screen(W.Countries[poland].X,W.Countries[poland].Y));
            pointer("OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,(int)pol.X,(int)pol.Y,0));pointer("OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,(int)pol.X,(int)pol.Y,0));
            check(menuCountry==poland,"GPU child input selects the starting country directly on the map");
            Invalidate();settle();hud=GpuHudBuilds;for(int i=0;i<20;i++){cx+=.01f;InvalidateCamera();PresentGpu();}
            check(GpuHudBuilds==hud,"Country-selection atlas also pans without rerasterizing the HUD");
            using(var frame=gpu.CaptureFrame())frame.Save(Path.Combine(directory,"gpu-setup.png"));
            menuScreen="";Navigate("Карта");Invalidate();settle();
            float oldScale=scale;OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,800,540,120));PresentGpu();
            check(scale>oldScale&&GpuActive&&AtlasMissingTiles>0,"New zoom presents immediately using the overview while detail loads");settle();
            ToggleFullScreen();PresentGpu();ToggleFullScreen();PresentGpu();
            check(GpuActive,"Fullscreen transitions retain a working GPU context");
            menuScreen="settings";Invalidate();PresentGpu();using(var frame=gpu.CaptureFrame())frame.Save(Path.Combine(directory,"gpu-settings.png"));
            check(hits.Any(h=>h.Label.Contains("Видеокарта")),"Renderer selection remains reachable in settings");
            ClientSize=new Size(1280,720);Invalidate();PresentGpu();using(var frame=gpu.CaptureFrame())frame.Save(Path.Combine(directory,"gpu-small.png"));
            check(gpuHud.Width==1280&&gpuHud.Height==720,"Resize rebuilds the retained HUD at the real pixel dimensions");
            StopGpu();check(!GpuActive,"Compatibility renderer can be restored without replacing the campaign");
            StartGpu();menuScreen="";Navigate("Карта");settle();check(GpuActive&&E.S.Day==day+1,"Recreated renderer restores resources without replacing the campaign");
            bool closingDeferred=false;
            using(var gate=new System.Threading.ManualResetEventSlim(false)) {
                BeforeSimulationStep=()=>gate.Wait();StartSimulationDay();
                try {Close();closingDeferred=simulationBusy&&!IsDisposed;} finally {gate.Set();}
                var wait=System.Diagnostics.Stopwatch.StartNew();while(!IsDisposed&&wait.ElapsedMilliseconds<10000){Application.DoEvents();System.Threading.Thread.Sleep(1);}
            }
            check(closingDeferred&&IsDisposed,"Closing waits for the worker and releases the renderer safely");
            lines.Add("Driver: "+(gpuError??"no renderer errors"));lines.Add("Failures: "+failed);File.WriteAllLines(Path.Combine(directory,"gpu-results.txt"),lines);return failed==0?0:1;
        }
    }
}
