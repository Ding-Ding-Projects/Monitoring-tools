using System.Windows;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The modes page. Its content is built in this file only.
        private FrameworkElement ModesPage()
        {
            var root = PageRoot("Settings", "Modes", "School mode, ADHD modes, the dim sum surprise and scheduled settings.");
            return root;
        }
    }
}
