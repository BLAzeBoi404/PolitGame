using System.Collections.Generic;

namespace Sovereign
{
	public class Army
	{
		public int Id;

		public int Owner;

		public int Region;

		public int Target = -1;

		public List<int> Route = new List<int>();

		public double Days;

		public double TotalDays;

		public double Strength = 100.0;

		public double Supply = 100.0;

		public double Experience;

		public Blueprint Design = new Blueprint();
	}
}
