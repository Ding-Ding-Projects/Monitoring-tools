using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>Hong Kong Cantonese for the media file tool dialogs: trim, GIF, audio, picture resize, privacy clean-up and icon making.
    /// File names, formats, units and numbers stay as they are.</summary>
    internal static partial class Strings
    {
        private static void AddMedia(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);

            // ---- tool names and buttons
            Add("Trim and cut video", "剪裁影片"); Add("Video to GIF or short clip", "影片轉 GIF 或短片"); Add("Trim, fade and loudness", "剪裁、淡入淡出同響度");
            Add("Resize, crop and compress", "縮放、裁切同壓縮"); Add("Image privacy clean-up", "圖片私隱清理"); Add("Make an icon", "製作圖示");
            Add("Trim", "剪裁"); Add("Make GIF", "製作 GIF"); Add("Make WebP", "製作 WebP"); Add("Make MP4", "製作 MP4"); Add("Save", "儲存");
            Add("Save copies", "儲存副本"); Add("Save 1 copy", "儲存 1 個副本"); Add("Remove selected", "移除所揀"); Add("Make icon", "製作圖示");
            Add("Choose folder", "揀資料夾"); Add("Save in…", "儲存到…"); Add("Select all", "全選"); Add("Show in folder", "喺資料夾顯示");
            Add("Play", "播放"); Add("Pause", "暫停"); Add("Copy file", "複製檔案"); Add("Copy path", "複製路徑"); Add("Copy details", "複製詳情");
            Add("Trim another section", "再剪另一段"); Add("Make another", "再整一個"); Add("Add pictures", "加入圖片");
            Add("Retry the failed one", "重試失敗嗰張"); Add("Retry the failed ones", "重試失敗嗰幾張"); Add("Check the copy", "檢查副本");
            Add("Switch to Short MP4", "轉用短 MP4"); Add("Back to the settings", "返回設定"); Add("Try again with exact cut", "用精確剪裁再試");
            Add("Turn off 128 and 256", "關閉 128 同 256"); Add("Trim…", "剪裁…"); Add("To GIF…", "轉 GIF…"); Add("Edit audio…", "編輯音訊…");
            Add("Resize…", "縮放…"); Add("Privacy…", "私隱…"); Add("Make icon…", "製作圖示…");
            Add("Keep or cut out a part of this video. A cut on key frames copies the video with no quality loss.", "保留或者剪走影片其中一段。喺關鍵格剪嘅話，影片直接複製，唔會失真。");
            Add("Make an animated GIF, animated WebP or short MP4 from part of this video, optionally under a size limit.", "用影片其中一段做動態 GIF、動態 WebP 或者短 MP4，仲可以限制檔案大小。");
            Add("Trim, fade in and out, and set the loudness of this audio.", "剪裁、淡入淡出，同調較呢段音訊嘅響度。");
            Add("Resize, crop and compress these pictures. The originals are kept.", "縮放、裁切同壓縮呢啲圖片，原圖會保留。");
            Add("See and remove location, camera, date and author data hidden in these pictures.", "睇吓同移除圖片入面收埋嘅位置、相機、日期同作者資料。");
            Add("Turn this picture into a multi-size .ico file.", "將呢張圖片變成多尺寸 .ico 檔案。");
            Add("Closes this window (Esc). Unfinished work is cancelled.", "關閉呢個視窗（Esc）。未完成嘅工作會取消。");

            // ---- shared
            Add("Nothing to edit", "冇嘢可以編輯"); Add("Nothing to trim", "冇嘢可以剪"); Add("Nothing to convert", "冇嘢可以轉換"); Add("The file is no longer there.", "個檔案唔喺度喇。");
            Add("Nothing was saved.", "冇儲存任何嘢。"); Add("Nothing was changed.", "冇改動任何嘢。"); Add("Cancelled", "已取消");
            Add("done", "完成"); Add("working", "處理緊"); Add("working…", "處理緊…"); Add("waiting", "等候中"); Add("could not read", "讀唔到"); Add("could not trim", "剪唔到");
            Add("could not save", "儲存唔到"); Add("could not convert", "轉換唔到"); Add("could not make the icon", "做唔到圖示"); Add("cannot reach the target", "達唔到目標大細");
            Add("nothing selected", "未揀任何嘢"); Add("scanning…", "掃描緊…"); Add("not supported", "唔支援"); Add("has hidden data", "有收埋嘅資料"); Add("no hidden data", "冇收埋嘅資料");
            Add("FFmpeg reported:", "FFmpeg 回報："); Add("The imaging component reported:", "影像元件回報："); Add("What was not changed", "冇改動嘅嘢"); Add("Details copied.", "已複製詳情。"); Add("Progress", "進度"); Add("Trim progress", "剪裁進度");
            Add("Start", "開始"); Add("End", "結束"); Add("New length", "新長度"); Add("From", "由"); Add("To", "去"); Add("Length", "長度"); Add("Size", "大小"); Add("Method", "方法");
            Add("Width", "闊度"); Add("Height", "高度"); Add("Format", "格式"); Add("Shape", "形狀"); Add("Padding", "邊距"); Add("Sizes", "尺寸"); Add("File size", "檔案大小");
            Add("Save as", "另存為"); Add("Picture", "畫面"); Add("Settings used", "用咗嘅設定"); Add("Kept", "保留咗"); Add("Tried", "試過嘅設定"); Add("Quality", "品質");
            Add("Close", "關閉"); Add("Cancel", "取消");
            Add("stereo", "立體聲"); Add("mono", "單聲道"); Add("transparent", "透明"); Add("loops", "循環播放"); Add("Pictures", "圖片");
            Add("Saved next to the original. A name that exists gets a number added; nothing is overwritten.", "儲存喺原檔旁邊。名已經存在就會加個數字，唔會覆蓋任何嘢。");
            Add("The original was not changed.", "原檔冇改動。"); Add("The original recording was not changed.", "原本嘅錄音冇改動。"); Add("The video was not changed.", "影片冇改動。");
            Add("Saved ", "已儲存 ");
            Add("Reading the file…", "讀緊檔案…"); Add("reading the pictures…", "讀緊圖片…"); Add("Reading the pictures…", "讀緊圖片…");
            Add("Waveform. Left and Right move the playhead.", "波形。按左右鍵移動播放位置。"); Add("Timeline. Left and Right move the playhead.", "時間軸。按左右鍵移動播放位置。");
            Add("Start marker", "開始標記"); Add("End marker", "結束標記"); Add("Start handle", "開始把手"); Add("End handle", "結束把手");

            // ---- trim
            Add("This video could not be read", "讀唔到呢段影片"); Add("There is no video in this file.", "呢個檔案入面冇影片。");
            Add("What to do with the selection", "點樣處理所揀嘅部分"); Add("Keep selected", "保留所揀");
            Add("Keep: the clip between the two handles becomes a new file. Remove: the section between them is cut out and the rest is joined.", "保留：兩個把手之間嘅片段變成新檔案。移除：將兩者之間嘅部分剪走，其餘接埋一齊。");
            Add("Exact cut instead", "改用精確剪裁"); Add("Re-encodes so the cut lands on the exact frame (slower)", "重新編碼，令剪裁位置啱啱好喺嗰一格（較慢）");
            Add("Choose where the trimmed video goes", "揀剪好嘅影片放喺邊");
            Add("Checking where the key frames are…", "檢查緊關鍵格喺邊…"); Add("A lossless cut needs its points on key frames. This takes a moment.", "無損剪裁要求兩個剪裁點都喺關鍵格上，需要一陣間。");
            Add("Type a time like 0:12.5 or 1:02:03.", "請輸入時間，例如 0:12.5 或者 1:02:03。"); Add("Type a time like 0:08.5 or 1:02.", "請輸入時間，例如 0:08.5 或者 1:02。"); Add("Type a time like 0:03.2 or 1:02.", "請輸入時間，例如 0:03.2 或者 1:02。");
            Add("Exact cut: the video is re-encoded", "精確剪裁：影片會重新編碼");
            Add("The cut lands on the exact frame. It is slower and costs a tiny amount of quality across the whole clip.", "剪裁位置啱啱好喺嗰一格。較慢，而且成段片都會有少少畫質損失。");
            Add("Lossless: no re-encode", "無損：唔使重新編碼");
            Add("Both points sit on keyframes, so the cut copies the video as it is. It is instant and loses no quality.", "兩個點都喺關鍵格上，所以直接複製影片，即刻完成，唔會失真。");
            Add("Exact cut needed", "需要精確剪裁");
            Add("The start is between keyframes, so the first seconds are re-encoded. Slower, with a tiny quality cost at the cut.", "開始位置喺兩個關鍵格之間，所以頭幾秒要重新編碼。較慢，剪裁位會有少少畫質損失。");
            Add("Cancel stops now and deletes the unfinished .part file.", "取消會即刻停低，並刪走未完成嘅 .part 檔案。");
            Add("Copying video and audio, no re-encode", "複製影片同音訊，唔重新編碼"); Add("Re-encoding the video, copying the audio", "重新編碼影片，複製音訊");
            Add("Re-encoding both pieces and joining them", "重新編碼兩段再接埋"); Add("Copying the part before the cut", "複製剪裁位之前嘅部分"); Add("Copying the part after the cut", "複製剪裁位之後嘅部分");
            Add("Joining the two pieces", "接合兩段");
            Add("keeping", "保留"); Add("removing", "移除"); Add("The trim stopped at ", "剪裁喺呢度停低："); Add("The trim could not be finished", "剪裁未能完成");
            Add("Cancelled. Nothing was saved and the unfinished file was deleted.", "已取消。冇儲存任何嘢，未完成嘅檔案已刪除。");
            Add("Cancelled. Nothing was saved.", "已取消。冇儲存任何嘢。");
            Add("Lossless copy, cut at keyframes", "無損複製，喺關鍵格剪裁"); Add("Exact cut, re-encoded", "精確剪裁，已重新編碼"); Add("Lossless copy, pieces joined at keyframes", "無損複製，喺關鍵格接合");
            Add("The selection is too short. Move the handles apart.", "所揀部分太短，請將把手拉開啲。"); Add("Nothing would be left. Move the handles apart.", "咁樣會乜都冇剩，請將把手拉開啲。");
            Add("This format cannot be played in the preview. The trim still works.", "預覽播放唔到呢種格式，但係剪裁照樣得。");
            Add("This format cannot be played in the preview. The clip can still be made.", "預覽播放唔到呢種格式，但係照樣做到短片。");

            // ---- gif
            Add("GIF", "GIF"); Add("Animated WebP", "動態 WebP"); Add("Short MP4", "短 MP4"); Add("Frames per second", "每秒格數"); Add("Loop forever", "無限循環"); Add("Plays again when it ends", "播完會再播");
            Add("Fit a target file size", "配合目標檔案大小"); Add("Lowers width and fps in small steps until the result is under the limit", "逐步調低闊度同每秒格數，直至結果細過上限");
            Add("Largest size", "最大檔案"); Add("Result will be", "結果會係"); Add("Choose where the clip goes", "揀短片放喺邊");
            Add("Estimating the size…", "估算緊大小…"); Add("The size is being estimated…", "估算緊大小…"); Add("The range is too short. Move the handles apart.", "範圍太短，請將把手拉開啲。");
            Add("making ", "製作緊 "); Add("Cannot reach ", "達唔到 "); Add("The clip could not be made", "做唔到短片");
            Add("Width and fps unchanged, no step-down needed", "闊度同每秒格數冇變，唔使調低");
            Add("Short MP4 is far smaller. Check the size and make it.", "短 MP4 細好多。睇吓大小再製作。"); Add("File copied.", "已複製檔案。");
            Add("Pass 1 of 2: choosing the best 256 colours for this clip", "第 1 / 2 步：為呢段片揀最啱嘅 256 色"); Add("Pass 2 of 2: encoding with a custom palette", "第 2 / 2 步：用專屬色盤編碼");
            Add("Encoding the animated WebP", "編碼動態 WebP 中"); Add("Encoding the short MP4", "編碼短 MP4 中");
            Add("Cancel deletes the unfinished file.", "取消會刪走未完成嘅檔案。");

            // ---- audio
            Add("Fade in", "淡入"); Add("Fade out", "淡出"); Add("Loudness", "響度"); Add("Measuring the loudness…", "量度緊響度…"); Add("Leave as it is", "保持原樣");
            Add("Podcast −16 LUFS", "播客 −16 LUFS"); Add("Streaming −14 LUFS", "串流 −14 LUFS"); Add("Broadcast −23 LUFS", "廣播 −23 LUFS"); Add("Custom", "自訂");
            Add("Loudness target", "響度目標"); Add("Custom target in LUFS", "自訂 LUFS 目標"); Add("Custom target (LUFS)", "自訂目標（LUFS）"); Add("True peak", "真峰值");
            Add("Same format as the original. Type .mp3, .flac, .wav, .m4a or .ogg at the end of the name to change it.", "格式同原檔一樣。想轉格式，喺檔名尾打 .mp3、.flac、.wav、.m4a 或者 .ogg。");
            Add("Loudness could not be measured", "量度唔到響度"); Add("Type a target from -50 to -5 LUFS.", "請輸入 -50 至 -5 LUFS 之間嘅目標。");
            Add("Leave as it is: the level is not changed. Fades and the trim are still applied.", "保持原樣：音量唔會改，淡入淡出同剪裁照樣做。");
            Add("The loudness is measured first, then the level is set to the target. Two passes: measure, then apply.", "會先量度響度，再調到目標。分兩步：先量度，再套用。");
            Add("This audio could not be decoded", "解碼唔到呢段音訊"); Add("There is no audio in this file.", "呢個檔案入面冇音訊。"); Add("The audio could not be saved", "儲存唔到音訊");
            Add("Step 1 of 2: measuring the loudness of the edited audio", "第 1 / 2 步：量度編輯後音訊嘅響度"); Add("Step 2 of 2: trimming, fading and applying the level", "第 2 / 2 步：剪裁、淡入淡出同調較音量");
            Add("Trimming and fading", "剪裁同淡入淡出中"); Add("Checking the result", "檢查結果中");
            Add("The loudness could not be measured. This audio may be silent or damaged.", "量度唔到響度，呢段音訊可能冇聲或者已損壞。");
            Add("This audio format cannot be written.", "寫唔到呢種音訊格式。"); Add("The selection is too short. Move the markers apart.", "所揀部分太短，請將標記拉開啲。");

            // ---- images
            Add("Pictures", "圖片"); Add("Keep proportions", "保持比例"); Add("Changing one side sets the other", "改一邊，另一邊會跟住變"); Add("Crop", "裁切"); Add("None", "冇"); Add("Free", "自由");
            Add("Quality 82", "品質 82"); Add("Target size", "目標大小"); Add("Quality mode", "品質模式"); Add("JPEG quality", "JPEG 品質"); Add("Aim for under", "目標細過"); Add("KB each", "KB 每張");
            Add("No pictures to work on", "冇圖片可以處理");
            Add("Copy pictures (JPG, PNG, WebP, BMP, GIF, TIFF, HEIC) and open this tool from the notice, or drop them here.", "複製圖片（JPG、PNG、WebP、BMP、GIF、TIFF、HEIC）再由通知打開呢個工具，或者直接拖放到呢度。");
            Add("Choose pictures", "揀圖片"); Add("Choose where the copies go", "揀副本放喺邊"); Add("Add pictures to start.", "加入圖片就可以開始。");
            Add("Quality is lowered per picture until it fits. Only JPEG and WebP pictures can be compressed this way.", "每張圖片會逐步調低品質直至符合大小。只有 JPEG 同 WebP 圖片可以咁樣壓縮。");
            Add("Quality applies to JPEG and WebP pictures. PNG, BMP and GIF keep every pixel.", "品質只適用於 JPEG 同 WebP 圖片。PNG、BMP 同 GIF 會保留每個像素。");
            Add("Before and after divider. Left and Right move it.", "前後對比分隔線。按左右鍵移動。");
            Add("Cancel keeps the pictures already saved.", "取消會保留已經儲存嘅圖片。"); Add("Not saved", "未儲存"); Add("Not saved:", "未儲存："); Add("1 copy saved", "已儲存 1 個副本");
            Add("finding the quality that fits ", "搵緊符合大小嘅品質 "); Add("resizing and saving", "縮放同儲存緊"); Add("Before", "之前"); Add("After", "之後");
            Add("unsupported compression: ", "唔支援嘅壓縮： ");

            // ---- privacy
            Add("Location", "位置"); Add("Clean", "乾淨"); Add("Unsupported", "唔支援"); Add("Hidden data", "收埋嘅資料"); Add("Nothing to clean", "冇嘢要清理"); Add("No pictures to clean", "冇圖片要清理");
            Add("This picture has no location, camera, date or author data. It is safe to share as it is.", "呢張圖片冇位置、相機、日期或者作者資料，可以放心原樣分享。");
            Add("These pictures have no location, camera, date or author data. They are safe to share as they are.", "呢啲圖片冇位置、相機、日期或者作者資料，可以放心原樣分享。");
            Add("Copy JPEG, PNG, TIFF or WebP pictures and open this tool from the notice.", "複製 JPEG、PNG、TIFF 或 WebP 圖片，再由通知打開呢個工具。");
            Add("Apply the same choices to all selected pictures", "對所有揀咗嘅圖片用同一組選擇"); Add("Keep the original", "保留原檔");
            Add("Clean copies are saved as name-clean. Turn off to replace the files; the originals go to the Recycle Bin.", "乾淨副本會存做「名稱-clean」。關閉就會取代原檔，原檔會放入資源回收筒。");
            Add("Choose where the clean copies go", "揀乾淨副本放喺邊"); Add("Other hidden data", "其他收埋嘅資料"); Add("Select a picture to see exactly what it holds.", "揀一張圖片，睇清楚佢有啲咩資料。");
            Add("This picture cannot be cleaned here", "呢張圖片喺度清理唔到"); Add("This picture shows where it was taken", "呢張圖片會顯示影相嘅地點");
            Add("Anyone you send it to can read the location", "收到嘅人可以讀到位置"); Add("Anyone you send it to can read the location and the camera's serial number.", "收到嘅人可以讀到位置同相機嘅序號。");
            Add("This picture carries hidden details", "呢張圖片收埋咗啲資料"); Add("Date, camera or author details can be read by anyone you send it to.", "收到嘅人可以讀到日期、相機或者作者資料。");
            Add("Nothing hidden in this picture", "呢張圖片冇收埋嘅資料"); Add("It has no location, camera, date or author data.", "冇位置、相機、日期或者作者資料。");
            Add("Kept by default", "預設保留"); Add("Found", "搵到"); Add("Not present", "冇");
            Add("Map placeholder, no map service is contacted", "地圖示意圖，唔會連接任何地圖服務"); Add("Location shown only in this window", "位置只會喺呢個視窗顯示");
            Add("No pictures are ticked.", "未剔選任何圖片。"); Add("Nothing is chosen to remove.", "未揀要移除嘅資料。"); Add("Cancel keeps the pictures already cleaned.", "取消會保留已經清理好嘅圖片。");
            Add("Removing metadata without re-compressing the picture.", "移除資料，唔會重新壓縮圖片。");
            Add("The original could not be moved to the Recycle Bin, so it was kept and the clean copy was saved beside it.", "原檔放唔到入資源回收筒，所以保留咗原檔，乾淨副本存喺旁邊。");
            Add("1 clean copy saved", "已儲存 1 個乾淨副本"); Add("The originals were kept.", "原檔已保留。"); Add("The originals went to the Recycle Bin.", "原檔已放入資源回收筒。");
            Add("Picture quality is unchanged, and the originals were kept.", "圖片品質冇變，原檔已保留。"); Add("Picture quality is unchanged, and the originals went to the Recycle Bin.", "圖片品質冇變，原檔已放入資源回收筒。"); Add("Orientation", "方向"); Add("Not changed: ", "冇改動： "); Add("unknown error", "未知錯誤"); Add("Colour profile", "色彩描述檔");
            Add("The copy is clean", "副本係乾淨嘅"); Add("Some data is still in the copy", "副本仲有啲資料");
            Add("Camera and lens", "相機同鏡頭"); Add("Date and time", "日期同時間"); Add("Author and notes", "作者同備註"); Add("Embedded thumbnail", "內嵌縮圖");
            Add("GPS position, altitude, direction", "GPS 位置、高度、方向"); Add("Make, model, serial numbers, lens", "廠牌、型號、序號、鏡頭"); Add("When the picture was taken or changed", "圖片幾時影或者改過");
            Add("Author, copyright, software, comments, XMP and IPTC", "作者、版權、軟件、備註、XMP 同 IPTC"); Add("Needed so colours look right; keep it", "令顏色顯示正確所需，建議保留");
            Add("A small copy that can show the uncropped original", "一張細圖，可能會顯示未裁切嘅原圖");
            Add("Author, software or comments are stored in the file", "檔案入面有作者、軟件或者備註"); Add("A date is stored in the file", "檔案入面有日期");
            Add("HEIC pictures cannot be read or cleaned in this version", "呢個版本讀唔到或者清理唔到 HEIC 圖片"); Add("This picture type is not supported", "唔支援呢種圖片");
            Add("The chosen data was still found in the new file, so it was not kept as a clean copy.", "新檔案入面仲搵到所揀嘅資料，所以冇當做乾淨副本保留。");
            Add("Removed in the file, picture data not re-compressed", "喺檔案入面直接移除，圖片資料冇重新壓縮"); Add("Saved again without the metadata, lossless", "冇附資料重新儲存，無損");
            Add("Clean ", "清理 "); Add("Include ", "包括 ");

            // ---- icon
            Add("This picture could not be read", "讀唔到呢張圖片"); Add("The picture on a checkerboard, so transparent parts show", "圖片放喺棋盤格上，透明部分就睇得到");
            Add("Keep transparency", "保留透明度"); Add("Sizes inside the .ico", ".ico 入面嘅尺寸");
            Add("Windows picks the closest size for the taskbar, title bars and Explorer. Keep all seven unless you know you need fewer.", "Windows 會為工作列、標題列同檔案總管揀最近嘅尺寸。除非肯定唔使，否則七個都保留。");
            Add("Preview (128 and 256 shown smaller)", "預覽（128 同 256 縮細顯示）"); Add("Preview of every size on a light background", "每個尺寸喺淺色背景嘅預覽"); Add("Preview of every size on a dark background", "每個尺寸喺深色背景嘅預覽");
            Add("Keep the picture as it is", "保持圖片原樣"); Add("Rounded square", "圓角正方形"); Add("Circle", "圓形"); Add("Choose where the icon goes", "揀圖示放喺邊");
            Add("Choose at least one size.", "請至少揀一個尺寸。"); Add("Cancel stops now. Nothing is written until every size is ready.", "取消會即刻停低。每個尺寸都準備好之前，唔會寫入任何嘢。");
            Add("Writing the file", "寫入檔案中"); Add("Checking the sizes inside it.", "檢查緊入面嘅尺寸。"); Add("The icon could not be made", "做唔到圖示");
            Add("The icon was built with different sizes than the ones chosen, so it was not saved.", "圖示入面嘅尺寸同所揀嘅唔同，所以冇儲存。"); Add("Path copied.", "已複製路徑。");
            Add(" (256 px stored compressed)", "（256 px 以壓縮方式儲存）");
        }

        private static void AddMediaPatterns(List<(Regex, Func<Match, string>)> p)
        {
            void Add(string rx, string yue) => p.Add((new Regex(rx, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline), m => m.Result(yue)));

            // sizes and counts
            Add(@"^(\d+) pictures?$", "$1 張圖片"); Add(@"^(\d+) channels$", "$1 條聲道");
            Add(@"^(\d+) of (\d+) saved\.?$", "已儲存 $1 / $2"); Add(@"^(\d+) saved, (\d+) failed$", "已儲存 $1 張，失敗 $2 張");
            Add(@"^(\d+) of (\d+) cleaned\.?$", "已清理 $1 / $2"); Add(@"^(\d+) cleaned, (\d+) failed$", "已清理 $1 張，失敗 $2 張");
            Add(@"^(\d+) copies saved\.?$", "已儲存 $1 個副本"); Add(@"^(\d+) clean copies saved\.?$", "已儲存 $1 個乾淨副本"); Add(@"^1 copy saved\.?$", "已儲存 1 個副本"); Add(@"^1 clean copy saved\.?$", "已儲存 1 個乾淨副本");
            Add(@"^(.+) \(was (.+)\)$", "$1（原本 $2）"); Add(@"^(.+) \(limit (.+)\)$", "$1（上限 $2）"); Add(@"^(.+) \(original (.+)\)$", "$1（原檔 $2）");
            Add(@"^(.+) LUFS target$", "目標 $1 LUFS"); Add(@"^(.+) → about (.+)$", "$1 → 約 $2");
            Add(@"^(.+) \(256 px stored compressed\)$", "$1（256 px 以壓縮方式儲存）");
            Add(@"^(\d+ \w+ \d{4}), (\d+:\d+:\d+) \(local\)$", "$1 $2（本地時間）");
            Add(@"^(\d+) px, larger than the picture$", "$1 px，大過原圖");
            Add(@"^(\d+) tags removed, (\d+) left that identify you$", "已移除 $1 項標籤，仲剩 $2 項可以識別你");
            Add(@"^(\d+) with location$", "$1 張有位置"); Add(@"^(\d+) with camera serial$", "$1 張有相機序號"); Add(@"^(\d+) with date$", "$1 張有日期");
            Add(@"^(\d+) groups? will be removed from each of (\d+) pictures?$", "每張圖片會移除 $1 組資料（共 $2 張）");
            Add(@"^Found in these (\d+) pictures$", "喺呢 $1 張圖片入面搵到");
            Add(@"^and (\d+) more · (\d+) of (\d+) have hidden data$", "同其他 $1 張 · $3 張入面有 $2 張有收埋嘅資料");
            Add(@"^and (\d+) more$", "同其他 $1 張");
            Add(@"^GPS · (\d+) more$", "GPS · 另外 $1 項"); Add(@"^(\d+) hidden items$", "$1 項收埋嘅資料");
            Add(@"^accurate to about (\d+) m$", "準確度約 $1 米"); Add(@"^altitude (\d+) m$", "海拔 $1 米");
            Add(@"^([^\n]+\.(?:mp4|mkv|mov|avi|webm|m4v|gif|webp|wav|mp3|flac|m4a|ogg|aac|ico|jpg|jpeg|png|bmp|tif|tiff)) and (\d+) more · (.+)$", "$1 同其他 $2 張 · $3");
            Add(@"^([^\n]+\.(?:mp4|mkv|mov|avi|webm|m4v|gif|webp|wav|mp3|flac|m4a|ogg|aac|ico|jpg|jpeg|png|bmp|tif|tiff)) and (\d+) more$", "$1 同其他 $2 張");
            // trim / gif / audio
            Add(@"^(.+) kept · about (.+)$", "保留 $1 · 約 $2"); Add(@"^keeping (.+) to (.+)$", "保留 $1 至 $2"); Add(@"^removing (.+) to (.+)$", "移除 $1 至 $2");
            Add(@"^([\d.]+) seconds selected\. Drag the handles on the timeline or type times\.$", "已揀 $1 秒。可以拖時間軸上嘅把手，或者直接打時間。");
            Add(@"^Clip of ([\d.]+) s from (.+)\.$", "由 $2 開始嘅 $1 秒短片。");
            Add(@"^Preview of the result at (\d+) px wide, looping\. Estimate: (.+)$", "結果預覽，闊 $1 px，循環播放。估算大小：$2");
            Add(@"^Preview of the result at (\d+) px wide, looping\.$", "結果預覽，闊 $1 px，循環播放。");
            Add(@"^Largest size (.+)\. The size is being estimated…$", "最大檔案 $1。估算緊大小…");
            Add(@"^Estimated (.+)\.$", "估算大小 $1。"); Add(@"^Estimated (.+), under your (.+) limit\.$", "估算大小 $1，低過你設定嘅 $2 上限。");
            Add(@"^Estimated (.+), over your (.+) limit\. The width and frame rate will be lowered to fit\.$", "估算大小 $1，超過你設定嘅 $2 上限。會調低闊度同每秒格數去配合。");
            Add(@"^Fits the (.+) limit\. The video was not changed\.$", "符合 $1 上限。影片冇改動。");
            Add(@"^Lowered to (\d+) px and (\d+) fps to fit$", "已調低到 $1 px 同每秒 $2 格去配合");
            Add(@"^Even at (\d+) px and (\d+) fps the best result is (.+)\. Raise the limit, shorten the range, or switch to Short MP4, which is far smaller\.$", "即使用 $1 px 同每秒 $2 格，最好嘅結果都有 $3。請調高上限、縮短範圍，或者轉用細好多嘅短 MP4。");
            Add(@"^Cannot reach ([^\n]+)$", "達唔到 $1"); Add(@"^Try (\d+) of (\d+) \((\d+) px, (\d+) fps\)\. (.+)$", "第 $1 / $2 次嘗試（$3 px，每秒 $4 格）。$5");
            Add(@"^Saved ([^\n]+\.(?:mp4|mkv|mov|avi|webm|m4v|gif|webp|wav|mp3|flac|m4a|ogg|aac|ico|jpg|jpeg|png|bmp|tif|tiff))$", "已儲存 $1"); Add(@"^([^\n]+\.(?:mp4|mkv|mov|avi|webm|m4v|gif|webp|wav|mp3|flac|m4a|ogg|aac|ico|jpg|jpeg|png|bmp|tif|tiff)) saved\.$", "已儲存 $1。"); Add(@"^making ([^\n]+\.(?:mp4|mkv|mov|avi|webm|m4v|gif|webp|wav|mp3|flac|m4a|ogg|aac|ico|jpg|jpeg|png|bmp|tif|tiff))$", "製作緊 $1");
            Add(@"^Writing ([^\n]+\.part)$", "寫入緊 $1"); Add(@"^Nothing was written and (.+) is untouched\.$", "冇寫入任何嘢，$1 原封不動。");
            Add(@"^No output file was kept\. (.+) is untouched\.( .+)?$", "冇保留任何輸出檔案，$1 原封不動。$2");
            Add(@"^No icon file was kept\. (.+) is untouched\.$", "冇保留任何圖示檔案，$1 原封不動。");
            Add(@"^ Try an end point before (.+) or turn on Exact cut\.$", " 試吓將結束點設喺 $1 之前，或者開啟精確剪裁。"); Add(@"^ Try turning on Exact cut\.$", " 試吓開啟精確剪裁。");
            Add(@"^The trim stopped at (.+)$", "剪裁喺 $1 停低"); Add(@"^FFmpeg reported: ([^\n]+)$", "FFmpeg 回報：$1");
            Add(@"^Measured now: (.+) LUFS · true peak (.+) dB$", "而家量度到：$1 LUFS · 真峰值 $2 dB");
            Add(@"^(Raises|Lowers) the whole recording by ([\d.]+) dB; a limiter keeps the peak under −1\.0 dB\. Two passes: measure, then apply\.$", "將成段錄音$1 $2 dB；限幅器會將峰值控制喺 −1.0 dB 以下。分兩步：先量度，再套用。");
            Add(@"^(Raises|Lowers) the whole recording by ([\d.]+) dB\. No limiter is needed\. Two passes: measure, then apply\.$", "將成段錄音$1 $2 dB，唔使限幅器。分兩步：先量度，再套用。");
            Add(@"^The whole file measures (.+) LUFS at the start\.$", "成個檔案一開始量度到 $1 LUFS。");
            Add(@"^(.+) LUFS before, (.+) LUFS after$", "調整前 $1 LUFS，調整後 $2 LUFS");
            Add(@"^(.+) dB before, (.+) dB after( \(limited\))?, (no clipping|clipping is possible)$", "調整前 $1 dB，調整後 $2 dB$3，$4");
            Add(@"^(Raises)$", "提高"); Add(@"^(Lowers)$", "調低");
            // images
            Add(@"^Picture (\d+) of (\d+)$", "第 $1 / $2 張圖片"); Add(@"^Size (\d+) of (\d+)$", "第 $1 / $2 個尺寸");
            Add(@"^(.+) · finding the quality that fits (.+)$", "$1 · 搵緊符合 $2 嘅品質"); Add(@"^(.+) · resizing and saving$", "$1 · 縮放同儲存緊");
            Add(@"^Resampling to (\d+)×(\d+) with a high-quality filter\.$", "用高品質濾鏡重新取樣到 $1×$2。");
            Add(@"^(.+) became (.+), (\d+)% smaller\. The originals were not changed\.$", "$1 變成 $2，細咗 $3%。原檔冇改動。");
            Add(@"^(.+) became (.+), (\d+)% smaller\. One picture could not be processed\.$", "$1 變成 $2，細咗 $3%。有一張圖片處理唔到。");
            Add(@"^(.+) became (.+), (\d+)% smaller\. (\d+) pictures could not be processed\.$", "$1 變成 $2，細咗 $3%。有 $4 張圖片處理唔到。");
            Add(@"^Cancelled after (\d+) of (\d+)\. The pictures already saved were kept\.$", "做咗 $1 / $2 張就取消。已經儲存嘅圖片會保留。");
            Add(@"^Cancelled after (\d+) of (\d+)\. The pictures already cleaned were kept\.$", "做咗 $1 / $2 張就取消。已經清理好嘅圖片會保留。");
            Add(@"^Not saved: (.+)$", "未儲存：$1"); Add(@"^Not changed: (.+)$", "冇改動：$1"); Add(@"^(.+) \(target not reached\)$", "$1（未達目標）");
            Add(@"^Before · (.+)$", "之前 · $1"); Add(@"^After · ~(.+)$", "之後 · 約 $1"); Add(@"^→ ~(.+)$", "→ 約 $1");
            Add(@"^Resize, crop and compress: nothing selected$", "縮放、裁切同壓縮：未揀任何嘢"); Add(@"^Resize, crop and compress: (\d+) pictures?$", "縮放、裁切同壓縮：$1 張圖片");
            Add(@"^Image privacy clean-up: (\d+) pictures?$", "圖片私隱清理：$1 張圖片");
            Add(@"^Save (\d+) copies$", "儲存 $1 個副本"); Add(@"^Save in (.+)$", "儲存到 $1"); Add(@"^Include ([^\n]+\.(?:mp4|mkv|mov|avi|webm|m4v|gif|webp|wav|mp3|flac|m4a|ogg|aac|ico|jpg|jpeg|png|bmp|tif|tiff))$", "包括 $1"); Add(@"^Clean ([^\n]+\.(?:mp4|mkv|mov|avi|webm|m4v|gif|webp|wav|mp3|flac|m4a|ogg|aac|ico|jpg|jpeg|png|bmp|tif|tiff))$", "清理 $1");
            Add(@"^Type a whole number from (\d+) to (\d+)\.$", "請輸入 $1 至 $2 之間嘅整數。");
            // icon
            Add(@"^This picture is smaller than (\d+) px$", "呢張圖片細過 $1 px");
            Add(@"^Sizes (.+) would be enlarged and look soft\. Choose them off, or start from a larger picture\.$", "尺寸 $1 要放大，會變得模糊。可以將佢哋關閉，或者用大啲嘅圖片。");
            Add(@"^(\d+) sizes? selected\. Not enough pixels: (.+)\. You can still continue; the result will be soft\.$", "已揀 $1 個尺寸。像素唔夠：$2。仍然可以繼續，不過結果會比較模糊。");
            Add(@"^(\d+) sizes? selected$", "已揀 $1 個尺寸"); Add(@"^(\d+) sizes? · about (.+)$", "$1 個尺寸 · 約 $2");
            Add(@"^Seven sizes in one file\. The picture was not changed\.$", "一個檔案入面有七個尺寸。圖片冇改動。");
            Add(@"^(\d+) sizes? in one file\. The picture was not changed\.$", "一個檔案入面有 $1 個尺寸。圖片冇改動。");
        }
    }
}
