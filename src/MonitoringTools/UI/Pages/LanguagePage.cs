using System.Windows;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The language page. Its content is built in this file only.
        private FrameworkElement LanguagePage()
        {
            var root = PageRoot("Settings", "Language and voice", "Language, humour, emoji and the narrator.");
            return root;
        }
    }
}
