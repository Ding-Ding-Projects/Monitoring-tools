# Cantonese review sheet

This sheet lists every entry of the Hong Kong Cantonese table (`Strings.Table` in `Core/Strings.yue.cs` and `Core/Strings.yue.More.cs`), English on the left and Cantonese on the right, grouped by the page or window where the string was first seen when the language audit ran. It exists so that a native speaker can read the wording and correct it.

**No native speaker has reviewed this wording.** It was written by an AI assistant as colloquial Hong Kong Cantonese in traditional characters (for example 嘅, 咗, 唔, 冇, 喺, 啲, 俾/畀) and checked only for completeness and layout, never for naturalness, register or regional usage. Treat every line as a draft. Expect some phrases to read as translated rather than written, and some technical terms (token, jar, otpauth, regular expression) to have a better local word than the one chosen.

How to correct an entry: find the English key in `Core/Strings.yue.cs` or `Core/Strings.yue.More.cs`, change the Cantonese text, and run `MonitoringTools.exe --selftest <folder>` on a hidden desktop. The check `language: every visible string on every page and window has a Cantonese entry` must still report zero untranslated strings.

How this file is produced: the self-test check writes `language-table.tsv` (table entries and the page where each was first seen) and `language-patterns.tsv` (strings that matched a pattern, with the Cantonese they produced). This sheet is those two files formatted. Entries are matched ignoring case, so an upper-case eyebrow uses the same line.

Entries: 2014 table entries, 678 strings produced by patterns.

## Contents

- [Appearance editor](#appearance-editor) (51)
- [Bulk selection](#bulk-selection) (15)
- [Colour picker](#colour-picker) (22)
- [Command palette](#command-palette) (55)
- [Commit template](#commit-template) (7)
- [Confirmation](#confirmation) (10)
- [Confirmation armed](#confirmation-armed) (1)
- [File tools tab 1](#file-tools-tab-1) (5)
- [File tools tab 2](#file-tools-tab-2) (4)
- [File tools tab 3](#file-tools-tab-3) (5)
- [Guided forms (DNS profile, new shortcut, new server)](#guided-forms-dns-profile-new-shortcut-new-server) (41)
- [History panel](#history-panel) (6)
- [Offline dialog](#offline-dialog) (23)
- [Page: about](#page-about) (14)
- [Page: activity](#page-activity) (11)
- [Page: appearance](#page-appearance) (76)
- [Page: clipboard](#page-clipboard) (16)
- [Page: conversion](#page-conversion) (17)
- [Page: game](#page-game) (10)
- [Page: help](#page-help) (49)
- [Page: language](#page-language) (31)
- [Page: minecraft](#page-minecraft) (89)
- [Page: modes](#page-modes) (94)
- [Page: network](#page-network) (28)
- [Page: ollama](#page-ollama) (149)
- [Page: overview](#page-overview) (27)
- [Page: records](#page-records) (41)
- [Page: safety](#page-safety) (67)
- [Page: service](#page-service) (12)
- [Page: shortcuts](#page-shortcuts) (28)
- [Page: tools](#page-tools) (62)
- [Page: tools (forge host choice)](#page-tools-forge-host-choice) (17)
- [Regex builder](#regex-builder) (212)
- [Search](#search) (1)
- [Setup wizard](#setup-wizard) (53)
- [Toasts](#toasts) (44)
- [Tray menu](#tray-menu) (26)
- [Welcome](#welcome) (16)
- [Not reached by the audit (messages, dialogs and states that need a particular event)](#not-reached-by-the-audit-messages-dialogs-and-states-that-need-a-particular-event) (579)
- [Strings produced by patterns](#strings-produced-by-patterns)

## Appearance editor

| English | Cantonese |
| --- | --- |
| 0 to 12 | 0 至 12 |
| 8 to 72 | 8 至 72 |
| Appearance editor status | 外觀編輯器狀態 |
| Black | 特粗 |
| Bold | 粗體 |
| Close (Esc) | 關閉 (Esc) |
| Close appearance editor | 關閉外觀編輯器 |
| Colour | 顏色 |
| Colour settings | 顏色設定 |
| Decrease font size | 縮細字體 |
| Decrease letter spacing | 收窄字距 |
| Edit appearance | 編輯外觀 |
| Font | 字體 |
| Font family | 字體系列 |
| Font settings | 字體設定 |
| font size | 字體大小 |
| Increase font size | 放大字體 |
| Increase letter spacing | 加闊字距 |
| Installed fonts | 已安裝嘅字體 |
| Italic | 斜體 |
| Letter spacing | 字距 |
| Library settings | 資料庫設定 |
| Light weight | 細幼字重 |
| Redo (Ctrl+Y) | 重做 (Ctrl+Y) |
| Redo appearance change | 重做外觀更改 |
| Regular | 正常 |
| Reset all… | 全部重設… |
| Reset element | 重設元素 |
| Reset Font family | 重設字體系列 |
| Reset Letter spacing | 重設字距 |
| Reset Size | 重設大小 |
| Reset Style | 重設樣式 |
| Reset Underline | 重設底線 |
| Reset Weight | 重設粗幼 |
| Return font family to the theme's value | 將字體系列還原做主題嘅數值 |
| Return letter spacing to the theme's value | 將字距還原做主題嘅數值 |
| Return size to the theme's value | 將大小還原做主題嘅數值 |
| Return style to the theme's value | 將樣式還原做主題嘅數值 |
| Return underline to the theme's value | 將底線還原做主題嘅數值 |
| Return weight to the theme's value | 將粗幼還原做主題嘅數值 |
| Sample text 123 | 樣本文字 123 |
| Saved by page and element text. Elements that share the same text on this page share the look. Cards recolour themselves when the pointer passes over them, so a background set on a card may not hold until that is changed. | 按頁面同元素文字儲存。同一頁面上文字相同嘅元素會共用同一個外觀。卡片喺滑鼠經過嗰陣會自己轉色，所以設喺卡片嘅背景可能要等嗰個狀態改變先會保持。 |
| Semibold | 半粗 |
| Shape | 形狀 |
| Shape settings | 形狀設定 |
| Size | 大小 |
| Style | 樣式 |
| Underline | 底線 |
| Undo appearance change | 復原外觀更改 |
| Undo the last change in this editor (Ctrl+Z) | 復原呢個編輯器入面最後一次更改 (Ctrl+Z) |
| Weight | 粗幼 |

## Bulk selection

| English | Cantonese |
| --- | --- |
| Choose several releases to copy, export or remove together. Off by default. | 揀幾個版本一齊複製、匯出或者移除。預設關閉。 |
| Copy | 複製 |
| Copy the chosen notifications to the clipboard as plain text. | 將所揀嘅通知以純文字複製到剪貼簿。 |
| Done selecting | 揀完 |
| Export as text… | 匯出成文字檔… |
| Leave select mode and clear the selection. | 離開選取模式並清除已選項目。 |
| Remove from history | 喺紀錄度移除 |
| Remove only the chosen notifications from the centre, after you confirm. Others stay. | 確認之後，只會由通知中心移除所揀嘅通知。其他會保留。 |
| Remove selected | 移除所選項目 |
| Save as text… | 存做文字檔… |
| Save the chosen notifications to a text file. | 將所揀嘅通知儲存到文字檔。 |
| Select all shown | 揀晒顯示緊嘅 |
| Select none | 全部不選 |
| Stop watching the chosen games. Restore defaults brings the built-in list back. | 停止監察所揀嘅遊戲。「還原預設值」會帶返內置清單。 |
| ·  RUNNING NOW | ·  而家運行緊 |

## Colour picker

| English | Cantonese |
| --- | --- |
| #RGB, #RRGGBB, or #RRGGBBAA (the last two digits are opacity). | #RGB、#RRGGBB 或者 #RRGGBBAA（最後兩位係不透明度）。 |
| CIELAB (D65): lightness 0 to 100, then a and b, about -128 to 128. Values outside sRGB are clipped and the picker says so. | CIELAB（D65）：亮度 0 至 100，然後係 a 同 b，大約 -128 至 128。超出 sRGB 嘅數值會被截走，選擇器會講明。 |
| CIELAB lightness 0 to 100, chroma about 0 to 150, hue 0 to 360 (D65). Values outside sRGB are clipped and the picker says so. | CIELAB 亮度 0 至 100，色度大約 0 至 150，色相 0 至 360（D65）。超出 sRGB 嘅數值會被截走，選擇器會講明。 |
| Click any pixel on any screen to take its colour. Press Esc to cancel. | 撳任何螢幕上嘅任何一點，就會攞到嗰點嘅顏色。按 Esc 取消。 |
| Colour picker | 顏色選擇器 |
| Colour space: sRGB. Edit any field and press Enter; the others follow. | 色彩空間：sRGB。改任何一個欄位然後按 Enter，其餘欄位會跟住變。 |
| Cyan, magenta, yellow and key (black) 0 to 100. A simple conversion with no printer profile. | 青、洋紅、黃同黑（K）0 至 100。只係簡單轉換，冇用印表機色彩設定檔。 |
| Drag, or use the arrow keys. Hold Shift for larger steps. | 拖曳，或者用方向鍵。按住 Shift 可以行大步啲。 |
| Drag, or use the left and right arrow keys. | 拖曳，或者用左右方向鍵。 |
| Hue | 色相 |
| Hue 0 to 360, saturation and lightness 0 to 100. | 色相 0 至 360，飽和度同亮度 0 至 100。 |
| Hue 0 to 360, saturation and value (brightness) 0 to 100. | 色相 0 至 360，飽和度同明度（亮度）0 至 100。 |
| Hue 0 to 360, whiteness and blackness 0 to 100. | 色相 0 至 360，白度同黑度 0 至 100。 |
| Lightness 0 to 1, chroma about 0 to 0.4, hue 0 to 360. Values outside sRGB are clipped and the picker says so. | 亮度 0 至 1，色度大約 0 至 0.4，色相 0 至 360。超出 sRGB 嘅數值會被截走，選擇器會講明。 |
| Opacity | 不透明度 |
| Original  ·  New | 原本  ·  新 |
| Original colour on the left, new colour on the right | 左邊係原本嘅顏色，右邊係新顏色 |
| Pick from screen | 由螢幕選色 |
| Recent | 最近用過 |
| Red, green and blue from 0 to 255, optionally followed by opacity from 0 to 1. | 紅、綠、藍 0 至 255，後面可以再加不透明度 0 至 1。 |
| Saturation and brightness field | 飽和度同亮度區域 |
| Theme colours | 主題顏色 |

## Command palette

| English | Cantonese |
| --- | --- |
| About and updates | 關於同更新 |
| Actions | 操作 |
| Activity | 活動紀錄 |
| Builder | 砌表達式 |
| Check every | 檢查間隔 |
| Check public IP now | 即刻檢查公開 IP |
| Choose a personal wording file | 揀個人用語檔案 |
| Clipboard settings | 剪貼簿設定 |
| Command playground, FFmpeg, formats | 指令試玩、FFmpeg、格式 |
| Conversion | 轉檔 |
| Create and run servers here, in Docker or over SSH | 喺本機、Docker 或者經 SSH 建立同執行伺服器 |
| Everything the service has done | 服務做過嘅所有嘢 |
| Focus | 專注 |
| Focus spotlight | 專注聚光燈 |
| Full | 最大聲 |
| Keep open on hover | 滑鼠停留時保持開啟 |
| Live latency, outages and activity | 即時延遲、斷線同活動 |
| Low | 細聲 |
| Low stimulation | 低刺激 |
| Medium | 中等 |
| Minecraft servers | Minecraft 伺服器 |
| Momentum | 動力 |
| Network and DNS | 網絡同 DNS |
| No file loaded | 未載入檔案 |
| One thing at a time | 一次做一樣 |
| Open IP history repository | 開啟 IP 紀錄儲存庫 |
| Overview | 總覽 |
| Pages | 頁面 |
| Personal wording | 個人用語 |
| Preview the offline dialog | 預覽斷線視窗 |
| Public IP history, DNS profiles, alerts | 公開 IP 紀錄、DNS 設定檔、警示 |
| Recent copies, kept in memory only | 最近複製，只留喺記憶體 |
| Release and renew IP | 釋放並更新 IP |
| Reminder interval | 提醒間隔 |
| Render Markdown | 顯示 Markdown 格式 |
| Run setup again | 重新執行設定 |
| Search commands, pages and settings | 搜尋指令、頁面同設定 |
| Service | 背景服務 |
| Settings | 設定 |
| Shortcuts | 捷徑 |
| Show sample toasts | 顯示範例通知 |
| Skip password managers | 略過密碼管理員 |
| Snooze 1 hour | 暫停一小時 |
| Sound effects | 音效 |
| Start-up, administrator rights, uninstall | 開機啟動、系統管理員權限、解除安裝 |
| Suppress while gaming | 遊戲時收埋通知 |
| Switch between a card and the full window | 喺卡片同全視窗之間切換 |
| Time awareness | 時間意識 |
| Toasts, Markdown, media folder | 通知、Markdown、媒體資料夾 |
| Uninstall service | 解除安裝服務 |
| Version, bundled tools, update check | 版本、內置工具、更新檢查 |
| Volume | 音量 |
| Watch the public IP | 監察公開 IP |
| Watched games and fullscreen | 監察嘅遊戲同全螢幕 |
| Your :commands and the script editor | 你嘅 :指令同腳本編輯器 |

## Commit template

| English | Cantonese |
| --- | --- |
| Default commit message | 預設提交訊息 |
| Every :push uses this message. Click a snippet to drop it in; it fills in at push time. | 每次 :push 都用呢個訊息。撳一個片段就會插入，推送時先會填入內容。 |
| FIRST :PUSH | 第一次 :PUSH |
| Not now | 遲啲先 |
| PREVIEW | 預覽 |
| Save | 儲存 |
| Set a default commit message | 設定預設提交訊息 |

## Confirmation

| English | Cantonese |
| --- | --- |
| Also remove settings, logs and scripts | 同時移除設定、日誌同腳本 |
| Both keys are off. | 兩把鑰匙都未轉。 |
| Emergency exit | 緊急出口 |
| First key | 第一條鎖匙 |
| Second key | 第二條鎖匙 |
| Slide to uninstall | 拖動以解除安裝 |
| This stops the background service, removes it from start-up and deletes the program. | 會停止背景服務、由開機啟動移除，並刪除程式。 |
| To go ahead, turn both keys, then slide all the way. | 要繼續，就扭兩條鎖匙，再一路拉到盡。 |
| Uninstall Monitoring Tools? | 解除安裝 Monitoring Tools？ |
| Uninstall: slide to 100 to confirm | 解除安裝：拖動到 100 確認 |

## Confirmation armed

| English | Cantonese |
| --- | --- |
| Both keys are on. Slide all the way to confirm. | 兩把鑰匙都轉咗。一直拖到盡頭就確認。 |

## File tools tab 1

| English | Cantonese |
| --- | --- |
| Copy to a folder… | 複製到資料夾… |
| Deletes go to the Recycle Bin | 刪除嘅檔案會去資源回收筒 |
| Encrypt | 加密 |
| Move and copy | 移動同複製 |
| Move to a folder… | 移動到資料夾… |

## File tools tab 2

| English | Cantonese |
| --- | --- |
| Decode | 解碼 |
| Encode | 編碼 |
| Turns .b64 files back into the originals. | 將 .b64 檔案還原成原檔。 |
| Writes name.ext.b64 next to each file. | 會喺每個檔案旁邊寫入 name.ext.b64。 |

## File tools tab 3

| English | Cantonese |
| --- | --- |
| AES-256-GCM. Writes name.ext.mtenc beside each file. There is no way back without the password. | AES-256-GCM。會喺每個檔案旁邊寫入 name.ext.mtenc。冇密碼就唔可能還原。 |
| Confirm | 確認 |
| Decrypt | 解密 |
| Password | 密碼 |
| Send originals to the Recycle Bin afterwards | 完成後將原檔送去資源回收筒 |

## Guided forms (DNS profile, new shortcut, new server)

| English | Cantonese |
| --- | --- |
| A short label that appears in the list and in the notice when you switch. A profile with the same name is replaced. | 顯示喺清單同切換通知入面嘅簡短標籤。同名嘅設定檔會被取代。 |
| A short label, for example Work or Family filter | 簡短標籤，例如「工作」或者「家庭過濾」 |
| Add a DNS profile | 新增 DNS 設定檔 |
| Asks Windows for administrator rights on each run. Leave off unless the script needs them. | 每次執行都會向 Windows 要求管理員權限。除非腳本需要，否則請保持關閉。 |
| Batch file | 批次檔 |
| Blank | 空白 |
| Blank batch file | 空白批次檔 |
| Blank PowerShell script | 空白 PowerShell 腳本 |
| Check what will be created. | 檢查將會建立咩。 |
| Choose a version first. | 請先揀版本。 |
| Choose how it runs | 選擇執行方式 |
| Computes the SHA-256 hash of the file, prints it, and copies it to the clipboard. | 計算檔案嘅 SHA-256 雜湊值，顯示出嚟，並複製到剪貼簿。 |
| Copy path | 複製路徑 |
| Creates a .zip next to the file or folder, named after it, using the tar tool that ships with Windows. | 喺檔案或資料夾旁邊建立一個 .zip，用同一個名稱，使用 Windows 內置嘅 tar 工具。 |
| Each one fills the script for you and can be edited afterwards. The blank ones use the app's own template. | 每個選項都會幫你填好腳本，之後仍然可以修改。空白嗰啲用程式自己嘅範本。 |
| Give the profile a name. | 請為設定檔命名。 |
| Hash file | 檔案雜湊 |
| Letters, digits, - and _, starting with a letter, 2 to 32 characters | 字母、數字、- 同 _，以字母開頭，2 至 32 個字元 |
| Makes a copy of an image no wider than 1600 pixels, keeping its proportions, saved beside the original with -resized in the name. The original is untouched. | 製作一張闊度唔超過 1600 像素嘅圖片副本，保持比例，儲存喺原圖旁邊，名稱加上 -resized。原圖保持不變。 |
| Name  (you type it after a colon, for example :script) | 名稱（喺冒號後面輸入，例如 :script） |
| Name it | 為佢命名 |
| Name the profile | 為設定檔命名 |
| Off runs the script hidden. Turn it on to see its output as it runs. | 關閉就會隱藏執行腳本。開啟就可以喺執行時睇到輸出。 |
| Opens the folder in Explorer. If the copied path is a file, its containing folder opens. | 喺檔案總管開啟資料夾。如果複製咗嘅路徑係檔案，就會開啟佢所在嘅資料夾。 |
| Pick a starting point | 揀一個起點 |
| Press “Read this version's worldgen files” to list them. | 撳「讀取呢個版本嘅世界生成檔案」就會列出佢哋。 |
| Puts the full path on the clipboard, without quotes, ready to paste. | 將完整路徑放上剪貼簿，唔帶引號，可以直接貼上。 |
| Rename by date | 按日期重新命名 |
| Renames the file to start with its last-modified date as yyyy-MM-dd. A file that already starts with a date is left alone. | 將檔案重新命名，開頭加上最後修改日期（yyyy-MM-dd）。已經以日期開頭嘅檔案唔會處理。 |
| Resize image | 縮放圖片 |
| Review | 檢視 |
| Script preview | 腳本預覽 |
| Shortcut name | 捷徑名稱 |
| Shows as the tooltip on the shortcut chip | 會顯示做捷徑標籤嘅提示 |
| Starts with the app's own template: %~1 receives whatever was before the colon, and one line prints it. | 由程式自己嘅範本開始：%~1 接收冒號前面嘅任何內容，再加一行將佢顯示出嚟。 |
| Starts with the app's own template: a $Target parameter that receives whatever was before the colon, and one line that prints it. | 由程式自己嘅範本開始：一個 $Target 參數，接收冒號前面嘅任何內容，再加一行將佢顯示出嚟。 |
| What it does | 功能說明 |
| What it does (optional) | 功能說明（可選） |
| You can change either choice later in the editor. | 兩個選擇之後都可以喺編輯器更改。 |
| You run the shortcut by copying a path or text and adding :name to the end. | 複製路徑或者文字，尾加 :名稱，就可以執行捷徑。 |
| Zip it | 壓縮成 ZIP |

## History panel

| English | Cantonese |
| --- | --- |
| Every save keeps a copy on this computer, up to 50; the oldest is dropped first. Pick a version to compare it with the text as it is now. Restore puts that version in the editor and saves it, after you confirm. The text you are replacing is kept in the history first. | 每次儲存都會喺呢部電腦留一份副本，最多 50 份，最舊嗰份先被捨棄。揀一個版本，同而家嘅文字比較。確認之後，「還原」會將嗰個版本放入編輯器並儲存。被取代嘅文字會先留入紀錄。 |
| History of this text | 呢段文字嘅紀錄 |
| History panel | 紀錄面板 |
| No earlier versions yet. The first save creates one. | 未有更早嘅版本。第一次儲存就會建立一個。 |
| Replaces the text in the editor with the selected version, after a two-key confirmation. Off until you pick a version. | 經兩把鑰匙確認之後，用所揀版本取代編輯器入面嘅文字。要揀咗版本先可以用。 |
| Restore this version | 還原呢個版本 |

## Offline dialog

| English | Cantonese |
| --- | --- |
| Adapter | 網絡卡 |
| Automatic | 自動 |
| Automatic (from router) | 自動（由路由器提供） |
| by IP | 用 IP |
| Cloudflare | Cloudflare |
| Copy details | 複製詳情 |
| Dismiss until the connection changes | 關閉，直到連線情況有變 |
| DNS servers | DNS 伺服器 |
| Down since | 斷線時間 |
| Flush DNS | 清除 DNS 快取 |
| Gateway | 閘道 |
| Google | Google |
| Internet | 互聯網 |
| Internet connection lost | 網絡連線中斷 |
| Network | 網絡 |
| Network settings | 網絡設定 |
| PROBE LADDER | 探測階梯 |
| Release & renew | 釋放並更新 |
| Retry now | 即刻重試 |
| SWITCH DNS | 轉換 DNS |
| The line is up, but DNS lookups are failing, so websites by name will not load. Switching DNS usually fixes this. | 線路係通嘅，但 DNS 查詢失敗，所以用名稱開唔到網站。轉 DNS 通常可以解決。 |
| timeout | 逾時 |
| up | 正常 |

## Page: about

| English | Cantonese |
| --- | --- |
| About | 關於 |
| Check for updates | 檢查更新 |
| Clipboard history stays in memory and is gone when the service stops. Copies that password managers mark private are skipped. The only network calls are the connectivity probes and this update check. | 剪貼簿紀錄只留喺記憶體，服務停止就消失。密碼管理員標記為私人嘅複製會被略過。唯一嘅網絡連線係連線探測同呢個更新檢查。 |
| Clipboard toasts, internet watch and quick commands for Windows. | 適用於 Windows 嘅剪貼簿通知、網絡監察同快捷指令。 |
| Every change to the project ships as a new release on GitHub. | 項目每次更改都會喺 GitHub 發佈新版本。 |
| Logs | 日誌 |
| Privacy | 私隱 |
| Program | 程式 |
| Project page | 項目頁面 |
| Release notes | 版本說明 |
| Saved media | 已儲存媒體 |
| Scripts | 腳本 |
| Updates | 更新 |
| Where things live | 檔案位置 |

## Page: activity

| English | Cantonese |
| --- | --- |
| All | 全部 |
| Choose several entries to copy, export or remove together. Off by default. | 揀幾個項目一齊複製、匯出或者移除。預設關閉。 |
| Choose several outages to copy, export or remove together. Off by default. | 揀幾次斷線一齊複製、匯出或者移除。預設關閉。 |
| Conversions | 轉檔 |
| Everything | 所有嘢 |
| Files | 檔案 |
| Kept in memory while the service runs. An outage that is still going cannot be removed. | 服務運行期間保留喺記憶體。仍然持續中嘅斷線唔可以移除。 |
| Kept in memory while the service runs. The log folder keeps the full record. | 服務行緊期間留喺記憶體，完整紀錄喺日誌資料夾。 |
| Pushes | 推送 |
| Select | 揀選 |
| What it has been doing | 佢做過啲咩 |

## Page: appearance

| English | Cantonese |
| --- | --- |
| A custom logo colour as #RRGGBB, applied when the field loses focus or you press Enter. Only used when Logo tint is Custom colour. Default: empty. | 自訂標誌顏色，格式 #RRGGBB，欄位失去焦點或者撳 Enter 就會套用。只喺標誌色調設為「自訂顏色」先會用。預設：空白。 |
| A guided way to set theme, seed colour, contrast, density and text size together, with a review of what changes before anything is applied. | 引導式一次過設定主題、主色、對比、密度同文字大小，套用之前會先俾你睇有咩改變。 |
| A switch, turned on | 一個開關，已開啟 |
| Amber | 琥珀色 |
| Another | 另一個 |
| Appearance | 外觀 |
| Bolt | 閃電 |
| Box | 盒子 |
| Chip | 標籤 |
| Choose a glyph from the app's icon set, or your own PNG or ICO picture, for the drawer logo and the tray icon. The tint above still colours a glyph. Starts as the Pulse glyph. | 喺程式嘅圖示庫揀一個圖案，或者用你自己嘅 PNG 或 ICO 圖片，做側欄標誌同系統匣圖示。上面嘅色調仍然會令圖案變色。預設係「脈搏」圖案。 |
| Choose a PNG or ICO... | 揀 PNG 或 ICO... |
| Clock | 時鐘 |
| Code | 程式碼 |
| Colours and text size apply at once. Code-built pages pick up new colours when the page reloads; a few older windows pick them up the next time they open. | 顏色同文字大小即時生效。用程式碼砌嘅頁面會喺頁面重新載入嗰陣用新顏色；少數較舊嘅視窗要下次開啟先會跟。 |
| Comfortable | 舒適 |
| Compact | 緊湊 |
| Compact tightens row heights so more fits on screen. Default: Comfortable. | 緊湊會收窄行高，令螢幕可以放多啲內容。預設：舒適。 |
| Controller | 遊戲手掣 |
| Custom colour | 自訂顏色 |
| Dark | 深色 |
| Density | 密度 |
| Display name | 顯示名稱 |
| Drawer logo | 側欄標誌 |
| Drawer logo preview | 側欄標誌預覽 |
| Drawn with the colours in use right now. | 用而家使用緊嘅顏色繪畫。 |
| Draws dividers and card borders in the stronger outline colour so edges are easier to see. Default: off. | 用較深嘅輪廓顏色畫分隔線同卡片邊框，令邊緣更易睇。預設：關。 |
| Filled | 實心 |
| Follow Windows | 跟隨 Windows |
| Globe | 地球 |
| Glyph | 圖案 |
| High-contrast outlines | 高對比輪廓 |
| How big things are, how tightly they sit and how much moves. | 東西幾大、排得幾密，同有幾多動態。 |
| Light | 淺色 |
| Light or dark colours. System follows the Windows app mode and changes with it. Default: System. | 淺色或者深色。「系統」會跟隨 Windows 嘅應用程式模式一齊轉。預設：系統。 |
| Light, dark or whatever Windows is using, plus the colour the app is built around. | 淺色、深色或者 Windows 而家用緊嘅，再加上程式圍繞嘅主色。 |
| Logo glyph and image | 標誌圖案同圖片 |
| Logo tint | 標誌色調 |
| Make a look | 製作造型 |
| Make a look step by step | 逐步製作造型 |
| Match seed colour | 配合主色 |
| Monitor | 螢幕 |
| Moon | 月亮 |
| Name and logo | 名稱同標誌 |
| Ocean | 海洋 |
| On | 開 |
| On skips animation. Follow Windows uses the Windows animation setting. Off always animates. Default: Follow Windows. | 「開」會跳過動畫。「跟隨 Windows」用 Windows 嘅動畫設定。「關」永遠都有動畫。預設：跟隨 Windows。 |
| Outlined | 外框 |
| Plain | 簡明 |
| PNG or ICO only, up to 256 KB, 16 to 512 pixels on each side. The picture is checked, then copied into the app's data folder; the original is never read again. | 只限 PNG 或 ICO，最大 256 KB，每邊 16 至 512 像素。圖片會先檢查，然後複製入程式嘅資料夾；之後唔會再讀取原檔。 |
| Pulse | 脈搏 |
| Puts every choice on this page back to its default: System theme, Mint, 100 %, Comfortable, Follow Windows, normal outlines, the default name and a seed-coloured logo. | 將呢頁所有選擇還原做預設：系統主題、薄荷色、100 %、舒適、跟隨 Windows、普通輪廓、預設名稱，同主色標誌。 |
| Reduce motion | 減少動態效果 |
| Remove the image | 移除圖片 |
| Reset | 重設 |
| Reset logo | 重設標誌 |
| Reset to defaults | 還原預設值 |
| Scales text and spacing together in open windows, 90 to 150 percent. Default: 100 %. | 喺已開啟嘅視窗同時縮放文字同間距，90 至 150%。預設：100 %。 |
| Seed colour | 主色 |
| Shield | 盾牌 |
| Shown in headers and the tray tooltip | 顯示喺標題列同系統匣提示 |
| Size, density and motion | 大小、密度同動態效果 |
| System | 系統 |
| Text size | 文字大小 |
| The colour every other colour is derived from. Each one has a full light and dark set. Default: Mint. | 其他所有顏色都由呢個顏色衍生。每個主色都有完整嘅淺色同深色配套。預設：薄荷色。 |
| The colour of the tray and header glyph. Custom uses the hex value below. Default: Match seed colour. | 系統匣同標題列圖示嘅顏色。「自訂」會用下面嘅十六進制數值。預設：配合主色。 |
| The name shown for the app. An empty name goes back to the default. Default: Monitoring Tools. | 程式顯示嘅名稱。留空就會返回預設。預設：Monitoring Tools。 |
| The symbol drawn in the drawer header and the tray icon. Choosing one replaces a custom image. Default: the pulse line. | 畫喺側欄標題同系統匣圖示嘅符號。揀一個就會取代自訂圖片。預設：脈搏線。 |
| Theme | 主題 |
| Theme and colour | 主題同顏色 |
| Theme, colour, text size, density and motion. | 主題、顏色、字體大小、密度同動態效果。 |
| Tonal | 色調 |
| Tray icon (the status dot is added on top) | 系統匣圖示（狀態圓點會加喺上面） |
| Tray icon preview | 系統匣圖示預覽 |
| Violet | 紫色 |
| What the app calls itself and the tint of its glyph. | 程式自稱嘅名稱，同佢圖示嘅色調。 |
| Wi-Fi | Wi-Fi |

## Page: clipboard

| English | Cantonese |
| --- | --- |
| Copies the source app marks as private (password managers do this) never pop up or reach history. On by default. | 來源程式標記為私人嘅複製（密碼管理員都會咁做）唔會彈出，亦唔會入紀錄。預設開啟。 |
| History size | 紀錄數量 |
| How long a toast stays on screen before it fades. Default: 3 s. | 通知喺螢幕停留幾耐先淡出。預設：3 秒。 |
| How loud the cues are. This does not change the Windows volume. Default: Medium. | 提示音幾大聲。呢個唔會改變 Windows 嘅音量。預設：中等。 |
| How many recent copies are kept in memory for the History page. Quitting the app clears them. Default: 50. | 「歷史」頁面喺記憶體保留最近幾多次複製。退出程式就會清除。預設：50。 |
| Media folder | 媒體資料夾 |
| Opens an explanation of this setting, its default and a sample you can try. | 開啟呢個設定嘅說明、預設值同一個可以試用嘅例子。 |
| Plays a short cue when an action finishes or fails, or when the connection drops and returns. Quiet during Windows quiet time, presentations and games. On by default. | 操作完成或者失敗，或者連線中斷同恢復嗰陣，播放一段短提示音。喺 Windows 安靜時間、簡報同打機期間會靜音。預設開啟。 |
| Resting the pointer on a toast holds it open and shows its action buttons. On by default. | 將滑鼠停喺通知上面就會留住佢，並顯示操作按鈕。預設開啟。 |
| Runs :mp4, :push and your own shortcuts as soon as they land on the clipboard. On by default. | :mp4、:push 同你自己嘅捷徑一去到剪貼簿就即刻執行。預設開啟。 |
| Saved clipboard images, audio and text go here. | 儲存咗嘅剪貼簿圖片、音訊同文字會放喺呢度。 |
| Short cues when an action finishes, fails, or the connection drops and returns. Quiet during Windows quiet time, presentations and games. | 操作完成、失敗，或者連線中斷同恢復時會有短短嘅提示音。Windows 安靜時間、簡報同遊戲期間唔會出聲。 |
| Shows a small card in the corner for every copy. On by default. | 每次複製都喺角落顯示一張細卡片。預設開啟。 |
| Shows headings, lists, code and links in the preview as formatted text instead of raw characters. On by default. | 預覽會將標題、清單、程式碼同連結以排版後嘅文字顯示，而唔係原始字元。預設開啟。 |
| Toasts | 通知 |
| What shows up in the corner when the clipboard changes. | 剪貼簿有變動時，喺角落彈出嘅通知。 |

## Page: conversion

| English | Cantonese |
| --- | --- |
| An existing file is never overwritten: the new one gets a (1), (2) suffix. | 現有檔案唔會被覆蓋，新檔案會加 (1)、(2) 後綴。 |
| Animated GIF | 動態 GIF |
| audio | 音訊 |
| Bitmap | 點陣圖 |
| Copy a file path, add :format on the end, and the converted file appears next to it with the same name. | 複製檔案路徑，尾加 :格式，轉好嘅檔案就會出現喺原檔旁邊，名稱相同。 |
| Formats | 格式 |
| Icon | 圖示 |
| Image | 圖片 |
| Install with winget | 用 winget 安裝 |
| Not found. Video and audio conversion needs it. | 搵唔到。影片同音訊轉檔需要佢。 |
| Not runnable yet: the path before the colon does not exist, or the word after it is not a format, :push or one of your shortcuts. | 暫時行唔到：冒號前面嘅路徑唔存在，或者後面嗰個字唔係格式、:push 或者你嘅捷徑。 |
| Pick ffmpeg.exe | 揀 ffmpeg.exe |
| Quick conversion | 快速轉檔 |
| Run it | 執行 |
| Try a command | 試吓指令 |
| Type what you would copy. It shows what will happen before anything runs. | 輸入你會複製嘅內容，執行之前會先顯示會發生咩事。 |
| Video | 影片 |

## Page: game

| English | Cantonese |
| --- | --- |
| Add game | 新增遊戲 |
| Choose several games to copy, export or remove together. Off by default. | 揀幾隻遊戲一齊複製、匯出或者移除。預設關閉。 |
| e.g. Valheim or javaw:Minecraft | 例如 Valheim 或者 javaw:Minecraft |
| Holds toasts and dialogs back while a listed game runs and shows them afterwards. Standing by. On by default. | 清單內嘅遊戲行緊嗰陣，通知同視窗會暫時收埋，之後先顯示。待命中。預設開啟。 |
| On by default. Toasts are held back while a listed game runs. An outage still going when you stop playing shows then. Quick commands still run. | 預設開啟。清單入面嘅遊戲行緊時會暫停彈出通知。如果你玩完之後仲斷緊線，嗰陣先會顯示。快捷指令照樣執行。 |
| Process names without .exe. Use process:Title to also match the window title (Minecraft Java runs as javaw). | 處理程序名稱唔使加 .exe。用 process:Title 可以同時配對視窗標題（Minecraft Java 係以 javaw 行）。 |
| Restore defaults | 還原預設 |
| Suppress popups while gaming | 玩遊戲時收埋通知 |
| Treats any fullscreen window (exclusive fullscreen games, fullscreen video, presentations) like a game, even if it is not on the list. On by default. | 任何全螢幕視窗（獨佔全螢幕遊戲、全螢幕影片、簡報）都當做遊戲，即使唔喺清單入面。預設開啟。 |
| Watched games | 監察嘅遊戲 |

## Page: help

| English | Cantonese |
| --- | --- |
| Actions for copied files, folders and Docker files. | 為複製咗嘅檔案、資料夾同 Docker 檔案而設嘅操作。 |
| Appearance editor, colour picker, info icons and guided forms | 外觀編輯器、顏色選擇器、資訊圖示同引導式表單 |
| Browse by topic | 按主題瀏覽 |
| Build a regex | 砌正規表示式 |
| Convert a video | 轉換影片 |
| Copy a file path, add :format on the end, and the converted file appears next to the original with the same name. | 複製檔案路徑，尾加 :格式，轉換好嘅檔案就會以同一個名出現喺原檔旁邊。 |
| Copy a repository folder with :push on the end and the app stages everything, commits and pushes the current branch. | 複製儲存庫資料夾，尾加 :push，程式就會暫存所有改動、提交並推送目前分支。 |
| Create and run Java edition servers on this computer, in Docker, or on another machine over SSH. | 喺呢部電腦、Docker 或者經 SSH 喺另一部機器建立同運行 Java 版伺服器。 |
| Create and run Java edition servers. | 建立同運行 Java 版伺服器。 |
| Each one opens the article at the right step. | 每一個都會直接開到文章啱嘅步驟。 |
| Every article, grouped by what it is about. | 每篇文章，按內容分類。 |
| Every feature explained, offline. Pick a task, browse a topic, or search. | 每個功能都有說明，離線都睇得。揀一項任務、按主題瀏覽，或者搜尋。 |
| Every list and page in Monitoring Tools has its own search bar. Plain text is the default; switch a bar to regular expressions with . (Alt+R), match case with Aa (Alt+C), and open the regex builder anchored under that bar with Builder (Alt+B). Ctrl+F jumps to the current page's search. | Monitoring Tools 每個清單同頁面都有自己嘅搜尋列。預設用純文字；用 .（Alt+R）轉做正規表示式，用 Aa（Alt+C）分大細楷，用「砌 pattern」（Alt+B）喺搜尋列下面開啟正規表示式建構器。Ctrl+F 會跳去目前頁面嘅搜尋列。 |
| Everything that happens when you copy something: the toast that slides in, the actions it offers, and the history that remembers it. | 你複製嘢嗰陣發生嘅所有事：滑入嘅通知、佢提供嘅操作，同記住佢嘅紀錄。 |
| Files and Docker | 檔案同 Docker |
| Find something you copied | 搵返複製過嘅嘢 |
| Fix a broken connection | 修復斷咗嘅連線 |
| Help | 說明 |
| Help and docs | 說明同文件 |
| How Monitoring Tools runs, and how to move around inside it. | Monitoring Tools 點樣運作，以及點樣喺入面行嚟行去。 |
| Keep popups out of the way while you play. | 玩嘢嗰陣唔好有彈出視窗阻住。 |
| Keep popups quiet while gaming | 打機嗰陣收埋彈出通知 |
| Know when the connection drops, why it dropped, and fix it fast. | 知道幾時斷線、點解斷線，同埋快手修復。 |
| Make your own :name command. Each one is a batch or PowerShell script. | 自訂你嘅 :名稱 指令。每個都係一個批次檔或者 PowerShell 腳本。 |
| Make your own command | 自訂指令 |
| On by default. Popups are held back while a listed game runs, or while any app is fullscreen. | 預設開啟。清單內嘅遊戲行緊，或者任何程式全螢幕嗰陣，彈出通知都會暫時收埋。 |
| Opens: Convert media | 開啟：轉換媒體 |
| Opens: Custom shortcuts and scripts | 開啟：自訂捷徑同腳本 |
| Per-element appearance editing and the helpers around it. | 逐個元素編輯外觀，以及相關嘅輔助功能。 |
| Push a repo from the clipboard | 由剪貼簿推送儲存庫 |
| Read the project overview | 睇專案總覽 |
| required-features | 必備功能 |
| Run a Minecraft server | 運行 Minecraft 伺服器 |
| Search | 搜尋 |
| Tabs | 分頁 |
| The app itself | 程式本身 |
| The builder opens directly under the search bar it belongs to (Builder button or Alt+B) and edits that bar's pattern. The pattern box at its top and the search bar stay in step in both directions. | 建構器會直接喺所屬搜尋列下面開啟（撳「砌 pattern」按鈕或者 Alt+B），並編輯嗰個搜尋列嘅表達式。頂部嘅表達式框同搜尋列會雙向保持同步。 |
| The control centre keeps your last copies so you can find something you copied a while ago. | 控制中心會保留你最近嘅複製紀錄，等你搵返之前複製過嘅嘢。 |
| The control centre presents its pages as browser-style tabs instead of a fixed navigation drawer. | 控制中心用瀏覽器式分頁顯示各個頁面，而唔係固定嘅導覽側欄。 |
| The Language and voice page (Control centre, Settings) controls the language of the whole app, the tone of headline messages and the optional narrator. | 「語言同語音」頁面（控制中心，設定）控制成個程式嘅語言、標題訊息嘅語氣，同可選嘅朗讀。 |
| The Modes page (Settings, then Modes) holds five features. Each control on the page states what it does and its default. | 「模式」頁面（設定，再去模式）有五個功能。頁面上每個控制項都會講明作用同預設值。 |
| The Records page in the control centre keeps four things together: a log of everything the app has told you, a history of your settings, exports of the app's data, and the release notes. | 控制中心嘅「紀錄」頁面集合咗四樣嘢：程式通知過你嘅所有紀錄、設定歷史、程式資料匯出，同版本說明。 |
| The Tools page in the control centre holds three things that work on your files: a drawing board, publishing a folder to GitHub, and the external editor choice. Long jobs show their progress in a shared progress card. | 控制中心嘅「工具」頁面有三樣處理檔案嘅功能：畫板、將資料夾發佈到 GitHub，同外部編輯器選擇。耗時嘅工作會喺共用進度卡片顯示進度。 |
| Theme, colour, text size, density, motion, name and logo. | 主題、顏色、文字大小、密度、動態效果、名稱同標誌。 |
| These pages are documentation. They describe what the program does; nothing on this page runs any of it, and the screenshots inside the articles are from the real program. | 呢啲頁面係說明文件，只係描述程式做咩；呢頁入面冇任何嘢會真正執行，而文章入面嘅截圖都係來自真正嘅程式。 |
| Toy locks, the unlock ladder, a local recovery desk, a built-in authenticator and a change history. All of it lives on the Safety page and works offline. | 玩具鎖、解鎖階梯、本機救援櫃檯、內置驗證器同變更紀錄。全部都喺「安全」頁面，離線都用得。 |
| Turn a copied path into an action: convert media, push a repo, or run your own script. All of them start the same way: copy something, add :name on the end. | 將複製咗嘅路徑變成操作：轉換媒體、推送儲存庫，或者執行你自己嘅腳本。全部都係同一個開始方法：複製啲嘢，尾加 :名稱。 |
| What do you want to do? | 你想做啲咩？ |
| When the connection drops, an always-on-top dialog explains where it broke and stays until the connection returns or you dismiss it. | 斷線嗰陣，會有個置頂視窗解釋喺邊度出事，並且一直留喺度，直到連線恢復或者你自己關閉。 |

## Page: language

| English | Cantonese |
| --- | --- |
| App language | 程式語言 |
| Cantonese humour | 廣東話幽默程度 |
| Cantonese voice | 廣東話語音 |
| Choose the language of the whole app: every page, toast, dialog, tray menu and palette entry. Switching applies at once to every open window. | 揀成個程式用嘅語言：每一頁、通知、視窗、系統匣選單同面板項目。切換後即時套用到所有開啟中嘅視窗。 |
| Default: 1.0×. Slower is 0.5×, faster is 2.0×. | 預設：1.0×。最慢 0.5×，最快 2.0×。 |
| Default: 3. Used for Cantonese headlines, and for the Cantonese line in bilingual mode. | 預設：3。用於廣東話標題，同雙語模式嘅廣東話行。 |
| Default: 3. Used for English headlines, and for the English line in bilingual mode. | 預設：3。用於英文標題，同雙語模式嘅英文行。 |
| Default: automatic, the first installed Cantonese voice. | 預設：自動，使用第一個已安裝嘅廣東話語音。 |
| Default: automatic, the first installed English voice. | 預設：自動，使用第一個已安裝嘅英文語音。 |
| Default: English. Bilingual shows English first, then Cantonese: short labels sit on one line, sentences use two lines. | 預設：英文。雙語會先顯示英文，再顯示廣東話：短標籤同行顯示，句子分兩行。 |
| Emoji in dialogs | 視窗內嘅表情符號 |
| English humour | 英文幽默程度 |
| English voice | 英文語音 |
| Humour | 幽默程度 |
| Humour changes only the tone of headline messages such as the offline dialog, conversion results and snooze notices. The facts in them never change. Level 1 is plain and precise; level 5 is playful. | 幽默程度只會改變標題訊息嘅語氣，例如斷線視窗、轉檔結果同暫停通知，內容事實唔會變。1 級最簡明直接，5 級最好玩。 |
| Language | 語言 |
| Language and voice | 語言同語音 |
| Language, humour, emoji and the narrator. | 語言、幽默程度、表情符號同朗讀。 |
| Narrator | 朗讀 |
| No Cantonese voice is installed on this computer, so Cantonese is not spoken. Add one in the Windows speech settings. | 呢部電腦冇安裝廣東話語音，所以唔會讀出廣東話。請喺 Windows 語音設定新增。 |
| Open Windows speech settings | 開啟 Windows 語音設定 |
| Read toasts aloud | 朗讀通知 |
| Search voices | 搜尋語音 |
| Show emoji in toast and dialog titles. Turn it off to strip emoji from the start and end of titles. Default: on. | 喺通知同視窗標題顯示表情符號。關閉後會移除標題頭尾嘅表情符號。預設：開啟。 |
| Speaking rate | 朗讀速度 |
| Speaks a short sentence in the current language so you can hear the voice and speed. | 用目前語言讀一句短句，等你聽到語音同速度。 |
| Speaks toast titles with the Windows voice you pick below. Stays quiet in game mode and when snoozed. Default: off. | 用你下面揀嘅 Windows 語音朗讀通知標題。遊戲模式同暫停期間唔會出聲。預設：關閉。 |
| Stop speaking | 停止朗讀 |
| Stops the current speech and clears anything waiting. | 停止目前朗讀，並清除排緊隊嘅內容。 |
| Test voice | 測試語音 |
| Voices | 語音 |

## Page: minecraft

| English | Cantonese |
| --- | --- |
| A Java edition version id such as 1.21.4. Default: the newest release. | Java 版嘅版本編號，例如 1.21.4。預設：最新正式版。 |
| Alpha | Alpha 版 |
| Also pre-generate the End (default off) | 同時預先生成終界（預設關閉） |
| Also pre-generate the Nether (default off) | 同時預先生成地獄（預設關閉） |
| Amplified | 放大化 |
| Area centre chunk X | 區域中心區塊 X |
| Area centre chunk Z | 區域中心區塊 Z |
| Area radius in chunks | 區域半徑（區塊數） |
| Area to pre-generate (default: radius 50 chunks around chunk 0, 0) | 預先生成區域（預設：區塊 0, 0 周圍半徑 50 個區塊） |
| Beta | Beta 版 |
| Browse… | 瀏覽… |
| Build a world with the real server for a version: choose a seed, a world type and an area, then save it as a folder or a ZIP. Or write those choices into one of your local servers. | 用真正嘅伺服器為指定版本建立世界：揀種子、世界類型同區域，然後儲存成資料夾或者 ZIP。或者將呢啲選擇寫入你其中一個本機伺服器。 |
| Centre chunk X | 中心區塊 X |
| Centre chunk Z | 中心區塊 Z |
| Choose the destination folder. | 揀目的地資料夾。 |
| Chunk X of the square's centre. Default 0. A chunk is 16 blocks, so chunk 10 starts at block 160. | 正方形中心嘅區塊 X。預設 0。一個區塊係 16 格，所以區塊 10 由方塊 160 開始。 |
| Chunk Z of the square's centre. Default 0. | 正方形中心嘅區塊 Z。預設 0。 |
| Create and run Java edition servers on this computer, in Docker, or on another machine over SSH. Versions come straight from Mojang's official catalogue. | 喺呢部電腦、Docker 或者經 SSH 喺另一部機建立同執行 Java 版伺服器。版本直接來自 Mojang 官方目錄。 |
| Destination | 目的地 |
| Downloads this version's server jar once (checked against Mojang's SHA-1) and reads its default noise settings and dimension type files. When the jar holds none, its data generator is run once, without starting a server, and its output is cached. | 下載一次呢個版本嘅伺服器 jar（用 Mojang 提供嘅 SHA-1 核對），並讀取佢預設嘅噪聲設定同維度類型檔案。如果 jar 入面冇，就會運行一次佢嘅資料產生器（唔會啟動伺服器），並快取輸出。 |
| Downloads this version's server jar once (checked against Mojang's SHA-1) and runs its own data generator, without starting a server, to read its game rules, block list and command list. Needed for the game rule editor and for checking superflat blocks. | 下載一次呢個版本嘅伺服器 jar（用 Mojang 提供嘅 SHA-1 核對），並運行佢自己嘅資料產生器（唔會啟動伺服器），讀取遊戲規則、方塊清單同指令清單。遊戲規則編輯器同檢查超平坦方塊都需要。 |
| Drag on the grid to draw the square, or type its radius and centre. The grid shows chunk numbers and the 32-chunk region boundaries; it shows no terrain, because terrain cannot be known without running the game. | 喺格仔上拖曳去畫正方形，或者直接輸入半徑同中心。格仔會顯示區塊編號同每 32 個區塊嘅區域邊界；唔會顯示地形，因為唔運行遊戲就無法得知地形。 |
| Draw the square to pre-generate: drag across the grid. Right-drag or Shift-drag pans, the wheel zooms, arrow keys move the square, + and - resize it, Home returns to 0, 0. | 畫出要預先生成嘅正方形：喺格仔上拖曳。右鍵拖曳或者 Shift 拖曳可以平移，滾輪縮放，方向鍵移動正方形，+ 同 - 調整大小，Home 返回 0, 0。 |
| Fill the seed field with a random number you can reuse later. | 喺種子欄填入一個隨機數字，之後可以重用。 |
| Flat | 超平坦 |
| Forgets every rule you changed here. Nothing is written until you generate. | 忘記你喺呢度改過嘅所有規則。要等到生成先會寫入任何嘢。 |
| Game rules (only the ones you change are set) | 遊戲規則（只會設定你改過嗰啲） |
| Generate a stand-alone world; no existing server is touched | 生成獨立世界；唔會動到任何現有伺服器 |
| Generate structures | 生成結構 |
| Generate world | 生成世界 |
| Half the side of the square in chunks, 0 to 100. 50 gives 100 x 100 chunks. 0 skips pre-generation. | 正方形邊長嘅一半（以區塊計），0 至 100。50 即係 100 x 100 個區塊。0 就跳過預先生成。 |
| I accept the | 我接受 |
| Large biomes | 大型生態域 |
| Leave empty for a random seed chosen by the server. Any text or number works. | 留空就由伺服器隨機揀種子。任何文字或數字都得。 |
| Name and version | 名稱同版本 |
| New server | 新增伺服器 |
| New world only | 只限新世界 |
| No area | 冇區域 |
| No items | 冇項目 |
| No version chosen yet. | 未揀版本。 |
| Noise settings and dimension types (advanced, only the values you change are written) | 噪聲設定同維度類型（進階，只會寫入你改過嘅數值） |
| Normal | 普通 |
| Output location | 輸出位置 |
| Pick a name and a version from the official catalogue. Nothing is downloaded until the last step. | 喺官方目錄揀一個名稱同版本。要到最後一步先會下載任何嘢。 |
| Pre-generation area picker. Drag to draw a square of chunks; arrow keys move it, plus and minus resize it. | 預先生成區域選擇器。拖曳畫出區塊正方形；方向鍵移動，加減號調整大小。 |
| Press “Read this version's jar” above to list this version's game rules. | 撳上面嘅「讀取呢個版本嘅 jar」，就會列出呢個版本嘅遊戲規則。 |
| Radius (chunks) | 半徑（區塊） |
| Random seed | 隨機種子 |
| Read this version's jar | 讀取呢個版本嘅 jar |
| Read this version's worldgen files | 讀取呢個版本嘅世界生成檔案 |
| Reads the version's own default files from its server jar and lets you change single values, such as the sea level, the world height or the ambient light. A data pack file replaces a whole entry, so each file written is the jar's default with only your values different, and files you did not touch are not written. Whether the game accepts a combination is decided by the game; this tool checks value types and the documented height limits. | 由伺服器 jar 讀取該版本自己嘅預設檔案，等你更改單一數值，例如海平面、世界高度或者環境光。資料包檔案會取代成個項目，所以寫出嚟嘅每個檔案都係 jar 嘅預設值，只有你改過嘅數值唔同，你冇碰過嘅檔案就唔會寫出。遊戲接唔接受某個組合由遊戲決定；呢個工具只會檢查數值類型同有記載嘅高度限制。 |
| Releases | 正式版 |
| Required: the server that builds the world will not run without it. Off until you accept. | 必須：冇呢個，建立世界嘅伺服器就唔會運行。你接受之前保持關閉。 |
| Reset all rules to server defaults | 將所有規則重設為伺服器預設值 |
| Runs a private server on this computer to build the world, then saves it to the destination. | 喺呢部電腦運行一個私人伺服器去建立世界，然後儲存到目的地。 |
| Runs the same bounded forceload procedure in the End with execute in minecraft:the_end, when the jar supports it. | 如果 jar 支援，會用 execute in minecraft:the_end 喺終界執行同樣有範圍限制嘅強制載入程序。 |
| Runs the same bounded forceload procedure in the Nether with execute in minecraft:the_nether, when the jar supports it. | 如果 jar 支援，會用 execute in minecraft:the_nether 喺地獄執行同樣有範圍限制嘅強制載入程序。 |
| Runs the same bounded procedure in the End when the jar supports it. Off by default. | 如果 jar 支援，會喺終界執行同樣有範圍限制嘅程序。預設關閉。 |
| Runs the same bounded procedure in the Nether when the jar supports it. Off by default. | 如果 jar 支援，會喺地獄執行同樣有範圍限制嘅程序。預設關閉。 |
| Runs worldborder set after generation. Default off. | 生成之後執行 worldborder set。預設關閉。 |
| Save the world as (default folder) | 世界儲存為（預設資料夾） |
| Saves the seed, world type, structures and (for a local process server on Java 1.19 or newer) custom superflat layers into the chosen local server. No world is built. Game rules and extra dimensions apply only to a generated world. | 將種子、世界類型、結構，以及（Java 1.19 或以上嘅本機程序伺服器）自訂超平坦圖層儲存到所揀嘅本機伺服器。唔會建立世界。遊戲規則同額外維度只適用於生成出嚟嘅世界。 |
| Scrolls the grid back to the world origin. The chosen square does not change. | 將格仔捲返世界原點。已揀嘅正方形唔會改變。 |
| Search versions, for example 1.21 | 搜尋版本，例如 1.21 |
| Seed (empty = random) | 種子（留空 = 隨機） |
| Server name | 伺服器名稱 |
| Server name, also used as the message of the day | 伺服器名稱，亦用作每日訊息 |
| Set the world border to this size | 將世界邊界設為呢個大小 |
| Sets the radius to 0, so nothing is pre-generated. | 將半徑設為 0，所以唔會預先生成任何嘢。 |
| Single biome | 單一生態域 |
| Snapshots | 快照版 |
| so a private server may run once to build this world | 所以可能會運行一次私人伺服器去建立呢個世界 |
| Stops the build and deletes the temporary server folder. | 停止建立並刪除臨時伺服器資料夾。 |
| The jar's command list gives each rule's name and type but not its default, so a rule left on “Server default” is not touched. Rules are applied to a world this tool generates; they are not written to an existing server. | jar 嘅指令清單只提供每條規則嘅名稱同類型，冇提供預設值，所以停喺「伺服器預設」嘅規則唔會被改動。規則只會套用喺呢個工具生成嘅世界，唔會寫入現有伺服器。 |
| The kind of terrain generated. Single biome appears only for versions that support it. Default: Normal. | 生成嘅地形類型。「單一生態域」只會喺支援嘅版本出現。預設：普通。 |
| There is no preview. Terrain cannot be drawn without running the game's own generator, so what you get is the real output of the server for the chosen version. | 冇預覽。唔運行遊戲自己嘅生成器就畫唔到地形，所以你攞到嘅係所揀版本伺服器嘅真正輸出。 |
| Uses the same chunk radius in each dimension. Whether this jar supports it is confirmed when you read the jar or generate; if not, that dimension is skipped and the result says so. | 每個維度用同一個區塊半徑。呢個 jar 支唔支援，要等你讀取 jar 或者生成嗰陣先確認；如果唔支援，嗰個維度就會被跳過，結果會講明。 |
| Version | 版本 |
| View 0, 0 | 檢視 0, 0 |
| Villages, strongholds, temples and the rest. Default on. Written as generate-structures. | 村莊、要塞、神殿等等。預設開啟。會寫成 generate-structures。 |
| Where to put the new folder or ZIP. Existing files are never replaced; a numbered name is used instead. | 新資料夾或 ZIP 放喺邊度。現有檔案永遠唔會被取代，會改用有編號嘅名稱。 |
| Which server | 邊個伺服器 |
| World generator | 世界生成器 |
| World seed | 世界種子 |
| World type (default Normal) | 世界類型（預設普通） |
| World version | 世界版本 |
| Write the finished world as a plain folder, or pack it into one ZIP file you can share. Default: folder. | 將完成嘅世界寫成普通資料夾，或者打包成一個可以分享嘅 ZIP 檔案。預設：資料夾。 |
| Write to server settings | 寫入伺服器設定 |
| ZIP file | ZIP 檔案 |

## Page: modes

| English | Cantonese |
| --- | --- |
| A rule says: between two times on chosen days, set one value. Rules are checked every minute. When a rule ends, your saved choice comes back; a rule never rewrites it. None by default. | 規則嘅意思係：喺揀咗嘅日子，兩個時間之間套用一個數值。規則每分鐘檢查一次。規則完咗，你儲存嘅選擇就會返嚟，規則永遠唔會改寫佢。預設冇規則。 |
| A shared switch. While it is on: game mode stays on and cannot be switched off, sounds are muted, quick commands that push to git are paused, and the app shows English only, with Cantonese, bilingual, humour levels, personal wording and the dim sum surprise left out entirely. It is turned off with the PIN or the password you set. Starts off. | 一個共用開關。開咗之後：遊戲模式會一直開住而且關唔到、聲音靜音、會推送去 git 嘅快捷指令暫停，而程式只會顯示英文，廣東話、雙語、幽默程度、個人用語同點心驚喜全部唔出現。要用你設定嘅 PIN 或密碼先可以關。預設係關。 |
| Add a rule step by step | 逐步新增規則 |
| Add rule | 新增規則 |
| ADHD modes | 專注力輔助模式 |
| and | 同 |
| Apply now | 即刻套用 |
| Browse | 瀏覽 |
| Choose a way to turn the mode off: a PIN of 4 to 12 digits, a password of at least 8 characters, or both. Either one works later. They are stored only as salted PBKDF2-SHA256 hashes (200,000 rounds). | 請揀一個關閉模式嘅方法：4 至 12 位數字嘅 PIN、至少 8 個字元嘅密碼，或者兩個都設。之後用邊個都得。兩者只會以加鹽 PBKDF2-SHA256 雜湊（200,000 輪）儲存。 |
| Choose several rules to copy, export or remove together. Off by default. | 揀幾條規則一齊複製、匯出或者移除。預設關閉。 |
| Clear cached pictures | 清除已快取嘅圖片 |
| Dates are written yyyy-MM-dd and both ends are included. Leave both empty for a rule with no date limit, or fill only one for an open-ended range. The window still follows the times and days above. | 日期寫法係 yyyy-MM-dd，頭尾兩日都計。兩個都留空就係冇日期限制，只填一個就係冇盡頭嘅範圍。時段仍然跟隨上面嘅時間同日子。 |
| Days (none selected means every day) | 日子（冇揀即係每日） |
| Dim sum surprise | 點心驚喜 |
| Disable | 停用 |
| Each time the app starts there is a 10% chance it shows one dim sum dish for a few seconds, with its English and Cantonese names and a picture. It never takes focus, never delays startup, and skips first-run setup and quiet moments such as game mode. There is no switch for it by design. | 程式每次啟動都有 10% 機會顯示一款點心幾秒鐘，連埋英文同廣東話名稱同一張圖片。佢唔會搶焦點，唔會拖慢啟動，首次設定同遊戲模式呢啲安靜時刻都會跳過。刻意唔設開關。 |
| End date (optional) | 結束日期（可選） |
| End time | 結束時間 |
| End time, 24-hour HH:mm | 結束時間，24 小時制 HH:mm |
| Every day | 每日 |
| Five independent modes and a Focus spotlight, each off by default. Turn on any mix. They are aids for focus, not medical advice, and they never hide anything you cannot get back with one click. | 五個獨立模式加一個專注聚光燈，預設全部關閉。可以隨意組合。呢啲係幫手專注嘅輔助，並非醫療建議，亦唔會隱藏任何撳一下就攞得返嘅嘢。 |
| Follow a JSON file you choose. It is read again whenever it changes and applied on top of your saved settings, below any scheduled rule. Off by default. | 跟隨你揀嘅 JSON 檔案。檔案一有改動就會重新讀取，並套用喺你已儲存嘅設定之上、排程規則之下。預設關閉。 |
| Follow this file | 跟隨呢個檔案 |
| Following: no | 跟隨中：否 |
| Fri | 五 |
| Friday | 星期五 |
| From | 由 |
| Full path to a .json file | .json 檔案嘅完整路徑 |
| How often the plain reminder appears while Time awareness is on. Ignored when it is off. | 開咗「時間意識」嗰陣，簡單提醒幾耐出現一次。關閉就唔會理。 |
| Language and theme rules are stored and offered to the rest of the app as the scheduled choice. Sounds, toasts and game mode rules apply immediately. A rule never changes your saved choice. | 語言同主題規則會被儲存，並以排程選擇嘅形式提供畀程式其他部分。聲音、通知同遊戲模式規則會即時生效。規則永遠唔會改變你儲存嘅選擇。 |
| Language mode | 語言模式 |
| Mode name | 模式名稱 |
| Modes | 模式 |
| Mon | 一 |
| Monday | 星期一 |
| Move earlier | 移前 |
| Move later | 移後 |
| New password (8 or more characters) | 新密碼（8 個字元或以上） |
| New PIN | 新 PIN |
| No catalogue cached yet. | 未有已快取嘅目錄。 |
| No rules yet. | 未有規則。 |
| Not following a file. | 冇跟隨任何檔案。 |
| Off by default. Dims everything except the card under keyboard focus or the mouse, so one thing stands out. Also switched with Ctrl+Shift+Space from any window of this app. It never blocks clicks, and it moves without animation when motion is reduced. | 預設關閉。將鍵盤焦點或者滑鼠所在嘅卡片以外嘅嘢全部調暗，令一樣嘢突出。喺呢個程式任何視窗都可以用 Ctrl+Shift+Space 開關。佢唔會擋住點擊，減少動態效果時亦唔會有動畫。 |
| Off by default. Messages that are not urgent (a copied item, an address change) go quietly to History instead of popping up. Failures and lost connections still show. | 預設關閉。唔緊急嘅訊息（例如複製咗嘢、位址改變）會靜靜雞入去歷史，唔會彈出嚟。失敗同斷線仍然會顯示。 |
| Off by default. No fade or slide motion, and only warnings and failures make a sound. It adds to Windows' own animation setting and never overrides it. Windows animation is currently on. | 預設關閉。冇淡出或者滑動效果，只有警告同失敗先會發聲。呢個設定係疊加喺 Windows 自己嘅動畫設定上面，永遠唔會凌駕佢。Windows 動畫而家係開住。 |
| Off by default. Only one popup is on screen at once. The rest wait their turn and also appear in History. | 預設關閉。同一時間螢幕上只得一個彈出通知，其餘排隊等候，同時亦會出現喺歷史。 |
| Off by default. Running jobs show how long they have taken, and a plain reminder appears at the interval below. | 預設關閉。進行中嘅工作會顯示用咗幾耐，並且按下面嘅間隔彈出簡單提醒。 |
| Off by default. This window reopens on the page you were last using, so you can pick up where you stopped. | 預設關閉。呢個視窗會重新開啟喺你上次用嘅頁面，等你接住上次繼續。 |
| Only between dates (optional) | 只喺呢段日期之間（可選） |
| Open the record's folder | 開啟紀錄所在資料夾 |
| Optional first date, yyyy-MM-dd. Empty means no start limit. | 可選嘅開始日期，yyyy-MM-dd。留空即係冇開始限制。 |
| Optional last date, yyyy-MM-dd, included. Empty means no end limit. | 可選嘅最後日期，yyyy-MM-dd，包括當日。留空即係冇結束限制。 |
| Optional name for the rule | 規則嘅可選名稱 |
| Pictures and names come from the public catalogue Ding-Ding-Projects/dim-sum-photos on GitHub, fetched over HTTPS only when a dish is shown, then cached in this app's data folder. Nothing is bundled with the app. If the catalogue or a picture cannot be reached, nothing is shown. | 圖片同名稱來自 GitHub 上嘅公開目錄 Ding-Ding-Projects/dim-sum-photos，只會喺顯示點心嗰陣經 HTTPS 取得，然後快取喺呢個程式嘅資料夾。程式本身唔附帶任何圖片。如果連唔到目錄或者圖片，就乜都唔會顯示。 |
| Reminder interval (default 30 minutes, used only with Time awareness) | 提醒間隔（預設 30 分鐘，只喺開咗時間意識先用） |
| Rename | 重新命名 |
| Rename it to anything up to 60 characters. After a rename only the new name appears in this app. | 可以改成最多 60 個字元嘅任何名稱。改名之後，呢個程式只會顯示新名稱。 |
| Repeat password | 重複密碼 |
| Repeat PIN | 重複 PIN |
| Rule added. | 已新增規則。 |
| Rule name | 規則名稱 |
| Sat | 六 |
| Saturday | 星期六 |
| Scheduled settings | 排程設定 |
| School mode | 學校模式 |
| School mode, ADHD modes, the dim sum surprise and scheduled settings. Each control says what it does and what it starts as. | 學校模式、專注力輔助模式、點心驚喜同排程設定。每個控制項都會講明作用同預設值。 |
| Setting to change | 要更改嘅設定 |
| Settings file | 設定檔 |
| Settings file path | 設定檔路徑 |
| Show one now (preview) | 即刻顯示一款（預覽） |
| Sound effects: Off | 音效：關 |
| Start date (optional) | 開始日期（可選） |
| Start time | 開始時間 |
| Start time, 24-hour HH:mm | 開始時間，24 小時制 HH:mm |
| Status: off | 狀態：關 |
| Stop following | 停止跟隨 |
| Sun | 日 |
| Sunday | 星期日 |
| The file must be under 64 KB. Expected shape (keys you leave out change nothing; unknown keys are ignored; a wrong type rejects the whole file and keeps the last good values): | 檔案必須細過 64 KB。預期格式（冇寫嘅鍵唔會改變任何嘢；未知嘅鍵會被忽略；類型錯誤會拒絕整個檔案並保留上次正常嘅數值）： |
| The name every surface will show. Defaults to "School mode". | 所有畫面都會顯示嘅名稱。預設係「學校模式」。 |
| This is a user-experience lock, not a security boundary. There is no way to recover a forgotten PIN or password: close the app and delete the shared record below, which turns the mode off and forgets its name, PIN and password. Anyone with this computer can do the same. | 呢個只係使用體驗上嘅鎖，並非安全防線。唔記得 PIN 或密碼係無法找回嘅：請關閉程式，然後刪除下面嘅共用紀錄，模式就會關閉，並忘記佢嘅名稱、PIN 同密碼。任何用呢部電腦嘅人都可以咁做。 |
| This launch: Not drawn this launch (each launch has a 10% chance). | 今次啟動：今次冇抽中（每次啟動有 10% 機會）。 |
| Thu | 四 |
| Thursday | 星期四 |
| to | 至 |
| Tue | 二 |
| Turn on | 開啟 |
| Value | 數值 |
| Waiting for its time | 等緊嘅時間到 |
| Wed | 三 |
| Wednesday | 星期三 |
| What the setting is set to during the window. Your saved choice comes back when the rule ends. | 喺時段內，設定會變成咩。規則完咗，你儲存嘅選擇就會返嚟。 |
| Which setting this rule changes while its time window is open. | 時段開啟期間，呢條規則會更改邊個設定。 |
| While School mode is on, these modes stay as you set them. School mode only adds its own limits on top. | 學校模式開咗嗰陣，呢啲模式照舊保持你嘅設定。學校模式只係喺上面加多佢自己嘅限制。 |

## Page: network

| English | Cantonese |
| --- | --- |
| 1 failed check | 1 次檢查失敗 |
| 1 min | 1 分鐘 |
| 2 in a row | 連續 2 次 |
| 3 in a row | 連續 3 次 |
| 5 min | 5 分鐘 |
| Add a profile… | 新增設定檔… |
| Alert after | 連續失敗幾多次先警示 |
| Check now | 即刻檢查 |
| Checking shortly… | 即將檢查… |
| DNS profiles | DNS 設定檔 |
| History repository | 紀錄儲存庫 |
| How many failed checks in a row are needed before the dialog appears. A higher number avoids false alarms. Default: 2 in a row. | 要連續失敗幾多次檢查先會彈出視窗。數字越大，越少誤報。預設：連續 2 次。 |
| How often the connection is tested. A shorter time notices a drop sooner and makes slightly more small requests. Default: 3 s. | 幾耐測試一次連線。間隔越短，越快發現斷線，但會多發少少細請求。預設：3 秒。 |
| How often the public IP address is looked up. Default: 5 min. | 幾耐查詢一次公開 IP 位址。預設：5 分鐘。 |
| In use | 使用中 |
| Looks up your public IP address every few minutes and right after the network changes, and records every change. On by default. | 每隔幾分鐘同埋網絡一有變動就查詢你嘅公開 IP 位址，並記錄每次改變。預設開啟。 |
| Now using Google (8.8.8.8). Switching needs admin rights: one prompt, or none when the service runs elevated. | 而家用緊 Google（8.8.8.8）。切換需要管理員權限：會問一次，如果服務已經以提升權限運行就唔使問。 |
| Open folder | 開啟資料夾 |
| Optional git remote URL, for example https://github.com/you/ip-history.git | 可選嘅 git 遠端網址，例如 https://github.com/you/ip-history.git |
| Public IP | 公開 IP |
| Push now | 即刻推送 |
| Release & renew IP | 釋放並更新 IP |
| Remove | 移除 |
| Save remote | 儲存遠端 |
| Shows a dialog when the internet drops and keeps it up until the line returns or you dismiss it. On by default. | 上網中斷嗰陣彈出視窗，並一直顯示到連線恢復或者你自己關閉。預設開啟。 |
| Test now | 即刻測試 |
| The offline dialog stays on top until the line comes back or you dismiss it. | 斷線視窗會一直置頂，直到連線恢復或者你自己關閉。 |
| Use | 使用 |

## Page: ollama

| English | Cantonese |
| --- | --- |
| 1 hour | 1 小時 |
| 30 minutes | 30 分鐘 |
| 5 minutes | 5 分鐘 |
| A built-in list of well-known Ollama models, written on 2026-11-04. Sizes are typical downloads for the default 4-bit version, not measurements, and newer models exist. Use the online search for the current library. | 內置咗一份常見 Ollama 模型清單，寫於 2026-11-04。大小係預設 4 位元版本嘅一般下載量，並非實測，而且已經有更新嘅模型。想睇最新模型庫，請用網上搜尋。 |
| A capable small general model. | 細細隻但好夠用嘅通用模型。 |
| A local conversation with an installed model. Replies stream in as they are produced. Chats are stored only if you press Save chat or switch automatic saving on; they never leave this computer. | 同已安裝嘅模型喺本機傾偈。回覆會一邊生成一邊顯示。除非你撳「儲存對話」或者開咗自動儲存，否則對話唔會被儲存，亦永遠唔會離開部電腦。 |
| A long-standing 7B general model. | 老牌 7B 通用模型。 |
| A name for the profile you are about to save. Saving over an existing name replaces it. | 你準備儲存嘅設定檔名稱。用已有嘅名稱儲存就會取代舊嘅。 |
| A strong all-rounder for everyday chat. | 日常傾偈嘅全能好手。 |
| Add to cart | 加入購物車 |
| Attach file | 附加檔案 |
| Attach up to 6 files to the next message. Images (PNG, JPEG, GIF, WebP, BMP, up to 12 MB) go to vision models; text files up to 1024 KB are pasted into the message, cut at 24,000 characters. You can also drop files on this card. | 下一則訊息最多可以附加 6 個檔案。圖片（PNG、JPEG、GIF、WebP、BMP，最大 12 MB）會交畀視覺模型；最大 1024 KB 嘅文字檔會貼入訊息，超過 24,000 個字元就會截斷。你亦可以將檔案拖放到呢張卡片。 |
| Can read images as well as text. | 除咗文字，仲睇得圖。 |
| Chat | 對話 |
| Chat message | 對話訊息 |
| Choose an installed model to see what it can do. | 揀一個已安裝嘅模型，睇下佢做到啲咩。 |
| Compact reasoning-oriented model. | 精簡嘅推理型模型。 |
| Context (tokens) | 上下文（token 數） |
| Context length in tokens | 上下文長度（token 數） |
| Context window in tokens (128 to 1048576). Empty uses the server default. A larger window needs more memory. | 上下文視窗大小（token 數，128 至 1048576）。留空就用伺服器預設值。視窗越大，要用越多記憶體。 |
| Default keep-alive when a chat sets none (default 5 minutes) | 對話冇設定時嘅預設保持載入時間（預設 5 分鐘） |
| Delete profile | 刪除設定檔 |
| Deletes the saved profile whose name is in the box (asks first). | 刪除名稱喺輸入框入面嗰個已儲存設定檔（會先問你）。 |
| Detected from the installed program and the local server's version endpoint. Default address: 127.0.0.1:11434. | 由已安裝嘅程式同本機伺服器嘅版本端點偵測所得。預設位址：127.0.0.1:11434。 |
| Downloads each queued model in turn through the local server. A finished model leaves the cart; a failed one stays. | 經本機伺服器逐個下載排隊中嘅模型。下載完成嘅會離開購物車，失敗嘅會留低。 |
| Downloads the named model through the local server. | 經本機伺服器下載指定嘅模型。 |
| Embedding model for search, not chat. | 用嚟搜尋嘅嵌入模型，唔係用嚟傾偈。 |
| Empty fields use the model's own defaults. These apply to this chat only and are saved with it. | 留空嘅欄位會用模型自己嘅預設值。呢啲只適用於呢個對話，並會連同對話一齊儲存。 |
| Empty the cart | 清空購物車 |
| Ends the Ollama processes you own. Models stay on disk. | 結束你擁有嘅 Ollama 程序。模型會保留喺磁碟。 |
| Export as Markdown | 匯出成 Markdown |
| Fills the system prompt and parameters below. Be strict. | 填入下面嘅系統提示同參數。要嚴格。 |
| Find a free port | 搵一個空閒埠 |
| From the server's process list. A model unloads by itself after its keep-alive time. | 來自伺服器嘅程序清單。模型過咗保持載入時間就會自動卸載。 |
| Goes back to the dated built-in list. | 返去有日期嘅內置清單。 |
| Good multilingual and coding balance. | 多語言同寫程式之間平衡得好。 |
| How long a model stays loaded in memory after a reply when the chat does not set its own time. A shorter time frees memory sooner but the next reply starts slower. Default: 5 minutes. | 對話冇自己設定時間嗰陣，模型喺回覆之後保持載入記憶體幾耐。時間越短越快釋放記憶體，不過下一次回覆會開始得慢啲。預設：5 分鐘。 |
| How long the model stays loaded after this chat's reply, such as 5m, 1h or 0. Empty uses the default below. | 呢個對話回覆之後，模型保持載入幾耐，例如 5m、1h 或者 0。留空就用下面嘅預設值。 |
| i  Firewall note | i  防火牆提示 |
| i  Server log found | i  搵到伺服器紀錄檔 |
| Installed models | 已安裝嘅模型 |
| Installed, server not running | 已安裝，伺服器冇運行 |
| Instructions sent before the conversation. Empty sends none. A profile fills this in; you can still edit it for this chat. | 喺對話開始之前先送出嘅指示。留空就唔送。設定檔會填入呢度，你仍然可以為呢個對話修改。 |
| Keep-alive | 保持載入時間 |
| Keep-alive for this chat | 呢個對話嘅保持載入時間 |
| Large reasoning model. | 大型推理模型。 |
| Large; needs a very big GPU or lots of RAM. | 體積大，要好大嘅 GPU 或者好多 RAM。 |
| Large; wants a 24 GB GPU or plenty of RAM. | 體積大，要 24 GB GPU 或者好多 RAM。 |
| Loaded in memory | 已載入記憶體 |
| Local models | 本機模型 |
| Long context at a moderate size. | 體積適中但上下文夠長。 |
| Mid-size with a long context window. | 中型模型，上下文視窗長。 |
| Model | 模型 |
| Model catalogue | 模型目錄 |
| Model details | 模型詳情 |
| Model name with an optional tag, for example llama3.2:3b | 模型名稱，可以加標籤，例如 llama3.2:3b |
| Model to pull | 要下載嘅模型 |
| New chat | 新對話 |
| New profile, step by step | 逐步建立新設定檔 |
| No model selected yet. | 未揀模型。 |
| No saved chats yet. | 未有已儲存嘅對話。 |
| Not included yet | 暫時未包含 |
| Nothing is listening there, so a server can start. If a server keeps failing to start, read the log below. | 嗰度冇嘢喺度監聽，所以可以啟動伺服器。如果伺服器一直啟動唔到，請睇下面嘅紀錄檔。 |
| Noticeably stronger; wants about 12 GB of memory. | 明顯強啲，要約 12 GB 記憶體。 |
| Nucleus sampling, above 0 and at most 1. Empty uses the model's own default. | 核採樣，大過 0 同最大 1。留空就用模型自己嘅預設值。 |
| Official download page | 官方下載頁面 |
| Older code-focused model. | 較舊、專攻程式碼嘅模型。 |
| Ollama on this computer | 呢部電腦上嘅 Ollama |
| Ollama server port | Ollama 伺服器埠 |
| Open logs folder | 開啟紀錄檔資料夾 |
| Open models folder | 開啟模型資料夾 |
| Opens ollama.com/download in your browser, for when winget is not available. | 喺瀏覽器開啟 ollama.com/download，用喺冇 winget 嘅時候。 |
| Opens the folder that holds Ollama's server.log and app.log. | 開啟存放 Ollama 嘅 server.log 同 app.log 嘅資料夾。 |
| Opens the folder where downloaded models are stored. | 開啟存放已下載模型嘅資料夾。 |
| Port (default 11434) | 埠（預設 11434） |
| Port of the local Ollama server, 1024 to 65535. Default 11434. | 本機 Ollama 伺服器嘅埠，1024 至 65535。預設 11434。 |
| Press Start server above. If it exits straight away, read the log below. | 請撳上面嘅「啟動伺服器」。如果佢一啟動就退出，請睇下面嘅紀錄檔。 |
| Profile (system prompt preset) | 設定檔（系統提示預設） |
| Profile name | 設定檔名稱 |
| Pull | 下載映像 |
| Pull a model | 下載模型 |
| Pull all | 全部下載 |
| Pull cart | 下載購物車 |
| Queue several models, see the combined download size against your free disk, then pull them one after another through the local server. | 排幾個模型，睇下總下載量同你剩餘磁碟空間比較，然後經本機伺服器逐個下載。 |
| Randomness, 0 to 2. Empty uses the model's own default. | 隨機程度，0 至 2。留空就用模型自己嘅預設值。 |
| Re-read hardware | 重新讀取硬件 |
| Reads memory, graphics adapters and free disk again, for example after closing programs. | 重新讀取記憶體、顯示卡同剩餘磁碟空間，例如關咗啲程式之後。 |
| Reads the server state again. | 重新讀取伺服器狀態。 |
| Reasoning model that shows its working. | 會顯示思考過程嘅推理模型。 |
| Reasoning-oriented mid-size model. | 中型推理模型。 |
| Refresh | 重新整理 |
| Removes every queued name. Nothing on disk changes. | 移除所有排隊中嘅名稱。磁碟上嘅嘢唔會有任何改變。 |
| Requests from this tool go to 127.0.0.1, which Windows Firewall does not filter. A firewall is only relevant if you made Ollama listen on the network. | 呢個工具嘅請求都係去 127.0.0.1，Windows 防火牆唔會過濾。除非你將 Ollama 設定成喺網絡上監聽，否則防火牆同呢度無關。 |
| Run checks | 執行檢查 |
| Run language models on this computer with Ollama. Everything here talks only to 127.0.0.1; nothing is sent to a cloud service. | 用 Ollama 喺呢部電腦運行語言模型。呢度所有嘢都只同 127.0.0.1 溝通，唔會傳送去任何雲端服務。 |
| Runs every local check again. | 重新執行所有本機檢查。 |
| Save as profile | 儲存為設定檔 |
| Save chat | 儲存對話 |
| Save chats automatically | 自動儲存對話 |
| saved chats | 已儲存嘅對話 |
| Send | 傳送 |
| Sends the message and any attached files to the chosen local model. | 將訊息同附加嘅檔案送畀所揀嘅本機模型。 |
| Sends your search words to ollama.com/search and reads the result page. This is the only action here that contacts the Ollama website. Default: not used unless you press it. | 將你嘅搜尋字詞送去 ollama.com/search 並讀取結果頁面。呢個係呢度唯一會連絡 Ollama 網站嘅操作。預設：除非你撳，否則唔會用。 |
| Server not running. | 伺服器冇運行。 |
| Show log | 顯示紀錄檔 |
| Show the built-in list | 顯示內置清單 |
| Shows the last lines of server.log, where start-up errors are recorded. | 顯示 server.log 最後幾行，啟動錯誤會記錄喺度。 |
| Size and family come from the server's tag list. Delete asks twice and cannot be undone. | 大小同系列來自伺服器嘅標籤清單。刪除會問兩次，而且無法還原。 |
| Small and fast. | 細而快。 |
| Small and quick; good for trying things out. | 細而快，啱晒試玩。 |
| Small multilingual model. | 細型多語言模型。 |
| Small step-by-step reasoning model. | 細型逐步推理模型。 |
| Small with a long context window. | 細型但上下文視窗長。 |
| Start server | 啟動伺服器 |
| Starts 'ollama serve' hidden and bound to 127.0.0.1 only. | 隱藏啟動 'ollama serve'，只綁定喺 127.0.0.1。 |
| Starts 'ollama serve' hidden on 127.0.0.1, then re-checks. | 喺 127.0.0.1 隱藏啟動 'ollama serve'，然後再檢查一次。 |
| Starts an empty conversation. The current one is kept only if you saved it. | 開始一個空白對話。目前嗰個只有儲存過先會保留。 |
| Stated plainly so nothing here is mistaken for more than it is. | 講清楚啲，等大家唔會高估呢度嘅功能。 |
| Stop | 停止 |
| Stop server | 停止伺服器 |
| Stops after the current download is interrupted. Layers already fetched are kept. | 目前下載中斷之後停止。已經攞到嘅層會保留。 |
| Stops the download. Layers already fetched are kept. | 停止下載。已經攞到嘅層會保留。 |
| Stops the reply that is streaming. | 停止正在串流嘅回覆。 |
| Stores the system prompt, temperature, top-p and context length shown here under the name in the box. The same name replaces the old profile. | 將呢度顯示嘅系統提示、溫度、top-p 同上下文長度，用輸入框入面嘅名稱儲存。同名會取代舊設定檔。 |
| Stores this conversation on this computer so you can open, export or delete it later. | 將呢個對話儲存喺本機，等你之後可以開啟、匯出或者刪除。 |
| Strict reviewer | 嚴格嘅審閱者 |
| Strong for its size. | 以體積嚟講好能打。 |
| Suggests an unused port above 11434 for when the default is taken. It does not change any setting. | 預設埠被佔用嗰陣，建議一個 11434 以上冇人用嘅埠。唔會更改任何設定。 |
| System prompt | 系統提示 |
| Temperature | 溫度 |
| The built-in catalogue is a dated snapshot, not the full library, and its sizes are typical figures. Ollama publishes no documented catalogue API, so the online search reads the public search page and can break if that page changes. Hardware verdicts are a rule of thumb. Chat attachments are limited to images and text files, saved chats keep no image bytes, and profiles hold a system prompt and parameters only (no tool-calling definitions). There is no launching of other local tools from here. See docs/features/ollama for the full list. | 內置目錄只係有日期嘅快照，並非完整模型庫，大小亦只係一般數字。Ollama 冇公開記載嘅目錄 API，所以網上搜尋係讀取公開搜尋頁面，頁面一改就可能失效。硬件評語只係經驗法則。對話附件只限圖片同文字檔，已儲存嘅對話唔會保留圖片資料，而設定檔只包含系統提示同參數（冇工具呼叫定義）。亦唔會喺呢度啟動其他本機工具。完整清單請睇 docs/features/ollama。 |
| The cart is empty. Use Add to cart in the catalogue above. | 購物車係空嘅。請喺上面嘅目錄用「加入購物車」。 |
| The server is not running, so the model list cannot be read. Start it above. | 伺服器冇運行，所以讀唔到模型清單。請喺上面啟動佢。 |
| The server is not running. | 伺服器冇運行。 |
| Tiny; for very constrained machines. | 極細，適合資源好緊絀嘅電腦。 |
| Top-p | Top-p 取樣 |
| Troubleshooter | 疑難排解 |
| Tuned for writing and reading code. | 專為寫同讀程式碼而調校。 |
| Type a message and press Enter or Send | 輸入訊息，然後撳 Enter 或者「傳送」 |
| Type a model name and tag, for example llama3.2 or qwen2.5:7b. The local server downloads it and reports progress here. | 輸入模型名稱同標籤，例如 llama3.2 或者 qwen2.5:7b。本機伺服器會下載，進度喺呢度顯示。 |
| Unload at once | 即刻卸載 |
| Very small; short context. | 非常細，上下文短。 |
| What the server reports for an installed model: parameters, template, context length, capabilities and licence. Press Details on an installed model. | 伺服器為已安裝模型回報嘅資料：參數、範本、上下文長度、功能同授權。喺已安裝嘅模型撳「詳細資料」。 |
| Works offline from local facts: whether Ollama is installed and answering, whether the port is taken and by what, the models folder and the log. Nothing is sent anywhere. | 憑本機資料離線運作：Ollama 有冇安裝同有冇回應、埠有冇被佔用同被邊個佔用、模型資料夾同紀錄檔。唔會傳送任何嘢去任何地方。 |
| Writes the chat to disk after every reply. Default off, so nothing is stored unless you press Save chat. | 每次回覆之後都將對話寫入磁碟。預設關閉，所以除非你撳「儲存對話」，否則咩都唔會儲存。 |
| Writes this conversation to a .md file you choose. | 將呢個對話寫入你揀嘅 .md 檔案。 |
| ✓  Models folder exists | ✓  模型資料夾存在 |
| ✓  Ollama is installed | ✓  Ollama 已安裝 |
| ✗  The server is not running | ✗  伺服器冇運行 |

## Page: overview

| English | Cantonese |
| --- | --- |
| Back online | 已恢復連線 |
| Clipboard today | 今日複製 |
| commit and push the repo | 提交並推送儲存庫 |
| Connection lost | 連線中斷 |
| Copies today | 今日複製次數 |
| Copy a path with a command on the end. The command runs the moment it lands on the clipboard. | 複製路徑時喺尾加個指令，指令一去到剪貼簿就即刻行。 |
| Copy again | 再複製一次 |
| DNS avg | DNS 平均 |
| DNS lookups failing | DNS 查詢失敗 |
| Every probe of the last few minutes. Gaps are failed checks. | 最近幾分鐘每一次探測，有缺口即係探測失敗。 |
| Everything is quiet | 一切平靜 |
| extract or convert audio | 抽取或轉換音訊 |
| Gateway avg | 閘道平均 |
| image or first frame to PNG | 圖片或第一格轉 PNG |
| image to WebP | 圖片轉 WebP |
| Latest activity | 最新活動 |
| Live | 即時 |
| Maximize | 最大化 |
| Minimize | 最小化 |
| Outages | 斷線次數 |
| Probes | 探測次數 |
| Recently copied | 最近複製 |
| Router is not responding | 路由器冇回應 |
| Standing by | 待命中 |
| Uptime | 連線時間 |
| video or audio to MP4 | 影片或音訊轉 MP4 |
| video to animated GIF | 影片轉動態 GIF |

## Page: records

| English | Cantonese |
| --- | --- |
| Also include clipboard history | 同時包括剪貼簿紀錄 |
| Changelog | 更新日誌 |
| Choose a file and write the selected data in the selected format | 揀一個檔案，將所選資料按所選格式寫入 |
| Choose several notifications to copy, export or remove together. Off by default. | 揀幾個通知一齊複製、匯出或者移除。預設關閉。 |
| Choose several snapshots to copy, export or remove together. Off by default. | 揀幾個快照一齊複製、匯出或者移除。預設關閉。 |
| Choose where to save one zip holding every dataset in every format | 揀一個位置，儲存包含所有資料集、所有格式嘅 ZIP |
| Clear all | 全部清除 |
| Each save that changes a setting keeps a snapshot, up to 200. Restore puts a snapshot back after you confirm. | 每次儲存如果改變咗設定，都會留一個快照，最多 200 個。確認之後，「還原」就會將快照放返。 |
| Every toast, notice, offline alert and IP change since the app started, newest first. Kept in memory only, up to 500. | 程式啟動之後每個通知、提示、斷線警示同 IP 變更，最新排最前。只保留喺記憶體，最多 500 項。 |
| Export everything | 全部匯出 |
| Export everything... | 全部匯出... |
| Exports | 匯出 |
| Fetch the release notes from GitHub again | 重新由 GitHub 取得版本說明 |
| Fetching release notes… | 正在取得版本說明… |
| Format | 格式 |
| Included: Activity, Settings, Notifications, Settings history, Shortcuts, Regex library, Appearance and looks, Schedule rules, Lock records (no secrets), DNS profiles. | 包括：活動紀錄、設定、通知、設定歷史、捷徑、正規表示式資料庫、外觀同造型、排程規則、鎖紀錄（不含機密）、DNS 設定檔。 |
| IP changed | IP 已更改 |
| Notice | 提示 |
| Notification centre | 通知中心 |
| Notifications | 通知 |
| Notifications, settings history, exports and the changelog. | 通知、設定歷史、匯出同更新日誌。 |
| Off by default. Clipboard history is plain text and may hold private copies, so it is left out unless you choose it. | 預設關閉。剪貼簿紀錄係純文字，可能有私人內容，所以除非你揀，否則唔會包括。 |
| Offline | 離線 |
| One zip with every dataset below in JSON, CSV, Markdown, HTML and plain text. Lock records list what each lock covers but never a PIN, a password or an authenticator secret. | 一個 ZIP，包含下面每個資料集嘅 JSON、CSV、Markdown、HTML 同純文字版本。鎖紀錄會列出每個鎖涵蓋咩，但永遠唔會有 PIN、密碼或者驗證器密鑰。 |
| Personal wording applied | 個人用語已套用 |
| Pick what to export and how. Clipboard history exports text entries only, never images, files or audio. | 揀要匯出咩同點樣匯出。剪貼簿紀錄只會匯出文字項目，唔會匯出圖片、檔案或者音訊。 |
| Plain text | 純文字 |
| Put the selected data on the clipboard as text in the selected format | 將所選資料按所選格式以文字放上剪貼簿 |
| Records | 紀錄 |
| Records and exports | 紀錄同匯出 |
| Remove every entry from the notification centre, after you confirm | 確認之後，移除通知中心所有項目 |
| Replace the current settings with this snapshot, after you confirm | 確認之後，用呢個快照取代目前設定 |
| Restore | 還原 |
| Save to file… | 儲存到檔案… |
| Settings history | 設定歷史 |
| Show every kind of notification | 顯示所有類型嘅通知 |
| Show what this save changed compared with the snapshot before it | 顯示今次儲存同上一個快照比較改變咗咩 |
| starting | 開始緊 |
| Toast | 通知 |
| View diff | 睇差異 |
| What to export | 要匯出咩 |

## Page: safety

| English | Cantonese |
| --- | --- |
| 15 minutes | 15 分鐘 |
| A local recovery desk for when a lock or an authenticator gets you stuck. | 喺本機嘅救援櫃檯，鎖或者驗證器攪到你寸步難行嗰陣用。 |
| A secrets export is not offered. Any export here leaves secrets out and says so. | 唔提供包含機密嘅匯出。呢度所有匯出都會剔走機密，並且會講明。 |
| A small peek at the code that follows, so you do not type one with two seconds left. Default: on. | 預覽下一個驗證碼，等你唔使喺得返兩秒嗰陣先輸入。預設：開。 |
| Account | 帳戶 |
| Account, for example your email | 帳戶，例如你嘅電郵 |
| Add account status | 新增帳戶狀態 |
| Add an account | 新增帳戶 |
| Adds "Unlock with Windows Hello" to lock prompts and the unlock ladder, and "Show as QR (Windows Hello)" to authenticator accounts, when Windows Hello is set up on this computer. It is an extra route and never replaces a lock's PIN or password. Default: on. | 當呢部電腦設定咗 Windows Hello，就會喺鎖嘅提示同解鎖階梯加入「用 Windows Hello 解鎖」，並喺驗證器帳戶加入「以 QR 顯示（Windows Hello）」。呢個只係額外嘅途徑，永遠唔會取代鎖嘅 PIN 或密碼。預設：開。 |
| Algorithm for a bare secret | 單獨密鑰嘅演算法 |
| Authenticator | 驗證器 |
| Change history | 變更紀錄 |
| Codes come from this computer's clock. This app is offline, so it cannot tell if the clock is wrong. If a site rejects a fresh code, sync the clock in Windows Settings, Time and language. | 驗證碼係根據呢部電腦嘅時鐘計算。呢個程式離線運作，無法知道時鐘啱唔啱。如果網站拒絕剛生成嘅驗證碼，請喺 Windows 設定嘅「時間和語言」同步時鐘。 |
| Copy folder path | 複製資料夾路徑 |
| Critical (will be treated as Normal) | 嚴重（會當做普通處理） |
| Decodes a QR code from a picture file, here on this computer, and fills the box above with its otpauth link. Nothing is sent anywhere. | 喺呢部電腦解讀圖片檔入面嘅 QR 碼，並將佢嘅 otpauth 連結填入上面嘅輸入框。唔會傳送任何嘢去任何地方。 |
| Decodes a QR code from a picture on the clipboard, here on this computer, and fills the box above with its otpauth link. Nothing is sent anywhere. | 喺呢部電腦解讀剪貼簿圖片入面嘅 QR 碼，並將佢嘅 otpauth 連結填入上面嘅輸入框。唔會傳送任何嘢去任何地方。 |
| Delete all tickets | 刪除所有單據 |
| Describe what happened | 描述發生咗咩事 |
| Describe what happened first, in a few words. | 請先用幾個字描述發生咗咩事。 |
| Describe what you were locked out of | 描述你被鎖喺外面嘅係咩 |
| Digits | 位數 |
| Element locks | 元素鎖 |
| Export list without secrets | 匯出清單（不含機密） |
| Export tickets | 匯出單據 |
| File a ticket | 提交單據 |
| Forgot a lock PIN or password | 唔記得鎖嘅 PIN 或密碼 |
| Fun locks for elements, a local authenticator, and a make-believe support desk that tells you how to get unstuck. Everything stays on this computer. | 好玩嘅元素鎖、本機驗證器，同一個扮晒嘢嘅支援櫃檯，教你點樣脫困。所有嘢都留喺呢部電腦。 |
| Hide locked elements from page search | 喺頁面搜尋隱藏已鎖元素 |
| High | 高 |
| Issuer | 簽發者 |
| Issuer, for example the website | 簽發者，例如網站 |
| Lock created | 已建立鎖 |
| Lock opened | 已開鎖 |
| Lock removed | 已移除鎖 |
| Locked out by too many wrong tries | 輸錯太多次而被鎖住 |
| Locks, authenticator and recovery | 鎖、驗證器同救援 |
| Lost my authenticator | 遺失咗驗證器 |
| New locks stay unlocked for (default 5 minutes) | 新鎖解鎖後保持開啟嘅時間（預設 5 分鐘） |
| No accounts yet. Add one above. | 未有帳戶。請喺上面新增一個。 |
| No tickets. Good for you. | 冇單據，好嘢。 |
| Nothing is sent anywhere. These tickets exist only on this computer, no network request is made, no data is collected, and nobody is reading them. | 唔會傳送任何嘢。呢啲單據只存在於呢部電腦，唔會發出網絡請求，唔會收集資料，亦冇人會睇。 |
| Off by default, so locked items are shown and marked as locked instead of quietly disappearing. | 預設關閉，所以已鎖項目會照常顯示並標明已鎖，而唔係靜靜雞消失。 |
| Offer Windows Hello | 提供 Windows Hello |
| Opens the unlock prompt for this lock | 開啟呢個鎖嘅解鎖提示 |
| otpauth link or base32 secret | otpauth 連結或者 base32 密鑰 |
| otpauth:// link or base32 secret | otpauth:// 連結或者 base32 密鑰 |
| Paste an otpauth:// link, or just a base32 secret. A link brings its issuer, algorithm, digits and period with it. Default for a bare secret: SHA-1, 6 digits, 30 seconds. | 貼上 otpauth:// 連結，或者單獨一個 base32 密鑰。連結會連同簽發者、演算法、位數同週期一齊帶入。單獨密鑰嘅預設：SHA-1、6 位、30 秒。 |
| Paste from clipboard | 由剪貼簿貼上 |
| Period | 週期 |
| Read a QR from a file… | 由檔案讀取 QR 碼… |
| Read a QR from the clipboard | 由剪貼簿讀取 QR 碼 |
| Right-click any button, setting or card and choose Lock this element. A locked element is dimmed and cannot be used until you unlock it here or from its menu. | 喺任何按鈕、設定或者卡片撳右鍵，揀「鎖住呢個元素」。已鎖嘅元素會變暗，要喺呢度或者佢嘅選單解鎖先用得返。 |
| SAFETY | 安全 |
| Self-check: 9 of 9 published RFC 6238 vectors match. | 自我檢查：9 個已公布嘅 RFC 6238 測試向量全部吻合。 |
| Severity (nobody will honour it) | 嚴重程度（反正冇人會理） |
| Show the next code too | 同時顯示下一個驗證碼 |
| Something else is locked | 有其他嘢被鎖 |
| Support desk notice | 支援櫃檯通知 |
| Support Tickets | 支援單據 |
| This lock is just for fun. It is not security and does not protect or encrypt anything. | 呢個鎖只係玩下。唔算安全措施，亦唔會保護或者加密任何嘢。 |
| Time-based one-time codes (RFC 6238) for any account, calculated here. Secrets are kept in a Windows-protected file and never in settings, logs or history. | 為任何帳戶喺本機計算基於時間嘅一次性驗證碼（RFC 6238）。密鑰存放喺受 Windows 保護嘅檔案，永遠唔會出現喺設定、紀錄或者歷史入面。 |
| Unlock | 解鎖 |
| Until the app closes | 直到程式關閉 |
| What changed in locks, authenticator accounts, tickets and the display name, and when. It records actions and labels only. No secret, PIN, password or code is ever written here. | 鎖、驗證器帳戶、單據同顯示名稱幾時改過咩。只記錄操作同標籤，永遠唔會寫入任何密鑰、PIN、密碼或者驗證碼。 |
| What happened | 發生咗咩事 |
| Writes issuers, accounts and settings. Secrets are always left out, and the file says so. | 寫出簽發者、帳戶同設定。密鑰一律剔走，而且檔案內會講明。 |

## Page: service

| English | Cantonese |
| --- | --- |
| Apply a copied wording file | 複製咗用字檔就即刻套用 |
| Background service | 背景服務 |
| Choose file… | 揀檔案… |
| Copy the path of a valid wording file (or the file itself in Explorer) and it is applied at once, like a drop. Other JSON files are left alone. Default: on. | 複製有效用字檔嘅路徑（或者喺檔案總管複製檔案本身），就會即時套用，同拖放一樣。其他 JSON 檔案唔會受影響。預設：開。 |
| No file loaded. The app shows its original wording. | 未載入檔案，程式顯示原本用語。 |
| Off by default. Turning it on asks once, then the service starts elevated at every sign-in until you turn it off here. Lets DNS switching and release and renew run without prompts. | 預設關閉。開啟時會問一次，之後每次登入服務都會以提升權限啟動，直到你喺度關閉。令轉 DNS 同釋放更新唔使每次都有提示。 |
| Open settings file | 開啟設定檔 |
| Optional. A local JSON file that swaps ordinary phrases in on-screen text for your own words. Drop it on any window, or choose it here. It never leaves this computer; paths, commands and code are never changed. | 可選。一個本機 JSON 檔案，可以將畫面上嘅一般用語換成你自己嘅講法。拖去任何視窗，或者喺度揀檔案。檔案絕對唔會離開呢部電腦，路徑、指令同程式碼唔會改。 |
| Runs at sign-in under a small supervisor that restarts it if it ever crashes. | 登入時自動啟動，由一個小型監督程式看管，萬一當機會自動重新啟動。 |
| Starts the background service at sign-in so the watchers run without opening the app. On by default. | 登入時啟動背景服務，令監察功能唔使開程式都運作。預設開啟。 |
| Stops the service, removes it from start-up and deletes the program. | 停止服務、由開機啟動移除，並刪除程式。 |
| Uninstall | 解除安裝 |

## Page: shortcuts

| English | Cantonese |
| --- | --- |
| :push message | :push 提交訊息 |
| Batch (.bat) | 批次檔 (.bat) |
| Built in. Stages everything, commits with this message and pushes the current branch. | 內置功能。會暫存所有改動，用呢個訊息提交，再推送目前分支。 |
| Choose several shortcuts to copy, export or remove together. Off by default. | 揀幾個捷徑一齊複製、匯出或者移除。預設關閉。 |
| Copy to clipboard | 複製到剪貼簿 |
| DESCRIPTION | 說明 |
| Edit | 編輯 |
| folder of input | 輸入嘅資料夾 |
| input path | 輸入路徑 |
| INSERT | 插入 |
| Lets the console window of the script appear while it runs. Off runs it hidden. Off by default. | 令腳本執行期間顯示主控台視窗。關閉就會隱藏執行。預設關閉。 |
| Make your own :commands. Copy any path or text with :name on the end and your batch or PowerShell script runs with it. | 自訂你嘅 :指令。複製任何路徑或文字，尾加 :名稱，就會用佢行你嘅批次檔或 PowerShell 腳本。 |
| NAME | 名稱 |
| name only | 只有名稱 |
| Name: letters, digits, - and _ | 名稱：字母、數字、- 同 _ |
| New shortcut | 新增捷徑 |
| Open scripts folder | 開啟腳本資料夾 |
| PowerShell | PowerShell |
| Run as administrator | 以系統管理員身分執行 |
| Runs the script with administrator rights, which asks for consent once per run unless the service already has them. Off by default. | 以管理員權限執行腳本，每次執行都會問一次同意，除非服務本身已經有權限。預設關閉。 |
| Runs with | 執行方式 |
| Save and run | 儲存並執行 |
| Show a console window | 顯示主控台視窗 |
| Test input (a path or any text) | 測試輸入（路徑或任何文字） |
| today | 今日 |
| What it does (shows as the chip tooltip) | 功能說明（會顯示做提示） |
| Whether the script is a PowerShell script (.ps1) or a batch file (.bat). Default: PowerShell. | 腳本係 PowerShell 腳本 (.ps1) 定係批次檔 (.bat)。預設：PowerShell。 |
| zip next to it | 旁邊嘅 zip |

## Page: tools

| English | Cantonese |
| --- | --- |
| accounts | 帳戶 |
| Add account | 新增帳戶 |
| Annotate pictures, publish a folder to GitHub and choose which editor opens your files. Each control says what it does and what it starts as. | 為圖片加註解、將資料夾發佈到 GitHub，以及揀邊個編輯器開啟你嘅檔案。每個控制項都會講明作用同預設值。 |
| Annotate the newest copied image | 為最新複製嘅圖片加註解 |
| Bottom | 下 |
| Checking signed-in accounts… | 正在檢查已登入嘅帳戶… |
| Choose the editor that opens your shortcut scripts and the files in clipboard history. Visual Studio Code opens a folder as a workspace, so its file tree is usable. | 揀用嚟開啟捷徑腳本同剪貼簿紀錄內檔案嘅編輯器。Visual Studio Code 會將資料夾當做工作區開啟，所以可以用佢嘅檔案樹。 |
| Commits the folder with Git, then uploads it. A new repository is only created after you agree to it. This app never sees or stores a password or token: sign-in lives in the GitHub CLI's own credential store. | 用 Git 提交資料夾，然後上載。要你同意先會建立新儲存庫。呢個程式永遠睇唔到或者儲存密碼同權杖：登入資料由 GitHub CLI 自己嘅憑證庫保管。 |
| Create a new repository on GitHub | 喺 GitHub 建立新儲存庫 |
| Custom editor program | 自訂編輯器程式 |
| Custom program | 自訂程式 |
| Draw on a picture or a blank board. Seven pens, a partial eraser, line, arrow, rectangle, ellipse, text, select and move, undo and redo. Nothing you draw leaves this computer. | 喺圖片或者空白畫板上面畫畫。有七款筆、局部擦膠、直線、箭嘴、長方形、橢圓、文字、選取同移動，同復原／重做。你畫嘅嘢永遠唔會離開部電腦。 |
| Draw, publish and edit | 繪畫、發佈同編輯 |
| Drawing and annotation | 繪畫同註解 |
| Existing repository address | 現有儲存庫位址 |
| External editor | 外部編輯器 |
| Folder to publish | 要發佈嘅資料夾 |
| Folders open as a workspace root in Visual Studio Code. If no editor can be found or started, the message says so instead of failing silently. | 資料夾會喺 Visual Studio Code 以工作區根目錄開啟。如果搵唔到或者啟動唔到編輯器，會有訊息講明，而唔係靜靜雞失敗。 |
| For example https://github.com/name/repository.git. Used only when you are not creating a new repository. | 例如 https://github.com/name/repository.git。只喺你唔係建立新儲存庫嗰陣先用。 |
| Forking is not offered. This copies your own folder into your own repository, which works on any host; a fork button would only make sense for someone else's project. The route taken is reported when it finishes. | 唔提供分叉。呢個功能係將你自己嘅資料夾複製入你自己嘅儲存庫，任何主機都用得；分叉按鈕只適用於別人嘅專案。完成之後會報告用咗邊條路線。 |
| Gitea or Forgejo | Gitea 或 Forgejo |
| GitHub CLI found. Accounts, creating repositories and sign-in are available. | 搵到 GitHub CLI。可以使用帳戶、建立儲存庫同登入。 |
| How it is built: this is a native Windows drawing board built on the ink canvas that ships with Windows. The Drawesome package is a JavaScript library, and using it would need a web view and a package download that this app does not have, so it is not used. Pencil, Pen and Brush thin as you draw faster and the Fountain pen follows the stroke direction; the width is applied when you lift the pen, so the live line is a constant width while you draw. Saved pictures never overwrite the original: a PNG goes next to it with "-annotated" added to the name. | 製作方式：呢個係原生 Windows 畫板，建基於 Windows 內置嘅墨跡畫布。Drawesome 套件係 JavaScript 程式庫，要用就需要網頁檢視同套件下載，而呢個程式冇，所以唔用。鉛筆、鋼筆同毛筆畫得越快就越幼，墨水筆會跟住筆劃方向；闊度喺你提起筆嗰陣先套用，所以畫緊嗰陣見到嘅線條闊度係固定嘅。儲存嘅圖片永遠唔會覆蓋原圖：會喺旁邊另存一個 PNG，名稱加上「-annotated」。 |
| How many times larger the exported PNG is than the board on screen. 2x and 3x are sharper and make bigger files. Default: 1x. | 匯出嘅 PNG 比螢幕上嘅畫板大幾多倍。2 倍同 3 倍更清晰，檔案亦更大。預設：1 倍。 |
| Left | 左 |
| New blank board | 新空白畫板 |
| No picture is in clipboard history right now. Copy one and it will show up here. | 剪貼簿紀錄而家冇圖片。複製一張，就會出現喺呢度。 |
| No picture is in clipboard history yet, so that button is off. | 剪貼簿紀錄未有圖片，所以嗰個按鈕用唔到。 |
| Off by default, and you are asked to confirm before anything is created. Needs the GitHub CLI and a signed-in account. If the folder already has an origin remote, nothing new is created. | 預設關閉，而且建立任何嘢之前都會問你確認。需要 GitHub CLI 同已登入嘅帳戶。如果資料夾已經有 origin 遠端，就唔會建立新嘢。 |
| Off by default. The toolbar starts as a small disc that still shows the active tool. Click it to expand. | 預設關閉。工具列一開始係一個細圓盤，仍然顯示使用中嘅工具。撳一下就會展開。 |
| On by default. Switch it off to commit on this computer only. Declining leaves the files unpublished: nothing is sent to GitHub until you run this again with it on. | 預設開啟。關閉就只會喺呢部電腦提交。拒絕就唔會發佈檔案：要再開啟並重新執行，先會傳送任何嘢去 GitHub。 |
| Open an image… | 開啟圖片… |
| Open it in Visual Studio Code | 用 Visual Studio Code 開啟 |
| Open the scripts folder in the editor | 喺編輯器開啟腳本資料夾 |
| Or push to an existing empty repository (paste its address) | 或者推送去現有嘅空儲存庫（貼上佢嘅位址） |
| Pens: Pencil P, Pen N, Fineliner F, Marker M, Highlighter H, Brush B, Fountain pen U. Eraser E, Select S, Line L, Arrow A, Rectangle R, Ellipse O, Text T. Colour C and size Z turn the toolbar into a palette or sliders. Ctrl+Z undo, Ctrl+Y redo, Ctrl+S save a PNG, Ctrl+Shift+C copy, Esc minimise. Hold Shift to lock a stroke to the nearest of eight directions. | 筆：鉛筆 P、鋼筆 N、針筆 F、麥克筆 M、螢光筆 H、毛筆 B、墨水筆 U。擦膠 E、選取 S、直線 L、箭嘴 A、長方形 R、橢圓 O、文字 T。顏色 C 同大小 Z 會將工具列變成色板或者滑桿。Ctrl+Z 復原、Ctrl+Y 重做、Ctrl+S 儲存 PNG、Ctrl+Shift+C 複製、Esc 最小化。按住 Shift 可以將筆劃鎖定喺最近嘅八個方向之一。 |
| PNG export size (default 1x) | PNG 匯出大小（預設 1 倍） |
| Preferred editor (default automatic: VS Code, then Notepad++, then the system default) | 偏好嘅編輯器（預設自動：先 VS Code，再 Notepad++，最後系統預設） |
| Private | 私人 |
| Private means only you and people you invite can see it; public means anyone can. Default: private. | 私人即係只有你同你邀請嘅人睇到；公開即係任何人都睇到。預設：私人。 |
| Public | 公開 |
| Publish | 發佈 |
| Publish a folder to GitHub | 將資料夾發佈到 GitHub |
| Refresh the list | 重新整理清單 |
| Repository name | 儲存庫名稱 |
| Right | 右 |
| Save custom program | 儲存自訂程式 |
| Start with the toolbar minimised | 啟動時將工具列最小化 |
| System default | 系統預設 |
| The folder whose files are committed and published. Starts as the last folder you used. | 要提交同發佈檔案嘅資料夾。預設係你上次用嘅資料夾。 |
| The full path of the program to use when "Custom program" is chosen. | 揀咗「自訂程式」之後要用嘅程式完整路徑。 |
| The IP history repository has not been set up on this computer, so its shortcut is not shown. | IP 紀錄儲存庫未喺呢部電腦設定，所以唔顯示佢嘅捷徑。 |
| The new repository's name on GitHub. Defaults to the folder name. | 新儲存庫喺 GitHub 嘅名稱。預設係資料夾名稱。 |
| The program that opens a shortcut's script when you choose Open in editor. Automatic tries VS Code, then Notepad++, then the system default. | 你揀「用編輯器開啟」嗰陣，用嚟開啟捷徑腳本嘅程式。「自動」會先試 VS Code，再試 Notepad++，最後用系統預設。 |
| The screen edge the drawing toolbar sits on when the board opens. Default: top. | 畫板開啟嗰陣，繪圖工具列所在嘅螢幕邊緣。預設：頂部。 |
| Toolbar position (default top) | 工具列位置（預設頂部） |
| Tools | 工具 |
| Top | 上 |
| Upload after committing | 提交之後上載 |
| Visibility of a new repository (default private) | 新儲存庫嘅可見度（預設私人） |
| Where to publish (default GitHub) | 發佈到邊度（預設 GitHub） |
| Which kind of site the folder is published to. Each one has its own sign-in and options below. Default: GitHub. | 資料夾發佈到邊類網站。每類都有自己嘅登入同下面嘅選項。預設：GitHub。 |

## Page: tools (forge host choice)

| English | Cantonese |
| --- | --- |
| (active) | （目前） |
| Only needed to create a new repository when the matching command-line tool is not signed in. It is kept in memory for this one action, sent only to the server named above, and cleared afterwards. It is never saved or logged. | 只有喺相應嘅命令列工具未登入、而你又要建立新儲存庫嗰陣先需要。只會為呢一次操作暫存喺記憶體，只會傳送去上面寫明嘅伺服器，用完即清除，永遠唔會儲存或者寫入紀錄。 |
| Owner of a new repository (optional) | 新儲存庫嘅擁有者（可選） |
| Owner of the new repository | 新儲存庫嘅擁有者 |
| Owner of the new repository (default: your own account) | 新儲存庫嘅擁有者（預設：你自己嘅帳戶） |
| Personal access token (used once, never saved) | 個人存取權杖（只用一次，永遠唔儲存） |
| Refresh sign-in | 重新整理登入狀態 |
| Server address | 伺服器位址 |
| Server address (empty means gitlab.com for GitLab) | 伺服器位址（留空即係 GitLab 嘅 gitlab.com） |
| Set active | 設為使用中 |
| Sign out | 登出 |
| The account or organisation that will own the new repository. | 將會擁有新儲存庫嘅帳戶或者組織。 |
| The glab command-line tool is not installed, so creating a repository here needs a token. Publishing to an existing empty repository does not. | 未安裝 glab 命令列工具，所以喺呢度建立儲存庫需要權杖。發佈到現有嘅空儲存庫就唔使。 |
| The group, organization or user that should own the new repository. Empty means the account that signs in. | 擁有新儲存庫嘅群組、組織或者使用者。留空即係登入嘅帳戶。 |
| The server address, for example https://git.example.org. Leave empty for gitlab.com. Gitea and Forgejo need an address. | 伺服器位址，例如 https://git.example.org。留空就用 gitlab.com。Gitea 同 Forgejo 一定要填位址。 |
| The tea command-line tool is not installed, so creating a repository here needs a token. Publishing to an existing empty repository does not. | 未安裝 tea 命令列工具，所以喺呢度建立儲存庫需要權杖。發佈到現有嘅空儲存庫就唔使。 |
| The token is only used when you tick "create a new repository" and the command-line tool is missing or not signed in. Pushing an existing repository uses whatever sign-in Git already has on this computer. | 只有喺你剔選「建立新儲存庫」，而命令列工具又缺少或者未登入嗰陣，先會用到權杖。推送現有儲存庫就用呢部電腦上 Git 已有嘅登入資料。 |

## Regex builder

| English | Cantonese |
| --- | --- |
| (?1) subroutine | (?1) 子程序 |
| (?R) recursion | (?R) 遞迴 |
| . also matches line breaks | . 亦會配對換行 |
| 1 or more | 1 個或以上 |
| 2 or more | 2 個或以上 |
| 2 to 5 | 2 至 5 個 |
| [[:alpha:]] POSIX class | [[:alpha:]] POSIX 類別 |
| \U \L \E case conversion | \U \L \E 大細楷轉換 |
| ^ and $ match at the start and end of every line | ^ 同 $ 會喺每一行嘅開頭同結尾配對 |
| a code point | 一個碼位 |
| a digit | 一個數字 |
| a dollar sign | 一個錢號 |
| a group | 一個群組 |
| a letter in any script | 任何文字系統嘅一個字母 |
| a literal dot | 一個字面句點 |
| a range | 一個範圍 |
| a set with subtraction | 帶減去嘅字元集 |
| A small built-in interpreter replays one match attempt of the default backtracking engine. It covers literals, classes, anchors, word boundaries, groups, alternation, greedy and lazy repeats and backreferences. Before a trace is shown it is checked against the real .NET engine on the same subject; if they disagree, or the pattern uses something the interpreter does not cover, you are told and nothing is faked. | 內置一個細型直譯器，重播預設回溯引擎嘅一次配對嘗試。涵蓋字面字元、字元類別、錨點、字邊界、群組、交替、貪婪同惰性重複，同反向參照。顯示追蹤之前，會同真正嘅 .NET 引擎喺同一個主體上核對；如果結果唔一致，或者表達式用咗直譯器涵蓋唔到嘅語法，就會通知你，絕對唔會作假。 |
| a word character | 一個字詞字元 |
| Add a line of text and say whether the pattern should match it. Each line shows pass or fail as you edit the pattern. They are saved with the pattern when you save it to the Library. | 加一行文字，並指明表達式應該配對定唔應該配對。你改表達式嗰陣，每一行都會顯示通過或者失敗。儲存到資料庫嗰陣會一齊儲存。 |
| Add: should match | 新增：應該配對 |
| Add: should not match | 新增：唔應該配對 |
| an uppercase letter | 一個大楷字母 |
| Anchors | 錨點 |
| any character | 任何字元 |
| Apply to the pattern | 套用到表達式 |
| Atomic | 原子 |
| atomic group | 原子群組 |
| attempt | 嘗試 |
| Back to plain text | 返去普通文字 |
| backreference by name | 按名稱反向參照 |
| balancing group without a capture of its own: removes the latest "close" capture and fails when there is none | 冇自己擷取嘅平衡群組：移除最新嘅「close」擷取，如果冇就配對失敗 |
| balancing group: matches only while "close" has a capture, removes it, and records the text between as a capture of "open" | 平衡群組：只喺「close」有擷取嗰陣先配對，會移除佢，並將中間嘅文字記錄成「open」嘅擷取 |
| Benchmark | 效能測試 |
| Brief | 簡短 |
| Build | 建構 |
| case-insensitive matching ignores the current culture | 唔分大細楷配對時唔理目前文化特性 |
| Characters | 字元 |
| Choose several saved patterns to copy, export or remove together. Off by default. | 揀幾個已儲存嘅表達式一齊複製、匯出或者移除。預設關閉。 |
| Close the regex builder | 關閉正規表示式建構器 |
| comment | 註解 |
| Compare | 比較 |
| Compare the .NET engine modes | 比較 .NET 引擎模式 |
| conditional | 條件式 |
| Copy code | 複製程式碼 |
| Copy result | 複製結果 |
| Culture invariant | 不受文化特性影響 |
| Debug | 偵錯 |
| Detailed | 詳細 |
| end | 結尾 |
| end of text | 文字結尾 |
| Esc closes and returns to the search box. Patterns stay on this computer. | 按 Esc 會關閉並返回搜尋框。表達式只會留喺呢部電腦。 |
| Escape and insert | 跳脫並插入 |
| Exclude masks (default .git;node_modules) | 排除遮罩（預設 .git;node_modules） |
| Expected outcomes | 預期結果 |
| Explicit capture | 明確擷取 |
| Export CSV | 匯出 CSV |
| Export HTML | 匯出 HTML |
| Export JSON | 匯出 JSON |
| Export Markdown | 匯出 Markdown |
| File and folder masks to exclude, separated by semicolons | 要排除嘅檔案同資料夾遮罩，用分號分隔 |
| File masks (default *) | 檔案遮罩（預設 *） |
| File masks to include, separated by semicolons | 要包括嘅檔案遮罩，用分號分隔 |
| File name masks separated by semicolons, for example *.txt;*.md. * and ? are wildcards. | 檔案名稱遮罩，用分號分隔，例如 *.txt;*.md。* 同 ? 係萬用字元。 |
| File or folder names to leave out, separated by semicolons. | 要略過嘅檔案或資料夾名稱，用分號分隔。 |
| Files larger than this are skipped and listed. Default 5. | 大過呢個大小嘅檔案會被略過並列出。預設 5。 |
| First | 第一個 |
| Flat tokens | 平面標記 |
| Folder to search | 要搜尋嘅資料夾 |
| Followed by | 後面緊接 |
| Full sentences in the tree | 樹狀圖用完整句子 |
| Generate C# | 產生 C# |
| Generate PowerShell | 產生 PowerShell |
| Generate Python | 產生 Python |
| Generated code | 產生嘅程式碼 |
| Go back one step | 返回上一步 |
| Go forward one step | 前進一步 |
| Go to the first step | 跳到第一步 |
| Go to the last step | 跳到最後一步 |
| group that does not capture | 唔擷取嘅群組 |
| Groups | 群組 |
| Groups of the selected match | 所選配對嘅群組 |
| ignore case from here | 由呢度起忽略大細楷 |
| ignore pattern whitespace | 忽略表達式內嘅空白 |
| Ignore whitespace | 忽略空白 |
| Import JSON | 匯入 JSON |
| Include subfolders | 包括子資料夾 |
| info | 資訊 |
| Inline options | 內嵌選項 |
| Insert literal text | 插入字面文字 |
| IPv4 address | IPv4 位址 |
| ISO date | ISO 日期 |
| JavaScript-style behaviour; only works with ignore case and multiline | JavaScript 式行為；只可以同「忽略大細楷」同「多行」一齊用 |
| Kind of group | 群組類型 |
| Last | 最後一個 |
| lazy | 惰性 |
| lazy one or more | 惰性一個或以上 |
| Library | 資料庫 |
| linear-time engine; no lookaround, backreferences or atomic groups | 線性時間引擎；唔支援環顧、反向參照同原子群組 |
| Listed so you can see they are missing. Choose one to see why. | 列出嚟等你睇到佢哋缺少。揀一個就可以睇原因。 |
| Literal text to escape and insert | 要跳脫並插入嘅字面文字 |
| Look around | 環顧 |
| Make this a atomic group | 改成原子群組 |
| Make this a followed by group | 改成「後面緊接」群組 |
| Make this a named group | 改成具名群組 |
| Make this a no capture group | 改成非擷取群組 |
| Make this a not followed by group | 改成「後面唔緊接」群組 |
| Make this a not preceded by group | 改成「前面唔緊接」群組 |
| Make this a numbered group | 改成編號群組 |
| Make this a preceded by group | 改成「前面緊接」群組 |
| Match | 配對 |
| Matches | 配對 |
| Multiline | 多行 |
| multiline from here | 由呢度起多行 |
| Name for the saved pattern | 已儲存表達式嘅名稱 |
| Named | 具名 |
| named group | 具名群組 |
| No capture | 非擷取 |
| No expected outcomes yet. | 未有預期結果。 |
| No search yet. Choose a folder and press Search files. | 未有搜尋。揀一個資料夾，然後撳「搜尋檔案」。 |
| Non-backtracking | 非回溯 |
| none of | 以下皆非 |
| Not available in .NET | .NET 冇提供 |
| Not available in .NET. Case-changing replacement escapes are not part of .NET replacement strings. To change case, use a match evaluator in code, or a second pass. | .NET 冇提供。轉換大細楷嘅取代跳脫字元唔屬於 .NET 取代字串。想轉換大細楷，請喺程式碼用配對評估器，或者再做一次處理。 |
| Not available in .NET. POSIX bracket classes are not part of the .NET flavour. Use a Unicode property such as \p{L}, or a range such as [a-zA-Z]. | .NET 冇提供。POSIX 括號類別唔屬於 .NET 語法。請用 Unicode 屬性（例如 \p{L}）或者範圍（例如 [a-zA-Z]）。 |
| Not available in .NET. Recursion, which lets a pattern call itself to match nested structures, is not part of the .NET flavour. Balancing groups, such as (?<open-close>), cover many nested-pair cases in .NET. | .NET 冇提供。遞迴（令表達式呼叫自己去配對巢狀結構）唔屬於 .NET 語法。.NET 嘅平衡群組（例如 (?<open-close>)）可以處理好多巢狀配對嘅情況。 |
| Not available in .NET. Subroutine calls, which re-use a group's pattern elsewhere, are not part of the .NET flavour. Repeat the sub-pattern, or build the pattern in code from a shared string. | .NET 冇提供。子程序呼叫（喺其他位置重用群組嘅表達式）唔屬於 .NET 語法。請重複子表達式，或者喺程式碼用共用字串砌出表達式。 |
| Not followed by | 後面唔緊接 |
| Not preceded by | 前面唔緊接 |
| Numbered | 編號 |
| numbered group | 編號群組 |
| Off by default because patterns can be private. When on, up to 50 recent patterns per search box are stored on this computer in the app settings. Turning it off erases them. | 預設關閉，因為表達式可能係私人嘅。開啟後，每個搜尋框最多 50 個最近用過嘅表達式會儲存喺呢部電腦嘅程式設定入面。關閉就會清除。 |
| one of | 其中之一 |
| One of 3 alternatives | 3 個選項其中之一 |
| one or more | 一個或以上 |
| one word character (a letter, digit, connector such as _) | 一個字詞字元（字母、數字，或者 _ 呢類連接字元） |
| only named groups capture | 只有具名群組會擷取 |
| OPTIONAL | 可選 |
| or | 或 |
| Pattern (current token highlighted) | 表達式（目前標記已突顯） |
| Patterns you use here appear while the app runs. Turn on Remember across restarts to keep them. | 你喺呢度用過嘅表達式會喺程式運行期間顯示。開啟「重新啟動後仍然記住」就可以保留佢哋。 |
| Play | 播放 |
| Preceded by | 前面緊接 |
| Press Run comparison to measure this pattern on the sample. | 撳「執行比較」，喺樣本上量度呢個表達式。 |
| Prev | 上一個 |
| Preview a replacement | 預覽取代結果 |
| Preview of the test sample. Nothing is changed until you copy it. | 測試樣本嘅預覽。複製之前唔會改變任何嘢。 |
| Preview splitting the sample at every match | 預覽喺每個配對位置分割樣本 |
| Preview the replacement in every file with matches. Nothing is written yet. | 預覽每個有配對嘅檔案嘅取代結果。暫時唔會寫入任何嘢。 |
| Put the files from the most recent replace run back as they were | 將最近一次取代所改動嘅檔案還原 |
| Recent in this search box | 呢個搜尋框最近用過 |
| Regex builder | 正則表達式砌法 |
| Remember across restarts | 重新啟動後仍然記住 |
| Repeat | 重複 |
| Replace | 取代 |
| Replace in files... | 喺檔案中取代... |
| Replacement | 取代內容 |
| Replacement (used by Replace in files) | 取代內容（「喺檔案中取代」使用） |
| Replacement explained | 取代內容說明 |
| Replacement text | 取代文字 |
| Replacement text for files | 用於檔案嘅取代文字 |
| Restore last backup... | 還原上次備份... |
| Result preview | 結果預覽 |
| Right to left | 由右至左 |
| Run comparison | 執行比較 |
| Run the pattern over the sample in every available .NET mode | 喺每個可用嘅 .NET 模式對樣本執行表達式 |
| Runs the pattern over the sample in each mode this runtime really has: the default backtracking engine, the non-backtracking engine, ECMAScript mode and compiled. Each result shows whether the mode accepts the pattern, the match count, the first difference from the default engine and the time of one run. Other languages' engines (PCRE, Java, JavaScript, Python and so on) are not available in this app, so no comparison with them is possible here. | 喺呢個執行環境真正有嘅每個模式，對樣本執行表達式：預設回溯引擎、非回溯引擎、ECMAScript 模式同編譯模式。每個結果都會顯示該模式接唔接受呢個表達式、配對數目、同預設引擎第一個差異，同執行一次嘅時間。其他語言嘅引擎（PCRE、Java、JavaScript、Python 等）呢個程式冇提供，所以喺度比較唔到。 |
| Same syntax as the Replace view: $1, ${name}, $0 and $$. | 同「取代」檢視一樣嘅語法：$1、${name}、$0 同 $$。 |
| Sample text | 樣本文字 |
| Sample text to test the pattern against | 用嚟測試表達式嘅樣本文字 |
| Save every match as a CSV file | 將每個配對儲存成 CSV 檔案 |
| Save the current pattern | 儲存目前嘅表達式 |
| Search from the end of the text | 由文字結尾開始搜尋 |
| Search inside folders below the chosen one. On by default. | 搜尋所揀資料夾下面嘅子資料夾。預設開啟。 |
| Search the folder. Nothing is changed. | 搜尋資料夾。唔會改動任何嘢。 |
| Searches the text of every matching file in a folder with the pattern above. Searching never changes a file. Replacing is a separate step with a preview, a deliberate confirmation and a backup of every file first. Matches are found on the whole file, so a pattern can span lines. | 用上面嘅表達式搜尋資料夾內每個符合檔案嘅文字。搜尋永遠唔會改動檔案。取代係另一個步驟，會有預覽、刻意嘅確認，同先備份每個檔案。配對係喺成個檔案上搵，所以表達式可以跨行。 |
| Sets | 字元集 |
| Short labels in the tree (default) | 樹狀圖用簡短標籤（預設） |
| Show a flat token-by-token list instead of the tree | 用逐個標記嘅平面清單取代樹狀圖 |
| Show the nested syntax tree (default) | 顯示巢狀語法樹（預設） |
| Singleline | 單行 |
| singleline from here | 由呢度起單行 |
| Skip a file with a null byte in its first 8,000 bytes. On by default. | 如果檔案頭 8,000 個位元組有空位元組就略過。預設開啟。 |
| Skip binary files | 略過二進位檔案 |
| Skip files larger than this many megabytes | 略過大過呢個 MB 數嘅檔案 |
| Skip files over (MB) | 略過大過以下大小嘅檔案（MB） |
| spaces and # comments inside the pattern are ignored | 表達式內嘅空格同 # 註解會被忽略 |
| Split | 分割 |
| Start | 啟動 |
| Start a match attempt at position 0 | 喺位置 0 開始一次配對嘗試 |
| Start of text | 文字開頭 |
| Step forward automatically, a few steps per second | 自動向前行，每秒幾步 |
| Step-by-step trace | 逐步追蹤 |
| Stop the running search | 停止進行中嘅搜尋 |
| Subject (current position highlighted) | 主體（目前位置已突顯） |
| Subject text for the step-by-step trace | 逐步追蹤嘅主體文字 |
| Subject to trace (first 4000 characters) | 要追蹤嘅主體（頭 4000 個字元） |
| Syntax tree of the pattern | 表達式嘅語法樹 |
| Test | 測試 |
| The replacement is empty, so every match is deleted. | 取代內容係空嘅，所以每個配對都會被刪除。 |
| the text after the match | 配對之後嘅文字 |
| the text before the match | 配對之前嘅文字 |
| the whole input | 成個輸入 |
| the whole match | 成個配對 |
| Tree | 樹狀 |
| Use this list as the sample | 用呢張清單做樣本 |
| where the last match ended | 上一個配對結束嘅位置 |
| Whitespace | 空白字元 |
| Windows path | Windows 路徑 |
| Word boundary | 字詞邊界 |
| Word character | 字詞字元 |
| zero or more | 零個或以上 |

## Search

| English | Cantonese |
| --- | --- |
| Search every page | 搜尋所有頁面 |

## Setup wizard

| English | Cantonese |
| --- | --- |
| A dialog appears when the connection is lost and stays until the line returns or you dismiss it. | 斷線嗰陣會彈出視窗，一直留喺度，直到連線恢復或者你自己關閉。 |
| A dialog on every change, and a history with first and last seen times. | 每次變動都有提示，並記錄首次同最後見到嘅時間。 |
| A small card with actions pops up each time you copy something. | 每次複製嘢，角落都會彈出一張帶操作嘅細卡片。 |
| A toast stays on screen while the pointer is over it, so you can reach its buttons. | 滑鼠停喺通知上面嗰陣，通知會留喺螢幕，等你撳到佢嘅按鈕。 |
| Alert me when the internet drops | 上網中斷時通知我 |
| All set | 全部設定好 |
| Any fullscreen app counts | 任何全螢幕程式都算 |
| Back | 返回 |
| Back (Alt+Left) | 返回 (Alt+左) |
| Check my connection now | 即刻檢查我嘅連線 |
| Clipboard | 剪貼簿 |
| Close (Esc). Progress is kept. | 關閉 (Esc)，進度會保留。 |
| Close setup and keep progress | 關閉設定並保留進度 |
| Commands | 指令 |
| Commit message for :push | :push 嘅提交訊息 |
| Connection | 連線 |
| current branch | 目前分支 |
| Every copy shows a toast in the bottom-right corner. Hover it to hold it and see actions. | 每次複製都會喺右下角彈出通知。滑鼠停留可以留住同睇到操作。 |
| folder name of the repository | 儲存庫嘅資料夾名稱 |
| Fullscreen games, video and presentations. | 全螢幕遊戲、影片同簡報。 |
| Here is what was set up. Everything can be changed later from the control centre. | 以下係已設定嘅內容。之後全部都可以喺控制中心更改。 |
| Hold popups while a game runs | 遊戲行緊時暫停通知 |
| Internet watch | 網絡監察 |
| Keep open while hovered | 滑鼠停留時保持開啟 |
| Monitoring Tools runs quietly in the tray. Choose whether it starts when you sign in. | Monitoring Tools 靜靜地喺系統匣運行。你可以揀登入時係咪自動啟動。 |
| Next | 下一步 |
| Next (Enter) | 下一步 (Enter) |
| number of changed files | 已更改檔案數目 |
| Optional. Copy a path with :mp4, :gif or :push on the end and it runs. Set the commit message :push uses, and try a conversion. | 可選。複製路徑尾加 :mp4、:gif 或 :push 就會執行。可以設定 :push 用嘅提交訊息，同試吓轉檔。 |
| Optional. Pick who answers your DNS lookups. Switching asks for administrator approval once. | 可選。揀由邊個回應你嘅 DNS 查詢。切換時會要求一次管理員批准。 |
| Popups wait while you play. On by default. | 你玩緊嘢時通知會等。預設開啟。 |
| Private copies never pop up or reach history. | 私人複製唔會彈出，亦唔會入紀錄。 |
| Progress saves as you go. Close any time and pick up from the tray. | 進度會自動儲存。隨時可以關閉，之後喺系統匣繼續。 |
| Quick commands | 快捷指令 |
| Recommended. You can change this later in the control centre. | 建議開啟。之後可以喺控制中心更改。 |
| Set earlier | 之前已設定 |
| Set up Monitoring Tools | 設定 Monitoring Tools |
| Setup | 設定 |
| Show clipboard toasts | 顯示剪貼簿通知 |
| Show me a toast | 彈個通知睇下 |
| Skip copies from password managers | 略過密碼管理員嘅複製 |
| Skip this step | 跳過呢一步 |
| Skipped | 已略過 |
| Start when I sign in | 登入時啟動 |
| Start with Windows | 隨 Windows 啟動 |
| Start-up | 啟動 |
| Toast duration | 通知顯示時間 |
| Try a conversion | 試吓轉檔 |
| Tuesday | 星期二 |
| Watch my public IP address | 監察我嘅公開 IP 位址 |
| When the connection drops, a dialog explains where it broke and stays until it is back. | 連線中斷時會有視窗講解邊度出事，並且留低直到恢復。 |
| While a watched game is running, popups wait until you stop playing. | 受監察嘅遊戲行緊嗰陣，彈出通知會等你唔玩先出現。 |
| Windows user name | Windows 使用者名稱 |

## Toasts

| English | Cantonese |
| --- | --- |
| Cancel | 取消 |
| Color | 顏色 |
| Copy as path | 複製做路徑 |
| Copy as plain text | 複製做純文字 |
| Copy domain | 複製網域 |
| Copy new IP | 複製新 IP |
| Copy to… | 複製到… |
| Decode Base64 | 解碼 Base64 |
| Delete | 刪除 |
| Dismiss | 關閉 |
| Document | 文件 |
| Done | 完成 |
| Drag it anywhere. Or copy it again with :jpg on the end to convert. | 可以拖去任何地方。或者再複製一次，尾加 :jpg 就可以轉換。 |
| Drag the list out to drop all of them. Hover to move, copy, delete, encode or encrypt. | 拖曳清單出去可以放低全部檔案。滑鼠停留可以移動、複製、刪除、編碼或加密。 |
| Email | 電郵 |
| Encode Base64 | 編碼 Base64 |
| File tools | 檔案工具 |
| Folder | 資料夾 |
| held | 已留住 |
| History | 歷史 |
| Image file | 圖片檔案 |
| JSON | JSON |
| Link | 連結 |
| lower | 全細楷 |
| Markdown | Markdown |
| Minify | 壓縮 |
| Move to… | 移動到… |
| now | 而家 |
| Open | 開啟 |
| Open in Photos | 用「相片」開啟 |
| Open private | 以私人模式開啟 |
| Open repository | 開啟儲存庫 |
| Open with… | 用其他程式開啟… |
| Pretty print | 格式化 |
| Save as .json | 儲存為 .json |
| Save as .md | 儲存為 .md |
| Saved to the IP history repository with first and last seen times. | 已連同首次同最後見到嘅時間，存入 IP 紀錄儲存庫。 |
| Search web | 網上搜尋 |
| Show in folder | 喺資料夾顯示 |
| Something finished. | 有嘢完成咗。 |
| Strip tracking | 移除追蹤參數 |
| Terminal here | 喺此開啟終端機 |
| Text | 文字 |
| UPPER | 全大楷 |

## Tray menu

| English | Cantonese |
| --- | --- |
| Administrator rights | 系統管理員權限 |
| auto | 自動 |
| Clipboard history | 剪貼簿紀錄 |
| Clipboard toasts | 剪貼簿通知 |
| Command palette | 指令面板 |
| Continue setup | 繼續設定 |
| double-click | 連按兩下 |
| Filter this menu | 篩選選單 |
| Game mode | 遊戲模式 |
| Match case (Alt+C) | 分大細楷 (Alt+C) |
| Monitoring Tools menu | Monitoring Tools 選單 |
| Network alerts | 網絡警示 |
| Off | 關 |
| Online | 在線 |
| Open control centre | 開啟控制中心 |
| Open log folder | 開啟日誌資料夾 |
| Open the regex builder for this search (Alt+B) | 為呢個搜尋開啟正規表示式建構器 (Alt+B) |
| Plain text by default. Alt+R switches to regular expressions, Alt+C matches case, Alt+B opens the regex builder. | 預設係純文字。Alt+R 轉用正規表示式，Alt+C 分大細楷，Alt+B 開啟建構器。 |
| Quit until next sign-in | 退出，直到下次登入 |
| Regular expression mode (Alt+R) | 正規表示式模式 (Alt+R) |
| Restart service | 重新啟動服務 |
| Shortcuts and scripts | 捷徑同腳本 |
| Snooze everything | 全部暫停 |
| Take the tour | 睇導覽 |
| Test connection now | 即刻測試連線 |
| Uninstall service… | 解除安裝服務… |

## Welcome

| English | Cantonese |
| --- | --- |
| Close | 關閉 |
| Copy a path with :mp4, :gif or :push on the end and it just happens. Try converts a sample picture. | 複製路徑尾加 :mp4、:gif 或 :push，就即刻執行。「試吓」會轉換一張範例圖片。 |
| Every copy slides in for 3 seconds. Hover to hold it and see actions made for what you copied. | 每次複製都會彈出 3 秒。滑鼠停留可以留住，並睇到針對你複製內容嘅操作。 |
| Everything below works on its own. Press Try it to see each one right now instead of waiting for it to happen. | 下面每項功能都可以獨立使用。撳「試吓」即刻睇效果，唔使等佢自己發生。 |
| It lives in your tray now. | 佢而家喺你嘅系統匣入面。 |
| Open editor | 開啟編輯器 |
| Pick games | 揀遊戲 |
| Preview the dialog | 預覽視窗 |
| Previews, move, copy, Base64 and password encryption for anything you copy. | 你複製嘅任何嘢都有預覽、移動、複製、Base64 同密碼加密。 |
| Right-click the tray icon any time. This tour lives there too. | 隨時右鍵點擊系統匣圖示，呢個導覽亦喺嗰度。 |
| Roblox, Minecraft, League and friends keep the screen. Popups wait until you stop. | Roblox、Minecraft、League 之類嘅遊戲霸住個畫面，通知會等你玩完先出現。 |
| Try it | 試吓 |
| Welcome | 歡迎 |
| When the line drops you see where it broke, plus one-press fixes. It stays until it is back. | 斷線時會睇到邊度出事，仲有一鍵修復。會一直留低，直到恢復。 |
| Write a batch or PowerShell script, name it, and copy anything with :name to run it. | 寫個批次檔或 PowerShell 腳本，改個名，之後複製任何嘢加 :名稱 就可以執行。 |
| Your shortcuts | 你嘅捷徑 |

## Not reached by the audit (messages, dialogs and states that need a particular event)

| English | Cantonese |
| --- | --- |
| (pinned) | （已釘選） |
| (theme) | （主題） |
| , ignore case | ，唔分大小寫 |
| , match case | ，區分大小寫 |
| 0 kept | 保留 0 個 |
| a backslash with nothing after it (the engine rejects this) | 後面冇嘢嘅反斜線（引擎會拒絕） |
| a carriage return (U+000D) | 歸位字元 (U+000D) |
| a closing parenthesis with no group to close (the engine rejects this) | 一個冇群組可以關閉嘅右括號（引擎會拒絕） |
| A comment up to the end of the line (ignored because Ignore whitespace is on) | 直到行尾嘅註解（因為開咗「忽略空白」，所以會被忽略） |
| a comment, ignored when matching | 註解，配對時會被忽略 |
| a form feed (U+000C) | 換頁字元 (U+000C) |
| a group that does not capture, because Explicit capture is on | 唔擷取嘅群組，因為開咗「明確擷取」 |
| a line feed (U+000A) | 換行字元 (U+000A) |
| a literal opening brace (it does not start a valid quantifier here) | 一個字面左大括號（喺呢度唔係有效量詞嘅開頭） |
| a mode that removes it is on | 有個會移除佢嘅模式開咗 |
| a position that is not a word boundary | 唔係字詞邊界嘅位置 |
| a quantifier with nothing before it to repeat (the engine rejects this) | 前面冇嘢可以重複嘅量詞（引擎會拒絕） |
| A small built-in interpreter replays one match attempt of the default backtracking engine. It covers literals, classes, anchors, word boundaries, groups, alternation, greedy and lazy repeats and backreferences. Before a trace is shown it is checked against the real .NET engine on the same subject; if they disagree, or the pattern uses something it cannot replay, no trace is shown and the reason is given. | 內置一個細型直譯器，重播預設回溯引擎嘅一次配對嘗試。涵蓋字面字元、字元類別、錨點、字邊界、群組、交替、貪婪同惰性重複，同反向參照。顯示追蹤之前，會同真正嘅 .NET 引擎喺同一個主體上核對；如果結果唔一致，或者表達式用咗佢重播唔到嘅語法，就唔會顯示追蹤，並會講明原因。 |
| a tab (U+0009) | Tab 字元 (U+0009) |
| A third alternative, which the engine rejects in a conditional | 第三個選項（引擎唔接受條件式有呢個） |
| a vertical tab (U+000B) | 垂直 Tab 字元 (U+000B) |
| a word boundary: between a word character and a non-word character | 字詞邊界：字詞字元同非字詞字元之間 |
| Accept the EULA first; the server will not run without it. | 請先同意 EULA，否則伺服器行唔到。 |
| active | 目前 |
| Active now:  | 而家： |
| Add plugin or mod… | 新增外掛或模組… |
| Add quotes | 加引號 |
| ADHD mode | 專注力輔助模式 |
| Administrator rights unchanged | 系統管理員權限冇變 |
| Alert | 警示色 |
| All open tabs are visible; scroll the strip to move through them | 所有已開分頁都睇到，捲動分頁列就可以逐個睇 |
| Allow flight | 允許飛行 |
| Allow list only | 只限白名單 |
| Alt+B regex builder | Alt+B 正規表示式建構器 |
| an atomic group: once it has matched, the engine never backtracks into it | 原子群組：配對成功之後，引擎永遠唔會退返入去 |
| and {0} more | 另外仲有 {0} 個 |
| any character except a line break (any character with Singleline) | 除換行以外嘅任何字元（用咗單行模式就係任何字元） |
| Approve | 批准 |
| Approve the prompt to release and renew the IP address… | 請喺提示按批准，釋放並更新 IP 位址… |
| Approve this host key? | 批准呢個主機金鑰？ |
| Archive | 壓縮檔 |
| Arguments, for example a player name or 'day' | 參數，例如玩家名稱或 'day' |
| Audio clip | 音訊片段 |
| Audio file | 音訊檔案 |
| Base64 and password encryption for all of them | 全部檔案都可以做 Base64 同密碼加密 |
| Bell | 響鈴 |
| Bitmap on the clipboard. Drag the preview out to drop it as a PNG file. | 剪貼簿入面係點陣圖。拖曳預覽出去就可以存成 PNG 檔案。 |
| Build and run | 建立並執行 |
| Build image | 建立映像 |
| Build on Windows | 喺 Windows 建置 |
| Builds the image as {image} using the file's folder as the build context. | 用檔案所在資料夾做建置環境，建立映像。 |
| Builds what needs building and starts every service in the background. | 建立需要建立嘅部分，並喺背景啟動所有服務。 |
| Builds, then runs it in a terminal and removes the container when it stops. | 建立後喺終端機執行，停止時會移除容器。 |
| Built in | 內置 |
| Cancelled | 已取消 |
| Cancelled at the approval prompt. Nothing changed. | 喺批准提示取消咗，冇任何更改。 |
| Cancelled at the prompt. | 喺提示取消咗。 |
| Cancelling | 取消緊 |
| Careful reviewer | 細心嘅審閱者 |
| Carriage return | 歸位 |
| Check accounts with Mojang | 向 Mojang 驗證帳戶 |
| Check host key | 檢查主機金鑰 |
| Checking adapter, gateway, DNS and the internet… | 檢查緊網絡卡、閘道、DNS 同互聯網… |
| Checking the connection | 檢查緊連線 |
| Checking… | 檢查緊… |
| Checks every few minutes and right after the network changes. | 每隔幾分鐘檢查一次，網絡有變動之後亦會即時檢查。 |
| Cheeky | 調皮 |
| Choose a folder and press Search files. | 揀一個資料夾，然後撳「搜尋檔案」。 |
| Choose a password of 8 to 128 characters. | 請設定 8 至 128 個字元嘅密碼。 |
| Choose a PIN of 4 to 12 digits or a password of at least 8 characters. | 請設定 4 至 12 位數字嘅 PIN 或者至少 8 個字元嘅密碼。 |
| Choose a PIN of 4 to 12 digits. | 請設定 4 至 12 位數字嘅 PIN。 |
| Choose a plugin or mod | 揀外掛或模組 |
| Choose several models to copy, export or remove together. Off by default. | 揀幾個模型一齊複製、匯出或者移除。預設關閉。 |
| Cleaned | 已清理 |
| Clear and restore original wording | 清除並還原原本用語 |
| Clear personal wording | 清除個人用語 |
| Close dialog | 關閉視窗 |
| Close other tabs | 關閉其他分頁 |
| Close tab | 關閉分頁 |
| Close tabs | 關閉分頁 |
| Close tabs containing text | 關閉包含文字嘅分頁 |
| Close tabs containing text… | 關閉包含文字嘅分頁… |
| Close tabs not containing text | 關閉唔包含文字嘅分頁 |
| Close tabs not containing text… | 關閉唔包含文字嘅分頁… |
| Close {0} tab | 關閉 {0} 個分頁 |
| Close {0} tabs | 關閉 {0} 個分頁 |
| Code file | 程式碼檔案 |
| Collapse group | 收合群組 |
| collapsed | 已收合 |
| collecting the first probes… | 正在收集頭幾次探測… |
| Colours come from the app theme, so they follow light, dark and your appearance choices. | 顏色來自程式主題，所以會跟住淺色、深色同你嘅外觀選擇。 |
| Colours you choose appear here. | 你揀過嘅顏色會出現喺呢度。 |
| Commit | 提交 |
| Commit and push | 提交並推送 |
| Commit everything and push. Same as copying the path with :push on the end. | 提交所有改動並推送。同複製路徑尾加 :push 一樣。 |
| Commit failed | 提交失敗 |
| Commit message set | 已設定提交訊息 |
| Committing | 提交緊 |
| Compare these with the host's own fingerprints before approving. /  /  | 批准之前，請同主機本身嘅指紋對比。 /  /  |
| completion | 文字補全 |
| Compose file | Compose 檔案 |
| Compose up | Compose 啟動 |
| Compose up finished | Compose 啟動完成 |
| Concise | 簡潔 |
| Condition | 條件 |
| Console | 主控台 |
| Console window: hidden. Administrator rights: not requested. | 主控台視窗：隱藏。管理員權限：不要求。 |
| Control centre | 控制中心 |
| Conversion cancelled | 轉檔已取消 |
| Conversion failed | 轉檔失敗 |
| Conversion tried | 已試過轉檔 |
| Conversions, pushes, shortcuts, file jobs and outages show up here. | 轉檔、推送、捷徑、檔案工作同斷線都會喺呢度顯示。 |
| Convert media | 轉換媒體 |
| Copied | 已複製 |
| Copied with quotes. Hover for Remove quotes. | 連引號一齊複製。滑鼠停留可以移除引號。 |
| copies | 複製紀錄 |
| Copies marked private by the source app never pop up or reach history. | 來源程式標記為私人嘅複製，唔會彈出，亦唔會入紀錄。 |
| Copy as Base64 text | 複製做 Base64 文字 |
| Copy as data URI (for HTML and CSS) | 複製做 data URI（用於 HTML 同 CSS） |
| Copy as file | 複製做檔案 |
| Copy as paths | 複製做路徑清單 |
| Copy error | 複製錯誤訊息 |
| Copy name | 複製名稱 |
| Copy names | 複製名稱清單 |
| Copy output | 複製輸出 |
| Copy path text | 複製路徑文字 |
| Copy text | 複製文字 |
| Copy to | 複製到 |
| Copying | 複製緊 |
| Could not read the latest version. | 讀取唔到最新版本。 |
| Create a new group | 建立新群組 |
| Create group | 建立群組 |
| Create group and move | 建立群組並移入 |
| Create server | 建立伺服器 |
| Current colour | 目前顏色 |
| Custom shortcuts and scripts | 自訂捷徑同腳本 |
| Decode to file | 解碼成檔案 |
| Decoded | 已解碼 |
| Decrypted | 已解密 |
| Deleted | 已刪除 |
| Details | 詳細資料 |
| Details copied. | 已複製詳情。 |
| Details: | 詳細資料： |
| Dialogs | 對話框 |
| Difficulty | 難度 |
| Digit | 數字 |
| Dim all but the card under focus or the mouse | 將焦點或者滑鼠所在卡片以外嘅嘢全部調暗 |
| DNS cache flushed. Checking again… | DNS 快取已清除，再檢查一次… |
| DNS left as it was | DNS 保持原狀 |
| DNS switch failed | 轉 DNS 失敗 |
| Dock tabs to the bottom | 將分頁列移到下邊 |
| Dock tabs to the left | 將分頁列移到左邊 |
| Dock tabs to the right | 將分頁列移到右邊 |
| Dock tabs to the top | 將分頁列移到上邊 |
| Docker Compose file. Hover to see what you can run and what each one does. | Docker Compose 檔案。滑鼠停留可以睇到可以執行咩同每項嘅功能。 |
| Docker here | 本機 Docker |
| Docker over SSH | 經 SSH 用 Docker |
| Dockerfile | Dockerfile |
| Dockerfile. Hover to build an image from it. | Dockerfile。滑鼠停留可以用佢建立映像。 |
| Down | 停止並移除 |
| Downloads newer images for the services without starting them. | 下載服務嘅較新映像，但唔會啟動。 |
| Drag the path out to drop the new file anywhere. | 拖曳路徑出去，就可以將新檔案放去任何地方。 |
| Drawn and shown this launch. | 今次抽中咗並且已經顯示。 |
| Drawn this launch. Fetching a dish. | 今次抽中咗，正在攞點心。 |
| Drawn this launch. Waiting for a quiet moment. | 今次抽中咗，等一個安靜嘅時刻。 |
| Edit appearance… | 編輯外觀… |
| Edit group appearance… | 編輯群組外觀… |
| Edit remote settings on the host; this target keeps them there. | 請喺主機上編輯遠端設定，呢個目標會將設定留喺嗰度。 |
| Edit tab appearance… | 編輯分頁外觀… |
| Edit the list in the control centre. | 喺控制中心編輯清單。 |
| Edit this shortcut | 編輯呢個捷徑 |
| elapsed time and a gentle reminder | 顯示用咗幾耐，同溫和提醒 |
| embedding | 嵌入 |
| Encoded | 已編碼 |
| Encrypted | 已加密 |
| End of previous match | 上一個配對嘅結尾 |
| End of text (before line break) | 文字結尾（換行之前） |
| Enter opens. Delete or Ctrl+W closes. Ctrl+Shift+P pins. Ctrl+Shift+Up or Down reorders. Applications key opens the menu. Shift plus right-click edits the tab appearance. | 按 Enter 開啟。按 Delete 或 Ctrl+W 關閉。Ctrl+Shift+P 釘選。Ctrl+Shift+上或下調整次序。應用程式鍵開選單。Shift 加右鍵可編輯分頁外觀。 |
| Enter or Space collapses or expands. F2 renames. Applications key opens the group menu. Shift plus right-click edits the group appearance. | 按 Enter 或空白鍵收合或展開。F2 重新命名。應用程式鍵開群組選單。Shift 加右鍵可編輯群組外觀。 |
| Enter text first. Nothing closes on an empty query. | 請先輸入文字。冇輸入就唔會關閉任何嘢。 |
| Enter the host first. | 請先輸入主機。 |
| Enter the SSH host. | 請輸入 SSH 主機。 |
| Enter to go | Enter 前往 |
| entries | 項目 |
| Esc to close | Esc 關閉 |
| Escape character | 跳脫字元 |
| Ethernet | 乙太網絡 |
| Every feature explained, offline. | 每項功能都有說明，離線都睇得。 |
| Every page of the control centre. Choosing one opens it in a tab, or switches to its tab when it is already open. | 控制中心所有頁面。揀一頁就會喺分頁開啟，如果已經開咗就會切換到佢嘅分頁。 |
| Every replacement must be text. | 所有替換內容都必須係文字。 |
| Example: | 例子： |
| Exclusive fullscreen games, fullscreen video and presentations. | 獨佔全螢幕遊戲、全螢幕影片同簡報。 |
| Expand group | 展開群組 |
| expanded | 已展開 |
| Extra alternative | 額外選項 |
| Fabric | Fabric |
| Failed | 失敗 |
| Fetch FFmpeg | 下載 FFmpeg |
| Fetching did not finish | 下載未完成 |
| Fetching FFmpeg | 下載緊 FFmpeg |
| FFmpeg is missing from this build | 呢個版本冇附帶 FFmpeg |
| FFmpeg ready | FFmpeg 已就緒 |
| File | 檔案 |
| Fills the system prompt and parameters below. Answer briefly and directly. Use short sentences and no filler. | 填入下面嘅系統提示同參數。簡短直接咁答，用短句，唔好講廢話。 |
| Fills the system prompt and parameters below. You review text and code carefully. List concrete problems first, then suggestions. If you are unsure, say so instead of guessing. | 填入下面嘅系統提示同參數。你會仔細審閱文字同程式碼，先列出具體問題，再提出建議。如果唔肯定，就直接講，唔好估。 |
| Filter actions | 篩選動作 |
| Find ffmpeg.exe | 搵 ffmpeg.exe |
| Finished | 已完成 |
| Finished. | 完成。 |
| First saved settings | 第一次儲存嘅設定 |
| First seen | 首次見到 |
| first-run setup is not finished | 首次設定未完成 |
| Flavour | 類型 |
| Flavour: Vanilla. Paper and Fabric run on the Docker targets. | 類型：原版。Paper 同 Fabric 要喺 Docker 目標上執行。 |
| Flushing the DNS cache… | 清除緊 DNS 快取… |
| FOLDER ON HOST | 主機上嘅資料夾 |
| Folder on the host (optional) | 主機上嘅資料夾（可選） |
| fonts | 字體 |
| Form feed | 換頁 |
| Format: a path, a colon, then a format such as mp4, gif, mp3 or webp. Also :push and your own :shortcuts. | 格式：一個路徑、一個冒號，再加格式，例如 mp4、gif、mp3 或 webp。亦可以用 :push 同你自訂嘅 :捷徑。 |
| Formatted | 已格式化 |
| Forward slashes | 正斜線 |
| Friendly | 友善 |
| Fullscreen app | 全螢幕程式 |
| Fullscreen game | 全螢幕遊戲 |
| Game mode off | 遊戲模式已關 |
| Game mode on | 遊戲模式已開 |
| game mode or snooze is holding popups | 遊戲模式或者暫停通知令彈出視窗被收埋 |
| game rules | 遊戲規則 |
| games | 遊戲 |
| gateway silent | 閘道冇回應 |
| Get the release build, which carries FFmpeg inside, or pick ffmpeg.exe below. | 請用附帶 FFmpeg 嘅正式版本，或者喺下面揀 ffmpeg.exe。 |
| Git is not installed | 未安裝 Git |
| Give it a name and valid IPv4 addresses. | 請輸入名稱同有效嘅 IPv4 位址。 |
| group | 群組 |
| Group (no capture) | 群組（不擷取） |
| Group (not captured) | 群組（不擷取） |
| Group colour | 群組顏色 |
| Group colour… | 群組顏色… |
| Group name | 群組名稱 |
| Hardcore | 極限模式 |
| Headings, lists, code and links render in the preview. | 預覽會顯示標題、清單、程式碼同連結。 |
| Headlines are plain while School mode is on. The humour levels are left out until it is off. | 學校模式開咗，標題會用簡單直接嘅講法，幽默程度要等關咗先有。 |
| Hide console | 收埋主控台 |
| hold non-urgent popups in History | 將唔緊急嘅彈出通知收入歷史 |
| Hover to save or convert. Quick commands work here too. | 滑鼠停留可以儲存或轉檔，快捷指令喺度都用得。 |
| Hovering holds the toast and reveals the actions. | 滑鼠停喺上面就會留住通知同顯示操作。 |
| How it is built: this is a native Windows drawing board built on the ink canvas that ships with Windows. The Drawesome package is a JavaScript library, and using it would need a web view and a package download that this app does not have, so it is not used. Pencil, Pen and Brush thin as you draw faster and the Fountain pen follows the stroke direction; the width is applied when the stroke ends. | 製作方式：呢個係原生 Windows 畫板，建基於 Windows 內置嘅墨跡畫布。Drawesome 套件係 JavaScript 程式庫，要用就需要網頁檢視同套件下載，而呢個程式冇，所以唔用。鉛筆、鋼筆同毛筆畫得越快就越幼，鋼筆會跟住筆劃方向；筆劃完咗先會套用闊度。 |
| I accept the  | 我同意  |
| if the condition holds, match the first branch, otherwise the second | 條件成立就配對第一個分支，否則配對第二個 |
| If true | 如果成立 |
| in | 喺 |
| Include pinned tabs (off by default) | 包括已釘選分頁（預設關閉） |
| Input | 輸入 |
| Inside a git repository. Copy it with :push on the end to commit and push. | 喺 git 儲存庫入面。複製時尾加 :push 就可以提交並推送。 |
| Install did not finish | 安裝未完成 |
| Install Git for Windows, then try again. | 請安裝 Git for Windows，然後再試。 |
| Installing FFmpeg | 安裝緊 FFmpeg |
| invalid | 無效 |
| IP | IP |
| items | 項目 |
| Join lines | 合併行 |
| Joined | 已合併 |
| just now | 啱啱 |
| Keep it | 保留 |
| Last seen | 最後見到 |
| Launch at sign-in. | 登入時啟動。 |
| Less | 收埋 |
| Level | 級別 |
| Line feed | 換行 |
| Link speed | 連線速度 |
| Local AI models | 本機 AI 模型 |
| Lock again | 再次上鎖 |
| Lock this element… | 鎖住呢個元素… |
| Locked | 已鎖 |
| locks | 鎖 |
| lookahead: the next text must match this, but it is not consumed | 前瞻：後面嘅文字必須配對呢個，但唔會被消耗 |
| lookbehind: the text just before must match this, and it is not consumed | 後顧：前面嘅文字必須配對呢個，而且唔會被消耗 |
| Markdown link | Markdown 連結 |
| Matches found | 搵到嘅配對 |
| Matches the visible tab label only. Nothing is closed until you press the button below, and the preview shows exactly which tabs would close. | 只會比對睇得到嘅分頁標籤。撳下面個掣之前唔會關閉任何嘢，預覽會清楚顯示邊啲分頁會被關閉。 |
| Maximize window | 最大化視窗 |
| Maximum players | 最多玩家數目 |
| Memory | 記憶體 |
| Message of the day | 每日訊息 |
| Minecraft EULA | Minecraft EULA |
| Minified | 已壓縮 |
| Mode: plain text, every word must appear | 模式：純文字，每個字都要出現 |
| Mode: plain text, every word must appear for a tab to be kept | 模式：純文字，分頁要包含每個字先會保留 |
| Mode: regular expression | 模式：正規表示式 |
| models | 模型 |
| Monitoring Tools has been removed. Bye for now. | Monitoring Tools 已經移除，再見。 |
| More tabs | 更多分頁 |
| More tabs, {0} hidden | 更多分頁，隱藏咗 {0} 個 |
| Move tab down | 分頁下移 |
| Move tab earlier | 分頁移前 |
| Move tab later | 分頁移後 |
| Move tab up | 分頁上移 |
| Move to | 移動到 |
| Move “{0}” into a group | 將「{0}」移入群組 |
| Move, copy, delete, Base64 and password encryption | 移動、複製、刪除、Base64 同密碼加密 |
| Moved | 已移動 |
| Move… into group… | 移入群組… |
| Moving | 移動緊 |
| My server | 我嘅伺服器 |
| Name must start with a letter: letters, digits, - and _ only, not 'push'. | 名稱要以字母開頭，只可以用字母、數字、- 同 _，而且唔可以係 'push'。 |
| Narrator is off. Switch on Read toasts aloud first. | 朗讀已關閉，請先開啟「朗讀通知」。 |
| negative lookahead: the next text must not match this | 負向前瞻：後面嘅文字唔可以配對呢個 |
| negative lookbehind: the text just before must not match this | 負向後顧：前面嘅文字唔可以配對呢個 |
| new address | 新地址 |
| New group | 新群組 |
| New group name | 新群組名稱 |
| New group with this tab… | 用呢個分頁建立新群組… |
| New group… | 新群組… |
| New tab | 新分頁 |
| New tab… | 新分頁… |
| No Cantonese voice is installed, so Cantonese is not spoken. | 冇安裝廣東話語音，所以唔會讀出廣東話。 |
| No English voice is installed on this computer. Add one in the Windows speech settings. | 呢部電腦冇安裝英文語音。請喺 Windows 語音設定新增。 |
| no group | 冇群組 |
| No groups yet. Create the first one below and the tab moves into it. | 仲未有群組。喺下面建立第一個，分頁就會移入去。 |
| No IP address from the router | 路由器冇分配 IP 位址 |
| No locks yet. Lock something from its right-click menu and it will be listed here. | 未有鎖。喺任何元素嘅右鍵選單揀「鎖住」，就會列喺呢度。 |
| no match | 冇配對 |
| No match in the sample. | 樣本入面冇配對。 |
| No model is loaded right now. | 而家冇載入任何模型。 |
| no motion, only warnings make a sound | 冇動態效果，只有警告先會發聲 |
| No network connection | 冇網絡連線 |
| No output. | 冇輸出。 |
| no published picture was found for the dishes tried | 試過嘅點心都搵唔到已發佈嘅圖片 |
| No route to the internet | 去唔到互聯網 |
| No system prompt. Fills the fields below with this profile's parameters. | 冇系統提示。用呢個設定檔嘅參數填入下面嘅欄位。 |
| No versions match. | 冇符合嘅版本。 |
| No voices match. | 冇符合嘅語音。 |
| None since the service started. Nice and quiet. | 服務啟動之後未斷過線，好平靜。 |
| None yet. | 暫時冇。 |
| Not a digit | 非數字 |
| Not a word boundary | 非字詞邊界 |
| Not a word character | 非字詞字元 |
| Not an embedding model | 唔係嵌入模型 |
| Not drawn this launch (each launch has a 10% chance). | 今次冇抽中（每次啟動有 10% 機會）。 |
| Not enough )'s. | 括號 ) 不夠。 |
| Not inside a git repository | 唔係喺 git 儲存庫入面 |
| Not inside a git repository. | 唔係喺 git 儲存庫入面。 |
| not selected | 未選取 |
| not set yet: the first :push will ask | 未設定：第一次 :push 會問你 |
| Not whitespace | 非空白字元 |
| Nothing runs now. You can edit the script, test it and delete it afterwards in the editor. | 而家唔會執行任何嘢。之後你可以喺編輯器修改、測試或者刪除腳本。 |
| Nothing saved yet. | 未有已儲存嘅項目。 |
| Nothing was written. | 冇寫入任何嘢。 |
| Nothing yet. Copy something and it shows up here. | 暫時未有嘢，複製啲嘢就會出現喺度。 |
| one character that is not a digit | 一個非數字字元 |
| one character that is not a word character | 一個非字詞字元 |
| one character that is not whitespace | 一個非空白字元 |
| one digit (a decimal digit in any script) | 一個數字（任何文字系統嘅十進制數字） |
| one popup on screen at once | 同一時間只顯示一個彈出通知 |
| one whitespace character (space, tab, line break and similar) | 一個空白字元（空格、Tab、換行等） |
| ongoing | 持續中 |
| Open in Notepad | 用記事本開啟 |
| Open in Paint | 用「繪圖」開啟 |
| Open logs | 開啟日誌 |
| Open sign-in page | 開啟登入頁面 |
| Open tabs | 已開分頁 |
| Open. Choosing it switches to its tab. | 已開啟，揀佢就會切換到佢嘅分頁。 |
| Opened | 已開啟 |
| Opens a terminal that follows the live logs. | 開啟終端機，即時跟住日誌。 |
| Opens in a new tab. | 會喺新分頁開啟。 |
| Otherwise | 否則 |
| Outage alerts off | 斷線警示已關 |
| Outage alerts on | 斷線警示已開 |
| Palette action failed:  | 面板操作失敗： |
| Paper | Paper |
| Password to turn off | 關閉用嘅密碼 |
| Pattern | 表達式 |
| Pick a version, a place to run it, and accept the EULA. Nothing is downloaded until you press Create. | 揀個版本、執行位置，同意 EULA。撳「建立」之前唔會下載任何嘢。 |
| Pick from the screen | 由螢幕選色 |
| Pin | 釘選 |
| PIN or password to turn off | 關閉用嘅 PIN 或密碼 |
| Pin tab | 釘選分頁 |
| PIN to turn off | 關閉用嘅 PIN |
| pinned | 已釘選 |
| Player versus player | 玩家對戰 (PvP) |
| Playful | 好玩 |
| Presentation | 簡報 |
| Press | 撳 |
| Primary | 主色 |
| Primary, e.g. 1.1.1.1 | 主要，例如 1.1.1.1 |
| Prompts once per run unless the service already has admin rights. | 除非服務已有管理員權限，否則每次執行都會問一次。 |
| Public IP changed | 公開 IP 已更改 |
| Push failed | 推送失敗 |
| Pushed. | 已推送。 |
| Pushing | 推送緊 |
| Pushing… | 推送緊… |
| Quiet while you play | 你玩緊嘢，所以靜晒 |
| reading source… | 讀取來源緊… |
| Reading the host key… | 讀取緊主機金鑰… |
| RECENT · click to copy, shift-click to move | 最近 · 撳一下複製，Shift 加撳一下移動 |
| Released and renewed. Checking again… | 已釋放並更新，再檢查一次… |
| Released, renewed and flushed DNS. | 已釋放、更新 IP 並清除 DNS 快取。 |
| Releasing and renewing the IP address… | 釋放同更新緊 IP 位址… |
| Remember across restarts. | 重新啟動後仍然記住。 |
| Remove from group | 從群組移除 |
| Remove group (keeps its tabs) | 移除群組（保留分頁） |
| Remove quotes | 移除引號 |
| Remove this lock | 移除呢個鎖 |
| Rename group | 重新命名群組 |
| Rename group… | 重新命名群組… |
| reopen on the page you left | 重新開啟喺你離開嗰頁 |
| Repeat the previous item | 重複前一項 |
| Replace file… | 更換檔案… |
| Restart | 重新啟動 |
| Restarts the running services in place. | 就地重新啟動行緊嘅服務。 |
| Run :mp4, :push and your own shortcuts when they land on the clipboard. | 當 :mp4、:push 同你自訂嘅捷徑出現喺剪貼簿，就會自動執行。 |
| Running the probe ladder now. | 而家行探測階梯。 |
| Runs the pattern over the sample in each mode this runtime really has: the default backtracking engine, the non-backtracking engine, ECMAScript mode and compiled. Each result shows whether the mode accepts the pattern, the match count, the first difference from the default engine and the time of the run. | 喺呢個執行環境真正有嘅每個模式，對樣本執行表達式：預設回溯引擎、非回溯引擎、ECMAScript 模式同編譯模式。每個結果都會顯示該模式接唔接受呢個表達式、配對數目、同預設引擎第一個差異，同執行時間。 |
| Runs: winget install -e --id Ollama.Ollama. Asks for your consent first. Default: not run unless you confirm. | 執行：winget install -e --id Ollama.Ollama。會先徵求你同意。預設：除非你確認，否則唔會執行。 |
| Safety and locks | 安全同鎖 |
| Same as group | 同群組一樣 |
| Samples | 範例 |
| Save + copy as file | 儲存並複製做檔案 |
| Save and push | 儲存並推送 |
| Save as .txt | 儲存為 .txt |
| Save JPG | 儲存 JPG |
| Save PNG | 儲存 PNG |
| Save profile | 儲存設定檔 |
| Save WAV | 儲存 WAV |
| Saved | 已儲存 |
| Saved. The next change pushes there. | 已儲存，下次變動就會推送去嗰度。 |
| schedule rules | 排程規則 |
| School mode, ADHD modes, the dim sum surprise and scheduled settings. | 學校模式、專注模式、點心驚喜同排程設定。 |
| Search all open tabs | 搜尋所有已開分頁 |
| Search all open tabs… | 搜尋所有已開分頁… |
| Search every open tab | 搜尋每個已開分頁 |
| Search group names | 搜尋群組名稱 |
| Search groups | 搜尋群組 |
| Search tab groups | 搜尋分頁群組 |
| Search tab groups… | 搜尋分頁群組… |
| Search tabs in this group | 搜尋呢個群組嘅分頁 |
| Search tabs in this strip | 搜尋呢個分頁列嘅分頁 |
| Search the group “{0}” | 搜尋群組「{0}」 |
| Search this group… | 搜尋呢個群組… |
| Search this tab strip | 搜尋呢個分頁列 |
| Search this tab strip… | 搜尋呢個分頁列… |
| Searches every open tab in this app. There is one window and one strip, shown on each result. | 搜尋呢個程式所有已開分頁。得一個視窗同一個分頁列，每個結果都會顯示。 |
| Searches every tab of this strip. Groups and pinned state are shown on each result. | 搜尋呢個分頁列嘅每個分頁。每個結果都會顯示群組同釘選狀態。 |
| Searches only the tabs of this group. It never looks at other groups. | 只搜尋呢個群組嘅分頁，唔會睇其他群組。 |
| Searches the names of tab groups. | 搜尋分頁群組嘅名稱。 |
| Secondary (optional) | 次要（可選） |
| Select none. | 全部不選。 |
| selected | 已選取 |
| Selected: | 已選： |
| Sending to the Recycle Bin… | 送去資源回收筒緊… |
| Settings and sections | 設定同分頁 |
| Settings · saved to server.properties, applied on the next start | 設定 · 儲存到 server.properties，下次啟動時生效 |
| Setup finished | 設定完成 |
| Show | 顯示 |
| Show a toast for every copy. | 每次複製都彈出通知。 |
| Show the dialog when the internet drops. | 上網中斷時顯示視窗。 |
| Shown | 已顯示 |
| Sign-in required | 需要登入 |
| signed-in accounts | 已登入嘅帳戶 |
| Simulation distance (chunks) | 模擬距離（區塊） |
| Snippets | 片段 |
| Some labels took too long to check and were left out. | 有啲標籤檢查太耐，已經略過。 |
| Spawn protection (blocks) | 出生點保護（方塊） |
| SSH host | SSH 主機 |
| Staging | 暫存緊 |
| Staging failed | 暫存失敗 |
| Starting point | 起點 |
| Starts at sign-in | 登入時啟動 |
| Stops and removes this project's containers and networks. Volumes stay. | 停止並移除呢個項目嘅容器同網絡，儲存區會保留。 |
| Switch | 切換 |
| Tab | Tab 鍵 |
| Tab options | 分頁選項 |
| tab strip on the {0} | 分頁列放喺{0}邊 |
| Tab strip position | 分頁列位置 |
| Tertiary | 第三色 |
| Text in the tab label | 分頁標籤內嘅文字 |
| Text the tab label must contain | 分頁標籤必須包含嘅文字 |
| That file could not be found. | 搵唔到嗰個檔案。 |
| That file could not be read, so nothing was changed. | 讀取唔到嗰個檔案，所以冇做任何更改。 |
| The adapter has an address but no default gateway, so traffic has nowhere to go. | 網絡卡有位址但冇預設閘道，所以資料無處可去。 |
| The adapter is connected but DHCP did not hand out an address. Release and renew usually fixes this. | 網絡卡已連線，但 DHCP 冇派位址俾你。釋放並更新通常可以解決。 |
| the appearance editor | 外觀編輯器 |
| the bell character (U+0007) | 響鈴字元 (U+0007) |
| The branch tried when the condition does not hold | 條件唔成立時試嘅分支 |
| The branch tried when the condition holds | 條件成立時試嘅分支 |
| The built-in catalogue is a dated snapshot, not the full library, and its sizes are typical figures. Ollama publishes no documented catalogue API, so the online search reads the public search page and can break if that page changes. Hardware verdicts are a rule of thumb. Chat attachments are limited to images and text files. | 內置目錄只係有日期嘅快照，並非完整模型庫，大小亦只係一般數字。Ollama 冇公開記載嘅目錄 API，所以網上搜尋係讀取公開搜尋頁面，頁面一改就可能失效。硬件評語只係經驗法則。對話附件只限圖片同文字檔。 |
| the catalogue | 目錄 |
| the changelog | 更新日誌 |
| the condition: this look-around test must succeed | 條件：呢個環顧測試必須成功 |
| The connection is back. | 連線恢復喇。 |
| the docs | 說明文件 |
| the end of the text, or before a final line break (of each line with Multiline) | 文字結尾，或者最後一個換行之前（用咗多行模式就係每一行嘅結尾） |
| the end of the text, or just before a final line break | 文字結尾，或者最後一個換行之前 |
| the escape character (U+001B) | 跳脫字元 (U+001B) |
| The file has a field this version does not know. | 檔案有呢個版本唔認識嘅欄位。 |
| The file has an unsafe or empty key. | 檔案有不安全或者空白嘅鍵。 |
| The file has no entries. | 檔案冇任何項目。 |
| The file is not UTF-8 text. | 檔案唔係 UTF-8 文字。 |
| The file is not valid JSON, or it is nested too deeply. | 檔案唔係有效嘅 JSON，或者巢狀層數太深。 |
| The file must contain one JSON object. | 檔案必須包含一個 JSON 物件。 |
| The file repeats a key. | 檔案有重複嘅鍵。 |
| the online library | 網上模型庫 |
| The passwords do not match. | 兩次密碼唔一致。 |
| The pattern is not valid, so nothing can close: | 表達式無效，所以唔會關閉任何嘢： |
| The pattern is too long to analyse. | 表達式太長，無法分析。 |
| the position where the previous match ended | 上一個配對結束嘅位置 |
| The prompt was declined or failed. | 提示被拒絕或者失敗。 |
| The server is not running | 伺服器冇運行 |
| The server is not running, so capabilities cannot be read. | 伺服器冇運行，所以讀唔到功能。 |
| the start of the text (of each line with Multiline) | 文字開頭（用咗多行模式就係每一行嘅開頭） |
| The two passwords do not match. | 兩個密碼唔一致。 |
| The two PINs do not match. | 兩個 PIN 唔一致。 |
| the very end of the text | 文字最尾 |
| the very start of the text, even with Multiline | 文字最頭，即使用咗多行模式 |
| The whole pattern | 成個表達式 |
| They go to the Recycle Bin, so you can restore them from there. | 檔案會去資源回收筒，可以喺嗰度還原。 |
| thinking | 思考 |
| This computer | 呢部電腦 |
| This copy does not carry its bundled FFmpeg. It can fetch it now and then run this job. | 呢個版本冇附帶 FFmpeg。可以即刻下載，再繼續呢個工作。 |
| This creates the shortcut :script and saves its script as script.ps1 in the scripts folder. | 呢個會建立捷徑 :script，並將佢嘅腳本以 script.ps1 儲存喺腳本資料夾。 |
| This item is locked and will not run until it is unlocked. | 呢個項目已鎖，解鎖之前唔會執行。 |
| This network shows a sign-in page before it lets traffic through (hotel, airport, café Wi-Fi). | 呢個網絡要先登入網頁先放行（例如酒店、機場、咖啡店 Wi-Fi）。 |
| This removes it from the list. Its world and files stay where they are, and a running server is stopped first. | 會由清單移除。世界同檔案會原封不動留低，行緊嘅伺服器會先停止。 |
| This stops the background service, removes it from start-up and deletes the program. Clipboard history is never stored, so nothing copied stays behind. | 會停止背景服務、由開機啟動移除，並刪除程式。剪貼簿紀錄從來唔會儲存，所以唔會留低任何複製內容。 |
| Ticket filed. | 已提交單據。 |
| tickets | 單據 |
| Tidied | 已整理 |
| Tidy spaces | 整理空格 |
| Times | 次數 |
| Title Case | 標題大小寫 |
| Toasts off | 通知已關 |
| Toasts on | 通知已開 |
| Translate | 翻譯 |
| Tray right-click menu | 系統匣右鍵選單 |
| Try winget install Gyan.FFmpeg in a terminal. | 試吓喺終端機行 winget install Gyan.FFmpeg。 |
| Turn off | 關閉 |
| unavailable while a mode that removes it is on | 有個會移除佢嘅模式開咗，所以用唔到 |
| Uninstalled | 已解除安裝 |
| unknown reason | 不明原因 |
| Unknown target. | 未知目標。 |
| Unlock this element | 解鎖呢個元素 |
| Unlock… | 解鎖… |
| Unpin | 取消釘選 |
| Unpin tab | 取消釘選分頁 |
| Unquoted | 已移除引號 |
| Use at least 8 characters. | 最少用 8 個字元。 |
| Valid pattern | 有效嘅表達式 |
| Vanilla | 原版 (Vanilla) |
| Vertical tab | 垂直 Tab |
| Very end of text | 文字最尾 |
| Video file | 影片檔案 |
| View distance (chunks) | 視距（區塊） |
| Vision | 視覺 |
| Voices installed | 已安裝語音 |
| Wake | 喚醒 |
| watching clipboard | 監察緊剪貼簿 |
| Watching:  | 監察緊： |
| Wave audio on the clipboard. Drag this line out to drop it as a .wav file. | 剪貼簿入面係 Wave 音訊。拖曳呢行出去就可以存成 .wav 檔案。 |
| Weekend server | 週末伺服器 |
| Welcome to Monitoring Tools | 歡迎使用 Monitoring Tools |
| Welcome tour | 歡迎導覽 |
| Where it runs | 執行位置 |
| Which edge of the control centre the tabs sit on. Default: left. | 分頁放喺控制中心邊一邊。預設：左。 |
| Wi-Fi is off or disconnected, the cable is unplugged, or airplane mode is on. | Wi-Fi 關咗或者斷咗線、網線拔咗，或者開咗飛行模式。 |
| Will not start at sign-in | 登入時唔會啟動 |
| Wording file refused | 用語檔案被拒絕 |
| working | 處理緊 |
| working… | 處理緊… |
| Your computer is connected, but the router does not answer and nothing gets past it. Try restarting the router. | 你部電腦有連線，但路由器冇回應，咩都出唔到去。試吓重新啟動路由器。 |
| Your router answers, but nothing beyond it does. This is usually an ISP or modem outage. | 路由器有回應，但再出面就冇。通常係網絡供應商或數據機出事。 |
| your shortcut | 你嘅捷徑 |
| {0} of {1} tabs would close. | {1} 個分頁中有 {0} 個會被關閉。 |
| {0} pinned tab is protected. | {0} 個已釘選分頁受保護。 |
| {0} pinned tabs are protected. | {0} 個已釘選分頁受保護。 |
| {0} tab | {0} 個分頁 |
| {0} tabs | {0} 個分頁 |
| ↑↓ to move | ↑↓ 移動 |
| ✓  The server is answering | ✓  伺服器有回應 |

## Strings produced by patterns

Built strings (counts, names, times, status lines) are not table entries: a pattern fills the parts that change. The rows below are the strings the audit met and what the patterns produced from them. The patterns themselves are in `Core/Strings.yue.cs` (older ones) and `Core/Strings.yue.Patterns.cs`.

| English as built | Cantonese produced | Seen on |
| --- | --- | --- |
| (?<name>\w+): Group "name" (2) | (?<name>\w+)：群組「name」（2） | Regex builder |
| (png\|md\|csv): Group 1 | (png\|md\|csv)：群組 1 | Regex builder |
| .NET 8.0.30 · System.Text.RegularExpressions · backtracking · 200 ms limit | .NET 8.0.30 · System.Text.RegularExpressions · 回溯 · 上限 200 毫秒 | Regex builder |
| 0 folders · 5 files · 475.9 KB · 4 .png, 1 .txt | 0 folders · 5 個檔案 · 475.9 KB · 4 .png, 1 .txt | Toasts |
| 0 of 15 | 0 / 15 | Search |
| 0 of 15 settings and sections | 0／15 個設定同分頁 | Search |
| 0 selected | 揀咗 0 個 | Bulk selection |
| 0.5B · 0.4 GB · 32K context · Qwen | 0.5B · 0.4 GB · 32K 上下文 · Qwen | Page: ollama |
| 02:14 / 04:46 · 3.1× · ~49 s left | 02:14 / 04:46 · 3.1× · 尚餘約 49 秒 | Toasts |
| 09:00 to 17:00, every day: sounds = off | 09:00 至 17:00，每日：sounds = off | Page: modes |
| 1 article | 1 篇文章 | Page: help |
| 1 h ago | 1 個鐘前 | Page: activity |
| 1 locks | 1 個鎖 | Page: safety |
| 1 m ago | 1 分鐘前 | Page: activity |
| 1 rules | 1 條規則 | Page: modes |
| 1.1B · 0.6 GB · 2K context · TinyLlama | 1.1B · 0.6 GB · 2K 上下文 · TinyLlama | Page: ollama |
| 1.5B · 1.0 GB · 32K context · Qwen | 1.5B · 1.0 GB · 32K 上下文 · Qwen | Page: ollama |
| 1.5B · 1.1 GB · 128K context · DeepSeek | 1.5B · 1.1 GB · 128K 上下文 · DeepSeek | Page: ollama |
| 10 m ago | 10 分鐘前 | Page: overview |
| 10 settings and sections | 10 個設定同分頁 | Page: clipboard |
| 100 x 100 chunks = 1600 x 1600 blocks, 10,000 chunks in 49 batches of at most 256 chunks, centred on chunk 0, 0 (block 0, 0) in each chosen dimension (the Overworld always, the Nether and the End if switched on below, with no coordinate scaling). Time and disk grow with the square of the radius. | 100 x 100 個區塊 = 1600 x 1600 格，共 10,000 個區塊，分 49 批，每批最多 256 個區塊，以區塊 0, 0（方塊 0, 0）為中心，用喺每個所揀嘅維度（主世界必定包括，如果下面開啟咗就包括地獄同終界，座標唔作縮放）。所需時間同磁碟空間會隨半徑嘅平方增長。 | Page: minecraft |
| 103 versions | 103 個版本 | Page: minecraft |
| 11 articles | 11 篇文章 | Page: help |
| 11 m ago | 11 分鐘前 | Element menu / ControlCentreWindow |
| 12 chars · 2 words | 12 個字元 · 2 個字 | Toasts |
| 12 settings and sections | 12 個設定同分頁 | Page: safety |
| 12B · 7.1 GB · 128K context · Mistral | 12B · 7.1 GB · 128K 上下文 · Mistral | Page: ollama |
| 12B · 8.1 GB · 128K context · Gemma | 12B · 8.1 GB · 128K 上下文 · Gemma | Page: ollama |
| 13 entries | 13 個項目 | Page: activity |
| 137M · 0.3 GB · 8K context · Nomic | 137M · 0.3 GB · 8K 上下文 · Nomic | Page: ollama |
| 14B · 9.0 GB · 128K context · DeepSeek | 14B · 9.0 GB · 128K 上下文 · DeepSeek | Page: ollama |
| 14B · 9.0 GB · 32K context · Qwen | 14B · 9.0 GB · 32K 上下文 · Qwen | Page: ollama |
| 14B · 9.1 GB · 16K context · Phi | 14B · 9.1 GB · 16K 上下文 · Phi | Page: ollama |
| 15 m | 15 分 | Tray menu |
| 15 m ago | 15 分鐘前 | Page: overview |
| 15 min | 15 分鐘 | Command palette |
| 15 settings and sections | 15 個設定同分頁 | Page: overview |
| 16 m ago | 16 分鐘前 | Element menu / ControlCentreWindow |
| 17 notifications | 17 個通知 | Page: records |
| 17 releases | 17 個版本 | Bulk selection |
| 17 releases fetched from GitHub just now. | 已由 GitHub 取得 17 個版本（啱啱）。 | Bulk selection |
| 18 games watched · fullscreen on | 監察緊 18 隻遊戲 · 全螢幕都計 | Page: overview |
| 1B · 1.3 GB · 128K context · Llama | 1B · 1.3 GB · 128K 上下文 · Llama | Page: ollama |
| 2 articles | 2 篇文章 | Page: help |
| 2 match(es) · 0.16 ms measured | 2 個配對 · 量度到 0.16 毫秒 | Regex builder |
| 2 of 15 | 2 / 15 | Regex builder |
| 2 of 15 settings and sections | 2／15 個設定同分頁 | Regex builder |
| 20 menu items | 20 個選單項目 | Tray menu |
| 20 settings and sections | 20 個設定同分頁 | Page: game |
| 2026-11-04 03:05:11  lasted 7 m 00 s  Router is not responding | 2026-11-04 03:05:11  維持咗 7 分 00 秒  路由器冇回應 | Page: activity |
| 2026-11-04 07:32:11  lasted 2 m 00 s  DNS lookups failing | 2026-11-04 07:32:11  維持咗 2 分 00 秒  DNS 查詢失敗 | Page: activity |
| 2026-11-04 08:03:48  ongoing  Selftest outage ongoing | 2026-11-04 08:03:48  持續中  Selftest outage ongoing | Page: activity |
| 233 chars · 36 words · 7 lines | 233 個字元 · 36 個字 · 7 行 | Toasts |
| 27B · 16.0 GB · 8K context · Gemma | 27B · 16.0 GB · 8K 上下文 · Gemma | Page: ollama |
| 27B · 17.0 GB · 128K context · Gemma | 27B · 17.0 GB · 128K 上下文 · Gemma | Page: ollama |
| 29 models | 29 個模型 | Page: ollama |
| 2B · 1.6 GB · 8K context · Gemma | 2B · 1.6 GB · 8K 上下文 · Gemma | Page: ollama |
| 3 articles | 3 篇文章 | Page: help |
| 3 files | 3 個檔案 | Toasts |
| 3 m ago | 3 分鐘前 | Page: overview |
| 3 settings and sections | 3 個設定同分頁 | Page: conversion |
| 3 text · 1 links · 1 files · 1 media | 3 段文字 · 1 條連結 · 1 個檔案 · 1 個媒體 | Page: overview |
| 3 voices | 3 把語音 | Page: language |
| 3 · Friendly | 3 · 友善 | Page: language |
| 3.8B · 2.2 GB · 128K context · Phi | 3.8B · 2.2 GB · 128K 上下文 · Phi | Page: ollama |
| 30 min | 30 分鐘 | Command palette |
| 32 m ago | 32 分鐘前 | Page: overview |
| 32B · 20.0 GB · 128K context · DeepSeek | 32B · 20.0 GB · 128K 上下文 · DeepSeek | Page: ollama |
| 32B · 20.0 GB · 32K context · Qwen | 32B · 20.0 GB · 32K 上下文 · Qwen | Page: ollama |
| 33 chars · 7 words | 33 個字元 · 7 個字 | Toasts |
| 33 m ago | 33 分鐘前 | Element menu / ControlCentreWindow |
| 34 m ago | 34 分鐘前 | Page: overview |
| 35 m ago | 35 分鐘前 | Element menu / ControlCentreWindow |
| 39 settings and sections | 39 個設定同分頁 | Page: ollama |
| 3B · 1.9 GB · 32K context · Qwen | 3B · 1.9 GB · 32K 上下文 · Qwen | Page: ollama |
| 3B · 2.0 GB · 128K context · Llama | 3B · 2.0 GB · 128K 上下文 · Llama | Page: ollama |
| 4 articles | 4 篇文章 | Page: help |
| 4 m ago | 4 分鐘前 | Element menu / ControlCentreWindow |
| 4 settings and sections | 4 個設定同分頁 | Page: service |
| 45 min | 45 分鐘 | Command palette |
| 49 m ago | 49 分鐘前 | Page: activity |
| 4B · 3.3 GB · 128K context · Gemma | 4B · 3.3 GB · 128K 上下文 · Gemma | Page: ollama |
| 5 accounts | 5 個帳戶 | Page: tools (forge host choice) |
| 5 accounts signed in. Tokens stay in the GitHub CLI's credential store; this app only reads the names. | 5 個帳戶已登入。權杖留喺 GitHub CLI 嘅憑證庫，呢個程式只會讀取名稱。 | Page: tools (forge host choice) |
| 5 articles | 5 篇文章 | Page: help |
| 5 items | 5 個項目 | Toasts |
| 5 settings and sections | 5 個設定同分頁 | Page: minecraft |
| 52 chars · 1 words | 52 個字元 · 1 個字 | Toasts |
| 53 commands | 53 個指令 | Command palette |
| 53 of 53 · ↑↓ to move · Enter to go · Esc to close · Alt+B regex builder | 53 / 53 · ↑↓ 移動 · Enter 前往 · Esc 關閉 · Alt+B 正規表示式建構器 | Command palette |
| 6 articles | 6 篇文章 | Page: help |
| 6 copies | 6 次複製 | Page: overview |
| 6 kept | 保留 6 個 | Tray menu |
| 6 settings and sections | 6 個設定同分頁 | Page: network |
| 6 shortcuts | 6 個捷徑 | Page: shortcuts |
| 60 min | 60 分鐘 | Command palette |
| 7 entries | 7 個項目 | Page: safety |
| 7 shortcuts | 7 個捷徑 | Guided forms (DNS profile, new shortcut, new server) |
| 70 settings and sections | 70 個設定同分頁 | Page: records |
| 70B · 43.0 GB · 128K context · Llama | 70B · 43.0 GB · 128K 上下文 · Llama | Page: ollama |
| 7B · 3.8 GB · 16K context · Code Llama | 7B · 3.8 GB · 16K 上下文 · Code Llama | Page: ollama |
| 7B · 4.1 GB · 32K context · Mistral | 7B · 4.1 GB · 32K 上下文 · Mistral | Page: ollama |
| 7B · 4.7 GB · 128K context · DeepSeek | 7B · 4.7 GB · 128K 上下文 · DeepSeek | Page: ollama |
| 7B · 4.7 GB · 32K context · LLaVA | 7B · 4.7 GB · 32K 上下文 · LLaVA | Page: ollama |
| 7B · 4.7 GB · 32K context · Qwen | 7B · 4.7 GB · 32K 上下文 · Qwen | Page: ollama |
| 8 articles | 8 篇文章 | Page: help |
| 8 settings and sections | 8 個設定同分頁 | Page: tools |
| 86 fonts | 86 款字體 | Appearance editor |
| 8B · 4.9 GB · 128K context · Llama | 8B · 4.9 GB · 128K 上下文 · Llama | Page: ollama |
| 90 articles | 90 篇文章 | Page: help |
| 90 min | 90 分鐘 | Command palette |
| 9B · 5.4 GB · 8K context · Gemma | 9B · 5.4 GB · 8K 上下文 · Gemma | Page: ollama |
| :backup · :backup finished in 1.8 s | :backup · :backup 用咗 1.8 秒完成 | Page: overview |
| \.: Literal "." | \.：字面「.」 | Regex builder |
| \w matches 'F' | \w 配對「F」 | Regex builder |
| \w matches 'i' | \w 配對「i」 | Regex builder |
| \w+: 1 or more | \w+：1 個或以上 | Regex builder |
| \w: Word character | \w：字詞字元 | Regex builder |
| About Alert after | 關於「連續失敗幾多次先警示」 | Page: network |
| About Also include clipboard history | 關於「同時包括剪貼簿紀錄」 | Page: records |
| About Any fullscreen app counts | 關於「任何全螢幕程式都算」 | Page: game |
| About App language | 關於「程式語言」 | Page: language |
| About Apply a copied wording file | 關於「複製咗用字檔就即刻套用」 | Page: service |
| About Check every | 關於「檢查間隔」 | Page: network |
| About Clipboard toasts | 關於「剪貼簿通知」 | Page: clipboard |
| About Create a new repository on GitHub | 關於「喺 GitHub 建立新儲存庫」 | Page: tools |
| About Default keep-alive when a chat sets none (default 5 minutes) | 關於「對話冇設定時嘅預設保持載入時間（預設 5 分鐘）」 | Page: ollama |
| About Density | 關於「密度」 | Page: appearance |
| About Emoji in dialogs | 關於「視窗內嘅表情符號」 | Page: language |
| About Focus | 關於「專注」 | Page: modes |
| About Focus spotlight | 關於「專注聚光燈」 | Page: modes |
| About Font family | 關於「字體系列」 | Appearance editor |
| About Generate structures | 關於「生成結構」 | Page: minecraft |
| About Glyph | 關於「圖案」 | Page: appearance |
| About Hide locked elements from page search | 關於「喺頁面搜尋隱藏已鎖元素」 | Page: safety |
| About High-contrast outlines | 關於「高對比輪廓」 | Page: appearance |
| About History size | 關於「紀錄數量」 | Page: clipboard |
| About Keep open on hover | 關於「滑鼠停留時保持開啟」 | Page: clipboard |
| About Letter spacing | 關於「字距」 | Appearance editor |
| About Logo tint | 關於「標誌色調」 | Page: appearance |
| About Low stimulation | 關於「低刺激」 | Page: modes |
| About Momentum | 關於「動力」 | Page: modes |
| About Network alerts | 關於「網絡警示」 | Page: network |
| About Offer Windows Hello | 關於「提供 Windows Hello」 | Page: safety |
| About One thing at a time | 關於「一次做一樣」 | Page: modes |
| About Owner of the new repository (default: your own account) | 關於「新儲存庫嘅擁有者（預設：你自己嘅帳戶）」 | Page: tools (forge host choice) |
| About PNG export size (default 1x) | 關於「PNG 匯出大小（預設 1 倍）」 | Page: tools |
| About Preferred editor (default automatic: VS Code, then Notepad++, then the system default) | 關於「偏好嘅編輯器（預設自動：先 VS Code，再 Notepad++，最後系統預設）」 | Page: tools |
| About Quick commands | 關於「快捷指令」 | Page: clipboard |
| About Read toasts aloud | 關於「朗讀通知」 | Page: language |
| About Reduce motion | 關於「減少動態效果」 | Page: appearance |
| About Reminder interval (default 30 minutes, used only with Time awareness) | 關於「提醒間隔（預設 30 分鐘，只喺開咗時間意識先用）」 | Page: modes |
| About Render Markdown | 關於「顯示 Markdown 格式」 | Page: clipboard |
| About Run as administrator | 關於「以系統管理員身分執行」 | Page: shortcuts |
| About Runs with | 關於「執行方式」 | Page: shortcuts |
| About Save chats automatically | 關於「自動儲存對話」 | Page: ollama |
| About Save the world as (default folder) | 關於「世界儲存為（預設資料夾）」 | Page: minecraft |
| About Seed colour | 關於「主色」 | Page: appearance |
| About Set the world border to this size | 關於「將世界邊界設為呢個大小」 | Page: minecraft |
| About Setting to change | 關於「要更改嘅設定」 | Page: modes |
| About Show a console window | 關於「顯示主控台視窗」 | Page: shortcuts |
| About Show the next code too | 關於「同時顯示下一個驗證碼」 | Page: safety |
| About Size | 關於「大小」 | Appearance editor |
| About Skip password managers | 關於「略過密碼管理員」 | Page: clipboard |
| About Sound effects | 關於「音效」 | Page: clipboard |
| About Start with the toolbar minimised | 關於「啟動時將工具列最小化」 | Page: tools |
| About Start with Windows | 關於「隨 Windows 啟動」 | Page: service |
| About Suppress while gaming | 關於「遊戲時收埋通知」 | Page: game |
| About Text size | 關於「文字大小」 | Page: appearance |
| About the appearance editor | 關於「外觀編輯器」 | Appearance editor |
| About Theme | 關於「主題」 | Page: appearance |
| About Time awareness | 關於「時間意識」 | Page: modes |
| About Toast duration | 關於「通知顯示時間」 | Page: clipboard |
| About Toolbar position (default top) | 關於「工具列位置（預設頂部）」 | Page: tools |
| About Underline | 關於「底線」 | Appearance editor |
| About Upload after committing | 關於「提交之後上載」 | Page: tools |
| About Value | 關於「數值」 | Page: modes |
| About Visibility of a new repository (default private) | 關於「新儲存庫嘅可見度（預設私人）」 | Page: tools |
| About Volume | 關於「音量」 | Page: clipboard |
| About Watch the public IP | 關於「監察公開 IP」 | Page: network |
| About Weight | 關於「粗幼」 | Appearance editor |
| About Where to publish (default GitHub) | 關於「發佈到邊度（預設 GitHub）」 | Page: tools |
| About World type (default Normal) | 關於「世界類型（預設普通）」 | Page: minecraft |
| Actions: Check public IP now | 操作：即刻檢查公開 IP | Command palette |
| Actions: Choose a personal wording file | 操作：揀個人用語檔案 | Command palette |
| Actions: Open IP history repository | 操作：開啟 IP 紀錄儲存庫 | Command palette |
| Actions: Open log folder | 操作：開啟日誌資料夾 | Command palette |
| Actions: Preview the offline dialog | 操作：預覽斷線視窗 | Command palette |
| Actions: Release and renew IP | 操作：釋放並更新 IP | Command palette |
| Actions: Restart service | 操作：重新啟動服務 | Command palette |
| Actions: Run setup again | 操作：重新執行設定 | Command palette |
| Actions: Show sample toasts | 操作：顯示範例通知 | Command palette |
| Actions: Snooze 1 hour | 操作：暫停一小時 | Command palette |
| Actions: Switch DNS to Automatic | 操作：轉 DNS 去 自動 | Command palette |
| Actions: Switch DNS to Cloudflare | 操作：轉 DNS 去 Cloudflare | Command palette |
| Actions: Switch DNS to Google | 操作：轉 DNS 去 Google | Command palette |
| Actions: Switch DNS to OpenDNS | 操作：轉 DNS 去 OpenDNS | Command palette |
| Actions: Switch DNS to Quad9 | 操作：轉 DNS 去 Quad9 | Command palette |
| Actions: Take the tour | 操作：睇導覽 | Command palette |
| Actions: Test connection now | 操作：即刻測試連線 | Command palette |
| Actions: Uninstall service | 操作：解除安裝服務 | Command palette |
| Add codellama:7b to the pull cart | 將 codellama:7b 加入下載購物車 | Page: ollama |
| Add deepseek-r1:1.5b to the pull cart | 將 deepseek-r1:1.5b 加入下載購物車 | Page: ollama |
| Add deepseek-r1:14b to the pull cart | 將 deepseek-r1:14b 加入下載購物車 | Page: ollama |
| Add deepseek-r1:32b to the pull cart | 將 deepseek-r1:32b 加入下載購物車 | Page: ollama |
| Add deepseek-r1:7b to the pull cart | 將 deepseek-r1:7b 加入下載購物車 | Page: ollama |
| Add gemma2:27b to the pull cart | 將 gemma2:27b 加入下載購物車 | Page: ollama |
| Add gemma2:2b to the pull cart | 將 gemma2:2b 加入下載購物車 | Page: ollama |
| Add gemma2:9b to the pull cart | 將 gemma2:9b 加入下載購物車 | Page: ollama |
| Add gemma3:12b to the pull cart | 將 gemma3:12b 加入下載購物車 | Page: ollama |
| Add gemma3:27b to the pull cart | 將 gemma3:27b 加入下載購物車 | Page: ollama |
| Add gemma3:4b to the pull cart | 將 gemma3:4b 加入下載購物車 | Page: ollama |
| Add llama3.1:8b to the pull cart | 將 llama3.1:8b 加入下載購物車 | Page: ollama |
| Add llama3.2:1b to the pull cart | 將 llama3.2:1b 加入下載購物車 | Page: ollama |
| Add llama3.2:3b to the pull cart | 將 llama3.2:3b 加入下載購物車 | Page: ollama |
| Add llama3.3:70b to the pull cart | 將 llama3.3:70b 加入下載購物車 | Page: ollama |
| Add llava:7b to the pull cart | 將 llava:7b 加入下載購物車 | Page: ollama |
| Add mistral-nemo:12b to the pull cart | 將 mistral-nemo:12b 加入下載購物車 | Page: ollama |
| Add mistral:7b to the pull cart | 將 mistral:7b 加入下載購物車 | Page: ollama |
| Add nomic-embed-text:latest to the pull cart | 將 nomic-embed-text:latest 加入下載購物車 | Page: ollama |
| Add phi3:mini to the pull cart | 將 phi3:mini 加入下載購物車 | Page: ollama |
| Add phi4:14b to the pull cart | 將 phi4:14b 加入下載購物車 | Page: ollama |
| Add qwen2.5-coder:7b to the pull cart | 將 qwen2.5-coder:7b 加入下載購物車 | Page: ollama |
| Add qwen2.5:0.5b to the pull cart | 將 qwen2.5:0.5b 加入下載購物車 | Page: ollama |
| Add qwen2.5:1.5b to the pull cart | 將 qwen2.5:1.5b 加入下載購物車 | Page: ollama |
| Add qwen2.5:14b to the pull cart | 將 qwen2.5:14b 加入下載購物車 | Page: ollama |
| Add qwen2.5:32b to the pull cart | 將 qwen2.5:32b 加入下載購物車 | Page: ollama |
| Add qwen2.5:3b to the pull cart | 將 qwen2.5:3b 加入下載購物車 | Page: ollama |
| Add qwen2.5:7b to the pull cart | 將 qwen2.5:7b 加入下載購物車 | Page: ollama |
| Add tinyllama:1.1b to the pull cart | 將 tinyllama:1.1b 加入下載購物車 | Page: ollama |
| ADHD mode · elapsed time and a gentle reminder | 專注力輔助模式 · 顯示用咗幾耐，同溫和提醒 | Command palette |
| ADHD mode · hold non-urgent popups in History | 專注力輔助模式 · 將唔緊急嘅彈出通知收入歷史 | Command palette |
| ADHD mode · no motion, only warnings make a sound | 專注力輔助模式 · 冇動態效果，只有警告先會發聲 | Command palette |
| ADHD mode · one popup on screen at once | 專注力輔助模式 · 同一時間只顯示一個彈出通知 | Command palette |
| ADHD mode · reopen on the page you left | 專注力輔助模式 · 重新開啟喺你離開嗰頁 | Command palette |
| Algorithm: SHA1 | 演算法：SHA1 | Page: safety |
| Algorithm: SHA256 | 演算法：SHA256 | Page: safety |
| Algorithm: SHA512 | 演算法：SHA512 | Page: safety |
| Alternative 1 | 選項 1 | Regex builder |
| Alternative 1 of 3 | 第 1 個選項，共 3 個 | Regex builder |
| Alternative 1 of 3. Enter selects it in the pattern; Left and Right collapse and expand. | 第 1 個選項，共 3 個。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| Alternative 2 | 選項 2 | Regex builder |
| Alternative 2 of 3 | 第 2 個選項，共 3 個 | Regex builder |
| Alternative 2 of 3. Enter selects it in the pattern; Left and Right collapse and expand. | 第 2 個選項，共 3 個。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| Alternative 3 | 選項 3 | Regex builder |
| Alternative 3 of 3 | 第 3 個選項，共 3 個 | Regex builder |
| Alternative 3 of 3. Enter selects it in the pattern; Left and Right collapse and expand. | 第 3 個選項，共 3 個。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| backreference to group 1 | 反向參照群組 1 | Regex builder |
| Black weight | 特粗字重 | Appearance editor |
| Blank batch file. Starts with the app's own template: %~1 receives whatever was before the colon, and one line prints it. | 空白批次檔。由程式自己嘅範本開始：%~1 接收冒號前面嘅任何內容，再加一行將佢顯示出嚟。 | Guided forms (DNS profile, new shortcut, new server) |
| Blank PowerShell script. Starts with the app's own template: a $Target parameter that receives whatever was before the colon, and one line that prints it. | 空白 PowerShell 腳本。由程式自己嘅範本開始：一個 $Target 參數，接收冒號前面嘅任何內容，再加一行將佢顯示出嚟。 | Guided forms (DNS profile, new shortcut, new server) |
| Bold weight | 粗體字重 | Appearance editor |
| Capturing group 1: remembers the text it matches | 擷取群組 1：記住佢配對到嘅文字 | Regex builder |
| Capturing group 1: remembers the text it matches (characters 14 to 26) | 擷取群組 1：記住佢配對到嘅文字（第 14 至 26 個字元） | Regex builder |
| Capturing group 1: remembers the text it matches. Enter selects it in the pattern; Left and Right collapse and expand. | 擷取群組 1：記住佢配對到嘅文字。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| Capturing group named "name" (number 2): remembers the text it matches | 具名擷取群組「name」（編號 2）：記住佢配對到嘅文字 | Regex builder |
| Capturing group named "name" (number 2): remembers the text it matches. Enter selects it in the pattern; Left and Right collapse and expand. | 具名擷取群組「name」（編號 2）：記住佢配對到嘅文字。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| Category: Forgot a lock PIN or password | 類別：唔記得鎖嘅 PIN 或密碼 | Page: safety |
| Category: Locked out by too many wrong tries | 類別：輸錯太多次而被鎖住 | Page: safety |
| Category: Lost my authenticator | 類別：遺失咗驗證器 | Page: safety |
| Category: Something else is locked | 類別：有其他嘢被鎖 | Page: safety |
| Checked against the .NET engine: same result (match at 72, length 10). | 已同 .NET 引擎核對：結果一樣（match at 72, length 10）。 | Regex builder |
| CMYK value | CMYK 數值 | Colour picker |
| color · 08:05:11 | 顏色 · 08:05:11 | Page: overview |
| Commands · optional | 指令 · 可選 | Setup wizard |
| Commit and push · Committed 3 files and pushed main | 提交並推送 · 已提交 3 個檔案並推送 main | Page: overview |
| Control centre · clipboard | 控制中心 · 剪貼簿 | Command palette |
| Control centre · game | 控制中心 · game | Command palette |
| Control centre · modes | 控制中心 · 模式 | Command palette |
| Control centre · network | 控制中心 · 網絡 | Command palette |
| Control centre · service | 控制中心 · 背景服務 | Command palette |
| Copy CMYK | 複製 CMYK | Colour picker |
| Copy HEX | 複製 HEX | Colour picker |
| Copy HSL | 複製 HSL | Colour picker |
| Copy HSV | 複製 HSV | Colour picker |
| Copy HWB | 複製 HWB | Colour picker |
| Copy LAB | 複製 LAB | Colour picker |
| Copy LCH | 複製 LCH | Colour picker |
| Copy OKLCH | 複製 OKLCH | Colour picker |
| Copy path. Puts the full path on the clipboard, without quotes, ready to paste. | 複製路徑。將完整路徑放上剪貼簿，唔帶引號，可以直接貼上。 | Guided forms (DNS profile, new shortcut, new server) |
| Copy RGB | 複製 RGB | Colour picker |
| Copy the CMYK value | 複製 CMYK 數值 | Colour picker |
| Copy the HEX value | 複製 HEX 數值 | Colour picker |
| Copy the HSL value | 複製 HSL 數值 | Colour picker |
| Copy the HSV value | 複製 HSV 數值 | Colour picker |
| Copy the HWB value | 複製 HWB 數值 | Colour picker |
| Copy the LAB value | 複製 LAB 數值 | Colour picker |
| Copy the LCH value | 複製 LCH 數值 | Colour picker |
| Copy the OKLCH value | 複製 OKLCH 數值 | Colour picker |
| Copy the RGB value | 複製 RGB 數值 | Colour picker |
| csv: Alternative 3 | csv：選項 3 | Regex builder |
| Decode 0 | 解碼 0 個 | File tools tab 2 |
| Default unlock time for the next lock you make: 1 hour | 你下一個建立嘅鎖預設解鎖時間：1 小時 | Page: safety |
| Default unlock time for the next lock you make: 15 minutes | 你下一個建立嘅鎖預設解鎖時間：15 分鐘 | Page: safety |
| Default unlock time for the next lock you make: 5 minutes | 你下一個建立嘅鎖預設解鎖時間：5 分鐘 | Page: safety |
| Default unlock time for the next lock you make: Until the app closes | 你下一個建立嘅鎖預設解鎖時間：直到程式關閉 | Page: safety |
| Delete 3 | 刪除 3 個 | File tools tab 1 |
| Digits: 6 | 位數：6 | Page: safety |
| Digits: 7 | 位數：7 | Page: safety |
| Digits: 8 | 位數：8 | Page: safety |
| Dim all but the card under focus or the mouse · Ctrl+Shift+Space | 將焦點或者滑鼠所在卡片以外嘅嘢全部調暗 · Ctrl+Shift+Space | Command palette |
| DNS · optional | DNS · 可選 | Setup wizard |
| docker compose up · Compose up finished | docker compose up · Compose 啟動完成 | Page: activity |
| down 0 m 38 s · next probe 0 s | 已斷線 0 分 38 秒 · 0 秒後再探測 | Offline dialog |
| Edit :backup | 編輯 :backup | Page: shortcuts |
| Edit :script | 編輯 :script | Guided forms (DNS profile, new shortcut, new server) |
| Encode 3 | 編碼 3 個 | File tools tab 2 |
| Encrypt 3 | 加密 3 個 | File tools tab 3 |
| Encrypted 2 items | 已加密 2 個項目 | Page: activity |
| Enter named group "name" | 進入具名群組「name」 | Regex builder |
| exactly 2 | 剛好 2 個 | Regex builder |
| Export: Activity | 匯出：活動紀錄 | Page: records |
| Export: Clipboard history | 匯出：剪貼簿紀錄 | Page: records |
| Export: Notifications | 匯出：通知 | Page: records |
| Export: Settings | 匯出：設定 | Page: records |
| Export: Settings history | 匯出：設定歷史 | Page: records |
| files · 08:05:11 | 檔案 · 08:05:11 | Page: overview |
| Fits on the GPU. Needs about 0.8 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 0.8 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 1.0 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 1.0 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 1.2 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 1.2 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 1.6 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 1.6 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 1.8 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 1.8 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 2.0 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 2.0 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 2.3 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 2.3 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 2.7 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 2.7 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 2.8 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 2.8 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 3.0 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 3.0 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 4.3 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 4.3 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 4.9 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 4.9 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 5.2 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 5.2 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 5.9 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 5.9 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 6.1 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 6.1 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Fits on the GPU. Needs about 6.7 GB; your best GPU reports 8.0 GB of memory. Should run fast. | 啱晒喺 GPU 度行。大約需要 6.7 GB；你最好嘅 GPU 報告有 8.0 GB 記憶體。應該行得快。 | Page: ollama |
| Format: {schemaVersion: 1, entries: {ordinary phrase: your words}} · up to 256 KiB | 格式：{schemaVersion: 1, entries: {一般詞語: 你嘅用字}} · up to 256 KiB | Page: service |
| Group "name" (2) | 群組「name」（2） | Regex builder |
| Group 1 | 群組 1 | Regex builder |
| Hash file. Computes the SHA-256 hash of the file, prints it, and copies it to the clipboard. | 檔案雜湊。計算檔案嘅 SHA-256 雜湊值，顯示出嚟，並複製到剪貼簿。 | Guided forms (DNS profile, new shortcut, new server) |
| HEX value | HEX 數值 | Colour picker |
| HSL value | HSL 數值 | Colour picker |
| HSV value | HSV 數值 | Colour picker |
| HWB value | HWB 數值 | Colour picker |
| i  Port 11434 is free | i  埠 11434 冇人用 | Page: ollama |
| Installed at %USERPROFILE%\Documents\GitHub\Monitoring-tools\.claude\worktrees\agent-abab4790887019552\src\MonitoringTools\bin\Debug / et8.0-windows10.0.19041.0\MonitoringTools.exe | 已安裝喺 %USERPROFILE%\Documents\GitHub\Monitoring-tools\.claude\worktrees\agent-abab4790887019552\src\MonitoringTools\bin\Debug / et8.0-windows10.0.19041.0\MonitoringTools.exe | Setup wizard |
| Iteration 1 of greedy + | greedy + 嘅第 1 次重複 | Regex builder |
| Iteration 2 of greedy + | greedy + 嘅第 2 次重複 | Regex builder |
| Iteration 3 of greedy + | greedy + 嘅第 3 次重複 | Regex builder |
| LAB value | LAB 數值 | Colour picker |
| LCH value | LCH 數值 | Colour picker |
| Literal "." | 字面「.」 | Regex builder |
| Locked · PIN · page selftest-page · made 2026-11-04 08:04 | 已鎖 · 釘選 · page selftest-page · made 2026-11-04 08:04 | Page: safety |
| markdown · 08:05:11 | Markdown · 08:05:11 | Page: overview |
| Match any one of 3 alternatives, trying them from left to right | 配對 3 個選項其中任何一個，由左至右逐個試 | Regex builder |
| Match any one of 3 alternatives, trying them from left to right. Enter selects it in the pattern; Left and Right collapse and expand. | 配對 3 個選項其中任何一個，由左至右逐個試。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| md: Alternative 2 | md：選項 2 | Regex builder |
| Medium weight | 中等字重 | Appearance editor |
| More · 1 | 更多 · 1 | Toasts |
| More · 3 | 更多 · 3 | Toasts |
| More · 4 | 更多 · 4 | Toasts |
| More · 5 | 更多 · 5 | Toasts |
| More · 6 | 更多 · 6 | Toasts |
| Not available in .NET: (?1) subroutine | .NET 冇提供：(?1) 子程序 | Regex builder |
| Not available in .NET: (?R) recursion | .NET 冇提供：(?R) 遞迴 | Regex builder |
| Not available in .NET: [[:alpha:]] POSIX class | .NET 冇提供：[[:alpha:]] POSIX 類別 | Regex builder |
| Not available in .NET: \U \L \E case conversion | .NET 冇提供：\U \L \E 大細楷轉換 | Regex builder |
| Not valid: '(unclosed' at offset 9. Not enough )'s. | 表達式無效：'(unclosed'，位置 9。括號 ) 不夠。 | Regex builder |
| Now using Google: 8.8.8.8 | 而家用緊 Google：8.8.8.8 | Setup wizard |
| OKLCH value | OKLCH 數值 | Colour picker |
| One word character (a letter, digit, connector such as _). Enter selects it in the pattern; Left and Right collapse and expand. | 一個字詞字元（字母、數字，或者 _ 呢類連接字元）。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| online · watching clipboard | 在線 · 監察緊剪貼簿 | Tray menu |
| Open folder. Opens the folder in Explorer. If the copied path is a file, its containing folder opens. | 開啟資料夾。喺檔案總管開啟資料夾。如果複製咗嘅路徑係檔案，就會開啟佢所在嘅資料夾。 | Guided forms (DNS profile, new shortcut, new server) |
| Open in editor | 用 editor 開啟 | Page: shortcuts |
| Open in Visual Studio Code | 用 Visual Studio Code 開啟 | Toasts |
| open in VS Code | 用 VS Code 開啟 | Page: shortcuts |
| Opens: Clipboard history | 開啟：剪貼簿紀錄 | Page: help |
| Opens: Game mode | 開啟：遊戲模式 | Page: help |
| Opens: Internet watch | 開啟：網絡監察 | Page: help |
| Opens: Minecraft servers | 開啟：Minecraft 伺服器 | Page: help |
| Opens: Push a repo from the clipboard | 開啟：由剪貼簿推送儲存庫 | Page: help |
| Opens: Regex builder | 開啟：正則表達式砌法 | Page: help |
| Pages: About and updates | 頁面：關於同更新 | Command palette |
| Pages: Activity | 頁面：活動紀錄 | Command palette |
| Pages: Clipboard history | 頁面：剪貼簿紀錄 | Command palette |
| Pages: Clipboard settings | 頁面：剪貼簿設定 | Command palette |
| Pages: Conversion | 頁面：轉檔 | Command palette |
| Pages: Game mode | 頁面：遊戲模式 | Command palette |
| Pages: Minecraft servers | 頁面：Minecraft 伺服器 | Command palette |
| Pages: Network and DNS | 頁面：網絡同 DNS | Command palette |
| Pages: Overview | 頁面：總覽 | Command palette |
| Pages: Service | 頁面：背景服務 | Command palette |
| Pages: Shortcuts and scripts | 頁面：捷徑同腳本 | Command palette |
| Pattern for Search overview, kept in step with the search box | 「搜尋總覽」嘅表達式，與搜尋框保持同步 | Regex builder |
| Period in seconds: 15 s | 週期（秒）：15 秒 | Page: safety |
| Period in seconds: 30 s | 週期（秒）：30 秒 | Page: safety |
| Period in seconds: 60 s | 週期（秒）：60 秒 | Page: safety |
| png: Alternative 1 | png：選項 1 | Regex builder |
| png\|md\|csv: One of 3 alternatives | png\|md\|csv：3 個選項其中之一 | Regex builder |
| Preview: Update my-repo on 2026-11-04 at 08:06 | 預覽：Update my-repo on 2026-11-04 at 08:06 | Setup wizard |
| previous held 5 h 59 m · last seen 08:05 | 上一個維持咗 5 h 59 m · last seen 08:05 | Toasts |
| Queues codellama:7b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 codellama:7b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues deepseek-r1:1.5b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 deepseek-r1:1.5b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues deepseek-r1:14b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 deepseek-r1:14b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues deepseek-r1:32b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 deepseek-r1:32b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues deepseek-r1:7b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 deepseek-r1:7b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues gemma2:27b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 gemma2:27b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues gemma2:2b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 gemma2:2b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues gemma2:9b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 gemma2:9b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues gemma3:12b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 gemma3:12b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues gemma3:27b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 gemma3:27b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues gemma3:4b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 gemma3:4b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues llama3.1:8b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 llama3.1:8b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues llama3.2:1b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 llama3.2:1b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues llama3.2:3b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 llama3.2:3b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues llama3.3:70b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 llama3.3:70b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues llava:7b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 llava:7b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues mistral-nemo:12b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 mistral-nemo:12b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues mistral:7b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 mistral:7b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues nomic-embed-text:latest in the pull cart below. Nothing is downloaded until you press Pull all. | 將 nomic-embed-text:latest 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues phi3:mini in the pull cart below. Nothing is downloaded until you press Pull all. | 將 phi3:mini 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues phi4:14b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 phi4:14b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues qwen2.5-coder:7b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 qwen2.5-coder:7b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues qwen2.5:0.5b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 qwen2.5:0.5b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues qwen2.5:1.5b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 qwen2.5:1.5b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues qwen2.5:14b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 qwen2.5:14b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues qwen2.5:32b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 qwen2.5:32b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues qwen2.5:3b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 qwen2.5:3b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues qwen2.5:7b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 qwen2.5:7b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| Queues tinyllama:1.1b in the pull cart below. Nothing is downloaded until you press Pull all. | 將 tinyllama:1.1b 排入下面嘅下載購物車。撳「全部下載」之前唔會下載任何嘢。 | Page: ollama |
| recent  #FF00D600 | 最近用過  #FF00D600 | Colour picker |
| recent  #FF33CC99 | 最近用過  #FF33CC99 | Colour picker |
| recent  #FF3B7BF1 | 最近用過  #FF3B7BF1 | Colour picker |
| recent  #FF3D7BF2 | 最近用過  #FF3D7BF2 | Colour picker |
| recent  #FF3D7CF2 | 最近用過  #FF3D7CF2 | Colour picker |
| recent  #FF3D7DF2 | 最近用過  #FF3D7DF2 | Colour picker |
| recent  #FFFF3366 | 最近用過  #FFFF3366 | Colour picker |
| recent  #FFFF8000 | 最近用過  #FFFF8000 | Colour picker |
| recent  #FFFF8001 | 最近用過  #FFFF8001 | Colour picker |
| recent  #FFFF8800 | 最近用過  #FFFF8800 | Colour picker |
| Regex builder for Search overview | 「搜尋總覽」嘅正規表示式建構器 | Regex builder |
| Regular weight | 正常字重 | Appearance editor |
| Release notes from GitHub. You are running v1.0.0. | 來自 GitHub 嘅版本說明。你而家用緊 v1.0.0。 | Page: records |
| Rename by date. Renames the file to start with its last-modified date as yyyy-MM-dd. A file that already starts with a date is left alone. | 按日期重新命名。將檔案重新命名，開頭加上最後修改日期（yyyy-MM-dd）。已經以日期開頭嘅檔案唔會處理。 | Guided forms (DNS profile, new shortcut, new server) |
| Repeat the previous item one or more times, as many times as possible | 重複前一項 一次或以上，盡量多重複 | Regex builder |
| Repeat the previous item one or more times, as many times as possible. Enter selects it in the pattern; Left and Right collapse and expand. | 重複前一項 一次或以上，盡量多重複。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| Resize image. Makes a copy of an image no wider than 1600 pixels, keeping its proportions, saved beside the original with -resized in the name. The original is untouched. | 縮放圖片。製作一張闊度唔超過 1600 像素嘅圖片副本，保持比例，儲存喺原圖旁邊，名稱加上 -resized。原圖保持不變。 | Guided forms (DNS profile, new shortcut, new server) |
| RGB value | RGB 數值 | Colour picker |
| Runs with: PowerShell (.ps1), set by the starting point you chose. | 執行方式：PowerShell (.ps1)，由你揀嘅起點決定。 | Guided forms (DNS profile, new shortcut, new server) |
| Runs, partly on the CPU. Needs about 10.8 GB; you have 29.8 GB of free RAM and 8.0 GB of GPU memory. It will work but slower than a full GPU fit. | 一部分喺 CPU 度行。大約需要 10.8 GB；你有 29.8 GB 可用 RAM 同 8.0 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。 | Page: ollama |
| Runs, partly on the CPU. Needs about 11.0 GB; you have 29.8 GB of free RAM and 8.0 GB of GPU memory. It will work but slower than a full GPU fit. | 一部分喺 CPU 度行。大約需要 11.0 GB；你有 29.8 GB 可用 RAM 同 8.0 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。 | Page: ollama |
| Runs, partly on the CPU. Needs about 18.9 GB; you have 29.8 GB of free RAM and 8.0 GB of GPU memory. It will work but slower than a full GPU fit. | 一部分喺 CPU 度行。大約需要 18.9 GB；你有 29.8 GB 可用 RAM 同 8.0 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。 | Page: ollama |
| Runs, partly on the CPU. Needs about 20.0 GB; you have 29.8 GB of free RAM and 8.0 GB of GPU memory. It will work but slower than a full GPU fit. | 一部分喺 CPU 度行。大約需要 20.0 GB；你有 29.8 GB 可用 RAM 同 8.0 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。 | Page: ollama |
| Runs, partly on the CPU. Needs about 23.5 GB; you have 29.8 GB of free RAM and 8.0 GB of GPU memory. It will work but slower than a full GPU fit. | 一部分喺 CPU 度行。大約需要 23.5 GB；你有 29.8 GB 可用 RAM 同 8.0 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。 | Page: ollama |
| Runs, partly on the CPU. Needs about 8.7 GB; you have 29.8 GB of free RAM and 8.0 GB of GPU memory. It will work but slower than a full GPU fit. | 一部分喺 CPU 度行。大約需要 8.7 GB；你有 29.8 GB 可用 RAM 同 8.0 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。 | Page: ollama |
| Runs, partly on the CPU. Needs about 9.8 GB; you have 29.8 GB of free RAM and 8.0 GB of GPU memory. It will work but slower than a full GPU fit. | 一部分喺 CPU 度行。大約需要 9.8 GB；你有 29.8 GB 可用 RAM 同 8.0 GB GPU 記憶體。行得到，不過會慢過完全放入 GPU 嘅情況。 | Page: ollama |
| SAVED · 3 | 已儲存 · 3 | Regex builder |
| Search about and updates | 搜尋關於同更新 | Page: about |
| Search accounts | 搜尋帳戶 | Page: safety |
| Search activity | 搜尋活動紀錄 | Page: activity |
| Search appearance | 搜尋外觀 | Page: appearance |
| Search clipboard | 搜尋剪貼簿 | Page: clipboard |
| Search clipboard history | 搜尋剪貼簿紀錄 | Page: history |
| Search clipboard history with a pattern | 搜尋剪貼簿紀錄（用正規表示式） | Page: history |
| Search conversion | 搜尋轉檔 | Page: conversion |
| SEARCH FILES | 搜尋檔案 | Regex builder |
| Search fonts | 搜尋字體 | Appearance editor |
| Search game mode | 搜尋遊戲模式 | Page: game |
| Search game rules | 搜尋遊戲規則 | Page: minecraft |
| Search history | 搜尋歷史 | Page: safety |
| Search installed models | 搜尋已安裝嘅模型 | Page: ollama |
| Search language and voice | 搜尋語言同語音 | Page: language |
| Search local ai models | 搜尋本機 AI 模型 | Page: ollama |
| Search locks | 搜尋鎖 | Page: safety |
| Search minecraft servers | 搜尋Minecraft 伺服器 | Page: minecraft |
| Search modes | 搜尋模式 | Page: modes |
| Search network and dns | 搜尋網絡同 DNS | Page: network |
| Search notifications | 搜尋通知 | Page: records |
| Search overview | 搜尋總覽 | Page: overview |
| Search records and exports | 搜尋紀錄同匯出 | Page: records |
| Search safety and locks | 搜尋安全同鎖 | Page: safety |
| Search saved chats | 搜尋已儲存嘅對話 | Page: ollama |
| Search schedule rules | 搜尋排程規則 | Page: modes |
| Search service | 搜尋背景服務 | Page: service |
| Search shortcuts and scripts | 搜尋捷徑同腳本 | Page: shortcuts |
| Search signed-in accounts | 搜尋已登入嘅帳戶 | Page: tools |
| Search the catalogue | 搜尋目錄 | Page: ollama |
| Search the changelog | 搜尋更新日誌 | Page: records |
| Search the docs | 搜尋說明文件 | Page: help |
| Search the online library | 搜尋網上模型庫 | Page: ollama |
| Search tickets | 搜尋單據 | Page: safety |
| Search tools | 搜尋工具 | Page: tools |
| Select 3 files | 揀 3 個檔案 | Bulk selection |
| Select Bulk drive notice 1 | 揀 Bulk drive notice 1 | Bulk selection |
| Select Color | 揀 顏色 | Bulk selection |
| Select cs2 | 揀 cs2 | Bulk selection |
| Select Document | 揀 文件 | Bulk selection |
| Select Done | 揀 完成 | Bulk selection |
| Select dota2 | 揀 dota2 | Bulk selection |
| Select eldenring | 揀 eldenring | Bulk selection |
| Select Email | 揀 電郵 | Bulk selection |
| Select every games the search currently shows. Rows the search hides stay unselected. | 揀晒搜尋目前顯示嘅遊戲。被搜尋隱藏嘅列唔會被揀。 | Bulk selection |
| Select every notifications the search currently shows. Rows the search hides stay unselected. | 揀晒搜尋目前顯示嘅通知。被搜尋隱藏嘅列唔會被揀。 | Bulk selection |
| Select Folder | 揀 資料夾 | Bulk selection |
| Select FortniteClient-Win64-Shipping | 揀 FortniteClient-Win64-Shipping | Bulk selection |
| Select GTA5 | 揀 GTA5 | Bulk selection |
| Select Image file | 揀 圖片檔案 | Bulk selection |
| Select javaw:Minecraft | 揀 javaw:Minecraft | Bulk selection |
| Select JSON | 揀 JSON | Bulk selection |
| Select League of Legends | 揀 League of Legends | Bulk selection |
| Select LeagueClient | 揀 LeagueClient | Bulk selection |
| Select LeagueClientUx | 揀 LeagueClientUx | Bulk selection |
| Select Link | 揀 連結 | Bulk selection |
| Select Markdown | 揀 Markdown | Bulk selection |
| Select Minecraft.Windows | 揀 Minecraft.Windows | Bulk selection |
| Select MinecraftLauncher | 揀 MinecraftLauncher | Bulk selection |
| Select Overwatch | 揀 Overwatch | Bulk selection |
| Select Personal wording applied | 揀 個人用語已套用 | Bulk selection |
| Select r5apex | 揀 r5apex | Bulk selection |
| Select RobloxPlayer | 揀 RobloxPlayer | Bulk selection |
| Select RobloxPlayerBeta | 揀 RobloxPlayerBeta | Bulk selection |
| Select RobloxStudioBeta | 揀 RobloxStudioBeta | Bulk selection |
| Select RocketLeague | 揀 RocketLeague | Bulk selection |
| Select Text | 揀 文字 | Bulk selection |
| Select VALORANT-Win64-Shipping | 揀 VALORANT-Win64-Shipping | Bulk selection |
| SELECTED: (PNG\|MD\|CSV) | 已選：(PNG\|MD\|CSV) | Regex builder |
| Semibold weight | 半粗字重 | Appearance editor |
| Settings: Any fullscreen app counts | 設定：任何全螢幕程式都算 | Command palette |
| Settings: Check every | 設定：檢查間隔 | Command palette |
| Settings: Clipboard toasts | 設定：剪貼簿通知 | Command palette |
| Settings: Focus | 設定：專注 | Command palette |
| Settings: Focus spotlight | 設定：專注聚光燈 | Command palette |
| Settings: Keep open on hover | 設定：滑鼠停留時保持開啟 | Command palette |
| Settings: Low stimulation | 設定：低刺激 | Command palette |
| Settings: Momentum | 設定：動力 | Command palette |
| Settings: Network alerts | 設定：網絡警示 | Command palette |
| Settings: One thing at a time | 設定：一次做一樣 | Command palette |
| Settings: Personal wording | 設定：個人用語 | Command palette |
| Settings: Quick commands | 設定：快捷指令 | Command palette |
| Settings: Reminder interval | 設定：提醒間隔 | Command palette |
| Settings: Render Markdown | 設定：顯示 Markdown 格式 | Command palette |
| Settings: Skip password managers | 設定：略過密碼管理員 | Command palette |
| Settings: Sound effects | 設定：音效 | Command palette |
| Settings: Start with Windows | 設定：隨 Windows 啟動 | Command palette |
| Settings: Suppress while gaming | 設定：遊戲時收埋通知 | Command palette |
| Settings: Time awareness | 設定：時間意識 | Command palette |
| Settings: Toast duration | 設定：通知顯示時間 | Command palette |
| Settings: Volume | 設定：音量 | Command palette |
| Settings: Watch the public IP | 設定：監察公開 IP | Command palette |
| Severity: Critical (will be treated as Normal) | 嚴重程度：嚴重（會當做普通處理） | Page: safety |
| Severity: High | 嚴重程度：高 | Page: safety |
| Severity: Low | 嚴重程度：細聲 | Page: safety |
| Severity: Normal | 嚴重程度：普通 | Page: safety |
| Show only IP changed notifications | 只顯示「IP 已更改」通知 | Page: records |
| Show only Notice notifications | 只顯示「提示」通知 | Page: records |
| Show only Offline notifications | 只顯示「離線」通知 | Page: records |
| Show only Toast notifications | 只顯示「通知」通知 | Page: records |
| Show the Benchmark view | 顯示「效能測試」檢視 | Regex builder |
| Show the Build view | 顯示「建構」檢視 | Regex builder |
| Show the Code view | 顯示「程式碼」檢視 | Regex builder |
| Show the Compare view | 顯示「比較」檢視 | Regex builder |
| Show the Debug view | 顯示「偵錯」檢視 | Regex builder |
| Show the Files view | 顯示「檔案」檢視 | Regex builder |
| Show the Library view | 顯示「資料庫」檢視 | Regex builder |
| Show the Replace view | 顯示「取代」檢視 | Regex builder |
| Show the Test view | 顯示「測試」檢視 | Regex builder |
| Showing the built-in snapshot of 29 models from 2026-11-04. | 顯示內置嘅 29 個模型快照（2026-11-04）。 | Page: ollama |
| Showing the newest 50 of 71 snapshots. | 顯示最新 50 個，共 71 個快照。 | Page: records |
| Step 1 of 1,798 at text position 0: Start a match attempt at position 0 | 第 1 步，共 1,798 步，文字位置 0：喺位置 0 開始一次配對嘗試 | Regex builder |
| Step 1 of 3 | 第 1 步，共 3 步 | Guided forms (DNS profile, new shortcut, new server) |
| Step 1 of 4 | 第 1 步，共 4 步 | Page: minecraft |
| STEP 1 OF 7 | 第 1 步，共 7 步 | Setup wizard |
| Step 1: Start-up, current | 第 1 步：啟動，目前 | Setup wizard |
| Step 1: Start-up, done | 第 1 步：啟動，已完成 | Setup wizard |
| Step 2 of 4 | 第 2 步，共 4 步 | Guided forms (DNS profile, new shortcut, new server) |
| STEP 2 OF 7 | 第 2 步，共 7 步 | Setup wizard |
| Step 2: Clipboard | 第 2 步：剪貼簿 | Setup wizard |
| Step 2: Clipboard, current | 第 2 步：剪貼簿，目前 | Setup wizard |
| Step 2: Clipboard, done | 第 2 步：剪貼簿，已完成 | Setup wizard |
| Step 3 of 4 | 第 3 步，共 4 步 | Guided forms (DNS profile, new shortcut, new server) |
| STEP 3 OF 7 | 第 3 步，共 7 步 | Setup wizard |
| Step 3: Connection | 第 3 步：連線 | Setup wizard |
| Step 3: Connection, current | 第 3 步：連線，目前 | Setup wizard |
| Step 3: Connection, done | 第 3 步：連線，已完成 | Setup wizard |
| Step 4 of 4 | 第 4 步，共 4 步 | Guided forms (DNS profile, new shortcut, new server) |
| STEP 4 OF 7 · OPTIONAL | 第 4 步，共 7 步 · OPTIONAL | Setup wizard |
| Step 4: DNS | 第 4 步：DNS | Setup wizard |
| Step 4: DNS, current | 第 4 步：DNS，目前 | Setup wizard |
| Step 4: DNS, done | 第 4 步：DNS，已完成 | Setup wizard |
| STEP 5 OF 7 | 第 5 步，共 7 步 | Setup wizard |
| Step 5: Game mode | 第 5 步：遊戲模式 | Setup wizard |
| Step 5: Game mode, current | 第 5 步：遊戲模式，目前 | Setup wizard |
| Step 5: Game mode, done | 第 5 步：遊戲模式，已完成 | Setup wizard |
| STEP 6 OF 7 · OPTIONAL | 第 6 步，共 7 步 · OPTIONAL | Setup wizard |
| Step 6: Commands | 第 6 步：指令 | Setup wizard |
| Step 6: Commands, current | 第 6 步：指令，目前 | Setup wizard |
| Step 6: Commands, done | 第 6 步：指令，已完成 | Setup wizard |
| STEP 7 OF 7 | 第 7 步，共 7 步 | Setup wizard |
| Step 7: Done | 第 7 步：完成 | Setup wizard |
| Step 7: Done, current | 第 7 步：完成，目前 | Setup wizard |
| Switch DNS to Automatic | 轉 DNS 去 自動 | Command palette |
| Switch DNS to Cloudflare | 轉 DNS 去 Cloudflare | Command palette |
| Switch DNS to Google | 轉 DNS 去 Google | Command palette |
| Switch DNS to OpenDNS | 轉 DNS 去 OpenDNS | Command palette |
| Switch DNS to Quad9 | 轉 DNS 去 Quad9 | Command palette |
| Task: Build a regex | 任務：砌正規表示式 | Page: help |
| Task: Convert a video | 任務：轉換影片 | Page: help |
| Task: Find something you copied | 任務：搵返複製過嘅嘢 | Page: help |
| Task: Fix a broken connection | 任務：修復斷咗嘅連線 | Page: help |
| Task: Keep popups quiet while gaming | 任務：打機嗰陣收埋彈出通知 | Page: help |
| Task: Make your own command | 任務：自訂指令 | Page: help |
| Task: Push a repo from the clipboard | 任務：由剪貼簿推送儲存庫 | Page: help |
| Task: Run a Minecraft server | 任務：運行 Minecraft 伺服器 | Page: help |
| text · 08:05:10 | 文字 · 08:05:10 | Page: history |
| The last 50 copies, kept in memory only. Quitting the app forgets them. | 最近 50 次複製，只留喺記憶體。退出程式就會忘記。 | Page: history |
| The literal character "." | 字面字元「.」 | Regex builder |
| The literal character ".". Enter selects it in the pattern; Left and Right collapse and expand. | 字面字元「.」。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| The text "csv" | 文字「csv」 | Regex builder |
| The text "csv". Enter selects it in the pattern; Left and Right collapse and expand. | 文字「csv」。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| The text "md" | 文字「md」 | Regex builder |
| The text "md". Enter selects it in the pattern; Left and Right collapse and expand. | 文字「md」。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| The text "png" | 文字「png」 | Regex builder |
| The text "png". Enter selects it in the pattern; Left and Right collapse and expand. | 文字「png」。按 Enter 會喺表達式中選取；按左右方向鍵可以收合同展開。 | Regex builder |
| This creates the shortcut :script and saves its script as script.ps1 in the scripts folder. / Starting point: Blank PowerShell script. Runs with PowerShell. / Console window: hidden. Administrator rights: not requested. / Nothing runs now. You can edit the script, test it and delete it afterwards in the editor. | 呢個會建立捷徑 :script，並將佢嘅腳本以 script.ps1 儲存喺腳本資料夾。 / 起點：空白 PowerShell 腳本。用 PowerShell 執行。 / 主控台視窗：隱藏。管理員權限：不要求。 / 而家唔會執行任何嘢。之後你可以喺編輯器修改、測試或者刪除腳本。 | Guided forms (DNS profile, new shortcut, new server) |
| Tight: close other programs. Needs about 49.9 GB, you have 29.8 GB free of 63.8 GB RAM. It fits only if other programs release memory, and may swap to disk. | 好緊：請關閉其他程式。大約需要 49.9 GB，你有 29.8 GB 可用（總共 63.8 GB RAM）。要其他程式釋放記憶體先放得入，而且可能會用到磁碟交換。 | Page: ollama |
| Times use this computer's clock in (UTC-05:00) Eastern Time (US & Canada). A window that crosses midnight belongs to the day it starts on. Equal start and end mean all day. Daylight-saving changes follow the wall clock, so a window inside a skipped hour does not happen that day. If rules overlap on one setting, the one lower in the list wins. | 時間跟隨呢部電腦嘅時鐘（(UTC-05:00) Eastern Time (US & Canada)）。跨過午夜嘅時段屬於佢開始嗰一日。開始同結束時間一樣即係成日。夏令時間跟住牆鐘變，所以落入被跳過嗰個鐘嘅時段嗰日唔會發生。如果多條規則重疊喺同一個設定，排喺清單較後嗰條優先。 | Page: modes |
| Topic: Appearance | 主題：外觀 | Page: help |
| Topic: Appearance editor, colour picker, info icons and guided forms | 主題：外觀編輯器、顏色選擇器、資訊圖示同引導式表單 | Page: help |
| Topic: Clipboard | 主題：剪貼簿 | Page: help |
| Topic: Commands | 主題：指令 | Page: help |
| Topic: Files and Docker | 主題：檔案同 Docker | Page: help |
| Topic: Game mode | 主題：遊戲模式 | Page: help |
| Topic: Language and voice | 主題：語言同語音 | Page: help |
| Topic: Minecraft | 主題：Minecraft | Page: help |
| Topic: Modes | 主題：模式 | Page: help |
| Topic: Network | 主題：網絡 | Page: help |
| Topic: Ollama | 主題：Ollama | Page: help |
| Topic: Records and exports | 主題：紀錄同匯出 | Page: help |
| Topic: required-features | 主題：必備功能 | Page: help |
| Topic: Safety | 主題：安全 | Page: help |
| Topic: Search | 主題：搜尋 | Page: help |
| Topic: Tabs | 主題：分頁 | Page: help |
| Topic: The app itself | 主題：程式本身 | Page: help |
| Topic: Tools | 主題：工具 | Page: help |
| Toronto, CA · seen 2 times before | Toronto, CA · 之前見過 2 次 | Toasts |
| Unavailable: choose a version first. | Unavailable：請先揀版本。 | Page: minecraft |
| Use colour onPrimary | 用顏色 onPrimary | Colour picker |
| Use colour onPrimaryContainer | 用顏色 onPrimaryContainer | Colour picker |
| Use colour onSecondaryContainer | 用顏色 onSecondaryContainer | Colour picker |
| Use colour onSurface | 用顏色 onSurface | Colour picker |
| Use colour onSurfaceVariant | 用顏色 onSurfaceVariant | Colour picker |
| Use colour onTertiaryContainer | 用顏色 onTertiaryContainer | Colour picker |
| Use colour outline | 用顏色 outline | Colour picker |
| Use colour outlineVariant | 用顏色 outlineVariant | Colour picker |
| Use colour primary | 用顏色 primary | Colour picker |
| Use colour primaryContainer | 用顏色 primaryContainer | Colour picker |
| Use colour recent | 用顏色 recent | Colour picker |
| Use colour secondaryContainer | 用顏色 secondaryContainer | Colour picker |
| Use colour surface | 用顏色 surface | Colour picker |
| Use colour surfaceContainer | 用顏色 surfaceContainer | Colour picker |
| Use colour surfaceContainerHigh | 用顏色 surfaceContainerHigh | Colour picker |
| Use colour surfaceContainerHighest | 用顏色 surfaceContainerHighest | Colour picker |
| Use colour surfaceContainerLow | 用顏色 surfaceContainerLow | Colour picker |
| Use colour tertiaryContainer | 用顏色 tertiaryContainer | Colour picker |
| Valid pattern · 2 capturing group(s) | 有效嘅表達式 · 2 個擷取群組 | Regex builder |
| Valid · 12 node(s) · 2 capturing group(s) | 有效 · 12 個節點 · 2 個擷取群組 | Regex builder |
| valid · 2 top-level keys | 有效 · 2 個頂層項目 | Toasts |
| Watching: Roblox, Minecraft, javaw, League of Legends, Valorant, Fortnite, Counter-Strike 2, Dota 2, Apex Legends, RocketLeague. Edit the list in the control centre. | 監察緊：Roblox, Minecraft, javaw, League of Legends, Valorant, Fortnite, Counter-Strike 2, Dota 2, Apex Legends, RocketLeague。可以喺控制中心編輯清單。 | Setup wizard |
| Wed 03:05 | 星期三 03:05 | Page: overview |
| Wed 07:32 | 星期三 07:32 | Page: overview |
| Wed 08:03 · ongoing | 星期三 08:03 · 持續中 | Page: overview |
| Wi-Fi · Intel Wi-Fi 6E AX211 | Wi-Fi · Intel Wi-Fi 6E AX211 | Offline dialog |
| Write as: CSV | 寫成：CSV | Page: records |
| Write as: HTML | 寫成：HTML | Page: records |
| Write as: JSON | 寫成：JSON | Page: records |
| Write as: Markdown | 寫成：Markdown | Page: records |
| Write as: Plain text | 寫成：純文字 | Page: records |
| Zip it. Creates a .zip next to the file or folder, named after it, using the tar tool that ships with Windows. | 壓縮成 ZIP。喺檔案或資料夾旁邊建立一個 .zip，用同一個名稱，使用 Windows 內置嘅 tar 工具。 | Guided forms (DNS profile, new shortcut, new server) |
| ✓ System default — whatever Windows opens this file type with | ✓ 系統預設 — Windows 用嚟開啟呢類檔案嘅程式 | Page: tools |
| ✕ Notepad++ — not found | ✕ Notepad++ — 未搵到 | Page: tools |
| ✕ Visual Studio Code Insiders — not found | ✕ Visual Studio Code Insiders — 未搵到 | Page: tools |
