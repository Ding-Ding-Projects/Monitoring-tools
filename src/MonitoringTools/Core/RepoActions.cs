using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MonitoringTools.Core
{
    /// <summary>
    /// What a copied folder offers: open its Git repository in Desktop Material (which adds it to its list if it is
    /// not there yet), or start a new repository in a folder that is not in one.
    /// </summary>
    public static class RepoActions
    {
        /// <summary>The top folder of the Git repository holding this folder, or null. A worktree's .git is a file.</summary>
        public static string RepoRoot(string folder)
        {
            try
            {
                for (var d = new DirectoryInfo(folder); d != null; d = d.Parent)
                {
                    var git = Path.Combine(d.FullName, ".git");
                    if (Directory.Exists(git) || File.Exists(git)) return d.FullName;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Test hook: the Desktop Material launcher to report, or "" for none. The app itself never sets it.</summary>
        internal static string DesktopMaterialForTest;

        /// <summary>
        /// Desktop Material's update-stable launcher, or null when it is not installed. It installs through Squirrel into
        /// %LOCALAPPDATA%\GitHubDesktop like GitHub Desktop does; GitHub Desktop is version 3, Desktop Material is
        /// version 4, so the newest app-* folder's version tells them apart.
        /// </summary>
        public static string DesktopMaterialExe()
        {
            if (DesktopMaterialForTest != null) return DesktopMaterialForTest.Length == 0 ? null : DesktopMaterialForTest;
            try
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktop");
                var stub = Path.Combine(root, "GitHubDesktop.exe");
                if (!File.Exists(stub)) return null;
                var newest = Directory.GetDirectories(root, "app-*")
                    .Select(d => (d, v: Version.TryParse(Path.GetFileName(d).Substring(4), out var v) ? v : null))
                    .Where(x => x.v != null).OrderByDescending(x => x.v).FirstOrDefault();
                return newest.v != null && newest.v.Major >= 4 ? stub : null;
            }
            catch { return null; }
        }

        /// <summary>Opens the repository in Desktop Material. Its --cli-open switch selects the repository, adding it to the
        /// list first when it is not there.</summary>
        public static void OpenInDesktopMaterial(string exe, string repo) =>
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, ArgumentList = { "--cli-open=" + repo } });

        private static readonly string[] NotRepos = { "settings", "orgs", "organizations", "marketplace", "sponsors", "topics", "features", "explore", "notifications", "login", "about", "pricing", "enterprise", "collections", "apps", "search", "users", "codespaces", "pulls", "issues", "new" };

        /// <summary>The owner and repository a GitHub web or clone address points at, or null. Deeper links (a file, a
        /// branch, an issue) still name their repository.</summary>
        public static (string Owner, string Name)? GitHubRepo(string url)
        {
            var s = (url ?? "").Trim();
            if (s.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase)) s = "https://" + s;
            string owner, name;
            var scp = System.Text.RegularExpressions.Regex.Match(s, @"^git@github\.com:(?<o>[^/\s]+)/(?<n>[^/\s]+?)(\.git)?/?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (scp.Success) { owner = scp.Groups["o"].Value; name = scp.Groups["n"].Value; }
            else
            {
                if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || !string.Equals(u.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                    || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp)) return null;
                // The first two path segments name the repository; deeper links (blob, tree, issues) still point at it.
                var parts = u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return null;
                owner = parts[0]; name = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
            }
            if (NotRepos.Contains(owner.ToLowerInvariant())) return null;
            if (!System.Text.RegularExpressions.Regex.IsMatch(owner, @"^[A-Za-z0-9-]{1,39}$") || !System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9._-]{1,100}$")) return null;
            return (owner, name);
        }

        /// <summary>Where Clone puts repositories: the chosen folder, or Documents\GitHub.</summary>
        public static string CloneFolder =>
            string.IsNullOrWhiteSpace(Settings.Current.CloneFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub")
                : Settings.Current.CloneFolder;

        /// <summary>Test hook: when set, clones run this instead of git or gh. The app itself never sets it.</summary>
        internal static Func<string, string, string, (int Code, string Output)> CloneForTest;

        /// <summary>Clones owner/name into parent\name. Uses the signed-in GitHub CLI when there is one (private repositories
        /// work), otherwise the bundled Git over HTTPS. A folder that already holds this repository is reported, not cloned again.</summary>
        public static (bool Ok, string Folder, string Message) Clone(string owner, string name, string parent)
        {
            var dest = Path.Combine(parent, name);
            if (Directory.Exists(dest))
            {
                if (RepoRoot(dest) == dest) return (true, dest, "already there");
                if (Directory.EnumerateFileSystemEntries(dest).Any()) return (false, dest, $"{dest} already exists and is not empty.");
            }
            Directory.CreateDirectory(parent);
            var url = $"https://github.com/{owner}/{name}.git";
            (int Code, string Output) r;
            if (CloneForTest != null) r = CloneForTest(owner + "/" + name, url, dest);
            else
            {
                var gh = ForgePublish.GhPath();
                var psi = new ProcessStartInfo(gh ?? GitPush.GitExe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = parent };
                if (gh != null) { psi.ArgumentList.Add("repo"); psi.ArgumentList.Add("clone"); psi.ArgumentList.Add(owner + "/" + name); psi.ArgumentList.Add(dest); }
                else { psi.ArgumentList.Add("clone"); psi.ArgumentList.Add(url); psi.ArgumentList.Add(dest); }
                psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
                using var p = Process.Start(psi);
                var outTask = p.StandardOutput.ReadToEndAsync(); var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(30 * 60 * 1000)) { try { p.Kill(true); } catch { } return (false, dest, "The clone took longer than 30 minutes and was stopped."); }
                r = (p.ExitCode, (outTask.Result + errTask.Result).Trim());
            }
            if (r.Code == 0 && RepoRoot(dest) == dest) return (true, dest, "cloned");
            return (false, dest, string.IsNullOrWhiteSpace(r.Output) ? "The clone failed." : r.Output);
        }

        /// <summary>Starts a new repository on the main branch with the bundled Git. Returns null on success, or Git's message.</summary>
        public static string Init(string folder)
        {
            if (RepoRoot(folder) != null) return "That folder is already inside a Git repository.";
            var r = GitPush.Git(folder, "init -b main");
            return r.Code == 0 ? null : (r.All ?? "").Trim();
        }
    }
}
