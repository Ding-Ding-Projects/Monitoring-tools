using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                || name.EndsWith(".Dockerfile", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase)) return DockerFileKind.Dockerfile;
            return DockerFileKind.None;
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
