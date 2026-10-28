using System.Collections.Generic;

namespace Sovereign
{
	public class Army
	{
		public int Id;
        public BattleDeployment Deployment;
        public double CasualtyRemainder;
        public int Personnel = 800;
        public int TemplateId = 1, Battalions = 1;
        public double Organization = 100;
        public double Fatigue;
        public int ExerciseRemaining;

		public int Owner;

		public int Region;

		public int Target = -1;

		public List<int> Route = new List<int>();

		public double Days;

		public double TotalDays;

		public double Strength = 100.0;

		public double Supply = 100.0;

		public double Experience;

		public Blueprint Design = new Blueprint();

        public int DesignId;
        public int TrainingDays;
        public double SupplyDays = 5;
        public List<EquipmentAttachment> Attachments = new List<EquipmentAttachment>();
	}
}
