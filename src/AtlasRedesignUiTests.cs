using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestAtlasRedesign(string directory, Action<bool,string> check, Action<string> click)
        {
            E.New(W.ByCode("TUR"),2020,1);speed=0;menuScreen="";showHelp=false;Navigate("Карта");Home();
            ClientSize=new Size(1600,900);clock.Stop();toastUntil=DateTime.MinValue;hoveredTip="";
            Action draw=()=>{using(var frame=RenderFrame()){};};
            Action<Point> tap=p=>{OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,(int)(p.X*UiScale),(int)(p.Y*UiScale),0));OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,(int)(p.X*UiScale),(int)(p.Y*UiScale),0));};
            int capital=W.Countries[E.S.Player].CapitalRegion;
            check(selected==capital && (army<0||E.S.Armies.Single(a=>a.Id==army).Region==capital) && cx==(float)W.Regions[capital].X && cy==(float)W.Regions[capital].Y,"Home selects the same capital shown by the camera and region inspector");
            E.S.Provinces[capital].Owner=W.ByCode("FRA");Home();
            check(selected!=capital&&selected>=0&&E.S.Provinces[selected].Owner==E.S.Player&&cx==(float)W.Regions[selected].X,"Home falls back to a controlled region when the capital is lost");
            E.S.Provinces[capital].Owner=E.S.Player;Home();
            draw();check(RegionInspectorVisible && flagCells.Count==174,"The region inspector opens on the chosen country with offline flags available");
            int chosen=selected;double cash=E.Player.Treasury;float camera=cx;
            tap(new Point(22,500));
            check(selected==chosen && cx==camera && E.Player.Treasury==cash,"The new inspector consumes background clicks without selecting territory or spending funds");
            click("region:ownership");check(selected==chosen,"A read-only ownership chart is safe to click");
            click("region:tab:1");draw();
            check(hits.Count(h=>h.Label!=null&&h.Label.StartsWith("region:industry:"))==8,"The regional industry table exposes the eight implemented sectors");
            click("region:industry:4");
            check(page=="Производство"&&detailPanel&&selected==chosen&&!equipmentProduction,"Regional industry opens actual construction for the same owned region");
            Navigate("Карта");draw();click("region:population-layer");draw();
            check(layer=="Население","The region panel activates the actual population-density layer");
            double population=E.S.Provinces[chosen].Population;string before=AtlasContentKey(false);Color color=PopulationColor(chosen);
            E.S.Provinces[chosen].Population=population*.01;Invalidate();
            check(AtlasContentKey(false)!=before && PopulationColor(chosen)!=color,"A material population change updates density colors and invalidates their atlas snapshot");
            E.S.Provinces[chosen].Population=population;Invalidate();
            var square=new Land {Rings=new[]{new[]{new PointF(0,0),new PointF(1,0),new PointF(1,-1),new PointF(0,-1)}}};
            var dateline=new Land {Rings=new[]{new[]{new PointF(179,0),new PointF(-179,0),new PointF(-179,-1),new PointF(179,-1)}}};
            check(Math.Abs(SphericalAreaKm2(square)-12363.718)<1 && Math.Abs(SphericalAreaKm2(dateline)/SphericalAreaKm2(square)-2)<.001,"Density areas use spherical geometry and normalize date-line crossings");
            double moldova=W.Countries[W.ByCode("MDA")].Regions.Sum(id=>RegionAreaKm2(id));
            check(moldova>30000&&moldova<37000,"Moldova's combined mapped districts have a plausible spherical area: "+moldova.ToString("0")+" km2");
            draw();click("region:close");draw();
            check(!RegionInspectorVisible&&MapInputAllowed(new Point(25,400)),"Closing the region inspector releases its map area");
            selected=W.Countries[E.S.Player].Regions.First(id=>id!=chosen);draw();
            check(RegionInspectorVisible,"Selecting another area reopens the region inspector");
            int owner=E.S.Player;selected=W.Countries[W.ByCode("FRA")].CapitalRegion;draw();click("region:manage");
            check(page=="Дипломатия"&&foreign==W.ByCode("FRA")&&E.S.Player==owner,"A foreign region opens diplomacy rather than another country's construction controls");
            Navigate("Карта");Home();draw();float zoom=scale;int selection=selected;
            var mini=MiniMapImageRect;tap(new Point((int)(mini.X+mini.Width*.25),(int)(mini.Y+mini.Height*.5)));draw();
            check(Math.Abs(cx+90)<2&&Math.Abs(cy)<2&&scale==zoom&&selected==selection,"Minimap mouse input moves the camera geographically without changing scale or selected region");
            draw();var dragStart=new Point(500,650);
            for(int dx=400;dx<1100;dx+=17)if(!hits.Any(h=>h.Rect.Contains(dx,650))){dragStart=new Point(dx,650);break;}
            OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,(int)(dragStart.X*UiScale),(int)(dragStart.Y*UiScale),0));
            var release=new Point((int)(mini.X+mini.Width/2),(int)(mini.Y+mini.Height/2));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,(int)(release.X*UiScale),(int)(release.Y*UiScale),0));
            float draggedCamera=cx;bool didDrag=panning&&moved;
            OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,(int)(release.X*UiScale),(int)(release.Y*UiScale),0));
            check(didDrag&&cx==draggedCamera&&!panning,"Releasing a map drag over the minimap does not teleport the camera");
            float miniCamera=cx;click("map:layers");tap(new Point((int)mini.X+4,(int)mini.Bottom-4));
            check(cx==miniCamera,"An open layer picker blocks the minimap underneath");
            mapLayersExpanded=false;layer="Политика";Home();scale=70;regionInspectorTab=0;draw();WaitForAtlas();
            foreach(var size in new[]{new Size(1280,720),new Size(1600,900),new Size(1920,1080)}) {
                ClientSize=size;draw();WaitForAtlas();
                using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"atlas19-region-"+size.Width+".png"));
                var controls=hits.Where(h=>h.FocusTarget&&h.Label!=null&&h.Label.StartsWith("region:")).ToArray();
                check(controls.All(h=>RegionInspectorRect.Contains(h.Rect))&&!controls.Any(a=>controls.Any(b=>a!=b&&RectangleF.Intersect(a.Rect,b.Rect).Width>1&&RectangleF.Intersect(a.Rect,b.Rect).Height>1)),"Region controls fit without overlap at "+size.Width+"x"+size.Height);
                check(!OutlinerRect.IntersectsWith(MiniMapRect)&&!MiniMapRect.IntersectsWith(MapPickerRect),"Minimap, operational summary and layer control do not overlap at "+size.Width);
            }
            ClientSize=new Size(1600,900);click("region:tab:1");using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"atlas19-industry.png"));
            click("region:tab:2");using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"atlas19-resources.png"));
            click("region:population-layer");draw();WaitForAtlas();using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"atlas19-density.png"));
            layer="Политика";regionInspectorTab=0;E.Validate(E.S);
            var raster=CaptureAtlas("overlap-fixture",false);raster.Width=raster.Height=256;raster.X=33;raster.Y=-40;raster.Zoom=raster.PixelScale=70;
            using(var first=AtlasRaster.Render(raster)) {
                raster.X+=128/raster.PixelScale;
                using(var second=AtlasRaster.Render(raster)) {
                    int count=0,close=0;double error=0;
                    for(int yy=16;yy<240;yy+=4)for(int xx=144;xx<240;xx+=4) {
                        var a=first.GetPixel(xx,yy);var b=second.GetPixel(xx-128,yy);int difference=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);
                        error+=difference;count++;if(difference<24)close++;
                    }
                    check(error/count<4&&close>count*.98,"World-anchored shading and grain agree across overlapping tiles (mean RGB error "+(error/count).ToString("0.00")+")");
                }
            }
            using(var canvas=new Bitmap(80,45))using(var ink=Graphics.FromImage(canvas)) {
                var unit=E.S.Armies.First(a=>a.Owner==E.S.Player);int badges=UnitBadgeBuilds;var badgeBox=new RectangleF(0,0,70,37);
                DrawUnitBadge(ink,unit,badgeBox,10001,100,3,75,-9900000,false,true);
                DrawUnitBadge(ink,unit,badgeBox,10002,101,3,75,-9900000,false,true);
                check(UnitBadgeBuilds==badges+1,"Small changes hidden by displayed personnel/count rounding reuse the unit bitmap");
                DrawUnitBadge(ink,unit,badgeBox,11000,101,3,75,-9900000,false,true);
                check(UnitBadgeBuilds==badges+2,"Crossing a visible personnel-label threshold refreshes the unit bitmap");
            }
            OpenMenu("new");click("○ Экспериментальная эпоха");startYear.Value=2000;click("✓ Экспериментальная эпоха");
            check(newYear==2020 && startYear.Value==2020 && !experimentalYear,"Leaving experimental dates restores the requested 2020 start");
            ContinueGame();
        }
    }
}
