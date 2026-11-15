using System.Collections.Generic;
namespace MonitoringTools.Core
{
    internal static partial class Strings
    {
        private static void AddQuickPacking(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);
            Add("Pack to ZIP", "打包成 ZIP"); Add("Pack to VHDX", "打包成 VHDX");
            Add("Create and verify a ZIP beside the original files. Starts immediately; originals are kept.", "即刻喺原檔旁邊建立並驗證 ZIP；原檔會保留。");
            Add("Original files are kept. Closing this window keeps packing in the background.", "原檔會保留。關閉呢個視窗後，打包會喺背景繼續。");
            Add("Scanning", "掃描緊"); Add("Compressing", "壓縮緊"); Add("Verifying", "驗證緊"); Add("Finishing", "完成最後步驟");
            Add("Packed with skipped items", "已打包，但有項目被略過"); Add("Waiting for Windows permission", "等緊 Windows 權限批准");
            Add("Creating and mounting", "建立同掛載緊"); Add("Growing disk", "擴大磁碟中"); Add("Copying and verifying", "複製同驗證緊");
            Add("Detaching", "卸載緊"); Add("Fitting disk", "調整磁碟大小中"); Add("Cleanup needs attention", "清理需要處理");
            Add("Cancel packing", "取消打包"); Add("Cancelling safely…", "安全取消中…"); Add("Hide and continue", "隱藏並繼續");
            Add("Copy output path", "複製輸出路徑"); Add("Open progress", "開啟進度"); Add("Open packing progress", "開啟打包進度");
            Add("Counting files and folders…", "數緊檔案同資料夾…"); Add("in progress", "處理中");
            Add("Mixed folders: output is saved beside the first copied item.", "來自不同資料夾：輸出會放喺第一個複製項目旁邊。");
            Add("ZIP packing started", "ZIP 打包已開始"); Add("VHDX packing started", "VHDX 打包已開始");
        }
    }
}
