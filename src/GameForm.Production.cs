using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void DrawEquipmentProduction(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, left = 280, right = x + left + 30, rw = w - left - 30;
            PageTitle(g, "Управление военными заказами", "");
            var designs = E.Player.Designs; var active = E.FindDesign(E.S.Player, E.Player.ActiveDesign); Blueprint b = active.Blueprint;
            Section(g, "КАТАЛОГ / " + designs.Count + " ПРОЕКТОВ", x, 224, left);
            catalogOffset = Math.Min(catalogOffset, Math.Max(0, designs.Count - 6));
            for (int i = catalogOffset; i < Math.Min(designs.Count, catalogOffset + 6); i++) {
                var record = designs[i]; float y = 264 + (i - catalogOffset) * 66; bool selectedDesign = record.Id == E.Player.ActiveDesign;
                Button(g, record.Blueprint.Name, x, y, left, () => Message(record.Retired ? "Проект в архиве. Восстановите его кнопкой ниже." : E.SelectDesign(record.Id)), selectedDesign, EquipmentRules.Roles[(int)record.Blueprint.Kind], 31);
                Txt(g, EquipmentRules.Names[(int)record.Blueprint.Kind] + " · " + record.Stock + " компл.", x + 10, y + 40, 11, record.Stock > 0 ? Palette.Teal : Palette.Muted, false, left - 75);
                Button(g, record.Retired ? "↶" : "Архив", x + left - 60, y + 35, 60, () => Message(E.RetireDesign(record.Id)), false, "Снять с производства / восстановить. Склад и оснащение частей сохраняются.", 24);
            }
            Button(g, "←", x, 674, 35, () => { catalogOffset = Math.Max(0, catalogOffset - 6); Invalidate(); }, false, "Предыдущие проекты", 28);
            Button(g, "→", x + 44, 674, 35, () => { catalogOffset = Math.Min(Math.Max(0, designs.Count - 6), catalogOffset + 6); Invalidate(); }, false, "Следующие проекты", 28);
            Txt(g, "Склад: " + designs.Sum(d => d.Stock) + " комплектов", x + 96, 682, 11, Palette.Teal);
            Button(g, "Модифицировать проект →", x, 730, left, () => { draft = b.Copy(); draft.Name = draft.Name.Substring(0, Math.Min(30, draft.Name.Length)) + " М" + E.Player.NextDesign; Navigate("Конструктор"); });
            Button(g, "Оснастить соединения →", x, 776, left, () => Navigate("Армия"), true);
            int capacity = E.FactoryCapacity(E.S.Player), assigned = E.AssignedFactories(E.S.Player);
            float strip = rw / 3;
            string[] labels = { "ОБОРОННАЯ МОЩНОСТЬ", "СВОБОДНО", "СЕРИЙ В РАБОТЕ" };
            string[] values = { assigned + " / " + capacity, Math.Max(0, capacity - assigned).ToString(), E.Player.Lines.Count(l => l.Active).ToString() };
            for (int i = 0; i < 3; i++) { Txt(g, labels[i], right + i * strip, 226, 9, Palette.Muted, true); Txt(g, values[i], right + i * strip, 247, 27, i == 1 ? Palette.Teal : Palette.Text, true); }
            Fill(g, Palette.Panel, right - 12, 297, rw + 12, 147);
            Txt(g, b.Name, right + 3, 309, 18, Palette.Gold, true, rw * .48f);
            Txt(g, EquipmentRules.Names[(int)b.Kind] + " · " + Money(b.Cost) + " за комплект", right + 3, 339, 11, Palette.Muted, false, rw * .48f);
            Adjust(g, "Заводы", orderFactories, right + rw * .49f, 309, rw * .24f, d => orderFactories = Math.Max(1, Math.Min(500, orderFactories + d)));
            Adjust(g, "Компл.", orderQuantity, right + rw * .75f, 309, rw * .25f, d => orderQuantity = Math.Max(1, Math.Min(1000, orderQuantity + d)));
            Txt(g, "Сталь " + b.Steel.ToString("0.00") + "  /  машины " + b.Machines.ToString("0.00") + "  /  оружие " + b.Arms.ToString("0.00"), right + 3, 372, 11, Palette.Muted, false, rw - 8);
            bool canOrder = !active.Retired && assigned + orderFactories <= capacity;
            AvailableButton(g, "Заказать " + orderQuantity + " компл. · всего " + Money(b.Cost * orderQuantity), right, 402, rw, () => Message(E.StartProduction(active.Id, orderFactories, orderQuantity)), canOrder,
                canOrder ? "Материалы и деньги списываются по мере работы. Эффективность начинается с 25% и растёт при обеспечении линии." : "Нужны свободные оборонные мощности и действующий проект.", true, 31);
            Section(g, "ОЧЕРЕДЬ ПРОИЗВОДСТВА", right, 463, rw);
            var lines = E.Player.Lines; int visible = 3; lineOffset = Math.Min(lineOffset, Math.Max(0, lines.Count - visible));
            if (lines.Count == 0) {
                NavIcon(g, 2, right + 4, 527, 30, Palette.Muted);
                Txt(g, "Заводы ожидают заказ", right + 52, 528, 19, Palette.Text, true);
                Para(g, "Выберите модель в каталоге, назначьте свободные заводы и укажите размер серии. Готовая техника поступит на склад, откуда её получат соединения.", right + 52, 566, rw - 65, 90, 14);
                Button(g, "Построить оборонные мощности →", right + 52, 687, Math.Min(355, rw - 70), () => { equipmentProduction = false; Invalidate(); });
            }
            for (int i = lineOffset; i < Math.Min(lines.Count, lineOffset + visible); i++) {
                var line = lines[i]; var model = E.FindDesign(E.S.Player, line.DesignId); float y = 505 + (i - lineOffset) * 94;
                if ((i - lineOffset) % 2 == 0) Fill(g, Palette.Card, right - 12, y - 5, rw + 12, 88);
                Txt(g, (i + 1).ToString("00"), right, y + 3, 10, Palette.Gold, true);
                Txt(g, model.Blueprint.Name, right + 29, y, 14, Palette.Text, true, rw - 313);
                Txt(g, E.ProductionStatus(E.S.Player, line), right + 29, y + 23, 10, Palette.Muted, false, rw - 308);
                Progress(g, right + 29, y + 49, rw - 310, (line.Completed + line.Progress) / line.Quantity, Palette.Teal);
                double days = (1 - line.Progress) * model.Blueprint.WorkDays / (line.Factories * line.Efficiency * (1 + E.Player.Tech[2] * .04) * ManpowerRules.Output(E.Player) * PolicyRules.Effects(E.Player, E.S.Day).EquipmentFactor);
                Txt(g, "Выпущено " + line.Completed + "/" + line.Quantity + (line.Completed < line.Quantity ? " · следующий ≈ " + Math.Ceiling(days) + " дн.*" : " · на складе"), right + 29, y + 61, 10, Palette.Muted, false, rw - 308);
                float controls = right + rw - 265;
                Txt(g, "Эффект. " + (line.Efficiency * 100).ToString("0") + "%", controls, y + 2, 10, Palette.Gold);
                Progress(g, controls, y + 24, 91, line.Efficiency, Palette.Gold);
                Txt(g, "Заводы " + line.Factories, controls, y + 39, 10, Palette.Muted);
                AvailableButton(g, "−", controls + 94, y + 35, 27, () => Message(E.ChangeLine(line.Id, line.Factories - 1, line.Paused)), line.Factories > 1 && line.Completed < line.Quantity, "Освободить завод", false, 26);
                AvailableButton(g, "+", controls + 126, y + 35, 27, () => Message(E.ChangeLine(line.Id, line.Factories + 1, line.Paused)), line.Completed < line.Quantity && (line.Paused || assigned < capacity), "Добавить завод", false, 26);
                AvailableButton(g, line.Paused ? "Пуск" : "Пауза", right + rw - 82, y, 82, () => Message(E.ChangeLine(line.Id, line.Factories, !line.Paused)), line.Completed < line.Quantity, "Пауза освобождает заводы и сохраняет прогресс", false, 28);
                Button(g, "Убрать", right + rw - 82, y + 36, 82, () => { if (line.Completed >= line.Quantity || Confirm("Отменить заказ? Готовая техника сохранится, затраты незавершённой работы не возвращаются.")) Message(E.CancelLine(line.Id)); }, false, "Удалить завершённую линию или отменить заказ", 27);
            }
            Button(g, "←", right, 800, 35, () => { lineOffset = Math.Max(0, lineOffset - 3); Invalidate(); }, false, "Предыдущие линии", 26);
            Button(g, "→", right + 44, 800, 35, () => { lineOffset = Math.Min(Math.Max(0, lines.Count - 3), lineOffset + 3); Invalidate(); }, false, "Следующие линии", 26);
            Txt(g, "Приоритет сверху вниз. *Срок при наличии ресурсов.", right + 95, 807, 10, Palette.Muted, false, rw - 95);
        }
    }
}
