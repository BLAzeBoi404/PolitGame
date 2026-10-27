namespace Sovereign
{
	public class Province
	{
		public int Owner;
        public int Buses, Trucks;
        public double FleetService;
        public double[] ContractWork = new double[8];
        public int Airfield;

		public double Population;
        // Optional exact representation in format 12. Some Mono versions round tiny
        // decimal doubles incorrectly on XML reads. Keep the readable value for older
        // files, then restore its exact IEEE bits when this element is present.
        public long PopulationBits { get => System.BitConverter.DoubleToInt64Bits(Population); set => Population = System.BitConverter.Int64BitsToDouble(value); }

		public double Infrastructure = 50.0;

		public double Unrest;

		public double Soil;

		public double Oil;

		public double Ore;

		public double[] Public = new double[8];

		public double[] Private = new double[8];
	}
}
