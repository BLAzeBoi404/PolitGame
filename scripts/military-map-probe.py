#!/usr/bin/env python3
"""Emit the same frame pacing harness for both releases, selecting the army layer.

python3 scripts/military-map-probe.py artifacts/military-map/Probe.cs
mcs -langversion:7.2 -r:System.Windows.Forms -r:System.Drawing -out:artifacts/military-map/Probe.exe artifacts/military-map/Probe.cs
# Run serially, without concurrent tests. OpenTK must be beside each target EXE.
bash scripts/with-display.sh mono artifacts/military-map/Probe.exe /absolute/Sovereign.exe /absolute/report.txt --165 --live
"""
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
source = (root / 'src/FramePacingProbe.cs').read_text()
needle = 'Set(form,"scale",45f);'
assert source.count(needle) == 1
source = source.replace(needle, needle + 'Set(form,"layer","Армии");')
source = source.replace('medium zoom 45; ', 'military map; medium zoom 45; ')
source += '''
namespace Sovereign {
    internal static class MilitaryMapProbe {
        [STAThread]
        private static int Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var assembly = Assembly.LoadFrom(args[0]);
            using (var world = (IDisposable)assembly.GetType("Sovereign.World").GetMethod("Load").Invoke(null, null))
                FramePacingProbe.Run(assembly.GetType("Sovereign.GameForm"), world, args[1], true, args.Contains("--live"), args.Contains("--165") ? 165 : 60);
            return 0;
        }
    }
}
'''
target = Path(sys.argv[1]); target.parent.mkdir(parents=True, exist_ok=True); target.write_text(source)
