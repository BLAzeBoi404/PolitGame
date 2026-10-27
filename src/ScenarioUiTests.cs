using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Sovereign
{
    public partial class GameForm
    {
        private void TestScenarioInterface(string directory,Action<bool,string> check)
        {
            menuCountry=W.ByCode("MDA");BeginCampaign();ClientSize=new Size(1600,900);speed=0;clock.Stop();prefs.GraphicsQuality=2;prefs.Labels=true;prefs.LabelDensity=2;prefs.RegionBorders=true;layer="Политика";toast="";toastUntil=DateTime.MinValue;
            Action<PointF> tap=point=>{
                cx=point.X;cy=point.Y;scale=400;
                using(var frame=RenderFrame()){}
                int x=(int)((mapRect.X+mapRect.Width/2)*UiScale),y=(int)((mapRect.Y+mapRect.Height/2)*UiScale);
                OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,x,y,0));OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,x,y,0));
            };
            using(var frame=RenderFrame()){}
            scale=100;for(int i=0;i<20;i++)OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,(int)(mapRect.X+mapRect.Width/2),(int)(mapRect.Y+mapRect.Height/2),120));
            check(scale==400,"Mouse wheel reaches the extended district zoom and remains bounded");
            int picked=0;
            foreach(int id in W.Countries[E.S.Player].Regions) {
                var r=W.Regions[id];PointF point=new PointF((float)r.X,(float)r.Y);
                for(int x=1;x<8;x++)for(int y=1;y<8;y++) {
                    var p=new PointF(r.Bounds.X+r.Bounds.Width*x/8,r.Bounds.Y+r.Bounds.Height*y/8);
                    if(W.FindRegion(p)==id&&Math.Abs(p.X-r.X)+Math.Abs(p.Y-r.Y)>.25)point=p;
                }
                // Hide no units: choose a geographic interior whose actual screen point
                // is not covered by a neighbouring badge. Tiny cities may use their own badge.
                tap(point);
                if(selected!=id) {
                    // Badges occupy screen pixels, not whole provinces. Try other visible
                    // interiors without hiding units or bypassing the actual mouse handler.
                    for(int x=1;x<8&&selected!=id;x++)for(int y=1;y<8&&selected!=id;y++) {
                        var p=new PointF(r.Bounds.X+r.Bounds.Width*x/8,r.Bounds.Y+r.Bounds.Height*y/8);
                        if(W.FindRegion(p)==id)tap(p);
                    }
                }
                if(selected==id)picked++;
                else check(false,"Cannot select Moldovan district with real mouse: "+r.Name);
            }
            check(picked==W.Countries[E.S.Player].Regions.Count,"Real mouse clicks can select all forty Moldovan districts and municipalities");
            cx=28.8f;cy=-47.20f;scale=250;selected=W.Countries[E.S.Player].CapitalRegion;army=E.S.Armies.First(a=>a.Owner==E.S.Player&&a.Region==selected).Id;
            using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"scenario-moldova.png"));
            menuCountry=W.ByCode("RUS");BeginCampaign();Home();
            check(Math.Abs(cx-37.6)<2&&Math.Abs(cy+55.75)<2,"Russia's home camera opens near its capital rather than the geographic center of Siberia");
            cx=39;cy=-55;scale=45;using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"scenario-russia.png"));
            var group=armyMapGroups.First(g=>g.Units.Count>1&&g.Units[0].Owner==E.S.Player);var first=group.Units[0];cx=(float)W.Regions[first.Region].X;cy=(float)W.Regions[first.Region].Y;scale=100;
            using(var frame=RenderFrame()){}
            int key=-1-first.Owner*W.Regions.Length-first.Region;var hit=hits.First(h=>h.Label=="army-group:"+key);army=first.Id;
            hit.Action();int second=army;hit.Action();check(second!=first.Id&&army!=second&&group.Personnel==group.Units.Sum(a=>a.Personnel),"A stacked army badge conserves total manpower and cycles through its real formations");
            selected=hoveredRegion=-1;cx=20;cy=-50;scale=45;using(var frame=RenderFrame()){}WaitForAtlas();
            foreach(float x in new[]{5f,35f,20f}) {cx=x;using(var frame=RenderFrame()){}WaitForAtlas();}
            int builds=MapCacheBuilds;
            foreach(float x in new[]{5f,35f,20f}) {cx=x;using(var frame=RenderFrame()){}WaitForAtlas();}
            check(MapCacheBuilds==builds&&AtlasMissingTiles==0,"Repeated left/right traversals reuse detailed tiles with no overview fallback or rerasterization");
            check(AtlasResidentTiles<=AtlasTiles.Limit&&atlasTiles.LastError==null,"Atlas keeps a bounded bitmap cache and completes background work without errors");
            E.Validate(E.S);speed=0;
        }
    }
}
