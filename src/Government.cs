using System;

namespace Sovereign
{
    public class GovernmentState
    {
        public int Form, ChangedDay = -1, LastStartedDay = -365;
        public int Target = -1, StartedDay = -1, Phase, Work, Choice = -1, ConsultationDay = -1, ChoiceDay = -1;
        public double Paid, QuotedGDP, ChoicePaid;
        public bool Paused;
    }
    public class GovernmentDefinition
    {
        public string Name, Description;
        public double PressureLimit, StabilityTarget;
        public PolicyEffects Effects;
    }
    public static class GovernmentRules
    {
        public const int DraftDays = 45, ResponseDays = 21, Cooldown = 365;
        public static readonly GovernmentDefinition[] Forms = {
            new GovernmentDefinition { Name = "Существующий порядок", Description = "Нейтральная исходная система. Историческая форма правления страны в модели не назначена.", PressureLimit = 100 },
            new GovernmentDefinition { Name = "Парламентская республика", Description = "Кабинет опирается на представительство и согласование. Поддержка исследований и одобрения ценой меньшего сбора налогов.", PressureLimit = 30, Effects = new PolicyEffects { Research = .08, Approval = 4, Revenue = -.03 } },
            new GovernmentDefinition { Name = "Президентская республика", Description = "Исполнительная власть координирует ведомства. Приоритет военной сборки и сбора налогов снижает целевое одобрение.", PressureLimit = 60, Effects = new PolicyEffects { Equipment = .06, Revenue = .03, Approval = -2 } },
            new GovernmentDefinition { Name = "Конституционная монархия", Description = "Преемственность институтов поддерживает выпуск и устойчивость. Согласительные процедуры уменьшают сбор налогов.", PressureLimit = 45, StabilityTarget = 4, Effects = new PolicyEffects { Output = .03, Revenue = -.04 } },
            new GovernmentDefinition { Name = "Централизованная республика", Description = "Единая вертикаль ускоряет военные заказы и укрепляет устойчивость. Научная автономия и целевое одобрение ниже.", PressureLimit = 90, StabilityTarget = 3, Effects = new PolicyEffects { Equipment = .08, Research = -.06, Approval = -4 } }
        };
        public static bool ValidTarget(int target) => target > 0 && target < Forms.Length;
        public static double ReformCost(double gdp) => Math.Max(.30, gdp * .006);
        public static double ConsultationCost(double gdp) => Math.Max(.10, gdp * .002);
        public static int ImplementationDays(int choice) => choice == 0 ? 60 : 30;
        public static PolicyEffects Effects(Nation n)
        {
            if (n.Government == null) return new PolicyEffects();
            var effects = Forms[n.Government.Form].Effects;
            if (n.Government.Target >= 0) { effects.Output -= .03; effects.Research -= .05; effects.Revenue -= .04; }
            return effects;
        }
        public static PolicyEffects CombinedEffects(Nation n, int day)
        {
            var effects = PolicyRules.Effects(n, day); effects.Add(Effects(n)); return effects;
        }
        public static double StabilityTarget(Nation n) => n.Government == null ? 0 : Forms[n.Government.Form].StabilityTarget;
    }
}
