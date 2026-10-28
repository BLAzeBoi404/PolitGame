using System;
using System.Collections.Generic;

namespace Sovereign
{
    public sealed class TradePolicy
    {
        public int Tariff = 10, ReserveWeeks = 3;
        public double[] Operations = new double[8]; // Forecast daily-system inputs over seven days.
        public double Tariffs, Freight;
    }
    public sealed class TradeDeal
    {
        public int Id, Buyer, Seller, Good, StartedDay, EndDay, LastDay = -1;
        public double WeeklyQuantity, UnitPrice, Delivered, Paid, LastDelivery;
    }
    public sealed class TradeFlow
    {
        public int Buyer, Seller, Good;
        public double Quantity, Value, Tariff, Freight;
    }
    public static class TradeRules
    {
        public const int MaxWeeks = 26;
        public static double RetainedBusinessIncome(double weeklyGDP, double tax) => Math.Max(0, weeklyGDP * (1 - tax / 100) * .06);
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
