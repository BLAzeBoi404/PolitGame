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

		public static string PathName => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

		public static Preferences Read()
		{
			try
			{
				using (FileStream stream = File.OpenRead(PathName))
				{
					var result = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(stream);
                    result.NormalizeBindings();
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
            NormalizeBindings();
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
