using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public sealed class GhAccount
    {
        public string Host = "github.com";
        public string Login;
        public bool Active;
    }

    public enum ForgeFailure { None, GitMissing, GhMissing, Identity, NestedRepository, NothingToPublish, NoRemote, Auth, Network, Rejected, Cancelled, Other }

    public sealed class ForgePlan
    {
        public string Folder;
        public string Owner;
        public string Name;
        public bool Private = true;
        /// <summary>True only after the person has explicitly agreed to create a new repository.</summary>
        public bool CreateRepository;
        /// <summary>An already existing, empty remote to push to when no repository is created.</summary>
        public string ExistingRemoteUrl;
        /// <summary>False commits locally and uploads nothing.</summary>
        public bool Push = true;
    }

    public sealed class ForgeResult
    {
        public bool Ok;
        public string Summary;
        public string Details;
        public ForgeFailure Failure;
        public string Route;        // which route was taken and what it produced
        public string Remote;
        public string Branch;
        public string Url;
    }

    /// <summary>
    /// Publishes a folder to GitHub with the bundled MinGit, and with the GitHub CLI where it is installed. This app never
    /// reads, stores or logs a token: sign-in, accounts and credentials stay in the GitHub CLI's own credential store.
    /// </summary>
    public static class ForgePublish
    {
        public static readonly string[] StepNames =
        {
            "Check tools", "Prepare the repository", "Commit the files", "Choose the remote", "Upload"
        };

        // -------------------------------------------------------------- the GitHub CLI

        public static string GhPath()
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Select(d => d.Trim().Trim('"')).Where(d => d.Length > 0))
            {
                try { var p = Path.Combine(dir, "gh.exe"); if (File.Exists(p)) return p; } catch { }
            }
            foreach (var f in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
            {
                var p = Path.Combine(Environment.GetFolderPath(f), f == Environment.SpecialFolder.LocalApplicationData ? Path.Combine("Programs", "GitHub CLI") : "GitHub CLI", "gh.exe");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        public static async Task<List<GhAccount>> AccountsAsync()
        {
            var gh = GhPath();
            if (gh == null) return new List<GhAccount>();
            var r = await Task.Run(() => Run(gh, new[] { "auth", "status" }, Environment.CurrentDirectory, CancellationToken.None, 20000));
            return ParseAccounts(r.All);
        }

        /// <summary>Reads the human output of "gh auth status". Only account names are taken from it, never a token.</summary>
        public static List<GhAccount> ParseAccounts(string text)
        {
            var list = new List<GhAccount>();
            GhAccount cur = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                var m = Regex.Match(line, @"Logged in to (?<h>\S+) (?:account|as) (?<l>[A-Za-z0-9][A-Za-z0-9-]*)");
                if (m.Success) { cur = new GhAccount { Host = m.Groups["h"].Value, Login = m.Groups["l"].Value, Active = true }; list.Add(cur); continue; }
                var a = Regex.Match(line, @"Active account:\s*(true|false)", RegexOptions.IgnoreCase);
                if (a.Success && cur != null) cur.Active = a.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            // The older single-account output has no "Active account" line, so the one account is active.
            if (list.Count == 1) list[0].Active = true;
            return list;
        }

        /// <summary>The active account plus any organizations it belongs to: the places a repository can be created.</summary>
        public static async Task<List<string>> OwnersAsync()
        {
            var gh = GhPath();
            var owners = new List<string>();
            if (gh == null) return owners;
            await Task.Run(() =>
            {
                var me = Run(gh, new[] { "api", "user", "--jq", ".login" }, Environment.CurrentDirectory, CancellationToken.None, 20000);
                if (me.Code == 0 && me.Out.Trim().Length > 0) owners.Add(me.Out.Trim());
                var orgs = Run(gh, new[] { "api", "user/orgs", "--paginate", "--jq", ".[].login" }, Environment.CurrentDirectory, CancellationToken.None, 20000);
                if (orgs.Code == 0) owners.AddRange(orgs.Out.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            });
            return owners.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static Task<(int Code, string All)> SwitchAccountAsync(string login) =>
            Task.Run(() => { var r = Run(GhPath() ?? "gh", new[] { "auth", "switch", "--hostname", "github.com", "--user", login }, Environment.CurrentDirectory, CancellationToken.None, 20000); return (r.Code, r.All); });

        public static Task<(int Code, string All)> SignOutAsync(string login) =>
            Task.Run(() => { var r = Run(GhPath() ?? "gh", new[] { "auth", "logout", "--hostname", "github.com", "--user", login }, Environment.CurrentDirectory, CancellationToken.None, 20000); return (r.Code, r.All); });

        /// <summary>Runs a GitHub CLI sign-in command in a visible console, so the person completes it themselves.
        /// Examples: "auth login --hostname github.com --web" or "auth refresh --hostname github.com --user NAME".</summary>
        public static bool OpenGhInTerminal(string ghArguments, out string message)
        {
            var gh = GhPath();
            if (gh == null) { message = "The GitHub CLI (gh) is not installed. Install it from https://cli.github.com and try again."; return false; }
            try
            {
                var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = true };
                psi.ArgumentList.Add("/k");
                psi.ArgumentList.Add($"\"{gh}\" {ghArguments}");
                using (Process.Start(psi)) { }
                message = "A terminal window opened. Finish the sign-in there, then press Refresh here.";
                return true;
            }
            catch (Exception ex) { message = "Could not open a terminal: " + ex.Message; return false; }
        }

        // -------------------------------------------------------------- publishing

        /// <summary>Report callback: step index, then one of running, done, skipped, failed, and an optional detail.</summary>
        public static Task<ForgeResult> PublishAsync(ForgePlan plan, Action<int, string, string> report, CancellationToken ct) =>
            Task.Run(() => Publish(plan, report ?? ((i, s, d) => { }), ct));

        private static ForgeResult Publish(ForgePlan plan, Action<int, string, string> report, CancellationToken ct)
        {
            string folder = plan.Folder;
            ForgeResult Fail(int step, ForgeFailure kind, string summary, string details, string branch = null, string remote = null)
            {
                report(step, ct.IsCancellationRequested ? "failed" : "failed", summary);
                return new ForgeResult { Ok = false, Failure = ct.IsCancellationRequested ? ForgeFailure.Cancelled : kind, Summary = ct.IsCancellationRequested ? "Cancelled" : summary, Details = details, Branch = branch, Remote = remote };
            }

            try
            {
                // 0. tools
                report(0, "running", null);
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return Fail(0, ForgeFailure.Other, "The folder does not exist", folder ?? "(none chosen)");
                var git = GitPush.GitExe;
                var ver = Run(git, new[] { "--version" }, folder, ct, 20000);
                if (ver.Code != 0) return Fail(0, ForgeFailure.GitMissing, "Git is not available", "This build has no bundled Git and none was found on this computer. Install Git for Windows, then retry.\n" + ver.All);
                var gh = GhPath();
                if (plan.CreateRepository && gh == null)
                    return Fail(0, ForgeFailure.GhMissing, "Creating a repository needs the GitHub CLI", "The GitHub CLI (gh) was not found, so a new repository cannot be created from here. Install it from https://cli.github.com, or create an empty repository on GitHub yourself and paste its address instead.");
                report(0, "done", ver.Out.Trim() + (gh != null ? "; GitHub CLI found" : "; GitHub CLI not installed"));

                // 1. repository
                report(1, "running", null);
                var root = GitPush.FindRepoRoot(folder);
                if (root != null && !SamePath(root, folder))
                    return Fail(1, ForgeFailure.NestedRepository, "This folder is inside another repository", "It sits inside the repository at " + root + ". Choose that repository's root folder instead, so nothing outside the folder you meant is published.");
                if (root == null)
                {
                    var init = Run(git, new[] { "init", "-b", "main" }, folder, ct);
                    if (init.Code != 0) { init = Run(git, new[] { "init" }, folder, ct); if (init.Code == 0) Run(git, new[] { "checkout", "-b", "main" }, folder, ct); }
                    if (init.Code != 0) return Fail(1, ForgeFailure.Other, "Could not create a repository here", init.All);
                    report(1, "done", "created a new repository in the folder");
                }
                else report(1, "done", "using the existing repository");
                var branch = Run(git, new[] { "rev-parse", "--abbrev-ref", "HEAD" }, folder, ct).Out.Trim();
                if (string.IsNullOrEmpty(branch) || branch == "HEAD")
                    branch = Run(git, new[] { "symbolic-ref", "--short", "HEAD" }, folder, ct).Out.Trim();
                if (string.IsNullOrEmpty(branch)) branch = "main";

                // 2. commit
                report(2, "running", null);
                var add = Run(git, new[] { "add", "-A" }, folder, ct);
                if (add.Code != 0) return Fail(2, ForgeFailure.Other, "Staging the files failed", add.All, branch);
                var staged = Run(git, new[] { "diff", "--cached", "--name-only" }, folder, ct).Out.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                bool hasHead = Run(git, new[] { "rev-parse", "--verify", "HEAD" }, folder, ct).Code == 0;
                if (staged == 0 && !hasHead)
                    return Fail(2, ForgeFailure.NothingToPublish, "There is nothing to publish", "The folder has no files that Git can track (it is empty or everything is ignored).", branch);
                if (staged > 0)
                {
                    var name = Run(git, new[] { "config", "user.name" }, folder, ct).Out.Trim();
                    var mail = Run(git, new[] { "config", "user.email" }, folder, ct).Out.Trim();
                    if (name.Length == 0 || mail.Length == 0)
                        return Fail(2, ForgeFailure.Identity, "Git does not know who you are yet",
                            "Git needs a name and an email address to record the commit, and none is configured. This app will not invent one. Run these in a terminal, with your own details:\n" +
                            "git config --global user.name \"Your Name\"\ngit config --global user.email \"you@example.com\"", branch);
                    var template = string.IsNullOrWhiteSpace(Settings.Current.CommitTemplate) ? GitPush.SuggestedTemplate : Settings.Current.CommitTemplate;
                    var message = GitPush.Expand(template, folder, branch, staged);
                    var msgFile = Path.Combine(Path.GetTempPath(), $"mt-commit-{Guid.NewGuid():N}.txt");
                    File.WriteAllText(msgFile, message, new UTF8Encoding(false));
                    var commit = Run(git, new[] { "commit", "-F", msgFile }, folder, ct);
                    try { File.Delete(msgFile); } catch { }
                    if (commit.Code != 0) return Fail(2, ForgeFailure.Other, "The commit failed", commit.All, branch);
                    report(2, "done", $"committed {staged} file{(staged == 1 ? "" : "s")}");
                }
                else report(2, "skipped", "nothing new to commit");

                // 3. remote
                report(3, "running", null);
                var origin = Run(git, new[] { "remote", "get-url", "origin" }, folder, ct);
                string remote = origin.Code == 0 ? origin.Out.Trim() : null;
                string route;
                string url = null;
                if (remote != null)
                {
                    report(3, "skipped", "origin already points to " + remote + "; no repository was created");
                    route = "Used the existing remote " + remote + ".";
                }
                else if (plan.CreateRepository)
                {
                    var slug = (string.IsNullOrWhiteSpace(plan.Owner) ? "" : plan.Owner.Trim() + "/") + plan.Name.Trim();
                    var create = Run(gh, new[] { "repo", "create", slug, plan.Private ? "--private" : "--public", "--source", ".", "--remote", "origin" }, folder, ct);
                    if (create.Code != 0)
                    {
                        var kind = LooksLikeAuth(create.All) ? ForgeFailure.Auth : create.All.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0 ? ForgeFailure.Other : ForgeFailure.Other;
                        return Fail(3, kind, kind == ForgeFailure.Auth ? "GitHub did not accept your sign-in" : "The repository could not be created", create.All, branch);
                    }
                    remote = Run(git, new[] { "remote", "get-url", "origin" }, folder, ct).Out.Trim();
                    url = "https://github.com/" + slug;
                    report(3, "done", $"created {slug} ({(plan.Private ? "private" : "public")})");
                    route = $"Created a new {(plan.Private ? "private" : "public")} repository {slug} with the GitHub CLI.";
                }
                else if (!string.IsNullOrWhiteSpace(plan.ExistingRemoteUrl))
                {
                    var addRemote = Run(git, new[] { "remote", "add", "origin", plan.ExistingRemoteUrl.Trim() }, folder, ct);
                    if (addRemote.Code != 0) return Fail(3, ForgeFailure.Other, "Could not set the remote", addRemote.All, branch);
                    remote = plan.ExistingRemoteUrl.Trim();
                    report(3, "done", "remote set to " + remote);
                    route = "Pushed to an existing repository you named: " + remote + ".";
                }
                else if (plan.Push)
                {
                    return Fail(3, ForgeFailure.NoRemote, "No destination was chosen", "Tick \"create a new repository\" and agree to it, or paste the address of an existing empty repository.", branch);
                }
                else
                {
                    report(3, "skipped", "no remote needed because nothing is uploaded");
                    route = "Committed locally only.";
                }

                // 4. upload
                if (!plan.Push)
                {
                    report(4, "skipped", "you chose to commit without uploading, so nothing has left this computer");
                    return new ForgeResult { Ok = true, Summary = "Committed locally; nothing was uploaded", Route = route, Branch = branch, Remote = remote, Url = url, Details = "Run the publish again with uploading ticked when you are ready." };
                }
                report(4, "running", null);
                var pushArgs = new List<string>();
                if (gh != null)
                {
                    // Let the GitHub CLI answer credential requests; the token stays in its own store.
                    pushArgs.AddRange(new[] { "-c", "credential.helper=", "-c", "credential.helper=!\"" + gh.Replace('\\', '/') + "\" auth git-credential" });
                }
                pushArgs.AddRange(new[] { "push", "-u", "origin", branch });
                var push = Run(git, pushArgs, folder, ct, 600000);
                if (push.Code != 0)
                {
                    var kind = LooksLikeAuth(push.All) ? ForgeFailure.Auth
                        : Regex.IsMatch(push.All, "rejected|non-fast-forward|fetch first", RegexOptions.IgnoreCase) ? ForgeFailure.Rejected
                        : Regex.IsMatch(push.All, "resolve host|timed out|unable to access|connection", RegexOptions.IgnoreCase) ? ForgeFailure.Network
                        : ForgeFailure.Other;
                    var title = kind switch { ForgeFailure.Auth => "GitHub did not accept your sign-in", ForgeFailure.Rejected => "GitHub rejected the push", ForgeFailure.Network => "Could not reach GitHub", _ => "The upload failed" };
                    return Fail(4, kind, title, push.All, branch, remote);
                }
                report(4, "done", $"uploaded {branch}");
                url ??= remote != null && remote.StartsWith("https://github.com/") ? Regex.Replace(remote, @"\.git$", "") : null;
                return new ForgeResult { Ok = true, Summary = $"Published {branch}", Route = route, Branch = branch, Remote = remote, Url = url, Details = route + (url != null ? "\n" + url : "") };
            }
            catch (OperationCanceledException) { return new ForgeResult { Ok = false, Failure = ForgeFailure.Cancelled, Summary = "Cancelled", Details = "Stopped before finishing. Steps that already completed were left as they are." }; }
            catch (Exception ex)
            {
                Log.Warn("Forge publish failed: " + ex.Message);
                return new ForgeResult { Ok = false, Failure = ct.IsCancellationRequested ? ForgeFailure.Cancelled : ForgeFailure.Other, Summary = ct.IsCancellationRequested ? "Cancelled" : "Something went wrong", Details = ex.Message };
            }
        }

        private static bool LooksLikeAuth(string text) =>
            Regex.IsMatch(text ?? "", "authentication failed|could not read Username|terminal prompts disabled|Permission denied|403|401|not logged in|gh auth login|requires authentication|insufficient scope|Bad credentials|missing .* scope", RegexOptions.IgnoreCase);

        private static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>The hand-off prompt for a local coding assistant. It states the real situation and forbids the fixes that lose work.</summary>
        public static string AssistantPrompt(string folder, string remote, string branch, string error)
        {
            return "I tried to publish a Git repository and the upload failed. Please help me fix it without losing any work.\n\n" +
                   "Folder: " + folder + "\n" +
                   "Remote: " + (string.IsNullOrWhiteSpace(remote) ? "(none set)" : remote) + "\n" +
                   "Branch: " + (string.IsNullOrWhiteSpace(branch) ? "(unknown)" : branch) + "\n" +
                   "Reported error:\n" + (error ?? "").Trim() + "\n\n" +
                   "Rules:\n" +
                   "- Never force-push (no --force, no --force-with-lease, no +refspec).\n" +
                   "- Never rewrite, amend, squash, drop or reset existing commits.\n" +
                   "- Never switch or delete branches, and never discard uncommitted changes.\n" +
                   "- Explain the cause first. If the remote has commits I do not have, fetch and merge or rebase only after telling me what will change.\n";
        }

        // -------------------------------------------------------------- process runner

        private static (int Code, string Out, string All) Run(string exe, IEnumerable<string> args, string dir, CancellationToken ct, int timeoutMs = 120000)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var psi = new ProcessStartInfo(exe)
                {
                    WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                foreach (var a in args) psi.ArgumentList.Add(a);
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                psi.EnvironmentVariables["GCM_INTERACTIVE"] = "never";
                psi.EnvironmentVariables["GH_PROMPT_DISABLED"] = "1";
                using var p = Process.Start(psi);
                using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return (-1, "", exe + " timed out"); }
                p.WaitForExit();
                ct.ThrowIfCancellationRequested();
                return (p.ExitCode, o.Result, (o.Result + "\n" + e.Result).Trim());
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (-1, "", ex.Message); }
        }
    }
}
