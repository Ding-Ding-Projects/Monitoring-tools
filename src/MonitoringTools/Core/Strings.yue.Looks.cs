using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>Hong Kong Cantonese for the four looks, the look picker, the setup step and the command palette entries.</summary>
    internal static partial class Strings
    {
        private static void AddLooks(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            Add("Look", "風格");
            Add("Graphite", "石墨"); Add("Instrument", "儀器"); Add("Blueprint", "藍圖"); Add("Pop", "波普");
            Add("Soft tonal layers and rounded shapes. The look you have today.", "柔和嘅色調層次同圓角形狀，即係你而家用緊嘅風格。");
            Add("Like test gear on a bench: panels, lit indicators and amber readouts.", "好似工作枱上面嘅測試儀器：面板、會亮嘅指示燈同琥珀色讀數。");
            Add("Drawn in fine ink on a grid, with square corners and no shadows.", "用幼墨線畫喺格仔紙上面，直角、冇陰影。");
            Add("Bold outlines, sticker colours and hard shadows.", "粗黑框、貼紙色同硬朗陰影。");
            Add("Changes type, shapes, colour and depth everywhere at once. Your text size, motion and language choices stay as they are.",
                "一次過改晒所有地方嘅字體、形狀、顏色同層次。你揀好嘅字體大小、動畫同語言保持唔變。");
            Add("Picking a look also picks the colour it was drawn with. Change the colour under Theme and colour whenever you like. Default: Graphite.",
                "揀風格會一齊揀返佢設計時用嘅顏色。之後喺「主題同顏色」度隨時都可以再改。預設：石墨。");
            Add("Look, theme, colour, text size, density and motion.", "風格、主題、顏色、字體大小、密度同動態效果。");
            Add("Look, theme, colour, text size and motion", "風格、主題、顏色、字體大小同動態效果");
            Add("Pick a look", "揀個風格");
            Add("Four complete styles for every window, each with its own type, shapes and depth. Switch any time on the Appearance page.",
                "每個視窗都有四套完整風格，各有自己嘅字體、形狀同層次。隨時可以喺「外觀」頁轉。");
            Add("Pick one of the looks.", "請揀其中一個風格。");
            Add("The whole style: type, shapes, outlines and depth. Picking one also picks the colour it was drawn with.",
                "成個風格：字體、形狀、框線同層次。揀咗之後會一齊揀返佢設計時用嘅顏色。");
            Add("A guided way to set the look, theme, seed colour, contrast, density and text size together, with a review of what changes before anything is applied.",
                "引導式一次過設定風格、主題、種子顏色、對比、密度同字體大小，套用之前會先俾你睇清楚有咩會改。");
            Add("Applying the look changes only the six choices above. Reset to defaults on this page undoes it.",
                "套用呢個外觀只會改上面六個選擇。喺呢頁撳「回復預設」就可以還原。");
            Add("The whole style of every window: type, shapes, outlines and depth. Graphite, Instrument, Blueprint or Pop.",
                "每個視窗嘅整體風格：字體、形狀、框線同層次。石墨、儀器、藍圖或者波普。");

            // labels that were written in capitals and are now in sentence case
            Add("Original and new", "原本同新");
            Add("Preview: what gets typed", "預覽：會打出嚟嘅字");
            t["First :push"] = "第一次 :push";
            Add("Look (optional)", "風格（可選）"); Add("DNS (optional)", "DNS（可選）"); Add("Commands (optional)", "指令（可選）");
        }

        private static void AddLooksPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));

            string Name(string en) => en switch { "Graphite" => "石墨", "Instrument" => "儀器", "Blueprint" => "藍圖", "Pop" => "波普", _ => en };
            AddFn(@"^Look: (Graphite|Instrument|Blueprint|Pop)$", m => "風格：" + Name(m.Groups[1].Value));
            AddFn(@"^Look: (Graphite|Instrument|Blueprint|Pop) \(in use\)$", m => "風格：" + Name(m.Groups[1].Value) + "（用緊）");
            AddFn(@"^Look: (Graphite|Instrument|Blueprint|Pop) to (Graphite|Instrument|Blueprint|Pop)$", m => "風格：由" + Name(m.Groups[1].Value) + "改做" + Name(m.Groups[2].Value));
            AddFn(@"^(Graphite|Instrument|Blueprint|Pop) preview$", m => Name(m.Groups[1].Value) + "預覽");
        }
    }
}
