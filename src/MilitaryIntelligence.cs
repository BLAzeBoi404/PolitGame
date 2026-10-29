using System;

namespace Sovereign
{
    public enum IntelSource { GroundContact, AirRecon }

    // Deliberately contains no unit IDs, blueprints, routes, readiness or exact strength.
    // A report survives loss of contact at its observed position, never follows the unit.
    public sealed class MilitaryContact
    {
        public int Observer, Owner, Region, Day, LowerPersonnel, UpperPersonnel;
        public IntelSource Source;
    }

    public static class IntelligenceRules
    {
        public const int ContactLifetime = 3;
        public static int EstimateStep(long people) => people < 2000 ? 500 : people < 10000 ? 2000 : people < 50000 ? 10000 : 25000;
        public static MilitaryContact Observe(int observer, int owner, int region, int day, long people, IntelSource source)
        {
            int step = EstimateStep(people);
            return new MilitaryContact { Observer = observer, Owner = owner, Region = region, Day = day, Source = source,
                LowerPersonnel = (int)(people / step * step), UpperPersonnel = (int)Math.Min(int.MaxValue, people / step * step + step) };
        }
    }
}
