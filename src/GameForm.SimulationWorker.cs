using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private volatile bool simulationBusy;
        private Task simulationTask;
        private bool processingSimulationActions;
        private readonly Queue<Action> pendingSimulationActions=new Queue<Action>();
        internal int AsyncSimulationSteps;
        internal Action BeforeSimulationStep;
        // Only this worker writes the campaign while it runs. Rendering uses the last
        // immutable atlas colors, HUD and world command list. UI commands wait for the
        // completed day, but camera/input/presentation never acquire a simulation lock.
        private void StartSimulationDay()
        {
            if(simulationBusy)return;
            if(!GpuActive){E.Tick();return;}
            if(gpuHudDirty||gpuWorldDirty||gpuHud==null)PresentGpu();
            if(!GpuActive){E.Tick();return;}
            simulationBusy=true;
            simulationTask=Task.Run(()=>{
                Exception error=null;try {if(BeforeSimulationStep!=null)BeforeSimulationStep();E.Tick();}catch(Exception e){error=e;}
                try {BeginInvoke((Action)(()=>CompleteSimulationDay(error)));}
                catch(InvalidOperationException){ }
            });
        }
        private void CompleteSimulationDay(Exception error)
        {
            simulationBusy=false;simulationTask=null;AsyncSimulationSteps++; hoveredTip = "";
            if(IsDisposed)return;
            if(error!=null){speed=0;pendingSimulationActions.Clear();Message("Расчёт остановлен: "+error.Message);}
            else {
                processingSimulationActions=true;
                try {ObservePolicyIncident();while(pendingSimulationActions.Count>0&&!IsDisposed)pendingSimulationActions.Dequeue()();}
                finally {processingSimulationActions=false;}
            }
            Invalidate();
        }
        private void QueueMapClick(MouseEventArgs e)
        {
            var point=Logical(e.Location);Hit chosen=null;
            if(e.Button==MouseButtons.Left)foreach(var hit in hits)if(hit.Rect.Contains(point))chosen=hit;
            if(chosen!=null){var action=chosen.Action;pendingSimulationActions.Enqueue(action);return;}
            if(menuScreen!=""||showHelp||!MapInputAllowed(point))return;
            int region=FindRegion(Geo(point));if(region<0)return;
            if(e.Button==MouseButtons.Right)pendingSimulationActions.Enqueue(()=>Message(E.Order(army,region)));
            else pendingSimulationActions.Enqueue(()=>{selected=region;foreign=E.S.Provinces[region].Owner;Invalidate();});
        }
        private void FinishSimulation()
        {
            // Closing is normally deferred until completion. Dispose also handles tests
            // and exceptional teardown before World or controls can be released.
            var task=simulationTask;if(task!=null)task.GetAwaiter().GetResult();simulationBusy=false;simulationTask=null;
        }
    }
}
