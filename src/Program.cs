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
				using (World world = World.Load())
                {
				if (args.Contains("--verify-save")) {
                    var loaded = new Engine(world) { AutoSaveEnabled = false };
                    loaded.Load(args[args.Length - 1]);
                    for (int day = 0; day < 30; day++) loaded.Tick();
                    loaded.Validate(loaded.S);
                    Console.WriteLine("PASS Supplied save migrated; 30 subsequent days validated; source file unchanged.");
                    return 0;
                }
                if (args.Contains("--test"))
				{
					return Tests.Run(world, (args.Length > 1) ? args[args.Length - 1] : "test-results.txt");
				}
				Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (args.Contains("--ui-test")) return UiTests.Run(world, args[args.Length - 1]);
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
                        gameForm.Show();
                        Application.DoEvents();
                        using (Bitmap bitmap = gameForm.RenderFrame()) bitmap.Save(args[args.Length - 1]);
					}
					return 0;
				}
				GameForm mainForm = new GameForm(world);
				Application.Run(mainForm);
				return 0;
                }
			}
			catch (Exception ex)
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), ex.ToString());
				if (!args.Contains("--test") && !args.Contains("--ui-test") && !args.Contains("--preview") && !args.Contains("--verify-save"))
				{
					MessageBox.Show("Не удалось запустить игру. Подробности записаны в error.log.\n" + ex.Message, "Sovereign");
				}
				Console.Error.WriteLine(ex);
                return 1;
			}
		}
	}
}
