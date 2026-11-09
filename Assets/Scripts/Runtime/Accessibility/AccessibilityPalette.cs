using FallenYard.Settings;
using UnityEngine;

namespace FallenYard.Accessibility
{
    /// <summary>What a colour is used for. Each palette gives every role a colour.</summary>
    public enum PaletteRole
    {
        /// <summary>Ring around a detected pile.</summary>
        Pile,
        /// <summary>Ring around the pile the player can bag right now.</summary>
        PileReady,
        /// <summary>Yard-clean progress bar fill.</summary>
        Progress,
        /// <summary>Progress bar once the goal is reached.</summary>
        ProgressDone,
        /// <summary>Good news text and icons (coins, unlocks).</summary>
        Positive,
        /// <summary>Warnings (hopper full, can't afford).</summary>
        Warning,
    }

    /// <summary>
    /// Colour sets for pile highlights and progress colours. The colour-blind sets use the
    /// Okabe-Ito palette, chosen so that the pairs that matter (pile vs ready pile, progress vs
    /// done, positive vs warning) stay distinct for each type of colour blindness. Highlights
    /// never rely on colour alone: a ready pile also gets a thicker, pulsing ring.
    /// </summary>
    public static class AccessibilityPalette
    {
        // Okabe-Ito
        static readonly Color Orange = Hex(0xE69F00);
        static readonly Color SkyBlue = Hex(0x56B4E9);
        static readonly Color BluishGreen = Hex(0x009E73);
        static readonly Color Yellow = Hex(0xF0E442);
        static readonly Color Blue = Hex(0x0072B2);
        static readonly Color Vermillion = Hex(0xD55E00);
        static readonly Color ReddishPurple = Hex(0xCC79A7);

        // Default autumn palette (matches FallenYardUI.uss)
        static readonly Color Pumpkin = Hex(0xD9792B);
        static readonly Color Moss = Hex(0x7BA65A);
        static readonly Color Gold = Hex(0xE8B23A);
        static readonly Color Brick = Hex(0xC0392B);

        public static Color Get(ColorBlindMode mode, PaletteRole role)
        {
            switch (mode)
            {
                case ColorBlindMode.Deuteranopia:
                case ColorBlindMode.Protanopia:
                    // Red/green confusion: lean on blue vs orange/yellow.
                    return role switch
                    {
                        PaletteRole.Pile => SkyBlue,
                        PaletteRole.PileReady => Yellow,
                        PaletteRole.Progress => Blue,
                        PaletteRole.ProgressDone => Orange,
                        PaletteRole.Positive => SkyBlue,
                        PaletteRole.Warning => Vermillion,
                        _ => Color.white,
                    };
                case ColorBlindMode.Tritanopia:
                    // Blue/yellow confusion: lean on red vs teal.
                    return role switch
                    {
                        PaletteRole.Pile => ReddishPurple,
                        PaletteRole.PileReady => BluishGreen,
                        PaletteRole.Progress => Vermillion,
                        PaletteRole.ProgressDone => BluishGreen,
                        PaletteRole.Positive => BluishGreen,
                        PaletteRole.Warning => Vermillion,
                        _ => Color.white,
                    };
                case ColorBlindMode.HighContrast:
                    return role switch
                    {
                        PaletteRole.Pile => Color.white,
                        PaletteRole.PileReady => Yellow,
                        PaletteRole.Progress => Color.white,
                        PaletteRole.ProgressDone => Yellow,
                        PaletteRole.Positive => Yellow,
                        PaletteRole.Warning => Hex(0xFF4040),
                        _ => Color.white,
                    };
                default:
                    return role switch
                    {
                        PaletteRole.Pile => Pumpkin,
                        PaletteRole.PileReady => Gold,
                        PaletteRole.Progress => Pumpkin,
                        PaletteRole.ProgressDone => Moss,
                        PaletteRole.Positive => Moss,
                        PaletteRole.Warning => Brick,
                        _ => Color.white,
                    };
            }
        }

        /// <summary>USS class put on the UI root for a palette, e.g. "fy-palette--deuteranopia". Null for the default.</summary>
        public static string UssClass(ColorBlindMode mode) =>
            mode == ColorBlindMode.Off ? null : "fy-palette--" + mode.ToString().ToLowerInvariant();

        public static string DisplayName(ColorBlindMode mode) => mode switch
        {
            ColorBlindMode.Off => "Off",
            ColorBlindMode.Deuteranopia => "Deuteranopia (green-weak)",
            ColorBlindMode.Protanopia => "Protanopia (red-weak)",
            ColorBlindMode.Tritanopia => "Tritanopia (blue-weak)",
            ColorBlindMode.HighContrast => "High contrast",
            _ => mode.ToString(),
        };

        /// <summary>
        /// Rough perceptual distance (CIE76 delta-E in Lab) between two colours, as seen through a
        /// simple colour-blindness simulation. Used by tests to check the palettes keep pairs apart.
        /// </summary>
        public static float SimulatedDistance(Color a, Color b, ColorBlindMode mode) =>
            DeltaE(Simulate(a, mode), Simulate(b, mode));

        /// <summary>Machado/Viénot style linear-RGB simulation of full dichromacy.</summary>
        public static Color Simulate(Color c, ColorBlindMode mode)
        {
            Vector3 lin = new Vector3(ToLinear(c.r), ToLinear(c.g), ToLinear(c.b));
            Vector3 o;
            switch (mode)
            {
                case ColorBlindMode.Protanopia:
                    o = new Vector3(
                        0.152286f * lin.x + 1.052583f * lin.y - 0.204868f * lin.z,
                        0.114503f * lin.x + 0.786281f * lin.y + 0.099216f * lin.z,
                        -0.003882f * lin.x - 0.048116f * lin.y + 1.051998f * lin.z);
                    break;
                case ColorBlindMode.Deuteranopia:
                    o = new Vector3(
                        0.367322f * lin.x + 0.860646f * lin.y - 0.227968f * lin.z,
                        0.280085f * lin.x + 0.672501f * lin.y + 0.047413f * lin.z,
                        -0.011820f * lin.x + 0.042940f * lin.y + 0.968881f * lin.z);
                    break;
                case ColorBlindMode.Tritanopia:
                    o = new Vector3(
                        1.255528f * lin.x - 0.076749f * lin.y - 0.178779f * lin.z,
                        -0.078411f * lin.x + 0.930809f * lin.y + 0.147602f * lin.z,
                        0.004733f * lin.x + 0.691367f * lin.y + 0.303900f * lin.z);
                    break;
                default:
                    return c;
            }
            return new Color(ToSrgb(o.x), ToSrgb(o.y), ToSrgb(o.z), c.a);
        }

        static float DeltaE(Color a, Color b)
        {
            Vector3 la = ToLab(a), lb = ToLab(b);
            return Vector3.Distance(la, lb);
        }

        static Vector3 ToLab(Color c)
        {
            float r = ToLinear(c.r), g = ToLinear(c.g), b = ToLinear(c.b);
            float x = (0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f;
            float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            float z = (0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f;
            x = LabF(x); y = LabF(y); z = LabF(z);
            return new Vector3(116f * y - 16f, 500f * (x - y), 200f * (y - z));
        }

        static float LabF(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;

        static float ToLinear(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

        static float ToSrgb(float v)
        {
            v = Mathf.Clamp01(v);
            return v <= 0.0031308f ? v * 12.92f : 1.055f * Mathf.Pow(v, 1f / 2.4f) - 0.055f;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
