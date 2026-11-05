using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The full outage list with select mode: copy, export, or remove finished outages from the in-memory log.
        private FrameworkElement OutagesBulkCard()
        {
            var sp = new StackPanel();
            var list = Activity.RecentOutages(50);
            sp.Children.Add(CardTitle("Outages", list.Count == 0 ? "None since the service started." : "Kept in memory while the service runs. An outage that is still going cannot be removed."));
            var bulk = _outageBulk;
            if (list.Count > 0)
            {
                bulk.Begin();
                bulk.Prune(list);
                string Noun(int n) => n == 1 ? "outage" : "outages";
                sp.Children.Add(bulk.Bar(new[]
                {
                    new BulkAction<Outage> { Label = "Copy", Hint = "Copy the chosen outages to the clipboard, one per line.",
                        Run = items => BulkSelect<Outage>.CopyText(string.Join(Environment.NewLine, items.Select(ListActions.OutageLine)), items.Count, Noun(items.Count)) },
                    new BulkAction<Outage> { Label = "Export as text...", Hint = "Save the chosen outages to a text file.",
                        Run = items => BulkSelect<Outage>.SaveText("outages.txt", "Text|*.txt", string.Join(Environment.NewLine, items.Select(ListActions.OutageLine)), items.Count, Noun(items.Count)) },
                    new BulkAction<Outage> { Label = "Remove", Danger = true, Hint = "Remove the chosen finished outages from this list, after you confirm.",
                        Run = items =>
                        {
                            var chosen = items.ToList();
                            bulk.ConfirmThen($"Remove {chosen.Count} {Noun(chosen.Count)}?", "They leave the outage list for good, because it is only kept in memory. An outage still in progress is kept.", "Remove", () => Activity.RemoveOutages(chosen));
                        } },
                }));
            }
            foreach (var o in list)
            {
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                row.Children.Add(new TextBlock { Text = o.Cause ?? "Connection lost", Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis });
                row.Children.Add(Ui.Mono(ListActions.OutageLine(o), Ui.Res("B.TextMute"), 11));
                sp.Children.Add(bulk.Wrap(o, SearchScope.SetItem(row, (o.Cause ?? "outage") + " outage"), o.Cause ?? "outage"));
            }
            return Card(sp);
        }
    }
}
