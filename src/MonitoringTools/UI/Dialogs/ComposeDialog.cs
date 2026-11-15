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
        private readonly TextBlock _stage = Text(), _result = Text(), _services = Text(), _preview = Text();
        private readonly TextBox _output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 210 };
        private readonly ComboBox _service = new ComboBox { MinWidth = 160, Margin = new Thickness(4) };
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
        private static string T(string en, string yue) => Language.Mode == LanguageMode.English ? en : Language.Mode == LanguageMode.Cantonese ? yue : en + " / " + yue;
        public ComposeDialog(string file, IComposeProcess process = null) : base(T("Compose startup", "Compose 啟動"))
        {
            _controller = new ComposeController(file, process);
            _controller.Output += line => { if (Interlocked.Increment(ref _queued) <= 500) _pending.Enqueue(line); else Interlocked.Decrement(ref _queued); };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = _controller.FilePath + "\n" + _controller.DirectoryPath, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextSoft") });
            panel.Children.Add(_stage); panel.Children.Add(_result); panel.Children.Add(_services);
            AutomationProperties.SetLiveSetting(_result, AutomationLiveSetting.Polite);
            panel.Children.Add(_output);
            _actions.Children.Add(Ui.Button(T("Retry startup", "重試啟動"), () => Plan("startup")));
            _actions.Children.Add(Ui.Button(T("Recheck health", "重新檢查狀態"), () => Plan("recheck")));
            _actions.Children.Add(_service);
            AutomationProperties.SetName(_service, T("Service for repair", "要處理嘅服務"));
            _actions.Children.Add(Ui.Button(T("Inspect logs", "查看記錄"), () => Plan("logs")));
            _actions.Children.Add(Ui.Button(T("Restart selected", "重新啟動所選服務"), () => Plan("restart")));
            panel.Children.Add(_actions); panel.Children.Add(_preview);
            panel.Children.Add(new TextBlock { Text = T("Evidence: command exit and service state appear above. Possible causes, not diagnoses: engine unavailable, invalid configuration, image access, port conflict or failed health check. Inspect logs before repair.", "證據：上方列出指令結束碼及服務狀態。可能原因並非診斷：引擎未啟動、設定無效、映像存取、連接埠衝突或健康檢查失敗。處理前請查看記錄。"), TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextSoft") });
            var footer = new WrapPanel();
            _confirm = Ui.Button(T("Run previewed action", "執行預覽操作"), async () => await Run());
            footer.Children.Add(_confirm);
            footer.Children.Add(Ui.Button(T("Cancel operation", "取消操作"), () => _cancellation?.Cancel()));
            footer.Children.Add(Ui.Button(T("Close", "關閉"), OnEscape));
            panel.Children.Add(footer);
            var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 650 };
            ShowView("form", scroll, 840, Title, T("Preview, run, then inspect service readiness", "先預覽，再執行及檢查服務狀態"), Icons.Box);
            Flex(scroll, 180);
            _timer.Tick += (s, e) => Refresh(); _timer.Start();
            Closed += (s, e) => { _cancellation?.Cancel(); _timer.Stop(); };
            Plan("startup");
        }
        private void Plan(string operation)
        {
            if (_busy) return;
            _planned = operation; _selected = _service.SelectedItem as string;
            try
            {
                _preview.Text = operation == "startup"
                    ? T("Will check CLI, Compose, engine and configuration, then run:", "會檢查 CLI、Compose、引擎及設定，然後執行：") + "\n" + string.Join("\n", new[] { "build", "pull", "start", "inspect" }.Select(op => _controller.Preview(op)))
                    : _controller.Preview(operation == "recheck" ? "inspect" : operation, operation == "recheck" ? null : _selected);
                _preview.Text += "\n" + T("Health observation: 120 seconds. No configuration edits, volume deletion or rollback. Published ports are not verified URLs.", "狀態觀察：120 秒。不會修改設定、刪除磁碟區或回復原狀。已發佈連接埠並非已驗證網址。");
                _confirm.IsEnabled = true;
            }
            catch (ArgumentException) { _preview.Text = T("Select a discovered service first.", "請先選擇已發現嘅服務。"); _confirm.IsEnabled = false; }
        }
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
            _stage.Text = T("Stage", "階段") + ": " + Language.T(_controller.Stage) + "   " + T("Elapsed", "已用時間") + ": " + _elapsed.Elapsed.ToString(@"hh\:mm\:ss");
            _result.Text = Language.T(_controller.Result);
            View = _busy ? "progress" : "done";
            _services.Text = string.Join("\n", _controller.Services.Select(s => $"{s.Service} [{s.Name}]: {Language.T(s.Verdict)}; exit={s.ExitCode?.ToString() ?? "unknown"}; {s.Ports}"));
            if (!_controller.ExpectedServices.SequenceEqual(_service.Items.Cast<string>()))
            { var selected = _service.SelectedItem; _service.ItemsSource = _controller.ExpectedServices; _service.SelectedItem = selected; }
            int count = 0;
            while (count++ < 100 && _pending.TryDequeue(out var line)) { Interlocked.Decrement(ref _queued); _output.AppendText(line + "\n"); }
            if (_output.Text.Length > 48000) _output.Text = "[older output discarded]\n" + _output.Text.Substring(_output.Text.Length - 40000);
        }
        protected override void OnPrimary() { if (_confirm.IsEnabled) _ = Run(); }
        protected override void OnEscape()
        {
            if (_busy)
            {
                _cancellation.Cancel();
                _result.Text = T("Stopping owned command and observation. Containers may remain running; close again after it stops.", "正在停止本次指令及觀察。容器可能仍然運作；停止後可再關閉。");
                return;
            }
            Close();
        }
    }
}
