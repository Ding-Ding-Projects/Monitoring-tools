using System.Windows;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The records page. Its content is built in this file only.
        private FrameworkElement RecordsPage()
        {
            var root = PageRoot("Records", "Records and exports", "Notifications, settings history, exports and the changelog.");
            return root;
        }
    }
}
