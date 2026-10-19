using UnityEngine;

namespace Pulse
{
    // Colours from gui-style.md. Pulse.uss holds the same values for style sheets.
    public static class Tokens
    {
        public static readonly Color Soot = Hex(0x14171C);
        public static readonly Color Plate = Hex(0x1F242B);
        public static readonly Color Plate2 = Hex(0x2A313A);
        public static readonly Color Seam = Hex(0x3D4752);
        public static readonly Color Chalk = Hex(0xE8E4DA);
        public static readonly Color Dust = Hex(0x9AA3AD);
        public static readonly Color PulseGreen = Hex(0x5CF2A0);
        public static readonly Color Heat = Hex(0xFF5A3C);
        public static readonly Color Shock = Hex(0xFFD93B);
        public static readonly Color Cryo = Hex(0x5CD6FF);
        public static readonly Color Oil = Hex(0xA77BFF);
        public static readonly Color Magnet = Hex(0xFF5FB4);
        // Not an interface colour: the pale amber enemies use in the world for every warning they give.
        public static readonly Color Alert = new Color(1f, 0.82f, 0.55f);

        public static Color Of(StateType state) => state switch
        {
            StateType.Heat => Heat,
            StateType.Shock => Shock,
            StateType.Cryo => Cryo,
            StateType.Oil => Oil,
            StateType.Magnet => Magnet,
            _ => Chalk,
        };

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
