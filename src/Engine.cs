using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Sovereign
{
	public partial class Engine
	{
		public World World;

		public Campaign S;

		public bool AutoSaveEnabled = true;

		public Nation Player => S.Nations[S.Player];

		public static string Saves => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saves");

		public event Action Changed;

		public Engine(World world)
		{
			World = world;
		}

		public int Pair(int a, int b)
		{
			return a * S.Nations.Length + b;
		}

		public bool AtWar(int a, int b)
		{
			if (a != b)
			{
				return S.Wars[Pair(a, b)];
			}
			return false;
		}

		public void Notify(string text, int type = 0)
		{
			S.News.Insert(0, new Bulletin
			{
				Date = S.Date.ToString("dd.MM.yyyy"),
				Text = text,
				Type = type
			});
			if (S.News.Count > 100)
			{
				S.News.RemoveAt(100);
			}
		}

        // Initial capacity is an estimate, quantized to a millionth of a factory.
        // Canonicalize using the save codec as Mono's short-decimal parser can differ
        // by one ULP from Math.Round; repeated save/load must not alter the simulation.
        private static double InitialFactoryCapacity(double value) => System.Xml.XmlConvert.ToDouble(System.Xml.XmlConvert.ToString(Math.Round(value, 6)));

		public void New(int player, int year, int difficulty)
		{
			S = new Campaign
			{
				Player = player,
				StartYear = year,
				Difficulty = difficulty
			};
			int num = World.Countries.Length;
			S.Nations = new Nation[num];
			S.Provinces = new Province[World.Regions.Length];
			S.Relations = new int[num * num];
			S.Wars = new bool[num * num];
			S.Embargo = new bool[num * num];
			S.Agreements = new bool[num * num];
			double num2 = Math.Pow(1.025, year - 2026);
			for (int i = 0; i < num; i++)
			{
				CountryInfo countryInfo = World.Countries[i];
				double num3 = Math.Max(0.8, countryInfo.GDP / 1000.0 * num2);
				Nation nation = new Nation();
				nation.GDP = num3;
				nation.BaseGDP = num3;
				nation.Population = countryInfo.Population / 1000000.0;
				nation.Treasury = num3 * 0.12;
				nation.Debt = num3 * 0.45;
				nation.PrivateCapital = num3 * 0.2;
				Nation nation2 = nation;
				nation2.Education = Rules.Clamp(25.0 + countryInfo.GDP * 1000000.0 / countryInfo.Population / 900.0, 25.0, 92.0);
				nation2.Living = Rules.Clamp(25.0 + countryInfo.GDP * 1000000.0 / countryInfo.Population / 1000.0, 20.0, 90.0);
				nation2.Stability = 55.0 + S.Random() * 22.0;
				for (int j = 0; j < 6; j++)
				{
					nation2.Tech[j] = Math.Max(0, (year - 1940) / 20);
				}
				for (int k = 0; k < 8; k++)
				{
					nation2.Price[k] = Rules.Prices[k];
					nation2.Stock[k] = Math.Max(2.0, nation2.Population) * (new double[8] { 8.0, 5.0, 4.0, 4.0, 3.0, 1.5, 5.0, 1.0 })[k];
				}
				nation2.Design = new Blueprint
				{
					Name = "Серия " + year,
					Armor = 2,
					Gun = 2,
					Engine = 2,
					Electronics = 1
				};
				nation2.History.Add(num3);
				nation2.StabilityHistory.Add(nation2.Stability);
				S.Nations[i] = nation2;
			}
			for (int l = 0; l < World.Regions.Length; l++)
			{
				RegionInfo regionInfo = World.Regions[l];
				Nation nation3 = S.Nations[regionInfo.Country];
				double num4 = regionInfo.Population / 1000000.0;
				double num5 = Math.Abs(regionInfo.Y);
				Province province = new Province();
				province.Owner = regionInfo.Country;
				province.Population = num4;
				province.Infrastructure = Rules.Clamp(nation3.Education * 0.8 + S.Random() * 20.0, 20.0, 95.0);
				province.Soil = Rules.Clamp(1.3 - num5 / 100.0 + S.Random() * 0.3, 0.2, 1.4);
				province.Oil = 0.25 + S.Random() * 0.8;
				province.Ore = 0.5 + S.Random();
				Province province2 = province;
				string code = World.Countries[regionInfo.Country].Code;
				if (new string[13]
				{
					"SAU", "RUS", "IRN", "IRQ", "USA", "CAN", "ARE", "KWT", "VEN", "NOR",
					"QAT", "KAZ", "DZA"
				}.Contains(code))
				{
					province2.Oil *= 2.2;
				}
				if (new string[7] { "AUS", "BRA", "CHN", "RUS", "ZAF", "CAN", "IND" }.Contains(code))
				{
					province2.Ore *= 1.6;
				}
				double num6 = Rules.Clamp(nation3.Education / 65.0, 0.35, 1.4) * Math.Max(0.25, num2);
				for (int m = 0; m < 8; m++)
				{
					double num7 = num4 * (new double[8] { 0.52, 0.43, 0.2, 0.19, 0.22, 0.18, 0.36, 0.1 })[m] * ((m == 0) ? 1.0 : num6);
					province2.Public[m] = InitialFactoryCapacity(num7 * ((m == 1 || m == 7) ? 0.65 : 0.18));
					province2.Private[m] = InitialFactoryCapacity(num7 - province2.Public[m]);
				}
				S.Provinces[l] = province2;
			}
			for (int n = 0; n < num; n++)
			{
				for (int num8 = n + 1; num8 < num; num8++)
				{
					int num9 = (int)(S.Random() * 50.0) - 10;
					S.Relations[Pair(n, num8)] = (S.Relations[Pair(num8, n)] = num9);
				}

			}
			InitializeEquipment(S);
            InitializeTreaties(S);
            InitializeAviation(S);
            InitializeManpower(S);
            InitializeNationalPolitics(S);
            InitializeTechnology(S, false);
            InitializeDoctrines(S);
            InitializeGovernments(S);
            InitializeOperations(S);
            InitializeStartingForces();
            InitializeTrade(S);
            InitializeReadiness(S);
			Notify("Кабинет сформирован. Проверьте баланс ресурсов и выберите программу развития.");
			Notify("Учебная модель: современная география; региональная экономика и ресурсы оценочные. Исторические границы пока не включены.");
			ComputeEconomy(apply: false);
			Raise();
		}

		public static Blueprint CopyDesign(Blueprint d)
		{
            return d.Copy();
		}

		public void Raise()
		{
			if (this.Changed != null)
			{
				this.Changed();
			}
		}

		public void Tick()
		{
			S.Day++;
            TickNationalPolitics();
            TickDoctrines();
            TickGovernments();
			foreach (Construction item in S.Projects.ToList())
			{
				if (--item.Days > 0)
				{
					continue;
				}
				Province province = S.Provinces[item.Region];
				if (province.Owner == item.Owner)
				{
					if (item.Sector == 9) { province.Airfield = Math.Min(3, province.Airfield + 1); }
                    else if (item.Sector == 8)
					{
						province.Infrastructure = Math.Min(100.0, province.Infrastructure + 12.0);
					}
					else if (item.Private)
					{
						province.Private[item.Sector] += 1.0;
					}
					else
					{
						province.Public[item.Sector] += 1.0;
					}
					if (item.Owner == S.Player)
					{
						Notify((item.Private ? "Бизнес: " : "Завершено: ") + ((item.Sector == 9) ? "аэродром" : (item.Sector == 8) ? "транспортная инфраструктура" : Rules.Plants[item.Sector]) + " · " + World.Regions[item.Region].Name, 1);
					}
				}
				S.Projects.Remove(item);
			}
			TickProduction();
            TickContracts(); TickPublicFleets();
            bool[] network = SupplyNetwork();
            TickReadiness(network);
            TickManpower(network);
            TickAviation();
            if (S.Day % 7 == 0) ExecuteFronts();
			MoveArmies(network);
            PruneFronts();
			if (S.Day % 7 == 0)
			{
				ComputeEconomy(apply: true);
				Research();
				Politics();
                PlanMilitaryPreparation();
			}
			if (S.Day % 30 == 0)
			{
				AI();
                PlanAviation();
			}
			if (S.Day % 90 == 0 && AutoSaveEnabled)
			{
				Autosave();
			}
			Raise();
		}

		public void ComputeEconomy(bool apply)
		{
			int num = S.Nations.Length;
            var armyCount = new int[num]; var armyBudget = new double[num];
            foreach (var unit in S.Armies) {
                armyCount[unit.Owner] += unit.Battalions;
                var nation = S.Nations[unit.Owner];
                // Weekly personnel costs depend on national incomes. Equipment already
                // has a separate, actually paid daily maintenance bill.
                armyBudget[unit.Owner] += unit.Personnel * .00002 * Rules.Clamp(nation.GDP / Math.Max(.001,nation.Population) / 30, .25, 2) / 52;
            }
			double[,] array = new double[num, 8];
			double[] array2 = new double[num];
			double[] array3 = new double[num];
			var reserved = apply ? null : ContractReservations();
            Province[] provinces = S.Provinces;
			for (int provinceId = 0; provinceId < provinces.Length; provinceId++)
			{
                Province province = provinces[provinceId];
				int owner = province.Owner;
				array2[owner] += province.Infrastructure * province.Population;
				array3[owner] += province.Population;
				for (int j = 0; j < 8; j++)
				{
					ref double reference = ref array[owner, j];
					double num2 = reference;
					double num3 = province.Public[j] + Math.Max(0, province.Private[j] - (apply ? province.ContractWork[j] / 7 : reserved == null ? 0 : reserved[provinceId, j]));
					double num4;
					switch (j)
					{
					default:
						num4 = 1.0;
						break;
					case 3:
						num4 = province.Ore;
						break;
					case 2:
						num4 = province.Oil;
						break;
					case 0:
						num4 = province.Soil;
						break;
					}
					reference = num2 + num3 * num4 * (0.55 + ProcurementRules.Infrastructure(province) / 200.0);
				}
			}
			int c;
			for (c = 0; c < num; c++)
			{
				Nation nation = S.Nations[c];
                var policyEffects = GovernmentRules.CombinedEffects(nation, S.Day);
                var technologyEffects = TechnologyRules.Effects(nation);
				nation.Population = Math.Max(0.001, array3[c]);
				nation.Production = new double[8];
				nation.Demand = new double[8];
				nation.Consumed = new double[8];
				double num5 = 0.0;
				for (int k = 0; k < 8; k++)
				{
					num5 += array[c, k] * 0.11;
				}
				double num6 = nation.Population * 0.48;
				double num7 = num6 * Rules.Clamp(0.67 + nation.Living * 0.001, 0.67, 0.8);
				double num8 = Math.Min(1.0, Math.Max(0.0, num6 - num7) / Math.Max(0.001, num5));
				nation.Unemployment = Rules.Clamp(100.0 * (1.0 - (num5 + num7) / Math.Max(0.001, num6)), 2.0, 65.0);
				double[] array4 = (double[])nation.Stock.Clone();
				for (int l = 0; l < 8; l++)
				{
					int num9;
					switch (l)
					{
					default:
						num9 = 2;
						break;
					case 1:
						num9 = 1;
						break;
					case 0:
						num9 = 0;
						break;
					}
					int num10 = num9;
					double num11 = array[c, l] * Rules.Output[l] * num8 * (1.0 + (double)nation.Tech[num10] * 0.05) * (1.0 - nation.Repression * 0.0015) * ManpowerRules.Output(nation) * policyEffects.GoodsFactor * TechnologyRules.GoodsFactor(technologyEffects, l);
					double num12 = 1.0;
					for (int m = 0; m < 8; m++)
					{
						double num13 = Rules.Inputs[l, m] * TechnologyRules.InputFactor(technologyEffects, l, m) * num11;
						nation.Demand[m] += num13;
						if (num13 > 0.0)
						{
							num12 = Math.Min(num12, array4[m] / num13);
						}
					}
					num12 = Rules.Clamp(num12, 0.0, 1.0);
					num11 *= num12;
					for (int n = 0; n < 8; n++)
					{
						double num14 = Rules.Inputs[l, n] * TechnologyRules.InputFactor(technologyEffects, l, n) * num11;
						array4[n] = Math.Max(0.0, array4[n] - num14);
						nation.Consumed[n] += num14;
					}
					nation.Production[l] = num11;
				}
				double[] array5 = new double[8] { 0.72, 0.28, 0.13, 0.0, 0.0, 0.035, 0.36, 0.0 };
				double num15 = 0.0;
				for (int num16 = 0; num16 < 8; num16++)
				{
					array4[num16] += nation.Production[num16];
					double num17 = nation.Population * array5[num16] * ((num16 == 6) ? (0.6 + nation.Living / 100.0) : 1.0);
					if (num16 == 0 || num16 == 6) num17 *= policyEffects.ConsumptionFactor;
                    if (num16 == 0) num17 *= 1 + technologyEffects.HouseholdFood;
                    if (num16 == 7)
					{
						num17 += (double)armyCount[c] * 0.003;
					}
					nation.Demand[num16] += num17;
					double num18 = Math.Min(array4[num16], num17);
					array4[num16] -= num18;
					nation.Consumed[num16] += num18;
					if (num17 > 0.0 && (num16 == 0 || num16 == 1 || num16 == 6))
					{
						num15 += (num17 - num18) / num17 / 3.0;
					}
				}
				if (apply)
				{
					nation.Shortage = num15;
				}
				if (apply)
				{
					nation.Stock = array4;
				}
			}
            PrepareTradeNeeds();
			if (apply)
			{
                foreach (var province in S.Provinces) Array.Clear(province.ContractWork, 0, 8);
				SettleTradeWeek();
			}
			int c2;
			for (c2 = 0; c2 < num; c2++)
			{
				Nation nation2 = S.Nations[c2];
				double num19 = 0.0;
				double num20 = 0.0;
				for (int num21 = 0; num21 < 8; num21++)
				{
					num19 += nation2.Production[num21] * nation2.Price[num21];
					double num22 = Rules.Prices[num21] * Rules.Clamp((nation2.Demand[num21] + 0.3) / (nation2.Production[num21] + nation2.Stock[num21] * 0.09 + 0.3), 0.45, 3.5);
					if (apply)
					{
						nation2.Price[num21] = nation2.Price[num21] * 0.85 + num22 * 0.15;
					}
					num20 += nation2.Price[num21] / Rules.Prices[num21] / 8.0;
					if (apply)
					{
						nation2.Stock[num21] = Math.Min(nation2.Stock[num21], Math.Max(5.0, TradeNeed(c2,num21) * 52.0));
					}
				}
				if (apply)
				{
					double num23 = array3[c2] / Math.Max(0.001, World.Countries[c2].Population / 1000000.0);
					double num24 = nation2.BaseGDP * num23 * 0.65 * (0.65 + nation2.Living / 140.0) + num19 * 52.0;
					nation2.GDP = Math.Max(0.1, nation2.GDP * 0.97 + num24 * 0.03);
					nation2.Inflation = Rules.Clamp(nation2.Inflation * 0.85 + (num20 / Math.Max(0.1, nation2.LastPrice) - 1.0) * 5200.0 * 0.15, -8.0, 65.0);
					nation2.LastPrice = num20;
				}
				double num25 = nation2.GDP / 52.0;
				nation2.Revenue = num25 * (nation2.Tax / 100.0) * (0.82 - nation2.Repression * 0.001) * GovernmentRules.CombinedEffects(nation2, S.Day).RevenueFactor + nation2.Trade.Tariffs;
				nation2.Spending = num25 * (0.08 + (nation2.Social + nation2.Science + nation2.Military) / 100.0) + nation2.Debt * nation2.Interest / 100.0 / 52.0 + armyBudget[c2];
				nation2.TradeBalance = nation2.Exports - nation2.Imports;
				if (!apply)
				{
					continue;
				}
				nation2.Treasury += nation2.Revenue - nation2.Spending - nation2.Trade.Tariffs; // Tariffs were transferred by settlement, not newly created here.
				nation2.PrivateCapital += TradeRules.RetainedBusinessIncome(num25,nation2.Tax);
				if (nation2.Treasury < 0.0)
				{
					nation2.Debt -= nation2.Treasury;
					nation2.Treasury = 0.0;
				}
				if (nation2.Debt > nation2.GDP * 2.5)
				{
					double num26 = nation2.Debt * 0.006;
					nation2.Debt -= num26;
					nation2.Inflation = Math.Min(65.0, nation2.Inflation + 1.0);
					if (c2 == S.Player && S.Day % 28 == 0)
					{
						Notify("Долговой кризис: денежное финансирование повышает инфляцию.", 2);
					}
				}
				nation2.History.Add(nation2.GDP);
				nation2.StabilityHistory.Add(nation2.Stability);
				if (nation2.History.Count > 104)
				{
					nation2.History.RemoveAt(0);
				}
				if (nation2.StabilityHistory.Count > 104)
				{
					nation2.StabilityHistory.RemoveAt(0);
				}
			}
		}

		public string Build(int region, int sector, bool privateBuild = false)
		{
			if (region < 0 || region >= S.Provinces.Length || sector < 0 || sector > 8)
			{
				return "Выберите регион и отрасль.";
			}
			Province province = S.Provinces[region];
			int owner = province.Owner;
			Nation nation = S.Nations[owner];
			if (!privateBuild && owner != S.Player)
			{
				return "Строить можно только на своей территории.";
			}
			if (S.Projects.Count((Construction x) => x.Region == region) >= 4)
			{
				return "В регионе уже четыре стройки.";
			}
			if (sector == 8 && province.Infrastructure >= 99.0)
			{
				return "Инфраструктура уже развита.";
			}
			double num = ((sector == 8) ? 0.3 : Rules.BuildCost[sector]);
			if ((privateBuild ? nation.PrivateCapital : nation.Treasury) < num)
			{
				return "Недостаточно средств: нужно " + num.ToString("0.00") + " млрд.";
			}
			if (privateBuild)
			{
				nation.PrivateCapital -= num;
			}
			else
			{
				nation.Treasury -= num;
			}
			int num2 = Math.Max(1, (int)((double)((sector == 8) ? 90 : (60 + sector * 15)) / ((1.0 + (double)nation.Tech[3] * 0.06) * (1 + TechnologyRules.Effects(nation).Construction))));
			S.Projects.Add(new Construction
			{
				Owner = owner,
				Region = region,
				Sector = sector,
				Days = num2,
				Total = num2,
				Private = privateBuild
			});
			if (owner == S.Player)
			{
				Notify("Начата стройка: " + ((sector == 8) ? "Инфраструктура" : Rules.Plants[sector]) + " · " + World.Regions[region].Name + " · " + num2 + " дн.");
			}
			Raise();
			return null;
		}

		private void Politics()
		{
            var growthBonus = S.Nations.Select(n => TechnologyRules.Effects(n).PopulationGrowth).ToArray();
			for (int i = 0; i < S.Nations.Length; i++)
			{
				Nation nation = S.Nations[i];
				double num = Rules.Clamp(65.0 + nation.Social * 2.0 - nation.Tax * 0.5 - nation.Unemployment * 0.3 - nation.Shortage * 55.0 - Math.Max(0.0, nation.Inflation - 5.0) * 0.4 - nation.Repression * 0.16 - (int)nation.Conscription * 2 + GovernmentRules.CombinedEffects(nation, S.Day).Approval, 5.0, 95.0);
				nation.Living = Rules.Clamp(nation.Living + (num - nation.Living) * 0.025, 5.0, 95.0);
				nation.Approval += (num - nation.Approval) * 0.07;
				double num2 = Rules.Clamp(nation.Approval + GovernmentRules.StabilityTarget(nation) + nation.Repression * 0.22 - (double)(S.Wars.Skip(i * S.Nations.Length).Take(S.Nations.Length).Count((bool x) => x) * 4), 3.0, 96.0);
				nation.Stability += (num2 - nation.Stability) * 0.06;
				nation.Education = Rules.Clamp(nation.Education + (nation.Science - 1.0) * 0.015, 10.0, 98.0);
				if (nation.Stability < 26.0 && S.Random() < (26.0 - nation.Stability) / 150.0)
				{
					nation.Leader++;
					nation.Stability = 42.0;
					nation.Approval = 45.0;
					nation.Repression *= 0.6;
					if (i == S.Player)
					{
						Notify("Смена власти. Кабинет №" + nation.Leader + " принял управление страной. Партия продолжается.", 2);
					}
				}
				if (S.Day % 364 == 0)
				{
					nation.Leader++;
					if (i == S.Player)
					{
						Notify("Политический цикл завершён. Новый кабинет №" + nation.Leader + " сохраняет обязательства государства.");
					}
				}
			}
			Province[] provinces = S.Provinces;
			for (int provinceId = 0; provinceId < provinces.Length; provinceId++)
			{
                Province province = provinces[provinceId];
				Nation nation2 = S.Nations[province.Owner];
				province.Unrest = Rules.Clamp(100.0 - nation2.Stability - nation2.Repression * 0.15, 0.0, 100.0);
				double num4 = 0.002 + growthBonus[province.Owner] + (double)nation2.Tech[4] * 0.0006 + nation2.Social * 0.0002 - nation2.Shortage * 0.035 - Math.Max(0.0, nation2.Inflation - 15.0) * 0.0001;
				province.Population *= 1.0 + num4 / 52.0;
			}
			if (S.Day % 28 == 0 && Player.Shortage > 0.15)
			{
				Notify("Дефицит потребительских товаров снижает уровень жизни. Проверьте рынок и импорт.", 2);
			}
		}

		private void AI()
		{
			var controlled = new List<int>[S.Nations.Length];
            for (int i = 0; i < controlled.Length; i++) controlled[i] = new List<int>();
            for (int i = 0; i < S.Provinces.Length; i++) controlled[S.Provinces[i].Owner].Add(i);
            int c;
			for (c = 0; c < S.Nations.Length; c++)
			{
				Nation n = S.Nations[c];
				List<int> list = controlled[c];
				if (list.Count == 0)
				{
					continue;
				}
				int num = Math.Min(3, 1 + (int)(n.GDP / 300.0));
				for (int num2 = 0; num2 < num; num2++)
				{
					int sector = (from k in Enumerable.Range(0, 8)
						orderby (n.Demand[k] + 0.2) / (n.Production[k] + 0.2) * (n.Price[k] / Rules.Prices[k]) descending
						select k).First();
					if (!(n.Tax > 40.0) || !(S.Random() < 0.65))
					{
						int region = list[(int)(S.Random() * (double)list.Count)];
						Build(region, sector, privateBuild: true);
					}
				}
				foreach (int item in list)
				{
					Province province = S.Provinces[item];
					for (int num3 = 0; num3 < 8; num3++)
					{
						if (n.Price[num3] < Rules.Prices[num3] * 0.6 && province.Private[num3] > 0.1)
						{
							province.Private[num3] *= 0.98;
						}
					}
				}
				if (c == S.Player)
				{
					continue;
				}
				PlanNationalPolicy(c);
                PlanDoctrines(c);
                PlanGovernment(c);
                PlanManpower(c);
                EquipmentAI(c, list);
                PlanResearch(c);
				if (S.Difficulty > 0)
				{
					if (n.Revenue < n.Spending)
					{
						n.Tax = Math.Min(32.0, n.Tax + 1.0);
					}
					if (n.Stability < 40.0)
					{
						n.Social = Math.Min(10.0, n.Social + 0.5);
					}
				}
                StrategicOrders(c, list);
				if (S.Difficulty <= 0 || S.Day <= 180 || !(S.Random() < 0.012 * (double)S.Difficulty))
				{
					continue;
				}
				List<int> list4 = (from r in list.SelectMany((int r) => World.Regions[r].Neighbors)
					select S.Provinces[r].Owner into o
					where o != c && S.Relations[Pair(c, o)] < 0 && !AtWar(c, o) && !S.Agreements[Pair(c, o)]
					select o).Distinct().ToList();
				if (list4.Count > 0)
				{
					int num4 = list4[(int)(S.Random() * (double)list4.Count)];
					if (CanPrepareWar(c, num4))
					{
						DeclareWar(c, num4);
					}
				}
			}
		}


		public string Recruit(int region)
		{
            return RecruitWithDesign(region, Player.ActiveDesign);
        }

		public string Order(int id, int target, bool playerOrder = true, bool hostileTransit = true)
		{
			Army army = S.Armies.FirstOrDefault((Army x) => x.Id == id);
			if (army == null || target < 0 || target >= S.Provinces.Length)
			{
				return "Выберите соединение и регион.";
			}
			if (playerOrder && army.Owner != S.Player)
			{
				return "Это чужое соединение.";
			}
			if (!CanEnter(army.Owner, S.Provinces[army.Region].Owner)) return "Часть интернирована: нет доступа к территории.";
			if (target == army.Region)
			{
				return "Соединение уже в этом регионе.";
			}
			if (army.TrainingDays > 0) return "Соединение проходит подготовку: " + army.TrainingDays + " дн.";
			int owner = S.Provinces[target].Owner;
            if (AtWar(army.Owner, owner) && army.Fatigue >= 85) return "Для новой атаки нужен отдых: усталость ниже 85%.";
            if (AtWar(army.Owner, owner) && (army.Organization < 25 || ManpowerRules.Manning(army) < .25)) return "Для атаки нужна организация от 25% и укомплектованность от 25%.";
			if (!CanEnter(army.Owner, owner) || (!hostileTransit && AtWar(army.Owner, owner)))
			{
				return "Военный доступ отсутствует. Нельзя войти в нейтральную страну.";
			}
			int[] array = Enumerable.Repeat(-2, World.Regions.Length).ToArray();
			Queue<int> queue = new Queue<int>();
			queue.Enqueue(army.Region);
			array[army.Region] = -1;
			while (queue.Count > 0 && array[target] == -2)
			{
				int num = queue.Dequeue();
				int[] neighbors = World.Regions[num].Neighbors;
				foreach (int num3 in neighbors)
				{
					int owner2 = S.Provinces[num3].Owner;
					if (array[num3] == -2 && CanEnter(army.Owner, owner2) && (hostileTransit || !AtWar(army.Owner, owner2)))
					{
						array[num3] = num;
						queue.Enqueue(num3);
					}
				}
			}
			if (array[target] == -2)
			{
				return "Нет сухопутного маршрута. Морские перевозки будут добавлены позже.";
			}
			List<int> list = new List<int>();
			for (int num4 = target; num4 != army.Region; num4 = array[num4])
			{
				list.Add(num4);
			}
			list.Reverse();
            army.ExerciseRemaining = 0;
			if (playerOrder) DetachFromFront(army.Id);
            army.Route = list;
			BeginLeg(army);
			if (playerOrder)
			{
				Notify("Соединение №" + id + ": приказ на перемещение в " + World.Regions[target].Name);
			}
			Raise();
			return null;
		}

		private void BeginLeg(Army a)
		{
			if (a.Route.Count == 0)
			{
				a.Target = -1;
				return;
			}
			a.Target = a.Route[0];
			a.Route.RemoveAt(0);
			RegionInfo regionInfo = World.Regions[a.Region];
			RegionInfo regionInfo2 = World.Regions[a.Target];
			double num = (regionInfo.X - regionInfo2.X) * Math.Cos(regionInfo.Y * Math.PI / 180.0);
			double num2 = regionInfo.Y - regionInfo2.Y;
			double num3 = Math.Sqrt(num * num + num2 * num2) * 111.0;
			double num4 = 0.5 + S.Provinces[a.Region].Infrastructure / 100.0;
			a.TotalDays = (a.Days = Math.Max(1.0, num3 / (45.0 * a.Design.Speed * num4 * (1 + EquipmentRules.Support(a, EquipmentKind.Truck)) * (1 + DoctrineRules.Effects(S.Nations[a.Owner], S.Day).Speed))));
		}

		private void MoveArmies(bool[] connected)
		{
            var live = new HashSet<Army>(S.Armies);
            var regionArmies = S.Armies.GroupBy(u => u.Region).ToDictionary(g => g.Key, g => g.ToList());
			foreach (Army a in S.Armies.ToList())
			{
                if (!live.Contains(a)) continue; // A defender destroyed earlier this day must not act.
                Nation nation = S.Nations[a.Owner];
                var doctrine = DoctrineRules.Effects(nation, S.Day);
                double upkeep = GroundUpkeep(a, doctrine);
                bool funded = nation.Treasury >= upkeep;
                if (funded) nation.Treasury -= upkeep;
                if (a.TrainingDays > 0) { if (funded) { a.Organization = Math.Min(100, a.Organization + (100 - a.Organization) / a.TrainingDays); a.TrainingDays--; } continue; }
                foreach (var attachment in a.Attachments)
                    attachment.Condition = Rules.Clamp(attachment.Condition + (funded && a.Target < 0 ? 0.2 + attachment.Blueprint.Support * 0.06 : -(100 - attachment.Blueprint.Reliability) * 0.008 / (1 + attachment.Blueprint.Protection / 100)), 0, 100);
				double num = ((a.Target < 0) ? 0.003 * a.Battalions : a.Design.Fuel * a.Battalions + a.Attachments.Sum(x => x.Blueprint.Fuel * 0.3));
				num *= (1 + TechnologyRules.Effects(nation).GroundFuel) * (1 + doctrine.Fuel);
				double num2 = Math.Min(nation.Stock[2], num);
				nation.Stock[2] -= num2;
				bool flag = IsSupplied(a.Owner, a.Region, connected);
                if (flag) a.SupplyDays = a.Design.Range / (45 * a.Design.Speed) + TechnologyRules.Effects(nation).SupplyDays + doctrine.SupplyDays;
                else a.SupplyDays = System.Math.Max(0, a.SupplyDays - 1);
				double num3 = Rules.Clamp(nation.Military / 2.5, 0.2, 1.35);
				double num4 = (flag ? 100.0 : (a.SupplyDays > 0 ? 65.0 : 25.0)) * ((num > 0.0) ? (num2 / num) : 1.0) * Math.Min(1.0, num3);
				num4 = Math.Min(100, num4 * (1 + EquipmentRules.Support(a, EquipmentKind.Truck) + EquipmentRules.Support(a, EquipmentKind.Transport)) * (funded ? 1 : 0.6));
                a.Supply += (num4 - a.Supply) * 0.15;
                if (!flag && a.SupplyDays <= 0) DamageArmy(a, (100 - a.Supply) * .004, .2, 8.1);
				if (a.Target < 0)
				{
                    double repair = Math.Min(100 - a.Strength, Math.Min(.4 * a.Supply / 100 * num3, nation.Stock[7] / (.005 * a.Battalions)));
                    nation.Stock[7] -= repair * .005 * a.Battalions;
                    a.Strength = Math.Min(100, a.Strength + repair);
					continue;
				}
				a.Strength = Math.Max(8.1, a.Strength - (100.0 - a.Design.Reliability) * 0.002);
                a.Organization = Math.Max(0, a.Organization - .2);
				int owner = S.Provinces[a.Target].Owner;
                if (AtWar(a.Owner, owner) && (a.Organization < 15 || ManpowerRules.Manning(a) < .2 || a.Fatigue >= 95)) { a.Target = -1; a.Route.Clear(); continue; }
				if (!CanEnter(a.Owner, owner))
				{
					a.Target = -1;
					a.Route.Clear();
					continue;
				}
				a.Days -= Math.Max(0.1, a.Supply / 100.0);
				if (a.Days > 0.0)
				{
					continue;
				}
				List<Army> occupants;
                regionArmies.TryGetValue(a.Target, out occupants);
                List<Army> list = occupants == null ? new List<Army>() : occupants.Where(d => live.Contains(d) && AtWar(a.Owner, d.Owner)).ToList();
				if (list.Count > 0)
				{
					a.Fatigue = Math.Min(100, a.Fatigue + 2.5); foreach (var defender in list) { defender.ExerciseRemaining = 0; defender.Fatigue = Math.Min(100, defender.Fatigue + 2); }
                    a.Experience = Math.Min(100, a.Experience + .5); foreach (var defender in list) defender.Experience = Math.Min(100, defender.Experience + .35);
                    double num6 = EquipmentRules.CombatPower(a, list[0], false) * GroundCombatFactor(a.Owner, false) * (1 + AirReconBonus(a.Owner, a.Target));
					double num7 = list.Sum(d => EquipmentRules.CombatPower(d, a, true) * GroundCombatFactor(d.Owner, true) * (1 + AirReconBonus(d.Owner, a.Target))) * 1.15;
					DamageArmy(a, Rules.Clamp(num7 / Math.Max(1.0, num6) * 9.0, 2.0, 28.0));
					foreach (Army item in list)
					{
						DamageArmy(item, Rules.Clamp(num6 / Math.Max(1.0, num7) * 13.0, 2.0, 30.0));
					}
					foreach (Army item2 in list.Where((Army d) => d.Strength <= 8.0))
					{
						DestroyArmy(item2); live.Remove(item2);
						if (item2.Owner == S.Player)
						{
							Notify("Потеряно соединение №" + item2.Id + " в регионе " + World.Regions[item2.Region].Name, 2);
						}
					}
					if (a.Strength <= 8.0)
					{
						DestroyArmy(a); live.Remove(a);
						if (a.Owner == S.Player)
						{
							Notify("Соединение №" + a.Id + " потеряно в бою.", 2);
						}
					}
					else
					{
						a.Days = 1.0;
					}
					continue;
				}
				int owner2 = S.Provinces[a.Target].Owner;
				if (AtWar(a.Owner, owner2))
				{
					S.Provinces[a.Target].Owner = a.Owner;
					S.Provinces[a.Target].Infrastructure = Math.Max(10.0, S.Provinces[a.Target].Infrastructure - 12.0);
					if (a.Owner == S.Player || owner2 == S.Player)
					{
						Notify(World.Regions[a.Target].Name + ": контроль перешёл к " + World.Countries[a.Owner].Name, 2);
					}
				}
				regionArmies[a.Region].Remove(a);
                a.Region = a.Target;
                if (!regionArmies.ContainsKey(a.Region)) regionArmies[a.Region] = new List<Army>();
                regionArmies[a.Region].Add(a);
				BeginLeg(a);
			}
		}

		public string Subsidize(int region)
		{
			if (region < 0 || region >= S.Provinces.Length || S.Provinces[region].Owner != S.Player)
			{
				return "Выберите свой регион.";
			}
			if (Player.Treasury < 0.2)
			{
				return "Нужно 0,20 млрд.";
			}
			Player.Treasury -= 0.2;
			Player.PrivateCapital += 0.2;
			S.Provinces[region].Infrastructure = Math.Min(100.0, S.Provinces[region].Infrastructure + 1.0);
			Notify("Фонд поддержки бизнеса получил 0,20 млрд. Компании инвестируют самостоятельно.");
			Raise();
			return null;
		}

		public string Adopt(Blueprint d)
		{
            return AdoptDesign(d, S.Player);
        }

		public void Save(string path)
		{
            Validate(S);
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
			string text = path + ".tmp";
			using (FileStream stream = File.Create(text))
			{
				CampaignSerializer.Serialize(stream, S);
			}
			if (File.Exists(path))
			{
				File.Replace(text, path, path + ".bak");
			}
			else
			{
				File.Move(text, path);
			}
		}

		public void Load(string path)
		{
			Campaign s;
			using (FileStream stream = File.OpenRead(path))
			{
				using (var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64000000 }))
                    s = (Campaign)CampaignSerializer.Deserialize(reader);
			}
			Validate(s);
            if (s.Version == 1) InitializeEquipment(s);
            if (s.Version == 2) InitializeTreaties(s);
            if (s.Version == 3) InitializeAviation(s);
            if (s.Version == 4) InitializeManpower(s);
            if (s.Version == 5) InitializeNationalPolitics(s);
            if (s.Version == 6) InitializeTechnology(s, true);
            if (s.Version == 7) ExpandTechnology(s);
            if (s.Version == 8) InitializeDoctrines(s);
            if (s.Version == 9) InitializeGovernments(s);
            if (s.Version == 10) InitializeOperations(s);
            if (s.Version == 11) s.Version = 12;
            if (s.Version == 12) InitializeTrade(s);
            if (s.Version == 13) InitializeReadiness(s);
            Validate(s);
			S = s;
			Raise();
		}


		public void Autosave()
		{
			try
			{
				Save(Path.Combine(Saves, "autosave.xml"));
			}
			catch (Exception ex)
			{
				Notify("Не удалось сохранить автоматически: " + ex.Message, 2);
			}
		}
	}
}
