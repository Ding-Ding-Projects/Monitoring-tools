using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>Hong Kong Cantonese for the editable text toast and the install command toast, its messages and its Settings card.</summary>
    internal static partial class Strings
    {
        private static void AddToasts(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- text toast, edited in place
            Add("Copied text", "已複製嘅文字"); Add("edited", "已編輯"); Add("Copied", "已複製");
            Add("Edit text", "編輯文字"); Add("Keep", "保留"); Add("Revert", "還原");
            Add("Copied text, editable", "已複製嘅文字，可以編輯");
            Add("Edit the copied text here. The clipboard follows your edits.", "喺度編輯已複製嘅文字，剪貼簿會跟住你嘅修改。");
            Add("Keep the edited text on the clipboard and close.", "將編輯後嘅文字留喺剪貼簿，然後關閉。");
            Add("Put the original text back on the clipboard.", "將原本嘅文字放返落剪貼簿。");
            Add("Click the text to edit it. The clipboard follows your edits.", "撳一下文字就可以編輯。剪貼簿會跟住你嘅修改。");
            Add("The clipboard already holds your edit.", "剪貼簿已經有你嘅修改。");
            Add("Reverted. The clipboard holds the original text again.", "已還原。剪貼簿又係原本嘅文字。");

            // ---- install command toast
            Add("Install command", "安裝指令"); Add("Packages", "套件"); Add("Package", "套件"); Add("Source", "來源"); Add("Permission", "權限");
            Add("Chocolatey community feed (default)", "Chocolatey 社群來源（預設）");
            Add("winget community source (default)", "winget 社群來源（預設）");
            Add("Windows will ask for permission (consent prompt)", "Windows 會問你批准（同意提示）");
            Add("Installing for every user. Windows will ask for permission.", "為所有使用者安裝。Windows 會問你批准。");
            Add("Installing for this account only. Windows should not ask.", "只為呢個帳戶安裝。Windows 應該唔會問。");
            Add("The package's installer decides. Windows may ask.", "由套件嘅安裝程式決定。Windows 可能會問。");
            Add("Chocolatey is not installed on this computer, so nothing here can run it.", "呢部電腦未安裝 Chocolatey，所以呢度冇嘢可以執行佢。");
            Add("winget is not installed on this computer, so nothing here can run it.", "呢部電腦未安裝 winget，所以呢度冇嘢可以執行佢。");
            Add("Install", "安裝"); Add("Nothing runs until you press Install.", "撳「安裝」之前，乜都唔會執行。");
            Add("Copy command", "複製指令"); Add("Run it yourself in a terminal if you trust it.", "如果你信得過，可以自己喺終端機執行。");
            Add("Not offered to install", "唔提供安裝");
            Add("Installing automatically", "自動安裝緊");
            Add("Automatic mode is on. Press Cancel to stop it.", "自動模式已開。撳「取消」就可以停。");
            Add("Cancel", "取消"); Add("Installing", "安裝緊");
            Add("Stops waiting. An elevated install that already started cannot be stopped from here.", "停止等候。已經開始嘅提升權限安裝喺呢度停唔到。");
            Add("Command checked", "指令已檢查"); Add("Waiting for the Windows consent prompt", "等緊 Windows 嘅同意提示");
            Add("The app never answers it for you.", "程式唔會代你回答。");
            Add("Installed", "已安裝"); Add("Open log", "開啟記錄檔"); Add("Copy summary", "複製摘要");
            Add("Windows asks for a restart to finish.", "Windows 要求重新啟動先完成。");
            Add("Not installed", "未安裝"); Add("Cancelled", "已取消"); Add("Try again", "再試一次"); Add("Copy details", "複製詳情");

            // reasons a command is refused (shown after the flag or word that caused it)
            Add("skips the file check", "會跳過檔案檢查"); Add("hides install failures", "會隱藏安裝失敗");
            Add("reinstalls and overwrites without asking", "會唔問就重新安裝同覆蓋");
            Add("passes options to the install script", "會將選項傳畀安裝腳本");
            Add("passes raw arguments to the installer", "會將原始參數傳畀安裝程式");
            Add("replaces the installer's own arguments", "會取代安裝程式自己嘅參數");
            Add("installs unreleased versions", "會安裝未正式發佈嘅版本");
            Add("installs an older version over a newer one", "會用舊版本蓋過新版本");
            Add("runs extra scripts", "會執行額外腳本"); Add("upgrades everything on the computer", "會升級部電腦所有嘢");
            Add("opens installer windows nobody is watching", "會開出冇人睇住嘅安裝視窗");
            Add("chains a second command", "會接駁第二個指令"); Add("pipes into another command", "會傳入另一個指令");
            Add("redirects input or output", "會重新導向輸入或輸出"); Add("quotes can hide code", "引號可以收埋程式碼");
            Add("can run code or expand a variable", "可以執行程式碼或者展開變數");
            Add("is not allowed in a plain command", "唔可以出現喺普通指令入面");
            Add("picks a custom source; only the default is used", "揀咗自訂來源；只會用預設來源");
            Add("needs a package id after it", "後面要有套件 ID"); Add("winget installs one package per command", "winget 每個指令只裝一個套件");
            Add("accepts only user or machine", "只接受 user 或者 machine"); Add("is not on the allowed list", "唔喺允許清單入面");
            Add("a second line is a second command; paste one command", "第二行就係第二個指令；請只貼一個指令");
            Add("name the package once, with --id", "套件名只可以用 --id 講一次");
            Add("package names use letters, digits, dots, dashes and underscores only", "套件名只可以用字母、數字、點、減號同底線");
            Add("this is a file, not a package name", "呢個係檔案，唔係套件名");
            Add("everything on the computer is not offered", "唔提供「全部」呢種做法");
            Add("no package name was given", "冇寫套件名"); Add("a command may name at most 10 packages", "一個指令最多寫 10 個套件");

            // results and runner messages
            Add("The Windows consent prompt was declined or timed out. Nothing was installed.", "Windows 嘅同意提示被拒絕或者逾時。乜都冇安裝。");
            Add("Installed.", "已安裝。"); Add("Installed. Windows asks for a restart to finish.", "已安裝。Windows 要求重新啟動先完成。");
            Add("Cancelled. The install was stopped.", "已取消。安裝已停止。");
            Add("Stopped waiting. The elevated install may still finish on its own.", "已停止等候。以提升權限執行嘅安裝可能仍然會自己完成。");
            Add("Cancelled.", "已取消。"); Add("The install did not start.", "安裝冇啟動到。");
            Add("The install did not finish in 40 minutes. It may still be running.", "安裝喺 40 分鐘內仍未完成，可能仍然喺度執行。");
            Add("The install did not finish in 40 minutes and was stopped.", "安裝喺 40 分鐘內仍未完成，已經停咗。");
            Add("Chocolatey is not installed on this computer.", "呢部電腦未安裝 Chocolatey。");
            Add("winget is not installed on this computer.", "呢部電腦未安裝 winget。");

            // ---- Settings card
            Add("Install copied commands automatically", "自動安裝已複製嘅指令");
            Add("Install without asking first", "唔問就安裝");
            Add("Off by default. When on, a plain install command starts after a short delay with a Cancel button.", "預設關。開咗之後，普通嘅安裝指令會喺短暫延遲後開始，而且有取消掣。");
            Add("Turn this on only if you trust everything you copy. Anything on the clipboard that matches the rules below will install software.", "淨係喺你信得過所有複製內容時先開。剪貼簿入面符合下面規則嘅內容，都會安裝軟件。");
            Add("Wait before starting", "開始前等候");
            Add("How long the Cancel toast shows before an automatic install starts. 3 to 60 seconds. Default: 10.", "取消通知出現幾耐之後先自動開始安裝。3 至 60 秒。預設：10。");
            Add("Most packages per command", "每個指令最多套件數");
            Add("A command that names more packages than this is shown as a toast and never starts by itself. 1 to 10. Default: 5.", "套件數目超過呢個數字嘅指令只會顯示通知，唔會自己開始。1 至 10。預設：5。");
            Add("seconds", "秒"); Add("packages", "個套件");
            Add("Installed automatically only when", "只有喺以下情況先會自動安裝");
            Add("The clipboard is exactly one command: choco install <name> [-y], choco upgrade <name> [-y] or winget install --id <id> [-e].", "剪貼簿剛好係一個指令：choco install <name> [-y]、choco upgrade <name> [-y] 或者 winget install --id <id> [-e]。");
            Add("No pipes, &&, ;, redirects, variables or quotes that run code.", "冇管道、&&、;、重新導向、變數，或者會執行程式碼嘅引號。");
            Add("Package names use letters, digits, dots, dashes and underscores only.", "套件名只可以用字母、數字、點、減號同底線。");
            Add("The default source is used. A custom source is refused.", "只用預設來源。自訂來源會被拒絕。");
            Add("Windows shows its consent prompt every time for choco. The app never answers, hides or pre-approves it.", "choco 每次都會彈出 Windows 嘅同意提示。程式絕對唔會代答、收埋或者預先批准。");
            Add("Each run is logged in Activity with the exact command.", "每次執行都會連同完整指令記錄喺「活動」。");
            Add("Refused flags", "被拒絕嘅參數"); Add("any flag not on the allowed list", "任何唔喺允許清單入面嘅參數");
            Add("A refused command still gets a toast that says why, with Copy command. It is never silently ignored.", "被拒絕嘅指令仍然會有通知講明原因，同埋有「複製指令」。絕不會靜靜雞忽略。");
        }

        private static void AddToastsPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));

            Add(@"^Scroll to read, click to edit\. Showing 8 of (\d[\d,]*) lines\.$", "捲動閱讀，撳一下就可以編輯。顯示 8 行，共 $1 行。");
            Add(@"^Install (\d+) packages$", "安裝 $1 個套件"); Add(@"^Installing (\d+) packages$", "安裝緊 $1 個套件");
            Add(@"^starts in (\d+) s$", "$1 秒後開始"); Add(@"^and (\d+) more$", "仲有 $1 項");
            Add(@"^Took (\d+) s\. The command is saved in Activity\.$", "用咗 $1 秒。指令已記錄喺「活動」。");
            Add(@"^The install ended with exit code (-?\d+)\.$", "安裝以結束代碼 $1 結束。");
            Add(@"^winget ended with exit code (-?\d+)\.$", "winget 以結束代碼 $1 結束。");
            Add(@"^exit code (-?\d+) · cancelled by the user$", "結束代碼 $1 · 用戶已取消"); Add(@"^exit code (-?\d+)$", "結束代碼 $1");
            // the six numbered rules on the Settings card
            AddFn(@"^(\d)\. (.+)$", m => { var y = Language.Yue(m.Groups[2].Value); return y == null ? null : m.Groups[1].Value + ". " + y; });
        }
    }
}
