using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The Hong Kong Cantonese table and patterns for the server manager's catalogues (Modrinth and Hangar, badges, Update
    /// all), the Bedrock server, discovery and adoption, and the RCON and SSH messages. Names, keys and versions stay as they are.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddMinecraftCatalogue(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- plugins: catalogues, badges, Update all, undo
            Add("Modrinth", "Modrinth"); Add("Hangar", "Hangar"); Add("Search Hangar", "搜尋 Hangar"); Add("Catalogue", "目錄"); Add("Catalogue results", "目錄結果");
            Add("Installed plugins", "已安裝嘅外掛"); Add("Installed mods", "已安裝嘅模組");
            Add("Check for updates", "檢查更新"); Add("Update all", "全部更新"); Add("Update", "更新"); Add("Undo the last change", "復原上一次改動");
            Add("Compatible", "相容"); Add("May not work", "可能唔用得"); Add("Not in a catalogue", "唔喺目錄入面");
            Add("Check for updates first.", "請先檢查更新。"); Add("Sample server: nothing was downloaded.", "示範伺服器：冇下載任何嘢。");
            Add("There is no backup of the folder yet, so there is nothing to undo.", "資料夾暫時未有備份，所以冇嘢可以復原。");
            Add("Put the folder back as it was?", "將資料夾還原返之前咁？"); Add("Put back", "還原");
            Add("The folder was put back as the backup holds it. Restart the server to apply it.", "資料夾已經還原成備份入面嘅樣。請重新啟動伺服器去生效。");
            Add("Not checked against the catalogues yet.", "暫時未同目錄核對。"); Add("Checking the catalogues…", "核對緊目錄…");
            Add("Checked against Modrinth and Hangar: nothing newer for this version.", "已同 Modrinth 同 Hangar 核對：呢個版本冇更新嘅檔案。");
            Add("1 update for this version.", "呢個版本有 1 個更新。");
            Add("This jar is not in Modrinth, and it was not installed from Hangar by this app.", "呢個 jar 唔喺 Modrinth，亦唔係由呢個程式喺 Hangar 安裝嘅。");
            Add("Every plugin is already on its newest compatible file.", "所有外掛已經係最新嘅相容檔案。");
            Add("Updated 1 plugin. The folder was backed up first. Restart the server to load it.", "已更新 1 個外掛。已先備份資料夾。請重新啟動伺服器去載入。");
            Add("Results come from the public Hangar catalogue, filtered to Paper on this version. Each download is checked against Hangar's SHA-256 before it is copied in; a version Hangar offers only as a link to another site is refused.",
                "結果來自公開嘅 Hangar 目錄，按呢個版本嘅 Paper 篩選。每次下載都會先同 Hangar 嘅 SHA-256 核對先至複製入去；Hangar 只係提供去其他網站連結嘅版本會被拒絕。");
            Add("Results come from the public Modrinth catalogue, filtered to this version and software. Each download is checked against the catalogue's SHA-512 before it is copied in. Hangar is the other catalogue for Paper plugins.",
                "結果來自公開嘅 Modrinth 目錄，按呢個版本同軟件篩選。每次下載都會先同目錄嘅 SHA-512 核對，先至複製入去。Paper 外掛嘅另一個目錄係 Hangar。");
            Add("Results come from the public Modrinth catalogue, filtered to this version and software. Each download is checked against the catalogue's SHA-512 before it is copied in.",
                "結果來自公開嘅 Modrinth 目錄，按呢個版本同軟件篩選。每次下載都會先同目錄嘅 SHA-512 核對，先至複製入去。");
            Add("Turning one off renames its jar to .jar.disabled, which the server skips. Before any install, update, removal or switch, the whole plugins folder is copied to a dated backup beside it, so the change can be rolled back. Changes take effect at the next start.",
                "關閉一個會將佢嘅 jar 改名做 .jar.disabled，伺服器會略過佢。每次安裝、更新、移除或者開關之前，成個 plugins 資料夾都會複製去旁邊一個有日期嘅備份，所以改動可以還原。改動會喺下次啟動生效。");
            Add("Turning one off renames its jar to .jar.disabled, which the server skips. Before any install, update, removal or switch, the whole mods folder is copied to a dated backup beside it, so the change can be rolled back. Changes take effect at the next start.",
                "關閉一個會將佢嘅 jar 改名做 .jar.disabled，伺服器會略過佢。每次安裝、更新、移除或者開關之前，成個 mods 資料夾都會複製去旁邊一個有日期嘅備份，所以改動可以還原。改動會喺下次啟動生效。");
            Add("The Bedrock server takes add-ons (behavior and resource packs) rather than plugins or mods. Put them in its behavior_packs and resource_packs folders; this app does not manage add-ons yet.",
                "Bedrock 伺服器用嘅係附加元件（行為包同資源包），唔係外掛或模組。請放入佢嘅 behavior_packs 同 resource_packs 資料夾；呢個程式暫時未管理附加元件。");
            Add("Hangar lists plugins for Paper and Purpur.", "Hangar 列出嘅係 Paper 同 Purpur 嘅外掛。");
            Add("Hangar lists no SHA-256 for this file, so it was not installed.", "Hangar 冇為呢個檔案列出 SHA-256，所以冇安裝。");
            Add("The Hangar file is not a jar.", "Hangar 嘅檔案唔係 jar。");
            Add("The download did not match Hangar's SHA-256, so it was not installed.", "下載嘅檔案同 Hangar 嘅 SHA-256 唔吻合，所以冇安裝。");
            Add("The catalogue lists no file to download.", "目錄冇列出可以下載嘅檔案。");
            Add("That backup is not there any more.", "嗰個備份已經唔喺度。");

            // ---- the help page cards for the Minecraft articles
            Add("Create, run and look after Java edition servers on this computer, in Docker, or on another machine over SSH, and Bedrock servers on this computer.", "喺呢部電腦、Docker，或者經 SSH 喺另一部機度建立、運行同打理 Java 版伺服器，同埋喺呢部電腦運行 Bedrock 伺服器。");
            Add("Create and run Java edition servers on this computer, in Docker, or on another machine over SSH, and the official Bedrock Dedicated Server on this computer.", "喺呢部電腦、Docker，或者經 SSH 喺另一部機度建立同運行 Java 版伺服器，同埋喺呢部電腦運行官方 Bedrock 專用伺服器。");

            // ---- discovery and adoption
            Add("Servers are listed here when this app created or adopted them. Their files stay in their own folders, and removing one from the list never deletes its world.",
                "由呢個程式建立或者接管嘅伺服器會列喺度。佢哋嘅檔案留喺各自嘅資料夾，由清單移除亦永遠唔會刪除世界。");
            Add("Found on this computer", "喺呢部電腦搵到"); Add("Look for servers", "搵伺服器"); Add("Servers found", "搵到嘅伺服器"); Add("Looking…", "搵緊…"); Add("None found.", "搵唔到。");
            Add("Java and Bedrock servers that run here, and Minecraft containers in Docker, that this app did not make. Nothing is touched until you adopt one.",
                "喺呢度運行、唔係呢個程式建立嘅 Java 同 Bedrock 伺服器，同埋 Docker 入面嘅 Minecraft 容器。你接管之前乜都唔會郁。");
            Add("Adopt", "接管"); Add("Docker container", "Docker 容器"); Add("Bedrock process", "Bedrock 程序"); Add("Java process", "Java 程序");
            Add("Adopted server", "接管咗嘅伺服器"); Add("Servers found on this computer", "喺呢部電腦搵到嘅伺服器");
            Add("This server was started outside the app. Turn on RCON in its settings (enable-rcon and rcon.password) to stop it from here, or stop it where it was started.",
                "呢個伺服器係喺程式以外啟動嘅。請喺佢嘅設定開啟 RCON（enable-rcon 同 rcon.password）先可以喺度停止，或者喺啟動佢嘅地方停止。");
            Add("This server was started outside the app, so commands need RCON. Turn on enable-rcon and set rcon.password in its settings.",
                "呢個伺服器係喺程式以外啟動嘅，所以指令要用 RCON。請喺佢嘅設定開啟 enable-rcon 同設定 rcon.password。");
            Add("The server was asked to stop over RCON but is still running after a minute.", "已經經 RCON 叫伺服器停止，但一分鐘之後仲運行緊。");
            Add("The RCON password in server.properties was refused.", "server.properties 入面嘅 RCON 密碼被拒絕。");
            Add("That container does not carry this app's label for this server, so it was not removed.", "嗰個容器冇呢個程式為呢個伺服器加嘅標籤，所以冇移除。");
            Add("Only a local Docker server has a container to remove.", "只有本機 Docker 伺服器先有容器可以移除。");

            // ---- Bedrock: create flow, console, players, worlds
            Add("Bedrock Dedicated Server for Windows", "Windows 版 Bedrock 專用伺服器"); Add("Bedrock Dedicated Server", "Bedrock 專用伺服器");
            Add("Reading Mojang's download link…", "讀取緊 Mojang 嘅下載連結…"); Add("Wait for Mojang's download link to be read.", "請等讀完 Mojang 嘅下載連結。");
            Add("Mojang offers only the newest release of the Bedrock server, so that is the one installed. It runs as a process on this computer and players connect over UDP. Mojang publishes no checksum for it; the server program must carry Microsoft's signature before it is installed.",
                "Mojang 只提供最新版本嘅 Bedrock 伺服器，所以會安裝嗰個。佢喺呢部電腦以程序運行，玩家經 UDP 連線。Mojang 冇公佈佢嘅校驗碼；伺服器程式要有 Microsoft 嘅簽署先會安裝。");
            Add("Mojang's own server for Bedrock players on phones, consoles and Windows.", "Mojang 自己嘅伺服器，俾用手機、遊戲機同 Windows 嘅 Bedrock 玩家。");
            Add("Bedrock has no other server software; plugins and mods are for Java edition.", "Bedrock 冇其他伺服器軟件；外掛同模組係 Java 版用嘅。");
            Add("The Bedrock server runs as a process on this computer. Choose This computer.", "Bedrock 伺服器會喺呢部電腦以程序運行。請揀「呢部電腦」。");
            Add("The Windows Bedrock server runs here. It needs no Java.", "Windows 版 Bedrock 伺服器喺度運行，唔需要 Java。");
            Add("The UDP port players connect to; the next port is used for IPv6. 1024 to 65535. A free pair is suggested.", "玩家連線用嘅 UDP 端口；下一個端口俾 IPv6 用。1024 至 65535。會建議一對空閒嘅端口。");
            Add("A Bedrock server starts with a new world here. Copy an existing world into its worlds folder after it is made.", "Bedrock 伺服器喺度會由新世界開始。建立之後可以將現有世界複製入佢嘅 worlds 資料夾。");
            Add("Bedrock worlds are made new here; copy an existing world into its worlds folder after the server is made.", "Bedrock 世界喺度會新建；伺服器建立之後可以將現有世界複製入佢嘅 worlds 資料夾。");
            Add("The Bedrock server has no command list to build from, so type the command as you would in its own console, for example list or allowlist add Ava.",
                "Bedrock 伺服器冇指令清單可以用嚟組合，所以請好似喺佢自己嘅主控台咁輸入指令，例如 list 或者 allowlist add Ava。");
            Add("Command", "指令"); Add("A Bedrock server command, without the slash", "Bedrock 伺服器指令，唔使加斜線");
            Add("A Bedrock server keeps its players in allowlist.json and permissions.json, which this section does not edit yet. While the server runs, the console takes allowlist add, allowlist remove, op, deop and kick.",
                "Bedrock 伺服器將玩家記錄喺 allowlist.json 同 permissions.json，呢部分暫時未可以編輯。伺服器運行嗰陣，主控台接受 allowlist add、allowlist remove、op、deop 同 kick。");
            Add("The Bedrock server makes its world in the worlds folder at its first start.", "Bedrock 伺服器第一次啟動嗰陣會喺 worlds 資料夾建立世界。");
            Add("bedrock_server.exe is not in the server's folder.", "伺服器資料夾入面冇 bedrock_server.exe。");
            Add("The download has no bedrock_server.exe, so it was not installed.", "下載嘅檔案冇 bedrock_server.exe，所以冇安裝。");
            Add("Checking the archive…", "檢查緊壓縮檔…"); Add("Copying the server into its folder…", "複製緊伺服器入佢嘅資料夾…");
            Add("server process working set", "伺服器程序工作集");
            Add("Mojang's download links list no Bedrock server for Windows right now.", "Mojang 嘅下載連結而家冇列出 Windows 版 Bedrock 伺服器。");

            // ---- Bedrock settings
            Add("Allow cheats", "容許作弊"); Add("Lets players use commands such as give and time.", "畀玩家用 give 同 time 呢類指令。");
            Add("World name", "世界名稱"); Add("The world's folder inside the worlds folder.", "世界喺 worlds 資料夾入面嘅資料夾。");
            Add("How far from players the world keeps running.", "離玩家幾遠嘅世界會繼續運行。");
            Add("Server name", "伺服器名稱"); Add("The name players see in their server list.", "玩家喺伺服器清單見到嘅名。");
            Add("Every player must sign in with a Microsoft account.", "每個玩家都要用 Microsoft 帳戶登入。");
            Add("Allow list", "允許名單"); Add("Only players in allowlist.json can join.", "只有 allowlist.json 入面嘅玩家可以加入。");
            Add("Permission for new players", "新玩家權限"); Add("What a player who joins for the first time may do.", "第一次加入嘅玩家可以做啲乜。");
            Add("Require the server's packs", "要求用伺服器嘅資源包"); Add("Players must accept the server's resource packs to join.", "玩家要接受伺服器嘅資源包先可以加入。");
            Add("Port (IPv4)", "端口（IPv4）"); Add("Port (IPv6)", "端口（IPv6）"); Add("The UDP port players connect to.", "玩家連線用嘅 UDP 端口。"); Add("The UDP port for IPv6 players.", "IPv6 玩家用嘅 UDP 端口。");
            Add("Show on the local network", "喺區域網絡顯示"); Add("Players on the same network see the server in their Friends tab.", "同一個網絡嘅玩家會喺「朋友」分頁見到呢個伺服器。");
            Add("Packets larger than this are compressed.", "大過呢個大小嘅封包會壓縮。");
            Add("Worker threads", "工作執行緒"); Add("Threads the server may use. 0 lets it pick as many as it can.", "伺服器可以用嘅執行緒。0 即係由佢自己用盡量多。");
            Add("Content log file", "內容記錄檔"); Add("Writes errors from packs and add-ons to a log file.", "將資源包同附加元件嘅錯誤寫入記錄檔。");
        }

        private static void AddMinecraftCataloguePatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));
            string Inner(string s) => Language.Yue(s) ?? s;

            Add(@"^(\d+) updates for this version\.$", "呢個版本有 $1 個更新。");
            Add(@"^The catalogues could not be reached: (.+)$", "連唔到目錄：$1");
            Add(@"^(Modrinth|Hangar) lists (.+) for (\S+)\.$", "$1 列出 $2 適用於 $3。");
            Add(@"^(Modrinth|Hangar) has (.+) for (\S+); this is (.+)\.$", "$1 有適用於 $3 嘅 $2；呢個係 $4。");
            Add(@"^(Modrinth|Hangar) does not list (\S+) for this file, and has no file for (\S+)\.$", "$1 冇為呢個檔案列出 $2，亦冇 $3 用嘅檔案。");
            AddFn(@"^(Compatible|Update|May not work|Not in a catalogue): (.+)$", m => Inner(m.Groups[1].Value) + "：" + Inner(m.Groups[2].Value));
            Add(@"^Update ([A-Z][^.]{0,60})$", "更新 $1");
            Add(@"^Installed (.+) \((.+)\) from Hangar\. The SHA-256 matched\. Restart the server to load it\.$", "已由 Hangar 安裝 $1（$2）。SHA-256 吻合。請重新啟動伺服器去載入。");
            Add(@"^Updated (\d+) plugins\. The folder was backed up first\. Restart the server to load them\.$", "已更新 $1 個外掛。已先備份資料夾。請重新啟動伺服器去載入。");
            Add(@"^Updating (.+) \((\d+) of (\d+)\)…$", "更新緊 $1（第 $2 個，共 $3 個）…");
            AddFn(@"^An update failed, so the folder was put back as it was before: (.+)$", m => "有個更新失敗，所以資料夾已還原成之前咁：" + Inner(m.Groups[1].Value));
            Add(@"^The (plugins|mods) folder is put back as the backup from (\S+) holds it\. The folder as it is now is backed up first, so this can be undone too\.$", "$1 資料夾會還原成 $2 嘅備份。而家嘅資料夾會先備份，所以呢個動作都可以復原。");
            Add(@"^(.+) has no file on Hangar for Paper (\S+)\.$", "$1 喺 Hangar 冇 Paper $2 用嘅檔案。");
            Add(@"^Hangar lists (.+) only as a link to another site, with no checksum, so it was not installed\.$", "Hangar 只係提供 $1 去其他網站嘅連結，冇校驗碼，所以冇安裝。");

            // ---- discovery
            AddFn(@"^(Docker container|Bedrock process|Java process) · (\S+) (\S+) · (running|not running)$", m => Inner(m.Groups[1].Value) + " · " + m.Groups[2].Value + " " + m.Groups[3].Value + " · " + (m.Groups[4].Value == "running" ? "運行中" : "冇運行"));
            Add(@"^Adopt (.+)\?$", "接管 $1？");
            Add(@"^Adopt (.+)$", "接管 $1");
            Add(@"^The server is added to this list and points at the container (.+?)\. Nothing is stopped, moved or changed\. Start, stop and commands then go through Docker\.$",
                "伺服器會加入呢個清單並指向容器 $1。唔會停止、搬走或者改動任何嘢。之後啟動、停止同指令都會經 Docker。");
            Add(@"^The server is added to this list and points at the folder (.+?)\. Nothing is stopped, moved or changed\. A server started elsewhere takes commands, and a graceful stop, only over RCON; once stopped it can be started from here\.$",
                "伺服器會加入呢個清單並指向資料夾 $1。唔會停止、搬走或者改動任何嘢。喺其他地方啟動嘅伺服器只可以經 RCON 接收指令同正常停止；停咗之後可以喺度啟動。");
            Add(@"^process (\d+), started outside this app$", "程序 $1，喺呢個程式以外啟動");
            Add(@"^Minecraft server adopted: (.+)$", "已接管 Minecraft 伺服器：$1");
            Add(@"^Minecraft server created: (.+)$", "已建立 Minecraft 伺服器：$1");
            Add(@"^RCON: (.+)$", "RCON：$1");
            Add(@"^RCON did not answer on port (\d+)\.$", "RCON 喺端口 $1 冇回應。");

            // ---- Bedrock
            Add(@"^Version (\S+) from minecraft\.net\.$", "來自 minecraft.net 嘅版本 $1。");
            Add(@"^Mojang's download link could not be read: (.+)$", "讀唔到 Mojang 嘅下載連結：$1");
            Add(@"^Server software for Bedrock (\S+)$", "Bedrock $1 嘅伺服器軟件");
            Add(@"^UDP port (\d+) is free on this computer\. The Bedrock server takes the memory it needs\.$", "UDP 端口 $1 喺呢部電腦係空閒嘅。Bedrock 伺服器會自己用需要嘅記憶體。");
            Add(@"^port (\d+)$", "端口 $1");
            Add(@"^Downloading the Bedrock server (\S+) from minecraft\.net…$", "由 minecraft.net 下載緊 Bedrock 伺服器 $1…");
            Add(@"^Bedrock worlds in the worlds folder: (.+)\. Backups hold the whole worlds folder\. Import, reset and the world generator are for Java edition\.$",
                "worlds 資料夾入面嘅 Bedrock 世界：$1。備份會包含成個 worlds 資料夾。匯入、重設同世界產生器係 Java 版用嘅。");
            Add(@"^bedrock_server\.exe does not carry a valid signature \(code (\S+)\), so it was not installed\.$", "bedrock_server.exe 冇有效簽署（代碼 $1），所以冇安裝。");
        }
    }
}
