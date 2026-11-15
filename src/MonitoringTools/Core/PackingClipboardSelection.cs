using System;
using System.IO;
using MonitoringTools.Clip;

namespace MonitoringTools.Core.Packing
{
    /// <summary>One in-memory file selection followed by the exact clipboard command. Never reads clipboard history.</summary>
    internal sealed class PackingClipboardSelection
    {
        private string[] _selection;
        public void Clear() => _selection = null;
        public bool ConsumeMatching(string[] paths)
        {
            if (_selection == null || paths == null || _selection.Length != paths.Length) return false;
            for (int i = 0; i < paths.Length; i++)
                if (!string.Equals(_selection[i], paths[i], StringComparison.OrdinalIgnoreCase)) return false;
            Clear();
            return true;
        }
        public bool Handle(ClipItem item, bool enabled, Action<string[]> start, Action<string> unavailable, Action<string[]> add = null)
        {
            if (!enabled || item == null || item.Sensitive) { Clear(); return false; }
            if (item.Kind == ClipKind.Files)
            {
                _selection = item.Files == null ? null : (string[])item.Files.Clone();
                return false;
            }
            var command = item.Text?.Trim();
            var adding = string.Equals(command, "vhd add", StringComparison.OrdinalIgnoreCase);
            if (!adding && !string.Equals(command, "vhd", StringComparison.OrdinalIgnoreCase))
            {
                Clear();
                return false;
            }
            var selected = _selection;
            Clear(); // Consume before validation or launching, including a declined native prompt.
            if (selected == null || selected.Length == 0)
            {
                unavailable("Copy files or folders first, then copy the exact word vhd. Each selection can be packed once.");
                return true;
            }
            foreach (var path in selected)
            {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || (!File.Exists(path) && !Directory.Exists(path)))
                {
                    unavailable("The last copied selection is no longer available. Copy the complete selection again; nothing was packed.");
                    return true;
                }
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        unavailable("The last copied selection contains a link. Copy the original files or folders again; nothing was packed.");
                        return true;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    unavailable("The last copied selection is no longer available. Copy the complete selection again; nothing was packed.");
                    return true;
                }
            }
            if (adding)
            {
                if (add == null) unavailable("No successful quick-pack VHDX is available. Create one with vhd or choose a target.");
                else add(selected);
            }
            else start(selected);
            return true;
        }
    }
}
