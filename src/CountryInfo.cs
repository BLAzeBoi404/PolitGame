using System.Collections.Generic;

namespace Sovereign
{
	public class CountryInfo : Land
	{
		public string Code;

		public double GDP;
        public int PopulationYear, ReferenceMilitary, StartingGroundPersonnel, MilitaryYear, CapitalRegion;
        public bool GroundStrengthEstimated;
        public double TrainingBaseline = 45, ReferenceFundingPerPerson;
        public int FundingYear, FundingGdpYear;

		public List<int> Regions = new List<int>();
	}
}
