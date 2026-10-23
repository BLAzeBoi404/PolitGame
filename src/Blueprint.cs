using System;

namespace Sovereign
{
	public class Blueprint
	{
		public string Name = "Стандарт-1";

		public int Armor = 2;

		public int Gun = 2;

		public int Engine = 2;

		public int Electronics = 1;

		public double Cost => 0.05 * (1.0 + (double)Armor * 0.45 + (double)Gun * 0.5 + (double)Engine * 0.35 + (double)Electronics * 0.55);

		public double Power => 1.0 + (double)Gun * 0.14 + (double)Armor * 0.11 + (double)Electronics * 0.1;

		public double Speed => Math.Max(0.45, 1.0 + (double)Engine * 0.15 - (double)Armor * 0.1 - (double)Gun * 0.025);

		public double Fuel => 0.04 * (1.0 + (double)Armor * 0.2 + (double)Engine * 0.15);

		public double Reliability => Rules.Clamp(95 - (Armor + Gun + Electronics) * 3 + Engine * 2, 35.0, 98.0);
	}
}
