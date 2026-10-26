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
            if (blueprintExpanded) { DrawTechnicalBoard(g); return; }
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
                AvailableButton(g, "+", x + lw - 27, y + 19, 27, () => { focusedModule = slot; EquipmentRules.SetModule(draft, slot, modules[slot] + 1); Invalidate(); }, modules[i] < cap, cap < TechnologyRules.ModuleCap(E.Player, i) ? "Предел этого класса техники: " + cap : cap < 8 ? "Следующий уровень: " + TechnologyRules.ModuleRequirement(i, cap + 1) : "Достигнут предельный уровень.", false, 26);
            }
            Button(g, "Автокомпоновка", x, 774, lw, () => { draft = EquipmentRules.Balanced(draft.Kind, E.Player, draft.Name); Invalidate(); }, false, "Сбалансированные модули до уровня 4. Более высокий уровень может увеличить расходы.");
            Button(g, "Исследования модулей →", x, 818, lw, () => { int cap = EquipmentRules.Cap(draft, focusedModule, E.Player); OpenResearchProject(cap >= 6 && cap < 8 ? TechnologyRules.ModuleTechnology(focusedModule, cap + 1) + 6 : TechnologyRules.ModuleBranch(focusedModule)); }, false, "Требования к следующему уровню выбранного модуля; ограничения класса сохраняются", 25);
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
            using (var background = new LinearGradientBrush(box, Color.FromArgb(31,43,43), Color.FromArgb(15,25,26), 90)) g.FillRectangle(background, box);
            for (float xx = box.Left + 14; xx < box.Right; xx += 28) Line(g, Color.FromArgb(15,180,200,180), xx, box.Top, xx, box.Bottom);
            for (float yy = box.Top + 14; yy < box.Bottom; yy += 28) Line(g, Color.FromArgb(15,180,200,180), box.Left, yy, box.Right, yy);
            HairlineFrame(g, new RectangleF(box.X + 12,box.Y + 12,box.Width - 24,box.Height - 24),Color.FromArgb(85,104,91));
            Txt(g, "SOV / КБ–" + ((int)design.Kind+1).ToString("00") + "   ·   РЕВ. " + EquipmentRules.Modules(design).Sum().ToString("00"), box.X+24,box.Y+23,10,Palette.Gold,true);
            float artHeight = (box.Height - 127) / 2;
            Txt(g, "01  БОКОВАЯ ПРОЕКЦИЯ",box.X+25,box.Y+51,8,Palette.Muted);
            TechnicalSilhouette(g,new RectangleF(box.X+20,box.Y+62,box.Width-40,artHeight),design,false);
            float split=box.Y+66+artHeight;
            Line(g,Color.FromArgb(75,95,80),box.X+24,split,box.Right-24,split);
            Txt(g,"02  ПЛАН / " + EquipmentRules.ModuleNames[module].ToUpperInvariant(),box.X+25,split+10,8,Palette.Gold);
            TechnicalSilhouette(g,new RectangleF(box.X+20,split+24,box.Width-40,artHeight),design,true);
            Txt(g,design.Weight.ToString("0.0")+" т  /  "+design.SpeedKph.ToString("0")+" км/ч  /  "+design.Range.ToString("0")+" км",box.X+24,box.Bottom-29,9,Palette.Muted,false,box.Width-48);
            g.Restore(state);
            if (!blueprintExpanded) {
                Button(g,"Развернуть чертёж ↗",box.Right-182,box.Y+18,160,()=>{blueprintExpanded=true;Invalidate();},false,"Две проекции и выбор узлов",22);
                hits[hits.Count-1].Label="blueprint:expand";
            }
        }
    }
}
