// Compile this unchanged against each release and run sequentially, without UI/tests.
// mcs -r:/absolute/release/Sovereign.exe -out:/absolute/release/SimulationProbe.exe scripts/SimulationProbe.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Sovereign;

internal static class SimulationProbe
{
    private static string Summary(string name,List<double> samples)
    {
        var sorted=samples.OrderBy(x=>x).ToArray();
        return string.Format(CultureInfo.InvariantCulture,"{0}: count={1}, mean_ms={2:0.000}, p50_ms={3:0.000}, p95_ms={4:0.000}, max_ms={5:0.000}",name,sorted.Length,sorted.Average(),sorted[sorted.Length/2],sorted[(int)Math.Ceiling(sorted.Length*.95)-1],sorted.Last());
    }
    private static void Main(string[] args)
    {
        using(var world=World.Load()) {
            var engine=new Engine(world){AutoSaveEnabled=false};engine.New(world.ByCode("UKR"),2026,1);
            for(int day=0;day<35;day++)engine.Tick();
            var all=new List<double>();var weekly=new List<double>();var ordinary=new List<double>();
            var report=new List<string>{"Assembly: "+typeof(Engine).Assembly.GetName().Version,"35 warm-up days; 730 measured days; UKR, 2026, difficulty 1; autosave off; no UI"};
            for(int day=0;day<730;day++) {
                var watch=Stopwatch.StartNew();engine.Tick();double ms=watch.Elapsed.TotalMilliseconds;all.Add(ms);
                if(engine.S.Day%7==0)weekly.Add(ms);else ordinary.Add(ms);
            }
            engine.Validate(engine.S);
            report.Add(Summary("All ticks",all));report.Add(Summary("Weekly settlement ticks",weekly));report.Add(Summary("Other ticks",ordinary));
            report.Add("Total measured ms: "+all.Sum().ToString("0.000",CultureInfo.InvariantCulture));
            report.Add("PASS Final campaign validates; countries="+engine.S.Nations.Length+", provinces="+engine.S.Provinces.Length+", formations="+engine.S.Armies.Count);
            File.WriteAllLines(args[0],report);foreach(string line in report)Console.WriteLine(line);
            // Also produces a genuine previous-release save for migration checks.
            if(args.Length>1)engine.Save(args[1]);
        }
    }
}
