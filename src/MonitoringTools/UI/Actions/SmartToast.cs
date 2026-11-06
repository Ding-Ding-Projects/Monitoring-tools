using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;

namespace MonitoringTools.UI
{
    /// <summary>The body of a toast for recognised text: the summary, then every value as a tile you tap to copy.
    /// Values that arrive later (automatic lookups) are added with Add.</summary>
    public sealed class SmartToast
    {
        private const int Shown = 8;
        private readonly WrapPanel _tiles = new WrapPanel();
        private readonly TextBlock _summary;
        private readonly StackPanel _root = new StackPanel();
        private readonly ToastWindow _w;
        private Button _more;
        private int _count;

        public SmartFact Fact { get; }

        public SmartToast(ToastWindow w, SmartFact fact)
        {
            _w = w; Fact = fact;
            _summary = Ui.Mono(fact.Summary ?? "", Ui.Res("B.TextSoft"), 12);
            _summary.TextWrapping = TextWrapping.Wrap; _summary.Margin = new Thickness(0, 0, 0, 8);
            Core.Language.SetLiteral(_summary, true);
            _root.Children.Add(_summary);
            if (fact.Values.Count > 0)
            {
                _root.Children.Add(new TextBlock { Text = "Tap one to copy it", FontSize = 11, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 0, 0, 4) });
                _root.Children.Add(_tiles);
                foreach (var v in fact.Values) AddTile(v);
            }
        }

        public UIElement Build() => _root;

        public static string Icon(SmartFact f) => f.Kind switch
        {
            "unit" or "maths" or "money" or "table" => Icons.Lines,
            "phone" => Icons.Monitor,
            "timestamp" or "zonetime" or "cron" => Icons.Clock,
            "trace" or "hresult" => Icons.Bolt,
            "jwt" or "secret" or "iban" => Icons.Shield,
            "ip" => Icons.Globe,
            "git" => Icons.Git,
            "parcel" => Icons.Box,
            "language" => Icons.Translate,
            "address" or "flight" => Icons.Globe,
            _ => f.FromExtension != null ? Icons.Wand : Icons.Code,
        };

        /// <summary>Adds a value that arrived after the toast opened, and refreshes the summary.</summary>
        public void Add(SmartValue v, string summary = null)
        {
            if (summary != null) _summary.Text = summary;
            if (!_root.Children.Contains(_tiles)) _root.Children.Add(_tiles);
            AddTile(v);
            Ui.Rise((FrameworkElement)_tiles.Children[_tiles.Children.Count - 1], 0, 6, 260);
        }

        private void AddTile(SmartValue v)
        {
            _count++;
            var label = new TextBlock { Text = v.Label, FontSize = 10.5, Foreground = Ui.Res("B.TextSoft") };
            var text = v.Text.Length > 60 ? v.Text.Substring(0, 57) + "…" : v.Text;
            var value = Ui.Mono(text.Replace("\r", "").Replace("\n", " ⏎ "), Ui.Res("B.Text"), 12.5);
            Core.Language.SetLiteral(value, true);
            var stack = new StackPanel(); stack.Children.Add(value); stack.Children.Add(label);
            var tile = new Button
            {
                Content = stack, Style = Ui.Style("Btn.Ghost"), Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 6, 10, 6),
                HorizontalContentAlignment = HorizontalAlignment.Left, BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1),
                ToolTip = "Copy " + (v.Text.Length > 200 ? v.Text.Substring(0, 200) + "…" : v.Text),
            };
            AutomationProperties.SetName(tile, "Copy " + v.Label + ": " + text);
            tile.Click += (s, e) =>
            {
                ClipboardWatcher.SetText(v.Text);
                label.Text = "✓ Copied";
                label.Foreground = Ui.Res("B.Mint");
                _w.KeepOpenBriefly();
            };
            if (_count > Shown) tile.Visibility = Visibility.Collapsed;
            _tiles.Children.Add(tile);
            if (_count == Shown + 1)
            {
                _more = Ui.Button("Show all", () =>
                {
                    foreach (UIElement c in _tiles.Children) c.Visibility = Visibility.Visible;
                    _tiles.Children.Remove(_more);
                }, "Btn.Ghost");
                _tiles.Children.Add(_more);
            }
            else if (_more != null) { _tiles.Children.Remove(_more); _tiles.Children.Add(_more); }
            if (_more != null) _more.Content = "Show all " + _count;
        }
    }
}
