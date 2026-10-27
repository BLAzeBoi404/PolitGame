using System;
using System.Collections.Generic;
using System.Drawing;

namespace Sovereign
{
    // A fixed geographic grid keeps picking independent of total world polygon count.
    // The smallest containing exterior wins for enclave cities in the legacy dataset.
    internal sealed class GeographicIndex
    {
        private const int Columns = 180, Rows = 90;
        private readonly List<int>[] cells = new List<int>[Columns * Rows];
        private readonly Land[] lands;
        internal GeographicIndex(Land[] source)
        {
            lands = source;
            for (int id = 0; id < lands.Length; id++) {
                var b = lands[id].Bounds;
                for (int y = Row(b.Top); y <= Row(b.Bottom); y++) for (int x = Column(b.Left); x <= Column(b.Right); x++) {
                    int cell = y * Columns + x; if (cells[cell] == null) cells[cell] = new List<int>(); cells[cell].Add(id);
                }
            }
            foreach (var cell in cells) if (cell != null) cell.Sort((a,b) => lands[a].Area.CompareTo(lands[b].Area));
        }
        private static int Column(float x) => Math.Max(0, Math.Min(Columns - 1, (int)Math.Floor((x + 180) / 2)));
        private static int Row(float y) => Math.Max(0, Math.Min(Rows - 1, (int)Math.Floor((y + 90) / 2)));
        internal int Find(PointF point)
        {
            if (point.X < -180 || point.X > 180 || point.Y < -90 || point.Y > 90) return -1;
            var cell = cells[Row(point.Y) * Columns + Column(point.X)];
            if (cell != null) foreach (int id in cell) if (lands[id].Contains(point)) return id;
            return -1;
        }
    }
}
