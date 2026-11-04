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

        // session history: snapshots of this element's look, for undo and redo
        private readonly List<string> _undo = new List<string>();
        private readonly List<string> _redo = new List<string>();
        private string _last;
        private string _state = "normal";
        private readonly TextBlock _status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 8, 0, 0) };
        private Button _undoBtn, _redoBtn;
        private Border _lockBanner;
        private Action _lockRefresh;
        private SearchField _libSearch;
        private StackPanel _presetList, _historyList;
        private TextBlock _clipNote;
        private FrameworkElement _libAnchor;

        /// <summary>Opens the editor for one element, beside it. Any editor already open is replaced.</summary>
        public static void Open(Window owner, FrameworkElement target, string key = null)
        {
            if (target == null) return;
            _open?.Close();
            var w = new AppearanceEditorWindow(owner, target, key);
            _open = w;
            w.Show();
        }

        private AppearanceEditorWindow(Window owner, FrameworkElement target, string key)
        {
            _owner = owner;
            _target = target;
            _key = key ?? ElementStyles.KeyFor(ElementStyles.PageIdFor(owner), target);
            _style = ElementStyles.Get(_key)?.Clone() ?? new ElementStyle();
            _support = ElementStyles.Support(target);
            _last = ElementStyles.Snapshot(_style);

            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.Height; Width = 420;
            MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 24);
            if (owner != null && owner.IsLoaded) Owner = owner;
            AutomationProperties.SetName(this, "Edit appearance");
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);   // editing the editor's own look from inside it would replace it mid-edit
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            Closed += (s, e) => { if (_open == this) _open = null; Locks.Changed -= OnLocksChanged; };
            Locks.Changed += OnLocksChanged;
            PreviewKeyDown += (s, e) =>
            {
                bool inText = Keyboard.FocusedElement is TextBox;
                if (e.Key == Key.Escape && !inText) { e.Handled = true; Close(); }
                else if (!inText && Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { e.Handled = true; Undo(); }
                else if (!inText && Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { e.Handled = true; Redo(); }
            };
            ContentRendered += (s, e) => Clamp();

            PlaceBeside();
            Content = Build();
            SwitchTab("font");
            UpdatePreview();
            RefreshLockUi();
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
            _undoBtn = new Button { Style = Ui.Style("Btn.Icon"), Content = new TextBlock { Text = "↶", FontSize = 18, Foreground = Ui.Res("B.TextSoft") }, ToolTip = "Undo the last change in this editor (Ctrl+Z)" };
            _redoBtn = new Button { Style = Ui.Style("Btn.Icon"), Content = new TextBlock { Text = "↷", FontSize = 18, Foreground = Ui.Res("B.TextSoft") }, ToolTip = "Redo (Ctrl+Y)" };
            AutomationProperties.SetName(_undoBtn, "Undo appearance change"); AutomationProperties.SetName(_redoBtn, "Redo appearance change");
            _undoBtn.Click += (s, e) => Undo(); _redoBtn.Click += (s, e) => Redo();
            var headButtons = new StackPanel { Orientation = Orientation.Horizontal };
            headButtons.Children.Add(_undoBtn); headButtons.Children.Add(_redoBtn); headButtons.Children.Add(close);
            Grid.SetColumn(headButtons, 1); head.Children.Add(headButtons);
            UpdateUndoButtons();
            head.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is not Button) try { DragMove(); } catch { } };
            root.Children.Add(head);

            var middle = new StackPanel();
            _lockBanner = new Border
            {
                Background = Ui.Res("B.DangerWash"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed,
                Child = new TextBlock { Text = "This look is locked. Changes are refused until you unlock it (Library tab).", Foreground = Ui.Res("B.Danger"), FontSize = 12, TextWrapping = TextWrapping.Wrap }
            };
            AutomationProperties.SetName(_lockBanner, "This look is locked");
            middle.Children.Add(_lockBanner);
            _preview.Child = _previewText;
            middle.Children.Add(_preview);
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
            AutomationProperties.SetName(_status, "Appearance editor status");
            middle.Children.Add(_status);
            var tabRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 10) };
            foreach (var (id, label) in new[] { ("font", "Font"), ("colour", "Colour"), ("shape", "Shape"), ("library", "Library") })
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
                page = id switch { "font" => BuildFont(), "colour" => BuildColour(), "library" => BuildLibrary(), _ => BuildShape() };
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

        private void Say(string text) => _status.Text = text;

        private bool _refreshing;

        /// <summary>Re-reads the style into every control. Controls ignore the change events this raises, so refreshing never edits the style.</summary>
        private void RefreshAll()
        {
            _refreshing = true;
            try { foreach (var r in _refreshers) r(); }
            finally { _refreshing = false; }
        }

        private void Live()
        {
            ElementStyles.Store(_key, _style.IsEmpty ? null : _style, save: false);
            ElementStyles.ApplyMatching(_key);
            UpdatePreview();
        }

        /// <summary>Saves the look and, when it differs from the last saved one, adds an undo step and a history line.</summary>
        private void Persist()
        {
            if (ElementStyles.IsLocked(_key)) return;
            ElementStyles.Store(_key, _style.IsEmpty ? null : _style, save: true);
            var now = ElementStyles.Snapshot(_style);
            if (now != _last)
            {
                _undo.Add(_last);
                if (_undo.Count > 100) _undo.RemoveAt(0);
                _redo.Clear();
                ElementStyles.RecordHistory(_key, ElementStyles.FromSnapshot(_last), _style);
                _last = now;
            }
            UpdateUndoButtons();
        }

        /// <summary>True, after showing why, when the look is frozen by a lock. The controls are put back to the saved look.</summary>
        private bool Locked()
        {
            if (!ElementStyles.IsLocked(_key)) return false;
            Say("This look is locked, so the change was refused. Open the Library tab and choose Unlock to change it.");
            _style.CopyFrom(ElementStyles.Get(_key));
            UpdatePreview();
            RefreshAll();
            return true;
        }

        private void Edit(Action change)
        {
            if (Locked()) return;
            change();
            Live();
            Persist();
            RefreshAll();
        }

        private void UpdateUndoButtons()
        {
            if (_undoBtn != null) _undoBtn.IsEnabled = _undo.Count > 0;
            if (_redoBtn != null) _redoBtn.IsEnabled = _redo.Count > 0;
        }

        private void Undo()
        {
            if (_undo.Count == 0 || Locked()) return;
            _redo.Add(ElementStyles.Snapshot(_style));
            var snap = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
            Jump(snap, "Undo");
        }

        private void Redo()
        {
            if (_redo.Count == 0 || Locked()) return;
            _undo.Add(ElementStyles.Snapshot(_style));
            var snap = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
            Jump(snap, "Redo");
        }

        private void Jump(string snapshot, string why)
        {
            var before = ElementStyles.FromSnapshot(_last);
            _style.CopyFrom(ElementStyles.FromSnapshot(snapshot));
            _last = snapshot;
            ElementStyles.Store(_key, _style.IsEmpty ? null : _style, save: true);
            ElementStyles.ApplyMatching(_key);
            ElementStyles.RecordHistory(_key, before, _style, why + ". " + ElementStyles.Describe(before, _style));
            UpdatePreview();
            RefreshAll();
            UpdateUndoButtons();
            Say(why + " done.");
        }

        private void UpdatePreview()
        {
            var eff = ElementStyles.Effective(_target);
            string F(string a, string b) => a ?? b;
            var look = ElementStyles.ResolveState(_style, _state);
            double size = _style.FontSize ?? Math.Min(eff.FontSize ?? 14, 32);
            _previewText.FontFamily = new FontFamily(F(_style.FontFamily, eff.FontFamily) ?? "Segoe UI");
            _previewText.FontSize = size;
            _previewText.FontWeight = FontWeight.FromOpenTypeWeight(_style.FontWeight ?? eff.FontWeight ?? 400);
            _previewText.FontStyle = (_style.Italic ?? eff.Italic ?? false) ? FontStyles.Italic : FontStyles.Normal;
            _previewText.Text = ElementStyles.Spaced("Sample text 123", _style.LetterSpacing ?? 0, size);
            _previewText.TextDecorations = (_style.Underline ?? false) ? TextDecorations.Underline : null;
            _previewText.Foreground = ElementStyles.TryColour(F(look.Foreground, eff.Foreground), out var fg) ? new SolidColorBrush(fg) : Ui.Res("B.Text");
            _preview.Background = ElementStyles.TryColour(F(look.Background, eff.Background), out var bg) ? new SolidColorBrush(bg) : Ui.Res("B.Sunken");
            _preview.CornerRadius = new CornerRadius(_style.CornerRadius ?? (eff.CornerRadius ?? 12));
            _preview.BorderThickness = new Thickness(_style.BorderThickness ?? 0);
            _preview.BorderBrush = ElementStyles.TryColour(look.BorderColour, out var bc) ? new SolidColorBrush(bc) : Ui.Res("B.LineStrong");
        }

        private void ResetElement()
        {
            if (Locked()) return;
            ElementStyles.Reset(_key, _target);
            _style.CopyFrom(null);
            // Reset removes the saved look directly, so record it like any other change and keep undo working.
            var now = ElementStyles.Snapshot(_style);
            if (now != _last)
            {
                _undo.Add(_last); _redo.Clear();
                ElementStyles.RecordHistory(_key, ElementStyles.FromSnapshot(_last), _style, "Reset element to the theme");
                _last = now;
            }
            UpdatePreview();
            RefreshAll();
            UpdateUndoButtons();
            Say("This element is back to the theme.");
        }

        private void ResetAll()
        {
            var ask = new ConfirmWindow("Reset every appearance edit?",
                $"This removes all {Math.Max(1, ElementStyles.Count)} saved element looks from every page and returns them to the theme. Looks frozen by an appearance lock stay as they are. The theme itself, your presets and your other settings stay as they are.",
                "Reset all", danger: true);
            ask.Confirmed += yes =>
            {
                if (!yes) return;
                int kept = ElementStyles.ResetAll();
                ElementStyles.RecordHistory("*", null, null, kept > 0 ? $"Reset all looks ({kept} locked kept)" : "Reset all looks");
                _style.CopyFrom(ElementStyles.Get(_key));
                _last = ElementStyles.Snapshot(_style);
                _undo.Clear(); _redo.Clear();
                UpdatePreview();
                RefreshAll();
                UpdateUndoButtons();
                Say(kept > 0 ? $"Everything was reset except {kept} locked look{(kept == 1 ? "" : "s")}." : "Every look is back to the theme.");
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
            it.Checked += (s, e) => { if (!_refreshing) Edit(() => _style.Italic = true); };
            it.Unchecked += (s, e) => { if (!_refreshing) Edit(() => _style.Italic = false); };
            _refreshers.Add(() => it.IsChecked = _style.Italic ?? ElementStyles.Effective(_target).Italic ?? false);
            sp.Children.Add(Section("Style", () => _style.Italic = null, it));

            // letter spacing
            sp.Children.Add(Section("Letter spacing", () => _style.LetterSpacing = null,
                Stepper("letter spacing", () => _style.LetterSpacing ?? 0, 0, 12, 0.5, v => _style.LetterSpacing = v <= 0 ? null : v),
                "Extra space between letters, 0 to 12 pixels, default none. It works by adding hair spaces between the letters, so it can only widen text, not tighten it. " +
                "Only plain text is spaced; text that comes from data or has mixed styles in it is left alone."));

            // underline
            var ul = new CheckBox { Style = Ui.Style("Switch"), Tag = Ui.Res("B.Mint"), IsChecked = _style.Underline ?? false, Content = new TextBlock { Text = "Underline", Foreground = Ui.Res("B.Text"), FontSize = 14 } };
            AutomationProperties.SetName(ul, "Underline");
            ul.Checked += (s, e) => { if (!_refreshing) Edit(() => _style.Underline = true); };
            ul.Unchecked += (s, e) => { if (!_refreshing) Edit(() => _style.Underline = false); };
            _refreshers.Add(() => ul.IsChecked = _style.Underline ?? false);
            sp.Children.Add(Section("Underline", () => _style.Underline = null, ul, "Draws a line under the element's text. Default off."));
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

        private static readonly (string Id, string Label)[] States =
        {
            ("normal", "Normal"), ("hover", "Hover"), ("pressed", "Pressed"), ("focused", "Focused"), ("disabled", "Disabled")
        };

        private FrameworkElement BuildColour()
        {
            var sp = new StackPanel();

            // which interaction state is being coloured
            var stateChips = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            var sb = new List<(Button B, string Id)>();
            var stateNote = new TextBlock { FontSize = 11, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            void PaintStates()
            {
                foreach (var (b, id) in sb) { bool on = id == _state; b.Background = on ? Ui.Res("B.SecondaryContainer") : Ui.Res("B.Raised"); b.Foreground = on ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft"); AutomationProperties.SetItemStatus(b, on ? "selected" : "not selected"); }
                stateNote.Text = _state == "normal"
                    ? "Normal is the everyday look. Pick Hover, Pressed, Focused or Disabled to colour that state instead; a state you leave empty keeps the normal look."
                    : $"Colours for the {_state} state. They apply while the element is {_state}, using the element's own mouse, keyboard and enabled events. Empty channels keep the normal look.";
            }
            foreach (var (id, label) in States)
            {
                var sid = id;
                var b = Ui.Button(label, () => { _state = sid; LoadPicker(); PaintStates(); UpdatePreview(); }, "Btn.Chip");
                b.ToolTip = sid == "normal" ? "The everyday look" : "The look while the element is " + sid;
                AutomationProperties.SetName(b, label + " state");
                sb.Add((b, id)); stateChips.Children.Add(b);
            }
            var stateHead = new StackPanel { Orientation = Orientation.Horizontal };
            stateHead.Children.Add(new TextBlock { Text = "State", Foreground = Ui.Res("B.Text"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            stateHead.Children.Add(InfoIcon.Create("states",
                "Give a button or row a different colour while the pointer is over it, while it is pressed, while it has keyboard focus, or while it is disabled. " +
                "The editor preview shows the state you pick. Default: no state colours, so the theme's own hover and press effects are all you see.", null, null));
            sp.Children.Add(stateHead);
            sp.Children.Add(stateChips);
            sp.Children.Add(stateNote);
            PaintStates();

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
            _picker.Changed += c => { if (_refreshing || Locked()) return; SetRole(c); Live(); };
            _picker.Committed += c => { if (_refreshing || Locked()) return; SetRole(c); Live(); Persist(); };
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
            if (_state != "normal")
            {
                var look = _style.StateOf(_state, true);
                if (_colourRole == "fg") look.Foreground = hex; else if (_colourRole == "bg") look.Background = hex; else look.BorderColour = hex;
                return;
            }
            if (_colourRole == "fg") _style.Foreground = hex; else if (_colourRole == "bg") _style.Background = hex; else _style.BorderColour = hex;
            // a visible border needs a thickness, so give it one the first time a colour is chosen
            if (_colourRole == "border" && (_style.BorderThickness ?? 0) <= 0) _style.BorderThickness = 1;
        }

        private void ClearRole()
        {
            if (_state != "normal")
            {
                var look = _style.StateOf(_state, false);
                if (look == null) return;
                if (_colourRole == "fg") look.Foreground = null; else if (_colourRole == "bg") look.Background = null; else look.BorderColour = null;
                _style.Tidy();
                return;
            }
            if (_colourRole == "fg") _style.Foreground = null; else if (_colourRole == "bg") _style.Background = null; else _style.BorderColour = null;
        }

        private void LoadPicker()
        {
            if (_picker == null) return;
            var eff = ElementStyles.Effective(_target);
            var look = ElementStyles.ResolveState(_style, _state);
            string hex = _colourRole switch { "fg" => look.Foreground ?? eff.Foreground, "bg" => look.Background ?? eff.Background, _ => look.BorderColour ?? eff.BorderColour };
            _picker.SetColor(ElementStyles.TryColour(hex, out var c) ? c : (_colourRole == "fg" ? Colors.Black : Colors.Transparent), false);
            string other = _colourRole == "fg" ? (look.Background ?? eff.Background) : (look.Foreground ?? eff.Foreground);
            Color? against = ElementStyles.TryColour(other, out var o) ? Color.FromRgb(o.R, o.G, o.B) : ((SolidColorBrush)Ui.Res("B.Surface")).Color;
            _picker.Against = against;
        }

        // ------------------------------------------------------------ library tab

        private static string LabelOf(string key)
        {
            var cut = key.IndexOf('|');
            var text = cut >= 0 ? key[(cut + 1)..] : key;
            return text.Length == 0 ? "(unnamed element)" : (text.Length > 36 ? text[..36] + "…" : text);
        }

        private Button Mini(string label, Action act, string name, bool danger = false)
        {
            var b = Ui.Button(label, act, danger ? "Btn.Danger" : "Btn.Chip");
            b.Height = 28; b.Margin = new Thickness(6, 0, 0, 0);
            AutomationProperties.SetName(b, name);
            return b;
        }

        private FrameworkElement BuildLibrary()
        {
            var sp = new StackPanel();
            _libAnchor = sp;

            // copy and paste
            _clipNote = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            void ShowClip() => _clipNote.Text = ElementStyles.Clipboard == null ? "Nothing copied yet." : $"Copied from \"{ElementStyles.ClipboardFrom}\". Paste replaces this element's whole look.";
            var cp = new WrapPanel();
            cp.Children.Add(Ui.Button("Copy style", () =>
            {
                if (_style.IsEmpty) { Say("This element has no custom look to copy. Change something first."); return; }
                ElementStyles.Clipboard = _style.Clone(); ElementStyles.ClipboardFrom = LabelOf(_key);
                ShowClip(); Say("Look copied. Open another element's editor and choose Paste style.");
            }, "Btn.Ghost", Icons.Clipboard));
            cp.Children.Add(Ui.Button("Paste style", () =>
            {
                if (ElementStyles.Clipboard == null) { Say("Nothing has been copied yet."); return; }
                var src = ElementStyles.Clipboard.Clone();
                if (ElementStyles.IsLocked(_key)) { Locked(); return; }
                Edit(() => _style.CopyFrom(src));
                Say("Look pasted.");
            }, "Btn.Ghost", Icons.Check));
            var cpBox = new StackPanel(); cpBox.Children.Add(cp); cpBox.Children.Add(_clipNote);
            ShowClip();
            sp.Children.Add(Section("Copy and paste", null, cpBox, "Copy this element's whole look, open another element's editor, and paste it there. The copy lives only until the app closes. Default: nothing copied."));

            // search over presets and history
            _libSearch = new SearchField("appearance:library", "Search presets and history", compact: true) { Margin = new Thickness(0, 0, 0, 10) };
            _libSearch.Filter += FillLibrary;
            sp.Children.Add(_libSearch);

            // presets
            var save = new Grid();
            save.ColumnDefinitions.Add(new ColumnDefinition()); save.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nameBox = new TextBox { Style = Ui.Style("Input"), MaxLength = 40, ToolTip = "A name for this look, up to 40 characters" };
            AutomationProperties.SetName(nameBox, "Preset name");
            var saveBtn = Ui.Button("Save preset", () =>
            {
                var err = ElementStyles.SavePreset(nameBox.Text, _style);
                if (err != null) { Say(err); return; }
                Say($"Preset \"{nameBox.Text.Trim()}\" saved."); nameBox.Text = ""; FillLibrary();
            }, "Btn.Ghost");
            saveBtn.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(saveBtn, 1); save.Children.Add(nameBox); save.Children.Add(saveBtn);
            _presetList = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            var presets = new StackPanel(); presets.Children.Add(save); presets.Children.Add(_presetList);
            sp.Children.Add(Section("Presets", null, presets,
                "A preset is a named copy of this element's look. Save one here, then open any other element's editor and choose Apply to give it the same look. At most 50 presets. Default: none."));

            // history
            _historyList = new StackPanel();
            var hist = new StackPanel();
            hist.Children.Add(_historyList);
            var clear = Ui.Button("Clear history", () =>
            {
                var ask = new ConfirmWindow("Clear the appearance history?", "This removes the list of past changes. The looks themselves stay as they are.", "Clear history", danger: true);
                ask.Confirmed += yes => { if (yes) { ElementStyles.ClearHistory(); FillLibrary(); Say("History cleared."); } };
                ask.Show();
            }, "Btn.Ghost", Icons.Trash);
            hist.Children.Add(clear);
            sp.Children.Add(Section("History", null, hist, "The last 100 changes with the time of each. Restore puts that look back on its element. Default: empty."));
            FillLibrary();

            // import and export
            var io = new WrapPanel();
            io.Children.Add(Ui.Button("Export all looks…", ExportLooks, "Btn.Ghost", Icons.File));
            io.Children.Add(Ui.Button("Import looks…", ImportLooks, "Btn.Ghost", Icons.Folder));
            sp.Children.Add(Section("Import and export", null, io,
                "Exports every saved look and preset as one JSON file. Import checks the whole file first (size, format, ranges, colours, locks) and changes nothing if anything is wrong; a good file is merged over your current looks."));

            // lock
            var lockBox = new StackPanel();
            var lockText = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
            var lockButtons = new WrapPanel();
            lockBox.Children.Add(lockText); lockBox.Children.Add(lockButtons);
            lockBox.Children.Add(new TextBlock { Text = Locks.Disclosure, FontSize = 11, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            _lockRefresh = () =>
            {
                lockButtons.Children.Clear();
                var entry = Locks.FindAppearance(_key);
                if (entry == null)
                {
                    lockText.Text = "This look is not locked. Locking freezes how it looks now; the element still works as usual.";
                    lockButtons.Children.Add(Ui.Button("Lock this look…", () => new LockWizardWindow(_libAnchor ?? _target, Locks.AppearancePage, _key, "Look of " + LabelOf(_key), null).Show(), "Btn.Ghost", Icons.Shield));
                }
                else if (Locks.IsOpen(entry))
                {
                    lockText.Text = "This look has a lock, and it is open right now, so it can be changed.";
                    lockButtons.Children.Add(Ui.Button("Close the lock again", () => Locks.Relock(entry.Id), "Btn.Ghost", Icons.Shield));
                    lockButtons.Children.Add(Ui.Button("Remove the lock", () => { var err = Locks.Remove(entry.Id); if (err != null) Say(err); else Say("Lock removed."); }, "Btn.Ghost", Icons.Trash));
                }
                else
                {
                    lockText.Text = "This look is locked. Nothing about it can change until it is unlocked.";
                    lockButtons.Children.Add(Ui.Button("Unlock…", () => ElementMenu.PromptEntry(entry, _libAnchor ?? _target), "Btn.Accent", Icons.Shield));
                }
            };
            _lockRefresh();
            sp.Children.Add(Section("Lock this look", null, lockBox,
                "An appearance lock freezes this element's saved look: its font, colours, shape and states cannot be changed, reset, pasted over, restored or overwritten by an import until the lock is opened with its PIN, password or code. Default: unlocked."));
            return sp;
        }

        private void OnLocksChanged() => Dispatcher.BeginInvoke(new Action(RefreshLockUi));

        private void RefreshLockUi()
        {
            if (_lockBanner != null) _lockBanner.Visibility = ElementStyles.IsLocked(_key) ? Visibility.Visible : Visibility.Collapsed;
            _lockRefresh?.Invoke();
        }

        private void FillLibrary()
        {
            if (_presetList == null || _historyList == null) return;
            var names = ElementStyles.PresetNames.ToList();
            var shownNames = names.Where(n => _libSearch == null || _libSearch.State.Matches(n)).ToList();
            _presetList.Children.Clear();
            foreach (var n in shownNames)
            {
                var name = n;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock { Text = name, Foreground = Ui.Res("B.Text"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                var btns = new StackPanel { Orientation = Orientation.Horizontal };
                btns.Children.Add(Mini("Apply", () =>
                {
                    var p = ElementStyles.Preset(name);
                    if (p == null) return;
                    if (ElementStyles.IsLocked(_key)) { Locked(); return; }
                    Edit(() => _style.CopyFrom(p));
                    Say($"Preset \"{name}\" applied to this element.");
                }, "Apply preset " + name));
                btns.Children.Add(Mini("Delete", () =>
                {
                    var ask = new ConfirmWindow("Delete this preset?", $"The preset \"{name}\" is removed. Elements that already use its look keep it.", "Delete preset", danger: true);
                    ask.Confirmed += yes => { if (yes && ElementStyles.DeletePreset(name)) { FillLibrary(); Say($"Preset \"{name}\" deleted."); } };
                    ask.Show();
                }, "Delete preset " + name, danger: true));
                Grid.SetColumn(btns, 1); row.Children.Add(btns);
                _presetList.Children.Add(row);
            }
            if (shownNames.Count == 0)
                _presetList.Children.Add(new TextBlock { Text = names.Count == 0 ? "No presets yet. Save the current look above." : "No preset matches the search.", FontSize = 12, Foreground = Ui.Res("B.TextMute") });

            var all = ElementStyles.History.Reverse().ToList();
            var shown = all.Where(h => _libSearch == null || _libSearch.State.Matches(h.Summary + " " + LabelOf(h.Key))).Take(30).ToList();
            _libSearch?.SetCount(shownNames.Count + shown.Count, names.Count + all.Count, "presets and changes");
            _historyList.Children.Clear();
            foreach (var h in shown)
            {
                var entry = h;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var tx = new StackPanel();
                tx.Children.Add(new TextBlock { Text = LabelOf(entry.Key) + " · " + entry.Summary, Foreground = Ui.Res("B.Text"), FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
                tx.Children.Add(new TextBlock { Text = entry.At.ToString("g", CultureInfo.CurrentCulture), Foreground = Ui.Res("B.TextMute"), FontSize = 11 });
                row.Children.Add(tx);
                bool can = !string.IsNullOrEmpty(entry.Key) && entry.Key != "*";
                var rb = Mini("Restore", () => RestoreEntry(entry), "Restore the look from " + entry.At.ToString("g", CultureInfo.CurrentCulture));
                rb.IsEnabled = can;
                Grid.SetColumn(rb, 1); row.Children.Add(rb);
                _historyList.Children.Add(row);
            }
            if (shown.Count == 0)
                _historyList.Children.Add(new TextBlock { Text = all.Count == 0 ? "No changes recorded yet." : "No change matches the search.", FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 6) });
        }

        private void RestoreEntry(StyleHistoryEntry entry)
        {
            if (entry.Key == _key)
            {
                if (ElementStyles.IsLocked(_key)) { Locked(); return; }
                var after = entry.After?.Clone();
                Edit(() => _style.CopyFrom(after));
                Say("Restored the look from " + entry.At.ToString("g", CultureInfo.CurrentCulture) + ".");
            }
            else
            {
                var err = ElementStyles.Restore(entry);
                Say(err ?? "Restored the look of \"" + LabelOf(entry.Key) + "\".");
            }
            FillLibrary();
        }

        private void ExportLooks()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Export element looks", Filter = "Element looks (*.json)|*.json", FileName = "element-looks.json", DefaultExt = ".json" };
                if (dlg.ShowDialog(this) != true) return;
                System.IO.File.WriteAllText(dlg.FileName, ElementStyles.ExportJson());
                Say($"Exported {ElementStyles.Count} looks and {ElementStyles.PresetNames.Count} presets.");
            }
            catch (Exception ex) { Say("Export failed: " + ex.Message); }
        }

        private void ImportLooks()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Import element looks", Filter = "Element looks (*.json)|*.json|All files (*.*)|*.*", CheckFileExists = true };
                if (dlg.ShowDialog(this) != true) return;
                var fi = new System.IO.FileInfo(dlg.FileName);
                if (fi.Length > ElementStyles.MaxImportBytes) { Say("That file is larger than " + (ElementStyles.MaxImportBytes / 1000) + " KB, so it was not read."); return; }
                var err = ElementStyles.ImportJson(System.IO.File.ReadAllText(dlg.FileName), out var styles, out var presets);
                if (err != null) { Say("Nothing was imported. " + err); return; }
                _style.CopyFrom(ElementStyles.Get(_key));
                _last = ElementStyles.Snapshot(_style);
                _undo.Clear(); _redo.Clear();
                UpdatePreview(); RefreshAll(); UpdateUndoButtons(); FillLibrary();
                Say($"Imported {styles} looks and {presets} presets.");
            }
            catch (Exception ex) { Say("Import failed: " + ex.Message); }
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
