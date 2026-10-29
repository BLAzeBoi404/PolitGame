using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void TestIntelligenceInterface(string directory, Action<bool, string> check, Action<string> click)
        {
            E.New(W.ByCode("UKR"), 2026, 1); speed = 0; showHelp = false; menuScreen = "";
            int me = E.S.Player, partner = W.ByCode("POL"), enemy = W.ByCode("RUS");
            Navigate("Карта"); cx = 25; cy = -50; selected = hoveredRegion = -1;
            foreach (string mode in MapLayers) foreach (float zoom in new[] { 4f, 100f }) {
                layer = mode; scale = zoom; Invalidate(); using (var frame = RenderFrame()) { }
                check(armyMapGroups.Count > 0 && armyMapGroups.All(group => group.Units.All(a => a.Owner == me)), "Foreign formations stay hidden in " + mode + " at zoom " + zoom);
            }
            layer = "Армии"; scale = 45; Invalidate(); using (var frame = RenderFrame()) { }
            int secretRegion = W.Countries[enemy].CapitalRegion; var secret = E.S.Armies.First(a => a.Owner == enemy);
            string before = AtlasContentKey(false); var snapshot = CaptureAtlas(before, false); Color prior = snapshot.RegionColors[secretRegion];
            int location = secret.Region; secret.Region = secretRegion; secret.Personnel += 111;
            Invalidate(); string after = AtlasContentKey(false); var changed = CaptureAtlas(after, false);
            check(before == after && prior == changed.RegionColors[secretRegion] && militaryIntensity[secretRegion] == 0, "Hidden enemy changes affect neither the military heatmap nor its cache key");
            secret.Region = location; secret.Personnel -= 111;
            var own = E.S.Armies.First(a => a.Owner == me); int originalPeople = own.Personnel;
            before = AtlasContentKey(false); own.Personnel--; Invalidate(); after = AtlasContentKey(false);
            check(before == after, "Small personnel changes that leave military colors unchanged reuse the atlas instead of rebuilding tiles");
            own.Personnel = originalPeople; Invalidate();
            foreign = partner; Navigate("Дипломатия"); click("Переговоры и договоры →");
            click("Согласовать обмен"); check(!E.SharesMilitaryIntel(me, partner), "The disabled treaty control cannot reveal a low-relation partner");
            E.S.Relations[E.Pair(me, partner)] = E.S.Relations[E.Pair(partner, me)] = 80; Invalidate();
            click("Согласовать обмен"); check(E.SharesMilitaryIntel(me, partner), "A real diplomacy click enables bilateral intelligence sharing");
            foreach (var size in new[] { new Size(1280,720), new Size(1600,900), new Size(1920,1080) }) {
                ClientSize = size; toastUntil = DateTime.MinValue; using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "intelligence-diplomacy-" + size.Width + ".png"));
                var treaty = hits.Last(h => h.Label == "Прекратить обмен");
                check(treaty.Rect.Bottom <= VH && treaty.Rect.Right <= VW && treaty.Rect.Width > 100, "The intelligence treaty remains reachable at " + size.Width + "x" + size.Height);
            }
            ClientSize = new Size(1600,900); Navigate("Карта"); scale = 45; Invalidate(); using (var frame = RenderFrame()) { }
            check(armyMapGroups.Any(group => group.Units.Any(a => a.Owner == partner)) && armyMapGroups.All(group => group.Units.All(a => a.Owner == me || a.Owner == partner)), "The map includes the agreed partner but no unrelated army");
            var allyHit = hits.First(h => h.Label != null && h.Label.StartsWith("army-group:") && h.Tip.StartsWith(W.Countries[partner].Name));
            army = E.S.Armies.First(a => a.Owner == partner).Id; foreign = partner; Navigate("Дипломатия"); click("Переговоры и договоры →"); click("Прекратить обмен");
            Navigate("Карта"); Invalidate(); using (var frame = RenderFrame()) { }
            check(army == -1 && armyMapGroups.All(group => group.Units.All(a => a.Owner == me)), "Revoking access clears the retained foreign selection and marker groups");
            allyHit.Action(); check(army == -1, "A stale retained hit cannot reselect a now-hidden partner formation");
            E.S.MilitaryContacts.Add(IntelligenceRules.Observe(me, enemy, secretRegion, E.S.Day, 1234, IntelSource.AirRecon));
            cx = (float)W.Regions[secretRegion].X; cy = (float)W.Regions[secretRegion].Y; scale = 60; Invalidate();
            using (var frame = RenderFrame()) frame.Save(Path.Combine(directory, "intelligence-contact.png"));
            var contact = hits.Single(h => h.Label != null && h.Label.StartsWith("intel-contact:"));
            check(contact.Tip.Contains("1,0") || contact.Tip.Contains("1.0") || contact.Tip.Contains("1 тыс") || contact.Tip.Contains("оценка"), "Contact tooltip labels approximate personnel and the observation date");
            check(!contact.Tip.Contains("1234") && !contact.Tip.Contains("№"), "An enemy report reveals neither exact personnel nor actual formation identifiers");
            click(contact.Label); check(army == -1 && selected == secretRegion, "Clicking an observation selects its area without obtaining a live enemy unit");
            secret.Region = secretRegion; E.S.Day = IntelligenceRules.ContactLifetime + 1; Invalidate(); using (var frame = RenderFrame()) { }
            check(!hits.Any(h => h.Label != null && h.Label.StartsWith("intel-contact:")), "An expired observation disappears even when an unknown enemy still occupies the area");
            secret.Region = location; E.S.MilitaryContacts.Clear(); E.S.Day = 0;
            OpenReadiness(); using (var frame = RenderFrame()) { }
            check(hits.Any(h => h.Label == "quality:detail" && h.Tip.Contains("85") && h.Tip.Contains("не рейтинг")), "The army desk explains actual grade thresholds and the limits of scenario estimates");
            E.Validate(E.S); Navigate("Карта"); Home(); layer = "Политика";
        }
    }
}
