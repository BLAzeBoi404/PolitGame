using System;

namespace Sovereign
{
	public static class Rules
	{
		public static string[] Goods = new string[8] { "Продовольствие", "Электроэнергия", "Топливо", "Руда", "Сталь", "Машины", "Потребтовары", "Вооружение" };

		public static string[] Plants = new string[8] { "Агрокомплекс", "Электростанция", "Нефтепереработка", "Рудник", "Металлургия", "Машиностроение", "Лёгкая промышленность", "Оборонный завод" };

		public static string[] Techs = new string[6] { "Агрономия", "Энергетика", "Промышленность", "Логистика", "Медицина", "Военные технологии" };

		public static double[] Prices = new double[8] { 0.012, 0.009, 0.024, 0.008, 0.022, 0.065, 0.035, 0.14 };

		public static double[] Output = new double[8] { 2.5, 3.8, 2.1, 3.0, 1.8, 0.8, 1.8, 0.42 };

		public static double[] BuildCost = new double[8] { 0.18, 0.45, 0.6, 0.22, 0.55, 0.8, 0.35, 1.2 };

		public static double[,] Inputs = new double[8, 8]
		{
			{ 0.0, 0.12, 0.05, 0.0, 0.0, 0.0, 0.0, 0.0 },
			{ 0.0, 0.0, 0.18, 0.0, 0.0, 0.0, 0.0, 0.0 },
			{ 0.0, 0.25, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
			{ 0.0, 0.1, 0.08, 0.0, 0.0, 0.0, 0.0, 0.0 },
			{ 0.0, 0.6, 0.0, 1.3, 0.0, 0.0, 0.0, 0.0 },
			{ 0.0, 0.3, 0.0, 0.0, 0.5, 0.0, 0.0, 0.0 },
			{ 0.0, 0.18, 0.0, 0.0, 0.1, 0.08, 0.0, 0.0 },
			{ 0.0, 0.4, 0.0, 0.0, 0.5, 0.15, 0.0, 0.0 }
		};

		public static double Clamp(double v, double a, double b)
		{
			return Math.Max(a, Math.Min(b, v));
		}
	}
}
