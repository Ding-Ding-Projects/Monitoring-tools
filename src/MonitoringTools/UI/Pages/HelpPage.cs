using System.Windows;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The help page. Its content is built in this file only.
        private FrameworkElement HelpPage()
        {
            var root = PageRoot("Help", "Help and docs", "Every feature explained, offline.");
            return root;
        }
    }
}
