using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Sovereign
{
    public partial class GameForm
    {
        private sealed class ArmyMapGroup
        {
            internal readonly List<Army> Units = new List<Army>();
            internal int Personnel, Stars;
            internal string Tip;
        }
        private readonly List<ArmyMapGroup> armyMapGroups = new List<ArmyMapGroup>();
        private long armyMapFingerprint = long.MinValue;
        private Campaign armyMapCampaign;
        internal int ArmyMapGroupCount => armyMapGroups.Count;
        private void PrepareArmyGroups()
        {
            bool foreignVisible = layer == "Армии" || scale >= 70;
            long fingerprint = foreignVisible ? 17 : 19;
            unchecked { foreach (var a in E.S.Armies) {
                if (!foreignVisible && a.Owner != E.S.Player && !E.AtWar(a.Owner,E.S.Player)) continue;
                fingerprint = fingerprint * 31 + a.Id; fingerprint = fingerprint * 31 + a.Owner; fingerprint = fingerprint * 31 + a.Region;
                fingerprint = fingerprint * 31 + a.Target; fingerprint = fingerprint * 31 + a.Personnel; fingerprint = fingerprint * 31 + DeploymentRules.Stars(a);
            } }
            if (armyMapCampaign == E.S && armyMapFingerprint == fingerprint) return;
            armyMapCampaign = E.S; armyMapFingerprint = fingerprint; armyMapGroups.Clear();
            var stationary = new Dictionary<long,ArmyMapGroup>();
            foreach (var a in E.S.Armies) {
                if (!foreignVisible && a.Owner != E.S.Player && !E.AtWar(a.Owner,E.S.Player)) continue;
                ArmyMapGroup group;
                long key = (long)a.Owner * W.Regions.Length + a.Region;
                if (a.Target >= 0 || !stationary.TryGetValue(key,out group)) {
                    group = new ArmyMapGroup(); armyMapGroups.Add(group);
                    if (a.Target < 0) stationary[key] = group;
                }
                group.Units.Add(a); group.Personnel += a.Personnel;
            }
            foreach (var group in armyMapGroups) {
                long weighted = 0; foreach (var a in group.Units) weighted += (long)DeploymentRules.Stars(a) * a.Personnel;
                group.Stars = (int)Math.Round((double)weighted / Math.Max(1,group.Personnel));
                group.Tip = W.Countries[group.Units[0].Owner].Name + " · " + People(group.Personnel) + " чел. · соединений: " + group.Units.Count + ". "
                    + (group.Units.Count > 1 ? "Повторный щелчок переключает части. " : "") + "ПКМ по области — приказ выбранной части.";
            }
        }
        private void DrawArmyMap(Graphics g)
        {
            PrepareArmyGroups();
            foreach (var group in armyMapGroups) {
                var unit = group.Units[0]; bool chosen = false;
                foreach (var member in group.Units) if (member.Id == army) { unit = member; chosen = true; break; }
                var region = W.Regions[unit.Region]; PointF position = Screen(region.X,region.Y);
                if (unit.Target >= 0) {
                    PointF target = Screen(W.Regions[unit.Target].X,W.Regions[unit.Target].Y);
                    float progress = (float)(1 - Rules.Clamp(unit.Days / unit.TotalDays,0,1));
                    position = new PointF(position.X+(target.X-position.X)*progress,position.Y+(target.Y-position.Y)*progress);
                    // Draw only the selected unit's route; hundreds of overlapping routes
                    // do not obscure the atlas or allocate arrow caps every frame.
                    if (chosen && unit.Owner == E.S.Player) using (var pen = new Pen(Palette.Gold,1.7f)) using (var cap = new AdjustableArrowCap(3,4)) {
                        pen.DashStyle = DashStyle.Dash; pen.CustomEndCap = cap; g.DrawLine(pen,position,target);
                        foreach (int stop in unit.Route) { var next=Screen(W.Regions[stop].X,W.Regions[stop].Y); g.DrawLine(pen,target,next); target=next; }
                    }
                }
                if (!mapRect.Contains(position)) continue;
                var bounds = new RectangleF(position.X-34,position.Y-18,70,37);
                int badgeId = group.Units.Count == 1 ? unit.Id : -1-unit.Owner*W.Regions.Length-unit.Region;
                DrawUnitBadge(g,unit,bounds,group.Personnel,group.Units.Count,group.Stars,badgeId,chosen);
                var captured = group;
                hits.Add(new Hit { Rect = RectangleF.Intersect(bounds,mapRect), Label = "army-group:"+badgeId, Tip = group.Tip, Action = () => {
                    int index = captured.Units.FindIndex(a => a.Id == army); var next = captured.Units[(index + 1) % captured.Units.Count];
                    army = next.Id; selected = next.Region; foreign = next.Owner; Invalidate();
                } });
            }
        }
    }
}
