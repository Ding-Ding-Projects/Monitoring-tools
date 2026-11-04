using MonitoringTools.Core;

namespace MonitoringTools.UI.Tabs
{
    /// <summary>Small wording helpers so every visible string of the tab strip goes through the language layer when it is created.</summary>
    internal static class TabText
    {
        /// <summary>The text in the current language mode.</summary>
        public static string T(string english) => Language.T(english);

        /// <summary>A translated format string filled with values. The English and Cantonese forms use the same placeholders.</summary>
        public static string F(string format, params object[] args) => string.Format(Language.T(format), args);

        /// <summary>"1 tab" or "3 tabs", in the current language.</summary>
        public static string TabCount(int n) => F(n == 1 ? "{0} tab" : "{0} tabs", n);
    }
}
