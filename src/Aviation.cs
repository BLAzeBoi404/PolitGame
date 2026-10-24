namespace Sovereign
{
    public enum AirMission { Reserve, Superiority, Strike, Recon, Supply }
    // One wing consumes one manufactured equipment kit, like a ground formation.
    public class AirWing
    {
        public int Id, Owner, BaseRegion, DesignId;
        public double CasualtyRemainder;
        public int Personnel = 120;
        public int Target = -1, TrainingDays = 21, Sorties;
        public Blueprint Design;
        public AirMission Mission;
        public double Strength = 100;
    }
}
