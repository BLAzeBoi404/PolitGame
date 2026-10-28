using System;
using System.Collections.Generic;

namespace Sovereign
{
	public class Campaign
	{
		public const int CurrentVersion = 13;
        public int Version = 1;
        public double[] MarketPrices;
        public List<TradeDeal> TradeDeals;
        public List<TradeFlow> TradeFlows;
        public int NextTradeDeal = 1, TradeDay = -1;

		public int Player;

		public int Day;

		public int StartYear = 2026;

		public int Difficulty = 1;

		public uint RandomState = 19770613u;

		public Nation[] Nations;

		public Province[] Provinces;

		public List<Army> Armies = new List<Army>();

		public List<Construction> Projects = new List<Construction>();

		public int[] Relations;

		public bool[] Wars;

		public bool[] Embargo;

		public bool[] Agreements;

        // Access[guest, host] grants the guest transit and land supply through the host.
        public bool[] Access;
        public bool[] Alliances;
        public int[] WarStarted;
        public int[] TruceUntil;
        public List<FrontPlan> Fronts = new List<FrontPlan>();
        public int NextFront = 1;
        public List<AirWing> AirWings = new List<AirWing>();
        public int NextAirWing = 1;

		public List<Bulletin> News = new List<Bulletin>();

		public int NextArmy = 1;

		public DateTime Date => new DateTime(StartYear, 1, 1).AddDays(Day);

		public double Random()
		{
			RandomState ^= RandomState << 13;
			RandomState ^= RandomState >> 17;
			RandomState ^= RandomState << 5;
			return (double)RandomState / 4294967296.0;
		}
	}
}
