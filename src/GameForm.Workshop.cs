using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool equipmentProduction;
        private int catalogOffset, lineOffset;
        private int orderFactories = 1, orderQuantity = 3;

        private bool Confirm(string text)
        {
            int previousSpeed = speed;
            speed = 0;
            try { return MessageBox.Show(this, text, "Подтверждение", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK; }
            finally { speed = previousSpeed; }
        }
        private void RenameDraft()
        {
            using (var dialog = new Form { Text = "Название проекта", Size = new Size(440, 170), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Palette.Panel, ForeColor = Palette.Text }) {
                var input = new TextBox { Text = draft.Name, MaxLength = 40, Left = 20, Top = 20, Width = 380 };
                var ok = new System.Windows.Forms.Button { Text = "Применить", Left = 190, Top = 66, Width = 105, DialogResult = DialogResult.OK };
                var cancel = new System.Windows.Forms.Button { Text = "Отмена", Left = 305, Top = 66, Width = 95, DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] { input, ok, cancel }); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK) draft.Name = input.Text.Trim();
            }
            Invalidate();
        }
        private void DrawDesigner(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, left = 330, right = x + left + 30, rw = w - left - 30;
            PageTitle(g, "Бюро военной техники", "Исследования → испытания → серия → склад → подготовка части. Каждый модуль имеет цену.");
            float tab = (w - 21) / 4;
            for (int k = 0; k < 8; k++) {
                var kind = (EquipmentKind)k;
                Button(g, EquipmentRules.Names[k], x + k % 4 * (tab + 7), 213 + k / 4 * 35, tab, () => {
                    draft = EquipmentRules.Balanced(kind, E.Player, EquipmentRules.Names[(int)kind] + " " + E.Player.NextDesign);
                    Invalidate();
                }, draft.Kind == kind, EquipmentRules.Roles[k], 29);
            }
            Section(g, "МОДУЛИ И КОМПРОМИССЫ", x, 302, left);
            string[] notes = { "Защита ↑ · масса и расход ↑", "Пробитие ↑ · цена и масса ↑", "Скорость ↑ · расход топлива ↑", "Точность ↑ · сложность ↑", "Защита ↑ · масса и стоимость ↑", "Скорость и надёжность ↑ · цена ↑", "Дальность ↑ · масса ↑", "Надёжность ↑ · затраты ↑" };
            int[] modules = EquipmentRules.Modules(draft);
            for (int i = 0; i < 8; i++) {
                int slot = i; int cap = EquipmentRules.Cap(draft, i, E.Player); float y = 339 + i * 47;
                Txt(g, EquipmentRules.ModuleNames[i], x, y, 14, Palette.Text, true, left - 132);
                Txt(g, notes[i], x, y + 21, 10, Palette.Muted, false, left - 126);
                AvailableButton(g, "−", x + left - 124, y, 32, () => { EquipmentRules.SetModule(draft, slot, modules[slot] - 1); Invalidate(); }, modules[i] > 1, "Уменьшить уровень", false, 29);
                Txt(g, modules[i] + "/" + cap, x + left - 82, y + 5, 14, Palette.Gold, true);
                AvailableButton(g, "+", x + left - 32, y, 32, () => { EquipmentRules.SetModule(draft, slot, modules[slot] + 1); Invalidate(); }, modules[i] < cap, "Лимит определяется военными технологиями, промышленностью или логистикой.", false, 29);
            }
            Button(g, "Сбалансированный проект", x, 735, left, () => { draft = EquipmentRules.Balanced(draft.Kind, E.Player, draft.Name); Invalidate(); }, false, "Подбор доступных модулей до уровня 4; максимальный уровень не всегда выгоден.");
            TechnicalFrame(g, new RectangleF(right, 300, rw, 180));
            Book(g, draft.Name, right + 18, 312, 25, Color.FromArgb(45, 63, 56), false, rw - 150);
            Button(g, "Название…", right + rw - 125, 312, 108, RenameDraft, false, "Назовите проект; имя должно быть уникальным.", 27);
            if (draft.Kind == EquipmentKind.Tank) DrawTank(g, new RectangleF(right + 14, 346, rw * 0.46f, 125));
            else DrawEquipmentSymbol(g, new RectangleF(right + 24, 357, rw * 0.41f, 100), draft);
            Txt(g, EquipmentRules.Names[(int)draft.Kind].ToUpperInvariant(), right + rw * 0.48f, 359, 14, Color.FromArgb(45, 63, 56), true);
            Para(g, EquipmentRules.Roles[(int)draft.Kind], right + rw * 0.48f, 386, rw * 0.48f - 12, 56, 13, Color.FromArgb(45, 63, 56));
            Txt(g, "Схема класса · характеристики ниже", right + 18, 458, 10, Color.FromArgb(60, 80, 65));
            Blueprint compare = E.Player.Design;
            Section(g, "ПАРАМЕТРЫ  /  СРАВНЕНИЕ С АКТИВНЫМ ПРОЕКТОМ", right, 500, rw);
            string[] labels = { "Атака", "Защита", "Броня / пробитие", "Скорость, км/ч", "Масса, т", "Дальность, км", "Надёжность, %", "Топливо / день", "Цена комплекта, млрд", "Содержание / день" };
            string[] values = { draft.Attack.ToString("0.0"), draft.Defense.ToString("0.0"), draft.Protection.ToString("0") + " / " + draft.Penetration.ToString("0"), draft.SpeedKph.ToString("0"), draft.Weight.ToString("0.0"), draft.Range.ToString("0"), draft.Reliability.ToString("0.0"), draft.Fuel.ToString("0.000"), draft.Cost.ToString("0.000"), draft.Maintenance.ToString("0.0000") };
            double[] change = { draft.Attack - compare.Attack, draft.Defense - compare.Defense, draft.Protection - compare.Protection, draft.SpeedKph - compare.SpeedKph, draft.Weight - compare.Weight, draft.Range - compare.Range, draft.Reliability - compare.Reliability, draft.Fuel - compare.Fuel, draft.Cost - compare.Cost, draft.Maintenance - compare.Maintenance };
            for (int i = 0; i < labels.Length; i++) {
                float y = 534 + i * 20;
                Txt(g, labels[i], right, y, 12, Palette.Muted, false, rw * 0.45f);
                Txt(g, values[i], right + rw * 0.49f, y, 13, Palette.Text, true);
                Txt(g, change[i].ToString("+0.###;-0.###;0"), right + rw * 0.8f, y, 12, Palette.Gold);
            }
            string error = EquipmentRules.Validate(draft, E.Player);
            if (error == null && E.Player.Designs.Any(d => string.Equals(d.Blueprint.Name, draft.Name.Trim(), StringComparison.OrdinalIgnoreCase))) error = "Укажите новое уникальное название.";
            if (error == null && E.Player.Treasury < draft.Cost * 2) error = "Недостаточно средств на испытания.";
            AvailableButton(g, "Испытать и сохранить · " + Money(draft.Cost * 2), right, 746, rw, () => Message(E.Adopt(draft)), error == null, error ?? "Испытания оплачиваются один раз. Производство заказывается отдельно.", true, 36);
            Txt(g, error ?? ("Сравнение: " + compare.Name + " · серия: " + draft.WorkDays.ToString("0") + " заводо-дней при 100% эффективности"), right, 788, 11, error == null ? Palette.Muted : Palette.Red, false, rw);
        }
        private void DrawEquipmentSymbol(Graphics g, RectangleF box, Blueprint design)
        {
            // Original vector schematics; no external game artwork.
            var state = g.Save(); g.TranslateTransform(box.X, box.Y); g.ScaleTransform(box.Width / 220, box.Height / 100);
            using (var pen = new Pen(Color.FromArgb(65, 85, 71), 2)) {
                if (design.Air) {
                    g.DrawPolygon(pen, new[] { new PointF(110, 4), new PointF(121, 42), new PointF(207, 67), new PointF(204, 78), new PointF(121, 65), new PointF(119, 84), new PointF(150, 95), new PointF(70, 95), new PointF(101, 84), new PointF(99, 65), new PointF(16, 78), new PointF(13, 67), new PointF(99, 42) });
                    g.DrawLine(pen, 110, 12, 110, 91);
                } else {
                    g.DrawPolygon(pen, new[] { new PointF(20, 65), new PointF(32, 36), new PointF(151, 36), new PointF(195, 62), new PointF(195, 77), new PointF(20, 77) });
                    for (int i = 0; i < 5; i++) g.DrawEllipse(pen, 29 + i * 32, 67, 23, 23);
                    if (design.Kind != EquipmentKind.Truck) { g.DrawRectangle(pen, 75, 20, 65, 16); g.DrawLine(pen, 140, 26, 213, 17); }
                    else g.DrawRectangle(pen, 29, 20, 112, 16);
                }
            }
            g.Restore(state);
        }
        private void DrawProduction(Graphics g)
        {
            if (equipmentProduction) DrawEquipmentProduction(g); else DrawConstruction(g);
            float x = sidebar + 34;
            Button(g, "Предприятия", x, 169, 138, () => { equipmentProduction = false; Invalidate(); }, !equipmentProduction, "Строительство и промышленная база", 28);
            Button(g, "Серии и склад", x + 148, 169, 162, () => { equipmentProduction = true; Invalidate(); }, equipmentProduction, "Заказы на конкретные модели техники", 28);
        }
        private void DrawEquipmentProduction(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, left = 340, right = x + left + 28, rw = w - left - 28;
            PageTitle(g, "Военные заказы", "Комплект оснащает одну часть или одну группу поддержки. Выпуск рассчитывается каждый день.");
            var designs = E.Player.Designs;
            var active = E.FindDesign(E.S.Player, E.Player.ActiveDesign);
            Section(g, "КАТАЛОГ ПРОЕКТОВ / СКЛАД", x, 218, left);
            catalogOffset = Math.Min(catalogOffset, Math.Max(0, designs.Count - 6));
            for (int i = catalogOffset; i < Math.Min(designs.Count, catalogOffset + 6); i++) {
                var record = designs[i]; float y = 256 + (i - catalogOffset) * 65;
                Button(g, record.Blueprint.Name, x, y, left, () => {
                    if (record.Retired) { Message("Проект в архиве. Восстановите его кнопкой ниже."); return; }
                    Message(E.SelectDesign(record.Id));
                }, record.Id == E.Player.ActiveDesign, EquipmentRules.Roles[(int)record.Blueprint.Kind], 32);
                Txt(g, EquipmentRules.Names[(int)record.Blueprint.Kind] + " · склад " + record.Stock + (record.Retired ? " · архив" : " компл."), x + 5, y + 37, 12, Palette.Muted, false, left - 72);
                Button(g, record.Retired ? "↶" : "Архив", x + left - 64, y + 35, 64, () => Message(E.RetireDesign(record.Id)), false, "Снять с производства / восстановить проект; склад и части сохраняются.", 24);
            }
            Button(g, "←", x, 657, 38, () => { catalogOffset = Math.Max(0, catalogOffset - 6); Invalidate(); });
            Button(g, "→", x + 48, 657, 38, () => { catalogOffset = Math.Min(Math.Max(0, designs.Count - 6), catalogOffset + 6); Invalidate(); });
            Txt(g, designs.Count + " проектов", x + 103, 666, 12, Palette.Muted);
            Button(g, "Изменить выбранный проект →", x, 708, left, () => {
                draft = active.Blueprint.Copy(); draft.Name = draft.Name.Substring(0, Math.Min(30, draft.Name.Length)) + " М" + E.Player.NextDesign; Navigate("Конструктор");
            }, false, "Создать независимую модификацию. Производимые и установленные модели не меняются.");
            Button(g, "Оснастить части →", x, 754, left, () => Navigate("Армия"));
            Section(g, "НОВАЯ СЕРИЯ", right, 218, rw);
            Txt(g, active.Blueprint.Name, right, 255, 21, Palette.Gold, true, rw - 220);
            int capacity = E.FactoryCapacity(E.S.Player), assigned = E.AssignedFactories(E.S.Player);
            Txt(g, "Заводы: " + assigned + " / " + capacity, right + rw - 200, 260, 14, Palette.Text);
            Adjust(g, "Заводы", orderFactories, right, 295, rw * 0.48f, d => orderFactories = Math.Max(1, Math.Min(500, orderFactories + d)));
            Adjust(g, "Комплекты", orderQuantity, right + rw * 0.52f, 295, rw * 0.48f, d => orderQuantity = Math.Max(1, Math.Min(1000, orderQuantity + d)));
            Blueprint b = active.Blueprint;
            Txt(g, "На комплект: " + Money(b.Cost) + " · сталь " + b.Steel.ToString("0.00") + " · машины " + b.Machines.ToString("0.00") + " · оружие " + b.Arms.ToString("0.00"), right, 342, 12, Palette.Muted, false, rw);
            bool canOrder = !active.Retired && assigned + orderFactories <= capacity;
            AvailableButton(g, "Заказать " + orderQuantity + " компл. · всего " + Money(b.Cost * orderQuantity), right, 370, rw, () => Message(E.StartProduction(active.Id, orderFactories, orderQuantity)), canOrder,
                canOrder ? "Деньги и материалы расходуются по мере выпуска. Начальная эффективность 25%; растёт при работе." : "Нужен действующий проект и свободные оборонные заводы.", true);
            Section(g, "ПРОИЗВОДСТВЕННЫЕ ЛИНИИ", right, 426, rw);
            var lines = E.Player.Lines; int visible = 3;
            lineOffset = Math.Min(lineOffset, Math.Max(0, lines.Count - visible));
            if (lines.Count == 0) Para(g, "Закажите первую серию. Если не хватает мощностей, постройте оборонный завод во вкладке предприятий. Нехватка ресурсов замедляет выпуск — проверьте экономику.", right, 476, rw, 100, 15);
            for (int i = lineOffset; i < Math.Min(lines.Count, lineOffset + visible); i++) {
                var line = lines[i]; var design = E.FindDesign(E.S.Player, line.DesignId); float y = 465 + (i - lineOffset) * 97;
                Txt(g, design.Blueprint.Name + " · " + line.Completed + "/" + line.Quantity, right, y, 14, Palette.Text, true, rw - 245);
                Txt(g, E.ProductionStatus(E.S.Player, line), right, y + 25, 11, Palette.Muted, false, rw - 245);
                Progress(g, right, y + 51, rw - 246, (line.Completed + line.Progress) / line.Quantity, Palette.Teal);
                double days = (1 - line.Progress) * design.Blueprint.WorkDays / (line.Factories * line.Efficiency * (1 + E.Player.Tech[2] * 0.04));
                Txt(g, line.Completed >= line.Quantity ? "Комплекты на складе" : "Следующий ≈ " + Math.Ceiling(days) + " дн. при наличии ресурсов", right, y + 65, 10, Palette.Muted, false, rw - 245);
                AvailableButton(g, line.Paused ? "Пуск" : "Пауза", right + rw - 229, y, 74, () => Message(E.ChangeLine(line.Id, line.Factories, !line.Paused)), line.Completed < line.Quantity, "Пауза освобождает заводы и сохраняет прогресс.", false, 29);
                Button(g, "Убрать", right + rw - 145, y, 74, () => {
                    if (line.Completed >= line.Quantity || Confirm("Отменить незавершённый заказ? Готовые комплекты сохранятся; вложения в незавершённый комплект не возвращаются.")) Message(E.CancelLine(line.Id));
                }, false, "Удалить завершённую линию или отменить заказ.", 29);
                Txt(g, "Заводы " + line.Factories, right + rw - 229, y + 41, 11, Palette.Muted);
                AvailableButton(g, "−", right + rw - 137, y + 37, 31, () => Message(E.ChangeLine(line.Id, line.Factories - 1, line.Paused)), line.Factories > 1 && line.Completed < line.Quantity, "Освободить завод", false, 27);
                AvailableButton(g, "+", right + rw - 97, y + 37, 31, () => Message(E.ChangeLine(line.Id, line.Factories + 1, line.Paused)), line.Completed < line.Quantity && (line.Paused || assigned < capacity), "Добавить завод", false, 27);
            }
            Button(g, "←", right, 774, 38, () => { lineOffset = Math.Max(0, lineOffset - 3); Invalidate(); }, false, "Предыдущие линии", 28);
            Button(g, "→", right + 48, 774, 38, () => { lineOffset = Math.Min(Math.Max(0, lines.Count - 3), lineOffset + 3); Invalidate(); }, false, "Следующие линии", 28);
            Txt(g, lines.Count + " линий · очередь имеет приоритет сверху вниз", right + 100, 780, 11, Palette.Muted);
        }
        private void DrawArmy(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68;
            var units = E.S.Armies.Where(a => a.Owner == E.S.Player).ToList();
            var active = E.FindDesign(E.S.Player, E.Player.ActiveDesign);
            PageTitle(g, "Вооружённые силы", "Серийная техника, подготовка и поддержка. Приказы движения: правая кнопка на карте.");
            AvailableButton(g, "Набрать часть · 0,30 млрд", x, 217, 260, () => Message(E.Recruit(OwnRegion())), active.Stock > 0 && active.Blueprint.Formation && !active.Retired,
                "Нужен готовый комплект танков, БМП или артиллерии. Подготовка 30 дней.");
            Button(g, "Выбрать модель / произвести →", x + 275, 217, 285, () => { equipmentProduction = true; Navigate("Производство"); });
            Txt(g, "Оснащение: " + active.Blueprint.Name + " · на складе " + active.Stock, x + 580, 226, 13, Palette.Gold, false, w - 580);
            int visible = Math.Max(1, (int)((VH - 410) / 115)); int offset = Math.Min(scroll, Math.Max(0, units.Count - visible));
            for (int i = offset; i < Math.Min(units.Count, offset + visible); i++) {
                var a = units[i]; float y = 278 + (i - offset) * 115;
                Card(g, x, y, w, 104);
                Txt(g, "СОЕДИНЕНИЕ №" + a.Id + " · " + a.Design.Name, x + 15, y + 11, 14, Palette.Gold, true, w - 370);
                Txt(g, W.Regions[a.Region].Name + " · готовность " + a.Strength.ToString("0") + "% · снабжение " + a.Supply.ToString("0") + "%", x + 15, y + 36, 12, Palette.Text, false, w - 370);
                string state = a.TrainingDays > 0 ? "Подготовка: " + a.TrainingDays + " дн." : a.Target >= 0 ? "В пути: " + Math.Ceiling(a.Days) + " дн." : "Готов к приказам";
                Txt(g, state, x + 15, y + 59, 12, Palette.Teal);
                Txt(g, "Поддержка: " + (a.Attachments.Count == 0 ? "не назначена (до 3 классов)" : string.Join(" · ", a.Attachments.Select(t => EquipmentRules.Names[(int)t.Blueprint.Kind] + " " + t.Condition.ToString("0") + "%"))), x + 15, y + 81, 10, Palette.Muted, false, w - 370);
                Button(g, "На карте →", x + w - 336, y + 12, 145, () => { army = a.Id; selected = a.Region; cx = (float)W.Regions[a.Region].X; cy = (float)W.Regions[a.Region].Y; scale = 30; Navigate("Карта"); }, false, "Выбрать соединение и перейти к карте", 30);
                bool ready = a.TrainingDays == 0 && a.Target < 0 && E.S.Provinces[a.Region].Owner == E.S.Player && active.Stock > 0 && !active.Retired;
                AvailableButton(g, "Перевооружить", x + w - 179, y + 12, 163, () => { if (Confirm("Заменить основную технику на «" + active.Blueprint.Name + "»? Подготовка 14 дней. Старая техника вернётся на склад только при готовности части от 90%.")) Message(E.Refit(a.Id, active.Id)); }, ready && active.Blueprint.Formation && a.DesignId != active.Id, "Новый комплект + 15% его стоимости. Часть должна стоять в своей области.", false, 30);
                AvailableButton(g, "+ Поддержка", x + w - 179, y + 56, 163, () => Message(E.Refit(a.Id, active.Id, true)), ready && active.Blueprint.Kind != EquipmentKind.Tank && active.Blueprint.Kind != EquipmentKind.IFV && a.Attachments.Count < 3 && !a.Attachments.Any(t => t.Blueprint.Kind == active.Blueprint.Kind), "Выберите грузовик, артиллерию или авиацию в каталоге. 14 дней подготовки.", false, 30);
            }
            Txt(g, "Авиация действует как поддержка частей; отдельных воздушных миссий и флота пока нет. Колесо — прокрутка.", x, VH - 126, 12, Palette.Muted);
        }
    }
}
