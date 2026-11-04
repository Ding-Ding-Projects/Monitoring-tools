using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // One action that writes every dataset the app keeps, in all five formats, into a single zip.
        private FrameworkElement RecordsExportEverything()
        {
            var box = new StackPanel { Tag = "export-everything" };
            box.Children.Add(CardTitle("Export everything", "One zip with every dataset below in JSON, CSV, Markdown, HTML and plain text. Lock records list what each lock covers but never a PIN, a password or an authenticator secret."));
            var all = Exporter.Datasets.Where(d => d.Id != "clipboard").Select(d => d.Label).Concat(ExportAll.ExtraDatasets.Select(d => d.Label));
            box.Children.Add(ModesNote("Included: " + string.Join(", ", all) + "."));
            bool clip = false;
            box.Children.Add(Switch("Also include clipboard history", "Off by default. Clipboard history is plain text and may hold private copies, so it is left out unless you choose it.", clip, v => clip = v));

            var bar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Sunken"), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 6), MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
            var status = ModesNote("");
            AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
            bool busy = false;
            var go = Ui.Button("Export everything...", async () =>
            {
                if (busy) return;
                busy = true;
                try
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog { FileName = "monitoring-tools-export.zip", Filter = "Zip archive|*.zip", Title = "Export everything" };
                    if (dlg.ShowDialog(this) != true) { status.Text = "Export cancelled."; return; }
                    var path = dlg.FileName;
                    bar.Value = 0; bar.Visibility = Visibility.Visible;
                    var tables = ExportAll.SnapshotAll(clip);
                    status.Text = $"Writing {tables.Count} datasets in 5 formats...";
                    var res = await Task.Run(() => ExportAll.WriteZip(path, tables, new Progress<double>(v => Dispatcher.BeginInvoke(new Action(() => bar.Value = v * 100)))));
                    status.Text = $"Saved {res.Files} files ({Ui.Bytes(res.Bytes)}) for {res.Datasets} datasets to {path}";
                    status.Foreground = Ui.Res("B.TextMute");
                }
                catch (Exception ex) { status.Text = "The export failed: " + ex.Message; status.Foreground = Ui.Res("B.Amber"); Log.Warn("Export everything failed: " + ex.Message); }
                finally { busy = false; bar.Visibility = Visibility.Collapsed; }
            }, "Btn.Accent", Icons.Archive);
            go.ToolTip = "Choose where to save one zip holding every dataset in every format";
            go.Margin = new Thickness(0, 4, 0, 0);
            box.Children.Add(go);
            box.Children.Add(bar);
            box.Children.Add(status);
            return SearchScope.SetItem(Card(box), "export everything zip all data shortcuts regex library looks schedules locks dns");
        }
    }
}
