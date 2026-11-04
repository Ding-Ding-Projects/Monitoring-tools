using System.Threading.Tasks;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>Checks for the typed number fields: no numeric preset rows remain, and the fields are driven by typing, steppers and keys.</summary>
        private static async Task RunNumberFieldsAsync()
        {
            await CheckAsync("number fields: no choice on any page or setup step offers only numeric presets, and the numeric settings use number fields", () => NumberFieldFlows.NoNumericPresets());
            await CheckAsync("number fields: the clipboard history size is typed, committed with Enter, trims the history, and out-of-range or non-numeric entries are refused", () => NumberFieldFlows.HistorySize());
            await CheckAsync("number fields: steppers and keys change the history size, and decimal, percent, rounded and minute fields store what was typed", () => NumberFieldFlows.Others());
            await CheckAsync("number fields: a slider and its typed box follow each other and the box refuses a level outside the slider", () => NumberFieldFlows.SliderPairs());
        }
    }
}
