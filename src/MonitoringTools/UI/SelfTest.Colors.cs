using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Clip;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>Colours in copied text and code: the scanner reads every form, and the real toast shows a swatch for each.
        /// Run on their own by the build (MT_ONLY=colors).</summary>
        private static async System.Threading.Tasks.Task ColorChecksAsync()
        {
            Check("colors: every written form parses to the right colour", () =>
            {
                (string text, Color want)[] cases =
                {
                    ("#f80", Color.FromRgb(0xFF, 0x88, 0x00)),
                    ("#FF8800", Color.FromRgb(0xFF, 0x88, 0x00)),
                    ("#ff880080", Color.FromArgb(0x80, 0xFF, 0x88, 0x00)),
                    ("rgb(255, 136, 0)", Color.FromRgb(255, 136, 0)),
                    ("rgb(255 136 0 / 50%)", Color.FromArgb(128, 255, 136, 0)),
                    ("rgba(0,0,255,0.5)", Color.FromArgb(128, 0, 0, 255)),
                    ("hsl(120, 100%, 50%)", Color.FromRgb(0, 255, 0)),
                    ("hsl(0deg 100% 50%)", Color.FromRgb(255, 0, 0)),
                    ("0xFF8800", Color.FromRgb(0xFF, 0x88, 0x00)),
                    ("Color accent = 0x80FF8800", Color.FromArgb(0x80, 0xFF, 0x88, 0x00)),
                    ("Color.FromArgb(128, 255, 136, 0)", Color.FromArgb(128, 255, 136, 0)),
                    ("Colors.Tomato", Colors.Tomato),
                    ("rebeccapurple", Color.FromRgb(0x66, 0x33, 0x99)),
                    ("slategrey", Colors.SlateGray),
                };
                foreach (var (text, want) in cases)
                {
                    var got = ColorScan.Parse(text);
                    if (got == null) return text + " did not parse";
                    if (got.Value != want) return $"{text} parsed as {got.Value}, expected {want}";
                }
                return null;
            });

            Check("colors: code and prose find each colour once, in order, without false hits", () =>
            {
                var css = ".card { color: tomato; background: #1B211E; border-color: rgb(0 81 58 / 80%); }\n.alt { color: tomato; }";
                var f = ColorScan.Find(css);
                var seen = string.Join(" | ", f.Select(x => x.Text));
                if (seen != "tomato | #1B211E | rgb(0 81 58 / 80%)") return "found " + seen;
                var xaml = "<Border Background=\"#FF2B4A3F\" />";
                var x1 = ColorScan.Find(xaml).FirstOrDefault();
                if (x1 == null || x1.Color != Color.FromArgb(0xFF, 0x2B, 0x4A, 0x3F)) return "XAML #AARRGGBB was not read alpha first";
                var prose = "Fixed in #123, see https://example.com/page#top and &#123; in the HTML. The red team met.";
                var p = ColorScan.Find(prose);
                if (p.Count != 0) return "prose gave colours: " + string.Join(", ", p.Select(z => z.Text));
                if (ColorScan.Find("Access denied: 0x80070005").Count != 0) return "a Windows error code read as a colour";
                if (ClipItem.FromText("0x80070005").Kind == ClipKind.Color) return "a copied error code became a colour copy";
                if (ColorScan.Whole("orange") != null) return "a bare colour name became a colour copy";
                if (ColorScan.Find("orange").Count != 1) return "a bare colour name had no preview";
                if (ColorScan.Whole("#00FF00;") == null) return "#00FF00; was not a colour copy";
                if (ClipItem.FromText("hsl(200 50% 40%)").Kind != ClipKind.Color) return "an hsl() copy was not a colour copy";
                return null;
            });

            await CheckAsync("colors: the toast of copied code shows a swatch per colour, and pressing one copies its hex", async () =>
            {
                var code = "button {\n  color: white;\n  background: #6FDBB4;\n  box-shadow: 0 0 0 4px rgba(111, 219, 180, .4);\n}";
                var w = App.Toasts.ShowClip(ClipItem.FromText(code));
                try
                {
                    w.ExpandNow();
                    await System.Threading.Tasks.Task.Delay(500); w.UpdateLayout();
                    var texts = ToastTexts(w);
                    if (!texts.Contains("Colours in this copy · 3")) return "no colour strip label: " + texts;
                    var chips = Descend<Button>(w).Where(b => (System.Windows.Automation.AutomationProperties.GetName(b) ?? "").StartsWith("Colour ")).ToList();
                    if (chips.Count != 3) return chips.Count + " swatches";
                    var green = chips.FirstOrDefault(b => System.Windows.Automation.AutomationProperties.GetName(b).Contains("#6FDBB4"));
                    if (green == null) return "no #6FDBB4 swatch: " + string.Join(", ", chips.Select(b => System.Windows.Automation.AutomationProperties.GetName(b)));
                    green.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                    string clip = "";
                    for (int i = 0; i < 10 && clip != "#6FDBB4"; i++) { await System.Threading.Tasks.Task.Delay(150); try { clip = System.Windows.Clipboard.GetText(); } catch { } }
                    if (clip != "#6FDBB4") return "pressing the swatch copied " + clip;
                    return null;
                }
                finally { w.Close(); }
            });

            await CheckAsync("colors: a copied colour shows a large preview with hex, rgb and hsl", async () =>
            {
                var w = App.Toasts.ShowClip(ClipItem.FromText("hsl(160 58% 65% / 50%)"));
                try
                {
                    w.ExpandNow();
                    await System.Threading.Tasks.Task.Delay(400); w.UpdateLayout();
                    var texts = ToastTexts(w);
                    foreach (var want in new[] { "#", "rgba(", "hsl(", "opaque" })
                        if (!texts.Contains(want)) return "missing " + want + ": " + texts;
                    return null;
                }
                finally { w.Close(); }
            });
        }
    }
}
