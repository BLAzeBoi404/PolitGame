using System;

namespace Sovereign
{
    public static class UnitQualityRules
    {
        public const double ExerciseTrainingLimit = 90;
        public const double ReplacementTraining = 30;
        public static readonly string[] Names = { "Формирование", "Начальная", "Базовая", "Регулярная", "Профессиональная", "Элитная" };
        public static int Grade(Army army)
        {
            if (army.TrainingDays > 0 || army.Personnel == 0) return 0;
            if (army.Proficiency < 30) return 1;
            if (army.Proficiency < 50) return 2;
            if (army.Proficiency < 70) return 3;
            if (army.Proficiency < 85 || army.Experience < 35) return 4;
            return 5;
        }
        public static string Name(Army army) => Names[Grade(army)];
        public static double CombatFactor(Army army) => .70 + Rules.Clamp(army.Proficiency, 0, 100) * .006;
        public static void IncorporateRecruits(Army army, int added)
        {
            if (added <= 0) return;
            army.Experience *= (double)army.Personnel / (army.Personnel + added);
            army.Proficiency = (army.Proficiency * army.Personnel + ReplacementTraining * added) / (army.Personnel + added);
        }
        public static double ScenarioTraining(CountryInfo country, int serial)
        {
            // Abstract priority / regular / lower-priority cohorts, NOT actual named
            // regiments. Country resources shape the mean, not an immutable ceiling.
            int cohort = serial % 10;
            return Rules.Clamp(country.TrainingBaseline + (cohort == 0 ? 20 : cohort >= 8 ? -15 : (serial % 3 - 1) * 4), 15, 90);
        }
    }
}
