using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Sovereign
{
    internal static class TradeTests
    {
        internal static int RunSuite(World world,string directory)
        {
            Directory.CreateDirectory(directory);var rows=new List<string>();int failures=0;
            Run(world,directory,(ok,name)=>{string line=(ok?"PASS ":"FAIL ")+name;rows.Add(line);Console.WriteLine(line);if(!ok)failures++;});
            rows.Add("Failures: "+failures);File.WriteAllLines(Path.Combine(directory,"trade-results.txt"),rows);return failures==0?0:1;
        }
        internal static void Run(World world,string directory,Action<bool,string> check)
        {
            int me=world.ByCode("UKR"),seller=world.ByCode("POL"),germany=world.ByCode("DEU"),france=world.ByCode("FRA");
            Func<Engine> fresh=()=>{var e=new Engine(world){AutoSaveEnabled=false};e.New(me,2026,1);foreach(var n in e.S.Nations){n.AutoTrade=false;n.PrivateCapital=100;n.Treasury=1000;Array.Clear(n.Stock,0,8);Array.Clear(n.Demand,0,8);Array.Clear(n.Production,0,8);Array.Clear(n.Trade.Operations,0,8);}e.S.Nations[seller].AutoTrade=true;e.S.Nations[seller].Stock[2]=100;e.Player.Demand[2]=2;e.S.Relations[e.Pair(me,seller)]=e.S.Relations[e.Pair(seller,me)]=60;return e;};
            Action<string,Action> scenario=(name,run)=>{try{run();}catch(Exception ex){check(false,name+": "+ex);}};
            Func<Engine,double> cash=e=>e.S.Nations.Sum(n=>n.PrivateCapital+n.Treasury);
            scenario("Settlement",()=>{
                var e=fresh();double money=cash(e),goods=e.Player.Stock[2]+e.S.Nations[seller].Stock[2];
                check(e.SignTrade(seller,2,3,2,100)==null&&cash(e)==money,"Signing a supply agreement creates no money, stock or advance charge");
                e.S.Day=7;e.SettleTradeWeek(false,false);var d=e.S.TradeDeals.Single();var flow=e.S.TradeFlows.Single();
                check(d.LastDelivery==3&&d.Delivered==3&&Math.Abs(e.Player.Stock[2]+e.S.Nations[seller].Stock[2]-goods)<1e-10,"Weekly delivery transfers existing goods exactly once");
                check(Math.Abs(cash(e)+flow.Freight-money)<1e-8&&Math.Abs(e.Player.Trade.Tariffs-flow.Value*.1)<1e-10,"Settlement conserves money including customs transfer and explicit freight consumption");
                money=cash(e);e.SettleTradeWeek(false,false);check(cash(e)==money&&d.Delivered==3,"A second settlement on the same day cannot repeat payments or deliveries");
                e.S.Day=14;e.SettleTradeWeek(false,false);check(e.S.TradeDeals.Count==0&&e.Player.Stock[2]==6,"Final contracted week is delivered before the agreement expires");e.Validate(e.S);
            });
            scenario("Restrictions",()=>{
                var e=fresh();double money=cash(e);int count=e.S.TradeDeals.Count;
                check(e.SignTrade(seller,2,double.NaN,4,100)!=null&&e.SignTrade(seller,2,-1,4,100)!=null&&e.SignTrade(seller,8,1,4,100)!=null&&e.SignTrade(me,2,1,4,100)!=null&&e.SignTrade(seller,2,1,27,100)!=null&&e.S.TradeDeals.Count==count&&cash(e)==money,"Invalid proposals and self-trade leave balances and agreements unchanged");
                check(e.SignTrade(seller,2,100,4,100)!=null,"An exporter cannot promise its protected domestic reserve");
                e.S.Relations[e.Pair(me,seller)]=-30;check(e.SignTrade(seller,2,1,4,100)!=null,"A hostile supplier rejects an ordinary-price offer");
                e.S.Alliances[e.Pair(me,seller)]=e.S.Alliances[e.Pair(seller,me)]=true;check(e.SignTrade(seller,2,1,4,85)==null,"An allied supplier can accept a bounded strategic discount");
                check(e.SignTrade(france,2,1,4,100)!=null,"A good has one main supply agreement, with spot purchases covering the rest");
                int id=e.S.TradeDeals.Single().Id;check(e.CancelTrade(id,germany)!=null&&e.S.TradeDeals.Count==1,"Third parties cannot cancel another country's trade agreement");
                check(e.CancelTrade(id)==null&&cash(e)==money,"Cancelling an undelivered agreement grants no refund or free capital");
                check(e.SetTradePolicy(31,3)!=null&&e.SetTradePolicy(10,0)!=null&&e.Player.Trade.Tariff==10,"Invalid tariffs and reserve targets are rejected");
            });
            scenario("Disruption and recovery",()=>{
                var e=fresh();e.SignTrade(seller,2,5,4,100);var d=e.S.TradeDeals.Single();double money=cash(e);
                e.S.Embargo[e.Pair(seller,me)]=true;e.S.Day=7;e.SettleTradeWeek(false,false);
                check(d.LastDelivery==0&&cash(e)==money&&EContains(e.TradeDealStatus(d),"эмбарго"),"A supplier embargo suspends deliveries and charges, including existing contracts");
                e.S.Embargo[e.Pair(seller,me)]=false;e.S.Nations[seller].Stock[2]=2;e.S.Day=14;e.SettleTradeWeek(false,false);
                check(d.LastDelivery==1&&e.S.Nations[seller].Stock[2]>=1,"A shortage produces only a partial delivery and preserves the exporter's reserve");
                e.S.Nations[seller].Stock[2]=100;e.Player.PrivateCapital=e.S.MarketPrices[2]*(1+.1+.08)*2;e.S.Day=21;e.SettleTradeWeek(false,false);
                check(Math.Abs(d.LastDelivery-2)<1e-8&&e.Player.PrivateCapital>=0,"An insolvent buyer receives only what it can pay for without hidden credit");
                e.Player.PrivateCapital=100;e.S.Day=22;e.Diplomacy(seller,3);money=cash(e);e.S.Day=28;e.SettleTradeWeek(false,false);
                check(cash(e)==money&&e.S.TradeFlows.Count==0,"War blocks the last promised shipment with no fictitious fee or reimbursement");
            });
            scenario("Preferences and market accounting",()=>{
                var e=fresh();e.Player.AutoTrade=true;e.S.Agreements[e.Pair(me,seller)]=e.S.Agreements[e.Pair(seller,me)]=true;
                e.S.Day=7;e.SettleTradeWeek(false,false);var f=e.S.TradeFlows.Single();
                check(Math.Abs(f.Tariff/f.Value-.025)<1e-10&&Math.Abs(f.Freight/f.Value-.03)<1e-10,"A preferential treaty reduces actual customs and freight charges");
                check(Math.Abs(e.Player.Stock[2]-e.ImportTarget(me,2))<1e-8&&e.ExportSurplus(me,2)==0,"Spot trade fills a strategic reserve without immediately re-exporting the same imports");
                e=fresh();foreach(var n in e.S.Nations)n.AutoTrade=true;double[] before=e.S.Nations.Select(n=>n.PrivateCapital).ToArray();double[] publicBefore=e.S.Nations.Select(n=>n.Treasury).ToArray();e.S.Day=7;e.ComputeEconomy(true);
                check(e.S.Nations.Select((n,i)=>Math.Abs(n.PrivateCapital-(before[i]+n.Exports-n.Imports+Math.Max(0,n.GDP/52*(1-n.Tax/100)*.06)))<1e-7).All(x=>x),"Economy credits exporter receipts once, without adding the trade balance again as profit");
                check(e.S.Nations.Select((n,i)=>Math.Abs(n.Treasury-(publicBefore[i]+n.Revenue-n.Spending))<1e-7).All(x=>x),"Budget includes customs income but never credits the same tariff twice");
                check(e.S.MarketPrices.Zip(Rules.Prices,(a,b)=>a>=b*.45&&a<=b*3.5).All(x=>x),"A common world price remains finite and bounded by global supply and demand");e.Validate(e.S);
            });
            scenario("Supplier ranking invalidation",()=>{
                var e=fresh();e.Player.AutoTrade=true;e.S.Nations[france].AutoTrade=true;e.S.Nations[france].Stock[2]=100;
                e.S.Alliances[e.Pair(me,seller)]=true;e.S.Day=7;e.SettleTradeWeek(false,false);
                check(e.S.TradeFlows.Single().Seller==seller,"Open-market procurement prefers the actual allied supplier at equal landed cost");
                e.Player.Stock[2]=0;e.S.Embargo[e.Pair(seller,me)]=true;e.S.Day=14;e.SettleTradeWeek(false,false);
                check(e.S.TradeFlows.Single().Seller==france,"A new reverse-direction embargo invalidates the cached supplier ranking immediately");
                e.Player.Stock[2]=0;e.S.Embargo[e.Pair(seller,me)]=false;e.S.Agreements[e.Pair(me,france)]=true;e.S.Day=21;e.SettleTradeWeek(false,false);
                check(e.S.TradeFlows.Single().Seller==france,"An updated preferential treaty changes real sourcing according to landed cost");
                e.Player.Stock[2]=0;e.S.Nations[france].AutoTrade=false;e.S.Day=28;e.SettleTradeWeek(false,false);
                check(e.S.TradeFlows.Single().Seller==seller,"Closing a supplier market invalidates an otherwise cheaper cached offer");
            });
            scenario("Industry and operations",()=>{
                var e=fresh();int region=world.Countries[me].Regions[0];e.S.Provinces[region].Public[7]=10;
                e.Player.AutoTrade=true;e.StartProduction(e.Player.ActiveDesign,1,8);var line=e.Player.Lines.Single();e.TickProduction();check(line.Progress==0,"An equipment line actually stops without its required materials");
                e.PrepareTradeNeeds();check(e.Player.Trade.Operations[4]>0&&e.Player.Trade.Operations[5]>0&&e.Player.Trade.Operations[7]>0,"Unfilled steel, machinery and arms requirements enter the weekly trade forecast");
                foreach(int good in new[]{4,5,7})e.S.Nations[seller].Stock[good]=1000;e.S.Day=7;e.SettleTradeWeek(false,false);e.TickProduction();check(line.Progress>0&&e.Player.Imports>0,"Imported materials restart actual equipment production through the same stock ledger");
                var unit=e.S.Armies.First(a=>a.Owner==me);double before=e.Player.Trade.Operations[2];unit.Target=unit.Region;e.PrepareTradeNeeds();check(e.Player.Trade.Operations[2]>before,"Deploying a formation increases forecast fuel procurement using the movement consumption formula");
                e.Player.Lines[0].Paused=true;e.PrepareTradeNeeds();check(e.Player.Trade.Operations[4]==0,"Paused assembly lines stop reserving imports for nonexistent work");
            });
            scenario("Public procurement inputs",()=>{
                var e=fresh();int region=world.Countries[me].Regions[0];e.S.Provinces[region].Private[5]=10;
                check(e.PlaceContract(region*8+5,8,10,1,region)==null,"A real bus procurement order is accepted against existing company capacity");
                e.TickContracts();var contract=e.Player.Contracts.Single();check(contract.Progress==0,"The government order cannot manufacture buses without inputs");
                e.PrepareTradeNeeds();check(e.Player.Trade.Operations[4]>0&&e.Player.Trade.Operations[5]>0,"Even a stalled government order forecasts the supplier's missing steel and machinery");
                e.Player.AutoTrade=true;for(int good=0;good<8;good++)e.S.Nations[seller].Stock[good]=1000;
                e.S.Day=7;e.SettleTradeWeek(false,false);e.TickContracts();check(contract.Progress>0,"Market inputs restart a government order and debit actual production materials");
                contract.Paused=true;e.PrepareTradeNeeds();check(e.Player.Trade.Operations[4]==0&&e.Player.Trade.Operations[5]==0,"Suspended government contracts create no phantom material demand");
            });
            scenario("AI and commitments",()=>{
                var e=fresh();var buyer=e.S.Nations[germany];buyer.AutoTrade=true;buyer.Demand[2]=8;e.S.Nations[france].AutoTrade=true;e.S.Nations[france].Stock[2]=100;
                e.S.Relations[e.Pair(germany,seller)]=60;e.S.Relations[e.Pair(germany,france)]=60;e.S.Alliances[e.Pair(germany,seller)]=e.S.Alliances[e.Pair(seller,germany)]=true;
                e.PlanTradeAgreements();var d=e.S.TradeDeals.Single(x=>x.Buyer==germany&&x.Good==2);
                check(d.Seller==seller&&d.UnitPrice<e.S.MarketPrices[2]&&d.WeeklyQuantity<=e.ExportSurplus(seller,2),"AI chooses an affordable allied supplier from real deficits, price and stock");
                e.S.Embargo[e.Pair(seller,germany)]=true;e.PlanTradeAgreements();check(e.S.TradeDeals.Single(x=>x.Buyer==germany&&x.Good==2).Seller==france,"AI finds an alternative supplier after an embargo rather than repeating a forbidden purchase");
                check(e.S.TradeDeals.All(x=>x.Seller!=me),"AI cannot sign an export commitment on behalf of the player");
                e=fresh();e.S.Nations[seller].Stock[2]=15;e.S.Nations[germany].PrivateCapital=100;e.S.Relations[e.Pair(germany,seller)]=60;
                check(e.SignTrade(seller,2,10,4,100)==null&&e.SignTrade(seller,2,10,4,100,germany)!=null,"Existing commitments prevent a seller from promising the same available reserve twice");
            });
            scenario("Persistence",()=>{
                var e=fresh();e.SignTrade(seller,2,2,4,100);e.S.Day=7;e.SettleTradeWeek(false,false);string path=Path.Combine(directory,"trade-save.xml");e.Save(path);
                var loaded=new Engine(world){AutoSaveEnabled=false};loaded.Load(path);
                check(loaded.S.Version==Campaign.CurrentVersion&&loaded.S.TradeDeals.Single().Delivered==2&&loaded.S.TradeFlows.Single().Quantity==2,"Market prices, policies, commitments, deliveries and ledger survive saving");
                for(int day=0;day<35;day++){e.Tick();loaded.Tick();}check(e.S.RandomState==loaded.S.RandomState&&e.S.Nations.Zip(loaded.S.Nations,(a,b)=>a.Treasury==b.Treasury&&a.PrivateCapital==b.PrivateCapital&&a.Stock.SequenceEqual(b.Stock)).All(x=>x),"Trade and AI continue deterministically for five weeks after loading");
                var old=fresh();old.S.Version=12;old.S.TradeDeals=null;old.S.MarketPrices=null;old.Player.Trade=null;double treasury=old.Player.Treasury;int armies=old.S.Armies.Count;uint random=old.S.RandomState;old.Save(path);loaded.Load(path);
                check(loaded.S.Version==Campaign.CurrentVersion&&loaded.S.TradeDeals.Count==0&&loaded.Player.Treasury==treasury&&loaded.S.Armies.Count==armies&&loaded.S.RandomState==random,"Format twelve migrates without changing money, armies, RNG or inventing foreign obligations");
                loaded.Save(path);string valid=File.ReadAllText(path);File.WriteAllText(path,valid.Replace("<Tariff>10</Tariff>","<Tariff>99</Tariff>"));bool rejected=false;try{loaded.Load(path);}catch(InvalidDataException){rejected=true;}check(rejected,"Malformed saved customs policy is rejected before replacing the campaign");
                var bad=fresh();bad.SignTrade(seller,2,1,4,100);bad.S.TradeDeals[0].Paid=10;rejected=false;try{bad.Validate(bad.S);}catch(InvalidDataException){rejected=true;}check(rejected,"Forged contract payments inconsistent with delivered quantities are rejected");
            });
        }
        private static bool EContains(string value,string term)=>value.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0;
    }
}
