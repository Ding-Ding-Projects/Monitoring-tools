using System.Linq;
using System.Windows;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private McManagerView _mc;

        /// <summary>The server manager built for the current Minecraft page, for the self-test and the capture run.</summary>
        internal McManagerView MinecraftManager => _mc;

        private FrameworkElement MinecraftPage()
        {
            var root = PageRoot("Minecraft", "Minecraft servers");
            root.MaxWidth = 1500; root.HorizontalAlignment = HorizontalAlignment.Stretch;
            _mc = new McManagerView(this);
            root.Children.Add(_mc);
            return root;
        }

        /// <summary>The world generator, shown in a server's Worlds section and aimed at that server when it is on this computer.</summary>
        internal FrameworkElement WorldGeneratorCard(ServerDef s)
        {
            if (s != null && s.IsLocal && ServerManager.Servers.Contains(s)) _wgServerId = s.Id;
            return WorldGenCard();
        }
    }
}
