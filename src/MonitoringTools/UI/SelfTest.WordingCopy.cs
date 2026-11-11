using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>Wording files in the shapes people actually copy and write. Run on their own (MT_ONLY=wordingcopy).</summary>
        private static async Task WordingCopyChecksAsync(string dir)
        {
            var saved = Settings.Current.ApplyCopiedWordingFile;
            Settings.Current.ApplyCopiedWordingFile = true;
            var folder = Path.Combine(dir, "wording copy");
            Directory.CreateDirectory(folder);
            string Write(string name, string json) { var p = Path.Combine(folder, name); File.WriteAllText(p, json, new UTF8Encoding(false)); return p; }
            var good = Write("my words.json", "{\"schemaVersion\":1,\"entries\":{\"Clipboard\":\"Copy pile\",\"Settings\":\"Knobs\"}}");
            try
            {
                Check("wording copy: File Explorer's Copy as path (in quotes) applies the file", () =>
                {
                    PersonalWording.Clear();
                    var item = ClipItem.FromText("\"" + good + "\"");
                    if (!App.TryApplyCopiedWording(item)) return "the quoted path was not taken (kind " + item.Kind + ")";
                    return PersonalWording.Count == 2 ? null : PersonalWording.Count + " phrases applied";
                });
                Check("wording copy: a plain path and a file:/// address apply it too", () =>
                {
                    PersonalWording.Clear();
                    if (!App.TryApplyCopiedWording(ClipItem.FromText(good))) return "the plain path was not taken";
                    PersonalWording.Clear();
                    if (!App.TryApplyCopiedWording(ClipItem.FromText(new Uri(good).AbsoluteUri))) return "the file:/// address was not taken";
                    return PersonalWording.Count == 2 ? null : PersonalWording.Count + " phrases";
                });
                Check("wording copy: a large file with case variants, metadata and object entries applies, and fast", () =>
                {
                    PersonalWording.Clear();
                    var sb = new StringBuilder("{\"schemaVersion\":2,\"name\":\"words\",\"generated\":\"2026-11-10\",\"notes\":{\"by\":\"a script\"},\"entries\":{");
                    for (int i = 0; i < 6000; i++) sb.Append($"\"phrase number {i}\":\"word {i} with a longer replacement to grow the file past the old limit\",");
                    sb.Append("\"Branch\":\"Twig\",\"branch\":\"twig\",\"push\":{\"replacement\":\"shove\",\"note\":\"x\"},\"constructor\":\"maker\"}}");
                    var big = Write("big words.json", sb.ToString());
                    if (new FileInfo(big).Length <= 256 * 1024) return "the sample is not larger than the old 256 KiB limit";
                    if (!App.TryApplyCopiedWording(ClipItem.FromText("\"" + big + "\""))) return "the large file was not taken";
                    if (PersonalWording.Count != 6004) return PersonalWording.Count + " phrases, expected 6004";
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    string r = null;
                    for (int i = 0; i < 2000; i++) r = PersonalWording.Apply("Push the Branch, then the branch; phrase number 5999 and the constructor.");
                    if (r != "Shove the Twig, then the twig; word 5999 with a longer replacement to grow the file past the old limit and the maker.") return "applied as: " + r;
                    return sw.ElapsedMilliseconds < 1500 ? null : $"2000 lines took {sw.ElapsedMilliseconds} ms";
                });
                Check("wording copy: a wording file that fails says why instead of being ignored, and changes nothing", () =>
                {
                    PersonalWording.Clear();
                    var bad = Write("broken words.json", "{\"schemaVersion\":1,\"entries\":{\"Clipboard\":42}}");
                    if (!App.TryApplyCopiedWording(ClipItem.FromText("\"" + bad + "\""))) return "the broken wording file was ignored silently";
                    if (PersonalWording.Count != 0) return "a refused file changed the wording";
                    PersonalWording.Validate(File.ReadAllBytes(bad), out _, out var why);
                    return why != null && why.Contains("Clipboard") ? null : "the reason does not name the entry: " + why;
                });
                Check("wording copy: other JSON files and other paths stay ordinary copies", () =>
                {
                    var pkg = Write("package.json", "{\"name\":\"x\",\"version\":\"1.0.0\"}");
                    if (App.TryApplyCopiedWording(ClipItem.FromText("\"" + pkg + "\""))) return "package.json was taken";
                    var txt = Write("notes.txt", "{\"schemaVersion\":1,\"entries\":{\"a\":\"b\"}}");
                    if (App.TryApplyCopiedWording(ClipItem.FromText(txt))) return "a .txt file was taken";
                    if (App.TryApplyCopiedWording(ClipItem.FromText("see \"" + good + "\" later"))) return "a sentence holding a path was taken";
                    return null;
                });
            }
            finally { Settings.Current.ApplyCopiedWordingFile = saved; PersonalWording.Clear(); }
            await Task.CompletedTask;
        }
    }
}
