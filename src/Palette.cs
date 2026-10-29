using System.Drawing;

namespace Sovereign
{
    public static class Palette
    {
        // Command desk: graphite surfaces, warm white type, brass for selection
        // and teal for healthy operational state. Map colors remain geographic.
        public static readonly Color Bg = Color.FromArgb(17, 23, 30);
        public static readonly Color Panel = Color.FromArgb(22, 30, 39);
        public static readonly Color Card = Color.FromArgb(31, 41, 52);
        public static readonly Color Hover = Color.FromArgb(43, 56, 69);
        public static readonly Color Selected = Color.FromArgb(62, 58, 43);
        public static readonly Color Line = Color.FromArgb(64, 79, 92);
        public static readonly Color Text = Color.FromArgb(235, 239, 242);
        public static readonly Color Muted = Color.FromArgb(164, 180, 194);
        public static readonly Color Gold = Color.FromArgb(226, 195, 129);
        public static readonly Color Teal = Color.FromArgb(105, 205, 183);
        public static readonly Color Red = Color.FromArgb(245, 145, 139);
        public static readonly Color Paper = Color.FromArgb(216, 220, 205);
        public static readonly Color Ink = Color.FromArgb(47, 66, 69);
        public static readonly Color[] Nations = {
            Color.FromArgb(194, 173, 121), Color.FromArgb(146, 176, 137), Color.FromArgb(193, 142, 123),
            Color.FromArgb(129, 161, 183), Color.FromArgb(182, 133, 147), Color.FromArgb(198, 187, 131),
            Color.FromArgb(132, 172, 161), Color.FromArgb(164, 144, 176), Color.FromArgb(169, 186, 157),
            Color.FromArgb(198, 164, 149), Color.FromArgb(160, 166, 116), Color.FromArgb(158, 170, 181)
        };
    }
}
