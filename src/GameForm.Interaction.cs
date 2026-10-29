using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private string keyboardFocusKey;
        private RectangleF lastTooltipBounds;
        private string FocusKey(Hit hit) => hit.Label + "@" + hit.Rect.X.ToString("0") + "," + hit.Rect.Y.ToString("0");
        private void InvalidateHud() { gpuHudDirty = true; frameRequested = true; ScheduleFrame(); }
        private Hit FocusedHit() => keyboardFocusKey == null ? null : hits.LastOrDefault(h => h.FocusTarget && h.Action != null && FocusKey(h) == keyboardFocusKey);
        private bool HandleDeskKey(Keys key)
        {
            if (menuScreen != "" || showHelp) return false;
            if (key == Keys.Escape && mapLayersExpanded) { mapLayersExpanded = false; keyboardFocusKey = null; InvalidateHud(); return true; }
            if (prefs.Bindings != null && prefs.Bindings.Any(b => b.Key == (int)key)) return false;
            bool tab = key == Keys.Tab || key == (Keys.Shift | Keys.Tab);
            bool enter = key == Keys.Enter && keyboardFocusKey != null;
            if (!tab && !enter) return false;
            if (simulationBusy) { pendingSimulationActions.Enqueue(() => HandleDeskKey(key)); return true; }
            if (tab) {
                var candidates = hits.Where(h => h.FocusTarget && h.Action != null && h.Rect.Width > 0 && h.Rect.Height > 0
                    && h.Rect.Left >= 0 && h.Rect.Right <= VW + 1 && h.Rect.Top >= 0 && h.Rect.Bottom <= VH + 1
                    && (!mapLayersExpanded || MapLayers.Contains(h.Label) || h.Label == "map:layers"))
                    .OrderBy(h => h.Rect.Top).ThenBy(h => h.Rect.Left).ToArray();
                if (candidates.Length == 0) return true;
                int index = Array.FindIndex(candidates, h => FocusKey(h) == keyboardFocusKey);
                index = index < 0 ? (key == Keys.Tab ? 0 : candidates.Length - 1) : (index + (key == Keys.Tab ? 1 : candidates.Length - 1)) % candidates.Length;
                var selectedHit = candidates[index]; keyboardFocusKey = FocusKey(selectedHit);
                hoveredTip = selectedHit.Tip ?? ""; hoverSince = DateTime.Now.AddSeconds(-1); InvalidateHud(); return true;
            }
            var focus = FocusedHit(); if (focus != null) focus.Action(); else keyboardFocusKey = null;
            InvalidateHud(); return true;
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (menuScreen == "" && !showHelp && (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab) || keyData == Keys.Enter)
                && prefs.Bindings != null && prefs.Bindings.Any(b => b.Key == (int)keyData)) {
                KeysDown(this, new KeyEventArgs(keyData)); return true;
            }
            if (HandleDeskKey(keyData)) return true;
            return base.ProcessCmdKey(ref message, keyData);
        }
        private void DrawKeyboardFocus(Graphics g)
        {
            var focus = FocusedHit(); if (focus == null) return;
            var rect = focus.Rect; rect.Inflate(2, 2);
            RoundBorder(g, Palette.Teal, rect, 5, 2);
        }
        private bool DismissLayerPicker(Point point)
        {
            if (!mapLayersExpanded || MapPickerRect.Contains(point) || MapLayerPopupRect.Contains(point)) return false;
            mapLayersExpanded = false; InvalidateHud();
            bool button = hits.Any(h => h.FocusTarget && h.Rect.Contains(point));
            if (!button) { panning = false; moved = false; CapturePointer(false); }
            return !button;
        }
    }
}
