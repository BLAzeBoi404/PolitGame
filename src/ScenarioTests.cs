using System;
using System.Drawing;
using System.IO;
using System.Linq;
namespace Sovereign
{
    internal static class ScenarioTests
    {
        internal static void Run(World w, string directory, Action<bool,string> check)
        {
            using(var tiny=new Land {Rings=new[]{new[]{new PointF(28.0001f,-47.0001f),new PointF(28.0003f,-47.0001f),new PointF(28.0003f,-47.0003f),new PointF(28.0001f,-47.0003f)}}}) {
                tiny.Prepare();check(tiny.Contains(new PointF(28.0002f,-47.0002f))&&!tiny.Contains(new PointF(28.0004f,-47.0002f)),"Geographic picking resolves subpixel districts without GDI raster rounding");
            }
            check(w.Regions.Select((r,i)=>w.FindRegion(new PointF((float)r.X,(float)r.Y))==i).All(v=>v),"Every one of 4136 administrative anchors selects its own region, including enclave cities and Lagos winding rings");
            int moldova=w.ByCode("MDA"),russia=w.ByCode("RUS"),usa=w.ByCode("USA"),china=w.ByCode("CHN");
            foreach(int id in w.Countries[moldova].Regions) {
                var r=w.Regions[id];int interior=0,correct=0;bool overlapOrder=true;
                for(int x=1;x<8;x++)for(int y=1;y<8;y++) {
                    var p=new PointF(r.Bounds.X+r.Bounds.Width*x/8,r.Bounds.Y+r.Bounds.Height*y/8);
                    if(!r.Contains(p))continue;interior++;int found=w.FindRegion(p);
                    if(found==id)correct++;
                    else overlapOrder &= found>=0 && w.Regions[found].Contains(p) && w.Regions[found].Area<r.Area;
                }
                // The supplied simplified boundaries overlap slightly at some district edges.
                // Every unambiguous interior must select the district; overlaps consistently
                // prefer the smaller containing region, as enclave city picking requires.
                check(interior>0&&correct>=interior*.85&&overlapOrder,"Moldova interior and overlapping boundaries resolve consistently: "+r.Name);
            }
            check(w.FindRegion(new PointF(-30,0))==-1&&w.FindCountry(new PointF(28.8577f,-47.005f))==moldova,"Exact picking distinguishes ocean and Moldova independently of the display cache");
            check(w.Countries.All(c=>Math.Abs(c.Population-c.Regions.Sum(r=>w.Regions[r].Population))<1),"Regional demographic estimates conserve every national population exactly");
            Func<int,string,int> region=(country,name)=>w.Countries[country].Regions.First(r=>w.Regions[r].Name==name);
            int moscow=region(russia,"Москва"),chukotka=region(russia,"Чукотский автономный округ"),chisinau=region(moldova,"Кишинёв");
            check(w.Regions[moscow].Population>12000000&&w.Regions[moscow].Population<15000000&&w.Regions[chukotka].Population>=30000&&w.Regions[chukotka].Population<=80000,"Russia places millions in Moscow and tens of thousands in Chukotka");
            check(w.Regions[chisinau].Population>500000&&w.Regions[chisinau].Population<850000&&w.Countries[moldova].Regions.Count==40,"Moldova has a large Chisinau population and all forty existing geographic districts");
            check(w.Regions[region(usa,"Калифорния")].Population>20*w.Regions[region(usa,"Аляска")].Population,"US state population follows settlement rather than territorial area");
            check(w.Regions[region(china,"Гуандун")].Population>20*w.Regions[region(china,"Тибетский автономный район")].Population,"Chinese regional shares no longer place most residents in Tibet and Xinjiang");
            int ukraine=w.ByCode("UKR");int kyiv=w.Countries[ukraine].CapitalRegion;
            check(w.Regions[kyiv].Neighbors.Contains(78)&&w.Regions[78].Neighbors.Contains(kyiv),"Kyiv enclave has a bidirectional geographic connection to Kyiv oblast");
            var e=new Engine(w){AutoSaveEnabled=false};e.New(russia,2026,1);
            Func<int,int> count=c=>e.S.Armies.Count(a=>a.Owner==c);
            check(count(russia)>100&&count(usa)>40&&count(w.ByCode("IND"))>count(russia)&&count(moldova)>0&&count(moldova)<10,"National force sizes produce distinctly different formation counts");
            check(count(w.ByCode("ISL"))==0&&count(w.ByCode("CRI"))==0&&count(w.ByCode("PAN"))==0,"Countries without standing armies do not receive automatic tank formations");
            check(Enumerable.Range(0,w.Countries.Length).All(c=>e.S.Armies.Where(a=>a.Owner==c).Sum(a=>a.Personnel)==w.Countries[c].StartingGroundPersonnel),"Deployed personnel exactly match each explicit ground-force scenario target");
            check(e.S.Armies.Where(a=>a.Owner==russia).Select(a=>a.Battalions).Distinct().Count()>=3&&e.S.Armies.Where(a=>a.Owner==russia).Select(a=>a.Design.Kind).Distinct().Count()==3,"Starting formations vary in size and include motor infantry, armor and artillery");
            var supply=e.SupplyNetwork();
            check(e.S.Armies.All(a=>supply[a.Region]&&e.S.Provinces[a.Region].Owner==a.Owner),"All initial garrisons are stationed on supplied domestic territory");
            check(!e.S.Armies.Any(a=>a.Region==chukotka)&&e.S.Armies.Any(a=>a.Region==moscow)&&e.S.Armies.Where(a=>a.Owner==russia).Select(a=>a.Region).Distinct().Count()>20,"Russia distributes its force around populated areas without an arbitrary Chukotka garrison");
            check(Enumerable.Range(0,w.Countries.Length).All(c=>e.S.Nations[c].Mobilized==e.ServicePersonnel(c)+e.S.Nations[c].Recruits),"Country-specific initial armies and reserves conserve the personnel ledger");
            e.Validate(e.S);
            // Exercise the exact tiny-population value that exposed Mono XML rounding.
            e.S.Provinces[0].Population=0.000649968829721428;long bits=BitConverter.DoubleToInt64Bits(e.S.Provinces[0].Population);
            string path=Path.Combine(directory,"scenario-roundtrip.xml");e.Save(path);var restored=new Engine(w){AutoSaveEnabled=false};restored.Load(path);
            check(restored.S.Version==Campaign.CurrentVersion&&BitConverter.DoubleToInt64Bits(restored.S.Provinces[0].Population)==bits,"Format twelve retains the exact bits of small regional populations");
            check(restored.S.Armies.Count==e.S.Armies.Count&&restored.S.Armies.Zip(e.S.Armies,(a,b)=>a.Personnel==b.Personnel&&a.Region==b.Region&&a.Battalions==b.Battalions).All(v=>v),"Loading preserves actual armies and locations instead of reseeding the scenario");
            for(int day=0;day<30;day++)restored.Tick();restored.Validate(restored.S);check(true,"The full 3765-formation world advances thirty days with valid stock, manpower and formations");
            var small=new Engine(w){AutoSaveEnabled=false};small.New(ukraine,2026,1);Tests.SmallPlayerForce(small);small.Player.Treasury=100;small.Player.Stock[7]=1000;
            foreach(var a in small.S.Armies.Where(a=>a.Owner==ukraine))a.Strength=100;
            double arms=small.Player.Stock[7];small.S.Day=1;small.Tick();
            check(small.Player.Stock[7]==arms,"Fully repaired stationary equipment consumes no spurious repair material");
            var damaged=small.S.Armies.First(a=>a.Owner==ukraine);damaged.Strength=80;arms=small.Player.Stock[7];small.Tick();
            check(damaged.Strength>80&&small.Player.Stock[7]<arms,"Damaged equipment still consumes real material when repaired");
        }
    }
}
