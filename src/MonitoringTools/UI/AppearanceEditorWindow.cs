using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A non-modal editor anchored beside one element. It changes that element's font, colours and shape live, saves each change under
    /// a stable key (page plus element text), and lets each property, the element, or everything be reset.
    /// </summary>
    public sealed class AppearanceEditorWindow : Window
    {
        private static AppearanceEditorWindow _open;

        private readonly Window _owner;
        private readonly FrameworkElement _target;
        private readonly string _key;
        private readonly ElementStyle _style;
        private readonly StyleSupport _support;
        private readonly Border _preview = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 10) };
        private readonly TextBlock _previewText = new TextBlock { Text = "Sample text 123", TextWrapping = TextWrapping.Wrap };
        private readonly ContentControl _body = new ContentControl { IsTabStop = false };
        private readonly List<(Button Chip, string Id)> _tabs = new List<(Button, string)>();
        private readonly Dictionary<string, FrameworkElement> _pages = new Dictionary<string, FrameworkElement>();
        private ColorPicker _picker;
        private string _colourRole;
        private readonly List<Action> _refreshers = new List<Action>();

        /// <summary>Opens the editor for one element, beside it. Any editor already open is replaced.</summary>
        public static void Open(Window owner, FrameworkElement target)
        {
            if (target == null) return;
            _open?.Close();
            var w = new AppearanceEditorWindow(owner, target);
            _open = w;
            w.Show();
        }

        private AppearanceEditorWindow(Window owner, FrameworkElement target)
        {
            _owner = owner;
            _target = target;
            _key = ElementStyles.KeyFor(ElementStyles.PageIdFor(owner), target);
            _style = ElementStyles.Get(_key)?.Clone() ?? new ElementStyle();
            _support = ElementStyles.Support(target);

            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.Height; Width = 420;
            MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 24);
            if (owner != null && owner.IsLoaded) Owner = owner;
            AutomationProperties.SetName(this, "Edit appearance");
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            Closed += (s, e) => { if (_open == this) _open = null; };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox)) { e.Handled = true; Close(); } };
            ContentRendered += (s, e) => Clamp();

            PlaceBeside();
            Content = Build();
            SwitchTab("font");
            UpdatePreview();
        }

        // ------------------------------------------------------------ placement

        private void PlaceBeside()
        {
            var src = PresentationSource.FromVisual(_target);
            var m = src?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var tl = m.Transform(_target.PointToScreen(new Point(0, 0)));
            var br = m.Transform(_target.PointToScreen(new Point(_target.ActualWidth, _target.ActualHeight)));
            double vsL = SystemParameters.VirtualScreenLeft, vsR = vsL + SystemParameters.VirtualScreenWidth;
            double x = br.X + 8;
            if (x + Width > vsR) x = tl.X - Width - 8;
            if (x < vsL) x = Math.Max(vsL + 8, Math.Min(tl.X, vsR - Width - 8));
            Left = x; Top = tl.Y;
        }

        private void Clamp()
        {
            double vsT = SystemParameters.VirtualScreenTop, vsB = vsT + SystemParameters.VirtualScreenHeight;
            if (Top + ActualHeight > vsB - 8) Top = Math.Max(vsT + 8, vsB - ActualHeight - 8);
            if (Top < vsT + 8) Top = vsT + 8;
        }

        // ------------------------------------------------------------ layout

        private FrameworkElement Build()
        {
            var text = ElementStyles.TextOf(_target);
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titles = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock { Text = "Edit appearance", Style = Ui.Style("T.Title"), FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
            titleRow.Children.Add(InfoIcon.Create("the appearance editor",
                "Changes how this one element looks: its font, colours and shape. Changes show at once and are saved by page and element text, so they come back when the page is rebuilt. " +
                "Every other element with the same text on this page gets the same look. Each property has its own reset, and Reset element returns this element to the theme.",
                null, null, () => new TextBlock { Text = "Sample text 123", Foreground = Ui.Res("B.Text"), FontSize = 16, Margin = new Thickness(0, 4, 0, 0) }));
            titles.Children.Add(titleRow);
            titles.Children.Add(new TextBlock { Text = text.Length == 0 ? "Unnamed element" : (text.Length > 48 ? text[..48] + "…" : text), Foreground = Ui.Res("B.TextMute"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            head.Children.Add(titles);
            var close = new Button { Style = Ui.Style("Btn.Icon"), Content = Ui.Icon(Icons.Close, Ui.Res("B.TextSoft"), 16), ToolTip = "Close (Esc)" };
            AutomationProperties.SetName(close, "Close appearance editor");
            close.Click += (s, e) => Close();
            Grid.SetColumn(close, 1); head.Children.Add(close);
            head.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is not Button) try { DragMove(); } catch { } };
            root.Children.Add(head);

            var middle = new StackPanel();
            _preview.Child = _previewText;
            middle.Children.Add(_preview);
            var tabRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var (id, label) in new[] { ("font", "Font"), ("colour", "Colour"), ("shape", "Shape") })
            {
                var chip = Ui.Button(label, () => SwitchTab(id), "Btn.Chip");
                AutomationProperties.SetName(chip, label + " settings");
                _tabs.Add((chip, id)); tabRow.Children.Add(chip);
            }
            middle.Children.Add(tabRow);
            middle.Children.Add(_body);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = middle, Padding = new Thickness(0, 0, 4, 0) };
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);

            var foot = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            foot.Children.Add(Ui.Button("Reset element", ResetElement, "Btn.Ghost", Icons.Restart));
            foot.Children.Add(Ui.Button("Reset all…", ResetAll, "Btn.Danger", Icons.Trash));
            Grid.SetRow(foot, 2); root.Children.Add(foot);

            var caption = new TextBlock
            {
                Text = "Saved by page and element text. Elements that share the same text on this page share the look. Cards recolour themselves when the pointer passes over them, so a background set on a card may not hold until that is changed.",
                Foreground = Ui.Res("B.TextMute"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
            };
            middle.Children.Add(caption);

            return new Grid
            {
                Margin = new Thickness(14),
                Children =
                {
                    new Border
                    {
                        Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(28), Padding = new Thickness(20, 18, 16, 16), Child = root,
                        Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"]
                    }
                }
            };
        }

        private void SwitchTab(string id)
        {
            if (!_pages.TryGetValue(id, out var page))
            {
                page = id switch { "font" => BuildFont(), "colour" => BuildColour(), _ => BuildShape() };
                _pages[id] = page;
            }
            _body.Content = page;
            foreach (var (chip, tid) in _tabs)
            {
                bool on = tid == id;
                chip.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                chip.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                chip.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                AutomationProperties.SetItemStatus(chip, on ? "selected" : "not selected");
            }
        }

        // ------------------------------------------------------------ applying

        private void Live()
        {
            ElementStyles.Store(_key, _style.IsEmpty ? null : _style, save: false);
            ElementStyles.ApplyMatching(_key);
            UpdatePreview();
        }

        private void Persist()
        {
            ElementStyles.Store(_key, _style.IsEmpty ? null : _style, save: true);
        }

        private void Edit(Action change)
        {
            change();
            Live();
            Persist();
            foreach (var r in _refreshers) r();
        }

        private void UpdatePreview()
        {
            var eff = ElementStyles.Effective(_target);
            string F(string a, string b) => a ?? b;
            _previewText.FontFamily = new FontFamily(F(_style.FontFamily, eff.FontFamily) ?? "Segoe UI");
            _previewText.FontSize = _style.FontSize ?? Math.Min(eff.FontSize ?? 14, 32);
            _previewText.FontWeight = FontWeight.FromOpenTypeWeight(_style.FontWeight ?? eff.FontWeight ?? 400);
            _previewText.FontStyle = (_style.Italic ?? eff.Italic ?? false) ? FontStyles.Italic : FontStyles.Normal;
            _previewText.Foreground = ElementStyles.TryColour(F(_style.Foreground, eff.Foreground), out var fg) ? new SolidColorBrush(fg) : Ui.Res("B.Text");
            _preview.Background = ElementStyles.TryColour(F(_style.Background, eff.Background), out var bg) ? new SolidColorBrush(bg) : Ui.Res("B.Sunken");
            _preview.CornerRadius = new CornerRadius(_style.CornerRadius ?? (eff.CornerRadius ?? 12));
            _preview.BorderThickness = new Thickness(_style.BorderThickness ?? 0);
            _preview.BorderBrush = ElementStyles.TryColour(_style.BorderColour, out var bc) ? new SolidColorBrush(bc) : Ui.Res("B.LineStrong");
        }

        private void ResetElement()
        {
            ElementStyles.Reset(_key, _target);
            _style.FontFamily = null; _style.FontSize = null; _style.FontWeight = null; _style.Italic = null; _style.Foreground = null; _style.Background = null;
            _style.CornerRadius = null; _style.Padding = null; _style.BorderThickness = null; _style.BorderColour = null;
            UpdatePreview();
            foreach (var r in _refreshers) r();
        }

        private void ResetAll()
        {
            var ask = new ConfirmWindow("Reset every appearance edit?",
                $"This removes all {Math.Max(1, ElementStyles.Count)} saved element looks from every page and returns them to the theme. The theme itself and your other settings stay as they are.",
                "Reset all", danger: true);
            ask.Confirmed += yes =>
            {
                if (!yes) return;
                ElementStyles.ResetAll();
                _style.FontFamily = null; _style.FontSize = null; _style.FontWeight = null; _style.Italic = null; _style.Foreground = null; _style.Background = null;
                _style.CornerRadius = null; _style.Padding = null; _style.BorderThickness = null; _style.BorderColour = null;
                UpdatePreview();
                foreach (var r in _refreshers) r();
            };
            ask.Show();
        }

        // ------------------------------------------------------------ small building blocks

        private FrameworkElement Section(string title, Action reset, FrameworkElement content, string info = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(new TextBlock { Text = title, Foreground = Ui.Res("B.Text"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            if (info != null) left.Children.Add(InfoIcon.Create(title, info, null, null));
            row.Children.Add(left);
            if (reset != null)
            {
                var r = Ui.Button("Reset", () => Edit(reset), "Btn.Chip");
                r.Height = 26; r.Margin = new Thickness(0); r.ToolTip = "Return " + title.ToLowerInvariant() + " to the theme's value";
                AutomationProperties.SetName(r, "Reset " + title);
                Grid.SetColumn(r, 1); row.Children.Add(r);
            }
            sp.Children.Add(row);
            content.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(content);
            return sp;
        }

        private FrameworkElement Unavailable(string title, string why)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Opacity = 0.7 };
            sp.Children.Add(new TextBlock { Text = title, Foreground = Ui.Res("B.TextSoft"), FontSize = 14 });
            sp.Children.Add(new TextBlock { Text = why, Foreground = Ui.Res("B.TextMute"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            return sp;
        }

        /// <summary>A number field with minus and plus buttons. <paramref name="get"/> supplies the shown value and is called again after any reset.</summary>
        private FrameworkElement Stepper(string name, Func<double> get, double min, double max, double step, Action<double> set)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var box = new TextBox { Style = Ui.Style("Input"), Width = 80, Margin = new Thickness(6, 0, 6, 0), TextAlignment = TextAlignment.Center };
            AutomationProperties.SetName(box, name);
            void Show() => box.Text = Math.Round(get(), 1).ToString("0.#", CultureInfo.InvariantCulture);
            void Commit(double v) { v = Math.Clamp(v, min, max); Edit(() => set(v)); }
            var minus = new Button { Style = Ui.Style("Btn.Icon"), Width = 36, Height = 36, Content = new TextBlock { Text = "−", FontSize = 18, Foreground = Ui.Res("B.TextSoft") } };
            var plus = new Button { Style = Ui.Style("Btn.Icon"), Width = 36, Height = 36, Content = new TextBlock { Text = "+", FontSize = 18, Foreground = Ui.Res("B.TextSoft") } };
            AutomationProperties.SetName(minus, "Decrease " + name); AutomationProperties.SetName(plus, "Increase " + name);
            minus.Click += (s, e) => Commit(get() - step);
            plus.Click += (s, e) => Commit(get() + step);
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter && double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { e.Handled = true; Commit(v); } };
            box.LostKeyboardFocus += (s, e) => { if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && Math.Abs(v - get()) > 0.001) Commit(v); else Show(); };
            row.Children.Add(minus); row.Children.Add(box); row.Children.Add(plus);
            row.Children.Add(new TextBlock { Text = $"{min:0.#} to {max:0.#}", Foreground = Ui.Res("B.TextMute"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
            _refreshers.Add(Show);
            Show();
            return row;
        }

        // ------------------------------------------------------------ font tab

        private FrameworkElement BuildFont()
        {
            var eff = ElementStyles.Effective(_target);
            var sp = new StackPanel();

            // family with search over every installed font
            var current = new TextBlock { FontSize = 16, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis };
            void ShowFamily()
            {
                var name = _style.FontFamily ?? eff.FontFamily ?? "Segoe UI";
                current.Text = name + (_style.FontFamily == null ? "  (theme)" : "");
                try { current.FontFamily = new FontFamily(name); } catch { }
            }
            _refreshers.Add(ShowFamily); ShowFamily();

            var all = Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
            var search = new SearchField("appearance:fonts", "Search fonts", compact: true) { Margin = new Thickness(0, 4, 0, 6) };
            search.SampleSource = () => all;
            var list = new ListBox { Height = 150, BorderThickness = new Thickness(0), Background = Ui.Res("B.Sunken"), HorizontalContentAlignment = HorizontalAlignment.Stretch, ItemContainerStyle = FontItemStyle() };
            AutomationProperties.SetName(list, "Installed fonts");
            bool filling = false;
            void Fill()
            {
                var shown = all.Where(n => search.State.Matches(n)).ToList();
                search.SetCount(shown.Count, all.Count, "fonts");
                filling = true;
                list.Items.Clear();
                foreach (var n in shown)
                {
                    var tb = new TextBlock { Text = n, FontSize = 15, Padding = new Thickness(10, 6, 10, 6), Tag = n, Foreground = Ui.Res("B.Text") };
                    try { tb.FontFamily = new FontFamily(n); } catch { }
                    list.Items.Add(tb);
                    if (n == _style.FontFamily) list.SelectedItem = tb;
                }
                filling = false;
            }
            search.Filter += Fill;
            list.SelectionChanged += (s, e) =>
            {
                if (filling || list.SelectedItem is not TextBlock tb) return;
                Edit(() => _style.FontFamily = (string)tb.Tag);
            };
            Fill();
            var fam = new StackPanel();
            fam.Children.Add(current); fam.Children.Add(search); fam.Children.Add(new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = list });
            sp.Children.Add(Section("Font family", () => _style.FontFamily = null, fam,
                "Lists every font installed on this computer, each name drawn in its own face. Type to filter; use the regex button next to the search for patterns. The theme's font is used until you choose one."));

            sp.Children.Add(Section("Size", () => _style.FontSize = null,
                Stepper("font size", () => _style.FontSize ?? eff.FontSize ?? 14, 8, 72, 1, v => _style.FontSize = v),
                "Text size in device-independent pixels, 8 to 72. Type a value or use the buttons."));

            // weight
            var weights = new WrapPanel();
            var wb = new List<(Button B, int W)>();
            void PaintW() { foreach (var (b, w) in wb) { bool on = (_style.FontWeight ?? eff.FontWeight ?? 400) == w; b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised"); b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft"); } }
            foreach (var (label, w) in new[] { ("Light", 300), ("Regular", 400), ("Medium", 500), ("Semibold", 600), ("Bold", 700), ("Black", 900) })
            {
                int weight = w;
                var b = Ui.Button(label, () => Edit(() => _style.FontWeight = weight), "Btn.Chip");
                AutomationProperties.SetName(b, label + " weight");
                wb.Add((b, w)); weights.Children.Add(b);
            }
            _refreshers.Add(PaintW); PaintW();
            sp.Children.Add(Section("Weight", () => _style.FontWeight = null, weights, "How heavy the strokes are. A font without the chosen weight draws its nearest one."));

            // italic
            var it = new CheckBox { Style = Ui.Style("Switch"), Tag = Ui.Res("B.Mint"), IsChecked = _style.Italic ?? eff.Italic ?? false, Content = new TextBlock { Text = "Italic", Foreground = Ui.Res("B.Text"), FontSize = 14 } };
            AutomationProperties.SetName(it, "Italic");
            it.Checked += (s, e) => Edit(() => _style.Italic = true);
            it.Unchecked += (s, e) => Edit(() => _style.Italic = false);
            _refreshers.Add(() => it.IsChecked = _style.Italic ?? ElementStyles.Effective(_target).Italic ?? false);
            sp.Children.Add(Section("Style", () => _style.Italic = null, it));
            return sp;
        }

        private static Style FontItemStyle()
        {
            var st = new Style(typeof(ListBoxItem));
            var tpl = new ControlTemplate(typeof(ListBoxItem));
            var bd = new FrameworkElementFactory(typeof(Border), "Bd");
            bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            var sel = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            sel.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Res("B.SecondaryContainer"), "Bd"));
            var hov = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Res("B.Surface"), "Bd"));
            tpl.Triggers.Add(hov); tpl.Triggers.Add(sel);
            st.Setters.Add(new Setter(Control.TemplateProperty, tpl));
            st.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            return st;
        }

        // ------------------------------------------------------------ colour tab

        private FrameworkElement BuildColour()
        {
            var sp = new StackPanel();
            var roles = new List<(string Id, string Label)> { ("fg", "Text colour") };
            if (_support.Background) roles.Add(("bg", "Background"));
            if (_support.Border) roles.Add(("border", "Border colour"));
            _colourRole = "fg";

            var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var cb = new List<(Button B, string Id)>();
            void PaintChips() { foreach (var (b, id) in cb) { bool on = id == _colourRole; b.Background = on ? Ui.Res("B.SecondaryContainer") : Ui.Res("B.Raised"); b.Foreground = on ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft"); } }
            foreach (var (id, label) in roles)
            {
                var rid = id;
                var b = Ui.Button(label, () => { _colourRole = rid; LoadPicker(); PaintChips(); }, "Btn.Chip");
                AutomationProperties.SetName(b, "Edit " + label);
                cb.Add((b, id)); chips.Children.Add(b);
            }
            sp.Children.Add(chips);

            _picker = new ColorPicker(Color.FromRgb(0, 0, 0));
            _picker.Changed += c => { SetRole(c); Live(); };
            _picker.Committed += c => { SetRole(c); Live(); Persist(); };
            sp.Children.Add(_picker);

            var reset = Ui.Button("Reset this colour", () => Edit(() => { ClearRole(); }), "Btn.Ghost", Icons.Restart);
            reset.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(reset);
            if (!_support.Background) sp.Children.Add(new TextBlock { Text = "This element has no background of its own to colour.", FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 4, 0, 0) });

            _refreshers.Add(LoadPicker);
            LoadPicker();
            PaintChips();
            return sp;
        }

        private void SetRole(Color c)
        {
            var hex = ElementStyles.Hex(c);
            if (_colourRole == "fg") _style.Foreground = hex; else if (_colourRole == "bg") _style.Background = hex; else _style.BorderColour = hex;
            // a visible border needs a thickness, so give it one the first time a colour is chosen
            if (_colourRole == "border" && (_style.BorderThickness ?? 0) <= 0) _style.BorderThickness = 1;
        }

        private void ClearRole()
        {
            if (_colourRole == "fg") _style.Foreground = null; else if (_colourRole == "bg") _style.Background = null; else _style.BorderColour = null;
        }

        private void LoadPicker()
        {
            if (_picker == null) return;
            var eff = ElementStyles.Effective(_target);
            string hex = _colourRole switch { "fg" => _style.Foreground ?? eff.Foreground, "bg" => _style.Background ?? eff.Background, _ => _style.BorderColour ?? eff.BorderColour };
            _picker.SetColor(ElementStyles.TryColour(hex, out var c) ? c : (_colourRole == "fg" ? Colors.Black : Colors.Transparent), false);
            string other = _colourRole == "fg" ? (_style.Background ?? eff.Background) : (_style.Foreground ?? eff.Foreground);
            Color? against = ElementStyles.TryColour(other, out var o) ? Color.FromRgb(o.R, o.G, o.B) : ((SolidColorBrush)Ui.Res("B.Surface")).Color;
            _picker.Against = against;
        }

        // ------------------------------------------------------------ shape tab

        private FrameworkElement BuildShape()
        {
            var eff = ElementStyles.Effective(_target);
            var sp = new StackPanel();
            if (_support.Radius)
                sp.Children.Add(Section("Corner radius", () => _style.CornerRadius = null,
                    Stepper("corner radius", () => _style.CornerRadius ?? eff.CornerRadius ?? 0, 0, 60, 2, v => _style.CornerRadius = v),
                    "How round the corners are, 0 to 60. A value of half the height makes a pill."));
            else
                sp.Children.Add(Unavailable("Corner radius", "This is a control whose shape comes from the theme template, so its corners cannot be edited here. Cards and panels can."));

            if (_support.Padding)
                sp.Children.Add(Section("Padding", () => _style.Padding = null,
                    Stepper("padding", () => _style.Padding ?? eff.Padding ?? 0, 0, 60, 2, v => _style.Padding = v),
                    "Space between the element's edge and what is inside it, applied equally on all four sides."));
            else
                sp.Children.Add(Unavailable("Padding", "This element does not have padding of its own."));

            if (_support.Border)
                sp.Children.Add(Section("Border thickness", () => { _style.BorderThickness = null; _style.BorderColour = null; },
                    Stepper("border thickness", () => _style.BorderThickness ?? eff.BorderThickness ?? 0, 0, 12, 1, v => _style.BorderThickness = v),
                    "Width of the outline, 0 to 12. Choose its colour on the Colour tab."));
            else
                sp.Children.Add(Unavailable("Border", "This element has no border to edit."));
            return sp;
        }
    }
}
