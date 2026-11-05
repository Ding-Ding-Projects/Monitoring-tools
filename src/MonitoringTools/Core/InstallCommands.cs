using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    public enum InstallManager { Chocolatey, Winget }

    /// <summary>When Windows will ask for permission to run an install.</summary>
    public enum InstallPermission { ConsentAlways, InstallerDecides, ConsentExpected, NoPromptExpected }

    public sealed class InstallRefusal
    {
        public string Token;
        public string Reason;
    }

    /// <summary>
    /// A copied package-manager install command, read strictly. Everything is an allow list: a flag, a name or a character
    /// that is not known to be safe is refused with a reason, never ignored. Copied text is untrusted data.
    /// </summary>
    public sealed class InstallCommand
    {
        public string Raw;
        public InstallManager Manager;
        public string Verb;                                   // install or upgrade
        public List<string> Packages = new List<string>();
        public List<string> Flags = new List<string>();       // validated flags in the order given, with their values joined as --name=value
        public List<InstallRefusal> Refusals = new List<InstallRefusal>();
        public string Scope;                                  // winget --scope value, or null
        public bool ExplicitSource;                           // the default source was named out loud

        public bool Accepted => Refusals.Count == 0 && Packages.Count > 0;
        public string Exe => Manager == InstallManager.Chocolatey ? "choco" : "winget";
        public string SourceName => Manager == InstallManager.Chocolatey ? "Chocolatey community feed" : "winget community source";

        public InstallPermission Permission =>
            Manager == InstallManager.Chocolatey ? InstallPermission.ConsentAlways :
            Scope == "machine" ? InstallPermission.ConsentExpected :
            Scope == "user" ? InstallPermission.NoPromptExpected : InstallPermission.InstallerDecides;

        /// <summary>The arguments that would run, one token each. Every token passed the allow list, so none needs quoting.</summary>
        public List<string> Arguments()
        {
            var a = new List<string> { Verb };
            if (Manager == InstallManager.Chocolatey) a.AddRange(Packages);
            else a.AddRange(new[] { "--id", Packages[0] });
            a.AddRange(Flags);
            return a;
        }

        public string ArgumentString => string.Join(" ", Arguments());
        public string Display => Exe + " " + ArgumentString;

        /// <summary>Why automatic mode would not run this command, or null when it would.</summary>
        public string AutoRefusal(int maxPackages)
        {
            if (!Accepted) return "The command is not a plain install.";
            if (Packages.Count > Math.Max(1, maxPackages)) return $"It names {Packages.Count} packages and the limit is {maxPackages}.";
            var strict = Manager == InstallManager.Chocolatey ? InstallCommands.ChocoAutoFlags : InstallCommands.WingetAutoFlags;
            foreach (var f in Flags)
                if (!strict.Contains(f.Split('=')[0])) return f + " is allowed in a toast but not in automatic mode.";
            if (Manager == InstallManager.Winget && !Raw.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Any(t => t.Equals("--id", StringComparison.OrdinalIgnoreCase) || t.StartsWith("--id=", StringComparison.OrdinalIgnoreCase)))
                return "Automatic mode needs winget install --id <id>.";
            return null;
        }
    }

    public static class InstallCommands
    {
        // The command word and verb that make a copy an install command at all. Anything else is ordinary text.
        private static readonly Regex Start = new Regex(@"^(?<exe>choco|choco\.exe|winget|winget\.exe)\s+(?<verb>install|upgrade)(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NameRx = new Regex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly string[] FileLike = { ".config", ".nupkg", ".nuspec", ".ps1", ".zip", ".exe", ".msi", ".bat", ".cmd", ".json", ".yaml", ".yml", ".txt" };

        // Flags allowed in a toast, by manager.
        private static readonly HashSet<string> ChocoFlags = new HashSet<string> { "-y", "--yes", "--confirm", "--no-progress" };
        private static readonly HashSet<string> WingetFlags = new HashSet<string> { "-e", "--exact", "--accept-package-agreements", "--accept-source-agreements", "--silent", "-h" };
        // Flags allowed in automatic mode: a strict subset.
        internal static readonly HashSet<string> ChocoAutoFlags = new HashSet<string> { "-y", "--yes", "--confirm" };
        internal static readonly HashSet<string> WingetAutoFlags = new HashSet<string> { "-e", "--exact" };

        private static readonly Dictionary<string, string> KnownRefusals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--ignore-checksums"] = "skips the file check",
            ["--ignore-package-exit-codes"] = "hides install failures",
            ["--force"] = "reinstalls and overwrites without asking",
            ["-f"] = "reinstalls and overwrites without asking",
            ["--params"] = "passes options to the install script",
            ["--parameters"] = "passes options to the install script",
            ["--package-parameters"] = "passes options to the install script",
            ["--install-arguments"] = "passes raw arguments to the installer",
            ["--installarguments"] = "passes raw arguments to the installer",
            ["--ia"] = "passes raw arguments to the installer",
            ["--override"] = "replaces the installer's own arguments",
            ["--override-arguments"] = "replaces the installer's own arguments",
            ["--overrideargs"] = "replaces the installer's own arguments",
            ["--pre"] = "installs unreleased versions",
            ["--prerelease"] = "installs unreleased versions",
            ["--allow-downgrade"] = "installs an older version over a newer one",
            ["--allowdowngrade"] = "installs an older version over a newer one",
            ["--scripts"] = "runs extra scripts",
            ["--all"] = "upgrades everything on the computer",
            ["--interactive"] = "opens installer windows nobody is watching",
            ["-i"] = "opens installer windows nobody is watching",
        };

        private static string MetaReason(char c) => c switch
        {
            '&' or ';' => "chains a second command",
            '|' => "pipes into another command",
            '<' or '>' => "redirects input or output",
            '"' or '\'' => "quotes can hide code",
            '`' or '$' or '%' or '^' or '(' or ')' or '{' or '}' => "can run code or expand a variable",
            _ => "is not allowed in a plain command",
        };
        private const string Meta = "&;|<>\"'`$%^(){}";

        /// <summary>Null when the text is not an install command at all (so it stays ordinary text). Otherwise the command with every refusal listed.</summary>
        public static InstallCommand Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 600) return null;
            var t = text.Trim();
            var m = Start.Match(t);
            if (!m.Success) return null;

            var c = new InstallCommand
            {
                Raw = t,
                Manager = m.Groups["exe"].Value.StartsWith("choco", StringComparison.OrdinalIgnoreCase) ? InstallManager.Chocolatey : InstallManager.Winget,
                Verb = m.Groups["verb"].Value.ToLowerInvariant(),
            };
            void Refuse(string token, string reason) { if (!c.Refusals.Any(r => r.Token == token && r.Reason == reason)) c.Refusals.Add(new InstallRefusal { Token = token, Reason = reason }); }

            if (t.IndexOfAny(new[] { '\r', '\n' }) >= 0) Refuse("line break", "a second line is a second command; paste one command");

            var allowed = c.Manager == InstallManager.Chocolatey ? ChocoFlags : WingetFlags;
            var tokens = t.Substring(m.Length).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var names = new List<string>();
            string wingetId = null;
            for (int i = 0; i < tokens.Length; i++)
            {
                var tok = tokens[i];
                int bad = tok.IndexOfAny(Meta.ToCharArray());
                if (bad >= 0) { Refuse(tok, tok + " " + MetaReason(tok[bad])); continue; }

                if (tok.StartsWith("-"))
                {
                    var name = tok; string value = null;
                    int eq = tok.IndexOf('=');
                    if (eq > 0) { name = tok.Substring(0, eq); value = tok.Substring(eq + 1); }
                    var lower = name.ToLowerInvariant();

                    // value-taking flags
                    if (lower == "--source" || lower == "-s")
                    {
                        value ??= i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-") ? tokens[++i] : null;
                        var def = c.Manager == InstallManager.Chocolatey ? "chocolatey" : "winget";
                        if (value != null && value.Equals(def, StringComparison.OrdinalIgnoreCase)) c.ExplicitSource = true;
                        else Refuse(value == null ? tok : tok + (eq > 0 ? "" : " " + value), $"{lower} picks a custom source; only the default is used");
                        continue;
                    }
                    if (c.Manager == InstallManager.Winget && lower == "--id")
                    {
                        value ??= i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-") ? tokens[++i] : null;
                        if (value == null) { Refuse(tok, "--id needs a package id after it"); continue; }
                        if (wingetId != null) { Refuse(tok, "winget installs one package per command"); continue; }
                        wingetId = value;
                        continue;
                    }
                    if (c.Manager == InstallManager.Winget && lower == "--scope")
                    {
                        value ??= i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-") ? tokens[++i] : null;
                        if (value != null && (value.Equals("user", StringComparison.OrdinalIgnoreCase) || value.Equals("machine", StringComparison.OrdinalIgnoreCase)))
                        { c.Scope = value.ToLowerInvariant(); c.Flags.Add("--scope=" + c.Scope); }
                        else Refuse(tok + (value == null ? "" : " " + value), "--scope accepts only user or machine");
                        continue;
                    }
                    if (value != null) { Refuse(tok, KnownRefusals.TryGetValue(lower, out var r0) ? $"{lower} {r0}" : $"{lower} is not on the allowed list"); continue; }
                    if (allowed.Contains(lower)) { if (!c.Flags.Contains(lower)) c.Flags.Add(lower); continue; }
                    Refuse(tok, KnownRefusals.TryGetValue(lower, out var r) ? $"{lower} {r}" : $"{lower} is not on the allowed list");
                    continue;
                }
                names.Add(tok);
            }

            if (c.Manager == InstallManager.Winget)
            {
                if (wingetId != null) { if (names.Count > 0) Refuse(names[0], "name the package once, with --id"); names.Clear(); names.Add(wingetId); }
                else if (names.Count > 1) { foreach (var extra in names.Skip(1)) Refuse(extra, "winget installs one package per command"); names = names.Take(1).ToList(); }
            }

            foreach (var n in names)
            {
                if (!NameRx.IsMatch(n)) { Refuse(n, "package names use letters, digits, dots, dashes and underscores only"); continue; }
                var lowerName = n.ToLowerInvariant();
                if (FileLike.Any(e => lowerName.EndsWith(e))) { Refuse(n, "this is a file, not a package name"); continue; }
                if (lowerName == "all") { Refuse(n, "everything on the computer is not offered"); continue; }
                if (!c.Packages.Contains(n, StringComparer.OrdinalIgnoreCase)) c.Packages.Add(n);
            }
            if (c.Packages.Count == 0 && c.Refusals.Count == 0) Refuse(c.Verb, "no package name was given");
            if (c.Packages.Count > 10) { Refuse($"{c.Packages.Count} packages", "a command may name at most 10 packages"); }
            if (c.ExplicitSource) { /* the default source named out loud is harmless and is not passed on */ }
            return c;
        }
    }
}
