using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using MonitoringTools.Core;
using Lang = MonitoringTools.Core.Language;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private static readonly string[] HumourLevels = { "Plain", "Light", "Friendly", "Cheeky", "Playful" };
        private static ControlTemplate _sliderTemplate;

        // The language page: app language, two humour sliders, emoji in dialogs and the narrator.
        private FrameworkElement LanguagePage()
        {
            var s = Settings.Current;
            var root = PageRoot("Settings", "Language and voice", "Language, humour, emoji and the narrator.");

            // ---- language
            var lang = new StackPanel();
            lang.Children.Add(CardTitle("Language", "Choose the language of the whole app: every page, toast, dialog, tray menu and palette entry. Switching applies at once to every open window."));
            lang.Children.Add(Choice("App language",
                new[] { ("English", LanguageMode.English), ("廣東話", LanguageMode.Cantonese), ("English + 廣東話", LanguageMode.Bilingual) },
                s.LanguageMode, v => { s.LanguageMode = v; Lang.Refresh(); }));
            lang.Children.Add(Ui.Text("Default: English. Bilingual shows English first, then Cantonese: short labels sit on one line, sentences use two lines."));
            root.Children.Add(Card(lang));

            // ---- humour
            var hum = new StackPanel();
            hum.Children.Add(CardTitle("Humour", "Humour changes only the tone of headline messages such as the offline dialog, conversion results and snooze notices. The facts in them never change. Level 1 is plain and precise; level 5 is playful."));
            hum.Children.Add(HumourRow("English humour", "Default: 3. Used for English headlines, and for the English line in bilingual mode.", LanguageMode.English,
                () => s.EnglishHumour, v => s.EnglishHumour = v));
            hum.Children.Add(HumourRow("Cantonese humour", "Default: 3. Used for Cantonese headlines, and for the Cantonese line in bilingual mode.", LanguageMode.Cantonese,
                () => s.CantoneseHumour, v => s.CantoneseHumour = v));
            hum.Children.Add(Switch("Emoji in dialogs", "Show emoji in toast and dialog titles. Turn it off to strip emoji from the start and end of titles. Default: on.",
                s.EmojiInDialogs, v => s.EmojiInDialogs = v));
            root.Children.Add(Card(hum));

            // ---- narrator
            root.Children.Add(Card(NarratorSection(s)));
            return root;
        }

        // -------------------------------------------------------------- humour

        private static FrameworkElement HumourRow(string label, string sub, LanguageMode language, Func<int> get, Action<int> set)
        {
            var row = new StackPanel { Margin = new Thickness(0, 4, 0, 16) };
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
            var value = new TextBlock { Foreground = Ui.Res("B.Mint"), FontSize = 13, FontFamily = Ui.Font("F.Body"), FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(value, 1); head.Children.Add(value);
            row.Children.Add(head);

            var slider = MakeSlider(1, 5, 1, get(), label);
            slider.Margin = new Thickness(0, 8, 0, 6);
            row.Children.Add(slider);
            row.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap });

            var preview = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            Lang.SetLiteral(preview, true);   // previews are already in their own language
            var previewTitle = new TextBlock { Text = "Preview", Style = Ui.Style("T.Eyebrow"), FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            var lines = Enumerable.Range(0, 4).Select(_ => new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) }).ToArray();
            var box = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8) };
            var inner = new StackPanel();
            foreach (var l in lines) inner.Children.Add(l);
            box.Child = inner;
            row.Children.Add(previewTitle);
            preview.Children.Add(box);
            row.Children.Add(preview);

            void Paint(int v)
            {
                value.Text = v + " · " + HumourLevels[v - 1];
                lines[0].Text = Lang.Variant("offline.title", language, v);
                lines[1].Text = Lang.Variant("convert.done", language, v, "MP4");
                lines[2].Text = Lang.Variant("snooze", language, v, "14:30");
                lines[3].Text = Lang.Variant("history.empty", language, v);
                Lang.Refresh(value);
            }
            slider.ValueChanged += (a, e) =>
            {
                int v = (int)Math.Round(slider.Value);
                if (v == get()) { Paint(v); return; }
                set(v); Settings.Save(); Paint(v);
            };
            Paint(get());
            SearchScope.SetItem(row, "humour level tone " + (language == LanguageMode.English ? "english" : "cantonese 廣東話") + " slider");
            return row;
        }

        private static Slider MakeSlider(double min, double max, double tick, double value, string name)
        {
            var sl = new Slider
            {
                Minimum = min, Maximum = max, Value = value, TickFrequency = tick, IsSnapToTickEnabled = true,
                SmallChange = tick, LargeChange = tick, Width = 360, HorizontalAlignment = HorizontalAlignment.Left,
                Focusable = true, FocusVisualStyle = null, Template = SliderTemplate()
            };
            AutomationProperties.SetName(sl, name);
            return sl;
        }

        private static ControlTemplate SliderTemplate()
        {
            if (_sliderTemplate != null) return _sliderTemplate;
            const string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'>
  <Grid Height='30' Background='Transparent'>
    <Border x:Name='Ring' BorderBrush='{DynamicResource B.Mint}' BorderThickness='1' CornerRadius='10' Margin='-6,0,-6,0' Visibility='Collapsed'/>
    <Border Height='6' CornerRadius='3' Background='{DynamicResource B.Raised}' BorderBrush='{DynamicResource B.LineStrong}' BorderThickness='1' VerticalAlignment='Center'/>
    <Track x:Name='PART_Track'>
      <Track.DecreaseRepeatButton>
        <RepeatButton Command='Slider.DecreaseLarge' Focusable='False' IsTabStop='False'>
          <RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='6' CornerRadius='3' Background='{DynamicResource B.Mint}' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template>
        </RepeatButton>
      </Track.DecreaseRepeatButton>
      <Track.IncreaseRepeatButton>
        <RepeatButton Command='Slider.IncreaseLarge' Focusable='False' IsTabStop='False'>
          <RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='30' Background='Transparent'/></ControlTemplate></RepeatButton.Template>
        </RepeatButton>
      </Track.IncreaseRepeatButton>
      <Track.Thumb>
        <Thumb Width='20' Height='20' Focusable='False' Cursor='Hand'>
          <Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Fill='{DynamicResource B.Mint}' Stroke='{DynamicResource B.Window}' StrokeThickness='3'/></ControlTemplate></Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Ring' Property='Visibility' Value='Visible'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
            _sliderTemplate = (ControlTemplate)XamlReader.Parse(xaml);
            return _sliderTemplate;
        }

        // -------------------------------------------------------------- narrator

        private static FrameworkElement NarratorSection(Settings s)
        {
            var sec = new StackPanel();
            sec.Children.Add(CardTitle("Narrator"));
            sec.Children.Add(Switch("Read toasts aloud", "Speaks toast titles with the Windows voice you pick below. Stays quiet in game mode and when snoozed. Default: off.",
                s.NarratorEnabled, v => { s.NarratorEnabled = v; if (!v) Narrator.Stop(); }));

            var english = Narrator.Voices(false);
            var cantonese = Narrator.Voices(true);

            var voices = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
            voices.Children.Add(new TextBlock { Text = "Voices", Style = Ui.Style("T.Title"), FontSize = 14, Margin = new Thickness(0, 0, 0, 8) });
            var search = new SearchField("language:voices", "Search voices", compact: true);
            voices.Children.Add(search);
            var host = new StackPanel();
            voices.Children.Add(host);

            void Build()
            {
                host.Children.Clear();
                int shown = 0;
                shown += VoiceGroup(host, "English voice", "Default: automatic, the first installed English voice.", english, search,
                    () => s.EnglishVoiceId, id => s.EnglishVoiceId = id, false);
                shown += VoiceGroup(host, "Cantonese voice", "Default: automatic, the first installed Cantonese voice.", cantonese, search,
                    () => s.CantoneseVoiceId, id => s.CantoneseVoiceId = id, true);
                search.SetCount(shown, english.Count + cantonese.Count, "voices");
            }
            search.Filter += Build;
            search.SampleSource = () => english.Concat(cantonese).Select(v => v.Name + " " + v.Language);
            Build();
            SearchScope.SetItem(voices, "voices narrator speech english cantonese 廣東話");
            sec.Children.Add(voices);

            // rate
            var rate = new StackPanel { Margin = new Thickness(0, 6, 0, 8) };
            var rateHead = new Grid();
            rateHead.ColumnDefinitions.Add(new ColumnDefinition());
            rateHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rateHead.Children.Add(new TextBlock { Text = "Speaking rate", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
            var rateValue = new TextBlock { Foreground = Ui.Res("B.Mint"), FontSize = 13, FontFamily = Ui.Font("F.Body"), FontWeight = FontWeights.SemiBold };
            Lang.SetLiteral(rateValue, true);
            Grid.SetColumn(rateValue, 1); rateHead.Children.Add(rateValue);
            rate.Children.Add(rateHead);
            var slider = MakeSlider(0.5, 2.0, 0.1, s.NarratorRate, "Speaking rate");
            slider.Margin = new Thickness(0, 8, 0, 6);
            rate.Children.Add(slider);
            rate.Children.Add(new TextBlock { Text = "Default: 1.0×. Slower is 0.5×, faster is 2.0×.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
            rateValue.Text = s.NarratorRate.ToString("0.0") + "×";
            slider.ValueChanged += (a, e) =>
            {
                var v = Math.Round(slider.Value, 1);
                rateValue.Text = v.ToString("0.0") + "×";
                if (Math.Abs(v - s.NarratorRate) < 0.001) return;
                s.NarratorRate = v; Settings.Save();
            };
            SearchScope.SetItem(rate, "speaking rate speed slider narrator");
            sec.Children.Add(rate);

            // test and stop
            var status = new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.Amber"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var test = Ui.Button("Test voice", () => { status.Visibility = Visibility.Collapsed; Narrator.Test(); }, "Btn.Accent", Icons.Play);
            test.ToolTip = "Speaks a short sentence in the current language so you can hear the voice and speed.";
            var stop = Ui.Button("Stop speaking", () => Narrator.Stop(), "Btn.Ghost", Icons.Pause);
            stop.ToolTip = "Stops the current speech and clears anything waiting.";
            buttons.Children.Add(test); buttons.Children.Add(stop);
            SearchScope.SetItem(buttons, "test voice stop speaking narrator");
            sec.Children.Add(buttons);

            Action<string> onNote = null;
            onNote = msg => status.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!status.IsLoaded) { Narrator.Note -= onNote; return; }
                status.Text = msg; status.Visibility = Visibility.Visible;
                Lang.Refresh(status);
            }));
            Narrator.Note += onNote;
            sec.Children.Add(status);
            return sec;
        }

        private static int VoiceGroup(Panel host, string title, string sub, IReadOnlyList<VoiceChoice> voices, SearchField search,
            Func<string> current, Action<string> choose, bool cantonese)
        {
            var group = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            group.Children.Add(new TextBlock { Text = title, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
            group.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 8) });

            int shown = 0;
            if (voices.Count == 0)
            {
                group.Children.Add(new TextBlock
                {
                    Text = cantonese
                        ? "No Cantonese voice is installed on this computer, so Cantonese is not spoken. Add one in the Windows speech settings."
                        : "No English voice is installed on this computer. Add one in the Windows speech settings.",
                    Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.Amber"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
                });
                group.Children.Add(Ui.Button("Open Windows speech settings", () => ActionProvider.Shell("ms-settings:speech"), "Btn.Ghost", Icons.Globe));
                host.Children.Add(group);
                return 0;
            }

            var wrap = new WrapPanel();
            var chips = new List<(Button B, string Id)>();
            void Paint()
            {
                var cur = current() ?? "";
                foreach (var (b, id) in chips)
                {
                    bool on = id == cur;
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }
            Button Chip(string label, string id, bool literal, string tip)
            {
                var b = Ui.Button(label, () => { choose(id); Settings.Save(); Paint(); }, "Btn.Chip");
                if (literal) Lang.SetLiteral(b, true);
                if (tip != null) b.ToolTip = tip;
                AutomationProperties.SetName(b, label);
                chips.Add((b, id));
                wrap.Children.Add(b);
                return b;
            }
            Chip("Automatic", "", false, null);
            foreach (var v in voices)
            {
                if (!search.State.Matches(v.Name + " " + v.Language)) continue;
                Chip(v.Name, v.Id, true, v.Language);
                shown++;
            }
            Paint();
            group.Children.Add(wrap);
            if (shown == 0 && search.State.IsActive)
                group.Children.Add(new TextBlock { Text = "No voices match.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute") });
            host.Children.Add(group);
            return shown;
        }
    }
}
