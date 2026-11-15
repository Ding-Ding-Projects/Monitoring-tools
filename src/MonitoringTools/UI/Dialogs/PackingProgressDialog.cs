using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Core.Packing;
namespace MonitoringTools.UI.Dialogs
{
    /// <summary>A packing job starts when opened; closing the view never cancels its work.</summary>
    public sealed class PackingProgressDialog : ToolDialogWindow
    {
        private static readonly Dictionary<PackingJob, PackingProgressDialog> Windows = new();
        private readonly PackingJob _job;
        private readonly TextBlock _stage = new(), _counts = new(), _rate = new(), _current = new(), _result = new(), _destinationNote = new();
        private readonly ProgressBar _bar = new() { Height = 10, Margin = new Thickness(0, 10, 0, 12), Minimum = 0, Maximum = 100 };
        private readonly WrapPanel _actions = new();
        private bool? _renderedComplete;
        public static PackingJob Start(string[] items, PackingFormat format)
        {
            var job = new PackingJob(items, format);
            Open(job);
            Activity.Add(ActivityKind.Files, format == PackingFormat.Zip ? "ZIP packing started" : "VHDX packing started", "Open packing progress", reopen: () => Open(job));
            job.Start();
            return job;
        }
        public static void Open(PackingJob job)
        {
            if (!Windows.TryGetValue(job, out var window)) { window = new PackingProgressDialog(job); Windows.Add(job, window); }
            window.Show(); window.Activate();
        }
        internal static PackingProgressDialog For(PackingJob job) => Windows.TryGetValue(job, out var window) ? window : null;
        private PackingProgressDialog(PackingJob job) : base(job.Format == PackingFormat.Zip ? "Pack to ZIP" : "Pack to VHDX")
        {
            _job = job;
            var body = new StackPanel();
            foreach (var text in new[] { _stage, _counts, _rate, _current, _result, _destinationNote })
            {
                text.TextWrapping = TextWrapping.Wrap;
                text.Foreground = Ui.Res("B.Text"); text.FontSize = 13;
                text.Margin = new Thickness(0, 4, 0, 4);
            }
            _stage.FontSize = 18;
            Core.Language.SetLiteral(_current, true);
            Core.Language.SetLiteral(_result, true);
            AutomationProperties.SetLiveSetting(_stage, AutomationLiveSetting.Polite);
            body.Children.Add(_stage); body.Children.Add(_bar); body.Children.Add(_counts); body.Children.Add(_rate); body.Children.Add(_current); body.Children.Add(_destinationNote); body.Children.Add(_result); body.Children.Add(_actions);
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(180, Ui.WorkArea.Height - 200) };
            ShowView("progress", scroll, 520, Title, "Original files are kept. Closing this window keeps packing in the background.", Icons.Archive);
            job.Changed += Refresh;
            Core.Language.Changed += RefreshLanguage;
            Closing += (s, e) => { if (!job.Complete) { e.Cancel = true; Hide(); } };
            Closed += (s, e) => { job.Changed -= Refresh; Core.Language.Changed -= RefreshLanguage; Windows.Remove(job); };
            Refresh();
        }
        internal string StageText => _stage.Text;
        internal string DestinationNote => _destinationNote.Text;
        internal string ResultText => _result.Text;
        private void RefreshLanguage() { _renderedComplete = null; Refresh(); }
        protected override void OnEscape() { if (_job.Complete) Close(); else Hide(); }
        private void Refresh()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(Refresh)); return; }
            TitleText.Text = Title = Core.Language.T(_job.Format == PackingFormat.Zip ? "Pack to ZIP" : "Pack to VHDX");
            _stage.Text = Core.Language.T(_job.CancellationRequested && !_job.Complete ? "Cancelling safely…" : _job.Stage);
            var p = _job.Progress;
            _bar.IsIndeterminate = !_job.Complete && (p == null || p.Total <= 0 || _job.Stage is "Growing disk" or "Detaching" or "Creating and mounting" or "Waiting for Windows permission");
            _bar.Value = p == null || p.Total == 0 ? 0 : Math.Clamp(100.0 * p.Done / p.Total, 0, 100);
            _counts.Text = p == null ? Core.Language.T("Counting files and folders…") : $"{Ui.Bytes(p.Done)} / {Ui.Bytes(p.Total)} · {Core.Language.T("Files")}: {(p.FilesDone < 0 ? Core.Language.T("in progress") : p.FilesDone.ToString("N0"))} / {p.FilesTotal:N0}";
            _rate.Text = p == null ? "" : $"{Ui.Bytes((long)p.BytesPerSecond)}/s · {Core.Language.T("Time left")}: {(p.Left is TimeSpan left ? left.ToString(@"hh\:mm\:ss") : Core.Language.T("working it out"))}";
            _current.Text = p?.Current ?? "";
            var details = _job.Error ?? "";
            _destinationNote.Text = _job.Sources?.MixedFolders == true ? Core.Language.T("Mixed folders: output is saved beside the first copied item.") : "";
            if (_job.Output != null) details += "\n" + _job.Output;
            if (_job.RetainedPath != null) details += "\n" + _job.RetainedPath;
            foreach (var warning in _job.Warnings) details += "\n" + warning;
            _result.Text = details.Trim();
            if (_renderedComplete == _job.Complete) return;
            _renderedComplete = _job.Complete;
            _actions.Children.Clear();
            if (!_job.Complete)
            {
                _actions.Children.Add(Ui.Button("Cancel packing", () => { _job.Cancel(); _stage.Text = Core.Language.T("Cancelling safely…"); }, "Btn.Ghost"));
                _actions.Children.Add(Ui.Button("Hide and continue", Hide, "Btn.Ghost"));
            }
            else
            {
                if (_job.Output != null)
                {
                    _actions.Children.Add(Ui.Button("Show in folder", () => ActionProvider.Reveal(_job.Output), "Btn.Accent", Icons.Folder));
                    _actions.Children.Add(Ui.Button("Copy output path", () => Clipboard.SetText(_job.Output), "Btn.Ghost"));
                    if (_job.Format == PackingFormat.Vhdx) _actions.Children.Add(Ui.Button("Open in the disk library", () => App.Current.OpenVirtualDisks(disks: new[] { _job.Output }), "Btn.Ghost"));
                }
                _actions.Children.Add(Ui.Button("Close", Close, "Btn.Ghost"));
            }
            Core.Language.Refresh(_actions);
        }
    }
}
