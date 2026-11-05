using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>Hong Kong Cantonese for the locks on menu items.</summary>
    internal static partial class Strings
    {
        private static void AddLockTypes(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            Add("Lock this menu item…", "鎖住呢個選單項目…");
            Add("Unlock this menu item…", "解鎖呢個選單項目…");
            Add("Unlock this element…", "解鎖呢個元素…");
            Add("Lock again", "再鎖返");
        }
    }
}
