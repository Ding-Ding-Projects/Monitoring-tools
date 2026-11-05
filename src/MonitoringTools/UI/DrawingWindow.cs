using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A drawing and annotation board modelled on Drawesome, built natively on the WPF ink canvas because the Drawesome
    /// package is a JavaScript library and this app has no web view or package manager to host it. It has seven pens, a partial
    /// eraser, shapes, text, select and move, undo and redo, and PNG, SVG and clipboard output. Drawing never leaves this computer.
    /// </summary>
    public sealed partial class DrawingWindow : Window
    {
        // -------------------------------------------------------------- entry points

        public static void OpenBlank() => Launch(new DrawingWindow(null, null, "Blank board", "blank"));

        public static bool OpenFile(string path, out string error)
        {
            error = null;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();
                Launch(new DrawingWindow(bi, path, Path.GetFileName(path), "file-" + Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..16].ToLowerInvariant()));
                return true;
            }
            catch (Exception ex) { error = "That image could not be opened: " + ex.Message; return false; }
        }

        /// <summary>Annotate an image that has no file yet, such as one from clipboard history.</summary>
        public static void OpenBitmap(BitmapSource image, string name = "Copied image")
        {
            if (image == null) return;
            Launch(new DrawingWindow(image, null, name, "clip-" + DateTime.Now.ToString("yyyyMMddHHmmssfff")));
        }

        private static void Launch(DrawingWindow w) { w.Show(); w.Activate(); }

        // -------------------------------------------------------------- tools

        private enum Behavior { Speed, Constant, Direction }
        private enum Tool { Pen, Eraser, Select, Line, Arrow, Rectangle, Ellipse, Text }

        private sealed class PenSpec
        {
            public string Id, Name, Hint;
            public Key Key;
            public Behavior Behavior;
            public double WidthFactor;
            public bool Highlighter;
            public StylusTip Tip;
            public byte Alpha;
        }

        private static readonly PenSpec[] Pens =
        {
            new PenSpec { Id = "pencil", Name = "Pencil", Key = Key.P, Behavior = Behavior.Speed, WidthFactor = 0.6, Tip = StylusTip.Ellipse, Alpha = 0xDC, Hint = "thins as you draw faster, slightly soft" },
            new PenSpec { Id = "pen", Name = "Pen", Key = Key.N, Behavior = Behavior.Speed, WidthFactor = 1.0, Tip = StylusTip.Ellipse, Alpha = 0xFF, Hint = "thins as you draw faster" },
            new PenSpec { Id = "fineliner", Name = "Fineliner", Key = Key.F, Behavior = Behavior.Constant, WidthFactor = 0.5, Tip = StylusTip.Ellipse, Alpha = 0xFF, Hint = "always the same thin width" },
            new PenSpec { Id = "marker", Name = "Marker", Key = Key.M, Behavior = Behavior.Constant, WidthFactor = 2.4, Tip = StylusTip.Rectangle, Alpha = 0xF2, Hint = "constant, wide, square tip" },
            new PenSpec { Id = "highlighter", Name = "Highlighter", Key = Key.H, Behavior = Behavior.Constant, WidthFactor = 4.5, Tip = StylusTip.Rectangle, Alpha = 0xFF, Highlighter = true, Hint = "constant, wide, see-through" },
            new PenSpec { Id = "brush", Name = "Brush", Key = Key.B, Behavior = Behavior.Speed, WidthFactor = 2.0, Tip = StylusTip.Ellipse, Alpha = 0xFF, Hint = "wide, thins as you draw faster" },
            new PenSpec { Id = "fountain", Name = "Fountain pen", Key = Key.U, Behavior = Behavior.Direction, WidthFactor = 1.6, Tip = StylusTip.Ellipse, Alpha = 0xFF, Hint = "width follows the direction of the stroke" },
        };

        private static readonly (Tool Tool, string Name, Key Key, string Hint)[] OtherTools =
        {
            (Tool.Eraser, "Eraser", Key.E, "removes only the part of a stroke you rub over"),
            (Tool.Select, "Select", Key.S, "click or drag around strokes and text to move or resize them; Delete removes them"),
            (Tool.Line, "Line", Key.L, "drag; hold Shift for 45 degree steps"),
            (Tool.Arrow, "Arrow", Key.A, "drag; hold Shift for 45 degree steps"),
            (Tool.Rectangle, "Rectangle", Key.R, "drag; hold Shift for a square"),
            (Tool.Ellipse, "Ellipse", Key.O, "drag; hold Shift for a circle"),
            (Tool.Text, "Text", Key.T, "click to type; Enter places it, Shift+Enter starts a new line, Esc cancels"),
        };

        // -------------------------------------------------------------- state

        private readonly BitmapSource _image;
        private readonly string _sourcePath;
        private readonly string _boardKey;
        private readonly double _w, _h;
        private readonly Grid _board = new Grid();
        private readonly LiveWidthRenderer _live = new LiveWidthRenderer();
        private readonly InkCanvas _canvas;
        private readonly Border _toolbarHost = new Border();
        private readonly TextBlock _status = new TextBlock();
        private readonly StackPanel _statusRow = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly DockPanel _dock = new DockPanel();
        private readonly Border _boardFrame = new Border();

        private Tool _tool = Tool.Pen;
        private PenSpec _pen = Pens[1];
        private Color _color;
        private double _width;
        private string _panel = "tools";        // tools, colour or size: the toolbar changes shape instead of switching panels
        private bool _suppress, _dirty;
        private readonly List<Snap> _undo = new List<Snap>();
        private readonly List<Snap> _redo = new List<Snap>();
        private Stroke _shape;
        private Point _shapeStart;
        private TextBox _editor;
        private TextBlock _editing;
        private string _lastSaved;

        /// <summary>
        /// Draws the stroke being drawn with the pen attributes in force at each moment, so a width change made while the pen is down
        /// shows in the very next segment instead of waiting for the stroke to end.
        /// </summary>
        private sealed class LiveInkCanvas : InkCanvas
        {
            public LiveInkCanvas(DynamicRenderer r) { DynamicRenderer = r; }   // the property is only open to a subclass
        }

        internal sealed class LiveWidthRenderer : DynamicRenderer
        {
            private volatile DrawingAttributes _now;
            public void Use(DrawingAttributes a) { _now = a.Clone(); }
            protected override void OnDraw(DrawingContext dc, StylusPointCollection points, Geometry geometry, Brush fillBrush)
            {
                var a = _now;
                if (a == null || points == null || points.Count == 0) { base.OnDraw(dc, points, geometry, fillBrush); return; }
                new Stroke(points, a.Clone()).Draw(dc);
            }
            internal void DrawForTest(DrawingContext dc, StylusPointCollection points) => OnDraw(dc, points, null, null);
        }

        private sealed class TextItem { public double X { get; set; } public double Y { get; set; } public string Text { get; set; } public string Color { get; set; } public double Size { get; set; } }
        private sealed class Snap { public byte[] Ink; public List<TextItem> Texts; }
        private sealed class BoardFile { public string Ink { get; set; } public List<TextItem> Texts { get; set; } }

        private DrawingWindow(BitmapSource image, string sourcePath, string name, string key)
        {
            _canvas = new LiveInkCanvas(_live);
            _image = image;
            _sourcePath = sourcePath;
            _boardKey = key;
            _w = image != null ? image.PixelWidth * 96.0 / Math.Max(1, image.DpiX) : 1280;
            _h = image != null ? image.PixelHeight * 96.0 / Math.Max(1, image.DpiY) : 800;
            var s = Settings.Current;
            _pen = Pens.FirstOrDefault(p => p.Id == s.DrawPen) ?? Pens[1];
            _width = Math.Max(1, Math.Min(48, s.DrawWidth));
            _color = ParseColor(s.DrawColor) ?? Ui.C("C.Mint");

            Title = "Draw: " + name;
            Width = 1180; Height = 800; MinWidth = 640; MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Res("B.Window");
            AutomationProperties.SetName(this, "Drawing board for " + name);
            SourceInitialized += (a, b) => Native.ApplyDarkChrome(this);

            BuildBoard();
            BuildChrome();
            RestoreBoard();
            PushSnapshot(initial: true);
            SelectTool(_tool);

            PreviewKeyDown += OnKey;
            Closing += (a, b) => { CommitEditor(); SaveBoard(); };
            ElementMenu.Attach(this, _ => MenuItems());
        }

        // -------------------------------------------------------------- layout

        private void BuildBoard()
        {
            _board.Width = _w; _board.Height = _h;
            if (_image != null)
                _board.Children.Add(new Image { Source = _image, Width = _w, Height = _h, Stretch = Stretch.Fill, SnapsToDevicePixels = true });
            else
                _board.Children.Add(new System.Windows.Shapes.Rectangle { Width = _w, Height = _h, Fill = Brushes.White });
            _canvas.Background = Brushes.Transparent;
            _canvas.Width = _w; _canvas.Height = _h;
            _canvas.UseCustomCursor = false;
            _board.Children.Add(_canvas);
            AutomationProperties.SetName(_canvas, "Drawing surface");
            AutomationProperties.SetHelpText(_canvas, "Draw with a mouse, pen or finger. Press P, N, F, M, H, B or U for a pen, E for the eraser.");

            _canvas.StrokeCollected += OnStrokeCollected;
            _canvas.Strokes.StrokesChanged += (s, e) => { if (!_suppress) _dirty = true; };
            _canvas.SelectionMoved += (s, e) => CommitChange();
            _canvas.SelectionResized += (s, e) => CommitChange();
            _canvas.PreviewMouseLeftButtonDown += OnShapeDown;
            _canvas.PreviewMouseMove += OnShapeMove;
            _canvas.PreviewMouseLeftButtonUp += OnShapeUp;
            _canvas.PreviewMouseLeftButtonUp += (s, e) => { if (_dirty && !_suppress && _shape == null && _tool != Tool.Select) CommitChange(); };
        }

        private void BuildChrome()
        {
            var view = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = _board, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _boardFrame.Background = Ui.Res("B.Sunken");
            _boardFrame.CornerRadius = new CornerRadius(20);
            _boardFrame.Padding = new Thickness(12);
            _boardFrame.Margin = new Thickness(12, 4, 12, 4);
            var scroller = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = view };
            _boardFrame.Child = scroller;

            _toolbarHost.Background = Ui.Res("B.Raised");
            _toolbarHost.CornerRadius = new CornerRadius(28);
            _toolbarHost.Padding = new Thickness(10, 8, 10, 2);
            _toolbarHost.Margin = new Thickness(12, 12, 12, 8);
            _toolbarHost.HorizontalAlignment = HorizontalAlignment.Left;

            _status.Style = Ui.Style("T.Body");
            _status.FontSize = 12;
            _status.Foreground = Ui.Res("B.TextMute");
            _status.TextWrapping = TextWrapping.Wrap;
            _status.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
            _statusRow.Margin = new Thickness(18, 2, 12, 10);
            _statusRow.Children.Add(_status);
            DockPanel.SetDock(_statusRow, Dock.Bottom);

            _dock.Children.Add(_statusRow);
            DockPanel.SetDock(_toolbarHost, DockFromSetting());
            _dock.Children.Add(_toolbarHost);
            _dock.Children.Add(_boardFrame);
            Content = _dock;
            ShowStatus("Annotations are saved on this computer only and come back next time you open this " + (_image != null ? "image." : "board."));
            RebuildToolbar();
        }

        private static Dock DockFromSetting() => Settings.Current.DrawDock switch { "bottom" => Dock.Bottom, "left" => Dock.Left, "right" => Dock.Right, _ => Dock.Top };

        private void ShowStatus(string text, string revealPath = null)
        {
            _status.Text = text;
            while (_statusRow.Children.Count > 1) _statusRow.Children.RemoveAt(1);
            if (revealPath != null)
            {
                var b = Ui.Button("Show in folder", () => ActionProvider.Reveal(revealPath));
                b.Margin = new Thickness(12, 0, 0, 0);
                _statusRow.Children.Add(b);
            }
        }

        // -------------------------------------------------------------- toolbar

        private Button ToolButton(string label, string tip, Action click, bool on, string automation = null)
        {
            var b = Ui.Button(label, click, "Btn.Chip");
            b.Margin = new Thickness(0, 0, 6, 6);
            b.ToolTip = tip;
            AutomationProperties.SetName(b, automation ?? label);
            AutomationProperties.SetHelpText(b, tip);
            b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Sunken");
            b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
            return b;
        }

        private static string KeyName(Key k) => k.ToString().ToUpperInvariant();

        private void RebuildToolbar()
        {
            bool vertical = Settings.Current.DrawDock == "left" || Settings.Current.DrawDock == "right";
            _toolbarHost.Child = null;
            if (Settings.Current.DrawToolbarMinimised)
            {
                // Minimised: a disc that still shows the active tool.
                var disc = new Button { Style = Ui.Style("Btn.Accent"), Width = 52, Height = 52, Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 6) };
                disc.Content = new TextBlock { Text = ActiveToolName().Substring(0, 2), FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                disc.ToolTip = $"Drawing toolbar, minimised. Active tool: {ActiveToolName()}. Click to expand.";
                AutomationProperties.SetName(disc, "Expand the drawing toolbar, active tool " + ActiveToolName());
                disc.Click += (s, e) => { Settings.Current.DrawToolbarMinimised = false; Settings.Save(); SwapToolbar(); };
                _toolbarHost.CornerRadius = new CornerRadius(28);
                _toolbarHost.Child = disc;
                return;
            }
            _toolbarHost.CornerRadius = new CornerRadius(28);
            Panel row = vertical ? new StackPanel { Orientation = Orientation.Vertical, Width = 168 } : new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = Math.Max(480, ActualWidth - 60) };

            // The first group changes shape: tools, a palette, or size sliders.
            if (_panel == "colour") AddColourPanel(row);
            else if (_panel == "size") AddSizePanel(row);
            else AddToolPanel(row);

            row.Children.Add(Sep());
            row.Children.Add(ToolButton("Undo", "Undo (Ctrl+Z)", Undo, false, "Undo"));
            row.Children.Add(ToolButton("Redo", "Redo (Ctrl+Y)", Redo, false, "Redo"));
            var swatch = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(_color), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) };
            var colourBtn = ToolButton("", "Colour (C): turns the toolbar into a palette", () => TogglePanel("colour"), _panel == "colour", "Colour");
            colourBtn.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { swatch, new TextBlock { Text = "Colour", VerticalAlignment = VerticalAlignment.Center } } };
            row.Children.Add(colourBtn);
            row.Children.Add(ToolButton("Size " + (int)_width, "Stroke width (Z): turns the toolbar into sliders", () => TogglePanel("size"), _panel == "size", "Stroke size " + (int)_width));
            row.Children.Add(Sep());
            row.Children.Add(ToolButton("Save PNG", "Save a PNG next to the original (Ctrl+S)", SavePng, false, "Save PNG"));
            row.Children.Add(ToolButton("PNG " + Settings.Current.DrawExportScale + "x", "PNG export scale: 1x, 2x or 3x of the image size. Click to change.", CycleScale, false, "PNG scale " + Settings.Current.DrawExportScale + "x"));
            row.Children.Add(ToolButton("SVG", "Save an SVG of exactly what is on the board", SaveSvg, false, "Save SVG"));
            row.Children.Add(ToolButton("Copy", "Copy the picture to the clipboard (Ctrl+Shift+C)", CopyImage, false, "Copy image to clipboard"));
            row.Children.Add(ToolButton("Clear", "Remove everything drawn (asks first)", ClearBoard, false, "Clear drawing"));
            row.Children.Add(Sep());
            row.Children.Add(ToolButton("Dock: " + Settings.Current.DrawDock, "Move the toolbar to another edge", CycleDock, false, "Toolbar position " + Settings.Current.DrawDock));
            row.Children.Add(ToolButton("Minimise", "Collapse the toolbar to a disc (Esc)", () => { Settings.Current.DrawToolbarMinimised = true; Settings.Save(); SwapToolbar(); }, false, "Minimise the toolbar"));
            _toolbarHost.Child = row;
        }

        private static FrameworkElement Sep() => new Border { Width = 1, Height = 22, Background = Ui.Res("B.Line"), Margin = new Thickness(4, 0, 10, 6), VerticalAlignment = VerticalAlignment.Top };

        private void AddToolPanel(Panel row)
        {
            foreach (var p in Pens)
            {
                var pen = p;
                row.Children.Add(ToolButton(p.Name, $"{p.Name} ({KeyName(p.Key)}): {p.Hint}", () => { _pen = pen; Settings.Current.DrawPen = pen.Id; Settings.Save(); SelectTool(Tool.Pen); }, _tool == Tool.Pen && _pen == p, p.Name));
            }
            row.Children.Add(Sep());
            foreach (var t in OtherTools)
            {
                var tool = t.Tool;
                row.Children.Add(ToolButton(t.Name, $"{t.Name} ({KeyName(t.Key)}): {t.Hint}", () => SelectTool(tool), _tool == t.Tool, t.Name));
            }
        }

        private void AddColourPanel(Panel row)
        {
            row.Children.Add(new TextBlock { Text = "Colour", Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 6) });
            var themed = new (string Name, Color C)[]
            {
                ("Primary", Ui.C("C.Mint")), ("Amber", Ui.C("C.Amber")), ("Rose", Ui.C("C.Danger")), ("Violet", Ui.C("C.Violet")),
                ("Sky", Ui.C("C.OnTertiaryContainer")), ("Light", Ui.C("C.Text")), ("Black", Colors.Black), ("White", Colors.White)
            };
            foreach (var (name, c) in themed)
            {
                var col = c;
                var b = new Button { Width = 34, Height = 34, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(0), ToolTip = name + " (" + Hex(col) + ")", Style = Ui.Style("Btn.Icon") };
                b.Content = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(col), BorderThickness = new Thickness(col == _color ? 3 : 1), BorderBrush = Ui.Res(col == _color ? "B.Text" : "B.LineStrong") };
                AutomationProperties.SetName(b, name + (col == _color ? ", selected" : ""));
                b.Click += (s, e) => SetColour(col);
                row.Children.Add(b);
            }
            row.Children.Add(ToolButton("Custom…", "Pick any colour with red, green and blue sliders or a hex code", ShowCustomColour, false, "Custom colour"));
            row.Children.Add(ToolButton("Done", "Back to the pens (C)", () => TogglePanel("tools"), false, "Close the palette"));
        }

        private void AddSizePanel(Panel row)
        {
            var label = new TextBlock { Text = "Width", Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 6) };
            var slider = new Slider { Minimum = 1, Maximum = 48, Value = _width, Width = 220, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 6) };
            AutomationProperties.SetName(slider, "Stroke width");
            slider.ToolTip = "Stroke width in pixels; also the eraser and text size. Default 4.";
            // A typed box beside the slider: both change the same width and follow each other.
            var typed = new NumberField("Stroke width", null, Math.Round(_width), 1, 48, 1, v => { slider.Value = v; Settings.Save(); }, "px", 0, false, true) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 6) };
            slider.ValueChanged += (s, e) =>
            {
                _width = slider.Value; typed.SetValue(_width);
                Settings.Current.DrawWidth = _width; ApplyTool();
            };
            slider.PreviewMouseLeftButtonUp += (s, e) => Settings.Save();
            slider.KeyUp += (s, e) => Settings.Save();
            row.Children.Add(label); row.Children.Add(slider); row.Children.Add(typed);
            row.Children.Add(ToolButton("Done", "Back to the pens (Z)", () => TogglePanel("tools"), false, "Close the size controls"));
        }

        private void TogglePanel(string panel)
        {
            _panel = _panel == panel ? "tools" : panel;
            SwapToolbar();
        }

        /// <summary>Rebuilds the toolbar with a short fade, or instantly under reduced motion or Low stimulation.</summary>
        private void SwapToolbar()
        {
            _toolbarHost.CornerRadius = new CornerRadius(28);
            DockPanel.SetDock(_toolbarHost, DockFromSetting());
            RebuildToolbar();
            if (Appearance.ReduceMotion || Modes.ReduceMotion) { _toolbarHost.Opacity = 1; return; }
            _toolbarHost.Opacity = 0.3;
            Ui.Fade(_toolbarHost, 1, 160);
        }

        private void CycleDock()
        {
            var order = new[] { "top", "right", "bottom", "left" };
            var i = Array.IndexOf(order, Settings.Current.DrawDock);
            Settings.Current.DrawDock = order[(i + 1) % order.Length];
            Settings.Save();
            SwapToolbar();
        }

        private void CycleScale()
        {
            Settings.Current.DrawExportScale = Settings.Current.DrawExportScale % 3 + 1;
            Settings.Save();
            SwapToolbar();
        }

        private string ActiveToolName() => _tool == Tool.Pen ? _pen.Name : OtherTools.First(t => t.Tool == _tool).Name;

        // -------------------------------------------------------------- colours

        private void SetColour(Color c)
        {
            _color = c;
            Settings.Current.DrawColor = Hex(c);
            Settings.Save();
            ApplyTool();
            SwapToolbar();
        }

        private void ShowCustomColour()
        {
            var pop = new Window
            {
                Title = "Custom colour", Width = 340, SizeToContent = SizeToContent.Height, Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Ui.Res("B.Raised")
            };
            pop.SourceInitialized += (a, b) => Native.ApplyDarkChrome(pop);
            var sp = new StackPanel { Margin = new Thickness(20) };
            var preview = new Border { Height = 36, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(_color), Margin = new Thickness(0, 0, 0, 12) };
            var hex = Ui_TextBox(Hex(_color));
            Slider S(string name, byte v)
            {
                var s = new Slider { Minimum = 0, Maximum = 255, Value = v, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 8) };
                AutomationProperties.SetName(s, name);
                s.ToolTip = name + " from 0 to 255";
                return s;
            }
            var r = S("Red", _color.R); var g = S("Green", _color.G); var b2 = S("Blue", _color.B);
            bool syncing = false;
            void FromSliders()
            {
                if (syncing) return;
                syncing = true;
                var c = Color.FromRgb((byte)r.Value, (byte)g.Value, (byte)b2.Value);
                preview.Background = new SolidColorBrush(c); hex.Text = Hex(c);
                syncing = false;
            }
            r.ValueChanged += (s, e) => FromSliders(); g.ValueChanged += (s, e) => FromSliders(); b2.ValueChanged += (s, e) => FromSliders();
            hex.TextChanged += (s, e) =>
            {
                if (syncing) return;
                var c = ParseColor(hex.Text);
                if (c == null) return;
                syncing = true; r.Value = c.Value.R; g.Value = c.Value.G; b2.Value = c.Value.B; preview.Background = new SolidColorBrush(c.Value); syncing = false;
            };
            sp.Children.Add(preview);
            sp.Children.Add(new TextBlock { Text = "Red", Foreground = Ui.Res("B.TextSoft") }); sp.Children.Add(r);
            sp.Children.Add(new TextBlock { Text = "Green", Foreground = Ui.Res("B.TextSoft") }); sp.Children.Add(g);
            sp.Children.Add(new TextBlock { Text = "Blue", Foreground = Ui.Res("B.TextSoft") }); sp.Children.Add(b2);
            sp.Children.Add(new TextBlock { Text = "Hex code, for example #3DD6A6", Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 4, 0, 4) });
            sp.Children.Add(hex);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            bar.Children.Add(Ui.Button("Cancel", () => pop.Close()));
            bar.Children.Add(Ui.Button("Use this colour", () => { SetColour(Color.FromRgb((byte)r.Value, (byte)g.Value, (byte)b2.Value)); pop.Close(); }, "Btn.Accent"));
            sp.Children.Add(bar);
            pop.Content = sp;
            pop.ShowDialog();
        }

        private static TextBox Ui_TextBox(string text) => new TextBox { Style = Ui.Style("Input"), Text = text, Margin = new Thickness(0, 0, 0, 4) };

        private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static Color? ParseColor(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            try { var c = ColorConverter.ConvertFromString(s.Trim()); return c is Color col ? Color.FromRgb(col.R, col.G, col.B) : (Color?)null; }
            catch { return null; }
        }

        // -------------------------------------------------------------- tool selection

        private void SelectTool(Tool t)
        {
            CommitEditor();
            _tool = t;
            ApplyTool();
            if (_panel == "tools" && Settings.Current.DrawToolbarMinimised == false && IsLoaded) RebuildToolbar();
            ShowStatus(_tool == Tool.Pen ? $"{_pen.Name}: {_pen.Hint}." : $"{ActiveToolName()}: {OtherTools.First(x => x.Tool == _tool).Hint}.");
        }

        /// <summary>Changes the stroke width from the keyboard ([ and ]). A stroke in progress takes it from its next segment.</summary>
        private void SetWidth(double v)
        {
            v = Math.Max(1, Math.Min(48, Math.Round(v)));
            if (v == _width) return;
            _width = v; Settings.Current.DrawWidth = v; Settings.Save();
            ApplyTool();
            ShowStatus("Stroke width " + (int)v);
            if (IsLoaded) RebuildToolbar();
        }

        private void ApplyTool()
        {
            var attrs = new DrawingAttributes
            {
                Color = Color.FromArgb(_tool == Tool.Pen ? _pen.Alpha : (byte)0xFF, _color.R, _color.G, _color.B),
                Width = Math.Max(1, _tool == Tool.Pen ? _width * _pen.WidthFactor : _width),
                Height = Math.Max(1, _tool == Tool.Pen ? _width * _pen.WidthFactor : _width),
                FitToCurve = _tool == Tool.Pen && _pen.Behavior != Behavior.Constant,
                IgnorePressure = _tool != Tool.Pen || _pen.Behavior == Behavior.Constant,
                StylusTip = _tool == Tool.Pen ? _pen.Tip : StylusTip.Ellipse,
                IsHighlighter = _tool == Tool.Pen && _pen.Highlighter
            };
            if (attrs.Width < 1) { attrs.Width = 1; attrs.Height = 1; }
            _canvas.DefaultDrawingAttributes = attrs;
            _live.Use(attrs);
            switch (_tool)
            {
                case Tool.Pen: _canvas.EditingMode = InkCanvasEditingMode.Ink; _canvas.Cursor = Cursors.Pen; break;
                case Tool.Eraser:
                    var size = Math.Max(6, _width * 3);
                    _canvas.EraserShape = new EllipseStylusShape(size, size);
                    _canvas.EditingMode = InkCanvasEditingMode.EraseByPoint; _canvas.Cursor = Cursors.Cross; break;
                case Tool.Select: _canvas.EditingMode = InkCanvasEditingMode.Select; _canvas.Cursor = Cursors.Arrow; break;
                case Tool.Text: _canvas.EditingMode = InkCanvasEditingMode.None; _canvas.Cursor = Cursors.IBeam; break;
                default: _canvas.EditingMode = InkCanvasEditingMode.None; _canvas.Cursor = Cursors.Cross; break;
            }
        }

        // -------------------------------------------------------------- keyboard

        private void OnKey(object sender, KeyEventArgs e)
        {
            bool editing = _editor != null && _editor.IsKeyboardFocused;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (editing) return;                                   // typing text: no shortcuts
            if (Keyboard.FocusedElement is TextBox) return;
            if (ctrl && key == Key.Z) { if (shift) Redo(); else Undo(); e.Handled = true; return; }
            if (ctrl && key == Key.Y) { Redo(); e.Handled = true; return; }
            if (ctrl && shift && key == Key.C) { CopyImage(); e.Handled = true; return; }
            if (ctrl && key == Key.S) { SavePng(); e.Handled = true; return; }
            if (ctrl || (Keyboard.Modifiers & ModifierKeys.Alt) != 0) return;
            if (key == Key.Delete || key == Key.Back) { DeleteSelection(); e.Handled = true; return; }
            if (key == Key.Escape)
            {
                if (_canvas.GetSelectedStrokes().Count + _canvas.GetSelectedElements().Count > 0) { _canvas.Select(new StrokeCollection()); }
                else { Settings.Current.DrawToolbarMinimised = !Settings.Current.DrawToolbarMinimised; Settings.Save(); SwapToolbar(); }
                e.Handled = true; return;
            }
            if (key == Key.C) { TogglePanel("colour"); e.Handled = true; return; }
            if (key == Key.Z) { TogglePanel("size"); e.Handled = true; return; }
            if (key == Key.OemOpenBrackets || key == Key.OemCloseBrackets)
            {
                SetWidth(_width + (key == Key.OemCloseBrackets ? 1 : -1) * (shift ? 5 : 1));   // also while the pen is down: the next segment takes it
                e.Handled = true; return;
            }
            var pen = Pens.FirstOrDefault(p => p.Key == key);
            if (pen != null) { _pen = pen; Settings.Current.DrawPen = pen.Id; Settings.Save(); SelectTool(Tool.Pen); e.Handled = true; return; }
            foreach (var t in OtherTools) if (t.Key == key) { SelectTool(t.Tool); e.Handled = true; return; }
        }

        private IEnumerable<ElementMenu.Item> MenuItems()
        {
            foreach (var p in Pens) { var pen = p; yield return new ElementMenu.Item(p.Name, null, KeyName(p.Key), () => { _pen = pen; Settings.Current.DrawPen = pen.Id; Settings.Save(); SelectTool(Tool.Pen); }); }
            foreach (var t in OtherTools) { var tool = t.Tool; yield return new ElementMenu.Item(t.Name, null, KeyName(t.Key), () => SelectTool(tool)); }
            yield return new ElementMenu.Item("Colour palette", null, "C", () => TogglePanel("colour"));
            yield return new ElementMenu.Item("Stroke size", null, "Z", () => TogglePanel("size"));
            yield return new ElementMenu.Item("Thinner stroke", null, "[", () => SetWidth(_width - 1));
            yield return new ElementMenu.Item("Thicker stroke", null, "]", () => SetWidth(_width + 1));
            yield return new ElementMenu.Item("Undo", null, "Ctrl+Z", Undo);
            yield return new ElementMenu.Item("Redo", null, "Ctrl+Y", Redo);
            yield return new ElementMenu.Item("Save PNG", null, "Ctrl+S", SavePng);
            yield return new ElementMenu.Item("Copy image", null, "Ctrl+Shift+C", CopyImage);
            yield return new ElementMenu.Item("Clear drawing", null, "", ClearBoard, true);
        }

        // -------------------------------------------------------------- pen behaviour after a stroke

        private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            if (_tool == Tool.Pen)
            {
                // a width changed while the pen was down applies to the whole finished stroke
                e.Stroke.DrawingAttributes.Width = _canvas.DefaultDrawingAttributes.Width;
                e.Stroke.DrawingAttributes.Height = _canvas.DefaultDrawingAttributes.Height;
                try { ShapeStroke(e.Stroke); } catch (Exception ex) { Log.Warn("Pen shaping skipped: " + ex.Message); }
            }
            CommitChange();
        }

        /// <summary>Gives the finished stroke its width behaviour and applies Shift direction locking.</summary>
        private void ShapeStroke(Stroke stroke)
        {
            var pts = stroke.StylusPoints.Select(p => new Point(p.X, p.Y)).ToList();
            if (pts.Count < 2) return;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                var a = pts[0]; var b = pts[^1];
                double dx = b.X - a.X, dy = b.Y - a.Y, len = Math.Sqrt(dx * dx + dy * dy);
                double snapped = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
                var end = new Point(a.X + len * Math.Cos(snapped), a.Y + len * Math.Sin(snapped));
                stroke.DrawingAttributes.FitToCurve = false;
                stroke.StylusPoints = new StylusPointCollection(new[] { a, end });
                return;
            }
            if (_pen.Behavior == Behavior.Constant) return;
            double w = Math.Max(1, _width * _pen.WidthFactor);
            var result = new StylusPointCollection();
            double smooth = 1.0;
            for (int i = 0; i < pts.Count; i++)
            {
                var prev = pts[Math.Max(0, i - 1)]; var next = pts[Math.Min(pts.Count - 1, i + 1)];
                double dx = next.X - prev.X, dy = next.Y - prev.Y;
                double factor;
                if (_pen.Behavior == Behavior.Speed)
                {
                    // Samples arrive at a steady rate, so the gap between neighbours stands for speed.
                    double speed = Math.Sqrt(dx * dx + dy * dy) / 2.0;
                    factor = 1.5 - 1.0 * Math.Min(1.0, speed / (w * 2.5 + 4));
                }
                else
                {
                    // A broad-edged nib: thick across the nib angle (45 degrees), thin along it.
                    double angle = Math.Atan2(dy, dx) - Math.PI / 4;
                    factor = 0.35 + 1.15 * Math.Abs(Math.Sin(angle));
                }
                smooth = i == 0 ? factor : smooth * 0.8 + factor * 0.2;
                float pressure = (float)Math.Max(0.05, Math.Min(1.0, 0.5 * smooth));
                result.Add(new StylusPoint(pts[i].X, pts[i].Y, pressure));
            }
            stroke.StylusPoints = result;
        }

        // -------------------------------------------------------------- shapes

        private bool IsShapeTool => _tool == Tool.Line || _tool == Tool.Arrow || _tool == Tool.Rectangle || _tool == Tool.Ellipse;

        private void OnShapeDown(object sender, MouseButtonEventArgs e)
        {
            if (_tool == Tool.Select && e.ClickCount == 2 && e.OriginalSource is TextBlock note && Equals(note.Tag, "note"))
            {
                _canvas.Select(new StrokeCollection()); BeginText(default, note); e.Handled = true; return;   // double-click edits placed text
            }
            if (_tool == Tool.Text) { BeginText(e.GetPosition(_canvas), null); e.Handled = true; return; }
            if (!IsShapeTool) return;
            _shapeStart = e.GetPosition(_canvas);
            var attrs = _canvas.DefaultDrawingAttributes.Clone();
            attrs.FitToCurve = false; attrs.IgnorePressure = true;
            _shape = new Stroke(new StylusPointCollection(new[] { _shapeStart, _shapeStart }), attrs);
            _canvas.Strokes.Add(_shape);
            _canvas.CaptureMouse();
            e.Handled = true;
        }

        private void OnShapeMove(object sender, MouseEventArgs e)
        {
            if (_shape == null) return;
            _shape.StylusPoints = new StylusPointCollection(ShapePoints(_shapeStart, e.GetPosition(_canvas)));
        }

        private void OnShapeUp(object sender, MouseButtonEventArgs e)
        {
            if (_shape == null) return;
            var end = e.GetPosition(_canvas);
            _canvas.ReleaseMouseCapture();
            var s = _shape; _shape = null;
            if ((end - _shapeStart).Length < 3) { _canvas.Strokes.Remove(s); _dirty = false; return; }   // a click is not a shape
            s.StylusPoints = new StylusPointCollection(ShapePoints(_shapeStart, end));
            CommitChange();
            e.Handled = true;
        }

        private IEnumerable<Point> ShapePoints(Point a, Point b)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            double dx = b.X - a.X, dy = b.Y - a.Y;
            if (shift && (_tool == Tool.Line || _tool == Tool.Arrow))
            {
                double len = Math.Sqrt(dx * dx + dy * dy);
                double ang = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
                dx = len * Math.Cos(ang); dy = len * Math.Sin(ang);
            }
            else if (shift && (_tool == Tool.Rectangle || _tool == Tool.Ellipse))
            {
                double m = Math.Max(Math.Abs(dx), Math.Abs(dy));
                dx = Math.Sign(dx == 0 ? 1 : dx) * m; dy = Math.Sign(dy == 0 ? 1 : dy) * m;
            }
            var end = new Point(a.X + dx, a.Y + dy);
            switch (_tool)
            {
                case Tool.Line: return new[] { a, end };
                case Tool.Arrow:
                    {
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len < 1) return new[] { a, end };
                        double head = Math.Min(len * 0.4, 14 + _width * 2.5), ang = Math.Atan2(dy, dx), spread = Math.PI / 7;
                        var l = new Point(end.X - head * Math.Cos(ang - spread), end.Y - head * Math.Sin(ang - spread));
                        var r = new Point(end.X - head * Math.Cos(ang + spread), end.Y - head * Math.Sin(ang + spread));
                        return new[] { a, end, l, end, r };
                    }
                case Tool.Rectangle:
                    return new[] { a, new Point(end.X, a.Y), end, new Point(a.X, end.Y), a };
                default:
                    {
                        var pts = new List<Point>();
                        double cx = (a.X + end.X) / 2, cy = (a.Y + end.Y) / 2, rx = Math.Abs(dx) / 2, ry = Math.Abs(dy) / 2;
                        for (int i = 0; i <= 72; i++) { double t = i * Math.PI * 2 / 72; pts.Add(new Point(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t))); }
                        return pts;
                    }
            }
        }

        // -------------------------------------------------------------- text

        private void BeginText(Point at, TextBlock existing)
        {
            CommitEditor();
            double size = existing?.FontSize ?? Math.Max(14, _width * 4 + 10);
            var color = existing?.Foreground is SolidColorBrush sb ? sb.Color : _color;
            _editing = existing;
            _editor = new TextBox
            {
                Text = existing?.Text ?? "", AcceptsReturn = true, MinWidth = 140, FontSize = size, Foreground = new SolidColorBrush(color),
                CaretBrush = new SolidColorBrush(color), Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                BorderBrush = Ui.Res("B.Mint"), BorderThickness = new Thickness(1), Padding = new Thickness(2)
            };
            AutomationProperties.SetName(_editor, "Text on the drawing");
            AutomationProperties.SetHelpText(_editor, "Enter places the text, Shift+Enter starts a new line, Escape cancels.");
            if (existing != null) { at = new Point(InkCanvas.GetLeft(existing), InkCanvas.GetTop(existing)); _canvas.Children.Remove(existing); }
            InkCanvas.SetLeft(_editor, at.X); InkCanvas.SetTop(_editor, at.Y);
            _editor.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { CommitEditor(); e.Handled = true; }
                else if (e.Key == Key.Escape) { CancelEditor(); e.Handled = true; }
            };
            _editor.LostKeyboardFocus += (s, e) => { if (_editor != null && !_editor.IsKeyboardFocusWithin) CommitEditor(); };
            _canvas.Children.Add(_editor);
            _editor.Loaded += (s, e) => { _editor.Focus(); Keyboard.Focus(_editor); _editor.SelectAll(); };
        }

        private void CommitEditor()
        {
            if (_editor == null) return;
            var ed = _editor; _editor = null;
            var restore = _editing; _editing = null;
            var text = ed.Text.TrimEnd();
            var left = InkCanvas.GetLeft(ed); var top = InkCanvas.GetTop(ed);
            var brush = ed.Foreground as SolidColorBrush;
            _canvas.Children.Remove(ed);
            if (text.Length == 0) { if (restore != null) CommitChange(); return; }
            AddNote(new TextItem { X = left, Y = top, Text = text, Color = Hex(brush?.Color ?? _color), Size = ed.FontSize });
            CommitChange(forceDirty: true);
        }

        private void CancelEditor()
        {
            if (_editor == null) return;
            var ed = _editor; _editor = null;
            var restore = _editing; _editing = null;
            _canvas.Children.Remove(ed);
            if (restore != null) { _canvas.Children.Add(restore); InkCanvas.SetLeft(restore, InkCanvas.GetLeft(ed)); InkCanvas.SetTop(restore, InkCanvas.GetTop(ed)); }
        }

        private void AddNote(TextItem t)
        {
            var tb = new TextBlock { Text = t.Text, FontSize = t.Size, Foreground = new SolidColorBrush(ParseColor(t.Color) ?? _color), Tag = "note", Background = Brushes.Transparent };
            InkCanvas.SetLeft(tb, t.X); InkCanvas.SetTop(tb, t.Y);
            _canvas.Children.Add(tb);
        }

        // -------------------------------------------------------------- selection

        private void DeleteSelection()
        {
            var strokes = _canvas.GetSelectedStrokes();
            var elements = _canvas.GetSelectedElements().ToList();
            if (strokes.Count == 0 && elements.Count == 0) return;
            _canvas.Select(new StrokeCollection());
            _canvas.Strokes.Remove(strokes);
            foreach (var el in elements) _canvas.Children.Remove(el);
            CommitChange(forceDirty: true);
        }

        // -------------------------------------------------------------- undo and redo

        private Snap Capture()
        {
            using var ms = new MemoryStream();
            _canvas.Strokes.Save(ms, true);
            var texts = _canvas.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, "note")).Select(t => new TextItem
            {
                X = InkCanvas.GetLeft(t), Y = InkCanvas.GetTop(t), Text = t.Text, Size = t.FontSize,
                Color = Hex((t.Foreground as SolidColorBrush)?.Color ?? _color)
            }).ToList();
            return new Snap { Ink = ms.ToArray(), Texts = texts };
        }

        private void ApplySnap(Snap s)
        {
            _suppress = true;
            try
            {
                _canvas.Select(new StrokeCollection());
                _canvas.Strokes.Clear();
                if (s.Ink.Length > 0)
                {
                    using var ms = new MemoryStream(s.Ink);
                    _canvas.Strokes.Add(new StrokeCollection(ms));
                }
                foreach (var tb in _canvas.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, "note")).ToList()) _canvas.Children.Remove(tb);
                foreach (var t in s.Texts) AddNote(t);
            }
            finally { _suppress = false; _dirty = false; }
        }

        private void PushSnapshot(bool initial = false)
        {
            _undo.Add(Capture());
            if (_undo.Count > 120) _undo.RemoveAt(0);
            if (!initial) _redo.Clear();
        }

        private void CommitChange(bool forceDirty = false)
        {
            if (_suppress) return;
            if (!_dirty && !forceDirty) return;
            _dirty = false;
            PushSnapshot();
            SaveBoard();
        }

        private void Undo()
        {
            CommitEditor();
            if (_undo.Count <= 1) { ShowStatus("Nothing to undo."); return; }
            _redo.Add(_undo[^1]); _undo.RemoveAt(_undo.Count - 1);
            ApplySnap(_undo[^1]);
            SaveBoard();
            ShowStatus("Undone.");
        }

        private void Redo()
        {
            CommitEditor();
            if (_redo.Count == 0) { ShowStatus("Nothing to redo."); return; }
            var s = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(s);
            ApplySnap(s);
            SaveBoard();
            ShowStatus("Redone.");
        }

        private void ClearBoard()
        {
            CommitEditor();
            if (_canvas.Strokes.Count == 0 && !_canvas.Children.OfType<TextBlock>().Any(t => Equals(t.Tag, "note"))) { ShowStatus("The board is already empty."); return; }
            // Clearing discards saved strokes, so it passes the two-key confirmation.
            var dlg = new ConfirmWindow("Clear this drawing?", "Everything drawn on this board is removed and the saved copy is overwritten. Undo (Ctrl+Z) can bring it back until you close the window.", "Clear", danger: true) { Owner = this };
            dlg.Confirmed += _ =>
            {
                _suppress = true;
                _canvas.Select(new StrokeCollection());
                _canvas.Strokes.Clear();
                foreach (var tb in _canvas.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, "note")).ToList()) _canvas.Children.Remove(tb);
                _suppress = false;
                CommitChange(forceDirty: true);
                ShowStatus("Cleared.");
            };
            dlg.Show();
        }

        // -------------------------------------------------------------- local persistence

        private static string BoardFolder
        {
            get { var d = Path.Combine(Settings.DataDir, "drawings"); Directory.CreateDirectory(d); return d; }
        }

        private string BoardPath => Path.Combine(BoardFolder, _boardKey + ".json");

        private void SaveBoard()
        {
            if (_boardKey.StartsWith("clip-") && _canvas.Strokes.Count == 0 && !_canvas.Children.OfType<TextBlock>().Any(t => Equals(t.Tag, "note"))) return;
            try
            {
                var snap = Capture();
                File.WriteAllText(BoardPath, JsonSerializer.Serialize(new BoardFile { Ink = Convert.ToBase64String(snap.Ink), Texts = snap.Texts }));
            }
            catch (Exception ex) { Log.Warn("Drawing could not be saved: " + ex.Message); }
        }

        private void RestoreBoard()
        {
            if (_boardKey.StartsWith("clip-")) return;
            try
            {
                if (!File.Exists(BoardPath)) return;
                var f = JsonSerializer.Deserialize<BoardFile>(File.ReadAllText(BoardPath));
                if (f == null) return;
                ApplySnap(new Snap { Ink = string.IsNullOrEmpty(f.Ink) ? Array.Empty<byte>() : Convert.FromBase64String(f.Ink), Texts = f.Texts ?? new List<TextItem>() });
            }
            catch (Exception ex) { Log.Warn("Saved drawing could not be restored: " + ex.Message); }
        }

        // -------------------------------------------------------------- output

        private RenderTargetBitmap Render(int scale)
        {
            CommitEditor();
            _canvas.Select(new StrokeCollection());      // selection handles are not part of the picture
            _board.UpdateLayout();
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
                dc.DrawRectangle(new VisualBrush(_board) { Stretch = Stretch.Fill }, null, new Rect(0, 0, _w, _h));
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(_w * scale), (int)Math.Ceiling(_h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private string NextOutputPath(string ext)
        {
            string dir, stem;
            if (_sourcePath != null && File.Exists(_sourcePath)) { dir = Path.GetDirectoryName(_sourcePath); stem = Path.GetFileNameWithoutExtension(_sourcePath) + "-annotated"; }
            else
            {
                dir = Path.Combine(Settings.Current.ResolvedMediaFolder, "Drawings"); Directory.CreateDirectory(dir);
                stem = "Drawing " + DateTime.Now.ToString("yyyy-MM-dd HHmmss");
            }
            var p = Path.Combine(dir, stem + ext);
            for (int i = 2; File.Exists(p); i++) p = Path.Combine(dir, $"{stem} ({i}){ext}");
            return p;
        }

        private void SavePng()
        {
            try
            {
                int scale = Math.Max(1, Math.Min(3, Settings.Current.DrawExportScale));
                var bmp = Render(scale);
                var path = NextOutputPath(".png");
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = File.Create(path)) enc.Save(fs);
                _lastSaved = path;
                ShowStatus($"Saved {Path.GetFileName(path)} at {scale}x ({bmp.PixelWidth} by {bmp.PixelHeight} pixels). The original was not changed.", path);
            }
            catch (Exception ex) { ShowStatus("The PNG could not be saved: " + ex.Message); }
        }

        private void CopyImage()
        {
            try
            {
                var bmp = Render(Math.Max(1, Math.Min(3, Settings.Current.DrawExportScale)));
                ClipboardWatcher.IgnoreNextChange();
                ClipboardWatcher.SetImage(bmp);
                ShowStatus("Copied the picture to the clipboard.");
            }
            catch (Exception ex) { ShowStatus("Could not copy the picture: " + ex.Message); }
        }

        private void SaveSvg()
        {
            try
            {
                CommitEditor();
                _canvas.Select(new StrokeCollection());
                var path = NextOutputPath(".svg");
                File.WriteAllText(path, BuildSvg(), new UTF8Encoding(false));
                ShowStatus($"Saved {Path.GetFileName(path)}. Strokes are saved as outlines, exactly as drawn, including erased parts.", path);
            }
            catch (Exception ex) { ShowStatus("The SVG could not be saved: " + ex.Message); }
        }

        private string BuildSvg()
        {
            var inv = CultureInfo.InvariantCulture;
            string N(double d) => d.ToString("0.##", inv);
            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{N(_w)}\" height=\"{N(_h)}\" viewBox=\"0 0 {N(_w)} {N(_h)}\">\n");
            if (_image != null)
            {
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(_image));
                using var ms = new MemoryStream(); enc.Save(ms);
                sb.Append($"<image x=\"0\" y=\"0\" width=\"{N(_w)}\" height=\"{N(_h)}\" xlink:href=\"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}\"/>\n");
            }
            else sb.Append($"<rect width=\"{N(_w)}\" height=\"{N(_h)}\" fill=\"#FFFFFF\"/>\n");
            foreach (var stroke in _canvas.Strokes)
            {
                var geo = stroke.GetGeometry(stroke.DrawingAttributes).GetFlattenedPathGeometry(0.2, ToleranceType.Absolute);
                var d = new StringBuilder();
                foreach (var fig in geo.Figures)
                {
                    d.Append($"M{N(fig.StartPoint.X)} {N(fig.StartPoint.Y)}");
                    foreach (var seg in fig.Segments)
                    {
                        if (seg is PolyLineSegment pl) foreach (var p in pl.Points) d.Append($"L{N(p.X)} {N(p.Y)}");
                        else if (seg is LineSegment ls) d.Append($"L{N(ls.Point.X)} {N(ls.Point.Y)}");
                    }
                    if (fig.IsClosed) d.Append('Z');
                }
                var c = stroke.DrawingAttributes.Color;
                double opacity = stroke.DrawingAttributes.IsHighlighter ? 0.5 : c.A / 255.0;
                sb.Append($"<path d=\"{d}\" fill=\"rgb({c.R},{c.G},{c.B})\" fill-opacity=\"{N(opacity)}\" fill-rule=\"{(geo.FillRule == FillRule.EvenOdd ? "evenodd" : "nonzero")}\"/>\n");
            }
            foreach (var tb in _canvas.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, "note")))
            {
                var c = (tb.Foreground as SolidColorBrush)?.Color ?? _color;
                double x = InkCanvas.GetLeft(tb), y = InkCanvas.GetTop(tb);
                sb.Append($"<text x=\"{N(x)}\" y=\"{N(y + tb.FontSize)}\" font-size=\"{N(tb.FontSize)}\" font-family=\"Segoe UI, sans-serif\" fill=\"rgb({c.R},{c.G},{c.B})\">");
                var lines = tb.Text.Replace("\r", "").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                    sb.Append($"<tspan x=\"{N(x)}\" dy=\"{(i == 0 ? "0" : N(tb.FontSize * 1.25))}\">{System.Security.SecurityElement.Escape(lines[i])}</tspan>");
                sb.Append("</text>\n");
            }
            sb.Append("</svg>\n");
            return sb.ToString();
        }
    }
}
