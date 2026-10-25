using System;
using System.Diagnostics;

namespace Sovereign
{
    public partial class GameForm
    {
        private readonly Stopwatch uiTime = Stopwatch.StartNew();
        private double nextSimulationStep;
        private int scheduledSpeed;
        private bool tooltipWasVisible, toastWasVisible;
        internal int SimulationPumps;
        internal void PumpSimulation(double milliseconds)
        {
            if (speed <= 0 || menuScreen != "" || E.S == null) { scheduledSpeed = 0; return; }
            double interval = 650.0 / (speed == 1 ? 1 : speed == 2 ? 3 : 7);
            if (scheduledSpeed != speed) { scheduledSpeed = speed; nextSimulationStep = milliseconds + interval; return; }
            if (milliseconds < nextSimulationStep) return;
            // Never catch up in a blocking batch. Input and paint get a turn between days.
            nextSimulationStep = milliseconds + interval; SimulationPumps++; E.Tick();
        }
        private void PumpUiClock()
        {
            bool tooltipVisible = prefs.Tooltips && !string.IsNullOrEmpty(hoveredTip) && (DateTime.Now - hoverSince).TotalMilliseconds >= 550;
            bool toastVisible = DateTime.Now < toastUntil;
            if (tooltipVisible != tooltipWasVisible || toastVisible != toastWasVisible) Invalidate();
            tooltipWasVisible = tooltipVisible; toastWasVisible = toastVisible;
            PumpSimulation(uiTime.Elapsed.TotalMilliseconds);
        }
    }
}
