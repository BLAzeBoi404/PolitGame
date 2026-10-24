using System.Drawing;

namespace Sovereign
{
    public static class Palette
    {
        // Night atlas: cool ink, warm paper and a single brass interaction accent.
        public static readonly Color Bg = Color.FromArgb(23, 32, 39);
        public static readonly Color Panel = Color.FromArgb(16, 25, 32);
        public static readonly Color Card = Color.FromArgb(29, 40, 48);
        public static readonly Color Hover = Color.FromArgb(43, 56, 64);
        public static readonly Color Selected = Color.FromArgb(55, 57, 48);
        public static readonly Color Line = Color.FromArgb(60, 76, 85);
        public static readonly Color Text = Color.FromArgb(225, 231, 230);
        public static readonly Color Muted = Color.FromArgb(155, 173, 183);
        public static readonly Color Gold = Color.FromArgb(224, 184, 112);
        public static readonly Color Teal = Color.FromArgb(124, 187, 172);
        public static readonly Color Red = Color.FromArgb(230, 140, 122);
        public static readonly Color Paper = Color.FromArgb(216, 220, 205);
        public static readonly Color Ink = Color.FromArgb(47, 66, 69);
        public static readonly Color[] Nations = {
            Color.FromArgb(191, 190, 159), Color.FromArgb(155, 180, 166), Color.FromArgb(198, 169, 143),
            Color.FromArgb(157, 178, 188), Color.FromArgb(184, 159, 161), Color.FromArgb(199, 190, 148),
            Color.FromArgb(147, 178, 173), Color.FromArgb(172, 164, 187), Color.FromArgb(183, 195, 173),
            Color.FromArgb(196, 178, 171), Color.FromArgb(165, 182, 148), Color.FromArgb(183, 184, 192)
        };
    }
}
