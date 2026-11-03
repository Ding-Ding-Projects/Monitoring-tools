using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public sealed class PushResult
    {
        public bool Ok;
        public string Summary;
        public string Detail;
        public string Repo;
        public string Branch;
        public string Message;
    }

    /// <summary>The ":push" command: stage everything, commit with the template, push the current branch.</summary>
    public static class GitPush
    {
        public static readonly (string Token, string Meaning)[] Snippets =
        {
            ("{date}", "2026-11-03"),
            ("{time}", "15:54"),
            ("{datetime}", "2026-11-03 15:54"),
            ("{weekday}", "Tuesday"),
            ("{branch}", "current branch"),
            ("{repo}", "folder name of the repository"),
            ("{files}", "number of changed files"),
            ("{user}", "Windows user name"),
        };

        public const string SuggestedTemplate = "Update {repo} on {date} at {time}";

        public static string FindRepoRoot(string path)
        {
            try
            {
                var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(dir))
                {
                    if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git"))) return dir;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }
            return null;
        }

        public static string Expand(string template, string repo, string branch, int files)
        {
            var now = DateTime.Now;
            return template
                .Replace("{datetime}", now.ToString("yyyy-MM-dd HH:mm"))
                .Replace("{date}", now.ToString("yyyy-MM-dd"))
                .Replace("{time}", now.ToString("HH:mm"))
                .Replace("{weekday}", now.DayOfWeek.ToString())
                .Replace("{branch}", branch ?? "")
                .Replace("{repo}", repo == null ? "" : Path.GetFileName(repo.TrimEnd('\\', '/')))
                .Replace("{files}", files.ToString())
                .Replace("{user}", Environment.UserName);
        }

        public static Task<PushResult> RunAsync(string path, Action<string> stage)
        {
            return Task.Run(() =>
            {
                var root = FindRepoRoot(path);
                if (root == null) return new PushResult { Ok = false, Summary = "Not inside a git repository", Detail = path };
                if (!HasGit()) return new PushResult { Ok = false, Summary = "Git is not installed", Detail = "Install Git for Windows, then try again.", Repo = root };

                var branch = Git(root, "rev-parse --abbrev-ref HEAD").Out.Trim();
                stage?.Invoke("Staging");
                var add = Git(root, "add -A");
                if (add.Code != 0) return Fail(root, branch, "Staging failed", add.All);

                var status = Git(root, "diff --cached --name-only").Out;
                int files = status.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                string message = null;
                if (files > 0)
                {
                    message = Expand(Settings.Current.CommitTemplate, root, branch, files);
                    stage?.Invoke("Committing");
                    var msgFile = Path.Combine(Path.GetTempPath(), $"mt-commit-{Guid.NewGuid():N}.txt");
                    File.WriteAllText(msgFile, message, new UTF8Encoding(false));
                    var commit = Git(root, $"commit -F \"{msgFile}\"");
                    try { File.Delete(msgFile); } catch { }
                    if (commit.Code != 0) return Fail(root, branch, "Commit failed", commit.All);
                }

                stage?.Invoke("Pushing");
                var upstream = Git(root, "rev-parse --abbrev-ref --symbolic-full-name @{u}");
                var push = upstream.Code == 0 ? Git(root, "push") : Git(root, $"push -u origin \"{branch}\"");
                if (push.Code != 0) return Fail(root, branch, "Push failed", push.All);

                return new PushResult
                {
                    Ok = true, Repo = root, Branch = branch, Message = message,
                    Summary = files > 0 ? $"Committed {files} file{(files == 1 ? "" : "s")} and pushed {branch}" : $"Nothing new to commit; pushed {branch}",
                    Detail = push.All.Trim()
                };
            });
        }

        private static PushResult Fail(string root, string branch, string what, string output) =>
            new PushResult { Ok = false, Repo = root, Branch = branch, Summary = what, Detail = output.Trim() };

        private static bool HasGit() => Git(Environment.CurrentDirectory, "--version").Code == 0;

        private static (int Code, string Out, string All) Git(string dir, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(120000)) { try { p.Kill(true); } catch { } return (-1, "", "git timed out"); }
                return (p.ExitCode, o.Result, (o.Result + "\n" + e.Result).Trim());
            }
            catch (Exception ex) { return (-1, "", ex.Message); }
        }
    }
}
