namespace MonitoringTools.Core
{
    public sealed partial class Settings
    {
        /// <summary>Port of the local Ollama server. Only 127.0.0.1 is ever contacted. Default 11434.</summary>
        public int OllamaPort { get; set; } = 11434;

        /// <summary>The model last used for the quick chat test, selected again next time.</summary>
        public string OllamaLastModel { get; set; } = "";

        /// <summary>How long a model stays loaded after a chat request: "5m" (Ollama's own default), "30m", "1h", or "0" to unload at once.</summary>
        public string OllamaKeepAlive { get; set; } = "5m";

        /// <summary>Save each chat to disk after every reply. Default off: a chat is only stored when you press Save chat.</summary>
        public bool OllamaAutoSaveChats { get; set; } = false;

        /// <summary>Name of the chat profile (system prompt preset) selected last. Empty means none.</summary>
        public string OllamaLastProfile { get; set; } = "";

        /// <summary>Model names waiting in the pull cart, kept between sessions. Empty by default.</summary>
        public System.Collections.Generic.List<string> OllamaCart { get; set; } = new System.Collections.Generic.List<string>();
    }
}
