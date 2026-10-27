using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign
{
    public partial class Engine
    {
        // Contemporary public strength estimates seed an abstract domestic force. These
        // are game formations, not named real regiments or the locations of deployed units.
        private void InitializeStartingForces()
        {
            var supplied = SupplyNetwork();
            for (int owner = 0; owner < S.Nations.Length; owner++) {
                var country = World.Countries[owner]; var nation = S.Nations[owner]; int remaining = country.StartingGroundPersonnel;
                if (remaining <= 0) continue;
                double largest = country.Regions.Max(r => S.Provinces[r].Population);
                var regions = country.Regions.Where(r => supplied[r] && (S.Provinces[r].Population >= Math.Min(.05, largest) && S.Provinces[r].Population >= largest * .025 || r == country.CapitalRegion)).ToList();
                if (regions.Count == 0) regions.Add(country.Regions.OrderByDescending(r => S.Provinces[r].Population).First());
                var weights = new double[regions.Count]; var deployed = new int[regions.Count];
                for (int i = 0; i < regions.Count; i++) {
                    int region = regions[i]; var p = S.Provinces[region];
                    bool frontier = World.Regions[region].Neighbors.Any(r => S.Provinces[r].Owner != owner);
                    weights[i] = Math.Pow(Math.Max(.001,p.Population),.75) * (.65 + p.Infrastructure / 100) * (frontier ? 1.15 : 1) * (region == country.CapitalRegion ? 1.25 : 1);
                }
                var designs = new Dictionary<EquipmentKind, EquipmentDesign>();
                foreach (var kind in new[] {EquipmentKind.IFV, EquipmentKind.Tank, EquipmentKind.Artillery}) {
                    var blueprint = nation.Design.Copy(); blueprint.Kind = kind;
                    blueprint.Name = kind == EquipmentKind.IFV ? "Мотопехота · базовый парк" : kind == EquipmentKind.Tank ? "Бронетехника · базовый парк" : "Артиллерия · базовый парк";
                    var d = new EquipmentDesign { Id = nation.NextDesign++, Blueprint = blueprint }; nation.Designs.Add(d); designs.Add(kind,d);
                }
                var templates = new Dictionary<int, FormationTemplate>();
                foreach (var t in nation.Templates) templates[t.Battalions] = t;
                for (int battalions = 1; battalions <= 6; battalions++) if (!templates.ContainsKey(battalions)) {
                    var t = new FormationTemplate { Id = nation.NextTemplate++, Name = "Штат · " + battalions + " бат.", Battalions = battalions };
                    templates.Add(battalions,t); nation.Templates.Add(t);
                }
                foreach (var template in nation.Templates) if (template.Battalions >= 4)
                    template.Deployment = new BattleDeployment { Front = template.Battalions - 2, Left = 1, Right = 1 };
                int serial = 0;
                while (remaining > 0) {
                    int slot = 0;
                    for (int i = 1; i < regions.Count; i++) if (weights[i] / (1 + deployed[i]) > weights[slot] / (1 + deployed[slot])) slot = i;
                    int mix = (serial + owner) % 10;
                    var kind = mix == 7 || mix == 8 ? EquipmentKind.Tank : mix == 9 ? EquipmentKind.Artillery : EquipmentKind.IFV;
                    int battalions = country.StartingGroundPersonnel < 15000 ? 1 + serial % 2 : country.StartingGroundPersonnel < 75000 ? 2 + serial % 3 : 4 + serial % 3;
                    int establishment = ManpowerRules.GroundSize(kind, battalions);
                    while (battalions > 1 && remaining < establishment * .6) { battalions--; establishment = ManpowerRules.GroundSize(kind,battalions); }
                    int people = Math.Min(remaining, establishment);
                    var layout = templates[battalions].Deployment == null ? null : templates[battalions].Deployment.Copy();
                    var d = designs[kind];
                    S.Armies.Add(new Army { Id = S.NextArmy++, Owner = owner, Region = regions[slot], DesignId = d.Id, Design = d.Blueprint.Copy(),
                        Battalions = battalions, TemplateId = templates[battalions].Id, Deployment = layout, Personnel = people,
                        Experience = 15 + (owner * 7 + serial * 11) % 36, Organization = 80 + (owner + serial * 3) % 21 });
                    remaining -= people; deployed[slot] += people; serial++;
                }
                // Select a law able to sustain the seeded ground force and a small reserve.
                long active = country.StartingGroundPersonnel;
                nation.Conscription = ConscriptionLaw.Volunteers;
                while ((int)nation.Conscription < 3 && Capacity(S, owner) < active * 1.1) nation.Conscription++;
                nation.Recruits = (int)Math.Min(Math.Max(0, Capacity(S, owner) - active), Math.Max(250,active * .08));
                nation.Mobilized = active + nation.Recruits;
            }
            S.Version = 12;
        }
    }
}
