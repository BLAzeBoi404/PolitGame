using System.Collections.Generic;

namespace Sovereign
{
	public class CountryInfo : Land
	{
		public string Code;

		public double GDP;

		public List<int> Regions = new List<int>();
	}
}
