using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private int compareDesignId, focusedModule = 1;
        private void DrawDesigner(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, lw = 264, rw = 270, center = x + lw + 20, cw = w - lw - rw - 40, right = center + cw + 20;
            PageTitle(g, "Конструктор вооружений", "От проекта до соединения: исследуйте модули, испытайте модель и закажите серию.");
            float tab = w / 8;
            for (int i = 0; i < 8; i++) {
                var kind = (EquipmentKind)i;
                Button(g, EquipmentRules.Names[i], x + i * tab, 217, tab - 3, () => {
                    draft = EquipmentRules.Balanced(kind, E.Player, EquipmentRules.Names[(int)kind] + " " + E.Player.NextDesign);
                    compareDesignId = 0; Invalidate();
                }, draft.Kind == kind, EquipmentRules.Roles[i], 35);
            }
            Section(g, "КОМПОНОВКА", x, 278, lw);
            string[] notes = { "Защита / нагрузка на шасси", "Пробитие / масса и цена", "Мощность / расход топлива", "Точность / сложность сборки", "Живучесть / масса корпуса", "Подвижность / стоимость", "Автономность / масса", "Надёжность / обслуживание" };
            int[] modules = EquipmentRules.Modules(draft);
            for (int i = 0; i < 8; i++) {
                int slot = i, cap = EquipmentRules.Cap(draft, i, E.Player); float y = 316 + i * 55;
                if (focusedModule == i) Fill(g, Palette.Card, x - 8, y - 3, lw + 8, 53);
                Txt(g, (i + 1).ToString("00"), x, y + 2, 10, Palette.Gold, true);
                Txt(g, EquipmentRules.ModuleNames[i], x + 26, y, 12, Palette.Text, true, lw - 27);
                Txt(g, notes[i], x + 26, y + 23, 9, Palette.Muted, false, lw - 142);
                AvailableButton(g, "−", x + lw - 108, y + 19, 27, () => { focusedModule = slot; EquipmentRules.SetModule(draft, slot, modules[slot] - 1); Invalidate(); }, modules[i] > 1, "Уменьшить уровень: " + EquipmentRules.ModuleNames[i], false, 26);
                Txt(g, modules[i] + "/" + cap, x + lw - 73, y + 25, 12, Palette.Gold, true);
                AvailableButton(g, "+", x + lw - 27, y + 19, 27, () => { focusedModule = slot; EquipmentRules.SetModule(draft, slot, modules[slot] + 1); Invalidate(); }, modules[i] < cap, "Лимит модуля связан с исследованиями. Откройте технологии для повышения.", false, 26);
            }
            Button(g, "Автокомпоновка", x, 774, lw, () => { draft = EquipmentRules.Balanced(draft.Kind, E.Player, draft.Name); Invalidate(); }, false, "Сбалансированные модули до уровня 4. Более высокий уровень может увеличить расходы.");
            DrawBlueprintSheet(g, new RectangleF(center, 278, cw, 365), draft, focusedModule);
            Txt(g, "ПРОЕКТ / " + EquipmentRules.Names[(int)draft.Kind].ToUpperInvariant(), center, 663, 10, Palette.Gold, true);
            Txt(g, draft.Name, center, 686, 23, Palette.Text, true, cw - 115);
            Button(g, "Название…", center + cw - 104, 682, 104, RenameDraft, false, "Имя модели должно быть уникальным", 31);
            Para(g, EquipmentRules.Roles[(int)draft.Kind], center, 724, cw, 36, 12);
            string error = EquipmentRules.Validate(draft, E.Player);
            if (error == null && E.Player.Designs.Any(d => string.Equals(d.Blueprint.Name, draft.Name.Trim(), StringComparison.OrdinalIgnoreCase))) error = "Укажите новое уникальное название.";
            if (error == null && E.Player.Treasury < draft.Cost * 2) error = "Недостаточно средств на испытания.";
            AvailableButton(g, "Испытать и сохранить · " + Money(draft.Cost * 2), center, 774, cw, () => Message(E.Adopt(draft)), error == null, error ?? "Испытания создают новую модель. Серия заказывается отдельно.", true);
            Txt(g, error ?? ("На комплект: сталь " + draft.Steel.ToString("0.00") + " · машины " + draft.Machines.ToString("0.00") + " · оружие " + draft.Arms.ToString("0.00")), center, 819, 10, error == null ? Palette.Muted : Palette.Red, false, cw);
            var comparisons = E.Player.Designs.Where(d => d.Blueprint.Kind == draft.Kind).ToList();
            var compare = comparisons.FirstOrDefault(d => d.Id == compareDesignId) ?? comparisons.FirstOrDefault(d => d.Id == E.Player.ActiveDesign) ?? comparisons.LastOrDefault();
            Blueprint baseline = compare == null ? EquipmentRules.Balanced(draft.Kind, E.Player, "Базовый проект") : compare.Blueprint;
            Section(g, "ХАРАКТЕРИСТИКИ", right, 278, rw);
            string[] labels = { "Атака", "Защита", "Броня", "Пробитие", "Скорость, км/ч", "Дальность, км", "Надёжность, %", "Масса, т", "Топливо / день", "Цена комплекта" };
            double[] values = { draft.Attack, draft.Defense, draft.Protection, draft.Penetration, draft.SpeedKph, draft.Range, draft.Reliability, draft.Weight, draft.Fuel, draft.Cost };
            double[] bases = { baseline.Attack, baseline.Defense, baseline.Protection, baseline.Penetration, baseline.SpeedKph, baseline.Range, baseline.Reliability, baseline.Weight, baseline.Fuel, baseline.Cost };
            for (int i = 0; i < labels.Length; i++) {
                float y = 316 + i * 38; double delta = values[i] - bases[i]; bool better = i >= 7 ? delta < 0 : delta > 0;
                Txt(g, labels[i], right, y, 11, Palette.Muted, false, 144);
                Txt(g, values[i].ToString(i >= 8 ? "0.000" : "0.0"), right + 143, y - 1, 15, Palette.Text, true, 68);
                Txt(g, delta.ToString("+0.##;-0.##;—"), right + 212, y + 1, 10, Math.Abs(delta) < .00001 ? Palette.Muted : better ? Palette.Teal : Palette.Red, false, 58);
                Line(g, Palette.Line, right, y + 27, right + rw, y + 27);
            }
            Txt(g, "СРАВНИТЬ С МОДЕЛЬЮ", right, 714, 9, Palette.Gold, true);
            Button(g, baseline.Name + (comparisons.Count > 1 ? "  ›" : ""), right, 736, rw, () => {
                if (comparisons.Count > 1) { int index = comparisons.FindIndex(d => d.Id == compare.Id); compareDesignId = comparisons[(index + 1) % comparisons.Count].Id; Invalidate(); }
            }, false, "Нажмите для переключения между проектами этого класса. Зелёное изменение — улучшение, красное — цена компромисса.", 31);
            Txt(g, "Сборка " + draft.WorkDays.ToString("0") + " заводо-дней", right, 789, 12, Palette.Text);
            Txt(g, "Содержание " + draft.Maintenance.ToString("0.0000") + " млрд/день", right, 815, 10, Palette.Muted);
        }
        private void DrawBlueprintSheet(Graphics g, RectangleF box, Blueprint design, int module)
        {
            var state = g.Save(); g.SetClip(box, CombineMode.Intersect);
            Fill(g, Color.FromArgb(25, 49, 61), box.X, box.Y, box.Width, box.Height);
            for (float xx = box.Left; xx < box.Right; xx += 20) Line(g, Color.FromArgb(24, 160, 195, 203), xx, box.Top, xx, box.Bottom);
            for (float yy = box.Top; yy < box.Bottom; yy += 20) Line(g, Color.FromArgb(24, 160, 195, 203), box.Left, yy, box.Right, yy);
            HairlineFrame(g, new RectangleF(box.X + 12, box.Y + 12, box.Width - 24, box.Height - 24), Color.FromArgb(86, 117, 127));
            Txt(g, "КБ / " + ((int)design.Kind + 1).ToString("00") + "   ·   ПРОЕКЦИЯ КЛАССА", box.X + 23, box.Y + 25, 9, Color.FromArgb(167, 196, 201), true);
            Txt(g, "МОДУЛЬ " + (module + 1).ToString("00"), box.Right - 109, box.Y + 25, 9, Palette.Gold, true);
            var drawing = new RectangleF(box.X + 26, box.Y + 74, box.Width - 52, box.Height - 142);
            DrawEquipmentSymbol(g, drawing, design);
            float dy = box.Bottom - 60;
            Line(g, Palette.Muted, box.X + 28, dy, box.Right - 28, dy);
            for (float xx = box.X + 28; xx <= box.Right - 28; xx += 20) Line(g, Palette.Muted, xx, dy - 3, xx, dy + 3);
            Txt(g, design.Air ? "ПЛАНЕР / ВИД СВЕРХУ" : "ШАССИ / БОКОВАЯ ПРОЕКЦИЯ", box.X + 24, box.Bottom - 39, 9, Palette.Muted);
            Txt(g, "НЕ В МАСШТАБЕ", box.Right - 117, box.Bottom - 39, 8, Palette.Muted);
            g.Restore(state);
        }
        private void DrawEquipmentSymbol(Graphics g, RectangleF box, Blueprint design)
        {
            var state = g.Save(); float scaleArt = Math.Min(box.Width / 420, box.Height / 220);
            g.TranslateTransform(box.X + (box.Width - 420 * scaleArt) / 2, box.Y + (box.Height - 220 * scaleArt) / 2);
            g.ScaleTransform(scaleArt, scaleArt);
            using (var outline = new Pen(Color.FromArgb(192, 217, 217), 1.6f))
            using (var fine = new Pen(Color.FromArgb(97, 145, 157), 1))
            using (var highlight = new Pen(Palette.Gold, 2.2f)) {
                fine.DashStyle = DashStyle.Dot;
                g.DrawLine(fine, 5, 110, 415, 110); g.DrawLine(fine, 210, 4, 210, 216);
                if (design.Air) {
                    bool heavy = design.Kind == EquipmentKind.Bomber || design.Kind == EquipmentKind.Transport;
                    float wing = heavy ? 32 : 74;
                    PointF[] body = { new PointF(210, 10), new PointF(220, 32), new PointF(224, 95), new PointF(390, 95 + wing), new PointF(386, 112 + wing), new PointF(224, 131), new PointF(220, 174), new PointF(277, 200), new PointF(275, 211), new PointF(210, 198), new PointF(145, 211), new PointF(143, 200), new PointF(200, 174), new PointF(196, 131), new PointF(34, 112 + wing), new PointF(30, 95 + wing), new PointF(196, 95), new PointF(200, 32) };
                    g.DrawPolygon(outline, body); g.DrawEllipse(outline, 203, 35, 14, 37); g.DrawLine(outline, 210, 78, 210, 187);
                    g.DrawLine(outline, 46, 112 + wing, 196, 111); g.DrawLine(outline, 224, 111, 374, 112 + wing);
                    for (int j = 0; j < (heavy ? 4 : 2); j++) { float ex = heavy ? 111 + j * 63 : 182 + j * 44; g.DrawRectangle(outline, ex, heavy ? 94 : 85, 13, 42); g.DrawLine(highlight, ex + 2, heavy ? 137 : 129, ex + 11, heavy ? 137 : 129); }
                    if (design.Kind == EquipmentKind.Recon) { g.DrawEllipse(highlight, 201, 140, 18, 18); g.DrawLine(fine, 210, 158, 210, 177); }
                    if (design.Kind == EquipmentKind.Bomber) for (int j = 0; j < 4; j++) g.DrawRectangle(fine, 205, 83 + j * 17, 10, 12);
                    if (design.Kind == EquipmentKind.Transport) g.DrawRectangle(highlight, 202, 142, 16, 29);
                } else {
                    bool truck = design.Kind == EquipmentKind.Truck, artillery = design.Kind == EquipmentKind.Artillery;
                    PointF[] hull = { new PointF(41, 134), new PointF(65, 94), new PointF(294, 94), new PointF(363, 125), new PointF(376, 153), new PointF(53, 153) };
                    g.DrawPolygon(outline, hull);
                    if (truck) {
                        g.DrawRectangle(outline, 57, 58, 213, 69); g.DrawRectangle(outline, 282, 65, 63, 64);
                        g.DrawRectangle(outline, 297, 73, 37, 29);
                        for (int j = 0; j < 9; j++) g.DrawLine(outline, 67 + j * 22, 65, 67 + j * 22, 120);
                        for (int j = 0; j < 4; j++) { float wx = j < 3 ? 82 + j * 48 : 320; g.DrawEllipse(outline, wx - 19, 141, 38, 38); g.DrawEllipse(outline, wx - 10, 150, 20, 20); }
                        g.DrawLine(highlight, 349, 112, 360, 112);
                    } else {
                        g.DrawArc(outline, 37, 142, 46, 45, 90, 180); g.DrawArc(outline, 324, 142, 46, 45, 270, 180);
                        g.DrawLine(outline, 60, 142, 347, 142); g.DrawLine(outline, 60, 187, 347, 187);
                        for (int j = 0; j < 7; j++) { g.DrawEllipse(outline, 64 + j * 41, 149, 32, 32); g.DrawEllipse(outline, 73 + j * 41, 158, 14, 14); }
                        for (int j = 0; j < 25; j++) g.DrawLine(fine, 64 + j * 11, 183, 64 + j * 11, 189);
                        float turretX = artillery ? 98 : 155, barrelY = artillery ? 42 : 68;
                        g.DrawPolygon(outline, new[] { new PointF(turretX, 95), new PointF(turretX + 12, 57), new PointF(turretX + 84, 57), new PointF(turretX + 107, 91) });
                        g.DrawPolygon(focusedModule == 1 ? highlight : outline, new[] { new PointF(turretX + 87, 66), new PointF(403, barrelY), new PointF(405, barrelY + 7), new PointF(turretX + 90, 77) });
                        g.DrawRectangle(outline, turretX + 21, 48, 35, 9); g.DrawLine(outline, turretX + 16, 56, turretX + 9, 20);
                        for (int j = 0; j < 6; j++) g.DrawLine(outline, 75 + j * 8, 104, 72 + j * 8, 122);
                        g.DrawRectangle(outline, 283, 109, 26, 20); g.DrawEllipse(outline, 337, 125, 11, 11);
                        if (design.Kind == EquipmentKind.IFV) for (int j = 0; j < 5; j++) g.DrawRectangle(outline, 128 + j * 25, 111, 12, 9);
                        if (focusedModule == 0) g.DrawLine(highlight, 297, 96, 363, 125);
                        if (focusedModule == 5) g.DrawLine(highlight, 64, 188, 344, 188);
                    }
                    g.DrawLine(fine, 41, 198, 376, 198); g.DrawLine(outline, 41, 194, 41, 202); g.DrawLine(outline, 376, 194, 376, 202);
                }
            }
            g.Restore(state);
        }
    }
}
