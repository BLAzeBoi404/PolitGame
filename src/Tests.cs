using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Sovereign
{
	internal static class Tests
	{
		private static List<string> report = new List<string>();

		private static int failures;

		private static void Check(bool pass, string name)
		{
			report.Add((pass ? "PASS " : "FAIL ") + name);
			if (!pass)
			{
				failures++;
			}
		}

		private static bool Finite(double d)
		{
			if (!double.IsNaN(d))
			{
				return !double.IsInfinity(d);
			}
			return false;
		}

		public static int Run(World w, string path)
		{
			report.Clear(); failures = 0;
			Stopwatch stopwatch = Stopwatch.StartNew();
			Engine e = new Engine(w);
			e.AutoSaveEnabled = false;
			e.New(w.ByCode("UKR"), 2026, 1);
			Check(w.Countries.Length >= 170 && w.Regions.Length > 4000, "Real geography loads: countries and administrative regions");
			Check(w.Regions.SelectMany((RegionInfo r, int i) => r.Neighbors.Select((int n) => new int[2] { i, n })).All((int[] p) => w.Regions[p[1]].Neighbors.Contains(p[0])), "Region adjacency is symmetric");
			Check(w.Countries.Select((CountryInfo c, int i) => Math.Abs(c.Population - w.Regions.Where((RegionInfo r) => r.Country == i).Sum((RegionInfo r) => r.Population))).Max() < 1.0, "Allocated regional population conserves country population");
			double treasury = e.Player.Treasury;
			int num = w.Countries[e.S.Player].Regions[0];
			double num2 = e.S.Provinces[num].Public[0];
			Check(e.Build(num, 0) == null && Math.Abs(e.Player.Treasury - (treasury - Rules.BuildCost[0])) < 1E-05, "Construction charges treasury exactly once");
			int days = e.S.Projects[0].Days;
			for (int num3 = 0; num3 < days; num3++)
			{
				e.Tick();
			}
			Check(Math.Abs(e.S.Provinces[num].Public[0] - num2 - 1.0) < 1E-05, "Construction completes and adds capacity");
			int other = w.ByCode("POL");
			Check(e.Build(w.Countries[other].Regions[0], 0) != null, "Player cannot build in another state");
			e.Player.Treasury = 0.0;
			Check(e.Build(num, 1) != null, "Insufficient funds reject construction");
			e.Player.Treasury = 10.0;
			Check(e.StartResearch(0) == null && e.StartResearch(1) != null, "Research accepts one active program");
			int num4 = e.Player.Tech[0];
			e.Player.ResearchProgress = 99.0;
			e.Player.Science = 10.0;
			while (e.S.Day % 7 != 6)
			{
				e.Tick();
			}
			e.Tick();
			Check(e.Player.Tech[0] > num4, "Funded research completes");
			Army army = e.S.Armies.First((Army x) => x.Owner == e.S.Player);
			int num5 = w.Regions[army.Region].Neighbors.FirstOrDefault((int r) => e.S.Provinces[r].Owner == e.S.Player);
			if (num5 != army.Region && e.S.Provinces[num5].Owner == e.S.Player)
			{
				Check(e.Order(army.Id, num5) == null, "Adjacent friendly movement receives route");
				int num6 = 0;
				while (army.Target >= 0 && num6++ < 500)
				{
					e.Tick();
				}
				Check(army.Region == num5, "Army arrives at ordered destination");
			}
			Check(e.Order(army.Id, w.Countries[other].Regions[0]) != null, "Neutral borders block movement");
			Engine combat = new Engine(w);
			combat.AutoSaveEnabled = false;
			combat.New(w.ByCode("UKR"), 2026, 0);
			combat.Diplomacy(other, 3);
			int num7 = w.Countries[combat.S.Player].Regions.First((int r) => w.Regions[r].Neighbors.Any((int q) => combat.S.Provinces[q].Owner == other));
			int target = w.Regions[num7].Neighbors.First((int q) => combat.S.Provinces[q].Owner == other);
			foreach (var removed in combat.S.Armies.Where(x => x.Region == target).ToList()) combat.DestroyArmy(removed);
			Army army2 = combat.S.Armies.First((Army x) => x.Owner == combat.S.Player);
			army2.Region = num7;
			army2.Strength = 100.0;
			combat.Player.Stock[2] = 100.0;
			Check(combat.Order(army2.Id, target) == null, "War opens adjacent enemy territory for movement");
			for (int num8 = 0; num8 < 100; num8++)
			{
				if (combat.S.Provinces[target].Owner == combat.S.Player)
				{
					break;
				}
				combat.Tick();
			}
			Check(combat.S.Provinces[target].Owner == combat.S.Player, "Arrival captures undefended enemy region");
			Army army3 = new Army();
			army3.Id = combat.S.NextArmy++;
			army3.Owner = other;
			army3.Region = target;
			army3.Strength = 100.0;
			army3.Design = new Blueprint();
			Army item = army3;
			combat.S.Armies.Add(item); combat.S.Nations[other].Mobilized += item.Personnel;
			army2.Region = num7;
			army2.Target = -1;
			army2.Strength = 100.0;
			combat.Order(army2.Id, target);
			for (int num9 = 0; num9 < 100; num9++)
			{
				if (army2.Strength != 100.0)
				{
					break;
				}
				combat.Tick();
			}
			Check(army2.Strength < 100.0, "Combat and field wear reduce army strength");
			double population = combat.S.Provinces[num7].Population;
			combat.Player.Tech[4] = 10;
			combat.Player.Social = 10.0;
			combat.Player.Stock[0] = 100000.0;
			for (int num10 = 0; num10 < 14; num10++)
			{
				combat.Tick();
			}
			Check(combat.S.Provinces[num7].Population > population, "Medicine and social spending support demographic growth");
			Check(e.Diplomacy(other, 3) == null && e.AtWar(e.S.Player, other) && e.AtWar(other, e.S.Player), "War is bilateral");
			Check(e.Diplomacy(other, 4) == null && !e.AtWar(e.S.Player, other), "Peace clears war state");
			e.Diplomacy(other, 0);
			e.Diplomacy(other, 0);
			e.Diplomacy(other, 0);
			Check(e.Diplomacy(other, 1) == null && e.S.Agreements[e.Pair(other, e.S.Player)], "Trade agreement is bilateral");
			Blueprint blueprint = new Blueprint();
			blueprint.Armor = 6;
			blueprint.Engine = 2;
			blueprint.Gun = 4;
			blueprint.Electronics = 3;
			Blueprint blueprint2 = blueprint;
			Blueprint blueprint3 = new Blueprint();
			blueprint3.Armor = 1;
			blueprint3.Engine = 2;
			blueprint3.Gun = 4;
			blueprint3.Electronics = 3;
			Blueprint blueprint4 = blueprint3;
			Check(blueprint2.Speed < blueprint4.Speed && blueprint2.Cost > blueprint4.Cost && blueprint2.Fuel > blueprint4.Fuel, "Tank design trades armor against speed, fuel and cost");
			string path2 = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "test-campaign.xml");
			e.Save(path2);
			Engine engine = new Engine(w);
			engine.AutoSaveEnabled = false;
			engine.Load(path2);
			Check(engine.S.Day == e.S.Day && engine.Player.Treasury == e.Player.Treasury && engine.S.RandomState == e.S.RandomState, "Save/load preserves state and RNG");
			for (int num11 = 0; num11 < 35; num11++)
			{
				e.Tick();
				engine.Tick();
			}
			Check(engine.Player.GDP == e.Player.GDP && engine.S.RandomState == e.S.RandomState, "Simulation remains deterministic across save/load");
			for (int num12 = 0; num12 < 730; num12++)
			{
				e.Tick();
			}
			Check(e.S.Nations.All((Nation n) => Finite(n.GDP) && Finite(n.Treasury) && Finite(n.Inflation) && n.Stock.All((double s) => Finite(s) && s >= -1E-05)), "Two-year simulation: finite finances and nonnegative stocks");
			Check(e.S.Provinces.All((Province p) => p.Owner >= 0 && p.Owner < w.Countries.Length && p.Public.All((double c) => c >= 0.0) && p.Private.All((double c) => c >= 0.0)), "Two-year simulation: valid ownership and capacities");
			Check(e.S.Nations.Where((Nation n, int i) => i != e.S.Player).Any((Nation n) => n.Tech.Sum() > 24), "Foreign AI researches technologies");
			Check(e.S.Nations.Any((Nation n) => n.Imports > 0.0) && e.S.Nations.Any((Nation n) => n.Exports > 0.0), "World market matches real imports and exports");
			e.Validate(e.S);
			Check(pass: true, "Final save state passes validation");
			EquipmentTests.Run(w, Path.GetDirectoryName(Path.GetFullPath(path)), Check);
            CampaignSystemsTests.Run(w, Path.GetDirectoryName(Path.GetFullPath(path)), Check);
            AviationTests.Run(w, Path.GetDirectoryName(Path.GetFullPath(path)), Check);
            ManpowerTests.Run(w, Path.GetDirectoryName(Path.GetFullPath(path)), Check);
            NationalPolicyTests.Run(w, Path.GetDirectoryName(Path.GetFullPath(path)), Check);
            ResearchTests.Run(w, Path.GetDirectoryName(Path.GetFullPath(path)), Check);
            ExpandedResearchTests.Run(w, Path.GetDirectoryName(Path.GetFullPath(path)), Check);
			report.Add("Simulated days: " + e.S.Day + "; wall seconds: " + stopwatch.Elapsed.TotalSeconds.ToString("0.00"));
			report.Add("Countries: " + w.Countries.Length + "; regions: " + w.Regions.Length);
			report.Add("Failures: " + failures);
			File.WriteAllLines(path, report, Encoding.UTF8);
            Console.WriteLine(string.Join(Environment.NewLine, report));
			if (failures != 0)
			{
				return 2;
			}
			return 0;
		}
	}
}
