using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private string pendingBinding, bindingError;
        private bool CaptureBinding(KeyEventArgs e)
        {
            if (pendingBinding == null || menuScreen != "controls") return false;
            e.Handled = true; e.SuppressKeyPress = true;
            if (e.KeyCode == Keys.Escape) { pendingBinding = null; bindingError = null; Invalidate(); return true; }
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu) return true;
            bindingError = prefs.SetBinding(pendingBinding, e.KeyData);
            if (bindingError == null) { pendingBinding = null; prefs.Save(); }
            Invalidate(); return true;
        }
        private void DrawBindings(Graphics g)
        {
            Fill(g, Palette.Bg, 0, 0, VW, VH);
            Button(g, "← Настройки", 48, 30, 160, () => { pendingBinding = null; OpenMenu("settings"); });
            Txt(g, "Клавиши управления", 48, 99, 30, Palette.Text, true);
            Txt(g, "Выберите действие и нажмите новое сочетание. Esc отменяет ввод; конфликтующие сочетания не сохраняются.", 50, 151, 14, Palette.Muted);
            float width = (VW - 126) / 2;
            for (int i = 0; i < Preferences.BindingActions.Length; i++) {
                string action = Preferences.BindingActions[i]; float x = 48 + (i / 8) * (width + 30), y = 217 + i % 8 * 64;
                Fill(g, Palette.Card, x, y, width, 51);
                Txt(g, Preferences.BindingNames[i], x + 14, y + 16, 14, Palette.Text, false, width - 210);
                Button(g, pendingBinding == action ? "Нажмите клавиши…" : prefs.KeyLabel(action), x + width - 186, y + 8, 173, () => { pendingBinding = action; bindingError = null; Invalidate(); }, pendingBinding == action, "Переназначить действие", 33);
            }
            Button(g, "Восстановить стандартные", 48, 761, 285, () => { prefs.Bindings = Preferences.DefaultBindings(); prefs.Save(); pendingBinding = null; bindingError = null; Invalidate(); });
            Txt(g, bindingError ?? (pendingBinding == null ? "Изменения сохраняются автоматически." : "Ожидается клавиша или сочетание с Ctrl, Alt, Shift…"), 48, 815, 13, bindingError == null ? Palette.Muted : Palette.Red, false, VW - 96);
        }
    }
}
