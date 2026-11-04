using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Core
{
    public sealed class NotificationEntry
    {
        public DateTime At = DateTime.Now;
        public string Kind;
        public string Title;
        public string Detail;

        /// <summary>The title in the language and humour level in force now. Headlines are rendered again; other titles are plain text the live layer translates.</summary>
        public string DisplayTitle => Render != null ? Render() : Title;

        /// <summary>Writes the headline again at the current language and humour level; null for a title that was never a headline.</summary>
        public Func<string> Render;
    }

    /// <summary>In-memory log of every toast, notice, offline alert and IP change. Nothing here is written to disk.</summary>
    public static class NotificationLog
    {
        public const int Cap = 500;
        public static readonly string[] Kinds = { "Toast", "Notice", "Offline", "IP changed" };

        private static readonly object Gate = new object();
        private static readonly List<NotificationEntry> Entries = new List<NotificationEntry>();
        public static event Action Changed;

        public static void Add(string kind, string title, string detail = null)
        {
            lock (Gate)
            {
                Entries.Insert(0, new NotificationEntry { Kind = kind, Title = title ?? "", Detail = detail ?? "", Render = Language.RendererFor(title) });
                if (Entries.Count > Cap) Entries.RemoveAt(Entries.Count - 1);
            }
            Changed?.Invoke();
        }

        /// <summary>Logs a toast that was just shown, classifying it from its header text.</summary>
        public static void AddToast(string title, string meta)
        {
            var t = title ?? "";
            var kind = t.IndexOf("IP changed", StringComparison.OrdinalIgnoreCase) >= 0 ? "IP changed"
                     : t.IndexOf("offline", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ? "Offline"
                     : "Toast";
            Add(kind, t, meta);
        }

        public static List<NotificationEntry> All()
        {
            lock (Gate) return Entries.ToList();
        }

        /// <summary>Removes just these entries (by reference) and returns how many were removed.</summary>
        public static int Remove(IEnumerable<NotificationEntry> items)
        {
            int n;
            var set = new HashSet<NotificationEntry>(items);
            lock (Gate) n = Entries.RemoveAll(set.Contains);
            if (n > 0) Changed?.Invoke();
            return n;
        }

        public static void Clear()
        {
            lock (Gate) Entries.Clear();
            Changed?.Invoke();
        }
    }
}
