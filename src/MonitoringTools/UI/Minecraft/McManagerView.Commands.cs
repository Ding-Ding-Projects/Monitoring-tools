using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Minecraft
{
    // The command builder: a gallery of tasks, each a form with proper controls, and the whole command tree walked node by
    // node; below it the coloured command line, whether it is complete, and Run, Copy, Save as a favourite and Schedule it.
    internal sealed partial class McManagerView
    {
        internal static string CommandMode = "tasks";   // "tasks" or "tree"
        internal static string CommandTaskId = "give";
        private static readonly Dictionary<string, Dictionary<string, string>> TaskValues = new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<string, List<McCommandTree.Step>> Drafts = new Dictionary<string, List<McCommandTree.Step>>();
        /// <summary>Set by the self-test in place of the two-key confirmation window: gets the line, answers whether it was confirmed.</summary>
        internal static Func<string, bool> ConfirmForTest;

        private McCommandNode _tree;
        private McRegistries _registries;
        private readonly Dictionary<string, FrameworkElement> _fieldFocus = new Dictionary<string, FrameworkElement>();
        private readonly List<FrameworkElement> _stepFocus = new List<FrameworkElement>();
        private Action _refreshCommand, _galleryPaint;
        private bool _galleryCompact;
        private TextBlock _cmdStatus, _cmdAnswer;
        private WrapPanel _tokens;
        private Button _runButton;

        internal McCommandNode CommandTreeForTest => _tree;
        internal McRegistries RegistriesForTest => _registries;
        internal McTaskBuild CommandBuild { get; private set; }
        internal string PreviewLine { get; private set; }
        internal string CommandStatus => _cmdStatus?.Text;
        internal string CommandAnswer => _cmdAnswer?.Text;
        internal bool RunEnabled => _runButton?.IsEnabled ?? false;
        internal WrapPanel PreviewParts => _tokens;
        internal List<McCommandTree.Step> BuilderSteps => Current == null ? null : Drafts.TryGetValue(Current.Id, out var d) ? d : null;
        internal FrameworkElement FieldFocus(string id) => _fieldFocus.TryGetValue(id, out var f) ? f : null;

        private const string StarIcon = "M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z";
        private const string BackIcon = "M19 12H5M11 6l-6 6 6 6";
        private const string TreeIcon = "M5 4v16M5 8h8M5 16h8M13 6h6v4h-6zM13 14h6v4h-6z";
        private const string SparkIcon = "M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5L18 18M6 18l2.5-2.5M15.5 8.5L18 6";

        internal static Dictionary<string, string> ValuesFor(ServerDef s, string task)
        {
            var key = s.Id + "|" + task;
            if (!TaskValues.TryGetValue(key, out var v)) TaskValues[key] = v = new Dictionary<string, string>(StringComparer.Ordinal);
            return v;
        }

        /// <summary>
        /// Opens every task, a selector with its filters, and Every command with a walked line, and calls
        /// <paramref name="scan"/> on each, for the language audit and the settings walk. Values are put back afterwards.
        /// </summary>
        internal async Task VisitCommandStatesAsync(Func<string, Task> scan)
        {
            var s = Current;
            if (s == null || s.IsBedrock) return;
            var mode = CommandMode; var task = CommandTaskId;
            try
            {
                foreach (var t in McCommandTasks.All)
                {
                    var v = ValuesFor(s, t.Id);
                    var saved = new Dictionary<string, string>(v);
                    if (t.Id == "time") v["when"] = "{time}";
                    if (t.Id == "tp") v["targets"] = "@a[distance=..10]";
                    OpenCommandTask(t.Id);
                    await scan("commands " + t.Id);
                    v.Clear(); foreach (var kv in saved) v[kv.Key] = kv.Value;
                }
                CommandMode = "tree";
                ShowSection("commands");
                await scan("commands every command");
                for (int i = 0; i < 40 && _tree == null; i++) await Task.Delay(50);
                OpenLine(s, "effect give @a minecraft:speed 30 1");
                await scan("commands every command effect");
            }
            finally { CommandMode = mode; CommandTaskId = task; }
        }

        /// <summary>Opens the builder on a task (used by the self-test, the console and the header).</summary>
        internal void OpenCommandTask(string taskId)
        {
            CommandMode = "tasks"; CommandTaskId = taskId;
            ShowSection("commands");
        }

        internal void SetCommandValue(string field, string value)
        {
            if (Current == null) return;
            ValuesFor(Current, CommandTaskId)[field] = value;
            BuildWorkspace();
        }

        private static string TaskIcon(string key) => key switch
        {
            "pin" => Icons.Globe, "flask" => Icons.Bolt, "game" => Icons.Game, "spark" => SparkIcon, "door" => Icons.Close, "shield" => Icons.Shield,
            "list" => Icons.Lines, "sun" => Icons.Moon, "cloud" => Icons.Wifi, "paw" => Icons.Box, "cube" => Icons.Box, "home" => Icons.Monitor,
            "rule" => Icons.Wand, "chat" => Icons.Mail, "save" => Icons.Archive, "power" => Icons.Power, _ => StarIcon,
        };

        private FrameworkElement BuildCommands(ServerDef s)
        {
            var root = new StackPanel();
            AutomationProperties.SetName(root, "Command builder");
            _fieldFocus.Clear(); _stepFocus.Clear();
            _tree = null; _registries = null;
            _refreshCommand = null; _tokens = null; _cmdStatus = null; _cmdAnswer = null; _runButton = null;

            // ---- header: back, title, the two modes
            var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var back = McUi.IconButton(BackIcon, "Back to the console", () => ShowSection("console"));
            back.VerticalAlignment = VerticalAlignment.Top; back.Margin = new Thickness(0, 2, 10, 0);
            head.Children.Add(back);
            var titles = new StackPanel();
            var title = McUi.Title("Command builder", 24);
            AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level3);
            titles.Children.Add(title);
            titles.Children.Add(McUi.Body(s.IsBedrock ? "Bedrock servers do not report a command list, so their console takes typed lines."
                : $"{s.Flavour} {s.Version}. Every field comes from this server's own command list, players and registries.", "B.TextSoft", 13.5));
            Grid.SetColumn(titles, 1); head.Children.Add(titles);
            var modes = new WrapPanel { VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(modes, "Builder mode");
            var tasksChip = McUi.Chip("Tasks", CommandMode == "tasks", () => { CommandMode = "tasks"; BuildWorkspace(); }, "Tasks: forms for what people most often want to do");
            var treeChip = McUi.Chip("Every command", CommandMode == "tree", () => { CommandMode = "tree"; BuildWorkspace(); }, "Every command: walk the server's whole command tree");
            tasksChip.PreviewKeyDown += (o, e) => { if (e.Key == Key.Right) { treeChip.Focus(); e.Handled = true; } };
            treeChip.PreviewKeyDown += (o, e) => { if (e.Key == Key.Left) { tasksChip.Focus(); e.Handled = true; } };
            modes.Children.Add(tasksChip); modes.Children.Add(treeChip);
            Grid.SetColumn(modes, 2); head.Children.Add(modes);
            root.Children.Add(head);

            if (s.IsBedrock)
            {
                var card = new StackPanel();
                card.Children.Add(McUi.Body("The Bedrock server has no command list to build from. Type its commands in the console instead.", "B.TextSoft", 13));
                var b = Ui.Button("Back to the console", () => ShowSection("console"), "Btn.Ghost", Icons.Code);
                b.HorizontalAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 10, 0, 0);
                card.Children.Add(b);
                root.Children.Add(McUi.Card(card));
                return root;
            }

            var host = new ContentControl { IsTabStop = false };
            root.Children.Add(host);
            root.PreviewKeyDown += (o, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { _ = RunCurrent(s); e.Handled = true; } };

            async void Load(bool download)
            {
                var note = McUi.Body(download ? "Reading the command list from the server jar…" : "Loading the command list…", "B.TextMute", 12.5);
                host.Content = McUi.Card(note);
                var (tree, unavailable) = await McCommandTree.LoadAsync(s.Version, t => Dispatcher.BeginInvoke(new Action(() => { note.Text = t; Core.Language.Refresh(note); })), download);
                if (tree == null)
                {
                    var sp = new StackPanel();
                    sp.Children.Add(McUi.Body(unavailable + " Reading it downloads the official server jar for this version once and asks it for its commands.", "B.TextSoft", 13));
                    var b = Ui.Button("Read this version's command list", () => Load(true), "Btn.Ghost", Icons.Code);
                    b.HorizontalAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 8, 0, 0);
                    sp.Children.Add(b);
                    host.Content = McUi.Card(sp);
                    Core.Language.Refresh(host);
                    return;
                }
                _tree = tree;
                (_registries, _) = await McRegistries.LoadAsync(s.Version);
                host.Content = CommandsBody(s);
                Core.Language.Refresh(host);
            }
            Load(false);
            return root;
        }

        private FrameworkElement CommandsBody(ServerDef s)
        {
            var grid = new Grid();
            var gallery = Gallery(s);
            var right = new StackPanel();
            var form = new ContentControl { IsTabStop = false };
            right.Children.Add(form);
            right.Children.Add(Preview(s));
            if (CommandMode == "tree")
            {
                var ex = new StackPanel();
                ex.Children.Add(McUi.Title("Every command", 16));
                var count = _tree.Children.Count(c => c.IsLiteral);
                var t = McUi.Body($"All {count} commands of this server, walked step by step from its own command tree. Each argument gets the control its type needs: whole numbers with their limits, true or false, a player or selector picker, positions as axes, items, blocks, effects, enchantments and entities as searchable lists from the game's registries, and text as a text box. Commands added by plugins or mods appear only when the server reports them.", "B.TextSoft", 13);
                t.Margin = new Thickness(0, 6, 0, 0);
                ex.Children.Add(t);
                right.Children.Add(McUi.Card(ex));
            }
            void Form()
            {
                _fieldFocus.Clear(); _stepFocus.Clear();
                form.Content = CommandMode == "tree" ? TreeForm(s, Form) : TaskForm(s, Form);
                Core.Language.Refresh(form);
                _refreshCommand?.Invoke();
            }
            bool stacked = false;
            void Arrange(bool narrow)
            {
                stacked = narrow;
                if (_galleryCompact != narrow) { _galleryCompact = narrow; _galleryPaint?.Invoke(); }
                grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
                if (narrow)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    Grid.SetColumn(right, 0); Grid.SetRow(right, 1);
                    gallery.Margin = new Thickness(0);
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(right, 1); Grid.SetRow(right, 0);
                    gallery.Margin = new Thickness(0, 0, 16, 0);
                }
            }
            grid.Children.Add(gallery);
            grid.Children.Add(right);
            Arrange(false);
            grid.SizeChanged += (o, e) => { bool narrow = e.NewSize.Width < 860; if (narrow != stacked) Arrange(narrow); };
            Form();
            return grid;
        }

        // ------------------------------------------------------------------ the gallery

        private FrameworkElement Gallery(ServerDef s)
        {
            var sp = new StackPanel();
            var search = new SearchField("minecraft:command-tasks", "Search tasks", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
            search.SampleSource = () => McCommandTasks.All.Select(t => t.Label).Concat(_tree.Children.Select(c => c.Name));
            var list = new StackPanel();
            AutomationProperties.SetName(list, "Tasks");
            void Paint()
            {
                list.Children.Clear();
                // favourites first
                if (s.FavouriteCommands.Count > 0)
                {
                    list.Children.Add(GroupTitle("Favourites"));
                    foreach (var f in s.FavouriteCommands.Where(f => search.State.Matches(f)).ToList())
                    {
                        var line = f;
                        var g = new Grid();
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var content = new StackPanel { Orientation = Orientation.Horizontal };
                        content.Children.Add(new ContentControl { Content = Ui.Icon(StarIcon, Ui.Res("B.Amber"), 15), Margin = new Thickness(0, 0, 10, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
                        var lt = McUi.Literal(line, "B.Text", 12.5, true);
                        lt.TextWrapping = TextWrapping.NoWrap; lt.TextTrimming = TextTrimming.CharacterEllipsis; lt.MaxWidth = 200;
                        content.Children.Add(lt);
                        var row = McUi.Row(content, "Open favourite " + line);
                        row.ToolTip = line;
                        row.Click += (o, e) => OpenLine(s, line);
                        g.Children.Add(row);
                        var del = McUi.IconButton(Icons.Trash, "Remove favourite " + line, () => { s.FavouriteCommands.Remove(line); SaveServers(); Paint(); });
                        Grid.SetColumn(del, 1); g.Children.Add(del);
                        list.Children.Add(g);
                    }
                }
                int shown = 0;
                foreach (var group in McCommandTasks.Groups)
                {
                    var tasks = McCommandTasks.All.Where(t => t.Group == group && (search.State.Matches(t.Label) || search.State.Matches(t.Shown) || search.State.Matches(t.Help))).ToList();
                    if (tasks.Count == 0) continue;
                    list.Children.Add(GroupTitle(group));
                    WrapPanel chips = null;
                    if (_galleryCompact) { chips = new WrapPanel(); AutomationProperties.SetName(chips, group); list.Children.Add(chips); }
                    foreach (var t in tasks)
                    {
                        shown++;
                        var task = t;
                        bool on = CommandMode == "tasks" && CommandTaskId == t.Id;
                        if (chips != null)
                        {
                            // at narrow widths the gallery is a few rows of chips, so the form stays in view under it
                            var chip = McUi.Chip(t.Label, on, () => OpenCommandTask(task.Id), t.Label);
                            chip.ToolTip = t.Help + " (" + t.Shown + ")";
                            chips.Children.Add(chip);
                            continue;
                        }
                        var g = new Grid();
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        g.Children.Add(new ContentControl { Content = Ui.Icon(TaskIcon(t.Icon), Ui.Res(on ? "B.OnSecondaryContainer" : "B.TextSoft"), 16), Margin = new Thickness(0, 0, 12, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
                        var label = new TextBlock { Text = t.Label, FontSize = 14, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
                        Grid.SetColumn(label, 1); g.Children.Add(label);
                        var cmd = McUi.Literal(t.Shown, on ? "B.OnSecondaryContainer" : "B.TextMute", 11.5, true);
                        cmd.VerticalAlignment = VerticalAlignment.Center; cmd.Margin = new Thickness(8, 0, 0, 0);
                        Grid.SetColumn(cmd, 2); g.Children.Add(cmd);
                        var row = McUi.Row(g, t.Label);
                        row.Foreground = Ui.Res(on ? "B.OnSecondaryContainer" : "B.Text");
                        row.Background = on ? Ui.Res("B.SecondaryContainer") : System.Windows.Media.Brushes.Transparent;
                        AutomationProperties.SetItemStatus(row, on ? "selected" : "");
                        row.ToolTip = t.Help;
                        row.Click += (o, e) => OpenCommandTask(task.Id);
                        if (on) row.Loaded += (o, e) => { var b = McUi.Shape(row); if (b != null) McUi.Morph(b, 24); };
                        list.Children.Add(row);
                    }
                }
                // commands of the tree that match, as a way into the Every command mode
                if (search.State.IsActive)
                {
                    var cmds = _tree.Children.Where(c => c.IsLiteral && search.State.Matches(c.Name)).Take(10).ToList();
                    if (cmds.Count > 0)
                    {
                        list.Children.Add(GroupTitle("Every command"));
                        var wrap = new WrapPanel();
                        Core.Language.SetLiteral(wrap, true);
                        foreach (var c in cmds)
                        {
                            var cc = c;
                            wrap.Children.Add(McUi.Chip("/" + c.Name, false, () => { CommandMode = "tree"; Drafts[s.Id] = new List<McCommandTree.Step> { new McCommandTree.Step { Node = cc } }; BuildWorkspace(); }, "Build /" + c.Name + " in Every command"));
                        }
                        list.Children.Add(wrap);
                    }
                }
                search.SetCount(shown, McCommandTasks.All.Length, "tasks");
                if (shown == 0 && list.Children.Count == 0) list.Children.Add(McUi.Body("No task matches. Every command has all of them.", "B.TextMute", 12.5));
                Core.Language.Refresh(list);
            }
            search.Filter += Paint;
            sp.Children.Add(search);
            sp.Children.Add(list);
            _galleryPaint = Paint;
            Paint();
            return McUi.Card(sp, 14);
        }

        private static IEnumerable<T> Logical<T>(DependencyObject d) where T : DependencyObject
        {
            foreach (var c in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>())
            {
                if (c is T t) yield return t;
                foreach (var x in Logical<T>(c)) yield return x;
            }
        }

        private static TextBlock GroupTitle(string text) => new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(8, 12, 0, 4) };

        private static void SaveServers() { try { ServerManager.Save(); } catch (Exception ex) { Log.Error("Minecraft save", ex); } }

        /// <summary>Opens a saved line in Every command with its fields filled, when it fits this version's command tree.</summary>
        internal void OpenLine(ServerDef s, string line)
        {
            var steps = McCommandTree.ParseLine(_tree, line);
            CommandMode = "tree";
            if (steps != null) Drafts[s.Id] = steps;
            BuildWorkspace();
            if (steps == null) Dispatcher.BeginInvoke(new Action(() => { if (_cmdAnswer != null) { _cmdAnswer.Text = line + " does not fit this version's command list, so it cannot be opened in the builder. It can still be run from the console's favourites."; Core.Language.Refresh(_cmdAnswer); } }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ------------------------------------------------------------------ a task form

        private FrameworkElement TaskForm(ServerDef s, Action rebuild)
        {
            var task = McCommandTasks.Find(CommandTaskId) ?? McCommandTasks.All[0];
            var values = ValuesFor(s, task.Id);
            var card = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            head.Children.Add(new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(14), Background = Ui.Res("B.MintWash"), Child = Ui.Icon(TaskIcon(task.Icon), Ui.Res("B.Mint"), 20), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top });
            var ht = new StackPanel();
            var title = McUi.Title(task.Label, 20);
            AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level3);
            ht.Children.Add(title);
            var help = McUi.Body(task.Help, "B.TextSoft", 13);
            help.MaxWidth = 560;
            ht.Children.Add(help);
            head.Children.Add(ht);
            card.Children.Add(head);

            var build = McCommandTasks.Build(task, _tree, values, _registries);
            if (build.Unavailable != null)
                card.Children.Add(McUi.Body(build.Unavailable + " Every command lists what this version has.", "B.Amber", 13));
            if (build.Fields.Count == 0 && build.Unavailable == null)
                card.Children.Add(McUi.Body("This command takes nothing more. Check the line below and run it.", "B.TextMute", 13));
            foreach (var f in build.Fields)
            {
                var field = f;
                if (f.IsChoice)
                {
                    var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
                    wrap.Children.Add(new TextBlock { Text = f.Label + (f.Optional ? " (optional)" : ""), FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 6) });
                    values.TryGetValue(f.Id, out var cur);
                    if (string.IsNullOrEmpty(cur) && !f.Optional) cur = f.Options[0];
                    void Set(string v)
                    {
                        values[field.Id] = v;
                        if (field.Id == "rule") values.Remove("value");   // a different rule takes a different kind of value
                        Dispatcher.BeginInvoke(new Action(() => { rebuild(); FieldFocus(field.Id)?.Focus(); }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    FrameworkElement control = f.Id == "rule" || f.Options.Count > 10
                        ? McCommandControls.LongChoice(f.Id, f.Label, f.Options, cur, Set, s.Version)
                        : McCommandControls.Enum(f.Id, f.Label, f.Options, f.Optional, cur, Set, f.Id == "rule");
                    wrap.Children.Add(control);
                    _fieldFocus[f.Id] = Logical<Button>(control).FirstOrDefault(b => AutomationProperties.GetItemStatus(b) == "selected") ?? (FrameworkElement)Logical<Button>(control).FirstOrDefault() ?? control;
                    card.Children.Add(wrap);
                    continue;
                }
                values.TryGetValue(f.Id, out var val);
                var view = McCommandControls.For(s, f.Node, f.Label, f.Optional, val ?? "", v => { values[field.Id] = v; _refreshCommand?.Invoke(); }, _registries, p => McCommandRun.PositionAsync(s, p));
                _fieldFocus[f.Id] = view.Focus;
                card.Children.Add(view.View);
            }
            var outer = McUi.Card(card);
            Ui.SharedAxis(outer, 1);
            return outer;
        }

        // ------------------------------------------------------------------ every command

        private FrameworkElement TreeForm(ServerDef s, Action rebuild)
        {
            if (!Drafts.TryGetValue(s.Id, out var steps)) Drafts[s.Id] = steps = new List<McCommandTree.Step>();
            var sp = new StackPanel();
            var picker = new SearchField("minecraft:commands", "Find a command", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
            var roots = _tree.Children.Where(c => c.IsLiteral).ToList();
            picker.SampleSource = () => roots.Select(r => r.Name);
            var rootChips = new WrapPanel();
            Core.Language.SetLiteral(rootChips, true);
            AutomationProperties.SetName(rootChips, "Commands");
            void Roots()
            {
                rootChips.Children.Clear();
                var hits = roots.Where(r => picker.State.Matches(r.Name)).ToList();
                picker.SetCount(hits.Count, roots.Count, "commands");
                foreach (var r in hits.Take(120))
                {
                    var rr = r;
                    rootChips.Children.Add(McUi.Chip(r.Name, steps.Count > 0 && steps[0].Node == r, () => { steps.Clear(); steps.Add(new McCommandTree.Step { Node = rr }); rebuild(); }, "Command " + r.Name));
                }
            }
            picker.Filter += Roots;
            sp.Children.Add(McUi.Title("Choose a command", 16));
            sp.Children.Add(picker);
            sp.Children.Add(rootChips);
            Roots();

            var chain = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            AutomationProperties.SetName(chain, "Command so far");
            var words = new WrapPanel();
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                int at = i;
                if (step.Node.IsLiteral)
                {
                    var chip = McUi.Chip(step.Node.Name, true, () => { steps.RemoveRange(at, steps.Count - at); rebuild(); }, $"Remove {step.Node.Name} and what follows");
                    Core.Language.SetLiteral(chip, true);
                    words.Children.Add(chip);
                    _stepFocus.Add(chip);
                }
                else
                {
                    if (words.Children.Count > 0) { chain.Children.Add(words); words = new WrapPanel(); }
                    bool known = McCommandTasks.Labels.ContainsKey(step.Node.Name);
                    var view = McCommandControls.For(s, step.Node, known ? McCommandTasks.LabelOf(step.Node.Name) : step.Node.Name, false, step.Value, v => { step.Value = v; _refreshCommand?.Invoke(); }, _registries, p => McCommandRun.PositionAsync(s, p));
                    if (!known && view.View is Panel vp && vp.Children.Count > 0 && vp.Children[0] is TextBlock lab) Core.Language.SetLiteral(lab, true);
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.Children.Add(view.View);
                    var del = McUi.IconButton(Icons.Close, $"Remove {step.Node.Name} and what follows", () => { steps.RemoveRange(at, steps.Count - at); rebuild(); });
                    del.VerticalAlignment = VerticalAlignment.Top;
                    Grid.SetColumn(del, 1); row.Children.Add(del);
                    chain.Children.Add(row);
                    _stepFocus.Add(view.Focus);
                }
            }
            if (words.Children.Count > 0) chain.Children.Add(words);
            sp.Children.Add(chain);

            var node = steps.Count == 0 ? null : steps[^1].Node;
            var kids = node?.Next ?? new List<McCommandNode>();
            var nextLabel = McUi.Body(node == null ? "Choose a command above." : kids.Count == 0 ? "This command is complete." : node.CanEnd ? "It can run as it is, or take more:" : "Next:", "B.TextMute", 12.5);
            nextLabel.Margin = new Thickness(0, 8, 0, 4);
            sp.Children.Add(nextLabel);
            var next = new WrapPanel();
            AutomationProperties.SetName(next, "What can come next");
            foreach (var k in kids)
            {
                var kk = k;
                var label = k.IsLiteral ? k.Name : "<" + k.Name + ">";
                var b = McUi.Chip(label, false, () => { steps.Add(new McCommandTree.Step { Node = kk }); rebuild(); }, k.IsLiteral ? "Add " + k.Name : $"Add {k.Name}, {k.Hint}");
                Core.Language.SetLiteral(b, true);
                b.ToolTip = k.IsLiteral ? null : k.Hint;
                next.Children.Add(b);
            }
            sp.Children.Add(next);
            var clear = Ui.Button("Clear", () => { steps.Clear(); rebuild(); }, "Btn.Ghost", Icons.Close);
            clear.HorizontalAlignment = HorizontalAlignment.Left; clear.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(clear);
            return McUi.Card(sp);
        }

        // ------------------------------------------------------------------ the preview and the actions

        private (List<McCommandTree.Step> Steps, string Problem, McTask Task) CurrentCommand(ServerDef s)
        {
            if (CommandMode == "tree")
            {
                var steps = Drafts.TryGetValue(s.Id, out var d) ? d : new List<McCommandTree.Step>();
                CommandBuild = null;
                return (steps, McCommandTree.Validate(steps, _registries), null);
            }
            var task = McCommandTasks.Find(CommandTaskId) ?? McCommandTasks.All[0];
            var b = McCommandTasks.Build(task, _tree, ValuesFor(s, task.Id), _registries);
            CommandBuild = b;
            return (b.Steps, b.Problem, task);
        }

        private static (string Bg, string Fg, string Name) Tone(McCommandTree.PartKind k) => k switch
        {
            McCommandTree.PartKind.Command => ("B.MintWash", "B.Mint", "Command"),
            McCommandTree.PartKind.Target => ("B.SecondaryContainer", "B.OnSecondaryContainer", "Target"),
            McCommandTree.PartKind.Number => ("B.VioletWash", "B.Violet", "Number"),
            _ => ("B.AmberWash", "B.Amber", "Value"),
        };

        private FrameworkElement Preview(ServerDef s)
        {
            var card = new StackPanel();
            var head = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            head.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Code, Ui.Res("B.Mint"), 16), Margin = new Thickness(0, 0, 8, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(new TextBlock { Text = "The command", FontSize = 15, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            head.Children.Add(new TextBlock { Text = "Each part is coloured by what it is. Click a part to go to its field.", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            card.Children.Add(head);
            _tokens = new WrapPanel();
            AutomationProperties.SetName(_tokens, "The exact command line");
            card.Children.Add(new Border { Background = Ui.Res("B.Ground"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 10, 6, 6), Child = _tokens });
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 10) };
            var statusIcon = new ContentControl { IsTabStop = false, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            statusRow.Children.Add(statusIcon);
            _cmdStatus = McUi.Body("", "B.Mint", 13.5);
            _cmdStatus.MaxWidth = 640;
            AutomationProperties.SetLiveSetting(_cmdStatus, AutomationLiveSetting.Polite);
            statusRow.Children.Add(_cmdStatus);
            card.Children.Add(statusRow);
            var buttons = new WrapPanel();
            _runButton = Ui.Button("Run on " + s.Name, () => _ = RunCurrent(s), "Btn.Accent", Icons.Play);
            AutomationProperties.SetName(_runButton, "Run on " + s.Name);
            _runButton.ToolTip = "Runs it through the server's console. Ctrl+Enter does the same.";
            buttons.Children.Add(_runButton);
            var copy = Ui.Button("Copy", () =>
            {
                try { Clipboard.SetText(PreviewLine ?? ""); Answer("Copied " + PreviewLine + ".", "B.Mint"); } catch (Exception ex) { Answer(ex.Message, "B.Amber"); }
            }, "Btn.Ghost", Icons.Clipboard);
            buttons.Children.Add(copy);
            var fav = Ui.Button("Save as a favourite", () => SaveFavourite(s), "Btn.Ghost", StarIcon);
            // the ghost style draws a disabled button like an enabled one; dim it so "already a favourite" reads as off
            fav.IsEnabledChanged += (o, e) => fav.Opacity = fav.IsEnabled ? 1 : 0.38;
            fav.Opacity = fav.IsEnabled ? 1 : 0.38;
            buttons.Children.Add(fav);
            var sched = Ui.Button("Schedule it", () => ScheduleLine(s), "Btn.Ghost", Icons.Clock);
            buttons.Children.Add(sched);
            card.Children.Add(buttons);
            _cmdAnswer = McUi.Body("", "B.TextSoft", 12.5);
            AutomationProperties.SetLiveSetting(_cmdAnswer, AutomationLiveSetting.Polite);
            _cmdAnswer.Margin = new Thickness(0, 6, 0, 0);
            card.Children.Add(_cmdAnswer);

            _refreshCommand = () =>
            {
                var (steps, problem, task) = CurrentCommand(s);
                PreviewLine = McCommandTree.Line(steps);
                _tokens.Children.Clear();
                var slash = new TextBlock { Text = "/", FontFamily = Ui.Font("F.Mono"), FontSize = 16, Foreground = Ui.Res("B.TextMute"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 6) };
                Core.Language.SetLiteral(slash, true);
                _tokens.Children.Add(slash);
                int i = 0;
                while (i < steps.Count)
                {
                    var kind = McCommandTree.KindOf(steps[i]);
                    int j = i;
                    var text = steps[i].Node.IsLiteral ? steps[i].Node.Name : steps[i].Value;
                    // consecutive command words read as one part, as "effect give"
                    if (kind == McCommandTree.PartKind.Command)
                        while (j + 1 < steps.Count && McCommandTree.KindOf(steps[j + 1]) == McCommandTree.PartKind.Command) { j++; text += " " + steps[j].Node.Name; }
                    bool empty = string.IsNullOrWhiteSpace(text);
                    var field = steps[i].Field ?? steps[i].Node.Name;
                    var label = steps[i].Node.IsArgument && McCommandTasks.Labels.ContainsKey(field) ? McCommandTasks.LabelOf(field) : steps[i].Node.IsLiteral && steps[i].Field != null ? McCommandTasks.LabelOf(steps[i].Field) : null;
                    var (bg, fg, kindName) = Tone(kind);
                    var tb = new TextBlock { Text = empty ? "<" + (label ?? steps[i].Node.Name) + ">" : text.Trim(), FontFamily = Ui.Font("F.Mono"), FontSize = 15, Foreground = Ui.Res(empty ? "B.TextMute" : fg), FontStyle = empty ? FontStyles.Italic : FontStyles.Normal };
                    Core.Language.SetLiteral(tb, true);
                    var part = new Button { Style = Ui.Style("Btn.Row"), Content = new Border { Background = Ui.Res(empty ? "B.Raised" : bg), CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 2, 7, 3), Child = tb }, Height = double.NaN, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 6) };
                    var tip = kindName + " · " + (label ?? (kind == McCommandTree.PartKind.Command ? "the command" : steps[i].Node.Name));
                    part.ToolTip = tip;
                    AutomationProperties.SetName(part, tip);
                    // the value itself is data (a name, an id, a message), so it is the item status rather than part of the translated name
                    AutomationProperties.SetItemStatus(part, empty ? "not filled in" : text.Trim());
                    int index = i; string fid = steps[i].Field;
                    part.Click += (o, e) =>
                    {
                        FrameworkElement target = null;
                        if (fid != null) _fieldFocus.TryGetValue(fid, out target);
                        if (target == null && CommandMode == "tree" && index < _stepFocus.Count) target = _stepFocus[index];
                        if (target != null) { target.BringIntoView(); target.Focus(); }
                    };
                    _tokens.Children.Add(part);
                    i = j + 1;
                }
                bool ok = problem == null && steps.Count > 0;
                _cmdStatus.Text = ok ? $"Complete and valid for {s.Version}. Ready to run." : problem ?? "Choose a command.";
                _cmdStatus.Foreground = Ui.Res(ok ? "B.Mint" : "B.Amber");
                statusIcon.Content = Ui.Icon(ok ? Icons.Check : Icons.Help, Ui.Res(ok ? "B.Mint" : "B.Amber"), 16);
                _runButton.IsEnabled = ok;
                fav.IsEnabled = ok && !s.FavouriteCommands.Contains(PreviewLine);
                sched.IsEnabled = ok;
                copy.IsEnabled = steps.Count > 0;
                AutomationProperties.SetHelpText(_runButton, ok ? "" : _cmdStatus.Text);
                Core.Language.Refresh(card);
            };
            return McUi.Card(card, 20, "B.Raised");
        }

        private void Answer(string text, string fg)
        {
            if (_cmdAnswer == null) return;
            _cmdAnswer.Text = text; _cmdAnswer.Foreground = Ui.Res(fg);
            Core.Language.Refresh(_cmdAnswer);
        }

        private void SaveFavourite(ServerDef s)
        {
            if (string.IsNullOrEmpty(PreviewLine) || s.FavouriteCommands.Contains(PreviewLine)) return;
            s.FavouriteCommands.Add(PreviewLine);
            SaveServers();
            BuildWorkspace();
            Answer("Saved " + PreviewLine + " as a favourite. It is at the top of the tasks and in the console.", "B.Mint");
        }

        private void ScheduleLine(ServerDef s)
        {
            if (string.IsNullOrEmpty(PreviewLine)) return;
            ScheduleDraft = new McSchedule { Kind = McScheduleKind.Command, Command = PreviewLine.TrimStart('/'), Repeat = McRepeat.Daily, Time = "12:00" };
            _scheduleStep = 1;
            ShowSection("schedules");
        }

        /// <summary>Runs the current command: the two-key confirmation first for stop, ban and operator changes.</summary>
        internal async Task<string> RunCurrent(ServerDef s)
        {
            var (steps, problem, task) = CurrentCommand(s);
            if (problem != null || steps.Count == 0) { Answer(problem ?? "Choose a command.", "B.Amber"); return problem; }
            var line = McCommandTree.Line(steps);
            if (task?.Confirm != null || McCommandRun.NeedsConfirmation(line))
            {
                bool yes;
                if (ConfirmForTest != null) yes = ConfirmForTest(line);
                else
                {
                    var first = line.TrimStart('/').Split(' ')[0];
                    var tcs = new TaskCompletionSource<bool>();
                    var dlg = new ConfirmWindow($"Run {line} on {s.Name}?", first switch
                    {
                        "stop" => "The world is saved and the server stops. Everyone on it is disconnected.",
                        "ban" or "ban-ip" => "They are disconnected and cannot join again until they are pardoned.",
                        "op" => "An operator can run every command on this server, including stopping it and changing other players.",
                        _ => "They lose access to operator commands straight away.",
                    }, first switch { "stop" => "Stop the server", "ban" or "ban-ip" => "Ban", "op" => "Make operator", _ => "Remove operator" }, danger: true);
                    bool done = false;
                    dlg.Confirmed += _ => { done = true; tcs.TrySetResult(true); };
                    dlg.Closed += (o, e) => { if (!done) tcs.TrySetResult(false); };
                    dlg.Show();
                    yes = await tcs.Task;
                }
                if (!yes) { Answer("Not run: it was not confirmed.", "B.TextMute"); return "not confirmed"; }
            }
            Answer("Sending…", "B.TextMute");
            string r = !McDemo.IsSample(s) && !IsUp(s) ? "The server is not running, so nothing was sent." : await McCommandRun.SendAsync(s, line);
            Answer(line + " · " + r, "B.TextSoft");
            if (!McDemo.IsSample(s) && IsUp(s))
            {
                s.RecentCommands.Remove(line);
                s.RecentCommands.Insert(0, line);
                if (s.RecentCommands.Count > 8) s.RecentCommands.RemoveRange(8, s.RecentCommands.Count - 8);
                SaveServers();
            }
            return r;
        }
    }
}
