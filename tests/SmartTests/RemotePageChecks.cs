using System;
using System.IO;
using System.Linq;
using MonitoringTools.Core;

namespace SmartTests
{
    public static partial class Program
    {
        /// <summary>Remote pages: which web page each kind of remote address opens, what the service is called, how a
        /// repository's configuration is read, and that a credential never survives.</summary>
        private static void RemotePageChecks()
        {
            (string Remote, string Page)[] pages =
            {
                ("https://github.com/octocat/Hello-World.git", "https://github.com/octocat/Hello-World"),
                ("https://github.com/octocat/Hello-World.git/", "https://github.com/octocat/Hello-World"),
                ("git@github.com:octocat/Hello-World.git", "https://github.com/octocat/Hello-World"),
                ("git@github.com:/octocat/Hello-World.git", "https://github.com/octocat/Hello-World"),
                ("ssh://git@github.com/octocat/Hello-World.git", "https://github.com/octocat/Hello-World"),
                ("git://github.com/octocat/Hello-World.git", "https://github.com/octocat/Hello-World"),
                ("git+ssh://git@github.com/octocat/Hello-World", "https://github.com/octocat/Hello-World"),
                ("https://gitlab.com/group/subgroup/project.git", "https://gitlab.com/group/subgroup/project"),
                ("git@gitlab.com:group/subgroup/project.git", "https://gitlab.com/group/subgroup/project"),
                ("ssh://git@gitlab.example.com:2222/team/app.git", "https://gitlab.example.com/team/app"),
                ("git@bitbucket.org:team/repo.git", "https://bitbucket.org/team/repo"),
                ("https://codeberg.org/forgejo/forgejo.git", "https://codeberg.org/forgejo/forgejo"),
                ("http://203.0.113.5:3000/me/notes.git", "http://203.0.113.5:3000/me/notes"),
                ("https://git.example.com:8443/me/notes", "https://git.example.com:8443/me/notes"),
                ("git@ssh.dev.azure.com:v3/contoso/Web%20Site/portal", "https://dev.azure.com/contoso/Web%20Site/_git/portal"),
                ("https://contoso@dev.azure.com/contoso/Web%20Site/_git/portal", "https://dev.azure.com/contoso/Web%20Site/_git/portal"),
                ("contoso@vs-ssh.visualstudio.com:v3/contoso/Site/portal", "https://contoso.visualstudio.com/Site/_git/portal"),
                ("https://git-codecommit.eu-west-1.amazonaws.com/v1/repos/billing", "https://eu-west-1.console.aws.amazon.com/codesuite/codecommit/repositories/billing/browse?region=eu-west-1"),
                ("https://bitbucket.example.com/scm/plat/api.git", "https://bitbucket.example.com/projects/PLAT/repos/api"),
                ("https://bitbucket.example.com/scm/~ana/dotfiles.git", "https://bitbucket.example.com/users/ana/repos/dotfiles"),
                ("ssh://git@bitbucket.example.com:7999/plat/api.git", "https://bitbucket.example.com/projects/PLAT/repos/api"),
                ("https://github.com/scm/owner/repo", "https://github.com/scm/owner/repo"),
                ("https://github.com/octo%20cat/hello", "https://github.com/octo%20cat/hello"),
            };
            foreach (var (remote, page) in pages)
                Check("remote page for " + remote, RemotePages.WebPage(remote) == page, RemotePages.WebPage(remote) ?? "null");

            foreach (var secret in new[]
            {
                "https://octocat:ghp_secret@github.com/octocat/private.git",
                "https://x-access-token:secret@github.com/octocat/private.git",
                "https://oauth2:secret@gitlab.com/team/private.git?private_token=secret#frag",
                "ssh://deploy:secret@git.example.com/team/private.git",
            })
            {
                var page = RemotePages.WebPage(secret);
                Check("remote page drops credentials, queries and fragments: " + secret, page != null && !page.Contains("secret") && !page.Contains("@") && !page.Contains("?") && !page.Contains("#"), page ?? "null");
            }

            foreach (var none in new[] { "", "   ", "/srv/git/project.git", "./relative/clone", "../sibling", @"C:\repos\project", "C:/repos/project", @"\\server\share\project", "//server/share/project", "file:///srv/git/project.git", "codecommit::eu-west-1://billing", "persistent-https::https://example.com/repo", "ftp://example.com/repo.git", "https://github.com/", "git@github.com:", "not a url" })
                Check("no remote page for '" + none + "'", RemotePages.WebPage(none) == null, RemotePages.WebPage(none) ?? "null");

            (string Page, string Host)[] hosts =
            {
                ("https://github.com/a/b", "GitHub"), ("https://org.ghe.com/a/b", "GitHub"), ("https://github.example.com/a/b", "GitHub"),
                ("https://gitlab.com/a/b", "GitLab"), ("https://gitlab.example.com/a/b", "GitLab"),
                ("https://bitbucket.org/a/b", "Bitbucket"), ("https://bitbucket.example.com/projects/A/repos/b", "Bitbucket"),
                ("https://dev.azure.com/o/p/_git/r", "Azure DevOps"), ("https://contoso.visualstudio.com/p/_git/r", "Azure DevOps"),
                ("https://codeberg.org/a/b", "Codeberg"), ("https://gitea.com/a/b", "Gitea"), ("https://gitea.example.com/a/b", "Gitea"),
                ("https://git.sr.ht/~a/b", "SourceHut"), ("https://eu-west-1.console.aws.amazon.com/codesuite", "AWS CodeCommit"),
                ("https://huggingface.co/a/b", "Hugging Face"), ("https://git.example.com/a/b", "git.example.com"), ("http://203.0.113.5:3000/a/b", "203.0.113.5:3000"),
            };
            foreach (var (page, host) in hosts)
                Check("remote host name for " + page, RemotePages.HostName(page) == host, RemotePages.HostName(page));

            // A repository's own configuration: quoted subsections, comments, a credential, a local remote, origin not first,
            // the old [remote.name] form and an insteadOf rewrite.
            var dir = Path.Combine(Path.GetTempPath(), "mt-remote-pages-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, ".git"));
                File.WriteAllText(Path.Combine(dir, ".git", "config"), string.Join("\n",
                    "[core]", "\tbare = false",
                    "[remote \"upstream\"]", "\turl = https://gitlab.com/team/app.git ; the team's copy", "\tfetch = +refs/heads/*:refs/remotes/upstream/*",
                    "[remote \"backup\"]", "\turl = /mnt/backup/app.git",
                    "[remote \"origin\"]", "\turl = \"https://me:ghp_secret@github.com/me/app.git\"",
                    "[remote.mirror]", "\turl = gh:me/app-mirror",
                    "[url \"https://github.com/\"]", "\tinsteadOf = gh:"));
                var found = RemotePages.For(dir);
                Check("remote pages list origin first, then the configured order, without local remotes",
                    string.Join(",", found.Select(p => p.Remote)) == "origin,upstream,mirror", string.Join(",", found.Select(p => p.Remote)));
                Check("remote pages read a quoted url and drop its credential", found.FirstOrDefault()?.Url == "https://github.com/me/app" && found.All(p => !p.Url.Contains("secret") && !p.Shown.Contains("secret")), found.FirstOrDefault()?.Url ?? "none");
                Check("remote pages drop a trailing comment", found.Any(p => p.Remote == "upstream" && p.Url == "https://gitlab.com/team/app" && p.Host == "GitLab"));
                Check("remote pages apply insteadOf", found.Any(p => p.Remote == "mirror" && p.Url == "https://github.com/me/app-mirror"), string.Join(" ", found.Select(p => p.Url)));
                Check("remote pages show the page without its scheme", found.FirstOrDefault()?.Shown == "github.com/me/app", found.FirstOrDefault()?.Shown ?? "none");

                // A linked worktree: .git is a file naming its git directory, whose commondir leads to the main repository.
                var wt = Path.Combine(dir, "wt");
                var wtGit = Path.Combine(dir, ".git", "worktrees", "wt");
                Directory.CreateDirectory(wt); Directory.CreateDirectory(wtGit);
                File.WriteAllText(Path.Combine(wt, ".git"), "gitdir: " + wtGit + "\n");
                File.WriteAllText(Path.Combine(wtGit, "commondir"), "../..\n");
                Check("remote pages follow a linked worktree to the main repository's remotes", RemotePages.For(wt).Count == 3, RemotePages.For(wt).Count.ToString());

                var plain = Path.Combine(dir, "plain");
                Directory.CreateDirectory(plain);
                Check("remote pages are empty for a folder without a repository", RemotePages.For(plain).Count == 0);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
