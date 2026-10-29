using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        private void InitializeIntelligence(Campaign campaign, bool newScenario)
        {
            campaign.IntelligenceSharing = new bool[campaign.Nations.Length * campaign.Nations.Length];
            campaign.MilitaryContacts = new List<MilitaryContact>();
            foreach (var army in campaign.Armies) {
                // Existing campaigns keep their forces, experience, fatigue and economy.
                if (!newScenario) army.Proficiency = Rules.Clamp(35 + army.Experience * .5, 35, 85);
            }
            campaign.Version = 15;
        }

        public bool SharesMilitaryIntel(int observer, int owner)
        {
            if (observer == owner) return true;
            if (AtWar(observer, owner)) return false;
            return Allied(observer, owner) || S.IntelligenceSharing[Pair(observer, owner)];
        }

        public List<MilitaryContact> KnownMilitaryContacts(int observer)
        {
            // Only first-hand partner reports. A -> B -> C does not confer A's access on C.
            var newest = new Dictionary<long, MilitaryContact>();
            foreach (var contact in S.MilitaryContacts) {
                if (S.Day - contact.Day > IntelligenceRules.ContactLifetime || !SharesMilitaryIntel(observer, contact.Observer)
                    || SharesMilitaryIntel(observer, contact.Owner)) continue;
                long key = (long)contact.Owner * S.Provinces.Length + contact.Region;
                MilitaryContact prior;
                if (!newest.TryGetValue(key, out prior) || contact.Day > prior.Day || (contact.Day == prior.Day && contact.Observer == observer)) newest[key] = contact;
            }
            return newest.Values.OrderBy(c => c.Owner).ThenBy(c => c.Region).ToList();
        }

        // Called at simulation boundaries, never by camera motion. Work is proportional
        // to troops + regions + local sightings, not countries * troops * regions.
        internal void RefreshGroundIntelligence()
        {
            S.MilitaryContacts.RemoveAll(c => S.Day - c.Day > IntelligenceRules.ContactLifetime);
            var atWar = new bool[S.Nations.Length]; bool anyWar = false;
            for (int a = 0; a < atWar.Length; a++) for (int b = 0; b < atWar.Length; b++)
                if (S.Wars[a * atWar.Length + b]) { atWar[a] = true; anyWar = true; }
            if (!anyWar) return;
            var stationed = new Dictionary<int, Dictionary<int, long>>();
            var coverage = new HashSet<long>();
            int count = S.Provinces.Length;
            foreach (var unit in S.Armies) {
                if (unit.Personnel <= 0) continue;
                Dictionary<int, long> owners;
                if (!stationed.TryGetValue(unit.Region, out owners)) stationed[unit.Region] = owners = new Dictionary<int, long>();
                long people; owners.TryGetValue(unit.Owner, out people); owners[unit.Owner] = people + unit.Personnel;
                if (!atWar[unit.Owner]) continue;
                coverage.Add((long)unit.Owner * count + unit.Region);
                if (unit.TrainingDays == 0 && unit.Strength >= 20 && unit.Organization >= 20)
                    foreach (int region in World.Regions[unit.Region].Neighbors) coverage.Add((long)unit.Owner * count + region);
            }
            // Domestic territorial surveillance detects hostile intrusions; neither
            // military access nor a trade treaty grants allied position reporting.
            foreach (var region in stationed) coverage.Add((long)S.Provinces[region.Key].Owner * count + region.Key);
            var fresh = new List<MilitaryContact>();
            foreach (long key in coverage) {
                int observer = (int)(key / count), region = (int)(key % count);
                Dictionary<int, long> owners;
                if (!stationed.TryGetValue(region, out owners)) continue;
                foreach (var owner in owners)
                    if (AtWar(observer, owner.Key)) fresh.Add(IntelligenceRules.Observe(observer, owner.Key, region, S.Day, owner.Value, IntelSource.GroundContact));
            }
            // An empty revisited area disproves its old report; unobserved reports age.
            // Keep same-day aerial sightings: a sortie and ground scan are distinct snapshots.
            S.MilitaryContacts.RemoveAll(c => coverage.Contains((long)c.Observer * count + c.Region)
                && AtWar(c.Observer, c.Owner) && !(c.Day == S.Day && c.Source == IntelSource.AirRecon));
            var indices = new Dictionary<long, int>();
            for (int i = 0; i < S.MilitaryContacts.Count; i++) {
                var c = S.MilitaryContacts[i]; indices[((long)c.Observer * S.Nations.Length + c.Owner) * count + c.Region] = i;
            }
            foreach (var contact in fresh) {
                int index; long key = ((long)contact.Observer * S.Nations.Length + contact.Owner) * count + contact.Region;
                if (indices.TryGetValue(key, out index)) S.MilitaryContacts[index] = contact;
                else { indices[key] = S.MilitaryContacts.Count; S.MilitaryContacts.Add(contact); }
            }
        }

        private void StoreContact(MilitaryContact contact)
        {
            int index = S.MilitaryContacts.FindIndex(c => c.Observer == contact.Observer && c.Owner == contact.Owner && c.Region == contact.Region);
            if (index < 0) S.MilitaryContacts.Add(contact); else S.MilitaryContacts[index] = contact;
        }

        private void RecordAirIntelligence(int observer, int region, List<Army> units)
        {
            S.MilitaryContacts.RemoveAll(c => c.Observer == observer && c.Region == region);
            if (units == null) return;
            foreach (var group in units.Where(a => a.Personnel > 0 && a.Owner != observer && AtWar(observer, a.Owner)).GroupBy(a => a.Owner))
                StoreContact(IntelligenceRules.Observe(observer, group.Key, region, S.Day, group.Sum(a => (long)a.Personnel), IntelSource.AirRecon));
        }

        internal Dictionary<int, double> KnownEnemyPower(int observer)
        {
            var result = new Dictionary<int, double>();
            foreach (var contact in KnownMilitaryContacts(observer)) {
                if (!AtWar(observer, contact.Owner)) continue;
                double prior; result.TryGetValue(contact.Region, out prior);
                result[contact.Region] = prior + EstimatedPersonnelPower(contact.Owner, contact.UpperPersonnel);
            }
            return result;
        }

        private double EstimatedPersonnelPower(int owner, double people)
        {
            // Public scenario baseline with deliberately conservative readiness. No
            // access to a hidden formation's equipment, supplies or current condition.
            return people / 800 * new Blueprint { Armor = 2, Gun = 2, Engine = 2, Electronics = 1 }.Power
                * (.70 + World.Countries[owner].TrainingBaseline * .006);
        }
        private double PlanningPower(Army unit) => unit.Battalions * unit.Design.Power * ReadinessRules.Score(unit) / 100
            * UnitQualityRules.CombatFactor(unit) * GroundCombatFactor(unit.Owner, false);

        private void ValidateIntelligence(Campaign campaign)
        {
            int nations = campaign.Nations.Length, regions = campaign.Provinces.Length;
            if (campaign.IntelligenceSharing == null || campaign.IntelligenceSharing.Length != nations * nations || campaign.MilitaryContacts == null)
                throw new InvalidDataException("Повреждены разведывательные соглашения.");
            for (int a = 0; a < nations; a++) for (int b = 0; b < nations; b++) {
                int key = a * nations + b;
                if (campaign.IntelligenceSharing[key] != campaign.IntelligenceSharing[b * nations + a]
                    || (campaign.IntelligenceSharing[key] && (a == b || campaign.Wars[key]))) throw new InvalidDataException("Несогласованный обмен разведданными.");
            }
            var unique = new HashSet<long>();
            foreach (var c in campaign.MilitaryContacts) {
                if (c == null || c.Observer < 0 || c.Observer >= nations || c.Owner < 0 || c.Owner >= nations || c.Observer == c.Owner
                    || c.Region < 0 || c.Region >= regions || c.Day < 0 || c.Day > campaign.Day || c.LowerPersonnel < 0 || c.UpperPersonnel <= c.LowerPersonnel
                    || !Enum.IsDefined(typeof(IntelSource), c.Source) || !unique.Add(((long)c.Observer * nations + c.Owner) * regions + c.Region))
                    throw new InvalidDataException("Повреждено разведывательное наблюдение.");
            }
            foreach (var a in campaign.Armies)
                if (!IsFinite(a.Proficiency) || a.Proficiency < 0 || a.Proficiency > 100) throw new InvalidDataException("Повреждена подготовка части.");
        }
    }
}
