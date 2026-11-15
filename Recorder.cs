using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SimpleRecorder
{
    // ======================================================================
    //  Simple global mouse + keyboard recorder / replayer.
    //  F9 = record / stop recording, F10 = play / stop playback, Esc = stop.
    // ======================================================================
    internal static class Program
    {
        internal static bool Elevated { get; private set; }

        internal static bool IsElevated()
        {
            try
            {
                using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                    return new System.Security.Principal.WindowsPrincipal(id)
                        .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // A medium integrity process gets no hook callbacks for input aimed at an
        // elevated window and cannot inject input into it either. Games and their
        // launchers are often elevated, so the GUI relaunches itself elevated.
        private static bool RelaunchElevated(string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = string.Join(" ", args)
                };
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Native.EnableDpiAwareness();

            var argList = new System.Collections.Generic.List<string>(args ?? new string[0]);
            bool noElevate = argList.Remove("--noelevate");   // testing switch
            args = argList.ToArray();

            bool guiMode = args.Length == 0;
            Elevated = IsElevated();

            if (guiMode && !noElevate && !Elevated && RelaunchElevated(new string[0]))
                return 0;

            if (args != null && args.Length > 0 && args[0] == "--e2e")
            {
                string report2 = E2E.Run();
                string outPath2 = args.Length > 1 ? args[1] : "e2e.txt";
                try { File.WriteAllText(outPath2, report2); } catch { }
                return report2.IndexOf("FAIL", StringComparison.Ordinal) >= 0 ? 1 : 0;
            }

            if (args != null && args.Length > 0 && args[0] == "--selftest")
            {
                string report = SelfTest.Run();
                string outPath = args.Length > 1 ? args[1] : "selftest.txt";
                try { File.WriteAllText(outPath, report); } catch { }
                return report.IndexOf("FAIL", StringComparison.Ordinal) >= 0 ? 1 : 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }

    internal sealed class Ev
    {
        public const int MouseMove = 0;
        public const int MouseDown = 1;
        public const int MouseUp = 2;
        public const int Wheel = 3;
        public const int KeyDown = 4;
        public const int KeyUp = 5;

        public int Type;    // Ev.*
        public int X;       // screen x
        public int Y;       // screen y
        public int Data;    // button id / wheel delta / virtual key
        public int Scan;    // keyboard scan code
        public int Flags;   // bit0 = extended key
        public long Time;   // ms since record start
    }

    // ---------------- win32 structures ----------------
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;      // 0 = mouse, 1 = keyboard
        public InputUnion U;
    }

    internal delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    // ---------------- win32 constants + wrappers ----------------
    internal static class K
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_MBUTTONUP = 0x0208;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_XBUTTONDOWN = 0x020B;
        public const int WM_XBUTTONUP = 0x020C;

        public const int LLKHF_EXTENDED = 0x01;
        public const int LLKHF_INJECTED = 0x10;
        public const int LLMHF_INJECTED = 0x01;

        public const int VK_ESCAPE = 0x1B;
        public const int VK_F9 = 0x78;
        public const int VK_F10 = 0x79;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint MOUSEEVENTF_XDOWN = 0x0080;
        public const uint MOUSEEVENTF_XUP = 0x0100;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0008;

        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;
    }

    internal static class Native
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        // Absolute mouse coordinates are physical pixels, so the process must be
        // DPI aware. Otherwise GetSystemMetrics reports scaled (logical) values and
        // every replayed click lands off target on a scaled display.
        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }   // PER_MONITOR_AWARE_V2
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-2))) return; } catch { }   // PER_MONITOR_AWARE
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-1))) return; } catch { }   // SYSTEM_AWARE
            try { if (SetProcessDpiAwareness(2) == 0) return; } catch { }                  // PER_MONITOR
            try { if (SetProcessDpiAwareness(1) == 0) return; } catch { }                  // SYSTEM
            try { SetProcessDPIAware(); } catch { }
        }

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        // ---- screen geometry for absolute mouse coordinates (multi monitor aware) ----
        public static int VX { get { return GetSystemMetrics(K.SM_XVIRTUALSCREEN); } }
        public static int VY { get { return GetSystemMetrics(K.SM_YVIRTUALSCREEN); } }
        public static int VW { get { return Math.Max(1, GetSystemMetrics(K.SM_CXVIRTUALSCREEN)); } }
        public static int VH { get { return Math.Max(1, GetSystemMetrics(K.SM_CYVIRTUALSCREEN)); } }

        public static int Norm(int pos, int origin, int size)
        {
            double v = (pos - origin) * 65535.0 / Math.Max(1, size - 1);
            if (v < 0) v = 0;
            if (v > 65535) v = 65535;
            return (int)Math.Round(v);
        }

        public static uint ButtonFlag(int btn, bool down)
        {
            switch (btn)
            {
                case 0: return down ? K.MOUSEEVENTF_LEFTDOWN : K.MOUSEEVENTF_LEFTUP;
                case 1: return down ? K.MOUSEEVENTF_RIGHTDOWN : K.MOUSEEVENTF_RIGHTUP;
                case 2: return down ? K.MOUSEEVENTF_MIDDLEDOWN : K.MOUSEEVENTF_MIDDLEUP;
                default: return down ? K.MOUSEEVENTF_XDOWN : K.MOUSEEVENTF_XUP;
            }
        }

        public static uint SendEvent(Ev e)
        {
            INPUT[] inp = new INPUT[1];

            if (e.Type >= Ev.KeyDown)
            {
                inp[0].type = 1;
                uint f = 0;
                if (e.Scan != 0)
                {
                    f |= K.KEYEVENTF_SCANCODE;
                    inp[0].U.ki.wScan = (ushort)e.Scan;
                    inp[0].U.ki.wVk = (ushort)e.Data;   // harmless, some targets read both
                }
                else
                {
                    inp[0].U.ki.wVk = (ushort)e.Data;
                }
                if ((e.Flags & 1) != 0) f |= K.KEYEVENTF_EXTENDEDKEY;
                if (e.Type == Ev.KeyUp) f |= K.KEYEVENTF_KEYUP;
                inp[0].U.ki.dwFlags = f;
            }
            else
            {
                inp[0].type = 0;
                inp[0].U.mi.dx = Norm(e.X, VX, VW);
                inp[0].U.mi.dy = Norm(e.Y, VY, VH);

                uint f;
                if (e.Type == Ev.Wheel)
                {
                    f = K.MOUSEEVENTF_WHEEL;
                    inp[0].U.mi.mouseData = (uint)e.Data;
                }
                else
                {
                    f = K.MOUSEEVENTF_MOVE | K.MOUSEEVENTF_ABSOLUTE | K.MOUSEEVENTF_VIRTUALDESK;
                    if (e.Type == Ev.MouseDown) f |= ButtonFlag(e.Data, true);
                    else if (e.Type == Ev.MouseUp) f |= ButtonFlag(e.Data, false);
                    if (e.Data >= 3) inp[0].U.mi.mouseData = (uint)(e.Data == 3 ? 1 : 2);
                }
                inp[0].U.mi.dwFlags = f;
            }

            return SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    // ======================================================================
    //  Main window
    // ======================================================================
    internal sealed class MainForm : Form
    {
        private readonly List<Ev> _events = new List<Ev>();
        private readonly Stopwatch _sw = new Stopwatch();
        private IntPtr _kbHook = IntPtr.Zero;
        private IntPtr _msHook = IntPtr.Zero;
        private readonly LowLevelProc _kbProc;
        private readonly LowLevelProc _msProc;

        private volatile bool _recording;
        private volatile bool _playing;
        private volatile bool _abortPlay;
        private volatile int _playLoops;

        private int _cntKey;
        private int _cntClick;
        private int _cntMove;
        private int _cntWheel;

        private long _lastMoveTime;
        private int _lastMoveX;
        private int _lastMoveY;

        private Button _btnRecord;
        private Button _btnPlay;
        private Button _btnStop;
        private NumericUpDown _numRepeat;
        private CheckBox _chkInfinite;
        private CheckBox _chkMinimize;
        private bool _restoreAfterPlay;
        private NumericUpDown _numGap;
        private Label _lblStatus;
        private TextBox _txtLog;
        private System.Windows.Forms.Timer _uiTimer;

        public MainForm()
        {
            _kbProc = KbProc;
            _msProc = MsProc;

            // scale baseline must match the font, otherwise WinForms shrinks the
            // pixel layout and clips the controls on scaled displays
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;

            Text = "Simple Mouse + Keyboard Recorder" + (Program.Elevated ? "  [admin]" : "  [NOT admin]");
            ClientSize = new Size(566, 452);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            BuildUi();

            _kbHook = Native.SetWindowsHookEx(K.WH_KEYBOARD_LL, _kbProc, Native.GetModuleHandle(null), 0);
            if (_kbHook == IntPtr.Zero)
                AppendLog("WARNING: keyboard hook failed (" + Marshal.GetLastWin32Error() + ")");

            _uiTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _uiTimer.Tick += (s, e) => RefreshStatus();
            _uiTimer.Start();

            AppendLog(Program.Elevated
                ? "Ready (elevated).  F9 = record/stop, F10 = play, Esc = stop."
                : "Ready (NOT elevated - elevated targets receive no input).  F9 = record/stop, F10 = play, Esc = stop.");
        }

        private void BuildUi()
        {
            _btnRecord = new Button { Text = "● Record (F9)", Bounds = new Rectangle(12, 12, 124, 34) };
            _btnRecord.Click += (s, e) => StartRecording();

            _btnPlay = new Button { Text = "▶ Play (F10)", Bounds = new Rectangle(144, 12, 124, 34) };
            _btnPlay.Click += (s, e) => StartPlayback();

            _btnStop = new Button { Text = "■ Stop (Esc)", Bounds = new Rectangle(276, 12, 124, 34) };
            _btnStop.Click += (s, e) => { StopRecording(); StopPlayback(); };

            var lblRepeat = new Label { Text = "Repeat:", Location = new Point(412, 20), AutoSize = true };
            _numRepeat = new NumericUpDown
            {
                Bounds = new Rectangle(470, 16, 84, 26),
                Minimum = 1,
                Maximum = 1000000,
                Value = 1,
                TextAlign = HorizontalAlignment.Right
            };

            _chkInfinite = new CheckBox { Text = "Infinite loop", Location = new Point(12, 58), AutoSize = true };
            _chkInfinite.CheckedChanged += (s, e) =>
            {
                _numRepeat.Enabled = !_chkInfinite.Checked;
                RefreshStatus();
            };

            _chkMinimize = new CheckBox { Text = "Minimize while playing", Location = new Point(150, 58), AutoSize = true, Checked = true };

            var lblGap = new Label { Text = "Gap between loops (ms):", Location = new Point(12, 90), AutoSize = true };
            _numGap = new NumericUpDown
            {
                Bounds = new Rectangle(186, 86, 84, 26),
                Minimum = 0,
                Maximum = 600000,
                Value = 100,
                TextAlign = HorizontalAlignment.Right
            };

            _lblStatus = new Label
            {
                Text = "Idle - nothing recorded",
                Bounds = new Rectangle(12, 122, 542, 30),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoEllipsis = true
            };

            _txtLog = new TextBox
            {
                Bounds = new Rectangle(12, 158, 542, 282),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(250, 250, 250),
                WordWrap = true
            };

            Controls.AddRange(new Control[] { _btnRecord, _btnPlay, _btnStop, lblRepeat, _numRepeat, _chkInfinite, _chkMinimize, lblGap, _numGap, _lblStatus, _txtLog });
        }
        // ---------------- ui helpers ----------------
        private void Post(Action a)
        {
            if (!IsHandleCreated) return;
            try { BeginInvoke(a); } catch { }
        }

        private void AppendLog(string line)
        {
            if (InvokeRequired) { Post(() => AppendLog(line)); return; }
            _txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
        }

        private void RefreshStatus()
        {
            string s;
            if (_recording)
                s = string.Format("Recording...  keys {0} | clicks {1} | moves {2} | wheel {3}   {4:0.0}s   (F9 / Esc stops)",
                    _cntKey, _cntClick, _cntMove, _cntWheel, _sw.ElapsedMilliseconds / 1000.0);
            else if (_playing)
                s = string.Format("Playing...  loop {0}   (F10 / Esc to stop)", _playLoops);
            else
                s = _events.Count == 0 ? "Idle - nothing recorded" : string.Format("Idle - {0} events recorded, ready to play", _events.Count);

            _lblStatus.Text = s;
            _btnRecord.Enabled = !_playing;
            _btnPlay.Enabled = !_recording && _events.Count > 0;
        }

        // ---------------- recording ----------------
        internal static bool TestCaptureInjected;   // self test only: capture synthetic input too

        internal int PlayLoops { get { return _playLoops; } }
        internal bool IsPlaying { get { return _playing; } }


        internal void TestSetMinimize(bool on) { _chkMinimize.Checked = on; }

        internal void TestSetRepeat(int times, bool infinite)
        {
            _chkInfinite.Checked = infinite;
            _numRepeat.Value = Math.Max(1, Math.Min(1000000, times));
        }

        internal int RecordedCount { get { return _events.Count; } }

        internal Point LastMousePoint
        {
            get
            {
                for (int i = _events.Count - 1; i >= 0; i--)
                    if (_events[i].Type == Ev.MouseMove) return new Point(_events[i].X, _events[i].Y);
                return new Point(-1, -1);
            }
        }

        private void Record(Ev e)
        {
            _events.Add(e);
            switch (e.Type)
            {
                case Ev.KeyDown: _cntKey++; break;
                case Ev.MouseDown: _cntClick++; break;
                case Ev.Wheel: _cntWheel++; break;
                case Ev.MouseMove: _cntMove++; break;
            }
        }

        internal string DumpEvents(int max)
        {
            var sb = new StringBuilder();
            int n = Math.Min(max, _events.Count);
            for (int i = 0; i < n; i++)
            {
                Ev e = _events[i];
                sb.AppendLine(string.Format("  #{0} type={1} x={2} y={3} data={4} scan={5} t={6}",
                    i, e.Type, e.X, e.Y, e.Data, e.Scan, e.Time));
            }
            return sb.ToString();
        }

        internal void StartRecording()
        {
            if (_playing) return;

            _events.Clear();
            _cntKey = _cntClick = _cntMove = _cntWheel = 0;
            _lastMoveTime = -1000;
            _lastMoveX = int.MinValue / 2;
            _lastMoveY = int.MinValue / 2;
            _sw.Reset();
            _sw.Start();
            _recording = true;

            if (_msHook == IntPtr.Zero)
            {
                _msHook = Native.SetWindowsHookEx(K.WH_MOUSE_LL, _msProc, Native.GetModuleHandle(null), 0);
                if (_msHook == IntPtr.Zero)
                    AppendLog("WARNING: mouse hook failed (" + Marshal.GetLastWin32Error() + ")");
            }

            AppendLog("Recording started.");
            RefreshStatus();
        }

        internal void StopRecording()
        {
            if (!_recording) return;
            _recording = false;
            _sw.Stop();
            UninstallMouseHook();
            double secs = _sw.ElapsedMilliseconds / 1000.0;
            AppendLog(string.Format("Recording stopped: {0} events (keys {1}, clicks {2}, moves {3}, wheel {4}), {5:0.0}s.",
                _events.Count, _cntKey, _cntClick, _cntMove, _cntWheel, secs));

            if (_cntKey == 0 && secs > 2)
                AppendLog("NOTE: no keyboard events were captured. If the game/target runs elevated this recorder must run elevated too - it asks for admin on launch. Accept the UAC prompt and record again.");
            else if (_events.Count < 4 && secs > 2)
                AppendLog("NOTE: almost no input was captured. Windows blocks input hooks for windows running at a higher integrity level - keep this app elevated.");
            RefreshStatus();
        }

        private void UninstallMouseHook()
        {
            if (_msHook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_msHook);
                _msHook = IntPtr.Zero;
            }
        }

        // ---------------- hooks ----------------
        private IntPtr KbProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                if ((k.flags & K.LLKHF_INJECTED) == 0 || TestCaptureInjected)
                {
                    int msg = wParam.ToInt32();
                    bool down = msg == K.WM_KEYDOWN || msg == K.WM_SYSKEYDOWN;
                    int vk = (int)k.vkCode;

                    if (vk == K.VK_F9)
                    {
                        if (down)
                        {
                            if (_recording) Post(StopRecording);
                            else if (_playing) Post(StopPlayback);
                            else Post(StartRecording);
                        }
                        return (IntPtr)1;
                    }
                    if (vk == K.VK_F10)
                    {
                        if (down)
                        {
                            if (_playing) Post(StopPlayback);
                            else { Post(StopRecording); Post(StartPlayback); }
                        }
                        return (IntPtr)1;
                    }
                    if (vk == K.VK_ESCAPE && (_recording || _playing))
                    {
                        if (down) Post(() => { StopRecording(); StopPlayback(); });
                        return (IntPtr)1;
                    }

                    if (_recording)
                    {
                        Record(new Ev
                        {
                            Type = down ? Ev.KeyDown : Ev.KeyUp,
                            Data = vk,
                            Scan = (int)k.scanCode,
                            Flags = (int)(k.flags & K.LLKHF_EXTENDED),
                            Time = _sw.ElapsedMilliseconds
                        });
                    }
                }
            }
            return Native.CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        private IntPtr MsProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _recording)
            {
                MSLLHOOKSTRUCT m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                if (((m.flags & K.LLMHF_INJECTED) == 0 || TestCaptureInjected) && !PointOverSelf(m.pt.x, m.pt.y))
                {
                    long t = _sw.ElapsedMilliseconds;
                    int msg = wParam.ToInt32();

                    switch (msg)
                    {
                        case K.WM_MOUSEMOVE:
                            if (t - _lastMoveTime >= 10 ||
                                Math.Abs(m.pt.x - _lastMoveX) + Math.Abs(m.pt.y - _lastMoveY) >= 4)
                            {
                                _lastMoveTime = t;
                                _lastMoveX = m.pt.x;
                                _lastMoveY = m.pt.y;
                                Record(new Ev { Type = Ev.MouseMove, X = m.pt.x, Y = m.pt.y, Time = t });
                            }
                            break;

                        case K.WM_LBUTTONDOWN: AddMouseButton(Ev.MouseDown, 0, m, t); break;
                        case K.WM_LBUTTONUP: AddMouseButton(Ev.MouseUp, 0, m, t); break;
                        case K.WM_RBUTTONDOWN: AddMouseButton(Ev.MouseDown, 1, m, t); break;
                        case K.WM_RBUTTONUP: AddMouseButton(Ev.MouseUp, 1, m, t); break;
                        case K.WM_MBUTTONDOWN: AddMouseButton(Ev.MouseDown, 2, m, t); break;
                        case K.WM_MBUTTONUP: AddMouseButton(Ev.MouseUp, 2, m, t); break;
                        case K.WM_XBUTTONDOWN:
                        case K.WM_XBUTTONUP:
                        {
                            int which = (int)((m.mouseData >> 16) & 0xFFFF);
                            int btn = which == 2 ? 4 : 3;
                            AddMouseButton(msg == K.WM_XBUTTONDOWN ? Ev.MouseDown : Ev.MouseUp, btn, m, t);
                            break;
                        }
                        case K.WM_MOUSEWHEEL:
                            Record(new Ev
                            {
                                Type = Ev.Wheel,
                                X = m.pt.x,
                                Y = m.pt.y,
                                Data = (short)((m.mouseData >> 16) & 0xFFFF),
                                Time = t
                            });
                            break;
                    }
                }
            }
            return Native.CallNextHookEx(_msHook, nCode, wParam, lParam);
        }

        private void AddMouseButton(int type, int button, MSLLHOOKSTRUCT m, long t)
        {
            Record(new Ev { Type = type, X = m.pt.x, Y = m.pt.y, Data = button, Time = t });
        }

        private bool PointOverSelf(int x, int y)
        {
            if (TestCaptureInjected) return false;
            if (!Visible || WindowState == FormWindowState.Minimized) return false;
            return Bounds.Contains(x, y);
        }

        // ---------------- playback ----------------
        internal void StartPlayback()
        {
            if (_recording || _playing) return;
            if (_events.Count == 0)
            {
                AppendLog("Nothing to play - record something first.");
                return;
            }

            Ev[] snapshot = _events.ToArray();
            int repeat = _chkInfinite.Checked ? 0 : (int)_numRepeat.Value;
            int gap = (int)_numGap.Value;

            _abortPlay = false;
            _playing = true;
            _playLoops = 0;

            if (_chkMinimize.Checked && WindowState != FormWindowState.Minimized)
            {
                _restoreAfterPlay = true;
                WindowState = FormWindowState.Minimized;
            }

            RefreshStatus();
            AppendLog(string.Format("Playback started: {0} events, {1}.",
                snapshot.Length, repeat <= 0 ? "infinite repeat" : repeat + " repeat(s)"));

            var th = new Thread(() => PlayLoop(snapshot, repeat, gap)) { IsBackground = true };
            th.Start();
        }

        internal void StopPlayback()
        {
            if (!_playing) return;
            _abortPlay = true;
            AppendLog("Stop requested.");
        }

        private void PlayLoop(Ev[] evts, int repeat, int gapMs)
        {
            int loops = repeat <= 0 ? int.MaxValue : repeat;
            var sw = Stopwatch.StartNew();
            int done = 0;

            try
            {
                // give the F10 key-up time to be released before injecting anything
                if (!WaitTicks(sw, 180)) return;

                for (int loop = 0; loop < loops; loop++)
                {
                    if (_abortPlay) break;
                    long t0 = sw.ElapsedMilliseconds;

                    for (int i = 0; i < evts.Length; i++)
                    {
                        if (!WaitUntil(sw, t0 + evts[i].Time)) break;
                        Native.SendEvent(evts[i]);
                    }

                    if (_abortPlay) break;

                    done++;
                    _playLoops = done;
                    int finished = done;
                    int totalLoops = loops;
                    if (totalLoops <= 20 || finished % 100 == 0)
                        Post(() => AppendLog(string.Format("Loop {0} finished.", finished)));

                    if (loop + 1 < loops && gapMs > 0)
                    {
                        if (!WaitTicks(sw, gapMs)) break;
                    }
                }
            }
            finally
            {
                int total = done;
                Post(() =>
                {
                    _playing = false;
                    _abortPlay = false;
                    if (_restoreAfterPlay)
                    {
                        _restoreAfterPlay = false;
                        WindowState = FormWindowState.Normal;
                        Activate();
                    }
                    AppendLog(string.Format("Playback stopped after {0} loop(s).", total));
                    RefreshStatus();
                });
            }
        }

        private bool WaitUntil(Stopwatch sw, long target)
        {
            while (!_abortPlay)
            {
                long rem = target - sw.ElapsedMilliseconds;
                if (rem <= 0) return true;
                Thread.Sleep((int)Math.Min(rem, 10));
            }
            return false;
        }

        private bool WaitTicks(Stopwatch sw, long ms)
        {
            return WaitUntil(sw, sw.ElapsedMilliseconds + ms);
        }

        // ---------------- shutdown ----------------
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _abortPlay = true;
            _recording = false;
            UninstallMouseHook();
            if (_kbHook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_kbHook);
                _kbHook = IntPtr.Zero;
            }
            if (_uiTimer != null) _uiTimer.Stop();
            base.OnFormClosing(e);
        }
    }

    // ======================================================================
    //  Self test: verifies hook installation, hook struct parsing and
    //  SendInput marshalling without touching the normal UI flow.
    // ======================================================================

    // ======================================================================
    //  Self test (--selftest out.txt)
    //  Verifies: hook installation, hook callback plumbing, struct parsing
    //  of KBDLLHOOKSTRUCT / MSLLHOOKSTRUCT, and SendInput marshalling.
    // ======================================================================
    internal static class SelfTest
    {
        private static int _kbEvents;
        private static int _msEvents;
        private static int _lastVk = -1;
        private static int _lastScan = -1;
        private static int _lastPtX = int.MinValue;
        private static int _lastPtY = int.MinValue;
        private static int _sendOk;
        private static int _shown;
        private static POINT _saved;

        // Hook procedures must stay rooted, otherwise Windows calls a dead thunk.
        private static readonly LowLevelProc KbHook = (n, w, l) =>
        {
            if (n >= 0)
            {
                _kbEvents++;
                var k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(KBDLLHOOKSTRUCT));
                _lastVk = (int)k.vkCode;
                _lastScan = (int)k.scanCode;
            }
            return Native.CallNextHookEx(IntPtr.Zero, n, w, l);
        };

        private static readonly LowLevelProc MsHook = (n, w, l) =>
        {
            if (n >= 0)
            {
                _msEvents++;
                var m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(MSLLHOOKSTRUCT));
                _lastPtX = m.pt.x;
                _lastPtY = m.pt.y;
            }
            return Native.CallNextHookEx(IntPtr.Zero, n, w, l);
        };

        private static void CloseLater(Form f)
        {
            try { f.BeginInvoke(new Action(() => { try { f.Close(); } catch { } })); } catch { }
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            Native.GetCursorPos(out _saved);

            IntPtr hk = Native.SetWindowsHookEx(K.WH_KEYBOARD_LL, KbHook, Native.GetModuleHandle(null), 0);
            IntPtr hm = Native.SetWindowsHookEx(K.WH_MOUSE_LL, MsHook, Native.GetModuleHandle(null), 0);
            sb.AppendLine("keyboard_hook=" + (hk != IntPtr.Zero));
            sb.AppendLine("mouse_hook=" + (hm != IntPtr.Zero));

            var f = new Form
            {
                Text = "selftest",
                Width = 240,
                Height = 140,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterScreen,
                Opacity = 0.02
            };

            f.Shown += (s, e) =>
            {
                _shown = 1;
                Native.SetForegroundWindow(f.Handle);   // injected input must have a target
                var injector = new Thread(() =>
                {
                    Thread.Sleep(250);
                    uint a = Native.SendEvent(new Ev { Type = Ev.KeyDown, Data = 0x41, Scan = 0x1E });
                    uint b = Native.SendEvent(new Ev { Type = Ev.KeyUp, Data = 0x41, Scan = 0x1E });
                    uint c = Native.SendEvent(new Ev { Type = Ev.MouseMove, X = _saved.x + 6, Y = _saved.y + 6 });
                    if (a == 1 && b == 1 && c == 1) _sendOk = 1;
                    Thread.Sleep(500);
                    try { Native.SetCursorPos(_saved.x, _saved.y); } catch { }
                    CloseLater(f);
                })
                { IsBackground = true };
                injector.Start();
            };

            var watchdog = new System.Windows.Forms.Timer { Interval = 5000 };
            watchdog.Tick += (s, e) => { watchdog.Stop(); try { f.Close(); } catch { } };
            watchdog.Start();

            Application.Run(f);

            Native.UnhookWindowsHookEx(hk);
            Native.UnhookWindowsHookEx(hm);
            try { Native.SetCursorPos(_saved.x, _saved.y); } catch { }

            sb.AppendLine("form_shown=" + (_shown == 1));
            sb.AppendLine("saved=" + _saved.x + "," + _saved.y);
            sb.AppendLine("sendinput_ok=" + (_sendOk == 1));
            sb.AppendLine("kb_events_seen=" + _kbEvents);
            sb.AppendLine("last_vk=0x" + (_lastVk < 0 ? "??" : _lastVk.ToString("X2")));
            sb.AppendLine("last_scan=0x" + (_lastScan < 0 ? "??" : _lastScan.ToString("X2")));
            sb.AppendLine("ms_events_seen=" + _msEvents);
            sb.AppendLine("last_mouse=" + _lastPtX + "," + _lastPtY);

            // pixel -> normalized -> pixel must round trip inside the virtual screen
            int probe = 250;
            int norm = Native.Norm(probe, Native.VX, Native.VW);
            int back = (int)Math.Round(norm * Native.VW / 65535.0) + Native.VX;
            sb.AppendLine("coord_roundtrip=" + probe + "->" + norm + "->" + back);
            sb.AppendLine("virtual_screen=" + Native.VX + "," + Native.VY + " " + Native.VW + "x" + Native.VH);

            bool pass = hk != IntPtr.Zero && hm != IntPtr.Zero
                        && _shown == 1
                        && _sendOk == 1
                        && _kbEvents >= 2
                        && _lastVk == 0x41
                        && _lastScan == 0x1E
                        && _msEvents >= 1
                        && Math.Abs(back - probe) <= 1;

            sb.AppendLine(pass ? "RESULT=PASS" : "RESULT=FAIL");
            return sb.ToString();
        }
    }
    // ======================================================================
    //  End to end test (--e2e out.txt): records a synthetic session through
    //  the real recording path, then replays it and counts what came back.
    // ======================================================================
    internal static class E2E
    {
        private static int _hookKb;
        private static int _hookMs;
        private static int _lastX = -1;
        private static int _lastY = -1;
        private static int _state;
        private static long _stateAt;
        private static readonly Stopwatch Sw = Stopwatch.StartNew();
        private static MainForm _form;
        private static int _recorded = -1;
        private static string _dump = "";
        private static int _kbAfterReplay;
        private static int _msAfterReplay;
        private static int _repeatLoops = -1;
        private static bool _stillPlaying = true;
        private static readonly StringBuilder InjectLog = new StringBuilder();
        private static int _hookKbAtRecord;
        private static int _hookMsAtRecord;
        private static int _injectStarted;
        private static int _injectDone;
        private static int _infLoops = -1;
        private static int _replayX = -1;
        private static int _replayY = -1;
        private static System.Drawing.Point _recLast = new System.Drawing.Point(-1, -1);
        private static bool _infPlayingBefore;
        private static bool _infStillPlayingAfterStop = true;

        private static readonly LowLevelProc KbHook = (n, w, l) =>
        {
            if (n >= 0) _hookKb++;
            return Native.CallNextHookEx(IntPtr.Zero, n, w, l);
        };

        private static readonly LowLevelProc MsHook = (n, w, l) =>
        {
            if (n >= 0)
            {
                _hookMs++;
                var m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(MSLLHOOKSTRUCT));
                _lastX = m.pt.x;
                _lastY = m.pt.y;
            }
            return Native.CallNextHookEx(IntPtr.Zero, n, w, l);
        };

        public static string Run()
        {
            MainForm.TestCaptureInjected = true;

            IntPtr hk = Native.SetWindowsHookEx(K.WH_KEYBOARD_LL, KbHook, Native.GetModuleHandle(null), 0);
            IntPtr hm = Native.SetWindowsHookEx(K.WH_MOUSE_LL, MsHook, Native.GetModuleHandle(null), 0);

            _form = new MainForm
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(100, 100),
                ShowInTaskbar = false,
                Opacity = 0.03
            };
            _form.TestSetMinimize(false);   // keep the test window on screen as a harmless click target
            _form.Shown += (s, e) => { _state = 1; _stateAt = Sw.ElapsedMilliseconds; };

            var tick = new System.Windows.Forms.Timer { Interval = 50 };
            tick.Tick += (s, e) => Step();
            tick.Start();

            Application.Run(_form);
            tick.Stop();

            Native.UnhookWindowsHookEx(hk);
            Native.UnhookWindowsHookEx(hm);

            var sb = new StringBuilder();
            sb.AppendLine("record_hooks=" + (hk != IntPtr.Zero) + "/" + (hm != IntPtr.Zero));
            sb.AppendLine("recorded_events=" + _recorded);
            sb.AppendLine("sendinput_returns=" + InjectLog.ToString());
            sb.AppendLine("hook_kb_at_record_stop=" + _hookKbAtRecord + " hook_ms_at_record_stop=" + _hookMsAtRecord);
            sb.AppendLine("recorded_sample:\n" + _dump);
            sb.AppendLine("kb_events_during_replay=" + _kbAfterReplay);
            sb.AppendLine("ms_events_during_replay=" + _msAfterReplay);
            sb.AppendLine("recorded_last_mouse=" + _recLast.X + "," + _recLast.Y);
            sb.AppendLine("replay_last_mouse=" + _replayX + "," + _replayY);
            sb.AppendLine("repeat_3_loops=" + _repeatLoops);
            sb.AppendLine("repeat_still_playing=" + _stillPlaying);
            sb.AppendLine("infinite_loops_in_900ms=" + _infLoops);
            sb.AppendLine("infinite_was_playing=" + _infPlayingBefore);
            sb.AppendLine("infinite_playing_after_stop=" + _infStillPlayingAfterStop);

            bool pass = hk != IntPtr.Zero && hm != IntPtr.Zero
                        && _recorded >= 4
                        && _kbAfterReplay >= 2
                        && _msAfterReplay >= 2
                        && Math.Abs(_recLast.X - 320) <= 2
                        && Math.Abs(_recLast.Y - 320) <= 2
                        && _repeatLoops == 3
                        && !_stillPlaying
                        && _infLoops >= 1
                        && _infPlayingBefore
                        && !_infStillPlayingAfterStop;

            sb.AppendLine(pass ? "RESULT=PASS" : "RESULT=FAIL");
            return sb.ToString();
        }

        private static void Step()
        {
            long now = Sw.ElapsedMilliseconds;

            switch (_state)
            {
                case 1:
                    if (now - _stateAt < 250) return;
                    _form.StartRecording();
                    _hookKb = 0; _hookMs = 0;
                    _state = 2;
                    _stateAt = now;
                    break;

                case 2:
                {
                    if (now - _stateAt < 60) return;
                    if (_injectStarted == 0)
                    {
                        // never block the hook thread: inject from a worker
                        _injectStarted = 1;
                        var th = new Thread(() => { InjectSession(); Interlocked.Exchange(ref _injectDone, 1); })
                        { IsBackground = true };
                        th.Start();
                        return;
                    }
                    if (_injectDone == 0) return;
                    _state = 3;
                    _stateAt = now;
                    break;
                }

                case 3:
                {
                    if (now - _stateAt < 500) return;
                    _form.StopRecording();
                    _hookKbAtRecord = _hookKb;
                    _hookMsAtRecord = _hookMs;
                    _recorded = _form.RecordedCount;
                    _dump = _form.DumpEvents(10);
                    if (_recorded <= 0) { _state = 9; break; }
                    _kbAfterReplay = 0;
                    _msAfterReplay = 0;
                    _hookKb = 0;
                    _hookMs = 0;
                    _form.StartPlayback();
                    _state = 4;
                    _stateAt = now;
                    break;
                }

                case 4:
                    if (now - _stateAt < 1800) return;
                    _kbAfterReplay = _hookKb;
                    _msAfterReplay = _hookMs;
                    _replayX = _lastX;
                    _replayY = _lastY;
                    _recLast = _form.LastMousePoint;
                    _form.TestSetRepeat(3, false);
                    _form.StartPlayback();
                    _state = 5;
                    _stateAt = now;
                    break;

                case 5:
                    if (now - _stateAt < 2600) return;
                    _repeatLoops = _form.PlayLoops;
                    _stillPlaying = _form.IsPlaying;
                    _form.TestSetRepeat(1, true);
                    _form.StartPlayback();
                    _state = 6;
                    _stateAt = now;
                    break;

                case 6:
                    if (now - _stateAt < 900) return;
                    _infLoops = _form.PlayLoops;
                    _infPlayingBefore = _form.IsPlaying;
                    _form.StopPlayback();
                    _state = 7;
                    _stateAt = now;
                    break;

                case 7:
                    if (now - _stateAt < 500) return;
                    _infStillPlayingAfterStop = _form.IsPlaying;
                    _state = 9;
                    break;

                case 9:
                    try { _form.Close(); } catch { }
                    break;
            }
        }

        private static void InjectSession()
        {
            InjectLog.Append("k_down=" + Native.SendEvent(new Ev { Type = Ev.KeyDown, Data = 0x41, Scan = 0x1E }));
            InjectLog.Append(" k_up=" + Native.SendEvent(new Ev { Type = Ev.KeyUp, Data = 0x41, Scan = 0x1E }));
            Thread.Sleep(40);
            InjectLog.Append(" move1=" + Native.SendEvent(new Ev { Type = Ev.MouseMove, X = 300, Y = 300 }));
            Thread.Sleep(40);
            InjectLog.Append(" move2=" + Native.SendEvent(new Ev { Type = Ev.MouseMove, X = 320, Y = 320 }));
            Thread.Sleep(40);
            InjectLog.Append(" down=" + Native.SendEvent(new Ev { Type = Ev.MouseDown, X = 320, Y = 320, Data = 0 }));
            InjectLog.Append(" up=" + Native.SendEvent(new Ev { Type = Ev.MouseUp, X = 320, Y = 320, Data = 0 }));
        }
    }
}