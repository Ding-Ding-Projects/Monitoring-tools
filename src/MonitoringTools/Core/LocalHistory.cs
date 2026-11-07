using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MonitoringTools.Core
{
    /// <summary>One action in the local history: what happened, when, and what it can be undone or restored with.</summary>
    public sealed class LocalAction
    {
        public string Id { get; set; } = "";
        public DateTime At { get; set; }
        /// <summary>discard, base64, base64-undo, vdisk, restore</summary>
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string Detail { get; set; }
        /// <summary>The file an action wrote or touched, for Show and Undo.</summary>
        public string Path { get; set; }
        /// <summary>SHA-256 of the file as the action left it, so an undo leaves alone a file changed since.</summary>
        public string Sha256 { get; set; }
        /// <summary>Discarded text kept so it can be restored: plain, or sealed with the clipboard history key when that history is protected.</summary>
        public string Kept { get; set; }
        public bool KeptSealed { get; set; }
        /// <summary>Set on a later line when the action has been undone or restored; earlier lines are never rewritten.</summary>
        public string Undoes { get; set; }
    }

    /// <summary>
    /// The append-only local history of actions: discarded unsaved work, Base64 conversions, virtual disk operations.
    /// Each action is one JSON line added to the end of a file in this app's data folder. Nothing is rewritten or
    /// removed; an undo or a restore is a new line that points at the one it reverses. Discarded text is kept with
    /// its entry so it can be restored, sealed with the clipboard history key whenever that history is protected.
    /// </summary>
    public static class LocalHistory
    {
        private static readonly object Gate = new object();
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = false };
        public static event Action Changed;

        public static string FilePath
        {
            get
            {
                var dir = System.IO.Path.Combine(Settings.DataDir, "history");
                Directory.CreateDirectory(dir);
                return System.IO.Path.Combine(dir, "actions.jsonl");
            }
        }

        /// <summary>Adds an action to the end of the history and returns it. Never throws: history is a record, not a gate.</summary>
        public static LocalAction Append(string kind, string title, string detail = null, string path = null, string sha256 = null, string keep = null, string undoes = null)
        {
            var a = new LocalAction { Id = Guid.NewGuid().ToString("N").Substring(0, 12), At = DateTime.Now, Kind = kind, Title = title, Detail = detail, Path = path, Sha256 = sha256, Undoes = undoes };
            if (keep != null)
            {
                byte[] sealedBlob = null;
                try { if (HistoryVault.Encrypts) sealedBlob = HistoryVault.Encrypt(keep); } catch { sealedBlob = null; }
                if (sealedBlob != null) { a.Kept = Convert.ToBase64String(sealedBlob); a.KeptSealed = true; }
                else if (HistoryVault.Encrypts) a.Detail = (detail == null ? "" : detail + " ") + "The text was not kept, because the protected history is locked.";
                else a.Kept = keep;
            }
            try
            {
                var line = JsonSerializer.Serialize(a, Json) + "\n";
                lock (Gate) File.AppendAllText(FilePath, line, new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.Warn("Local history could not be written: " + ex.Message); }
            try { Changed?.Invoke(); } catch { }
            return a;
        }

        /// <summary>The most recent actions, newest first.</summary>
        public static List<LocalAction> Recent(int max = 200, string kind = null)
        {
            var list = new List<LocalAction>();
            try
            {
                string[] lines;
                lock (Gate) lines = File.Exists(FilePath) ? File.ReadAllLines(FilePath) : Array.Empty<string>();
                for (int i = lines.Length - 1; i >= 0 && list.Count < max; i--)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    try
                    {
                        var a = JsonSerializer.Deserialize<LocalAction>(lines[i], Json);
                        if (a != null && (kind == null || a.Kind == kind)) list.Add(a);
                    }
                    catch { /* a damaged line is skipped, the rest still read */ }
                }
            }
            catch (Exception ex) { Log.Warn("Local history could not be read: " + ex.Message); }
            return list;
        }

        /// <summary>True when a later line already undoes or restores this action.</summary>
        public static bool IsReversed(string id) => Recent(5000).Any(a => a.Undoes == id);

        /// <summary>The discarded text of an entry, or null when it was not kept or the protected history is locked.</summary>
        public static string KeptText(LocalAction a)
        {
            if (a?.Kept == null) return null;
            if (!a.KeptSealed) return a.Kept;
            try { return HistoryVault.Decrypt(Convert.FromBase64String(a.Kept)); } catch { return null; }
        }

        /// <summary>Records unsaved work that is being thrown away, before the window closes, so it can be brought back.</summary>
        public static LocalAction RecordDiscard(string where, string text, string detail = null) =>
            Append("discard", "Discarded unsaved changes in " + where, detail, keep: text ?? "");
    }
}
