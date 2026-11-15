using System.Collections.Generic;
namespace MonitoringTools.Core
{
    internal static partial class Strings
    {
        private static void AddCompose(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);
            Add("Context", "環境"); Add("No failure observed", "尚未觀察到失敗");
            Add("Command completed; service readiness unchanged. Recheck health separately.", "指令已完成；服務就緒狀態未變。請另行重新檢查健康狀態。");
            Add("Possible cause: a published port is already occupied. Inspect the reported port and configuration manually; no process is stopped and no port is changed.", "可能原因：已發佈連接埠已被佔用。請自行查看報告中嘅連接埠及設定；不會停止任何程序或更改連接埠。");
            Add("Possible cause: image access or registry authentication is unavailable. Review access outside this dialog, then retry pull. No credentials are changed.", "可能原因：映像存取或登錄驗證未能使用。請喺視窗以外檢查存取權，再重試下載。不會更改憑證。");
            Add("Possible cause: image availability, registry reachability or image reference. Review the pull diagnostic, then retry pull.", "可能原因：映像可用性、登錄連線或映像名稱。請查看下載記錄，再重試下載。");
            Add("Possible cause: the selected configuration or interpolation is invalid. Review the configuration manually, then retry startup. No file is edited.", "可能原因：所選設定或變數展開無效。請自行檢查設定，再重試啟動。不會修改任何檔案。");
            Add("Possible cause: the selected Docker context cannot reach its engine. Check that context and engine, then recheck engine.", "可能原因：所選 Docker 環境未能連接引擎。請檢查環境及引擎，再重新檢查引擎。");
            Add("Possible cause: a build instruction, input or dependency failed. Review the build diagnostic, then retry build.", "可能原因：建置指令、輸入或相依項目失敗。請查看建置記錄，再重試建置。");
            Add("Possible cause: the required CLI or Compose plugin is unavailable. Review its installation and retry startup.", "可能原因：所需 CLI 或 Compose 外掛未能使用。請檢查安裝，再重試啟動。");
            Add("No specific cause established. Inspect the failed stage diagnostics before retrying.", "未能確定具體原因。重試前請查看失敗階段嘅記錄。");
            Add("Possible cause: the service process or its configured health check failed. Inspect selected-service logs; a failing health check does not establish the underlying cause.", "可能原因：服務程序或設定嘅健康檢查失敗。請查看所選服務記錄；健康檢查失敗唔代表已確定根本原因。");
            Add("Ready", "準備就緒"); Add("Not started", "未開始"); Add("In progress", "進行中");
            Add("CLI", "CLI"); Add("Compose", "Compose"); Add("Engine", "引擎"); Add("Configuration", "設定");
            Add("Build", "建置"); Add("Pull", "下載映像"); Add("Start", "啟動"); Add("Health observation", "觀察服務狀態");
            Add("Logs", "記錄"); Add("Restart", "重新啟動"); Add("Stopped", "已停止");
            Add("healthy", "健康"); Add("unhealthy", "不健康"); Add("waiting", "等候中"); Add("failed", "失敗");
            Add("running (no health check)", "運作中（未設定健康檢查）");
            Add("completed (exit 0; possible one-shot)", "已結束（結束碼 0；可能係一次性工作）");
            Add("Services running; health checks passed where configured. URLs are unverified.", "服務運作中；已設定嘅健康檢查通過。網址尚未驗證。");
            Add("Service failure observed; inspect the selected service logs", "觀察到服務失敗；請查看所選服務記錄");
            Add("Exited service observed with code 0. Confirm intended one-shot behavior; stack readiness is unverified.", "觀察到服務以結束碼 0 結束。請確認是否預期嘅一次性工作；整組服務尚未確認就緒。");
            Add("Observation timed out; services may still be starting. Inspect logs or recheck.", "觀察已逾時；服務可能仍在啟動。請查看記錄或重新檢查。");
            Add("Cancelled. Owned CLI/observation stopped; containers may remain running. No rollback was performed.", "已取消。本次 CLI 及觀察已停止；容器可能仍然運作。未執行回復。");
            Add("Command timed out. Containers may remain running; recheck before retrying.", "指令已逾時。容器可能仍然運作；重試前請重新檢查。");
            Add("Logs retrieved; readiness unchanged", "已取得記錄；就緒狀態未變");
            Add("No active services were discovered.", "搵唔到已啟用嘅服務。");
            Add("Run startup preflight before checking services.", "檢查服務前請先執行啟動檢查。");
        }
    }
}
