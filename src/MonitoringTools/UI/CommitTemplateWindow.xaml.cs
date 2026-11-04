using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class CommitTemplateWindow : Window
    {
        public event Action Saved;
        private readonly bool _continueAfter;

        public CommitTemplateWindow(bool continueAfter = true)
        {
            InitializeComponent();
            _continueAfter = continueAfter;
            if (!continueAfter) SaveBtn.Content = "Save";
            Input.Text = string.IsNullOrWhiteSpace(Settings.Current.CommitTemplate) ? GitPush.SuggestedTemplate : Settings.Current.CommitTemplate;
            foreach (var (token, meaning) in GitPush.Snippets)
            {
                var t = token;
                var b = Ui.Button(token, () => Insert(t), "Btn.Chip");
                b.ToolTip = meaning;
                Snippets.Children.Add(b);
            }
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            ElementMenu.Attach(this);
            Loaded += (s, e) =>
            {
                RootScale.ScaleX = RootScale.ScaleY = 0.94;
                Root.Opacity = 0;
                RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(380)) { EasingFunction = Ui.Spring });
                RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(380)) { EasingFunction = Ui.Spring });
                Ui.Fade(Root, 1, 220);
                int i = 0;
                foreach (FrameworkElement c in Snippets.Children) Ui.Rise(c, 120 + i++ * 30, 8);
                Input.Focus();
                Input.SelectAll();
                Activate();
            };
            OnChanged(null, null);
        }

        private void Insert(string token)
        {
            var at = Input.CaretIndex;
            Input.Text = Input.Text.Insert(at, token);
            Input.CaretIndex = at + token.Length;
            Input.Focus();
        }

        private void OnChanged(object s, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Preview == null) return;
            Preview.Text = GitPush.Expand(Input.Text, @"C:\Code\my-repo", "main", 3);
            SaveBtn.IsEnabled = !string.IsNullOrWhiteSpace(Input.Text);
        }

        private void OnSave(object s, RoutedEventArgs e)
        {
            Settings.Current.CommitTemplate = Input.Text.Trim();
            Settings.Save();
            ScriptHistory.Save(ScriptHistory.CommitTemplateKey, Settings.Current.CommitTemplate);
            Close();
            if (_continueAfter) Saved?.Invoke();
        }

        private void OnHistory(object s, RoutedEventArgs e)
        {
            if (HistoryHost.Visibility == Visibility.Visible) { HistoryHost.Visibility = Visibility.Collapsed; return; }
            if (HistoryHost.Children.Count == 0)
            {
                // Restoring puts the version in the box and saves it, without sending a :push on its own.
                HistoryHost.Children.Add(HistoryPanel.Build(ScriptHistory.CommitTemplateKey, () => Input.Text.Trim(), t =>
                {
                    Input.Text = t;
                    Settings.Current.CommitTemplate = t.Trim();
                    Settings.Save();
                    ScriptHistory.Save(ScriptHistory.CommitTemplateKey, Settings.Current.CommitTemplate);
                }, "message template"));
            }
            HistoryHost.Visibility = Visibility.Visible;
        }

        private void OnCancel(object s, RoutedEventArgs e) => Close();
        private void OnDrag(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed && !(e.OriginalSource is System.Windows.Controls.TextBox)) try { DragMove(); } catch { } }
    }
}
