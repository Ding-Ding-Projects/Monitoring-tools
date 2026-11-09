using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>The clipboard history repository with the real Git: a commit per copy, no limit, secrets kept out,
        /// edits as versions, pins and removals as commits, the History manager, and the rewrite that forgets removed copies.
        /// Run on their own by the build (MT_ONLY=cliprepo).</summary>
        private static async Task ClipRepoChecksAsync(string dir)
        {
            var s = Settings.Current;
            var saved = (s.ClipRepoEnabled, s.ClipRepoPaused, s.ClipRepoKeepSecrets, s.ClipRepoPath, s.HistorySize);
            s.ClipRepoEnabled = true; s.ClipRepoPaused = false; s.ClipRepoKeepSecrets = false; s.HistorySize = 50;
            s.ClipRepoPath = Path.Combine(dir, "clipboard-history");
            ClipRepo.ForceForTest = true;
            ClipRepo.Reload();
            string repo = ClipRepo.Dir;
            int Commits() { var r = GitPush.Git(repo, "rev-list --count HEAD"); return r.Code == 0 && int.TryParse(r.Out.Trim(), out var n) ? n : -1; }
            ClipItem alpha = null;
            try
            {
                Check("cliprepo: each copy is one commit, and a secret never reaches the repository", () =>
                {
                    alpha = ClipItem.FromText("alpha first copy for the repository");
                    App.History.Add(alpha);
                    App.History.Add(ClipItem.FromText("https://example.com/repository-check"));
                    var secret = ClipItem.FromText("hunter2-not-for-the-repository"); secret.Sensitive = true;
                    App.History.Add(secret);
                    if (!ClipRepo.Drain()) return "the commits did not finish: " + ClipRepo.LastError;
                    if (ClipRepo.LastError != null) return ClipRepo.LastError;
                    if (!Directory.Exists(Path.Combine(repo, ".git"))) return "no Git repository at " + repo;
                    if (Commits() != 3) return $"{Commits()} commits, expected 3 (the start and two copies)";
                    var all = ClipRepo.All();
                    if (all.Count != 2) return all.Count + " entries";
                    if (all.Any(e => e.ShownPreview.Contains("hunter2"))) return "the secret is in the index";
                    var grep = GitPush.Git(repo, "log --all -p");
                    if (grep.Out.Contains("hunter2")) return "the secret is in a commit";
                    var msgs = GitPush.Git(repo, "log --format=%s").Out;
                    if (msgs.Contains("alpha first")) return "a commit message holds the copied text: " + msgs;
                    var e0 = all.Last();
                    if (!File.Exists(Path.Combine(repo, e0.File.Replace('/', Path.DirectorySeparatorChar)))) return "no file for " + e0.File;
                    if (ClipRepo.ReadText(e0) != "alpha first copy for the repository") return "read back: " + ClipRepo.ReadText(e0);
                    return null;
                });

                Check("cliprepo: an edit is a new version of the same copy, and the old text is one commit back", () =>
                {
                    alpha.Text = "alpha edited in place";
                    App.History.Touch(alpha);
                    if (!ClipRepo.Drain()) return "the edit did not commit";
                    var e = ClipRepo.Find(alpha.RepoId);
                    if (e == null) return "the copy lost its entry";
                    if (e.Edits != 1) return "edits " + e.Edits;
                    var v = ClipRepo.Versions(e);
                    if (v.Count != 2) return v.Count + " versions";
                    var old = ClipRepo.TextAt(e, v[1].Sha);
                    if (old != "alpha first copy for the repository") return "the older version reads " + old;
                    if (ClipRepo.All().Count != 2) return "an edit added an entry";
                    return null;
                });

                Check("cliprepo: there is no limit: more copies than the memory size are all kept", () =>
                {
                    for (int i = 0; i < 60; i++) App.History.Add(ClipItem.FromText($"bulk copy number {i:000} for the unlimited check"));
                    if (!ClipRepo.Drain(120000)) return "the bulk commits did not finish";
                    if (App.History.Items.Count > 50) return "memory kept " + App.History.Items.Count;
                    var n = ClipRepo.All().Count;
                    if (n != 62) return n + " entries in the repository, expected 62";
                    if (Commits() != 64) return Commits() + " commits, expected 64";
                    return null;
                });

                Check("cliprepo: pins and removals are commits, and removing takes the file out of the tree", () =>
                {
                    var before = Commits();
                    var e = ClipRepo.Find(alpha.RepoId);
                    ClipRepo.SetPinned(new[] { e }, true);
                    var url = ClipRepo.All().First(x => x.Kind == "url");
                    ClipRepo.Remove(new[] { url });
                    if (!ClipRepo.Drain()) return "did not finish";
                    if (Commits() != before + 2) return $"{Commits() - before} commits for a pin and a removal";
                    if (File.Exists(Path.Combine(repo, url.File.Replace('/', Path.DirectorySeparatorChar)))) return "the removed file is still there";
                    ClipRepo.Reload();
                    if (!ClipRepo.Find(alpha.RepoId).Pinned) return "the pin was not kept in the index";
                    if (ClipRepo.All().Any(x => x.Id == url.Id)) return "the removed copy is still listed";
                    return null;
                });

                await CheckAsync("cliprepo: the History page manages every copy: status, filters, search and paging", async () =>
                {
                    var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                    try
                    {
                        cc.Show(); await Task.Delay(700);
                        cc.Navigate("history", animate: false); await Task.Delay(800);
                        var t = TextOf(cc);
                        foreach (var want in new[] { "Recording every copy", "61 copies", "All copies", "alpha edited in place", "pinned", "Latest commits" })
                            if (!t.Contains(want)) return "the page lacks \"" + want + "\"";
                        if (!Press(cc, "Pinned only")) return "no Pinned only filter";
                        await Task.Delay(400);
                        t = TextOf(cc);
                        if (!t.Contains("1 of 61 copies")) return "Pinned only shows: " + t.Substring(0, Math.Min(300, t.Length));
                        if (!Press(cc, "Pinned only")) return "the Pinned only filter could not be turned off";
                        await Task.Delay(300);
                        return null;
                    }
                    finally { cc.Close(); }
                });

                Check("cliprepo: Forget removed copies for good leaves one commit and no trace of removed copies", () =>
                {
                    var err = ClipRepo.ForgetRemoved();
                    if (err != null) return err;
                    if (Commits() != 1) return Commits() + " commits after the rewrite";
                    if (GitPush.Git(repo, "log --all -p").Out.Contains("repository-check")) return "the removed copy is still in Git";
                    if (ClipRepo.All().Count != 61) return ClipRepo.All().Count + " entries after the rewrite";
                    return null;
                });
            }
            finally
            {
                ClipRepo.Drain();
                (s.ClipRepoEnabled, s.ClipRepoPaused, s.ClipRepoKeepSecrets, s.ClipRepoPath, s.HistorySize) = saved;
                ClipRepo.ForceForTest = false;
                ClipRepo.Reload();
            }
        }
    }
}
