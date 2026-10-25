using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public enum NationalDecision { Relief, Industry, Grants, Audit, WarBonds, Dialogue }
    public enum PolicyEventKind { Shortages, LaborDispute, WarWeariness, Procurement, ResearchOffer, Investment }
    public enum PolicyModifierKind { Hardship, EmergencyRelief, Rationing, Strike, LaborAccord, SecurityControl, ResearchFund, ResearchExchange, HomeSupport, CivilianPriority, Distrust, Transparency, Administration, Guarantees, SmallBusiness }

    public class DecisionState
    {
        public NationalDecision Kind;
        public int StartedDay = -1, ActiveUntil, CooldownUntil;
    }
    public class NationalModifier
    {
        public PolicyModifierKind Kind;
        public int StartedDay, UntilDay;
    }
    public class PolicyIncident
    {
        public int Id, CreatedDay, DeadlineDay;
        public PolicyEventKind Kind;
        public double Economy, Population;
    }
    public class PolicyRecord
    {
        public int Day;
        public string Title, Outcome;
    }
    public class NationalPolitics
    {
        public List<DecisionState> Decisions = new List<DecisionState>();
        public List<NationalModifier> Modifiers = new List<NationalModifier>();
        public List<PolicyRecord> History = new List<PolicyRecord>();
        public PolicyIncident Pending;
        public int NextIncident = 1, NextEventDay;
        public int[] EventCooldowns = new int[6];
    }
    public struct PolicyEffects
    {
        public double Output, Equipment, Research, Revenue, Approval, Consumption;
        public void Add(PolicyEffects other) { Output += other.Output; Equipment += other.Equipment; Research += other.Research; Revenue += other.Revenue; Approval += other.Approval; Consumption += other.Consumption; }
        public double GoodsFactor => Math.Max(.2, 1 + Output);
        public double EquipmentFactor => Math.Max(.2, 1 + Equipment);
        public double ResearchFactor => Math.Max(.2, 1 + Research);
        public double RevenueFactor => Math.Max(.2, 1 + Revenue);
        public double ConsumptionFactor => Math.Max(.5, 1 + Consumption);
    }
    public class DecisionDefinition
    {
        public NationalDecision Kind;
        public string Name, Description, Conditions, Consequences;
        public int Duration, Cooldown;
        public double CostShare, MinimumCost, Approval, Stability;
        public PolicyEffects Effects;
    }
    public class ModifierDefinition
    {
        public string Name, Description;
        public int Duration;
        public PolicyEffects Effects;
    }
    public class PolicyChoice
    {
        public string Name, Description;
        public double CostShare, Approval, Stability, Living, Repression;
        public bool Goods, Research, Partner, Pressure;
        public PolicyModifierKind? Modifier;
    }
    public class EventDefinition
    {
        public string Name, Description;
        public PolicyChoice[] Choices;
    }
    public static class PolicyRules
    {
        public static readonly DecisionDefinition[] Decisions = {
            new DecisionDefinition { Kind = NationalDecision.Relief, Name = "Адресная помощь", Description = "Оплатить временную поддержку семей и местных служб. Смягчает недовольство, но не создаёт продовольствие или товары.", Conditions = "Одобрение ниже 60% либо дефицит от 10%.", Consequences = "Сразу: одобрение +4. На 90 дней: целевое одобрение +6.", Duration = 90, Cooldown = 180, CostShare = .003, MinimumCost = .03, Approval = 4, Effects = new PolicyEffects { Approval = 6 } },
            new DecisionDefinition { Kind = NationalDecision.Industry, Name = "Промышленный заказ", Description = "Согласовать дополнительные смены и контракты. Выпуск по-прежнему ограничен сырьём, деньгами, рабочей силой и мощностями.", Conditions = "Стабильность не ниже 45%.", Consequences = "На 120 дней: выпуск товаров +6%, сборка техники +10%, целевое одобрение −2.", Duration = 120, Cooldown = 240, CostShare = .006, MinimumCost = .08, Effects = new PolicyEffects { Output = .06, Equipment = .10, Approval = -2 } },
            new DecisionDefinition { Kind = NationalDecision.Grants, Name = "Научные гранты", Description = "Финансировать лаборатории действующей программы. Поддержка действует ограниченное время и не выдаёт готовую технологию.", Conditions = "Активное исследование, научный бюджет от 1% и образование от 40.", Consequences = "На 180 дней: скорость исследований +20%.", Duration = 180, Cooldown = 360, CostShare = .004, MinimumCost = .04, Effects = new PolicyEffects { Research = .20 } },
            new DecisionDefinition { Kind = NationalDecision.Audit, Name = "Аудит госзакупок", Description = "Проверить сбор налогов и государственные контракты. Начальная перестройка ведомств вызывает политическое сопротивление.", Conditions = "Стабильность не ниже 35%.", Consequences = "Сразу: стабильность −3. На 90 дней: сбор налогов +5%, целевое одобрение +3.", Duration = 90, Cooldown = 270, CostShare = .002, MinimumCost = .02, Stability = -3, Effects = new PolicyEffects { Revenue = .05, Approval = 3 } },
            new DecisionDefinition { Kind = NationalDecision.WarBonds, Name = "Военные облигации", Description = "Привлечь заём в казну. Вся полученная сумма увеличивает государственный долг; проценты оплачиваются обычным бюджетом.", Conditions = "Война, стабильность от 40%; долг после займа не выше 150% ВВП.", Consequences = "Заём: 4% ВВП, минимум 0,10 млрд. На 180 дней: целевое одобрение −4.", Duration = 180, Cooldown = 360, Effects = new PolicyEffects { Approval = -4 } },
            new DecisionDefinition { Kind = NationalDecision.Dialogue, Name = "Общественный диалог", Description = "Провести переговоры с местными объединениями и профсоюзами. Программа требует расходов и умеренного политического давления.", Conditions = "Мир, одобрение ниже 65%, давление не выше 40.", Consequences = "Сразу: одобрение +3, стабильность +2. На 90 дней: целевое одобрение +5.", Duration = 90, Cooldown = 180, CostShare = .003, MinimumCost = .03, Approval = 3, Stability = 2, Effects = new PolicyEffects { Approval = 5 } }
        };
        public static readonly ModifierDefinition[] Modifiers = {
            new ModifierDefinition { Name = "Неразрешённый дефицит", Description = "Выпуск товаров −3%; целевое одобрение −4", Duration = 90, Effects = new PolicyEffects { Output = -.03, Approval = -4 } },
            new ModifierDefinition { Name = "Экстренная поддержка", Description = "Целевое одобрение +4", Duration = 60, Effects = new PolicyEffects { Approval = 4 } },
            new ModifierDefinition { Name = "Нормирование", Description = "Бытовое потребление продовольствия и товаров −15%; целевое одобрение −3", Duration = 90, Effects = new PolicyEffects { Consumption = -.15, Approval = -3 } },
            new ModifierDefinition { Name = "Забастовки", Description = "Выпуск товаров и сборка техники −10%", Duration = 45, Effects = new PolicyEffects { Output = -.10, Equipment = -.10 } },
            new ModifierDefinition { Name = "Трудовое соглашение", Description = "Выпуск товаров +2%", Duration = 90, Effects = new PolicyEffects { Output = .02 } },
            new ModifierDefinition { Name = "Усиленный контроль", Description = "Выпуск товаров −4%", Duration = 90, Effects = new PolicyEffects { Output = -.04 } },
            new ModifierDefinition { Name = "Исследовательский фонд", Description = "Скорость исследований +25%", Duration = 120, Effects = new PolicyEffects { Research = .25 } },
            new ModifierDefinition { Name = "Научный обмен", Description = "Скорость исследований +12%", Duration = 180, Effects = new PolicyEffects { Research = .12 } },
            new ModifierDefinition { Name = "Поддержка семей", Description = "Целевое одобрение +4", Duration = 90, Effects = new PolicyEffects { Approval = 4 } },
            new ModifierDefinition { Name = "Гражданский приоритет", Description = "Выпуск товаров +3%; сборка техники −15%", Duration = 90, Effects = new PolicyEffects { Output = .03, Equipment = -.15 } },
            new ModifierDefinition { Name = "Недоверие к закупкам", Description = "Целевое одобрение −4", Duration = 90, Effects = new PolicyEffects { Approval = -4 } },
            new ModifierDefinition { Name = "Прозрачные контракты", Description = "Сбор налогов +3%", Duration = 120, Effects = new PolicyEffects { Revenue = .03 } },
            new ModifierDefinition { Name = "Смена администрации", Description = "Выпуск товаров −3%; сборка техники −5%", Duration = 60, Effects = new PolicyEffects { Output = -.03, Equipment = -.05 } },
            new ModifierDefinition { Name = "Инвестиционные гарантии", Description = "Выпуск товаров и сборка техники +5%", Duration = 180, Effects = new PolicyEffects { Output = .05, Equipment = .05 } },
            new ModifierDefinition { Name = "Малое предпринимательство", Description = "Выпуск товаров +3%", Duration = 90, Effects = new PolicyEffects { Output = .03 } }
        };
        public static readonly EventDefinition[] Events = {
            new EventDefinition { Name = "Перебои с товарами", Description = "Местные службы сообщают о заметном дефиците. Кабинету предстоит выбрать между расходованием запасов, нормированием и сохранением текущего курса.", Choices = new[] {
                new PolicyChoice { Name = "Отложить вмешательство", Description = "Одобрение −4; стабильность −2.", Approval = -4, Stability = -2, Modifier = PolicyModifierKind.Hardship },
                new PolicyChoice { Name = "Распределить резерв", Description = "Одобрение +6; стабильность +2; уровень жизни +3. Расходуются продовольствие и потребительские товары.", CostShare = .003, Goods = true, Approval = 6, Stability = 2, Living = 3, Modifier = PolicyModifierKind.EmergencyRelief },
                new PolicyChoice { Name = "Ввести нормирование", Description = "Одобрение −5; уровень жизни −3. Снижается именно бытовой спрос, не промышленный расход.", Approval = -5, Living = -3, Modifier = PolicyModifierKind.Rationing }
            } },
            new EventDefinition { Name = "Трудовой конфликт", Description = "На фоне низкого одобрения и нестабильности трудовые объединения требуют пересмотра условий. Длительный спор грозит остановками производства.", Choices = new[] {
                new PolicyChoice { Name = "Сохранить условия", Description = "Стабильность −4.", Stability = -4, Modifier = PolicyModifierKind.Strike },
                new PolicyChoice { Name = "Согласовать выплаты", Description = "Одобрение +5; стабильность +3.", CostShare = .004, Approval = 5, Stability = 3, Modifier = PolicyModifierKind.LaborAccord },
                new PolicyChoice { Name = "Ограничить протест", Description = "Нужно давление от 20. Стабильность +5; одобрение −8; давление до +5.", CostShare = .001, Stability = 5, Approval = -8, Repression = 5, Pressure = true, Modifier = PolicyModifierKind.SecurityControl }
            } },
            new EventDefinition { Name = "Усталость от войны", Description = "Война продолжается не менее 60 дней. Семьи военнослужащих и гражданские предприятия ожидают поддержки; перераспределение ресурсов ослабит военный выпуск.", Choices = new[] {
                new PolicyChoice { Name = "Сохранить курс", Description = "Одобрение −5; стабильность −3.", Approval = -5, Stability = -3 },
                new PolicyChoice { Name = "Помочь семьям", Description = "Одобрение +6; стабильность +2.", CostShare = .004, Approval = 6, Stability = 2, Modifier = PolicyModifierKind.HomeSupport },
                new PolicyChoice { Name = "Гражданский приоритет", Description = "Одобрение +8; часть мощностей отвлекается от военной сборки.", CostShare = .002, Approval = 8, Modifier = PolicyModifierKind.CivilianPriority }
            } },
            new EventDefinition { Name = "Вопросы к госзакупкам", Description = "При высоких военных расходах или политическом давлении выросли претензии к прозрачности контрактов. Кабинет должен определить форму проверки.", Choices = new[] {
                new PolicyChoice { Name = "Закрытая проверка", Description = "Одобрение −4.", Approval = -4, Modifier = PolicyModifierKind.Distrust },
                new PolicyChoice { Name = "Открытая комиссия", Description = "Стабильность −3; одобрение +7.", CostShare = .002, Stability = -3, Approval = 7, Modifier = PolicyModifierKind.Transparency },
                new PolicyChoice { Name = "Сменить руководство ведомства", Description = "Стабильность +2; одобрение +2. Перестройка временно замедлит выпуск.", CostShare = .001, Stability = 2, Approval = 2, Modifier = PolicyModifierKind.Administration }
            } },
            new EventDefinition { Name = "Предложение исследователей", Description = "Исследовательские группы подготовили расширение текущей программы. Можно увеличить финансирование, привлечь торговых партнёров или оставить обычный бюджет.", Choices = new[] {
                new PolicyChoice { Name = "Оставить обычный бюджет", Description = "Без дополнительных затрат и эффектов." },
                new PolicyChoice { Name = "Учредить научный фонд", Description = "Требуется действующее исследование.", CostShare = .003, Research = true, Modifier = PolicyModifierKind.ResearchFund },
                new PolicyChoice { Name = "Организовать научный обмен", Description = "Нужны исследование и торговый договор с мирным партнёром.", CostShare = .0015, Research = true, Partner = true, Modifier = PolicyModifierKind.ResearchExchange }
            } },
            new EventDefinition { Name = "Окно для инвестиций", Description = "Мир и устойчивая власть позволяют предприятиям планировать расширение. Государственная поддержка ускорит выпуск, но потребует средств казны.", Choices = new[] {
                new PolicyChoice { Name = "Сохранить бюджет", Description = "Без дополнительных затрат и эффектов." },
                new PolicyChoice { Name = "Оплатить гарантии", Description = "Одобрение −1; поддержка выпуска на 180 дней.", CostShare = .004, Approval = -1, Modifier = PolicyModifierKind.Guarantees },
                new PolicyChoice { Name = "Поддержать малый бизнес", Description = "Одобрение +2; поддержка выпуска на 90 дней.", CostShare = .002, Approval = 2, Modifier = PolicyModifierKind.SmallBusiness }
            } }
        };
        public static double DecisionCost(DecisionDefinition d, Nation n) => d.CostShare == 0 ? 0 : Math.Max(d.MinimumCost, Math.Max(0, n.GDP) * d.CostShare);
        public static double BondAmount(Nation n) => Math.Max(.10, n.GDP * .04);
        public static double ChoiceCost(PolicyIncident e, PolicyChoice c) => c.CostShare == 0 ? 0 : Math.Max(.02, e.Economy * c.CostShare);
        public static PolicyEffects Effects(Nation n, int day)
        {
            var total = new PolicyEffects(); if (n.Politics == null) return total;
            foreach (var d in n.Politics.Decisions) if (d.ActiveUntil > day) total.Add(Decisions[(int)d.Kind].Effects);
            foreach (var m in n.Politics.Modifiers) if (m.UntilDay > day) total.Add(Modifiers[(int)m.Kind].Effects);
            return total;
        }
    }
}
