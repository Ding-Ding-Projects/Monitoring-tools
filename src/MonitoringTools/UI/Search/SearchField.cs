using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MonitoringTools.UI.Search
{
    /// <summary>
    /// A search bar with plain-text search by default, a case toggle, a regex toggle and a
    /// Builder button that opens a regex builder anchored to this field. Every instance is bound
    /// to its own <see cref="SearchState"/>, so each list or page keeps its own search.
    /// </summary>
    public sealed class SearchField : StackPanel
    {
        private static readonly List<WeakReference<SearchField>> Live = new List<WeakReference<SearchField>>();

        /// <summary>True while any search field is being typed in or has its builder open, so pages can hold off rebuilding.</summary>
        public static bool AnyActive
        {
            get
            {
                Live.RemoveAll(w => !w.TryGetTarget(out _));
                foreach (var w in Live)
                    if (w.TryGetTarget(out var f) && f.IsLoaded && (f.Box.IsKeyboardFocusWithin || (f._builder?.IsOpen ?? false))) return true;
                return false;
            }
        }

        public SearchState State { get; }
        public TextBox Box { get; }
        public Border Bar { get; }
        public string Label { get; }

        /// <summary>Supplies the texts this field searches, used as the builder's default test sample.</summary>
        public Func<IEnumerable<string>> SampleSource { get; set; }

        /// <summary>When set, the empty result offers to run the same search across every page.</summary>
        public Action<SearchState> SearchEverywhere { get; set; }

        /// <summary>Raised whenever the matching rules change; the owner re-filters its items.</summary>
        public event Action Filter;

        private readonly TextBlock _hint, _count, _empty;
        private readonly Button _everywhere;
        private readonly StackPanel _emptyRow;
        private readonly ToggleButton _case, _regex, _build;
        private RegexBuilderPopup _builder;
        private bool _syncing;

        public SearchField(string id, string label, bool compact = false)
        {
            State = SearchState.For(id);
            Label = label;
            Live.Add(new WeakReference<SearchField>(this));
            double h = compact ? 44 : 56;

            Box = new TextBox
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Text"),
                SelectionBrush = Ui.Res("B.Mint"), FontSize = compact ? 13 : 14, VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0), Margin = new Thickness(0), Text = State.Query, FocusVisualStyle = null,
                FontFamily = State.IsRegex ? Ui.Font("F.Mono") : Ui.Font("F.Body")
            };
            AutomationProperties.SetName(Box, label);
            AutomationProperties.SetHelpText(Box, "Plain text by default. Alt+R switches to regular expressions, Alt+C matches case, Alt+B opens the regex builder.");
            _hint = new TextBlock { Text = label, Foreground = Ui.Res("B.TextMute"), FontSize = Box.FontSize, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, FontFamily = Ui.Font("F.Body") };
            var boxHost = new Grid { VerticalAlignment = VerticalAlignment.Center };
            boxHost.Children.Add(_hint); boxHost.Children.Add(Box);

            _count = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) };
            AutomationProperties.SetLiveSetting(_count, AutomationLiveSetting.Polite);
            _case = Toggle("Aa", "Match case (Alt+C)", compact);
            _regex = Toggle(".*", "Regular expression mode (Alt+R)", compact);
            _build = Toggle(compact ? null : "Builder", "Open the regex builder for this search (Alt+B)", compact, Icons.Wand);

            var g = new Grid();
            foreach (var w in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto })
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            var icon = new ContentControl { Content = Ui.Icon(Icons.Search, Ui.Res("B.TextMute"), compact ? 14 : 16), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Focusable = false };
            g.Children.Add(icon);
            Grid.SetColumn(boxHost, 1); g.Children.Add(boxHost);
            Grid.SetColumn(_count, 2); g.Children.Add(_count);
            Grid.SetColumn(_case, 3); g.Children.Add(_case);
            Grid.SetColumn(_regex, 4); g.Children.Add(_regex);
            Grid.SetColumn(_build, 5); g.Children.Add(_build);

            Bar = new Border
            {
                Background = Ui.Res("B.Raised"), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(h / 2), Padding = new Thickness(compact ? 12 : 18, 0, compact ? 4 : 8, 0), Height = h, Child = g
            };
            _empty = new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _everywhere = Ui.Button("Search every page", () => SearchEverywhere?.Invoke(State), "Btn.Ghost", Icons.Search);
            _everywhere.Margin = new Thickness(12, 0, 0, 0);
            _emptyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 10, 0, 0), Visibility = Visibility.Collapsed };
            _emptyRow.Children.Add(_empty); _emptyRow.Children.Add(_everywhere);
            Children.Add(Bar);
            Children.Add(_emptyRow);
            Margin = new Thickness(0, 0, 0, compact ? 8 : 16);

            Box.TextChanged += (s, e) => { if (_syncing) return; State.SetQuery(Box.Text, this); };
            Box.GotKeyboardFocus += (s, e) => Paint();
            Box.LostKeyboardFocus += (s, e) => { State.Remember(); Paint(); };
            Box.PreviewKeyDown += OnKey;
            _case.Click += (s, e) => State.SetMatchCase(_case.IsChecked == true, this);
            _regex.Click += (s, e) => State.SetRegex(_regex.IsChecked == true, this);
            _build.Click += (s, e) => { if (_build.IsChecked == true) OpenBuilder(); else _builder?.Close(); };
            State.Changed += OnState;
            Unloaded += (s, e) => State.Changed -= OnState;
            Loaded += (s, e) => { State.Changed -= OnState; State.Changed += OnState; Paint(); };
            Paint();
        }

        private void OnState(object source)
        {
            if (source != this && Box.Text != State.Query)
            {
                _syncing = true;
                var caret = Box.CaretIndex;
                Box.Text = State.Query;
                Box.CaretIndex = Math.Min(caret, Box.Text.Length);
                _syncing = false;
            }
            Paint();
            Filter?.Invoke();
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape && Box.Text.Length > 0) { Box.Clear(); e.Handled = true; }
            else if (mods == ModifierKeys.Alt && key == Key.R) { State.SetRegex(!State.IsRegex, this); e.Handled = true; }
            else if (mods == ModifierKeys.Alt && key == Key.C) { State.SetMatchCase(!State.MatchCase, this); e.Handled = true; }
            else if (mods == ModifierKeys.Alt && key == Key.B) { OpenBuilder(); e.Handled = true; }
        }

        public void OpenBuilder(string tab = null)
        {
            if (_builder == null)
            {
                _builder = new RegexBuilderPopup(this);
                _builder.Closed += () => { _build.IsChecked = false; Paint(); Box.Focus(); };
            }
            if (!State.IsRegex) State.SetRegex(true, this);
            _build.IsChecked = true;
            _builder.Open(tab);
            Paint();
        }

        public bool BuilderOpen => _builder?.IsOpen ?? false;
        public FrameworkElement BuilderCard => _builder?.Card;

        /// <summary>Shows the result count and an honest message when nothing matches.</summary>
        public void SetCount(int shown, int total, string noun = "items")
        {
            if (!State.IsActive) { _count.Text = total > 0 ? $"{total}" : ""; _emptyRow.Visibility = Visibility.Collapsed; }
            else if (State.Error != null) { _count.Text = "invalid"; _empty.Text = "The pattern is not valid, so nothing is filtered: " + State.Error; _emptyRow.Visibility = Visibility.Visible; _everywhere.Visibility = Visibility.Collapsed; }
            else
            {
                _count.Text = $"{shown} of {total}";
                _empty.Text = State.TimedOut
                    ? "Some items took longer than 200 ms to check and were left out. The builder's warning explains why."
                    : $"No {noun} match “{State.Query}”.";
                _emptyRow.Visibility = shown == 0 || State.TimedOut ? Visibility.Visible : Visibility.Collapsed;
                _everywhere.Visibility = SearchEverywhere != null && shown == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            AutomationProperties.SetName(_count, _count.Text.Length == 0 ? "No items" : _count.Text + " " + noun);
            _count.Foreground = State.Error != null || (State.IsActive && shown == 0) ? Ui.Res("B.Amber") : Ui.Res("B.TextMute");
        }

        private void Paint()
        {
            _hint.Visibility = Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _hint.Text = State.IsRegex ? Label + " with a pattern" : Label;
            Box.FontFamily = State.IsRegex ? Ui.Font("F.Mono") : Ui.Font("F.Body");
            _case.IsChecked = State.MatchCase;
            _regex.IsChecked = State.IsRegex;
            _build.IsChecked = BuilderOpen;
            Bar.BorderBrush = State.Error != null ? Ui.Res("B.Danger") : Box.IsKeyboardFocusWithin || BuilderOpen ? Ui.Res("B.Mint") : Brushes.Transparent;
            Bar.CornerRadius = BuilderOpen ? new CornerRadius(Bar.Height / 2, Bar.Height / 2, 16, 16) : new CornerRadius(Bar.Height / 2);
        }

        private static ToggleButton Toggle(string text, string name, bool compact, string icon = null)
        {
            object content = text;
            if (icon != null)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(Ui.Icon(icon, Ui.Res("B.TextSoft"), 14));
                if (text != null) sp.Children.Add(new TextBlock { Text = text, Margin = new Thickness(6, 0, 0, 0), FontFamily = Ui.Font("F.Body"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                content = sp;
            }
            var t = new ToggleButton
            {
                Content = content, MinWidth = compact ? 32 : 40, Height = compact ? 32 : 40, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(text != null && icon != null ? 10 : 0, 0, text != null && icon != null ? 12 : 0, 0),
                Cursor = Cursors.Hand, FocusVisualStyle = null, FontFamily = Ui.Font("F.Mono"), FontSize = compact ? 11 : 12, Foreground = Ui.Res("B.TextSoft"), ToolTip = name
            };
            AutomationProperties.SetName(t, name);
            var tpl = new ControlTemplate(typeof(ToggleButton));
            var bd = new FrameworkElementFactory(typeof(Border), "Bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(compact ? 14 : 20));
            bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            bd.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Res("B.SecondaryContainer"), "Bd"));
            on.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(12), "Bd"));
            on.Setters.Add(new Setter(Control.ForegroundProperty, Ui.Res("B.OnSecondaryContainer")));
            tpl.Triggers.Add(on);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Res("B.Mint"), "Bd"));
            tpl.Triggers.Add(focus);
            t.Template = tpl;
            return t;
        }
    }
}
