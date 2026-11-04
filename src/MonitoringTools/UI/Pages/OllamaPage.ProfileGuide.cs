using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        /// <summary>
        /// A guided way to make a chat profile: name and instructions, the optional parameters, then a review. Saving goes through
        /// <see cref="OllamaProfiles.TryBuild"/> and <see cref="OllamaProfiles.Upsert"/>, the same checks the one-line save uses.
        /// </summary>
        internal GuidedForm OllamaProfileGuide(List<OllamaProfile> profiles, Action<OllamaProfile> saved, Action cancel)
        {
            var name = Input("", 260, "A short name. The same name replaces the old profile.");
            var prompt = Input("", double.NaN, "Instructions sent before the conversation. Empty sends none.");
            prompt.HorizontalAlignment = HorizontalAlignment.Stretch; prompt.AcceptsReturn = true; prompt.TextWrapping = TextWrapping.Wrap; prompt.MinHeight = 80; prompt.MaxHeight = 160;
            prompt.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            var temp = Input("", 80, "Randomness, 0 to 2. Empty uses the model's own default.");
            var topP = Input("", 80, "Nucleus sampling, above 0 and at most 1. Empty uses the model's own default.");
            var ctx = Input("", 110, "Context window in tokens (128 to 1048576). Empty uses the server default.");
            var hint = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            var review = new TextBlock { Style = Ui.Style("T.Body"), TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text") };

            string Problem() => OllamaProfiles.TryBuild(name.Text, prompt.Text, temp.Text, topP.Text, ctx.Text, out _);
            void ShowHint()
            {
                var p = Problem();
                var allEmpty = new[] { temp.Text, topP.Text, ctx.Text }.All(string.IsNullOrWhiteSpace);
                hint.Text = allEmpty ? "All three are optional. Empty fields use the model's own defaults." : (p != null && p.StartsWith("Give the profile") ? "Parameters look fine." : p ?? "Parameters look fine.");
                hint.Foreground = Ui.Res(allEmpty || p == null || p.StartsWith("Give the profile") ? "B.TextMute" : "B.Amber");
            }
            foreach (var tb in new[] { temp, topP, ctx }) tb.TextChanged += (a, b) => ShowHint();

            string Fmt(string s, string empty) => string.IsNullOrWhiteSpace(s) ? empty : s.Trim();
            var steps = new List<GuidedStep>
            {
                new GuidedStep
                {
                    Title = "Name and instructions", Help = "The name appears as a chip above the chat. The instructions are the system prompt the model sees first.",
                    Build = () =>
                    {
                        var sp = new StackPanel();
                        sp.Children.Add(Labeled("Name", name, "Profile name"));
                        sp.Children.Add(Labeled("Instructions (system prompt)", prompt, "System prompt"));
                        return sp;
                    },
                    Validate = () => string.IsNullOrWhiteSpace(name.Text) ? "Give the profile a name first." : name.Text.Trim().Length > 40 ? "Profile names are limited to 40 characters." : null
                },
                new GuidedStep
                {
                    Title = "Parameters (optional)", Help = "Temperature, top-p and context length. Leave any empty to keep the model's own default.",
                    Build = () =>
                    {
                        var sp = new StackPanel();
                        var row = new WrapPanel();
                        row.Children.Add(Labeled("Temperature", temp, "Temperature")); row.Children.Add(Labeled("Top-p", topP, "Top-p")); row.Children.Add(Labeled("Context (tokens)", ctx, "Context length in tokens"));
                        sp.Children.Add(row); sp.Children.Add(hint); ShowHint();
                        return sp;
                    },
                    Validate = () => { var p = Problem(); return p != null && !p.StartsWith("Give the profile") ? p : null; }
                },
                new GuidedStep
                {
                    Title = "Review and save", Help = "Saving over an existing name replaces that profile. Chats that used it keep their own copy of the prompt.",
                    Build = () => review,
                    Entered = () =>
                    {
                        var replaces = profiles.Any(x => x.Name.Equals(name.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                        review.Text = $"Name: {name.Text.Trim()}\nInstructions: {Fmt(prompt.Text, "none")}\nTemperature: {Fmt(temp.Text, "model default")}\nTop-p: {Fmt(topP.Text, "model default")}\nContext: {Fmt(ctx.Text, "server default")}"
                            + (replaces ? "\nA profile with this name exists and will be replaced." : "");
                    }
                }
            };
            OllamaProfile made = null;
            var form = new GuidedForm("New chat profile", steps, "Save profile", () =>
            {
                var err = OllamaProfiles.TryBuild(name.Text, prompt.Text, temp.Text, topP.Text, ctx.Text, out made) ?? OllamaProfiles.Upsert(profiles, made);
                if (err != null) return err;
                try { OllamaProfiles.Save(profiles); } catch (Exception ex) { return "Could not save the profile: " + ex.Message; }
                return null;
            }, cancel);
            form.Finished += () => saved(made);
            return form;
        }
    }
}
