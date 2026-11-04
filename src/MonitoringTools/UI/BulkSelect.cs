using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>One action offered while selecting: runs on the chosen items, in list order.</summary>
    public sealed class BulkAction<T>
    {
        public string Label { get; init; }
        public Action<IReadOnlyList<T>> Run { get; init; }
        public bool Danger { get; init; }
        public string Hint { get; init; }
    }

    /// <summary>
    /// One reusable select mode for a list: a Select toggle, a checkbox on every row, a live "n selected" count,
    /// "Select all shown" (only the rows the search currently shows), "Select none", and caller-supplied actions.
    /// The page builds rows through <see cref="Wrap"/>, so the same behaviour backs every list that offers bulk actions.
    /// </summary>
    public sealed class BulkSelect<T>
    {
        private sealed class Entry
        {
            public T Item;
            public Func<bool> Shown;
            public CheckBox Box;
            public Border Row;
        }

        private readonly Action _refresh;
        private readonly string _noun;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly HashSet<T> _selected;
        private bool _bulk;
        private TextBlock _count;
        private readonly List<Button> _needsSelection = new List<Button>();

        /// <param name="refresh">Rebuilds the page. Called when select mode is switched on or off and after an action changes the list.</param>
        /// <param name="noun">Plural noun used in the count, for example "entries".</param>
        public BulkSelect(Action refresh, string noun, IEqualityComparer<T> comparer = null)
        {
            _refresh = refresh; _noun = noun;
            _selected = comparer == null ? new HashSet<T>() : new HashSet<T>(comparer);
        }

        public bool Selecting { get; private set; }
        public int Count => _selected.Count;

        /// <summary>Selected items in the order they were registered, which is the order of the list on screen.</summary>
        public IReadOnlyList<T> Chosen => _entries.Where(e => _selected.Contains(e.Item)).Select(e => e.Item).ToList();

        /// <summary>Call at the start of building a list so rows from the previous build are forgotten.</summary>
        public void Begin() => _entries.Clear();

        /// <summary>Drops selections whose item no longer exists, so a removed item cannot stay selected.</summary>
        public void Prune(IEnumerable<T> alive)
        {
            var keep = new HashSet<T>(alive, _selected.Comparer);
            _selected.IntersectWith(keep);
        }

        /// <summary>Registers an item with a visibility test. A hidden item (filtered out by search) is skipped by <see cref="SelectAllShown"/>.</summary>
        public void Track(T item, Func<bool> shown = null) => _entries.Add(new Entry { Item = item, Shown = shown ?? (() => true) });

        /// <summary>Selects every tracked item that is currently shown. Hidden (filtered) items are left alone.</summary>
        public int SelectAllShown()
        {
            _bulk = true;
            foreach (var e in _entries.Where(x => x.Shown()))
            {
                _selected.Add(e.Item);
                if (e.Box != null) e.Box.IsChecked = true;
                Paint(e);
            }
            _bulk = false;
            UpdateBar();
            return _selected.Count;
        }

        public void SelectNone()
        {
            _bulk = true;
            _selected.Clear();
            foreach (var e in _entries) { if (e.Box != null) e.Box.IsChecked = false; Paint(e); }
            _bulk = false;
            UpdateBar();
        }

        public void Set(T item, bool on)
        {
            if (on) _selected.Add(item); else _selected.Remove(item);
        }

        /// <summary>
        /// Wraps one row's content. Outside select mode the content is returned untouched. In select mode the row gets a leading
        /// checkbox, a tonal background while chosen, and is registered so "Select all shown" respects the search filter.
        /// </summary>
        public FrameworkElement Wrap(T item, FrameworkElement content, string name)
        {
            if (!Selecting) return content;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var cb = new CheckBox { IsChecked = _selected.Contains(item), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
            AutomationProperties.SetName(cb, "Select " + name);
            grid.Children.Add(cb);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
            var row = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(4, 2, 4, 2), Child = grid, Background = Brushes.Transparent };
            SearchScope.SetItem(row, SearchScope.GetItem(content) ?? "");
            var entry = new Entry { Item = item, Box = cb, Row = row, Shown = () => row.IsLoaded ? row.IsVisible : row.Visibility == Visibility.Visible };
            _entries.Add(entry);
            cb.Checked += (s, e) => { _selected.Add(item); Paint(entry); if (!_bulk) UpdateBar(); };
            cb.Unchecked += (s, e) => { _selected.Remove(item); Paint(entry); if (!_bulk) UpdateBar(); };
            Paint(entry);
            return row;
        }

        private void Paint(Entry e)
        {
            if (e.Row == null) return;
            e.Row.Background = _selected.Contains(e.Item) ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
        }

        private void UpdateBar()
        {
            if (_count != null) _count.Text = $"{_selected.Count} selected";
            foreach (var b in _needsSelection) b.IsEnabled = _selected.Count > 0;
        }

        /// <summary>
        /// The bar: Select / Done selecting, then (in select mode) the live count, Select all shown, Select none and the actions.
        /// Actions receive the chosen items in list order and are disabled until at least one is chosen.
        /// </summary>
        public FrameworkElement Bar(IEnumerable<BulkAction<T>> actions)
        {
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            _needsSelection.Clear();
            var toggle = Ui.Button(Selecting ? "Done selecting" : "Select", () =>
            {
                Selecting = !Selecting;
                if (!Selecting) _selected.Clear();
                _refresh();
            }, Selecting ? "Btn.Accent" : "Btn.Ghost");
            toggle.ToolTip = Selecting ? "Leave select mode and clear the selection." : $"Choose several {_noun} to copy, export or remove together. Off by default.";
            bar.Children.Add(toggle);
            if (!Selecting) return bar;

            _count = new TextBlock { Text = $"{_selected.Count} selected", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
            AutomationProperties.SetLiveSetting(_count, AutomationLiveSetting.Polite);
            bar.Children.Add(_count);
            var all = Ui.Button("Select all shown", () => SelectAllShown());
            all.ToolTip = $"Select every {_noun} the search currently shows. Rows the search hides stay unselected.";
            bar.Children.Add(all);
            var none = Ui.Button("Select none", SelectNone);
            bar.Children.Add(none);
            foreach (var a in actions)
            {
                var act = a;
                var b = Ui.Button(act.Label, () => act.Run(Chosen), act.Danger ? "Btn.Danger" : "Btn.Ghost");
                b.IsEnabled = _selected.Count > 0;
                if (act.Hint != null) b.ToolTip = act.Hint;
                _needsSelection.Add(b);
                bar.Children.Add(b);
            }
            return bar;
        }

        /// <summary>Raises the two-key confirmation for a destructive action on the chosen items, then runs it and rebuilds the page.</summary>
        public void ConfirmThen(string title, string body, string okLabel, Action run)
        {
            var dlg = new ConfirmWindow(title, body, okLabel, danger: true);
            dlg.Confirmed += _ => { run(); _selected.Clear(); _refresh(); };
            dlg.Show();
        }

        /// <summary>Convenience used by every list: "n entry" / "n entries".</summary>
        public static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

        // ---- shared export helpers so every list saves the same way and tells the person what happened

        public static void CopyText(string text, int count, string noun)
        {
            Clip.ClipboardWatcher.SetText(text);
            Notify("Copied", $"{count} {noun} copied to the clipboard.");
        }

        public static bool SaveText(string fileName, string filter, string content, int count, string noun)
        {
            var d = new Microsoft.Win32.SaveFileDialog { FileName = fileName, Filter = filter };
            if (d.ShowDialog() != true) return false;
            try { File.WriteAllText(d.FileName, content); }
            catch (Exception ex) { Notify("Export failed", ex.Message); return false; }
            Notify("Exported", $"{count} {noun} saved to {Path.GetFileName(d.FileName)}.");
            return true;
        }

        private static void Notify(string title, string body)
        {
            try { App.Toasts?.ShowNotice(title, body, App.Toasts.Mint, 3.5, icon: Icons.Check); } catch { }
        }
    }
}
