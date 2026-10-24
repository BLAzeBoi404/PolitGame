using System;
using System.Collections.Generic;

namespace Sovereign
{
    public partial class Engine
    {
        private readonly Dictionary<int, bool[]> transitSupply = new Dictionary<int, bool[]>();
        private Campaign supplyCampaign;
        private int supplyFingerprint;
        private bool[] domesticSupply;
        private bool IsSupplied(int owner, int region, bool[] domestic)
        {
            if (S.Provinces[region].Owner == owner && domestic[region]) return true;
            bool[] connected;
            if (!transitSupply.TryGetValue(owner, out connected)) {
                connected = new bool[S.Provinces.Length];
                var queue = new Queue<int>();
                for (int r = 0; r < connected.Length; r++)
                    if (S.Provinces[r].Owner == owner && domestic[r]) { connected[r] = true; queue.Enqueue(r); }
                while (queue.Count > 0) {
                    int r = queue.Dequeue();
                    foreach (int next in World.Regions[r].Neighbors)
                        if (!connected[next] && HasAccess(owner, S.Provinces[next].Owner)) { connected[next] = true; queue.Enqueue(next); }
                }
                transitSupply.Add(owner, connected);
            }
            return connected[region];
        }
        // Reuse the domestic graph until ownership or treaties change. The baseline
        // starts at each largest controlled home region; transit graphs extend it on demand.
        public bool[] SupplyNetwork()
        {
            int fingerprint = 17;
            unchecked {
                foreach (var p in S.Provinces) fingerprint = fingerprint * 31 + p.Owner;
                for (int i = 0; i < S.Access.Length; i++) fingerprint = fingerprint * 31 + (S.Access[i] ? 1 : 0) + (S.Alliances[i] ? 2 : 0);
            }
            if (ReferenceEquals(supplyCampaign, S) && supplyFingerprint == fingerprint && domesticSupply != null) return domesticSupply;
            supplyCampaign = S; supplyFingerprint = fingerprint; transitSupply.Clear();
            int[] hubs = new int[S.Nations.Length];
            for (int i = 0; i < hubs.Length; i++) hubs[i] = -1;
            for (int i = 0; i < S.Provinces.Length; i++) {
                int owner = S.Provinces[i].Owner;
                if (World.Regions[i].Country == owner && (hubs[owner] < 0 || S.Provinces[i].Population > S.Provinces[hubs[owner]].Population)) hubs[owner] = i;
            }
            bool[] connected = new bool[S.Provinces.Length];
            var queue = new Queue<int>();
            foreach (int hub in hubs) if (hub >= 0) { connected[hub] = true; queue.Enqueue(hub); }
            while (queue.Count > 0) {
                int region = queue.Dequeue();
                foreach (int next in World.Regions[region].Neighbors)
                    if (!connected[next] && S.Provinces[next].Owner == S.Provinces[region].Owner) {
                        connected[next] = true; queue.Enqueue(next);
                    }
            }
            domesticSupply = connected;
            return connected;
        }
    }
}
