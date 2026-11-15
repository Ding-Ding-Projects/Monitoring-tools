using System.Collections.Generic;
namespace MonitoringTools.Core
{
    internal static partial class Strings
    {
        private static void AddCompose(Dictionary<string, string> t)
        {
            void Add(string en, string yue) => t.TryAdd(en, yue);
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
