using System;
using System.Collections.Generic;
using System.Linq;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace SmartTests
{
    /// <summary>Cut or copy, the tabs a control centre opens with, and what a hand-started install shows.</summary>
    public static partial class Program
    {
        private static void AppStartChecks()
        {
            // --- cut or copy
            Check("drop effect move is a cut", CopyOrigin.FromDropEffect(2) == CopyGesture.Cut);
            Check("drop effect copy and link is a copy", CopyOrigin.FromDropEffect(5) == CopyGesture.Copy);
            Check("drop effect copy, move and link is a copy", CopyOrigin.FromDropEffect(7) == CopyGesture.Copy);
            Check("drop effect link alone is unknown", CopyOrigin.FromDropEffect(4) == CopyGesture.Unknown);
            Check("unreadable drop effect is unknown", CopyOrigin.FromDropEffect(CopyOrigin.ReadDropEffect(new byte[] { 2 })) == CopyGesture.Unknown);
            Check("drop effect bytes read little-endian", CopyOrigin.ReadDropEffect(new byte[] { 2, 0, 0, 0 }) == 2 && CopyOrigin.ReadDropEffect(new byte[] { 5, 0, 0, 0, 9 }) == 5);
            Check("Ctrl+X is a cut", CopyOrigin.FromKeys(true, false, false, true, false, false, false) == CopyGesture.Cut);
            Check("Ctrl+C is a copy", CopyOrigin.FromKeys(true, false, false, false, true, false, false) == CopyGesture.Copy);
            Check("Ctrl+Shift+C is a copy", CopyOrigin.FromKeys(true, true, false, false, true, false, false) == CopyGesture.Copy);
            Check("Shift+Delete is a cut", CopyOrigin.FromKeys(false, true, false, false, false, false, true) == CopyGesture.Cut);
            Check("Ctrl+Insert is a copy", CopyOrigin.FromKeys(true, false, false, false, false, true, false) == CopyGesture.Copy);
            Check("Ctrl+Alt+X is not a cut", CopyOrigin.FromKeys(true, false, true, true, false, false, false) == CopyGesture.Unknown);
            Check("X without Ctrl is unknown", CopyOrigin.FromKeys(false, false, false, true, false, false, false) == CopyGesture.Unknown);
            Check("nothing held is unknown", CopyOrigin.FromKeys(false, false, false, false, false, false, false) == CopyGesture.Unknown);
            Check("files mark wins over keys", CopyOrigin.Combine(CopyGesture.Cut, CopyGesture.Copy) == CopyGesture.Cut);
            Check("keys decide without a files mark", CopyOrigin.Combine(CopyGesture.Unknown, CopyGesture.Cut) == CopyGesture.Cut);
            Check("labels", CopyOrigin.Label(CopyGesture.Cut) == "Cut" && CopyOrigin.Label(CopyGesture.Copy) == "Copied" && CopyOrigin.Label(CopyGesture.Unknown) == null);
            Check("cut files say pasting moves them", CopyOrigin.Explain(CopyGesture.Cut, true).Contains("moves"));

            // --- every page open by default
            var pages = new List<string> { "overview", "activity", "history", "clipboard", "help", "about" };
            var open = new List<string> { "overview" };
            var known = new List<string>();
            Check("first start opens every page", TabDefaults.OpenUnseen(open, known, pages) && open.SequenceEqual(pages), string.Join(",", open));
            Check("first start remembers every page", known.SequenceEqual(pages));
            Check("second start changes nothing", !TabDefaults.OpenUnseen(open, known, pages));
            open.Remove("history");
            Check("a closed tab stays closed", !TabDefaults.OpenUnseen(open, known, pages) && !open.Contains("history"));
            var withNew = pages.Concat(new[] { "newpage" }).ToList();
            Check("a page added by an update opens once", TabDefaults.OpenUnseen(open, known, withNew) && open.Last() == "newpage" && !open.Contains("history"));
            var custom = new List<string> { "clipboard", "overview" };
            var none = new List<string>();
            TabDefaults.OpenUnseen(custom, none, pages);
            Check("a customised strip keeps its order and gains the rest", custom.Take(2).SequenceEqual(new[] { "clipboard", "overview" }) && custom.Count == pages.Count, string.Join(",", custom));

            // --- what a hand-started install shows
            var v66 = new Version(1, 0, 66); var v67 = new Version(1, 0, 67);
            Check("first install shows the welcome", InstallNote.Decide(false, false, null, v67) == InstallNote.Kind.FirstRun);
            Check("an upgrade by hand is a quiet update", InstallNote.Decide(true, true, v66, v67) == InstallNote.Kind.Updated);
            Check("an upgrade by hand never shows the welcome", InstallNote.Decide(true, false, v66, v67) != InstallNote.Kind.FirstRun);
            Check("the same version again opens the control centre", InstallNote.Decide(true, true, v67, v67) == InstallNote.Kind.Reopen);
            Check("a reinstall after uninstalling opens the control centre", InstallNote.Decide(false, true, null, v67) == InstallNote.Kind.Reopen);
            Check("an unreadable installed version opens the control centre", InstallNote.Decide(true, true, null, v67) == InstallNote.Kind.Reopen);
            Check("manual update sentence", InstallNote.UpdatedSentence(InstallNote.UpdatedText(v66, v67, true), v67) == "Updated from 1.0.66 to 1.0.67.");
            Check("background update sentence", InstallNote.UpdatedSentence(InstallNote.UpdatedText(v66, v67, false), v67) == "Updated from 1.0.66 to 1.0.67 in the background.");
            Check("older two-part flag still reads", InstallNote.UpdatedSentence("1.0.28>1.0.31", v67) == "Updated from 1.0.28 to 1.0.31 in the background.");
            Check("unknown from version", InstallNote.UpdatedSentence(">1.0.67>manual", v67) == "Updated to 1.0.67.");
        }
    }
}
