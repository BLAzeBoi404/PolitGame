using System.Collections.Generic;

namespace Sovereign
{
    public partial class Engine
    {
        // One graph traversal per simulated day. Each nation is supplied from its
        // largest still-controlled home region; foreign and neutral land blocks supply.
        public bool[] SupplyNetwork()
        {
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
            return connected;
        }
    }
}
