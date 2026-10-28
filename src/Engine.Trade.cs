using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        private void InitializeTrade(Campaign campaign)
        {
            campaign.MarketPrices=(double[])Rules.Prices.Clone();campaign.TradeDeals=new List<TradeDeal>();campaign.TradeFlows=new List<TradeFlow>();campaign.NextTradeDeal=1;campaign.TradeDay=-1;
            foreach(var nation in campaign.Nations)nation.Trade=new TradePolicy();
            campaign.Version=13;
        }
        public double TradeNeed(int owner,int good) => S.Nations[owner].Demand[good]+S.Nations[owner].Trade.Operations[good];
        public double ImportTarget(int owner,int good) => TradeNeed(owner,good)*S.Nations[owner].Trade.ReserveWeeks;
        public double ExportSurplus(int owner,int good) => Math.Max(0,S.Nations[owner].Stock[good]-Math.Max(1,TradeNeed(owner,good)*(S.Nations[owner].Trade.ReserveWeeks+1)));
        public string TradeBlock(int buyer,int seller)
        {
            if(buyer<0||seller<0||buyer>=S.Nations.Length||seller>=S.Nations.Length||buyer==seller)return "Выберите другую страну.";
            if(AtWar(buyer,seller))return "Торговля прекращена войной.";
            if(S.Embargo[Pair(buyer,seller)]||S.Embargo[Pair(seller,buyer)])return "Поставки блокирует эмбарго.";
            if(!S.Nations[seller].AutoTrade)return "Поставщик закрыл внешний рынок.";
            return null;
        }
        public double ImportTariff(int buyer,int seller) => S.Nations[buyer].Trade.Tariff/100.0*(S.Agreements[Pair(buyer,seller)]?.25:1);
        public double FreightRate(int buyer,int seller) => S.Agreements[Pair(buyer,seller)]?.03:.08;
        public double LandedPrice(int buyer,int seller,int good) => S.MarketPrices[good]*(1+ImportTariff(buyer,seller)+FreightRate(buyer,seller));
        public int MinimumTradeOffer(int buyer,int seller) => Allied(buyer,seller)?85:S.Relations[Pair(buyer,seller)]>=50?95:S.Relations[Pair(buyer,seller)]<0?110:100;
        private double CommittedExports(int seller,int good) => S.TradeDeals.Where(d=>d.Seller==seller&&d.Good==good&&d.EndDay>S.Day).Sum(d=>d.WeeklyQuantity);
        public double AvailableTradeOffer(int seller,int good) => Math.Max(0,ExportSurplus(seller,good)-CommittedExports(seller,good));
        public string TradeOfferReason(int buyer,int seller,int good,double quantity,int weeks,int percent)
        {
            string blocked=TradeBlock(buyer,seller);if(blocked!=null)return blocked;
            if(good<0||good>=8||!TradeRules.Finite(quantity)||quantity<=0||quantity>1000000||weeks<1||weeks>TradeRules.MaxWeeks||percent<80||percent>150)return "Некорректные условия поставки.";
            if(seller==S.Player&&buyer!=S.Player)return "ИИ не подписывает экспортное обязательство за игрока.";
            if(S.TradeDeals.Any(d=>d.Buyer==buyer&&d.Good==good&&d.EndDay>=S.Day))return "Для этого товара уже есть основной договор. Сначала завершите или отмените его.";
            if(percent<MinimumTradeOffer(buyer,seller))return "Поставщик требует не менее "+MinimumTradeOffer(buyer,seller)+"% мировой цены: учитываются отношения и союз.";
            if(quantity>AvailableTradeOffer(seller,good)+1e-8)return "Поставщик сохраняет внутренний резерв и уже обещанные поставки; свободно "+AvailableTradeOffer(seller,good).ToString("0.00")+" ед./нед.";
            double first=quantity*S.MarketPrices[good]*percent/100*(1+FreightRate(buyer,seller)+ImportTariff(buyer,seller));
            if(S.Nations[buyer].PrivateCapital+1e-9<first)return "У бизнеса не хватает оборотного капитала на первую неделю.";
            return null;
        }
        public string SignTrade(int seller,int good,double quantity,int weeks,int percent,int buyer=-1)
        {
            if(buyer<0)buyer=S.Player;S.TradeDeals.RemoveAll(d=>d.EndDay<S.Day);string reason=TradeOfferReason(buyer,seller,good,quantity,weeks,percent);if(reason!=null)return reason;
            S.TradeDeals.Add(new TradeDeal {Id=S.NextTradeDeal++,Buyer=buyer,Seller=seller,Good=good,WeeklyQuantity=quantity,UnitPrice=S.MarketPrices[good]*percent/100,StartedDay=S.Day,EndDay=S.Day+weeks*7});
            if(buyer==S.Player)Notify("Поставки: "+World.Countries[seller].Name+" → "+Rules.Goods[good]+", "+quantity.ToString("0.00")+" ед./нед. Оплата при фактической доставке.",1);
            if(buyer==S.Player)Raise();return null;
        }
        public string CancelTrade(int id,int actor=-1)
        {
            if(actor<0)actor=S.Player;var d=S.TradeDeals.FirstOrDefault(t=>t.Id==id);if(d==null||d.Buyer!=actor&&d.Seller!=actor)return "Нет доступа к договору.";
            S.TradeDeals.Remove(d);if(d.Buyer==S.Player||d.Seller==S.Player){Notify("Договор поставок №"+id+" прекращён. Платежи за доставленный товар не возвращаются.");Raise();}return null;
        }
        public string TradeDealStatus(TradeDeal d)
        {
            if(d.EndDay<=S.Day)return "Срок завершён";
            string block=TradeBlock(d.Buyer,d.Seller);if(block!=null)return block;
            if(ExportSurplus(d.Seller,d.Good)<.001)return "Нет свободного товара у поставщика";
            if(S.Nations[d.Buyer].PrivateCapital<d.UnitPrice*(1+FreightRate(d.Buyer,d.Seller)+ImportTariff(d.Buyer,d.Seller))*.001)return "Нет оборотного капитала";
            return d.LastDay<0?"Первая поставка в ближайший недельный расчёт":d.LastDelivery+1e-8<d.WeeklyQuantity?"Частичная поставка":"Поставляется";
        }
        public string SetTradePolicy(int tariff,int reserveWeeks,int owner=-1)
        {
            if(owner<0)owner=S.Player;if(owner>=S.Nations.Length||tariff<0||tariff>30||reserveWeeks<1||reserveWeeks>12)return "Пошлина: 0–30%; резерв: 1–12 недель.";
            var p=S.Nations[owner].Trade;p.Tariff=tariff;p.ReserveWeeks=reserveWeeks;Raise();return null;
        }
        // A bounded weekly pass; never queried from a map frame. This is a forecast,
        // not a second resource debit. Unfunded factories do not generate purchase needs.
        public void PrepareTradeNeeds()
        {
            var capacity=EquipmentCapacity();var budgets=S.Nations.Select(n=>Math.Max(0,n.Treasury)).ToArray();
            for(int owner=0;owner<S.Nations.Length;owner++) {
                var n=S.Nations[owner];var needs=n.Trade.Operations;Array.Clear(needs,0,8);
                if(n.Treasury<=0)continue;
                double budget=n.Treasury;int available=Math.Min(500,(int)Math.Floor(capacity[owner]));var tech=TechnologyRules.Effects(n);
                foreach(var line in n.Lines)if(line.Active) {
                    available-=line.Factories;if(available<0)continue;var design=FindDesign(owner,line.DesignId).Blueprint;
                    double work=Math.Min(7*ProductionPotential(n,line,design,tech),Math.Min(line.Quantity-line.Completed-line.Progress,budget/design.Cost));budget-=work*design.Cost;
                    needs[4]+=work*design.Steel*(1+tech.EquipmentSteel);needs[5]+=work*design.Machines;needs[7]+=work*design.Arms;
                }
                budgets[owner]=budget;
            }
            foreach(var a in S.Armies)if(a.TrainingDays==0) {
                var n=S.Nations[a.Owner];
                if(a.ExerciseRemaining>0 && n.Treasury>=ExerciseDailyCost(a)) {
                    double days=Math.Min(7,a.ExerciseRemaining);
                    n.Trade.Operations[2]+=days*ReadinessRules.ExerciseFuel(a);
                    n.Trade.Operations[7]+=days*ReadinessRules.ExerciseArms(a);
                }
                double fuel=a.Target<0?.003*a.Battalions:a.Design.Fuel*a.Battalions+a.Attachments.Sum(t=>t.Blueprint.Fuel*.3);
                n.Trade.Operations[2]+=7*fuel*(1+TechnologyRules.Effects(n).GroundFuel)*(1+DoctrineRules.Effects(n,S.Day).Fuel);
                if(a.Target<0)n.Trade.Operations[7]+=Math.Min(100-a.Strength,7*.4)*.005*a.Battalions;
            }
            foreach(var a in S.AirWings)if(a.TrainingDays==0&&a.Mission!=AirMission.Reserve) {
                var needs=S.Nations[a.Owner].Trade.Operations;needs[2]+=7*AirFuel(a);
                if(a.Mission==AirMission.Supply){needs[0]+=.14;needs[7]+=.105;}
            }
            foreach(var p in S.Provinces)S.Nations[p.Owner].Trade.Operations[2]+=(p.Buses+p.Trucks)*.00002*7;
            for(int buyer=0;buyer<S.Nations.Length;buyer++)foreach(var c in S.Nations[buyer].Contracts)if(c.Active&&S.Provinces[c.Company/8].Owner==c.Supplier&&ContractStatus(buyer,c,true)=="В производстве") {
                var design=c.Product==10?FindDesign(buyer,c.DesignId).Blueprint:null;
                double work=Math.Min(c.Quantity-c.Delivered-c.Progress,7*c.Factories*ProcurementRules.CapacityUnit*ContractEfficiency(c)/ProcurementRules.Work(c.Product,design));
                double price=ProcurementRules.Price(c.Product,design);work=Math.Min(work,budgets[buyer]/price);budgets[buyer]-=work*price;
                for(int good=0;good<8;good++)S.Nations[c.Supplier].Trade.Operations[good]+=work*ProcurementRules.Input(c.Product,good,design);
            }
        }
        private void UpdateMarketPrices()
        {
            for(int k=0;k<8;k++) {
                double demand=0,output=0,stock=0;for(int c=0;c<S.Nations.Length;c++){demand+=TradeNeed(c,k);output+=S.Nations[c].Production[k];stock+=S.Nations[c].Stock[k];}
                double target=Rules.Prices[k]*Rules.Clamp((demand+.3)/(output+stock*.09+.3),.45,3.5);
                S.MarketPrices[k]=S.MarketPrices[k]*.85+target*.15;
            }
        }
        private double TransferGoods(int buyer,int seller,int good,double wanted,double price,Dictionary<long,TradeFlow> flows)
        {
            if(TradeBlock(buyer,seller)!=null)return 0;
            var a=S.Nations[buyer];var b=S.Nations[seller];double tariff=ImportTariff(buyer,seller),freight=FreightRate(buyer,seller);
            double quantity=Math.Min(wanted,Math.Min(ExportSurplus(seller,good),Math.Max(0,a.PrivateCapital)/(price*(1+tariff+freight))));
            if(quantity<=1e-9)return 0;
            double value=quantity*price,tax=value*tariff,shipping=value*freight;
            a.Stock[good]+=quantity;b.Stock[good]-=quantity;a.PrivateCapital=Math.Max(0,a.PrivateCapital-value-tax-shipping);b.PrivateCapital+=value;a.Treasury+=tax;
            a.Imports+=value+tax+shipping;b.Exports+=value;a.Trade.Tariffs+=tax;a.Trade.Freight+=shipping;
            long key=((long)buyer*S.Nations.Length+seller)*8+good;TradeFlow flow;
            if(!flows.TryGetValue(key,out flow)){flow=new TradeFlow {Buyer=buyer,Seller=seller,Good=good};flows.Add(key,flow);S.TradeFlows.Add(flow);}
            flow.Quantity+=quantity;flow.Value+=value;flow.Tariff+=tax;flow.Freight+=shipping;return quantity;
        }
        internal void SettleTradeWeek(bool updatePrices=true,bool planAI=true)
        {
            if(S.TradeDay==S.Day)return;S.TradeDay=S.Day;
            foreach(var n in S.Nations){n.Imports=n.Exports=n.Trade.Tariffs=n.Trade.Freight=0;}
            S.TradeFlows.Clear();S.TradeDeals.RemoveAll(d=>d.EndDay<S.Day);
            if(updatePrices)UpdateMarketPrices();if(planAI)PlanTradeAgreements();
            var flows=new Dictionary<long,TradeFlow>();
            // Rotate claim priority; a single seller cannot sell the same stock twice.
            int start=S.TradeDeals.Count==0?0:S.Day/7%S.TradeDeals.Count;
            for(int i=0;i<S.TradeDeals.Count;i++) {
                var d=S.TradeDeals[(start+i)%S.TradeDeals.Count];if(d.StartedDay>=S.Day||d.LastDay==S.Day)continue;
                d.LastDay=S.Day;d.LastDelivery=TransferGoods(d.Buyer,d.Seller,d.Good,d.WeeklyQuantity,d.UnitPrice,flows);d.Delivered+=d.LastDelivery;d.Paid+=d.LastDelivery*d.UnitPrice;
            }
            var orders=TradeSupplierOrders();
            for(int k=0;k<8;k++) {
                // Spot purchases stop below the protected export reserve, so they
                // cannot create a new exporter within this pass. Track only depletion.
                var remaining=new double[S.Nations.Length];
                for(int seller=0;seller<remaining.Length;seller++)remaining[seller]=ExportSurplus(seller,k);
                for(int i=0;i<S.Nations.Length;i++) {
                    int buyer=(i+S.Day)%S.Nations.Length;var n=S.Nations[buyer];if(!n.AutoTrade)continue;
                    double wanted=Math.Max(0,ImportTarget(buyer,k)-n.Stock[k]);if(wanted<=0)continue;
                    foreach(int seller in orders[buyer]) {
                        if(remaining[seller]<=1e-9)continue;
                        double delivered=TransferGoods(buyer,seller,k,wanted,S.MarketPrices[k],flows);
                        wanted-=delivered;remaining[seller]-=delivered;if(wanted<.001)break;
                    }
                }
            }
            S.TradeDeals.RemoveAll(d=>d.EndDay<=S.Day);
        }
        private double SupplierScore(int buyer,int seller,int good) => S.MarketPrices[good]*SupplierWeight(buyer,seller);
        private double SupplierWeight(int buyer,int seller)
        {
            if(TradeBlock(buyer,seller)!=null)return double.MaxValue;
            double relation=S.Relations[Pair(buyer,seller)];
            return (1+ImportTariff(buyer,seller)+FreightRate(buyer,seller))*(1+Math.Max(0,-relation)*.003-(Allied(buyer,seller)?.08:0));
        }
        private double[][] tradeSupplierWeights;
        private int[][] tradeSupplierOrders;
        private int[][] TradeSupplierOrders()
        {
            // All goods share the same diplomatic/freight multiplier. World prices
            // therefore do not change supplier rank. Compare exact current weights
            // (including every restriction) before sorting; no stale hash or save cache.
            int count=S.Nations.Length;
            if(tradeSupplierOrders==null||tradeSupplierOrders.Length!=count) {
                tradeSupplierOrders=new int[count][];
                tradeSupplierWeights=new double[count][];
            }
            for(int buyer=0;buyer<count;buyer++) {
                bool changed=tradeSupplierOrders[buyer]==null;
                var weights=tradeSupplierWeights[buyer]??(tradeSupplierWeights[buyer]=new double[count]);
                for(int seller=0;seller<count;seller++) {
                    double weight=SupplierWeight(buyer,seller);
                    if(weights[seller]!=weight){weights[seller]=weight;changed=true;}
                }
                if(!changed)continue;
                var allowed=new List<int>(count);
                for(int seller=0;seller<count;seller++)if(weights[seller]!=double.MaxValue)allowed.Add(seller);
                var order=allowed.ToArray();
                Array.Sort(order,(a,b)=>{int comparison=weights[a].CompareTo(weights[b]);return comparison!=0?comparison:a.CompareTo(b);});
                tradeSupplierOrders[buyer]=order;
            }
            return tradeSupplierOrders;
        }
        internal void PlanTradeAgreements()
        {
            // Reassess blocked commitments before looking for replacement partners.
            foreach(var d in S.TradeDeals.Where(d=>d.Buyer!=S.Player&&TradeBlock(d.Buyer,d.Seller)!=null).ToArray())CancelTrade(d.Id,d.Buyer);
            var committed=new double[S.Nations.Length,8];var claimed=new bool[S.Nations.Length,8];
            foreach(var d in S.TradeDeals){committed[d.Seller,d.Good]+=d.WeeklyQuantity;claimed[d.Buyer,d.Good]=true;}
            var available=new double[S.Nations.Length,8];for(int c=0;c<S.Nations.Length;c++)for(int k=0;k<8;k++)available[c,k]=Math.Max(0,ExportSurplus(c,k)-committed[c,k]);
            // One main supplier per good, with the open market covering the remainder.
            for(int buyer=0;buyer<S.Nations.Length;buyer++) {
                var n=S.Nations[buyer];if(buyer==S.Player||!n.AutoTrade||n.PrivateCapital<=0)continue;
                for(int k=0;k<8;k++) {
                    if(claimed[buyer,k])continue;
                    double gap=TradeNeed(buyer,k)-n.Production[k];if(gap<=.01||n.Stock[k]>=ImportTarget(buyer,k))continue;
                    int best=-1;double score=double.MaxValue,amount=0;int offer=100;
                    for(int seller=0;seller<S.Nations.Length;seller++) {
                        if(seller==S.Player||TradeBlock(buyer,seller)!=null||S.Relations[Pair(buyer,seller)]<0)continue;
                        int percent=MinimumTradeOffer(buyer,seller);double q=Math.Min(gap*.5,available[seller,k]);
                        q=Math.Min(q,n.PrivateCapital*.12/(S.MarketPrices[k]*percent/100*(1+ImportTariff(buyer,seller)+FreightRate(buyer,seller))));
                        if(q<.01)continue;double candidate=SupplierScore(buyer,seller,k)*percent/100;
                        if(candidate<score){score=candidate;best=seller;amount=q;offer=percent;}
                    }
                    if(best>=0&&SignTrade(best,k,amount,4,offer,buyer)==null){available[best,k]-=amount;claimed[buyer,k]=true;}
                }
            }
        }
        private void ValidateTrade(Campaign s)
        {
            int count=s.Nations.Length;
            if(!Values(s.MarketPrices,8)||s.MarketPrices.Any(v=>v<=0)||s.TradeDeals==null||s.TradeDeals.Count>count*8||s.TradeFlows==null||s.TradeFlows.Count>count*count*8||s.NextTradeDeal<1||s.TradeDay< -1||s.TradeDay>s.Day)throw new InvalidDataException("Повреждён международный рынок.");
            for(int k=0;k<8;k++)if(s.MarketPrices[k]<Rules.Prices[k]*.45-1e-10||s.MarketPrices[k]>Rules.Prices[k]*3.5+1e-10)throw new InvalidDataException("Неверная мировая цена.");
            foreach(var n in s.Nations)if(n.Trade==null||n.Trade.Tariff<0||n.Trade.Tariff>30||n.Trade.ReserveWeeks<1||n.Trade.ReserveWeeks>12||!Values(n.Trade.Operations,8)||!IsFinite(n.Trade.Tariffs)||n.Trade.Tariffs<0||!IsFinite(n.Trade.Freight)||n.Trade.Freight<0)throw new InvalidDataException("Повреждена торговая политика.");
            var ids=new HashSet<int>();var purchases=new HashSet<int>();
            foreach(var d in s.TradeDeals) {
                if(d==null||d.Id<1||d.Id>=s.NextTradeDeal||!ids.Add(d.Id)||d.Buyer<0||d.Buyer>=count||d.Seller<0||d.Seller>=count||d.Buyer==d.Seller||d.Good<0||d.Good>=8||!purchases.Add(d.Buyer*8+d.Good)
                    ||d.StartedDay<0||d.StartedDay>s.Day||d.EndDay<=d.StartedDay||d.EndDay>d.StartedDay+TradeRules.MaxWeeks*7||(d.EndDay-d.StartedDay)%7!=0||d.LastDay< -1||d.LastDay>s.Day||(d.LastDay>=0&&d.LastDay<=d.StartedDay)
                    ||!new[]{d.WeeklyQuantity,d.UnitPrice,d.Delivered,d.Paid,d.LastDelivery}.All(v=>IsFinite(v)&&v>=0)||d.WeeklyQuantity<=0||d.WeeklyQuantity>1000000||d.UnitPrice<=0||d.UnitPrice>Rules.Prices[d.Good]*6||d.LastDelivery>d.WeeklyQuantity+1e-8||d.Delivered<d.LastDelivery||d.Delivered>d.WeeklyQuantity*(d.EndDay-d.StartedDay)/7+1e-6||Math.Abs(d.Paid-d.Delivered*d.UnitPrice)>1e-6||d.LastDay==-1&&(d.Delivered!=0||d.Paid!=0||d.LastDelivery!=0))throw new InvalidDataException("Повреждён договор поставок.");
            }
            var pairs=new HashSet<long>();foreach(var f in s.TradeFlows)if(f==null||f.Buyer<0||f.Buyer>=count||f.Seller<0||f.Seller>=count||f.Buyer==f.Seller||f.Good<0||f.Good>=8||!pairs.Add(((long)f.Buyer*count+f.Seller)*8+f.Good)||!new[]{f.Quantity,f.Value,f.Tariff,f.Freight}.All(v=>IsFinite(v)&&v>=0)||f.Quantity<=0)throw new InvalidDataException("Повреждён журнал поставок.");
        }
    }
}
