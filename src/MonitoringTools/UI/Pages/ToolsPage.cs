using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private string _forgeOwner;
        private string _forgeFolder;
        private ForgeKind _forgeKind = ForgeKind.GitHub;

        // The tools page: drawing and annotation, publishing a folder to GitHub, and the external editor.
        private FrameworkElement ToolsPage()
        {
            var root = PageRoot("Tools", "Draw, publish and edit",
                "Annotate pictures, publish a folder to GitHub and choose which editor opens your files. Each control says what it does and what it starts as.");
            root.Children.Add(ToolsDrawCard());
            root.Children.Add(ToolsPublishCard());
            root.Children.Add(ToolsEditorCard());
            return root;
        }

        private static TextBlock ToolsNote(string text, string brush = "B.TextMute")
            => new TextBlock { Text = text, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res(brush), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };

        // -------------------------------------------------------------- drawing

        private FrameworkElement ToolsDrawCard()
        {
            var s = Settings.Current;
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Drawing and annotation",
                "Draw on a picture or a blank board. Seven pens, a partial eraser, line, arrow, rectangle, ellipse, text, select and move, undo and redo. Nothing you draw leaves this computer."));
            var result = ToolsNote("");

            var row = new WrapPanel();
            row.Children.Add(Ui.Button("New blank board", () => DrawingWindow.OpenBlank(), "Btn.Accent", Icons.Wand));
            row.Children.Add(Ui.Button("Open an image…", () =>
            {
                var dlg = new OpenFileDialog { Title = "Choose an image to annotate", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.ico|All files|*.*" };
                if (dlg.ShowDialog() != true) return;
                if (!DrawingWindow.OpenFile(dlg.FileName, out var err)) { result.Text = err; result.Foreground = Ui.Res("B.Amber"); }
            }, "Btn.Ghost", Icons.Image));
            var newest = App.History.Items.FirstOrDefault(i => i.Kind == ClipKind.Image && i.Image != null);
            var annotate = Ui.Button("Annotate the newest copied image", () => { if (newest != null) DrawingWindow.OpenBitmap(newest.Image, "Copied image"); }, "Btn.Ghost", Icons.Clipboard);
            annotate.IsEnabled = newest != null;
            annotate.ToolTip = newest != null ? "Opens the most recent picture in clipboard history on the drawing board" : "No picture is in clipboard history right now. Copy one and it will show up here.";
            row.Children.Add(annotate);
            sp.Children.Add(row);
            if (newest == null) sp.Children.Add(ToolsNote("No picture is in clipboard history yet, so that button is off."));
            sp.Children.Add(result);

            sp.Children.Add(Choice("Toolbar position (default top)",
                new[] { ("Top", "top"), ("Right", "right"), ("Bottom", "bottom"), ("Left", "left") }, s.DrawDock, v => s.DrawDock = v));
            sp.Children.Add(Switch("Start with the toolbar minimised", "Off by default. The toolbar starts as a small disc that still shows the active tool. Click it to expand.",
                s.DrawToolbarMinimised, v => s.DrawToolbarMinimised = v));
            sp.Children.Add(Choice("PNG export size (default 1x)", new[] { ("1x", 1), ("2x", 2), ("3x", 3) }, Math.Max(1, Math.Min(3, s.DrawExportScale)), v => s.DrawExportScale = v));

            sp.Children.Add(ToolsNote("Pens: Pencil P, Pen N, Fineliner F, Marker M, Highlighter H, Brush B, Fountain pen U. Eraser E, Select S, Line L, Arrow A, Rectangle R, Ellipse O, Text T. " +
                "Colour C and size Z turn the toolbar into a palette or sliders. Ctrl+Z undo, Ctrl+Y redo, Ctrl+S save a PNG, Ctrl+Shift+C copy, Esc minimise. Hold Shift to lock a stroke to the nearest of eight directions."));
            sp.Children.Add(ToolsNote("How it is built: this is a native Windows drawing board built on the ink canvas that ships with Windows. The Drawesome package is a JavaScript library, and using it would need a web view and a package download that this app does not have, so it is not used. " +
                "Pencil, Pen and Brush thin as you draw faster and the Fountain pen follows the stroke direction; the width is applied when you lift the pen, so the live line is a constant width while you draw. " +
                "Saved pictures never overwrite the original: a PNG goes next to it with \"-annotated\" added to the name."));
            return SearchScope.SetItem(Card(sp), "drawing annotate pen pencil marker highlighter brush eraser shapes text png svg draw sketch");
        }

        // -------------------------------------------------------------- publishing

        private FrameworkElement ToolsPublishCard()
        {
            var s = Settings.Current;
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Publish a folder to GitHub",
                "Commits the folder with Git, then uploads it. A new repository is only created after you agree to it. This app never sees or stores a password or token: sign-in lives in the GitHub CLI's own credential store."));

            Action applyKind = null;
            sp.Children.Add(Choice("Where to publish (default GitHub)",
                new[] { ("GitHub", ForgeKind.GitHub), ("GitLab", ForgeKind.GitLab), ("Gitea or Forgejo", ForgeKind.Gitea) }, _forgeKind,
                v => { _forgeKind = v; applyKind?.Invoke(); }));

            var gh = ForgePublish.GhPath();
            var ghNote = ToolsNote(gh != null
                ? "GitHub CLI found. Accounts, creating repositories and sign-in are available."
                : "The GitHub CLI (gh) is not installed. Creating a repository and managing accounts need it. You can still publish to an existing, empty repository by pasting its address, if Git is already signed in on this computer.", gh != null ? "B.TextMute" : "B.Amber");
            sp.Children.Add(ghNote);
            if (gh == null) sp.Children.Add(Ui.Button("Get the GitHub CLI", () => ActionProvider.Shell("https://cli.github.com"), "Btn.Ghost", Icons.Globe));

            // ---- accounts
            var accountsBox = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            var search = new SearchField("tools:accounts", "Search signed-in accounts") { Margin = new Thickness(0, 0, 0, 8) };
            var accountRows = new StackPanel();
            var ownerHost = new StackPanel();
            var accountsStatus = ToolsNote(gh != null ? "Checking signed-in accounts…" : "No GitHub CLI, so no accounts to list.");
            var accountList = new List<(GhAccount A, FrameworkElement Row)>();

            void FilterAccounts()
            {
                int shown = 0;
                foreach (var (a, row) in accountList)
                {
                    bool ok = !search.State.IsActive || search.State.Matches(a.Login + " " + a.Host);
                    row.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
                    if (ok) shown++;
                }
                search.SetCount(shown, accountList.Count, "accounts");
            }
            search.Filter += FilterAccounts;

            async void LoadAccounts()
            {
                if (gh == null) return;
                accountsStatus.Text = "Checking signed-in accounts…";
                var list = await ForgePublish.AccountsAsync();
                accountRows.Children.Clear(); accountList.Clear();
                if (list.Count == 0) accountsStatus.Text = "No account is signed in. Use \"Add account\" to sign in with GitHub.";
                else accountsStatus.Text = $"{list.Count} account{(list.Count == 1 ? "" : "s")} signed in. Tokens stay in the GitHub CLI's credential store; this app only reads the names.";
                foreach (var a in list)
                {
                    var acc = a;
                    var line = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var label = new TextBlock { Foreground = Ui.Res(acc.Active ? "B.Mint" : "B.Text"), FontFamily = Ui.Font("F.Body"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                    var loginRun = new System.Windows.Documents.Run(acc.Login); Core.Language.SetLiteral(loginRun, true);   // account and host names are not translated
                    var hostRun = new System.Windows.Documents.Run("  ·  " + acc.Host); Core.Language.SetLiteral(hostRun, true);
                    label.Inlines.Add(loginRun);
                    if (acc.Active) label.Inlines.Add(new System.Windows.Documents.Run("  (active)"));
                    label.Inlines.Add(hostRun);
                    var btns = new WrapPanel { Orientation = Orientation.Horizontal };
                    if (!acc.Active) btns.Children.Add(Ui.Button("Set active", async () =>
                    {
                        var r = await ForgePublish.SwitchAccountAsync(acc.Login);
                        accountsStatus.Text = r.Code == 0 ? $"{acc.Login} is now the active account." : "Could not switch: " + r.All;
                        LoadAccounts(); LoadOwners();
                    }));
                    btns.Children.Add(Ui.Button("Refresh sign-in", () =>
                    {
                        ForgePublish.OpenGhInTerminal($"auth refresh --hostname {acc.Host} --user {acc.Login}", out var m);
                        accountsStatus.Text = m;
                    }));
                    btns.Children.Add(Ui.Button("Sign out", () =>
                    {
                        var dlg = new ConfirmWindow($"Sign {acc.Login} out?", "This removes the stored sign-in for this account from the GitHub CLI on this computer. Repositories on GitHub are not touched.", "Sign out", danger: true);
                        dlg.Confirmed += async _ =>
                        {
                            var r = await ForgePublish.SignOutAsync(acc.Login);
                            accountsStatus.Text = r.Code == 0 ? $"Signed {acc.Login} out." : "Could not sign out: " + r.All;
                            LoadAccounts(); LoadOwners();
                        };
                        dlg.Show();
                    }, "Btn.Danger"));
                    Grid.SetColumn(btns, 1);
                    line.Children.Add(label); line.Children.Add(btns);
                    AutomationProperties.SetName(line, "Account " + acc.Login + (acc.Active ? ", active" : ""));
                    var card = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 6, 6, 0), Margin = new Thickness(0, 0, 0, 6), Child = line };
                    SearchScope.SetItem(card, "account " + acc.Login);
                    accountRows.Children.Add(card);
                    accountList.Add((acc, card));
                }
                FilterAccounts();
            }

            async void LoadOwners()
            {
                ownerHost.Children.Clear();
                if (gh == null) return;
                var owners = await ForgePublish.OwnersAsync();
                if (owners.Count == 0) { ownerHost.Children.Add(ToolsNote("Owners could not be listed because no account is signed in. A new repository needs one.", "B.Amber")); return; }
                if (_forgeOwner == null || !owners.Contains(_forgeOwner, StringComparer.OrdinalIgnoreCase)) _forgeOwner = owners[0];
                ownerHost.Children.Add(Choice("Owner of the new repository (default: your own account)", owners.Select(o => (o, o)), _forgeOwner, v => _forgeOwner = v, literalOptions: true));
            }

            if (gh != null)
            {
                accountsBox.Children.Add(new TextBlock { Text = "Accounts", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 6) });
                accountsBox.Children.Add(accountsStatus);
                accountsBox.Children.Add(search);
                accountsBox.Children.Add(accountRows);
                var acctBar = new WrapPanel();
                acctBar.Children.Add(Ui.Button("Add account", () => { ForgePublish.OpenGhInTerminal("auth login --hostname github.com --web", out var m); accountsStatus.Text = m; }, "Btn.Ghost", Icons.Plus));
                acctBar.Children.Add(Ui.Button("Refresh the list", () => { LoadAccounts(); LoadOwners(); }, "Btn.Ghost", Icons.Restart));
                accountsBox.Children.Add(acctBar);
                sp.Children.Add(accountsBox);
                LoadAccounts();
                LoadOwners();
            }

            // ---- other hosts: GitLab and Gitea/Forgejo
            var otherPanel = new StackPanel();
            var serverBox = Input("", 440, "The server address, for example https://git.example.org. Leave empty for gitlab.com. Gitea and Forgejo need an address.");
            AutomationProperties.SetName(serverBox, "Server address");
            var ownerBox = Input("", 260, "The group, organization or user that should own the new repository. Empty means the account that signs in.");
            AutomationProperties.SetName(ownerBox, "Owner of the new repository");
            var tokenBox = ModesPin("Personal access token (used once, never saved)");
            tokenBox.Width = 440; tokenBox.MaxLength = 300;
            tokenBox.ToolTip = "Only needed to create a new repository when the matching command-line tool is not signed in. It is kept in memory for this one action, sent only to the server named above, and cleared afterwards. It is never saved or logged.";
            var cliNote = ToolsNote("");
            otherPanel.Children.Add(ToolsLabel("Server address (empty means gitlab.com for GitLab)"));
            otherPanel.Children.Add(serverBox);
            otherPanel.Children.Add(ToolsLabel("Owner of a new repository (optional)"));
            otherPanel.Children.Add(ownerBox);
            otherPanel.Children.Add(cliNote);
            otherPanel.Children.Add(tokenBox);
            otherPanel.Children.Add(ToolsNote("The token is only used when you tick \"create a new repository\" and the command-line tool is missing or not signed in. Pushing an existing repository uses whatever sign-in Git already has on this computer."));
            sp.Children.Add(otherPanel);
            applyKind = () =>
            {
                bool gitHub = _forgeKind == ForgeKind.GitHub;
                otherPanel.Visibility = gitHub ? Visibility.Collapsed : Visibility.Visible;
                ghNote.Visibility = gitHub ? Visibility.Visible : Visibility.Collapsed;
                accountsBox.Visibility = gitHub ? Visibility.Visible : Visibility.Collapsed;
                ownerHost.Visibility = gitHub ? Visibility.Visible : Visibility.Collapsed;
                if (!gitHub)
                {
                    var tool = ForgeHosts.CliName(_forgeKind);
                    cliNote.Text = ForgeHosts.CliPath(_forgeKind) != null
                        ? "The " + tool + " command-line tool was found and is used first when it is signed in to this server."
                        : "The " + tool + " command-line tool is not installed, so creating a repository here needs a token. Publishing to an existing empty repository does not.";
                }
            };

            // ---- what to publish
            sp.Children.Add(new TextBlock { Text = "Folder to publish", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 8, 0, 6) });
            _forgeFolder ??= s.ForgeLastFolder;
            var folderBox = Input(_forgeFolder, 440, "The folder whose files are committed and published. Starts as the last folder you used.");
            AutomationProperties.SetName(folderBox, "Folder to publish");
            var nameBox = Input(string.IsNullOrWhiteSpace(_forgeFolder) ? "" : Path.GetFileName(_forgeFolder.TrimEnd('\\', '/')), 260, "The new repository's name on GitHub. Defaults to the folder name.");
            AutomationProperties.SetName(nameBox, "Repository name");
            var folderRow = new WrapPanel();
            folderRow.Children.Add(folderBox);
            folderRow.Children.Add(Ui.Button("Browse…", () =>
            {
                var dlg = new OpenFolderDialog { Title = "Choose the folder to publish" };
                if (dlg.ShowDialog() == true) { folderBox.Text = dlg.FolderName; if (string.IsNullOrWhiteSpace(nameBox.Text)) nameBox.Text = Path.GetFileName(dlg.FolderName.TrimEnd('\\', '/')); }
            }));
            if (!string.IsNullOrWhiteSpace(s.IpHistoryRepo) && Directory.Exists(s.IpHistoryRepo))
                folderRow.Children.Add(Ui.Button("Use the IP history repository", () => { folderBox.Text = s.IpHistoryRepo; nameBox.Text = Path.GetFileName(s.IpHistoryRepo.TrimEnd('\\', '/')); }, "Btn.Ghost", Icons.Git));
            sp.Children.Add(folderRow);
            if (string.IsNullOrWhiteSpace(s.IpHistoryRepo) || !Directory.Exists(s.IpHistoryRepo))
                sp.Children.Add(ToolsNote("The IP history repository has not been set up on this computer, so its shortcut is not shown."));
            else
                sp.Children.Add(ToolsNote("The IP history repository lists your addresses and when they changed. Keep it private unless you are sure you want it public.", "B.Amber"));

            sp.Children.Add(ownerHost);
            sp.Children.Add(ToolsLabel("Repository name"));
            sp.Children.Add(nameBox);
            sp.Children.Add(Choice("Visibility of a new repository (default private)", new[] { ("Private", true), ("Public", false) }, s.ForgePrivate, v => s.ForgePrivate = v));

            var createSwitch = false;
            sp.Children.Add(Switch("Create a new repository on GitHub", "Off by default, and you are asked to confirm before anything is created. Needs the GitHub CLI and a signed-in account. If the folder already has an origin remote, nothing new is created.",
                createSwitch, v => createSwitch = v));
            sp.Children.Add(ToolsLabel("Or push to an existing empty repository (paste its address)"));
            var remoteBox = Input("", 440, "For example https://github.com/name/repository.git. Used only when you are not creating a new repository.");
            AutomationProperties.SetName(remoteBox, "Existing repository address");
            sp.Children.Add(remoteBox);
            sp.Children.Add(Switch("Upload after committing", "On by default. Switch it off to commit on this computer only. Declining leaves the files unpublished: nothing is sent to GitHub until you run this again with it on.",
                s.ForgePush, v => s.ForgePush = v));

            var card = new OperationCard("Publish");
            card.SetSteps(ForgePublish.StepNames);
            Button publish = null;
            publish = Ui.Button("Publish", () =>
            {
                if (card.IsRunning) return;               // a keyboard submit lands here too
                var plan = new ForgePlan
                {
                    Folder = folderBox.Text.Trim().Trim('"'), Name = nameBox.Text.Trim(), Owner = _forgeOwner,
                    Private = s.ForgePrivate, CreateRepository = createSwitch, ExistingRemoteUrl = remoteBox.Text.Trim(), Push = s.ForgePush,
                    Kind = _forgeKind
                };
                if (_forgeKind != ForgeKind.GitHub)
                {
                    plan.Owner = ownerBox.Text.Trim();
                    plan.BaseUrl = serverBox.Text.Trim();
                    plan.Token = tokenBox.Password;
                }
                string problem = null;
                if (plan.Folder.Length == 0 || !Directory.Exists(plan.Folder)) problem = "Choose a folder that exists.";
                else if (plan.CreateRepository && !System.Text.RegularExpressions.Regex.IsMatch(plan.Name, @"^[A-Za-z0-9._-]{1,100}$")) problem = "A repository name uses letters, digits, dots, dashes and underscores only.";
                else if (plan.CreateRepository && plan.Kind == ForgeKind.GitHub && gh == null) problem = "Creating a repository needs the GitHub CLI, which is not installed.";
                else if (plan.CreateRepository && plan.Kind == ForgeKind.Gitea && plan.BaseUrl.Length == 0) problem = "Enter the address of your Gitea or Forgejo server.";
                else if (plan.CreateRepository && plan.Kind != ForgeKind.GitHub && ForgeHosts.NormaliseBase(plan.BaseUrl.Length == 0 ? "https://gitlab.com" : plan.BaseUrl) == null) problem = "The server address is not a valid web address.";
                else if (!plan.CreateRepository && plan.Push && plan.ExistingRemoteUrl.Length == 0 && plan.Kind != ForgeKind.GitHub) problem = "Paste the address of an existing empty repository, or tick \"create a new repository\".";
                if (problem != null) { ShowPublishProblem(card, problem); return; }
                s.ForgeLastFolder = plan.Folder; Settings.Save();

                if (plan.CreateRepository)
                {
                    var slug = (plan.Owner != null ? plan.Owner + "/" : "") + plan.Name;
                    var where = ForgeHosts.Display(plan.Kind);
                    var dlg = new ConfirmWindow($"Create {slug} on {where}?",
                        $"This creates a new {(plan.Private ? "private" : "PUBLIC")} repository named {slug} on {(plan.Kind == ForgeKind.GitHub ? "GitHub" : string.IsNullOrWhiteSpace(plan.BaseUrl) ? "gitlab.com" : plan.BaseUrl)} and then uploads the committed files of this folder.\n\n" +
                        (plan.Kind != ForgeKind.GitHub && !string.IsNullOrEmpty(plan.Token) ? "The access token you pasted is used for this one action only and is cleared afterwards.\n\n" : "") +
                        (plan.Private ? "Only you and people you invite can see it." : "A public repository can be seen by anyone on the internet. Check the folder has nothing you want to keep to yourself."),
                        "Create and publish", danger: false);
                    bool started = false;
                    dlg.Confirmed += _ => { started = true; StartPublish(card, plan); };
                    dlg.Closed += (_, _) => { if (!started) plan.Token = null; };
                    dlg.Show();
                }
                else StartPublish(card, plan);
                tokenBox.Clear();           // the plan holds the only remaining copy, and drops it when the action ends
            }, "Btn.Accent", Icons.Git);
            card.Guard(publish, folderBox, nameBox, remoteBox);
            var pubRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            pubRow.Children.Add(publish);
            sp.Children.Add(pubRow);
            sp.Children.Add(card);
            sp.Children.Add(ToolsNote("Forking is not offered. This copies your own folder into your own repository, which works on any host; a fork button would only make sense for someone else's project. The route taken is reported when it finishes."));
            applyKind();
            return SearchScope.SetItem(Card(sp), "publish github gitlab gitea forgejo repository push commit git account owner private public gh glab tea cli token server ip history");
        }

        private static TextBlock ToolsLabel(string text) =>
            new TextBlock { Text = text, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 6, 0, 6) };

        private static void ShowPublishProblem(OperationCard card, string text)
        {
            card.SetSteps(ForgePublish.StepNames);
            _ = card.RunAsync(_ => System.Threading.Tasks.Task.FromResult(OperationOutcome.Failure("Check the form", text)));
        }

        private void StartPublish(OperationCard card, ForgePlan plan)
        {
            card.SetSteps(ForgePublish.StepNames);
            _ = card.RunAsync(async run =>
            {
                ForgeResult r = await ForgePublish.PublishAsync(plan, (i, state, detail) =>
                {
                    var st = state switch { "running" => StepState.Running, "done" => StepState.Done, "skipped" => StepState.Skipped, _ => StepState.Failed };
                    run.Step(i, st, detail);
                    if (st == StepState.Running) run.Message(ForgePublish.StepNames[i] + "…");
                }, run.Token);
                return ToOutcome(r, plan);
            });
        }

        private OperationOutcome ToOutcome(ForgeResult r, ForgePlan plan)
        {
            var folder = plan.Folder;
            if (r.Ok)
            {
                var o = OperationOutcome.Success(r.Summary, r.Details);
                if (!string.IsNullOrEmpty(r.Url)) o.Recovery.Add(new RecoveryAction("Open on GitHub", () => ActionProvider.Shell(r.Url)));
                o.Recovery.Add(new RecoveryAction("Open the folder", () => ActionProvider.Shell(folder)));
                return o;
            }
            var recovery = new List<RecoveryAction>();
            switch (r.Failure)
            {
                case ForgeFailure.Auth:
                    recovery.Add(new RecoveryAction("Sign in again", () => ForgePublish.OpenGhInTerminal("auth login --hostname github.com --web", out _)));
                    break;
                case ForgeFailure.GhMissing:
                    recovery.Add(new RecoveryAction("Get the GitHub CLI", () => ActionProvider.Shell("https://cli.github.com")));
                    break;
                case ForgeFailure.GitMissing:
                    recovery.Add(new RecoveryAction("Get Git for Windows", () => ActionProvider.Shell("https://git-scm.com/download/win")));
                    break;
                case ForgeFailure.Identity:
                    recovery.Add(new RecoveryAction("Copy the commands", () => ClipboardWatcher.SetText("git config --global user.name \"Your Name\"\r\ngit config --global user.email \"you@example.com\"")));
                    break;
            }
            if (r.Failure == ForgeFailure.Rejected || r.Failure == ForgeFailure.Other || r.Failure == ForgeFailure.Auth || r.Failure == ForgeFailure.Network)
                recovery.Add(new RecoveryAction("Copy a prompt for a coding assistant", () => ClipboardWatcher.SetText(ForgePublish.AssistantPrompt(folder, r.Remote, r.Branch, r.Details))));
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                recovery.Add(new RecoveryAction("Open the folder", () => ActionProvider.Shell(folder)));
            return OperationOutcome.Failure(r.Summary, r.Details, recovery.ToArray());
        }

        // -------------------------------------------------------------- external editor

        private FrameworkElement ToolsEditorCard()
        {
            var s = Settings.Current;
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("External editor",
                "Choose the editor that opens your shortcut scripts and the files in clipboard history. Visual Studio Code opens a folder as a workspace, so its file tree is usable."));

            var detected = ExternalEditor.Detect();
            foreach (var e in detected)
            {
                var line = new TextBlock
                {
                    Text = (e.Found ? "✓ " : "✕ ") + e.Name + " — " + (e.Path != null ? e.Path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%") + " (" + e.Origin + ")" : e.Id == "default" ? e.Origin : "not found"),
                    Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res(e.Found ? "B.TextSoft" : "B.TextMute"), Margin = new Thickness(0, 1, 0, 1), TextWrapping = TextWrapping.Wrap
                };
                sp.Children.Add(line);
            }
            var result = ToolsNote("");

            if (detected.First(e => e.Id == "vscode").Path == null && detected.First(e => e.Id == "vscode-insiders").Path == null)
            {
                sp.Children.Add(ToolsNote("Visual Studio Code was not found in the usual folders or on PATH. A portable copy is not detected automatically: choose its Code.exe as a custom editor below.", "B.Amber"));
                sp.Children.Add(Ui.Button("Download Visual Studio Code", () => ActionProvider.Shell(ExternalEditor.VsCodeDownloadUrl), "Btn.Ghost", Icons.Globe));
            }

            sp.Children.Add(Choice("Preferred editor (default automatic: VS Code, then Notepad++, then the system default)",
                new[] { ("Automatic", "auto"), ("Visual Studio Code", "vscode"), ("Notepad++", "notepadpp"), ("System default", "default"), ("Custom program", "custom") },
                s.PreferredEditor ?? "auto", v => s.PreferredEditor = v));

            var customBox = Input(s.CustomEditorPath, 440, "The full path of the program to use when \"Custom program\" is chosen.");
            AutomationProperties.SetName(customBox, "Custom editor program");
            var customRow = new WrapPanel();
            customRow.Children.Add(customBox);
            customRow.Children.Add(Ui.Button("Browse…", () =>
            {
                var dlg = new OpenFileDialog { Title = "Choose an editor program", Filter = "Programs|*.exe;*.cmd;*.bat|All files|*.*" };
                if (dlg.ShowDialog() == true) customBox.Text = dlg.FileName;
            }));
            customRow.Children.Add(Ui.Button("Save custom program", () =>
            {
                s.CustomEditorPath = customBox.Text.Trim().Trim('"'); Settings.Save();
                result.Text = File.Exists(s.CustomEditorPath) ? "Saved. Choose \"Custom program\" above to use it." : "Saved, but that file does not exist, so it will be ignored until it does.";
                result.Foreground = Ui.Res(File.Exists(s.CustomEditorPath) ? "B.TextMute" : "B.Amber");
            }));
            sp.Children.Add(customRow);

            var test = new WrapPanel();
            test.Children.Add(Ui.Button("Open the scripts folder in the editor", () => Report(result, ExternalEditor.Open(Shortcuts.Folder, out var m), m), "Btn.Ghost", Icons.Code));
            test.Children.Add(Ui.Button("Open it in Visual Studio Code", () =>
            {
                bool ok = ExternalEditor.OpenInVsCode(Shortcuts.Folder, out var m, out var missing);
                Report(result, ok, m + (missing ? " Use the download button above." : ""));
            }, "Btn.Ghost", Icons.Code));
            sp.Children.Add(test);
            sp.Children.Add(result);
            sp.Children.Add(ToolsNote("Folders open as a workspace root in Visual Studio Code. If no editor can be found or started, the message says so instead of failing silently."));
            return SearchScope.SetItem(Card(sp), "editor vs code visual studio notepad++ external open edit scripts");
        }

        private static void Report(TextBlock target, bool ok, string message)
        {
            target.Text = message;
            target.Foreground = Ui.Res(ok ? "B.TextMute" : "B.Amber");
        }
    }
}
