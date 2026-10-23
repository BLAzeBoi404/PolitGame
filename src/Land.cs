using System.Drawing;
using System.Drawing.Drawing2D;

namespace Sovereign
{
	public class Land
	{
		public string Name;

		public double X;

		public double Y;

		public double Population;

		public PointF[][] Rings;

		public GraphicsPath Path;

		public RectangleF Bounds;

		public void Prepare()
		{
			Path = new GraphicsPath(FillMode.Winding);
			PointF[][] rings = Rings;
			foreach (PointF[] array in rings)
			{
				if (array.Length >= 3)
				{
					Path.AddPolygon(array);
				}
			}
			Bounds = Path.GetBounds();
		}
	}
}
