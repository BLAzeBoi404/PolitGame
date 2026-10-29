using System;
using System.IO;
using System.Linq;
namespace Sovereign
{
    internal static class OperationsTests
    {
        public static void Run(World world, string directory, Action<bool,string> check)
        {
            Func<Engine> fresh = () => { var e = new Engine(world) {AutoSaveEnabled = false}; e.New(world.ByCode("UKR"),2026,1); e.Player.Treasury = 1000; foreach(var n in e.S.Nations) for(int k=0;k<8;k++)n.Stock[k]=10000; return e; };
            var e1 = fresh(); int destination = world.Countries[e1.S.Player].Regions[0], supplier = world.ByCode("ITA"), region = world.Countries[supplier].Regions[0];
            int company = region*8+5;
            check(Enumerable.Range(0, world.Regions.Length).Where(r => e1.S.Provinces[r].Owner == supplier).Any(r => e1.CompanyCapacity(r * 8 + 5) > 0), "The unmodified Italian economy has eligible private machine-building suppliers");
            e1.S.Provinces[region].Private[5] = .4;
            double before = e1.Player.Treasury, capital = e1.S.Nations[supplier].PrivateCapital;
            check(e1.PlaceContract(company,8,2,2,destination)==null && e1.Player.Treasury==before && e1.CompanyAssigned(company)==2,"Contract reserves actual Italian private capacity without an upfront or duplicate charge");
            check(e1.PlaceContract(company,9,1,3,destination)!=null,"Two contracts cannot overbook the same company's capacity");
            var c=e1.Player.Contracts[0]; double steel=e1.S.Nations[supplier].Stock[4]; e1.TickContracts();
            check(c.Progress>0 && c.Delivered==0 && e1.S.Provinces[destination].Buses==0 && e1.S.Nations[supplier].Stock[4]<steel,"Civil vehicle production consumes supplier materials and delivers only complete batches");
            check(Math.Abs(before-e1.Player.Treasury-c.Paid)<1e-9 && Math.Abs(e1.S.Nations[supplier].PrivateCapital-capital-c.Paid)<1e-9,"Contract payments transfer exactly from the public treasury to the supplier's private capital");
            double progress=c.Progress, work=e1.S.Provinces[region].ContractWork[5];
            e1.ToggleContract(c.Id);e1.TickContracts();
            check(c.Progress==progress && e1.CompanyAssigned(company)==0 && e1.S.Provinces[region].ContractWork[5]==work,"Pausing releases capacity, retains unfinished work and preserves the weekly capacity ledger");
            e1.ToggleContract(c.Id); e1.S.Embargo[e1.Pair(supplier,e1.S.Player)]=true; e1.TickContracts();
            check(c.Progress==progress && e1.ContractReason(company,8,1,1,destination)!=null,"Supplier-side embargo blocks both existing deliveries and new contracts");
            e1.S.Embargo[e1.Pair(supplier,e1.S.Player)]=false;
            e1.S.Wars[e1.Pair(supplier,e1.S.Player)]=e1.S.Wars[e1.Pair(e1.S.Player,supplier)]=true; e1.TickContracts();check(c.Progress==progress,"War stops an already active private contract");
            e1.S.Wars[e1.Pair(supplier,e1.S.Player)]=e1.S.Wars[e1.Pair(e1.S.Player,supplier)]=false;
            e1.S.Provinces[region].Private[5]=.1;e1.TickContracts();check(c.Progress==progress && e1.ContractStatus(e1.S.Player,c)=="Утрачены мощности","Loss of reserved factories pauses work with an explicit status");e1.S.Provinces[region].Private[5]=.4;
            for(int i=0;i<250;i++)e1.TickContracts();
            check(c.Delivered==2 && c.Progress<1e-8 && e1.S.Provinces[destination].Buses==20 && Math.Abs(c.Paid-.036)<1e-8,"The quoted bus contract completes exactly once into the specified public fleet");
            e1.TickContracts();check(e1.S.Provinces[destination].Buses==20,"A completed contract cannot issue duplicate vehicles");
            e1.Tick(); var province=e1.S.Provinces[destination];
            check(province.FleetService>0 && ProcurementRules.Infrastructure(province)>province.Infrastructure,"Delivered vehicles receive paid fuel and improve effective regional transport services");
            e1.Player.Treasury=0;e1.Player.Stock[2]=0;e1.Tick();check(province.FleetService==0 && ProcurementRules.Infrastructure(province)==province.Infrastructure,"An unfunded or unfuelled public fleet provides no free service bonus");
            var goods=fresh();int domestic=destination*8;goods.S.Provinces[destination].Private[0]=2;double food=goods.Player.Stock[0];
            check(goods.PlaceContract(domestic,0,3,1,destination)==null,"Domestic food procurement uses private agricultural plants");
            for(int i=0;i<300;i++)goods.TickContracts();check(goods.Player.Stock[0]==food+3,"Food contracts deliver real usable national stocks");
            double reserved=goods.S.Provinces[destination].ContractWork[0];goods.CancelContract(goods.Player.Contracts[0].Id);
            check(reserved>0 && goods.S.Provinces[destination].ContractWork[0]==reserved,"Cancelling a completed order cannot erase factory time already used that week");
            var reference=fresh();reference.S.Provinces[destination].Private[0]=2; reference.Player.Stock=(double[])goods.Player.Stock.Clone();
            goods.ComputeEconomy(true);reference.ComputeEconomy(true);
            check(goods.Player.Production[0]<reference.Player.Production[0] && goods.S.Provinces[destination].ContractWork[0]==0,"Weekly ordinary output deducts settled contract factory-days and then resets the ledger");
            for(int product=1;product<8;product++) {
                var order=fresh();order.S.Provinces[destination].Private[product]=2;double priorStock=order.Player.Stock[product];
                check(order.PlaceContract(destination*8+product,product,1,2,destination)==null,"Private procurement accepts goods sector "+product);
                for(int i=0;i<400;i++)order.TickContracts();
                check(order.Player.Contracts[0].Delivered==1&&order.Player.Stock[product]==priorStock+1,"Goods sector "+product+" delivers exactly one paid unit into stocks");
            }
            var equipment=fresh();equipment.S.Provinces[destination].Private[7]=10;int design=equipment.Player.ActiveDesign;int stock=equipment.FindDesign(equipment.S.Player,design).Stock;
            int capacity=equipment.FactoryCapacity(equipment.S.Player); equipment.PlaceContract(destination*8+7,10,1,10,destination,design);
            check(equipment.FactoryCapacity(equipment.S.Player)==capacity-1,"Private defense contracts reduce factories available to equipment production lines");
            for(int i=0;i<150;i++)equipment.TickContracts();check(equipment.FindDesign(equipment.S.Player,design).Stock==stock+1,"A private equipment contract delivers the exact adopted model into the equipment inventory");
            var cancelled=fresh();cancelled.S.Provinces[destination].Private[5]=2;cancelled.PlaceContract(destination*8+5,9,5,1,destination);cancelled.TickContracts();double paid=cancelled.Player.Contracts[0].Paid;double cash=cancelled.Player.Treasury;cancelled.CancelContract(1);
            check(paid>0 && cancelled.Player.Treasury==cash && cancelled.S.Provinces[destination].Trucks==0,"Cancellation retains sunk costs without creating unfinished products or refunds");
            var a=equipment.S.Armies.First(u=>u.Owner==equipment.S.Player);a.Battalions=4;a.Personnel=ManpowerRules.Establishment(a);a.Organization=a.Supply=a.Strength=100;
            var enemy=new Army {Battalions=4,Personnel=2600,Organization=100,Supply=100,Strength=100,Design=a.Design.Copy()};
            a.Deployment=new BattleDeployment {Front=2,Left=1,Right=1};double flank=EquipmentRules.CombatPower(a,enemy,false);enemy.Deployment=new BattleDeployment {Front=2,Left=1,Right=1};
            check(flank>EquipmentRules.CombatPower(a,enemy,false),"Covering both flanks counters the attacker's manoeuvre advantage");
            a.Deployment.Specialty=FormationSpecialty.Assault;double assault=EquipmentRules.CombatPower(a,enemy,false),defense=EquipmentRules.CombatPower(a,enemy,true);a.Deployment.Specialty=FormationSpecialty.Entrenched;
            check(EquipmentRules.CombatPower(a,enemy,false)<assault && EquipmentRules.CombatPower(a,enemy,true)>defense,"Assault and defensive specialties create actual attack-defense tradeoffs");
            a.Experience=100;a.Proficiency=90;int veteran=DeploymentRules.Stars(a);double ready=ReadinessRules.Score(a);a.Supply=10;check(veteran==5 && DeploymentRules.Stars(a)==veteran && ReadinessRules.Score(a)<ready,"Elite qualification persists during supply shortages while actual readiness falls");a.TrainingDays=1;check(DeploymentRules.Stars(a)==0,"An untrained formation cannot display elite stars");
            var formation=fresh(); var layout=new BattleDeployment {Front=2,Left=1,Right=1,Specialty=FormationSpecialty.Mobile};
            check(formation.CreateTemplate("Манёвренная бригада",4,layout)==null && formation.CreateTemplate("Некорректный штат",2,layout)!=null,"Formation editor validates the total allocation instead of inventing extra battalions");
            var record=formation.FindDesign(formation.S.Player,formation.Player.ActiveDesign);record.Stock=20;formation.Player.Recruits+=10000;formation.Player.Mobilized+=10000;int people=formation.Player.Recruits;
            check(formation.Recruit(destination)==null,"A custom tactical formation recruits through the existing equipment and manpower rules");var unit=formation.S.Armies.Last();
            check(unit.Deployment.Left==1 && unit.Deployment.Right==1 && unit.Deployment.Front==2 && record.Stock==16 && people-formation.Player.Recruits==unit.Personnel,"Recruitment copies the chosen tactical layout and consumes exactly four kits and its personnel");
            layout.Left=0;check(unit.Deployment.Left==1,"Saved and recruited layouts do not alias the editor's mutable draft");
            formation.S.Provinces[destination].Private[5]=3;formation.PlaceContract(destination*8+5,9,2,1,destination);formation.TickContracts();
            string path=Path.Combine(directory,"operations-copy.xml");formation.Save(path);var loaded=fresh();loaded.Load(path);
            check(loaded.S.Version==Campaign.CurrentVersion && loaded.Player.Contracts.Count==1 && loaded.Player.Contracts[0].Paid==formation.Player.Contracts[0].Paid && loaded.S.Provinces[destination].ContractWork[5]>0 && loaded.S.Armies.Last().Deployment.Right==1,"Version-eleven saves retain live procurement payments, factory-days and tactical layouts");
            loaded.Validate(loaded.S);check(true,"Live operations pass the full campaign integrity and personnel balance validator");
            loaded.Player.Contracts[0].Progress=double.NaN;bool rejected=false;try{loaded.Validate(loaded.S);}catch(InvalidDataException){rejected=true;}check(rejected,"Nonfinite contract progress is rejected before loading");loaded.Load(path);loaded.Player.Contracts[0].Paid+=1;rejected=false;try{loaded.Validate(loaded.S);}catch(InvalidDataException){rejected=true;}check(rejected,"A forged payment total cannot disagree with contract progress");
            loaded.Load(path);loaded.S.Armies.Last().Deployment.Front=6;rejected=false;try{loaded.Validate(loaded.S);}catch(InvalidDataException){rejected=true;}check(rejected,"Impossible tactical allocations are rejected on load");
            var legacy=fresh();legacy.S.Version=10;double treasury=legacy.Player.Treasury;int count=legacy.S.Armies.Count;legacy.Save(path);loaded.Load(path);check(loaded.S.Version==Campaign.CurrentVersion && loaded.Player.Treasury==treasury && loaded.S.Armies.Count==count && loaded.Player.Contracts.Count==0,"Version-ten migration preserves money and armies without inventing contracts or vehicles");
            var prefs=new Preferences {WindowWidth=-1,WindowHeight=int.MaxValue,FrameLimit=17,GraphicsQuality=99,LabelDensity=-5};prefs.NormalizeDisplay();check(prefs.WindowWidth==1000 && prefs.WindowHeight==4320 && prefs.FrameLimit==60 && prefs.GraphicsQuality==2 && prefs.LabelDensity==0,"Invalid display settings normalize to bounded supported values");
        }
    }
}
