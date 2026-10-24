using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public class KeyBinding
    {
        public string Action;
        public int Key;
    }
    public partial class Preferences
    {
        public List<KeyBinding> Bindings;
        public static readonly string[] BindingActions = { "Pause", "Save", "Load", "Home", "Help", "Fullscreen", "Page0", "Page1", "Page2", "Page3", "Page4", "Page5", "Page6", "Page7", "Page8" };
        public static readonly string[] BindingNames = { "Пауза / продолжить", "Сохранить кампанию", "Загрузить кампанию", "К своей стране", "Справочник", "Полноэкранное окно", "Сводка", "Карта", "Экономика", "Производство", "Исследования", "Дипломатия", "Армия", "Конструктор", "Политика" };
        public static List<KeyBinding> DefaultBindings()
        {
            Keys[] keys = { Keys.Space, Keys.F5, Keys.F9, Keys.Home, Keys.OemQuestion, Keys.F11, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9 };
            return keys.Select((key, i) => new KeyBinding { Action = BindingActions[i], Key = (int)key }).ToList();
        }
        public void NormalizeBindings()
        {
            var defaults = DefaultBindings();
            if (Bindings == null || Bindings.Count != defaults.Count || Bindings.Any(b => b == null || !BindingActions.Contains(b.Action) || !ValidBinding((Keys)b.Key))
                || Bindings.Select(b => b.Action).Distinct().Count() != defaults.Count || Bindings.Select(b => b.Key).Distinct().Count() != defaults.Count) Bindings = defaults;
        }
        private static bool ValidBinding(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            return (key & ~(Keys.KeyCode | Keys.Control | Keys.Shift | Keys.Alt)) == 0 && Enum.IsDefined(typeof(Keys), code) && code >= Keys.Back
                && code != Keys.Escape && code != Keys.ControlKey && code != Keys.ShiftKey && code != Keys.Menu && code != Keys.LWin && code != Keys.RWin;
        }
        public Keys Binding(string action) { if (Bindings == null) Bindings = DefaultBindings(); return (Keys)Bindings.First(b => b.Action == action).Key; }
        public string SetBinding(string action, Keys key)
        {
            if (Bindings == null) Bindings = DefaultBindings();
            var target = Bindings.FirstOrDefault(b => b.Action == action);
            if (target == null || !ValidBinding(key)) return "Выберите обычную клавишу или сочетание. Esc зарезервирована для отмены.";
            var conflict = Bindings.FirstOrDefault(b => b.Action != action && b.Key == (int)key);
            if (conflict != null) return "Сочетание уже назначено: " + BindingNames[Array.IndexOf(BindingActions, conflict.Action)] + ".";
            target.Key = (int)key; return null;
        }
        public string KeyLabel(string action)
        {
            Keys key = Binding(action), code = key & Keys.KeyCode;
            string name = code == Keys.Space ? "Пробел" : code == Keys.OemQuestion ? "?" : code == Keys.Home ? "Home"
                : code >= Keys.D0 && code <= Keys.D9 ? ((int)code - (int)Keys.D0).ToString() : code.ToString();
            return ((key & Keys.Control) != 0 ? "Ctrl+" : "") + ((key & Keys.Alt) != 0 ? "Alt+" : "") + ((key & Keys.Shift) != 0 ? "Shift+" : "") + name;
        }
    }
}
