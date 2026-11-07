using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Every control centre page starts open, so nothing hides behind New tab. A page the strip has never offered opens
    /// once: on a new install that is every page, after an update just the new ones. A tab the person closed stays closed.
    /// </summary>
    public static class TabDefaults
    {
        /// <summary>
        /// Opens the pages in <paramref name="pages"/> that are missing from <paramref name="known"/>. A strip still on the old
        /// single Overview default is laid out in page order; otherwise the unseen pages join the end in page order.
        /// Returns true when anything changed.
        /// </summary>
        public static bool OpenUnseen(List<string> open, List<string> known, IReadOnlyList<string> pages)
        {
            var unseen = pages.Where(id => !known.Contains(id)).ToList();
            if (unseen.Count == 0) return false;
            bool untouched = known.Count == 0 && open.All(t => t == "overview");
            if (untouched)
            {
                open.Clear();
                open.AddRange(pages);
            }
            else
            {
                foreach (var id in unseen) if (!open.Contains(id)) open.Add(id);
            }
            known.AddRange(unseen);
            return true;
        }
    }
}
