using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Sovereign
{
    public partial class GameForm
    {
        private void DrawIntelligenceContacts(Graphics g)
        {
            foreach (var contact in E.KnownMilitaryContacts(E.S.Player)) {
                PointF point = Screen(W.Regions[contact.Region].X, W.Regions[contact.Region].Y);
                if (!(gpuRecordingWorld ? GpuWorldVisible(point) : mapRect.Contains(point))) continue;
                var bounds = new RectangleF(point.X - 38, point.Y - 18, 78, 37);
                int badgeId = -1 - (W.Countries.Length + contact.Owner) * W.Regions.Length - contact.Region;
                int age = E.S.Day - contact.Day;
                string key = "contact/" + contact.LowerPersonnel + "/" + contact.UpperPersonnel + "/" + age + "/" + UiScale;
                UnitBadge badge;
                if (!unitBadges.TryGetValue(badgeId, out badge) || badge.Key != key) {
                    if (unitBadges.Count >= 1024 && !gpuRecordingWorld) { ClearUnitBadges(); badge = null; }
                    if (badge != null) badge.Dispose();
                    var bitmap = new Bitmap((int)Math.Ceiling(78 * UiScale), (int)Math.Ceiling(37 * UiScale), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (var ink = Graphics.FromImage(bitmap)) {
                        ink.ScaleTransform(UiScale, UiScale); ink.SmoothingMode = SmoothingMode.AntiAlias; ink.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        Round(ink, Color.FromArgb(38, 31, 29), new RectangleF(0, 0, 77, 36), 4);
                        RoundBorder(ink, Palette.Gold, new RectangleF(0, 0, 77, 36), 4, 1);
                        string estimate = contact.UpperPersonnel >= 1000 ? (contact.UpperPersonnel / 1000.0).ToString("0.#") + "к" : contact.UpperPersonnel.ToString();
                        Txt(ink, "<" + estimate + " ?", 5, 3, 13, Palette.Gold, true, 69);
                        Txt(ink, age == 0 ? "наблюдение" : age + " д. назад", 5, 21, 9, Palette.Muted, false, 69);
                    }
                    badge = new UnitBadge { Key = key, Image = bitmap }; unitBadges[badgeId] = badge; UnitBadgeBuilds++;
                }
                if (gpuRecordingWorld) RecordImage(badge.Image, Physical(bounds), true);
                else { var state = g.Save(); g.ResetTransform(); g.DrawImageUnscaled(badge.Image, (int)Math.Round(bounds.X * UiScale), (int)Math.Round(bounds.Y * UiScale)); g.Restore(state); }
                var seen = contact;
                string source = contact.Source == IntelSource.AirRecon ? "авиаразведка" : "наземное наблюдение";
                string tip = W.Countries[contact.Owner].Name + " · оценка " + People(contact.LowerPersonnel) + "–" + People(contact.UpperPersonnel)
                    + " чел. · " + new DateTime(E.S.StartYear, 1, 1).AddDays(contact.Day).ToString("dd.MM.yyyy") + " · " + source
                    + (contact.Observer == E.S.Player ? ". " : " от " + W.Countries[contact.Observer].Name + ". ")
                    + "Последнее известное положение. Состав, качество и маршрут неизвестны. Наблюдение исчезнет через " + (IntelligenceRules.ContactLifetime + 1 - age) + " д.";
                hits.Add(new Hit { Rect = gpuRecordingWorld ? bounds : RectangleF.Intersect(bounds, mapRect), Label = "intel-contact:" + badgeId, Tip = tip,
                    Action = () => { if (!E.SharesMilitaryIntel(E.S.Player, seen.Observer)) return; army = -1; selected = seen.Region; foreign = seen.Owner; Invalidate(); } });
            }
        }
    }
}
