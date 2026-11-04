using System.Threading.Tasks;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>
        /// Checks that open real windows and press real controls: the element menus, scheduled settings, bulk select,
        /// the guided forms, the Focus spotlight, School mode, the appearance editor, the colour picker, local history
        /// and the subtitle on every setting. The flows are shared with the capture run (see Wave6Flows).
        /// </summary>
        private static async Task RunWave6Async()
        {
            await CheckAsync("menus: toast, offline dialog, setup wizard, confirmation window and file tools each open an element menu; the confirmation offers only Copy text", () => Wave6Flows.Menus(null));
            await CheckAsync("scheduled settings: a rule with a date range is created through the form, edited in place and stored", () => Wave6Flows.Schedule(null));
            await CheckAsync("bulk select: activity, notifications, shortcuts and the regex Library count choices, and a destructive action needs both keys and a full slide", () => Wave6Flows.Bulk(null));
            await CheckAsync("guided forms: the DNS profile form and the new-server form step through, validate, keep answers on Back and save or cancel", () => Wave6Flows.Guided(null));
            await CheckAsync("spotlight: the Modes page switch adds the dim layer and removes it", () => Wave6Flows.Spotlight(null));
            await CheckAsync("school: a PIN set through the Modes page forces English and hides the language choices; the PIN turns it off", () => Wave6Flows.School(null));
            await CheckAsync("appearance editor: per-state colours, undo and redo, presets and export and import work through the editor", () => Wave6Flows.Appearance(null));
            await CheckAsync("colour picker: a value typed into each of the nine fields comes back as the same colour in hex", () => Wave6Flows.Picker(null));
            await CheckAsync("local history: three saves of a script are listed, a version shows a comparison and Restore asks for both keys", () => Wave6Flows.History(null));
            await CheckAsync("settings explain themselves: every switch and choice on every page and setup step shows a subtitle", () => Wave6Flows.Explain());
        }
    }
}
