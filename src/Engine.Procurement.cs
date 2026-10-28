using System;
using System.IO;
using System.Linq;
namespace Sovereign
{
    public partial class Engine
    {
        private void InitializeOperations(Campaign campaign)
        {
            foreach (var n in campaign.Nations) { n.Contracts = new System.Collections.Generic.List<PublicContract>(); n.NextContract = 1; }
            foreach (var p in campaign.Provinces) { p.Buses = p.Trucks = 0; p.FleetService = 0; p.ContractWork = new double[8]; }
            campaign.Version = 11;
        }
        public string CompanyName(int company) => World.Regions[company / 8].Name + " · " + Rules.Plants[company % 8];
        public int CompanyCapacity(int company) => Math.Min(500, Math.Max(0, (int)Math.Floor(S.Provinces[company / 8].Private[company % 8] / ProcurementRules.CapacityUnit + 1e-9)));
        public int CompanyAssigned(int company)
        {
            int total = 0, owner = S.Provinces[company / 8].Owner;
            foreach (var n in S.Nations) foreach (var c in n.Contracts) if (c.Company == company && c.Active && c.Supplier == owner) total += c.Factories;
            return total;
        }
        private double ReservedDefenseCapacity(int owner)
        {
            double total = 0;
            foreach (var n in S.Nations) foreach (var c in n.Contracts)
                if (c.Active && c.Supplier == owner && c.Company % 8 == 7 && S.Provinces[c.Company / 8].Owner == owner) total += c.Factories * ProcurementRules.CapacityUnit;
            return total;
        }
        private double[,] ContractReservations()
        {
            double[,] reserved = null;
            foreach (var n in S.Nations) foreach (var c in n.Contracts) if (c.Active && S.Provinces[c.Company / 8].Owner == c.Supplier) {
                if (reserved == null) reserved = new double[S.Provinces.Length, 8];
                reserved[c.Company / 8, c.Company % 8] += c.Factories * ProcurementRules.CapacityUnit;
            }
            return reserved;
        }
        public string ContractReason(int company, int product, int quantity, int factories, int destination, int designId = 0, bool resuming = false)
        {
            if (product < 0 || product > 10 || company < 0 || company >= S.Provinces.Length * 8 || company % 8 != ProcurementRules.Sector(product)) return "Предприятие не выпускает эту продукцию.";
            if (quantity < 1 || quantity > 1000 || factories < 1 || factories > 500) return "Количество: 1–1000 партий, мощности: 1–500.";
            if (destination < 0 || destination >= S.Provinces.Length || S.Provinces[destination].Owner != S.Player) return "Выберите свою область для поставки.";
            if (!resuming && Player.Contracts.Count >= 128) return "Удалите завершённые контракты (лимит 128).";
            if (!resuming && (product == 8 || product == 9)) {
                long vehicles = product == 8 ? S.Provinces[destination].Buses : S.Provinces[destination].Trucks;
                vehicles += Player.Contracts.Where(c => c.Destination == destination && c.Product == product).Sum(c => (long)(c.Quantity - c.Delivered) * 10);
                if (vehicles + quantity * 10 > 10000000) return "Достигнут предел регионального автопарка.";
            }
            int supplier = S.Provinces[company / 8].Owner;
            if (AtWar(S.Player, supplier) || S.Embargo[Pair(S.Player, supplier)] || S.Embargo[Pair(supplier, S.Player)]) return "Поставка невозможна: война или эмбарго.";
            if (CompanyCapacity(company) - CompanyAssigned(company) < factories) return "У предприятия недостаточно свободных частных мощностей.";
            if (company % 8 == 7 && S.Provinces.Where(p => p.Owner == supplier).Sum(p => p.Public[7] + p.Private[7]) - ReservedDefenseCapacity(supplier) - AssignedFactories(supplier) + 1e-9 < factories * ProcurementRules.CapacityUnit) return "Оборонные мощности уже заняты серийными заказами.";
            var design = FindDesign(S.Player, designId);
            if (product == 10 && (design == null || (!resuming && design.Retired))) return "Выберите действующий проект из своего каталога.";
            if (Player.Treasury < ProcurementRules.Price(product, design == null ? null : design.Blueprint)) return "Нужны деньги хотя бы на первую партию.";
            return null;
        }
        public string PlaceContract(int company, int product, int quantity, int factories, int destination, int designId = 0)
        {
            string reason = ContractReason(company, product, quantity, factories, destination, designId); if (reason != null) return reason;
            Player.Contracts.Add(new PublicContract { Id = Player.NextContract++, Company = company, Supplier = S.Provinces[company / 8].Owner, Product = product, Quantity = quantity, Factories = factories, Destination = destination, DesignId = product == 10 ? designId : 0 });
            Notify("Госзаказ принят: " + CompanyName(company) + ". Оплата по мере изготовления."); ComputeEconomy(false); Raise(); return null;
        }
        public string ContractStatus(int buyer, PublicContract c, bool forecast = false)
        {
            if (c.Delivered == c.Quantity) return "Выполнен";
            if (c.Paused) return "Приостановлен";
            if (S.Provinces[c.Company / 8].Owner != c.Supplier) return "Предприятие утрачено";
            if (S.Provinces[c.Destination].Owner != buyer) return "Область поставки утрачена";
            if (AtWar(buyer, c.Supplier) || S.Embargo[Pair(buyer, c.Supplier)] || S.Embargo[Pair(c.Supplier, buyer)]) return "Война / эмбарго";
            int assigned = 0;
            foreach (var nation in S.Nations) { foreach (var prior in nation.Contracts) { if (prior.Active && prior.Company == c.Company && prior.Supplier == c.Supplier) assigned += prior.Factories; if (object.ReferenceEquals(prior, c)) goto capacityChecked; } }
            capacityChecked:
            if (assigned > CompanyCapacity(c.Company)) return "Утрачены мощности";
            if (S.Nations[buyer].Treasury <= 0) return "Нет средств";
            var design = c.Product == 10 ? FindDesign(buyer, c.DesignId).Blueprint : null;
            for (int good = 0; !forecast && good < 8; good++) if (ProcurementRules.Input(c.Product, good, design) > 0 && S.Nations[c.Supplier].Stock[good] <= 1e-9) return "Дефицит: " + Rules.Goods[good];
            return "В производстве";
        }
        public string ToggleContract(int id)
        {
            var c = Player.Contracts.FirstOrDefault(x => x.Id == id); if (c == null || c.Delivered == c.Quantity) return "Контракт завершён или не найден.";
            if (c.Paused && S.Provinces[c.Company / 8].Owner != c.Supplier) return "Предприятие сменило владельца.";
            if (c.Paused) { string reason = ContractReason(c.Company, c.Product, c.Quantity, c.Factories, c.Destination, c.DesignId, true); if (reason != null) return reason; }
            c.Paused = !c.Paused; ComputeEconomy(false); Raise(); return null;
        }
        public string CancelContract(int id)
        {
            var c = Player.Contracts.FirstOrDefault(x => x.Id == id); if (c == null) return "Контракт не найден.";
            Player.Contracts.Remove(c); ComputeEconomy(false); Raise(); return null;
        }
        private double ContractEfficiency(PublicContract c)
        {
            var p = S.Provinces[c.Company / 8]; var n = S.Nations[c.Supplier]; var technology = TechnologyRules.Effects(n);
            var policy = GovernmentRules.CombinedEffects(n, S.Day);
            double efficiency = ManpowerRules.Output(n) * (1 - n.Repression * .0015) * (.55 + ProcurementRules.Infrastructure(p) / 200);
            if (c.Product == 10) return efficiency * policy.EquipmentFactor * (1 + technology.Equipment) * (1 + n.Tech[2] * .04);
            int sector = ProcurementRules.Sector(c.Product), branch = sector == 0 ? 0 : sector == 1 ? 1 : 2;
            double resource = sector == 0 ? p.Soil : sector == 2 ? p.Oil : sector == 3 ? p.Ore : 1;
            return efficiency * Math.Max(.1, resource) * policy.GoodsFactor * TechnologyRules.GoodsFactor(technology, sector) * (1 + n.Tech[branch] * .05);
        }
        public void TickContracts()
        {
            if (!S.Nations.Any(n => n.Contracts.Any(c => c.Active))) return;
            var used = new int[S.Provinces.Length * 8];
            for (int buyer = 0; buyer < S.Nations.Length; buyer++) foreach (var c in S.Nations[buyer].Contracts) {
                if (!c.Active || S.Provinces[c.Company / 8].Owner != c.Supplier) continue;
                used[c.Company] += c.Factories;
                if (used[c.Company] > CompanyCapacity(c.Company) || ContractStatus(buyer, c) != "В производстве") continue;
                var n = S.Nations[buyer]; var producer = S.Nations[c.Supplier];
                var d = c.Product == 10 ? FindDesign(buyer, c.DesignId).Blueprint : null;
                double price = ProcurementRules.Price(c.Product, d);
                double outputFactor = ContractEfficiency(c);
                double work = Math.Min(c.Quantity - c.Delivered - c.Progress, c.Factories * ProcurementRules.CapacityUnit * outputFactor / ProcurementRules.Work(c.Product, d));
                work = Math.Min(work, Math.Max(0, n.Treasury) / price);
                for (int good = 0; good < 8; good++) { double input = ProcurementRules.Input(c.Product, good, d); if (input > 0) work = Math.Min(work, producer.Stock[good] / input); }
                if (work <= 1e-12) continue;
                S.Provinces[c.Company / 8].ContractWork[c.Company % 8] += work * ProcurementRules.Work(c.Product, d) / outputFactor;
                double payment = work * price; n.Treasury = Math.Max(0, n.Treasury - payment); producer.PrivateCapital += payment; c.Paid += payment;
                for (int good = 0; good < 8; good++) producer.Stock[good] = Math.Max(0, producer.Stock[good] - work * ProcurementRules.Input(c.Product, good, d));
                c.Progress += work; int delivered = Math.Min(c.Quantity - c.Delivered, (int)Math.Floor(c.Progress + 1e-10));
                if (delivered == 0) continue;
                c.Progress = Math.Max(0, c.Progress - delivered); c.Delivered += delivered;
                if (c.Product < 8) n.Stock[c.Product] += delivered;
                else if (c.Product == 8) S.Provinces[c.Destination].Buses += delivered * 10;
                else if (c.Product == 9) S.Provinces[c.Destination].Trucks += delivered * 10;
                else FindDesign(buyer, c.DesignId).Stock += delivered;
                if (buyer == S.Player) Notify("Госзаказ №" + c.Id + ": поставлено " + c.Delivered + "/" + c.Quantity + " · " + ProcurementRules.Products[c.Product]);
            }
        }
        private void TickPublicFleets()
        {
            foreach (var p in S.Provinces) {
                int vehicles = p.Buses + p.Trucks; if (vehicles == 0) { p.FleetService = 0; continue; }
                var n = S.Nations[p.Owner]; double cost = vehicles * .000001, fuel = vehicles * .00002;
                p.FleetService = Math.Min(1, Math.Min(Math.Max(0, n.Treasury) / cost, n.Stock[2] / fuel));
                n.Treasury = Math.Max(0, n.Treasury - cost * p.FleetService); n.Stock[2] = Math.Max(0, n.Stock[2] - fuel * p.FleetService);
            }
        }
        private void ValidateOperations(Campaign s)
        {
            for (int owner = 0; owner < s.Nations.Length; owner++) {
                var n = s.Nations[owner];
                if (n.Contracts == null || n.Contracts.Count > 128 || n.Contracts.Any(c => c == null) || n.NextContract < 1 || n.Contracts.Select(c => c.Id).Distinct().Count() != n.Contracts.Count) throw new InvalidDataException("Повреждены госзаказы.");
                foreach (var c in n.Contracts) {
                    var design = n.Designs.FirstOrDefault(d => d.Id == c.DesignId);
                    if (c.Id < 1 || c.Id >= n.NextContract || c.Company < 0 || c.Company >= s.Provinces.Length * 8 || c.Supplier < 0 || c.Supplier >= s.Nations.Length || c.Product < 0 || c.Product > 10 || c.Company % 8 != ProcurementRules.Sector(c.Product)
                        || c.Destination < 0 || c.Destination >= s.Provinces.Length || c.Quantity < 1 || c.Quantity > 1000 || c.Delivered < 0 || c.Delivered > c.Quantity || c.Factories < 1 || c.Factories > 500
                        || !IsFinite(c.Progress) || c.Progress < 0 || c.Progress >= 1 || (c.Delivered == c.Quantity && c.Progress > 1e-8) || !IsFinite(c.Paid) || c.Paid < 0
                        || (c.Product == 10 ? design == null : c.DesignId != 0)) throw new InvalidDataException("Некорректный контракт.");
                    if (Math.Abs(c.Paid - (c.Delivered + c.Progress) * ProcurementRules.Price(c.Product, design == null ? null : design.Blueprint)) > 1e-6) throw new InvalidDataException("Нарушен учёт оплаты контракта.");
                }
                foreach (var t in n.Templates) if (!DeploymentRules.Valid(t.Deployment, t.Battalions)) throw new InvalidDataException("Некорректный боевой порядок штата.");
            }
            foreach (var p in s.Provinces) if (!Values(p.ContractWork, 8) || p.ContractWork.Any(v => v > 10000000) || p.Buses < 0 || p.Buses > 10000000 || p.Trucks < 0 || p.Trucks > 10000000 || !IsFinite(p.FleetService) || p.FleetService < 0 || p.FleetService > 1) throw new InvalidDataException("Повреждён автопарк.");
            foreach (var a in s.Armies) if (!DeploymentRules.Valid(a.Deployment, a.Battalions) || a.Experience < 0 || a.Experience > 100 || !DeploymentRules.Same(a.Deployment, s.Nations[a.Owner].Templates.First(t => t.Id == a.TemplateId).Deployment, a.Battalions)) throw new InvalidDataException("Некорректный боевой порядок части.");
        }
    }
}
