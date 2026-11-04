using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The Hong Kong Cantonese table and patterns for the Minecraft command builder: the task gallery, the field labels and
    /// explanations, the choices, the status line and the replies. Command words, ids, player names and selectors stay as
    /// they are.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddMcCommands(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- frame, modes and gallery
            Add("Command builder", "指令砌法"); Add("Tasks", "任務"); Add("Every command", "所有指令"); Add("Builder mode", "砌法模式");
            Add("Tasks: forms for what people most often want to do", "任務：大家最常做嘅嘢，一格格填");
            Add("Every command: walk the server's whole command tree", "所有指令：行勻伺服器成棵指令樹");
            Add("Back to the console", "返去主控台"); Add("Search tasks and commands", "搜尋任務同指令"); Add("Favourites", "我的最愛");
            Add("Players", "玩家"); Add("World", "世界"); Add("Rules", "規則"); Add("Server", "伺服器");
            Add("No task matches. Every command has all of them.", "冇任務符合。「所有指令」有齊晒。");
            Add("Bedrock servers do not report a command list, so their console takes typed lines.", "Bedrock 伺服器唔會報告指令清單，所以佢嘅主控台要自己打指令。");
            Add("The Bedrock server has no command list to build from. Type its commands in the console instead.", "Bedrock 伺服器冇指令清單可以用嚟砌。請喺主控台直接打指令。");
            Add("Loading the command list…", "載入緊指令清單…"); Add("Reading the command list from the server jar…", "由伺服器 jar 讀緊指令清單…");
            Add("Choose a command", "揀一個指令"); Add("Find a command", "搵指令"); Add("Command so far", "目前嘅指令"); Add("What can come next", "下一步可以加乜");
            Add("Choose a command above.", "喺上面揀一個指令。"); Add("This command is complete.", "呢個指令已經完整。"); Add("It can run as it is, or take more:", "可以直接執行，或者再加：");
            Add("Next:", "下一步："); Add("Choose a command.", "揀一個指令。");
            Add("This command takes nothing more. Check the line below and run it.", "呢個指令唔使再填嘢。睇吓下面嗰行，然後執行。");

            // ---- the tasks
            Add("Give an item", "俾物品"); Add("Puts items straight into a player's inventory.", "將物品直接放入玩家嘅背包。");
            Add("Teleport", "傳送"); Add("Moves players to a place, exactly or relative to where they stand.", "將玩家傳送去一個位置，可以係準確座標，或者相對佢哋而家企嘅位置。");
            Add("Give an effect", "俾效果"); Add("Speed, night vision, regeneration and the rest, for a time you choose.", "速度、夜視、回復同其他效果，時間由你揀。");
            Add("Clear an effect", "清除效果"); Add("Removes one effect, or every effect when none is chosen.", "移除一個效果；冇揀就移除全部效果。");
            Add("Change game mode", "轉遊戲模式"); Add("Survival, creative, adventure or spectator for chosen players.", "幫揀咗嘅玩家轉做生存、創造、冒險或者旁觀模式。");
            Add("Enchant the held item", "附魔手上嘅物品"); Add("Adds an enchantment to the item a player holds; it has to suit that item.", "幫玩家手上嘅物品加附魔；附魔要啱嗰件物品先得。");
            Add("Experience", "經驗"); Add("Adds or sets experience levels or points.", "增加或者設定經驗等級或者經驗值。");
            Add("Kick a player", "踢走玩家"); Add("Disconnects players with an optional reason. They can join again.", "斷開玩家連線，可以加理由。佢哋可以再加入。");
            Add("Ban a player", "封禁玩家"); Add("Disconnects a player and keeps them out until they are pardoned.", "斷開玩家連線，解封之前都唔可以再入。");
            Add("Operator", "管理員"); Add("Makes a player an operator, who can run every command, or removes that.", "將玩家設為可以執行所有指令嘅管理員，或者取消。");
            Add("Whitelist", "白名單"); Add("Adds a player to the whitelist or removes them from it.", "將玩家加入白名單或者由白名單移除。");
            Add("Set the time", "設定時間"); Add("Day, noon, night, midnight or an exact time.", "日頭、中午、夜晚、半夜，或者準確時間。");
            Add("Change the weather", "轉天氣"); Add("Clear, rain or thunder, for a while or until it changes on its own.", "晴天、落雨或者行雷，維持一段時間或者等佢自己轉。");
            Add("Summon a mob", "召喚生物"); Add("Spawns any entity at a position.", "喺一個位置生成任何實體。");
            Add("Set a block", "放方塊"); Add("Places one block at a position.", "喺一個位置放一個方塊。");
            Add("Fill an area", "填滿範圍"); Add("Fills the box between two corners with one block. A box holds at most 32,768 blocks.", "用一種方塊填滿兩個角之間嘅範圍。一次最多 32,768 個方塊。");
            Add("Set the world spawn", "設定世界出生點"); Add("Where new players and players without a bed appear.", "新玩家同冇床嘅玩家會喺度出現。");
            Add("Set a game rule", "設定遊戲規則"); Add("Keep inventory, mob griefing, daylight cycle and every other rule this version has.", "死亡保留物品、生物破壞、日夜循環同呢個版本嘅所有其他規則。");
            Add("Difficulty", "難度"); Add("Peaceful, easy, normal or hard, at once for everyone.", "和平、簡單、普通或者困難，即刻對所有人生效。");
            Add("Say in chat", "喺聊天講嘢"); Add("Sends a message to everyone, from the server.", "以伺服器名義向所有人發訊息。");
            Add("Show a title", "顯示標題"); Add("Shows large text, a subtitle or a line above the hotbar to chosen players.", "向揀咗嘅玩家顯示大字、副標題，或者快捷欄上面一行字。");
            Add("Save the world", "儲存世界"); Add("Writes the world to disk now.", "即刻將世界寫入磁碟。");
            Add("List players", "列出玩家"); Add("Asks the server who is online.", "問伺服器邊個喺線。");
            Add("Stop the server", "停止伺服器"); Add("Saves the world and stops the server. Players are disconnected.", "儲存世界然後停止伺服器。玩家會斷線。");

            // ---- field labels
            Add("Who", "邊個"); Add("Item", "物品"); Add("How many", "幾多個"); Add("Where", "位置"); Add("Effect", "效果"); Add("Seconds", "秒");
            Add("Strength", "強度"); Add("Game mode", "遊戲模式"); Add("Enchantment", "附魔"); Add("Level", "等級"); Add("Amount", "數量");
            Add("Reason", "理由"); Add("Exact time", "準確時間"); Add("How long", "幾耐"); Add("Mob", "生物"); Add("Block", "方塊");
            Add("From corner", "起點角"); Add("To corner", "終點角"); Add("Value", "值"); Add("Message", "訊息"); Add("Text", "文字");
            Add("Change", "改法"); Add("Count in", "計法"); Add("Time", "時間"); Add("Weather", "天氣"); Add("How to place", "點樣放");
            Add("How to fill", "點樣填"); Add("Rule", "規則"); Add("Where it shows", "喺邊度顯示");
            foreach (var l in new[] { "Who", "Item", "How many", "Where", "Effect", "Seconds", "Strength", "Game mode", "Enchantment", "Level", "Amount", "Reason", "Exact time", "How long", "Mob", "Block", "From corner", "To corner", "Value", "Message", "Text", "Change", "Count in", "Operator", "Whitelist", "Time", "Weather", "How to place", "How to fill", "Rule", "Difficulty", "Where it shows" })
                if (t.TryGetValue(l, out var y)) Add(l + " (optional)", y + "（可選）");

            // ---- choices and their explanations
            Add("Add", "加"); Add("Set", "設定"); Add("Levels", "等級"); Add("Points", "經驗值"); Add("Op", "設為管理員"); Add("Deop", "取消管理員");
            Add("Remove", "移除"); Add("Day", "日頭"); Add("Noon", "中午"); Add("Night", "夜晚"); Add("Midnight", "半夜"); Add("Clear", "清除");
            Add("Rain", "落雨"); Add("Thunder", "行雷"); Add("Replace", "取代"); Add("Keep", "保留"); Add("Destroy", "破壞"); Add("Hollow", "中空");
            Add("Outline", "外框"); Add("Peaceful", "和平"); Add("Easy", "簡單"); Add("Normal", "普通"); Add("Hard", "困難"); Add("Title", "標題");
            Add("Subtitle", "副標題"); Add("Actionbar", "快捷欄上方"); Add("Survival", "生存"); Add("Creative", "創造"); Add("Adventure", "冒險");
            Add("Spectator", "旁觀"); Add("Leave out", "唔加"); Add("True", "是"); Add("False", "否"); Add("Ticks", "刻"); Add("Days", "日");
            Add("Unit", "單位"); Add("Any", "任何"); Add("Nearest", "最近"); Add("Furthest", "最遠"); Add("Random", "隨機"); Add("Arbitrary", "任意");
            Add("Any game mode", "任何遊戲模式"); Add("Order", "次序"); Add("Only players in", "只計呢個模式嘅玩家");
            Add("Add puts more on top of what they have; set replaces it.", "加係喺原有之上再加；設定係取代原有嘅。");
            Add("Levels are the number above the hotbar; points fill the bar between levels.", "等級係快捷欄上面嗰個數字；經驗值係填滿兩級之間嗰條棒。");
            Add("Op makes them an operator, who can run every command; deop takes that away.", "設為管理員之後佢可以執行所有指令；取消管理員就收返呢個權限。");
            Add("Add lets them join while the whitelist is on; remove takes them off it.", "加入之後白名單開住都可以入；移除就由白名單刪走。");
            Add("Day is 1000, noon 6000, night 13000 and midnight 18000. An exact time is in ticks: 20 ticks are a second and a whole day is 24000.", "日頭係 1000，中午 6000，夜晚 13000，半夜 18000。準確時間用刻計：20 刻係一秒，一日係 24000。");
            Add("Without a length, the weather lasts a random while and then changes on its own.", "唔填長度，天氣會維持隨機一段時間，然後自己轉。");
            Add("Replace overwrites what is there; keep only places into air; destroy breaks the old block as a player would, so it drops.", "取代會蓋過原有嘅；保留只會放喺空氣位；破壞會好似玩家咁打爛舊方塊，所以會跌落嚟。");
            Add("Replace fills every block; hollow and outline keep only the shell; keep only fills air; destroy drops what it breaks.", "取代會填滿每一格；中空同外框只做外殼；保留只填空氣位；破壞會令打爛嘅嘢跌出嚟。");
            Add("Peaceful has no hostile mobs and refills hunger; hard makes mobs stronger and lets hunger take all your health.", "和平冇敵對生物，飢餓會自己回復；困難令生物更強，餓到可以餓死。");
            Add("A title is large in the middle of the screen, a subtitle sits under it, and the action bar is a line above the hotbar. A subtitle shows with the next title.", "標題係畫面中間嘅大字，副標題喺佢下面，快捷欄上方係快捷欄上面一行字。副標題會同下一個標題一齊出。");
            Add("Survival is the normal game; creative has unlimited blocks and flight; adventure stops block breaking; spectator flies through everything unseen.", "生存係正常玩法；創造有無限方塊又可以飛；冒險唔可以打爛方塊；旁觀可以隱形穿過所有嘢。");
            Add("True turns it on, false turns it off.", "是即係開，否即係關。");
            Add("Ticks are the game's own unit: 20 ticks are one second, and a game day is 24000 ticks.", "刻係遊戲自己嘅單位：20 刻係一秒，遊戲一日係 24000 刻。");
            Add("Which players come first when there are more than the limit.", "人數多過上限嗰陣，邊啲玩家排先。");
            Add("Only counts players in this game mode.", "只計呢個遊戲模式嘅玩家。");
            Add("Choose one of the words this command takes here.", "揀一個呢個指令喺呢度接受嘅字。");

            // ---- who
            Add("Or type a name", "或者打個名"); Add("Filters", "篩選"); Add("Hide the selector filters", "收埋選擇器篩選"); Add("Filter the selector by distance, number, order and game mode", "按距離、人數、次序同遊戲模式篩選選擇器");
            Add("Within this many blocks", "喺呢個方塊數以內"); Add("At most this many", "最多呢個數目"); Add("blocks", "格");
            Add("Another player's name", "其他玩家名"); Add("A player who is not online now; letters, digits and _, up to 16", "而家唔喺線嘅玩家；字母、數字同 _，最多 16 個字");
            Add("Choose a player who is online, a selector, or type a name.", "揀一個喺線嘅玩家、一個選擇器，或者打個名。");
            Add("No one is online now. Choose a selector, or type a name.", "而家冇人喺線。揀一個選擇器，或者打個名。");
            Add("The player nearest to where the command runs. From the console that is the nearest to the world spawn.", "離執行指令位置最近嘅玩家。由主控台執行就係離世界出生點最近嗰個。");
            Add("Every player online.", "所有喺線玩家。"); Add("One random player.", "隨機一個玩家。");
            Add("Whoever runs the command. From the console that is no one, so most commands fail with it.", "執行指令嗰個人。由主控台執行就冇人，所以大部分指令會失敗。");
            Add("Every entity: players, mobs, items on the ground and the rest.", "所有實體：玩家、生物、地上嘅物品同其他。");

            // ---- positions
            Add("Relative: ~ means where the target is, ~5 means five blocks further along this axis.", "相對：~ 即係目標嘅位置，~5 即係沿住呢條軸再行五格。");
            Add("A whole number, or an offset when ~ is on", "整數，開咗 ~ 就係偏移"); Add("A number, or an offset when ~ is on", "數字，開咗 ~ 就係偏移");
            Add("Use a player's position", "用玩家嘅位置"); Add("No one is online, so there is no player's position to use.", "冇人喺線，所以冇玩家位置可以用。");
            Add("Sample server: positions are not read from a sample.", "示範伺服器：唔會由示範讀位置。");
            Add("The server is not running, so a player's position cannot be read.", "伺服器冇運行，所以讀唔到玩家位置。");

            // ---- lists
            Add("This version's lists have not been read, so type the id.", "未讀取呢個版本嘅清單，所以請自己打 id。");
            Add("item", "物品"); Add("block", "方塊"); Add("mob effect", "效果"); Add("entity type", "實體"); Add("enchantment", "附魔"); Add("damage type", "傷害類型");

            // ---- hints of the argument types
            Add("a whole number", "整數"); Add("a number", "數字"); Add("true or false", "是或否"); Add("text to the end of the line", "直到行尾嘅文字");
            Add("a word or a quoted phrase", "一個字或者加引號嘅句子"); Add("one word", "一個字"); Add("a player name or a selector such as @a", "玩家名或者 @a 之類嘅選擇器");
            Add("a player name, a selector or *", "玩家名、選擇器或者 *"); Add("a message", "訊息"); Add("a time such as 1000, 2d or 300s", "時間，例如 1000、2d 或者 300s");
            Add("three coordinates such as ~ ~ ~", "三個座標，例如 ~ ~ ~"); Add("two coordinates such as ~ ~", "兩個座標，例如 ~ ~");
            Add("an item id such as minecraft:diamond", "物品 id，例如 minecraft:diamond"); Add("a block id such as minecraft:stone", "方塊 id，例如 minecraft:stone");
            Add("a block id or a block tag", "方塊 id 或者方塊標籤"); Add("survival, creative, adventure or spectator", "survival、creative、adventure 或者 spectator");
            Add("a dimension such as minecraft:the_nether", "維度，例如 minecraft:the_nether"); Add("a text", "一段文字"); Add("an angle in degrees", "角度（度）");
            Add("an id from the game's list", "遊戲清單入面嘅 id");

            // ---- preview and actions
            Add("The command", "指令"); Add("Each part is coloured by what it is. Click a part to go to its field.", "每部分按類型上色。撳一部分就會跳去佢嘅欄位。");
            Add("The exact command line", "準確嘅指令行"); Add("Save as a favourite", "加入我的最愛"); Add("Schedule it", "排程執行");
            Add("Runs it through the server's console. Ctrl+Enter does the same.", "經伺服器主控台執行。Ctrl+Enter 都得。");
            Add("Command", "指令"); Add("Target", "目標"); Add("Number", "數字"); Add("the command", "指令本身"); Add("not filled in", "未填");
            Add("Not run: it was not confirmed.", "冇執行：未確認。"); Add("There is no command to send.", "冇指令可以傳送。");
            Add("Sent. The server did not answer in the console.", "已傳送。伺服器冇喺主控台回覆。");
            Add("The world is saved and the server stops. Everyone on it is disconnected.", "世界會儲存，然後伺服器停止。上面所有人都會斷線。");

            // ---- console
            Add("Build any command with proper controls: players and selectors, items, blocks, effects and mobs from this version's own lists, numbers with their real limits and positions with relative axes. There is no free shell field.",
                "用適合嘅控制項砌任何指令：玩家同選擇器，呢個版本自己清單入面嘅物品、方塊、效果同生物，有真實上下限嘅數字，同埋可以設相對軸嘅位置。冇任意輸入嘅命令欄。");
            Add("Open the command builder", "打開指令砌法"); Add("Favourites:", "我的最愛：");

            // ---- schedules
            Add("Run a command", "執行指令");
            Add("A restart warns players first; a backup copies the world; a message is said in chat; a command runs a line from the command builder; an update check only tells you about a newer build.",
                "重新啟動會先通知玩家；備份會複製世界；訊息會喺聊天講；指令會執行指令砌法砌好嘅一行；檢查更新只會話你知有冇新版本。");
            Add("The command line to run, as the command builder makes it", "要執行嘅指令行，即係指令砌法砌出嚟嗰行");
            Add("Build it in the command builder", "喺指令砌法砌");
            Add("It runs through the server's console and is skipped, and recorded as skipped, when the server is not running.", "會經伺服器主控台執行；伺服器冇運行嗰陣會略過，並記錄為略過。");
            Add("Type the command, or build it in the command builder.", "打指令，或者喺指令砌法砌。");
            Add("Skipped: the schedule has no command.", "略過：排程冇指令。");
        }

        private static void AddMcCommandsPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline), fn));
            string Inner(string s) => Language.Yue(s) ?? s;

            AddFn(@"^(\S+) (\S+)\. Every field comes from this server's own command list, players and registries\.$", m => m.Groups[1].Value + " " + m.Groups[2].Value + "。每一格都嚟自呢個伺服器自己嘅指令清單、玩家同登錄表。");
            Add(@"^Complete and valid for (\S+)\. Ready to run\.$", "完整，喺 $1 有效。可以執行。");
            Add(@"^Run on (.+)$", "喺 $1 執行");
            Add(@"^Run (/.+) on (.+)\?$", "喺 $2 執行 $1？");
            AddFn(@"^Fill in (.+) first, or clear (.+)\.$", m => "請先填" + Inner(m.Groups[1].Value) + "，或者清除" + Inner(m.Groups[2].Value) + "。");
            AddFn(@"^Fill in (.+)\.$", m => "請填" + Inner(m.Groups[1].Value) + "。");
            AddFn(@"^(.+) must be at least (\S+)\.$", m => Inner(m.Groups[1].Value) + "最少要係 " + m.Groups[2].Value + "。");
            AddFn(@"^(.+) must be at most (\S+)\.$", m => Inner(m.Groups[1].Value) + "最多只可以係 " + m.Groups[2].Value + "。");
            AddFn(@"^(.+) must be a whole number\.$", m => Inner(m.Groups[1].Value) + "要係整數。");
            AddFn(@"^(.+) must be a number\.$", m => Inner(m.Groups[1].Value) + "要係數字。");
            AddFn(@"^(.+) must be true or false\.$", m => Inner(m.Groups[1].Value) + "要係 true 或者 false。");
            AddFn(@"^(.+) is one word\.$", m => Inner(m.Groups[1].Value) + "只可以係一個字。");
            AddFn(@"^(.+) needs (\d) coordinates\.$", m => Inner(m.Groups[1].Value) + "要 " + m.Groups[2].Value + " 個座標。");
            AddFn(@"^(.+) must be a number of ticks, or a number followed by d, s or t\.$", m => Inner(m.Groups[1].Value) + "要係刻數，或者數字後面加 d、s 或者 t。");
            AddFn(@"^(.+) takes players only, so @e cannot be used here\.$", m => Inner(m.Groups[1].Value) + "只接受玩家，所以呢度唔可以用 @e。");
            AddFn(@"^(.+) takes one target, so (@\w) needs limit=1\.$", m => Inner(m.Groups[1].Value) + "只接受一個目標，所以 " + m.Groups[2].Value + " 要加 limit=1。");
            AddFn(@"^(.+) must be a player name \(letters, digits and _, up to 16\) or a selector such as @a\.$", m => Inner(m.Groups[1].Value) + "要係玩家名（字母、數字同 _，最多 16 個字）或者 @a 之類嘅選擇器。");
            AddFn(@"^(.+) must be an item id such as minecraft:diamond\.$", m => Inner(m.Groups[1].Value) + "要係物品 id，例如 minecraft:diamond。");
            AddFn(@"^(.+) must be a block id such as minecraft:stone\.$", m => Inner(m.Groups[1].Value) + "要係方塊 id，例如 minecraft:stone。");
            AddFn(@"^(.+) must be an id such as minecraft:name\.$", m => Inner(m.Groups[1].Value) + "要係 id，例如 minecraft:name。");
            AddFn(@"^(.+) must be survival, creative, adventure or spectator\.$", m => Inner(m.Groups[1].Value) + "要係 survival、creative、adventure 或者 spectator。");
            AddFn(@"^(.+) cannot mix \^ with other coordinates\.$", m => Inner(m.Groups[1].Value) + "唔可以將 ^ 同其他座標混埋用。");
            AddFn(@"^(.+): ""(.+)"" is not a (whole number|number), ~ or ~ with a number\.$", m => Inner(m.Groups[1].Value) + "：「" + m.Groups[2].Value + "」唔係" + (m.Groups[3].Value == "number" ? "數字" : "整數") + "、~ 或者 ~ 加數字。");
            AddFn(@"^(\S+) is not in this version's (.+) list\.$", m => m.Groups[1].Value + " 唔喺呢個版本嘅" + Inner(m.Groups[2].Value) + "清單入面。");
            AddFn(@"^This command needs more: (.+)\.$", m => "呢個指令仲要加：" + string.Join(" 或者 ", Array.ConvertAll(m.Groups[1].Value.Split(" or "), Inner)) + "。");
            Add(@"^This version has no ""(.+)"" command\.$", "呢個版本冇「$1」指令。");
            AddFn(@"^This version's command has no (.+) here\.$", m => "呢個版本嘅指令喺呢度冇" + Inner(m.Groups[1].Value) + "。");
            AddFn(@"^(.+) Every command lists what this version has\.$", m => Inner(m.Groups[1].Value) + "「所有指令」會列出呢個版本有嘅嘢。");
            AddFn(@"^All (\d+) commands of this server, walked step by step from its own command tree\. (.+)$", m => "呢個伺服器全部 " + m.Groups[1].Value + " 個指令，跟佢自己嘅指令樹一步步行。每個參數都有啱佢類型嘅控制項：有上下限嘅整數、是或否、玩家或者選擇器、軸座標位置、由遊戲登錄表嚟可以搜尋嘅物品、方塊、效果、附魔同實體清單，同埋文字框。外掛或者模組加嘅指令只會喺伺服器有報告時先出現。");
            AddFn(@"^a whole number from (\S+) to (\S+)$", m => m.Groups[1].Value + " 至 " + m.Groups[2].Value + " 嘅整數");
            AddFn(@"^a whole number from (\S+)$", m => m.Groups[1].Value + " 或以上嘅整數");
            AddFn(@"^a whole number to (\S+)$", m => m.Groups[1].Value + " 或以下嘅整數");
            AddFn(@"^a number from (\S+) to (\S+)$", m => m.Groups[1].Value + " 至 " + m.Groups[2].Value + " 嘅數字");
            AddFn(@"^a number from (\S+)$", m => m.Groups[1].Value + " 或以上嘅數字");
            AddFn(@"^a value of type (\S+)$", m => m.Groups[1].Value + " 類型嘅值");
            AddFn(@"^(.+) · (minecraft:\S+|brigadier:\S+)$", m => Inner(m.Groups[1].Value) + " · " + m.Groups[2].Value);
            AddFn(@"^(Command|Target|Number|Value) · (.+?)(: (.+))?$", m => Inner(m.Groups[1].Value) + " · " + Inner(m.Groups[2].Value) + (m.Groups[3].Success ? "：" + Inner(m.Groups[4].Value) : ""));
            AddFn(@"^([\d,]+) (items|blocks|effects|entities|enchantments|dimensions|particles|sounds|biomes|attributes|entries) in (\S+)$", m => m.Groups[3].Value + " 有 " + m.Groups[1].Value + " 個" + Kind(m.Groups[2].Value));
            AddFn(@"^Search (items|blocks|effects|entities|enchantments|dimensions|particles|sounds|biomes|attributes|entries|rules)$", m => "搜尋" + Kind(m.Groups[1].Value));
            AddFn(@"^(\d+) rules in (\S+), from its own command list\.$", m => m.Groups[2].Value + " 有 " + m.Groups[1].Value + " 條規則，嚟自佢自己嘅指令清單。");
            AddFn(@"^(\d+) more; narrow the search\.$", m => "仲有 " + m.Groups[1].Value + " 個；請收窄搜尋。");
            AddFn(@"^The (.+) list is not in this version's reports, so type the id\.$", m => "呢個版本嘅報告冇" + Inner(m.Groups[1].Value) + "清單，所以請自己打 id。");
            Add(@"^Use (\S+)'s position$", "用 $1 嘅位置");
            Add(@"^Reading (\S+)'s position…$", "讀緊 $1 嘅位置…");
            Add(@"^(\S+) is at (.+)\.$", "$1 喺 $2。");
            Add(@"^(\S+) is online now\.$", "$1 而家喺線。");
            Add(@"^(\S+) is not online now; that is fine for this command\.$", "$1 而家唔喺線；呢個指令冇問題。");
            Add(@"^(\S+) is not online now, so the command finds no one unless they join first\.$", "$1 而家唔喺線，除非佢先加入，否則指令搵唔到人。");
            AddFn(@"^The server did not give a position: (.+)$", m => "伺服器冇俾位置：" + m.Groups[1].Value);
            AddFn(@"^(X|Y|Z|Yaw|Pitch) is (relative to the target|exact)$", m => m.Groups[1].Value + (m.Groups[2].Value == "exact" ? " 係準確座標" : " 相對目標"));
            Add(@"^Copied (/.+)\.$", "已複製 $1。");
            Add(@"^Saved (/.+) as a favourite\. It is at the top of the tasks and in the console\.$", "已將 $1 加入我的最愛。佢喺任務最上面同主控台入面。");
            Add(@"^(/.+) does not fit this version's command list, so it cannot be opened in the builder\. It can still be run from the console's favourites\.$", "$1 唔啱呢個版本嘅指令清單，所以唔可以喺砌法打開。仍然可以喺主控台嘅我的最愛執行。");
            Add(@"^Open favourite (/.+)$", "打開我的最愛 $1"); Add(@"^Remove favourite (/.+)$", "移除我的最愛 $1");
            Add(@"^Run favourite: (/.+)$", "執行我的最愛：$1");
            Add(@"^Build (/\S+) in Every command$", "喺「所有指令」砌 $1");
            Add(@"^Remove (\S+) and what follows$", "移除 $1 同後面嘅部分");
            AddFn(@"^(\d+) (tasks|rules|items|blocks|effects|entities|enchantments|dimensions|entries)$", m => m.Groups[1].Value + " 個" + Kind(m.Groups[2].Value));
            AddFn(@"^(.+) \((/[a-z\-]+)\)$", m => Language.Yue(m.Groups[1].Value) is string y ? y + "（" + m.Groups[2].Value + "）" : null);
            AddFn(@"^(\d+) of (\d+) (tasks|rules|items|blocks|effects|entities|enchantments|dimensions|entries)$", m => m.Groups[1].Value + " / " + m.Groups[2].Value + " 個" + Kind(m.Groups[3].Value));
        }

        private static string Kind(string plural) => plural switch
        {
            "items" => "物品", "blocks" => "方塊", "effects" => "效果", "entities" => "實體", "enchantments" => "附魔", "dimensions" => "維度",
            "particles" => "粒子", "sounds" => "聲音", "biomes" => "生態域", "attributes" => "屬性", "rules" => "規則", "tasks" => "任務", _ => "項目",
        };
    }
}
