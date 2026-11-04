using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the server file store work: SSH players, plugins, backups and worlds; Bedrock players, add-ons,
    /// worlds and targets; and the multi-select player list with its bulk actions. Names, ids, paths and commands stay as they are.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddMcFiles(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- players
            Add("The server is stopped, so changes are written to ops.json, whitelist.json and banned-players.json on the SSH host, over SSH. Bans and operator changes ask for the two-key confirmation.", "伺服器已停止，所以改動會經 SSH 寫入 SSH 主機上嘅 ops.json、whitelist.json 同 banned-players.json。封鎖同操作員改動要雙重確認。");
            Add("Reading the player files on the SSH host…", "讀緊 SSH 主機上嘅玩家檔案…");
            Add("Saved to the server's files on the SSH host. It reads them on its next start.", "已儲存到 SSH 主機上伺服器嘅檔案。佢下次啟動會讀取。");
            Add("A Bedrock name is 1 to 32 letters, digits, spaces and . _ # -.", "Bedrock 名稱係 1 至 32 個字母、數字、空格同 . _ # -。");
            Add("A Bedrock name: 1 to 32 letters, digits, spaces and . _ # -", "Bedrock 名稱：1 至 32 個字母、數字、空格同 . _ # -");
            Add("Xbox user id (optional)", "Xbox 用戶編號（可選）");
            Add("Needed to make a player an operator while the server is stopped, until the server has learned it. 6 to 20 digits.", "伺服器停咗嘅時候要將玩家設為操作員就需要，直到伺服器知道佢嘅編號為止。6 至 20 位數字。");
            Add("Allow list", "允許名單"); Add("Allow list: ", "允許名單："); Add("Can join while the allow list is on.", "允許名單開咗嗰陣可以加入。");
            Add("Can run every command.", "可以執行所有指令。");
            Add("Make visitor", "設為訪客"); Add("Back to the default permission", "還原預設權限"); Add("Remove permission entry", "移除權限記錄");
            Add("The server is running, so allow list and operator changes go through its console (allowlist add, allowlist remove, op, deop) and it saves them itself. Operator changes ask for the two-key confirmation.", "伺服器運行緊，所以允許名單同操作員改動會經佢嘅主控台（allowlist add、allowlist remove、op、deop），佢會自己儲存。操作員改動要雙重確認。");
            Add("The server is stopped, so changes are written to allowlist.json and permissions.json on the SSH host. Operator changes ask for the two-key confirmation.", "伺服器已停止，所以改動會寫入 SSH 主機上嘅 allowlist.json 同 permissions.json。操作員改動要雙重確認。");
            Add("The server is stopped, so changes are written to allowlist.json and permissions.json. Operator changes ask for the two-key confirmation.", "伺服器已停止，所以改動會寫入 allowlist.json 同 permissions.json。操作員改動要雙重確認。");
            Add(" The allow list is switched off in server.properties (allow-list=false), so these names are not enforced until it is turned on in Settings.", " 允許名單喺 server.properties 入面關咗（allow-list=false），所以喺設定開返之前，呢啲名唔會生效。");
            Add("Reading the player files…", "讀緊玩家檔案…");
            Add("A running server takes allow list, operator and member changes from its console. Stop it to make someone a visitor through the files.", "運行緊嘅伺服器要用主控台改允許名單、操作員同成員。想經檔案設訪客就要先停咗佢。");

            // ---- bulk player actions
            Add("Search players", "搜尋玩家"); Add("players", "位玩家");
            Add("Select several players with Ctrl or Shift and a click or the arrow keys, or press Ctrl+A for all shown.", "按住 Ctrl 或 Shift 再點擊或用方向鍵可以揀多位玩家，按 Ctrl+A 全選顯示中嘅玩家。");
            Add("Nothing selected. Select players to act on several at once.", "未揀任何玩家。揀幾位玩家就可以一次過處理。");
            Add("Actions on the selected players", "對所揀玩家嘅操作");
            Add("Reason for kick or ban", "踢走或封鎖嘅原因"); Add("Optional. Sent with the kick or ban to every selected player.", "可選。會連同踢走或封鎖一齊發俾每位所揀玩家。");
            Add("Teleport to", "傳送到"); Add("A player's name, or three coordinates such as 10 64 -20. A ~ means relative to the player.", "玩家名，或者三個座標，例如 10 64 -20。~ 代表相對於玩家。");
            Add("Item to give", "要俾嘅物品"); Add("An item id such as minecraft:diamond.", "物品編號，例如 minecraft:diamond。"); Add("Count", "數量"); Add("1 to 6400.", "1 至 6400。");
            Add("Kick…", "踢走…"); Add("Ban…", "封鎖…"); Add("Pardon", "赦免"); Add("Make operators…", "設為操作員…"); Add("Remove operators", "移除操作員");
            Add("Add to whitelist", "加入白名單"); Add("Remove from whitelist", "移出白名單"); Add("Add to allow list", "加入允許名單"); Add("Remove from allow list", "移出允許名單");
            Add("Game mode", "遊戲模式"); Add("Teleport", "傳送"); Add("Give", "俾物品"); Add("Select all shown", "全選顯示中嘅"); Add("Clear selection", "清除選擇");
            Add("Select players first.", "請先揀玩家。");
            Add("Make operators", "設為操作員");
            Add("Operators can run every command on this server, including stopping it. ", "操作員可以喺呢個伺服器執行所有指令，包括停止佢。");
            Add("They are disconnected and cannot join again until they are pardoned. ", "佢哋會被斷線，直到被赦免先可以再加入。");
            Add("They are disconnected now and can join again. ", "佢哋即時斷線，之後可以再加入。");
            Add("Players", "玩家"); Add("Working…", "處理緊…");
            Add("A Bedrock server has no ban or pardon command.", "Bedrock 伺服器冇封鎖或赦免指令。");
            Add("Choose a game mode.", "請揀遊戲模式。");
            Add("Enter a player to teleport to, or three coordinates such as 10 64 -20 (a ~ means relative).", "請輸入要傳送去嘅玩家，或者三個座標，例如 10 64 -20（~ 代表相對）。");
            Add("A teleport target is a player's name or three coordinates such as 10 64 -20.", "傳送目標係玩家名或者三個座標，例如 10 64 -20。");
            Add("Enter an item id such as minecraft:diamond (lowercase letters, digits and _ . - only).", "請輸入物品編號，例如 minecraft:diamond（只可以用小寫字母、數字同 _ . -）。");
            Add("The count is 1 to 6400.", "數量係 1 至 6400。");

            // ---- plugins on an SSH host
            Add("Names come from the file names, and catalogue checks and updates are for servers on this computer.", "名稱取自檔案名，目錄檢查同更新只適用於呢部電腦上嘅伺服器。");

            // ---- backups on an SSH host
            Add("A backup of a running server runs save-off and save-all, copies the world, then save-on, so the copy is consistent while players stay online. Restore stops the server, keeps the current world as one more backup, then swaps. This server's world is on the SSH host: a backup copies it here with scp, and a restore copies the backup back up, moving the host's current folders aside until every folder has arrived.", "運行緊嘅伺服器備份會先 save-off 同 save-all，再複製世界，最後 save-on，所以玩家留喺線上備份都係一致嘅。還原會停咗伺服器，將而家個世界另存做多一個備份，然後對調。呢個伺服器嘅世界喺 SSH 主機上：備份會用 scp 複製落嚟，還原就將備份複製返上去，主機現有嘅資料夾會先擺開一邊，直到所有資料夾都到齊。");
            Add("Copying the world from the SSH host…", "喺 SSH 主機複製世界緊…");

            // ---- Bedrock add-ons
            Add("Add-ons", "附加包"); Add("Installed add-ons", "已安裝嘅附加包");
            Add("Add-ons are behavior packs and resource packs. A pack lives in the server's behavior_packs or resource_packs folder, and a world uses it only when the world's pack list names it; the switch below edits that list.", "附加包即係行為包同資源包。包放喺伺服器嘅 behavior_packs 或 resource_packs 資料夾，世界要喺佢嘅包清單入面列出先會用到；下面個開關就係改呢份清單。");
            Add("Install an add-on…", "安裝附加包…"); Add("Undo the last change", "還原上一次改動"); Add("Choose an add-on", "揀附加包");
            Add("Switch on for the world after installing", "安裝後喺世界開啟"); Add("Names each new pack in the world's pack list, with the packs it depends on.", "將每個新包同佢依賴嘅包列入世界嘅包清單。");
            Add("Before every install, switch or removal, what it touches is copied to a dated folder under addons-backups, so the last change can be undone. A running server reads its packs at the next start.", "每次安裝、開關或移除之前，受影響嘅嘢會先複製到 addons-backups 底下一個有日期嘅資料夾，所以可以還原上一次改動。運行緊嘅伺服器會喺下次啟動讀取包。");
            Add("Put the add-ons back as they were?", "將附加包還原返原狀？"); Add("Put back", "還原");
            Add("The newest saved copy is put back, and anything that change added is removed. What is there now is saved first, so this can be undone too.", "會還原最新儲存嘅副本，嗰次改動加咗嘅嘢會被移除。而家嘅狀態會先儲起，所以呢個動作都可以還原。");
            Add("Putting the add-ons back…", "還原附加包緊…");
            Add("Reading the pack folders…", "讀緊包資料夾…");
            Add("No add-ons installed. Install a .mcpack or .mcaddon file.", "未安裝附加包。請安裝 .mcpack 或 .mcaddon 檔案。");
            Add("On: the world uses it at the next start.", "開：世界下次啟動會用。"); Add("Off: the world does not use it.", "關：世界唔會用。");
            Add("The pack's folder is deleted and it is taken out of the world's pack list. What it touches is copied to addons-backups first, so it can be put back.", "會刪除包嘅資料夾，並將佢移出世界嘅包清單。受影響嘅嘢會先複製到 addons-backups，所以可以還原。");
            Add("Nothing was installed.", "冇安裝任何嘢。");
            Add("The world does not exist yet. Start the server once so it makes its world, then switch the add-on on.", "世界未存在。請先啟動伺服器一次等佢整出世界，再開啟附加包。");
            Add("This pack ships with the server and is not removed from here.", "呢個包係伺服器自帶嘅，唔可以喺度移除。");
            Add("There is no add-on change to undo yet.", "暫時冇附加包改動可以還原。");
            Add("That file holds no pack: there is no manifest.json in it.", "呢個檔案入面冇包：搵唔到 manifest.json。");
            Add("The archive has a path outside its own folder, so it was refused.", "壓縮檔有路徑指去自己資料夾以外，所以拒絕咗。");
            Add("No manifest.json in this folder.", "呢個資料夾冇 manifest.json。");
            Add("The manifest has no header uuid.", "manifest 冇 header uuid。");

            // ---- Bedrock worlds
            Add("Import a world…", "匯入世界…"); Add("Choose a .mcworld file, or cancel to choose a folder", "揀 .mcworld 檔案，或者取消後揀資料夾");
            Add("Choose a Bedrock world folder (the one with level.dat and db)", "揀 Bedrock 世界資料夾（有 level.dat 同 db 嗰個）");
            Add("Reading the worlds folder…", "讀緊 worlds 資料夾…");
            Add("The Bedrock server makes its world in the worlds folder at its first start. You can also import one now.", "Bedrock 伺服器第一次啟動會喺 worlds 資料夾整出世界。你亦可以而家匯入一個。");
            Add("Backups hold the whole worlds folder. Import copies a world into the folder the server uses (level-name) and checks it first; replacing a world stops the server and takes a backup first. Reset and the world generator are for Java edition.", "備份會包含成個 worlds 資料夾。匯入會先檢查，再將世界複製到伺服器用緊嘅資料夾（level-name）；取代世界會先停伺服器同做備份。重設同世界產生器只適用於 Java 版。");
            Add("Stop the server first: it holds its world open while it runs.", "請先停咗伺服器：佢運行時會打開住個世界。");
            Add("Replace", "取代");
            Add("Imported. The server uses this world on its next start.", "已匯入。伺服器下次啟動會用呢個世界。");
            Add("The world was replaced. The old one is in the newest backup. Start the server to play it.", "世界已取代。舊嗰個喺最新備份入面。啟動伺服器就可以玩。");
            Add("Importing…", "匯入緊…"); Add("Stopping and backing up…", "停止同備份緊…");
            Add("Writing the ZIP…", "寫緊 ZIP…"); Add("Copying the world from the SSH host and writing the ZIP…", "喺 SSH 主機複製世界同寫 ZIP 緊…");
            Add("That folder has no level.dat, so it is not a Minecraft world.", "呢個資料夾冇 level.dat，所以唔係 Minecraft 世界。");
            Add("That is a Java edition world (it has region files and no db folder). A Bedrock server cannot open it.", "呢個係 Java 版世界（有 region 檔案但冇 db 資料夾）。Bedrock 伺服器開唔到。");
            Add("That folder has a level.dat but no db folder, so it is not a Bedrock world.", "呢個資料夾有 level.dat 但冇 db 資料夾，所以唔係 Bedrock 世界。");
            Add("That file has no level.dat, so it is not a Minecraft world.", "呢個檔案冇 level.dat，所以唔係 Minecraft 世界。");
            Add("That file has a level.dat but no db folder, so it is not a Bedrock world.", "呢個檔案有 level.dat 但冇 db 資料夾，所以唔係 Bedrock 世界。");
            Add("Choose a world folder or a .mcworld file.", "請揀世界資料夾或 .mcworld 檔案。");

            // ---- Java worlds on an SSH host
            Add("Reading the world folders on the host…", "讀緊主機上嘅世界資料夾…");
            Add("These folders are on the SSH host and are read over SSH; Download ZIP copies a folder here first. Import, reset and replace for a server on a host are done on the host, and the world generator below can build a world here to copy over. Backups of this server copy its world here too.", "呢啲資料夾喺 SSH 主機上，經 SSH 讀取；下載 ZIP 會先將資料夾複製過嚟。主機上伺服器嘅匯入、重設同取代要喺主機做，下面嘅世界產生器可以喺度整個世界再複製過去。呢個伺服器嘅備份都會將世界複製到呢度。");

            // ---- create flow
            Add("Mojang offers only the newest release of the Bedrock server, so that is the one installed. Players connect over UDP. On this computer it runs as a Windows process; Mojang publishes no checksum for it, so the server program must carry Microsoft's signature before it is installed. Over SSH it is Mojang's Linux build, unpacked on the host (Mojang publishes no checksum or signature for it, so the download is held to HTTPS on minecraft.net and nothing more). In Docker it is the community image itzg/minecraft-bedrock-server, which downloads the server itself.", "Mojang 只提供最新版本嘅 Bedrock 伺服器，所以裝嘅就係呢個。玩家經 UDP 連線。喺呢部電腦佢係 Windows 程序；Mojang 冇公布校驗碼，所以伺服器程式要帶 Microsoft 簽署先會安裝。經 SSH 就係 Mojang 嘅 Linux 版本，喺主機解壓（Mojang 冇公布校驗碼或簽署，所以只確保下載係 minecraft.net 嘅 HTTPS，僅此而已）。Docker 就用社群映像 itzg/minecraft-bedrock-server，由佢自己下載伺服器。");
            Add("A Bedrock server starts with a new world here. Import an existing world from its Worlds section after it is made.", "Bedrock 伺服器喺度會以新世界開始。建立後可以喺佢嘅「世界」部分匯入現有世界。");
        }

        private static void AddMcFilesPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));
            string Inner(string s) => Language.Yue(s) ?? s;

            // bulk player actions
            Add(@"^(\d+) selected: (.+)$", "已揀 $1 位：$2");
            Add(@"^(Kick|Ban|Make operators of) (\d+) players?\?$", "確定要處理 $2 位玩家？");
            Add(@"^These (\d+) players: (.+)\.$", "呢 $1 位玩家：$2。");
            AddFn(@"^(Kick|Ban|Pardon|Make operator|Remove operator|Add to the allow list|Remove from the allow list|Set game mode|Teleport|Give): (\d+) of (\d+) done\.(.*)$", m =>
                Inner(m.Groups[1].Value) + "：" + m.Groups[3].Value + " 位中完成咗 " + m.Groups[2].Value + " 位。" + (m.Groups[4].Value.Trim().StartsWith("Failed:") ? " 失敗：" + m.Groups[4].Value.Trim()[7..] : ""));
            Add(@"^Game mode: (survival|creative|adventure|spectator)$", "遊戲模式：$1");
            Add(@"^(Kick|Ban|Pardon|Make operator|Remove operator|Add to the allow list|Remove from the allow list|Set game mode|Teleport|Give) needs the server to be running; it is stopped\.$", "伺服器已停止，要運行緊先可以做呢個操作。");
            Add(@"^and (\d+) more$", "仲有 $1 位");
            Add(@"^(\d+) of (\d+) players$", "$1 / $2 位玩家");
            Add(@"^(\d+) players$", "$1 位玩家");
            AddFn(@"^(.+), (online|offline)(, banned)?(, operator)?$", m => m.Groups[1].Value + "，" + (m.Groups[2].Value == "online" ? "在線" : "離線") + (m.Groups[3].Success ? "，被封鎖" : "") + (m.Groups[4].Success ? "，操作員" : ""));

            // add-ons and files
            Add(@"^Installed (.+)\.( It is switched on for the world; restart the server to use it\.)?( The world does not exist yet.*)?$", "已安裝 $1。");
            Add(@"^Installing (.+)…$", "安裝緊 $1…");
            Add(@"^(Switched on|Switched off) (.+)\. Restart the server to apply it\.$", "已切換 $2。請重新啟動伺服器套用。");
            Add(@"^(Switching on|Switching off) (.+)…$", "切換緊 $2…");
            Add(@"^Removed (.+)\. The folder was copied to addons-backups first\.$", "已移除 $1。資料夾已先複製到 addons-backups。");
            Add(@"^Removing (.+)…$", "移除緊 $1…");
            Add(@"^Remove (.+)\?$", "移除 $1？");
            Add(@"^Put back the state from before (.+)\. Restart the server to apply it\.$", "已還原到 $1 之前嘅狀態。請重新啟動伺服器套用。");
            Add(@"^(\d+) packs that ship with the server are not listed\.$", "伺服器自帶嘅 $1 個包冇列出。");
            Add(@"^World: (.+)\. Packs are switched on for this world\.$", "世界：$1。包會喺呢個世界開啟。");
            Add(@"^World: (.+) \(not made yet\)\. Start the server once so it makes its world; then packs can be switched on\.$", "世界：$1（未建立）。請先啟動伺服器一次等佢整出世界，之後先可以開啟包。");
            Add(@"^Replace the world of (.+)\?$", "取代 $1 嘅世界？");
            Add(@"^The world ""(.+)"" is swapped for the one you chose\. The server is stopped first, and a backup of the current worlds folder is taken before anything is removed\.$", "世界「$1」會被你揀嘅取代。會先停伺服器，並喺移除任何嘢之前備份而家嘅 worlds 資料夾。");
            Add(@"^The server already has a world folder ""(.+)""\. Replace it to swap it, which backs it up first\.$", "伺服器已經有世界資料夾「$1」。請用取代嚟對調，會先備份。");
            Add(@"^Download (.+) as a ZIP$", "將 $1 下載做 ZIP");
            Add(@"^Copying (.+) to the SSH host…$", "複製 $1 到 SSH 主機緊…");
            Add(@"^Copied (.+) into (plugins|mods) on the host\. The folder was copied to a dated backup beside it first\. Restart the server to load it\.$", "已複製 $1 到主機嘅 $2。資料夾已先複製到旁邊一個有日期嘅備份。請重新啟動伺服器載入。");
            Add(@"^Reading the (plugins|mods) folder on the SSH host…$", "讀緊 SSH 主機上嘅 $1 資料夾…");
            Add(@"^Working on the SSH host…$", "喺 SSH 主機處理緊…");
            AddFn(@"^(Turned on|Turned off) (.+)\. The folder was copied to a dated backup on the host first; restart the server to apply it\.$", m => "已" + (m.Groups[1].Value == "Turned on" ? "開啟" : "關閉") + " " + m.Groups[2].Value + "。資料夾已先喺主機複製到一個有日期嘅備份；請重新啟動伺服器套用。");
            Add(@"^Removed (.+)\. The folder was copied to a dated backup on the host first\.$", "已移除 $1。資料夾已先喺主機複製到一個有日期嘅備份。");
            Add(@"^(.+) · Xbox user id (\d+)$", "$1 · Xbox 用戶編號 $2");
        }
    }
}
