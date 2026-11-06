using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>Settings for the smart clipboard: which kinds are recognised, automatic lookups, the secret guard,
    /// extensions and the clipboard shortcuts. Nothing copied is ever stored here.</summary>
    public sealed partial class Settings
    {
        // Kinds of copies, by group: developer, everyday, numbers. A group listed here is off.
        public List<string> SmartGroupsOff { get; set; } = new List<string>();

        // Automatic lookups send the smallest possible piece (a domain, a currency code) to the named service.
        public bool AutoLookups { get; set; } = true;
        public bool LookupLinks { get; set; } = true;
        public bool LookupIps { get; set; } = true;
        public bool LookupRepos { get; set; } = true;
        public bool LookupMoney { get; set; } = true;
        public bool LookupBooksAndPackages { get; set; } = true;
        public string HomeCurrency { get; set; } = "CAD";

        // Secret guard: keys, tokens and bank numbers stay out of history and leave the clipboard after this many seconds.
        public bool SecretGuard { get; set; } = true;
        public int SecretClearSeconds { get; set; } = 30;

        // Extensions add kinds of copies and buttons. Turning this off stops every extension at once.
        public bool ExtensionsEnabled { get; set; } = true;

        // Clipboard shortcuts. Windows keeps Win+V for its own history, so ours uses Win+Shift+V.
        public string HotkeyHistory { get; set; } = "Win+Shift+V";
        public string HotkeyPlainPaste { get; set; } = "Ctrl+Shift+Alt+V";
        public string HotkeyQueue { get; set; } = "Ctrl+Shift+Q";
        public string HotkeyUndo { get; set; } = "Ctrl+Shift+Z";
        public string HotkeyConversions { get; set; } = "Ctrl+Shift+U";
        public string HotkeyPin { get; set; } = "Ctrl+Shift+P";
        public string HotkeyCollect { get; set; } = "Ctrl+Shift+A";
        public string HotkeyTypeIt { get; set; } = "Ctrl+Shift+Alt+T";
        public bool ClipboardHotkeysOn { get; set; } = true;

        // Paste queue: press Tab after each paste so the next field is ready.
        public bool QueueTabAfterPaste { get; set; } = true;
    }
}
