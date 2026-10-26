using System;
namespace Sovereign
{
    public class PublicContract
    {
        public int Id, Company, Supplier, Product, DesignId, Destination, Quantity, Delivered;
        public int Factories = 1;
        public double Progress, Paid;
        public bool Paused;
        public bool Active => !Paused && Delivered < Quantity;
    }
    public static class ProcurementRules
    {
        public const double CapacityUnit = .1; // One procurement workshop uses one tenth of a factory.
        public static readonly string[] Products = { "Продовольствие", "Электроэнергия", "Топливо", "Руда", "Сталь", "Машины", "Потребтовары", "Вооружение", "Городские автобусы (10 шт.)", "Грузовой автопарк (10 шт.)", "Техника по проекту" };
        public static int Sector(int product) => product < 8 ? product : product == 10 ? 7 : 5;
        public static double Price(int product, Blueprint design) => product < 8 ? Rules.Prices[product] * 1.2 : product == 10 ? design.Cost * 1.2 : product == 8 ? .018 : .014;
        public static double Work(int product, Blueprint design) => product < 8 ? 7 / Rules.Output[product] : product == 10 ? design.WorkDays : product == 8 ? 12 : 9;
        public static double Input(int product, int good, Blueprint design) => product < 8 ? Rules.Inputs[product, good] : product == 10 ? (good == 4 ? design.Steel : good == 5 ? design.Machines : good == 7 ? design.Arms : 0) : good == 4 ? .18 : good == 5 ? .10 : good == 1 ? .08 : 0;
        public static double Infrastructure(Province p) => Math.Min(100, p.Infrastructure + p.FleetService * (Math.Min(5, p.Buses / Math.Max(1, p.Population * 100)) + Math.Min(5, p.Trucks / Math.Max(1, p.Population * 100))));
    }
}
