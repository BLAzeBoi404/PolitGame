using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool tradePage;
        private int tradeTab,tradeSeller=-1,tradeOffset,tradeWeeks=4,tradePercent=100;
        private double tradeQuantity=1;
        private void OpenTrade(){Navigate("Экономика");tradePage=detailPanel=true;tradeOffset=0;E.PrepareTradeNeeds();Invalidate();}
        private void TradeLink(Graphics g,float x,float y,float w,int partner=-1)
        {
            Button(g,"Внешняя торговля →",x,y,w,()=>{OpenTrade();if(partner>=0)tradeSeller=partner;},false,"Потребности армии и промышленности, пошлины, поставщики и договоры",30);hits[hits.Count-1].Label="trade:open";
        }
        private void TradeButton(Graphics g,string label,string text,float x,float y,float w,Action action,bool chosen=false,string tip="")
        {
            Button(g,text,x,y,w,action,chosen,tip,30);hits[hits.Count-1].Label=label;
        }
        private void DrawTrade(Graphics g)
        {
            int me=E.S.Player;var n=E.Player;float x=sidebar+28,w=VW-sidebar-56;
            PageTitle(g,"Внешняя торговля","Запасы и поставки связывают гражданскую экономику, сборку техники и снабжение армии.");
            TradeButton(g,"trade:market","Рынок и поставщики",x,222,205,()=>{tradeTab=0;Invalidate();},tradeTab==0);
            int count=E.S.TradeDeals.Count(d=>d.Buyer==me||d.Seller==me);
            TradeButton(g,"trade:deals","Договоры · "+count,x+216,222,180,()=>{tradeTab=1;Invalidate();},tradeTab==1);
            TradeButton(g,"trade:back","← Экономика",x+w-180,222,180,()=>Navigate("Экономика"));
            Fill(g,Palette.Card,x,270,w,49);
            TradeButton(g,"trade:tariff","Пошлина: "+n.Trade.Tariff+"%  ›",x+10,280,195,()=>Message(E.SetTradePolicy((n.Trade.Tariff+5)%35,n.Trade.ReserveWeeks)),false,"Сбор поступает в казну. Торговый договор снижает ставку на 75%.");
            TradeButton(g,"trade:reserve","Резерв: "+n.Trade.ReserveWeeks+" нед.  ›",x+217,280,195,()=>Message(E.SetTradePolicy(n.Trade.Tariff,n.Trade.ReserveWeeks%12+1)),false,"Цель импорта; экспорт оставляет ещё одну неделю запаса. Учитываются ежедневные системы.");
            Txt(g,"Оборотный капитал  "+Money(n.PrivateCapital),x+430,283,13,Palette.Text,true,w-440);
            if(tradeTab==1){DrawTradeDeals(g,x,w);return;}
            float left=244,middle=(w-left-36)*.49f,right=w-left-middle-36,mx=x+left+18,rx=mx+middle+18;
            Section(g,"ТОВАР / ЗАПАС НА НЕДЕЛИ",x,341,left);
            for(int k=0;k<8;k++) {
                int good=k;float y=378+k*44;double need=E.TradeNeed(me,k);
                if(k==marketGood)Fill(g,Palette.Selected,x,y-4,left,42);
                Txt(g,Rules.Goods[k],x+9,y,13,Palette.Text,true,left-20);
                Txt(g,Num(n.Stock[k])+" ед.  /  "+(need>1e-8?(n.Stock[k]/need).ToString("0.0"):"—")+" нед.",x+9,y+20,11,n.Stock[k]<need?Palette.Red:Palette.Muted);
                hits.Add(new Hit {Rect=new RectangleF(x,y-4,left,42),Label="trade:good:"+good,Tip="В неделю: гражданские потребности "+Num(n.Demand[k])+", производство техники и операции "+Num(n.Trade.Operations[k]),Action=()=>{marketGood=good;tradeOffset=0;tradeSeller=-1;Invalidate();}});
            }
            var flows=E.S.TradeFlows.Where(f=>f.Buyer==me&&f.Good==marketGood).OrderByDescending(f=>f.Quantity).ToArray();double imported=flows.Sum(f=>f.Quantity);
            Section(g,"ПОСЛЕДНИЕ ПОСТАВКИ",x,755,left);
            Para(g,imported<=0?"Нет импорта за последний расчёт.":"Ввезено "+Num(imported)+" ед.\n"+W.Countries[flows[0].Seller].Name+": "+(flows[0].Quantity/imported*100).ToString("0")+"% импорта товара",x,784,left,68,12,Palette.Muted);
            var suppliers=Enumerable.Range(0,E.S.Nations.Length).Where(c=>c!=me).OrderBy(c=>E.TradeBlock(me,c)==null?0:1).ThenByDescending(c=>E.AvailableTradeOffer(c,marketGood)>0).ThenBy(c=>E.LandedPrice(me,c,marketGood)).ThenBy(c=>c).ToArray();
            if(tradeSeller<0||tradeSeller==me)tradeSeller=suppliers[0];tradeOffset=Math.Max(0,Math.Min(tradeOffset,Math.Max(0,suppliers.Length-6)));
            Section(g,"ПОСТАВЩИКИ / ЦЕНА С ДОСТАВКОЙ",mx,341,middle);
            for(int i=tradeOffset;i<Math.Min(suppliers.Length,tradeOffset+6);i++) {
                int seller=suppliers[i];float y=376+(i-tradeOffset)*65;string block=E.TradeBlock(me,seller);
                TradeButton(g,"trade:supplier:"+seller,W.Countries[seller].Name,mx,y,middle,()=>{tradeSeller=seller;Invalidate();},tradeSeller==seller,block??"Свободно после внутреннего резерва и обязательств: "+Num(E.AvailableTradeOffer(seller,marketGood))+" ед.");
                Txt(g,block??(Money(E.LandedPrice(me,seller,marketGood))+" / ед. · свободно "+Num(E.AvailableTradeOffer(seller,marketGood))),mx+7,y+35,11,block!=null?Palette.Red:Palette.Muted,false,middle-14);
            }
            TradeButton(g,"trade:prev","←",mx,776,45,()=>{tradeOffset=Math.Max(0,tradeOffset-6);Invalidate();});
            TradeButton(g,"trade:next","→",mx+53,776,45,()=>{tradeOffset=Math.Min(suppliers.Length-6,tradeOffset+6);Invalidate();});
            Txt(g,(tradeOffset+1)+"–"+Math.Min(suppliers.Length,tradeOffset+6)+" / "+suppliers.Length,mx+110,784,11,Palette.Muted);
            Section(g,"СОГЛАШЕНИЕ О ПОСТАВКАХ",rx,341,right);
            Book(g,W.Countries[tradeSeller].Name,rx,375,21,Palette.Text,true,right);
            Txt(g,Rules.Goods[marketGood]+" · мировой ориентир "+Money(E.S.MarketPrices[marketGood]),rx,409,12,Palette.Gold,false,right);
            TradeButton(g,"trade:quantity-down","−",rx,444,34,()=>{tradeQuantity=Math.Max(.01,tradeQuantity/2);Invalidate();});
            Txt(g,tradeQuantity.ToString("0.00")+" ед./нед.",rx+43,451,14,Palette.Text,true,right-90);
            TradeButton(g,"trade:quantity-up","+",rx+right-34,444,34,()=>{tradeQuantity=Math.Min(1000000,tradeQuantity*2);Invalidate();});
            TradeButton(g,"trade:need","По текущей потребности",rx,485,right,()=>{tradeQuantity=Math.Max(.01,Math.Min(E.AvailableTradeOffer(tradeSeller,marketGood),Math.Max(.01,E.TradeNeed(me,marketGood)-n.Production[marketGood])));Invalidate();});
            TradeButton(g,"trade:weeks","Срок: "+tradeWeeks+" нед.  ›",rx,528,right,()=>{tradeWeeks=tradeWeeks==4?12:tradeWeeks==12?26:4;Invalidate();});
            TradeButton(g,"trade:price-down","−",rx,571,34,()=>{tradePercent=Math.Max(80,tradePercent-5);Invalidate();});
            Txt(g,tradePercent+"% мировой цены",rx+43,578,13,Palette.Text,true,right-82);
            TradeButton(g,"trade:price-up","+",rx+right-34,571,34,()=>{tradePercent=Math.Min(150,tradePercent+5);Invalidate();});
            double baseCost=tradeQuantity*E.S.MarketPrices[marketGood]*tradePercent/100,tax=E.ImportTariff(me,tradeSeller),freight=E.FreightRate(me,tradeSeller);
            Txt(g,"За неделю: "+Money(baseCost*(1+tax+freight)),rx,621,17,Palette.Gold,true,right);
            Txt(g,"Товар "+Money(baseCost)+" · сбор "+(tax*100).ToString("0.#")+"% · доставка "+(freight*100).ToString("0")+"%",rx,649,11,Palette.Muted,false,right);
            string reason=E.TradeOfferReason(me,tradeSeller,marketGood,tradeQuantity,tradeWeeks,tradePercent);
            Para(g,reason??"Партнёр согласен. Фиксируется цена товара; пошлина и доставка зависят от действующих отношений.",rx,682,right,73,12,reason!=null?Palette.Red:Palette.Teal);
            AvailableButton(g,"Заключить договор",rx,766,right,()=>Message(E.SignTrade(tradeSeller,marketGood,tradeQuantity,tradeWeeks,tradePercent)),reason==null,reason??"Оплата капиталом бизнеса за фактическую доставку; предоплаты нет.",true,36);hits[hits.Count-1].Label="trade:sign";
            Para(g,"Оплата из капитала бизнеса. Основной договор исполняется до свободного рынка. Недостаток товара или денег уменьшает поставку.",rx,814,right,52,11,Palette.Muted);
        }
        private void DrawTradeDeals(Graphics g,float x,float w)
        {
            var deals=E.S.TradeDeals.Where(d=>d.Buyer==E.S.Player||d.Seller==E.S.Player).ToArray();
            Section(g,"ПАРТНЁР / ТОВАР",x,346,w);
            if(deals.Length==0)Para(g,"Основных договоров пока нет. На вкладке рынка выберите товар, поставщика и условия. Свободная автоторговля продолжает работать независимо.",x,395,w-20,80,17,Palette.Muted);
            for(int i=0;i<deals.Length;i++) {
                var d=deals[i];float y=382+i*55;
                if(i%2==0)Fill(g,Palette.Card,x,y-5,w,51);
                Txt(g,W.Countries[d.Seller].Name+" → "+Rules.Goods[d.Good],x+9,y,13,Palette.Text,true,w*.35f);
                Txt(g,"Последняя неделя "+Num(d.LastDelivery)+" / "+Num(d.WeeklyQuantity)+" · ещё "+Math.Max(0,d.EndDay-E.S.Day)+" дн.",x+9,y+23,11,Palette.Muted,false,w*.42f);
                Txt(g,E.TradeDealStatus(d),x+w*.44f,y+3,12,Palette.Gold,false,w*.39f);
                Txt(g,"Всего "+Num(d.Delivered)+" ед. · товар оплачен "+Money(d.Paid),x+w*.44f,y+23,10,Palette.Muted,false,w*.39f);
                TradeButton(g,"trade:cancel:"+d.Id,"Прекратить",x+w-105,y+3,105,()=>{if(Confirm("Прекратить поставки из "+W.Countries[d.Seller].Name+"? Оплаченные доставки не возвращаются."))Message(E.CancelTrade(d.Id));});
            }
            Txt(g,"Пошлины за расчёт: "+Money(E.Player.Trade.Tariffs)+" в казну · доставка: "+Money(E.Player.Trade.Freight),x,849,12,Palette.Muted,false,w);
        }
    }
}
