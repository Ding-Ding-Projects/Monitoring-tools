using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// The shell the archive and virtual disk dialogs share: a rounded card with the clipboard toast's icon chip, a title and a
    /// line under it, and a close button. The body is swapped between the form, the progress, the result and the error views,
    /// and the window changes width to suit (840 px for the form, 456 px for the others). Esc closes, Ctrl+Enter is the primary action.
    /// </summary>
    public abstract class ToolDialogWindow : Window
    {
        protected readonly ContentControl Body = new ContentControl { IsTabStop = false };
        protected readonly TextBlock TitleText = new TextBlock { FontFamily = Ui.Font("F.Display"), FontSize = 22, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis };
        protected readonly TextBlock SubText = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 13, Foreground = Ui.Res("B.TextSoft"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 0) };
        private readonly ContentControl _icon = new ContentControl { Focusable = false, VerticalAlignment = VerticalAlignment.Center };
        private readonly Border _card;
        private double _cardWidth = 840;

        /// <summary>The kind of view on show: form, progress, done or error. Checks read it.</summary>
        public string View { get; protected set; } = "form";

        protected ToolDialogWindow(string windowTitle)
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.Height;
            MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height);
            WindowStartupLocation = WindowStartupLocation.CenterScreen; ShowInTaskbar = true;
            Title = windowTitle;
            AutomationProperties.SetName(this, windowTitle);
            SourceInitialized += (s, e) => Core.Native.ApplyDarkChrome(this);
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is Border) try { DragMove(); } catch { } };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; OnEscape(); }
                else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { e.Handled = true; OnPrimary(); }
            };

            var close = Ui.Button("", () => OnEscape(), "Btn.Icon", Icons.Close);
            close.Margin = new Thickness(0);
            AutomationProperties.SetName(close, "Close");
            close.ToolTip = "Closes this window (Esc).";
            var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _icon.Margin = new Thickness(0, 0, 14, 0);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(TitleText); titles.Children.Add(SubText);
            Grid.SetColumn(titles, 1); Grid.SetColumn(close, 2);
            head.Children.Add(_icon); head.Children.Add(titles); head.Children.Add(close);

            var stack = new DockPanel();
            DockPanel.SetDock(head, Dock.Top);
            stack.Children.Add(head);
            stack.Children.Add(Body);
            _card = new Border
            {
                Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(28), Padding = new Thickness(24, 20, 24, 18), Child = stack,
                BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1),
                Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"]
            };
            Content = new Grid { Margin = new Thickness(16), Children = { _card } };
            Width = _cardWidth + 32;
        }

        protected virtual void OnEscape() => Close();
        protected virtual void OnPrimary() { }

        /// <summary>Shows a view in the shell. The window keeps its centre when its width changes.</summary>
        protected void ShowView(string view, FrameworkElement content, double cardWidth, string title, string sub, string icon, Brush ink = null, Brush wash = null)
        {
            View = view;
            TitleText.Text = title; SubText.Text = sub;
            _icon.Content = Ui.IconChip(icon, ink ?? Ui.Res("B.Mint"), wash ?? Ui.Res("B.MintWash"), 44, 22);
            Body.Content = content;
            if (Math.Abs(cardWidth - _cardWidth) > 0.5)
            {
                if (IsLoaded) Left += (_cardWidth - cardWidth) / 2;
                _cardWidth = cardWidth; Width = cardWidth + 32;
            }
            AutomationProperties.SetLiveSetting(SubText, AutomationLiveSetting.Polite);
        }

        /// <summary>The card's own size without the shadow margin, for captures.</summary>
        internal double CardWidth => _cardWidth;
    }
}
