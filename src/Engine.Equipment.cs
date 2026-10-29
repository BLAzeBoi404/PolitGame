using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        public void InitializeEquipment(Campaign campaign)
        {
            foreach (Nation n in campaign.Nations) {
                if (n.Designs.Count != 0) continue;
                var record = new EquipmentDesign { Id = n.NextDesign++, Blueprint = n.Design.Copy() };
                n.Designs.Add(record);
                n.ActiveDesign = record.Id;
            }
            foreach (Army a in campaign.Armies) {
                if (a.DesignId != 0) continue;
                Nation n = campaign.Nations[a.Owner];
                var record = n.Designs.FirstOrDefault(d => SameDesign(d.Blueprint, a.Design));
                if (record == null) {
                    record = new EquipmentDesign { Id = n.NextDesign++, Blueprint = a.Design.Copy(), Retired = true };
                    n.Designs.Add(record);
                }
                a.DesignId = record.Id;
            }
            campaign.Version = 2;
        }

        private static bool SameDesign(Blueprint a, Blueprint b)
        {
            return a.Kind == b.Kind && a.Name == b.Name && EquipmentRules.Modules(a).SequenceEqual(EquipmentRules.Modules(b));
        }

        public EquipmentDesign FindDesign(int owner, int id) => S.Nations[owner].Designs.FirstOrDefault(d => d.Id == id);
        public int FactoryCapacity(int owner)
        {
            return Math.Max(0, Math.Min(500, (int)Math.Floor(S.Provinces.Where(p => p.Owner == owner).Sum(p => p.Public[7] + p.Private[7]) - ReservedDefenseCapacity(owner))));
        }
        public int AssignedFactories(int owner) => S.Nations[owner].Lines.Where(l => l.Active).Sum(l => l.Factories);
        public string AdoptDesign(Blueprint d, int owner)
        {
            Nation n = S.Nations[owner];
            string error = EquipmentRules.Validate(d, n);
            if (error != null) return error;
            if (n.Designs.Count >= 128) return "Каталог заполнен (128 проектов).";
            if (n.Designs.Any(x => x.Blueprint.Name.Equals(d.Name.Trim(), StringComparison.OrdinalIgnoreCase))) return "У проекта должно быть уникальное название.";
            if (n.Treasury < d.Cost * 2) return "Недостаточно средств на испытания: " + (d.Cost * 2).ToString("0.00") + " млрд.";
            n.Treasury -= d.Cost * 2;
            Blueprint copy = d.Copy(); copy.Name = copy.Name.Trim();
            var record = new EquipmentDesign { Id = n.NextDesign++, Blueprint = copy };
            n.Designs.Add(record);
            n.ActiveDesign = record.Id;
            n.Design = copy.Copy();
            if (owner == S.Player) Notify("Проект «" + copy.Name + "» испытан. Закажите серию в разделе производства.", 1);
            Raise();
            return null;
        }
        public string SelectDesign(int id)
        {
            var d = FindDesign(S.Player, id);
            if (d == null || d.Retired) return "Проект недоступен.";
            Player.ActiveDesign = id;
            Player.Design = d.Blueprint.Copy();
            Raise();
            return null;
        }
        public string RetireDesign(int id)
        {
            var d = FindDesign(S.Player, id);
            if (d == null) return "Проект не найден.";
            if (Player.Lines.Any(l => l.DesignId == id && l.Completed < l.Quantity)) return "Сначала завершите или отмените заказы этого проекта.";
            d.Retired = !d.Retired;
            Raise();
            return null;
        }
        public string StartProduction(int designId, int factories, int quantity, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            Nation n = S.Nations[owner];
            var d = FindDesign(owner, designId);
            if (d == null || d.Retired) return "Выберите действующий проект из каталога.";
            if (factories < 1 || factories > 500 || quantity < 1 || quantity > 1000) return "Заказ: 1–1000 комплектов; заводы: 1–500.";
            if (n.Lines.Count >= 64) return "Удалите завершённые линии (лимит 64).";
            if (AssignedFactories(owner) + factories > FactoryCapacity(owner)) return "Нет свободных оборонных мощностей. Постройте завод или освободите другую линию.";
            n.Lines.Add(new ProductionLine { Id = n.NextLine++, DesignId = designId, Factories = factories, Quantity = quantity });
            if (owner == S.Player) Notify("Серия «" + d.Blueprint.Name + "»: заказано " + quantity + " комплектов.");
            Raise();
            return null;
        }
        public string ChangeLine(int id, int factories, bool paused)
        {
            var line = Player.Lines.FirstOrDefault(l => l.Id == id);
            if (line == null || line.Completed >= line.Quantity) return "Линия завершена или не найдена.";
            if (factories < 1 || factories > 500) return "Назначьте от 1 до 500 заводов.";
            int others = AssignedFactories(S.Player) - (line.Active ? line.Factories : 0);
            if (!paused && others + factories > FactoryCapacity(S.Player)) return "Недостаточно свободных заводов.";
            line.Factories = factories; line.Paused = paused;
            Raise();
            return null;
        }
        public string CancelLine(int id)
        {
            var line = Player.Lines.FirstOrDefault(l => l.Id == id);
            if (line == null) return "Линия не найдена.";
            // Completed stock is retained; already consumed resources are not refunded.
            Player.Lines.Remove(line);
            Raise();
            return null;
        }
        public string ProductionStatus(int owner, ProductionLine line)
        {
            if (line.Completed >= line.Quantity) return "Заказ выполнен";
            if (line.Paused) return "Пауза — заводы свободны";
            int prior = S.Nations[owner].Lines.TakeWhile(l => l != line).Where(l => l.Active).Sum(l => l.Factories);
            if (prior + line.Factories > FactoryCapacity(owner)) return "Остановлено: утрачены заводы";
            Nation n = S.Nations[owner];
            if (n.Treasury <= 0) return "Остановлено: нет денег";
            foreach (int good in new[] { 4, 5, 7 }) if (n.Stock[good] <= 1e-9) return "Дефицит: " + Rules.Goods[good];
            return "Выпуск · эффективность " + line.Efficiency.ToString("P0");
        }
        private double[] EquipmentCapacity()
        {
            // Aggregate capacity once per day, rather than scanning all provinces for every line.
            double[] capacity = new double[S.Nations.Length];
            foreach (Province p in S.Provinces) capacity[p.Owner] += p.Public[7] + p.Private[7];
            foreach (var buyer in S.Nations) foreach (var contract in buyer.Contracts) if (contract.Active && contract.Company % 8 == 7 && S.Provinces[contract.Company / 8].Owner == contract.Supplier) capacity[contract.Supplier] -= contract.Factories * ProcurementRules.CapacityUnit;
            return capacity;
        }
        private double ProductionPotential(Nation n,ProductionLine line,Blueprint d,TechnologyEffects technology)
        {
            return line.Factories*line.Efficiency*(1+n.Tech[2]*.04)*ManpowerRules.Output(n)*GovernmentRules.CombinedEffects(n,S.Day).EquipmentFactor*(1+technology.Equipment)/d.WorkDays;
        }
        public void TickProduction()
        {
            var capacity=EquipmentCapacity();
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                Nation n = S.Nations[owner];
                int available = Math.Min(500, (int)Math.Floor(capacity[owner]));
                var technology = TechnologyRules.Effects(n);
                foreach (var line in n.Lines) {
                    if (!line.Active) continue;
                    available -= line.Factories;
                    if (available < 0) continue;
                    Blueprint d = FindDesign(owner, line.DesignId).Blueprint;
                    double potential = ProductionPotential(n,line,d,technology);
                    double work = Math.Min(potential, line.Quantity - line.Completed - line.Progress);
                    work = Math.Min(work, Math.Max(0, n.Treasury) / d.Cost);
                    work = Math.Min(work, n.Stock[4] / (d.Steel * (1 + technology.EquipmentSteel)));
                    work = Math.Min(work, n.Stock[5] / d.Machines);
                    work = Math.Max(0, Math.Min(work, n.Stock[7] / d.Arms));
                    if (work <= 1e-12) continue;
                    n.Treasury -= work * d.Cost;
                    n.Stock[4] = Math.Max(0, n.Stock[4] - work * d.Steel * (1 + technology.EquipmentSteel));
                    n.Stock[5] = Math.Max(0, n.Stock[5] - work * d.Machines);
                    n.Stock[7] = Math.Max(0, n.Stock[7] - work * d.Arms);
                    line.Progress += work;
                    line.Efficiency = Math.Min(1, line.Efficiency + 0.006 * work / potential);
                    int finished = Math.Min(line.Quantity - line.Completed, (int)Math.Floor(line.Progress + 1e-10));
                    if (finished > 0) {
                        line.Completed += finished;
                        line.Progress = Math.Max(0, line.Progress - finished);
                        FindDesign(owner, line.DesignId).Stock += finished;
                        if (owner == S.Player) Notify("Поставка: «" + d.Name + "» +" + finished + " компл. (" + line.Completed + "/" + line.Quantity + ")", 1);
                    }
                }
            }
        }
        public string RecruitWithDesign(int region, int designId, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (region < 0 || region >= S.Provinces.Length || S.Provinces[region].Owner != owner) return "Выберите свой регион.";
            if (!IsSupplied(owner, region, SupplyNetwork())) return "Набор требует сухопутного снабжения области.";
            var d = FindDesign(owner, designId);
            if (d == null || d.Retired || !d.Blueprint.Formation) return "Для новой части выберите танк, БМП или артиллерию.";
            Nation n = S.Nations[owner]; var template = SelectedTemplate(owner);
            int people = ManpowerRules.GroundSize(d.Blueprint.Kind, template.Battalions);
            string reason = RecruitmentReason(owner, d.Id, template.Id); if (reason != null) return reason;
            n.Treasury -= .3 * template.Battalions; d.Stock -= template.Battalions; n.Recruits -= people;
            int days = ManpowerRules.TrainingDays(template.Battalions);
            S.Armies.Add(new Army { Id = S.NextArmy++, Owner = owner, Region = region, Strength = 100, Personnel = people,
                Deployment = template.Deployment == null ? null : template.Deployment.Copy(),
                TemplateId = template.Id, Battalions = template.Battalions, Organization = 20,
                Design = d.Blueprint.Copy(), DesignId = d.Id, TrainingDays = days });
            if (owner == S.Player) Notify(template.Name + " формируется: " + World.Regions[region].Name + ". " + people + " чел., подготовка " + days + " дней.");
            Raise(); return null;
        }
        public string Refit(int armyId, int designId, bool support = false)
        {
            var a = S.Armies.FirstOrDefault(x => x.Id == armyId && x.Owner == S.Player);
            var d = FindDesign(S.Player, designId);
            if (a == null || d == null || d.Retired) return "Выберите свою часть и действующую модель.";
            if (a.Target >= 0 || a.TrainingDays > 0 || S.Provinces[a.Region].Owner != S.Player) return "Оснащение доступно подготовленной части на стоянке в своей области.";
            int kits = support ? 1 : a.Battalions;
            if (d.Stock < kits) return "Нужно комплектов этой модели: " + kits + ".";
            if (!support && !d.Blueprint.Formation) return "Этот класс устанавливается как поддержка.";
            if (support && (d.Blueprint.Kind == EquipmentKind.Tank || d.Blueprint.Kind == EquipmentKind.IFV)) return "Танк и БМП используются как основа соединения.";
            if (support && (a.Attachments.Count >= 3 || a.Attachments.Any(x => x.Blueprint.Kind == d.Blueprint.Kind))) return "До трёх разных классов поддержки на соединение.";
            if (!support && a.DesignId == d.Id) return "Эта модель уже установлена.";
            int oldSize = ManpowerRules.Establishment(a);
            int newSize = ManpowerRules.GroundSize(support ? a.Design.Kind : d.Blueprint.Kind, a.Battalions, a.Attachments.Count + (support ? 1 : 0));
            int added = Math.Max(0, newSize - oldSize), released = Math.Max(0, a.Personnel - newSize);
            if (Player.Recruits < added) return "Для нового штата нужно дополнительно людей: " + added + ".";
            double cost = d.Blueprint.Cost * 0.15 * kits;
            if (Player.Treasury < cost) return "Недостаточно средств на переоснащение.";
            Player.Treasury -= cost; d.Stock -= kits; Player.Recruits += released - added;
            UnitQualityRules.IncorporateRecruits(a, added); a.Personnel += added - released;
            if (support) a.Attachments.Add(new EquipmentAttachment { DesignId = d.Id, Blueprint = d.Blueprint.Copy() });
            else {
                var old = FindDesign(S.Player, a.DesignId);
                if (old != null && a.Strength >= 90) old.Stock += a.Battalions;
                a.Design = d.Blueprint.Copy(); a.DesignId = d.Id; a.Strength = 100;
            }
            a.ExerciseRemaining = 0; a.TrainingDays = 14; a.Organization = Math.Min(a.Organization, 60);
            Notify("Соединение №" + a.Id + ": освоение «" + d.Blueprint.Name + "», 14 дней.");
            Raise(); return null;
        }
        private void EquipmentAI(int owner, List<int> regions)
        {
            Nation n = S.Nations[owner];
            n.Lines.RemoveAll(l => l.Completed >= l.Quantity);
            int capacity = FactoryCapacity(owner);
            if (capacity < 1 || n.Treasury < 1) return;
            if (S.Day % 180 == 0 && n.Designs.Count < 24) {
                var upgraded = EquipmentRules.Balanced(EquipmentKind.Tank, n, "Модель " + S.Day);
                if (upgraded.Power > n.Design.Power * 1.05) AdoptDesign(upgraded, owner);
            }
            var design = FindDesign(owner, n.ActiveDesign);
            if (design.Stock < 2 && !n.Lines.Any(l => l.DesignId == design.Id) && AssignedFactories(owner) < capacity)
                StartProduction(design.Id, Math.Min(2, capacity - AssignedFactories(owner)), 2, owner);
            int target = World.Countries[owner].StartingGroundPersonnel;
            if (target == 0) return;
            int desired = Math.Max(1, (int)Math.Ceiling(target / (double)ManpowerRules.GroundSize(design.Blueprint.Kind, SelectedTemplate(owner).Battalions)));
            if (design.Stock > 0 && S.Armies.Count(a => a.Owner == owner) < desired && ServicePersonnel(owner) < target) RecruitWithDesign(regions[0], design.Id, owner);
        }
    }
}
