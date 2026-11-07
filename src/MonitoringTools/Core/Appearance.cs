using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Theme, colour, text size, density and motion. <see cref="Apply"/> rewrites the colour and brush
    /// resources in the application dictionary and raises <see cref="Changed"/>.
    /// </summary>
    public static class Appearance
    {
        public const string DefaultName = "Monitoring Tools";

        public static readonly string[] SeedIds = { "mint", "violet", "amber", "ocean", "rose" };
        public static readonly string[] SeedLabels = { "Mint", "Violet", "Amber", "Ocean", "Rose" };

        // Role order in every row below.
        private static readonly string[] Roles =
        {
            "primary", "onPrimary", "primaryContainer", "onPrimaryContainer", "secondaryContainer", "onSecondaryContainer",
            "tertiaryContainer", "onTertiaryContainer", "surface", "surfaceContainerLow", "surfaceContainer",
            "surfaceContainerHigh", "surfaceContainerHighest", "onSurface", "onSurfaceVariant", "outline", "outlineVariant"
        };

        private static readonly Dictionary<string, (string Dark, string Light)> Schemes = new Dictionary<string, (string, string)>
        {
            ["mint"] = ("#6FDBB4 #003827 #00513A #8CF8CF #354B41 #CFE9DB #244C5D #C2E8FD #0F1512 #171D1A #1B211E #262B28 #303633 #DEE4DF #BFC9C2 #89938C #404943",
                        "#006C4F #FFFFFF #8CF8CF #002116 #CFE9DB #09201A #C2E8FD #001F2A #F5FBF6 #EFF5F0 #E9EFEA #E4EAE5 #DEE4DF #171D1A #404943 #707973 #BFC9C2"),
            ["violet"] = ("#CFBCFF #381E72 #4F378A #E9DDFF #4A4458 #E8DEF8 #633B48 #FFD8E4 #141218 #1D1B20 #211F26 #2B2930 #36343B #E6E0E9 #CAC4D0 #948F99 #49454F",
                          "#6750A4 #FFFFFF #E9DDFF #22005D #E8DEF8 #1E192B #FFD8E4 #31111D #FEF7FF #F7F2FA #F3EDF7 #ECE6F0 #E6E0E9 #1D1B20 #49454F #79747E #CAC4D0"),
            ["amber"] = ("#FFB95F #462A00 #653E00 #FFDDB8 #56442A #FADEBC #384D27 #D2EAB5 #18120B #211A13 #251E17 #302921 #3B332B #EEE0D4 #D5C4B4 #9D8E80 #504539",
                         "#855300 #FFFFFF #FFDDB8 #2A1700 #FADEBC #271904 #D2EAB5 #0E2004 #FFF8F4 #FCF2EA #F6ECE4 #F0E6DE #EEE0D4 #211A13 #504539 #827568 #D5C4B4"),
            ["ocean"] = ("#9ECAFF #003258 #00497D #D1E4FF #3B4858 #D7E3F7 #523F5F #F2DAFF #101418 #181C20 #1C2024 #272A2F #32353A #E0E2E8 #C3C7CF #8D9199 #43474E",
                         "#0061A4 #FFFFFF #D1E4FF #001D36 #D7E3F7 #101C2B #F2DAFF #251431 #F8F9FF #F2F3F9 #ECEEF4 #E6E8EE #E0E2E8 #181C20 #43474E #73777F #C3C7CF"),
            ["rose"] = ("#FFB2BC #670020 #8E0F32 #FFD9DC #5D3F42 #FFD9DC #5C4019 #FFDDB5 #191113 #22191B #261D1F #312829 #3C3234 #F0DEDF #D7C1C2 #9F8C8D #524345",
                        "#B32148 #FFFFFF #FFD9DC #40000F #FFD9DC #2C1516 #FFDDB5 #2A1800 #FFF8F7 #FEF0F0 #F8EAEA #F2E4E5 #ECDEDF #22191B #524345 #857374 #D7C1C2"),
        };

        private static bool _hooked;

        /// <summary>Raised on the UI thread after the resources have been rewritten.</summary>
        public static event Action Changed;

        /// <summary>True while a text field commits its own value: windows update their header but keep the page and its keyboard focus, so tabbing out of the field lands on the next control.</summary>
        public static bool KeepPage;

        // ------------------------------------------------------------------ live values

        private static Settings S => Settings.Current;

        public static string ThemeMode => Normalise(S.ThemeMode, "system", "light", "dark");
        public static string Seed => Normalise(S.SeedColour, SeedIds);

        /// <summary>True when the dark scheme is in use right now.</summary>
        public static bool IsDark => ThemeMode == "dark" || (ThemeMode == "system" && !WindowsUsesLightApps());

        public static double TextScale => Math.Max(90, Math.Min(150, S.TextScalePercent)) / 100.0;
        public static bool Compact => S.CompactDensity;

        /// <summary>A row or control height for the current density: the comfortable value, or about four fifths of it when Compact is on (40 becomes 32).</summary>
        public static double Row(double comfortable) => Compact ? Math.Round(comfortable * 0.8) : comfortable;

        /// <summary>A gap, margin or padding for the current density: the comfortable value, or about three quarters of it when Compact is on.</summary>
        public static double Gap(double comfortable) => Compact ? Math.Round(comfortable * 0.75) : comfortable;

        /// <summary>Scales only the vertical parts of a thickness, so rows tighten without squeezing text sideways.</summary>
        public static Thickness Vertical(double left, double top, double right, double bottom) =>
            new Thickness(left, Gap(top), right, Gap(bottom));

        // Buttons take their height from the theme styles. When density is Compact their height is trimmed at runtime, and
        // restored when it is Comfortable again. A height the code set itself is never touched.
        private static readonly DependencyProperty TrimmedHeight = DependencyProperty.RegisterAttached("TrimmedHeight", typeof(double), typeof(Appearance), new PropertyMetadata(0.0));
        private static bool _densityHooked, _densityWasCompact;

        private static void FitButton(Button b)
        {
            double mine = (double)b.GetValue(TrimmedHeight);
            if (mine > 0 && b.ReadLocalValue(FrameworkElement.HeightProperty) is double local && Math.Abs(local - mine) < 0.01)
            {
                if (!Compact) { b.ClearValue(FrameworkElement.HeightProperty); b.ClearValue(TrimmedHeight); }
                return;
            }
            if (!Compact) return;
            if (b.ReadLocalValue(FrameworkElement.HeightProperty) != DependencyProperty.UnsetValue) return;
            double h = b.Height;
            if (double.IsNaN(h) || h < 30) return;
            double next = Row(h);
            b.Height = next;
            b.SetValue(TrimmedHeight, next);
        }

        private static void FitTree(DependencyObject d)
        {
            if (d is Button b) FitButton(b);
            int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = 0; i < n; i++) FitTree(VisualTreeHelper.GetChild(d, i));
        }

        /// <summary>Rounds a stored percentage to the nearest allowed step, 90 to 150.</summary>
        public static int ClampPercent(int p) => Math.Max(90, Math.Min(150, (int)(Math.Round(p / 10.0) * 10)));

        /// <summary>True when animation should be skipped, either by choice or because Windows has animation off.</summary>
        public static bool ReduceMotion
        {
            get
            {
                switch (Normalise(S.MotionMode, "follow", "on", "off"))
                {
                    case "on": return true;
                    case "off": return false;
                    default: return !SystemParameters.ClientAreaAnimation;
                }
            }
        }

        public static bool HighContrastOutlines => S.HighContrastOutlines;

        public static string DisplayName => string.IsNullOrWhiteSpace(S.DisplayName) ? DefaultName : S.DisplayName.Trim();

        /// <summary>The tint for the tray and header glyph.</summary>
        public static Color LogoColor
        {
            get
            {
                if (S.LogoSource == "custom" && TryParseHex(S.LogoCustomHex, out var c)) return c;
                return Role(0);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static string Normalise(string v, params string[] allowed)
        {
            foreach (var a in allowed) if (string.Equals(a, v, StringComparison.OrdinalIgnoreCase)) return a;
            return allowed[0];
        }

        private static bool WindowsUsesLightApps()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return !(k?.GetValue("AppsUseLightTheme") is int i) || i != 0;
            }
            catch { return false; }
        }

        /// <summary>Accepts #RRGGBB or RRGGBB.</summary>
        public static bool TryParseHex(string text, out Color color)
        {
            color = default;
            var t = (text ?? "").Trim().TrimStart('#');
            if (t.Length != 6) return false;
            foreach (var ch in t) if (!Uri.IsHexDigit(ch)) return false;
            color = Color.FromRgb(Convert.ToByte(t.Substring(0, 2), 16), Convert.ToByte(t.Substring(2, 2), 16), Convert.ToByte(t.Substring(4, 2), 16));
            return true;
        }

        private static Color[] Scheme(string seed, bool dark)
        {
            var row = dark ? Schemes[seed].Dark : Schemes[seed].Light;
            var parts = row.Split(' ');
            var result = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) TryParseHex(parts[i], out result[i]);
            return result;
        }

        /// <summary>One role of the active scheme, by the order of the role table (0 primary ... 16 outlineVariant).</summary>
        public static Color Role(int index) => Scheme(Seed, IsDark)[index];

        public static string RoleName(int index) => Roles[index];

        private static double Lum(Color c)
        {
            double Ch(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }

        /// <summary>Moves a text colour toward black (on a light background) or white (on a dark one) until it reaches the contrast ratio.</summary>
        public static Color EnsureContrast(Color fg, Color bg, double min)
        {
            double lb = Lum(bg);
            bool toBlack = lb > 0.4;
            var c = fg;
            for (int i = 0; i < 60; i++)
            {
                double lf = Lum(c), ratio = (Math.Max(lf, lb) + 0.05) / (Math.Min(lf, lb) + 0.05);
                if (ratio >= min) return c;
                c = toBlack ? Color.FromRgb((byte)(c.R * 0.96), (byte)(c.G * 0.96), (byte)(c.B * 0.96))
                            : Color.FromRgb((byte)Math.Min(255, c.R + (255 - c.R) * 0.06 + 1), (byte)Math.Min(255, c.G + (255 - c.G) * 0.06 + 1), (byte)Math.Min(255, c.B + (255 - c.B) * 0.06 + 1));
            }
            return c;
        }

        private static Color Darker(Color c, byte by) =>
            Color.FromRgb((byte)Math.Max(0, c.R - by), (byte)Math.Max(0, c.G - by), (byte)Math.Max(0, c.B - by));

        // ------------------------------------------------------------------ apply

        /// <summary>Call once after Settings.Load, before any window is shown.</summary>
        public static void Initialize()
        {
            if (!_hooked)
            {
                _hooked = true;
                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (ThemeMode != "system" && Normalise(S.MotionMode, "follow", "on", "off") != "follow") return;
                    Application.Current?.Dispatcher.BeginInvoke(new Action(Apply));
                };
            }
            MonitoringTools.UI.AccessibleLabels.Install();
            MonitoringTools.UI.LookShape.Install();
            if (!_densityHooked)
            {
                _densityHooked = true;
                EventManager.RegisterClassHandler(typeof(Button), FrameworkElement.LoadedEvent, new RoutedEventHandler((o, a) => FitButton((Button)o)));
            }
            Apply();
        }

        /// <summary>Writes the colours and brushes for the current choices, scales open windows and raises <see cref="Changed"/>.</summary>
        public static void Apply()
        {
            var app = Application.Current;
            if (app == null) return;
            if (!app.Dispatcher.CheckAccess()) { app.Dispatcher.BeginInvoke(new Action(Apply)); return; }

            bool dark = IsDark;
            var look = Looks.Current;
            var r = Scheme(Seed, dark);
            var n = dark ? look.Dark : look.Light;
            if (n == null)
            {
                // Graphite: the seed's own tinted neutrals
                var ground = dark ? Darker(r[8], 5) : r[8];
                Set("Ground", ground);
                Set("Window", r[8]);
                Set("Surface", r[10]);
                Set("Raised", r[11]);
                Set("Sunken", r[12]);
                Set("Line", S.HighContrastOutlines ? r[15] : r[16]);
                Set("LineStrong", r[15]);
                Set("Text", r[13]);
                Set("TextSoft", r[14]);
                // muted text is the outline role nudged just far enough to read at 4.5:1 on the strongest surface it sits on
                Set("TextMute", EnsureContrast(r[15], r[12], 4.9));
            }
            else
            {
                Set("Ground", n.Ground);
                Set("Window", n.Window);
                Set("Surface", n.Surface);
                Set("Raised", n.Raised);
                Set("Sunken", n.Sunken);
                Set("Line", S.HighContrastOutlines ? n.LineStrong : n.Line);
                Set("LineStrong", n.LineStrong);
                Set("Text", n.Text);
                Set("TextSoft", n.TextSoft);
                Set("TextMute", EnsureContrast(Mix(n.TextSoft, n.Sunken, 0.3), n.Sunken, 4.9));
            }
            Set("Mint", r[0]);
            Set("MintInk", r[1]);
            Set("MintWash", r[2]);
            Set("OnPrimaryContainer", r[3]);
            Set("SecondaryContainer", r[4]);
            Set("OnSecondaryContainer", r[5]);
            Set("TertiaryContainer", r[6]);
            Set("OnTertiaryContainer", r[7]);
            if (look.Vibrant)
            {
                // sticker colours: the bright tone of this seed and two companions, all with dark ink on them
                var ink = Color.FromRgb(0x16, 0x17, 0x1B);
                var (b, c) = Companions(Seed);
                Set("MintWash", Scheme(Seed, true)[0]); Set("OnPrimaryContainer", ink);
                Set("SecondaryContainer", Scheme(b, true)[0]); Set("OnSecondaryContainer", ink);
                Set("TertiaryContainer", Scheme(c, true)[0]); Set("OnTertiaryContainer", ink);
                if (dark) Set("MintInk", ink);
            }
            // readout windows (Instrument): the seed's bright tone on a near-black well, in both themes
            Set("Readout", Scheme(Seed, true)[0]);
            Set("ReadoutWell", Color.FromRgb(0x0B, 0x0D, 0x0C));
            ApplyLook(look, dark, n);

            // the warning, accent and error families have a dark and a light set, so text in them stays readable on either theme
            if (dark)
            {
                Set("Amber", Color.FromRgb(0xFF, 0xB9, 0x5F)); Set("AmberInk", Color.FromRgb(0x46, 0x2A, 0x00)); Set("AmberWash", Color.FromRgb(0x3B, 0x2A, 0x12));
                Set("Violet", Color.FromRgb(0xCF, 0xBC, 0xFF)); Set("VioletWash", Color.FromRgb(0x2B, 0x26, 0x40));
                Set("Danger", Color.FromRgb(0xFF, 0xB4, 0xAB)); Set("DangerWash", Color.FromRgb(0x93, 0x00, 0x0A));
            }
            else
            {
                Set("Amber", Color.FromRgb(0x7A, 0x4A, 0x00)); Set("AmberInk", Color.FromRgb(0xFF, 0xFF, 0xFF)); Set("AmberWash", Color.FromRgb(0xFF, 0xDD, 0xB3));
                Set("Violet", Color.FromRgb(0x62, 0x47, 0xB0)); Set("VioletWash", Color.FromRgb(0xE9, 0xDD, 0xFF));
                Set("Danger", Color.FromRgb(0xBA, 0x1A, 0x1A)); Set("DangerWash", Color.FromRgb(0xFF, 0xDA, 0xD6));
            }

            foreach (Window w in app.Windows) ScaleFor(w);
            if (_densityWasCompact != Compact)
            {
                _densityWasCompact = Compact;
                foreach (Window w in app.Windows) FitTree(w);
            }
            Changed?.Invoke();
        }

        private static Color Mix(Color a, Color b, double t) =>
            Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

        /// <summary>The two seeds whose bright tones join this one in the vibrant (Pop) containers.</summary>
        private static (string, string) Companions(string seed) => seed switch
        {
            "mint" => ("amber", "violet"),
            "violet" => ("mint", "amber"),
            "amber" => ("ocean", "rose"),
            "ocean" => ("amber", "mint"),
            _ => ("amber", "mint"),
        };

        private static string _appliedLook;

        /// <summary>The look's fonts, title weight, shadows and card outlines; shapes are remapped when the look changes.</summary>
        private static void ApplyLook(LookSpec look, bool dark, LookNeutrals n)
        {
            var res = Application.Current.Resources;
            if (res[look.DisplayFont] is FontFamily display) res["F.Display"] = display;
            if (res[look.BodyFont] is FontFamily body) res["F.Body"] = body;
            if (res[look.MonoFont] is FontFamily mono) res["F.Mono"] = mono;
            res["W.Display"] = look.TitleWeight;

            var shadowInk = look.HardShadow > 0 && n != null ? n.LineStrong : Colors.Black;
            var floating = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = look.FloatBlur, ShadowDepth = look.FloatDepth, Direction = look.HardShadow > 0 ? 315 : 270,
                Opacity = look.FloatOpacity, Color = shadowInk
            };
            floating.Freeze();
            res["Fx.Shadow"] = floating;
            if (look.HardShadow > 0)
            {
                var card = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 0, ShadowDepth = look.HardShadow, Direction = 315, Opacity = 1, Color = shadowInk };
                card.Freeze();
                res["Fx.Card"] = card;
            }
            else res.Remove("Fx.Card");

            res["Th.CardEdge"] = new Thickness(look.CardEdge);
            var edge = res[look.CardEdgeStrong ? "B.LineStrong" : "B.Line"];
            res["B.CardEdge"] = look.CardEdge > 0 ? edge : Brushes.Transparent;

            if (_appliedLook != null && _appliedLook != look.Id) MonitoringTools.UI.LookShape.RemapAll();
            _appliedLook = look.Id;
        }

        private static void Set(string name, Color c)
        {
            var res = Application.Current.Resources;
            res["C." + name] = c;
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            res["B." + name] = brush;
        }

        /// <summary>
        /// Scales a window's content by <see cref="TextScale"/> with a LayoutTransform on its root element,
        /// so text and spacing grow together and the layout reflows. Call it when a window opens.
        /// </summary>
        public static void ScaleFor(Window w)
        {
            if (w?.Content is FrameworkElement root)
            {
                double s = TextScale;
                root.LayoutTransform = Math.Abs(s - 1) < 0.001 ? Transform.Identity : new ScaleTransform(s, s);
            }
        }

        /// <summary>Puts every appearance choice back to its default and applies it.</summary>
        public static void ResetToDefaults()
        {
            var d = new Settings();
            S.ThemeMode = d.ThemeMode;
            S.Look = d.Look;
            S.SeedColour = d.SeedColour;
            S.TextScalePercent = d.TextScalePercent;
            S.CompactDensity = d.CompactDensity;
            S.MotionMode = d.MotionMode;
            S.HighContrastOutlines = d.HighContrastOutlines;
            S.DisplayName = d.DisplayName;
            S.LogoSource = d.LogoSource;
            S.LogoCustomHex = d.LogoCustomHex;
            LogoKit.ResetQuiet();
            Settings.Save();
            Apply();
        }
    }
}
