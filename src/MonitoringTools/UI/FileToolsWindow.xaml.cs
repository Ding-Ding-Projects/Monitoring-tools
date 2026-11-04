using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>The File tools sheet: move and copy, Base64, and password encryption, one tab at a time.</summary>
    public partial class FileToolsWindow : Window
    {
        private readonly string[] _files;
        private readonly Button[] _tabButtons = new Button[3];
        private int _tab = -1;
        private bool _busy;

        // -------------------------------------------------------------- entry points used by toasts

        public static void Open(string[] files, string tab = null)
        {
            var w = new FileToolsWindow(files);
            w.Show();
            w.Select(tab == "base64" ? 1 : tab == "encrypt" || files.All(FileOps.IsEncrypted) ? 2 : 0, animate: false);
        }

        public static async void MoveOrCopy(string[] files, bool move)
        {
            var folder = PickFolder(move ? "Move to" : "Copy to");
            if (folder == null) return;
            await RunMoveCopy(files, folder, move, null);
        }

        public static void ConfirmRecycle(string[] files)
        {
            var dlg = new ConfirmWindow($"Delete {Count(files)}?",
                "They go to the Recycle Bin, so you can restore them from there.", "Delete", danger: true);
            dlg.Confirmed += async _ =>
            {
                var r = await FileOps.RecycleAsync(files);
                Report("Deleted", r, null);
            };
            dlg.Show();
        }

        private static string Count(string[] files) => files.Length == 1 ? Path.GetFileName(files[0].TrimEnd('\\')) : $"{files.Length} items";

        private static string PickFolder(string title)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = title, Multiselect = false };
            var recent = Settings.Current.RecentFolders.FirstOrDefault(Directory.Exists);
            if (recent != null) dlg.InitialDirectory = recent;
            return dlg.ShowDialog() == true ? dlg.FolderName : null;
        }

        private static void Remember(string folder)
        {
            var list = Settings.Current.RecentFolders;
            list.RemoveAll(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, folder);
            while (list.Count > 5) list.RemoveAt(list.Count - 1);
            Settings.Save();
        }

        private static async Task RunMoveCopy(string[] files, string folder, bool move, TextBlock status)
        {
            Remember(folder);
            if (status != null) status.Text = (move ? "Moving" : "Copying") + "…";
            var r = await FileOps.MoveOrCopyAsync(files, folder, move);
            Report(move ? "Moved" : "Copied", r, status, folder);
        }

        /// <summary>One summary line in the sheet, plus a toast when the sheet is not open.</summary>
        private static void Report(string verb, List<OpResult> r, TextBlock status, string reveal = null)
        {
            int ok = r.Count(x => x.Ok);
            var fails = r.Where(x => !x.Ok).ToList();
            var line = fails.Count == 0 ? $"✓ {verb} {ok} {(ok == 1 ? "item" : "items")}" : $"{verb} {ok} of {r.Count} · {Path.GetFileName(fails[0].Source)}: {fails[0].Error}";
            Activity.Add(ActivityKind.Files, line.TrimStart('✓', ' '), reveal, fails.Count == 0);
            if (status != null)
            {
                status.Text = line;
                status.Foreground = Ui.Res(fails.Count == 0 ? "B.Mint" : "B.Amber");
                Ui.Rise(status, 0, 4, 240);
                return;
            }
            var actions = new List<ToastAction>();
            var outFile = r.FirstOrDefault(x => x.Ok && x.Output != null)?.Output;
            if (outFile != null) actions.Add(new ToastAction { Label = "Show", Icon = Icons.Folder, Run = () => ActionProvider.Reveal(outFile), Done = "Shown" });
            else if (reveal != null) actions.Add(new ToastAction { Label = "Open folder", Icon = Icons.Folder, Run = () => ActionProvider.Shell(reveal), Done = "Opened" });
            App.Toasts.ShowNotice(line, fails.Count == 0 ? "" : string.Join("\n", fails.Take(3).Select(f => $"{Path.GetFileName(f.Source)}: {f.Error}")),
                fails.Count == 0 ? App.Toasts.Mint : App.Toasts.Amber, fails.Count == 0 ? 4 : 8, actions, Icons.File);
        }

        // -------------------------------------------------------------- the sheet

        public FileToolsWindow(string[] files)
        {
            InitializeComponent();
            _files = files;
            long size = 0;
            foreach (var f in files) try { if (File.Exists(f)) size += new FileInfo(f).Length; } catch { }
            TitleText.Text = files.Length == 1 ? Path.GetFileName(files[0].TrimEnd('\\')) : $"{files.Length} files";
            SubText.Text = $"{Ui.Bytes(size)} · {Path.GetDirectoryName(files[0].TrimEnd('\\'))}";
            IconHost.Content = Ui.IconChip(Icons.File, Ui.Res("B.Mint"), Ui.Res("B.MintWash"), 40, 20);

            var names = new[] { "Move and copy", "Base64", "Encrypt" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = new Button { Style = Ui.Style("Btn.Row"), Content = new TextBlock { Text = names[i], HorizontalAlignment = HorizontalAlignment.Center }, Height = 34, HorizontalContentAlignment = HorizontalAlignment.Center };
                b.Click += (s, e) => Select(idx);
                Grid.SetColumn(b, i);
                Tabs.Children.Add(b);
                _tabButtons[i] = b;
            }

            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            CommandPalette.Attach(this);
            Loaded += (s, e) =>
            {
                RootScale.ScaleX = RootScale.ScaleY = 0.94;
                RootShift.Y = 16;
                Root.Opacity = 0;
                RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(400)) { EasingFunction = Ui.Spring });
                RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(400)) { EasingFunction = Ui.Spring });
                RootShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(400)) { EasingFunction = Ui.Spring });
                Ui.Fade(Root, 1, 220);
                Activate();
            };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        public void Select(int idx, bool animate = true)
        {
            if (idx == _tab) return;
            _tab = idx;
            Pill.Width = double.NaN;
            Grid.SetColumn(Pill, 0);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var w = Tabs.ActualWidth / 3;
                Pill.Width = w;
                Pill.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetColumnSpan(Pill, 3);
                PillShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(idx * w, TimeSpan.FromMilliseconds(animate ? 340 : 0)) { EasingFunction = Ui.Spring });
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            for (int i = 0; i < 3; i++)
            {
                var t = (TextBlock)_tabButtons[i].Content;
                t.Foreground = Ui.Res(i == idx ? "B.Text" : "B.TextMute");
                t.FontWeight = i == idx ? FontWeights.SemiBold : FontWeights.Normal;
            }
            Status.Text = "";
            FrameworkElement page = idx switch { 1 => Base64Page(), 2 => CryptoPage(), _ => MovePage() };
            Panel.Content = page;
            if (animate && page is Panel p) { int i = 0; foreach (FrameworkElement c in p.Children) Ui.Rise(c, i++ * 40, 10, 320); }
        }

        private static Button Big(string label, Action act, string style = "Btn.Ghost")
        {
            var b = Ui.Button(label, act, style);
            b.Height = 44; b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 0, 0, 10);
            return b;
        }

        // -------------------------------------------------------------- move and copy

        private FrameworkElement MovePage()
        {
            var sp = new StackPanel();
            sp.Children.Add(Big("Move to a folder…", async () => { var f = PickFolder("Move to"); if (f != null) { await RunMoveCopy(_files, f, true, Status); } }));
            sp.Children.Add(Big("Copy to a folder…", async () => { var f = PickFolder("Copy to"); if (f != null) await RunMoveCopy(_files, f, false, Status); }));

            var recent = Settings.Current.RecentFolders.Where(Directory.Exists).Take(4).ToList();
            if (recent.Count > 0)
            {
                sp.Children.Add(Ui.Mono("RECENT · click to copy, shift-click to move", Ui.Res("B.TextMute"), 11));
                var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
                foreach (var r in recent)
                {
                    var folder = r;
                    var chip = Ui.Button(Path.GetFileName(folder.TrimEnd('\\')) is var n && n.Length > 0 ? n : folder, null, "Btn.Chip");
                    chip.ToolTip = folder;
                    chip.Click += async (s, e) => await RunMoveCopy(_files, folder, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), Status);
                    wrap.Children.Add(chip);
                }
                sp.Children.Add(wrap);
            }

            var foot = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            foot.Children.Add(new TextBlock { Text = "Deletes go to the Recycle Bin", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), VerticalAlignment = VerticalAlignment.Center });
            var del = Ui.Button(_files.Length == 1 ? "Delete" : $"Delete {_files.Length}", async () =>
            {
                Status.Text = "Sending to the Recycle Bin…";
                var r = await FileOps.RecycleAsync(_files);
                Report("Deleted", r, Status);
            }, "Btn.Danger", Icons.Trash);
            del.HorizontalAlignment = HorizontalAlignment.Right; del.Margin = new Thickness(0);
            foot.Children.Add(del);
            sp.Children.Add(foot);
            return sp;
        }

        // -------------------------------------------------------------- base64

        private FrameworkElement Base64Page()
        {
            var sp = new StackPanel();
            var files = _files.Where(File.Exists).ToArray();
            var b64 = files.Where(f => f.EndsWith(".b64", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".base64", StringComparison.OrdinalIgnoreCase)).ToArray();
            var grid = new UniformGridPanel(2);
            grid.Add(Tile("Encode", "Writes name.ext.b64 next to each file.", files.Length == 0 ? "Encode" : $"Encode {files.Length}", files.Length > 0, async () =>
            {
                Status.Text = "Encoding…";
                Report("Encoded", await FileOps.Base64EncodeFilesAsync(files), Status);
            }, primary: b64.Length == 0));
            grid.Add(Tile("Decode", "Turns .b64 files back into the originals.", $"Decode {b64.Length}", b64.Length > 0, async () =>
            {
                Status.Text = "Decoding…";
                Report("Decoded", await FileOps.Base64DecodeFilesAsync(b64), Status);
            }, primary: b64.Length > 0));
            sp.Children.Add(grid);

            if (files.Length == 1 && new FileInfo(files[0]).Length <= 8 * 1024 * 1024)
            {
                var f = files[0];
                sp.Children.Add(Big("Copy as Base64 text", () =>
                {
                    ClipboardWatcher.SetText(Convert.ToBase64String(File.ReadAllBytes(f)));
                    Status.Text = "✓ Base64 text copied"; Status.Foreground = Ui.Res("B.Mint");
                }));
                if (ClipItem.Classify(f) == FileKind.Image)
                    sp.Children.Add(Big("Copy as data URI (for HTML and CSS)", () =>
                    {
                        var ext = Path.GetExtension(f).TrimStart('.').ToLowerInvariant();
                        var mime = ext == "jpg" ? "jpeg" : ext == "svg" ? "svg+xml" : ext;
                        ClipboardWatcher.SetText($"data:image/{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(f))}");
                        Status.Text = "✓ Data URI copied"; Status.Foreground = Ui.Res("B.Mint");
                    }));
            }
            return sp;
        }

        private static FrameworkElement Tile(string title, string sub, string button, bool enabled, Action act, bool primary)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 15 });
            sp.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 4, 0, 10), MinHeight = 34 });
            var b = Ui.Button(button, act, primary ? "Btn.Accent" : "Btn.Ghost");
            b.IsEnabled = enabled; b.HorizontalAlignment = HorizontalAlignment.Stretch; b.Margin = new Thickness(0);
            sp.Children.Add(b);
            return new Border { Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(14), Margin = new Thickness(0, 0, 10, 12), Child = sp };
        }

        /// <summary>A two-column grid that fills row by row.</summary>
        private sealed class UniformGridPanel : System.Windows.Controls.Primitives.UniformGrid
        {
            public UniformGridPanel(int cols) { Columns = cols; Margin = new Thickness(0, 0, -10, 0); }
            public void Add(UIElement e) => Children.Add(e);
        }

        // -------------------------------------------------------------- encryption

        private FrameworkElement CryptoPage()
        {
            var sp = new StackPanel();
            var files = _files.Where(File.Exists).ToArray();
            var encrypted = files.Where(FileOps.IsEncrypted).ToArray();
            var plain = files.Except(encrypted).ToArray();
            bool decryptMode = encrypted.Length > 0 && plain.Length == 0;

            sp.Children.Add(new TextBlock { Text = "Password", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 6) });
            var pw = Pass();
            sp.Children.Add(pw.Frame);
            var meter = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Height = 4, Margin = new Thickness(0, 8, 0, 0) };
            var bars = Enumerable.Range(0, 4).Select(_ => new Border { CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 4, 0), Background = Ui.Res("B.Line") }).ToList();
            foreach (var b in bars) meter.Children.Add(b);
            (PasswordBox Box, Border Frame) confirm = default;
            if (!decryptMode)
            {
                sp.Children.Add(meter);
                sp.Children.Add(new TextBlock { Text = "Confirm", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 12, 0, 6) });
                confirm = Pass();
                sp.Children.Add(confirm.Frame);
            }
            pw.Box.PasswordChanged += (s, e) =>
            {
                int score = FileOps.PasswordScore(pw.Box.Password);
                var col = score <= 1 ? "B.Amber" : "B.Mint";
                for (int i = 0; i < 4; i++)
                {
                    var target = i < score ? ((SolidColorBrush)Ui.Res(col)).Color : ((SolidColorBrush)Ui.Res("B.Line")).Color;
                    var brush = new SolidColorBrush(((SolidColorBrush)bars[i].Background).Color);
                    bars[i].Background = brush;
                    Ui.ColorTo(brush, target, 220);
                }
            };

            sp.Children.Add(new TextBlock
            {
                Text = decryptMode
                    ? $"{encrypted.Length} encrypted {(encrypted.Length == 1 ? "file" : "files")}. The original comes back next to it."
                    : "AES-256-GCM. Writes name.ext.mtenc beside each file. There is no way back without the password.",
                Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 12, 0, 0)
            });

            bool deleteAfter = false;
            if (!decryptMode)
            {
                var del = new CheckBox { Style = Ui.Style("Switch"), Tag = Ui.Res("B.Amber"), Margin = new Thickness(0, 12, 0, 0),
                    Content = new TextBlock { Text = "Send originals to the Recycle Bin afterwards", Foreground = Ui.Res("B.TextSoft"), FontSize = 13, FontFamily = Ui.Font("F.Body") } };
                del.Checked += (s, e) => deleteAfter = true;
                del.Unchecked += (s, e) => deleteAfter = false;
                sp.Children.Add(del);
            }

            var row = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            async void DoEncrypt()
            {
                if (_busy || plain.Length == 0) return;
                if (pw.Box.Password.Length < 8) { Warn("Use at least 8 characters."); return; }
                if (confirm.Box != null && confirm.Box.Password != pw.Box.Password) { Warn("The passwords do not match."); return; }
                _busy = true;
                Status.Foreground = Ui.Res("B.TextMute");
                var r = await FileOps.EncryptAsync(plain, pw.Box.Password, false, n => Dispatcher.BeginInvoke(new Action(() => Status.Text = $"Encrypting {n} of {plain.Length}…")));
                if (deleteAfter) await FileOps.RecycleAsync(r.Where(x => x.Ok).Select(x => x.Source));
                _busy = false;
                Report("Encrypted", r, Status);
            }
            async void DoDecrypt()
            {
                if (_busy || encrypted.Length == 0) return;
                _busy = true;
                Status.Foreground = Ui.Res("B.TextMute");
                var r = await FileOps.DecryptAsync(encrypted, pw.Box.Password, n => Dispatcher.BeginInvoke(new Action(() => Status.Text = $"Decrypting {n} of {encrypted.Length}…")));
                _busy = false;
                Report("Decrypted", r, Status);
            }
            if (decryptMode)
            {
                var dec = Ui.Button($"Decrypt {encrypted.Length}", DoDecrypt, "Btn.Accent", Icons.Shield);
                dec.Height = 40; dec.HorizontalAlignment = HorizontalAlignment.Stretch; dec.Margin = new Thickness(0);
                Grid.SetColumnSpan(dec, 2);
                row.Children.Add(dec);
                pw.Box.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoDecrypt(); };
            }
            else
            {
                var enc = Ui.Button($"Encrypt {plain.Length}", DoEncrypt, "Btn.Accent", Icons.Shield);
                enc.Height = 40; enc.HorizontalAlignment = HorizontalAlignment.Stretch; enc.Margin = new Thickness(0, 0, 8, 0);
                row.Children.Add(enc);
                var dec = Ui.Button(encrypted.Length > 0 ? $"Decrypt {encrypted.Length}" : "Decrypt", DoDecrypt);
                dec.Height = 40; dec.Margin = new Thickness(0); dec.IsEnabled = encrypted.Length > 0;
                Grid.SetColumn(dec, 1);
                row.Children.Add(dec);
                confirm.Box.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoEncrypt(); };
            }
            sp.Children.Add(row);
            Dispatcher.BeginInvoke(new Action(() => pw.Box.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            return sp;
        }

        private void Warn(string text)
        {
            Status.Text = text;
            Status.Foreground = Ui.Res("B.Amber");
            Ui.Rise(Status, 0, 4, 200);
        }

        private static (PasswordBox Box, Border Frame) Pass()
        {
            var box = new PasswordBox
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"),
                FontFamily = Ui.Font("F.Mono"), FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0)
            };
            box.Template = PlainBox();
            var frame = new Border { Height = 42, CornerRadius = new CornerRadius(10), Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 0, 12, 0), Child = box };
            box.GotKeyboardFocus += (s, e) => frame.BorderBrush = Ui.Res("B.Mint");
            box.LostKeyboardFocus += (s, e) => frame.BorderBrush = Ui.Res("B.LineStrong");
            return (box, frame);
        }

        /// <summary>A bare PasswordBox template so no stock border shows; the frame around it carries the style.</summary>
        private static ControlTemplate PlainBox()
        {
            var t = new ControlTemplate(typeof(PasswordBox));
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            host.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            t.VisualTree = host;
            return t;
        }

        private void OnClose(object s, RoutedEventArgs e) => Close();

        private void OnDrag(object s, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            if (e.OriginalSource is DependencyObject d && (FindParent<TextBox>(d) != null || FindParent<PasswordBox>(d) != null || FindParent<Button>(d) != null)) return;
            try { DragMove(); } catch { }
        }

        private static T FindParent<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null) { if (d is T t) return t; d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
            return null;
        }
    }
}
