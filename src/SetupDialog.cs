using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
	public class SetupDialog : Form
	{
		private ComboBox countries = new ComboBox();

		private ComboBox level = new ComboBox();

		private NumericUpDown year = new NumericUpDown();

		private CheckBox experimental = new CheckBox();

		private List<int> indices;

		public int Country => indices[countries.SelectedIndex];

		public int Year => (int)year.Value;

		public int Difficulty => level.SelectedIndex;

		public SetupDialog(World w, bool campaign, int selected)
		{
			SetupDialog setupDialog = this;
			Text = (campaign ? "Новая кампания" : "Выбор государства");
			base.ClientSize = new Size(630, campaign ? 508 : 210);
			base.StartPosition = FormStartPosition.CenterParent;
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			BackColor = Palette.Bg;
			ForeColor = Palette.Text;
			Font = new Font("Segoe UI", 11f);
			base.AutoScaleMode = AutoScaleMode.None;
			Label value = new Label
			{
				Text = (campaign ? "Государство начинается с решения" : "Выберите государство"),
				Left = 28,
				Top = 24,
				Width = 570,
				Height = 45,
				Font = new Font("Segoe UI", 19f, FontStyle.Bold),
				ForeColor = Palette.Gold
			};
			base.Controls.Add(value);
			indices = (from i in Enumerable.Range(0, w.Countries.Length)
				orderby w.Countries[i].Name
				select i).ToList();
			countries.SetBounds(30, 91, 568, 34);
			countries.DropDownStyle = ComboBoxStyle.DropDownList;
			foreach (int index in indices)
			{
				countries.Items.Add(w.Countries[index].Name);
			}
			countries.SelectedIndex = indices.IndexOf(selected);
			base.Controls.Add(countries);
			if (campaign)
			{
				base.Controls.Add(new Label
				{
					Text = "Начальный год",
					Left = 30,
					Top = 151,
					Width = 250
				});
				year.SetBounds(30, 180, 180, 34);
				year.Minimum = 1940m;
				year.Maximum = 2026m;
				year.Value = 2026m;
				base.Controls.Add(year);
				base.Controls.Add(new Label
				{
					Text = "Поведение ИИ",
					Left = 245,
					Top = 151,
					Width = 260
				});
				level.SetBounds(245, 180, 353, 34);
				level.DropDownStyle = ComboBoxStyle.DropDownList;
				level.Items.AddRange(new object[3] { "Спокойное", "Стратегическое", "Конкурентное" });
				level.SelectedIndex = 1;
				base.Controls.Add(level);
				experimental.SetBounds(30, 236, 568, 30);
				experimental.Text = "Разрешить экспериментальную настройку эпохи";
				base.Controls.Add(experimental);
				year.Enabled = false;
				experimental.CheckedChanged += delegate
				{
					year.Enabled = experimental.Checked;
					if (!experimental.Checked)
					{
						year.Value = 2026m;
					}
				};
				base.Controls.Add(new Label
				{
					Text = "Сборка 0.1: современная карта Natural Earth. Год меняет дату, технологический уровень и масштаб экономики. Исторические границы 1940–2025 ещё не реализованы. Показатели регионов и ресурсов — оценки модели, не историческая статистика.",
					Left = 30,
					Top = 287,
					Width = 568,
					Height = 123,
					ForeColor = Palette.Muted
				});
			}
			Button button = new Button
			{
				Text = (campaign ? "Начать кампанию" : "Выбрать"),
				Left = 30,
				Top = base.ClientSize.Height - 68,
				Width = 350,
				Height = 40,
				BackColor = Palette.Gold,
				ForeColor = Palette.Bg,
				FlatStyle = FlatStyle.Flat,
				DialogResult = DialogResult.OK
			};
			Button button2 = new Button
			{
				Text = "Назад",
				Left = 399,
				Top = base.ClientSize.Height - 68,
				Width = 199,
				Height = 40,
				BackColor = Palette.Card,
				ForeColor = Palette.Text,
				FlatStyle = FlatStyle.Flat,
				DialogResult = DialogResult.Cancel
			};
			base.Controls.Add(button);
			base.Controls.Add(button2);
			base.AcceptButton = button;
			base.CancelButton = button2;
		}
	}
}
