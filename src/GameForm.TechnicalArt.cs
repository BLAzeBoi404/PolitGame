using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace Sovereign
{
    public partial class GameForm
    {
        private bool blueprintExpanded;
        private void DrawTechnicalBoard(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, right = x + w - 260;
            PageTitle(g, "Технический альбом", draft.Name + " / " + EquipmentRules.Names[(int)draft.Kind]);
            Button(g, "← Вернуться к компоновке", x, 217, 280, () => { blueprintExpanded = false; Invalidate(); });
            DrawBlueprintSheet(g, new RectangleF(x, 275, w - 286, 547), draft, focusedModule);
            Section(g, "УЗЛЫ И АГРЕГАТЫ", right, 279, 260);
            int[] modules = EquipmentRules.Modules(draft);
            for (int i = 0; i < 8; i++) { int slot = i;
                Button(g, (i + 1).ToString("00") + "  " + EquipmentRules.ModuleNames[i], right, 319 + i * 52, 260, () => { focusedModule = slot; Invalidate(); }, focusedModule == i, "Выделить узел на проекциях", 32); hits[hits.Count - 1].Label = "blueprint:module:" + i;
                Txt(g, "Уровень " + modules[i] + " / " + EquipmentRules.Cap(draft, i, E.Player), right + 10, 354 + i * 52, 9, Palette.Muted);
            }
            Para(g, "Проекции условные. Геометрия отражает класс и выбранные модули; характеристики рассчитываются по проекту.", right, 761, 260, 62, 12);
        }
        private void TechnicalSilhouette(Graphics g, RectangleF box, Blueprint d, bool top)
        {
            var state = g.Save(); float logicalY = d.Air ? (top ? 0 : 60) : (top ? 40 : 10), logicalHeight = d.Air ? (top ? 230 : 115) : (top ? 150 : 210);
            float ratio = Math.Min(box.Width / 440, box.Height / logicalHeight);
            g.TranslateTransform(box.X + (box.Width - 440 * ratio) / 2, box.Y + (box.Height - logicalHeight * ratio) / 2 - logicalY * ratio); g.ScaleTransform(ratio, ratio);
            Color edge = Color.FromArgb(192, 204, 193), dark = Color.FromArgb(36, 49, 46), metal = Color.FromArgb(105, 123, 108);
            using (var pen = new Pen(edge, 1.2f)) using (var seam = new Pen(Color.FromArgb(47, 67, 59), 1.1f))
            using (var glint = new Pen(Palette.Gold, 2.5f)) using (var body = new LinearGradientBrush(new RectangleF(0,0,440,230), Color.FromArgb(132, 145, 121), dark, 90))
            using (var steel = new SolidBrush(metal)) using (var shade = new SolidBrush(dark)) using (var glass = new SolidBrush(Color.FromArgb(43, 84, 92))) {
                using (var guide = new Pen(Color.FromArgb(65, 188, 198, 175), .7f)) { guide.DashStyle = DashStyle.DashDot; g.DrawLine(guide, 10, 115, 430, 115); g.DrawLine(guide, 220, 12, 220, 213); }
                if (d.Air) {
                    bool heavy = d.Kind == EquipmentKind.Bomber || d.Kind == EquipmentKind.Transport, scout = d.Kind == EquipmentKind.Recon;
                    float spread = 125 + d.Mobility * 8, nose = 20 - d.Frame, tail = 198 + d.Frame;
                    if (top) {
                        float wingY = heavy ? 83 : scout ? 109 : 136;
                        var wing = new[] {new PointF(207,68),new PointF(220-spread,wingY),new PointF(220-spread+7,wingY+20),new PointF(204,133),new PointF(199,177),new PointF(163,194),new PointF(165,208),new PointF(220,194),new PointF(275,208),new PointF(277,194),new PointF(241,177),new PointF(236,133),new PointF(220+spread-7,wingY+20),new PointF(220+spread,wingY),new PointF(233,68)};
                        g.FillPolygon(body, wing); g.DrawPolygon(pen, wing);
                        g.DrawLine(seam, 220-spread+10, wingY+12, 203, 125); g.DrawLine(seam, 237,125,220+spread-10,wingY+12);
                        using (var fuselage = new GraphicsPath()) { fuselage.AddBezier(220,nose,195,39,198,162,213,tail); fuselage.AddBezier(213,tail,220,tail+8,240,166,232,54); fuselage.AddBezier(232,54,230,28,224,nose,220,nose); g.FillPath(body,fuselage);g.DrawPath(pen,fuselage); }
                        g.FillEllipse(glass,213,35,14,38);g.DrawEllipse(pen,213,35,14,38);
                        for (int i=0;i<(heavy?4:2);i++) {float xx=heavy?117+i*64:191+i*44;g.FillRectangle(shade,xx,89,14,37+d.Engine*2);g.DrawRectangle(focusedModule==2?glint:pen,xx,89,14,37+d.Engine*2);for(int j=0;j<4;j++)g.DrawLine(seam,xx+2,96+j*7,xx+12,96+j*7);}
                        for(int i=0;i<5;i++)g.DrawLine(seam,208,83+i*18,232,83+i*18);
                        if(d.Kind==EquipmentKind.Bomber)for(int i=0;i<4;i++){g.FillRectangle(shade,212,90+i*16,16,11);g.DrawRectangle(focusedModule==1?glint:pen,212,90+i*16,16,11);}
                        if(scout){g.FillEllipse(glass,209,135,22,22);g.DrawEllipse(focusedModule==3?glint:pen,209,135,22,22);}
                        if(d.Kind==EquipmentKind.Transport){g.DrawRectangle(glint,209,148,22,30);for(int i=0;i<4;i++)g.DrawLine(seam,211,151+i*7,229,151+i*7);}
                        if(focusedModule==5)g.DrawLine(glint,220-spread+10,wingY+12,203,125);
                        if(focusedModule==4)g.DrawLine(glint,236,132,241,177);
                    } else {
                        var fuselage=new[]{new PointF(38,130),new PointF(65,109),new PointF(328,100),new PointF(386,72),new PointF(400,73),new PointF(389,140),new PointF(83,150)};
                        g.FillPolygon(body,fuselage);g.DrawPolygon(pen,fuselage);g.FillPolygon(glass,new[]{new PointF(67,112),new PointF(103,110),new PointF(115,127),new PointF(55,130)});
                        g.FillPolygon(body,new[]{new PointF(179,128),new PointF(288,137),new PointF(315,159),new PointF(198,146)});g.DrawLines(pen,new[]{new PointF(179,128),new PointF(315,159),new PointF(198,146)});
                        for(int i=0;i<(heavy?10:3);i++)g.DrawRectangle(seam,125+i*16,113,8,7);
                        g.FillEllipse(shade,199,143,52+d.Engine*3,15);g.DrawEllipse(focusedModule==2?glint:pen,199,143,52+d.Engine*3,15);
                    }
                } else if (top) {
                    bool truck=d.Kind==EquipmentKind.Truck;float breadth=78+d.Armor*3, length=292+d.Frame*5, xx=(440-length)/2, yy=115-breadth/2;
                    if(!truck){g.FillRectangle(shade,xx,yy-13,length,17);g.FillRectangle(shade,xx,yy+breadth-4,length,17);for(int i=0;i<32;i++){float sx=xx+i*length/32;g.DrawLine(pen,sx,yy-12,sx,yy+3);g.DrawLine(pen,sx,yy+breadth-3,sx,yy+breadth+12);}}
                    g.FillRectangle(body,xx,yy,length,breadth);g.DrawRectangle(focusedModule==4?glint:pen,xx,yy,length,breadth);
                    for(int i=0;i<10;i++)g.DrawLine(seam,xx+12+i*5,yy+9,xx+12+i*5,yy+breadth-9);
                    g.DrawRectangle(focusedModule==2?glint:pen,xx+8,yy+6,62,breadth-12);
                    if(truck){g.FillRectangle(shade,xx+10,yy+8,length-100,breadth-16);for(int i=0;i<7;i++)g.DrawLine(pen,xx+16+i*28,yy+10,xx+16+i*28,yy+breadth-10);g.FillRectangle(glass,xx+length-58,yy+6,34,breadth-12);}
                    else {float turret=d.Kind==EquipmentKind.Artillery?xx+110:xx+170;float turretWidth = d.Kind == EquipmentKind.IFV ? 32 : 48;
                        var armour = new[] {new PointF(turret-turretWidth,yy+23),new PointF(turret-26,yy+5),new PointF(turret+22,yy+5),new PointF(turret+turretWidth,yy+25),new PointF(turret+turretWidth,yy+breadth-25),new PointF(turret+22,yy+breadth-5),new PointF(turret-26,yy+breadth-5),new PointF(turret-turretWidth,yy+breadth-23)};
                        g.FillPolygon(body,armour);g.DrawPolygon(pen,armour);
                        g.DrawLines(seam,new[] {new PointF(turret-25,yy+10),new PointF(turret+18,yy+10),new PointF(turret+turretWidth-6,yy+29)});
                        g.DrawLines(seam,new[] {new PointF(turret-25,yy+breadth-10),new PointF(turret+18,yy+breadth-10),new PointF(turret+turretWidth-6,yy+breadth-29)});
                        g.DrawEllipse(pen,turret-16,yy+breadth-37,21,21);
                        g.DrawLine(pen,turret-5,yy+breadth-35,turret-5,yy+breadth-19);
                        for(int panel=0;panel<7;panel++){float px=xx+76+panel*28;g.DrawRectangle(seam,px,yy+3,23,9);g.DrawRectangle(seam,px,yy+breadth-12,23,9);}
                        g.DrawPolygon(seam,new[]{new PointF(xx+length-57,yy+17),new PointF(xx+length-10,yy+6),new PointF(xx+length-10,yy+breadth-6),new PointF(xx+length-57,yy+breadth-17)});
                        for(int bolt=0;bolt<15;bolt++){float bx=xx+8+bolt*(length-16)/15;g.FillEllipse(shade,bx,yy+1,2,2);g.FillEllipse(shade,bx,yy+breadth-3,2,2);}
                        g.FillRectangle(steel,turret+25,108,75+d.Gun*8,10);g.DrawRectangle(focusedModule==1?glint:pen,turret+25,108,75+d.Gun*8,10);g.FillEllipse(shade,turret-23,yy+17,26,24);g.DrawEllipse(pen,turret-23,yy+17,26,24);g.DrawRectangle(focusedModule==3?glint:pen,turret+8,yy+14,18,15);}
                    if(focusedModule==0)g.DrawLine(glint,xx+length,yy,xx+length,yy+breadth);
                    if(focusedModule==6)g.DrawRectangle(glint,xx+8,yy+breadth-22,60,14);
                    if(focusedModule==7)g.DrawRectangle(glint,xx+75,yy+5,32,20);
                } else {
                    bool truck=d.Kind==EquipmentKind.Truck;float belly=153, deck=106-d.Armor*2, hullLength=305+d.Frame*4;
                    if(truck){g.FillRectangle(body,49,65,hullLength-85,85);g.DrawRectangle(pen,49,65,hullLength-85,85);for(int i=0;i<9;i++)g.DrawLine(seam,55+i*23,69,55+i*23,144);g.FillPolygon(body,new[]{new PointF(298,147),new PointF(298,72),new PointF(349,72),new PointF(375,115),new PointF(380,147)});g.DrawPolygon(pen,new[]{new PointF(298,147),new PointF(298,72),new PointF(349,72),new PointF(375,115),new PointF(380,147)});g.FillPolygon(glass,new[]{new PointF(309,80),new PointF(343,80),new PointF(360,108),new PointF(309,108)});}
                    else {using(var tracks=new GraphicsPath()){tracks.AddArc(39,135,52,52,90,180);tracks.AddArc(330,135,52,52,270,180);tracks.CloseFigure();g.FillPath(shade,tracks);g.DrawPath(focusedModule==5?glint:pen,tracks);}var hull=new[]{new PointF(43,belly),new PointF(61,deck),new PointF(hullLength-5,deck),new PointF(hullLength+45,128),new PointF(hullLength+53,belly)};g.FillPolygon(body,hull);g.DrawPolygon(focusedModule==0?glint:pen,hull);
                        float tx=d.Kind==EquipmentKind.Artillery?108:174, ty=deck-(d.Kind==EquipmentKind.IFV?26:40)-d.Frame*2;var turret=new[]{new PointF(tx-30,deck),new PointF(tx-18,ty),new PointF(tx+60,ty),new PointF(tx+86,deck)};g.FillPolygon(body,turret);g.DrawPolygon(pen,turret);g.FillRectangle(steel,tx+49,ty+12,66+d.Gun*10,8);g.DrawRectangle(focusedModule==1?glint:pen,tx+49,ty+12,66+d.Gun*10,8);g.FillRectangle(shade,tx-2,ty-6,28,6);g.DrawRectangle(pen,tx-2,ty-6,28,6);g.DrawLine(focusedModule==3?glint:pen,tx+4,ty-6,tx-1,ty-30);
                        for(int i=0;i<5+d.Engine;i++)g.DrawLine(seam,69+i*4,deck+8,66+i*4,deck+25);
                        g.DrawLines(pen,new[]{new PointF(tx-23,deck-4),new PointF(tx-13,ty+5),new PointF(tx+55,ty+5),new PointF(tx+77,deck-4)});
                        g.DrawLine(seam,tx+40,ty+6,tx+58,deck-3);
                        g.DrawLine(pen,tx+69,ty+10,tx+69,ty+22);
                        g.FillRectangle(shade,tx+112+d.Gun*10,ty+10,8,12);g.DrawRectangle(pen,tx+112+d.Gun*10,ty+10,8,12);
                        g.DrawRectangle(seam,tx+1,ty+13,22,11);g.DrawLine(pen,tx+13,ty+14,tx+13,ty+23);
                        g.DrawLine(pen,61,deck+3,hullLength-5,deck+3);g.DrawLine(seam,61,deck+30,hullLength+30,deck+30);
                        for(int plate=0;plate<9;plate++){float px=61+plate*32;g.DrawPolygon(seam,new[]{new PointF(px,134),new PointF(px+29,134),new PointF(px+26,151),new PointF(px-2,151)});g.FillEllipse(shade,px+3,137,2,2);g.FillEllipse(shade,px+22,137,2,2);}
                        g.DrawRectangle(focusedModule==6?glint:pen,45,deck+9,17,27);g.DrawLine(seam,45,deck+22,62,deck+22);
                        g.DrawRectangle(focusedModule==7?glint:pen,hullLength-45,deck+8,26,15);
                        g.DrawEllipse(pen,hullLength+22,131,8,8);g.DrawLine(pen,hullLength+33,129,hullLength+43,129);
                        if(focusedModule==2)g.DrawRectangle(glint,65,deck+5,53,25);
                        for(int i=0;i<(d.Kind==EquipmentKind.IFV?6:3);i++){g.FillRectangle(shade,139+i*27,deck+12,17,13);g.DrawRectangle(seam,139+i*27,deck+12,17,13);}
                    }
                    for(int i=0;i<(truck?4:7);i++){float wx=truck?(i==3?342:80+i*51):73+i*44;float wy=truck?167:162;g.FillEllipse(shade,wx-18,wy-18,36,36);g.DrawEllipse(pen,wx-18,wy-18,36,36);g.FillEllipse(steel,wx-11,wy-11,22,22);g.DrawEllipse(seam,wx-8,wy-8,16,16);for(int b=0;b<6;b++){double a=b*Math.PI/3;g.FillEllipse(shade,wx+(float)Math.Cos(a)*7-1,wy+(float)Math.Sin(a)*7-1,2,2);}}
                    if(!truck)for(int i=0;i<31;i++){g.DrawLine(seam,62+i*10,181,62+i*10,188);}
                    g.DrawLine(pen,34,207,399,207);g.DrawLine(pen,34,201,34,213);g.DrawLine(pen,399,201,399,213);
                }
            }
            g.Restore(state);
        }
    }
}
