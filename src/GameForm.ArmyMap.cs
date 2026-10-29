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
            internal int Personnel, Stars, Readiness, Sector;
            internal bool Overview;
            internal PointF Anchor;
            internal string Tip;
        }
        private readonly List<ArmyMapGroup> armyMapGroups = new List<ArmyMapGroup>();
        private long armyMapFingerprint = long.MinValue;
        private Campaign armyMapCampaign;
        internal int ArmyMapGroupCount => armyMapGroups.Count;
        private void PrepareArmyGroups()
        {
            // Fixed world sectors do not shuffle as the camera pans. Detail stays exact.
            int cell = scale < 3 ? 48 : scale < 6 ? 24 : scale < 12 ? 12 : scale < 24 ? 6 : 0;
            long fingerprint = 17 + cell;
            unchecked { foreach (var a in E.S.Armies) {
                if (!E.SharesMilitaryIntel(E.S.Player, a.Owner)) continue;
                fingerprint = fingerprint * 31 + a.Id; fingerprint = fingerprint * 31 + a.Owner; fingerprint = fingerprint * 31 + a.Region;
                fingerprint = fingerprint * 31 + a.Target; fingerprint = fingerprint * 31 + a.Personnel; fingerprint = fingerprint * 31 + DeploymentRules.Stars(a); fingerprint = fingerprint * 31 + (int)ReadinessRules.Score(a);
            } }
            if (armyMapCampaign == E.S && armyMapFingerprint == fingerprint) return;
            armyMapCampaign = E.S; armyMapFingerprint = fingerprint; armyMapGroups.Clear();
            var stationary = new Dictionary<long,ArmyMapGroup>();
            foreach (var a in E.S.Armies) {
                if (!E.SharesMilitaryIntel(E.S.Player, a.Owner)) continue;
                ArmyMapGroup group;
                var region = W.Regions[a.Region];
                int column = cell == 0 ? 0 : (int)Math.Floor((region.X + 180) / cell);
                int row = cell == 0 ? 0 : (int)Math.Floor((region.Y + 90) / cell);
                int sector = row * 128 + column;
                long key = cell == 0 ? (long)a.Owner * W.Regions.Length + a.Region : (long)a.Owner * 4096 + sector;
                if ((cell == 0 && a.Target >= 0) || !stationary.TryGetValue(key,out group)) {
                    group = new ArmyMapGroup { Overview = cell > 0, Sector = sector };
                    armyMapGroups.Add(group);
                    if (cell > 0 || a.Target < 0) stationary[key] = group;
                }
                group.Units.Add(a); group.Personnel += a.Personnel;
            }
            foreach (var group in armyMapGroups) {
                long weighted = 0; double readiness = 0, anchorX = 0, anchorY = 0;
                foreach (var a in group.Units) {
                    weighted += (long)DeploymentRules.Stars(a) * a.Personnel; readiness += ReadinessRules.Score(a) * a.Personnel;
                    anchorX += W.Regions[a.Region].X * Math.Max(1, a.Personnel); anchorY += W.Regions[a.Region].Y * Math.Max(1, a.Personnel);
                }
                int anchorWeight = 0; foreach (var a in group.Units) anchorWeight += Math.Max(1, a.Personnel);
                group.Anchor = new PointF((float)(anchorX / anchorWeight), (float)(anchorY / anchorWeight));
                group.Readiness = (int)Math.Round(readiness / Math.Max(1,group.Personnel));
                group.Stars = (int)Math.Round((double)weighted / Math.Max(1,group.Personnel));
                group.Tip = W.Countries[group.Units[0].Owner].Name + " · " + People(group.Personnel) + " чел. · соединений: " + group.Units.Count + ". "
                    + (group.Overview ? "Обзор сектора (Σ): включает части на марше из этих областей. Щелчок приближает точные позиции. " : group.Units.Count > 1 ? "Повторный щелчок переключает части. " : "") + (group.Units[0].Owner == E.S.Player ? "ПКМ по области — приказ выбранной части. " : "Позиции переданы партнёром. ") + "Подготовка: " + UnitQualityRules.Names[group.Stars] + ". Звёзды — квалификация; полоска — текущая готовность.";
            }
        }
        private void DrawArmyMap(Graphics g)
        {
            PrepareArmyGroups();
            if (army >= 0 && !E.S.Armies.Exists(a => a.Id == army && E.SharesMilitaryIntel(E.S.Player, a.Owner))) army = -1;
            foreach (var group in armyMapGroups) {
                var unit = group.Units[0]; bool chosen = false;
                foreach (var member in group.Units) if (member.Id == army) { unit = member; chosen = true; break; }
                var region = W.Regions[unit.Region]; PointF position = group.Overview ? Screen(group.Anchor.X, group.Anchor.Y) : Screen(region.X,region.Y);
                if (!group.Overview && unit.Target >= 0) {
                    PointF target = Screen(W.Regions[unit.Target].X,W.Regions[unit.Target].Y);
                    float progress = (float)(1 - Rules.Clamp(unit.Days / unit.TotalDays,0,1));
                    position = new PointF(position.X+(target.X-position.X)*progress,position.Y+(target.Y-position.Y)*progress);
                    // Draw only the selected unit's route; hundreds of overlapping routes
                    // do not obscure the atlas or allocate arrow caps every frame.
                    if (chosen && unit.Owner == E.S.Player) using (var pen = new Pen(Palette.Gold,1.7f)) using (var cap = new AdjustableArrowCap(3,4)) {
                        pen.DashStyle = DashStyle.Dash; pen.CustomEndCap = cap;
                        if(gpuRecordingWorld)MapPolyline(g,Palette.Gold,1.7f,new[]{position,target},true,true);else g.DrawLine(pen,position,target);
                        foreach (int stop in unit.Route) {var next=Screen(W.Regions[stop].X,W.Regions[stop].Y);if(gpuRecordingWorld)MapPolyline(g,Palette.Gold,1.7f,new[]{target,next},true,true);else g.DrawLine(pen,target,next);target=next;}
                    }
                }
                if (!(gpuRecordingWorld?GpuWorldVisible(position):mapRect.Contains(position))) continue;
                var bounds = new RectangleF(position.X-34,position.Y-18,70,37);
                int badgeId = group.Overview ? -2000000-unit.Owner*4096-group.Sector : group.Units.Count == 1 ? unit.Id : -1-unit.Owner*W.Regions.Length-unit.Region;
                DrawUnitBadge(g,unit,bounds,group.Personnel,group.Units.Count,group.Stars,group.Readiness,badgeId,chosen,group.Overview);
                var captured = group;
                hits.Add(new Hit { Rect = gpuRecordingWorld?bounds:RectangleF.Intersect(bounds,mapRect), Label = "army-group:"+badgeId, Tip = group.Tip, Action = () => {
                    if (!E.SharesMilitaryIntel(E.S.Player, captured.Units[0].Owner)) return;
                    int index = captured.Units.FindIndex(a => a.Id == army); var next = captured.Units[(index + 1) % captured.Units.Count];
                    army = next.Id; selected = next.Region; foreign = next.Owner;
                    if (captured.Overview) {
                        var origin = W.Regions[next.Region]; cx = (float)origin.X; cy = (float)origin.Y;
                        if (next.Target >= 0) { var target = W.Regions[next.Target]; float progress = (float)(1 - Rules.Clamp(next.Days / next.TotalDays, 0, 1)); cx += ((float)target.X-cx)*progress; cy += ((float)target.Y-cy)*progress; }
                        scale = 45; ClampCamera();
                    }
                    Invalidate();
                } });
            }
            DrawIntelligenceContacts(g);
        }
    }
}
