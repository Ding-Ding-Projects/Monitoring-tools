using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The Hong Kong Cantonese table and patterns for the item component editor, the NBT editor, plugin and mod commands read from
    /// the live server, the peak players history and the pre-generation pause. Ids, command words and names a server prints stay as they are.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddMcLane(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- item components
            Add("Item components", "物品組件"); Add("Item data (NBT)", "物品資料（NBT）");
            Add("Custom name, lore, enchantments, damage and other components of the item", "物品嘅自訂名稱、描述文字、附魔、損耗同其他組件");
            Add("Item data written as NBT", "用 NBT 寫嘅物品資料");
            Add("Custom name", "自訂名稱"); Add("Replaces the name shown for the item.", "取代物品顯示嘅名稱。");
            Add("A name shown instead of the item's own", "顯示喺物品原名位置嘅名稱");
            Add("Lore", "描述文字"); Add("Grey lines of text under the item's name. One line per row.", "喺物品名下面嘅灰色文字。每行一列。");
            Add("Lore, one line per row", "描述文字，每行一列"); Add("Lines of text under the item's name", "喺物品名下面嘅文字");
            Add("Enchantments", "附魔"); Add("Enchantment", "附魔"); Add("Enchantment level", "附魔等級"); Add("Enchantment id", "附魔 id");
            Add("An enchantment id such as minecraft:sharpness", "附魔 id，例如 minecraft:sharpness");
            Add("Each enchantment with its level. Adding one that is already there changes its level.", "每個附魔連等級。加入已經有嘅附魔會改佢嘅等級。");
            Add("Add enchantment", "加入附魔"); Add("Enchantments on the item", "物品上嘅附魔");
            Add("Damage", "損耗"); Add("How worn the item is, in points. Leave empty for undamaged.", "物品損耗幾多點。留空即係冇損耗。");
            Add("Unbreakable", "不可破壞"); Add("The item never loses durability.", "物品永遠唔會損耗耐久。");
            Add("Custom model data", "自訂模型資料");
            Add("A number a resource pack can use to pick a different model.", "資源包可以用呢個數字揀另一個模型。");
            Add("A number a resource pack can use to pick a different model. Written as the first float, which this version reads.", "資源包可以用呢個數字揀另一個模型。會寫成第一個浮點數，呢個版本讀得到。");
            Add("Other components", "其他組件"); Add("Component", "組件"); Add("Component id", "組件 id"); Add("Component value", "組件值");
            Add("A component id such as minecraft:max_stack_size", "組件 id，例如 minecraft:max_stack_size");
            Add("The value as SNBT, like 16 or {} or \"text\"", "SNBT 寫法嘅值，例如 16、{} 或者 \"text\"");
            Add("Add component", "加入組件"); Add("Other components on the item", "物品上嘅其他組件");
            Add("This version's component list has not been read, so type the id. The value is SNBT.", "未讀過呢個版本嘅組件清單，所以請自己打 id。值要用 SNBT 寫。");
            Add("Choose an enchantment first.", "請先揀一個附魔。"); Add("The level must be from 1 to 255.", "等級要喺 1 至 255 之間。");
            Add("Choose a component first.", "請先揀一個組件。");
            Add("The value is not valid SNBT. Numbers, \"text\", {…} and […] are.", "呢個值唔係有效嘅 SNBT。數字、\"文字\"、{…} 同 […] 先係。");
            Add("No components. The item is written as just its id.", "冇組件。物品只會寫成佢嘅 id。");
            Add("Item components go in square brackets, like [minecraft:damage=3].", "物品組件要放喺方括號入面，例如 [minecraft:damage=3]。");
            Add("This version takes item data as components in square brackets, not NBT.", "呢個版本嘅物品資料用方括號入面嘅組件，唔係 NBT。");
            Add("Item data here is NBT in curly braces, like {Damage:3}.", "呢個版本嘅物品資料係花括號入面嘅 NBT，例如 {Damage:3}。");

            // ---- the NBT editor
            Add("Add key", "加入鍵"); Add("Add item", "加入項目"); Add("NBT as text", "文字形式嘅 NBT"); Add("NBT in curly braces", "花括號入面嘅 NBT");
            Add("Start again with the tree", "用樹狀編輯器重新開始");
            Add("This value uses a form the tree editor does not build (for example a typed array), so it stays as text. Edit it here, or start again with the tree.", "呢個值用咗樹狀編輯器砌唔到嘅寫法（例如有類型嘅陣列），所以保持文字。可以喺度改，或者用樹狀編輯器重新開始。");
            Add("Nothing yet. Add a key to start.", "仲未有嘢。加入一個鍵開始。");
            Add("The name of this value, like Invulnerable or CustomName", "呢個值嘅名，例如 Invulnerable 或者 CustomName");
            Add("A whole number from -128 to 127. Also used for true (1) and false (0).", "-128 至 127 嘅整數。亦用嚟表示 true（1）同 false（0）。");
            Add("A whole number from -32768 to 32767.", "-32768 至 32767 嘅整數。");
            Add("A whole number from -2147483648 to 2147483647.", "-2147483648 至 2147483647 嘅整數。");
            Add("A very large whole number.", "好大嘅整數。"); Add("A number with a decimal part, single precision.", "有小數嘅數字，單精度。");
            Add("A number with a decimal part, double precision.", "有小數嘅數字，雙精度。"); Add("Text.", "文字。");
            Add("An ordered list of values that are all the same type.", "一串有次序、類型相同嘅值。"); Add("A set of named values.", "一組有名嘅值。");

            // ---- plugin and mod commands from the live server
            Add("Plugins and mods", "外掛同模組");
            Add("Asking the server for its commands…", "問緊伺服器有咩指令…"); Add("Not read yet.", "仲未讀。");
            Add("Start the server and its plugin and mod commands appear here, read from the live server.", "啟動伺服器之後，外掛同模組嘅指令會顯示喺度，由運作中嘅伺服器讀取。");
            Add("Read the server's commands", "讀取伺服器嘅指令"); Add("Read the server's commands again", "再讀取伺服器嘅指令");
            Add("Sample commands, shown to illustrate the form. Nothing was asked of a real server.", "示範指令，用嚟展示表格。冇向真正嘅伺服器查詢過。");
            Add("From the live server. The fields follow the usage text the server printed.", "嚟自運作中嘅伺服器。欄位跟伺服器印出嘅用法文字。");
            Add("Usage the server printed", "伺服器印出嘅用法");
            Add("A command the running server reports. Its form comes from the usage text the server printed.", "運作中嘅伺服器報告嘅指令。表格嚟自伺服器印出嘅用法文字。");
            Add("The server did not answer help.", "伺服器冇回應 help。");
            Add("The server answered help, but no command in its answer is missing from the jar's own list.", "伺服器回應咗 help，但係答案入面冇一個指令係 jar 自己清單冇嘅。");
            // sample server text, shown only for the sample servers
            Add("Teleports to one of your homes", "傳送去你其中一個家"); Add("Stops a player from chatting", "禁止玩家傾偈"); Add("Teleports to a named warp", "傳送去指定嘅傳送點");
            Add("Pausing after the batch being built. Nothing is lost; Resume carries on from the next batch.", "完成而家砌緊嗰批之後暫停。冇嘢會丟失；按「繼續」就由下一批接住做。");
            Add("Resumed.", "已繼續。");

            // ---- the peak players history
            Add("Players over the last 7 days", "過去 7 日嘅玩家人數"); Add("Peak today", "今日高峰"); Add("Peak in 7 days", "7 日內高峰");
            Add("Peak players, last 7 days", "過去 7 日嘅高峰玩家人數");
            Add("Sample values: nothing was read from a real server.", "示範數值：冇由真正嘅伺服器讀取。");
            Add("The most players online at once on each day, from the list answers this app read. A dash is a day with no reading, not an empty server.", "每日同時喺線嘅最多玩家，嚟自本程式讀到嘅 list 回應。短橫線係冇讀數嘅日子，唔係冇人嘅伺服器。");

            // ---- worlds
            Add("Pause pre-generation", "暫停預先生成"); Add("Resume pre-generation", "繼續預先生成");
            Add("Holds the build between batches of chunks and carries on from the next batch when you resume. The private server stays up while paused.", "喺兩批區塊之間停低，繼續時由下一批接住做。暫停期間私人伺服器會保持運行。");
            Add("Not changed: it was not confirmed.", "冇改動：未有確認。");
        }

        private static string Day(string en) => en switch { "Monday" => "星期一", "Tuesday" => "星期二", "Wednesday" => "星期三", "Thursday" => "星期四", "Friday" => "星期五", "Saturday" => "星期六", _ => "星期日" };

        private static void AddMcLanePatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), fn));
            string Inner(string s) => Language.Yue(s) ?? s;

            // item components
            AddFn(@"^Any of the (\d+) components this version lists\. The value is SNBT\.$", m => "呢個版本列出嘅 " + m.Groups[1].Value + " 個組件任揀。值要用 SNBT 寫。");
            AddFn(@"^(\S+) has its own control above\.$", m => m.Groups[1].Value + " 喺上面有自己嘅控制項。");
            AddFn(@"^(\S+) is not a component in this version's list\.$", m => m.Groups[1].Value + " 唔係呢個版本清單入面嘅組件。");
            AddFn(@"^(\S+) is listed twice\.$", m => m.Groups[1].Value + " 出現咗兩次。");
            AddFn(@"^The value of (\S+) is not valid SNBT\.$", m => m.Groups[1].Value + " 嘅值唔係有效嘅 SNBT。");
            AddFn(@"^""(.*)"" is not a component\. Write name=value\.$", m => "「" + m.Groups[1].Value + "」唔係組件。請寫成 名稱=值。");
            AddFn(@"^""(.*)"" is not a component id\.$", m => "「" + m.Groups[1].Value + "」唔係組件 id。");
            AddFn(@"^(\S+) removes a component, so it takes no value\.$", m => m.Groups[1].Value + " 係移除組件，所以唔使填值。");
            AddFn(@"^(.+) must be NBT in curly braces, like \{Invulnerable:1b\}\.$", m => Inner(m.Groups[1].Value) + "要係花括號入面嘅 NBT，例如 {Invulnerable:1b}。");
            AddFn(@"^Remove (.+) (\d+)$", m => "移除 " + Inner(m.Groups[1].Value) + " " + m.Groups[2].Value);
            AddFn(@"^Remove component (\S+)$", m => "移除組件 " + m.Groups[1].Value);

            // the NBT editor
            AddFn(@"^Add key in (\S+)$", m => "喺 " + m.Groups[1].Value + " 加入鍵");
            AddFn(@"^Add item in (\S+)$", m => "喺 " + m.Groups[1].Value + " 加入項目");
            AddFn(@"^Key of (\S+)$", m => m.Groups[1].Value + " 嘅鍵");
            AddFn(@"^Value of (\S+)$", m => m.Groups[1].Value + " 嘅值");
            AddFn(@"^Type of (\S+)$", m => m.Groups[1].Value + " 嘅類型");
            AddFn(@"^Type (\w+) for (\S+)$", m => m.Groups[2].Value + " 嘅類型：" + m.Groups[1].Value);
            AddFn(@"^Remove (root\S*)$", m => "移除 " + m.Groups[1].Value);
            AddFn(@"^A (byte|short|int|long) is a whole number\.$", m => m.Groups[1].Value + " 要係整數。");
            AddFn(@"^A (byte|short|int|long) is from (-?\d+) to (-?\d+)\.$", m => m.Groups[1].Value + " 要喺 " + m.Groups[2].Value + " 至 " + m.Groups[3].Value + " 之間。");
            AddFn(@"^A (float|double) is a number\.$", m => m.Groups[1].Value + " 要係數字。");
            AddFn(@"^A key in (.+) has no name\.$", m => Inner(m.Groups[1].Value) + "入面有個鍵冇名。");
            AddFn(@"^The key (.+) is used twice in (.+)\.$", m => "鍵 " + m.Groups[1].Value + " 喺" + Inner(m.Groups[2].Value) + "入面用咗兩次。");
            AddFn(@"^(.+) mixes (\w+) and (\w+); a list holds one type\.$", m => Inner(m.Groups[1].Value) + "混咗 " + m.Groups[2].Value + " 同 " + m.Groups[3].Value + "；一個清單只可以放一種類型。");
            AddFn(@"^(\S+): (A .+)$", m => Language.Yue(m.Groups[2].Value) is string y ? m.Groups[1].Value + "：" + y : null);

            // plugin and mod commands
            AddFn(@"^(\d+) commands the running server reports that the jar's own list does not have\. Read from the live server (.+)\.$", m => "運作中嘅伺服器報告咗 " + m.Groups[1].Value + " 個 jar 自己清單冇嘅指令。" + Inner(m.Groups[2].Value) + "由運作中嘅伺服器讀取。");
            AddFn(@"^The server's command list could not be read: (.+)$", m => "讀唔到伺服器嘅指令清單：" + m.Groups[1].Value);

            // the peak players history
            AddFn(@"^Peak (today|in 7 days): (\d+|no reading)$", m => (m.Groups[1].Value == "today" ? "今日高峰：" : "7 日內高峰：") + (m.Groups[2].Value == "no reading" ? "冇讀數" : m.Groups[2].Value));
            AddFn(@"^(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday): (no reading|peak (\d+) players?)$", m => Day(m.Groups[1].Value) + "：" + (m.Groups[3].Success ? "高峰 " + m.Groups[3].Value + " 人" : "冇讀數"));

            // worlds
            AddFn(@"^Paused after batch (\d+) of (\d+)\. Resume to carry on\.$", m => "完成第 " + m.Groups[1].Value + " / " + m.Groups[2].Value + " 批後已暫停。按「繼續」接住做。");
        }
    }
}
