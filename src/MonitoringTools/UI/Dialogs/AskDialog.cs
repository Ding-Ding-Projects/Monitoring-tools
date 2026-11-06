using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>Asks for the {ask:Name} values a snippet needs before it is pasted. Enter fills it in, Esc cancels.</summary>
    internal sealed class AskDialog : ToolDialogWindow
    {
        private readonly Dictionary<string, TextBox> _boxes = new Dictionary<string, TextBox>();
        private bool _ok;

        /// <summary>The answers, or null when cancelled.</summary>
        public static Dictionary<string, string> Ask(string snippetName, IList<string> names)
        {
            var d = new AskDialog(snippetName, names) { Topmost = true };
            d.ShowDialog();
            return d._ok ? d._boxes.ToDictionary(k => k.Key, k => k.Value.Text) : null;
        }

        private AskDialog(string snippetName, IList<string> names) : base("Fill in the snippet")
        {
            var sp = new StackPanel();
            foreach (var n in names)
            {
                sp.Children.Add(DialogParts.Label(n));
                var box = new TextBox { Style = Ui.Style("Input"), Height = 44, FontSize = 14 };
                AutomationProperties.SetName(box, n);
                _boxes[n] = box; sp.Children.Add(box);
            }
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost");
            var ok = Ui.Button("Paste it", OnPrimary, "Btn.Accent", Icons.Check);
            sp.Children.Add(SmartDialogParts.ActionBar(null, "", new[] { cancel }, ok, "Enter"));
            ShowView("form", sp, 460, snippetName, "Fill in the blanks, then it is pasted where you were typing.", Icons.Text);
            PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; OnPrimary(); } };
            Loaded += (s, e) => _boxes.Values.FirstOrDefault()?.Focus();
        }

        protected override void OnPrimary() { _ok = true; Close(); }
    }
}
