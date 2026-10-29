using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Sovereign
{
    public partial class World
    {
        private static string[][] ScenarioRows(string resource)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using (var reader = new StreamReader(input)) {
                reader.ReadLine(); var rows = new System.Collections.Generic.List<string[]>();
                while (!reader.EndOfStream) { string line = reader.ReadLine(); if (line.Length > 0) rows.Add(line.Split('\t')); }
                return rows.ToArray();
            }
        }
        private void LoadScenario()
        {
            var culture = CultureInfo.InvariantCulture;
            var countries = ScenarioRows("scenario-countries.tsv");
            var regions = ScenarioRows("scenario-regions.tsv");
            if (countries.Length != Countries.Length || regions.Length != Regions.Length) throw new InvalidDataException("Несовместимые стартовые данные мира.");
            for (int i = 0; i < countries.Length; i++) {
                var row = countries[i]; var c = Countries[i];
                if (row[0] != c.Code) throw new InvalidDataException("Нарушен порядок стран в сценарии.");
                c.Population = double.Parse(row[1], culture); c.PopulationYear = int.Parse(row[2], culture);
                c.ReferenceMilitary = int.Parse(row[3], culture); c.StartingGroundPersonnel = int.Parse(row[4], culture);
                c.MilitaryYear = int.Parse(row[5], culture); c.GroundStrengthEstimated = row[6] != "reported ground strength" && row[6] != "no standing army";
                c.CapitalRegion = int.Parse(row[7], culture);
            }
            foreach (var row in ScenarioRows("scenario-military-quality.tsv")) {
                var c = Countries.First(country => country.Code == row[0]);
                c.TrainingBaseline = double.Parse(row[1], culture); c.ReferenceFundingPerPerson = double.Parse(row[2], culture);
                c.FundingYear = int.Parse(row[3], culture); c.FundingGdpYear = int.Parse(row[4], culture);
            }
            for (int i = 0; i < regions.Length; i++) {
                var row = regions[i]; var r = Regions[i];
                if (int.Parse(row[0], culture) != i || row[1] != Countries[r.Country].Code) throw new InvalidDataException("Нарушен порядок областей в сценарии.");
                r.Population = double.Parse(row[2], culture); r.X = double.Parse(row[3], culture); r.Y = -double.Parse(row[4], culture);
                r.Settlement = row[5]; r.PopulationBasis = row[6];
                if (i == 78) r.Name = "Киевская область";
            }
            foreach (var row in ScenarioRows("scenario-adjacency.tsv")) {
                int a = int.Parse(row[0], culture), b = int.Parse(row[1], culture);
                if (Regions[a].Country != Regions[b].Country) throw new InvalidDataException("Некорректная связь анклава.");
                Regions[a].Neighbors = Regions[a].Neighbors.Concat(new[] {b}).Distinct().ToArray();
                Regions[b].Neighbors = Regions[b].Neighbors.Concat(new[] {a}).Distinct().ToArray();
            }
        }
    }
}
