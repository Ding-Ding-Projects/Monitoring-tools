using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MonitoringTools.Core
{
    /// <summary>The neutral colours a look brings: everything except the accent families, which still come from the colour seed.</summary>
    public sealed class LookNeutrals
    {
        public Color Ground, Window, Surface, Raised, Sunken, Line, LineStrong, Text, TextSoft;

        public LookNeutrals(string row)
        {
            var p = row.Split(' ');
            Color C(int i) { Appearance.TryParseHex(p[i], out var c); return c; }
            Ground = C(0); Window = C(1); Surface = C(2); Raised = C(3); Sunken = C(4); Line = C(5); LineStrong = C(6); Text = C(7); TextSoft = C(8);
        }
    }

    /// <summary>
    /// One look: a whole visual language for every window. Type, shapes, outlines, depth, the side rail's selected tab,
    /// the page header and the neutral colours change; the colour seed, theme, text size, density, motion and language stay
    /// the person's own. Each look was designed on its own canvas first (design/looks/).
    /// </summary>
    public sealed class LookSpec
    {
        public string Id, Name, Description, DefaultSeed;
        /// <summary>Theme.xaml font resources this look swaps in for F.Display, F.Body and F.Mono.</summary>
        public string DisplayFont, BodyFont, MonoFont;
        public FontWeight TitleWeight;
        /// <summary>Maps a corner radius drawn for Graphite to this look's radius.</summary>
        public Func<double, double> Radius;
        /// <summary>Card outline width (0: tonal cards with no outline) and whether it uses the strong outline colour.</summary>
        public double CardEdge;
        public bool CardEdgeStrong;
        /// <summary>A hard offset shadow under cards and chosen controls (Pop), in device-independent pixels; 0 for none.</summary>
        public double HardShadow;
        /// <summary>Shadow under windows that float (toasts, menus, dialogs): blur, depth, opacity. Opacity 0 means none.</summary>
        public double FloatBlur, FloatDepth, FloatOpacity;
        /// <summary>"pill", "key", "wire" or "sticker": how the side rail marks the open page.</summary>
        public string Nav;
        /// <summary>"crumb", "plate", "rule" or "tape": the small label above each page title.</summary>
        public string Header;
        /// <summary>Big figures sit in dark readout windows (Instrument).</summary>
        public bool Readouts;
        /// <summary>A fine grid behind the pages (Blueprint).</summary>
        public bool Grid;
        /// <summary>Cards lift a little on hover.</summary>
        public bool Lift;
        /// <summary>Containers take the bright tones of three seeds instead of soft tints (Pop).</summary>
        public bool Vibrant;
        /// <summary>Null keeps the seed's own tinted neutrals (Graphite).</summary>
        public LookNeutrals Dark, Light;
    }

    public static class Looks
    {
        public static readonly LookSpec Graphite = new LookSpec
        {
            Id = "graphite", Name = "Graphite", DefaultSeed = "mint",
            Description = "Soft tonal layers and rounded shapes. The look you have today.",
            DisplayFont = "F.Look.Graphite.Display", BodyFont = "F.Look.Graphite.Body", MonoFont = "F.Look.Mono",
            TitleWeight = FontWeights.Normal, Radius = r => r,
            CardEdge = 0, FloatBlur = 32, FloatDepth = 10, FloatOpacity = 0.42,
            Nav = "pill", Header = "crumb", Lift = true,
        };

        public static readonly LookSpec Instrument = new LookSpec
        {
            Id = "instrument", Name = "Instrument", DefaultSeed = "amber",
            Description = "Like test gear on a bench: panels, lit indicators and amber readouts.",
            DisplayFont = "F.Look.Instrument.Display", BodyFont = "F.Look.Instrument.Body", MonoFont = "F.Look.Mono",
            TitleWeight = FontWeights.SemiBold, Radius = r => Math.Min(r * 0.5, 12),
            CardEdge = 1, FloatBlur = 18, FloatDepth = 6, FloatOpacity = 0.5,
            Nav = "key", Header = "plate", Readouts = true,
            Dark = new LookNeutrals("#121413 #181B19 #202421 #292D2A #303531 #3B403C #858B84 #EAECE6 #B8BDB5"),
            Light = new LookNeutrals("#D3D5CF #DFE1DB #EEEFEA #F7F7F3 #D7D9D2 #B3B7AE #6E736A #191B19 #41463F"),
        };

        public static readonly LookSpec Blueprint = new LookSpec
        {
            Id = "blueprint", Name = "Blueprint", DefaultSeed = "ocean",
            Description = "Drawn in fine ink on a grid, with square corners and no shadows.",
            DisplayFont = "F.Look.Blueprint.Display", BodyFont = "F.Look.Blueprint.Body", MonoFont = "F.Look.Blueprint.Mono",
            TitleWeight = FontWeights.SemiBold, Radius = r => Math.Min(r * 0.15, 3),
            CardEdge = 1, CardEdgeStrong = true, FloatOpacity = 0,
            Nav = "wire", Header = "rule", Grid = true,
            Dark = new LookNeutrals("#061630 #0A1D3A #0E2546 #132D53 #183560 #2A4F80 #7EA6D8 #E8F1FF #A8C0E4"),
            Light = new LookNeutrals("#D8E3EF #E8EFF7 #F4F8FC #FFFFFF #DCE6F1 #A9BDD6 #4A6A92 #0D2140 #33507A"),
        };

        public static readonly LookSpec Pop = new LookSpec
        {
            Id = "pop", Name = "Pop", DefaultSeed = "rose",
            Description = "Bold outlines, sticker colours and hard shadows.",
            DisplayFont = "F.Look.Pop.Display", BodyFont = "F.Look.Pop.Body", MonoFont = "F.Look.Mono",
            TitleWeight = FontWeights.ExtraBold, Radius = r => r >= 18 ? Math.Min(r, 24) : r * 1.25,
            CardEdge = 2, CardEdgeStrong = true, HardShadow = 4, FloatBlur = 0, FloatDepth = 5, FloatOpacity = 1,
            Nav = "sticker", Header = "tape", Lift = true, Vibrant = true,
            Dark = new LookNeutrals("#0E0F12 #15161A #1D1F24 #25272D #2D3037 #3A3D46 #ECEAE3 #F6F5F0 #C4C6CE"),
            Light = new LookNeutrals("#E9EAEE #F4F5F7 #FFFFFF #FFFFFF #EBECF0 #CDD0D8 #16171B #16171B #4A4D57"),
        };

        public static readonly LookSpec[] All = { Graphite, Instrument, Blueprint, Pop };

        public static LookSpec Find(string id) => All.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Graphite;

        /// <summary>The look in use right now.</summary>
        public static LookSpec Current => Find(Settings.Current?.Look);

        /// <summary>
        /// A corner drawn for Graphite, mapped to the current look. Circles stay circles (an avatar, a status dot or a round
        /// icon button keeps its shape in every look); pills follow the look like any other corner.
        /// </summary>
        public static CornerRadius Corner(CornerRadius original, double width, double height)
        {
            var look = Current;
            if (look == Graphite) return original;
            if (width > 0 && height > 0 && Math.Abs(width - height) < 2 && original.TopLeft >= width / 2 - 1 && original.TopLeft == original.BottomRight) return original;
            return new CornerRadius(look.Radius(original.TopLeft), look.Radius(original.TopRight), look.Radius(original.BottomRight), look.Radius(original.BottomLeft));
        }
    }
}
