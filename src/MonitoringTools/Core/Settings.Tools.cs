namespace MonitoringTools.Core
{
    /// <summary>Settings owned by the Tools page: drawing, publishing and the external editor. No credentials are ever stored here.</summary>
    public sealed partial class Settings
    {
        // External editor. Values: auto, vscode, notepadpp, default, custom.
        public string PreferredEditor { get; set; } = "auto";
        public string CustomEditorPath { get; set; } = "";

        // Drawing. Defaults: Pen, 4 px wide, the theme's primary colour, toolbar docked at the top and expanded.
        public string DrawPen { get; set; } = "pen";
        public double DrawWidth { get; set; } = 4;
        public string DrawColor { get; set; } = "";
        public string DrawDock { get; set; } = "top";
        public bool DrawToolbarMinimised { get; set; } = false;
        public int DrawExportScale { get; set; } = 1;

        // Publishing. Private by default; the choice is remembered, never an account or a token.
        public bool ForgePrivate { get; set; } = true;
        public bool ForgePush { get; set; } = true;
        public string ForgeLastFolder { get; set; } = "";
    }
}
