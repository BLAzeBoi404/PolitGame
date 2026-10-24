using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
	public partial class GameForm : Form
	{
		public Engine E;

		private World W;

		private Timer clock = new Timer();

		private List<Hit> hits = new List<Hit>();

		private Dictionary<string, Font> fonts = new Dictionary<string, Font>();

		private string page = "Карта";

		private string layer = "Политика";

		private string toast = "";

		private DateTime toastUntil;

		private int selected = -1;

		private int foreign = -1;

		private int army = -1;

		private int speed;

		private int scroll;

		private int marketGood;

		private float scale = 13f;

		private float cx = 25f;

		private float cy = -49f;

		private RectangleF mapRect;

		private Point mouse;

		private Point last;
        private Point panOrigin;

		private bool panning;

		private bool moved;

		private const int sidebar = 0;
        private float workspaceWidth;
        private bool detailPanel;
        private const float DocketWidth = 396;
        private const float OutlinerWidth = 262;

		private Blueprint draft = new Blueprint();

		private bool showHelp;

		private bool campaignStarted;

		private Preferences prefs = Preferences.Read();

		private string menuScreen = "main";

		private string menuBack = "main";

		private string countryQuery = "";

		private int menuCountry;

		private int menuScroll;

		private int newYear = 2026;

		private int newDifficulty = 1;

		private bool experimentalYear;

		private TextBox countrySearch;

		private NumericUpDown startYear;

		private List<int> countryList;


		private Bitmap terrainTexture;

		private bool fullScreen;

		private Rectangle windowBounds;

		private FormBorderStyle previousBorder;

		private DateTime hoverSince = DateTime.Now;

		private string hoveredTip = "";

		private float UiScale => Math.Min(1f, Math.Min((float)base.ClientSize.Height / 900f, (float)base.ClientSize.Width / 1440f));

		private float VW => workspaceWidth > 0 ? workspaceWidth : (float)base.ClientSize.Width / UiScale;

		private float VH => (float)base.ClientSize.Height / UiScale;

		private Point Logical(Point p)
		{
			return new Point((int)((float)p.X / UiScale), (int)((float)p.Y / UiScale));
		}

		public GameForm(World world)
		{
			W = world;
            InitializeAtlasColors();
			E = new Engine(world);
			base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			Text = "SOVEREIGN — Государство · 0.6";
			BackColor = Palette.Bg;
			ForeColor = Palette.Text;
			base.AutoScaleMode = AutoScaleMode.None;
			base.ClientSize = new Size(1540, 900);
			MinimumSize = new Size(1000, 640);
			base.StartPosition = FormStartPosition.CenterScreen;
			base.KeyPreview = true;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			InitializePresentation();
            cameraRefresh.Interval = 100;
            cameraRefresh.Tick += delegate { cameraRefresh.Stop(); deferMapRefresh = false; Invalidate(); };
			Engine e = E;
			Action value = delegate
			{
				Invalidate();
			};
			e.Changed += value;
			clock.Interval = 650;
			clock.Tick += delegate
			{
				if (prefs.Tooltips && hoveredTip != "")
				{
					Invalidate();
				}
				if (speed > 0 && menuScreen == "" && E.S != null)
				{
					int num = ((speed == 1) ? 1 : ((speed == 2) ? 3 : 7));
					for (int i = 0; i < num; i++)
					{
						E.Tick();
					}
				}
			};
			clock.Start();
			base.KeyDown += KeysDown;
			base.FormClosing += delegate
			{
				if (campaignStarted && prefs.Autosave)
				{
					E.Autosave();
				}
				prefs.Save();
			};
		}

		private Font F(float size, bool bold = false)
		{
			string key = size + "/" + bold;
			if (!fonts.ContainsKey(key))
			{
				fonts[key] = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
			}
			return fonts[key];
		}

		private void Txt(Graphics g, string s, float x, float y, float size, Color color, bool bold = false, float width = 0f)
		{
			using (SolidBrush brush = new SolidBrush(color))
			{
				if (width > 0f)
				{
					StringFormat stringFormat = new StringFormat();
					stringFormat.Trimming = StringTrimming.EllipsisCharacter;
					stringFormat.FormatFlags = StringFormatFlags.NoWrap;
					using (StringFormat format = stringFormat)
					{
						g.DrawString(s, F(size, bold), brush, new RectangleF(x, y, width, size + 10f), format);
						return;
					}
				}
				g.DrawString(s, F(size, bold), brush, x, y);
			}
		}

		private void Para(Graphics g, string s, float x, float y, float width, float height, float size = 14f, Color? color = null)
		{
			using (SolidBrush brush = new SolidBrush(color ?? Palette.Muted))
			{
				g.DrawString(s, F(size), brush, new RectangleF(x, y, width, height));
			}
		}

		private void Fill(Graphics g, Color color, float x, float y, float w, float h)
		{
			using (SolidBrush brush = new SolidBrush(color))
			{
				g.FillRectangle(brush, x, y, w, h);
			}
		}

		private void Line(Graphics g, Color color, float x, float y, float x2, float y2, float thick = 1f)
		{
			using (Pen pen = new Pen(color, thick))
			{
				g.DrawLine(pen, x, y, x2, y2);
			}
		}

        private void Card(Graphics g, float x, float y, float w, float h)
        {
            Fill(g, Palette.Card, x, y, w, h);
            Line(g, Palette.Line, x, y + h, x + w, y + h);
        }

        private void Button(Graphics g, string text, float x, float y, float w, Action action, bool active = false, string tooltip = null, float h = 34f)
        {
            var rect = new RectangleF(x, y, w, h);
            bool hover = rect.Contains(mouse);
            Fill(g, active ? Palette.Selected : hover ? Palette.Hover : Palette.Card, x, y, w, h);
            Line(g, active ? Palette.Gold : hover ? Palette.Muted : Palette.Line, x, y + h, x + w, y + h);
            if (active) Fill(g, Palette.Gold, x, y + 6, 2, h - 12);
            Txt(g, text, x + 10, y + (h - 18) / 2, 13, active ? Palette.Gold : Palette.Text, active, w - 18);
            hits.Add(new Hit { Rect = rect, Label = text, Action = action, Tip = tooltip ?? text });
        }

		private void Progress(Graphics g, float x, float y, float w, double fraction, Color color)
		{
			Fill(g, Palette.Line, x, y, w, 4f);
			Fill(g, color, x, y, w * (float)Rules.Clamp(fraction, 0.0, 1.0), 4f);
		}

		private string Money(double n)
		{
			return n.ToString("N2") + " млрд";
		}

		private string Num(double n)
		{
			return n.ToString("N1");
		}

		private void Message(string error)
		{
			if (error != null)
			{
				toast = error;
				toastUntil = DateTime.Now.AddSeconds(6.0);
			}
			Invalidate();
		}

		private void Navigate(string p)
		{
			page = p;
            aviationPage = false; manpowerPage = false;
            detailPanel = p == "Конструктор";
			scroll = 0;
			hoveredTip = "";
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			Graphics graphics = e.Graphics;
			graphics.ScaleTransform(UiScale, UiScale);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			hits.Clear();
            SynchronizeAtlasCamera();
			if (menuScreen != "")
			{
				DrawMenu(graphics);
				DrawTooltip(graphics);
				return;
			}
            float num = VH - 34;
            mapRect = new RectangleF(0, 99, VW, num - 99);
            DrawMap(graphics);
            DrawCommandPanel(graphics);
            if (!detailPanel || page == "Карта") DrawOutliner(graphics);
            DrawHeader(graphics);
            DrawNav(graphics);
            DrawFooter(graphics, num);
			if (showHelp)
			{
				DrawHelp(graphics);
			}
			DrawTooltip(graphics);
			if (DateTime.Now < toastUntil)
			{
				float num2 = Math.Min(710f, VW - 240f);
				Fill(graphics, Color.FromArgb(42, 55, 65), (VW - num2) / 2f, num - 65f, num2, 50f);
				Para(graphics, toast, (VW - num2) / 2f + 16f, num - 56f, num2 - 32f, 42f, 14f, Palette.Gold);
			}
		}


		private PointF Screen(double x, double y)
		{
			return new PointF(mapRect.X + mapRect.Width / 2f + (float)(x - (double)cx) * scale, mapRect.Y + mapRect.Height / 2f + (float)(y - (double)cy) * scale);
		}

		private PointF Geo(Point p)
		{
			return new PointF(cx + ((float)p.X - mapRect.X - mapRect.Width / 2f) / scale, cy + ((float)p.Y - mapRect.Y - mapRect.Height / 2f) / scale);
		}

        private Color CountryColor(int c) => atlasColors[c];

		private Color RegionColor(int i)
		{
			Province province = E.S.Provinces[i];
            if (layer == "Дипломатия") return DiplomaticColor(province.Owner);
            if (layer == "Армии") return Mix(Color.FromArgb(119, 131, 130), Color.FromArgb(234, 195, 131), (float)Math.Min(1, militaryIntensity[i] / 300));
            if (layer == "Экономика") return Mix(Color.FromArgb(125, 119, 104), Color.FromArgb(192, 213, 169), (float)Math.Min(1, (province.Public.Sum() + province.Private.Sum()) / Math.Max(0.001, province.Population) / 4));
			if (layer == "Политика")
			{
				return CountryColor(province.Owner);
			}
			double v = ((layer == "Ресурсы") ? ((province.Oil + province.Ore + province.Soil) / 5.0) : ((layer == "Инфраструктура") ? (province.Infrastructure / 100.0) : (1.0 - province.Unrest / 100.0)));
			v = Rules.Clamp(v, 0.0, 1.0);
			return Color.FromArgb((int)(140.0 - 75.0 * v), (int)(80.0 + 70.0 * v), (int)(75.0 + 54.0 * v));
		}

        private void PageTitle(Graphics g, string title, string subtitle)
        {
            Txt(g, page.ToUpperInvariant() + "  /  " + W.Countries[E.S.Player].Code, sidebar + 28, 116, 10, Palette.Gold, true);
            Txt(g, title, sidebar + 26, 137, 27, Palette.Text, true, VW - sidebar - 60);
            if (page != "Производство") Txt(g, subtitle, sidebar + 28, 178, 12, Palette.Muted, false, VW - sidebar - 60);
            Line(g, Palette.Line, sidebar + 28, 204, VW - 28, 204);
        }

		private void Stat(Graphics g, string label, string value, float x, float y, float w, Color? color = null)
		{
			Card(g, x, y, w, 91f);
			Txt(g, label, x + 18f, y + 16f, 11f, Palette.Muted, bold: true);
			Txt(g, value, x + 17f, y + 40f, 24f, color ?? Palette.Text, bold: true, w - 28f);
		}

		private void DrawPage(Graphics g)
		{
			if (page == "Сводка")
			{
				DrawOverview(g);
			}
			else if (page == "Экономика")
			{
				DrawEconomy(g);
			}
			else if (page == "Производство")
			{
				DrawProduction(g);
			}
			else if (page == "Исследования")
			{
				DrawResearch(g);
			}
			else if (page == "Дипломатия")
			{
				DrawDiplomacy(g);
			}
			else if (page == "Армия")
			{
				DrawArmy(g);
			}
			else if (page == "Конструктор")
			{
				DrawDesigner(g);
			}
			else if (page == "Политика")
			{
				DrawPolitics(g);
			}
			else
			{
				DrawNews(g);
			}
		}

		private void DrawEconomy(Graphics g)
		{
			Nation n = E.Player;
			float num = sidebar + 34;
			float num2 = VW - (float)sidebar - 68f;
			PageTitle(g, "Экономика и рынки", "Выпуск и спрос — за неделю, запас — на складе. Все товары измеряются в условных единицах.");
			float num3 = (num2 - 36f) / 4f;
			Stat(g, "ДОХОДЫ / НЕДЕЛЯ", Money(n.Revenue), num, 222f, num3, Palette.Teal);
			Stat(g, "РАСХОДЫ / НЕДЕЛЯ", Money(n.Spending), num + num3 + 12f, 222f, num3);
			Stat(g, "ГОСУДАРСТВЕННЫЙ ДОЛГ", Money(n.Debt), num + (num3 + 12f) * 2f, 222f, num3);
			Stat(g, "ИНФЛЯЦИЯ", Num(n.Inflation) + "%", num + (num3 + 12f) * 3f, 222f, num3, (n.Inflation > 10.0) ? Palette.Red : Palette.Gold);
			float num4 = num2 * 0.64f;
			Card(g, num, 333f, num4, 355f);
			Txt(g, "ТОВАР", num + 15f, 350f, 11f, Palette.Muted, bold: true);
			Txt(g, "ВЫПУСК", num + num4 * 0.42f, 350f, 11f, Palette.Muted, bold: true);
			Txt(g, "СПРОС", num + num4 * 0.61f, 350f, 11f, Palette.Muted, bold: true);
			Txt(g, "ЗАПАС", num + num4 * 0.8f, 350f, 11f, Palette.Muted, bold: true);
			for (int i = 0; i < 8; i++)
			{
				int good = i;
				float num5 = 380 + i * 36;
				if (i == marketGood)
				{
					Fill(g, Color.FromArgb(45, 63, 69), num + 1f, num5 - 3f, num4 - 2f, 34f);
				}
				Txt(g, Rules.Goods[i], num + 15f, num5, 14f, Palette.Text);
				Txt(g, Num(n.Production[i]), num + num4 * 0.42f, num5, 14f, Palette.Teal);
				Txt(g, Num(n.Demand[i]), num + num4 * 0.61f, num5, 14f, Palette.Text);
				Txt(g, Num(n.Stock[i]), num + num4 * 0.8f, num5, 14f, (n.Stock[i] < n.Demand[i]) ? Palette.Red : Palette.Text);
				hits.Add(new Hit
				{
					Rect = new RectangleF(num, num5 - 3f, num4, 34f),
					Action = delegate
					{
						marketGood = good;
						Invalidate();
					},
					Tip = "Показать цену и производственную цепочку: " + Rules.Goods[good]
				});
			}
			float num6 = num + num4 + 18f;
			float num7 = num2 - num4 - 18f;
			Card(g, num6, 333f, num7, 355f);
			Txt(g, Rules.Goods[marketGood], num6 + 18f, 351f, 20f, Palette.Gold, bold: true, num7 - 30f);
			Txt(g, "Цена: " + Num(n.Price[marketGood] * 1000.0) + " млн / ед.", num6 + 18f, 390f, 15f, Palette.Text);
			string text = "";
			for (int num8 = 0; num8 < 8; num8++)
			{
				if (Rules.Inputs[marketGood, num8] > 0.0)
				{
					string text2 = text;
					text = text2 + Rules.Goods[num8] + ": " + Rules.Inputs[marketGood, num8].ToString("0.00") + "\n";
				}
			}
			Para(g, "Для выпуска одной единицы:\n" + ((text == "") ? "Природный потенциал, рабочие места и инфраструктура." : text) + "\nЦена реагирует на спрос, выпуск и запас. Импорт оплачивается капиталом бизнеса.", num6 + 18f, 429f, num7 - 36f, 166f);
			Button(g, n.AutoTrade ? "Автоторговля: включена" : "Автоторговля: выключена", num6 + 18f, 620f, num7 - 36f, delegate
			{
				n.AutoTrade = !n.AutoTrade;
				Invalidate();
			}, n.AutoTrade);
			if (VH > 820f)
			{
				Txt(g, "ДИНАМИКА ВВП", num, 714f, 11f, Palette.Muted, bold: true);
				Spark(g, n.History, new RectangleF(num, 740f, num4, VH - 850f), Palette.Teal);
				Txt(g, "Импорт: " + Money(n.Imports) + " / нед.", num6, 716f, 14f, Palette.Text);
				Txt(g, "Экспорт: " + Money(n.Exports) + " / нед.", num6, 745f, 14f, Palette.Teal);
			}
		}

		private void Spark(Graphics g, List<double> values, RectangleF box, Color color)
		{
			if (!(box.Height < 10f) && values.Count >= 2)
			{
				double min = values.Min();
				double max = values.Max();
				if (max - min < 0.01)
				{
					max = min + 1.0;
				}
				PointF[] points = values.Select((double v, int i) => new PointF(box.X + (float)i * box.Width / (float)(values.Count - 1), box.Bottom - (float)((v - min) / (max - min)) * box.Height)).ToArray();
				using (Pen pen = new Pen(color, 2f))
				{
					g.DrawLines(pen, points);
				}
				Line(g, Palette.Line, box.X, box.Bottom, box.Right, box.Bottom);
			}
		}

		private int OwnRegion()
		{
			if (selected >= 0 && E.S.Provinces[selected].Owner == E.S.Player)
			{
				return selected;
			}
			return Enumerable.Range(0, W.Regions.Length).FirstOrDefault((int r) => E.S.Provinces[r].Owner == E.S.Player);
		}


		private void Adjust(Graphics g, string label, int value, float x, float y, float width, Action<int> change)
		{
			Txt(g, label, x, y + 7f, 15f, Palette.Text);
			Button(g, "−", x + width - 133f, y, 36f, delegate
			{
				change(-1);
				Invalidate();
			});
			Txt(g, value.ToString(), x + width - 79f, y + 6f, 18f, Palette.Gold, bold: true);
			Button(g, "+", x + width - 38f, y, 36f, delegate
			{
				change(1);
				Invalidate();
			});
		}

		private void Policy(Graphics g, string label, string explanation, double value, float x, float y, float w, Action<double> set, double min, double max, double step)
		{
			Card(g, x, y, w, 85f);
			Txt(g, label, x + 18f, y + 13f, 17f, Palette.Text, bold: true);
			Txt(g, explanation, x + 18f, y + 45f, 12f, Palette.Muted, bold: false, w - 240f);
			Button(g, "−", x + w - 169f, y + 24f, 36f, delegate
			{
				set(Rules.Clamp(value - step, min, max));
				E.ComputeEconomy(apply: false);
				Invalidate();
			});
			Txt(g, Num(value) + "%", x + w - 119f, y + 30f, 19f, Palette.Gold, bold: true);
			Button(g, "+", x + w - 52f, y + 24f, 36f, delegate
			{
				set(Rules.Clamp(value + step, min, max));
				E.ComputeEconomy(apply: false);
				Invalidate();
			});
		}

		private void DrawPolitics(Graphics g)
		{
			float x = sidebar + 34;
			float w = VW - (float)sidebar - 68f;
			Nation n = E.Player;
			PageTitle(g, "Внутренняя политика", "Вы определяете курс государства. Уровень жизни и устойчивость власти постепенно реагируют на решения.");
			Policy(g, "Налоговая нагрузка", "Доля экономики, направляемая в бюджет. Высокие налоги сдерживают инвестиции.", n.Tax, x, 220f, w, delegate(double v)
			{
				n.Tax = v;
			}, 5.0, 50.0, 1.0);
			Policy(g, "Социальные расходы", "Доля ВВП на общественные услуги. Поддерживает благополучие и одобрение.", n.Social, x, 318f, w, delegate(double v)
			{
				n.Social = v;
			}, 0.0, 15.0, 0.5);
			Policy(g, "Наука и образование", "Доля ВВП. Ускоряет исследования и постепенно повышает образование.", n.Science, x, 416f, w, delegate(double v)
			{
				n.Science = v;
			}, 0.0, 10.0, 0.5);
			Policy(g, "Военный бюджет", "Доля ВВП. Влияет на снабжение, восстановление и содержание соединений.", n.Military, x, 514f, w, delegate(double v)
			{
				n.Military = v;
			}, 0.5, 15.0, 0.5);
			Policy(g, "Политическое давление", "Временно поддерживает контроль, но снижает одобрение и производительность.", n.Repression, x, 612f, w, delegate(double v)
			{
				n.Repression = v;
			}, 0.0, 100.0, 5.0);
			Txt(g, "Одобрение: " + Num(n.Approval) + "%    ·    Уровень жизни: " + Num(n.Living) + " / 100    ·    Безработица: " + Num(n.Unemployment) + "%", x, 727f, 15f, Palette.Teal);
		}

		private void DrawNews(Graphics g)
		{
			float num = sidebar + 34;
			float num2 = VW - (float)sidebar - 68f;
			PageTitle(g, "Хроника государства", "Решения, завершённые проекты и события. Новые записи появляются сверху.");
			int num3 = (int)Math.Max(1f, (VH - 338f) / 69f);
			int num4 = Math.Min(scroll, Math.Max(0, E.S.News.Count - num3));
			for (int i = num4; i < Math.Min(E.S.News.Count, num4 + num3); i++)
			{
				Bulletin bulletin = E.S.News[i];
				float num5 = 220 + (i - num4) * 69;
				Card(g, num, num5, num2, 58f);
				Txt(g, bulletin.Date, num + 16f, num5 + 16f, 13f, Palette.Gold);
				Para(g, bulletin.Text, num + 130f, num5 + 10f, num2 - 148f, 42f, 14f, (bulletin.Type == 2) ? Palette.Red : Palette.Text);
			}
		}


		private void DrawHelp(Graphics g)
		{
			hits.Clear();
			Fill(g, Color.FromArgb(225, 8, 14, 21), sidebar, 99f, VW - (float)sidebar, VH - 99f);
			float x = sidebar + 55;
			float width = VW - (float)sidebar - 110f;
			Txt(g, "Ваш первый год у власти", x, 145f, 32f, Palette.Gold, bold: true);
			string[] array = new string[7] { "1. Начните со «Сводки»: она показывает бюджет, баланс товаров и текущие проекты.", "2. Выберите свою область на карте → «Строить и развивать». Постройте недостающие мощности.", "3. Откройте «Политика»: согласуйте налоги, социальные расходы и науку с бюджетом.", "4. Запустите исследование. Частные компании инвестируют автоматически каждые 30 дней.", "5. Улучшайте отношения и заключайте торговые договоры. Автоторговля работает раз в неделю.", "6. Выберите армию и нажмите ПКМ по области или создайте фронт. Для прохода через мирную страну нужен военный доступ.", "7. F5 сохраняет кампанию локально; F9 загружает. Автосохранение — каждые 90 игровых дней." };
			for (int i = 0; i < array.Length; i++)
			{
				Para(g, array[i], x, 218 + i * 59, width, 51f, 16f, Palette.Text);
			}
			Para(g, "География современная; данные экономики оценочные. Союзы, доступ и условия мира — в дипломатии. Управление можно переназначить в настройках. Авиация: Армия → Авиация. Постройте аэродром, выпустите самолёты и назначьте область вылетов. Морская война ещё не реализована.", x, 661f, width, 75f, 14f, Palette.Muted);
			Button(g, "Понятно, к управлению", x, 755f, 260f, delegate
			{
				showHelp = false;
				Invalidate();
			}, active: true);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			mouse = Logical(e.Location);
			Hit hit = hits.LastOrDefault((Hit h) => h.Rect.Contains(mouse));
			hoveredRegion = menuScreen == "" && MapInputAllowed(mouse) && hit == null && !panning ? FindRegion(Geo(mouse)) : -1;
            if (menuScreen == "new") hoveredCountry = MapInputAllowed(mouse) && !panning ? FindCountry(Geo(mouse)) : -1;
            string text = menuScreen == "new" && hit == null ? (hoveredCountry >= 0 ? W.Countries[hoveredCountry].Name + " · нажмите для выбора" : "") : hit == null ? (hoveredRegion >= 0 ? W.Regions[hoveredRegion].Name + " · " + W.Countries[E.S.Provinces[hoveredRegion].Owner].Name : "") : hit.Tip;
			if (text != hoveredTip)
			{
				hoveredTip = text;
				hoverSince = DateTime.Now;
			}
			if (panning)
			{
				int num = mouse.X - last.X;
				int num2 = mouse.Y - last.Y;
				if (Math.Abs(mouse.X - panOrigin.X) + Math.Abs(mouse.Y - panOrigin.Y) > 4)
				{
					moved = true;
				}
				cx -= (float)num / scale;
				cy -= (float)num2 / scale;
                ClampCamera();
				last = Logical(e.Location);
			}
			Cursor = (hits.Any((Hit h) => h.Rect.Contains(mouse)) ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button == MouseButtons.Left && MapInputAllowed(Logical(e.Location)) && !hits.Any((Hit h) => h.Rect.Contains(Logical(e.Location))) && !showHelp)
			{
				panning = true;
				moved = false;
				last = Logical(e.Location); panOrigin = last;
				base.Capture = true;
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			panning = false;
			base.Capture = false;
			if (moved)
			{
				moved = false;
                Invalidate();
				return;
			}
			if (e.Button == MouseButtons.Left)
			{
				Hit hit = hits.LastOrDefault((Hit h) => h.Rect.Contains(Logical(e.Location)));
				if (hit != null)
				{
					hit.Action();
					return;
				}
			}
			if (menuScreen == "new" && MapInputAllowed(Logical(e.Location)) && e.Button == MouseButtons.Left) {
                int country = FindCountry(Geo(Logical(e.Location)));
                if (country >= 0) { menuCountry = country; Invalidate(); }
                return;
            }
            if (menuScreen != "" || showHelp || !MapInputAllowed(Logical(e.Location)))
			{
				return;
			}
			PointF pt = Geo(Logical(e.Location));
			int num = FindRegion(pt);
			if (num >= 0)
			{
				if (e.Button == MouseButtons.Right)
				{
					Message(E.Order(army, num));
					return;
				}
				selected = num;
				foreign = E.S.Provinces[num].Owner;
				Invalidate();
			}
		}

		private int FindRegion(PointF pt)
		{
			for (int i = 0; i < W.Regions.Length; i++)
			{
				if (W.Regions[i].Bounds.Contains(pt) && W.Regions[i].Path.IsVisible(pt))
				{
					return i;
				}
			}
			return -1;
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);
			if (showHelp)
			{
				return;
			}
			if (menuScreen != "" && !MapInputAllowed(Logical(e.Location)))
			{
				menuScroll = Math.Max(0, menuScroll + ((e.Delta < 0) ? 2 : (-2)));
				Invalidate();
				return;
			}
			if (MapInputAllowed(Logical(e.Location)))
			{
				deferMapRefresh = true; cameraRefresh.Stop(); cameraRefresh.Start();
                PointF pointF = Geo(Logical(e.Location));
				scale = Math.Max(2f, Math.Min(100f, scale * ((e.Delta > 0) ? 1.18f : 0.84745765f)));
				PointF pointF2 = Geo(Logical(e.Location));
				cx += pointF.X - pointF2.X;
				cy += pointF.Y - pointF2.Y;
                ClampCamera();
			}
			else
			{
				scroll = Math.Max(0, scroll + ((e.Delta < 0) ? 1 : (-1)));
			}
			Invalidate();
		}

        private void KeysDown(object sender, KeyEventArgs e)
        {
            if (CaptureBinding(e)) return;
            if (e.KeyData == prefs.Binding("Fullscreen")) { ToggleFullScreen(); e.Handled = true; return; }
            if (menuScreen != "") {
                if (e.KeyCode == Keys.Escape) { if (menuScreen == "main" && campaignStarted) ContinueGame(); else OpenMenu(menuScreen == "controls" ? "settings" : "main"); e.Handled = true; }
                return;
            }
            for (int i = 0; i < 9; i++) if (e.KeyData == prefs.Binding("Page" + i)) { Navigate(Navigation[i]); e.Handled = true; }
            if (e.KeyData == prefs.Binding("Pause")) { speed = speed == 0 ? 1 : 0; e.Handled = true; }
            if (e.KeyData == prefs.Binding("Save")) { Save(); e.Handled = true; }
            if (e.KeyData == prefs.Binding("Load")) { LoadCampaign(); e.Handled = true; }
            if (e.KeyCode == Keys.Escape) { if (showHelp) showHelp = false; else OpenMenu("main"); e.Handled = true; }
            if (e.KeyData == prefs.Binding("Home")) { Home(); e.Handled = true; }
            if (e.KeyData == prefs.Binding("Help")) { speed = 0; showHelp = !showHelp; e.Handled = true; }
            if (e.Handled) e.SuppressKeyPress = true;
            Invalidate();
        }

		public void PreviewPage(string p)
		{
			switch (p)
			{
			case "Меню":
				menuScreen = "main";
                LayoutMenuControls();
				return;
			case "Новая игра":
				menuScreen = "new";
				LayoutMenuControls();
				return;
			case "Настройки":
				menuScreen = "settings";
                LayoutMenuControls();
				return;
			case "Сохранения":
				menuScreen = "load";
                LayoutMenuControls();
				return;
			}
			if (E.S == null)
			{
				E.New(W.ByCode("UKR"), 2026, 1);
				Home();
				draft = Engine.CopyDesign(E.Player.Design);
			}
			menuScreen = "";
			page = p; detailPanel = p == "Конструктор"; aviationPage = p == "Авиация"; manpowerPage = p == "Резерв";
            if (aviationPage || manpowerPage) page = "Армия";
            if (p == "Серии") { page = "Производство"; equipmentProduction = true; }
			LayoutMenuControls();
			Invalidate();
		}

		public void Home()
		{
			int num = (from i in Enumerable.Range(0, W.Regions.Length)
				where E.S.Provinces[i].Owner == E.S.Player
				orderby E.S.Provinces[i].Population descending
				select i).FirstOrDefault();
			selected = num;
			CountryInfo countryInfo = W.Countries[E.S.Player];
			cx = (float)countryInfo.X;
			cy = (float)countryInfo.Y;
			scale = (float)Math.Max(4.0, Math.Min(19.0, 680.0 / Math.Max(18.0, (double)countryInfo.Bounds.Width * 1.5)));
			army = (from a in E.S.Armies
				where a.Owner == E.S.Player
				select a.Id).FirstOrDefault();
			Invalidate();
		}

		private void Save()
		{
			if (E.S == null)
			{
				return;
			}
			try
			{
				E.Save(Path.Combine(Engine.Saves, "campaign.xml"));
				Message("Кампания сохранена. Файл находится в папке Saves рядом с игрой.");
			}
			catch (Exception ex)
			{
				Message("Не удалось сохранить: " + ex.Message);
			}
		}

		private void LoadCampaign()
		{
			speed = 0;
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = "Загрузить кампанию";
			openFileDialog.Filter = "Сохранения Sovereign (*.xml)|*.xml";
			openFileDialog.InitialDirectory = Engine.Saves;
			using (OpenFileDialog openFileDialog2 = openFileDialog)
			{
				if (openFileDialog2.ShowDialog(this) == DialogResult.OK)
				{
					LoadFrom(openFileDialog2.FileName);
				}
			}
		}

		private void ChooseForeign()
		{
			using (SetupDialog setupDialog = new SetupDialog(W, campaign: false, E.S.Player))
			{
				if (setupDialog.ShowDialog(this) == DialogResult.OK)
				{
					foreign = setupDialog.Country;
					Invalidate();
				}
			}
		}

		public void NewGame()
		{
			OpenMenu("new");
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				clock.Dispose();
                cameraRefresh.Dispose();
                if (mapCache != null) mapCache.Dispose();

				if (terrainTexture != null)
				{
					terrainTexture.Dispose();
				}
				foreach (Font value in fonts.Values)
				{
					value.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		private void InitializePresentation()
		{
			menuCountry = W.ByCode("UKR");
			countryList = (from c in Enumerable.Range(0, W.Countries.Length)
				orderby W.Countries[c].Name
				select c).ToList();

			countrySearch = new TextBox
			{
				BorderStyle = BorderStyle.None,
				BackColor = Palette.Card,
				ForeColor = Palette.Text,
				Font = new Font("Segoe UI", 13f),
				Visible = false
			};
			countrySearch.TextChanged += delegate
			{
				countryQuery = countrySearch.Text;
				menuScroll = 0;
				Invalidate();
			};
			base.Controls.Add(countrySearch);
			startYear = new NumericUpDown
			{
				Minimum = 1940m,
				Maximum = 2026m,
				Value = 2026m,
				BackColor = Palette.Card,
				ForeColor = Palette.Text,
				BorderStyle = BorderStyle.None,
				Visible = false,
				Font = new Font("Segoe UI", 18f, FontStyle.Bold)
			};
			startYear.ValueChanged += delegate
			{
				newYear = (int)startYear.Value;
				Invalidate();
			};
			base.Controls.Add(startYear);
			base.Resize += delegate
			{
				LayoutMenuControls();
			};
			terrainTexture = new Bitmap(128, 128);
			Random random = new Random(7893);
			for (int num = 0; num < 128; num++)
			{
				for (int num2 = 0; num2 < 128; num2++)
				{
					int alpha = random.Next(2, 8);
					terrainTexture.SetPixel(num, num2, Color.FromArgb(alpha, 240, 230, 205));
				}
			}
			E.AutoSaveEnabled = prefs.Autosave;
			base.Shown += delegate
			{
				if (prefs.Fullscreen && !fullScreen)
				{
					ToggleFullScreen();
				}
			};
		}

		private void LayoutMenuControls()
		{
			if (countrySearch != null)
			{
				bool flag = menuScreen == "new";
				countrySearch.Visible = flag;
				startYear.Visible = flag;
				if (flag)
				{
					countrySearch.SetBounds((int)(24 * UiScale), (int)(142 * UiScale), (int)(270 * UiScale), (int)(29 * UiScale));
					startYear.SetBounds((int)((VW - 290) * UiScale), (int)(390 * UiScale), (int)(258 * UiScale), (int)(32 * UiScale));
					startYear.Enabled = experimentalYear;
				}
			}
		}

		private void OpenMenu(string target)
		{
			if (target == "game")
			{
				ContinueGame();
				return;
			}
			hoveredTip = "";
			speed = 0;
			menuScreen = target;
			showHelp = false;
			menuScroll = 0;
			LayoutMenuControls();
			Invalidate();
		}

		private string LatestSave()
		{
			if (!Directory.Exists(Engine.Saves))
			{
				return null;
			}
			return Directory.GetFiles(Engine.Saves, "*.xml").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
		}

		private void ContinueGame()
		{
			if (campaignStarted && E.S != null)
			{
				menuScreen = "";
				LayoutMenuControls();
				Invalidate();
				return;
			}
			string text = LatestSave();
			if (text != null)
			{
				LoadFrom(text);
			}
		}

		private void LoadFrom(string path)
		{
			try
			{
				E.Load(path);
				E.AutoSaveEnabled = prefs.Autosave;
				campaignStarted = true;
				draft = Engine.CopyDesign(E.Player.Design);
				menuScreen = "";
				LayoutMenuControls();
				Home();
				Navigate("Карта");
				Message("Кампания загружена. Время на паузе.");
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Сохранение не загружено.\n" + ex.Message, "Sovereign", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
		}

		private void BeginCampaign()
		{
            selectedUnits.Clear(); frontOffset = 0; compareDesignId = 0; selectedAirWing = -1; airRosterOffset = 0;
			if (campaignStarted && E.S != null && prefs.Autosave)
			{
				E.Autosave();
			}
			E.New(menuCountry, newYear, newDifficulty);
			E.AutoSaveEnabled = prefs.Autosave;
			campaignStarted = true;
			speed = 0;
			menuScreen = "";
			LayoutMenuControls();
			Home();
			SynchronizeAtlasCamera();
            Home();
            Navigate("Карта");
			draft = Engine.CopyDesign(E.Player.Design);
			showHelp = prefs.Tutorial;
		}

		private void ToggleFullScreen()
		{
			if (!fullScreen)
			{
				windowBounds = base.Bounds;
				previousBorder = base.FormBorderStyle;
				base.FormBorderStyle = FormBorderStyle.None;
				base.WindowState = FormWindowState.Maximized;
				fullScreen = true;
			}
			else
			{
				base.WindowState = FormWindowState.Normal;
				base.FormBorderStyle = previousBorder;
				base.Bounds = windowBounds;
				fullScreen = false;
			}
			prefs.Fullscreen = fullScreen;
			prefs.Save();
			LayoutMenuControls();
			Invalidate();
		}

		private void MenuButton(Graphics g, string title, string description, float x, float y, float w, Action action, bool primary = false, bool enabled = true)
		{
			RectangleF rectangleF = new RectangleF(x, y, w, 71f);
			bool flag = enabled && rectangleF.Contains(mouse);
			Color c = (primary ? Color.FromArgb(193, 169, 114) : (flag ? Color.FromArgb(40, 54, 60) : Color.FromArgb(23, 35, 43)));
			Round(g, c, rectangleF, 8f);
			if (!primary)
			{
				RoundBorder(g, flag ? Palette.Gold : Palette.Line, rectangleF, 8f, 1f);
			}
			Color color = ((!enabled) ? Color.FromArgb(90, 107, 114) : (primary ? Palette.Bg : Palette.Text));
			Txt(g, title, x + 22f, y + 12f, 20f, color, bold: true);
			Txt(g, description, x + 22f, y + 43f, 12f, primary ? Color.FromArgb(50, 57, 52) : Palette.Muted, bold: false, w - 55f);
			Txt(g, "›", x + w - 31f, y + 15f, 25f, color);
			if (enabled)
			{
				hits.Add(new Hit
				{
					Rect = rectangleF,
					Action = action,
					Tip = description
				});
			}
		}

		private void AtlasBackdrop(Graphics g, RectangleF rect, bool focusCountry)
		{
			GraphicsState gstate = g.Save();
			g.SetClip(rect);
			using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(16, 31, 42), Color.FromArgb(8, 17, 25), 35f))
			{
				g.FillRectangle(brush, rect);
			}
			float num = rect.Width / 260f;
			float num2 = 35f;
			float num3 = -20f;
			if (focusCountry)
			{
				CountryInfo countryInfo = W.Countries[menuCountry];
				num2 = (float)countryInfo.X;
				num3 = (float)countryInfo.Y;
				num = Math.Min(rect.Width / Math.Max(10f, countryInfo.Bounds.Width * 1.7f), rect.Height / Math.Max(10f, countryInfo.Bounds.Height * 1.8f));
				num = Math.Max(1.5f, Math.Min(20f, num));
			}
			float num4 = rect.X + rect.Width / 2f - num2 * num;
			float num5 = rect.Y + rect.Height / 2f - num3 * num;
			for (int i = -180; i <= 180; i += 15)
			{
				Line(g, Color.FromArgb(25, 43, 53), num4 + (float)i * num, rect.Top, num4 + (float)i * num, rect.Bottom);
			}
			for (int j = -90; j <= 90; j += 15)
			{
				Line(g, Color.FromArgb(25, 43, 53), rect.Left, num5 + (float)j * num, rect.Right, num5 + (float)j * num);
			}
			Matrix transform = g.Transform;
			using (Matrix transform2 = new Matrix(num * UiScale, 0f, 0f, num * UiScale, num4 * UiScale, num5 * UiScale))
			{
				g.Transform = transform2;
				for (int k = 0; k < W.Countries.Length; k++)
				{
					using (SolidBrush brush2 = new SolidBrush((focusCountry && k == menuCountry) ? Color.FromArgb(145, 152, 115) : Color.FromArgb(36 + k % 3 * 5, 58 + k % 4 * 3, 66 + k % 3 * 4)))
					{
						g.FillPath(brush2, W.Countries[k].Path);
					}
					using (Pen pen = new Pen(Color.FromArgb(11, 29, 39), 0.7f / num))
					{
						g.DrawPath(pen, W.Countries[k].Path);
					}
				}
				if (focusCountry)
				{
					using (Pen pen2 = new Pen(Palette.Gold, 2f / num))
					{
						g.DrawPath(pen2, W.Countries[menuCountry].Path);
					}
				}
			}
			g.Transform = transform;
			transform.Dispose();
			if (!focusCountry)
			{
				PointF pointF = new PointF(num4 + 30f * num, num5 - 48f * num);
				PointF pointF2 = new PointF(num4 + 100f * num, num5 - 35f * num);
				PointF pointF3 = new PointF(num4 - 75f * num, num5 - 30f * num);
				using (Pen pen3 = new Pen(Color.FromArgb(90, 191, 168, 111), 1f))
				{
					g.DrawBezier(pen3, pointF, new PointF(pointF.X + 80f, pointF.Y - 110f), new PointF(pointF2.X - 70f, pointF2.Y - 100f), pointF2);
					g.DrawBezier(pen3, pointF, new PointF(pointF.X - 120f, pointF.Y - 160f), new PointF(pointF3.X + 90f, pointF3.Y - 100f), pointF3);
				}
				PointF[] array = new PointF[3] { pointF, pointF2, pointF3 };
				for (int l = 0; l < array.Length; l++)
				{
					PointF pointF4 = array[l];
					using (SolidBrush brush3 = new SolidBrush(Palette.Gold))
					{
						g.FillEllipse(brush3, pointF4.X - 4f, pointF4.Y - 4f, 8f, 8f);
					}
					using (Pen pen4 = new Pen(Color.FromArgb(70, 222, 188, 122), 1f))
					{
						g.DrawEllipse(pen4, pointF4.X - 12f, pointF4.Y - 12f, 24f, 24f);
					}
				}
			}
			if (prefs.Texture)
			{
				using (TextureBrush brush4 = new TextureBrush(terrainTexture, WrapMode.Tile))
				{
					g.FillRectangle(brush4, rect);
				}
			}
			g.Restore(gstate);
		}

		private void DrawMenu(Graphics g)
		{
			if (menuScreen == "main")
			{
				DrawMainMenu(g);
			}
			else if (menuScreen == "new")
			{
				DrawCampaignSetup(g);
			}
			else if (menuScreen == "controls") { DrawBindings(g); }
            else if (menuScreen == "settings")
			{
				DrawSettings(g);
			}
			else if (menuScreen == "load")
			{
				DrawLoadMenu(g);
			}
			else
			{
				DrawMenuGuide(g);
			}
			if (DateTime.Now < toastUntil)
			{
				Round(g, Palette.Card, new RectangleF(50f, VH - 100f, VW - 100f, 45f), 6f);
				Txt(g, toast, 66f, VH - 88f, 14f, Palette.Gold, bold: false, VW - 140f);
			}
		}

		private void DrawMainMenu(Graphics g)
		{
			AtlasBackdrop(g, new RectangleF(0f, 0f, VW, VH), focusCountry: false);
			using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(0f, 0f, VW * 0.8f, VH), Color.FromArgb(255, 9, 18, 26), Color.FromArgb(0, 9, 18, 26), 0f))
			{
				g.FillRectangle(brush, 0f, 0f, VW * 0.8f, VH);
			}
			Fill(g, Color.FromArgb(150, 8, 15, 22), 0f, VH - 88f, VW, 88f);
			float num = 70f;
			Crest(g, num, 58f, 46f, Palette.Gold);
			Txt(g, "Г Л О Б А Л Ь Н А Я   С Т Р А Т Е Г И Я", num + 66f, 71f, 11f, Palette.Gold, bold: true);
			Txt(g, "SOVEREIGN", num - 4f, 135f, 69f, Palette.Text, bold: true);
			Txt(g, "ГОСУДАРСТВО", num, 219f, 17f, Palette.Gold, bold: true);
			Para(g, "История страны — в ваших решениях.", num, 257f, 500f, 50f, 19f, Palette.Muted);
			bool flag = campaignStarted || LatestSave() != null;
			MenuButton(g, "Продолжить", campaignStarted ? "Вернуться к текущему государству" : (flag ? "Открыть последнее локальное сохранение" : "Сохранённых кампаний пока нет"), num, 330f, 405f, ContinueGame, primary: false, flag);
			MenuButton(g, "Новая игра", "Страна, начальный год и сложность", num, 414f, 405f, delegate
			{
				OpenMenu("new");
			}, primary: true);
			MenuButton(g, "Загрузить кампанию", "Выбрать сохранённую кампанию", num, 498f, 405f, delegate
			{
				OpenMenu("load");
			});
			Button(g, "Настройки", num, 594f, 195f, delegate
			{
				menuBack = "main";
				OpenMenu("settings");
			}, active: false, "Окно, карта, подсказки и автосохранение", 44f);
			Button(g, "Как играть", num + 210f, 594f, 195f, delegate
			{
				OpenMenu("guide");
			}, active: false, "Короткое руководство по управлению государством", 44f);
			Button(g, "Выйти из игры", num, 660f, 405f, base.Close, active: false, "Закрыть игру. Начатая кампания сохранится, если включено автосохранение.", 40f);
			float num2 = VW - 420f;
			Round(g, Color.FromArgb(220, 18, 32, 41), new RectangleF(num2, VH - 290f, 350f, 159f), 10f);
			Txt(g, "МИР В ВАШИХ РУКАХ", num2 + 22f, VH - 268f, 11f, Palette.Gold, bold: true);
			Txt(g, "176", num2 + 20f, VH - 240f, 39f, Palette.Text, bold: true);
			Txt(g, "4 136", num2 + 172f, VH - 240f, 39f, Palette.Text, bold: true);
			Txt(g, "государств и территорий", num2 + 22f, VH - 188f, 11f, Palette.Muted);
			Txt(g, "областей", num2 + 175f, VH - 188f, 11f, Palette.Muted);
			Txt(g, "ЭКОНОМИКА  /  ДИПЛОМАТИЯ  /  ТЕХНОЛОГИИ  /  СТРАТЕГИЯ", num, VH - 59f, 12f, Palette.Muted);
			Txt(g, "0.6   •   ОФЛАЙН   •   РАННЯЯ ВЕРСИЯ", VW - 354f, VH - 59f, 11f, Palette.Gold, bold: true);
		}


		private void SettingRow(Graphics g, string label, string detail, float x, float y, float w, bool enabled, Action toggle)
		{
			Card(g, x, y, w, 90f);
			Txt(g, label, x + 22f, y + 15f, 19f, Palette.Text, bold: true);
			Para(g, detail, x + 22f, y + 47f, w - 220f, 35f, 13f);
			Button(g, enabled ? "Включено" : "Выключено", x + w - 164f, y + 27f, 142f, delegate
			{
				toggle();
				prefs.Save();
				E.AutoSaveEnabled = prefs.Autosave;
				Invalidate();
			}, enabled, detail, 36f);
		}

		private void DrawSettings(Graphics g)
		{
			Fill(g, Palette.Bg, 0f, 0f, VW, VH);
			Button(g, "← Назад", 48f, 30f, 150f, delegate
			{
				OpenMenu(menuBack);
			});
			Txt(g, "Настройки", 48f, 99f, 36f, Palette.Text, bold: true);
			Txt(g, "Изменения применяются сразу и сохраняются на этом компьютере", 50f, 151f, 15f, Palette.Muted);
			float num = 48f;
			float num2 = VW - 96f;
			float num3 = (num2 - 20f) / 2f;
			SettingRow(g, "Полноэкранное окно", "Клавиша: " + prefs.KeyLabel("Fullscreen"), num, 215f, num3, fullScreen, ToggleFullScreen);
			SettingRow(g, "Подписи на карте", "Названия стран и областей с учётом масштаба.", num + num3 + 20f, 215f, num3, prefs.Labels, delegate
			{
				prefs.Labels = !prefs.Labels;
			});
			SettingRow(g, "Границы областей", "Показывать административные границы при приближении.", num, 322f, num3, prefs.RegionBorders, delegate
			{
				prefs.RegionBorders = !prefs.RegionBorders;
			});
			SettingRow(g, "Текстура карты", "Тонкая фактура атласа. Можно отключить для более плоского вида.", num + num3 + 20f, 322f, num3, prefs.Texture, delegate
			{
				prefs.Texture = !prefs.Texture;
			});
			SettingRow(g, "Подсказки при наведении", "Объяснять кнопки, показатели и действия.", num, 429f, num3, prefs.Tooltips, delegate
			{
				prefs.Tooltips = !prefs.Tooltips;
			});
			SettingRow(g, "Автосохранение", "Каждые 90 игровых дней и при выходе из начатой кампании.", num + num3 + 20f, 429f, num3, prefs.Autosave, delegate
			{
				prefs.Autosave = !prefs.Autosave;
			});
			SettingRow(g, "Обучение в новой игре", "Короткая последовательность первых действий.", num, 536f, num3, prefs.Tutorial, delegate
			{
				prefs.Tutorial = !prefs.Tutorial;
			});
			Button(g, "Настроить клавиши →", num + num3 + 20, 559, num3, () => OpenMenu("controls"), true);
            Para(g, "Размер интерфейса подстраивается под окно. Звук пока не добавлен. Настройки не меняют расчёты экономики или сложность уже начатой кампании.", num, 685f, num2, 80f, 15f);
		}

		private void DrawLoadMenu(Graphics g)
		{
			Fill(g, Palette.Bg, 0f, 0f, VW, VH);
			Button(g, "← Главное меню", 48f, 30f, 190f, delegate
			{
				OpenMenu("main");
			});
			Txt(g, "Сохранённые кампании", 48f, 99f, 36f, Palette.Text, bold: true);
			Txt(g, "Выберите локальную кампанию или откройте файл вручную", 50f, 151f, 15f, Palette.Muted);
			string[] array = (Directory.Exists(Engine.Saves) ? Directory.GetFiles(Engine.Saves, "*.xml").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : new string[0]);
			int num = (int)((VH - 340f) / 88f);
			int num2 = Math.Min(menuScroll, Math.Max(0, array.Length - num));
			if (array.Length == 0)
			{
				Para(g, "Сохранений пока нет. Начните новую историю — её можно будет сохранить клавишей F5.", 50f, 239f, 750f, 100f, 22f, Palette.Text);
			}
			for (int num3 = num2; num3 < Math.Min(array.Length, num2 + num); num3++)
			{
				string path = array[num3];
				float num4 = 215 + (num3 - num2) * 88;
				Card(g, 48f, num4, VW - 96f, 75f);
				Txt(g, (Path.GetFileName(path) == "autosave.xml") ? "Автосохранение" : Path.GetFileNameWithoutExtension(path), 70f, num4 + 12f, 20f, Palette.Text, bold: true);
				Txt(g, File.GetLastWriteTime(path).ToString("dd.MM.yyyy  HH:mm") + "  •  " + new FileInfo(path).Length / 1024 + " КБ", 72f, num4 + 44f, 12f, Palette.Muted);
				Button(g, "Загрузить →", VW - 240f, num4 + 21f, 170f, delegate
				{
					LoadFrom(path);
				}, active: true, "Продолжить выбранную кампанию");
			}
			Button(g, "Открыть другой файл…", 48f, VH - 98f, 270f, LoadCampaign, active: false, "Выбрать сохранение в другой папке", 42f);
		}

		private void DrawMenuGuide(Graphics g)
		{
			Fill(g, Palette.Bg, 0f, 0f, VW, VH);
			Button(g, "← Главное меню", 48f, 30f, 190f, delegate
			{
				OpenMenu("main");
			});
			Txt(g, "С чего начать", 48f, 99f, 36f, Palette.Text, bold: true);
			Txt(g, "Пять решений, которые запускают вашу историю", 50f, 151f, 15f, Palette.Muted);
			string[] array = new string[5] { "Изучите страну", "Сведите бюджет", "Запустите производство", "Развивайте технологии", "Определите внешнюю политику" };
			string[] array2 = new string[5] { "Выберите область на карте. Посмотрите ресурсы, население и инфраструктуру.", "В разделе «Политика» настройте налоги и расходы. Проверяйте недельный баланс.", "В «Экономике» найдите дефицит. Постройте нужные мощности или полагайтесь на импорт.", "Научная программа повышает эффективность. Частные компании инвестируют самостоятельно.", "Торговля, соглашения и эмбарго влияют на ресурсы. Армии нужны снабжение и техника." };
			for (int num = 0; num < 5; num++)
			{
				float num2 = 216 + num * 101;
				Card(g, 48f, num2, VW - 96f, 87f);
				Txt(g, "0" + (num + 1), 68f, num2 + 16f, 33f, Palette.Gold, bold: true);
				Txt(g, array[num], 133f, num2 + 14f, 21f, Palette.Text, bold: true);
				Para(g, array2[num], 135f, num2 + 48f, VW - 250f, 32f);
			}
			Txt(g, "Пробел — пауза    •    Колесо — масштаб    •    ПКМ — приказ армии    •    F5 — сохранить    •    Esc — меню", 50f, VH - 98f, 14f, Palette.Gold);
		}

		private GraphicsPath Rounded(RectangleF r, float radius)
		{
			float num = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
			GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		private void Round(Graphics g, Color c, RectangleF r, float radius)
		{
			using (GraphicsPath path = Rounded(r, radius))
			{
				using (SolidBrush brush = new SolidBrush(c))
				{
					g.FillPath(brush, path);
				}
			}
		}

		private void RoundBorder(Graphics g, Color c, RectangleF r, float radius, float thickness)
		{
			using (GraphicsPath path = Rounded(r, radius))
			{
				using (Pen pen = new Pen(c, thickness))
				{
					g.DrawPath(pen, path);
				}
			}
		}

		private void Crest(Graphics g, float x, float y, float size, Color color)
		{
			using (Pen pen = new Pen(color, 1.5f))
			{
				g.DrawPolygon(pen, new PointF[5]
				{
					new PointF(x, y + size * 0.1f),
					new PointF(x + size, y + size * 0.1f),
					new PointF(x + size * 0.9f, y + size * 0.67f),
					new PointF(x + size * 0.5f, y + size),
					new PointF(x + size * 0.1f, y + size * 0.67f)
				});
				g.DrawLines(pen, new PointF[8]
				{
					new PointF(x + size * 0.32f, y + size * 0.58f),
					new PointF(x + size * 0.32f, y + size * 0.3f),
					new PointF(x + size * 0.4f, y + size * 0.43f),
					new PointF(x + size * 0.5f, y + size * 0.34f),
					new PointF(x + size * 0.6f, y + size * 0.43f),
					new PointF(x + size * 0.78f, y + size * 0.3f),
					new PointF(x + size * 0.78f, y + size * 0.58f),
					new PointF(x + size * 0.32f, y + size * 0.58f)
				});
			}
		}

		private void NavIcon(Graphics g, int kind, float x, float y, float size, Color color)
		{
			Pen pen = new Pen(color, 1.5f);
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;
			using (Pen pen2 = pen)
			{
				switch (kind)
				{
				case 0:
					g.DrawPolygon(pen2, new PointF[8]
					{
						new PointF(x, y + 4f),
						new PointF(x + 6f, y),
						new PointF(x + 13f, y + 4f),
						new PointF(x + size, y),
						new PointF(x + size, y + size - 4f),
						new PointF(x + 13f, y + size),
						new PointF(x + 6f, y + size - 4f),
						new PointF(x, y + size)
					});
					g.DrawLine(pen2, x + 6f, y, x + 6f, y + size - 4f);
					g.DrawLine(pen2, x + 13f, y + 4f, x + 13f, y + size);
					break;
				case 1:
					g.DrawLine(pen2, x, y + size, x + size, y + size);
					g.DrawLines(pen2, new PointF[4]
					{
						new PointF(x + 2f, y + 14f),
						new PointF(x + 7f, y + 9f),
						new PointF(x + 12f, y + 12f),
						new PointF(x + 19f, y + 3f)
					});
					g.DrawLines(pen2, new PointF[3]
					{
						new PointF(x + 14f, y + 3f),
						new PointF(x + 19f, y + 3f),
						new PointF(x + 19f, y + 8f)
					});
					break;
				case 2:
					g.DrawLines(pen2, new PointF[7]
					{
						new PointF(x, y + size),
						new PointF(x, y + 9f),
						new PointF(x + 7f, y + 5f),
						new PointF(x + 7f, y + 11f),
						new PointF(x + 14f, y + 7f),
						new PointF(x + 14f, y + size),
						new PointF(x, y + size)
					});
					g.DrawRectangle(pen2, x + 15f, y, 4f, size);
					g.DrawLine(pen2, x + 4f, y + 15f, x + 5f, y + 15f);
					g.DrawLine(pen2, x + 10f, y + 15f, x + 11f, y + 15f);
					break;
				case 3:
					g.DrawEllipse(pen2, x + 4f, y + 1f, 12f, 12f);
					g.DrawLine(pen2, x + 7f, y + 13f, x + 7f, y + 17f);
					g.DrawLine(pen2, x + 13f, y + 13f, x + 13f, y + 17f);
					g.DrawLine(pen2, x + 7f, y + 18f, x + 13f, y + 18f);
					g.DrawLine(pen2, x + 9f, y + 21f, x + 11f, y + 21f);
					break;
				case 4:
					g.DrawEllipse(pen2, x, y, size, size);
					g.DrawEllipse(pen2, x + 6f, y, 8f, size);
					g.DrawLine(pen2, x, y + size / 2f, x + size, y + size / 2f);
					break;
				case 5:
					g.DrawPolygon(pen2, new PointF[5]
					{
						new PointF(x, y + 2f),
						new PointF(x + size, y + 2f),
						new PointF(x + size - 2f, y + 15f),
						new PointF(x + size / 2f, y + size),
						new PointF(x + 2f, y + 15f)
					});
					g.DrawLine(pen2, x + 5f, y + 6f, x + 15f, y + 13f);
					g.DrawLine(pen2, x + 15f, y + 6f, x + 5f, y + 13f);
					break;
				case 6:
					g.DrawEllipse(pen2, x + 1f, y + 12f, 18f, 7f);
					g.DrawLine(pen2, x + 3f, y + 10f, x + 17f, y + 10f);
					g.DrawRectangle(pen2, x + 7f, y + 5f, 8f, 5f);
					g.DrawLine(pen2, x + 14f, y + 6f, x + 22f, y + 6f);
					break;
				case 7:
				{
					g.DrawLines(pen2, new PointF[3]
					{
						new PointF(x, y + 6f),
						new PointF(x + 10f, y),
						new PointF(x + 20f, y + 6f)
					});
					g.DrawLine(pen2, x, y + 20f, x + 20f, y + 20f);
					for (int j = 0; j < 3; j++)
					{
						g.DrawLine(pen2, x + 3f + (float)(j * 7), y + 8f, x + 3f + (float)(j * 7), y + 17f);
					}
					break;
				}
				default:
				{
					g.DrawRectangle(pen2, x + 2f, y, 16f, 20f);
					for (int i = 0; i < 3; i++)
					{
						g.DrawLine(pen2, x + 6f, y + 5f + (float)(i * 5), x + 14f, y + 5f + (float)(i * 5));
					}
					break;
				}
				}
			}
		}




		private Color Mix(Color a, Color b, float t)
		{
			return Color.FromArgb((int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
		}

		private string mapRasterProfile;
        private void DrawMapTerrain(Graphics g)
        {
            if (layer == "Армии") {
                if (militaryIntensity == null) militaryIntensity = new double[W.Regions.Length];
                Array.Clear(militaryIntensity, 0, militaryIntensity.Length);
                foreach (var unit in E.S.Armies) militaryIntensity[unit.Region] += unit.Strength;
            }
			var terrainWatch = System.Diagnostics.Stopwatch.StartNew();
            GraphicsState gstate = g.Save();
			g.SetClip(mapRect);
			using (LinearGradientBrush brush = new LinearGradientBrush(mapRect, Color.FromArgb(142, 163, 165), Color.FromArgb(104, 132, 142), 90f))
			{
				g.FillRectangle(brush, mapRect);
			}
			for (int i = -180; i <= 180; i += 10)
			{
				PointF pointF = Screen(i, 0.0);
				Line(g, Color.FromArgb(45, 45, 77, 85), pointF.X, mapRect.Top, pointF.X, mapRect.Bottom);
			}
			for (int j = -80; j <= 80; j += 10)
			{
				PointF pointF2 = Screen(0.0, j);
				Line(g, Color.FromArgb(45, 45, 77, 85), mapRect.Left, pointF2.Y, mapRect.Right, pointF2.Y);
			}
			RectangleF rect = new RectangleF(cx - mapRect.Width / scale / 2f, cy - mapRect.Height / scale / 2f, mapRect.Width / scale, mapRect.Height / scale);
            RectangleF clippedView = rect; clippedView.Inflate(10 / scale, 10 / scale);
            var clippedPaths = new Dictionary<Land, GraphicsPath>();
            Func<Land, GraphicsPath> visiblePath = land => {
                if (clippedView.Contains(land.Bounds)) return land.DisplayPath(scale);
                GraphicsPath path;
                if (!clippedPaths.TryGetValue(land, out path)) {
                    path = land.ClipDisplayPath(clippedView, scale); clippedPaths.Add(land, path);
                }
                return path;
            };
			using (Matrix transform = new Matrix(scale * UiScale, 0f, 0f, scale * UiScale, (mapRect.X + mapRect.Width / 2f - cx * scale) * UiScale, (mapRect.Y + mapRect.Height / 2f - cy * scale) * UiScale))
			{
				Matrix transform2 = g.Transform;
				g.Transform = transform;
				var rasterWatch = System.Diagnostics.Stopwatch.StartNew();
                CountryInfo[] countries = W.Countries;

				mapRasterProfile = "coast=" + rasterWatch.Elapsed.TotalMilliseconds.ToString("0.0"); rasterWatch.Restart();
                for (int l = 0; l < W.Countries.Length; l++)
				{
					CountryInfo countryInfo2 = W.Countries[l];
					if (countryInfo2.Bounds.IntersectsWith(rect))
					{
						using (SolidBrush brush2 = new SolidBrush(CountryColor(l)))
						{
							if (visiblePath(countryInfo2).PointCount > 0) g.FillPath(brush2, visiblePath(countryInfo2));
						}
					}
				}
				mapRasterProfile += " country=" + rasterWatch.Elapsed.TotalMilliseconds.ToString("0.0"); rasterWatch.Restart();
                using (var provinceBorders = new GraphicsPath()) {
                if (scale > 4f || layer != "Политика" || E.S.Provinces.Where((p, i) => p.Owner != W.Regions[i].Country).Any())
				{
					for (int m = 0; m < W.Regions.Length; m++)
					{
						RegionInfo regionInfo = W.Regions[m];
						if (!regionInfo.Bounds.IntersectsWith(rect) || (scale <= 4f && layer == "Политика" && E.S.Provinces[m].Owner == regionInfo.Country))
						{
							continue;
						}
                        // The national base already covers unoccupied political provinces.
                        // Avoid repainting it thousands of times for imperceptible shade variation.
                        if (layer != "Политика" || E.S.Provinces[m].Owner != regionInfo.Country) {
                            using (var brush = new SolidBrush(RegionColor(m))) {
                                var path = visiblePath(regionInfo); if (path.PointCount > 0) g.FillPath(brush, path);
                            }
                        }
                        if (prefs.RegionBorders && scale > 18f && regionInfo.Bounds.Width * regionInfo.Bounds.Height * scale * scale >= 35) {
                            var borderPath = visiblePath(regionInfo);
                            if (borderPath.PointCount > 0) provinceBorders.AddPath(borderPath, false);
                        }
					}
				}
                g.SmoothingMode = SmoothingMode.None;
                using (var borderPen = new Pen(Color.FromArgb(64, 64, 79, 76), 0.65f / scale))
                    if (provinceBorders.PointCount > 0) g.DrawPath(borderPen, provinceBorders);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                }
				mapRasterProfile += " provinces=" + rasterWatch.Elapsed.TotalMilliseconds.ToString("0.0"); rasterWatch.Restart();
                countries = W.Countries;
				foreach (CountryInfo countryInfo3 in countries)
				{
					if (countryInfo3.Bounds.IntersectsWith(rect))
					{
						using (Pen pen3 = new Pen(Color.FromArgb(91, 103, 91), 1.1f / scale))
						{
							if (visiblePath(countryInfo3).PointCount > 0) g.DrawPath(pen3, visiblePath(countryInfo3));
						}
					}
				}
				mapRasterProfile += " borders=" + rasterWatch.Elapsed.TotalMilliseconds.ToString("0.0");
                g.Transform = transform2;
				transform2.Dispose();
			}
			foreach (var clipped in clippedPaths.Values) clipped.Dispose();
            mapGeometryMs = terrainWatch.Elapsed.TotalMilliseconds; terrainWatch.Restart();
            if (prefs.Texture)
			{
				using (TextureBrush brush5 = new TextureBrush(terrainTexture, WrapMode.Tile))
				{
					g.FillRectangle(brush5, mapRect);
				}
			}
			mapTextureMs = terrainWatch.Elapsed.TotalMilliseconds; terrainWatch.Restart();
            List<RectangleF> list = new List<RectangleF>();
			if (prefs.Labels)
			{
				RectangleF bounds = default(RectangleF);
				foreach (int item in from num8 in Enumerable.Range(0, W.Countries.Length)
					orderby (num8 != E.S.Player) ? ((double)(W.Countries[num8].Bounds.Width * W.Countries[num8].Bounds.Height)) : double.MaxValue descending
					select num8)
				{
					CountryInfo countryInfo4 = W.Countries[item];
					PointF pt = Screen(countryInfo4.X, countryInfo4.Y);
					if (mapRect.Contains(pt) && !(countryInfo4.Bounds.Width * scale < 74f) && !(countryInfo4.Bounds.Height * scale < 27f))
					{
						string s = countryInfo4.Name.ToUpperInvariant();
						float num = ((item == E.S.Player) ? 15 : 12);
						SizeF sizeF = g.MeasureString(s, BookFont(num, bold: true));
						bounds = new RectangleF(pt.X - sizeF.Width / 2f - 8f, pt.Y - 3f, sizeF.Width + 16f, num + 13f);
						if (!list.Any((RectangleF b) => b.IntersectsWith(bounds)) && !((double)sizeF.Width > (double)(countryInfo4.Bounds.Width * scale) * 1.35))
						{
							list.Add(bounds);
							Book(g, s, pt.X - sizeF.Width / 2f + 1f, pt.Y + 1f, num, Color.FromArgb(125, 246, 243, 221), bold: true);
							Book(g, s, pt.X - sizeF.Width / 2f, pt.Y, num, Color.FromArgb(52, 66, 59), bold: true);
						}
					}
				}
				if (scale > 25f)
				{
					RegionInfo[] regions = W.Regions;
					RectangleF bounds2 = default(RectangleF);
					foreach (RegionInfo regionInfo2 in regions)
					{
						if (!regionInfo2.Bounds.IntersectsWith(rect) || regionInfo2.Bounds.Width * scale < 100f || regionInfo2.Bounds.Height * scale < 44f)
						{
							continue;
						}
						PointF pointF3 = Screen(regionInfo2.X, regionInfo2.Y);
						SizeF sizeF2 = g.MeasureString(regionInfo2.Name, F(11f));
						bounds2 = new RectangleF(pointF3.X - 3f, pointF3.Y + 9f, Math.Min(sizeF2.Width, 150f) + 8f, 22f);
						if (!list.Any((RectangleF b) => b.IntersectsWith(bounds2)))
						{
							list.Add(bounds2);
							using (SolidBrush brush6 = new SolidBrush(Color.FromArgb(70, 83, 72)))
							{
								g.FillEllipse(brush6, pointF3.X - 2f, pointF3.Y + 17f, 3f, 3f);
							}
							Txt(g, regionInfo2.Name, pointF3.X + 5f, pointF3.Y + 9f, 11f, Color.FromArgb(66, 80, 68), bold: false, 150f);
						}
					}
				}
			}
            mapLabelsMs = terrainWatch.Elapsed.TotalMilliseconds;
            g.Restore(gstate);
        }

        private void DrawMap(Graphics g)
        {
            var gstate = g.Save();
            g.SetClip(mapRect);
            DrawCachedMap(g);
            using (var transform = new Matrix(scale * UiScale, 0, 0, scale * UiScale,
                (mapRect.X + mapRect.Width / 2 - cx * scale) * UiScale,
                (mapRect.Y + mapRect.Height / 2 - cy * scale) * UiScale)) {
                var highlightState = g.Save();
                g.Transform = transform;
                if (hoveredRegion >= 0 && hoveredRegion != selected) {
                    using (var highlight = new SolidBrush(Color.FromArgb(65, 255, 245, 199))) g.FillPath(highlight, W.Regions[hoveredRegion].Path);
                    using (var border = new Pen(Color.FromArgb(85, 91, 70), 1.6f / scale)) g.DrawPath(border, W.Regions[hoveredRegion].Path);
                }
				if (selected >= 0)
				{
					using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(43, 246, 238, 181)))
					{
						g.FillPath(brush4, W.Regions[selected].Path);
					}
					using (Pen pen4 = new Pen(Color.FromArgb(120, 100, 98, 64), 4f / scale))
					{
						g.DrawPath(pen4, W.Regions[selected].Path);
					}
					using (Pen pen5 = new Pen(Color.FromArgb(99, 93, 54), 1.6f / scale))
					{
						g.DrawPath(pen5, W.Regions[selected].Path);
					}
				}
                DrawFrontRegions(g);
                g.Restore(highlightState);
            }
			foreach (Army army in E.S.Armies)
			{
				if (army.Owner != E.S.Player && !E.AtWar(army.Owner, E.S.Player) && scale < 25f)
				{
					continue;
				}
				RegionInfo regionInfo3 = W.Regions[army.Region];
				PointF pointF4 = Screen(regionInfo3.X, regionInfo3.Y);
				if (army.Target >= 0)
				{
					PointF pt2 = Screen(W.Regions[army.Target].X, W.Regions[army.Target].Y);
					double num2 = 1.0 - Rules.Clamp(army.Days / army.TotalDays, 0.0, 1.0);
					pointF4 = new PointF(pointF4.X + (pt2.X - pointF4.X) * (float)num2, pointF4.Y + (pt2.Y - pointF4.Y) * (float)num2);
					if (army.Owner == E.S.Player)
					{
						Pen pen6 = new Pen(Palette.Gold, 1.7f);
						pen6.DashStyle = DashStyle.Dash;
						pen6.CustomEndCap = new AdjustableArrowCap(3f, 4f);
						using (Pen pen7 = pen6)
						{
							g.DrawLine(pen7, pointF4, pt2);
                            PointF legStart = pt2;
                            foreach (int stop in army.Route) {
                                PointF legEnd = Screen(W.Regions[stop].X, W.Regions[stop].Y);
                                g.DrawLine(pen7, legStart, legEnd); legStart = legEnd;
                            }
						}
					}
				}
				if (mapRect.Contains(pointF4))
				{
					bool flag = army.Owner == E.S.Player;
					RectangleF rectangleF = new RectangleF(pointF4.X - 26f, pointF4.Y - 14f, 61f, 30f);
					Round(g, (army.Id == this.army) ? Color.FromArgb(204, 185, 135) : Color.FromArgb(18, 32, 38), rectangleF, 4f);
					RoundBorder(g, (army.Id == this.army) ? Palette.Gold : Color.FromArgb(92, 113, 111), rectangleF, 4f, 1f);
					NavIcon(g, 6, rectangleF.X + 5f, rectangleF.Y + 5f, 17f, (army.Id == this.army) ? Palette.Bg : (flag ? Palette.Teal : (E.AtWar(E.S.Player, army.Owner) ? Palette.Red : Palette.Muted)));
					Txt(g, army.Id.ToString(), rectangleF.X + 29f, rectangleF.Y + 6f, 12f, (army.Id == this.army) ? Palette.Bg : Palette.Text, bold: true);
					Progress(g, rectangleF.X + 3f, rectangleF.Bottom - 3f, 55f, army.Strength / 100.0, flag ? Palette.Teal : (E.AtWar(E.S.Player, army.Owner) ? Palette.Red : Palette.Muted));
					int id = army.Id;
					int ri = army.Region;
					hits.Add(new Hit
					{
						Rect = RectangleF.Intersect(rectangleF, mapRect),
						Action = delegate
						{
							this.army = id;
							selected = ri;
							foreign = E.S.Provinces[ri].Owner;
							Invalidate();
						},
						Tip = "Соединение №" + id + ". Выберите его, затем нажмите правой кнопкой по области назначения."
					});
				}
			}
            DrawBattleMarkers(g);
            DrawAirOperations(g);
            g.Restore(gstate);
        }


		private void DrawTooltip(Graphics g)
		{
			if (prefs.Tooltips && !(hoveredTip == "") && !(DateTime.Now.Subtract(hoverSince).TotalMilliseconds < 550.0) && !showHelp)
			{
				float num = 290f;
				float num2 = Math.Min(VW - num - 12f, mouse.X + 18);
				float num3 = mouse.Y + 24;
				float num4 = Math.Min(110f, g.MeasureString(hoveredTip, F(13f), (int)(num - 26f)).Height + 25f);
				if (num3 + num4 > VH - 15f)
				{
					num3 = (float)mouse.Y - num4 - 10f;
				}
				Round(g, Color.FromArgb(249, 37, 50, 57), new RectangleF(num2, num3, num, num4), 6f);
				RoundBorder(g, Color.FromArgb(111, 122, 105), new RectangleF(num2, num3, num, num4), 6f, 1f);
				Para(g, hoveredTip, num2 + 13f, num3 + 11f, num - 26f, num4 - 16f, 13f, Palette.Text);
			}
		}

		private void Section(Graphics g, string name, float x, float y, float w)
		{
			Txt(g, name, x, y, 11f, Palette.Gold, bold: true);
			Line(g, Palette.Line, x, y + 26f, x + w, y + 26f);
		}

        private void AvailableButton(Graphics g, string label, float x, float y, float w, Action action, bool enabled, string reason, bool active = false, float h = 34f)
        {
            if (enabled) { Button(g, label, x, y, w, action, active, reason, h); return; }
            Fill(g, Palette.Panel, x, y, w, h);
            Line(g, Palette.Line, x, y + h, x + w, y + h);
            Txt(g, label, x + 10, y + (h - 18) / 2, 13, Palette.Muted, false, w - 18);
            hits.Add(new Hit { Rect = new RectangleF(x, y, w, h), Label = label, Action = () => Message(reason), Tip = reason });
        }

		private void DrawOverview(Graphics g)
		{
			Nation n = E.Player;
			float num = sidebar + 34;
			float num2 = VW - (float)sidebar - 68f;
			float num3 = num2 * 0.6f;
			float num4 = num + num3 + 38f;
			float num5 = num2 - num3 - 38f;
			PageTitle(g, "Положение страны", W.Countries[E.S.Player].Name + " · " + E.S.Date.ToString("dd.MM.yyyy") + " · время можно остановить в любой момент");
			Section(g, "ГОСУДАРСТВЕННЫЕ ФИНАНСЫ", num, 222f, num3);
			double num6 = n.Revenue - n.Spending;
			Txt(g, ((num6 >= 0.0) ? "+" : "") + Money(num6), num, 269f, 38f, (num6 >= 0.0) ? Palette.Teal : Palette.Red, bold: true);
			Txt(g, (num6 >= 0.0) ? "профицит за неделю" : "дефицит за неделю", num + 2f, 319f, 14f, Palette.Muted);
			float num7 = num + num3 * 0.52f;
			float num8 = num3 * 0.48f;
			double num9 = Math.Max(0.01, Math.Max(n.Revenue, n.Spending));
			Txt(g, "Доходы", num7, 271f, 13f, Palette.Muted);
			Txt(g, Money(n.Revenue), num7 + num8 - 128f, 271f, 14f, Palette.Text, bold: true);
			Progress(g, num7, 296f, num8, n.Revenue / num9, Palette.Teal);
			Txt(g, "Расходы", num7, 315f, 13f, Palette.Muted);
			Txt(g, Money(n.Spending), num7 + num8 - 128f, 315f, 14f, Palette.Text, bold: true);
			Progress(g, num7, 340f, num8, n.Spending / num9, Palette.Gold);
			Txt(g, "Резервы: " + Money(n.Treasury) + "     Долг: " + Money(n.Debt), num, 367f, 14f, Palette.Text);
			Button(g, "Изменить налоги и расходы →", num, 403f, 292f, delegate
			{
				Navigate("Политика");
			});
			Section(g, "ПРОИЗВОДСТВО И ПОТРЕБЛЕНИЕ", num, 475f, num3);
			int[] array = (from k in Enumerable.Range(0, 8)
				orderby n.Production[k] - n.Demand[k]
				select k).Take(4).ToArray();
			for (int num10 = 0; num10 < array.Length; num10++)
			{
				int num11 = array[num10];
				float num12 = 521 + num10 * 43;
				double num13 = n.Production[num11] - n.Demand[num11];
				Txt(g, Rules.Goods[num11], num, num12, 15f, Palette.Text);
				Txt(g, ((num13 >= 0.0) ? "+" : "") + Num(num13) + " ед. / нед.", num + num3 - 153f, num12, 14f, (num13 < 0.0) ? Palette.Gold : Palette.Teal, bold: true);
				Line(g, Palette.Line, num, num12 + 32f, num + num3, num12 + 32f);
			}
			Para(g, "Нехватку собственного выпуска может покрывать импорт. Остатки на складе и торговлю смотрите в экономике.", num, 702f, num3, 48f, 13f);
			Button(g, "Открыть рынки →", num, 759f, 210f, delegate
			{
				Navigate("Экономика");
			});
			Section(g, "ДЕЛА ГОСУДАРСТВА", num4, 222f, num5);
			Txt(g, "Научная программа", num4, 272f, 13f, Palette.Muted);
			Txt(g, (n.Research < 0) ? "Не выбрана" : Rules.Techs[n.Research], num4, 299f, 22f, Palette.Text, bold: true, num5);
			if (n.Research >= 0)
			{
				Progress(g, num4, 339f, num5, n.ResearchProgress / 100.0, Palette.Teal);
				Txt(g, Num(n.ResearchProgress) + "% выполнено", num4, 351f, 12f, Palette.Muted);
			}
			else
			{
				Para(g, "Научный бюджет выделен. Выберите направление, чтобы начать исследование.", num4, 335f, num5, 50f);
			}
			Button(g, (n.Research < 0) ? "Выбрать программу →" : "Ход исследования →", num4, 403f, Math.Min(275f, num5), delegate
			{
				Navigate("Исследования");
			});
			int num14 = E.S.Projects.Count((Construction p) => p.Owner == E.S.Player);
			List<Army> list = E.S.Armies.Where((Army a) => a.Owner == E.S.Player).ToList();
			Section(g, "В РАБОТЕ", num4, 475f, num5);
			Txt(g, num14 + " строек", num4, 522f, 22f, Palette.Text, bold: true);
			Txt(g, "Частные и государственные проекты", num4, 555f, 13f, Palette.Muted, bold: false, num5);
			Txt(g, list.Count + " соединения", num4, 605f, 22f, Palette.Text, bold: true);
			Txt(g, "Среднее снабжение: " + Num((list.Count == 0) ? 0.0 : list.Average((Army a) => a.Supply)) + "%", num4, 638f, 13f, Palette.Muted);
			Txt(g, "Одобрение правительства", num4, 697f, 13f, Palette.Muted);
			Txt(g, Num(n.Approval) + "%", num4 + num5 - 78f, 693f, 22f, (n.Approval < 35.0) ? Palette.Red : Palette.Text, bold: true);
			Progress(g, num4, 729f, num5, n.Approval / 100.0, Palette.Teal);
			Button(g, "Вернуться к карте →", num4, 759f, Math.Min(275f, num5), delegate
			{
				Navigate("Карта");
			});
		}

		private void DrawConstruction(Graphics g)
		{
			float num = sidebar + 34;
			float num2 = VW - (float)sidebar - 68f;
			float num3 = num2 * 0.69f;
			float x = num + num3 + 28f;
			float num4 = num2 - num3 - 28f;
			int ri = OwnRegion();
			Province province = E.S.Provinces[ri];
			PageTitle(g, "Предприятия и строительство", W.Regions[ri].Name + " · государственные заказы в выбранной области");
			Button(g, "Выбрать область на карте", num, 217f, 252f, delegate
			{
				selected = ri;
				cx = (float)W.Regions[ri].X;
				cy = (float)W.Regions[ri].Y;
				scale = 27f;
				Navigate("Карта");
			});
			Txt(g, "Казна: " + Money(E.Player.Treasury), num + num3 - 215f, 226f, 14f, Palette.Gold, bold: true);
			Section(g, "ПРЕДПРИЯТИЕ", num, 282f, num3);
			Txt(g, "ГОС. / ЧАСТНЫЕ", num + num3 * 0.42f, 282f, 10f, Palette.Muted, bold: true);
			Txt(g, "ЗАКАЗ", num + num3 - 150f, 282f, 10f, Palette.Muted, bold: true);
			for (int num5 = 0; num5 < 8; num5++)
			{
				int sector = num5;
				float num6 = 324 + num5 * 55;
				Txt(g, Rules.Plants[num5], num, num6, 16f, Palette.Text, bold: true, num3 * 0.41f);
				Txt(g, Rules.Output[num5] + " ед. / нед. на мощность", num, num6 + 23f, 12f, Palette.Muted, bold: false, num3 * 0.41f);
				Txt(g, Num(province.Public[num5]) + " / " + Num(province.Private[num5]), num + num3 * 0.42f, num6 + 10f, 14f, Palette.Text);
				AvailableButton(g, "+ " + Rules.BuildCost[num5].ToString("0.00") + " млрд", num + num3 - 152f, num6 + 3f, 152f, delegate
				{
					Message(E.Build(ri, sector));
				}, E.Player.Treasury >= Rules.BuildCost[num5], (E.Player.Treasury >= Rules.BuildCost[num5]) ? "Заказать одну мощность. Стоимость списывается сразу, предприятие строится со временем." : "Недостаточно средств в казне для этого заказа.");
				Line(g, Palette.Line, num, num6 + 48f, num + num3, num6 + 48f);
			}
			Section(g, "РАЗВИТИЕ ОБЛАСТИ", x, 282f, num4);
			Txt(g, "Инфраструктура", x, 326f, 17f, Palette.Text, bold: true);
			Txt(g, Num(province.Infrastructure) + " / 100", x, 354f, 14f, Palette.Gold);
			Progress(g, x, 383f, num4, province.Infrastructure / 100.0, Palette.Teal);
			AvailableButton(g, "Улучшить · 0,30 млрд", x, 405f, num4, delegate
			{
				Message(E.Build(ri, 8));
			}, E.Player.Treasury >= 0.3 && province.Infrastructure < 100.0, (province.Infrastructure >= 100.0) ? "Инфраструктура уже развита до максимума." : "Строительство повышает инфраструктуру на 12 пунктов.");
			AvailableButton(g, "Помочь бизнесу · 0,20 млрд", x, 453f, num4, delegate
			{
				Message(E.Subsidize(ri));
			}, E.Player.Treasury >= 0.3, "Средства передаются частному сектору. Компании сами выбирают, куда инвестировать.");
			Section(g, "ОЧЕРЕДЬ СТРОИТЕЛЬСТВА", x, 523f, num4);
			List<Construction> list = E.S.Projects.Where((Construction j) => j.Region == ri).ToList();
			if (list.Count == 0)
			{
				Para(g, "Заказов пока нет. Выберите предприятие слева. Частный бизнес может начать стройку самостоятельно.", x, 567f, num4, 105f, 15f);
			}
			else
			{
				for (int num7 = 0; num7 < Math.Min(3, list.Count); num7++)
				{
					Construction construction = list[num7];
					float num8 = 566 + num7 * 64;
					Txt(g, (construction.Sector == 8) ? "Инфраструктура" : (construction.Sector == 9 ? "Аэродром" : Rules.Plants[construction.Sector]), x, num8, 14f, Palette.Text, bold: true, num4);
					Txt(g, (construction.Private ? "Частная" : "Государственная") + " · " + construction.Days + " дн.", x, num8 + 23f, 12f, Palette.Muted);
					Progress(g, x, num8 + 44f, num4, 1.0 - (double)construction.Days / (double)Math.Max(1, construction.Total), Palette.Teal);
				}
			}
			if (list.Count > 3)
			{
				Txt(g, "Ещё проектов: " + (list.Count - 3), x, 770f, 12f, Palette.Muted);
			}
		}


		private Font BookFont(float size, bool bold = false)
		{
			string key = "book/" + size + "/" + bold;
			if (!fonts.ContainsKey(key))
			{
				fonts[key] = new Font("Georgia", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
			}
			return fonts[key];
		}

		private void Book(Graphics g, string s, float x, float y, float size, Color color, bool bold = false, float width = 0f)
		{
			using (SolidBrush brush = new SolidBrush(color))
			{
				StringFormat stringFormat = new StringFormat();
				stringFormat.Trimming = StringTrimming.EllipsisCharacter;
				stringFormat.FormatFlags = StringFormatFlags.NoWrap;
				using (StringFormat format = stringFormat)
				{
					g.DrawString(s, BookFont(size, bold), brush, new RectangleF(x, y, (width > 0f) ? width : VW, size + 16f), format);
				}
			}
		}

		private void HairlineFrame(Graphics g, RectangleF r, Color color)
		{
			using (Pen pen = new Pen(color, 1f))
			{
				g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
			}
			float num = 8f;
			PointF[] array = new PointF[4]
			{
				new PointF(r.Left, r.Top),
				new PointF(r.Right, r.Top),
				new PointF(r.Left, r.Bottom),
				new PointF(r.Right, r.Bottom)
			};
			for (int i = 0; i < array.Length; i++)
			{
				PointF pointF = array[i];
				Line(g, color, pointF.X - num, pointF.Y, pointF.X + num, pointF.Y);
				Line(g, color, pointF.X, pointF.Y - num, pointF.X, pointF.Y + num);
			}
		}

		private void Compass(Graphics g, float x, float y)
		{
			Color color = Color.FromArgb(115, 36, 51, 55);
			using (Pen pen = new Pen(color, 1f))
			{
				g.DrawEllipse(pen, x - 24f, y - 24f, 48f, 48f);
				g.DrawEllipse(pen, x - 18f, y - 18f, 36f, 36f);
				g.DrawLine(pen, x - 30f, y, x + 30f, y);
				g.DrawLine(pen, x, y - 30f, x, y + 30f);
			}
			using (SolidBrush brush = new SolidBrush(color))
			{
				g.FillPolygon(brush, new PointF[4]
				{
					new PointF(x, y - 25f),
					new PointF(x + 6f, y + 7f),
					new PointF(x, y + 3f),
					new PointF(x - 6f, y + 7f)
				});
			}
			Book(g, "N", x - 6f, y - 46f, 13f, color, bold: true, 25f);
		}


	}
}
