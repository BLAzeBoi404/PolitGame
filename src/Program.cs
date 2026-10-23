using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Sovereign
{
	internal static class Program
	{
		[STAThread]
		private static int Main(string[] args)
		{
			try
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(defaultValue: false);
				World world = World.Load();
				if (args.Contains("--test"))
				{
					return Tests.Run(world, (args.Length > 1) ? args[args.Length - 1] : "test-results.txt");
				}
				if (args.Contains("--preview"))
				{
					using (GameForm gameForm = new GameForm(world))
					{
						gameForm.ClientSize = (args.Contains("--small") ? new Size(1200, 720) : new Size(1600, 950));
						gameForm.CreateControl();
						if (args.Length > 2 && !args[1].StartsWith("--"))
						{
							gameForm.PreviewPage(args[1]);
						}
						using (Bitmap bitmap = new Bitmap(gameForm.Width, gameForm.Height))
						{
							gameForm.DrawToBitmap(bitmap, new Rectangle(0, 0, gameForm.Width, gameForm.Height));
							bitmap.Save(args[args.Length - 1]);
						}
					}
					return 0;
				}
				GameForm mainForm = new GameForm(world);
				Application.Run(mainForm);
				return 0;
			}
			catch (Exception ex)
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), ex.ToString());
				if (!args.Contains("--test"))
				{
					MessageBox.Show("Не удалось запустить игру. Подробности записаны в error.log.\n" + ex.Message, "Sovereign");
				}
				return 1;
			}
		}
	}
}
