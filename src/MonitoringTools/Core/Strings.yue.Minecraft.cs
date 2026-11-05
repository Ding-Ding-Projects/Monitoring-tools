using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The Hong Kong Cantonese table and patterns for the Minecraft server manager: the server list, the guided create
    /// flow, the overview, console, players, settings, worlds, plugins and mods, backups and schedules, and the messages
    /// the server manager reports. Player, plugin, server and file names, Minecraft keys and command text stay as they are.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddMinecraft(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- the help page's card for the Minecraft docs
            Add("Create, run and look after Java edition servers on this computer, in Docker, or on another machine over SSH.", "喺呢部電腦、Docker，或者經 SSH 喺另一部機度建立、運行同打理 Java 版伺服器。");

            // ---- manager frame, server list and header
            Add("Minecraft server manager", "Minecraft 伺服器管理"); Add("Server list", "伺服器清單"); Add("Server sections", "伺服器分頁");
            Add("Search servers", "搜尋伺服器"); Add("Filter by state", "按狀態篩選"); Add("Running", "運行中"); Add("Stopped", "已停止");
            Add("Starting", "啟動緊"); Add("Checking", "檢查緊"); Add("Missing", "搵唔到"); Add("Host offline", "主機離線"); Add("Offline host", "離線主機");
            Add("All", "全部"); Add("Reconnect", "重新連線"); Add("Restart", "重新啟動");
            Add("No servers yet", "暫時未有伺服器");
            Add("No servers yet. New server walks you through making one.", "暫時未有伺服器。「新增伺服器」會一步步帶你建立。");
            Add("Create a Java edition server on this computer, in Docker, or on another machine over SSH. The guide asks one question at a time and downloads nothing until the last step.",
                "喺呢部電腦、Docker 或者經 SSH 喺另一部機建立 Java 版伺服器。精靈每次只問一條問題，去到最後一步先會下載嘢。");
            Add("Servers are listed here when this app created them. Their files stay in their own folders, and removing one from the list never deletes its world.",
                "由呢個程式建立嘅伺服器會列喺度。佢哋嘅檔案留喺各自嘅資料夾，由清單移除亦永遠唔會刪除世界。");
            Add("on this computer", "喺呢部電腦"); Add("in Docker on this computer", "喺呢部電腦嘅 Docker");
            Add("the host cannot be reached, so its state is unknown", "連唔到主機，所以唔知佢嘅狀態");
            Add("Copied the join address.", "已複製加入位址。"); Add("Copy join address", "複製加入位址");
            Add("The server is stopped first if it runs. Its world, settings and backups stay in their folders; only the entry in this list goes.",
                "如果伺服器運行緊會先停止。佢嘅世界、設定同備份會留喺原本嘅資料夾；只會移除清單入面嘅項目。");
            Add("Remove from the list", "由清單移除"); Add("Remove from the list. The world and files stay.", "由清單移除。世界同檔案會保留。");
            Add("The SSH host could not be reached (sample server).", "連唔到 SSH 主機（示範伺服器）。");
            Add("a process on this computer", "呢部電腦上嘅程序"); Add("The container is not on this computer any more.", "呢部電腦上已經冇呢個容器。");
            Add("The container is not on the host any more.", "主機上已經冇呢個容器。"); Add("container running", "容器運行緊"); Add("container restarting", "容器重新啟動緊");
            Add("Docker is not running on this computer.", "呢部電腦嘅 Docker 冇運行。");

            // ---- overview
            Add("Keep", "保留"); Add("Warnings", "警告");
            Add("Live status", "即時狀態"); Add("Players online", "在線玩家"); Add("Java memory in use", "Java 用緊嘅記憶體"); Add("Uptime", "運行時間");
            Add("Ticks per second", "每秒 tick 數"); Add("Port", "埠"); Add("Players", "玩家"); Add("Memory", "記憶體");
            Add("Needs your attention", "需要你處理"); Add("Nothing needs you right now.", "而家冇嘢要你處理。"); Add("Recent console", "最近主控台");
            Add("Open console", "開啟主控台"); Add("Last server log lines", "伺服器紀錄最後幾行"); Add("Nobody is online.", "冇人在線。");
            Add("No console lines yet. Start the server to see its log here.", "暫時未有主控台紀錄。啟動伺服器就會喺度見到紀錄。");
            Add("No console lines yet. They appear here once the server has run.", "暫時未有主控台紀錄。伺服器運行過之後會喺度出現。");
            Add("not running", "冇運行"); Add("the allowance; not running", "分配量；冇運行"); Add("since the last start", "自上次啟動以來");
            Add("since the container started", "自容器啟動以來"); Add("from the server, last minute", "來自伺服器，最近一分鐘");
            Add("waiting for the list command", "等緊 list 指令"); Add("waiting for the tps command", "等緊 tps 指令"); Add("reading…", "讀取緊…");
            Add("Java process working set", "Java 程序工作集"); Add("Docker did not report memory", "Docker 冇回報記憶體");
            Add("container memory in use", "容器用緊嘅記憶體"); Add("Docker did not report a start time", "Docker 冇回報啟動時間");
            Add("on the SSH host", "喺 SSH 主機上"); Add("open on this computer", "喺呢部電腦開放"); Add("free on this computer", "呢部電腦上冇人用");
            Add("shared with another server here", "同呢度另一個伺服器共用"); Add("in use by another program", "被另一個程式用緊");
            Add("Unavailable", "冇提供"); Add("Checking for a newer build…", "檢查緊有冇較新嘅構建…"); Add("Change the port", "更改埠");
            Add("Back up first", "先備份"); Add("Back up now", "即刻備份");

            // ---- create flow
            Add("Edition and version", "版別同版本"); Add("Software", "軟件"); Add("Name and resources", "名稱同資源"); Add("World", "世界"); Add("Review", "檢查");
            Add("Edition", "版別"); Add("Java edition", "Java 版"); Add("Bedrock edition", "基岩版"); Add("Versions", "版本"); Add("Steps", "步驟");
            Add("This PC", "呢部電腦"); Add("Runs on", "運行位置"); Add("New schedule, step by step", "逐步建立新排程");
            Add("Java edition is every version in Mojang's catalogue plus Paper, Fabric, Forge and others. Bedrock is the dedicated server for phones, consoles and Windows players.",
                "Java 版包括 Mojang 目錄入面嘅所有版本，另外仲有 Paper、Fabric、Forge 等等。基岩版係畀手機、遊戲機同 Windows 玩家用嘅專用伺服器。");
            Add("Every version in Mojang's official catalogue. Nothing is downloaded until the last step.", "Mojang 官方目錄入面嘅所有版本。去到最後一步先會下載嘢。");
            Add("The Bedrock dedicated server is not managed by this app yet. Choose Java edition.", "呢個程式暫時未管理基岩版專用伺服器，請揀 Java 版。");
            Add("Show snapshots", "顯示快照版"); Add("Show old alpha and beta", "顯示舊 Alpha 同 Beta 版"); Add("Search versions", "搜尋版本");
            Add("release", "正式版"); Add("snapshot", "快照版"); Add("old beta", "舊 Beta 版"); Add("old alpha", "舊 Alpha 版");
            Add("Loading Mojang's catalogue…", "載入緊 Mojang 嘅目錄…"); Add("checking for a dedicated server…", "檢查緊有冇專用伺服器…");
            Add("Only software with a build for the chosen version can be picked.", "只可以揀有所選版本構建嘅軟件。");
            Add("Mojang's own server.", "Mojang 官方伺服器。"); Add("Fast, with Bukkit and Spigot plugins.", "速度快，支援 Bukkit 同 Spigot 外掛。");
            Add("Paper with many extra options.", "加入咗好多額外選項嘅 Paper。"); Add("A light mod loader.", "輕量嘅模組載入器。");
            Add("The classic mod loader.", "經典嘅模組載入器。"); Add("A modern fork of Forge.", "Forge 嘅現代分支。");
            Add("from Mojang", "來自 Mojang"); Add("no build for this version", "呢個版本冇構建");
            Add("Each place is checked as you pick it.", "揀嗰陣會逐個位置檢查。"); Add("Pick it to check.", "揀咗就會檢查。");
            Add("A process on this PC. Simplest.", "喺呢部電腦上運行嘅程序，最簡單。");
            Add("Docker on this computer", "呢部電腦上嘅 Docker"); Add("Docker on this computer.", "呢部電腦上嘅 Docker。");
            Add("A container with its own folder for the world.", "一個容器，世界有自己嘅資料夾。");
            Add("Another computer over SSH", "經 SSH 連去另一部電腦"); Add("Another computer over SSH.", "經 SSH 連去另一部電腦。");
            Add("A process on a machine you can sign in to with an SSH key.", "喺一部你可以用 SSH 金鑰登入嘅機器上運行嘅程序。");
            Add("Docker over SSH.", "經 SSH 用 Docker。"); Add("A container on another machine.", "喺另一部機上面嘅容器。");
            Add("This computer.", "呢部電腦。");
            Add("A process over SSH runs Vanilla. Choose Docker over SSH for other software.", "經 SSH 嘅程序只會運行原版。其他軟件請揀「經 SSH 用 Docker」。");
            Add("Enter the host below; its key is checked first.", "喺下面輸入主機；會先檢查佢嘅金鑰。");
            Add("The host could not be reached, or its key is not approved yet.", "連唔到主機，或者佢嘅金鑰未獲批准。");
            Add("Docker is not available on the host.", "主機上冇 Docker。"); Add("Java is installed on the host.", "主機已安裝 Java。");
            Add("Java is not installed on the host.", "主機未安裝 Java。");
            Add("user@host or user@host:port, signed in with an SSH key", "user@host 或者 user@host:port，用 SSH 金鑰登入");
            Add("Folder on the host", "主機上嘅資料夾"); Add("Optional. Default: monitoring-tools-mc/<id> in the home folder", "可選。預設：主資料夾入面嘅 monitoring-tools-mc/<id>");
            Add("This host's key is already approved.", "呢個主機嘅金鑰已經批准咗。");
            Add("Approve only if these match the host's own fingerprints:", "只有同主機本身嘅指紋相符先好批准：");
            Add("Approve only if these match the host's own fingerprints:\n", "只有同主機本身嘅指紋相符先好批准：\n");
            Add("Host key approved for Minecraft servers.", "已批准主機金鑰，用於 Minecraft 伺服器。"); Add("Check the host", "檢查主機");
            Add("Give the server a name.", "請幫伺服器改個名。"); Add("Memory must be from 512 MB to 65536 MB.", "記憶體必須介乎 512 MB 至 65536 MB。");
            Add("The port must be from 1024 to 65535.", "埠必須介乎 1024 至 65535。"); Add("Maximum players must be from 1 to 1000.", "最多玩家數目必須介乎 1 至 1000。");
            Add("The port and memory are checked on the host when the server starts.", "伺服器啟動時會喺主機上檢查埠同記憶體。");
            Add("Typed numbers with their limits. The port is checked against this computer.", "輸入數字，附有上下限。埠會喺呢部電腦上檢查。");
            Add("Start with a new world, one you already have, or a backup of another server.", "可以由新世界、你已有嘅世界，或者另一個伺服器嘅備份開始。");
            Add("A new world from a seed; a world you have as a folder or ZIP, checked and copied, never moved; or a backup of another server.",
                "用種子建立新世界；用你已有嘅資料夾或者 ZIP 世界，會檢查同複製，唔會搬走；或者用另一個伺服器嘅備份。");
            Add("New world", "新世界"); Add("Use a world I have", "用我已有嘅世界"); Add("From a backup", "由備份"); Add("New world, random seed", "新世界，隨機種子");
            Add("A seed has no spaces.", "種子唔可以有空格。");
            Add("An existing world can be copied to an SSH host after the server is made. Choose a new world here.", "伺服器建立之後可以將現有世界複製去 SSH 主機。呢度請揀新世界。");
            Add("Choose a backup.", "請揀一個備份。"); Add("Choose a world folder or ZIP.", "請揀世界資料夾或者 ZIP。");
            Add("Seed (empty picks one at random)", "種子（留空會隨機揀）"); Add("The generator for the new world. Default: normal.", "新世界嘅生成器。預設：normal。");
            Add("Superflat", "超平坦"); Add("Choose a ZIP…", "揀 ZIP…"); Add("Choose a folder…", "揀資料夾…");
            Add("Choose a world folder (the one with level.dat)", "揀世界資料夾（有 level.dat 嗰個）");
            Add("It has a level.dat, so it is a Minecraft world. It is copied in when the server is made.", "入面有 level.dat，所以係 Minecraft 世界。建立伺服器時會複製入去。");
            Add("No backups of other servers on this computer yet.", "呢部電腦暫時未有其他伺服器嘅備份。");
            Add("Check the answers and accept the EULA. Creating downloads the server and makes its folder.", "檢查你嘅答案並同意 EULA。建立時會下載伺服器同建立佢嘅資料夾。");
            Add("Required: the server will not run without it. Off until you accept.", "必須：冇咗佢伺服器就唔會運行。你同意之前保持關閉。");
            Add("Accept the Minecraft EULA", "同意 Minecraft EULA");
            Add("Your answers are kept as a draft, so you can leave and come back.", "你嘅答案會儲存做草稿，所以可以走開再返嚟。");
            Add("Opens the guided create flow.", "開啟引導式建立流程。"); Add("Choose…", "揀…");
            Add("A Minecraft name: 1 to 16 letters, digits or underscores.", "Minecraft 名稱：1 至 16 個英文字母、數字或者底線。");

            // ---- console and command builder
            Add("Server log", "伺服器紀錄"); Add("Search the log", "搜尋紀錄"); Add("No lines match the filters.", "冇紀錄行符合篩選。");
            Add("Follow", "跟隨"); Add("Follow the newest line", "跟住最新一行"); Add("Stop following the newest line", "停止跟住最新一行");
            Add("Hide chat", "隱藏聊天"); Add("Hide commands", "隱藏指令"); Add("Hide info", "隱藏資訊"); Add("Hide warnings", "隱藏警告");
            Add("Show chat", "顯示聊天"); Add("Show commands", "顯示指令"); Add("Show info", "顯示資訊"); Add("Show warnings", "顯示警告");
            Add("Run a command", "執行指令"); Add("Find a command", "搵指令"); Add("Choose a command above.", "請喺上面揀一個指令。"); Add("Choose a command.", "請揀一個指令。");
            Add("Command so far", "目前嘅指令"); Add("What can come next", "下一步可以接乜嘢"); Add("This command is complete.", "呢個指令已經完整。");
            Add("The exact command line", "確實嘅指令行"); Add("It can run as it is, or take more:", "可以直接執行，或者再加："); Add("Next:", "下一步：");
            Add("Recent:", "最近："); Add("Run", "執行"); Add("Sending…", "傳送緊…"); Add("Sent.", "已傳送。"); Add("Clear", "清除");
            Add("Sample server: nothing was sent.", "示範伺服器：冇傳送任何嘢。"); Add("The server is not running, so nothing was sent.", "伺服器冇運行，所以冇傳送任何嘢。");
            Add("Read this version's command list", "讀取呢個版本嘅指令清單");
            Add("Reading the command list from the server jar…", "由伺服器 jar 讀取緊指令清單…"); Add("Loading the command list…", "載入緊指令清單…");
            Add("This version's command list has not been read yet.", "未讀取呢個版本嘅指令清單。");
            Add("This version's command list has not been read yet. Reading it downloads the official server jar for this version once and asks it for its commands.",
                "未讀取呢個版本嘅指令清單。讀取時會下載一次呢個版本嘅官方伺服器 jar，再問佢攞指令。");
            Add("The jar did not list its commands.", "個 jar 冇列出佢嘅指令。"); Add("A command is one line.", "指令只可以有一行。");
            Add("a whole number", "整數"); Add("a number", "數字"); Add("true or false", "true 或 false"); Add("text to the end of the line", "直到行尾嘅文字");
            Add("a word or a quoted phrase", "一個字或者用引號包住嘅片語"); Add("one word", "一個字"); Add("a player name or a selector such as @a", "玩家名稱或者選擇器，例如 @a");
            Add("a message", "一段訊息"); Add("a time such as 1000, 2d or 300s", "時間，例如 1000、2d 或 300s"); Add("three coordinates such as ~ ~ ~", "三個座標，例如 ~ ~ ~");
            Add("an item id such as minecraft:diamond", "物品 ID，例如 minecraft:diamond"); Add("a value", "一個數值");

            // ---- players
            Add("Which players", "邊啲玩家"); Add("Everyone", "所有人"); Add("Online", "在線"); Add("Operators", "管理員"); Add("Banned", "已封禁");
            Add("Add a player by name", "用名稱加入玩家"); Add("No players match this filter.", "冇玩家符合呢個篩選。");
            Add("No players yet. Add one by name, or they appear once they join.", "暫時未有玩家。可以用名稱加入，或者等佢哋加入之後就會出現。");
            Add("Operator", "管理員"); Add("operator", "管理員"); Add("Whitelist", "白名單"); Add("whitelisted", "已加入白名單"); Add("banned", "已封禁");
            Add("offline", "離線"); Add("Can run every command.", "可以執行所有指令。"); Add("Can join while the whitelist is on.", "白名單開咗都可以加入。");
            Add("Make operator", "設為管理員"); Add("Make operator…", "設為管理員…"); Add("Remove operator", "移除管理員");
            Add("An operator can run every command on this server, including stopping it and changing other players.", "管理員可以喺呢個伺服器執行所有指令，包括停止伺服器同更改其他玩家。");
            Add("They lose access to operator commands straight away.", "佢哋會即刻失去管理員指令嘅權限。");
            Add("They are disconnected and cannot join again until they are pardoned.", "佢哋會被斷線，解封之前唔可以再加入。");
            Add("Ban", "封禁"); Add("Ban…", "封禁…"); Add("Kick", "踢走"); Add("Pardon", "解封");
            Add("Game mode: survival", "遊戲模式：生存"); Add("Game mode: creative", "遊戲模式：創造"); Add("Game mode: adventure", "遊戲模式：冒險"); Add("Game mode: spectator", "遊戲模式：旁觀");
            Add("The server is running, so changes go through its console and it saves them itself. Bans and operator changes ask for the two-key confirmation.",
                "伺服器運行緊，所以更改會經佢嘅主控台傳送，由佢自己儲存。封禁同管理員更改需要雙重確認。");
            Add("The server is stopped, so changes are written to ops.json, whitelist.json and banned-players.json. Bans and operator changes ask for the two-key confirmation.",
                "伺服器已停止，所以更改會寫入 ops.json、whitelist.json 同 banned-players.json。封禁同管理員更改需要雙重確認。");
            Add("This server keeps its player files on the SSH host. Start it to change players through its console.", "呢個伺服器嘅玩家檔案喺 SSH 主機上。請啟動伺服器，經佢嘅主控台更改玩家。");
            Add("A Minecraft name has 1 to 16 letters, digits or underscores.", "Minecraft 名稱有 1 至 16 個英文字母、數字或者底線。");
            Add("A player can only be kicked while the server runs.", "只可以喺伺服器運行時踢走玩家。"); Add("Banned by an operator.", "被管理員封禁。");
            Add("The server is stopped and its files are on the SSH host. Start it to change players, or edit the files on the host.", "伺服器已停止，而佢嘅檔案喺 SSH 主機上。請啟動伺服器先更改玩家，或者喺主機上編輯檔案。");
            Add("Saved to the server's files. It reads them on its next start.", "已儲存到伺服器嘅檔案。佢下次啟動時會讀取。");

            // ---- settings: groups, states and controls
            Add("Setting groups", "設定群組"); Add("Gameplay", "玩法"); Add("Players and access", "玩家同存取權限"); Add("Network", "網絡"); Add("Performance", "效能");
            Add("Game rules", "遊戲規則"); Add("Management", "管理"); Add("Other keys", "其他鍵"); Add("Search settings", "搜尋設定"); Add("Matching settings", "符合嘅設定");
            Add("Applies now", "即時生效"); Add("Needs restart", "需要重新啟動"); Add("New worlds only", "只限新世界"); Add("Not on this version", "呢個版本冇");
            Add("In effect now", "而家生效緊"); Add("Changed", "已更改"); Add("Unsaved changes", "未儲存嘅更改"); Add("Discard", "捨棄");
            Add("Save, apply at next start", "儲存，下次啟動時套用"); Add("Save and restart", "儲存並重新啟動"); Add("(empty)", "（空白）");
            Add("Read the current values", "讀取目前數值"); Add("Reading server.properties…", "讀取緊 server.properties…"); Add("Reading…", "讀取緊…");
            Add("Shown at its usual default; read the current values to be sure.", "顯示嘅係平時嘅預設值；讀取目前數值先確定。");
            Add("Kept exactly as written in server.properties.", "會完全按 server.properties 原文保留。");
            Add("Keys this app does not document for this version. They are shown as they are and saved back unchanged.", "呢個程式冇為呢個版本記錄嘅鍵。會按原樣顯示，儲存時亦唔會改動。");
            Add("The settings could not be read from the host. It may be offline.", "讀唔到主機上嘅設定，主機可能離線。");
            Add("The value the server reported.", "伺服器回報嘅數值。"); Add("Read the current values to see this rule.", "讀取目前數值就會見到呢條規則。");
            Add("Game rules live inside the world. Start the server to change them.", "遊戲規則儲存喺世界入面。啟動伺服器先可以更改。");
            Add("Game rules live inside the world, so they can only be changed while the server runs.", "遊戲規則儲存喺世界入面，所以只可以喺伺服器運行時更改。");
            Add("This version's game rules have not been read yet. They come from the jar's own command list.", "未讀取呢個版本嘅遊戲規則。佢哋來自 jar 本身嘅指令清單。");
            Add("Read the game rules from the jar", "由 jar 讀取遊戲規則");
            Add("Changes are sent with /gamerule when you save, and apply at once.", "儲存時會用 /gamerule 傳送更改，即刻生效。");
            Add("server.properties has no keys beyond the documented ones.", "server.properties 冇已記錄以外嘅鍵。"); Add("No settings match the search.", "冇設定符合搜尋。");
            Add("server.properties could not be read, so nothing was saved.", "讀唔到 server.properties，所以冇儲存任何嘢。");
            Add("Saved. Restarting…", "已儲存。重新啟動緊…"); Add("Saved. The changes apply at the next start.", "已儲存。更改會喺下次啟動時套用。");
            Add("Type a whole number.", "請輸入整數。");
            Add("Off: the server skips it.", "關：伺服器會略過佢。"); Add("On: the server loads it at its next start.", "開：伺服器下次啟動時會載入佢。");
            Add("blocks", "格"); Add("chunks", "區塊"); Add("bytes", "位元組"); Add("min", "分鐘"); Add("per second", "每秒"); Add("players", "個玩家"); Add("none", "冇");

            // ---- settings: labels and help
            Add("Game mode for new players", "新玩家嘅遊戲模式"); Add("The mode a player starts in the first time they join.", "玩家第一次加入時嘅模式。");
            Add("Force the game mode", "強制遊戲模式"); Add("Puts every player back in the default game mode each time they join.", "每次玩家加入時都會將佢改返預設遊戲模式。");
            Add("Players are switched to spectator when they die. Takes effect only for a new world.", "玩家死咗會變成旁觀者。只對新世界生效。");
            Add("Players can hurt each other.", "玩家之間可以互相傷害。"); Add("How dangerous mobs are and how fast hunger drains.", "生物有幾危險，同埋肚餓得幾快。");
            Add("Stops the server kicking players whose game flies, for example with a flight mod.", "防止伺服器踢走遊戲入面飛行嘅玩家，例如用咗飛行模組。");
            Add("Spawn protection", "出生點保護"); Add("Radius around spawn that only operators can build in. 0 turns it off.", "出生點周圍只有管理員可以建築嘅半徑。0 即係關閉。");
            Add("Command blocks", "指令方塊"); Add("Lets command blocks run commands.", "容許指令方塊執行指令。");
            Add("Spawn monsters", "生成怪物"); Add("Hostile mobs appear in the world.", "世界會出現敵對生物。");
            Add("Allow the Nether", "允許地獄"); Add("Players can travel to the Nether through portals.", "玩家可以經傳送門去地獄。");
            Add("Seed", "種子"); Add("The seed for a new world. Empty picks one at random.", "新世界嘅種子。留空會隨機揀一個。");
            Add("World type", "世界類型"); Add("The generator for a new world.", "新世界嘅生成器。");
            Add("World folder", "世界資料夾"); Add("The folder the world is saved in, inside the server folder.", "伺服器資料夾入面儲存世界嘅資料夾。");
            Add("Villages, temples and other structures in new chunks.", "新區塊入面嘅村莊、神殿同其他結構。");
            Add("World size limit", "世界大小上限"); Add("Radius of the world border the server allows.", "伺服器容許嘅世界邊界半徑。");
            Add("How many can be online at once. 1 to 1000. Default: 20.", "最多幾多人可以同時在線。1 至 1000。預設：20。");
            Add("How many people can be online at once.", "最多幾多人可以同時在線。");
            Add("Enforce the whitelist", "強制執行白名單"); Add("Only players on the whitelist can join.", "只有白名單上嘅玩家可以加入。");
            Add("Kicks players who are online but not on the whitelist when it is reloaded.", "重新載入白名單時，踢走在線但唔喺白名單上嘅玩家。");
            Add("Online mode", "正版驗證模式"); Add("Checks every player with Mojang. Turning it off lets anyone use any name.", "向 Mojang 核實每個玩家。關咗佢就任何人都可以用任何名稱。");
            Add("Require signed chat", "要求簽署聊天訊息"); Add("Only players with a Mojang-signed profile key can join.", "只有擁有 Mojang 簽署個人檔案金鑰嘅玩家可以加入。");
            Add("Block proxy connections", "封鎖代理連線"); Add("Refuses players whose connection comes through a proxy or VPN.", "拒絕經代理伺服器或者 VPN 連線嘅玩家。");
            Add("Operator level", "管理員權限等級");
            Add("What operators may do: 1 build at spawn, 2 cheat commands, 3 player commands, 4 every command.", "管理員可以做嘅嘢：1 喺出生點建築，2 作弊指令，3 玩家指令，4 所有指令。");
            Add("Idle kick", "閒置踢出"); Add("Minutes a player can stand still before being kicked. 0 never kicks.", "玩家可以企喺度唔郁幾多分鐘先會被踢走。0 即係永遠唔踢。");
            Add("Hide the player list", "隱藏玩家清單"); Add("Leaves player names out of the server list ping.", "伺服器列表 ping 唔會包括玩家名稱。");
            Add("The port players connect to.", "玩家連線用嘅埠。"); Add("The port players connect to. 1024 to 65535. A free one is suggested.", "玩家連線用嘅埠。1024 至 65535。會建議一個冇人用嘅。");
            Add("Listen address", "監聽位址"); Add("The address to listen on. Empty listens on every address of the computer.", "要監聽嘅位址。留空就監聽呢部電腦所有位址。");
            Add("Also used as the message of the day", "亦用作每日訊息"); Add("The line players see under the server's name in their server list.", "玩家喺伺服器列表入面，伺服器名稱下面見到嗰行字。");
            Add("Show in the server list", "喺伺服器列表顯示"); Add("Answers the server list ping, so the server shows as online.", "回應伺服器列表嘅 ping，令伺服器顯示為在線。");
            Add("Query protocol", "查詢協定"); Add("Answers GameSpy4 query requests, used by some server list websites.", "回應 GameSpy4 查詢請求，部分伺服器列表網站會用到。");
            Add("Remote console", "遠端主控台"); Add("Accepts remote console connections. Keep it off unless you use it, and set a strong password.", "接受遠端主控台連線。除非你會用，否則保持關閉，並設定一個強密碼。");
            Add("Remote console port", "遠端主控台埠"); Add("The port for remote console connections.", "遠端主控台連線用嘅埠。");
            Add("Compression threshold", "壓縮門檻"); Add("Packets larger than this are compressed. -1 turns compression off.", "大過呢個大小嘅封包會被壓縮。-1 會關閉壓縮。");
            Add("View distance", "視距"); Add("How many chunks around each player are sent to their game.", "每個玩家周圍有幾多個區塊會傳送去佢嘅遊戲。");
            Add("Simulation distance", "模擬距離"); Add("How far from players mobs move and crops grow.", "離玩家幾遠範圍內生物會郁同農作物會生長。");
            Add("Entity distance", "實體距離"); Add("How far away entities are sent to players, as a percentage of the normal distance.", "實體喺幾遠嘅距離內會傳送畀玩家，以正常距離嘅百分比計。");
            Add("Max tick time", "最長 tick 時間"); Add("A tick slower than this stops the server as frozen. -1 turns the watchdog off.", "一個 tick 慢過呢個時間，伺服器就當凍結咗而停止。-1 會關閉監察程式。");
            Add("Write chunks at once", "即時寫入區塊"); Add("Saves chunks straight to disk. Safer after a crash, slower on slow disks.", "直接將區塊寫入磁碟。當機之後比較安全，喺慢嘅磁碟會比較慢。");
            Add("Chained block updates", "連鎖方塊更新"); Add("How many block updates in a row are allowed before the rest are skipped.", "連續容許幾多次方塊更新，之後嘅會略過。");
            Add("Packet rate limit", "封包速率上限"); Add("Packets a player may send per second before being kicked. 0 has no limit.", "玩家每秒最多可以傳送幾多個封包，超過就會被踢走。0 即係冇上限。");
            Add("Memory for the Java server. 512 MB to 65536 MB. Default: 2048 MB.", "Java 伺服器嘅記憶體。512 MB 至 65536 MB。預設：2048 MB。");
            Add("Memory given to the Java server. Stored by this app, not in server.properties.", "分配畀 Java 伺服器嘅記憶體。由呢個程式儲存，唔係寫喺 server.properties。");
            Add("Management API", "管理 API"); Add("JSON-RPC management over WebSocket.", "經 WebSocket 嘅 JSON-RPC 管理介面。");
            Add("Settings for the management interface that arrived with 1.21.9.", "1.21.9 開始提供嘅管理介面設定。");

            // ---- worlds
            Add("Worlds", "世界"); Add("Overworld", "主世界"); Add("Nether", "地獄"); Add("The End", "終界"); Add("Download ZIP", "下載 ZIP");
            Add("not made yet; the server makes it on its first start", "未建立；伺服器第一次啟動時會建立");
            Add("Import a world…", "匯入世界…"); Add("Reset the world…", "重設世界…");
            Add("Generate a new world, or pre-generate chunks", "生成新世界，或者預先生成區塊"); Add("Open the generator", "開啟生成器"); Add("Hide the generator", "收埋生成器");
            Add("Builds terrain with the real server jar: a seed, a world type and a square area of chunks generated ahead of time, saved as a folder or a ZIP, or written into this server.",
                "用真正嘅伺服器 jar 建立地形：一個種子、一種世界類型同一個預先生成嘅正方形區塊範圍，可以儲存成資料夾或者 ZIP，或者直接寫入呢個伺服器。");
            Add("Reset and replace stop the server, take a backup first and ask for the two-key confirmation. An imported world is copied and checked for a level.dat, never moved.",
                "重設同取代會先停止伺服器，先備份，再要求雙重確認。匯入嘅世界會被複製並檢查有冇 level.dat，唔會搬走原本嘅。");
            Add("This server keeps its world on the SSH host, so import, download and reset happen there. The world generator below can still build a world on this computer to copy over.",
                "呢個伺服器嘅世界喺 SSH 主機上，所以匯入、下載同重設都喺嗰度進行。下面嘅世界生成器仍然可以喺呢部電腦建立世界，再複製過去。");
            Add("Choose a world ZIP, or cancel to choose a folder", "揀世界 ZIP，或者取消改揀資料夾");
            Add("Imported. The server uses this world on its next start.", "已匯入。伺服器下次啟動時會用呢個世界。");
            Add("The world folders are removed, and the server makes a new world on its next start.", "世界資料夾會被移除，伺服器下次啟動時會建立新世界。");
            Add("The current world is swapped for the one you chose.", "目前嘅世界會換成你揀嗰個。");
            Add("The server is stopped first, and a backup of the current world is taken before anything is removed.", "伺服器會先停止，移除任何嘢之前會先備份目前嘅世界。");
            Add("Stopping and backing up…", "停止緊同備份緊…"); Add("The world was reset.", "世界已重設。"); Add("The world was replaced.", "世界已取代。");
            Add("Save the world as a ZIP", "將世界儲存做 ZIP"); Add("Writing the ZIP…", "寫入緊 ZIP…");
            Add("That folder has no level.dat, so it is not a Minecraft world.", "嗰個資料夾冇 level.dat，所以唔係 Minecraft 世界。");
            Add("That ZIP has no level.dat, so it is not a Minecraft world.", "嗰個 ZIP 冇 level.dat，所以唔係 Minecraft 世界。");
            Add("That file is not a readable ZIP.", "嗰個檔案唔係讀得到嘅 ZIP。"); Add("Choose a world folder or a .zip file.", "請揀世界資料夾或者 .zip 檔案。");
            Add("The ZIP has a path outside its own folder, so it was refused.", "個 ZIP 有路徑喺自己資料夾以外，所以已拒絕。");
            Add("The copied world has no level.dat.", "複製咗嘅世界冇 level.dat。");

            // ---- plugins and mods
            Add("Plugins", "外掛"); Add("Mods", "模組"); Add("Plugins and mods", "外掛同模組"); Add("Find plugins", "搵外掛"); Add("Find mods", "搵模組");
            Add("Install from a file…", "由檔案安裝…"); Add("Install", "安裝"); Add("Search Modrinth", "搜尋 Modrinth"); Add("Search the catalogue", "搜尋目錄"); Add("Searching…", "搜尋緊…");
            Add("Results come from the public Modrinth catalogue, filtered to this version and software. Each download is checked against the catalogue's SHA-512 before it is copied in. Hangar is not searched yet.",
                "結果來自公開嘅 Modrinth 目錄，按呢個版本同軟件篩選。每次下載都會先同目錄嘅 SHA-512 核對，先至複製入去。暫時未搜尋 Hangar。");
            Add("Catalogue installs copy into a folder on this computer; this server's folder is on the SSH host.", "目錄安裝會複製到呢部電腦上嘅資料夾；呢個伺服器嘅資料夾喺 SSH 主機上。");
            Add("The jar is deleted from the folder. A backup of the whole folder is taken first, so it can be put back.", "會由資料夾刪除個 jar。之前會先備份成個資料夾，所以可以還原。");
            Add("Vanilla runs neither plugins nor mods. A Paper or Purpur server takes plugins, and a Fabric, Forge or NeoForge server takes mods; make a new server with one of those to use them.",
                "原版唔可以運行外掛或模組。Paper 或 Purpur 伺服器支援外掛，Fabric、Forge 或 NeoForge 伺服器支援模組；要用嘅話請用其中一種建立新伺服器。");
            Add("Vanilla runs no plugins or mods.", "原版唔會運行外掛或模組。"); Add("Choose a .jar plugin or mod.", "請揀 .jar 外掛或模組。"); Add("That jar is empty.", "嗰個 jar 係空嘅。");
            Add("The catalogue lists no SHA-512 for this file, so it was not installed.", "目錄冇為呢個檔案列出 SHA-512，所以冇安裝。");
            Add("The catalogue file is not a jar.", "目錄檔案唔係 jar。");
            Add("The download did not match the catalogue's SHA-512, so it was not installed.", "下載嘅檔案同目錄嘅 SHA-512 唔吻合，所以冇安裝。");

            // ---- backups
            Add("Backup", "備份"); Add("Backups", "備份"); Add("Backups to keep", "保留備份數量"); Add("Newest backups to keep", "保留最新備份數量");
            Add("Older ones are removed after each new backup. Default: 14.", "每次新備份之後會移除較舊嘅。預設：14。");
            Add("No backups yet.", "暫時未有備份。"); Add("There is no backup of this world yet.", "呢個世界暫時未有備份。"); Add("Restore…", "還原…");
            Add("Note for the next backup (optional)", "下次備份嘅備註（可選）"); Add("Note for this backup", "呢次備份嘅備註");
            Add("Optional, for example: before the nether rebuild", "可選，例如：重建地獄之前");
            Add("Scheduled", "已排程"); Add("By hand", "手動"); Add("Before a restore", "還原之前"); Add("Before a reset", "重設之前"); Add("Before a replace", "取代之前");
            Add("Where should backups go?", "備份要放去邊？"); Add("Stopping and restoring…", "停止緊同還原緊…"); Add("Restored.", "已還原。");
            Add("The server is stopped, the current world is kept as one more backup, and then the backup's folders replace the current ones.", "伺服器會先停止，目前嘅世界會另存多一份備份，然後用備份嘅資料夾取代目前嗰啲。");
            Add("A backup of a running server runs save-off and save-all, copies the world, then save-on, so the copy is consistent while players stay online. Restore stops the server, keeps the current world as one more backup, then swaps.",
                "備份運行緊嘅伺服器時，會先執行 save-off 同 save-all，複製世界，再執行 save-on，咁玩家留喺線上都可以得到一致嘅副本。還原會先停止伺服器，將目前嘅世界另存多一份備份，然後先至對調。");
            Add("This server keeps its world on the SSH host, so this app cannot copy it from here. Back it up on that host. Backups for servers on this computer, including Docker ones, are made here.",
                "呢個伺服器嘅世界喺 SSH 主機上，所以呢個程式唔可以由呢度複製。請喺嗰部主機上備份。呢部電腦上嘅伺服器（包括 Docker 嗰啲）會喺呢度備份。");
            Add("There is no world to back up yet. A world is made the first time the server starts.", "暫時冇世界可以備份。伺服器第一次啟動時會建立世界。");
            Add("That backup is not there any more.", "嗰個備份已經唔喺度。");
            Add("This server keeps its world on the SSH host, so back it up on that host.", "呢個伺服器嘅世界喺 SSH 主機上，所以請喺嗰部主機上備份。");
            Add("Pausing saves…", "暫停緊儲存…"); Add("Copying the world…", "複製緊世界…");

            // ---- schedules
            Add("Schedules", "排程"); Add("New schedule", "新排程"); Add("Add schedule", "加入排程"); Add("Message", "訊息");
            Add("No schedules yet. A daily restart and a backup every few hours suit most servers.", "暫時未有排程。每日重新啟動一次加每幾個鐘備份一次，啱大部分伺服器。");
            Add("Schedules run in the background app, in this computer's time zone. If the computer was asleep or the app was closed at the time, a missed run happens once when it is next running, never several times.",
                "排程喺背景程式運行，用呢部電腦嘅時區。如果嗰陣電腦瞓咗或者程式關咗，錯過嘅排程會喺下次運行時執行一次，唔會執行幾次。");
            Add("Due now: it runs within a minute while this app is running.", "到期：程式運行緊嘅話一分鐘內會執行。"); Add("No next time.", "冇下一次。");
            Add("Time of day", "時間"); Add("24-hour time, for example 04:00", "24 小時制時間，例如 04:00"); Add("What should run?", "要執行乜嘢？");
            Add("Pick the job. Restart and message need the server to be running at the time; they are skipped and recorded when it is not.", "揀工作。重新啟動同訊息需要伺服器喺嗰陣運行緊；如果冇運行，就會略過並記錄低。");
            Add("Job", "工作"); Add("When?", "幾時？"); Add("Every", "每隔"); Add("Daily", "每日"); Add("Weekly", "每週"); Add("Days", "日子");
            Add("A restart warns players first; a backup copies the world; a message is said in chat; an update check only tells you about a newer build.", "重新啟動會先警告玩家；備份會複製世界；訊息會喺聊天度講出嚟；檢查更新只會話你知有冇較新嘅構建。");
            Add("Every day at a time, on chosen days at a time, or every so many minutes.", "每日指定時間、揀好嘅日子指定時間，或者每隔若干分鐘。");
            Add("Minutes between runs", "每次執行相隔分鐘");
            Add("Daily runs every day; weekly runs on the days you pick; every runs on a fixed gap from now.", "「每日」每日執行；「每週」喺你揀嘅日子執行；「每隔」由而家起按固定間隔執行。");
            Add("Type the time as HH:mm, for example 04:00.", "請用 HH:mm 格式輸入時間，例如 04:00。"); Add("Pick at least one day.", "請揀最少一日。");
            Add("A restart can warn players in chat first; a message needs its text.", "重新啟動可以先喺聊天度警告玩家；訊息需要內容。");
            Add("Warn players this many minutes before", "提前幾多分鐘警告玩家"); Add("Warning minutes", "警告分鐘"); Add("0 restarts without a warning.", "0 即係唔警告就重新啟動。");
            Add("What the server says in chat", "伺服器喺聊天度講嘅內容"); Add("Type the message.", "請輸入訊息。"); Add("The schedule starts counting from now.", "排程由而家開始計。");
            Add("Only the Paper, Purpur, Fabric, Forge and NeoForge build lists can be checked. Nothing is installed by itself.", "只可以檢查 Paper、Purpur、Fabric、Forge 同 NeoForge 嘅構建清單。唔會自動安裝任何嘢。");
            Add("no warning", "唔警告"); Add("notify only, never installs by itself", "只會通知，永遠唔會自動安裝");
            Add("Skipped: the host could not be reached.", "已略過：連唔到主機。"); Add("Skipped: the server was not running.", "已略過：伺服器冇運行。");
            Add("Up to date.", "已經係最新。"); Add("Restarted.", "已重新啟動。");
            Add("None since the service started.", "背景服務啟動之後一個都冇。");
            Add("Nothing yet. Locks, accounts and display-name changes will be listed as they happen.", "暫時冇嘢。鎖、帳戶同顯示名稱嘅更改發生時會列出嚟。");

            // ---- server manager messages
            Add("That version is not in the official catalogue.", "嗰個版本唔喺官方目錄入面。");
            Add("The version metadata did not match its catalogue checksum.", "版本中繼資料同目錄嘅校驗碼唔吻合。");
            Add("Accept the Minecraft EULA for this server first.", "請先為呢個伺服器同意 Minecraft EULA。");
            Add("The server jar failed its SHA-1 check.", "伺服器 jar 未能通過 SHA-1 檢查。"); Add("Pulling the server image…", "拉取緊伺服器映像…");
            Add("SSH process servers run Vanilla. Use an SSH Docker target for other software.", "經 SSH 嘅程序伺服器只會運行原版。其他軟件請用 SSH Docker 目標。");
            Add("Copy an existing world to an SSH host from the Worlds section after the server is created.", "伺服器建立之後，請喺「世界」分頁將現有世界複製去 SSH 主機。");
            Add("Preparing the folder on the SSH host…", "喺 SSH 主機上準備緊資料夾…"); Add("Pulling the server image on the SSH host…", "喺 SSH 主機上拉取緊伺服器映像…");
            Add("Vanilla has no build API; new game versions appear in Mojang's catalogue.", "原版冇構建 API；新嘅遊戲版本會喺 Mojang 目錄出現。");
            Add("The installed build was not recorded when this server was created.", "建立呢個伺服器時冇記錄已安裝嘅構建。");
        }

        private static void AddMinecraftPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));
            // The Cantonese of an inner phrase when it has one, otherwise the phrase as it is (a name, a code, a path).
            string Inner(string s) => Language.Yue(s) ?? s;
            // The Cantonese of an inner phrase, or null when it has none, so the whole pattern steps aside.
            string Known(string s) => Language.Yue(s);

            const string DayNames = "Mon|Tue|Wed|Thu|Fri|Sat|Sun";
            string Day(string d) => d.Substring(0, 3).ToLowerInvariant() switch
            {
                "mon" => "星期一", "tue" => "星期二", "wed" => "星期三", "thu" => "星期四", "fri" => "星期五", "sat" => "星期六", _ => "星期日",
            };
            // A span such as "2 h 14 m", "52 m" or "1 d 3 h".
            string Span(string s)
            {
                var r = Regex.Replace(s, @"(\d+) d\b", "$1 日");
                r = Regex.Replace(r, @"(\d+) h\b", "$1 小時");
                r = Regex.Replace(r, @"(\d+) min\b", "$1 分鐘");
                return Regex.Replace(r, @"(\d+) m\b", "$1 分");
            }
            // An age such as "just now", "5 min ago", "3 h ago" or "2 d ago", or null when it is not one.
            string Age(string s)
            {
                if (string.Equals(s, "just now", StringComparison.OrdinalIgnoreCase)) return "啱啱";
                var a = Regex.Match(s, @"^(\d+) (min|h|d) ago$");
                return a.Success ? Span(a.Groups[1].Value + " " + a.Groups[2].Value) + "前" : null;
            }
            // The "when" part of a schedule, such as "Every day at 04:00", "Every 30 min" or "Mon, Thu at 09:00", or null.
            string When(string s)
            {
                Match w;
                if ((w = Regex.Match(s, @"^Every day at (\d\d:\d\d)$")).Success) return "每日 " + w.Groups[1].Value;
                if ((w = Regex.Match(s, @"^Every (\d+) min$")).Success) return "每 " + w.Groups[1].Value + " 分鐘";
                if ((w = Regex.Match(s, @"^Every (\d+) h$")).Success) return "每 " + w.Groups[1].Value + " 小時";
                if ((w = Regex.Match(s, @"^((?:" + DayNames + @")(?:, (?:" + DayNames + @"))*) at (\d\d:\d\d)$")).Success)
                    return string.Join("、", w.Groups[1].Value.Split(", ").Select(Day)) + " " + w.Groups[2].Value;
                return null;
            }
            string Kind(string k) => k.ToLowerInvariant() switch
            {
                "restart" => "重新啟動", "backup" => "備份", "message" => "訊息", "check for updates" => "檢查更新", _ => k,
            };
            string Noun(string n) => n.ToLowerInvariant() == "mods" ? "模組" : "外掛";
            string Unit(string u) => u.ToLowerInvariant() switch
            {
                "min" => "分鐘", "bytes" => "位元組", "blocks" => "格", "chunks" => "區塊", "players" => "個玩家", _ => u,
            };

            // ---- counts, ranges and dates
            Add(@"^(\d+) servers?$", "$1 個伺服器");
            Add(@"^(\d+) settings?$", "$1 個設定");
            AddFn(@"^(.+), (\d+) settings?$", m => Inner(m.Groups[1].Value) + "，" + m.Groups[2].Value + " 個設定");
            Add(@"^(\d+) of (\d+) players$", "$1／$2 個玩家");
            Add(@"^(\d+) of (\d+) lines$", "$2 行入面嘅 $1 行");
            Add(@"^Showing the last (\d+) of (\d+) lines\.$", "顯示 $2 行入面最後 $1 行。");
            Add(@"^(\d+) more; narrow the search\.$", "仲有 $1 個；請收窄搜尋範圍。");
            Add(@"^(-\d+) to (-?\d+)$", "$1 至 $2");
            AddFn(@"^(-?\d+) to (-?\d+) (per second)$", m => "每秒 " + m.Groups[1].Value + " 至 " + m.Groups[2].Value);
            AddFn(@"^(-?\d+) to (-?\d+) (MB|GB|ms|min|bytes|blocks|chunks|players|%)$", m => m.Groups[1].Value + " 至 " + m.Groups[2].Value + " " + Unit(m.Groups[3].Value));
            AddFn(@"^(\d{1,2}) (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) (\d{4})$", m =>
                m.Groups[3].Value + "年" + (Array.IndexOf(new[] { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" }, m.Groups[2].Value.ToLowerInvariant()) + 1) + "月" + m.Groups[1].Value + "日");
            Add(@"^([\d,]+) downloads$", "$1 次下載");
            Add(@"^(?-i:seed) (\S+)$", "種子 $1"); Add(@"^(?-i:port) (\d+)$", "埠 $1"); Add(@"^(?-i:build) (\S+)$", "構建 $1"); Add(@"^(?-i:loader) (\S+)$", "載入器 $1");
            Add(@"^(Vanilla|Paper|Purpur|Fabric|Forge|NeoForge) (\d[\w.\-]*) build (\S+)$", "$1 $2 構建 $3");
            Add(@"^(?-i:server) ([\d.]+) MB$", "伺服器 $1 MB"); Add(@"^needs Java (\d+)$", "需要 Java $1");
            Add(@"^Version (\d[\w.\-]*)$", "版本 $1");
            Add(@"^For example (\S+)$", "例如 $1");

            // ---- filter chips and labelled values
            AddFn(@"^(All|Running|Stopped|Offline host|Everyone|Online|Operators|Banned) (\d+)$", m => Inner(m.Groups[1].Value) + " " + m.Groups[2].Value);
            AddFn(@"^(All|Running|Stopped|Offline host): (\d+) servers?$", m => Inner(m.Groups[1].Value) + "：" + m.Groups[2].Value + " 個伺服器");
            AddFn(@"^(Everyone|Online|Operators|Banned): (\d+)$", m => Inner(m.Groups[1].Value) + "：" + m.Groups[2].Value);
            AddFn(@"^from the list command, (.+)$", m => "來自 list 指令，" + (Age(m.Groups[1].Value) ?? m.Groups[1].Value));
            AddFn(@"^(Players|Memory|Uptime|Ticks per second|Port): (.+?), (.+)$", m =>
                Inner(m.Groups[1].Value) + "：" + (Known(m.Groups[2].Value) ?? Span(m.Groups[2].Value)) + "，" + Inner(m.Groups[3].Value));
            AddFn(@"^State: (.+)$", m => "狀態：" + Inner(m.Groups[1].Value));
            Add(@"^Operator: (.+)$", "管理員：$1"); Add(@"^Whitelist: (.+)$", "白名單：$1");
            Add(@"^More actions for (.+)$", "$1 嘅更多操作");
            AddFn(@"^(Vanilla|Paper|Purpur|Fabric|Forge|NeoForge): (.+)$", m => m.Groups[1].Value + "：" + Inner(m.Groups[2].Value));
            Add(@"^banned: (.+)$", "已封禁：$1");

            // ---- header, server list and create flow
            AddFn(@"^started (.+) ago$", m => Span(m.Groups[1].Value) + "前啟動");
            Add(@"^on (.+) over SSH$", "經 SSH 喺 $1 上");
            Add(@"^in Docker on (.+)$", "喺 $1 嘅 Docker");
            Add(@"^Copy join address (\S+)$", "複製加入位址 $1");
            AddFn(@"^(.+), current step$", m => Inner(m.Groups[1].Value) + "，目前步驟");
            AddFn(@"^(.+), done: (.+)$", m => Inner(m.Groups[1].Value) + "，已完成：" + Inner(m.Groups[2].Value));
            AddFn(@"^(This computer|Docker on this computer|Another computer over SSH|Docker over SSH)\. (.+)$", m => Inner(m.Groups[1].Value) + "。" + Inner(m.Groups[2].Value));
            Add(@"^Docker (\S+) is running\.$", "Docker $1 運行緊。");
            Add(@"^Docker (\S+) is running on the host\.$", "主機上嘅 Docker $1 運行緊。");
            Add(@"^(\S+) needs Docker\.$", "$1 需要 Docker。");
            AddFn(@"^(\S+) does not report it$", m => Inner(m.Groups[1].Value) + " 唔會回報呢項");
            Add(@"^Java (\d+) is ready on this computer\.$", "呢部電腦已經有 Java $1。");
            Add(@"^Java (\d+) will be downloaded for it \(Eclipse Temurin\)\.$", "會為佢下載 Java $1（Eclipse Temurin）。");
            Add(@"^Wait for the (\S+) build list, or choose Vanilla\.$", "請等 $1 嘅構建清單，或者揀原版。");
            Add(@"^Server software for (\S+)$", "$1 嘅伺服器軟件");
            Add(@"^The newest (\S+) builds for (\S+), newest first\. The newest is the default\.$", "$2 嘅最新 $1 構建，由新到舊排列。預設係最新嗰個。");
            Add(@"^(\S+) runs in Docker, on this computer or over SSH, because it needs its own installer\.$", "$1 需要自己嘅安裝程式，所以要喺呢部電腦或者經 SSH 嘅 Docker 運行。");
            Add(@"^(\S+) runs in Docker\. Choose a Docker target, or go back and choose Vanilla, Paper or Purpur\.$", "$1 要喺 Docker 運行。請揀 Docker 目標，或者返去揀原版、Paper 或 Purpur。");
            Add(@"^Another server here already uses port (\d+)\. Pick a different one\.$", "呢度已經有另一個伺服器用緊埠 $1，請揀另一個。");
            Add(@"^Port (\d+) is in use by another program on this computer\.$", "埠 $1 喺呢部電腦被另一個程式用緊。");
            Add(@"^Port (\d+) is used by another server here\.$", "埠 $1 被呢度另一個伺服器用緊。");
            Add(@"^Port (\d+) is in use by another program\.$", "埠 $1 被另一個程式用緊。");
            Add(@"^Port (\d+) is already in use on this computer\.$", "埠 $1 喺呢部電腦已經有人用緊。");
            Add(@"^Port (\d+) is in use by another program, so the server cannot start until it is free or the port changes\.$", "埠 $1 被另一個程式用緊，要等佢釋放或者改埠，伺服器先可以啟動。");
            Add(@"^(\d+) MB is more than three quarters of the (\d+) MB this computer has\.$", "$1 MB 超過咗呢部電腦 $2 MB 記憶體嘅四分之三。");
            Add(@"^Port (\d+) is free on this computer\. (\d+) GB of memory is installed, so (\d+) MB is comfortable\.$", "埠 $1 喺呢部電腦冇人用。已安裝 $2 GB 記憶體，所以 $3 MB 綽綽有餘。");
            Add(@"^New world, seed (\S+)$", "新世界，種子 $1");
            Add(@"^Backup of (.+) from (.+)$", "$1 喺 $2 嘅備份");
            Add(@"^Could not read the version metadata: (.+)$", "讀唔到版本中繼資料：$1");
            Add(@"^Could not load the official catalogue: (.+)$", "載入唔到官方目錄：$1");
            Add(@"^Approve only if these match the host's own fingerprints:\n(.*)$", "只有同主機本身嘅指紋相符先好批准：\n$1");

            // ---- schedules
            AddFn(@"^Delete the (restart|backup|message|check for updates) schedule$", m => "刪除「" + Kind(m.Groups[1].Value) + "」排程");
            AddFn(@"^(Next|First run): (" + DayNames + @") (\d\d:\d\d)$", m => (m.Groups[1].Value.Length == 4 ? "下次：" : "第一次執行：") + Day(m.Groups[2].Value) + " " + m.Groups[3].Value);
            // A labelled value whose value has several " · " parts; the label is translated too.
            AddFn(@"^(Next|Chosen): (.+ · .+)$", m => (m.Groups[1].Value.Length == 4 ? "下次：" : "已揀：") + Inner(m.Groups[2].Value));
            AddFn(@"^(.+? at \d\d:\d\d|Every \d+ (?:min|h)): (.*)$", m => { var w = When(m.Groups[1].Value); return w == null ? null : w + "：" + m.Groups[2].Value; });
            AddFn(@"^(Every .+|.+ at \d\d:\d\d)$", m => When(m.Groups[1].Value));
            Add(@"^players get a (\d+)-minute warning in chat$", "玩家會喺聊天度收到 $1 分鐘警告");
            AddFn(@"^Last run (.+?): (.*)$", m => "上次執行（" + (Age(m.Groups[1].Value) ?? m.Groups[1].Value) + "）：" + Inner(m.Groups[2].Value));
            Add(@"^Backups go to the server's backup folder and the newest (\d+) are kept\.$", "備份會放喺伺服器嘅備份資料夾，保留最新 $1 個。");
            AddFn(@"^Minecraft schedule: (.+) on (.+)$", m => "Minecraft 排程：" + m.Groups[2].Value + " 嘅「" + Kind(m.Groups[1].Value) + "」");
            AddFn(@"^Failed: (.+)$", m => "失敗：" + Inner(m.Groups[1].Value));
            Add(@"^(\S+) build (\S+) is available\.$", "有 $1 構建 $2 可用。");
            Add(@"^(\S+) build (\S+) is out for (\S+)\. This server runs build (\S+)\. Take a backup first; the old build stays available to roll back to\.$",
                "$3 有新嘅 $1 構建 $2。呢個伺服器用緊構建 $4。請先備份；舊構建會保留，可以退返去用。");

            // ---- overview and backups
            AddFn(@"^Last backup (.+?)\.( The next scheduled backup runs at (\d\d:\d\d)\.| No backup is scheduled\.)?$", m =>
                "上次備份：" + (Age(m.Groups[1].Value) ?? m.Groups[1].Value) + "。" +
                (!m.Groups[2].Success ? "" : m.Groups[3].Success ? "下次排程備份會喺 " + m.Groups[3].Value + " 執行。" : "冇排程備份。"));
            AddFn(@"^The last start failed: (.+)$", m => "上次啟動失敗：" + Inner(m.Groups[1].Value));
            Add(@"^The server stopped on its own: (.+)$", "伺服器自己停咗：$1");
            AddFn(@"^Restore the backup from (.+?)(\?)?$", m => "還原 " + m.Groups[1].Value + " 嘅備份" + (m.Groups[2].Success ? "？" : ""));
            Add(@"^Backed up to (.+) \((.+)\)\.$", "已備份到 $1（$2）。");
            Add(@"^Backed up to (.+)$", "已備份到 $1");
            AddFn(@"^Restored\.( The world as it was is in (.+?)\.)? Start the server to play it\.$", m => "已還原。" + (m.Groups[1].Success ? "原本嘅世界喺 " + m.Groups[2].Value + "。" : "") + "啟動伺服器就可以玩。");
            Add(@"^Taken automatically before restoring (.+)$", "還原 $1 之前自動建立");
            AddFn(@"^(\d+ (?:min|h|d) ago)$", m => Age(m.Groups[1].Value));

            // ---- worlds
            Add(@"^Download (.+) as a ZIP$", "將 $1 下載做 ZIP");
            Add(@"^Reset the world of (.+)\?$", "重設 $1 嘅世界？");
            Add(@"^Replace the world of (.+)\?$", "取代 $1 嘅世界？");
            AddFn(@"^(The world folders are removed, and the server makes a new world on its next start\.|The current world is swapped for the one you chose\.) (The server is stopped first, and a backup of the current world is taken before anything is removed\.)$",
                m => Inner(m.Groups[1].Value) + Inner(m.Groups[2].Value));
            AddFn(@"^The world was (reset|replaced)\.( The old world is in the backup (.+?)\.)?$", m =>
                (m.Groups[1].Value.ToLowerInvariant() == "reset" ? "世界已重設。" : "世界已取代。") + (m.Groups[2].Success ? "舊世界喺備份 " + m.Groups[3].Value + " 入面。" : ""));
            AddFn(@"^The world was (reset|replaced)\. The old world is kept on the host in (.+?)\.$", m =>
                (m.Groups[1].Value.ToLowerInvariant() == "reset" ? "世界已重設。" : "世界已取代。") + "舊世界留喺主機嘅 " + m.Groups[2].Value + "。");
            Add(@"^Saved (.+\.zip)\.$", "已儲存 $1。");
            Add(@"^The server already has a world folder ""(.+)""\. Use Replace to swap it, which backs it up first\.$", "伺服器已經有世界資料夾「$1」。用「取代」去對調，會先備份。");

            // ---- players
            Add(@"^Make (.+) an operator\?$", "將 $1 設為管理員？");
            Add(@"^Remove (.+) as an operator\?$", "取消 $1 嘅管理員身分？");
            Add(@"^Ban (.+)\?$", "封禁 $1？");
            Add(@"^Sample server: the command would be (/.+)$", "示範伺服器：指令會係 $1");
            Add(@"^Use ([A-Za-z0-9_]{1,16})$", "用 $1");

            // ---- plugins and mods
            Add(@"^Remove (.+) from the list\?$", "由清單移除 $1？");
            Add(@"^Remove (\S+) and what follows$", "移除 $1 同之後嘅部分");
            AddFn(@"^Remove ([^?]+)$", m => m.Groups[1].Value.Contains(" from ") || m.Groups[1].Value.Contains(" and ") ? null : "移除 " + m.Groups[1].Value);
            Add(@"^Installed on (\S+) (\S+)$", "已安裝喺 $1 $2");
            Add(@"^Works with (.+) only$", "只適用於 $1");
            AddFn(@"^No (plugins|mods) yet\. Install one from a file or find one in the catalogue\.$", m => "暫時未有" + Noun(m.Groups[1].Value) + "。可以由檔案安裝，或者喺目錄搵。");
            AddFn(@"^This server's (plugins|mods) folder is on the SSH host\. Install from a file copies a jar there over SSH; the list itself is managed on the host\.$",
                m => "呢個伺服器嘅" + Noun(m.Groups[1].Value) + "資料夾喺 SSH 主機上。「由檔案安裝」會經 SSH 將 jar 複製過去；清單本身喺主機上管理。");
            AddFn(@"^Turning one off renames its jar to \.jar\.disabled, which the server skips\. Before any install, removal or switch, the whole (plugins|mods) folder is copied to a dated backup beside it, so the change can be rolled back\. Changes take effect at the next start\.$",
                m => "關閉其中一個會將佢嘅 jar 改名做 .jar.disabled，伺服器會略過佢。每次安裝、移除或者切換之前，成個" + Noun(m.Groups[1].Value) + "資料夾都會複製到旁邊一個有日期嘅備份，所以可以還原更改。更改會喺下次啟動時生效。");
            Add(@"^Nothing in the catalogue matches for (\S+) (\S+)\.$", "目錄入面冇嘢符合 $1 $2。");
            Add(@"^The catalogue could not be reached: (.+)$", "連唔到目錄：$1");
            Add(@"^Installed (.+?)\. The folder was backed up first\. Restart the server to load it\.$", "已安裝 $1。已先備份資料夾。請重新啟動伺服器去載入。");
            AddFn(@"^Turned (on|off) (.+?)\. The folder was backed up first; restart the server to apply it\.$", m =>
                (m.Groups[1].Value.ToLowerInvariant() == "on" ? "已開啟 " : "已關閉 ") + m.Groups[2].Value + "。已先備份資料夾；請重新啟動伺服器去套用。");
            Add(@"^Removed (.+?)\. The folder was backed up first\.$", "已移除 $1。已先備份資料夾。");
            Add(@"^Installed (.+) \((.+)\)\. The SHA-512 matched the catalogue\. Restart the server to load it\.$", "已安裝 $1（$2）。SHA-512 同目錄吻合。請重新啟動伺服器去載入。");
            Add(@"^(.+) has no file for (\S+) (\S+)\.$", "$1 冇 $2 $3 用嘅檔案。");
            Add(@"^(.+) lists no file to download\.$", "$1 冇列出可以下載嘅檔案。");
            Add(@"^(.+) already exists in the folder\.$", "$1 已經喺資料夾入面。");
            AddFn(@"^Install ([^.?]+)$", m => m.Groups[1].Value.Contains(" with ") || m.Groups[1].Value.StartsWith("it ", StringComparison.OrdinalIgnoreCase) ? null : "安裝 " + m.Groups[1].Value);

            // ---- console and command builder
            AddFn(@"^Commands and their arguments come from the (.+) game's own command list\. Commands added by plugins or mods are not in it\.$", m => "指令同參數來自 " + m.Groups[1].Value + " 遊戲本身嘅指令清單，外掛或模組加嘅指令唔喺入面。");
            Add(@"^Commands and their arguments come from this server's own command list \((.+)\)\.$", "指令同參數來自呢個伺服器本身嘅指令清單（$1）。");
            AddFn(@"^(.+) Reading it downloads the official server jar for this version once and asks it for its commands\.$", m => Inner(m.Groups[1].Value) + "讀取時會下載一次呢個版本嘅官方伺服器 jar，再問佢攞指令。");
            Add(@"^Command (?-i:([a-z][\w:\-]*))$", "指令 $1");
            AddFn(@"^Add (\S+), (.+)$", m => { var k = Known(m.Groups[2].Value); return k == null ? null : "加入 " + m.Groups[1].Value + "，" + k; });
            Add(@"^Run again: (.+)$", "再執行：$1");
            AddFn(@"^a whole number(?: from (-?[\d.]+))?(?: to (-?[\d.]+))?$", m =>
                m.Groups[1].Success && m.Groups[2].Success ? m.Groups[1].Value + " 至 " + m.Groups[2].Value + " 嘅整數"
                : m.Groups[1].Success ? "最少 " + m.Groups[1].Value + " 嘅整數" : m.Groups[2].Success ? "最多 " + m.Groups[2].Value + " 嘅整數" : "整數");
            Add(@"^Fill in (\S+)\.$", "請填 $1。");
            Add(@"^(\S+) must be a whole number\.$", "$1 必須係整數。");
            Add(@"^(\S+) must be at least (.+)\.$", "$1 最少要係 $2。");
            Add(@"^(\S+) must be at most (.+)\.$", "$1 最多只可以係 $2。");
            Add(@"^(\S+) must be a number\.$", "$1 必須係數字。");
            Add(@"^(\S+) must be true or false\.$", "$1 必須係 true 或 false。");
            Add(@"^(\S+) is one word\.$", "$1 只可以係一個字。");
            AddFn(@"^This command needs more: (.+)\.$", m => "呢個指令仲需要：" + m.Groups[1].Value.Replace(" or ", " 或 ") + "。");
            Add(@"^The command list had an unexpected shape: (.+)$", "指令清單嘅格式出乎意料：$1");

            // ---- settings
            AddFn(@"^(\d+) unsaved changes?\.( It needs a restart to take effect\.| Some need a restart to take effect\.)?$", m =>
                m.Groups[1].Value + " 項未儲存嘅更改。" + (!m.Groups[2].Success ? "" : m.Groups[2].Value.Contains("Some") ? "部分要重新啟動先生效。" : "要重新啟動先生效。"));
            Add(@"^Values and limits are for (\S+) (\S+)\. Keys in server\.properties that are not listed here are kept exactly as written\.$", "數值同上下限適用於 $1 $2。server.properties 入面冇喺度列出嘅鍵會按原文保留。");
            AddFn(@"^(.+\.) Not available on ([\w.\-]+)\.$", m => { var k = Known(m.Groups[1].Value); return k == null ? null : k + m.Groups[2].Value + " 冇呢項。"; });
            AddFn(@"^(.+\.) A Docker server's memory is fixed when its container is made, so a change here applies only to new containers\.$", m =>
            { var k = Known(m.Groups[1].Value); return k == null ? null : k + "Docker 伺服器嘅記憶體喺建立容器時已經固定，所以呢度嘅更改只會套用到新容器。"; });
            AddFn(@"^(?-i:default) (\S+|\(empty\))$", m => "預設 " + Inner(m.Groups[1].Value));
            AddFn(@"^Saved\. Applied now: (.+)$", m => "已儲存。即時套用：" + m.Groups[1].Value);
            Add(@"^The lowest is (-?\d+)\.$", "最低係 $1。"); Add(@"^The highest is (-?\d+)\.$", "最高係 $1。");
            AddFn(@"^Decrease (.+)$", m => Known(m.Groups[1].Value) is string k ? "減少" + k : "減少 " + m.Groups[1].Value);
            AddFn(@"^Increase (.+)$", m => Known(m.Groups[1].Value) is string k ? "增加" + k : "增加 " + m.Groups[1].Value);

            // ---- server actions and server manager messages
            AddFn(@"^(Start|Stop|Restart|Reconnect)…$", m => Inner(m.Groups[1].Value) + "緊…");
            AddFn(@"^(Start|Stop|Restart|Reconnect): done\.$", m => Inner(m.Groups[1].Value) + "：完成。");
            AddFn(@"^Could not (start|stop|restart) the server: (.+)$", m => Inner(m.Groups[1].Value) + "唔到伺服器：" + m.Groups[2].Value);
            Add(@"^The SSH host could not be reached: (.+)$", "連唔到 SSH 主機：$1");
            Add(@"^a process on (.+)$", "$1 上嘅程序");
            Add(@"^(?-i:container ([a-z]+))$", "容器：$1");
            Add(@"^(\S+) lists no build for (.+)\.$", "$1 冇列出 $2 嘅構建。");
            Add(@"^(\S+) lists no builds for (.+)\.$", "$1 冇列出 $2 嘅構建。");
            Add(@"^(\S+) has no builds for (.+)\.$", "$1 冇 $2 嘅構建。");
            Add(@"^Fabric lists no loader for (.+)\.$", "Fabric 冇列出 $1 嘅載入器。");
            Add(@"^The (\S+) build list could not be read: (.+)$", "讀唔到 $1 嘅構建清單：$2");
            Add(@"^Mojang publishes no dedicated server for (.+)\.$", "Mojang 冇為 $1 發佈專用伺服器。");
            Add(@"^Getting Java (\d+) \(Eclipse Temurin\)…$", "攞緊 Java $1（Eclipse Temurin）…");
            Add(@"^Adoptium has no Windows Java (\d+) build\.$", "Adoptium 冇 Windows 版 Java $1。");
            Add(@"^Java (\d+) download failed its SHA-256 check\.$", "Java $1 下載未能通過 SHA-256 檢查。");
            Add(@"^Downloading (\d+)% of (\d+) MB$", "下載緊 $2 MB 嘅 $1%");
            Add(@"^Downloading the (\S+) server \(([\d.]+) MB\)…$", "下載緊 $1 伺服器（$2 MB）…");
            Add(@"^Downloading (\S+) (\S+) build (\S+)…$", "下載緊 $1 $2 構建 $3…");
            Add(@"^Downloading (.+)…$", "下載緊 $1…");
            Add(@"^(\S+) needs its own installer, so it runs on the Docker targets\. On this computer as a process, choose Vanilla, Paper or Purpur\.$", "$1 需要自己嘅安裝程式，所以要喺 Docker 目標運行。喺呢部電腦以程序運行嘅話，請揀原版、Paper 或 Purpur。");
            Add(@"^Docker is not running on this computer: (.+)$", "呢部電腦嘅 Docker 冇運行：$1");
            Add(@"^Pull failed: (.+)$", "拉取失敗：$1");
            Add(@"^Container create failed: (.+)$", "建立容器失敗：$1");
            Add(@"^SSH setup failed: (.+)$", "SSH 設定失敗：$1");
            Add(@"^Docker is not available on the SSH host: (.+)$", "SSH 主機上冇 Docker：$1");
            Add(@"^(\S+) build (\S+) is not listed for (\S+)\.$", "$3 冇列出 $1 構建 $2。");
            Add(@"^(\S+) publishes no checksum for this build, so it was not installed\.$", "$1 冇為呢個構建發佈校驗碼，所以冇安裝。");
            Add(@"^The (\S+) jar failed its (\S+) check\.$", "$1 jar 未能通過 $2 檢查。");
            Add(@"^Could not write server\.properties on the host: (.+)$", "寫唔到主機上嘅 server.properties：$1");
            Add(@"^Installed into (\S+)\. Restart the server to load it\.$", "已安裝到 $1。請重新啟動伺服器去載入。");
            Add(@"^Copied into (\S+) on the host\. Restart the server to load it\.$", "已複製到主機上嘅 $1。請重新啟動伺服器去載入。");
            Add(@"^Could not reach the host to read its key: (.+)$", "連唔到主機去讀取佢嘅金鑰：$1");
            Add(@"^Could not reach the host: (.+)$", "連唔到主機：$1");
            Add(@"^Copy failed: (.+)$", "複製失敗：$1");
        }
    }
}
