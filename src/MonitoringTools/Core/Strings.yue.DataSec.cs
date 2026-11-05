using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Hong Kong Cantonese for the hash, signature, certificate, data and encoding dialogs. File names, subjects, publishers, hashes and the file's own
    /// text are shown as they are; what is translated is the interface around them.
    /// </summary>
    internal static partial class Strings
    {
        private static void AddDataSec(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- the five tools and the clipboard notice buttons
            Add("Hash and verify", "雜湊同驗證"); Add("Who signed this?", "邊個簽署嘅？"); Add("Certificate inspector", "憑證檢視器");
            Add("Data viewer and converter", "資料檢視同轉換"); Add("Encoding and line endings", "編碼同換行符");
            Add("Inspect certificate", "檢視憑證"); Add("View data", "睇資料");
            Add("Shows who signed this program or installer, the certificate chain and whether the file was changed since. Nothing is run.", "顯示邊個簽署呢個程式或安裝程式、憑證鏈，同檔案之後有冇被改過。唔會執行任何嘢。");
            Add("Subject, issuer, expiry countdown, allowed uses and fingerprints. Convert between PEM, DER and PKCS#12.", "主體、簽發者、到期倒數、允許用途同指紋。可以喺 PEM、DER 同 PKCS#12 之間轉換。");
            Add("Table view, validity check with the line and column of an error, and conversion between CSV, JSON and YAML.", "表格檢視、有效性檢查（會講出錯嘅行同列），同 CSV、JSON、YAML 之間轉換。");
            Add("Finds the text encoding and line endings and converts them, with a backup. Binary files are never touched.", "搵出文字編碼同換行符再轉換，會留備份。二進位檔案絕對唔會郁。");
            Add("SHA-256, SHA-1, MD5 or SHA-512 of this file, and a check against the checksum the publisher gave.", "呢個檔案嘅 SHA-256、SHA-1、MD5 或 SHA-512，並同發佈者提供嘅校驗碼對照。");
            Add("Nothing to check", "冇嘢可以檢查"); Add("Nothing to inspect", "冇嘢可以檢視"); Add("Nothing to open", "冇嘢可以開"); Add("The file is no longer there.", "個檔案已經唔喺度。");
            Add("Closes this window (Esc). Work that is running is cancelled and nothing unfinished is kept.", "關閉呢個視窗（Esc）。進行緊嘅工作會取消，未完成嘅嘢唔會留低。");

            // ---- hash and verify
            Add("Algorithm", "演算法");
            Add("MD5 and SHA-1 are fine for spotting damage but not for proving authenticity.", "MD5 同 SHA-1 可以用嚟發現損壞，但證明唔到檔案係真嘅。");
            Add("Copy hash", "複製雜湊值"); Add("Copy as file line", "複製成檔案行");
            Add("Copies the hash alone.", "只複製雜湊值。"); Add("Copies a line in the format checksum files use: the hash, then * and the file name.", "複製校驗碼檔案用嘅格式：雜湊值，之後係 * 同檔案名稱。");
            Add("Copied a checksum line: hash, a space, then the file name.", "已複製一行校驗碼：雜湊值、一個空格，再加檔案名稱。");
            Add("Expected value (paste the publisher's checksum)", "預期值（貼上發佈者提供嘅校驗碼）");
            Add("Paste here to check it", "貼喺呢度就可以對照"); Add("Paste", "貼上"); Add("Expected checksum", "預期嘅校驗碼");
            Add("The checksum the publisher gave. Spaces, dashes and capitals are ignored. A whole line such as 'hash *file name' also works.", "發佈者提供嘅校驗碼。空格、橫線同大細楷都會忽略。成行「雜湊值 *檔案名稱」都得。");
            Add("Spaces, dashes and capitals are ignored. Pasting a whole checksum line works too.", "空格、橫線同大細楷都會忽略。貼成行校驗碼都得。");
            Add("Verify", "驗證"); Add("Reading the file…", "讀緊個檔案…"); Add("Reading the file", "讀緊個檔案");
            Add("Cancel stops reading; nothing is changed.", "取消會停止讀取，乜都唔會改。");
            Add("Stopped", "已停止"); Add("Reading the file was cancelled. Nothing was changed. Choose an algorithm to start again.", "讀取已取消，乜都冇改。揀一個演算法就可以再開始。");
            Add("Stopped. Nothing was changed.", "已停止，乜都冇改。");
            Add("The file could not be read", "讀唔到個檔案"); Add("Could not read the file.", "讀唔到個檔案。");
            Add("It matches", "一致"); Add("It does not match", "唔一致"); Add("Match.", "一致。");
            Add("Do not run or install this file. It is damaged or not the file the publisher published. First difference at character 9.", "唔好執行或安裝呢個檔案。佢已損壞，或者唔係發佈者發佈嘅檔案。第一個分別喺第 9 個字元。");
            Add("Nothing pasted yet.", "未貼上嘢。"); Add("That is not a checksum. A checksum has only the digits 0 to 9 and the letters a to f.", "呢個唔係校驗碼。校驗碼只有數字 0 至 9 同字母 a 至 f。");
            Add("Show in folder", "喺資料夾顯示"); Add("Copy details", "複製詳情"); Add("Convert", "轉換");

            // ---- who signed this
            Add("Checking the signature…", "檢查緊簽署…"); Add("Reading the file. It is not run.", "讀緊個檔案，唔會執行。");
            Add("Revocation is not checked, so this works offline.", "唔會檢查憑證有冇被撤銷，所以離線都用到。");
            Add("The check could not run", "檢查執行唔到");
            Add("Copied the details. They hold no file contents.", "已複製詳情，入面冇檔案內容。");
            Add("This file is not signed", "呢個檔案冇簽署");
            Add("Nobody vouches for who made it. That is common for small or older tools and is not proof of a problem. Check where you got it.", "冇人擔保邊個做嘅。細工具或者舊工具好常見，唔代表有問題。睇下你喺邊度攞到。");
            Add("The signature is broken", "簽署已損壞");
            Add("The file was changed after it was signed. Do not run it. Delete it and get a fresh copy from the publisher.", "個檔案喺簽署之後被改過。唔好執行。刪除佢，再問發佈者攞份新嘅。");
            Add("Signed, but the root is not trusted on this computer", "有簽署，但根憑證喺呢部電腦唔受信任");
            Add("The signature could not be read", "讀唔到簽署");
            Add("Windows cannot read the signature format of this file.", "Windows 讀唔到呢個檔案嘅簽署格式。");
            Add("The signature checks out and the certificate chains to a trusted root. This tells you who made it and that it was not altered, not that it is safe.", "簽署驗證通過，憑證鏈去到受信任嘅根憑證。呢個只係話你知邊個做嘅同有冇被改過，唔代表安全。");
            Add("Signed by Microsoft Windows, and the file is intact", "Microsoft Windows 簽署，檔案完好");
            Add("Certificate chain", "憑證鏈"); Add("Open certificate", "開啟憑證");
            Add("Publisher", "發佈者"); Add("Signed on", "簽署日期"); Add("Certificate valid", "憑證有效期"); Add("Signature kept", "簽署存放喺"); Add("Digest", "摘要"); Add("Thumbprint", "指紋");
            Add("Claims to be from", "自稱來自"); Add("Problem", "問題"); Add("Size", "大小"); Add("Modified", "修改日期");
            Add("Inside the file", "檔案入面"); Add("In a Windows security catalog (a system file)", "喺 Windows 安全目錄入面（系統檔案）");
            Add("Not timestamped, so the date of signing is not proven", "冇時間戳，所以簽署日期冇得證明");
            Add("The check does not ask the issuer whether a certificate was revoked, so it works offline. A signature never says anything about what a program does.", "檢查唔會問簽發者憑證有冇被撤銷，所以離線都用到。簽署從來都唔會講程式會做啲咩。");
            Add("Verified against the Windows trusted list.", "已對照 Windows 受信任清單驗證。"); Add("No signature found.", "搵唔到簽署。");
            Add("Do not rely on this file until the problem is understood.", "搞清楚個問題之前，唔好信呢個檔案。");
            Add("Digest mismatch: the content no longer matches the signature", "摘要唔一致：內容同簽署對唔上");
            Add("The certificate had expired and there is no trusted timestamp that shows when the file was signed.", "憑證已過期，而且冇受信任嘅時間戳證明幾時簽署。");
            Add("The certificate is on this computer's list of distrusted certificates.", "呢個憑證喺呢部電腦嘅不受信任清單入面。");
            Add("The certificate was revoked by its issuer.", "呢個憑證已被簽發者撤銷。");
            Add("Valid", "有效"); Add("Trusted", "受信任"); Add("Trusted root", "受信任根憑證"); Add("Not trusted", "不受信任"); Add("Valid when signed", "簽署時有效"); Add("Expired", "已過期");
            Add("Delete file", "刪除檔案");
            Add("Hash and verify", "雜湊同驗證");

            // ---- certificate inspector
            Add("Subject", "主體"); Add("Issuer", "簽發者"); Add("Alternative names", "別名"); Add("Key", "金鑰"); Add("Serial", "序號"); Add("Thumbprint (SHA-1)", "指紋（SHA-1）");
            Add("Allowed uses", "允許用途"); Add("Convert…", "轉換…"); Add("none", "冇");
            Add("Digital signature", "數碼簽署"); Add("Non-repudiation", "不可否認"); Add("Key encipherment", "金鑰加密"); Add("Data encipherment", "資料加密"); Add("Key agreement", "金鑰協議");
            Add("Certificate signing", "憑證簽發"); Add("CRL signing", "撤銷清單簽署"); Add("Server authentication", "伺服器驗證"); Add("Client authentication", "用戶端驗證");
            Add("Code signing", "程式碼簽署"); Add("Email protection", "電郵保護"); Add("Time stamping", "時間戳"); Add("OCSP signing", "OCSP 簽署"); Add("Certificate authority", "憑證頒發機構");
            Add("Not restricted by the certificate.", "憑證冇限制用途。");
            Add("This file holds a private key. It is never shown here.", "呢個檔案有私密金鑰，呢度永遠唔會顯示。");
            Add("Valid, ending soon", "有效，就快到期"); Add("How much of the validity period has passed", "有效期已過咗幾多");
            Add("This certificate has expired", "呢個憑證已過期"); Add("This certificate is not valid yet", "呢個憑證仲未生效");
            Add("Expired today", "今日過期");
            Add("Password", "密碼"); Add("Password for the certificate file", "憑證檔案嘅密碼"); Add("Show password", "顯示密碼"); Add("Shows or hides the password you typed.", "顯示或者隱藏你打嘅密碼。");
            Add("This file is protected with a password", "呢個檔案受密碼保護");
            Add("It holds a private key. The password is used once to read the certificate and is never stored or written to the log.", "入面有私密金鑰。密碼只會用一次嚟讀憑證，唔會儲存，亦唔會寫入紀錄。");
            Add("That password did not open the file", "個密碼開唔到檔案"); Add("Try again. The file is not changed.", "再試一次。個檔案冇被改。");
            Add("Only the public details are shown. The private key is never displayed.", "只會顯示公開資料。私密金鑰永遠唔會顯示。");
            Add("Open", "開啟"); Add("certificate with private key", "有私密金鑰嘅憑證"); Add("public certificate", "公開憑證");
            Add("This file could not be opened", "呢個檔案開唔到"); Add("Nothing was changed.", "乜都冇改。"); Add("not read", "未讀取");
            Add("Copied the details. A private key or password is never copied.", "已複製詳情。私密金鑰或密碼永遠唔會複製。");
            Add("Convert to", "轉換做"); Add("PEM (.pem)", "PEM (.pem)"); Add("DER (.cer)", "DER (.cer)"); Add("PKCS#12 (.pfx)", "PKCS#12 (.pfx)");
            Add("Include the private key", "包括私密金鑰"); Add("Needs a new password to protect the result.", "要設定新密碼嚟保護結果。"); Add("Only possible when the source has one.", "來源有私密金鑰先做到。");
            Add("This file holds only the public certificate, so a PEM or DER copy is safe to share.", "呢個檔案只有公開憑證，所以 PEM 或 DER 副本可以放心分享。");
            Add("A DER file cannot hold a private key.", "DER 檔案放唔到私密金鑰。");
            Add("The copy will contain the private key, protected by the password below. Do not share it.", "副本會包含私密金鑰，由下面嘅密碼保護。唔好分享。");
            Add("Off: the copy holds the public certificate only and is safe to share.", "關：副本只有公開憑證，可以放心分享。");
            Add("New password for the private key", "私密金鑰嘅新密碼");
            Add("At least 6 characters. Anyone with the file and this password can use the key, so keep both safe.", "至少 6 個字元。有檔案同呢個密碼嘅人都用到條金鑰，所以兩樣都要收好。");
            Add("Save as", "另存為"); Add("File name to save as", "另存嘅檔案名稱");
            Add("Saved next to the original. A name that exists gets a number added, never an overwrite.", "會存喺原檔隔籬。如果個名已經有，會加個數字，絕對唔會覆蓋。");
            Add("Public data only.", "只有公開資料。"); Add("Includes the private key, protected by your password.", "包括私密金鑰，由你嘅密碼保護。");
            Add("Choose a valid file name.", "請揀一個有效嘅檔案名稱。"); Add("Choose a password of at least 6 characters to protect the private key.", "請設定至少 6 個字元嘅密碼嚟保護私密金鑰。");
            Add("Nothing was saved.", "乜都冇存到。"); Add("Format", "格式"); Add("Private key", "私密金鑰"); Add("included, password protected", "已包括，有密碼保護"); Add("not included", "冇包括");
            Add("Saved.", "已儲存。");
            Add("This copy contains the private key, protected by the password you set. Share it only with someone who needs the key.", "呢份副本包含私密金鑰，由你設定嘅密碼保護。只可以分享俾需要條金鑰嘅人。");
            Add("This copy holds only public data, so it is safe to share.", "呢份副本只有公開資料，可以放心分享。");

            // ---- data viewer and converter
            Add("Search rows", "搜尋行"); Add("Reading the file…", "讀緊個檔案…"); Add("Checking that it is valid.", "檢查緊有冇效。");
            Add("Table", "表格"); Add("Text", "文字"); Add("View", "檢視"); Add("Separator", "分隔符號"); Add("Comma", "逗號"); Add("Semicolon", "分號"); Add("Tab", "Tab 鍵"); Add("Auto", "自動");
            Add("File text", "檔案文字"); Add("There is no table until the file is valid. Switch to Text to see the file as it is.", "檔案有效之前冇表格。切換去「文字」就睇到檔案原貌。");
            Add("Select a row and press Ctrl+C to copy it; click a cell first to copy just that cell.", "揀一行再按 Ctrl+C 就可以複製；先點一個儲存格就只複製嗰格。");
            Add("The file is empty.", "個檔案係空嘅。");
            Add("There is text after a closing quote.", "收尾引號之後仲有文字。"); Add("A quote opened here is never closed.", "呢度開咗嘅引號一直冇收尾。");
            Add("A key appears twice in the same mapping.", "同一個映射入面有個鍵出現兩次。");
            Add("Nothing can be converted until it is fixed.", "改好之前，乜都轉換唔到。");
            Add("Pretty print", "排版美化"); Add("Indent 2 spaces", "縮排 2 個空格"); Add("Format in place", "原地整理格式");
            Add("Convert saves a new file. Format in place rewrites this one.", "「轉換」會存一個新檔案。「原地整理格式」會改寫呢個檔案。");
            Add("Fix the file first. Nothing was changed.", "先修正個檔案。乜都冇改。");
            Add("Go to the error", "去到出錯位置"); Add("Copy message", "複製訊息"); Add("Copied the message.", "已複製訊息。");
            Add("Converting…", "轉換緊…"); Add("Copy to clipboard", "複製到剪貼簿"); Add("Copied the converted text.", "已複製轉換後嘅文字。");
            Add("The result is larger than 2 MB, so it is not copied. It is saved as a file.", "結果超過 2 MB，所以冇複製，已存做檔案。");
            Add("Format", "格式"); Add("Formatted in place", "已原地整理格式"); Add("Nothing was changed", "乜都冇改"); Add("Numbers", "數字"); Add("Comments", "註解");
            Add("YAML comments in the original are not carried over.", "原檔入面嘅 YAML 註解唔會帶過去。");
            Add("every value kept as text", "所有值都當文字保留");
            Add("Valid CSV, with a note", "CSV 有效，不過有提示"); Add("Valid JSON, with a note", "JSON 有效，不過有提示"); Add("Valid YAML, with a note", "YAML 有效，不過有提示");
            Add("Nothing to open", "冇嘢可以開");

            // ---- encoding and line endings
            Add("File", "檔案"); Add("Encoding found", "搵到嘅編碼"); Add("Line ends", "換行符"); Add("Result", "結果"); Add("Text encoding", "文字編碼"); Add("Line endings", "換行符");
            Add("UTF-8", "UTF-8"); Add("UTF-8 with BOM", "UTF-8 連 BOM"); Add("UTF-16", "UTF-16"); Add("Keep", "保持原樣");
            Add("Keep a backup", "保留備份"); Add("Original saved as a .bak file next to each file", "原檔會存做 .bak 檔放喺每個檔案隔籬");
            Add("Will change", "會更改"); Add("Mixed ends", "換行符混雜"); Add("Already ok", "已經啱晒"); Add("Cannot decode", "解碼唔到"); Add("Skipped, not text", "已跳過，唔係文字");
            Add("Not text", "唔係文字"); Add("Mixed", "混雜"); Add("No line ends", "冇換行符"); Add("Skipped, already matches", "已跳過，已經一致"); Add("Not ticked, left as it is", "冇剔，保持原樣");
            Add("Show mixed lines", "顯示混雜嘅行"); Add("Show the first lines", "顯示頭幾行");
            Add("This is not a text file", "呢個唔係文字檔");
            Add("This file mixes line endings", "呢個檔案嘅換行符混雜");
            Add("The encoding is a guess", "編碼係估嘅");
            Add("No byte-order mark was found and the bytes are not valid UTF-8, so this uses the Windows code page. If characters look wrong in the preview, choose the encoding by hand.", "搵唔到位元組順序標記，而且啲位元組唔係有效嘅 UTF-8，所以用咗 Windows 代碼頁。如果預覽見到字元唔啱，就自己揀編碼。");
            Add("Could not convert without losing characters", "轉換會遺失字元，所以冇轉");
            Add("What was not changed", "冇改過嘅嘢"); Add("Nothing was written. Choose the source encoding by hand and try again.", "乜都冇寫入。請自己揀來源編碼再試。");
            Add("Read it as Shift-JIS", "當 Shift-JIS 讀"); Add("Choose encoding", "揀編碼");
            Add("No preview: the file cannot be decoded yet.", "冇預覽：個檔案暫時解碼唔到。"); Add("No preview: this is not a text file.", "冇預覽：呢個唔係文字檔。");
            Add("Nothing is ticked that would change.", "冇剔任何會更改嘅檔案。"); Add("Nothing to change: every file already matches.", "冇嘢要改：所有檔案已經一致。");
            Add("Backups were saved as .bak next to each file.", "備份已經存做 .bak 檔，放喺每個檔案隔籬。"); Add("No backups were kept, as chosen.", "按你嘅選擇，冇保留備份。");
            Add("A file that stopped was not changed. The details are below.", "停咗嘅檔案冇被改過。詳情喺下面。");
            Add("Undo from backups", "由備份還原"); Add("Done.", "完成。"); Add("Restored.", "已還原。");
            Add("The converted copies were replaced by the originals and the .bak files were removed.", "轉換後嘅檔案已換返做原檔，.bak 檔亦已移除。");
            Add("UTF-16 LE", "UTF-16（小端）"); Add("UTF-16 BE", "UTF-16（大端）"); Add("UTF-8 BOM", "UTF-8（有 BOM）"); Add("UTF-32 LE", "UTF-32（小端）");
        }

        private static void AddDataSecPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));
            void AddFn(string rx, Func<Match, string> fn) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), fn));
            string Inner(string s) => Language.Yue(s) ?? s;
            string Known(string s) => Language.Yue(s);
            string Num(string s) => s;

            // a banner's accessible name is its title, a full stop, then its sentence
            AddFn(@"^([^.]+?)\. (.+)$", m =>
            {
                var a = Known(m.Groups[1].Value); var b = Known(m.Groups[2].Value);
                return a != null && b != null ? a + "。" + b : null;
            });

            // ---- dialog headers and summaries
            AddFn(@"^(program|installer|library|driver|cabinet|package) · (.+)$", m => (m.Groups[1].Value switch { "program" => "程式", "installer" => "安裝程式", "library" => "程式庫", "driver" => "驅動程式", "cabinet" => "CAB 壓縮檔", _ => "套件" }) + " · " + m.Groups[2].Value);
            AddFn(@"^(public certificate|certificate with private key)(?: · (.+))?$", m => Inner(m.Groups[1].Value) + (m.Groups[2].Success ? " · " + m.Groups[2].Value : ""));
            AddFn(@"^(\d[\d,]*) (rows?) · (\d[\d,]*) (columns?)(?: · (.+))?$", m => m.Groups[1].Value + " 行 · " + m.Groups[3].Value + " 欄" + (m.Groups[5].Success ? " · " + m.Groups[5].Value : ""));
            AddFn(@"^(\d[\d,]*) (items?)(?: · (.+))?$", m => m.Groups[1].Value + " 個項目" + (m.Groups[3].Success ? " · " + m.Groups[3].Value : ""));
            Add(@"^(\d+) files$", "$1 個檔案"); Add(@"^(\d+) rows$", "$1 行");
            Add(@"^(\d+) text files?, (\d+) skipped$", "$1 個文字檔，跳過 $2 個");
            AddFn(@"^(\d+) text files?$", m => m.Groups[1].Value + " 個文字檔");

            // ---- hash and verify
            AddFn(@"^(SHA-256|SHA-1|MD5|SHA-512) of this file$", m => "呢個檔案嘅 " + m.Groups[1].Value);
            Add(@"^Computed in under 1 s(?: · (.+))?$", "計算咗唔使 1 秒 · $1");
            Add(@"^Computed in (\d+) s(?: · (.+))?$", "計算咗 $1 秒 · $2");
            Add(@"^Computed in (\d+) min (\d+) s(?: · (.+))?$", "計算咗 $1 分 $2 秒 · $3");
            AddFn(@"^(\S+ \S+) of (\S+ \S+)(?: · (\S+ MB/s))?(?: · about (.+) left)?$", m =>
            {
                var s = m.Groups[1].Value + " / " + m.Groups[2].Value;
                if (m.Groups[3].Success) s += " · " + m.Groups[3].Value;
                if (m.Groups[4].Success) s += " · 尚餘約 " + Regex.Replace(Regex.Replace(m.Groups[4].Value, @"(\d+) min", "$1 分"), @"(\d+) s", "$1 秒").Replace("under 1 秒", "唔使 1 秒");
                return s;
            });
            Add(@"^Copied the (.+) hash\.$", "已複製 $1 雜湊值。");
            Add(@"^That is (\d+) characters, the length of (.+?)(?:, from the line for (.+))?\.$", "$1 個字元，同 $2 一樣長。");
            Add(@"^This is (\d+) characters, which is (.+)\. Verify will use (.+) instead of (.+)\.$", "呢個有 $1 個字元，即係 $2。驗證會用 $3 而唔係 $4。");
            Add(@"^That is (\d+) characters\. A checksum is 32 \(MD5\), 40 \(SHA-1\), 64 \(SHA-256\) or 128 \(SHA-512\) characters long\.$", "有 $1 個字元。校驗碼要係 32 個（MD5）、40 個（SHA-1）、64 個（SHA-256）或 128 個（SHA-512）字元。");
            Add(@"^This file is identical to what the publisher published\. Algorithm: (.+), all (\d+) characters equal\.$", "呢個檔案同發佈者發佈嘅完全一樣。演算法：$1，全部 $2 個字元都一致。");
            Add(@"^Do not run or install this file\. It is damaged or not the file the publisher published\. First difference at character (\d+)\.$", "唔好執行或安裝呢個檔案。佢已損壞，或者唔係發佈者發佈嘅檔案。第一個分別喺第 $1 個字元。");
            Add(@"^Mismatch: first difference at character (\d+)\.$", "唔一致：第一個分別喺第 $1 個字元。");

            // ---- who signed this
            Add(@"^Signed by (.+), and the file is intact$", "$1 簽署，檔案完好");
            AddFn(@"^The signature is intact(?: and the certificate was valid when signed(?: \(timestamped (.+)\))?)?, but its issuer is not in your trusted list\.$", m =>
                "簽署完好" + (m.Value.Contains("valid when signed") ? "，而且簽署時憑證有效" + (m.Groups[1].Success ? "（時間戳 " + m.Groups[1].Value + "）" : "") : "") + "，但簽發者唔喺你嘅受信任清單入面。");
            AddFn(@"^to (\S+)(, was valid when signed|, valid now|, was not valid when signed)$", m => "至 " + m.Groups[1].Value + (m.Groups[2].Value.Contains("valid when signed") && !m.Groups[2].Value.Contains("not") ? "，簽署時有效" : m.Groups[2].Value.Contains("valid now") ? "，而家有效" : "，簽署時無效"));
            Add(@"^(.+) \(trusted timestamp\)$", "$1（受信任時間戳）");
            Add(@"^Selected (.+)\. Open certificate shows its details\.$", "已揀 $1。「開啟憑證」會顯示詳情。");
            AddFn(@"^(.+), (Valid when signed|Trusted root|Trusted|Valid|Not trusted|Expired)$", m => m.Groups[1].Value + "，" + Inner(m.Groups[2].Value));
            Add(@"^Windows does not accept this signature \(code (.+)\)$", "Windows 唔接受呢個簽署（代碼 $1）");
            Add(@"^The signature itself is damaged \(code (.+)\)$", "簽署本身已損壞（代碼 $1）");
            Add(@"^The issuer is not on this computer's trusted list \(code (.+)\)\.$", "簽發者唔喺呢部電腦嘅受信任清單入面（代碼 $1）。");
            Add(@"^Windows could not confirm the signature \(code (.+)\)\.$", "Windows 確認唔到簽署（代碼 $1）。");

            // ---- certificate inspector
            Add(@"^(\d+) days? left$", "仲有 $1 日");
            Add(@"^Expired (\d+) days? ago$", "$1 日前已過期");
            Add(@"^(\d+) days? left\.$", "仲有 $1 日。");
            Add(@"^Expired (\d+) days? ago\.$", "$1 日前已過期。");
            Add(@"^Expires (\d{4}-\d\d-\d\d)$", "到期日 $1"); Add(@"^Expired (\d{4}-\d\d-\d\d)$", "過期日 $1");
            Add(@"^from (\S+) to (\S+)$", "由 $1 至 $2");
            Add(@"^Expires in (\d+) days?$", "$1 日後到期");
            Add(@"^Renew it before (\S+) so clients keep trusting it\.$", "喺 $1 之前續期，用戶端先會繼續信任。");
            Add(@"^It stopped being valid on (\S+)\. Clients will refuse it\.$", "佢喺 $1 失效。用戶端會拒絕佢。");
            Add(@"^It becomes valid on (\S+)\.$", "佢會喺 $1 生效。");
            Add(@"^The file holds (\d+) certificates\. This is the first\.$", "個檔案有 $1 個憑證，呢個係第一個。");
            Add(@"^Saved (.+)$", "已儲存 $1");

            // ---- data viewer
            AddFn(@"^Valid (CSV|JSON|YAML)$", m => m.Groups[1].Value + " 有效");
            AddFn(@"^Invalid (CSV|JSON|YAML) at line (\d+), column (\d+)$", m => m.Groups[1].Value + " 無效，喺第 " + m.Groups[2].Value + " 行第 " + m.Groups[3].Value + " 列");
            AddFn(@"^Invalid (CSV|JSON|YAML) at line (\d+)$", m => m.Groups[1].Value + " 無效，喺第 " + m.Groups[2].Value + " 行");
            AddFn(@"^Invalid (CSV|JSON|YAML)$", m => m.Groups[1].Value + " 無效");
            AddFn(@"^(Comma|Semicolon|Tab|Bar) separated, (header row found|no header row found)(?:, every row has (\d+) values)?\.(?: Shown: the first (\d+) rows\.)?$", m =>
                (m.Groups[1].Value switch { "Comma" => "逗號分隔", "Semicolon" => "分號分隔", "Tab" => "Tab 分隔", _ => "直線分隔" }) + "，" + (m.Groups[2].Value.StartsWith("header") ? "搵到標題行" : "搵唔到標題行")
                + (m.Groups[3].Success ? "，每行都有 " + m.Groups[3].Value + " 個值" : "") + "。" + (m.Groups[4].Success ? "顯示：頭 " + m.Groups[4].Value + " 行。" : ""));
            AddFn(@"^(\d+) levels?, (\d+) keys? in all, (\d+) (?:keys?|items?) at the top\.(?: No tabs used for indentation\.)?$", m =>
                m.Groups[1].Value + " 層，合共 " + m.Groups[2].Value + " 個鍵，頂層有 " + m.Groups[3].Value + " 個。" + (m.Value.Contains("No tabs") ? "縮排冇用 Tab。" : ""));
            AddFn(@"^(\d+) (?:keys?|items?) at the top\.$", m => "頂層有 " + m.Groups[1].Value + " 個。");
            Add(@"^(\d+) rows? (?:have|has) a different number of values than most rows \((\d+)\)\. Missing values are left empty when converting\.$", "有 $1 行嘅值數目同大部分行（$2）唔同。轉換時缺少嘅值會留空。");
            Add(@"^The file holds (\d+) YAML documents\. Only the first is shown and converted\.$", "個檔案有 $1 份 YAML 文件，只會顯示同轉換第一份。");
            Add(@"^Table of the first (\d+) rows$", "頭 $1 行嘅表格");
            Add(@"^(\d+) of (\d+) rows$", "$1 / $2 行");
            Add(@"^Showing the first (\d[\d,]*) characters\. Conversion uses the whole file\.$", "顯示頭 $1 個字元。轉換會用成個檔案。");
            Add(@"^Showing the first (\d+) of (\d[\d,]*) rows\. Conversion uses all of them\.$", "顯示 $2 行入面嘅頭 $1 行。轉換會用晒所有行。");
            AddFn(@"^Copied (the cell|the row)\.$", m => m.Groups[1].Value == "the cell" ? "已複製儲存格。" : "已複製成行。");
            Add(@"^Saved (.+)$", "已儲存 $1");
            Add(@"^(\d[\d,]*) rows? became (\d[\d,]*) objects? with the header names as keys\.$", "$1 行變成 $2 個物件，用標題名稱做鍵。");
            Add(@"^(\d[\d,]*) records? became (\d[\d,]*) rows?\.$", "$1 筆記錄變成 $2 行。");
            AddFn(@"^The (CSV|JSON|YAML) content was written as (CSV|JSON|YAML)\.$", m => m.Groups[1].Value + " 內容已寫成 " + m.Groups[2].Value + "。");
            Add(@"^(.+) \((CSV|JSON|YAML) was (.+)\)$", "$1（$2 原本係 $3）");
            AddFn(@"^(.+) kept as numbers(?:; (.+) kept as text)?$", m => m.Groups[1].Value.Replace(" and ", "、") + " 當數字保留" + (m.Groups[2].Success ? "；" + m.Groups[2].Value.Replace(" and ", "、") + " 當文字保留" : ""));
            Add(@"^Format (.+) in place\?$", "原地整理 $1 嘅格式？");
            Add(@"^This file is rewritten tidied, and the original is first saved as a \.bak copy next to it\.( YAML comments are not kept\.)?$", "呢個檔案會整理後改寫，原檔會先存做 .bak 副本放喺隔籬。$1");
            Add(@"^Formatted\. The original is saved as (.+)\.$", "已整理格式。原檔已存做 $1。");
            Add(@"^The original is saved as (.+) next to the file\.$", "原檔已存做 $1，放喺個檔案隔籬。");

            // ---- encoding and line endings
            Add(@"^(\d+) of (\d+) files? will change$", "$2 個檔案入面有 $1 個會更改");
            AddFn(@"^(.+) \(guess\)$", m => Inner(m.Groups[1].Value) + "（估）");
            AddFn(@"^(.+) \(chosen\)$", m => Inner(m.Groups[1].Value) + "（自選）");
            Add(@"^(.+), first lines after the change \(visible marks on\)$", "$1，更改後嘅頭幾行（顯示換行標記）");
            Add(@"^(.+), lines whose ending differs from most \(visible marks on\)$", "$1，換行符同大部分唔同嘅行（顯示換行標記）");
            AddFn(@"^(\d+) lines end differently: (.+)\. (?:Choosing LF or CRLF makes them all the same|Converting to (LF|CRLF) makes them all the same, which shell scripts need)\.$", m =>
            {
                var parts = Regex.Replace(m.Groups[2].Value.Replace(" and ", "、"), @"(\d+) with (LF|CRLF|CR)", "$1 行用 $2");
                return m.Groups[1].Value + " 行嘅換行符唔同：" + parts + "。" + (m.Groups[3].Success ? "轉換做 " + m.Groups[3].Value + " 會令全部一致，shell 腳本就需要咁。" : "揀 LF 或 CRLF 就會令全部一致。");
            });
            AddFn(@"^Byte (0x[0-9A-F]+) at position ([\d,]+) is not valid in (.+)\. The file is probably a different encoding, such as Shift-JIS\.$", m => "第 " + m.Groups[2].Value + " 個位置嘅位元組 " + m.Groups[1].Value + " 喺 " + m.Groups[3].Value + " 入面無效。個檔案好可能係另一種編碼，例如 Shift-JIS。");
            Add(@"^Converted (\d+) files?$", "已轉換 $1 個檔案");
            Add(@"^(\d+) converted, (\d+) stopped$", "已轉換 $1 個，停咗 $2 個");
            Add(@"^Restored (\d+) files? from backups$", "已由備份還原 $1 個檔案");
            AddFn(@"^(.*)line ends made (LF|CRLF)$", m => m.Groups[1].Value + "換行符已統一做 " + m.Groups[2].Value);
            AddFn(@"^This is an? (.+) file, not text\. It is never changed here\. It is skipped\.$", m => "呢個係 " + m.Groups[1].Value + " 檔案，唔係文字。呢度絕對唔會改佢。已跳過。");
            Add(@"^This file is larger than 200 MB, which is more than the converter reads\. It is skipped\.$", "呢個檔案超過 200 MB，多過轉換器可以讀嘅份量。已跳過。");
            Add(@"^The bytes look like binary data, not text\. It is never changed here\. It is skipped\.$", "啲位元組似二進位資料，唔似文字。呢度絕對唔會改佢。已跳過。");
        }
    }
}
