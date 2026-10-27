using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Sovereign
{
    public partial class GameForm
    {
        private void TestOperationsInterface(string directory, Action<bool,string> check, Action<string> click)
        {
            E.New(W.ByCode("UKR"),2026,1);E.AutoSaveEnabled=false;speed=0;clock.Stop();cameraRefresh.Stop();showHelp=false;
            var sizes=new[]{new Size(1280,720),new Size(1600,900),new Size(1920,1080)};
            int home=W.Countries[E.S.Player].Regions[0];selected=home;int italy=W.ByCode("ITA"),factory=W.Countries[italy].Regions[0];
            procurementCountry=italy;procurementProduct=8;procurementCompany=factory*8+5;E.S.Provinces[factory].Private[5]=2;
            E.Player.Treasury=100;for(int k=0;k<8;k++)E.S.Nations[italy].Stock[k]=1000;
            foreach(var size in sizes) {ClientSize=size;foreach(string panel in new[]{"Госзаказы","Боевой порядок","Настройки"}){PreviewPage(panel);using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"operations-"+panel+"-"+size.Width+".png"));check(hits.All(h=>h.Rect.Left>=0&&h.Rect.Top>=0&&h.Rect.Right<=VW+1&&h.Rect.Bottom<=VH+1),panel+" controls remain visible at "+size.Width);}}
            ClientSize=new Size(1600,900);PreviewPage("Карта");OpenProcurement();confirmationForTests=_=>false;click("contract:place");check(E.Player.Contracts.Count==0,"Declining procurement leaves the treasury and contract book unchanged");
            try {confirmationForTests=_=>true;click("contract:place");check(E.Player.Contracts.Count==1 && E.Player.Contracts[0].Supplier==italy,"Procurement UI places the selected product with the chosen Italian supplier");var contract=E.Player.Contracts[0];E.TickContracts();double progress=contract.Progress;click("contract:pause:"+contract.Id);E.TickContracts();check(contract.Paused&&contract.Progress==progress,"Contract pause control stops real production");click("contract:pause:"+contract.Id);check(!contract.Paused,"Contract resume control reacquires free private capacity");confirmationForTests=_=>false;click("contract:cancel:"+contract.Id);check(E.Player.Contracts.Count==1,"Declining cancellation preserves the paid contract");confirmationForTests=_=>true;click("contract:cancel:"+contract.Id);check(E.Player.Contracts.Count==0,"Accepting cancellation removes the actual contract");}finally{confirmationForTests=null;}
            procurementProduct=10;foreach(var project in E.Player.Designs)project.Retired=true;
            int heldDesign=E.Player.ActiveDesign;click("contract:design");check(E.Player.ActiveDesign==heldDesign&&toast.Contains("Восстановите проект"),"An entirely archived catalog disables equipment-model selection without division by zero");
            foreach(var project in E.Player.Designs)project.Retired=false;
            OpenDeployment();deploymentDraft=new BattleDeployment {Front=2,Left=1,Right=1};templateDraftName="Фланговая группа UI";click("deployment:more:3");int total=E.Player.Templates.Count;click("Создать боевой штат · 5 бат.");check(E.Player.Templates.Count==total+1 && E.SelectedTemplate(E.S.Player).Deployment.Reserve==1,"Deployment controls create a five-battalion template with a real reserve");
            foreach(var size in sizes) {ClientSize=size;Navigate("Конструктор");blueprintExpanded=false;click("blueprint:expand");for(int kind=0;kind<8;kind++){draft=EquipmentRules.Balanced((EquipmentKind)kind,E.Player,"Альбом "+kind);click("blueprint:module:2");using(var frame=RenderFrame())frame.Save(Path.Combine(directory,"blueprint-"+kind+"-"+size.Width+".png"));check(blueprintExpanded&&focusedModule==2&&hits.All(h=>h.Rect.Left>=0&&h.Rect.Right<=VW+1&&h.Rect.Bottom<=VH+1),"Technical album class "+kind+" has reachable module controls at "+size.Width);}}
            blueprintExpanded=false;ClientSize=new Size(1600,900);PreviewPage("Карта");detailPanel=false;scale=45;cx=20;cy=-50;selected=hoveredRegion=-1;layer="Политика";prefs.Labels=false;prefs.Texture=false;prefs.RegionBorders=false;
            var handle=Handle;WaitForAtlas();using(var frame=RenderFrame()){} int builds=MapCacheBuilds;int fallback=AtlasFallbackFrames;panning=true;cx=42;
            using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height))using(var g=Graphics.FromImage(bitmap)) {
                // Exercise the production path: no synchronous screenshot helper during a drag.
                OnPaint(new PaintEventArgs(g,ClientRectangle));PointF land=Screen(48,-50);
                check(AtlasFallbackFrames>fallback && bitmap.GetPixel((int)land.X,(int)land.Y).ToArgb()!=Color.FromArgb(122,148,154).ToArgb(),"Panning beyond the cached edge keeps newly exposed inland geography visible");
                bitmap.Save(Path.Combine(directory,"async-map-during-drag.png"));
            }
            WaitForAtlas();using(var frame=RenderFrame()){}check(panning&&MapCacheBuilds>builds&&AtlasMissingTiles==0,"Fine geography refreshes while the mouse is still held, without waiting for release");
            using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height))using(var g=Graphics.FromImage(bitmap)) {for(int i=0;i<10;i++){cx=20+i*2;scale=30+i*3;g.ResetTransform();OnPaint(new PaintEventArgs(g,ClientRectangle));}}
            WaitForAtlas();using(var frame=RenderFrame()){}check(AtlasMissingTiles==0&&AtlasPixelScale>=scale*UiScale,"Rapid pan and zoom coalesce to the latest complete atlas request");panning=false;
            string oldKey=overviewKey;layer="Ресурсы";using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height))using(var g=Graphics.FromImage(bitmap))OnPaint(new PaintEventArgs(g,ClientRectangle));WaitForAtlas();check(cachedScene.Key==overviewKey&&overviewKey!=oldKey,"Changing map layers rejects stale background surfaces");
            prefs.GraphicsQuality=0;layer="Политика";using(var frame=RenderFrame()){}float low=AtlasPixelScale;prefs.GraphicsQuality=2;using(var frame=RenderFrame()){}check(AtlasPixelScale>low,"Graphics quality changes actual raster resolution");
            prefs.FrameLimit=30;nextFrameAt=0;Invalidate();bool first=FrameDue(1000);Invalidate();bool early=FrameDue(1010),later=FrameDue(1040);check(first&&!early&&later,"Frame limiting coalesces updates and enforces the selected thirty-frame interval");check(!FrameDue(2000),"An idle window does not schedule redundant repaint frames");
            prefs.FrameLimit=0;nextFrameAt=0;Invalidate();bool free=FrameDue(2000);Invalidate();check(free&&FrameDue(2001),"Unlimited mode permits consecutive pending frames without an artificial cap");prefs.FrameLimit=60;
            var originalBounds=Bounds;ToggleFullScreen();check(fullScreen && FormBorderStyle==FormBorderStyle.None && Bounds==System.Windows.Forms.Screen.FromControl(this).Bounds,"Fullscreen mode covers the selected monitor without a window border");ToggleFullScreen();check(!fullScreen&&Bounds==originalBounds,"Leaving fullscreen restores the prior window bounds");
            prefs.WindowWidth=1280;prefs.WindowHeight=720;CycleResolution();check(ClientSize.Width==prefs.WindowWidth&&ClientSize.Height==prefs.WindowHeight&&ClientSize.Width>1280,"Resolution controls resize the actual client area and persist the applied dimensions");
            using(var frame=RenderFrame()){}int badges=UnitBadgeBuilds;using(var frame=RenderFrame()){}check(UnitBadgeBuilds==badges,"Unchanged army badges reuse cached text and stars across frames");
            prefs.FrameLimit=90;prefs.GraphicsQuality=0;prefs.Save();var disk=Preferences.Read();check(disk.FrameLimit==90&&disk.GraphicsQuality==0&&disk.WindowWidth==ClientSize.Width,"Display and performance settings survive saving and reloading preferences");prefs.FrameLimit=60;
            prefs.Labels=true;prefs.Texture=true;prefs.RegionBorders=true;prefs.GraphicsQuality=1;nextFrameAt=0;
        }
    }
}
