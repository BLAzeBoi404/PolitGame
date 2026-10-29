using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool readinessPage;
        private int readinessOffset, readinessFilter, readinessSort;
        private Army[] ReadinessUnits()
        {
            var units = E.S.Armies.Where(a => a.Owner == E.S.Player);
            if (readinessFilter == 1) units = units.Where(a => ReadinessRules.Score(a) < 65 || a.Fatigue >= 50 || a.Supply < 60 || ManpowerRules.Manning(a) < .7);
            if (readinessFilter == 2) units = units.Where(a => a.ExerciseRemaining > 0 || a.TrainingDays > 0);
            return (readinessSort == 1 ? units.OrderBy(a => ReadinessRules.Score(a)).ThenBy(a => a.Id)
                : readinessSort == 2 ? units.OrderByDescending(a => a.Proficiency).ThenBy(a => a.Id)
                : readinessSort == 3 ? units.OrderByDescending(a => a.Fatigue).ThenBy(a => a.Id) : units.OrderBy(a => a.Id)).ToArray();
        }
        private bool ScrollReadiness(MouseEventArgs e)
        {
            var point = Logical(e.Location);
            if (!readinessPage || !detailPanel || menuScreen != "" || point.X >= ExpandedWidth - 410 || point.Y < 400 || point.Y > 810) return false;
            if (simulationBusy) { pendingSimulationActions.Enqueue(() => ScrollReadiness(e)); return true; }
            readinessOffset = Math.Max(0, Math.Min(Math.Max(0, ReadinessUnits().Length - 6), readinessOffset + (e.Delta < 0 ? 3 : -3)));
            InvalidateHud(); return true;
        }
        private void OpenReadiness() { Navigate("Армия"); readinessPage = detailPanel = true; readinessOffset = readinessFilter = readinessSort = 0; Invalidate(); }
        private void ReadinessLink(Graphics g, float x, float y, float width)
        {
            Button(g, "Подготовка и боеготовность →", x, y, width, OpenReadiness, false, "Учения, отдых, снабжение и последствия войны", 28);
            hits[hits.Count - 1].Label = "readiness:open";
        }
        private void ReadinessButton(Graphics g, string id, string text, float x, float y, float width, Action action, string reason = null)
        {
            AvailableButton(g, text, x, y, width, action, reason == null, reason ?? "", false, 30);
            hits[hits.Count - 1].Label = id;
        }
        private void DrawReadiness(Graphics g)
        {
            float x = sidebar + 28, w = VW - sidebar - 56, rightW = 355, leftW = w - rightW - 26, rx = x + leftW + 26;
            var n = E.Player; var report = E.AssessMilitary(E.S.Player);
            var units = ReadinessUnits();
            var selectedArmy = units.FirstOrDefault(a => a.Id == army);
            if (selectedArmy == null && units.Length > 0) { selectedArmy = units[0]; army = selectedArmy.Id; gpuWorldDirty = true; }
            PageTitle(g, "Готовность войск", "Состояние соединений, квалификация и обучение · выберите часть для управления");
            float cardW = (w - 24) / 3;
            string[] labels = { "ГОТОВНОСТЬ СОЕДИНЕНИЙ", "ОПЕРАТИВНЫЙ ЗАПАС", "ВОЕННАЯ УСТАЛОСТЬ" };
            string[] values = { report.Readiness.ToString("0") + "% · " + report.Ready + "/" + report.Formations + " готовы", "Топливо " + report.FuelDays.ToString("0.0") + " д. · вооружение " + report.ArmsDays.ToString("0.0") + " д.", n.Condition.Exhaustion.ToString("0.0") + " / 100" };
            string[] tips = {
                "Готовность учитывает технику, организацию, укомплектованность, снабжение и усталость. Готовая часть: от 65%, вне учений и начального обучения.",
                "Оценка запаса для операций всех наземных частей: расход топлива на марше с поддержкой, технологиями и доктринами; вооружение — 0,01 ед. на батальон в день. Не прогнозирует будущие потери.",
                "Война и реальные потери увеличивают усталость; мир постепенно снижает её. При 100: выпуск −10%, приток резерва −50%, наземная боевая сила −20%. Старые потери при загрузке не начисляются заново."
            };
            for (int k = 0; k < 3; k++) {
                float xx = x + k * (cardW + 12); Card(g, xx, 222, cardW, 78);
                Fill(g, k == 2 && n.Condition.Exhaustion >= 50 ? Palette.Red : k == 1 ? Palette.Gold : Palette.Teal, xx + 12, 236, 3, 12);
                Txt(g, labels[k], xx + 25, 234, 10, Palette.Muted, true, cardW - 24);
                Txt(g, values[k], xx + 12, 259, 17, k == 2 && n.Condition.Exhaustion >= 50 ? Palette.Red : Palette.Text, true, cardW - 24);
                hits.Add(new Hit { Rect = new RectangleF(xx,222,cardW,78), Label = "readiness:summary:"+k, Tip = tips[k] });
            }
            Txt(g, report.Posture + " · " + report.Reason, x, 314, 12, Palette.Muted, false, w);
            Section(g, "СОЕДИНЕНИЯ / " + units.Length, x, 346, leftW);
            string[] filters = { "Все части", "Требуют внимания", "Обучаются" };
            float[] widths = { 94, 172, 118 };
            float filterX = x;
            for (int f = 0; f < filters.Length; f++) {
                int value = f;
                Button(g, filters[f], filterX, 380, widths[f], () => { readinessFilter = value; readinessOffset = 0; InvalidateHud(); }, readinessFilter == f,
                    f == 1 ? "Готовность <65%, усталость от 50%, снабжение <60% или менее 70% личного состава" : f == 2 ? "Учения, начальная подготовка и переоснащение" : "Все собственные наземные части", 28);
                hits[hits.Count - 1].Label = "readiness:filter:" + f; filterX += widths[f] + 8;
            }
            Button(g, new[] { "По номеру", "Готовность ↑", "Подготовка ↓", "Усталость ↓" }[readinessSort], x + leftW - 147, 380, 147,
                () => { readinessSort = (readinessSort + 1) % 4; readinessOffset = 0; InvalidateHud(); }, false, "Изменить порядок соединений", 28);
            hits[hits.Count - 1].Label = "readiness:sort";
            if (units.Length == 0) Para(g, "По выбранному фильтру частей нет. Измените фильтр, чтобы вернуться ко всем соединениям.", x + 12, 451, leftW - 24, 95, 15, Palette.Muted);
            readinessOffset = Math.Max(0, Math.Min(readinessOffset, Math.Max(0, units.Length - 6)));
            for (int i = readinessOffset; i < Math.Min(units.Length, readinessOffset + 6); i++) {
                var a = units[i]; float y = 424 + (i - readinessOffset) * 55;
                Fill(g, a.Id == army ? Palette.Selected : Palette.Card, x, y - 4, leftW, 50);
                if (a.Id == army) Fill(g, Palette.Gold, x, y + 3, 3, 34);
                Txt(g, "№" + a.Id + " · " + W.Regions[a.Region].Name, x + 10, y + 3, 14, Palette.Text, true, leftW - 130);
                Txt(g, ReadinessRules.Score(a).ToString("0") + "%", x + leftW - 100, y + 3, 16, ReadinessRules.Score(a) < 50 ? Palette.Red : Palette.Teal, true, 90);
                Txt(g, UnitQualityRules.Name(a) + " · усталость " + a.Fatigue.ToString("0") + "% · " + (a.ExerciseRemaining > 0 ? "учения " + a.ExerciseRemaining + " д." : a.TrainingDays > 0 ? "начальная подготовка" : a.Target >= 0 ? "марш" : "отдых / служба"), x + 10, y + 26, 11, Palette.Muted, false, leftW - 20);
                hits.Add(new Hit { Rect = new RectangleF(x, y - 4, leftW, 50), Label = "readiness:unit:" + a.Id, FocusTarget = true, Action = () => { army = a.Id; Invalidate(); }, Tip = "Выбрать соединение для учений и просмотра условий" });
            }
            ReadinessButton(g, "readiness:previous", "←", x, 772, 45, () => { readinessOffset = Math.Max(0, readinessOffset - 6); Invalidate(); });
            ReadinessButton(g, "readiness:next", "→", x + 54, 772, 45, () => { readinessOffset = Math.Min(Math.Max(0, units.Length - 6), readinessOffset + 6); Invalidate(); });
            Txt(g, units.Length == 0 ? "Нет соединений" : (readinessOffset + 1) + "–" + Math.Min(units.Length, readinessOffset + 6) + " / " + units.Length, x + 113, 780, 12, Palette.Muted);
            ReadinessButton(g, "readiness:back", "← Командование", x, 824, 195, () => Navigate("Армия"));
            ReadinessButton(g, "readiness:trade", "Снабжение и торговля →", x + 208, 824, 225, OpenTrade);
            Section(g, "ПОДГОТОВКА ЧАСТИ", rx, 348, rightW);
            if (selectedArmy == null) { Para(g, "Выберите другой фильтр или наберите соединение из имеющихся людей и техники.", rx, 396, rightW, 100, 16); return; }
            var unit = selectedArmy;
            Book(g, "Соединение №" + unit.Id, rx, 385, 22, Palette.Text, true, rightW);
            Txt(g, People(unit.Personnel) + " / " + People(ManpowerRules.Establishment(unit)) + " чел. · " + unit.Battalions + " бат.", rx, 422, 13, Palette.Muted, false, rightW);
            Txt(g, "Техника " + unit.Strength.ToString("0") + "% · организация " + unit.Organization.ToString("0") + "%", rx, 449, 13, Palette.Text, false, rightW);
            Txt(g, "Снабжение " + unit.Supply.ToString("0") + "% · опыт " + unit.Experience.ToString("0.0"), rx, 476, 13, Palette.Teal, false, rightW);
            Txt(g, "Усталость " + unit.Fatigue.ToString("0.0") + "% · сила боя ×" + ReadinessRules.FatigueFactor(unit).ToString("0.00"), rx, 503, 13, Palette.Gold, false, rightW);
            Txt(g, UnitQualityRules.Name(unit) + " · подготовка " + unit.Proficiency.ToString("0.0") + "/100", rx, 528, 13, Palette.Gold, true, rightW);
            var country = W.Countries[unit.Owner];
            string basis = country.ReferenceFundingPerPerson > 0 ? "Стартовая оценка возможностей: расходы / численность, " + country.FundingYear + "; ВВП " + country.FundingGdpYear + "." : "Для страны нет совместимых данных: нейтральная стартовая оценка.";
            hits.Add(new Hit { Rect = new RectangleF(rx,525,rightW,22), Label = "quality:detail", Tip = "Звёзды — квалификация, отдельно от готовности. Пороги подготовки: 30 / 50 / 70 / 85. Элита: подготовка от 85 и опыт от 35. Подготовка влияет на боевую силу (×0,70–1,30). Пополнения имеют подготовку 30 и снижают среднюю квалификацию. " + basis + " Это игровая модель, не рейтинг реальных частей. Любая страна может обучать войска при наличии денег и снабжения." });
            Section(g, "КУРС УЧЕНИЙ · ДО 21 ДНЯ", rx, 558, rightW);
            Txt(g, "Доп. расходы в день: " + (E.ExerciseDailyCost(unit) * 1000).ToString("0.00") + " млн", rx, 597, 14, Palette.Text, true, rightW);
            Txt(g, "Топливо " + ReadinessRules.ExerciseFuel(unit).ToString("0.000") + " · вооружение " + ReadinessRules.ExerciseArms(unit).ToString("0.000") + " ед.", rx, 624, 12, Palette.Muted, false, rightW);
            string reason = unit.ExerciseRemaining > 0 ? null : E.ExerciseReason(unit.Id);
            string status = unit.ExerciseRemaining > 0 ? E.ExerciseStatus(unit) : reason ?? "Курс доступен: подготовка до 90, опыт до 35. Высокая квалификация дороже; после учений нужен отдых.";
            Para(g, status, rx, 654, rightW, 70, 13, reason == null ? Palette.Teal : Palette.Gold);
            ReadinessButton(g, "readiness:exercise", unit.ExerciseRemaining > 0 ? "Прекратить учения" : "Начать учения", rx, 733, rightW, () => Message(unit.ExerciseRemaining > 0 ? E.StopExercise(unit.Id) : E.StartExercise(unit.Id)), reason);
            Para(g, "Цена растёт с подготовкой выше 50. Оплата ежедневно; курс снимает часть с фронта. Приказ на марш прекращает учения, сохраняя результат.", rx, 782, rightW, 74, 11, Palette.Muted);
        }
    }
}
