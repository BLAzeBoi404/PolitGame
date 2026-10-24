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
            if (s == null || (s.Version != 1 && s.Version != 2) || s.Nations == null || s.Nations.Length != nations || s.Provinces == null || s.Provinces.Length != regions
                || s.Player < 0 || s.Player >= nations || s.StartYear < 1940 || s.StartYear > 2026 || s.Day < 0 || s.Day > 2000000 || s.RandomState == 0
                || s.Difficulty < 0 || s.Difficulty > 2) throw new InvalidDataException("Несовместимое сохранение кампании.");
            int pairs = nations * nations;
            if (s.Relations == null || s.Relations.Length != pairs || s.Wars == null || s.Wars.Length != pairs || s.Embargo == null || s.Embargo.Length != pairs
                || s.Agreements == null || s.Agreements.Length != pairs || s.Relations.Any(x => x < -100 || x > 100)) throw new InvalidDataException("Повреждена дипломатия.");
            for (int i = 0; i < nations; i++) for (int j = 0; j < nations; j++)
                if (s.Wars[i * nations + j] != s.Wars[j * nations + i] || s.Agreements[i * nations + j] != s.Agreements[j * nations + i]
                    || (i == j && (s.Wars[i * nations + j] || s.Agreements[i * nations + j]))) throw new InvalidDataException("Несогласованная дипломатия.");
            foreach (var p in s.Provinces)
                if (p == null || p.Owner < 0 || p.Owner >= nations || !Values(p.Public, 8) || !Values(p.Private, 8)
                    || !IsFinite(p.Population) || p.Population < 0 || !IsFinite(p.Infrastructure) || p.Infrastructure < 0 || p.Infrastructure > 100
                    || !IsFinite(p.Unrest) || !IsFinite(p.Soil) || !IsFinite(p.Oil) || !IsFinite(p.Ore)) throw new InvalidDataException("Повреждены регионы.");
            foreach (var n in s.Nations) {
                if (n == null || !Values(n.Stock, 8) || !Values(n.Price, 8) || !Values(n.Production, 8) || !Values(n.Demand, 8) || !Values(n.Consumed, 8)
                    || n.Tech == null || n.Tech.Length != 6 || n.Tech.Any(v => v < 0 || v > 10000) || n.Research < -1 || n.Research >= 6
                    || !new[] { n.Treasury, n.GDP, n.Debt, n.BaseGDP, n.Population, n.Stability, n.Approval, n.Inflation, n.Unemployment, n.Living,
                        n.Education, n.Tax, n.Social, n.Science, n.Military, n.Interest, n.Repression, n.PrivateCapital, n.Revenue, n.Spending,
                        n.TradeBalance, n.Imports, n.Exports, n.Shortage, n.LastPrice, n.ResearchProgress, n.ProjectProgress }.All(IsFinite)
                    || n.History == null || n.StabilityHistory == null || n.History.Any(v => !IsFinite(v)) || n.StabilityHistory.Any(v => !IsFinite(v))
                    || EquipmentRules.Validate(n.Design) != null) throw new InvalidDataException("Повреждена экономика или проект.");
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
                    || a.Strength < 0 || a.Strength > 100 || a.Supply < 0 || a.Supply > 100 || a.TrainingDays < 0 || a.TrainingDays > 30
                    || EquipmentRules.Validate(a.Design) != null || !a.Design.Formation) throw new InvalidDataException("Повреждены армии.");
                if (s.Version == 1) continue;
                var n = s.Nations[a.Owner];
                if (!n.Designs.Any(d => d.Id == a.DesignId && SameDesign(d.Blueprint, a.Design)) || a.Attachments == null || a.Attachments.Count > 3
                    || a.Attachments.Any(t => t == null || EquipmentRules.Validate(t.Blueprint) != null || !IsFinite(t.Condition) || t.Condition < 0 || t.Condition > 100
                        || t.Blueprint.Kind == EquipmentKind.Tank || t.Blueprint.Kind == EquipmentKind.IFV || !n.Designs.Any(d => d.Id == t.DesignId && SameDesign(d.Blueprint, t.Blueprint)))
                    || a.Attachments.Select(t => t.Blueprint.Kind).Distinct().Count() != a.Attachments.Count) throw new InvalidDataException("Повреждено оснащение частей.");
            }
            foreach (var p in s.Projects)
                if (p == null || p.Owner < 0 || p.Owner >= nations || p.Region < 0 || p.Region >= regions || p.Sector < 0 || p.Sector > 8 || p.Days <= 0 || p.Total < p.Days)
                    throw new InvalidDataException("Повреждена очередь строительства.");
        }
    }
}
