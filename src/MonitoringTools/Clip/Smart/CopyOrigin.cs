namespace MonitoringTools.Clip.Smart
{
    /// <summary>How something reached the clipboard: cut, copied, or not known.</summary>
    public enum CopyGesture { Unknown, Copy, Cut }

    /// <summary>
    /// Tells a cut from a copy. Windows records it for files: Explorer and most file managers add a "Preferred DropEffect"
    /// value, where move means the files were cut and pasting moves them. Text and pictures carry no such mark, so the
    /// shortcut still held when the clipboard changed decides: Ctrl+X or Shift+Delete is a cut, Ctrl+C or Ctrl+Insert a
    /// copy. A Cut or Copy picked from a menu leaves no trace, so it stays unknown rather than being guessed.
    /// </summary>
    public static class CopyOrigin
    {
        public const string DropEffectFormat = "Preferred DropEffect";
        public const int DropEffectCopy = 1, DropEffectMove = 2, DropEffectLink = 4;

        /// <summary>From a file drop's Preferred DropEffect value: move alone is a cut, anything that allows copying is a copy.</summary>
        public static CopyGesture FromDropEffect(int effect)
        {
            if (effect <= 0) return CopyGesture.Unknown;
            if ((effect & DropEffectMove) != 0 && (effect & DropEffectCopy) == 0) return CopyGesture.Cut;
            if ((effect & DropEffectCopy) != 0) return CopyGesture.Copy;
            return CopyGesture.Unknown;
        }

        /// <summary>The DropEffect value in the first four bytes of the clipboard data, or -1.</summary>
        public static int ReadDropEffect(byte[] data) =>
            data != null && data.Length >= 4 ? data[0] | data[1] << 8 | data[2] << 16 | data[3] << 24 : -1;

        /// <summary>From the keys held down at the moment the clipboard changed.</summary>
        public static CopyGesture FromKeys(bool ctrl, bool shift, bool alt, bool x, bool c, bool insert, bool delete)
        {
            if (alt) return CopyGesture.Unknown;
            if (ctrl && !shift && x) return CopyGesture.Cut;
            if (ctrl && c) return CopyGesture.Copy;              // Ctrl+Shift+C copies in terminals
            if (shift && !ctrl && delete) return CopyGesture.Cut;
            if (ctrl && !shift && insert) return CopyGesture.Copy;
            return CopyGesture.Unknown;
        }

        /// <summary>What Windows recorded for files wins; otherwise the keys decide.</summary>
        public static CopyGesture Combine(CopyGesture fromFiles, CopyGesture fromKeys) =>
            fromFiles != CopyGesture.Unknown ? fromFiles : fromKeys;

        /// <summary>The word the toast shows, or null when it is not known.</summary>
        public static string Label(CopyGesture g) => g == CopyGesture.Cut ? "Cut" : g == CopyGesture.Copy ? "Copied" : null;

        /// <summary>The longer sentence for a screen reader and the tooltip.</summary>
        public static string Explain(CopyGesture g, bool files) => g switch
        {
            CopyGesture.Cut when files => "Cut: pasting moves these files.",
            CopyGesture.Cut => "Cut: it was taken out of where it came from.",
            CopyGesture.Copy when files => "Copied: pasting makes copies and leaves the originals.",
            CopyGesture.Copy => "Copied: the original is still where it was.",
            _ => null,
        };
    }
}
