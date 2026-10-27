using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;

namespace Sovereign
{
	public partial class World : IDisposable
	{
		public CountryInfo[] Countries;

		public RegionInfo[] Regions;
        private GeographicIndex regionIndex, countryIndex;
        public int FindRegion(PointF point) => (regionIndex ?? (regionIndex = new GeographicIndex(Regions))).Find(point);
        public int FindCountry(PointF point) => (countryIndex ?? (countryIndex = new GeographicIndex(Countries))).Find(point);

		private static string ReadString(BinaryReader b)
		{
			return Encoding.UTF8.GetString(b.ReadBytes(b.ReadInt32()));
		}

		private static PointF[][] ReadRings(BinaryReader b)
		{
			PointF[][] array = new PointF[b.ReadInt32()][];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new PointF[b.ReadInt32()];
				for (int j = 0; j < array[i].Length; j++)
				{
					array[i][j] = new PointF(b.ReadSingle(), 0f - b.ReadSingle());
				}
			}
			return array;
		}

		public static World Load()
		{
			World world = new World();
			using (BinaryReader binaryReader = new BinaryReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("world.bin")))
			{
				if (binaryReader.ReadInt32() != 1)
				{
					throw new Exception("Неизвестная версия карты");
				}
				world.Countries = new CountryInfo[binaryReader.ReadInt32()];
				for (int i = 0; i < world.Countries.Length; i++)
				{
					CountryInfo countryInfo = new CountryInfo();
					countryInfo.Code = ReadString(binaryReader);
					countryInfo.Name = ReadString(binaryReader);
					countryInfo.Population = binaryReader.ReadDouble();
					countryInfo.GDP = binaryReader.ReadDouble();
					countryInfo.X = binaryReader.ReadDouble();
					countryInfo.Y = 0.0 - binaryReader.ReadDouble();
					countryInfo.Rings = ReadRings(binaryReader);
					CountryInfo countryInfo2 = countryInfo;
					countryInfo2.Prepare();
					world.Countries[i] = countryInfo2;
				}
				world.Regions = new RegionInfo[binaryReader.ReadInt32()];
				for (int j = 0; j < world.Regions.Length; j++)
				{
					RegionInfo regionInfo = new RegionInfo();
					regionInfo.Country = binaryReader.ReadInt32();
					regionInfo.Name = ReadString(binaryReader);
					regionInfo.Population = binaryReader.ReadDouble();
					regionInfo.X = binaryReader.ReadDouble();
					regionInfo.Y = 0.0 - binaryReader.ReadDouble();
					regionInfo.Rings = ReadRings(binaryReader);
					RegionInfo regionInfo2 = regionInfo;
					regionInfo2.Neighbors = new int[binaryReader.ReadInt32()];
					for (int k = 0; k < regionInfo2.Neighbors.Length; k++)
					{
						regionInfo2.Neighbors[k] = binaryReader.ReadInt32();
					}
					regionInfo2.Prepare();
					world.Regions[j] = regionInfo2;
					world.Countries[regionInfo2.Country].Regions.Add(j);
				}
                world.LoadScenario();
				return world;
			}
		}

        public void Dispose()
        {
            foreach (var country in Countries) country.Dispose();
            foreach (var region in Regions) region.Dispose();
        }

		public int ByCode(string code)
		{
			return Array.FindIndex(Countries, (CountryInfo c) => c.Code == code);
		}
	}
}
