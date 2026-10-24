using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool manpowerPage;
        private int templateDraftBattalions = 2;
        private string templateDraftName = "Новый штат";
        private static string People(long value) => value.ToString("N0", new CultureInfo("ru-RU"));
        private string RecruitButtonText() => "Набрать часть · " + (.3 * E.SelectedTemplate(E.S.Player).Battalions).ToString("0.00", new CultureInfo("ru-RU")) + " млрд";
        private void OpenManpower() { Navigate("Армия"); manpowerPage = true; detailPanel = false; Invalidate(); }
        private void CycleTemplate(int step)
        {
            var list = E.Player.Templates; int index = list.FindIndex(t => t.Id == E.Player.ActiveTemplate);
            Message(E.SelectTemplate(list[(index + step + list.Count) % list.Count].Id));
        }
        private void RenameTemplate()
        {
            int previous = speed; speed = 0;
            try {
                using (var dialog = new Form { Text = "Название штата", Size = new Size(440, 170), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false, MinimizeBox = false, BackColor = Palette.Panel, ForeColor = Palette.Text }) {
                    var input = new TextBox { Text = templateDraftName, MaxLength = 32, Left = 20, Top = 20, Width = 380 };
                    var ok = new System.Windows.Forms.Button { Text = "Применить", Left = 190, Top = 66, Width = 105, DialogResult = DialogResult.OK };
                    var cancel = new System.Windows.Forms.Button { Text = "Отмена", Left = 305, Top = 66, Width = 95, DialogResult = DialogResult.Cancel };
                    dialog.Controls.AddRange(new Control[] { input, ok, cancel }); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) == DialogResult.OK) templateDraftName = input.Text.Trim();
                }
            } finally { speed = previous; }
            Invalidate();
        }
        private void DrawManpowerPanel(Graphics g)
        {
            var n = E.Player; float w = DocketWidth - 40;
            Txt(g, "ДОСТУПНО В РЕЗЕРВЕ", 20, 157, 10, Palette.Gold, true);
            Book(g, People(n.Recruits), 18, 177, 30, Palette.Text, true);
            Txt(g, "+" + People(E.WeeklyIntake(E.S.Player)) + " в неделю · предел " + People(E.ManpowerCapacity(E.S.Player)), 20, 222, 11, Palette.Muted, false, w);
            LedgerRow(g, "На службе", People(E.ServicePersonnel(E.S.Player)), 252, Palette.Teal);
            LedgerRow(g, "Безвозвратные потери", People(n.PersonnelLosses), 281, Palette.Red);
            CompactSection(g, "КОМПЛЕКТОВАНИЕ", 324);
            for (int i = 0; i < 4; i++) {
                var law = (ConscriptionLaw)i; string reason = E.ConscriptionReason(law);
                string label = ManpowerRules.LawNames[i] + "  /  " + (ManpowerRules.PopulationShare[i] * 100).ToString("0.#") + "% · выпуск −" + (ManpowerRules.OutputPenalty[i] * 100).ToString("0") + "%";
                string detail = "Доля населения своих домашних областей. Выпуск −" + (ManpowerRules.OutputPenalty[i] * 100).ToString("0") + "%. Повышение: 0,15 млрд и −3 одобрения за ступень. Смена раз в 90 дней.";
                if (n.Conscription == law) Button(g, label, 20, 365 + i * 34, w, () => {}, true, detail, 29);
                else AvailableButton(g, label, 20, 365 + i * 34, w, () => Message(E.SetConscription(law)), reason == null, reason ?? detail, false, 29);
                hits[hits.Count - 1].Label = "conscription:" + i;
            }
            CompactSection(g, "ШТАТ НОВОГО НАБОРА", 518);
            var template = E.SelectedTemplate(E.S.Player); var design = E.FindDesign(E.S.Player, n.ActiveDesign);
            Txt(g, template.Name, 20, 560, 17, Palette.Text, true, 265);
            Button(g, "‹", 290, 552, 35, () => CycleTemplate(-1), false, "Предыдущий штат", 30); hits[hits.Count - 1].Label = "template:previous";
            Button(g, "›", 338, 552, 35, () => CycleTemplate(1), false, "Следующий штат", 30); hits[hits.Count - 1].Label = "template:next";
            Txt(g, template.Battalions + " бат. · " + template.Battalions + " компл. · " + ManpowerRules.TrainingDays(template.Battalions) + " дней", 20, 593, 12, Palette.Gold);
            Txt(g, design.Blueprint.Formation ? People(ManpowerRules.GroundSize(design.Blueprint.Kind, template.Battalions)) + " чел. · " + design.Blueprint.Name : "Для набора выберите наземный проект в каталоге", 20, 617, 11, Palette.Muted, false, w);
            Line(g, Palette.Line, 20, 646, 376, 646);
            Button(g, templateDraftName + "…", 20, 664, 234, RenameTemplate, false, "Название нового штата; существующие части сохраняют свой штат", 30);
            Button(g, "−", 270, 664, 30, () => { templateDraftBattalions = Math.Max(1, templateDraftBattalions - 1); Invalidate(); }, false, "Меньше батальонов", 30); hits[hits.Count - 1].Label = "template:less";
            Txt(g, templateDraftBattalions.ToString(), 314, 671, 15, Palette.Gold, true);
            Button(g, "+", 345, 664, 30, () => { templateDraftBattalions = Math.Min(6, templateDraftBattalions + 1); Invalidate(); }, false, "Больше батальонов", 30); hits[hits.Count - 1].Label = "template:more";
            Para(g, "Одна основная модель на штат. Каждый батальон требует комплект, людей, топливо и содержание. Создание нового штата не меняет существующие части.", 20, 710, w, 63, 12);
            Button(g, "Сохранить штат", 20, 779, 168, () => { string result = E.CreateTemplate(templateDraftName, templateDraftBattalions); Message(result); if (result == null) templateDraftName = "Штат " + E.Player.NextTemplate; }, true, "Создать и выбрать штат для будущего набора", 29);
            var selectedArmy = E.S.Armies.FirstOrDefault(a => a.Id == army && a.Owner == E.S.Player);
            string reorganization = E.ReorganizationReason(army, template.Id);
            AvailableButton(g, selectedArmy == null ? "Выбрать часть…" : "Применить к №" + army, 198, 779, 178, () => {
                if (Confirm("Перестроить часть №" + army + " на штат «" + template.Name + "»? " + template.Battalions + " батальонов. Стоимость " + Money(E.ReorganizationCost(selectedArmy, template)) + "; дополнительная техника и люди берутся со склада и из резерва. На время подготовки часть снимается с фронта.")) Message(E.ReorganizeArmy(army, template.Id));
            }, reorganization == null, reorganization ?? "Применить выбранный штат к части, выделенной на карте; начнётся переобучение.", false, 29);
        }
    }
}
