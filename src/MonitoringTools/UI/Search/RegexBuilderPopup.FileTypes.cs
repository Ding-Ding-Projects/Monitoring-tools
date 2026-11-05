using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Search
{
    // The Files view's per-extension settings: encoding, skipping and binary handling, kept in settings and used by every file search.
    public sealed partial class RegexBuilderPopup
    {
        private StackPanel _gTypes;
        private TextBox _gTypeExt;

        private FrameworkElement FileTypesPanel()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 4) };
            sp.Children.Add(Eyebrow("File type settings"));
            sp.Children.Add(Note("Rules by file extension, kept on this computer and used by every file search: the encoding to read the file with, whether to skip it, and how to search it when it looks binary. A forced encoding reads the file as text even when it has null bytes, such as UTF-16 without a byte order mark."));
            _gTypes = new StackPanel();
            sp.Children.Add(_gTypes);
            var add = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var btn = Ui.Button("Add file type", AddFileType, "Btn.Ghost");
            btn.ToolTip = "Add a rule for this extension";
            DockPanel.SetDock(btn, Dock.Right); add.Children.Add(btn);
            _gTypeExt = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Margin = new Thickness(0, 0, 6, 0), ToolTip = "An extension such as .log or .txt" };
            AutomationProperties.SetName(_gTypeExt, "Extension for a new file type rule, such as .log");
            add.Children.Add(_gTypeExt);
            sp.Children.Add(add);
            RenderFileTypes();
            return sp;
        }

        /// <summary>Adds a rule for an extension. Returns false with a status when the extension is not valid or already has a rule.</summary>
        internal bool AddFileType(string extension)
        {
            var ext = RegexGrep.NormalizeExtension(extension);
            if (ext == null) { FileTypeStatus("Type an extension such as .log: letters, digits, _ and - only."); return false; }
            var list = Settings.Current.RegexFileTypes ??= new List<RegexFileType>();
            if (list.Any(r => string.Equals(RegexGrep.NormalizeExtension(r.Extension), ext, StringComparison.OrdinalIgnoreCase))) { FileTypeStatus("There is already a rule for " + ext + "."); return false; }
            list.Add(new RegexFileType { Extension = ext });
            Settings.Save();
            RenderFileTypes();
            return true;
        }

        private void AddFileType()
        {
            if (AddFileType(_gTypeExt?.Text ?? "") && _gTypeExt != null) _gTypeExt.Text = "";
        }

        private void FileTypeStatus(string text)
        {
            if (_gStatus == null) return;
            _gStatus.Text = text;
            _gStatus.Foreground = Ui.Res("B.Amber");
        }

        private static string Next(string[] choices, string current)
        {
            int i = Array.IndexOf(choices, current);
            return choices[(i + 1) % choices.Length];
        }

        private void RenderFileTypes()
        {
            if (_gTypes == null) return;
            _gTypes.Children.Clear();
            var list = Settings.Current.RegexFileTypes ?? new List<RegexFileType>();
            if (list.Count == 0) { _gTypes.Children.Add(Ui.Mono("No file type rules. Every file uses the options above.", Ui.Res("B.TextMute"), 11)); return; }
            foreach (var r in list.ToList())
            {
                var rule = r;
                var ext = RegexGrep.NormalizeExtension(rule.Extension) ?? rule.Extension;
                var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
                var del = Ui.Button("Remove", () => { Settings.Current.RegexFileTypes.Remove(rule); Settings.Save(); RenderFileTypes(); }, "Btn.Ghost");
                AutomationProperties.SetName(del, "Remove the rule for " + ext);
                DockPanel.SetDock(del, Dock.Right); row.Children.Add(del);
                Button skip = null;
                skip = Chip("Skip", "Leave every " + ext + " file out of the search", () => { rule.Skip = !rule.Skip; Settings.Save(); Paint(skip, rule.Skip); });
                AutomationProperties.SetName(skip, "Skip " + ext + " files");
                Paint(skip, rule.Skip);
                DockPanel.SetDock(skip, Dock.Right); row.Children.Add(skip);
                var bin = Ui.Button("Binary: " + (rule.Binary ?? "Default"), () => { rule.Binary = Next(RegexGrep.BinaryChoices, rule.Binary ?? "Default"); Settings.Save(); RenderFileTypes(); }, "Btn.Chip");
                bin.ToolTip = "When a " + ext + " file looks binary: Default uses the options above, Skip leaves it out, Bytes searches it byte by byte, Text reads it as text. Press to change.";
                AutomationProperties.SetName(bin, "Binary handling for " + ext + " files: " + (rule.Binary ?? "Default"));
                bin.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(bin, Dock.Right); row.Children.Add(bin);
                var enc = Ui.Button("Encoding: " + (rule.Encoding ?? "Auto"), () => { rule.Encoding = Next(RegexGrep.Encodings, rule.Encoding ?? "Auto"); Settings.Save(); RenderFileTypes(); }, "Btn.Chip");
                enc.ToolTip = "The encoding " + ext + " files are read with: Auto detects it, the others force it. Press to change.";
                AutomationProperties.SetName(enc, "Encoding for " + ext + " files: " + (rule.Encoding ?? "Auto"));
                DockPanel.SetDock(enc, Dock.Right); row.Children.Add(enc);
                var label = Ui.Mono(ext, Ui.Res("B.Text"), 12);
                label.VerticalAlignment = VerticalAlignment.Center;
                Core.Language.SetLiteral(label, true);
                row.Children.Add(label);
                _gTypes.Children.Add(row);
            }
        }
    }
}
