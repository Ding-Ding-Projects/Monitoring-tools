using System.Windows;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The appearance page. Its content is built in this file only.
        private FrameworkElement AppearancePage()
        {
            var root = PageRoot("Settings", "Appearance", "Theme, colour, text size, density and motion.");
            return root;
        }
    }
}
