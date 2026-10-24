using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
    public partial class GameForm
    {
        private bool equipmentProduction;
        private int catalogOffset, lineOffset;
        private int orderFactories = 1, orderQuantity = 3;

        private bool Confirm(string text)
        {
            int previousSpeed = speed;
            speed = 0;
            try { return MessageBox.Show(this, text, "Подтверждение", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK; }
            finally { speed = previousSpeed; }
        }
        private void RenameDraft()
        {
            using (var dialog = new Form { Text = "Название проекта", Size = new Size(440, 170), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Palette.Panel, ForeColor = Palette.Text }) {
                var input = new TextBox { Text = draft.Name, MaxLength = 40, Left = 20, Top = 20, Width = 380 };
                var ok = new System.Windows.Forms.Button { Text = "Применить", Left = 190, Top = 66, Width = 105, DialogResult = DialogResult.OK };
                var cancel = new System.Windows.Forms.Button { Text = "Отмена", Left = 305, Top = 66, Width = 95, DialogResult = DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[] { input, ok, cancel }); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK) draft.Name = input.Text.Trim();
            }
            Invalidate();
        }
        private void DrawProduction(Graphics g)
        {
            if (equipmentProduction) DrawEquipmentProduction(g); else DrawConstruction(g);
            float x = sidebar + 34;
            Button(g, "Предприятия", x, 169, 138, () => { equipmentProduction = false; Invalidate(); }, !equipmentProduction, "Строительство и промышленная база", 28);
            Button(g, "Серии и склад", x + 148, 169, 162, () => { equipmentProduction = true; Invalidate(); }, equipmentProduction, "Заказы на конкретные модели техники", 28);
        }
    }
}
