using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool readinessPage;
        private int readinessOffset;
        private void OpenReadiness() { Navigate("Армия"); readinessPage = detailPanel = true; readinessOffset = 0; Invalidate(); }
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
            var units = E.S.Armies.Where(a => a.Owner == E.S.Player).ToArray();
            var selectedArmy = units.FirstOrDefault(a => a.Id == army);
            if (selectedArmy == null && units.Length > 0) { selectedArmy = units[0]; army = selectedArmy.Id; }
            PageTitle(g, "Подготовка и боеготовность", "Обучайте действующие соединения, сохраняйте оперативный резерв и восстанавливайте уставшие части.");
            float cardW = (w - 24) / 3;
            string[] labels = { "ГОТОВНОСТЬ СОЕДИНЕНИЙ", "ОПЕРАТИВНЫЙ ЗАПАС", "ВОЕННАЯ УСТАЛОСТЬ" };
            string[] values = { report.Readiness.ToString("0") + "% · " + report.Ready + "/" + report.Formations + " готовы", "Топливо " + report.FuelDays.ToString("0.0") + " д. · вооружение " + report.ArmsDays.ToString("0.0") + " д.", n.Condition.Exhaustion.ToString("0.0") + " / 100" };
            string[] tips = {
                "Готовность учитывает технику, организацию, укомплектованность, снабжение и усталость. Готовая часть: от 65%, вне учений и начального обучения.",
                "Оценка запаса для операций всех наземных частей: расход топлива на марше с поддержкой, технологиями и доктринами; вооружение — 0,01 ед. на батальон в день. Не прогнозирует будущие потери.",
                "Война и реальные потери увеличивают усталость; мир постепенно снижает её. При 100: выпуск −10%, приток резерва −50%, наземная боевая сила −20%. Старые потери при загрузке не начисляются заново."
            };
            for (int k = 0; k < 3; k++) {
                float xx = x + k * (cardW + 12); Fill(g, Palette.Card, xx, 222, cardW, 72);
                Txt(g, labels[k], xx + 12, 234, 10, Palette.Gold, true, cardW - 24);
                Txt(g, values[k], xx + 12, 259, 17, k == 2 && n.Condition.Exhaustion >= 50 ? Palette.Red : Palette.Text, true, cardW - 24);
                hits.Add(new Hit { Rect = new RectangleF(xx,222,cardW,72), Label = "readiness:summary:"+k, Tip = tips[k] });
            }
            Txt(g, report.Posture + " · " + report.Reason, x, 310, 12, Palette.Muted, false, w);
            Section(g, "СОЕДИНЕНИЕ / СОСТОЯНИЕ", x, 348, leftW);
            readinessOffset = Math.Max(0, Math.Min(readinessOffset, Math.Max(0, units.Length - 6)));
            for (int i = readinessOffset; i < Math.Min(units.Length, readinessOffset + 6); i++) {
                var a = units[i]; float y = 385 + (i - readinessOffset) * 62;
                Fill(g, a.Id == army ? Palette.Selected : Palette.Card, x, y - 4, leftW, 56);
                Txt(g, "№" + a.Id + " · " + W.Regions[a.Region].Name, x + 10, y + 3, 14, Palette.Text, true, leftW - 130);
                Txt(g, ReadinessRules.Score(a).ToString("0") + "%", x + leftW - 100, y + 3, 16, ReadinessRules.Score(a) < 50 ? Palette.Red : Palette.Teal, true, 90);
                Txt(g, "Опыт " + a.Experience.ToString("0.0") + " · усталость " + a.Fatigue.ToString("0") + "% · " + (a.ExerciseRemaining > 0 ? "учения " + a.ExerciseRemaining + " д." : a.TrainingDays > 0 ? "начальная подготовка" : a.Target >= 0 ? "марш" : "отдых / служба"), x + 10, y + 29, 11, Palette.Muted, false, leftW - 20);
                hits.Add(new Hit { Rect = new RectangleF(x, y - 4, leftW, 56), Label = "readiness:unit:" + a.Id, Action = () => { army = a.Id; Invalidate(); }, Tip = "Выбрать соединение для учений и просмотра условий" });
            }
            ReadinessButton(g, "readiness:previous", "←", x, 772, 45, () => { readinessOffset = Math.Max(0, readinessOffset - 6); Invalidate(); });
            ReadinessButton(g, "readiness:next", "→", x + 54, 772, 45, () => { readinessOffset = Math.Min(Math.Max(0, units.Length - 6), readinessOffset + 6); Invalidate(); });
            Txt(g, units.Length == 0 ? "Нет соединений" : (readinessOffset + 1) + "–" + Math.Min(units.Length, readinessOffset + 6) + " / " + units.Length, x + 113, 780, 12, Palette.Muted);
            ReadinessButton(g, "readiness:back", "← Командование", x, 824, 195, () => Navigate("Армия"));
            ReadinessButton(g, "readiness:trade", "Снабжение и торговля →", x + 208, 824, 225, OpenTrade);
            Section(g, "ПОДГОТОВКА ЧАСТИ", rx, 348, rightW);
            if (selectedArmy == null) { Para(g, "Сначала наберите соединение из имеющихся людей и техники.", rx, 396, rightW, 100, 16); return; }
            var unit = selectedArmy;
            Book(g, "Соединение №" + unit.Id, rx, 385, 22, Palette.Text, true, rightW);
            Txt(g, People(unit.Personnel) + " / " + People(ManpowerRules.Establishment(unit)) + " чел. · " + unit.Battalions + " бат.", rx, 422, 13, Palette.Muted, false, rightW);
            Txt(g, "Техника " + unit.Strength.ToString("0") + "% · организация " + unit.Organization.ToString("0") + "%", rx, 449, 13, Palette.Text, false, rightW);
            Txt(g, "Снабжение " + unit.Supply.ToString("0") + "% · опыт " + unit.Experience.ToString("0.0"), rx, 476, 13, Palette.Teal, false, rightW);
            Txt(g, "Усталость " + unit.Fatigue.ToString("0.0") + "% · сила боя ×" + ReadinessRules.FatigueFactor(unit).ToString("0.00"), rx, 503, 13, Palette.Gold, false, rightW);
            Section(g, "КУРС УЧЕНИЙ · ДО 21 ДНЯ", rx, 546, rightW);
            Txt(g, "Доп. расходы в день: " + (E.ExerciseDailyCost(unit) * 1000).ToString("0.00") + " млн", rx, 582, 14, Palette.Text, true, rightW);
            Txt(g, "Топливо " + ReadinessRules.ExerciseFuel(unit).ToString("0.000") + " · вооружение " + ReadinessRules.ExerciseArms(unit).ToString("0.000") + " ед.", rx, 609, 12, Palette.Muted, false, rightW);
            string reason = unit.ExerciseRemaining > 0 ? null : E.ExerciseReason(unit.Id);
            string status = unit.ExerciseRemaining > 0 ? E.ExerciseStatus(unit) : reason ?? "Курс доступен. Опыт до 35; дальнейший рост — в бою. После учений потребуется отдых.";
            Para(g, status, rx, 644, rightW, 78, 13, reason == null ? Palette.Teal : Palette.Gold);
            ReadinessButton(g, "readiness:exercise", unit.ExerciseRemaining > 0 ? "Прекратить учения" : "Начать учения", rx, 733, rightW, () => Message(unit.ExerciseRemaining > 0 ? E.StopExercise(unit.Id) : E.StartExercise(unit.Id)), reason);
            Para(g, "Учения снимают часть с фронта. Оплата по факту дня, без возврата. Приказ на марш прекращает курс; опыт и усталость сохраняются.", rx, 782, rightW, 74, 11, Palette.Muted);
        }
    }
}
