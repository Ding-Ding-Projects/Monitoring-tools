using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>A starting point for a new script: what it creates, what it sets, and its body.</summary>
    public sealed class ScriptPreset
    {
        public string Id { get; init; }
        public string Label { get; init; }
        public ScriptShell Shell { get; init; }
        public string Group { get; init; }
        /// <summary>What the script does when it runs.</summary>
        public string Does { get; init; }
        public string SuggestedName { get; init; }
        public string Body { get; init; }
    }

    /// <summary>The presets offered by the new-shortcut guide and by an empty script editor. Blank ones come from the app's own templates.</summary>
    public static class ScriptPresets
    {
        private static string Crlf(string s) => s.Replace("\r\n", "\n").Replace("\n", "\r\n");

        public static IReadOnlyList<ScriptPreset> All { get; } = new List<ScriptPreset>
        {
            new ScriptPreset { Id = "blank-ps", Label = "Blank PowerShell script", Group = "Blank", Shell = ScriptShell.PowerShell, SuggestedName = "script",
                Does = "Starts with the app's own template: a $Target parameter that receives whatever was before the colon, and one line that prints it.",
                Body = Shortcuts.Template(ScriptShell.PowerShell) },
            new ScriptPreset { Id = "blank-bat", Label = "Blank batch file", Group = "Blank", Shell = ScriptShell.Batch, SuggestedName = "script",
                Does = "Starts with the app's own template: %~1 receives whatever was before the colon, and one line prints it.",
                Body = Shortcuts.Template(ScriptShell.Batch) },

            new ScriptPreset { Id = "bat-open-folder", Label = "Open folder", Group = "Batch file", Shell = ScriptShell.Batch, SuggestedName = "openfolder",
                Does = "Opens the folder in Explorer. If the copied path is a file, its containing folder opens.",
                Body = Crlf("""
@echo off
rem %~1 is whatever was before the colon: a file or a folder path.
if exist "%~1\*" (
  explorer "%~1"
) else (
  explorer "%~dp1"
)
""") },
            new ScriptPreset { Id = "bat-copy-path", Label = "Copy path", Group = "Batch file", Shell = ScriptShell.Batch, SuggestedName = "copypath",
                Does = "Puts the full path on the clipboard, without quotes, ready to paste.",
                Body = Crlf("""
@echo off
rem %~f1 expands to the full path of the input.
echo %~f1| clip
echo Copied: %~f1
""") },
            new ScriptPreset { Id = "bat-zip", Label = "Zip it", Group = "Batch file", Shell = ScriptShell.Batch, SuggestedName = "zipit",
                Does = "Creates a .zip next to the file or folder, named after it, using the tar tool that ships with Windows.",
                Body = Crlf("""
@echo off
rem Creates "<input>.zip" next to the input using the tar that ships with Windows.
tar -a -c -f "%~1.zip" "%~1"
if errorlevel 1 (
  echo Could not create the archive.
  exit /b 1
)
echo Created %~1.zip
""") },

            new ScriptPreset { Id = "ps-resize-image", Label = "Resize image", Group = "PowerShell", Shell = ScriptShell.PowerShell, SuggestedName = "resizeimg",
                Does = "Makes a copy of an image no wider than 1600 pixels, keeping its proportions, saved beside the original with -resized in the name. The original is untouched.",
                Body = Crlf("""
# $Target is the image path. A copy no wider than $MaxWidth pixels is saved beside it.
param([string]$Target = $env:MT_INPUT)
$MaxWidth = 1600
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile($Target)
try {
    $ratio = [Math]::Min(1.0, $MaxWidth / $source.Width)
    $width = [int][Math]::Round($source.Width * $ratio)
    $height = [int][Math]::Round($source.Height * $ratio)
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source, 0, 0, $width, $height)
    $folder = Split-Path -Parent $Target
    $name = [IO.Path]::GetFileNameWithoutExtension($Target) + '-resized' + [IO.Path]::GetExtension($Target)
    $out = Join-Path $folder $name
    $bitmap.Save($out)
    Write-Output "Saved $out ($width x $height)"
} finally {
    $source.Dispose()
}
""") },
            new ScriptPreset { Id = "ps-rename-date", Label = "Rename by date", Group = "PowerShell", Shell = ScriptShell.PowerShell, SuggestedName = "datename",
                Does = "Renames the file to start with its last-modified date as yyyy-MM-dd. A file that already starts with a date is left alone.",
                Body = Crlf("""
# $Target is the file path. The name gets a yyyy-MM-dd prefix taken from the file's last-modified time.
param([string]$Target = $env:MT_INPUT)
$item = Get-Item -LiteralPath $Target
if ($item.PSIsContainer) { Write-Output 'Pick a file, not a folder.'; exit 1 }
if ($item.Name -match '^\d{4}-\d{2}-\d{2}') { Write-Output "Already dated: $($item.Name)"; exit 0 }
$stamp = $item.LastWriteTime.ToString('yyyy-MM-dd')
$newName = "$stamp $($item.Name)"
Rename-Item -LiteralPath $item.FullName -NewName $newName
Write-Output "Renamed to $newName"
""") },
            new ScriptPreset { Id = "ps-hash-file", Label = "Hash file", Group = "PowerShell", Shell = ScriptShell.PowerShell, SuggestedName = "hashit",
                Does = "Computes the SHA-256 hash of the file, prints it, and copies it to the clipboard.",
                Body = Crlf("""
# $Target is the file path. Prints the SHA-256 hash and copies it to the clipboard.
param([string]$Target = $env:MT_INPUT)
$hash = (Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash
Set-Clipboard -Value $hash
Write-Output "SHA-256: $hash"
Write-Output 'Copied to the clipboard.'
""") },
        };

        /// <summary>
        /// A row of chips for an editor whose script is still empty or untouched. <paramref name="insert"/> receives the body to put in
        /// the editor; the caller decides whether that replaces the text, so nothing is overwritten silently.
        /// </summary>
        public static FrameworkElement Chips(ScriptShell shell, Action<ScriptPreset> insert)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            wrap.Children.Add(Ui.Mono("START FROM A PRESET", Ui.Res("B.TextMute"), 11));
            foreach (var p in All.Where(p => p.Shell == shell && p.Group != "Blank"))
            {
                var preset = p;
                var b = Ui.Button(p.Label, () => insert(preset), "Btn.Chip");
                b.Height = 26; b.ToolTip = p.Does;
                AutomationProperties.SetName(b, "Use preset " + p.Label + ". " + p.Does);
                wrap.Children.Add(b);
            }
            return wrap;
        }
    }

    /// <summary>The guided "New shortcut" flow: choose a starting point, name it, choose how it runs, then review exactly what will be created.</summary>
    public static class ShortcutGuide
    {
        public static FrameworkElement Build(Action<Shortcut> created, Action cancelled)
        {
            var preset = ScriptPresets.All[0];
            string name = null, description = "";
            bool showWindow = false, elevated = false;

            string UniqueName(string wanted)
            {
                var n = wanted; int i = 1;
                while (Shortcuts.Find(n) != null || Shortcuts.Reserved.Contains(n.ToLowerInvariant()) || !Shortcuts.IsValidName(n)) n = wanted + i++;
                return n;
            }

            // step 1: starting point
            FrameworkElement StepPreset()
            {
                var sp = new StackPanel();
                var rows = new List<(Button B, ScriptPreset P)>();
                void Paint() { foreach (var (b, p) in rows) { b.Background = p == preset ? Ui.Res("B.SecondaryContainer") : System.Windows.Media.Brushes.Transparent; AutomationProperties.SetItemStatus(b, p == preset ? "selected" : "not selected"); } }
                foreach (var group in ScriptPresets.All.GroupBy(p => p.Group))
                {
                    sp.Children.Add(Ui.Mono(group.Key.ToUpperInvariant(), Ui.Res("B.TextMute"), 11));
                    ((FrameworkElement)sp.Children[sp.Children.Count - 1]).Margin = new Thickness(0, 6, 0, 4);
                    foreach (var p in group)
                    {
                        var item = p;
                        var content = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
                        content.Children.Add(new TextBlock { Text = p.Label, Foreground = Ui.Res("B.Text"), FontSize = 14 });
                        content.Children.Add(new TextBlock { Text = p.Does, Foreground = Ui.Res("B.TextMute"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                        var b = new Button { Style = Ui.Style("Btn.Row"), Content = content, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 1, 0, 1) };
                        AutomationProperties.SetName(b, p.Label + ". " + p.Does);
                        b.Click += (s, e) => { preset = item; Paint(); };
                        rows.Add((b, p)); sp.Children.Add(b);
                    }
                }
                Paint();
                return sp;
            }

            // step 2: name
            FrameworkElement StepName()
            {
                var sp = new StackPanel();
                var nameBox = new TextBox { Style = Ui.Style("Input"), Width = 240, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "Letters, digits, - and _, starting with a letter, 2 to 32 characters" };
                AutomationProperties.SetName(nameBox, "Shortcut name");
                var descBox = new TextBox { Style = Ui.Style("Input"), ToolTip = "Shows as the tooltip on the shortcut chip" };
                AutomationProperties.SetName(descBox, "What it does");
                nameBox.TextChanged += (s, e) => name = nameBox.Text.Trim();
                descBox.TextChanged += (s, e) => description = descBox.Text.Trim();
                nameBox.Text = name = UniqueName(preset.SuggestedName);
                sp.Children.Add(Ui.Mono("NAME  (YOU TYPE IT AFTER A COLON, FOR EXAMPLE :" + preset.SuggestedName + ")", Ui.Res("B.TextMute"), 11));
                sp.Children.Add(nameBox);
                sp.Children.Add(Ui.Mono("WHAT IT DOES (OPTIONAL)", Ui.Res("B.TextMute"), 11).WithTopMargin(8));
                descBox.Text = description = preset.Id.StartsWith("blank") ? "" : preset.Does;
                sp.Children.Add(descBox);
                return sp;
            }

            // step 3: how it runs
            FrameworkElement StepOptions()
            {
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = $"Runs with: {(preset.Shell == ScriptShell.Batch ? "a batch file (.bat)" : "PowerShell (.ps1)")}, set by the starting point you chose.", Foreground = Ui.Res("B.TextSoft"), FontSize = 12, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
                FrameworkElement Sw(string label, string sub, bool initial, Action<bool> set, string accent)
                {
                    var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = initial, Tag = Ui.Res(accent), Margin = new Thickness(0, 4, 0, 10) };
                    var text = new StackPanel();
                    text.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14 });
                    text.Children.Add(new TextBlock { Text = sub, Foreground = Ui.Res("B.TextMute"), FontSize = 12, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(text);
                    row.Children.Add(InfoIcon.ForSwitch(label, sub, () => cb.IsChecked == true));
                    cb.Content = row;
                    cb.Checked += (s, e) => set(true); cb.Unchecked += (s, e) => set(false);
                    return cb;
                }
                sp.Children.Add(Sw("Show a console window", "Off runs the script hidden. Turn it on to see its output as it runs.", showWindow, v => showWindow = v, "B.Mint"));
                sp.Children.Add(Sw("Run as administrator", "Asks Windows for administrator rights on each run. Leave off unless the script needs them.", elevated, v => elevated = v, "B.Amber"));
                return sp;
            }

            // step 4: review
            var review = new TextBlock { Foreground = Ui.Res("B.Text"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
            var preview = Ui.Mono("", Ui.Res("B.TextSoft"), 12);
            preview.TextWrapping = TextWrapping.NoWrap;
            FrameworkElement StepReview()
            {
                var sp = new StackPanel();
                sp.Children.Add(review);
                sp.Children.Add(Ui.Mono("SCRIPT PREVIEW", Ui.Res("B.TextMute"), 11).WithTopMargin(10));
                sp.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 0), Child = new ScrollViewer { MaxHeight = 200, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = preview } }); Core.Language.SetLiteral(preview, true);   // the script itself
                return sp;
            }
            void FillReview()
            {
                var ext = preset.Shell == ScriptShell.Batch ? ".bat" : ".ps1";
                review.Text = $"This creates the shortcut :{name} and saves its script as {name}{ext} in the scripts folder.\n" +
                              $"Starting point: {preset.Label}. Runs with {(preset.Shell == ScriptShell.Batch ? "a batch file" : "PowerShell")}.\n" +
                              $"Console window: {(showWindow ? "shown" : "hidden")}. Administrator rights: {(elevated ? "requested on each run" : "not requested")}.\n" +
                              "Nothing runs now. You can edit the script, test it and delete it afterwards in the editor.";
                preview.Text = preset.Body.TrimEnd();
            }

            var steps = new List<GuidedStep>
            {
                new GuidedStep { Title = "Pick a starting point", Help = "Each one fills the script for you and can be edited afterwards. The blank ones use the app's own template.", Build = StepPreset },
                new GuidedStep
                {
                    Title = "Name it", Help = "You run the shortcut by copying a path or text and adding :name to the end.", Build = StepName,
                    Validate = () =>
                    {
                        if (!Shortcuts.IsValidName(name ?? "")) return "The name must start with a letter and use only letters, digits, - and _, 2 to 32 characters.";
                        if (Shortcuts.Reserved.Contains(name.ToLowerInvariant())) return $"\"{name}\" is built in. Choose another name.";
                        if (Shortcuts.Find(name) != null) return $":{name} already exists. Choose another name.";
                        return null;
                    }
                },
                new GuidedStep { Title = "Choose how it runs", Help = "You can change either choice later in the editor.", Build = StepOptions },
                new GuidedStep { Title = "Review", Help = "Check what will be created.", Build = StepReview, Entered = FillReview },
            };

            GuidedForm form = null;
            form = new GuidedForm("New shortcut", steps, "Create shortcut", () =>
            {
                var s = Settings.Current;
                if (Shortcuts.Find(name) != null) return $":{name} already exists. Go back and choose another name.";
                var sc = new Shortcut { Name = name, Shell = preset.Shell, Description = description, ShowWindow = showWindow, RunElevated = elevated };
                Shortcuts.WriteScript(sc, preset.Body);
                s.Shortcuts.Add(sc);
                Settings.Save();
                created?.Invoke(sc);
                return null;
            }, cancelled);
            return form;
        }

        private static T WithTopMargin<T>(this T e, double top) where T : FrameworkElement { e.Margin = new Thickness(0, top, 0, 0); return e; }
    }
}
