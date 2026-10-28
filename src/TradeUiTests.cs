using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestTradeInterface(string directory,Action<bool,string> check,Action<string> click)
        {
            E.New(W.ByCode("UKR"),2026,1);speed=0;showHelp=false;menuScreen="";int seller=W.ByCode("POL");
            E.S.Nations[seller].AutoTrade=true;E.S.Nations[seller].Stock[2]=10000;E.S.Relations[E.Pair(E.S.Player,seller)]=E.S.Relations[E.Pair(seller,E.S.Player)]=60;
            E.Player.PrivateCapital=100;marketGood=2;tradeSeller=seller;tradeQuantity=1;tradePercent=100;tradeWeeks=4;tradeTab=0;
            Navigate("Экономика");click("trade:open");check(tradePage&&detailPanel,"Economy opens the working international trade desk");
            int rate=E.Player.Trade.Tariff;click("trade:tariff");check(E.Player.Trade.Tariff==(rate+5)%35,"Trade desk changes the real saved customs policy");
            int weeks=E.Player.Trade.ReserveWeeks;click("trade:reserve");check(E.Player.Trade.ReserveWeeks==weeks%12+1,"Trade desk changes the actual import reserve target");
            click("trade:sign");check(E.S.TradeDeals.Any(d=>d.Buyer==E.S.Player&&d.Seller==seller&&d.Good==2),"A mouse click signs a real supply agreement through the same validated engine API");
            click("trade:deals");using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"trade-deals.png"));
            int id=E.S.TradeDeals.Single().Id;confirmationForTests=_=>false;click("trade:cancel:"+id);check(E.S.TradeDeals.Count==1,"Declining cancellation retains the trade commitment");
            confirmationForTests=_=>true;click("trade:cancel:"+id);confirmationForTests=null;check(E.S.TradeDeals.Count==0,"Confirmed cancellation removes the agreement without a refund exploit");
            click("trade:market");E.S.Embargo[E.Pair(seller,E.S.Player)]=true;click("trade:sign");check(E.S.TradeDeals.Count==0,"The disabled signing control cannot bypass a supplier embargo");E.S.Embargo[E.Pair(seller,E.S.Player)]=false;
            foreach(var size in new[]{new Size(1280,720),new Size(1600,900),new Size(1920,1080)}) {
                ClientSize=size;OpenTrade();tradeSeller=seller;tradeTab=0;toastUntil=DateTime.MinValue;
                using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"trade-market-"+size.Width+".png"));
                var targets=hits.Where(h=>h.Label!=null&&h.Label.StartsWith("trade:")).ToArray();
                check(targets.Length>20&&targets.All(h=>h.Rect.Left>=0&&h.Rect.Right<=VW+1&&h.Rect.Top>=0&&h.Rect.Bottom<=VH+1),"Trade controls remain reachable at "+size.Width+"x"+size.Height);
                bool overlap=false;for(int a=0;a<targets.Length;a++)for(int b=a+1;b<targets.Length;b++){var r=RectangleF.Intersect(targets[a].Rect,targets[b].Rect);if(r.Width>1&&r.Height>1)overlap=true;}
                check(!overlap,"Trade controls do not overlap at "+size.Width+"x"+size.Height);
            }
            ClientSize=new Size(1600,900);Navigate("Дипломатия");detailPanel=true;foreign=seller;tradeSeller=-1;click("trade:open");check(tradePage&&tradeSeller==seller,"The diplomatic dossier opens trade with the selected country");
            Navigate("Карта");check(!tradePage,"Leaving the trade desk restores ordinary navigation");
        }
    }
}
