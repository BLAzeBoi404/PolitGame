using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Sovereign
{
    public partial class Engine
    {
        private static readonly XmlSerializer CampaignSerializer = new XmlSerializer(typeof(Campaign));
        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static bool Values(double[] values, int size, bool nonnegative = true)
        {
            return values != null && values.Length == size && values.All(v => IsFinite(v) && (!nonnegative || v >= 0));
        }
        public void Validate(Campaign s)
        {
            int nations = World.Countries.Length, regions = World.Regions.Length;
            if (s == null || (s.Version < 1 || s.Version > 10) || s.Nations == null || s.Nations.Length != nations || s.Provinces == null || s.Provinces.Length != regions
                || s.Player < 0 || s.Player >= nations || s.StartYear < 1940 || s.StartYear > 2026 || s.Day < 0 || s.Day > 2000000 || s.RandomState == 0
                || s.Difficulty < 0 || s.Difficulty > 2) throw new InvalidDataException("Несовместимое сохранение кампании.");
            int pairs = nations * nations;
            if (s.Relations == null || s.Relations.Length != pairs || s.Wars == null || s.Wars.Length != pairs || s.Embargo == null || s.Embargo.Length != pairs
                || s.Agreements == null || s.Agreements.Length != pairs || s.Relations.Any(x => x < -100 || x > 100)) throw new InvalidDataException("Повреждена дипломатия.");
            for (int i = 0; i < nations; i++) for (int j = 0; j < nations; j++)
                if (s.Wars[i * nations + j] != s.Wars[j * nations + i] || s.Agreements[i * nations + j] != s.Agreements[j * nations + i]
                    || (i == j && (s.Wars[i * nations + j] || s.Agreements[i * nations + j]))) throw new InvalidDataException("Несогласованная дипломатия.");
            if (s.Version >= 3) {
                if (s.Access == null || s.Access.Length != pairs || s.Alliances == null || s.Alliances.Length != pairs
                    || s.WarStarted == null || s.WarStarted.Length != pairs || s.TruceUntil == null || s.TruceUntil.Length != pairs)
                    throw new InvalidDataException("Повреждены договоры.");
                for (int i = 0; i < nations; i++) for (int j = 0; j < nations; j++) {
                    int k = i * nations + j, reverse = j * nations + i;
                    if (s.Alliances[k] != s.Alliances[reverse] || s.WarStarted[k] != s.WarStarted[reverse] || s.TruceUntil[k] != s.TruceUntil[reverse]
                        || s.WarStarted[k] < 0 || s.WarStarted[k] > s.Day || s.TruceUntil[k] < 0 || s.TruceUntil[k] > s.Day + 180
                        || (i == j && (s.Alliances[k] || s.Access[k])) || (s.Wars[k] && (s.Alliances[k] || s.Access[k] || s.TruceUntil[k] > s.Day)))
                        throw new InvalidDataException("Несогласованные договоры.");
                }
            }
            foreach (var p in s.Provinces)
                if (p == null || p.Owner < 0 || p.Owner >= nations || !Values(p.Public, 8) || !Values(p.Private, 8)
                    || (s.Version >= 4 && (p.Airfield < 0 || p.Airfield > 3)) || !IsFinite(p.Population) || p.Population < 0 || !IsFinite(p.Infrastructure) || p.Infrastructure < 0 || p.Infrastructure > 100
                    || !IsFinite(p.Unrest) || !IsFinite(p.Soil) || !IsFinite(p.Oil) || !IsFinite(p.Ore)) throw new InvalidDataException("Повреждены регионы.");
            foreach (var n in s.Nations) {
                if (n == null || !Values(n.Stock, 8) || !Values(n.Price, 8) || !Values(n.Production, 8) || !Values(n.Demand, 8) || !Values(n.Consumed, 8)
                    || n.Tech == null || n.Tech.Length != 6 || n.Tech.Any(v => v < 0 || v > 10000) || n.Research < -1 || n.Research >= 6
                    || !new[] { n.Treasury, n.GDP, n.Debt, n.BaseGDP, n.Population, n.Stability, n.Approval, n.Inflation, n.Unemployment, n.Living,
                        n.Education, n.Tax, n.Social, n.Science, n.Military, n.Interest, n.Repression, n.PrivateCapital, n.Revenue, n.Spending,
                        n.TradeBalance, n.Imports, n.Exports, n.Shortage, n.LastPrice, n.ResearchProgress, n.ProjectProgress }.All(IsFinite)
                    || n.History == null || n.StabilityHistory == null || n.History.Any(v => !IsFinite(v)) || n.StabilityHistory.Any(v => !IsFinite(v))
                    || EquipmentRules.Validate(n.Design) != null) throw new InvalidDataException("Повреждена экономика или проект.");
                if (s.Version >= 6) ValidateNationalPolitics(n, s);
                if (s.Version >= 7) ValidateTechnology(n, s.Version);
                if (s.Version >= 9) ValidateDoctrines(n, s);
                if (s.Version >= 10) ValidateGovernment(n, s);
                if (s.Version >= 5) {
                    if (!Enum.IsDefined(typeof(ConscriptionLaw), n.Conscription) || n.Recruits < 0 || n.Recruits > 1000000000 || n.LawChangedDay < -90 || n.LawChangedDay > s.Day
                        || n.Mobilized < 0 || n.Discharged < 0 || n.PersonnelLosses < 0 || n.Mobilized > 1000000000000000L || n.Discharged > n.Mobilized || n.PersonnelLosses > n.Mobilized)
                        throw new InvalidDataException("Повреждён учёт личного состава.");
                    if (n.Templates == null || n.Templates.Count < 1 || n.Templates.Count > 24 || n.Templates.Any(t => t == null || t.Id < 1 || t.Battalions < 1 || t.Battalions > 6
                            || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 32 || t.Name.Any(char.IsControl))
                        || n.Templates.Select(t => t.Id).Distinct().Count() != n.Templates.Count || n.NextTemplate <= n.Templates.Max(t => t.Id)
                        || !n.Templates.Any(t => t.Id == n.ActiveTemplate)) throw new InvalidDataException("Повреждены штаты соединений.");
                }
                if (s.Version == 1) continue;
                if (n.Designs == null || n.Designs.Count < 1 || n.Designs.Count > 128 || n.Lines == null || n.Lines.Count > 64
                    || n.Designs.Any(d => d == null || d.Id <= 0 || d.Stock < 0 || EquipmentRules.Validate(d.Blueprint) != null)
                    || n.Designs.Select(d => d.Id).Distinct().Count() != n.Designs.Count || n.NextDesign <= n.Designs.Max(d => d.Id)
                    || !n.Designs.Any(d => d.Id == n.ActiveDesign && SameDesign(d.Blueprint, n.Design))) throw new InvalidDataException("Повреждён каталог техники.");
                if (n.Lines.Any(l => l == null || l.Id <= 0 || !n.Designs.Any(d => d.Id == l.DesignId) || l.Factories < 1 || l.Factories > 500 || l.Quantity < 1 || l.Quantity > 1000
                    || l.Completed < 0 || l.Completed > l.Quantity || !IsFinite(l.Progress) || l.Progress < 0 || l.Progress >= 1 || (l.Completed == l.Quantity && l.Progress > 1e-8)
                    || !IsFinite(l.Efficiency) || l.Efficiency < 0.25 || l.Efficiency > 1)
                    || n.Lines.Select(l => l.Id).Distinct().Count() != n.Lines.Count || n.NextLine <= n.Lines.Select(l => l.Id).DefaultIfEmpty(0).Max()) throw new InvalidDataException("Повреждены производственные линии.");
            }
            if (s.Armies == null || s.Projects == null || s.News == null || s.News.Count > 100 || s.NextArmy <= 0) throw new InvalidDataException("Повреждены списки кампании.");
            if (s.Armies.Any(a => a == null) || s.Armies.Select(a => a.Id).Distinct().Count() != s.Armies.Count) throw new InvalidDataException("Повторяются номера частей.");
            foreach (var a in s.Armies) {
                if (a.Id < 1 || a.Id >= s.NextArmy || a.Owner < 0 || a.Owner >= nations || a.Region < 0 || a.Region >= regions || a.Target < -1 || a.Target >= regions
                    || a.Route == null || a.Route.Any(r => r < 0 || r >= regions) || !new[] { a.Days, a.TotalDays, a.Strength, a.Supply, a.Experience, a.SupplyDays }.All(IsFinite)
                    || a.Strength < 0 || a.Strength > 100 || a.Supply < 0 || a.Supply > 100 || a.TrainingDays < 0 || a.TrainingDays > (s.Version >= 5 ? 60 : 30)
                    || EquipmentRules.Validate(a.Design) != null || !a.Design.Formation) throw new InvalidDataException("Повреждены армии.");
                if (s.Version == 1) continue;
                var n = s.Nations[a.Owner];
                if (!n.Designs.Any(d => d.Id == a.DesignId && SameDesign(d.Blueprint, a.Design)) || a.Attachments == null || a.Attachments.Count > 3
                    || a.Attachments.Any(t => t == null || EquipmentRules.Validate(t.Blueprint) != null || !IsFinite(t.Condition) || t.Condition < 0 || t.Condition > 100
                        || t.Blueprint.Kind == EquipmentKind.Tank || t.Blueprint.Kind == EquipmentKind.IFV || !n.Designs.Any(d => d.Id == t.DesignId && SameDesign(d.Blueprint, t.Blueprint)))
                    || a.Attachments.Select(t => t.Blueprint.Kind).Distinct().Count() != a.Attachments.Count) throw new InvalidDataException("Повреждено оснащение частей.");
                if (s.Version >= 5 && (a.Battalions < 1 || a.Battalions > 6 || a.Personnel < 0 || a.Personnel > ManpowerRules.Establishment(a)
                    || !IsFinite(a.Organization) || a.Organization < 0 || a.Organization > 100 || !IsFinite(a.CasualtyRemainder) || a.CasualtyRemainder < 0 || a.CasualtyRemainder >= 1
                    || !n.Templates.Any(t => t.Id == a.TemplateId && t.Battalions == a.Battalions))) throw new InvalidDataException("Повреждён штат или личный состав части.");
            }
            if (s.Version >= 3) {
                if (s.Fronts == null || s.Fronts.Count > nations * 24 || s.Fronts.Any(p => p == null) || s.NextFront < 1
                    || s.Fronts.Select(p => p.Id).Distinct().Count() != s.Fronts.Count || s.Fronts.GroupBy(p => p.Owner).Any(g => g.Count() > 24)) throw new InvalidDataException("Повреждены фронты.");
                var assigned = new System.Collections.Generic.HashSet<int>();
                foreach (var p in s.Fronts) {
                    if (p.Id < 1 || p.Id >= s.NextFront || p.Owner < 0 || p.Owner >= nations || p.Enemy < 0 || p.Enemy >= nations || p.Owner == p.Enemy
                        || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 100 || !Enum.IsDefined(typeof(FrontStance), p.Stance)
                        || p.ArmyIds == null || (p.Active && !s.Wars[p.Owner * nations + p.Enemy])
                        || p.ArmyIds.Any(id => !assigned.Add(id) || !s.Armies.Any(a => a.Id == id && a.Owner == p.Owner)))
                        throw new InvalidDataException("Некорректный приказ фронту.");
                }
            }
            if (s.Version >= 4) {
                if (s.AirWings == null || s.AirWings.Count > nations * 48 || s.AirWings.Any(a => a == null) || s.NextAirWing < 1
                    || s.AirWings.Select(a => a.Id).Distinct().Count() != s.AirWings.Count || s.AirWings.GroupBy(a => a.Owner).Any(g => g.Count() > 48)) throw new InvalidDataException("Повреждены авиакрылья.");
                foreach (var a in s.AirWings) {
                    if (a.Id < 1 || a.Id >= s.NextAirWing || a.Owner < 0 || a.Owner >= nations || a.BaseRegion < 0 || a.BaseRegion >= regions
                        || a.Target < -1 || a.Target >= regions || a.TrainingDays < 0 || a.TrainingDays > 21 || a.Sorties < 0
                        || !IsFinite(a.Strength) || a.Strength < 0 || a.Strength > 100 || EquipmentRules.Validate(a.Design) != null || !a.Design.Air
                        || !Enum.IsDefined(typeof(AirMission), a.Mission) || !AirMissionCompatible(a.Design, a.Mission)
                        || (a.Mission == AirMission.Reserve ? a.Target != -1 : a.Target < 0)
                        || s.Provinces[a.BaseRegion].Airfield < 1
                        || !s.Nations[a.Owner].Designs.Any(d => d.Id == a.DesignId && SameDesign(d.Blueprint, a.Design))) throw new InvalidDataException("Повреждены приказы или оснащение авиации.");
                    if (s.Version >= 5 && (a.Personnel < 0 || a.Personnel > ManpowerRules.AirSize(a.Design.Kind) || !IsFinite(a.CasualtyRemainder) || a.CasualtyRemainder < 0 || a.CasualtyRemainder >= 1)) throw new InvalidDataException("Повреждены экипажи.");
                }
                if (s.AirWings.GroupBy(a => a.BaseRegion).Any(g => g.Count() > s.Provinces[g.Key].Airfield * 2)) throw new InvalidDataException("Превышена вместимость аэродрома.");
            }
            if (s.Version >= 5) {
                var serving = new long[nations];
                foreach (var a in s.Armies) serving[a.Owner] += a.Personnel;
                foreach (var a in s.AirWings) serving[a.Owner] += a.Personnel;
                for (int i = 0; i < nations; i++) {
                    var n = s.Nations[i];
                    if (n.Mobilized - n.Discharged != n.Recruits + serving[i] + n.PersonnelLosses) throw new InvalidDataException("Нарушен баланс резерва, военнослужащих и потерь.");
                }
            }
            foreach (var p in s.Projects)
                if (p == null || p.Owner < 0 || p.Owner >= nations || p.Region < 0 || p.Region >= regions || p.Sector < 0 || p.Sector > (s.Version >= 4 ? 9 : 8) || p.Days <= 0 || p.Total < p.Days || (p.Sector == 9 && p.Private))
                    throw new InvalidDataException("Повреждена очередь строительства.");
        }
    }
}
