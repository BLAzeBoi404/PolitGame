using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private int regionInspectorTab, dismissedRegion = -1;
        private bool RegionInspectorVisible => menuScreen == "" && page == "Карта" && selected >= 0 && dismissedRegion != selected;
        private RectangleF RegionInspectorRect => new RectangleF(12,115,326,VH-161);
        private void RegionMetric(Graphics g, string label, string value, float x, float y, string tip, Color? color = null)
        {
            Round(g, Palette.Bg, new RectangleF(x,y,144,59), 3);
            Txt(g,label,x+9,y+8,9,Palette.Muted,true,130);
            Txt(g,value,x+9,y+28,17,color??Palette.Text,true,130);
            hits.Add(new Hit { Rect = new RectangleF(x,y,144,59), Label = "region:metric:"+label, Tip = tip });
        }
        private void DrawRegionInspector(Graphics g)
        {
            if (!RegionInspectorVisible) return;
            var box = RegionInspectorRect; float x = box.X+14, y = box.Y, w = box.Width-28;
            var p = E.S.Provinces[selected]; var nation = E.S.Nations[p.Owner]; int region = selected;
            DocketFrame(g,box,"");
            Txt(g,"РЕГИОН / "+W.Countries[p.Owner].Code,x,y+13,11,Palette.AccentText,true);
            Button(g,"×",box.Right-39,y+7,28,()=>{dismissedRegion=selected;keyboardFocusKey=null;InvalidateHud();},false,"Скрыть карточку до следующего выбора области",28);
            hits[hits.Count-1].Label="region:close";
            Para(g,W.Regions[region].Name,x,y+56,w,51,20,Palette.Text);
            hits.Add(new Hit {Rect=new RectangleF(x,y+53,w,53),Label="region:name",Tip=W.Regions[region].Name});
            CountryFlag(g,p.Owner,new RectangleF(x,y+115,40,30));
            Txt(g,W.Countries[p.Owner].Name,x+51,y+116,15,Palette.Text,true,w-53);
            Txt(g,p.Owner==E.S.Player?"Ваша территория":"Контролирующее государство",x+51,y+138,10,Palette.Muted,false,w-53);
            string[] tabs={"Обзор","Мощности","Ресурсы"};
            for(int i=0;i<tabs.Length;i++) {int tab=i;Button(g,tabs[i],x+i*101,y+167,96,()=>{regionInspectorTab=tab;InvalidateHud();},regionInspectorTab==i,"Данные выбранной области",30);hits[hits.Count-1].Label="region:tab:"+i;}
            float top=y+216;
            if(regionInspectorTab==0) {
                RegionMetric(g,"НАСЕЛЕНИЕ",People((long)Math.Round(p.Population*1000000)),x,top,"Текущее население этой области в кампании.");
                RegionMetric(g,"ЧЕЛОВЕК / КМ²",(p.Population*1000000/RegionAreaKm2(region)).ToString("0.0"),x+154,top,"Население / площадь по сферической геометрии карты; площадь "+RegionAreaKm2(region).ToString("N0")+" км².");
                RegionMetric(g,"СПОКОЙСТВИЕ",(100-p.Unrest).ToString("0")+"%",x,top+68,"Настроения населения выбранной области.",Palette.Teal);
                RegionMetric(g,"ДОРОЖНАЯ СЕТЬ",p.Infrastructure.ToString("0")+"%",x+154,top+68,"Базовая инфраструктура. Автобусы и грузовики дополнительно влияют на обслуживание транспорта.");
                Section(g,"ВЛАДЕНИЕ МОЩНОСТЯМИ",x,top+155,w);
                double state=p.Public.Sum(), business=p.Private.Sum(), total=state+business;
                var ring=new RectangleF(x+5,top+198,94,94);
                using(var pen=new Pen(Palette.Line,13))g.DrawEllipse(pen,ring);
                if(total>0)using(var pen=new Pen(Palette.Accent,13))g.DrawArc(pen,ring,-90,(float)(state/total*360));
                Txt(g,total.ToString("0.0"),x+15,top+231,19,Palette.Text,true,76);
                Txt(g,"ГОСУДАРСТВО",x+120,top+203,10,Palette.AccentText,true);
                Txt(g,state.ToString("0.00"),x+120,top+223,19,Palette.Text,true);
                Txt(g,"ЧАСТНЫЙ СЕКТОР",x+120,top+255,10,Palette.Muted,true);
                Txt(g,business.ToString("0.00"),x+120,top+275,19,Palette.Text,true);
                hits.Add(new Hit {Rect=new RectangleF(x,top+192,w,111),Label="region:ownership",Tip="Доля государственных и частных производственных мощностей области. Это условные мощности восьми отраслей, не число юридических лиц."});
                Section(g,"ПОКАЗАТЕЛИ СТРАНЫ",x,top+328,w);
                Txt(g,"Образование",x,top+367,12,Palette.Muted);Txt(g,nation.Education.ToString("0")+" / 100",x+208,top+367,13,Palette.Text,true);
                Txt(g,"Безработица",x,top+394,12,Palette.Muted);Txt(g,nation.Unemployment.ToString("0.0")+"%",x+208,top+394,13,Palette.Text,true);
            } else if(regionInspectorTab==1) {
                Txt(g,"ОТРАСЛЬ",x,top,10,Palette.Muted,true);Txt(g,"ГОС.",x+198,top,10,Palette.AccentText,true);Txt(g,"ЧАСТ.",x+249,top,10,Palette.Muted,true);
                for(int i=0;i<8;i++) {
                    int sector=i;float row=top+32+i*40;
                    if(i%2==0)Fill(g,Palette.Card,x-3,row-5,w+6,39);
                    Txt(g,Rules.Goods[i],x+3,row+4,12,Palette.Text,false,185);
                    Txt(g,p.Public[i].ToString("0.0"),x+198,row+4,12,Palette.AccentText,true,49);
                    Txt(g,p.Private[i].ToString("0.0"),x+249,row+4,12,Palette.Text,true,49);
                    hits.Add(new Hit {Rect=new RectangleF(x,row-5,w,39),Label="region:industry:"+i,FocusTarget=true,Tip=Rules.Plants[i]+" · открыть управление выбранной областью",Action=()=>{selected=region;if(p.Owner==E.S.Player){marketGood=sector;equipmentProduction=false;Navigate("Производство");detailPanel=true;}else{foreign=p.Owner;Navigate("Дипломатия");}Invalidate();}});
                }
                Para(g,"Мощности производят товары через действующие цепочки. Выпуск зависит от работников, технологий, ресурсов и снабжения страны.",x,top+374,w,73,12,Palette.Muted);
            } else {
                string[] labels={"Плодородие","Нефть","Руда"};double[] amounts={p.Soil,p.Oil,p.Ore};
                for(int i=0;i<3;i++){float row=top+i*70;Txt(g,labels[i],x,row,14,Palette.Text,true);Txt(g,"×"+amounts[i].ToString("0.00"),x+225,row,15,Palette.Gold,true);Progress(g,x,row+32,w,amounts[i]/2,Palette.Accent);}
                Para(g,"Коэффициенты ресурсной базы сценария, а не тонны разведанных запасов. Они изменяют выпуск агрокомплексов, переработки и рудников.",x,top+220,w,86,12,Palette.Muted);
                Section(g,"ТРАНСПОРТ И БАЗЫ",x,top+328,w);
                Txt(g,"Автобусы / грузовики",x,top+367,12,Palette.Muted);Txt(g,p.Buses+" / "+p.Trucks,x+208,top+367,13,Palette.Text,true,90);
                Txt(g,"Аэродромы",x,top+394,12,Palette.Muted);Txt(g,p.Airfield.ToString(),x+208,top+394,13,Palette.Text,true);
            }
            float bottom=box.Bottom-77;
            Button(g,p.Owner==E.S.Player?"Развитие и строительство →":"Дипломатическое досье →",x,bottom,w,()=>{selected=region;foreign=p.Owner;equipmentProduction=false;Navigate(p.Owner==E.S.Player?"Производство":"Дипломатия");detailPanel=true;Invalidate();},true,"Перейти к действиям для этой территории",31);hits[hits.Count-1].Label="region:manage";
            Button(g,"Плотность населения на карте",x,bottom+39,w,()=>{layer="Население";Invalidate();},false,"Цвет показывает население на квадратный километр",28);hits[hits.Count-1].Label="region:population-layer";
        }
    }
}
