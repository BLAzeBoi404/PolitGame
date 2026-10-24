using System;
using System.Drawing;

namespace Sovereign
{
    public partial class GameForm
    {
        private void DrawResearch(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, left = w - 330, right = x + left + 30;
            PageTitle(g, "Технологические программы", "Уровни отраслей открывают модули и повышают эффективность действующей экономики.");
            Section(g, "ПРОГРЕСС ОТРАСЛЕЙ", x, 226, left);
            string[] effects = { "Урожайность сельского хозяйства", "Выработка электроэнергии", "Двигатели · корпус · ремонт", "Ходовая часть · баки · строительство", "Население и качество жизни", "Броня · вооружение · электроника" };
            var n = E.Player;
            for (int i = 0; i < 6; i++) {
                int tech = i; float y = 271 + i * 83; bool current = n.Research == i;
                Fill(g, current ? Palette.Selected : Palette.Card, x, y, left, 72);
                Txt(g, (i + 1).ToString("00"), x + 12, y + 13, 12, Palette.Gold, true);
                Txt(g, Rules.Techs[i], x + 46, y + 10, 16, Palette.Text, true, left * 0.43f - 46);
                Txt(g, effects[i], x + 46, y + 38, 11, Palette.Muted, false, left * 0.43f - 46);
                float pathX = x + left * 0.46f, step = (left * 0.54f - 128) / 4;
                int start = Math.Max(0, n.Tech[i] - 1);
                for (int j = 0; j < 4; j++) {
                    int level = start + j; float bx = pathX + j * step;
                    if (j < 3) Line(g, Palette.Line, bx + 27, y + 32, bx + step, y + 32);
                    Fill(g, level <= n.Tech[i] ? Palette.Hover : Palette.Panel, bx, y + 18, 27, 28);
                    Txt(g, level.ToString(), bx + 7, y + 25, 11, level <= n.Tech[i] ? Palette.Teal : Palette.Muted, true);
                    if (level == n.Tech[i] + 1 && current) Progress(g, bx, y + 49, 27, n.ResearchProgress / 100, Palette.Gold);
                }
                AvailableButton(g, current ? "В работе" : "Начать", x + left - 100, y + 20, 88, () => Message(E.StartResearch(tech)), n.Research < 0,
                    current ? "Исследуется следующий уровень." : n.Research >= 0 ? "Завершите текущую программу." : "Запуск программы. Прогресс зависит от научного бюджета и образования.", current, 30);
            }
            Fill(g, Palette.Panel, right, 226, 300, 545);
            Txt(g, "ТЕКУЩАЯ ПРОГРАММА", right + 20, 246, 10, Palette.Gold, true);
            NavIcon(g, 3, right + 21, 294, 35, Palette.Teal);
            Txt(g, n.Research < 0 ? "Выберите программу" : Rules.Techs[n.Research], right + 20, 355, 21, Palette.Text, true, 260);
            Txt(g, n.Research < 0 ? "—" : n.ResearchProgress.ToString("0") + "%", right + 16, 394, 56, Palette.Gold, true);
            Progress(g, right + 20, 470, 260, n.Research < 0 ? 0 : n.ResearchProgress / 100, Palette.Gold);
            Para(g, n.Research < 0 ? "Научный бюджет превращается в прогресс после выбора направления. Одна программа за раз." : "После завершения отрасль получает следующий уровень. Улучшения экономики вступают в силу автоматически; новые модули доступны в конструкторе.", right + 20, 495, 260, 112, 13);
            Line(g, Palette.Line, right + 20, 624, right + 280, 624);
            Txt(g, "Наука: " + Num(n.Science) + "% ВВП", right + 20, 644, 15, Palette.Text, true);
            Txt(g, "Образование: " + Num(n.Education) + " / 100", right + 20, 677, 12, Palette.Muted);
            Button(g, "Финансирование →", right + 20, 718, 260, () => Navigate("Политика"));
            Button(g, "К доступным модулям →", right, 790, 300, () => Navigate("Конструктор"), true);
        }
    }
}
