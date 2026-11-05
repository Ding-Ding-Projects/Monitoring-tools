using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public enum DockerFileKind { None, Compose, Dockerfile }

    public sealed class DockerAction
    {
        public string Label;
        public string Explain;
        public string Args;           // docker CLI arguments
        public bool InTerminal;       // long-running output (logs) opens a terminal instead
        public bool Primary;
    }

    public static class DockerActions
    {
        private static readonly Regex ComposeName = new Regex(@"^(docker-)?compose(\.[\w-]+)?\.ya?ml$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static DockerFileKind Detect(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return DockerFileKind.None;
            var name = Path.GetFileName(path);
            if (ComposeName.IsMatch(name)) return DockerFileKind.Compose;
            if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Containerfile", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".Dockerfile", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase)) return DockerFileKind.Dockerfile;
            // Any other name: read the start of the file. Names vary (Dockerfile_prod, stack.yml, docker-compose-dev.yml).
            return Sniff(path);
        }

        private static readonly Regex DockerInstruction = new Regex(@"^(FROM|ARG|RUN|CMD|LABEL|EXPOSE|ENV|ADD|COPY|ENTRYPOINT|VOLUME|USER|WORKDIR|ONBUILD|STOPSIGNAL|HEALTHCHECK|SHELL|MAINTAINER)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ComposeService = new Regex(@"^[ \t]+(image|build)[ \t]*:", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>Recognises a Dockerfile or a Compose file by what it says. Only small text files are read.</summary>
        internal static DockerFileKind Sniff(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length == 0 || info.Length > 512 * 1024) return DockerFileKind.None;
                var ext = info.Extension.ToLowerInvariant();
                if (ext is ".exe" or ".dll" or ".png" or ".jpg" or ".zip" or ".json" or ".md" or ".cs" or ".js" or ".ts" or ".py") return DockerFileKind.None;
                string text;
                using (var r = new StreamReader(path, true)) { var buf = new char[65536]; int n = r.ReadBlock(buf, 0, buf.Length); text = new string(buf, 0, n); }
                if (text.IndexOf('\0') >= 0) return DockerFileKind.None;

                if (ext is ".yml" or ".yaml" or "")
                {
                    // Compose: a top-level services: key with entries that have an image or a build, and none of the keys
                    // that make it a workflow or a Kubernetes manifest instead.
                    bool services = Regex.IsMatch(text, @"^services[ \t]*:", RegexOptions.Multiline);
                    bool other = Regex.IsMatch(text, @"^(jobs|apiVersion|kind|steps|stages)[ \t]*:", RegexOptions.Multiline);
                    if (services && !other && ComposeService.IsMatch(text)) return DockerFileKind.Compose;
                    if (ext != "") return DockerFileKind.None;
                }

                // Dockerfile: the first instruction (after comments, blank lines and parser directives) is FROM, or ARG
                // lines followed by FROM, and nearly every instruction line is a Dockerfile instruction.
                var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
                if (lines.Count == 0) return DockerFileKind.None;
                int i = 0;
                while (i < lines.Count && lines[i].StartsWith("ARG ", StringComparison.OrdinalIgnoreCase)) i++;
                if (i >= lines.Count || !lines[i].StartsWith("FROM ", StringComparison.OrdinalIgnoreCase)) return DockerFileKind.None;
                // continuation lines (after a trailing backslash) are not instructions
                int instructions = 0, starts = 0; bool cont = false;
                foreach (var l in lines)
                {
                    if (!cont) { starts++; if (DockerInstruction.IsMatch(l)) instructions++; }
                    cont = l.EndsWith("\\");
                }
                return instructions >= Math.Max(1, starts * 0.8) ? DockerFileKind.Dockerfile : DockerFileKind.None;
            }
            catch { return DockerFileKind.None; }
        }

        public static string ImageNameFor(string dockerfile)
        {
            var dir = Path.GetFileName(Path.GetDirectoryName(dockerfile)) ?? "image";
            var clean = Regex.Replace(dir.ToLowerInvariant(), "[^a-z0-9._-]+", "-").Trim('-', '.', '_');
            return (clean.Length == 0 ? "image" : clean) + ":latest";
        }

        public static List<DockerAction> For(string path)
        {
            var list = new List<DockerAction>();
            var q = $"\"{path}\"";
            switch (Detect(path))
            {
                case DockerFileKind.Compose:
                    list.Add(new DockerAction { Label = "Compose up", Primary = true, Args = $"compose -f {q} up -d --build",
                        Explain = "Builds what needs building and starts every service in the background." });
                    list.Add(new DockerAction { Label = "Down", Args = $"compose -f {q} down",
                        Explain = "Stops and removes this project's containers and networks. Volumes stay." });
                    list.Add(new DockerAction { Label = "Pull", Args = $"compose -f {q} pull",
                        Explain = "Downloads newer images for the services without starting them." });
                    list.Add(new DockerAction { Label = "Restart", Args = $"compose -f {q} restart",
                        Explain = "Restarts the running services in place." });
                    list.Add(new DockerAction { Label = "Logs", Args = $"compose -f {q} logs -f --tail 200", InTerminal = true,
                        Explain = "Opens a terminal that follows the live logs." });
                    break;
                case DockerFileKind.Dockerfile:
                    var image = ImageNameFor(path);
                    var ctx = $"\"{Path.GetDirectoryName(path)}\"";
                    list.Add(new DockerAction { Label = "Build image", Primary = true, Args = $"build -t {image} -f {q} {ctx}",
                        Explain = $"Builds the image as {image} using the file's folder as the build context." });
                    list.Add(new DockerAction { Label = "Build and run", Args = $"build -t {image} -f {q} {ctx}", InTerminal = true,
                        Explain = "Builds, then runs it in a terminal and removes the container when it stops." });
                    break;
            }
            return list;
        }

        public static bool DockerAvailable()
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo("docker", "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        public static void OpenInTerminal(string file, DockerAction a)
        {
            var dir = Path.GetDirectoryName(file);
            var cmd = "docker " + a.Args;
            if (a.Label == "Build and run") cmd += " && docker run --rm -it " + ImageNameFor(file);
            Process.Start(new ProcessStartInfo("cmd.exe", $"/k {cmd}") { UseShellExecute = true, WorkingDirectory = dir });
        }

        public static Task<(int Code, string Output)> RunAsync(string file, DockerAction a)
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("docker", a.Args)
                    {
                        WorkingDirectory = Path.GetDirectoryName(file), UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    };
                    var sb = new StringBuilder();
                    using var p = new Process { StartInfo = psi };
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    p.WaitForExit();
                    return (p.ExitCode, sb.ToString().TrimEnd());
                }
                catch (Exception ex) { return (-1, ex.Message); }
            });
        }
    }
}
