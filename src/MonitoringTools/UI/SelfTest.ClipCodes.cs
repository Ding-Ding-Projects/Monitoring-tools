using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using ZXing;
using ZXing.Common;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>Codes in copied pictures, folders that are or are not repositories, and Docker files under any name.</summary>
        private static void ClipCodeChecks(string dir)
        {
            BitmapSource Render(BarcodeFormat format, string text, int w, int h)
            {
                var writer = new ZXing.BarcodeWriterPixelData { Format = format, Options = new EncodingOptions { Width = w, Height = h, Margin = 10 } };
                var px = writer.Write(text);
                var bmp = BitmapSource.Create(px.Width, px.Height, 96, 96, PixelFormats.Bgra32, null, px.Pixels, px.Width * 4);
                bmp.Freeze();
                return bmp;
            }

            Check("clipboard codes: QR, Data Matrix, EAN-13 and Code 128 in a copied picture are read, shown and offered to copy", () =>
            {
                var cases = new (BarcodeFormat F, string Text, string Kind, string Label)[]
                {
                    (BarcodeFormat.QR_CODE, "https://example.com/menu", "QR code", "Copy QR text"),
                    (BarcodeFormat.DATA_MATRIX, "PART-4471-B", "Data Matrix code", "Copy code text"),
                    (BarcodeFormat.EAN_13, "4006381333931", "EAN-13 barcode", "Copy barcode text"),
                    (BarcodeFormat.CODE_128, "SHIP-2026-0042", "Code 128 barcode", "Copy barcode text"),
                };
                foreach (var c in cases)
                {
                    var img = Render(c.F, c.Text, c.F is BarcodeFormat.EAN_13 or BarcodeFormat.CODE_128 ? 420 : 300, c.F is BarcodeFormat.EAN_13 or BarcodeFormat.CODE_128 ? 160 : 300);
                    var found = CodeScanner.Scan(img);
                    var hit = found.FirstOrDefault(x => x.Text == c.Text);
                    if (hit == null) return $"{c.Kind} not read; got [{string.Join(", ", found.Select(x => x.Kind + "=" + x.Text))}]";
                    if (hit.Kind != c.Kind) return $"{c.Text} was called {hit.Kind}, not {c.Kind}";
                    var actions = ActionProvider.For(new ClipItem { Kind = ClipKind.Image, Image = img });
                    var copy = actions.FirstOrDefault(a => a.Label == c.Label);
                    if (copy == null) return $"no {c.Label} for the {c.Kind}; actions: {string.Join(", ", actions.Select(a => a.Label))}";
                    if (!copy.Primary) return $"{c.Label} is not the first choice";
                    if (c.Text.StartsWith("https://") && !actions.Any(a => a.Label == "Open link")) return "a link QR has no Open link";
                    if (!c.Text.StartsWith("https://") && actions.Any(a => a.Label == "Open link")) return $"{c.Kind} without a link offered Open link";
                }
                // a plain picture offers nothing extra, and a picture is only read once
                var blank = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, new byte[64 * 64 * 4], 64 * 4);
                if (CodeScanner.Scan(blank).Count != 0) return "a blank picture produced a code";
                if (!ReferenceEquals(CodeScanner.Scan(blank), CodeScanner.Scan(blank))) return "the same picture was read twice";
                // an authenticator QR never shows its secret in the preview
                if (!ActionProvider.Preview("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP").Contains("secret hidden")) return "an authenticator link preview shows its secret";
                return null;
            });

            Check("folders: a plain folder offers Start a Git repository, which makes it one; a repository offers Open in Desktop Material only when it is installed", () =>
            {
                var root = Path.Combine(dir, "repo-actions");
                if (Directory.Exists(root)) Directory.Delete(root, true);
                var plain = Path.Combine(root, "plain"); Directory.CreateDirectory(plain);
                ClipItem Folder(string p) => new ClipItem { Kind = ClipKind.Files, Files = new[] { p } };
                try
                {
                    RepoActions.DesktopMaterialForTest = Path.Combine(dir, "fake-desktop-material.exe");
                    var a1 = ActionProvider.For(Folder(plain));
                    var start = a1.FirstOrDefault(a => a.Label == "Start a Git repository");
                    if (start == null) return "no Start a Git repository; actions: " + string.Join(", ", a1.Select(a => a.Label));
                    if (a1.Any(a => a.Label == "Open in Desktop Material")) return "a plain folder offered Open in Desktop Material";
                    start.Run();
                    if (!Directory.Exists(Path.Combine(plain, ".git"))) return "git init did not create .git";
                    var head = File.ReadAllText(Path.Combine(plain, ".git", "HEAD")).Trim();
                    if (head != "ref: refs/heads/main") return "the new repository is not on main: " + head;
                    var inner = Path.Combine(plain, "src"); Directory.CreateDirectory(inner);
                    if (RepoActions.RepoRoot(inner) != plain) return "a folder inside the repository did not find its top folder";
                    var a2 = ActionProvider.For(Folder(inner));
                    if (!a2.Any(a => a.Label == "Open in Desktop Material")) return "a repository folder did not offer Open in Desktop Material";
                    if (a2.Any(a => a.Label == "Start a Git repository")) return "a repository offered to start another one";
                    if (RepoActions.Init(inner) == null) return "init inside a repository was not refused";
                    RepoActions.DesktopMaterialForTest = "";
                    if (ActionProvider.For(Folder(inner)).Any(a => a.Label == "Open in Desktop Material")) return "Open in Desktop Material offered without Desktop Material installed";
                    return null;
                }
                finally { RepoActions.DesktopMaterialForTest = null; foreach (var t in App.Toasts.LiveToasts.ToList()) { try { t.Close(); } catch { } } }
            });

            Check("github links: a repository link offers Clone and Clone to…, other GitHub pages do not, and cloning twice reports the existing copy", () =>
            {
                foreach (var (link, want) in new (string, string)[]
                {
                    ("https://github.com/Ding-Ding-Projects/Monitoring-tools", "Ding-Ding-Projects/Monitoring-tools"),
                    ("https://github.com/Ding-Ding-Projects/Monitoring-tools/blob/main/README.md", "Ding-Ding-Projects/Monitoring-tools"),
                    ("git@github.com:octo-org/hello.world.git", "octo-org/hello.world"),
                    ("github.com/octo/repo", "octo/repo"),
                    ("https://github.com/settings/profile", null), ("https://github.com/octo", null), ("https://gitlab.com/a/b", null),
                })
                {
                    var r = RepoActions.GitHubRepo(link);
                    var got = r == null ? null : r.Value.Owner + "/" + r.Value.Name;
                    if (got != want) return $"{link}: {got ?? "none"}, expected {want ?? "none"}";
                }
                var acts = ActionProvider.For(new ClipItem { Kind = ClipKind.Url, Text = "https://github.com/octo/repo" });
                if (!acts.Any(a => a.Label.StartsWith("Clone to ") && a.Label != "Clone to…") || !acts.Any(a => a.Label == "Clone to…")) return "no clone buttons: " + string.Join(", ", acts.Select(a => a.Label));
                if (ActionProvider.For(new ClipItem { Kind = ClipKind.Url, Text = "https://example.com/a/b" }).Any(a => a.Label.StartsWith("Clone"))) return "a non-GitHub link offered Clone";

                // clone a local repository through the same code path
                var src = Path.Combine(dir, "clone-src"); var parent = Path.Combine(dir, "clone-into");
                foreach (var p in new[] { src, parent }) if (Directory.Exists(p)) { foreach (var f in Directory.GetFiles(p, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(p, true); }
                Directory.CreateDirectory(src);
                File.WriteAllText(Path.Combine(src, "README.md"), "clone me");
                foreach (var g in new[] { "init -b main", "-c user.name=t -c user.email=t@example.com add README.md", "-c user.name=t -c user.email=t@example.com commit -m first" })
                    if (GitPush.Git(src, g).Code != 0) return "could not make the source repository: " + g;
                RepoActions.CloneForTest = (slug, url, dest) => { var r = GitPush.Git(parent, $"clone \"{src}\" \"{dest}\""); return (r.Code, r.All); };
                try
                {
                    var first = RepoActions.Clone("octo", "repo", parent);
                    if (!first.Ok || first.Message != "cloned" || !File.Exists(Path.Combine(parent, "repo", "README.md"))) return "first clone: " + first.Message;
                    var second = RepoActions.Clone("octo", "repo", parent);
                    if (!second.Ok || second.Message != "already there") return "second clone: " + second.Message;
                    Directory.CreateDirectory(Path.Combine(parent, "busy")); File.WriteAllText(Path.Combine(parent, "busy", "x.txt"), "x");
                    var busy = RepoActions.Clone("octo", "busy", parent);
                    if (busy.Ok || !busy.Message.Contains("not empty")) return "a non-empty folder was not refused: " + busy.Message;
                }
                finally { RepoActions.CloneForTest = null; }
                return null;
            });

            Check("docker: Dockerfiles and Compose files are recognised under any name by what they say, and look-alikes are not", () =>
            {
                var d = Path.Combine(dir, "docker-names"); Directory.CreateDirectory(d);
                string W(string name, string text) { var p = Path.Combine(d, name); File.WriteAllText(p, text); return p; }
                var cases = new (string Name, string Text, DockerFileKind Want)[]
                {
                    ("Dockerfile_prod", "# syntax=docker/dockerfile:1\nARG BASE=alpine\nFROM ${BASE}:3.20\nRUN apk add --no-cache curl \\\n    git\nCOPY . /app\nCMD [\"sh\"]\n", DockerFileKind.Dockerfile),
                    ("Containerfile", "FROM scratch\n", DockerFileKind.Dockerfile),
                    ("build.docker", "FROM nginx:1.27\nCOPY site /usr/share/nginx/html\n", DockerFileKind.Dockerfile),
                    ("stack.yml", "name: shop\nservices:\n  web:\n    image: nginx:1.27\n    ports: [\"8080:80\"]\n", DockerFileKind.Compose),
                    ("docker-compose-dev.yml", "services:\n  api:\n    build: ./api\n", DockerFileKind.Compose),
                    ("compose.override.yaml", "services:\n  api:\n    environment: [DEBUG=1]\n", DockerFileKind.Compose),
                    ("ci.yml", "on: push\njobs:\n  test:\n    services:\n      db:\n        image: postgres\n", DockerFileKind.None),
                    ("deploy.yaml", "apiVersion: v1\nkind: Pod\nspec:\n  containers:\n    - image: nginx\n", DockerFileKind.None),
                    ("notes", "FROM the start of the week we\nwill try a new approach to the\nmeeting schedule and see.\n", DockerFileKind.None),
                    ("config.yml", "services:\n  - name: a\n", DockerFileKind.None),
                };
                foreach (var c in cases)
                {
                    var got = DockerActions.Detect(W(c.Name, c.Text));
                    if (got != c.Want) return $"{c.Name}: {got}, expected {c.Want}";
                }
                if (!DockerActions.For(Path.Combine(d, "stack.yml")).Any(a => a.Label == "Compose up")) return "stack.yml offers no Compose up";
                if (!DockerActions.For(Path.Combine(d, "Dockerfile_prod")).Any(a => a.Label == "Build image")) return "Dockerfile_prod offers no Build image";
                return null;
            });
        }
    }
}
