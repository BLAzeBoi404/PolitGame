using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        public void InitializeTreaties(Campaign campaign)
        {
            int size = campaign.Nations.Length * campaign.Nations.Length;
            campaign.Access = new bool[size]; campaign.Alliances = new bool[size];
            campaign.WarStarted = new int[size]; campaign.TruceUntil = new int[size];
            campaign.Fronts = new List<FrontPlan>(); campaign.NextFront = 1;
            campaign.Version = 3;
        }
        public bool Allied(int a, int b) => a != b && S.Alliances[Pair(a, b)];
        public bool HasAccess(int guest, int host) => guest == host || Allied(guest, host) || S.Access[Pair(guest, host)];
        public bool CanEnter(int guest, int host) => HasAccess(guest, host) || AtWar(guest, host);
        public double Occupation(int occupant, int homeland)
        {
            double total = 0, occupied = 0;
            foreach (int r in World.Countries[homeland].Regions) {
                double weight = Math.Max(0.01, S.Provinces[r].Population);
                total += weight; if (S.Provinces[r].Owner == occupant) occupied += weight;
            }
            return total > 0 ? occupied / total : 0;
        }
        public string PeaceReason(int other, bool restoreBorders)
        {
            int me = S.Player;
            if (!AtWar(me, other)) return "Между странами нет войны.";
            int days = S.Day - S.WarStarted[Pair(me, other)];
            double ours = Occupation(me, other), theirs = Occupation(other, me);
            if (restoreBorders) {
                if (theirs > ours + 0.05 && days < 360) return "Противник удерживает преимущество и пока отвергает возврат территорий. Утомление войной: " + days + "/360 дн.";
                if (days < 30 && ours < 0.15) return "Противник пока не готов к переговорам: нужны 30 дней войны или контроль 15% его населения.";
            } else if (ours > theirs + 0.05 && ours < 0.35 && days < 180)
                return "Противник не согласен уступить территории: нужны контроль 35% населения либо 180 дней войны.";
            return null;
        }
        public string DiplomacyReason(int other, int action)
        {
            int me = S.Player;
            if (other < 0 || other >= S.Nations.Length || other == me || action < 0 || action > 8) return "Выберите другую страну и действие.";
            int pair = Pair(me, other);
            if (action == 0) return Player.Treasury >= 0.1 ? null : "Нужно 0,10 млрд для дипломатической миссии.";
            if (action == 2) return null;
            if (action == 3) {
                if (AtWar(me, other)) return "Война уже идёт.";
                if (Allied(me, other)) return "Сначала расторгните оборонный союз.";
                if (S.TruceUntil[pair] > S.Day) return "Перемирие действует ещё " + (S.TruceUntil[pair] - S.Day) + " дн.";
                // A defensive pact must not pull an attacker into war with its own ally.
                if (Enumerable.Range(0, S.Nations.Length).Any(c => Allied(other, c) && Allied(me, c))) return "Общий союзник блокирует объявление войны. Сначала урегулируйте союзные обязательства.";
                return null;
            }
            if (action == 4 || action == 8) return PeaceReason(other, action == 8);
            if (AtWar(me, other)) return "Сначала заключите мир.";
            if (action == 1 && !S.Agreements[pair] && S.Relations[pair] < 20) return "Нужны отношения не ниже +20.";
            if (action == 5 && !Allied(me, other)) {
                if (S.Relations[pair] < 50) return "Для оборонного союза нужны отношения +50.";
                for (int c = 0; c < S.Nations.Length; c++)
                    if (AtWar(me, c) || AtWar(other, c)) return "Оборонный союз заключается в мирное время.";
            }
            if (action == 6 && !S.Access[pair] && S.Relations[pair] < 30) return "Для военного доступа нужны отношения +30.";
            return null;
        }
        public string Diplomacy(int other, int action)
        {
            string error = DiplomacyReason(other, action); if (error != null) return error;
            int me = S.Player, pair = Pair(me, other), reverse = Pair(other, me);
            string country = World.Countries[other].Name;
            switch (action) {
                case 0:
                    Player.Treasury -= 0.1;
                    S.Relations[pair] = S.Relations[reverse] = Math.Min(100, S.Relations[pair] + 15);
                    Notify("Дипломатическая миссия: " + country + ". Отношения +15.", 1); break;
                case 1:
                    S.Agreements[pair] = S.Agreements[reverse] = !S.Agreements[pair];
                    Notify("Торговый договор: " + country + (S.Agreements[pair] ? " · заключён." : " · расторгнут.")); break;
                case 2:
                    S.Embargo[pair] = !S.Embargo[pair];
                    S.Relations[pair] = S.Relations[reverse] = Math.Max(-100, S.Relations[pair] - 15);
                    Notify((S.Embargo[pair] ? "Введено" : "Отменено") + " эмбарго: " + country); break;
                case 3: DeclareWar(me, other); break;
                case 4: case 8: ConcludePeace(me, other, action == 8); break;
                case 5:
                    S.Alliances[pair] = S.Alliances[reverse] = !S.Alliances[pair];
                    if (!S.Alliances[pair]) { S.Relations[pair] = S.Relations[reverse] = Math.Max(-100, S.Relations[pair] - 20); RepatriateStranded(); }
                    Notify("Оборонный союз: " + country + (S.Alliances[pair] ? " · заключён. Союзники вступают при нападении." : " · расторгнут."), 1); break;
                case 6:
                    S.Access[pair] = !S.Access[pair]; RepatriateStranded();
                    Notify("Военный доступ в " + country + (S.Access[pair] ? " получен." : " прекращён.")); break;
                case 7:
                    S.Access[reverse] = !S.Access[reverse]; RepatriateStranded();
                    Notify("Доступ для армии " + country + (S.Access[reverse] ? " предоставлен." : " отозван.")); break;
            }
            Raise(); return null;
        }
        private void DeclareWar(int attacker, int defender)
        {
            if (attacker == defender || AtWar(attacker, defender) || Allied(attacker, defender) || S.TruceUntil[Pair(attacker, defender)] > S.Day) return;
            var defenders = Enumerable.Range(0, S.Nations.Length).Where(c => c == defender || Allied(c, defender)).ToList();
            if (defenders.Any(c => c == attacker || Allied(c, attacker))) return;
            foreach (int country in defenders) {
                int pair = Pair(attacker, country), reverse = Pair(country, attacker);
                if (AtWar(attacker, country)) continue;
                S.Wars[pair] = S.Wars[reverse] = true;
                S.WarStarted[pair] = S.WarStarted[reverse] = S.Day;
                S.TruceUntil[pair] = S.TruceUntil[reverse] = 0;
                S.Agreements[pair] = S.Agreements[reverse] = false;
                S.Access[pair] = S.Access[reverse] = false;
                S.Relations[pair] = S.Relations[reverse] = -100;
                Notify(World.Countries[attacker].Name + " — " + World.Countries[country].Name + (country == defender ? ": объявлена война." : ": вступление по оборонному союзу."), 2);
            }
        }
        private void ConcludePeace(int a, int b, bool restoreBorders)
        {
            int pair = Pair(a, b), reverse = Pair(b, a);
            S.Wars[pair] = S.Wars[reverse] = false;
            S.TruceUntil[pair] = S.TruceUntil[reverse] = S.Day + 180;
            S.Relations[pair] = S.Relations[reverse] = -20;
            if (restoreBorders) for (int r = 0; r < S.Provinces.Length; r++) {
                int home = World.Regions[r].Country, owner = S.Provinces[r].Owner;
                if ((home == a && owner == b) || (home == b && owner == a)) S.Provinces[r].Owner = home;
            }
            foreach (var unit in S.Armies.Where(u => u.Owner == a || u.Owner == b)) { unit.Target = -1; unit.Route.Clear(); }
            foreach (var plan in S.Fronts.Where(p => (p.Owner == a && p.Enemy == b) || (p.Owner == b && p.Enemy == a))) plan.Active = false;
            RepatriateStranded();
            Notify("Мир: " + World.Countries[a].Name + " — " + World.Countries[b].Name + (restoreBorders ? ". Домашние территории возвращены." : ". Сохранена линия контроля.") + " Перемирие на 180 дней.", 1);
        }
        private void RepatriateStranded()
        {
            foreach (var unit in S.Armies) {
                if (CanEnter(unit.Owner, S.Provinces[unit.Region].Owner)) continue;
                int home = Enumerable.Range(0, S.Provinces.Length).Where(r => S.Provinces[r].Owner == unit.Owner)
                    .OrderBy(r => StrategicDistance(unit.Region, r)).DefaultIfEmpty(-1).First();
                unit.Target = -1; unit.Route.Clear();
                if (home >= 0) { unit.Region = home; unit.TrainingDays = Math.Max(14, unit.TrainingDays); unit.Supply = Math.Min(50, unit.Supply); }
                // No home territory: the force is interned and cannot receive orders.
            }
        }
    }
}
