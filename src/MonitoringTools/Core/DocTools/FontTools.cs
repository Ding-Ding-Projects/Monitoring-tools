using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Media;

namespace MonitoringTools.Core.DocTools
{
    public sealed class FontFacts
    {
        public string Family, Face, Version, Licence, Copyright;
        public int Weight, Glyphs;
        public List<string> Scripts = new List<string>();
        public bool IsCff;
        public string Error;                 // set when the file is not a usable font
        public bool Ok => Error == null;
    }

    /// <summary>Where and how a font would be installed for the current user only.</summary>
    public sealed class FontPlan
    {
        public string Source, TargetFile, RegistryPath, ValueName;
        public string RegistryHive = "HKEY_CURRENT_USER";
        public override string ToString() => $"{TargetFile} + {RegistryHive}\\{RegistryPath}\\{ValueName}";
    }

    /// <summary>
    /// Reads a font file with WPF's GlyphTypeface (the preview installs nothing) and installs or removes it for the current user
    /// only: a copy under %LOCALAPPDATA%\Microsoft\Windows\Fonts, the name under the per-user Fonts registry key, then
    /// AddFontResource and a font-change broadcast. No administrator rights and no system folder are touched.
    /// </summary>
    public static class FontTools
    {
        public const string UserFontsKey = @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";
        public static readonly string[] Extensions = { ".ttf", ".otf", ".ttc", ".woff" };

        /// <summary>Checks and captures replace the real install, uninstall and "is it installed" steps. Each returns null on success or a message.</summary>
        internal static Func<FontPlan, string> InstallHook, UninstallHook;
        internal static Func<FontPlan, bool?> InstalledHook;

        public static bool IsFont(string path) => Extensions.Contains(Path.GetExtension(path ?? "").ToLowerInvariant());

        public static string FontsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");

        public static FontFacts Read(string file)
        {
            var f = new FontFacts();
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".woff") { f.Error = "WOFF is a web font format. Windows cannot install it, so convert it to TTF or OTF first."; return f; }
            try
            {
                if (new FileInfo(file).Length < 12) { f.Error = "The file is too small to be a font."; return f; }
                var g = new GlyphTypeface(new Uri(Path.GetFullPath(file)));
                string First(IDictionary<System.Globalization.CultureInfo, string> d) => d == null || d.Count == 0 ? null : (d.TryGetValue(System.Globalization.CultureInfo.GetCultureInfo("en-US"), out var v) ? v : d.Values.First());
                f.Family = First(g.Win32FamilyNames) ?? First(g.FamilyNames) ?? Path.GetFileNameWithoutExtension(file);
                f.Face = First(g.Win32FaceNames) ?? First(g.FaceNames) ?? "Regular";
                f.Version = First(g.VersionStrings);
                f.Licence = First(g.LicenseDescriptions);
                f.Copyright = First(g.Copyrights);
                f.Weight = g.Weight.ToOpenTypeWeight();
                var map = g.CharacterToGlyphMap;
                f.Glyphs = g.GlyphCount;
                bool Has(int a, int b) => map.Keys.Any(k => k >= a && k <= b);
                if (Has(0x41, 0x5A) || Has(0xC0, 0x24F)) f.Scripts.Add("Latin");
                if (Has(0x400, 0x4FF)) f.Scripts.Add("Cyrillic");
                if (Has(0x370, 0x3FF)) f.Scripts.Add("Greek");
                if (Has(0x590, 0x5FF)) f.Scripts.Add("Hebrew");
                if (Has(0x600, 0x6FF)) f.Scripts.Add("Arabic");
                if (Has(0xE00, 0xE7F)) f.Scripts.Add("Thai");
                if (Has(0x3040, 0x30FF)) f.Scripts.Add("Kana");
                if (Has(0x4E00, 0x9FFF)) f.Scripts.Add("CJK");
                if (Has(0xAC00, 0xD7AF)) f.Scripts.Add("Hangul");
                f.IsCff = ext == ".otf";
            }
            catch (Exception ex) when (ex is System.IO.FileFormatException || ex is IOException || ex is ArgumentException || ex is NotSupportedException || ex is UriFormatException || ex is InvalidOperationException)
            {
                f.Error = "The font tables are damaged, so Windows would refuse it.";
                f.Family = null;
                Log.Warn("Font read failed: " + ex.Message);
            }
            return f;
        }

        public static FontPlan Plan(string file, FontFacts facts)
        {
            var name = Path.GetFileName(file);
            var face = string.Equals(facts?.Face, "Regular", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(facts?.Face) ? "" : " " + facts.Face;
            var kind = Path.GetExtension(file).Equals(".otf", StringComparison.OrdinalIgnoreCase) ? "OpenType" : "TrueType";
            return new FontPlan
            {
                Source = file,
                TargetFile = Path.Combine(FontsFolder, name),
                RegistryPath = UserFontsKey,
                ValueName = $"{facts?.Family ?? Path.GetFileNameWithoutExtension(file)}{face} ({kind})"
            };
        }

        private static string Hash(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)); }

        /// <summary>True when the same font file is already installed for this user.</summary>
        public static bool IsInstalled(FontPlan plan)
        {
            var h = InstalledHook?.Invoke(plan);
            if (h.HasValue) return h.Value;
            try
            {
                if (!File.Exists(plan.TargetFile)) return false;
                if (new FileInfo(plan.TargetFile).Length != new FileInfo(plan.Source).Length) return false;
                if (Hash(plan.TargetFile) != Hash(plan.Source)) return false;
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(plan.RegistryPath);
                return k?.GetValue(plan.ValueName) != null;
            }
            catch { return false; }
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int AddFontResourceW(string file);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool RemoveFontResourceW(string file);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

        private static void Broadcast() { try { SendMessageTimeout((IntPtr)0xFFFF, 0x1D, IntPtr.Zero, IntPtr.Zero, 2, 1500, out _); } catch { } }

        /// <summary>Installs for the current user only. Returns null on success, otherwise a plain message.</summary>
        public static string Install(FontPlan plan)
        {
            if (InstallHook != null) return InstallHook(plan);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plan.TargetFile));
                var target = plan.TargetFile;
                if (File.Exists(target) && Hash(target) != Hash(plan.Source)) { target = FileOps.Unique(target); plan.TargetFile = target; }
                if (!File.Exists(target)) File.Copy(plan.Source, target);
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(plan.RegistryPath)) k.SetValue(plan.ValueName, target);
                AddFontResourceW(target);
                Broadcast();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static string Uninstall(FontPlan plan)
        {
            if (UninstallHook != null) return UninstallHook(plan);
            try
            {
                RemoveFontResourceW(plan.TargetFile);
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(plan.RegistryPath, true)) k?.DeleteValue(plan.ValueName, false);
                try { if (File.Exists(plan.TargetFile)) File.Delete(plan.TargetFile); } catch { /* in use by a running app: the registry entry is gone, so it disappears at next sign-in */ }
                Broadcast();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
