using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("NAVIGATOR // E-Ink Hourglass")]
[assembly: AssemblyDescription("Ultra-lightweight E-Ink & Technical Instrument Hourglass Timer for Windows")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Navigator Instruments")]
[assembly: AssemblyProduct("NAVIGATOR Hourglass Widget")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace HourglassApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (args != null && args.Length >= 2 && args[0] == "--preview")
                {
                    using (HourglassForm f = new HourglassForm())
                    {
                        f.SavePreviewImage(args[1]);
                    }
                    return;
                }
                if (args != null && args.Length >= 2 && args[0] == "--make-icon")
                {
                    GenerateMultiSizeIcon(args[1]);
                    return;
                }
                if (args != null && args.Length >= 2 && args[0] == "--make-video")
                {
                    VideoBuilder.GenerateTutorialVideo(args[1]);
                    return;
                }
                Application.Run(new HourglassForm());
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), ex.ToString());
            }
        }

        private static void GenerateMultiSizeIcon(string icoPath)
        {
            int[] sizes = new int[] { 256, 64, 48, 32, 16 };
            List<byte[]> pngBuffers = new List<byte[]>();

            foreach (int sz in sizes)
            {
                using (Bitmap bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    float s = sz / 64.0f;
                    g.ScaleTransform(s, s);

                    // E-Ink rounded instrument tile
                    using (GraphicsPath bgPath = HourglassForm.CreateRoundedRect(4f, 4f, 56f, 56f, 12f))
                    using (SolidBrush paperBrush = new SolidBrush(Color.FromArgb(232, 230, 224)))
                    using (Pen borderPen = new Pen(Color.FromArgb(28, 30, 34), Math.Max(2.2f, 2.8f / s)))
                    {
                        g.FillPath(paperBrush, bgPath);
                        g.DrawPath(borderPen, bgPath);
                    }

                    // Hourglass caps & bulbs inside 64x64 space
                    using (SolidBrush inkBrush = new SolidBrush(Color.FromArgb(28, 30, 34)))
                    using (Pen inkPen = new Pen(Color.FromArgb(28, 30, 34), Math.Max(2.2f, 2.4f / s)))
                    {
                        inkPen.LineJoin = LineJoin.Round;
                        g.FillRectangle(inkBrush, 15f, 11f, 34f, 4.5f);
                        g.FillRectangle(inkBrush, 15f, 48.5f, 34f, 4.5f);

                        PointF[] bulbPts = new PointF[]
                        {
                            new PointF(18f, 15f),
                            new PointF(46f, 15f),
                            new PointF(34.5f, 32f),
                            new PointF(46f, 49f),
                            new PointF(18f, 49f),
                            new PointF(29.5f, 32f)
                        };
                        g.DrawPolygon(inkPen, bulbPts);

                        // Top & Bottom sand triangles
                        g.FillPolygon(inkBrush, new PointF[]
                        {
                            new PointF(22f, 21f),
                            new PointF(42f, 21f),
                            new PointF(32f, 31f)
                        });
                        g.FillPolygon(inkBrush, new PointF[]
                        {
                            new PointF(19.5f, 48.5f),
                            new PointF(44.5f, 48.5f),
                            new PointF(32f, 38f)
                        });

                        // Falling pixel dots
                        g.FillRectangle(inkBrush, 30.8f, 33f, 2.4f, 2.4f);
                        g.FillRectangle(inkBrush, 30.8f, 36.5f, 2.4f, 2.4f);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        pngBuffers.Add(ms.ToArray());
                    }
                }
            }

            using (FileStream fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // ICONDIR header
                bw.Write((short)0);
                bw.Write((short)1); // Type 1 = ICON
                bw.Write((short)sizes.Length);

                int offset = 6 + sizes.Length * 16;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    bw.Write((byte)(sz >= 256 ? 0 : sz));
                    bw.Write((byte)(sz >= 256 ? 0 : sz));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    bw.Write(pngBuffers[i].Length);
                    bw.Write(offset);
                    offset += pngBuffers[i].Length;
                }

                for (int i = 0; i < pngBuffers.Count; i++)
                {
                    bw.Write(pngBuffers[i]);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }

    public enum TimerState
    {
        Ready,
        Running,
        Paused,
        Alarm
    }

    public sealed class EInkPalette
    {
        public string Name;
        public Color PaperBg;        // Matte E-Ink background / chamber fill
        public Color BezelOuter;     // Hardware frame outer color
        public Color BezelInner;     // Hardware frame inner lip
        public Color InkPrimary;     // Primary crisp line & active dot color
        public Color InkSecondary;   // Secondary hairline / tick / muted label color
        public Color InkGhost;       // Inactive dot-matrix ghost pixel color
        public Color AccentInk;      // Subtle technical accent (reticle / alarm state)
        public Color OuterHalo;      // Subtle contrast halo in transparent mode

        public EInkPalette(
            string name,
            Color paperBg, Color bezelOuter, Color bezelInner,
            Color inkPrimary, Color inkSecondary, Color inkGhost,
            Color accentInk, Color outerHalo)
        {
            Name = name;
            PaperBg = paperBg;
            BezelOuter = bezelOuter;
            BezelInner = bezelInner;
            InkPrimary = inkPrimary;
            InkSecondary = inkSecondary;
            InkGhost = inkGhost;
            AccentInk = accentInk;
            OuterHalo = outerHalo;
        }
    }

    public sealed class HourglassForm : Form
    {
        // Win32 Layered Window APIs for Per-Pixel Alpha Transparency
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const int ULW_ALPHA = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
            public POINT(int x, int y) { this.x = x; this.y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, uint crKey,
            ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        // Base canvas dimensions at 1.0x scale
        private const int BaseWidth = 210;
        private const int BaseHeight = 310;

        // 5x7 Dot-Matrix Font Bitmap Table (Each byte array has 7 rows, 5 bits per row: bit 4..0)
        private static readonly Dictionary<char, byte[]> DotFont = BuildDotMatrixFont();

        // State & Timing
        private TimerState _state = TimerState.Ready;
        private double _totalSeconds = 300.0; // Default 5 minutes
        private double _remainingSeconds = 300.0;
        private long _lastTickTimestamp;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        // Animation & Visual State
        private readonly Timer _animTimer;
        private float _scale = 1.0f;
        private bool _alwaysOnTop = true;
        private bool _showDigitalTime = true;
        private bool _soundEnabled = true;
        private bool _panelMode = false; // false = Transparent Instrument Line-Art, true = E-Ink Panel Card
        private int _themeIndex = 0;

        // Flip animation (0.0 -> 1.0)
        private bool _isFlipping = false;
        private float _flipProgress = 1.0f;
        private long _flipStartMs;
        private const int FlipDurationMs = 420;

        // Temporary HUD overlay when scrolling mouse wheel
        private long _hudShowUntilMs = 0;
        private string _hudCustomText = null;

        // Alarm state
        private long _alarmStartMs = 0;
        private long _lastSoundPlayMs = 0;
        private SoundPlayer _chimePlayer;
        private MemoryStream _chimeStream;

        // Dragging vs Clicking
        private bool _isMouseDown = false;
        private bool _isDragging = false;
        private Point _mouseDownScreenPos;
        private Point _formStartPos;
        private long _lastClickMs = 0;

        // Reusable surface to minimize allocations
        private Bitmap _canvasBitmap;
        private readonly EInkPalette[] _themes;

        // UI Controls
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _menuStartPause;
        private ToolStripMenuItem _menuModeTransparent;
        private ToolStripMenuItem _menuModePanel;
        private ToolStripMenuItem _menuAlwaysOnTop;
        private ToolStripMenuItem _menuShowTime;
        private ToolStripMenuItem _menuSound;
        private readonly List<ToolStripMenuItem> _scaleMenuItems = new List<ToolStripMenuItem>();
        private readonly List<ToolStripMenuItem> _themeMenuItems = new List<ToolStripMenuItem>();

        private string SettingsFilePath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "hourglass_settings.ini");
            }
        }

        public HourglassForm()
        {
            _themes = new EInkPalette[]
            {
                // 0: Light E-Ink (Navigator / E-Paper Matte Gray & Charcoal Ink)
                new EInkPalette(
                    "라이트 잉크 (E-Paper 기본)",
                    Color.FromArgb(228, 227, 221), // #E4E3DD warm matte paper
                    Color.FromArgb(242, 242, 238), // #F2F2EE white hardware bezel
                    Color.FromArgb(195, 194, 188), // inner bezel lip
                    Color.FromArgb(30, 32, 34),    // #1E2022 deep charcoal ink
                    Color.FromArgb(105, 108, 104), // secondary technical gray
                    Color.FromArgb(206, 205, 198), // ghost matrix dots
                    Color.FromArgb(198, 62, 42),   // technical orange-red accent
                    Color.FromArgb(210, 245, 245, 240)),

                // 1: Dark Inverted (Backlit OLED / White Ink on Carbon)
                new EInkPalette(
                    "다크 반전 (화이트 라인)",
                    Color.FromArgb(26, 29, 34),    // #1A1D22 deep matte slate
                    Color.FromArgb(42, 46, 54),    // dark bezel
                    Color.FromArgb(68, 74, 84),    // inner lip
                    Color.FromArgb(236, 238, 234), // #ECEEEA crisp white ink
                    Color.FromArgb(132, 138, 146), // secondary tick gray
                    Color.FromArgb(42, 46, 53),    // ghost matrix dots
                    Color.FromArgb(255, 105, 75),  // alarm accent
                    Color.FromArgb(215, 15, 17, 20)),

                // 2: Warm Cream Paper (Braun / Industrial Blueprint)
                new EInkPalette(
                    "웜 크림 계기판",
                    Color.FromArgb(240, 235, 222), // warm vintage paper
                    Color.FromArgb(222, 216, 202), // warm putty bezel
                    Color.FromArgb(182, 175, 162),
                    Color.FromArgb(36, 32, 28),    // espresso ink
                    Color.FromArgb(118, 110, 98),
                    Color.FromArgb(220, 214, 200),
                    Color.FromArgb(210, 75, 35),
                    Color.FromArgb(210, 248, 244, 234)),

                // 3: Cockpit Amber (Night Instrument Panel)
                new EInkPalette(
                    "다크 앰버 계기판",
                    Color.FromArgb(22, 20, 18),
                    Color.FromArgb(38, 34, 30),
                    Color.FromArgb(72, 60, 45),
                    Color.FromArgb(245, 172, 42),  // aviation amber ink
                    Color.FromArgb(148, 104, 32),
                    Color.FromArgb(44, 36, 24),
                    Color.FromArgb(255, 215, 90),
                    Color.FromArgb(220, 14, 12, 10)),

                // 4: Phosphor Green (Oscilloscope / Terminal)
                new EInkPalette(
                    "다크 그린 터미널",
                    Color.FromArgb(18, 24, 21),
                    Color.FromArgb(32, 42, 37),
                    Color.FromArgb(55, 78, 66),
                    Color.FromArgb(88, 228, 138),  // phosphor green ink
                    Color.FromArgb(54, 138, 86),
                    Color.FromArgb(28, 42, 34),
                    Color.FromArgb(160, 255, 195),
                    Color.FromArgb(220, 10, 15, 12))
            };

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = _alwaysOnTop;
            Text = "NAVIGATOR // HOURGLASS";

            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(workArea.Right - BaseWidth - 40, workArea.Bottom - BaseHeight - 40);
            Size = new Size(BaseWidth, BaseHeight);

            LoadSettings();
            ApplyScale(_scale, false);

            BuildChimeSound();
            BuildContextMenu();
            BuildTrayIcon();

            _animTimer = new Timer();
            _animTimer.Interval = 50;
            _animTimer.Tick += OnAnimTimerTick;

            MouseDown += OnFormMouseDown;
            MouseMove += OnFormMouseMove;
            MouseUp += OnFormMouseUp;
            MouseWheel += OnFormMouseWheel;
            KeyDown += OnFormKeyDown;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
                if (_alwaysOnTop)
                {
                    cp.ExStyle |= WS_EX_TOPMOST;
                }
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TopMost = _alwaysOnTop;
            ShowHudTemporarily("WHEEL: SET / CLICK: RUN", 2400);
            RenderLayeredWindow();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            _animTimer.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            if (_canvasBitmap != null)
            {
                _canvasBitmap.Dispose();
            }
            if (_chimePlayer != null)
            {
                _chimePlayer.Dispose();
            }
            if (_chimeStream != null)
            {
                _chimeStream.Dispose();
            }
            base.OnFormClosing(e);
        }

        // =====================================================================
        // Settings Persistence
        // =====================================================================
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath)) return;
                string[] lines = File.ReadAllLines(SettingsFilePath);
                int savedX = Location.X;
                int savedY = Location.Y;
                bool hasPos = false;

                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (key == "TotalSeconds")
                    {
                        double s;
                        if (double.TryParse(val, out s) && s >= 5 && s <= 86400)
                        {
                            _totalSeconds = s;
                            _remainingSeconds = s;
                        }
                    }
                    else if (key == "Scale")
                    {
                        float sc;
                        if (float.TryParse(val, out sc) && sc >= 0.5f && sc <= 2.5f)
                            _scale = sc;
                    }
                    else if (key == "AlwaysOnTop")
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) _alwaysOnTop = b;
                    }
                    else if (key == "ShowDigitalTime")
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) _showDigitalTime = b;
                    }
                    else if (key == "SoundEnabled")
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) _soundEnabled = b;
                    }
                    else if (key == "PanelMode")
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) _panelMode = b;
                    }
                    else if (key == "ThemeIndex")
                    {
                        int ti;
                        if (int.TryParse(val, out ti) && ti >= 0 && ti < _themes.Length)
                            _themeIndex = ti;
                    }
                    else if (key == "X")
                    {
                        if (int.TryParse(val, out savedX)) hasPos = true;
                    }
                    else if (key == "Y")
                    {
                        if (int.TryParse(val, out savedY)) hasPos = true;
                    }
                }

                if (hasPos)
                {
                    Point pt = new Point(savedX, savedY);
                    bool visible = false;
                    foreach (Screen scr in Screen.AllScreens)
                    {
                        if (scr.WorkingArea.Contains(pt.X + 40, pt.Y + 40))
                        {
                            visible = true;
                            break;
                        }
                    }
                    if (visible) Location = pt;
                }
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            try
            {
                string[] lines = new string[]
                {
                    "TotalSeconds=" + _totalSeconds.ToString("F0"),
                    "Scale=" + _scale.ToString("F2"),
                    "AlwaysOnTop=" + _alwaysOnTop,
                    "ShowDigitalTime=" + _showDigitalTime,
                    "SoundEnabled=" + _soundEnabled,
                    "PanelMode=" + _panelMode,
                    "ThemeIndex=" + _themeIndex,
                    "X=" + Location.X,
                    "Y=" + Location.Y
                };
                File.WriteAllLines(SettingsFilePath, lines);
            }
            catch
            {
            }
        }

        // =====================================================================
        // Synthesized Chime Audio (Zero External Dependencies)
        // =====================================================================
        private void BuildChimeSound()
        {
            try
            {
                int sampleRate = 22050;
                double durationSec = 1.35;
                int numSamples = (int)(sampleRate * durationSec);
                int dataBytes = numSamples * 2;

                _chimeStream = new MemoryStream(44 + dataBytes);
                BinaryWriter bw = new BinaryWriter(_chimeStream);

                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataBytes);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataBytes);

                // Braun-inspired clean digital-acoustic double beep chime
                for (int i = 0; i < numSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    double sample = 0.0;

                    double env1 = Math.Exp(-t * 5.0);
                    sample += 0.34 * env1 * Math.Sin(2.0 * Math.PI * 1046.50 * t);
                    sample += 0.10 * Math.Exp(-t * 9.0) * Math.Sin(2.0 * Math.PI * 2093.00 * t);

                    if (t >= 0.18)
                    {
                        double t2 = t - 0.18;
                        double env2 = Math.Exp(-t2 * 3.8);
                        sample += 0.38 * env2 * Math.Sin(2.0 * Math.PI * 1567.98 * t2);
                        sample += 0.12 * Math.Exp(-t2 * 7.5) * Math.Sin(2.0 * Math.PI * 3135.96 * t2);
                    }

                    if (sample > 0.95) sample = 0.95;
                    if (sample < -0.95) sample = -0.95;

                    short pcm = (short)(sample * 32767);
                    bw.Write(pcm);
                }

                _chimeStream.Position = 0;
                _chimePlayer = new SoundPlayer(_chimeStream);
                _chimePlayer.Load();
            }
            catch
            {
            }
        }

        private void PlayAlarmChime()
        {
            if (!_soundEnabled) return;
            try
            {
                if (_chimePlayer != null && _chimeStream != null)
                {
                    _chimeStream.Position = 0;
                    _chimePlayer.Play();
                }
                else
                {
                    SystemSounds.Exclamation.Play();
                }
            }
            catch
            {
            }
        }

        // =====================================================================
        // Context Menu & System Tray Icon
        // =====================================================================
        private void BuildContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            _menuStartPause = new ToolStripMenuItem("시작 (클릭)", null, (s, e) => ToggleStartPause());
            _menuStartPause.Font = new Font(_contextMenu.Font, FontStyle.Bold);
            _contextMenu.Items.Add(_menuStartPause);

            var menuFlip = new ToolStripMenuItem("모래시계 뒤집기 / 재시작 (더블클릭)", null, (s, e) => FlipAndRestart());
            _contextMenu.Items.Add(menuFlip);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Display Mode Switcher (Transparent Line-Art vs E-Ink Panel Card)
            _menuModeTransparent = new ToolStripMenuItem("모드: 투명 라인아트 계기판 (단축키 M)", null, (s, e) =>
            {
                _panelMode = false;
                UpdateMenuChecks();
                SaveSettings();
                RenderLayeredWindow();
            });
            _menuModePanel = new ToolStripMenuItem("모드: E-Ink 디스플레이 패널 카드 (단축키 M)", null, (s, e) =>
            {
                _panelMode = true;
                UpdateMenuChecks();
                SaveSettings();
                RenderLayeredWindow();
            });
            _contextMenu.Items.Add(_menuModeTransparent);
            _contextMenu.Items.Add(_menuModePanel);

            // Theme Submenu
            var menuTheme = new ToolStripMenuItem("잉크 색상 테마 (라이트 / 다크 반전)");
            for (int i = 0; i < _themes.Length; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(_themes[i].Name, null, (s, e) =>
                {
                    _themeIndex = idx;
                    UpdateMenuChecks();
                    SaveSettings();
                    RenderLayeredWindow();
                });
                item.Tag = idx;
                _themeMenuItems.Add(item);
                menuTheme.DropDownItems.Add(item);
            }
            _contextMenu.Items.Add(menuTheme);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Quick Presets
            var menuPresets = new ToolStripMenuItem("빠른 시간 설정 (마우스 휠)");
            int[] presetSeconds = new int[] { 30, 60, 180, 300, 600, 900, 1500, 1800, 2700, 3600 };
            string[] presetNames = new string[]
            {
                "30초", "1분", "3분", "5분", "10분",
                "15분", "25분 (포모도로)", "30분", "45분", "60분 (1시간)"
            };
            for (int i = 0; i < presetSeconds.Length; i++)
            {
                int sec = presetSeconds[i];
                var item = new ToolStripMenuItem(presetNames[i], null, (s, e) => SetNewDuration(sec, true));
                menuPresets.DropDownItems.Add(item);
            }
            _contextMenu.Items.Add(menuPresets);

            var menuCustomTime = new ToolStripMenuItem("시간 직접 입력...", null, (s, e) => ShowCustomTimeDialog());
            _contextMenu.Items.Add(menuCustomTime);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Options
            _menuAlwaysOnTop = new ToolStripMenuItem("항상 위에 고정", null, (s, e) =>
            {
                _alwaysOnTop = !_alwaysOnTop;
                TopMost = _alwaysOnTop;
                _menuAlwaysOnTop.Checked = _alwaysOnTop;
                SaveSettings();
            });
            _contextMenu.Items.Add(_menuAlwaysOnTop);

            _menuShowTime = new ToolStripMenuItem("하단 도트 타이머 표시 (휠클릭)", null, (s, e) =>
            {
                _showDigitalTime = !_showDigitalTime;
                _menuShowTime.Checked = _showDigitalTime;
                SaveSettings();
                RenderLayeredWindow();
            });
            _contextMenu.Items.Add(_menuShowTime);

            _menuSound = new ToolStripMenuItem("알람 소리 켜기", null, (s, e) =>
            {
                _soundEnabled = !_soundEnabled;
                _menuSound.Checked = _soundEnabled;
                SaveSettings();
            });
            _contextMenu.Items.Add(_menuSound);

            // Size submenu
            var menuSize = new ToolStripMenuItem("크기 조절 (Ctrl + 마우스 휠)");
            float[] scales = new float[] { 0.75f, 1.0f, 1.25f, 1.5f, 2.0f };
            string[] scaleNames = new string[] { "작게 (75%)", "기본 (100%)", "크게 (125%)", "아주 크게 (150%)", "특대 (200%)" };
            for (int i = 0; i < scales.Length; i++)
            {
                float sc = scales[i];
                var item = new ToolStripMenuItem(scaleNames[i], null, (s, e) => ApplyScale(sc, true));
                item.Tag = sc;
                _scaleMenuItems.Add(item);
                menuSize.DropDownItems.Add(item);
            }
            _contextMenu.Items.Add(menuSize);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var menuHelp = new ToolStripMenuItem("조작 방법 안내", null, (s, e) => ShowHelpMessage());
            _contextMenu.Items.Add(menuHelp);

            var menuExit = new ToolStripMenuItem("종료", null, (s, e) => Close());
            _contextMenu.Items.Add(menuExit);

            _contextMenu.Opening += (s, e) => UpdateMenuChecks();
            ContextMenuStrip = _contextMenu;
            UpdateMenuChecks();
        }

        private void UpdateMenuChecks()
        {
            if (_state == TimerState.Running)
                _menuStartPause.Text = "일시정지 (클릭)";
            else if (_state == TimerState.Paused)
                _menuStartPause.Text = "계속하기 (클릭)";
            else if (_state == TimerState.Alarm)
                _menuStartPause.Text = "알람 끄기 (클릭)";
            else
                _menuStartPause.Text = "시작 (클릭)";

            _menuModeTransparent.Checked = !_panelMode;
            _menuModePanel.Checked = _panelMode;
            _menuAlwaysOnTop.Checked = _alwaysOnTop;
            _menuShowTime.Checked = _showDigitalTime;
            _menuSound.Checked = _soundEnabled;

            foreach (var item in _scaleMenuItems)
            {
                float sc = (float)item.Tag;
                item.Checked = Math.Abs(_scale - sc) < 0.05f;
            }
            for (int i = 0; i < _themeMenuItems.Count; i++)
            {
                _themeMenuItems[i].Checked = (i == _themeIndex);
            }
        }

        private void BuildTrayIcon()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = CreateTrayHourglassIcon();
            _trayIcon.Text = "모래시계 - " + FormatTime(_remainingSeconds);
            _trayIcon.ContextMenuStrip = _contextMenu;
            _trayIcon.Visible = true;
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    Show();
                    Activate();
                    if (_state == TimerState.Alarm)
                    {
                        StopAlarmAndReset();
                    }
                }
            };
        }

        private Icon CreateTrayHourglassIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(230, 228, 222)))
                using (Pen inkPen = new Pen(Color.FromArgb(30, 32, 34), 2.2f))
                using (Brush inkBrush = new SolidBrush(Color.FromArgb(30, 32, 34)))
                {
                    g.FillRectangle(bgBrush, 3, 3, 26, 26);
                    g.DrawRectangle(inkPen, 3, 3, 26, 26);

                    g.DrawLine(inkPen, 7, 7, 25, 7);
                    g.DrawLine(inkPen, 7, 25, 25, 25);

                    PointF[] pts = new PointF[]
                    {
                        new PointF(9, 7),
                        new PointF(23, 7),
                        new PointF(17, 16),
                        new PointF(23, 25),
                        new PointF(9, 25),
                        new PointF(15, 16)
                    };
                    g.DrawPolygon(inkPen, pts);
                    g.FillPolygon(inkBrush, new PointF[]
                    {
                        new PointF(10, 24),
                        new PointF(22, 24),
                        new PointF(16, 18)
                    });
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon cloned = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
                return cloned;
            }
        }

        // =====================================================================
        // Timer Logic & Adaptive 0% Idle CPU Management
        // =====================================================================
        private void EnsureTimerState()
        {
            bool needsAnimation =
                (_state == TimerState.Running) ||
                (_state == TimerState.Alarm) ||
                _isFlipping ||
                (_clock.ElapsedMilliseconds < _hudShowUntilMs);

            if (needsAnimation)
            {
                int targetInterval = (_isFlipping || _state == TimerState.Alarm) ? 30 : 50;
                if (_animTimer.Interval != targetInterval)
                    _animTimer.Interval = targetInterval;
                if (!_animTimer.Enabled)
                    _animTimer.Start();
            }
            else
            {
                if (_animTimer.Enabled)
                    _animTimer.Stop();
            }
        }

        private void OnAnimTimerTick(object sender, EventArgs e)
        {
            long nowMs = _clock.ElapsedMilliseconds;

            if (_isFlipping)
            {
                float elapsed = nowMs - _flipStartMs;
                _flipProgress = Math.Min(1.0f, elapsed / FlipDurationMs);
                if (_flipProgress >= 1.0f)
                {
                    _isFlipping = false;
                    _flipProgress = 1.0f;
                }
            }

            if (_state == TimerState.Running)
            {
                double dt = (nowMs - _lastTickTimestamp) / 1000.0;
                _lastTickTimestamp = nowMs;
                _remainingSeconds -= dt;

                if (_remainingSeconds <= 0.0)
                {
                    _remainingSeconds = 0.0;
                    EnterAlarmState();
                }
            }
            else if (_state == TimerState.Alarm)
            {
                if (nowMs - _alarmStartMs > 60000)
                {
                    StopAlarmAndReset();
                }
                else if (nowMs - _lastSoundPlayMs >= 3200)
                {
                    _lastSoundPlayMs = nowMs;
                    PlayAlarmChime();
                }
            }

            UpdateTrayTooltip();
            RenderLayeredWindow();
            EnsureTimerState();
        }

        private void UpdateTrayTooltip()
        {
            if (_trayIcon == null) return;
            string status = _state == TimerState.Running ? "RUN" :
                            _state == TimerState.Paused ? "HOLD" :
                            _state == TimerState.Alarm ? "ALARM" : "READY";
            string text = "모래시계 [" + status + "] " + FormatTime(_remainingSeconds);
            if (text.Length >= 63) text = text.Substring(0, 62);
            if (_trayIcon.Text != text)
            {
                _trayIcon.Text = text;
            }
        }

        private void ToggleStartPause()
        {
            if (_state == TimerState.Alarm)
            {
                StopAlarmAndReset();
                return;
            }

            if (_state == TimerState.Running)
            {
                _state = TimerState.Paused;
                ShowHudTemporarily("HOLD // PAUSED", 1100);
            }
            else
            {
                if (_remainingSeconds <= 0.05)
                {
                    _remainingSeconds = _totalSeconds;
                }
                _state = TimerState.Running;
                _lastTickTimestamp = _clock.ElapsedMilliseconds;
                ShowHudTemporarily("RUN // " + FormatTime(_remainingSeconds), 1100);
            }

            UpdateTrayTooltip();
            RenderLayeredWindow();
            EnsureTimerState();
        }

        private void FlipAndRestart()
        {
            _isFlipping = true;
            _flipProgress = 0.0f;
            _flipStartMs = _clock.ElapsedMilliseconds;

            _remainingSeconds = _totalSeconds;
            _state = TimerState.Running;
            _lastTickTimestamp = _clock.ElapsedMilliseconds;

            ShowHudTemporarily("FLIP // " + FormatTime(_totalSeconds), 1300);
            UpdateTrayTooltip();
            RenderLayeredWindow();
            EnsureTimerState();
        }

        private void EnterAlarmState()
        {
            _state = TimerState.Alarm;
            _alarmStartMs = _clock.ElapsedMilliseconds;
            _lastSoundPlayMs = _alarmStartMs;
            PlayAlarmChime();
            EnsureTimerState();
        }

        private void StopAlarmAndReset()
        {
            _state = TimerState.Ready;
            _remainingSeconds = _totalSeconds;
            ShowHudTemporarily("RESET // " + FormatTime(_totalSeconds), 1300);
            UpdateTrayTooltip();
            RenderLayeredWindow();
            EnsureTimerState();
        }

        private void SetNewDuration(double seconds, bool startFlipping)
        {
            if (seconds < 10) seconds = 10;
            if (seconds > 36000) seconds = 36000;

            _totalSeconds = seconds;
            _remainingSeconds = seconds;
            if (_state == TimerState.Alarm || _state == TimerState.Paused)
            {
                _state = TimerState.Ready;
            }

            if (startFlipping && _state == TimerState.Running)
            {
                _lastTickTimestamp = _clock.ElapsedMilliseconds;
            }

            SaveSettings();
            ShowHudTemporarily("SET // " + FormatTime(_totalSeconds), 1500);
            UpdateTrayTooltip();
            RenderLayeredWindow();
            EnsureTimerState();
        }

        private void ShowHudTemporarily(string customText, int durationMs)
        {
            _hudCustomText = customText;
            _hudShowUntilMs = _clock.ElapsedMilliseconds + durationMs;
            EnsureTimerState();
        }

        // =====================================================================
        // User Input
        // =====================================================================
        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isMouseDown = true;
                _isDragging = false;
                _mouseDownScreenPos = Control.MousePosition;
                _formStartPos = Location;
            }
            else if (e.Button == MouseButtons.Middle)
            {
                _showDigitalTime = !_showDigitalTime;
                _menuShowTime.Checked = _showDigitalTime;
                SaveSettings();
                RenderLayeredWindow();
            }
        }

        private void OnFormMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown) return;

            Point cur = Control.MousePosition;
            int dx = cur.X - _mouseDownScreenPos.X;
            int dy = cur.Y - _mouseDownScreenPos.Y;

            if (!_isDragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                Location = new Point(_formStartPos.X + dx, _formStartPos.Y + dy);
            }
        }

        private void OnFormMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            bool wasDragging = _isDragging;
            _isMouseDown = false;
            _isDragging = false;

            if (wasDragging)
            {
                SaveSettings();
                return;
            }

            long now = _clock.ElapsedMilliseconds;
            if (now - _lastClickMs <= SystemInformation.DoubleClickTime)
            {
                _lastClickMs = 0;
                FlipAndRestart();
            }
            else
            {
                _lastClickMs = now;
                ToggleStartPause();
            }
        }

        private void OnFormMouseWheel(object sender, MouseEventArgs e)
        {
            int notches = e.Delta / 120;
            if (notches == 0) notches = e.Delta > 0 ? 1 : -1;

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                float newScale = _scale + notches * 0.1f;
                if (newScale < 0.6f) newScale = 0.6f;
                if (newScale > 2.2f) newScale = 2.2f;
                ApplyScale(newScale, true);
                ShowHudTemporarily("SCALE " + (int)Math.Round(_scale * 100) + "%", 1200);
                return;
            }

            double step;
            if ((ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                step = 300.0;
            }
            else if ((ModifierKeys & Keys.Alt) == Keys.Alt)
            {
                step = 10.0;
            }
            else
            {
                if (_totalSeconds < 60.0 || (_totalSeconds <= 60.0 && notches < 0))
                    step = 10.0;
                else
                    step = 60.0;
            }

            double nextTotal;
            if (step == 60.0 && (_totalSeconds % 60.0 != 0))
            {
                if (notches > 0)
                    nextTotal = Math.Ceiling(_totalSeconds / 60.0) * 60.0 + (notches - 1) * 60.0;
                else
                    nextTotal = Math.Floor(_totalSeconds / 60.0) * 60.0 + (notches + 1) * 60.0;
            }
            else
            {
                nextTotal = _totalSeconds + notches * step;
            }

            if (nextTotal < 10.0) nextTotal = 10.0;
            if (nextTotal > 36000.0) nextTotal = 36000.0;

            SetNewDuration(nextTotal, false);
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                ToggleStartPause();
            }
            else if (e.KeyCode == Keys.R)
            {
                FlipAndRestart();
            }
            else if (e.KeyCode == Keys.M)
            {
                _panelMode = !_panelMode;
                UpdateMenuChecks();
                SaveSettings();
                RenderLayeredWindow();
            }
            else if (e.KeyCode == Keys.Escape && _state == TimerState.Alarm)
            {
                StopAlarmAndReset();
            }
        }

        private void ApplyScale(float newScale, bool save)
        {
            _scale = newScale;
            int newW = (int)Math.Round(BaseWidth * _scale);
            int newH = (int)Math.Round(BaseHeight * _scale);

            int centerX = Location.X + Width / 2;
            int centerY = Location.Y + Height / 2;

            Size = new Size(newW, newH);
            if (IsHandleCreated)
            {
                Location = new Point(centerX - newW / 2, centerY - newH / 2);
            }

            if (_canvasBitmap != null)
            {
                _canvasBitmap.Dispose();
                _canvasBitmap = null;
            }

            if (save)
            {
                UpdateMenuChecks();
                SaveSettings();
                RenderLayeredWindow();
            }
        }

        // =====================================================================
        // Dialogs
        // =====================================================================
        private void ShowCustomTimeDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "시간 설정 (CHRONO SET)";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterScreen;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ClientSize = new Size(255, 130);
                dlg.TopMost = true;
                dlg.Font = new Font("Malgun Gothic", 9.5f);

                int curMin = (int)(_totalSeconds / 60);
                int curSec = (int)(_totalSeconds % 60);

                Label lblMin = new Label() { Text = "분 (Min):", Left = 22, Top = 24, Width = 75 };
                NumericUpDown numMin = new NumericUpDown()
                {
                    Left = 105, Top = 20, Width = 120, Minimum = 0, Maximum = 600, Value = curMin
                };

                Label lblSec = new Label() { Text = "초 (Sec):", Left = 22, Top = 56, Width = 75 };
                NumericUpDown numSec = new NumericUpDown()
                {
                    Left = 105, Top = 52, Width = 120, Minimum = 0, Maximum = 59, Value = curSec
                };

                Button btnOk = new Button()
                {
                    Text = "확인", DialogResult = DialogResult.OK, Left = 45, Top = 90, Width = 80, Height = 28
                };
                Button btnCancel = new Button()
                {
                    Text = "취소", DialogResult = DialogResult.Cancel, Left = 135, Top = 90, Width = 80, Height = 28
                };

                dlg.Controls.AddRange(new Control[] { lblMin, numMin, lblSec, numSec, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    double total = (double)numMin.Value * 60.0 + (double)numSec.Value;
                    if (total < 5.0) total = 5.0;
                    SetNewDuration(total, true);
                }
            }
        }

        private void ShowHelpMessage()
        {
            string msg =
                "⏳ NAVIGATOR // E-INK 모래시계 조작 안내\n\n" +
                "• 왼쪽 클릭 : 시작 / 일시정지 / 알람 해제\n" +
                "• 더블 클릭 : 모래시계 180° 뒤집기 (재시작)\n" +
                "• 드래그 : 원하는 위치로 이동\n" +
                "• 마우스 휠 : 시간 조절 (±1분, 1분 미만은 ±10초)\n" +
                "   - Shift + 휠 : ±5분 단위 조절\n" +
                "   - Alt + 휠 : ±10초 단위 미세 조절\n" +
                "   - Ctrl + 휠 : 크기 확대/축소\n" +
                "• 단축키 M (또는 우클릭) : '투명 라인아트 모드' ↔ 'E-Ink 패널 모드' 전환\n" +
                "• 휠 클릭(가운데 버튼) : 하단 도트 타이머 표시 켜기/끄기\n" +
                "• 우클릭 : 라이트 잉크 / 다크 반전 테마 및 환경설정";

            MessageBox.Show(this, msg, "모래시계 도움말", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // =====================================================================
        // Preview Sheet Generator (For Verification)
        // =====================================================================
        public void SavePreviewImage(string path)
        {
            if (_trayIcon != null) _trayIcon.Visible = false;
            int cols = 4;
            int pad = 16;
            int w = cols * BaseWidth + (cols + 1) * pad;
            int h = BaseHeight + pad * 2;

            using (Bitmap sheet = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(sheet))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.CompositingQuality = CompositingQuality.HighQuality;

                // Split background: Left half medium-dark desktop, Right half light-warm desktop
                using (SolidBrush darkDesk = new SolidBrush(Color.FromArgb(255, 42, 46, 52)))
                using (SolidBrush lightDesk = new SolidBrush(Color.FromArgb(255, 212, 210, 204)))
                {
                    g.FillRectangle(darkDesk, 0, 0, w / 2, h);
                    g.FillRectangle(lightDesk, w / 2, 0, w - w / 2, h);
                }

                // Sample 1: Transparent Line-Art Mode (Light Ink)
                _panelMode = false;
                _themeIndex = 0;
                _state = TimerState.Running;
                _totalSeconds = 300;
                _remainingSeconds = 195; // 03:15
                DrawPreviewCell(g, pad + 0 * (BaseWidth + pad), pad);

                // Sample 2: Transparent Line-Art Mode (Dark Inverted Ink)
                _panelMode = false;
                _themeIndex = 1;
                _state = TimerState.Running;
                _totalSeconds = 300;
                _remainingSeconds = 142; // 02:22
                DrawPreviewCell(g, pad + 1 * (BaseWidth + pad), pad);

                // Sample 3: E-Ink Panel Card Mode (Light E-Paper)
                _panelMode = true;
                _themeIndex = 0;
                _state = TimerState.Running;
                _totalSeconds = 1500;
                _remainingSeconds = 1044; // 17:24
                DrawPreviewCell(g, pad + 2 * (BaseWidth + pad), pad);

                // Sample 4: E-Ink Panel Card Mode (Dark Inverted / OLED)
                _panelMode = true;
                _themeIndex = 1;
                _state = TimerState.Running;
                _totalSeconds = 600;
                _remainingSeconds = 315; // 05:15
                DrawPreviewCell(g, pad + 3 * (BaseWidth + pad), pad);

                sheet.Save(path, ImageFormat.Png);
            }
        }

        private void DrawPreviewCell(Graphics g, int x, int y)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            DrawScene(g);
            g.Restore(st);
        }

        public void HideTrayForRender()
        {
            if (_trayIcon != null) _trayIcon.Visible = false;
        }

        public void RenderCustomFrame(
            Graphics g, float x, float y, float scale,
            bool panelMode, int themeIndex, TimerState state,
            double totalSec, double remSec,
            bool isFlipping, float flipProgress,
            string hudText, long simMs)
        {
            _panelMode = panelMode;
            _themeIndex = themeIndex;
            _state = state;
            _totalSeconds = totalSec;
            _remainingSeconds = remSec;
            _isFlipping = isFlipping;
            _flipProgress = flipProgress;
            _alarmStartMs = 0;
            if (!string.IsNullOrEmpty(hudText))
            {
                _hudCustomText = hudText;
                _hudShowUntilMs = long.MaxValue;
            }
            else
            {
                _hudCustomText = null;
                _hudShowUntilMs = 0;
            }

            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(scale, scale);
            DrawSceneAtTime(g, simMs);
            g.Restore(st);
        }

        // =====================================================================
        // Per-Pixel Alpha Layered Window Rendering
        // =====================================================================
        private void RenderLayeredWindow()
        {
            if (!IsHandleCreated || IsDisposed || Width <= 0 || Height <= 0) return;

            if (_canvasBitmap == null || _canvasBitmap.Width != Width || _canvasBitmap.Height != Height)
            {
                if (_canvasBitmap != null) _canvasBitmap.Dispose();
                _canvasBitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            }

            using (Graphics g = Graphics.FromImage(_canvasBitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.CompositingQuality = CompositingQuality.HighQuality;

                g.ScaleTransform(_scale, _scale);
                DrawScene(g);
            }

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = _canvasBitmap.GetHbitmap(Color.FromArgb(0));
                hOldBitmap = SelectObject(memDc, hBitmap);

                SIZE size = new SIZE(Width, Height);
                POINT pointSource = new POINT(0, 0);
                POINT topPos = new POINT(Left, Top);
                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(
                    Handle, screenDc, ref topPos, ref size,
                    memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (hOldBitmap != IntPtr.Zero) SelectObject(memDc, hOldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // =====================================================================
        // E-Ink & Navigator Technical Instrument Scene Drawing
        // =====================================================================
        private void DrawScene(Graphics g)
        {
            DrawSceneAtTime(g, _clock.ElapsedMilliseconds);
        }

        private void DrawSceneAtTime(Graphics g, long nowMs)
        {
            EInkPalette pal = _themes[_themeIndex];

            double progress = 0.0;
            if (_totalSeconds > 0.0)
            {
                progress = 1.0 - (_remainingSeconds / _totalSeconds);
                if (progress < 0.0) progress = 0.0;
                if (progress > 1.0) progress = 1.0;
            }

            float cx = BaseWidth * 0.5f; // 105

            if (_panelMode)
            {
                DrawEInkPanelFrame(g, pal, cx, progress, nowMs);
            }

            // Hourglass instrument center
            float hgCenterY = _panelMode ? 122f : 120f;
            float topBulbTop = hgCenterY - 76f; // 44 or 46
            float neckY = hgCenterY;            // 120 or 122
            float botBulbBot = hgCenterY + 76f; // 196 or 198
            float bulbHalfW = _panelMode ? 39f : 43f;
            float neckHalfW = 4.2f;

            GraphicsState hgState = g.Save();

            // Alarm Shake or 180-degree Flip Rotation on the Hourglass Assembly
            if (_state == TimerState.Alarm)
            {
                double t = (nowMs - _alarmStartMs) / 1000.0;
                double cycle = t % 1.1;
                if (cycle < 0.42)
                {
                    float angle = (float)(Math.Sin(cycle * 58.0) * 4.5 * (1.0 - cycle / 0.42));
                    g.TranslateTransform(cx, hgCenterY);
                    g.RotateTransform(angle);
                    g.TranslateTransform(-cx, -hgCenterY);
                }
            }
            else if (_isFlipping)
            {
                float t = _flipProgress;
                float ease = t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                float angle = ease * 180.0f;
                g.TranslateTransform(cx, hgCenterY);
                g.RotateTransform(angle);
                g.TranslateTransform(-cx, -hgCenterY);
            }

            DrawTechnicalHourglass(
                g, pal, cx,
                topBulbTop, neckY, botBulbBot,
                bulbHalfW, neckHalfW,
                progress, nowMs);

            g.Restore(hgState);

            // Bottom Readout Section (Dot-Matrix Digital Clock + Technical Gauge Bar)
            bool showTempHud = nowMs < _hudShowUntilMs;
            if (_panelMode)
            {
                DrawPanelDashboardReadout(g, pal, cx, progress, showTempHud, nowMs);
            }
            else if (_showDigitalTime || showTempHud || _state == TimerState.Paused || _state == TimerState.Alarm)
            {
                DrawTransparentReadoutPlaque(g, pal, cx, 250f, progress, showTempHud, nowMs);
            }
        }

        // =====================================================================
        // E-Ink Hardware Bezel & Grid Dashboard (Panel Mode)
        // =====================================================================
        private void DrawEInkPanelFrame(Graphics g, EInkPalette pal, float cx, double progress, long nowMs)
        {
            // Soft subtle hardware drop shadow
            using (GraphicsPath shadowPath = CreateRoundedRect(6f, 8f, BaseWidth - 12f, BaseHeight - 10f, 10f))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(65, 0, 0, 0)))
            {
                g.FillPath(shadowBrush, shadowPath);
            }

            // Outer Hardware Bezel (like the white E-Ink frame in reference photos 2 & 3)
            RectangleF bezelRect = new RectangleF(4f, 4f, BaseWidth - 8f, BaseHeight - 8f);
            using (GraphicsPath bezelPath = CreateRoundedRect(bezelRect.X, bezelRect.Y, bezelRect.Width, bezelRect.Height, 8f))
            using (SolidBrush bezelBrush = new SolidBrush(pal.BezelOuter))
            using (Pen bezelBorder = new Pen(pal.InkPrimary, 1.6f))
            {
                g.FillPath(bezelBrush, bezelPath);
                g.DrawPath(bezelBorder, bezelPath);
            }

            // Inner E-Ink Matte Screen Surface
            RectangleF screenRect = new RectangleF(13f, 13f, BaseWidth - 26f, BaseHeight - 26f);
            using (SolidBrush paperBrush = new SolidBrush(pal.PaperBg))
            using (Pen innerLipPen = new Pen(pal.BezelInner, 1.5f))
            using (Pen inkGridPen = new Pen(pal.InkPrimary, 1.3f))
            {
                g.FillRectangle(paperBrush, screenRect);
                g.DrawRectangle(innerLipPen, screenRect.X - 1f, screenRect.Y - 1f, screenRect.Width + 2f, screenRect.Height + 2f);
                g.DrawRectangle(inkGridPen, screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);

                // Top Header Bar (Image 2 & 3 style: [CHRONO-01] | STATUS)
                float headerBotY = 30f;
                g.DrawLine(inkGridPen, screenRect.Left, headerBotY, screenRect.Right, headerBotY);

                string statusCode = _state == TimerState.Running ? "RUN" :
                                    _state == TimerState.Paused ? "HOLD" :
                                    _state == TimerState.Alarm ? "ALRM" : "RDY";

                using (Font headerFont = new Font("Consolas", 7.2f, FontStyle.Bold))
                using (SolidBrush inkBrush = new SolidBrush(pal.InkPrimary))
                {
                    g.DrawString("NAVIGATOR // E-INK", headerFont, inkBrush, screenRect.Left + 4f, screenRect.Top + 3f);

                    // Right-aligned status pill
                    SizeF stSz = g.MeasureString(statusCode, headerFont);
                    float pillW = stSz.Width + 8f;
                    RectangleF stRect = new RectangleF(screenRect.Right - pillW - 4f, screenRect.Top + 2.5f, pillW, 12f);
                    Color stFill = (_state == TimerState.Alarm && ((nowMs / 250) % 2 == 0)) ? pal.AccentInk : pal.InkPrimary;
                    using (SolidBrush stBg = new SolidBrush(stFill))
                    using (SolidBrush stFg = new SolidBrush(pal.PaperBg))
                    {
                        g.FillRectangle(stBg, stRect);
                        g.DrawString(statusCode, headerFont, stFg, stRect.X + 4f, stRect.Y + 0.5f);
                    }
                }

                // Horizontal Divider between Hourglass Chamber and Bottom Dot-Matrix Readout
                float midDividerY = 214f;
                g.DrawLine(inkGridPen, screenRect.Left, midDividerY, screenRect.Right, midDividerY);

                // Corner Bolt Screw Heads inside Chamber (Image 1 RAIL BALANCE style)
                DrawScrewHead(g, pal, screenRect.Left + 8f, headerBotY + 8f, 3.0f);
                DrawScrewHead(g, pal, screenRect.Right - 8f, headerBotY + 8f, 3.0f);
                DrawScrewHead(g, pal, screenRect.Left + 8f, midDividerY - 8f, 3.0f);
                DrawScrewHead(g, pal, screenRect.Right - 8f, midDividerY - 8f, 3.0f);
            }
        }

        // =====================================================================
        // Technical Hourglass Instrument (Navigator Line-Art + Dot-Matrix Sand)
        // =====================================================================
        private void DrawTechnicalHourglass(
            Graphics g, EInkPalette pal, float cx,
            float topY, float neckY, float botY,
            float halfW, float neckHalfW,
            double progress, long nowMs)
        {
            float pillarXOffset = halfW + 11f;

            // 1. Graduated Scale Pillars (Left: Remaining % scale, Right: Precision ruler ticks)
            DrawGraduatedScalePillar(g, pal, cx - pillarXOffset, topY, botY, true);
            DrawGraduatedScalePillar(g, pal, cx + pillarXOffset, topY, botY, false);

            // 2. Center Neck Crosshair / Reticle (Image 1 Attitude Indicator style)
            if (!_panelMode)
            {
                using (Pen haloPen = new Pen(pal.PaperBg, 3.2f))
                {
                    g.DrawLine(haloPen, cx - pillarXOffset + 3f, neckY, cx - neckHalfW - 4f, neckY);
                    g.DrawLine(haloPen, cx + neckHalfW + 4f, neckY, cx + pillarXOffset - 3f, neckY);
                }
            }
            using (Pen dashedPen = new Pen(pal.InkSecondary, 1.0f))
            {
                dashedPen.DashStyle = DashStyle.Dot;
                g.DrawLine(dashedPen, cx - pillarXOffset + 3f, neckY, cx - neckHalfW - 4f, neckY);
                g.DrawLine(dashedPen, cx + neckHalfW + 4f, neckY, cx + pillarXOffset - 3f, neckY);
            }
            // Small triangles pointing at the neck waist
            using (SolidBrush arrowBrush = new SolidBrush(_state == TimerState.Alarm ? pal.AccentInk : pal.InkPrimary))
            {
                g.FillPolygon(arrowBrush, new PointF[]
                {
                    new PointF(cx - neckHalfW - 3f, neckY),
                    new PointF(cx - neckHalfW - 8f, neckY - 3f),
                    new PointF(cx - neckHalfW - 8f, neckY + 3f)
                });
                g.FillPolygon(arrowBrush, new PointF[]
                {
                    new PointF(cx + neckHalfW + 3f, neckY),
                    new PointF(cx + neckHalfW + 8f, neckY - 3f),
                    new PointF(cx + neckHalfW + 8f, neckY + 3f)
                });
            }

            // 3. Build Hourglass Chamber Paths
            using (GraphicsPath upperBulbPath = CreateUpperBulbPath(cx, topY, neckY, halfW, neckHalfW))
            using (GraphicsPath lowerBulbPath = CreateLowerBulbPath(cx, neckY, botY, halfW, neckHalfW))
            using (GraphicsPath fullGlassPath = CreateFullGlassPath(cx, topY, neckY, botY, halfW, neckHalfW))
            {
                // In Transparent Mode, fill the inside of the hourglass chamber with E-Ink paper so
                // the inner dot-matrix sand and hairlines have crisp contrast, while outside is 100% transparent!
                if (!_panelMode)
                {
                    using (Pen outerHaloPen = new Pen(pal.PaperBg, 4.5f))
                    {
                        outerHaloPen.LineJoin = LineJoin.Round;
                        g.DrawPath(outerHaloPen, fullGlassPath);
                    }
                    using (SolidBrush chamberBg = new SolidBrush(pal.PaperBg))
                    {
                        g.FillPath(chamberBg, fullGlassPath);
                    }
                }
                // Fill glass interior with subtle ghost grid dots (E-Ink texture)
                GraphicsState bgClip = g.Save();
                g.SetClip(fullGlassPath, CombineMode.Intersect);
                using (SolidBrush ghostBrush = new SolidBrush(pal.InkGhost))
                {
                    for (float py = topY + 4f; py < botY - 2f; py += 6f)
                    {
                        for (float px = cx - halfW; px <= cx + halfW; px += 6f)
                        {
                            g.FillRectangle(ghostBrush, px - 0.6f, py - 0.6f, 1.2f, 1.2f);
                        }
                    }
                }
                g.Restore(bgClip);

                // 4. Draw Ink + Dot-Matrix Sand & Falling Pixel Stream
                DrawDotMatrixSand(
                    g, pal,
                    upperBulbPath, lowerBulbPath,
                    cx, topY, neckY, botY, halfW,
                    progress, nowMs);

                // 5. Crisp Technical Double-Line Glass Wall Contour
                Color contourColor = (_state == TimerState.Alarm && ((nowMs / 220) % 2 == 0))
                    ? pal.AccentInk
                    : pal.InkPrimary;

                using (Pen outerWallPen = new Pen(contourColor, 2.2f))
                using (Pen innerMeasurementPen = new Pen(pal.InkSecondary, 0.9f))
                {
                    outerWallPen.LineJoin = LineJoin.Round;
                    g.DrawPath(outerWallPen, fullGlassPath);

                    // Horizontal level reference hairlines inside upper and lower bulbs
                    GraphicsState lineClip = g.Save();
                    g.SetClip(fullGlassPath, CombineMode.Intersect);
                    innerMeasurementPen.DashStyle = DashStyle.Dash;
                    float hBulb = neckY - topY;
                    for (int step = 1; step <= 3; step++)
                    {
                        float yUp = neckY - hBulb * (step * 0.22f);
                        float yDn = neckY + hBulb * (step * 0.22f);
                        g.DrawLine(innerMeasurementPen, cx - 12f, yUp, cx + 12f, yUp);
                        g.DrawLine(innerMeasurementPen, cx - 12f, yDn, cx + 12f, yDn);
                    }
                    g.Restore(lineClip);
                }
            }

            // 6. Top & Bottom Navigator-style Nameplate Caps with Screw Ears (Image 1 NAVIGATOR badge)
            DrawNavigatorCap(g, pal, cx, topY - 9f, halfW * 2f + 18f, 10f);
            DrawNavigatorCap(g, pal, cx, botY - 1f, halfW * 2f + 18f, 10f);
        }

        private void DrawGraduatedScalePillar(Graphics g, EInkPalette pal, float x, float topY, float botY, bool isLeft)
        {
            if (!_panelMode)
            {
                using (Pen haloSpine = new Pen(pal.PaperBg, 4.2f))
                {
                    g.DrawLine(haloSpine, x, topY, x, botY);
                }
            }

            using (Pen mainLinePen = new Pen(pal.InkPrimary, 1.5f))
            using (Pen minorTickPen = new Pen(pal.InkSecondary, 1.0f))
            {
                // Vertical axis spine
                g.DrawLine(mainLinePen, x, topY, x, botY);

                int totalTicks = 20;
                float totalH = botY - topY;
                for (int i = 0; i <= totalTicks; i++)
                {
                    float ty = topY + (totalH * i) / totalTicks;
                    bool isMajor = (i % 5 == 0);
                    float tickLen = isMajor ? 5.5f : 3.0f;

                    float x1 = isLeft ? x : x - tickLen;
                    float x2 = isLeft ? x + tickLen : x;
                    g.DrawLine(isMajor ? mainLinePen : minorTickPen, x1, ty, x2, ty);
                }
            }
        }

        private void DrawNavigatorCap(Graphics g, EInkPalette pal, float cx, float y, float width, float height)
        {
            RectangleF rect = new RectangleF(cx - width * 0.5f, y, width, height);
            using (GraphicsPath path = CreateRoundedRect(rect.X, rect.Y, rect.Width, rect.Height, height * 0.48f))
            using (SolidBrush fillBrush = new SolidBrush(pal.InkPrimary))
            using (Pen innerLinePen = new Pen(pal.PaperBg, 1.0f))
            {
                g.FillPath(fillBrush, path);

                // Inner thin stadium frame (exactly like the NAVIGATOR nameplate in Image 1)
                using (GraphicsPath innerPath = CreateRoundedRect(rect.X + 2.2f, rect.Y + 2.2f, rect.Width - 4.4f, rect.Height - 4.4f, (height - 4.4f) * 0.45f))
                {
                    g.DrawPath(innerLinePen, innerPath);
                }
            }

            // Left & Right screw ears on the cap
            DrawScrewHead(g, pal, rect.Left + 5.5f, y + height * 0.5f, 2.8f);
            DrawScrewHead(g, pal, rect.Right - 5.5f, y + height * 0.5f, 2.8f);
        }

        private void DrawScrewHead(Graphics g, EInkPalette pal, float cx, float cy, float r)
        {
            using (SolidBrush bg = new SolidBrush(pal.PaperBg))
            using (Pen border = new Pen(pal.InkPrimary, 1.1f))
            {
                g.FillEllipse(bg, cx - r, cy - r, r * 2f, r * 2f);
                g.DrawEllipse(border, cx - r, cy - r, r * 2f, r * 2f);
                // Cross/angled slot
                float d = r * 0.62f;
                g.DrawLine(border, cx - d, cy - d, cx + d, cy + d);
                g.DrawLine(border, cx - d, cy + d, cx + d, cy - d);
            }
        }

        private void DrawDotMatrixSand(
            Graphics g, EInkPalette pal,
            GraphicsPath upperBulbPath, GraphicsPath lowerBulbPath,
            float cx, float topY, float neckY, float botY, float halfW,
            double progress, long nowMs)
        {
            double rem = 1.0 - progress;
            float maxBulbHeight = (neckY - topY) - 7f;
            Color sandColor = (_state == TimerState.Alarm && ((nowMs / 220) % 2 == 0))
                ? pal.AccentInk
                : pal.InkPrimary;

            using (SolidBrush inkBrush = new SolidBrush(sandColor))
            using (SolidBrush paperBrush = new SolidBrush(pal.PaperBg))
            {
                // 1. UPPER SAND (Solid Ink + Halftone Pixel Surface Row)
                if (rem > 0.002)
                {
                    float hRatio = (float)Math.Pow(rem, 0.68);
                    float sandTopY = neckY - maxBulbHeight * hRatio;
                    float funnelDip = (_state == TimerState.Running && rem > 0.04)
                        ? Math.Min(7.5f, (neckY - sandTopY) * 0.30f)
                        : 2.0f;

                    GraphicsState clipState = g.Save();
                    g.SetClip(upperBulbPath, CombineMode.Intersect);

                    List<PointF> topPts = new List<PointF>();
                    for (float px = cx - halfW - 6f; px <= cx + halfW + 6f; px += 2.0f)
                    {
                        float norm = Math.Abs(px - cx) / halfW;
                        float y = sandTopY + funnelDip * (float)Math.Exp(-norm * norm * 6.5);
                        topPts.Add(new PointF(px, y));
                    }
                    topPts.Add(new PointF(cx + halfW + 6f, neckY + 2f));
                    topPts.Add(new PointF(cx - halfW - 6f, neckY + 2f));

                    g.FillPolygon(inkBrush, topPts.ToArray());

                    // Crisp E-Ink halftone pixel dithering directly along the surface curve
                    float step = 3.2f;
                    for (float px = cx - halfW; px <= cx + halfW; px += step)
                    {
                        float norm = Math.Abs(px - cx) / halfW;
                        float localY = sandTopY + funnelDip * (float)Math.Exp(-norm * norm * 6.5);
                        if (((int)Math.Round((px - cx) / step)) % 2 == 0)
                        {
                            g.FillRectangle(inkBrush, px - 1.1f, localY - 2.4f, 2.2f, 2.2f);
                        }
                        else
                        {
                            g.FillRectangle(paperBrush, px - 1.0f, localY + 0.8f, 2.0f, 2.0f);
                        }
                    }

                    g.Restore(clipState);
                }

                // 2. LOWER SAND (Conical Mound + Dot-Matrix Halftone Slopes)
                float pilePeakY = botY;
                if (progress > 0.001)
                {
                    float fillRatio = (float)Math.Pow(progress, 0.78);
                    float baseHeight = maxBulbHeight * fillRatio;
                    float edgeY = botY - baseHeight * 0.82f;
                    float coneExtra = (float)(Math.Sin(Math.Min(1.0, progress * 1.15) * Math.PI) * 14.5 + 3.0);
                    pilePeakY = Math.Max(neckY + 5f, edgeY - coneExtra);

                    GraphicsState clipState = g.Save();
                    g.SetClip(lowerBulbPath, CombineMode.Intersect);

                    List<PointF> botPts = new List<PointF>();
                    for (float px = cx - halfW - 6f; px <= cx + halfW + 6f; px += 2.0f)
                    {
                        float norm = Math.Min(1.2f, Math.Abs(px - cx) / (halfW * 0.92f));
                        float y = pilePeakY + (edgeY - pilePeakY) * (float)Math.Pow(norm, 1.35);
                        botPts.Add(new PointF(px, y));
                    }
                    botPts.Add(new PointF(cx + halfW + 6f, botY + 4f));
                    botPts.Add(new PointF(cx - halfW - 6f, botY + 4f));

                    g.FillPolygon(inkBrush, botPts.ToArray());

                    // Crisp E-Ink halftone pixel dithering directly along the cone slope
                    float step = 3.2f;
                    for (float px = cx - halfW; px <= cx + halfW; px += step)
                    {
                        float norm = Math.Min(1.2f, Math.Abs(px - cx) / (halfW * 0.92f));
                        float slopeY = pilePeakY + (edgeY - pilePeakY) * (float)Math.Pow(norm, 1.35);
                        if (((int)Math.Round((px - cx) / step)) % 2 == 0)
                        {
                            g.FillRectangle(inkBrush, px - 1.1f, slopeY - 2.4f, 2.2f, 2.2f);
                        }
                        else
                        {
                            g.FillRectangle(paperBrush, px - 1.0f, slopeY + 0.8f, 2.0f, 2.0f);
                        }
                    }

                    g.Restore(clipState);
                }

                // 3. FALLING PIXEL-DOT STREAM (Discrete square E-Ink blocks falling down center)
                if (_state == TimerState.Running && rem > 0.002 && !_isFlipping)
                {
                    float streamEndY = Math.Min(botY - 2f, pilePeakY);
                    if (streamEndY > neckY)
                    {
                        int phase = (int)((nowMs / 40) % 100);
                        float offset = (phase % 5) * 1.1f;
                        for (float py = neckY + offset; py < streamEndY - 1f; py += 4.6f)
                        {
                            g.FillRectangle(inkBrush, cx - 1.3f, py - 1.3f, 2.6f, 2.6f);
                        }

                        // Bouncing pixel dots at the impact peak
                        float bounce = (phase % 3) * 1.4f;
                        g.FillRectangle(inkBrush, cx - 4.5f - bounce, streamEndY - 3.5f, 2.2f, 2.2f);
                        g.FillRectangle(inkBrush, cx + 2.5f + bounce, streamEndY - 3.0f, 2.2f, 2.2f);
                    }
                }
            }
        }

        // =====================================================================
        // Readout Renderers (5x7 Dot-Matrix Clock + Technical Progress Bar)
        // =====================================================================
        private void DrawPanelDashboardReadout(
            Graphics g, EInkPalette pal, float cx,
            double progress, bool showTempHud, long nowMs)
        {
            RectangleF screenRect = new RectangleF(13f, 13f, BaseWidth - 26f, BaseHeight - 26f);
            float topY = 216f;

            // 1. Large 5x7 Dot-Matrix Clock (Image 3 style `17:24`)
            string clockStr = FormatTime(_remainingSeconds);
            float dotPitch = clockStr.Length > 5 ? 2.6f : 3.4f;
            Color activeDotColor = (_state == TimerState.Alarm && ((nowMs / 220) % 2 == 0))
                ? pal.AccentInk
                : pal.InkPrimary;

            DrawDotMatrixCentered(g, clockStr, cx, topY + 6f, dotPitch, activeDotColor, pal.InkGhost);

            // 2. Technical Sub-Label or Temporary Wheel HUD
            string subLabel;
            if (showTempHud && !string.IsNullOrEmpty(_hudCustomText))
            {
                subLabel = _hudCustomText;
            }
            else if (_state == TimerState.Alarm)
            {
                subLabel = ">>> TIME ELAPSED // CLICK <<<";
            }
            else
            {
                int pct = (int)Math.Round(progress * 100.0);
                subLabel = string.Format("ELAPSED: {0}% | SET: {1}", pct, FormatTime(_totalSeconds));
            }

            using (Font techFont = new Font("Consolas", 6.8f, FontStyle.Bold))
            using (SolidBrush secBrush = new SolidBrush(pal.InkPrimary))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center })
            {
                g.DrawString(subLabel, techFont, secBrush, new RectangleF(screenRect.Left, topY + 34f, screenRect.Width, 13f), sf);
            }

            // 3. E-Ink Technical Linear Progress Bar (Image 3 `PRINTER: RUNNING` / `CLAUDE AI USAGE` bar style)
            RectangleF barRect = new RectangleF(screenRect.Left + 10f, topY + 49f, screenRect.Width - 20f, 11f);
            DrawTechnicalProgressBar(g, pal, barRect, progress);
        }

        private void DrawTransparentReadoutPlaque(
            Graphics g, EInkPalette pal, float cx, float cy,
            double progress, bool showTempHud, long nowMs)
        {
            float plaqueW = 166f;
            float plaqueH = 54f;
            RectangleF rect = new RectangleF(cx - plaqueW * 0.5f, cy - plaqueH * 0.5f, plaqueW, plaqueH);

            // Navigator-style technical instrument plaque
            using (GraphicsPath path = CreateRoundedRect(rect.X, rect.Y, rect.Width, rect.Height, 6f))
            using (SolidBrush bgBrush = new SolidBrush(pal.PaperBg))
            using (Pen borderPen = new Pen(pal.InkPrimary, 1.8f))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // Corner screw heads on the plaque
            DrawScrewHead(g, pal, rect.Left + 6f, rect.Top + 6f, 2.3f);
            DrawScrewHead(g, pal, rect.Right - 6f, rect.Top + 6f, 2.3f);
            DrawScrewHead(g, pal, rect.Left + 6f, rect.Bottom - 6f, 2.3f);
            DrawScrewHead(g, pal, rect.Right - 6f, rect.Bottom - 6f, 2.3f);

            // Dot-Matrix Digital Time inside the plaque
            string clockStr = FormatTime(_remainingSeconds);
            float dotPitch = clockStr.Length > 5 ? 2.4f : 3.0f;
            Color activeDotColor = (_state == TimerState.Alarm && ((nowMs / 220) % 2 == 0))
                ? pal.AccentInk
                : pal.InkPrimary;

            DrawDotMatrixCentered(g, clockStr, cx, rect.Top + 7f, dotPitch, activeDotColor, pal.InkGhost);

            // Bottom progress bar or temporary status text inside plaque
            if (showTempHud && !string.IsNullOrEmpty(_hudCustomText))
            {
                using (Font f = new Font("Consolas", 6.8f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(pal.InkPrimary))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center })
                {
                    g.DrawString(_hudCustomText, f, b, new RectangleF(rect.X, rect.Bottom - 16f, rect.Width, 13f), sf);
                }
            }
            else
            {
                RectangleF barRect = new RectangleF(rect.X + 16f, rect.Bottom - 14f, rect.Width - 32f, 8f);
                DrawTechnicalProgressBar(g, pal, barRect, progress);
            }
        }

        private void DrawTechnicalProgressBar(Graphics g, EInkPalette pal, RectangleF barRect, double progress)
        {
            using (Pen borderPen = new Pen(pal.InkPrimary, 1.2f))
            using (SolidBrush fillBrush = new SolidBrush(pal.InkPrimary))
            using (Pen tickPen = new Pen(pal.InkSecondary, 0.8f))
            {
                g.DrawRectangle(borderPen, barRect.X, barRect.Y, barRect.Width, barRect.Height);

                float innerMaxW = barRect.Width - 4f;
                float fillW = (float)(innerMaxW * Math.Max(0.0, Math.Min(1.0, progress)));
                if (fillW > 0.5f)
                {
                    g.FillRectangle(fillBrush, barRect.X + 2f, barRect.Y + 2f, fillW, barRect.Height - 4f);
                }

                // Scale ticks along the top edge (Image 1 OLLIE HEIGHT gauge style)
                int divisions = 10;
                for (int i = 1; i < divisions; i++)
                {
                    float tx = barRect.X + (barRect.Width * i) / divisions;
                    float th = (i == 5) ? 3.5f : 2.0f;
                    g.DrawLine(tickPen, tx, barRect.Y, tx, barRect.Y + th);
                }
            }
        }

        // =====================================================================
        // 5x7 Pixel Dot-Matrix Renderer (Matches 3rd Reference Image `17:24`)
        // =====================================================================
        private static void DrawDotMatrixCentered(
            Graphics g, string text, float centerX, float topY,
            float pitch, Color activeColor, Color ghostColor)
        {
            if (string.IsNullOrEmpty(text)) return;

            int charCount = text.Length;
            float charW = 5f * pitch;
            float charSpacing = 1.6f * pitch;
            float totalW = charCount * charW + (charCount - 1) * charSpacing;
            float startX = centerX - totalW * 0.5f;
            float dotSize = pitch * 0.80f;

            using (SolidBrush onBrush = new SolidBrush(activeColor))
            using (SolidBrush offBrush = new SolidBrush(ghostColor))
            {
                for (int c = 0; c < charCount; c++)
                {
                    char ch = char.ToUpperInvariant(text[c]);
                    byte[] glyph;
                    if (!DotFont.TryGetValue(ch, out glyph))
                    {
                        glyph = DotFont[' '];
                    }

                    float cx = startX + c * (charW + charSpacing);
                    for (int row = 0; row < 7; row++)
                    {
                        byte rowBits = glyph[row];
                        float ry = topY + row * pitch;
                        for (int col = 0; col < 5; col++)
                        {
                            bool isOn = ((rowBits >> (4 - col)) & 1) != 0;
                            float rx = cx + col * pitch;
                            if (isOn)
                            {
                                g.FillRectangle(onBrush, rx, ry, dotSize, dotSize);
                            }
                            else if (ch != ':')
                            {
                                g.FillRectangle(offBrush, rx + dotSize * 0.15f, ry + dotSize * 0.15f, dotSize * 0.7f, dotSize * 0.7f);
                            }
                        }
                    }
                }
            }
        }

        private static Dictionary<char, byte[]> BuildDotMatrixFont()
        {
            var d = new Dictionary<char, byte[]>();
            d['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
            d['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
            d['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x06, 0x08, 0x10, 0x1F };
            d['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x06, 0x01, 0x11, 0x0E };
            d['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
            d['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
            d['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
            d['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
            d['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
            d['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };
            d[':'] = new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
            d[' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            return d;
        }

        // =====================================================================
        // Geometry Helpers
        // =====================================================================
        private GraphicsPath CreateUpperBulbPath(float cx, float topY, float neckY, float halfW, float neckHalfW)
        {
            GraphicsPath path = new GraphicsPath();
            float h = neckY - topY;
            path.AddLine(cx - halfW, topY, cx + halfW, topY);
            path.AddBezier(
                cx + halfW, topY,
                cx + halfW + 3f, topY + h * 0.54f,
                cx + neckHalfW + 7f, neckY - h * 0.16f,
                cx + neckHalfW, neckY);
            path.AddLine(cx + neckHalfW, neckY, cx - neckHalfW, neckY);
            path.AddBezier(
                cx - neckHalfW, neckY,
                cx - neckHalfW - 7f, neckY - h * 0.16f,
                cx - halfW - 3f, topY + h * 0.54f,
                cx - halfW, topY);
            path.CloseFigure();
            return path;
        }

        private GraphicsPath CreateLowerBulbPath(float cx, float neckY, float botY, float halfW, float neckHalfW)
        {
            GraphicsPath path = new GraphicsPath();
            float h = botY - neckY;
            path.AddLine(cx - neckHalfW, neckY, cx + neckHalfW, neckY);
            path.AddBezier(
                cx + neckHalfW, neckY,
                cx + neckHalfW + 7f, neckY + h * 0.16f,
                cx + halfW + 3f, botY - h * 0.54f,
                cx + halfW, botY);
            path.AddLine(cx + halfW, botY, cx - halfW, botY);
            path.AddBezier(
                cx - halfW, botY,
                cx - halfW - 3f, botY - h * 0.54f,
                cx - neckHalfW - 7f, neckY + h * 0.16f,
                cx - neckHalfW, neckY);
            path.CloseFigure();
            return path;
        }

        private GraphicsPath CreateFullGlassPath(float cx, float topY, float neckY, float botY, float halfW, float neckHalfW)
        {
            GraphicsPath path = new GraphicsPath();
            float hTop = neckY - topY;
            float hBot = botY - neckY;

            path.AddLine(cx - halfW, topY, cx + halfW, topY);
            path.AddBezier(
                cx + halfW, topY,
                cx + halfW + 3f, topY + hTop * 0.54f,
                cx + neckHalfW + 6f, neckY - hTop * 0.12f,
                cx + neckHalfW, neckY);
            path.AddBezier(
                cx + neckHalfW, neckY,
                cx + neckHalfW + 6f, neckY + hBot * 0.12f,
                cx + halfW + 3f, botY - hBot * 0.54f,
                cx + halfW, botY);
            path.AddLine(cx + halfW, botY, cx - halfW, botY);
            path.AddBezier(
                cx - halfW, botY,
                cx - halfW - 3f, botY - hBot * 0.54f,
                cx - neckHalfW - 6f, neckY + hBot * 0.12f,
                cx - neckHalfW, neckY);
            path.AddBezier(
                cx - neckHalfW, neckY,
                cx - neckHalfW - 6f, neckY - hTop * 0.12f,
                cx - halfW - 3f, topY + hTop * 0.54f,
                cx - halfW, topY);
            path.CloseFigure();
            return path;
        }

        internal static GraphicsPath CreateRoundedRect(float x, float y, float width, float height, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d > width) d = width;
            if (d > height) d = height;

            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string FormatTime(double totalSeconds)
        {
            int sec = (int)Math.Ceiling(Math.Max(0.0, totalSeconds));
            int hours = sec / 3600;
            int mins = (sec % 3600) / 60;
            int secs = sec % 60;

            if (hours > 0)
            {
                return string.Format("{0}:{1:D2}:{2:D2}", hours, mins, secs);
            }
            return string.Format("{0:D2}:{1:D2}", mins, secs);
        }
    }
}
