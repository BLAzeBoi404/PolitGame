using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private RectangleF MiniMapRect => new RectangleF(VW-314,VH-309,302,179);
        private RectangleF MiniMapImageRect => new RectangleF(VW-306,VH-282,286,143);
        private RectangleF MiniMapViewRect()
        {
            var box=MiniMapImageRect;var view=RectangleF.Intersect(AtlasView,new RectangleF(-180,-90,360,180));
            return new RectangleF(box.X+(view.X+180)/360*box.Width,box.Y+(view.Y+90)/180*box.Height,view.Width/360*box.Width,view.Height/180*box.Height);
        }
        private void DrawMiniMap(Graphics g)
        {
            var box=MiniMapRect;PanelPlate(g,box);
            Txt(g,"НАВИГАЦИЯ",box.X+11,box.Y+8,10,Palette.Muted,true);
            Txt(g,"ЛКМ · перейти",box.Right-108,box.Y+8,10,Palette.Muted);
            var preview=MiniMapImageRect;
            if(gpuCapturingHud) {
                // A transparent window keeps the live viewport below tooltips/popups
                // without rebuilding the HUD on every camera movement.
                var old=g.CompositingMode;g.CompositingMode=CompositingMode.SourceCopy;
                Fill(g,Color.Transparent,preview.X,preview.Y,preview.Width,preview.Height);g.CompositingMode=old;
            } else {
                Fill(g,Palette.Sea,preview.X,preview.Y,preview.Width,preview.Height);
                if(atlasOverview!=null)g.DrawImage(atlasOverview,preview);
                var view=MiniMapViewRect();if(view.Width>0&&view.Height>0)RoundBorder(g,Palette.Text,view,0,1.2f);
            }
            RoundBorder(g,Palette.Line,preview,0,1);
            hits.Add(new Hit {Rect=box,Label="map:overview",Tip="Щёлкните по мини-карте для быстрого перехода. Рамка показывает видимый участок.",Action=()=>{}});
        }
        private void DrawGpuMiniMap()
        {
            if(menuScreen!=""||showHelp||atlasOverview==null)return;
            var box=Physical(MiniMapImageRect);
            gpu.Clip(box);gpu.Image(atlasOverview,box,new RectangleF(0,0,atlasOverview.Width,atlasOverview.Height),true);
            var view=Physical(MiniMapViewRect());
            if(view.Width>0&&view.Height>0)gpu.Line(Palette.Text,1.2f*UiScale,new[]{new PointF(view.Left,view.Top),new PointF(view.Right,view.Top),new PointF(view.Right,view.Bottom),new PointF(view.Left,view.Bottom),new PointF(view.Left,view.Top)},false);
            gpu.Clip(null);
        }
        private bool MiniMapClick(MouseEventArgs e)
        {
            var point=Logical(e.Location);
            if(moved)return false; // Releasing a map drag over the inset is not a minimap click.
            if(menuScreen!=""||showHelp||mapLayersExpanded||!MiniMapRect.Contains(point))return false;
            if(e.Button==MouseButtons.Left && MiniMapImageRect.Contains(point)) {
                cx=(point.X-MiniMapImageRect.X)/MiniMapImageRect.Width*360-180;
                cy=(point.Y-MiniMapImageRect.Y)/MiniMapImageRect.Height*180-90;
                ClampCamera();InvalidateCamera(true);
            }
            panning=moved=false;CapturePointer(false);return true;
        }
    }
}
