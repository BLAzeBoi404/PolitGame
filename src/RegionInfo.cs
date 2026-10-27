namespace Sovereign
{
	public class RegionInfo : Land
	{
		public int Country;
        public string Settlement = "", PopulationBasis = "";

		public int[] Neighbors;
	}
}
