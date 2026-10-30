using System.Drawing;

namespace Sovereign
{
    public static class Palette
    {
        // Command desk: graphite surfaces, warm white type, brass for selection
        // and teal for healthy operational state. Map colors remain geographic.
        public static readonly Color Bg = Color.FromArgb(13, 15, 21);
        public static readonly Color Panel = Color.FromArgb(21, 23, 30);
        public static readonly Color Card = Color.FromArgb(31, 34, 43);
        public static readonly Color Hover = Color.FromArgb(46, 48, 59);
        public static readonly Color Selected = Color.FromArgb(67, 30, 42);
        public static readonly Color Line = Color.FromArgb(70, 73, 86);
        public static readonly Color Text = Color.FromArgb(235, 239, 242);
        public static readonly Color Muted = Color.FromArgb(164, 180, 194);
        public static readonly Color Gold = Color.FromArgb(226, 195, 129);
        public static readonly Color Teal = Color.FromArgb(105, 205, 183);
        public static readonly Color Red = Color.FromArgb(245, 145, 139);
        public static readonly Color Accent = Color.FromArgb(190, 61, 83);
        public static readonly Color AccentText = Color.FromArgb(242, 157, 167);
        public static readonly Color Sea = Color.FromArgb(16, 29, 52);
        public static readonly Color Paper = Color.FromArgb(216, 220, 205);
        public static readonly Color Ink = Color.FromArgb(47, 66, 69);
        public static readonly Color[] Nations = {
            Color.FromArgb(159, 125, 62), Color.FromArgb(64, 120, 86), Color.FromArgb(153, 64, 77),
            Color.FromArgb(65, 105, 158), Color.FromArgb(146, 83, 126), Color.FromArgb(161, 146, 75),
            Color.FromArgb(55, 128, 130), Color.FromArgb(115, 85, 152), Color.FromArgb(104, 130, 65),
            Color.FromArgb(164, 97, 63), Color.FromArgb(88, 104, 136), Color.FromArgb(123, 130, 141)
        };
    }
}
