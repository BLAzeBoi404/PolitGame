using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private readonly HashSet<int> selectedUnits = new HashSet<int>();
        private int frontOffset;
        private void DrawArmy(Graphics g)
        {
            if (readinessPage) { DrawReadiness(g); return; }
            if (deploymentPage) { DrawDeployment(g); return; }
            float x = sidebar + 34, w = VW - sidebar - 68, fw = 302, listWidth = w - fw - 28, right = x + listWidth + 28;
            var units = E.S.Armies.Where(a => a.Owner == E.S.Player).ToList();
            selectedUnits.RemoveWhere(id => !units.Any(a => a.Id == id));
            var active = E.FindDesign(E.S.Player, E.Player.ActiveDesign);
            Button(g, "Резерв и штаты →", x + w - 285, 178, 155, OpenManpower, false, "Управление личным составом", 25);
            Button(g, "Авиация →", x + w - 120, 178, 120, OpenAviation, false, "Самостоятельные воздушные операции", 25);
            PageTitle(g, "Командование сухопутными силами", "Выберите соединения и задайте фронт. Ручной приказ на карте снимает часть с фронта.");
            var template = E.SelectedTemplate(E.S.Player); string recruitReason = E.RecruitmentReason(E.S.Player, active.Id, template.Id);
            AvailableButton(g, RecruitButtonText(), x, 218, 245, () => Message(E.Recruit(OwnRegion())), recruitReason == null,
                recruitReason ?? (template.Name + ": " + template.Battalions + " компл., " + People(ManpowerRules.GroundSize(active.Blueprint.Kind, template.Battalions)) + " чел., " + ManpowerRules.TrainingDays(template.Battalions) + " дней подготовки."), true);
            Button(g, "Склад и оснащение →", x + 258, 218, 215, () => { equipmentProduction = true; Navigate("Производство"); });
            Txt(g, template.Name + " · " + active.Stock + " компл. · резерв " + People(E.Player.Recruits), x + 488, 227, 12, Palette.Gold, false, w - 488);
            Button(g, "Боевой порядок →", x, VH - 101, 200, OpenDeployment, false, "Передовая, фланги, резерв и специализация", 25);
            Section(g, "БОЕВОЙ СОСТАВ / " + units.Count + " СОЕДИНЕНИЙ", x, 274, listWidth);
            Button(g, selectedUnits.Count == units.Count && units.Count > 0 ? "Снять выбор" : "Выбрать все", x + listWidth - 127, 267, 127, () => {
                if (selectedUnits.Count == units.Count) selectedUnits.Clear(); else foreach (var unit in units) selectedUnits.Add(unit.Id); Invalidate();
            }, false, "Назначить несколько соединений одному фронту", 27);
            int visible = Math.Max(1, (int)((VH - 417) / 114)); int offset = Math.Min(scroll, Math.Max(0, units.Count - visible));
            for (int i = offset; i < Math.Min(units.Count, offset + visible); i++) {
                var unit = units[i]; float y = 316 + (i - offset) * 114; bool chosen = selectedUnits.Contains(unit.Id);
                Fill(g, chosen ? Palette.Selected : Palette.Card, x, y, listWidth, 103);
                Button(g, chosen ? "✓" : "+", x + 10, y + 11, 30, () => { if (!selectedUnits.Add(unit.Id)) selectedUnits.Remove(unit.Id); Invalidate(); }, chosen, "Выбрать часть №" + unit.Id + " для фронта", 29);
                Txt(g, "№" + unit.Id + "  /  " + unit.Design.Name + " · " + unit.Battalions + " бат.", x + 51, y + 11, 14, Palette.Text, true, listWidth - 209);
                var plan = E.FrontFor(unit.Id);
                Txt(g, DeploymentRules.Names[(int)DeploymentRules.For(unit).Specialty] + " · " + W.Regions[unit.Region].Name + (plan == null ? "" : " · " + plan.Name), x + 51, y + 35, 11, Palette.Muted, false, listWidth - 209);
                float statW = Math.Max(80, (listWidth - 245) / 3);
                Txt(g, "Готовность " + ReadinessRules.Score(unit).ToString("0") + "%", x + 51, y + 58, 10, Palette.Teal);
                Progress(g, x + 51, y + 80, statW - 15, ReadinessRules.Score(unit) / 100, Palette.Teal);
                Txt(g, "Снабжение " + unit.Supply.ToString("0") + "%", x + 51 + statW, y + 58, 10, unit.Supply < 40 ? Palette.Red : Palette.Gold);
                Progress(g, x + 51 + statW, y + 80, statW - 15, unit.Supply / 100, Palette.Gold);
                string status = unit.TrainingDays > 0 ? "Обучение " + unit.TrainingDays + " д." : unit.Target >= 0 ? "Марш " + Math.Ceiling(unit.Days) + " д." : "В резерве";
                Txt(g, status, x + 51 + statW * 2, y + 58, 10, Palette.Muted, false, statW + 12);
                Txt(g, People(unit.Personnel) + "/" + People(ManpowerRules.Establishment(unit)) + " чел. · орг. " + unit.Organization.ToString("0") + "%", x + 51 + statW * 2, y + 79, 10, unit.Organization < 40 ? Palette.Red : Palette.Muted, false, statW + 12);
                float bx = x + listWidth - 146;
                Button(g, "На карте →", bx, y + 6, 135, () => { army = unit.Id; selected = unit.Region; cx = (float)W.Regions[unit.Region].X; cy = (float)W.Regions[unit.Region].Y; scale = 30; Navigate("Карта"); }, false, "Выбрать соединение", 20);
                bool ready = unit.TrainingDays == 0 && unit.Target < 0 && E.S.Provinces[unit.Region].Owner == E.S.Player && active.Stock > 0 && !active.Retired;
                AvailableButton(g, "Перевооружить", bx, y + 30, 135, () => { if (Confirm("Заменить технику на «" + active.Blueprint.Name + "»? 14 дней обучения и " + unit.Battalions + " комплектов; исправная старая техника вернётся на склад.")) Message(E.Refit(unit.Id, active.Id)); }, ready && active.Stock >= unit.Battalions && active.Blueprint.Formation && unit.DesignId != active.Id, "Готовый комплект + 15% его стоимости; часть должна стоять в своей области.", false, 20);
                AvailableButton(g, "+ Поддержка", bx, y + 54, 135, () => Message(E.Refit(unit.Id, active.Id, true)), ready && active.Blueprint.Kind != EquipmentKind.Tank && active.Blueprint.Kind != EquipmentKind.IFV && unit.Attachments.Count < 3 && !unit.Attachments.Any(t => t.Blueprint.Kind == active.Blueprint.Kind), "Выберите класс поддержки в каталоге; нужны комплект и 14 дней подготовки.", false, 20);
                Button(g, "Расформировать", bx, y + 78, 135, () => { if (Confirm("Расформировать часть №" + unit.Id + "? Личный состав вернётся в резерв, уцелевшая техника — на склад.")) Message(E.DisbandArmy(unit.Id)); }, false, "Только на стоянке в своей снабжаемой области", 20);
            }
            ReadinessLink(g, x + 218, VH - 103, listWidth - 218);
            DrawFrontCommand(g, right, 273, fw);
        }
        private void DrawFrontCommand(Graphics g, float x, float y, float w)
        {
            Section(g, "ОПЕРАТИВНЫЕ ПРИКАЗЫ", x, y, w);
            bool hasEnemy = foreign >= 0 && E.AtWar(E.S.Player, foreign);
            if (!hasEnemy) {
                int candidate = Enumerable.Range(0, W.Countries.Length).FirstOrDefault(c => E.AtWar(E.S.Player, c));
                if (E.AtWar(E.S.Player, candidate)) { foreign = candidate; hasEnemy = true; }
            }
            Txt(g, "НАПРАВЛЕНИЕ", x, y + 42, 9, Palette.Muted, true);
            Button(g, hasEnemy ? W.Countries[foreign].Name : "Выбрать противника…", x, y + 63, w, ChooseForeign, false, "Выберите государство. Фронт можно создать только в войне.");
            bool canCreate = hasEnemy && selectedUnits.Count > 0;
            string reason = !hasEnemy ? "Выберите страну, с которой идёт война, в дипломатическом досье." : "Отметьте соединения в списке слева.";
            AvailableButton(g, "Удерживать", x, y + 111, w / 2 - 5, () => Message(E.CreateFront(foreign, selectedUnits.ToArray(), FrontStance.Hold)), canCreate, canCreate ? "Занять фронтовые области и удерживать. Атаки не выполняются." : reason, true);
            AvailableButton(g, "Наступать", x + w / 2 + 5, y + 111, w / 2 - 5, () => Message(E.CreateFront(foreign, selectedUnits.ToArray(), FrontStance.Advance)), canCreate, canCreate ? "Подтянуть части, атаковать при достаточных силах. Истощённые части восстанавливаются." : reason);
            var plans = E.S.Fronts.Where(p => p.Owner == E.S.Player).ToList();
            if (plans.Count == 0) Para(g, "Фронт пересматривает приказы каждые 7 дней. Учитываются снабжение, готовность, оборона противника и доступ через союзников.", x, y + 181, w, 130, 13);
            frontOffset = Math.Min(frontOffset, Math.Max(0, plans.Count - 2));
            for (int i = frontOffset; i < Math.Min(plans.Count, frontOffset + 2); i++) {
                var plan = plans[i]; float yy = y + 180 + (i - frontOffset) * 149;
                Fill(g, Palette.Panel, x - 9, yy, w + 18, 137);
                Txt(g, plan.Name, x + 2, yy + 12, 14, Palette.Text, true, w - 2);
                Txt(g, plan.ArmyIds.Count + " · " + E.FrontStatus(plan), x + 2, yy + 38, 11, Palette.Muted, false, w - 2);
                Button(g, "Оборона", x, yy + 63, w / 2 - 5, () => Message(E.ChangeFront(plan.Id, FrontStance.Hold, true)), plan.Active && plan.Stance == FrontStance.Hold, "Занять и удерживать границу", 27);
                Button(g, "Наступление", x + w / 2 + 5, yy + 63, w / 2 - 5, () => Message(E.ChangeFront(plan.Id, FrontStance.Advance, true)), plan.Active && plan.Stance == FrontStance.Advance, "Продвигаться при достаточных силах", 27);
                Button(g, plan.Active ? "Остановить" : "Продолжить", x, yy + 101, w / 2 - 5, () => Message(E.ChangeFront(plan.Id, plan.Stance, !plan.Active)), false, "Остановить движение или возобновить план", 26);
                Button(g, "Расформировать", x + w / 2 + 5, yy + 101, w / 2 - 5, () => E.DeleteFront(plan.Id), false, "Снять автоматические приказы; части сохраняются", 26);
            }
            if (plans.Count > 2) {
                Button(g, "←", x, y + 491, 35, () => { frontOffset = Math.Max(0, frontOffset - 2); Invalidate(); }, false, "Предыдущие фронты", 26);
                Button(g, "→", x + 43, y + 491, 35, () => { frontOffset = Math.Min(plans.Count - 2, frontOffset + 2); Invalidate(); }, false, "Следующие фронты", 26);
            }
            Button(g, "Дипломатия и условия войны →", x, y + 532, w, () => Navigate("Дипломатия"), false, "Союзы, мир и военный доступ", 30);
        }
    }
}
