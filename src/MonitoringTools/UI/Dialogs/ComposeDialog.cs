using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>Live command progress and explicit, scoped recovery. Closing never claims rollback.</summary>
    public sealed class ComposeDialog : ToolDialogWindow
    {
        private readonly ComposeController _controller;
        private readonly TextBlock _stage = Text(), _result = Text(), _services = Text(), _preview = Text(), _diagnosis = Text(), _context = Text();
        private readonly TextBox _output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 180, Style = Ui.Style("Input.Multi"),
            FontFamily = Ui.Font("F.Mono"), FontSize = 12, Background = Ui.Res("B.Sunken"), Foreground = Ui.Res("B.Text"), Padding = new Thickness(10, 8, 10, 8) };
        private readonly Dropdown _service = new Dropdown("Service for repair", Array.Empty<Choice>(), null) { MinWidth = 180, Margin = new Thickness(0, 0, 8, 8) };
        private readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        private readonly Stopwatch _elapsed = new Stopwatch();
        private CancellationTokenSource _cancellation;
        private readonly WrapPanel _actions = new WrapPanel();
        private readonly Button _confirm;
        private string _planned = "startup", _selected;
        private bool _busy;
        private int _queued;
        private static TextBlock Text() => new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 4, 0, 4) };
        public ComposeDialog(string file, IComposeProcess process = null) : base("Compose startup")
        {
            _controller = new ComposeController(file, process);
            _controller.Output += line => { if (Interlocked.Increment(ref _queued) <= 500) _pending.Enqueue(line); else Interlocked.Decrement(ref _queued); };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = _controller.FilePath + "\n" + _controller.DirectoryPath, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextSoft") });
            panel.Children.Add(_context); panel.Children.Add(_stage); panel.Children.Add(_result); panel.Children.Add(_services);
            AutomationProperties.SetLiveSetting(_result, AutomationLiveSetting.Polite);
            panel.Children.Add(_output);
            _actions.Children.Add(Ui.Button("Retry startup", () => Plan("startup")));
            _actions.Children.Add(Ui.Button("Recheck engine", () => Plan("engine")));
            _actions.Children.Add(Ui.Button("Retry build", () => Plan("build")));
            _actions.Children.Add(Ui.Button("Retry pull", () => Plan("pull")));
            _actions.Children.Add(Ui.Button("Recheck health", () => Plan("recheck")));
            _actions.Children.Add(_service);
            AutomationProperties.SetName(_service, "Service for repair");
            _actions.Children.Add(Ui.Button("Inspect logs", () => Plan("logs")));
            _actions.Children.Add(Ui.Button("Restart selected", () => Plan("restart")));
            panel.Children.Add(_actions); panel.Children.Add(_preview);
            panel.Children.Add(_diagnosis);
            var footer = new WrapPanel();
            _confirm = Ui.Button("Run previewed action", async () => await Run());
            footer.Children.Add(_confirm);
            footer.Children.Add(Ui.Button("Cancel operation", () => _cancellation?.Cancel()));
            footer.Children.Add(Ui.Button("Close", OnEscape));
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 650 };
            var body = new DockPanel { LastChildFill = true };
            footer.Margin = new Thickness(0, 12, 0, 0);
            DockPanel.SetDock(footer, Dock.Bottom);
            body.Children.Add(footer);
            body.Children.Add(scroll);
            ShowView("form", body, 840, Title, "Preview, run, then inspect service readiness", Icons.Box);
            Flex(scroll, 180);
            _timer.Tick += (s, e) => Refresh(); _timer.Start();
            Core.Language.Changed += OnLanguageChanged;
            Closed += (s, e) => { _cancellation?.Cancel(); _timer.Stop(); Core.Language.Changed -= OnLanguageChanged; };
            Plan("startup");
        }
        private void OnLanguageChanged()
        {
            // Regenerate dynamic English originals, then let the shared layer translate once.
            Refresh();
            if (!_busy) Plan(_planned);
        }
        private void Plan(string operation)
        {
            if (_busy) return;
            _planned = operation; _selected = _service.SelectedId;
            try
            {
                _preview.Text = operation == "startup"
                    ? "Will check CLI, Compose, engine and configuration, then run:" + "\n" + string.Join("\n", new[] { "build", "pull", "start", "inspect" }.Select(op => _controller.Preview(op)))
                    : _controller.Preview(operation == "recheck" ? "inspect" : operation, operation is "recheck" or "engine" or "build" or "pull" ? null : _selected);
                _preview.Text += "\n" + "Health observation: 120 seconds. No configuration edits, volume deletion or rollback. Published ports are not verified URLs.";
                _confirm.IsEnabled = true;
            }
            catch (ArgumentException) { _preview.Text = "Select a discovered service first."; _confirm.IsEnabled = false; }
            Core.Language.Refresh(this);
        }
        internal string SelectedServiceForTest => _service.SelectedId;
        internal bool FooterOutsideScrollForTest => _confirm.Parent is WrapPanel footer && footer.Parent is DockPanel;
        internal string Outcome => _controller.Result;
        internal Task RunForTest() => Run();
        internal void CancelForTest() => _cancellation?.Cancel();
        private async Task Run()
        {
            if (_busy) return;
            _busy = true; _actions.IsEnabled = false; _confirm.IsEnabled = false;
            _cancellation?.Dispose(); _cancellation = new CancellationTokenSource(); _elapsed.Restart();
            try { await _controller.ExecuteAsync(_planned, _selected, _cancellation.Token); }
            finally { _elapsed.Stop(); _busy = false; _actions.IsEnabled = true; _confirm.IsEnabled = true; Refresh(); }
        }
        private void Refresh()
        {
            _context.Text = "Docker context" + ": " + _controller.DockerContext;
            _diagnosis.Text = "Observed evidence" + ": " + _controller.FailureEvidence + "\n" + _controller.PossibleCause;
            _stage.Text = "Stage" + ": " + _controller.Stage + "   " + "Elapsed" + ": " + _elapsed.Elapsed.ToString(@"hh\:mm\:ss");
            _result.Text = _controller.Result;
            View = _busy ? "progress" : "done";
            _services.Text = string.Join("\n", _controller.Services.Select(s => $"{s.Service} [{s.Name}]: {s.Verdict}; exit={s.ExitCode?.ToString() ?? "unknown"}; {s.Ports}"));
            if (!_controller.ExpectedServices.SequenceEqual(_service.Choices.Select(c => c.Id)))
            { _service.SetChoices(_controller.ExpectedServices.Select(name => new Choice(name, name)), _service.SelectedId); }
            int count = 0;
            while (count++ < 100 && _pending.TryDequeue(out var line)) { Interlocked.Decrement(ref _queued); _output.AppendText(line + "\n"); }
            if (_output.Text.Length > 48000) _output.Text = "[older output discarded]\n" + _output.Text.Substring(_output.Text.Length - 40000);
            Core.Language.Refresh(this);
        }
        protected override void OnPrimary() { if (_confirm.IsEnabled) _ = Run(); }
        protected override void OnEscape()
        {
            if (_busy)
            {
                _cancellation.Cancel();
                _result.Text = "Stopping owned command and observation. Containers may remain running; close again after it stops.";
                return;
            }
            Close();
        }
    }
}
