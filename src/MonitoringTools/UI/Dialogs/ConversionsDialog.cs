using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Quick conversions: type or paste an amount and see it in every unit of its kind at once, each one a tile that copies
    /// on click. The kind chips switch to another family for the same number. Money uses today's rates when automatic
    /// lookups are on. Enter copies the first result; Ctrl+Shift+C copies them all as a list.
    /// </summary>
    internal sealed class ConversionsDialog : ToolDialogWindow
    {
        internal static ConversionsDialog Current { get; private set; }

        private readonly TextBox _input = new TextBox { Style = Ui.Style("Input"), Height = 56, FontSize = 22 };
        private readonly TextBlock _parsed;
        private readonly WrapPanel _families = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        private readonly UniformGrid3 _grid = new UniformGrid3();
        private readonly ContentControl _banner = new ContentControl { Focusable = false };
        private readonly Button _primary, _copyAll;
        private string _family;            // a family picked with the chips, or null to follow the input
        private List<SmartValue> _shown = new List<SmartValue>();
        private int _lookupToken;

        internal IReadOnlyList<SmartValue> Shown => _shown;
        internal TextBox Input => _input;

        public static ConversionsDialog Open(string text)
        {
            if (Current != null && Current.IsLoaded) { Current.SetText(text); Current.Activate(); return Current; }
            var d = new ConversionsDialog();
            Current = d;
            d.Closed += (s, e) => { if (Current == d) Current = null; };
            d.SetText(text);
            d.Show();
            d._input.Focus(); d._input.SelectAll();
            return d;
        }

        private ConversionsDialog() : base("Quick conversions")
        {
            _parsed = Ui.Mono("", Ui.Res("B.TextSoft"), 12);
            _parsed.VerticalAlignment = VerticalAlignment.Center; _parsed.Margin = new Thickness(12, 0, 0, 0);
            _input.FontFamily = Ui.Font("F.Mono") ?? _input.FontFamily;
            AutomationProperties.SetName(_input, "Amount and unit");
            _input.TextChanged += (s, e) => { _family = null; Refresh(); };

            var inputRow = new Grid();
            inputRow.ColumnDefinitions.Add(new ColumnDefinition()); inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(_parsed, 1); inputRow.Children.Add(_input); inputRow.Children.Add(_parsed);

            foreach (var f in Units.Families.Select(x => (Id: x.Id, Name: x.Name)).Append((Id: "money", Name: "Money")))
            {
                var id = f.Id;
                var chip = Ui.Button(f.Name, () => { _family = id; Refresh(); }, "Btn.Chip");
                chip.Tag = id; chip.Margin = new Thickness(0, 0, 6, 6);
                AutomationProperties.SetName(chip, "Show as " + f.Name);
                _families.Children.Add(chip);
            }

            var scroll = new ScrollViewer { Content = _grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 420, Margin = new Thickness(0, 10, 0, 0), Focusable = false };

            _copyAll = Ui.Button("Copy all as a list", CopyAll, "Btn.Ghost", Icons.Clipboard);
            _copyAll.ToolTip = "Every result, one per line (Ctrl+Shift+C)";
            _primary = Ui.Button("Copy", () => OnPrimary(), "Btn.Accent", Icons.Clipboard);
            var bar = SmartDialogParts.ActionBar(null, "Click any result to copy it.", new[] { _copyAll }, _primary, "Enter");

            var body = new StackPanel();
            body.Children.Add(inputRow);
            body.Children.Add(_families);
            body.Children.Add(scroll);
            body.Children.Add(_banner);
            body.Children.Add(bar);
            ShowView("form", body, 860, "Quick conversions", "Type or paste anything with a number and a unit.", Icons.Lines,
                Ui.Res("B.Amber"), Ui.Res("B.AmberWash"));

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; OnPrimary(); }
                else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { e.Handled = true; CopyAll(); }
            };
        }

        public void SetText(string text) { _input.Text = (text ?? "").Trim(); _input.CaretIndex = _input.Text.Length; }

        protected override void OnPrimary()
        {
            var first = _shown.FirstOrDefault();
            if (first != null) Copy(first);
        }

        private void CopyAll()
        {
            if (_shown.Count == 0) return;
            ClipboardWatcher.SetText(string.Join(Environment.NewLine, _shown.Select(v => v.Text)));
            Say("Copied " + _shown.Count + " conversions as a list.");
        }

        private void Copy(SmartValue v)
        {
            ClipboardWatcher.SetText(v.Text);
            Say("Copied " + v.Text + ". Paste it anywhere.");
        }

        private void Say(string text)
        {
            var b = DialogParts.Banner(Icons.Check, text, "mint");
            b.Margin = new Thickness(0, 10, 0, 0);
            _banner.Content = b;
            Ui.Rise(b, 0, 6, 260);
        }

        internal void Refresh()
        {
            _banner.Content = null;
            var raw = _input.Text.Trim();
            var q = Units.Parse(raw);
            var money = q == null ? Everyday2.Money(raw, Settings.Current.HomeCurrency) : null;
            string fam = _family ?? q?.Family.Id ?? (money != null ? "money" : null);
            foreach (Button c in _families.Children)
                c.Style = Ui.Style((string)c.Tag == fam ? "Btn.Accent" : "Btn.Chip");

            _shown = new List<SmartValue>();
            string sourceText = null;
            if (fam == "money")
            {
                money ??= NumberOnly(raw) is double n ? Everyday2.Money(Units.Format(n) + " " + Settings.Current.HomeCurrency, Settings.Current.HomeCurrency) : null;
                if (money != null)
                {
                    _parsed.Text = money.Summary;
                    _shown.AddRange(money.Values.Skip(1));
                    LookupRates(money);
                }
                else _parsed.Text = "type an amount, like $40 or 12 EUR";
            }
            else if (fam != null)
            {
                var family = Units.Family(fam);
                if (q == null || q.Family.Id != fam)
                {
                    var n = q?.Number ?? NumberOnly(raw);
                    q = n is double num ? new Quantity { Number = num, Family = family, Unit = family.Units[0], Source = raw } : null;
                }
                if (q != null)
                {
                    _parsed.Text = (q.Unit.Name.Length > 0 ? q.Unit.Name : family.Name.ToLowerInvariant()) + " · " + family.Name.ToLowerInvariant();
                    foreach (var c in Units.ConvertAll(q)) { _shown.Add(new SmartValue(c.Unit.Name, c.Text)); if (c.IsSource) sourceText = c.Text; }
                }
                else _parsed.Text = "type a number";
            }
            else _parsed.Text = raw.Length == 0 ? "type a number and a unit" : "add a unit, like 5 kg or 20 cm";

            _grid.Children.Clear();
            int i = 0;
            foreach (var v in _shown)
            {
                bool yours = v.Text == sourceText;
                var tile = Tile(v, yours);
                _grid.Children.Add(tile);
                Ui.Rise(tile, Math.Min(i++, 12) * 25, 6, 280);
            }
            var others = _shown.Where(v => v.Text != sourceText).ToList();
            if (others.Count > 0) { _shown = others.Concat(_shown.Where(v => v.Text == sourceText)).ToList(); }
            _primary.IsEnabled = _copyAll.IsEnabled = _shown.Count > 0;
            _primary.Content = _shown.Count > 0 ? "Copy " + Short(_shown[0].Text) : "Copy";
        }

        private static string Short(string s) => s.Length > 22 ? s.Substring(0, 21) + "…" : s;

        private static double? NumberOnly(string raw)
        {
            var m = System.Text.RegularExpressions.Regex.Match(raw, @"-?\d[\d,]*(?:\.\d+)?");
            return m.Success && double.TryParse(m.Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (double?)null;
        }

        private Button Tile(SmartValue v, bool yours)
        {
            var value = Ui.Mono(v.Text, Ui.Res("B.Text"), 15);
            value.FontWeight = FontWeights.Medium; value.TextTrimming = TextTrimming.CharacterEllipsis;
            Core.Language.SetLiteral(value, true);
            var label = new TextBlock { Text = v.Label + (yours ? " · what you typed" : ""), FontSize = 11.5, Foreground = Ui.Res("B.TextSoft") };
            var stack = new StackPanel(); stack.Children.Add(value); stack.Children.Add(label);
            var b = new Button
            {
                Content = stack, Style = Ui.Style("Btn.Row"), Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(14, 8, 14, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left, Height = 58, ToolTip = "Copy " + v.Text,
            };
            AutomationProperties.SetName(b, "Copy " + v.Text + ", " + v.Label);
            b.Click += (s, e) => Copy(v);
            return b;
        }

        private void LookupRates(SmartFact money)
        {
            var url = SmartLookups.UrlFor(money);
            if (url == null) { Say(Settings.Current.AutoLookups ? "Rates for this currency are not available." : "Turn on automatic lookups to convert money with today's rates."); return; }
            int token = ++_lookupToken;
            _ = Task.Run(async () =>
            {
                string body = null;
                try { body = await new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(4) }.GetStringAsync(url); } catch { }
                var adds = body == null ? new List<SmartValue>() : SmartLookups.Read(money, body, out _);
                await Dispatcher.InvokeAsync(() =>
                {
                    if (token != _lookupToken) return;
                    if (adds.Count == 0) { Say("Today's rates could not be reached. Try again in a moment."); return; }
                    foreach (var v in adds) { _shown.Add(v); var t = Tile(v, false); _grid.Children.Add(t); Ui.Rise(t, 0, 6, 260); }
                    _primary.IsEnabled = _copyAll.IsEnabled = true;
                    _primary.Content = "Copy " + Short(_shown[0].Text);
                });
            });
        }

        /// <summary>A three-column grid whose rows size to their tiles.</summary>
        private sealed class UniformGrid3 : System.Windows.Controls.Primitives.UniformGrid
        {
            public UniformGrid3() { Columns = 3; }
        }
    }
}
