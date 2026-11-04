using System;

namespace MonitoringTools.Core
{
    /// <summary>Paths as they are shown on screen: the profile folders become variables so a user name never appears in a capture or a screen share.</summary>
    public static class PathDisplay
    {
        public static string Shown(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            foreach (var (folder, name) in new[]
            {
                (Environment.SpecialFolder.LocalApplicationData, "%LOCALAPPDATA%"),
                (Environment.SpecialFolder.ApplicationData, "%APPDATA%"),
                (Environment.SpecialFolder.UserProfile, "%USERPROFILE%"),
            })
            {
                var dir = Environment.GetFolderPath(folder);
                if (dir.Length > 0 && path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return name + path[dir.Length..];
            }
            return path;
        }
    }
}
