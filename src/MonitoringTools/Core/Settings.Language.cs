namespace MonitoringTools.Core
{
    public enum LanguageMode { English = 0, Cantonese = 1, Bilingual = 2 }

    /// <summary>Language, humour and narrator settings (see docs/features/language).</summary>
    public sealed partial class Settings
    {
        /// <summary>Interface language. Default English.</summary>
        public LanguageMode LanguageMode { get; set; } = LanguageMode.English;

        /// <summary>Tone of English headline messages, 1 (plain) to 5 (playful). Default 3.</summary>
        public int EnglishHumour { get; set; } = 3;

        /// <summary>Tone of Cantonese headline messages, 1 (plain) to 5 (playful). Default 3.</summary>
        public int CantoneseHumour { get; set; } = 3;

        /// <summary>Whether toast and dialog titles keep their emoji. Default on.</summary>
        public bool EmojiInDialogs { get; set; } = true;

        /// <summary>Read toast titles aloud. Default off.</summary>
        public bool NarratorEnabled { get; set; } = false;

        /// <summary>Installed English voice id, or empty for the first English voice.</summary>
        public string EnglishVoiceId { get; set; } = "";

        /// <summary>Installed Cantonese voice id, or empty for the first Cantonese voice.</summary>
        public string CantoneseVoiceId { get; set; } = "";

        /// <summary>Speaking rate multiplier, 0.5 to 2.0. Default 1.0.</summary>
        public double NarratorRate { get; set; } = 1.0;
    }
}
