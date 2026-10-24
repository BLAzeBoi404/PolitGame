using System;
using System.Collections.Generic;

namespace Sovereign
{
    public enum FrontStance { Hold, Advance }
    public class FrontPlan
    {
        public int Id;
        public int Owner;
        public int Enemy;
        public string Name = "Фронт";
        public FrontStance Stance;
        public bool Active = true;
        public List<int> ArmyIds = new List<int>();
    }
}
