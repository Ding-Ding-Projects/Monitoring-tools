using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Pure helpers behind the bulk actions and guided forms: text formats for exports, shortcut duplication and
    /// DNS profile validation. Nothing here touches the screen, so the self-test can check it directly.
    /// </summary>
    public static class ListActions
    {
        public static string ActivityLine(ActivityEntry e) =>
            $"{e.At:yyyy-MM-dd HH:mm:ss}  [{e.Kind}] {(e.Ok ? "" : "FAILED ")}{e.Title}{(string.IsNullOrWhiteSpace(e.Detail) ? "" : " - " + e.Detail)}";

        public static string NotificationLine(NotificationEntry n) =>
            $"{n.At:yyyy-MM-dd HH:mm:ss}  [{n.Kind}] {n.Title}{(string.IsNullOrWhiteSpace(n.Detail) ? "" : " - " + n.Detail)}";

        /// <summary>Selected shortcuts as indented JSON, including each script body so the export is complete on its own.</summary>
        public static string ShortcutsJson(IEnumerable<Shortcut> items, Func<Shortcut, string> readScript)
        {
            var rows = items.Select(s => new
            {
                s.Name, s.Description, Shell = s.Shell.ToString(), s.ShowWindow, s.RunElevated,
                Script = readScript(s)
            }).ToList();
            return JsonSerializer.Serialize(new { version = 1, shortcuts = rows }, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>A free name for a copy of <paramref name="name"/>: name-copy, name-copy2, ... within the 32-character limit.</summary>
        public static string CopyName(string name, Func<string, bool> taken)
        {
            for (int i = 1; i < 1000; i++)
            {
                var suffix = i == 1 ? "-copy" : "-copy" + i;
                var stem = name.Length + suffix.Length > 32 ? name.Substring(0, 32 - suffix.Length) : name;
                var candidate = stem + suffix;
                if (!taken(candidate) && Shortcuts.IsValidName(candidate)) return candidate;
            }
            return null;
        }

        /// <summary>Creates a copy of each shortcut (settings and script file) under a free name and returns the new ones.</summary>
        public static List<Shortcut> Duplicate(IEnumerable<Shortcut> items)
        {
            var made = new List<Shortcut>();
            foreach (var s in items.ToList())
            {
                var name = CopyName(s.Name, n => Shortcuts.Find(n) != null || Shortcuts.Reserved.Contains(n.ToLowerInvariant()));
                if (name == null) continue;
                var copy = new Shortcut { Name = name, Description = s.Description, Shell = s.Shell, ShowWindow = s.ShowWindow, RunElevated = s.RunElevated };
                Settings.Current.Shortcuts.Add(copy);
                try { Shortcuts.WriteScript(copy, Shortcuts.ReadScript(s)); } catch (Exception ex) { Log.Warn("Duplicating " + s.Name + ": " + ex.Message); }
                made.Add(copy);
            }
            return made;
        }

        /// <summary>Removes the shortcuts and their script files. Returns how many were removed.</summary>
        public static int DeleteShortcuts(IEnumerable<Shortcut> items)
        {
            int n = 0;
            foreach (var s in items.ToList())
            {
                try { File.Delete(s.ScriptPath); } catch { }
                if (Settings.Current.Shortcuts.Remove(s)) n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- DNS profile input

        public static bool IsIPv4(string text) =>
            !string.IsNullOrWhiteSpace(text) && text.Trim().Count(c => c == '.') == 3
            && System.Net.IPAddress.TryParse(text.Trim(), out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

        /// <summary>Null when the name is usable, otherwise a plain sentence saying what to fix.</summary>
        public static string ValidateDnsName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Give the profile a name.";
            if (name.Trim().Length > 40) return "Keep the name to 40 characters or fewer.";
            if (name.Trim().Equals("Automatic", StringComparison.OrdinalIgnoreCase)) return "\"Automatic\" is built in and means servers from the router.";
            return null;
        }

        /// <summary>Null when the servers are usable: a primary IPv4 address and, if given, a secondary IPv4 address that differs from it.</summary>
        public static string ValidateDnsServers(string primary, string secondary)
        {
            if (string.IsNullOrWhiteSpace(primary)) return "Enter a primary server, for example 1.1.1.1.";
            if (!IsIPv4(primary)) return "The primary server must be an IPv4 address such as 1.1.1.1.";
            if (!string.IsNullOrWhiteSpace(secondary))
            {
                if (!IsIPv4(secondary)) return "The secondary server must be an IPv4 address such as 1.0.0.1, or left empty.";
                if (secondary.Trim() == primary.Trim()) return "The secondary server is the same as the primary. Use a different one or leave it empty.";
            }
            return null;
        }
    }
}
