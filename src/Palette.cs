using System.Drawing;

namespace Sovereign
{
    public static class Palette
    {
        // Field atlas: warm ink, aged paper and restrained brass accents.
        public static readonly Color Bg = Color.FromArgb(29, 33, 29);
        public static readonly Color Panel = Color.FromArgb(23, 28, 26);
        public static readonly Color Card = Color.FromArgb(39, 45, 37);
        public static readonly Color Hover = Color.FromArgb(55, 62, 48);
        public static readonly Color Selected = Color.FromArgb(67, 65, 43);
        public static readonly Color Line = Color.FromArgb(78, 85, 65);
        public static readonly Color Text = Color.FromArgb(233, 229, 211);
        public static readonly Color Muted = Color.FromArgb(166, 175, 158);
        public static readonly Color Gold = Color.FromArgb(216, 190, 125);
        public static readonly Color Teal = Color.FromArgb(124, 187, 172);
        public static readonly Color Red = Color.FromArgb(230, 140, 122);
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
