using System;
using System.Drawing;

namespace Sovereign
{
	public class Hit
	{
		public RectangleF Rect;
        public string Label;
        public bool FocusTarget;

		public Action Action;

		public string Tip;
	}
}
