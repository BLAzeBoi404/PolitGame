using System;
using System.Drawing;
using System.Linq;
namespace Sovereign
{
    public partial class GameForm
    {
        private bool deploymentPage;
        private BattleDeployment deploymentDraft = new BattleDeployment { Front = 2, Left = 1, Right = 1 };
        private void OpenDeployment() { Navigate("Армия"); deploymentPage = true; detailPanel = true; Invalidate(); }
        private void DrawRankStars(Graphics g, float x, float y, int count, Color ink)
        {
            for (int star = 0; star < 5; star++) {
                var points = new PointF[10];
                for (int i = 0; i < 10; i++) { double angle = -Math.PI / 2 + i * Math.PI / 5; float r = i % 2 == 0 ? 4 : 1.8f; points[i] = new PointF(x + star * 12 + (float)Math.Cos(angle) * r, y + (float)Math.Sin(angle) * r); }
                if (star < count) using (var brush = new SolidBrush(ink)) g.FillPolygon(brush, points);
                else using (var pen = new Pen(Color.FromArgb(80, ink), .7f)) g.DrawPolygon(pen, points);
            }
        }
        private void DrawDeployment(Graphics g)
        {
            float x = sidebar + 34, w = VW - sidebar - 68, left = w * .58f, right = x + left + 30, rw = w - left - 30;
            PageTitle(g, "Боевой порядок", "Штат определяет распределение батальонов. Изменения существующей части требуют переобучения.");
            Button(g, "← Командование", x, 217, 210, () => { deploymentPage = false; Invalidate(); });
            Section(g, "НОВЫЙ ШТАТ / ДО 6 БАТАЛЬОНОВ", x, 279, left);
            Button(g, templateDraftName + "…", x, 318, left, RenameTemplate, false, "Название нового штата");
            string[] positions = { "Передовая", "Левый фланг", "Правый фланг", "Резерв" };
            int[] counts = { deploymentDraft.Front, deploymentDraft.Left, deploymentDraft.Right, deploymentDraft.Reserve };
            for (int i = 0; i < 4; i++) {
                int role = i; float xx = x + (i == 2 ? left * .52f : 0), yy = i == 0 ? 380 : i == 3 ? 552 : 466;
                float width = i == 1 || i == 2 ? left * .48f : left;
                Fill(g, Palette.Card, xx, yy, width, 72);
                Txt(g, positions[i], xx + 12, yy + 10, 12, Palette.Gold, true);
                Txt(g, counts[i] + " бат.", xx + 12, yy + 39, 15, Palette.Text, true);
                AvailableButton(g, "−", xx + width - 76, yy + 31, 28, () => ChangeDeployment(role, -1), counts[i] > (i == 0 ? 1 : 0), "Убрать батальон", false, 28); hits[hits.Count - 1].Label = "deployment:less:" + i;
                AvailableButton(g, "+", xx + width - 37, yy + 31, 28, () => ChangeDeployment(role, 1), counts.Sum() < 6, "Добавить батальон", false, 28); hits[hits.Count - 1].Label = "deployment:more:" + i;
            }
            Txt(g, "ПОДДЕРЖКА / до 3 приданных групп", x, 646, 11, Palette.Gold, true);
            Para(g, "Грузовики, артиллерия, разведка и авиация назначаются готовой части из каталога через «+ Поддержка». Они требуют отдельной техники, людей и содержания.", x, 672, left, 65, 13);
            int total = counts.Sum();
            Button(g, "Создать боевой штат · " + total + " бат.", x, 770, left, () => { string error = E.CreateTemplate(templateDraftName, total, deploymentDraft); Message(error); if (error == null) templateDraftName = "Штат " + E.Player.NextTemplate; }, true);
            Section(g, "СПЕЦИАЛИЗАЦИЯ", right, 279, rw);
            for (int i = 0; i < DeploymentRules.Names.Length; i++) { int type = i; Button(g, DeploymentRules.Names[i], right, 318 + i * 42, rw, () => { deploymentDraft.Specialty = (FormationSpecialty)type; Invalidate(); }, (int)deploymentDraft.Specialty == i, DeploymentRules.Descriptions[i], 32); }
            Para(g, DeploymentRules.Descriptions[(int)deploymentDraft.Specialty], right, 588, rw, 58, 14, Palette.Gold);
            Para(g, "Фланги обходят неприкрытый край противника. Резерв прикрывает обходы, но слабее в наступлении. Передовая удерживает центр. Звёзды зависят от опыта, людей, организации, техники и снабжения.", right, 657, rw, 107, 13);
            Button(g, "Набор и переобучение →", right, 786, rw, OpenManpower);
        }
        private void ChangeDeployment(int role, int delta)
        {
            int[] values = {deploymentDraft.Front, deploymentDraft.Left, deploymentDraft.Right, deploymentDraft.Reserve};
            if (values.Sum() + delta > 6 || values[role] + delta < (role == 0 ? 1 : 0)) return;
            values[role] += delta; deploymentDraft.Front = values[0]; deploymentDraft.Left = values[1]; deploymentDraft.Right = values[2]; deploymentDraft.Reserve = values[3]; Invalidate();
        }
    }
}
