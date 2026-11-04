using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using MonitoringTools.UI.Search;

namespace MonitoringTools.Core
{
    /// <summary>
    /// One element's appearance overrides. A null value means "leave the theme's value alone".
    /// Colours are stored as #AARRGGBB.
    /// </summary>
    public sealed class ElementStyle
    {
        public string FontFamily { get; set; }
        public double? FontSize { get; set; }
        /// <summary>OpenType weight, 100 to 900.</summary>
        public int? FontWeight { get; set; }
        public bool? Italic { get; set; }
        public string Foreground { get; set; }
        public string Background { get; set; }
        public double? CornerRadius { get; set; }
        public double? Padding { get; set; }
        public double? BorderThickness { get; set; }
        public string BorderColour { get; set; }

        public bool IsEmpty =>
            FontFamily == null && FontSize == null && FontWeight == null && Italic == null && Foreground == null && Background == null &&
            CornerRadius == null && Padding == null && BorderThickness == null && BorderColour == null;

        public ElementStyle Clone() => (ElementStyle)MemberwiseClone();
    }

    /// <summary>What an element can be given. A card (border) has no font of its own, but text inside it inherits one.</summary>
    public readonly struct StyleSupport
    {
        public bool Font { get; init; }
        public bool Foreground { get; init; }
        public bool Background { get; init; }
        public bool Radius { get; init; }
        public bool Padding { get; init; }
        public bool Border { get; init; }
    }

    /// <summary>
    /// Stores and applies per-element appearance edits. Elements are identified by the page they are on plus their
    /// visible text, so a style survives the page being rebuilt. Call <see cref="ApplyAll"/> after each page is built.
    /// </summary>
    public static class ElementStyles
    {
        private sealed class Originals { public readonly Dictionary<DependencyProperty, object> Saved = new Dictionary<DependencyProperty, object>(); }
        private static readonly ConditionalWeakTable<FrameworkElement, Originals> Remembered = new ConditionalWeakTable<FrameworkElement, Originals>();

        /// <summary>The page last passed to <see cref="ApplyAll"/>; the element menu uses it to build keys.</summary>
        public static string CurrentPageId { get; private set; }

        /// <summary>Raised after any style is set, reset or cleared.</summary>
        public static event Action Changed;

        private static Dictionary<string, ElementStyle> Map =>
            Settings.Current.ElementStyles ??= new Dictionary<string, ElementStyle>();

        // ------------------------------------------------------------ identity

        /// <summary>True for the elements a person thinks of as "this": a marked search item, a button, or a card.</summary>
        public static bool IsCandidate(FrameworkElement fe) =>
            SearchScope.GetItem(fe) != null || fe is ButtonBase || (fe is Border b && b.CornerRadius.TopLeft >= 20 && b.Child != null);

        public static string TextOf(FrameworkElement fe)
        {
            var s = string.Join(" ", SearchScope.Text(fe).Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return s.Length > 120 ? s[..120] : s;
        }

        public static string KeyFor(string pageId, FrameworkElement fe) => (pageId ?? "") + "|" + TextOf(fe);

        public static string PageIdFor(Window w) =>
            w is MonitoringTools.UI.ControlCentreWindow ? (CurrentPageId ?? "home") : (w?.GetType().Name ?? "window");

        public static ElementStyle Get(string key) => Map.TryGetValue(key, out var s) ? s : null;

        public static int Count => Map.Count;

        // ------------------------------------------------------------ capabilities and reading

        public static StyleSupport Support(FrameworkElement fe) => new StyleSupport
        {
            Font = true,
            Foreground = true,
            Background = BackgroundProperty(fe) != null,
            Radius = fe is Border,
            Padding = PaddingProperty(fe) != null,
            Border = BorderThicknessProperty(fe) != null
        };

        private static DependencyProperty BackgroundProperty(FrameworkElement fe) => fe switch
        {
            Border => Border.BackgroundProperty,
            Panel => Panel.BackgroundProperty,
            Control => Control.BackgroundProperty,
            TextBlock => TextBlock.BackgroundProperty,
            _ => null
        };

        private static DependencyProperty PaddingProperty(FrameworkElement fe) => fe switch
        {
            Border => Border.PaddingProperty,
            Control => Control.PaddingProperty,
            TextBlock => TextBlock.PaddingProperty,
            _ => null
        };

        private static DependencyProperty BorderThicknessProperty(FrameworkElement fe) => fe switch
        {
            Border => Border.BorderThicknessProperty,
            Control => Control.BorderThicknessProperty,
            _ => null
        };

        private static DependencyProperty BorderBrushProperty(FrameworkElement fe) => fe switch
        {
            Border => Border.BorderBrushProperty,
            Control => Control.BorderBrushProperty,
            _ => null
        };

        public static string Hex(Color c) => c.ToString();

        public static bool TryColour(string hex, out Color c)
        {
            c = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try { c = (Color)ColorConverter.ConvertFromString(hex.Trim()); return true; }
            catch { return false; }
        }

        private static string BrushHex(object o) => o is SolidColorBrush b ? Hex(b.Color) : null;

        /// <summary>The values the element shows right now, so an editor can start from what the person sees.</summary>
        public static ElementStyle Effective(FrameworkElement fe)
        {
            var s = new ElementStyle
            {
                FontFamily = (fe.GetValue(TextElement.FontFamilyProperty) as FontFamily)?.Source,
                FontSize = (double)fe.GetValue(TextElement.FontSizeProperty),
                FontWeight = ((FontWeight)fe.GetValue(TextElement.FontWeightProperty)).ToOpenTypeWeight(),
                Italic = ((FontStyle)fe.GetValue(TextElement.FontStyleProperty)) == FontStyles.Italic,
                Foreground = BrushHex(fe.GetValue(TextElement.ForegroundProperty))
            };
            var bg = BackgroundProperty(fe); if (bg != null) s.Background = BrushHex(fe.GetValue(bg));
            if (fe is Border b) s.CornerRadius = b.CornerRadius.TopLeft;
            var pd = PaddingProperty(fe); if (pd != null) s.Padding = ((Thickness)fe.GetValue(pd)).Left;
            var bt = BorderThicknessProperty(fe); if (bt != null) s.BorderThickness = ((Thickness)fe.GetValue(bt)).Left;
            var bb = BorderBrushProperty(fe); if (bb != null) s.BorderColour = BrushHex(fe.GetValue(bb));
            return s;
        }

        // ------------------------------------------------------------ applying

        /// <summary>Applies every saved style whose key matches an element under <paramref name="root"/>. Call it after each page is built.</summary>
        public static void ApplyAll(DependencyObject root, string pageId)
        {
            CurrentPageId = pageId;
            if (root == null || Map.Count == 0) return;
            var prefix = (pageId ?? "") + "|";
            if (!Map.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal))) return;
            Walk(root, pageId);
        }

        private static void Walk(DependencyObject d, string pageId)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>())
            {
                if (child is FrameworkElement fe && IsCandidate(fe) && Map.TryGetValue(KeyFor(pageId, fe), out var style)) Apply(fe, style);
                Walk(child, pageId);
            }
        }

        /// <summary>Applies the style to one element. A null member restores what the element had before.</summary>
        public static void Apply(FrameworkElement fe, ElementStyle s)
        {
            if (fe == null) return;
            s ??= new ElementStyle();
            var o = Remembered.GetOrCreateValue(fe);
            void Put(DependencyProperty dp, object value)
            {
                if (dp == null) return;
                if (!o.Saved.ContainsKey(dp)) o.Saved[dp] = fe.ReadLocalValue(dp);
                if (value != null) { fe.SetValue(dp, value); return; }
                var was = o.Saved[dp];
                if (was == DependencyProperty.UnsetValue || was is Expression) fe.ClearValue(dp);
                else fe.SetValue(dp, was);
            }
            object B(string hex) => TryColour(hex, out var c) ? new SolidColorBrush(c) : null;

            Put(TextElement.FontFamilyProperty, string.IsNullOrWhiteSpace(s.FontFamily) ? null : new FontFamily(s.FontFamily));
            Put(TextElement.FontSizeProperty, s.FontSize is double sz && sz > 0 ? sz : (object)null);
            Put(TextElement.FontWeightProperty, s.FontWeight is int w ? System.Windows.FontWeight.FromOpenTypeWeight(Math.Clamp(w, 1, 999)) : (object)null);
            Put(TextElement.FontStyleProperty, s.Italic is bool it ? (it ? FontStyles.Italic : FontStyles.Normal) : (object)null);
            Put(TextElement.ForegroundProperty, B(s.Foreground));
            Put(BackgroundProperty(fe), B(s.Background));
            if (fe is Border) Put(Border.CornerRadiusProperty, s.CornerRadius is double r ? new CornerRadius(Math.Max(0, r)) : (object)null);
            Put(PaddingProperty(fe), s.Padding is double p ? new Thickness(Math.Max(0, p)) : (object)null);
            Put(BorderThicknessProperty(fe), s.BorderThickness is double bt ? new Thickness(Math.Max(0, bt)) : (object)null);
            Put(BorderBrushProperty(fe), B(s.BorderColour));
        }

        // ------------------------------------------------------------ storing

        /// <summary>Stores a style for a key (removing it when empty) and optionally saves the settings file.</summary>
        public static void Store(string key, ElementStyle s, bool save)
        {
            if (s == null || s.IsEmpty) Map.Remove(key); else Map[key] = s;
            if (save) Settings.Save();
            Changed?.Invoke();
        }

        /// <summary>Removes one element's style and restores every matching element in the open windows.</summary>
        public static void Reset(string key, FrameworkElement target)
        {
            Map.Remove(key);
            Settings.Save();
            if (target != null) Apply(target, new ElementStyle());
            RestoreMatching(key);
            Changed?.Invoke();
        }

        /// <summary>Removes every saved style and restores the elements that are on screen.</summary>
        public static void ResetAll()
        {
            var keys = Map.Keys.ToList();
            Map.Clear();
            Settings.Save();
            foreach (var k in keys) RestoreMatching(k);
            Changed?.Invoke();
        }

        /// <summary>Re-applies a style to every element in every open window that has the same key.</summary>
        public static void ApplyMatching(string key)
        {
            if (!Map.TryGetValue(key, out var style)) { RestoreMatching(key); return; }
            ForMatching(key, fe => Apply(fe, style));
        }

        private static void RestoreMatching(string key) => ForMatching(key, fe => Apply(fe, new ElementStyle()));

        private static void ForMatching(string key, Action<FrameworkElement> act)
        {
            var cut = key.IndexOf('|');
            if (cut < 0) return;
            var page = key[..cut];
            foreach (Window w in Application.Current.Windows)
            {
                if (PageIdFor(w) != page) continue;
                void Go(DependencyObject d)
                {
                    foreach (var c in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>())
                    {
                        if (c is FrameworkElement fe && IsCandidate(fe) && KeyFor(page, fe) == key) act(fe);
                        Go(c);
                    }
                }
                Go(w);
            }
        }
    }
}
