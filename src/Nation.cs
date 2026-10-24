using System.Collections.Generic;

namespace Sovereign
{
	public class Nation
	{
		public double Treasury;

		public double Debt;

		public double GDP;

		public double BaseGDP;

		public double Population;

		public double Stability = 65.0;

		public double Approval = 58.0;

		public double Inflation = 3.0;

		public double Unemployment = 8.0;

		public double Living = 50.0;

		public double Education = 55.0;

		public double Tax = 22.0;

		public double Social = 4.0;

		public double Science = 1.8;

		public double Military = 2.5;

		public double Interest = 4.0;

		public double Repression;

		public double PrivateCapital;

		public double Revenue;

		public double Spending;

		public double TradeBalance;

		public double Imports;

		public double Exports;

		public double Shortage;

		public double LastPrice = 1.0;

		public double[] Stock = new double[8];

		public double[] Price = new double[8];

		public double[] Production = new double[8];

		public double[] Demand = new double[8];

		public double[] Consumed = new double[8];

		public int[] Tech = new int[6];

		public int Research = -1;

		public int Leader = 1;

		public double ResearchProgress;

		public bool AutoTrade = true;

		public int Focus;

		public double ProjectProgress;

		public Blueprint Design = new Blueprint();

        public List<EquipmentDesign> Designs = new List<EquipmentDesign>();
        public List<ProductionLine> Lines = new List<ProductionLine>();
        public int NextDesign = 1;
        public int NextLine = 1;
        public int ActiveDesign;

		public List<double> History = new List<double>();

		public List<double> StabilityHistory = new List<double>();
	}
}
