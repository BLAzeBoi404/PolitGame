using System;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        public void InitializeManpower(Campaign campaign)
        {
            var deployed = new long[campaign.Nations.Length];
            foreach (var a in campaign.Armies) {
                a.CasualtyRemainder = 0; a.TemplateId = 1; a.Battalions = 1; a.Organization = a.TrainingDays > 0 ? 30 : 100;
                a.Personnel = (int)Math.Round(ManpowerRules.Establishment(a) * (a.TrainingDays > 0 ? 1 : a.Strength / 100));
                deployed[a.Owner] += a.Personnel;
            }
            foreach (var a in campaign.AirWings) {
                a.CasualtyRemainder = 0; a.Personnel = (int)Math.Round(ManpowerRules.AirSize(a.Design.Kind) * (a.TrainingDays > 0 ? 1 : a.Strength / 100)); deployed[a.Owner] += a.Personnel;
            }
            for (int i = 0; i < campaign.Nations.Length; i++) {
                var n = campaign.Nations[i]; n.Templates = ManpowerRules.Defaults(); n.NextTemplate = 4; n.ActiveTemplate = 1;
                n.Conscription = ConscriptionLaw.Volunteers; n.LawChangedDay = -90;
                long cap = Capacity(campaign, i);
                n.Recruits = (int)Math.Min(Math.Max(0, cap - deployed[i]), deployed[i] + 1000);
                n.Mobilized = deployed[i] + n.Recruits; n.Discharged = n.PersonnelLosses = 0;
            }
            campaign.Version = 5;
        }
        private long Capacity(Campaign campaign, int owner)
        {
            double population = World.Countries[owner].Regions.Where(r => campaign.Provinces[r].Owner == owner).Sum(r => campaign.Provinces[r].Population);
            return (long)Math.Min(1000000000, Math.Max(0, population * 1000000 * ManpowerRules.PopulationShare[(int)campaign.Nations[owner].Conscription]));
        }
        public long ManpowerCapacity(int owner) => Capacity(S, owner);
        public long ServicePersonnel(int owner) => S.Armies.Where(a => a.Owner == owner).Sum(a => (long)a.Personnel) + S.AirWings.Where(a => a.Owner == owner).Sum(a => (long)a.Personnel);
        public long ManpowerHeadroom(int owner) => Math.Max(0, ManpowerCapacity(owner) - (S.Nations[owner].Mobilized - S.Nations[owner].Discharged));
        public int WeeklyIntake(int owner)
        {
            var n = S.Nations[owner]; long cap = ManpowerCapacity(owner);
            return (int)Math.Min(ManpowerHeadroom(owner), Math.Max(0, cap / 52.0 * Rules.Clamp(n.Education / 70, .5, 1.3) * Rules.Clamp(n.Military / 2.5, .4, 1.3) * (1 + TechnologyRules.Effects(n).Intake)));
        }
        public FormationTemplate SelectedTemplate(int owner) => S.Nations[owner].Templates.First(t => t.Id == S.Nations[owner].ActiveTemplate);
        public string RecruitmentReason(int owner, int designId, int templateId)
        {
            var n = S.Nations[owner]; var d = FindDesign(owner, designId); var t = n.Templates.FirstOrDefault(p => p.Id == templateId);
            if (d == null || !d.Blueprint.Formation || d.Retired || t == null) return "Выберите действующий наземный проект и штат.";
            if (d.Stock < t.Battalions) return "Нужно " + t.Battalions + " готовых комплектов. На складе: " + d.Stock + ".";
            int people = ManpowerRules.GroundSize(d.Blueprint.Kind, t.Battalions);
            if (n.Recruits < people) return "Нужно " + people + " человек. В резерве: " + n.Recruits + ".";
            if (n.Treasury < .3 * t.Battalions) return "Для набора нужно " + (.3 * t.Battalions).ToString("0.00") + " млрд.";
            return null;
        }
        public string CreateTemplate(string name, int battalions)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 32 || name.Any(char.IsControl)) return "Название штата: 1–32 печатных символа.";
            if (battalions < 1 || battalions > 6) return "Штат: от одного до шести батальонов одной основной модели.";
            if (Player.Templates.Count >= 24) return "Достигнут предел: 24 штата.";
            if (Player.Templates.Any(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))) return "Укажите уникальное название штата.";
            var template = new FormationTemplate { Id = Player.NextTemplate++, Name = name.Trim(), Battalions = battalions };
            Player.Templates.Add(template); Player.ActiveTemplate = template.Id; Raise(); return null;
        }
        public string SelectTemplate(int id)
        {
            if (!Player.Templates.Any(t => t.Id == id)) return "Штат не найден.";
            Player.ActiveTemplate = id; Raise(); return null;
        }
        public string ConscriptionReason(ConscriptionLaw law, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            if (!Enum.IsDefined(typeof(ConscriptionLaw), law)) return "Неизвестный режим комплектования.";
            var n = S.Nations[owner];
            if (law == n.Conscription) return "Этот режим уже действует.";
            if (S.Day - n.LawChangedDay < 90) return "Следующая смена режима через " + (90 - S.Day + n.LawChangedDay) + " дн.";
            if (law > n.Conscription) {
                if (n.Stability < 35) return "Для расширения призыва нужна стабильность не ниже 35%.";
                if (law >= ConscriptionLaw.Extensive && !Enumerable.Range(0, S.Nations.Length).Any(c => AtWar(owner, c))) return "Расширенный и всеобщий призыв доступны только во время войны.";
                if (n.Treasury < .15 * ((int)law - (int)n.Conscription)) return "Недостаточно денег на развёртывание призывной системы.";
            }
            return null;
        }
        public string SetConscription(ConscriptionLaw law, int owner = -1)
        {
            if (owner < 0) owner = S.Player;
            string reason = ConscriptionReason(law, owner); if (reason != null) return reason;
            var n = S.Nations[owner]; int delta = (int)law - (int)n.Conscription;
            if (delta > 0) { n.Treasury -= delta * .15; n.Approval = Math.Max(0, n.Approval - delta * 3); n.Stability = Math.Max(0, n.Stability - delta * 1.5); }
            n.Conscription = law; n.LawChangedDay = S.Day; TrimReserve(owner);
            if (owner == S.Player) Notify("Комплектование: " + ManpowerRules.LawNames[(int)law] + ". Пополнение резерва поступает еженедельно.");
            if (owner == S.Player) ComputeEconomy(false); Raise(); return null;
        }
        private void TrimReserve(int owner)
        {
            var n = S.Nations[owner]; long excess = Math.Max(0, n.Mobilized - n.Discharged - ManpowerCapacity(owner));
            int release = (int)Math.Min(n.Recruits, excess); n.Recruits -= release; n.Discharged += release;
        }
        public void TickManpower(bool[] network = null)
        {
            if (S.Day > 0 && S.Day % 7 == 0) for (int owner = 0; owner < S.Nations.Length; owner++) {
                TrimReserve(owner); int intake = WeeklyIntake(owner); var n = S.Nations[owner];
                n.Recruits += intake; n.Mobilized += intake;
            }
            network = network ?? SupplyNetwork();
            foreach (var a in S.Armies) {
                if (a.TrainingDays > 0 || a.Target >= 0) continue;
                bool supplied = IsSupplied(a.Owner, a.Region, network);
                if (supplied && a.Supply >= 60 && S.Provinces[a.Region].Owner == a.Owner) {
                    int replacements = DrawReplacements(a.Owner, ManpowerRules.Establishment(a) - a.Personnel, Math.Max(1, (int)(ManpowerRules.Establishment(a) / 100.0 * (1 + TechnologyRules.Effects(S.Nations[a.Owner]).Replacements))));
                    a.Personnel += replacements;
                    if (replacements > 0) a.Organization = Math.Max(0, a.Organization - replacements * 20.0 / ManpowerRules.Establishment(a));
                }
                if (supplied && S.Nations[a.Owner].Treasury >= a.Design.Maintenance * a.Battalions)
                    a.Organization = Math.Min(100, a.Organization + 2 * a.Supply / 100);
                else a.Organization = Math.Max(0, a.Organization - .4);
            }
            foreach (var a in S.AirWings) if (a.Mission == AirMission.Reserve && a.TrainingDays == 0 && S.Provinces[a.BaseRegion].Owner == a.Owner && IsSupplied(a.Owner, a.BaseRegion, network))
                a.Personnel += DrawReplacements(a.Owner, ManpowerRules.AirSize(a.Design.Kind) - a.Personnel, 2);
        }
        private int DrawReplacements(int owner, int missing, int daily)
        {
            var n = S.Nations[owner];
            int number = Math.Min(Math.Max(0, missing), Math.Min(n.Recruits, daily));
            number = (int)Math.Min(number, Math.Min(Math.Max(0, n.Treasury) / ManpowerRules.ReplacementCost, n.Stock[7] / ManpowerRules.ReplacementArms));
            if (number <= 0) return 0;
            n.Recruits -= number; n.Treasury = Math.Max(0, n.Treasury - number * ManpowerRules.ReplacementCost); n.Stock[7] = Math.Max(0, n.Stock[7] - number * ManpowerRules.ReplacementArms);
            return number;
        }
        internal void DamageArmy(Army a, double damage, double casualtyRate = .4, double floor = 0)
        {
            double applied = Math.Max(0, Math.Min(a.Strength - floor, damage));
            a.Strength -= applied; a.Organization = Math.Max(0, a.Organization - applied * 1.2);
            double casualties = a.CasualtyRemainder + ManpowerRules.Establishment(a) * applied / 100 * casualtyRate * (1 + TechnologyRules.Effects(S.Nations[a.Owner]).Casualties);
            int losses = Math.Min(a.Personnel, (int)Math.Floor(casualties)); a.CasualtyRemainder = casualties - Math.Floor(casualties);
            a.Personnel -= losses; S.Nations[a.Owner].PersonnelLosses += losses;
        }
        internal void DamageAirWing(AirWing a, double damage, double floor = 0)
        {
            double applied = Math.Max(0, Math.Min(a.Strength - floor, damage)); a.Strength -= applied;
            double casualties = a.CasualtyRemainder + ManpowerRules.AirSize(a.Design.Kind) * applied / 100 * .3 * (1 + TechnologyRules.Effects(S.Nations[a.Owner]).Casualties);
            int losses = Math.Min(a.Personnel, (int)Math.Floor(casualties)); a.CasualtyRemainder = casualties - Math.Floor(casualties);
            a.Personnel -= losses; S.Nations[a.Owner].PersonnelLosses += losses;
        }
        internal void DestroyArmy(Army a)
        {
            if (!S.Armies.Remove(a)) return;
            S.Nations[a.Owner].PersonnelLosses += a.Personnel; a.Personnel = 0; DetachFromFront(a.Id);
        }
        internal void DestroyAirWing(AirWing a)
        {
            if (!S.AirWings.Remove(a)) return;
            S.Nations[a.Owner].PersonnelLosses += a.Personnel; a.Personnel = 0;
        }
        public double ReorganizationCost(Army a, FormationTemplate template) => .05 + .15 * Math.Abs(template.Battalions - a.Battalions);
        public string ReorganizationReason(int id, int templateId)
        {
            var a = S.Armies.FirstOrDefault(u => u.Id == id && u.Owner == S.Player); var t = Player.Templates.FirstOrDefault(p => p.Id == templateId);
            if (a == null || t == null) return "Выберите свою часть на карте и штат в списке.";
            if (a.TemplateId == t.Id) return "Этот штат уже установлен.";
            if (a.Target >= 0 || a.TrainingDays > 0 || S.Provinces[a.Region].Owner != S.Player || !IsSupplied(S.Player, a.Region, SupplyNetwork())) return "Нужна подготовленная часть на стоянке в своей снабжаемой области.";
            int extra = Math.Max(0, t.Battalions - a.Battalions);
            if (FindDesign(S.Player, a.DesignId).Stock < extra) return "Не хватает комплектов основной модели: нужно ещё " + extra + ".";
            int needed = Math.Max(0, ManpowerRules.GroundSize(a.Design.Kind, t.Battalions, a.Attachments.Count) - ManpowerRules.Establishment(a));
            if (Player.Recruits < needed) return "Для расширения штата нужно ещё людей: " + needed + ".";
            if (Player.Treasury < ReorganizationCost(a, t)) return "Недостаточно денег на перестройку штата.";
            return null;
        }
        public string ReorganizeArmy(int id, int templateId)
        {
            string reason = ReorganizationReason(id, templateId); if (reason != null) return reason;
            var a = S.Armies.First(u => u.Id == id); var t = Player.Templates.First(p => p.Id == templateId);
            var d = FindDesign(S.Player, a.DesignId); int delta = t.Battalions - a.Battalions;
            int size = ManpowerRules.GroundSize(a.Design.Kind, t.Battalions, a.Attachments.Count);
            int added = Math.Max(0, size - ManpowerRules.Establishment(a)), released = Math.Max(0, a.Personnel - size);
            Player.Treasury -= ReorganizationCost(a, t); Player.Recruits += released - added; a.Personnel += added - released;
            if (delta > 0) { d.Stock -= delta; a.Strength = (a.Strength * a.Battalions + delta * 100) / t.Battalions; }
            else if (delta < 0) d.Stock += a.Strength >= 90 ? -delta : (int)Math.Floor(-delta * a.Strength / 100);
            a.Battalions = t.Battalions; a.TemplateId = t.Id; a.TrainingDays = 14 + 3 * Math.Abs(delta); a.Organization = Math.Min(a.Organization, 40);
            DetachFromFront(a.Id); TrimReserve(S.Player);
            Notify("Часть №" + id + " переходит на штат «" + t.Name + "»: " + a.TrainingDays + " дней подготовки."); Raise(); return null;
        }
        public string DisbandArmy(int id)
        {
            var a = S.Armies.FirstOrDefault(u => u.Id == id && u.Owner == S.Player);
            if (a == null) return "Выберите свою часть.";
            if (a.Target >= 0 || S.Provinces[a.Region].Owner != S.Player || !IsSupplied(S.Player, a.Region, SupplyNetwork())) return "Расформирование требует стоянки в своей области с сухопутным снабжением.";
            Player.Recruits += a.Personnel; a.Personnel = 0;
            var design = FindDesign(S.Player, a.DesignId);
            int kits = a.Strength >= 90 ? a.Battalions : (int)Math.Floor(a.Battalions * a.Strength / 100);
            design.Stock += kits;
            foreach (var support in a.Attachments) if (support.Condition >= 90) FindDesign(S.Player, support.DesignId).Stock++;
            S.Armies.Remove(a); DetachFromFront(id); TrimReserve(S.Player);
            Notify("Часть №" + id + " расформирована. Личный состав возвращён в резерв, техника: " + kits + " компл."); Raise(); return null;
        }
        internal void PlanManpower(int owner)
        {
            var n = S.Nations[owner]; bool war = Enumerable.Range(0, S.Nations.Length).Any(c => AtWar(owner, c));
            if (war && n.Recruits < Math.Max(1600, ServicePersonnel(owner) / 5) && n.Conscription < ConscriptionLaw.Extensive)
                SetConscription(n.Conscription + 1, owner);
            else if (!war && n.Conscription > ConscriptionLaw.Volunteers) SetConscription(n.Conscription - 1, owner);
            var d = FindDesign(owner, n.ActiveDesign);
            n.ActiveTemplate = war && d.Blueprint.Formation && d.Stock >= 2 && n.Recruits >= ManpowerRules.GroundSize(d.Blueprint.Kind, 2) && n.Treasury >= .6 ? 2 : 1;
        }
    }
}
