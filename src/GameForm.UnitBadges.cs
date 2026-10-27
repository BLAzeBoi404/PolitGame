using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
namespace Sovereign
{
    public partial class GameForm
    {
        private sealed class UnitBadge : IDisposable { public string Key; public Bitmap Image; public void Dispose() { Image.Dispose(); } }
        private readonly Dictionary<int, UnitBadge> unitBadges = new Dictionary<int, UnitBadge>();
        internal int UnitBadgeBuilds;
        private void ClearUnitBadges() { foreach(var badge in unitBadges.Values) badge.Dispose(); unitBadges.Clear(); }
        private void DrawUnitBadge(Graphics g, Army unit, RectangleF bounds, int people, int count, int stars, int badgeId, bool chosen)
        {
            bool own = unit.Owner == E.S.Player;
            Color affiliation = own ? Palette.Teal : E.AtWar(E.S.Player, unit.Owner) ? Palette.Red : Palette.Muted;
            int readiness = (int)Math.Round(unit.Strength);
            string key = people + "/" + count + "/" + stars + "/" + readiness + "/" + chosen + "/" + affiliation.ToArgb() + "/" + UiScale;
            UnitBadge badge;
            if (!unitBadges.TryGetValue(badgeId, out badge) || badge.Key != key) {
                if (unitBadges.Count >= 1024 && !gpuRecordingWorld) { ClearUnitBadges(); badge = null; }
                if (badge != null) badge.Dispose();
                var bitmap = new Bitmap((int)Math.Ceiling(70 * UiScale), (int)Math.Ceiling(37 * UiScale), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var ink = Graphics.FromImage(bitmap)) {
                    ink.ScaleTransform(UiScale,UiScale);ink.SmoothingMode=SmoothingMode.AntiAlias;ink.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                    var local=new RectangleF(0,0,69,36);Color color=chosen?Palette.Bg:Palette.Text;
                    Round(ink,chosen?Color.FromArgb(204,185,135):Color.FromArgb(18,32,38),local,4);
                    RoundBorder(ink,chosen?Palette.Gold:Color.FromArgb(92,113,111),local,4,1);
                    Txt(ink,people>=1000?(people/1000.0).ToString(people>=10000?"0":"0.0")+"к":people.ToString(),6,3,13,color,true,count>1?43:59);
                    if(count>1) Txt(ink,"×"+count,46,5,9,color,false,23);
                    DrawRankStars(ink,7,23,stars,color);Progress(ink,3,34,64,readiness/100.0,affiliation);
                }
                badge=new UnitBadge {Key=key,Image=bitmap};unitBadges[badgeId]=badge;UnitBadgeBuilds++;
            }
            if(gpuRecordingWorld){RecordImage(badge.Image,Physical(bounds),true);return;}
            var state=g.Save(); g.ResetTransform(); g.DrawImageUnscaled(badge.Image,(int)Math.Round(bounds.X*UiScale),(int)Math.Round(bounds.Y*UiScale)); g.Restore(state);
        }
    }
}
