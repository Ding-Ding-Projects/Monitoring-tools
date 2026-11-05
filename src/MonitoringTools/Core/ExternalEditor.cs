using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MonitoringTools.Core
{
    public sealed class EditorInfo
    {
        public string Id;          // vscode, vscode-insiders, notepadpp, default, custom
        public string Name;
        public string Path;        // null when not found
        public string Origin;      // where it was found, for display
        public bool Found => Path != null || Id == "default";
    }

    /// <summary>
    /// Finds installed editors and opens files or folders in them. Visual Studio Code gets first-class treatment:
    /// folders open as a workspace root, and when it is missing the caller is told so and offered the download.
    /// </summary>
    public static class ExternalEditor
    {
        public const string VsCodeDownloadUrl = "https://code.visualstudio.com/download";

        private static IEnumerable<string> OnPath(params string[] names)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Select(d => d.Trim().Trim('"')).Where(d => d.Length > 0))
                foreach (var n in names)
                {
                    string p = null;
                    try { p = System.IO.Path.Combine(dir, n); } catch { }
                    if (p != null && File.Exists(p)) yield return p;
                }
        }

        private static string FirstExisting(IEnumerable<string> candidates) => candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && File.Exists(c));

        private static string Env(Environment.SpecialFolder f) => Environment.GetFolderPath(f);

        /// <summary>Per-user, machine and Insiders installs of Visual Studio Code. The executable is preferred over code.cmd
        /// because it starts without a console window and takes the folder as a workspace root.</summary>
        public static EditorInfo FindVsCode(bool insiders = false)
        {
            var local = Env(Environment.SpecialFolder.LocalApplicationData);
            var pf = Env(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Env(Environment.SpecialFolder.ProgramFilesX86);
            string folder = insiders ? "Microsoft VS Code Insiders" : "Microsoft VS Code";
            string exe = insiders ? "Code - Insiders.exe" : "Code.exe";
            string cmd = insiders ? "code-insiders.cmd" : "code.cmd";
            var info = new EditorInfo { Id = insiders ? "vscode-insiders" : "vscode", Name = insiders ? "Visual Studio Code Insiders" : "Visual Studio Code" };
            var hit = FirstExisting(new[]
            {
                System.IO.Path.Combine(local, "Programs", folder, exe),
                System.IO.Path.Combine(pf, folder, exe),
                System.IO.Path.Combine(pf86, folder, exe)
            });
            if (hit != null) { info.Path = hit; info.Origin = hit.StartsWith(local, StringComparison.OrdinalIgnoreCase) ? "per-user install" : "machine install"; return info; }
            hit = OnPath(cmd, exe).FirstOrDefault();
            if (hit != null)
            {
                // code.cmd lives in <install>\bin; prefer the sibling Code.exe when it exists.
                var sibling = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(hit) ?? "", "..", exe));
                info.Path = File.Exists(sibling) ? sibling : hit;
                info.Origin = "found on PATH";
            }
            return info;
        }

        public static EditorInfo FindNotepadPlusPlus()
        {
            var info = new EditorInfo { Id = "notepadpp", Name = "Notepad++" };
            var hit = FirstExisting(new[]
            {
                System.IO.Path.Combine(Env(Environment.SpecialFolder.ProgramFiles), "Notepad++", "notepad++.exe"),
                System.IO.Path.Combine(Env(Environment.SpecialFolder.ProgramFilesX86), "Notepad++", "notepad++.exe"),
                System.IO.Path.Combine(Env(Environment.SpecialFolder.LocalApplicationData), "Programs", "Notepad++", "notepad++.exe")
            });
            if (hit != null) { info.Path = hit; info.Origin = "install folder"; return info; }
            hit = OnPath("notepad++.exe").FirstOrDefault();
            if (hit != null) { info.Path = hit; info.Origin = "found on PATH"; }
            return info;
        }

        /// <summary>Everything this app can choose from right now, including editors that were not found (so the page can say so).</summary>
        public static List<EditorInfo> Detect()
        {
            var list = new List<EditorInfo> { FindVsCode(false), FindVsCode(true), FindNotepadPlusPlus(),
                new EditorInfo { Id = "default", Name = "System default", Origin = "whatever Windows opens this file type with" } };
            var custom = Settings.Current.CustomEditorPath;
            if (!string.IsNullOrWhiteSpace(custom))
                list.Add(new EditorInfo { Id = "custom", Name = "Custom: " + System.IO.Path.GetFileName(custom), Path = File.Exists(custom) ? custom : null, Origin = File.Exists(custom) ? "your chosen program" : "file not found" });
            return list;
        }

        /// <summary>The editor the settings point at, resolving "auto" to the first one found in the order VS Code, Notepad++, system default.</summary>
        public static EditorInfo Preferred()
        {
            var all = Detect();
            var want = Settings.Current.PreferredEditor ?? "auto";
            if (want == "vscode") return all.FirstOrDefault(e => e.Id == "vscode" && e.Path != null) ?? all.FirstOrDefault(e => e.Id == "vscode-insiders" && e.Path != null) ?? all[0];
            if (want != "auto") { var pick = all.FirstOrDefault(e => e.Id == want); if (pick != null) return pick; }
            return all.FirstOrDefault(e => (e.Id == "vscode" || e.Id == "vscode-insiders" || e.Id == "notepadpp") && e.Path != null) ?? all.First(e => e.Id == "default");
        }

        /// <summary>Opens a file or folder in the preferred editor. Returns false with a plain message when it cannot.</summary>
        public static bool Open(string path, out string message) => OpenWith(Preferred(), path, out message);

        /// <summary>Opens in the preferred editor and, when that fails, tells the person why with a notice.</summary>
        public static void OpenAndTell(string path)
        {
            if (!Open(path, out var message))
                App.Toasts?.ShowNotice("Open in editor", message, App.Toasts.Amber, 6, icon: UI.Icons.Code);
        }

        /// <summary>Opens in Visual Studio Code only. When it is absent the message says so and the caller can offer <see cref="VsCodeDownloadUrl"/>.</summary>
        public static bool OpenInVsCode(string path, out string message, out bool missing)
        {
            var vs = FindVsCode(false);
            if (vs.Path == null) vs = FindVsCode(true);
            missing = vs.Path == null;
            if (missing) { message = "Visual Studio Code was not found on this computer. It is not installed in the usual folders and is not on PATH."; return false; }
            return OpenWith(vs, path, out message);
        }

        /// <summary>
        /// The arguments that make an editor open a file at a line and column, or null when this editor has no way to do that
        /// (the system default editor, a custom program, or an unknown id). Lines and columns start at 1.
        /// </summary>
        public static List<string> LineArguments(EditorInfo editor, string path, int line, int column)
        {
            if (editor == null || editor.Path == null || line < 1) return null;
            if (column < 1) column = 1;
            if (editor.Id.StartsWith("vscode", StringComparison.Ordinal)) return new List<string> { "-g", path + ":" + line + ":" + column };
            if (editor.Id == "notepadpp") return new List<string> { "-n" + line, "-c" + column, path };
            return null;
        }

        /// <summary>Opens a file at a line in the preferred editor. An editor that cannot take a line opens the file itself and the message says so.</summary>
        public static bool OpenAtLine(string path, int line, int column, out string message)
        {
            var editor = Preferred();
            var args = LineArguments(editor, path, line, column);
            if (args == null)
            {
                var ok = OpenWith(editor, path, out message);
                if (ok) message += " This editor cannot jump to line " + line + ".";
                return ok;
            }
            if (!File.Exists(path)) { message = "That path no longer exists: " + path; return false; }
            try
            {
                var start = new ProcessStartInfo(editor.Path) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var a in args) start.ArgumentList.Add(a);
                using (Process.Start(start)) { }
                message = "Opened the file at line " + line + " in " + editor.Name + ".";
                return true;
            }
            catch (Exception ex) { Log.Warn("Open at line failed: " + ex.Message); message = "Could not start " + editor.Name + ": " + ex.Message; return false; }
        }

        public static bool OpenWith(EditorInfo editor, string path, out string message)
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                message = "That path no longer exists: " + path;
                return false;
            }
            try
            {
                bool isDir = Directory.Exists(path);
                if (editor.Id == "default" || editor.Path == null)
                {
                    if (editor.Id != "default") { message = editor.Name + " was not found. Choose another editor on the Tools page."; return false; }
                    // The system default for a folder is Explorer; for a file try Edit first, then Open.
                    var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                    if (!isDir)
                    {
                        try { psi.Verb = "edit"; using (Process.Start(psi)) { } message = "Opened with the system default editor."; return true; }
                        catch { psi = new ProcessStartInfo(path) { UseShellExecute = true }; }
                    }
                    using (Process.Start(psi)) { }
                    message = "Opened with the system default.";
                    return true;
                }
                // ArgumentList quotes the path correctly, including spaces and ampersands.
                var start = new ProcessStartInfo(editor.Path) { UseShellExecute = false, CreateNoWindow = true };
                if (editor.Id.StartsWith("vscode") && isDir) start.ArgumentList.Add("--new-window");
                start.ArgumentList.Add(path);       // a folder opens as the workspace root
                using (Process.Start(start)) { }
                message = $"Opened {(isDir ? "the folder" : "the file")} in {editor.Name}.";
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Open in editor failed: " + ex.Message);
                message = $"Could not start {editor.Name}: {ex.Message}";
                return false;
            }
        }
    }
}
