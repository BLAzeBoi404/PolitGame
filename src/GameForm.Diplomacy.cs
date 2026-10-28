using System;
using System.Drawing;
using System.Linq;

namespace Sovereign
{
    public partial class GameForm
    {
        private void DrawDiplomacy(Graphics g)
        {
            if (foreign < 0 || foreign == E.S.Player) foreign = Enumerable.Range(0, W.Countries.Length).Where(c => c != E.S.Player).OrderByDescending(c => E.S.Nations[c].GDP).First();
            int other = foreign, pair = E.Pair(E.S.Player, other); float x = sidebar + 34, w = VW - sidebar - 68, left = 300, right = x + left + 30, rw = w - left - 30;
            PageTitle(g, "Дипломатическое досье", "Отношения, обязательства и условия мира. Каждый договор меняет доступные действия.");
            Fill(g, Palette.Panel, x, 222, left, 344);
            CountrySilhouette(g, other, new RectangleF(x + 20, 239, left - 40, 152), Color.FromArgb(110, 140, 148));
            Txt(g, W.Countries[other].Code, x + 20, 239, 11, Palette.Gold, true);
            Txt(g, W.Countries[other].Name, x + 18, 407, 24, Palette.Text, true, left - 36);
            Txt(g, E.AtWar(E.S.Player, other) ? "ВОЙНА" : E.Allied(E.S.Player, other) ? "ОБОРОННЫЙ СОЮЗ" : "МИРНЫЕ ОТНОШЕНИЯ", x + 20, 445, 11, E.AtWar(E.S.Player, other) ? Palette.Red : Palette.Teal, true);
            Button(g, "Выбрать государство…", x + 18, 508, left - 36, ChooseForeign);
            Section(g, "БАЛАНС ОТНОШЕНИЙ", x, 592, left);
            Txt(g, E.S.Relations[pair].ToString("+0;-0;0"), x, 628, 36, E.S.Relations[pair] >= 0 ? Palette.Teal : Palette.Red, true);
            Progress(g, x + 88, 650, left - 88, (E.S.Relations[pair] + 100) / 200.0, E.S.Relations[pair] >= 0 ? Palette.Teal : Palette.Red);
            Txt(g, "Экономика · " + Money(E.S.Nations[other].GDP), x, 687, 12, Palette.Muted);
            if (E.AtWar(E.S.Player, other)) {
                Txt(g, "Война: " + (E.S.Day - E.S.WarStarted[pair]) + " дн.", x, 715, 13, Palette.Red);
                Txt(g, "Оккупация: мы " + (E.Occupation(E.S.Player, other) * 100).ToString("0") + "% / они " + (E.Occupation(other, E.S.Player) * 100).ToString("0") + "%", x, 743, 12, Palette.Muted);
                Button(g, "Назначить фронт →", x, 779, left, () => Navigate("Армия"), true);
            } else if (E.S.TruceUntil[pair] > E.S.Day) Txt(g, "Перемирие: ещё " + (E.S.TruceUntil[pair] - E.S.Day) + " дн.", x, 729, 13, Palette.Gold);
            if(!E.AtWar(E.S.Player,other))TradeLink(g,x,779,left,other);
            Section(g, "ПЕРЕГОВОРЫ И ДОГОВОРЫ", right, 223, rw);
            string[] title = { "Дипломатическая миссия", "Торговое соглашение", "Торговое эмбарго", "Оборонный союз", "Военный доступ к партнёру", "Доступ армии партнёра", "Объявление войны", "Мир по линии контроля", "Мир с возвратом территорий" };
            string[] detail = { "0,10 млрд · отношения +15", "Двусторонний договор · дешевле перевозка товаров", "Прекращение торговли по вашей инициативе", "Взаимный доступ · помощь при нападении", "Проход и сухопутное снабжение ваших частей", "Пропустить и снабжать иностранные части", "В конфликт вступят оборонные союзники противника", "Сохранить занятую территорию · перемирие 180 дней", "Вернуть домашние области друг другу · 180 дней" };
            int[] actions = { 0, 1, 2, 5, 6, 7, 3, 4, 8 };
            string[] labels = { "Отправить", E.S.Agreements[pair] ? "Расторгнуть" : "Заключить", E.S.Embargo[pair] ? "Снять" : "Ввести", E.Allied(E.S.Player, other) ? "Расторгнуть союз" : "Предложить союз", E.S.Access[pair] ? "Отказаться" : "Запросить", E.S.Access[E.Pair(other, E.S.Player)] ? "Отозвать" : "Предоставить", "Объявить", "Предложить мир", "Вернуть границы" };
            for (int i = 0; i < actions.Length; i++) {
                int action = actions[i]; float y = 262 + i * 60; string reason = E.DiplomacyReason(other, action);
                if (i % 2 == 0) Fill(g, Palette.Card, right - 10, y - 4, rw + 10, 57);
                Txt(g, title[i], right + 3, y + 3, 14, i == 6 ? Palette.Red : Palette.Text, true, rw - 184);
                Txt(g, detail[i], right + 3, y + 26, 11, Palette.Muted, false, rw - 184);
                AvailableButton(g, labels[i], right + rw - 168, y + 7, 168, () => {
                    if ((action == 3 || action == 4 || action == 8 || (action == 5 && E.Allied(E.S.Player, other))) && !Confirm(title[Array.IndexOf(actions, action)] + ": " + W.Countries[other].Name + "?")) return;
                    Message(E.Diplomacy(other, action));
                }, reason == null, reason ?? detail[i], false, 31);
            }
        }
    }
}
