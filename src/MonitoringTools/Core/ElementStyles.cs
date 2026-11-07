using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using MonitoringTools.UI.Search;

namespace MonitoringTools.Core
{
    /// <summary>The colours an element takes while it is in one interaction state. A null value means "keep the normal look".</summary>
    public sealed class StateLook
    {
        public string Foreground { get; set; }
        public string Background { get; set; }
        public string BorderColour { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEmpty => Foreground == null && Background == null && BorderColour == null;
        public StateLook Clone() => (StateLook)MemberwiseClone();
    }

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
        /// <summary>Extra space between letters in device-independent pixels, 0 to 12.</summary>
        public double? LetterSpacing { get; set; }
        public bool? Underline { get; set; }
        public string Foreground { get; set; }
        public string Background { get; set; }
        public double? CornerRadius { get; set; }
        public double? Padding { get; set; }
        public double? BorderThickness { get; set; }
        public string BorderColour { get; set; }

        /// <summary>Colours while the pointer is over the element.</summary>
        public StateLook Hover { get; set; }
        /// <summary>Colours while the element is being pressed.</summary>
        public StateLook Pressed { get; set; }
        /// <summary>Colours while the element has keyboard focus.</summary>
        public StateLook Focused { get; set; }
        /// <summary>Colours while the element is disabled.</summary>
        public StateLook Disabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasStates => !(Hover?.IsEmpty ?? true) || !(Pressed?.IsEmpty ?? true) || !(Focused?.IsEmpty ?? true) || !(Disabled?.IsEmpty ?? true);

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEmpty =>
            FontFamily == null && FontSize == null && FontWeight == null && Italic == null && LetterSpacing == null && Underline == null &&
            Foreground == null && Background == null && CornerRadius == null && Padding == null && BorderThickness == null && BorderColour == null && !HasStates;

        public ElementStyle Clone()
        {
            var c = (ElementStyle)MemberwiseClone();
            c.Hover = Hover?.Clone(); c.Pressed = Pressed?.Clone(); c.Focused = Focused?.Clone(); c.Disabled = Disabled?.Clone();
            return c;
        }

        /// <summary>Replaces every member with the values of <paramref name="other"/> (or clears them when it is null).</summary>
        public void CopyFrom(ElementStyle other)
        {
            var o = other?.Clone() ?? new ElementStyle();
            FontFamily = o.FontFamily; FontSize = o.FontSize; FontWeight = o.FontWeight; Italic = o.Italic; LetterSpacing = o.LetterSpacing; Underline = o.Underline;
            Foreground = o.Foreground; Background = o.Background; CornerRadius = o.CornerRadius; Padding = o.Padding; BorderThickness = o.BorderThickness; BorderColour = o.BorderColour;
            Hover = o.Hover; Pressed = o.Pressed; Focused = o.Focused; Disabled = o.Disabled;
        }

        /// <summary>The look for one state ("hover", "pressed", "focused", "disabled"), created when <paramref name="create"/> is set.</summary>
        public StateLook StateOf(string state, bool create)
        {
            switch (state)
            {
                case "hover": return create ? (Hover ??= new StateLook()) : Hover;
                case "pressed": return create ? (Pressed ??= new StateLook()) : Pressed;
                case "focused": return create ? (Focused ??= new StateLook()) : Focused;
                case "disabled": return create ? (Disabled ??= new StateLook()) : Disabled;
                default: return null;
            }
        }

        /// <summary>Drops empty state looks so a cleared state does not leave an empty object behind.</summary>
        public void Tidy()
        {
            if (Hover?.IsEmpty == true) Hover = null;
            if (Pressed?.IsEmpty == true) Pressed = null;
            if (Focused?.IsEmpty == true) Focused = null;
            if (Disabled?.IsEmpty == true) Disabled = null;
        }
    }

    /// <summary>One recorded change to an element's look. Restoring it puts <see cref="After"/> back.</summary>
    public sealed class StyleHistoryEntry
    {
        public DateTime At { get; set; } = DateTime.Now;
        public string Key { get; set; } = "";
        public string Summary { get; set; } = "";
        public ElementStyle Before { get; set; }
        public ElementStyle After { get; set; }
    }

    /// <summary>The file format used to export and import every element look and preset.</summary>
    public sealed class StyleBundle
    {
        public string Format { get; set; } = "monitoringtools.element-styles";
        public int Version { get; set; } = 1;
        public Dictionary<string, ElementStyle> Styles { get; set; } = new Dictionary<string, ElementStyle>();
        public Dictionary<string, ElementStyle> Presets { get; set; } = new Dictionary<string, ElementStyle>();
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

        /// <summary>Per element: the style the state handlers read, and which states are currently active.</summary>
        private sealed class Hooks { public ElementStyle Style; public bool Hover, Pressed, Focused, Wired, LoadedWired, TextTouched; }
        private static readonly ConditionalWeakTable<FrameworkElement, Hooks> HookTable = new ConditionalWeakTable<FrameworkElement, Hooks>();

        /// <summary>True when the person gave this element its own corner radius, which a look must not reshape.</summary>
        public static bool HasCustomRadius(FrameworkElement fe) => HookTable.TryGetValue(fe, out var h) && h.Style?.CornerRadius != null;

        /// <summary>Remembers the text and decorations of a text element before letter spacing and underline changed them.</summary>
        private sealed class TextMemo { public string Original, Spaced; public bool DecorationsSaved; public object Decorations; }
        private static readonly ConditionalWeakTable<DependencyObject, TextMemo> TextMemos = new ConditionalWeakTable<DependencyObject, TextMemo>();

        /// <summary>A hair space, used to widen letter spacing. It is stripped again when a key is built from the text.</summary>
        public const char Hair = ' ';

        public const int MaxImportBytes = 1_000_000;
        public const int MaxImportStyles = 2000;
        public const int MaxPresets = 50;
        public const int MaxHistory = 100;

        /// <summary>The page last passed to <see cref="ApplyAll"/>; the element menu uses it to build keys.</summary>
        public static string CurrentPageId { get; private set; }

        /// <summary>Raised after any style is set, reset or cleared.</summary>
        public static event Action Changed;

        /// <summary>The style copied with "Copy style", waiting to be pasted onto another element. Held in memory only.</summary>
        public static ElementStyle Clipboard { get; set; }
        public static string ClipboardFrom { get; set; }

        private static Dictionary<string, ElementStyle> Map =>
            Settings.Current.ElementStyles ??= new Dictionary<string, ElementStyle>();

        // ------------------------------------------------------------ identity

        /// <summary>True for the elements a person thinks of as "this": a marked search item, a button, or a card.</summary>
        public static bool IsCandidate(FrameworkElement fe) =>
            SearchScope.GetItem(fe) != null || fe is ButtonBase || (fe is Border b && b.CornerRadius.TopLeft >= 20 && b.Child != null);

        public static string TextOf(FrameworkElement fe)
        {
            var s = string.Join(" ", SearchScope.Text(fe).Replace(Hair.ToString(), "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return s.Length > 120 ? s[..120] : s;
        }

        public static string KeyFor(string pageId, FrameworkElement fe) => (pageId ?? "") + "|" + TextOf(fe);

        public static string PageIdFor(Window w) =>
            w is MonitoringTools.UI.ControlCentreWindow ? (CurrentPageId ?? "home") : (w?.GetType().Name ?? "window");

        public static ElementStyle Get(string key) => Map.TryGetValue(key, out var s) ? s : null;

        public static int Count => Map.Count;

        /// <summary>True when this element's look is frozen by an appearance lock that is still closed.</summary>
        public static bool IsLocked(string key) => Locks.IsAppearanceLocked(key);

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

        private static string ResourceKeyOf(object expression)
        {
            var t = expression?.GetType();
            if (t == null || t.Name != "ResourceReferenceExpression") return null;
            return t.GetProperty("ResourceKey", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)?.GetValue(expression) as string;
        }

        /// <summary>Sets a property, remembering what the element had before so that a null value can put it back.</summary>
        private static void Put(FrameworkElement fe, Originals o, DependencyProperty dp, object value)
        {
            if (dp == null) return;
            if (!o.Saved.ContainsKey(dp)) o.Saved[dp] = fe.ReadLocalValue(dp);
            if (value != null) { fe.SetValue(dp, value); return; }
            var was = o.Saved[dp];
            if (was == DependencyProperty.UnsetValue) { fe.ClearValue(dp); return; }
            if (was is Expression)
            {
                // a theme reference is restored by key, so the element keeps following the theme after the edit is removed
                var key = ResourceKeyOf(was);
                if (key != null) fe.SetResourceReference(dp, key); else fe.ClearValue(dp);
                return;
            }
            fe.SetValue(dp, was);
        }

        private static object Brush(string hex) => TryColour(hex, out var c) ? new SolidColorBrush(c) : null;

        /// <summary>Applies the style to one element. A null member restores what the element had before.</summary>
        public static void Apply(FrameworkElement fe, ElementStyle s)
        {
            if (fe == null) return;
            s ??= new ElementStyle();
            var o = Remembered.GetOrCreateValue(fe);

            Put(fe, o, TextElement.FontFamilyProperty, string.IsNullOrWhiteSpace(s.FontFamily) ? null : new FontFamily(s.FontFamily));
            Put(fe, o, TextElement.FontSizeProperty, s.FontSize is double sz && sz > 0 ? sz : (object)null);
            Put(fe, o, TextElement.FontWeightProperty, s.FontWeight is int w ? System.Windows.FontWeight.FromOpenTypeWeight(Math.Clamp(w, 1, 999)) : (object)null);
            Put(fe, o, TextElement.FontStyleProperty, s.Italic is bool it ? (it ? FontStyles.Italic : FontStyles.Normal) : (object)null);
            if (fe is Border) Put(fe, o, Border.CornerRadiusProperty, s.CornerRadius is double r ? new CornerRadius(Math.Max(0, r)) : (object)null);
            Put(fe, o, PaddingProperty(fe), s.Padding is double p ? new Thickness(Math.Max(0, p)) : (object)null);
            Put(fe, o, BorderThicknessProperty(fe), s.BorderThickness is double bt ? new Thickness(Math.Max(0, bt)) : (object)null);

            // colours come last because they are the part the interaction states change
            var hooks = HookTable.GetOrCreateValue(fe);
            hooks.Style = s;
            if (s.HasStates) WireStates(fe, hooks);
            PaintColours(fe, hooks);

            ApplyText(fe, s);
        }

        // ------------------------------------------------------------ interaction states

        private static void WireStates(FrameworkElement fe, Hooks h)
        {
            if (h.Wired) return;
            h.Wired = true;
            void Refresh() => PaintColours(fe, h);
            fe.MouseEnter += (s, e) => { h.Hover = true; Refresh(); };
            fe.MouseLeave += (s, e) => { h.Hover = false; h.Pressed = false; Refresh(); };
            fe.PreviewMouseLeftButtonDown += (s, e) => { h.Pressed = true; Refresh(); };
            fe.PreviewMouseLeftButtonUp += (s, e) => { h.Pressed = false; Refresh(); };
            fe.LostMouseCapture += (s, e) => { h.Pressed = false; Refresh(); };
            fe.GotKeyboardFocus += (s, e) => { h.Focused = true; Refresh(); };
            fe.LostKeyboardFocus += (s, e) => { h.Focused = false; Refresh(); };
            fe.IsEnabledChanged += (s, e) => Refresh();
            h.Hover = fe.IsMouseOver; h.Focused = fe.IsKeyboardFocused;
        }

        /// <summary>
        /// Paints the three colour properties from the base style, then lets the active interaction states take over the channels they set.
        /// Disabled wins over pressed, pressed over focused, focused over hover.
        /// </summary>
        private static void PaintColours(FrameworkElement fe, Hooks h)
        {
            var s = h.Style ?? new ElementStyle();
            var o = Remembered.GetOrCreateValue(fe);
            var active = new List<StateLook>();
            if (s.HasStates)
            {
                if (!fe.IsEnabled) active.Add(s.Disabled);
                else
                {
                    if (h.Pressed) active.Add(s.Pressed);
                    if (h.Focused) active.Add(s.Focused);
                    if (h.Hover) active.Add(s.Hover);
                }
            }
            string Pick(Func<StateLook, string> get, string baseValue)
            {
                foreach (var l in active) { var v = l == null ? null : get(l); if (v != null) return v; }
                return baseValue;
            }
            Put(fe, o, TextElement.ForegroundProperty, Brush(Pick(l => l.Foreground, s.Foreground)));
            Put(fe, o, BackgroundProperty(fe), Brush(Pick(l => l.Background, s.Background)));
            Put(fe, o, BorderBrushProperty(fe), Brush(Pick(l => l.BorderColour, s.BorderColour)));
        }

        /// <summary>The colours an element would show in a given state ("normal", "hover", "pressed", "focused", "disabled"), used by previews and tests.</summary>
        public static StateLook ResolveState(ElementStyle s, string state)
        {
            var look = new StateLook { Foreground = s.Foreground, Background = s.Background, BorderColour = s.BorderColour };
            var over = s.StateOf(state, false);
            if (over != null)
            {
                look.Foreground = over.Foreground ?? look.Foreground;
                look.Background = over.Background ?? look.Background;
                look.BorderColour = over.BorderColour ?? look.BorderColour;
            }
            return look;
        }

        // ------------------------------------------------------------ letter spacing and underline

        /// <summary>
        /// Widens text by putting hair spaces between its letters (WPF text has no letter-spacing property) and draws or removes an underline.
        /// Only plain text is touched: text bound to data, or made of several styled runs, is left alone.
        /// </summary>
        public static string Spaced(string text, double pixels, double fontSize)
        {
            if (string.IsNullOrEmpty(text) || pixels <= 0) return text;
            int n = Math.Clamp((int)Math.Round(pixels / Math.Max(4, fontSize * 0.08)), 1, 12);
            var sb = new StringBuilder(text.Length * (n + 1));
            for (int i = 0; i < text.Length; i++)
            {
                sb.Append(text[i]);
                if (i < text.Length - 1 && !char.IsWhiteSpace(text[i]) && !char.IsWhiteSpace(text[i + 1])) sb.Append(Hair, n);
            }
            return sb.ToString();
        }

        private static void ApplyText(FrameworkElement fe, ElementStyle s)
        {
            var hk = HookTable.GetOrCreateValue(fe);
            bool wants = s.LetterSpacing != null || s.Underline != null;
            // an element that never had spacing or an underline has no text work to undo
            if (!wants && !hk.TextTouched) return;
            if (wants) hk.TextTouched = true;
            var targets = TextTargets(fe);
            double size = s.FontSize ?? (double)fe.GetValue(TextElement.FontSizeProperty);
            foreach (var t in targets)
            {
                if (t is TextBlock tb)
                {
                    if (tb.GetBindingExpression(TextBlock.TextProperty) != null || tb.Inlines.Any(i => i is not Run)) continue;
                    var m = TextMemos.GetOrCreateValue(tb);
                    if (m.Spaced == null || tb.Text != m.Spaced) m.Original = tb.Text;
                    var want = Spaced(m.Original, s.LetterSpacing ?? 0, size);
                    if (tb.Text != want) tb.Text = want;
                    m.Spaced = (s.LetterSpacing ?? 0) > 0 ? want : null;
                    ApplyDecoration(tb, m, s.Underline);
                }
                else if (t is ContentControl cc)
                {
                    var m = TextMemos.GetOrCreateValue(cc);
                    var current = cc.Content as string;
                    if (current == null) continue;
                    if (m.Spaced == null || current != m.Spaced) m.Original = current;
                    var want = Spaced(m.Original, s.LetterSpacing ?? 0, size);
                    if (current != want) cc.Content = want;
                    m.Spaced = (s.LetterSpacing ?? 0) > 0 ? want : null;
                }
            }
            // the text blocks inside a templated control (a button's own label) only exist once it has been loaded
            if (!fe.IsLoaded && (s.LetterSpacing != null || s.Underline != null))
            {
                var h = HookTable.GetOrCreateValue(fe);
                if (!h.LoadedWired)
                {
                    h.LoadedWired = true;
                    fe.Loaded += (o, e) => { if (h.Style != null) ApplyText(fe, h.Style); };
                }
            }
        }

        private static void ApplyDecoration(TextBlock tb, TextMemo m, bool? underline)
        {
            if (!m.DecorationsSaved) { m.Decorations = tb.ReadLocalValue(TextBlock.TextDecorationsProperty); m.DecorationsSaved = true; }
            if (underline == true) tb.TextDecorations = TextDecorations.Underline;
            else if (underline == false) tb.TextDecorations = new TextDecorationCollection();
            else if (m.Decorations == DependencyProperty.UnsetValue) tb.ClearValue(TextBlock.TextDecorationsProperty);
            else tb.SetValue(TextBlock.TextDecorationsProperty, m.Decorations);
        }

        /// <summary>The text elements a look changes: the element itself, a string used as button content, and the text blocks inside it (not inside a nested button).</summary>
        private static List<DependencyObject> TextTargets(FrameworkElement fe)
        {
            var list = new List<DependencyObject>();
            var seen = new HashSet<DependencyObject>();
            void Add(DependencyObject d) { if (seen.Add(d)) list.Add(d); }
            if (fe is TextBlock) Add(fe);
            if (fe is ContentControl cc && cc.Content is string) Add(cc);
            if (fe is Control c) c.ApplyTemplate();
            void Visit(DependencyObject d, bool root)
            {
                if (!root && d is ButtonBase) return;
                if (d is TextBlock) Add(d);
                if (!root && d is ContentControl inner && inner.Content is string && d is not ButtonBase) Add(d);
                foreach (var ch in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>()) Visit(ch, false);
                if (d is Visual)
                {
                    int n = VisualTreeHelper.GetChildrenCount(d);
                    for (int i = 0; i < n; i++) Visit(VisualTreeHelper.GetChild(d, i), false);
                }
            }
            Visit(fe, true);
            return list;
        }

        // ------------------------------------------------------------ storing

        /// <summary>
        /// Stores a style for a key (removing it when empty) and optionally saves the settings file.
        /// Returns false, changing nothing, when the look is frozen by an appearance lock.
        /// </summary>
        public static bool Store(string key, ElementStyle s, bool save)
        {
            if (IsLocked(key)) return false;
            s?.Tidy();
            if (s == null || s.IsEmpty) Map.Remove(key); else Map[key] = s;
            if (save) Settings.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes one element's style and restores every matching element in the open windows. Returns false when the look is locked.</summary>
        public static bool Reset(string key, FrameworkElement target)
        {
            if (IsLocked(key)) return false;
            Map.Remove(key);
            Settings.Save();
            // Keys without a page part ("tab:..." and "group:...") belong to the tab strip, which rebuilds itself from the change event.
            if (target != null && key.IndexOf('|') >= 0) Apply(target, new ElementStyle());
            RestoreMatching(key);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes every saved style that is not frozen by a lock, and returns how many looks were kept because of a lock.</summary>
        public static int ResetAll()
        {
            var keys = Map.Keys.ToList();
            int kept = 0;
            foreach (var k in keys)
            {
                if (IsLocked(k)) { kept++; continue; }
                Map.Remove(k);
            }
            Settings.Save();
            foreach (var k in keys) if (!Map.ContainsKey(k)) RestoreMatching(k);
            Changed?.Invoke();
            return kept;
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
            if (cut < 0 || Application.Current == null) return;
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

        // ------------------------------------------------------------ comparing and describing

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 16
        };

        /// <summary>A compact text form of one style, used to compare two styles and to keep editor undo snapshots.</summary>
        public static string Snapshot(ElementStyle s)
        {
            var c = s?.Clone() ?? new ElementStyle();
            c.Tidy();
            return JsonSerializer.Serialize(c, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        }

        public static ElementStyle FromSnapshot(string snapshot)
        {
            try { return JsonSerializer.Deserialize<ElementStyle>(snapshot, JsonOptions) ?? new ElementStyle(); }
            catch { return new ElementStyle(); }
        }

        public static bool Same(ElementStyle a, ElementStyle b) => Snapshot(a) == Snapshot(b);

        /// <summary>Names the properties that differ between two styles, for a history line.</summary>
        public static string Describe(ElementStyle before, ElementStyle after)
        {
            before ??= new ElementStyle(); after ??= new ElementStyle();
            var names = new List<string>();
            void Cmp(string n, object x, object y) { if (!Equals(x, y)) names.Add(n); }
            Cmp("font", before.FontFamily, after.FontFamily); Cmp("size", before.FontSize, after.FontSize); Cmp("weight", before.FontWeight, after.FontWeight);
            Cmp("italic", before.Italic, after.Italic); Cmp("letter spacing", before.LetterSpacing, after.LetterSpacing); Cmp("underline", before.Underline, after.Underline);
            Cmp("text colour", before.Foreground, after.Foreground); Cmp("background", before.Background, after.Background);
            Cmp("corner radius", before.CornerRadius, after.CornerRadius); Cmp("padding", before.Padding, after.Padding);
            Cmp("border", before.BorderThickness, after.BorderThickness); Cmp("border colour", before.BorderColour, after.BorderColour);
            foreach (var st in new[] { "hover", "pressed", "focused", "disabled" })
                if (Snapshot(new ElementStyle { Hover = before.StateOf(st, false) }) != Snapshot(new ElementStyle { Hover = after.StateOf(st, false) })) names.Add(st + " look");
            if (names.Count == 0) return "No change";
            return "Changed " + string.Join(", ", names);
        }

        // ------------------------------------------------------------ presets

        public static IReadOnlyList<string> PresetNames =>
            (Settings.Current.StylePresets ??= new Dictionary<string, ElementStyle>()).Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase).ToList();

        public static ElementStyle Preset(string name) =>
            Settings.Current.StylePresets != null && name != null && Settings.Current.StylePresets.TryGetValue(name, out var p) ? p.Clone() : null;

        /// <summary>Saves a copy of a look under a name. Returns an error message, or null on success. An existing name is replaced.</summary>
        public static string SavePreset(string name, ElementStyle style)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "Give the preset a name first.";
            if (name.Length > 40) return "Preset names can be at most 40 characters.";
            if (style == null || style.IsEmpty) return "This element has no custom look to save. Change something first.";
            var all = Settings.Current.StylePresets ??= new Dictionary<string, ElementStyle>();
            if (!all.ContainsKey(name) && all.Count >= MaxPresets) return $"You can keep at most {MaxPresets} presets. Delete one first.";
            var copy = style.Clone(); copy.Tidy();
            all[name] = copy;
            Settings.Save();
            Changed?.Invoke();
            return null;
        }

        public static bool DeletePreset(string name)
        {
            var all = Settings.Current.StylePresets;
            if (all == null || !all.Remove(name)) return false;
            Settings.Save();
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ history

        public static IReadOnlyList<StyleHistoryEntry> History => (Settings.Current.StyleHistory ??= new List<StyleHistoryEntry>()).AsReadOnly();

        public static void RecordHistory(string key, ElementStyle before, ElementStyle after, string summary = null)
        {
            var list = Settings.Current.StyleHistory ??= new List<StyleHistoryEntry>();
            list.Add(new StyleHistoryEntry
            {
                At = DateTime.Now, Key = key ?? "", Summary = summary ?? Describe(before, after),
                Before = before?.Clone(), After = after?.Clone()
            });
            if (list.Count > MaxHistory) list.RemoveRange(0, list.Count - MaxHistory);
            Settings.Save();
        }

        public static void ClearHistory()
        {
            Settings.Current.StyleHistory = new List<StyleHistoryEntry>();
            Settings.Save();
        }

        /// <summary>Puts a history entry's look back. Returns an error message, or null on success.</summary>
        public static string Restore(StyleHistoryEntry e)
        {
            if (e == null || e.After == null && e.Before == null && string.IsNullOrEmpty(e.Key)) return "That entry cannot be restored.";
            if (string.IsNullOrEmpty(e.Key) || e.Key == "*") return "That entry covers many elements and cannot be restored one by one.";
            if (IsLocked(e.Key)) return "This look is locked. Unlock it first.";
            var before = Get(e.Key)?.Clone();
            Store(e.Key, e.After?.Clone(), true);
            ApplyMatching(e.Key);
            RecordHistory(e.Key, before, e.After, "Restored: " + e.Summary);
            return null;
        }

        // ------------------------------------------------------------ validation, export and import

        /// <summary>Checks one style's values are in range and its colours are real. Returns an error message, or null when it is fine.</summary>
        public static string Validate(ElementStyle s)
        {
            if (s == null) return "empty style";
            if (s.FontFamily != null && (s.FontFamily.Length > 100 || s.FontFamily.Any(char.IsControl))) return "font name is too long or has control characters";
            if (s.FontSize is double fs && !(fs >= 1 && fs <= 400)) return "font size must be 1 to 400";
            if (s.FontWeight is int w && (w < 1 || w > 999)) return "font weight must be 1 to 999";
            if (s.LetterSpacing is double ls && !(ls >= 0 && ls <= 12)) return "letter spacing must be 0 to 12";
            if (s.CornerRadius is double cr && !(cr >= 0 && cr <= 200)) return "corner radius must be 0 to 200";
            if (s.Padding is double p && !(p >= 0 && p <= 200)) return "padding must be 0 to 200";
            if (s.BorderThickness is double bt && !(bt >= 0 && bt <= 50)) return "border thickness must be 0 to 50";
            foreach (var (name, hex) in new[] { ("text colour", s.Foreground), ("background", s.Background), ("border colour", s.BorderColour) })
                if (hex != null && (hex.Length > 12 || !TryColour(hex, out _))) return name + " is not a colour";
            foreach (var st in new[] { "hover", "pressed", "focused", "disabled" })
            {
                var l = s.StateOf(st, false);
                if (l == null) continue;
                foreach (var hex in new[] { l.Foreground, l.Background, l.BorderColour })
                    if (hex != null && (hex.Length > 12 || !TryColour(hex, out _))) return st + " look has a value that is not a colour";
            }
            return null;
        }

        /// <summary>Every saved look and preset as one JSON document.</summary>
        public static string ExportJson()
        {
            var bundle = new StyleBundle();
            foreach (var kv in Map) bundle.Styles[kv.Key] = kv.Value;
            foreach (var kv in Settings.Current.StylePresets ?? new Dictionary<string, ElementStyle>()) bundle.Presets[kv.Key] = kv.Value;
            return JsonSerializer.Serialize(bundle, JsonOptions);
        }

        /// <summary>
        /// Checks a JSON document completely and, only if all of it is acceptable, merges its looks and presets into the saved ones.
        /// Nothing is applied when anything is wrong, too large, or would change a locked look. Returns an error message, or null on success.
        /// </summary>
        public static string ImportJson(string json, out int styles, out int presets)
        {
            styles = 0; presets = 0;
            if (string.IsNullOrWhiteSpace(json)) return "The file is empty.";
            if (Encoding.UTF8.GetByteCount(json) > MaxImportBytes) return "The file is larger than " + (MaxImportBytes / 1000) + " KB, which is more than a set of looks needs.";
            StyleBundle b;
            try { b = JsonSerializer.Deserialize<StyleBundle>(json, JsonOptions); }
            catch (Exception ex) { return "The file is not valid JSON for element looks: " + ex.Message; }
            // a file that never names its format must not pass because the class fills in the right name by default
            bool named = false;
            try { using var doc = JsonDocument.Parse(json); named = doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.EnumerateObject().Any(p => p.Name.Equals("format", StringComparison.OrdinalIgnoreCase)); } catch { }
            if (b == null || !named || b.Format != "monitoringtools.element-styles") return "This is not an element looks file (its format name is wrong).";
            if (b.Version != 1) return $"This file is version {b.Version}; only version 1 is understood.";
            b.Styles ??= new Dictionary<string, ElementStyle>(); b.Presets ??= new Dictionary<string, ElementStyle>();
            if (b.Styles.Count > MaxImportStyles) return $"The file has more than {MaxImportStyles} looks.";
            if (b.Presets.Count > MaxPresets) return $"The file has more than {MaxPresets} presets.";
            foreach (var kv in b.Styles)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Key.Length > 200) return "A look has an empty or oversized key.";
                var err = Validate(kv.Value);
                if (err != null) return $"Look \"{Short(kv.Key)}\": {err}.";
                if (IsLocked(kv.Key)) return $"Look \"{Short(kv.Key)}\" is locked, so the import was stopped before changing anything. Unlock it first.";
            }
            foreach (var kv in b.Presets)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Key.Length > 40) return "A preset has an empty or oversized name.";
                var err = Validate(kv.Value);
                if (err != null) return $"Preset \"{Short(kv.Key)}\": {err}.";
            }
            var all = Settings.Current.StylePresets ??= new Dictionary<string, ElementStyle>();
            if (all.Keys.Union(b.Presets.Keys).Count() > MaxPresets) return $"Importing would exceed {MaxPresets} presets.";

            // everything checked: apply it
            foreach (var kv in b.Styles) { var s = kv.Value.Clone(); s.Tidy(); if (s.IsEmpty) Map.Remove(kv.Key); else Map[kv.Key] = s; styles++; }
            foreach (var kv in b.Presets) { var s = kv.Value.Clone(); s.Tidy(); if (!s.IsEmpty) { all[kv.Key] = s; presets++; } }
            Settings.Save();
            foreach (var k in b.Styles.Keys) ApplyMatching(k);
            RecordHistory("*", null, null, $"Imported {styles} looks and {presets} presets");
            Changed?.Invoke();
            return null;
        }

        private static string Short(string s) => s.Length > 40 ? s[..40] + "…" : s;
    }
}
