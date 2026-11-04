using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        /// <summary>The "Make a look" card: a button that opens the guided flow in place.</summary>
        private FrameworkElement AppearanceLookCard()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Make a look", "A guided way to set theme, seed colour, contrast, density and text size together, with a review of what changes before anything is applied."));
            var host = new ContentControl { IsTabStop = false };
            var start = Ui.Button("Make a look step by step", () =>
            {
                host.Content = AppearanceLookGuide(() => host.Content = null, () => host.Content = null);
            }, "Btn.Accent", Icons.Wand);
            sp.Children.Add(start);
            sp.Children.Add(host);
            return SearchScope.SetItem(Card(sp), "make a look guided theme seed contrast density review");
        }

        internal GuidedForm AppearanceLookGuide(Action done, Action cancel)
        {
            var now = AppearanceLook.FromCurrent();
            var look = AppearanceLook.FromCurrent();
            var review = new TextBlock { Style = Ui.Style("T.Body"), TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text") };
            var steps = new List<GuidedStep>
            {
                new GuidedStep
                {
                    Title = "Theme", Help = "Light, dark, or whatever Windows is using. System follows the Windows app mode.",
                    Build = () => Choice("Theme", new[] { ("Light", "light"), ("Dark", "dark"), ("System", "system") }, look.Theme, v => look.Theme = v)
                },
                new GuidedStep
                {
                    Title = "Seed colour", Help = "Every other colour is derived from this one, with a full light and dark set.",
                    Build = () => Choice("Seed colour", Appearance.SeedIds.Select((id, i) => (Appearance.SeedLabels[i], id)).ToArray(), look.Seed, v => look.Seed = v)
                },
                new GuidedStep
                {
                    Title = "Contrast and density", Help = "Stronger outlines make edges easier to see. Compact rows fit more on screen. Text size scales text and spacing together.",
                    Build = () =>
                    {
                        var sp = new StackPanel();
                        sp.Children.Add(Switch("High-contrast outlines", "Draws dividers and card borders in the stronger outline colour.", look.HighContrastOutlines, v => look.HighContrastOutlines = v));
                        sp.Children.Add(Choice("Density", new[] { ("Comfortable", false), ("Compact", true) }, look.Compact, v => look.Compact = v));
                        sp.Children.Add(Choice("Text size", new[] { 90, 100, 110, 120, 130, 140, 150 }.Select(p => (p + " %", p)).ToArray(), look.TextPercent, v => look.TextPercent = v));
                        return sp;
                    }
                },
                new GuidedStep
                {
                    Title = "Review and apply", Help = "Applying changes only the five choices above. Reset to defaults on this page undoes it.",
                    Build = () => review,
                    Entered = () =>
                    {
                        var changes = look.Changes(now);
                        review.Text = changes.Count == 0 ? "Nothing would change: this is the look you already have." : string.Join("\n", changes);
                    }
                }
            };
            var form = new GuidedForm("Make a look", steps, "Apply look", () => look.Apply(), cancel);
            form.Finished += done;
            return form;
        }
    }
}
