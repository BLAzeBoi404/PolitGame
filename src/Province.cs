namespace Sovereign
{
	public class Province
	{
		public int Owner;
        public int Airfield;

		public double Population;

		public double Infrastructure = 50.0;

		public double Unrest;

		public double Soil;

		public double Oil;

		public double Ore;

		public double[] Public = new double[8];

		public double[] Private = new double[8];
	}
}
