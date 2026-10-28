using System;

namespace Sovereign
{
    public sealed class MilitaryCondition
    {
        public double Exhaustion;
        public long AccountedLosses;
    }

    public sealed class MilitaryAssessment
    {
        public int Formations, Exercising, Ready, Battalions;
        public double Readiness, DailyUpkeep, DailyCommitment, FuelDays, ArmsDays;
        public bool AtWar;
        public string Posture, Reason;
    }

    public static class ReadinessRules
    {
        public const int ExerciseLength = 21;
        public const double ExerciseExperienceLimit = 35;
        public static double FatigueFactor(Army army) => 1 - Rules.Clamp(army.Fatigue, 0, 100) * .004;
        public static double Score(Army army) => army.TrainingDays > 0 ? 0 : Math.Min(army.Strength, army.Organization)
            * ManpowerRules.Manning(army) * (.5 + .5 * army.Supply / 100) * FatigueFactor(army);
        public static double Exhaustion(Nation nation) => nation.Condition == null ? 0 : nation.Condition.Exhaustion;
        public static double ExerciseArms(Army army) => .01 * army.Battalions;
        public static double ExerciseFuel(Army army) => (.003 + army.Design.Fuel * .15) * army.Battalions;
        public static double ExerciseCash(Army army) => army.Personnel * .000001;
    }
}
