using System;
using System.IO;
using System.Xml.Serialization;

namespace Sovereign
{
	public partial class Preferences
	{
		public bool Labels = true;

		public bool RegionBorders = true;

		public bool Tooltips = true;

		public bool Autosave = true;

		public bool Tutorial = true;

		public bool Texture = true;

		public bool Fullscreen;
        public int WindowWidth = 1540, WindowHeight = 900, FrameLimit = -1;
        public int Renderer; // 0: GPU when available; 1: compatibility GDI+
        public int GraphicsQuality = 2, LabelDensity = 2;
        public bool PerformanceOverlay;
        public bool OutlinerCollapsed;
        public void NormalizeDisplay()
        {
            WindowWidth = Math.Max(1000, Math.Min(7680, WindowWidth));
            WindowHeight = Math.Max(640, Math.Min(4320, WindowHeight));
            if (FrameLimit != -1 && FrameLimit != 0 && FrameLimit != 30 && FrameLimit != 60 && FrameLimit != 90 && FrameLimit != 120 && FrameLimit != 144 && FrameLimit != 165 && FrameLimit != 240) FrameLimit = 60;
            Renderer = Renderer == 1 ? 1 : 0;
            GraphicsQuality = Math.Max(0, Math.Min(2, GraphicsQuality));
            LabelDensity = Math.Max(0, Math.Min(2, LabelDensity));
        }

		public static string PathName => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

		public static Preferences Read()
		{
			try
			{
				using (FileStream stream = File.OpenRead(PathName))
				{
					var result = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(stream);
                    result.NormalizeBindings(); result.NormalizeDisplay();
                    return result;
				}
			}
			catch
			{
				return new Preferences();
			}
		}

		public void Save()
		{
            NormalizeBindings(); NormalizeDisplay();
			try
			{
				using (FileStream stream = File.Create(PathName))
				{
					new XmlSerializer(typeof(Preferences)).Serialize(stream, this);
				}
			}
			catch
			{
			}
		}
	}
}
