using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>One expected-outcome line for a pattern: a piece of text and whether the pattern should match it.</summary>
    public sealed class RegexTestCase
    {
        public string Text { get; set; } = "";
        public bool ShouldMatch { get; set; } = true;
    }

    /// <summary>One saved benchmark run, kept so a pattern can be compared with an earlier version of itself.</summary>
    public sealed class RegexBenchRecord
    {
        public string When { get; set; } = "";
        public string Pattern { get; set; } = "";
        public int Options { get; set; }
        public int SubjectLength { get; set; }
        public double MsPerRun { get; set; }
        public bool TimedOut { get; set; }
        public string Verdict { get; set; } = "";
    }

    /// <summary>A named set of file search settings for the Files view.</summary>
    public sealed class RegexGrepPreset
    {
        public string Name { get; set; } = "";
        public string Folder { get; set; } = "";
        public string Masks { get; set; } = "*";
        public string ExcludeMasks { get; set; } = "";
        public string NameRegex { get; set; } = "";
        public bool Subfolders { get; set; } = true;
        public bool SkipBinary { get; set; } = true;
        /// <summary>How a binary file is searched when it is not skipped: Bytes or Text.</summary>
        public string BinaryMode { get; set; } = "Bytes";
        public bool SearchArchives { get; set; } = false;
    }

    /// <summary>File search settings for one file extension: the encoding to read it with, whether to skip it, and how to treat it when it looks binary.</summary>
    public sealed class RegexFileType
    {
        /// <summary>The extension with its dot, such as ".log".</summary>
        public string Extension { get; set; } = "";
        /// <summary>Auto, UTF-8, UTF-16 LE, UTF-16 BE or Latin-1. A forced encoding also means the file is read as text even when it has null bytes.</summary>
        public string Encoding { get; set; } = "Auto";
        public bool Skip { get; set; } = false;
        /// <summary>Default (the Files view setting), Skip, Bytes or Text.</summary>
        public string Binary { get; set; } = "Default";
    }

    /// <summary>One finished file search, kept in the results history.</summary>
    public sealed class RegexGrepRecord
    {
        public string When { get; set; } = "";
        public string Folder { get; set; } = "";
        public string Pattern { get; set; } = "";
        public int FilesScanned { get; set; }
        public int FilesWithMatches { get; set; }
        public int Matches { get; set; }
    }

    /// <summary>Settings owned by the regex builder: remembered patterns and test cases stored beside the library.</summary>
    public sealed partial class Settings
    {
        /// <summary>
        /// Remember the recent patterns of every search box across restarts. Off by default because patterns can be private.
        /// Turning it off also clears what was stored.
        /// </summary>
        public bool RegexKeepHistory { get; set; } = false;

        /// <summary>Recent patterns per search field id, newest first, at most 50 each. Only filled while <see cref="RegexKeepHistory"/> is on.</summary>
        public Dictionary<string, List<string>> RegexHistory { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>Expected-outcome test cases per library entry name. Kept apart from the entries so older library files still load.</summary>
        public Dictionary<string, List<RegexTestCase>> RegexPatternTests { get; set; } = new Dictionary<string, List<RegexTestCase>>();

        /// <summary>Edited code templates keyed by "language|kind". An entry replaces the built-in template; removing it restores the built-in one.</summary>
        public Dictionary<string, string> RegexCodeTemplates { get; set; } = new Dictionary<string, string>();

        /// <summary>Saved benchmark runs, newest first, at most 30.</summary>
        public List<RegexBenchRecord> RegexBenchHistory { get; set; } = new List<RegexBenchRecord>();

        /// <summary>Saved file search settings.</summary>
        public List<RegexGrepPreset> RegexGrepPresets { get; set; } = new List<RegexGrepPreset>();

        /// <summary>Per-extension file search settings used by every Files view search.</summary>
        public List<RegexFileType> RegexFileTypes { get; set; } = new List<RegexFileType>();

        /// <summary>Finished file searches, newest first, at most 20.</summary>
        public List<RegexGrepRecord> RegexGrepHistory { get; set; } = new List<RegexGrepRecord>();

        // ---- builder preferences

        /// <summary>The view the builder opens on. Build, Test, Debug, Compare, Benchmark, Replace, Files, Code, Library or Help.</summary>
        public string RegexDefaultTab { get; set; } = "Build";

        /// <summary>Width of the builder card in device-independent pixels, 560 to 1100. Zero keeps it as wide as the search bar, at least 680.</summary>
        public int RegexCardWidth { get; set; } = 0;

        /// <summary>Height at which the builder scrolls, 360 to 900.</summary>
        public int RegexCardMaxHeight { get; set; } = 620;

        /// <summary>Views left out of the tab strip. Build and Help always stay.</summary>
        public List<string> RegexHiddenTabs { get; set; } = new List<string>();

        /// <summary>Open the Build view with the detailed explanation instead of the brief one.</summary>
        public bool RegexDetailedByDefault { get; set; } = false;
    }
}
