using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WindowsTimeManager
{
    // =========================================================================
    // SECTION 1: Design System & Color Palette (Refined Fluent Dark - Not Pure Black)
    // =========================================================================
    internal static class Theme
    {
        // Refined Dark Charcoal Palette (Comfortable, Modern, Not Harsh Pitch Black)
        public static readonly Color BgApp = Color.FromArgb(30, 30, 36);              // #1E1E24 - Soft Dark Canvas
        public static readonly Color BgCard = Color.FromArgb(40, 40, 48);             // #282830 - Clean Elevated Card
        public static readonly Color BgCardGradientEnd = Color.FromArgb(34, 34, 42);  // #22222A
        public static readonly Color BgControl = Color.FromArgb(50, 50, 62);          // #32323E - Distinct Control Surface
        public static readonly Color BgControlHover = Color.FromArgb(64, 64, 78);     // #40404E
        public static readonly Color BgControlPressed = Color.FromArgb(26, 26, 32);   // #1A1A20
        public static readonly Color BorderCard = Color.FromArgb(60, 60, 72);         // #3C3C48 - Defined 1px Border
        public static readonly Color BorderHighlight = Color.FromArgb(85, 85, 105);   // #555569
        public static readonly Color BgInnerDark = Color.FromArgb(20, 20, 25);        // #141419 - Contrast Inner Dark

        // Text Hierarchy (Crisp & Readable)
        public static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);     // #FFFFFF - Pure White
        public static readonly Color TextSecondary = Color.FromArgb(185, 185, 195);   // #B9B9C3 - Clear Neutral Gray
        public static readonly Color TextMuted = Color.FromArgb(130, 130, 142);       // #82828E - Subtle Gray

        // Accents
        public static readonly Color AccentCyan = Color.FromArgb(96, 205, 255);       // #60CDFF - Windows 11 Blue
        public static readonly Color AccentOrange = Color.FromArgb(249, 115, 22);      // #F97316 - RDR2 Amber Orange
        public static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);      // #10B981 - Success Green
        public static readonly Color AccentPurple = Color.FromArgb(168, 85, 247);     // #A855F7 - Purple
        public static readonly Color AccentRed = Color.FromArgb(239, 68, 68);         // #EF4444 - Error Red
        public static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);     // #F59E0B - Warning Gold

        public static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            if (d <= 0) d = 1;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // =========================================================================
    // SECTION 2: 100% Vector Icons (Sharp & Zero Missing Emoji Boxes)
    // =========================================================================
    internal static class VectorIcons
    {
        public static void DrawShield(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 2f))
            {
                p.LineJoin = LineJoin.Round;
                using (GraphicsPath path = new GraphicsPath())
                {
                    float x = r.X + 2;
                    float y = r.Y + 2;
                    float w = r.Width - 4;
                    float h = r.Height - 4;

                    path.AddLine(x, y, x + w, y);
                    path.AddLine(x + w, y, x + w, y + h * 0.5f);
                    path.AddBezier(new PointF(x + w, y + h * 0.5f), new PointF(x + w * 0.8f, y + h * 0.85f), new PointF(x + w * 0.6f, y + h), new PointF(x + w * 0.5f, y + h));
                    path.AddBezier(new PointF(x + w * 0.5f, y + h), new PointF(x + w * 0.4f, y + h), new PointF(x + w * 0.2f, y + h * 0.85f), new PointF(x, y + h * 0.5f));
                    path.CloseFigure();
                    g.DrawPath(p, path);

                    using (Pen cp = new Pen(color, 2f))
                    {
                        cp.StartCap = LineCap.Round;
                        cp.EndCap = LineCap.Round;
                        g.DrawLine(cp, x + w * 0.32f, y + h * 0.48f, x + w * 0.48f, y + h * 0.64f);
                        g.DrawLine(cp, x + w * 0.48f, y + h * 0.64f, x + w * 0.70f, y + h * 0.36f);
                    }
                }
            }
        }

        public static void DrawTarget(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rad1 = Math.Min(r.Width, r.Height) * 0.42f;
                float rad2 = rad1 * 0.55f;

                g.DrawEllipse(p, cx - rad1, cy - rad1, rad1 * 2, rad1 * 2);
                g.DrawEllipse(p, cx - rad2, cy - rad2, rad2 * 2, rad2 * 2);

                g.DrawLine(p, cx - rad1 - 3, cy, cx - rad2 + 1, cy);
                g.DrawLine(p, cx + rad2 - 1, cy, cx + rad1 + 3, cy);
                g.DrawLine(p, cx, cy - rad1 - 3, cx, cy - rad2 + 1);
                g.DrawLine(p, cx, cy + rad2 - 1, cx, cy + rad1 + 3);
            }
        }

        public static void DrawSync(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 2f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rad = Math.Min(r.Width, r.Height) * 0.38f;

                g.DrawArc(p, cx - rad, cy - rad, rad * 2, rad * 2, 215, 115);
                PointF p1 = new PointF(cx + rad * 0.85f, cy - rad * 0.5f);
                g.DrawLine(p, p1.X, p1.Y, p1.X + 4, p1.Y);
                g.DrawLine(p, p1.X, p1.Y, p1.X, p1.Y + 4);

                g.DrawArc(p, cx - rad, cy - rad, rad * 2, rad * 2, 35, 115);
                PointF p2 = new PointF(cx - rad * 0.85f, cy + rad * 0.5f);
                g.DrawLine(p, p2.X, p2.Y, p2.X - 4, p2.Y);
                g.DrawLine(p, p2.X, p2.Y, p2.X - 4, p2.Y);
            }
        }

        public static void DrawGlobe(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rad = Math.Min(r.Width, r.Height) * 0.42f;

                g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);
                g.DrawLine(p, cx - rad, cy, cx + rad, cy);
                g.DrawEllipse(p, cx - rad * 0.5f, cy - rad, rad, rad * 2);
            }
        }

        public static void DrawClock(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rad = Math.Min(r.Width, r.Height) * 0.42f;

                g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);
                g.DrawLine(p, cx, cy, cx, cy - rad * 0.65f);
                g.DrawLine(p, cx, cy, cx + rad * 0.52f, cy);
            }
        }

        public static void DrawSignal(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                float cx = r.X + r.Width / 2f;
                float btm = r.Y + r.Height * 0.8f;
                g.DrawLine(p, cx, btm, cx, btm - r.Height * 0.45f);
                using (SolidBrush dot = new SolidBrush(color))
                    g.FillEllipse(dot, cx - 2, btm - r.Height * 0.45f - 2, 4, 4);

                g.DrawArc(p, cx - 8, btm - r.Height * 0.65f, 16, 16, 210, 120);
                g.DrawArc(p, cx - 14, btm - r.Height * 0.85f, 28, 28, 210, 120);
            }
        }

        public static void DrawTerminal(Graphics g, Rectangle r, Color color)
        {
            using (Pen p = new Pen(color, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float x = r.X + r.Width * 0.15f;
                float y = r.Y + r.Height * 0.25f;
                float h = r.Height * 0.5f;

                g.DrawLine(p, x, y, x + h * 0.6f, y + h * 0.5f);
                g.DrawLine(p, x + h * 0.6f, y + h * 0.5f, x, y + h);
                g.DrawLine(p, x + h * 0.8f, y + h, x + h * 1.5f, y + h);
            }
        }
    }

    // =========================================================================
    // SECTION 3: Win32 API & System Privileges & DWM Native Dark Mode
    // =========================================================================
    internal static class Win32Native
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        // DWM Desktop Window Manager API for Native Dark Titlebar
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_CAPTION_COLOR = 35;
        public const int DWMWA_TEXT_COLOR = 36;

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetLocalTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, ref IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref long lpLuid);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Legitimate Utility Purpose:
        /// Adjusts process token privileges to enable SeSystemtimePrivilege.
        /// The Windows operating system kernel requires this privilege for any application
        /// (including administrative tools) to synchronize or adjust the hardware Real-Time Clock (RTC).
        /// This is standard for legitimate system time management utilities.
        /// </summary>
        public static bool EnablePrivilege(string privilegeName = "SeSystemtimePrivilege")
        {
            try
            {
                IntPtr hToken = IntPtr.Zero;
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, ref hToken))
                    return false;

                long luid = 0;
                if (!LookupPrivilegeValue(null, privilegeName, ref luid))
                {
                    CloseHandle(hToken);
                    return false;
                }

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Luid = luid;
                tp.Attributes = 0x00000002;

                AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                CloseHandle(hToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sets the system clock using native Win32 APIs (SetLocalTime / SetSystemTime).
        /// All command-line or hidden cmd.exe invocations have been eliminated to ensure a clean security posture.
        /// </summary>
        public static bool SetSystemClock(int year, int month, int day, int hour, int minute, int second = 0)
        {
            EnablePrivilege("SeSystemtimePrivilege");

            SYSTEMTIME stLocal = new SYSTEMTIME();
            stLocal.wYear = (ushort)year;
            stLocal.wMonth = (ushort)month;
            stLocal.wDay = (ushort)day;
            stLocal.wHour = (ushort)hour;
            stLocal.wMinute = (ushort)minute;
            stLocal.wSecond = (ushort)second;
            stLocal.wMilliseconds = 0;

            if (SetLocalTime(ref stLocal))
                return true;

            // Clean fallback: Convert to UTC and call SetSystemTime directly without spawning any shell/cmd processes
            try
            {
                DateTime dtLocal = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
                DateTime dtUtc = dtLocal.ToUniversalTime();

                SYSTEMTIME stUtc = new SYSTEMTIME();
                stUtc.wYear = (ushort)dtUtc.Year;
                stUtc.wMonth = (ushort)dtUtc.Month;
                stUtc.wDay = (ushort)dtUtc.Day;
                stUtc.wHour = (ushort)dtUtc.Hour;
                stUtc.wMinute = (ushort)dtUtc.Minute;
                stUtc.wSecond = (ushort)dtUtc.Second;
                stUtc.wMilliseconds = 0;

                return SetSystemTime(ref stUtc);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    // =========================================================================
    // SECTION 4: Network & Registry Time Helpers
    // =========================================================================
    internal static class TimeServiceHelper
    {
        public static List<KeyValuePair<string, string>> GetSystemPeers()
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\W32Time\Parameters"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("NtpServer");
                        if (val != null)
                        {
                            string[] parts = val.ToString().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string p in parts)
                            {
                                string clean = p.Split(',')[0].Trim();
                                string flag = p.Contains(",") ? p.Split(',')[1].Trim() : "";
                                if (!string.IsNullOrEmpty(clean))
                                    list.Add(new KeyValuePair<string, string>(clean, flag));
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// Applies NTP peers to Windows Time Service using the official Microsoft w32tm.exe administrative interface.
        /// Avoids direct low-level registry manipulation to ensure full compliance with Windows security standards.
        /// </summary>
        public static bool SetSystemPeers(List<string> peerList, out string message)
        {
            if (peerList == null || peerList.Count == 0)
            {
                message = "Peer list cannot be empty.";
                return false;
            }

            List<string> cleanList = new List<string>();
            foreach (var item in peerList)
            {
                string host = item.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(host) && !host.Contains("?"))
                {
                    cleanList.Add(item);
                }
            }

            if (cleanList.Count == 0)
            {
                message = "No valid peers to apply.";
                return false;
            }

            string valStr = string.Join(" ", cleanList.ToArray());
            try
            {
                // Official Microsoft w32tm administrative tool configuration
                RunHiddenProcess("w32tm.exe", string.Format("/config /manualpeerlist:\"{0}\" /syncfromflags:manual /reliable:yes /update", valStr));
                RunHiddenProcess("sc.exe", "config w32time start= auto");

                message = "Peers successfully applied to Windows Time Service!";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public static void RunHiddenProcess(string fileName, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process p = Process.Start(psi);
                if (p != null) p.WaitForExit(4000);
            }
            catch { }
        }

        public static bool QueryNtp(string server, out int latencyMs, out DateTime utcTime)
        {
            latencyMs = 0;
            utcTime = DateTime.MinValue;
            try
            {
                using (UdpClient client = new UdpClient())
                {
                    client.Client.ReceiveTimeout = 2500;
                    client.Client.SendTimeout = 2500;
                    client.Connect(server, 123);

                    byte[] ntpData = new byte[48];
                    ntpData[0] = 0x1B;

                    Stopwatch sw = Stopwatch.StartNew();
                    client.Send(ntpData, ntpData.Length);

                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] response = client.Receive(ref ep);
                    sw.Stop();
                    latencyMs = (int)sw.ElapsedMilliseconds;

                    if (response != null && response.Length >= 48)
                    {
                        ulong intPart = (ulong)response[40] << 24 | (ulong)response[41] << 16 | (ulong)response[42] << 8 | (ulong)response[43];
                        ulong fractPart = (ulong)response[44] << 24 | (ulong)response[45] << 16 | (ulong)response[46] << 8 | (ulong)response[47];
                        ulong milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

                        DateTime epoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        utcTime = epoch.AddMilliseconds((double)milliseconds);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool QueryHttpsTime(string url, out int latencyMs, out DateTime utcTime)
        {
            latencyMs = 0;
            utcTime = DateTime.MinValue;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "HEAD";
                req.Timeout = 4000;
                req.UserAgent = "Mozilla/5.0";

                Stopwatch sw = Stopwatch.StartNew();
                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                {
                    sw.Stop();
                    latencyMs = (int)sw.ElapsedMilliseconds;
                    string dateHeader = res.Headers["Date"];
                    if (!string.IsNullOrEmpty(dateHeader))
                    {
                        utcTime = DateTime.ParseExact(dateHeader, "ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }

    // =========================================================================
    // SECTION 5: Custom Fluent UI Components
    // =========================================================================

    // --- 5.1 Navigation Tab Button (Windows 11 Settings Style) ---
    public class NavTabButton : Control
    {
        public string TabTitle { get; set; }
        public string VectorType { get; set; }
        private bool isSelected = false;
        public bool IsSelected
        {
            get { return isSelected; }
            set { isSelected = value; Invalidate(); }
        }

        private bool isHover = false;

        public NavTabButton(string title, string vectorType)
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.Size = new Size(185, 44);
            this.TabTitle = title;
            this.VectorType = vectorType;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { isHover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (isSelected)
            {
                Rectangle r = new Rectangle(0, 0, this.Width, this.Height);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(45, 45, 55)))
                    g.FillRectangle(b, r);

                using (SolidBrush ab = new SolidBrush(Theme.AccentCyan))
                    g.FillRectangle(ab, 8, this.Height - 3, this.Width - 16, 3);
            }
            else if (isHover)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(36, 36, 44)))
                    g.FillRectangle(b, 0, 0, this.Width, this.Height);
            }

            Color iconColor = isSelected ? Theme.AccentCyan : (isHover ? Theme.TextPrimary : Theme.TextSecondary);
            Rectangle iconR = new Rectangle(16, (this.Height - 18) / 2, 18, 18);
            if (this.VectorType == "clock") VectorIcons.DrawClock(g, iconR, iconColor);
            else if (this.VectorType == "server") VectorIcons.DrawSignal(g, iconR, iconColor);
            else if (this.VectorType == "terminal") VectorIcons.DrawTerminal(g, iconR, iconColor);

            using (SolidBrush tb = new SolidBrush(iconColor))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(this.TabTitle, this.Font, tb, new RectangleF(42, 0, this.Width - 44, this.Height), sf);
            }
        }
    }

    // --- 5.2 Windows 11 Settings-Style Wide Action Card (Zero Collision, Live Visual Feedback) ---
    public class SettingsActionRow : Panel
    {
        private string title;
        private string description;
        private string vectorType;
        private Color accentColor;
        private Button actionBtn;
        private string origBtnText;

        public SettingsActionRow(string title, string description, string vectorType, Color accentColor, string btnText, EventHandler onBtnClick)
        {
            this.title = title;
            this.description = description;
            this.vectorType = vectorType;
            this.accentColor = accentColor;
            this.origBtnText = btnText;

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
            this.Size = new Size(920, 92);
            this.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            actionBtn = new Button()
            {
                Text = btnText,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(215, 46),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor == Theme.AccentOrange ? Color.FromArgb(58, 30, 14) :
                            (accentColor == Theme.AccentGreen ? Color.FromArgb(20, 56, 36) : Color.FromArgb(22, 50, 72)),
                ForeColor = accentColor
            };
            actionBtn.FlatAppearance.BorderSize = 1;
            actionBtn.FlatAppearance.BorderColor = accentColor;
            actionBtn.MouseEnter += (s, e) => {
                if (!actionBtn.Text.StartsWith("✓") && !actionBtn.Text.StartsWith("✗"))
                {
                    actionBtn.BackColor = accentColor == Theme.AccentOrange ? Color.FromArgb(80, 42, 18) :
                                          (accentColor == Theme.AccentGreen ? Color.FromArgb(28, 78, 48) : Color.FromArgb(32, 68, 98));
                }
            };
            actionBtn.MouseLeave += (s, e) => {
                if (!actionBtn.Text.StartsWith("✓") && !actionBtn.Text.StartsWith("✗"))
                {
                    actionBtn.BackColor = accentColor == Theme.AccentOrange ? Color.FromArgb(58, 30, 14) :
                                          (accentColor == Theme.AccentGreen ? Color.FromArgb(20, 56, 36) : Color.FromArgb(22, 50, 72));
                }
            };
            actionBtn.Click += onBtnClick;
            this.Controls.Add(actionBtn);

            this.Resize += (s, e) => RepositionButton();
            RepositionButton();
        }

        private void RepositionButton()
        {
            if (actionBtn != null)
            {
                actionBtn.Location = new Point(this.Width - actionBtn.Width - 22, (this.Height - actionBtn.Height) / 2);
            }
        }

        // Live Visual Feedback System for all action buttons!
        public void SetLoading(string loadingText)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetLoading(loadingText)));
                return;
            }
            actionBtn.Enabled = false;
            actionBtn.Text = loadingText;
        }

        public void SetResult(bool isSuccess, string resultText)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetResult(isSuccess, resultText)));
                return;
            }

            actionBtn.Enabled = true;
            actionBtn.Text = resultText;
            Color origBg = accentColor == Theme.AccentOrange ? Color.FromArgb(58, 30, 14) :
                           (accentColor == Theme.AccentGreen ? Color.FromArgb(20, 56, 36) : Color.FromArgb(22, 50, 72));
            Color origFg = accentColor;
            string origText = this.origBtnText;

            actionBtn.BackColor = isSuccess ? Color.FromArgb(20, 85, 45) : Color.FromArgb(95, 25, 25);
            actionBtn.ForeColor = Color.White;

            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 2600;
            t.Tick += (s, e) => {
                t.Stop();
                t.Dispose();
                actionBtn.Text = origText;
                actionBtn.BackColor = origBg;
                actionBtn.ForeColor = origFg;
            };
            t.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RepositionButton();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = Theme.CreateRoundedPath(rect, 10))
            {
                using (LinearGradientBrush lgb = new LinearGradientBrush(rect, Theme.BgCard, Theme.BgCardGradientEnd, 90f))
                    g.FillPath(lgb, path);

                using (Pen p = new Pen(Theme.BorderCard, 1))
                    g.DrawPath(p, path);
            }

            // Left Accent Notch
            using (GraphicsPath notch = Theme.CreateRoundedPath(new Rectangle(0, 20, 4, this.Height - 40), 2))
            using (SolidBrush ab = new SolidBrush(this.accentColor))
                g.FillPath(ab, notch);

            // Icon Box
            int iconBoxSize = 46;
            Rectangle iconBox = new Rectangle(20, (this.Height - iconBoxSize) / 2, iconBoxSize, iconBoxSize);
            using (GraphicsPath ip = Theme.CreateRoundedPath(iconBox, 8))
            {
                using (SolidBrush ib = new SolidBrush(Color.FromArgb(35, this.accentColor.R, this.accentColor.G, this.accentColor.B)))
                    g.FillPath(ib, ip);
                using (Pen iBorder = new Pen(Color.FromArgb(80, this.accentColor.R, this.accentColor.G, this.accentColor.B), 1))
                    g.DrawPath(iBorder, ip);
            }

            Rectangle innerIcon = new Rectangle(iconBox.X + 8, iconBox.Y + 8, iconBox.Width - 16, iconBox.Height - 16);
            if (this.vectorType == "target") VectorIcons.DrawTarget(g, innerIcon, this.accentColor);
            else if (this.vectorType == "sync") VectorIcons.DrawSync(g, innerIcon, this.accentColor);
            else if (this.vectorType == "globe") VectorIcons.DrawGlobe(g, innerIcon, this.accentColor);

            int textX = 82;
            int availTextW = (actionBtn != null ? actionBtn.Left : this.Width - 240) - textX - 16;
            if (availTextW < 100) availTextW = 100;

            using (Font titleFont = new Font("Segoe UI", 11F, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString(this.title, titleFont, tb, new PointF(textX, 18));
            }

            using (Font subFont = new Font("Segoe UI", 9F))
            using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString(this.description, subFont, sb, new RectangleF(textX, 44, availTextW, 44));
            }

            base.OnPaint(e);
        }
    }

    // --- 5.3 Modern Clean Card Container (Custom Background Support) ---
    public class ModernFluentCard : Panel
    {
        public Color AccentColor { get; set; }
        public Color CustomBgStart { get; set; }
        public Color CustomBgEnd { get; set; }
        public bool UseCustomBg { get; set; }

        public ModernFluentCard()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.Transparent;
            this.AccentColor = Theme.AccentCyan;
            this.UseCustomBg = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = Theme.CreateRoundedPath(rect, 10))
            {
                Color c1 = UseCustomBg ? CustomBgStart : Theme.BgCard;
                Color c2 = UseCustomBg ? CustomBgEnd : Theme.BgCardGradientEnd;

                using (LinearGradientBrush bgBrush = new LinearGradientBrush(rect, c1, c2, 90f))
                    g.FillPath(bgBrush, path);

                using (Pen borderPen = new Pen(Theme.BorderCard, 1))
                    g.DrawPath(borderPen, path);
            }
            base.OnPaint(e);
        }
    }

    // --- 5.4 Custom Dark Console (Clean Contrast & Zero White Scrollbars) ---
    public class CustomDarkConsole : Control
    {
        private class LogEntry
        {
            public string Time;
            public string Message;
            public Color Color;
        }

        private List<LogEntry> entries = new List<LogEntry>();
        private int scrollOffset = 0;

        public CustomDarkConsole()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Theme.BgInnerDark;
            this.Font = new Font("Consolas", 9.5F);

            this.MouseWheel += (s, e) => {
                if (e.Delta > 0) scrollOffset = Math.Max(0, scrollOffset - 2);
                else if (e.Delta < 0) scrollOffset = Math.Min(GetMaxScroll(), scrollOffset + 2);
                Invalidate();
            };
        }

        private int LineHeight { get { return 22; } }
        private int VisibleLines { get { return Math.Max(1, (this.Height - 14) / LineHeight); } }
        private int GetMaxScroll() { return Math.Max(0, entries.Count - VisibleLines); }

        public void AddLog(string msg, Color? col = null)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AddLog(msg, col)));
                return;
            }

            Color c = col ?? Theme.TextSecondary;
            if (msg.Contains("[✓]") || msg.Contains("SUCCESS")) c = Theme.AccentGreen;
            else if (msg.Contains("[✗]") || msg.Contains("ERROR")) c = Theme.AccentRed;
            else if (msg.Contains("[!]") || msg.Contains("Warning") || msg.Contains("Notice")) c = Theme.AccentYellow;
            else if (msg.Contains("[i]")) c = Theme.AccentCyan;

            entries.Add(new LogEntry() { Time = DateTime.Now.ToString("HH:mm:ss"), Message = msg, Color = c });
            scrollOffset = GetMaxScroll();
            Invalidate();
        }

        public void ClearLog()
        {
            entries.Clear();
            scrollOffset = 0;
            Invalidate();
        }

        public string GetPlainText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var e in entries)
                sb.AppendLine(string.Format("[{0}] {1}", e.Time, e.Message));
            return sb.ToString();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            using (GraphicsPath path = Theme.CreateRoundedPath(rect, 8))
            {
                using (SolidBrush bb = new SolidBrush(this.BackColor))
                    g.FillPath(bb, path);
                using (Pen bp = new Pen(Theme.BorderCard, 1))
                    g.DrawPath(bp, path);
            }

            int maxScroll = GetMaxScroll();
            int startIndex = Math.Min(scrollOffset, maxScroll);
            int y = 10;

            for (int i = startIndex; i < entries.Count && y < this.Height - LineHeight; i++)
            {
                LogEntry entry = entries[i];
                using (SolidBrush tb = new SolidBrush(Theme.TextMuted))
                    g.DrawString(string.Format("[{0}]", entry.Time), this.Font, tb, new PointF(12, y));

                using (SolidBrush mb = new SolidBrush(entry.Color))
                    g.DrawString(entry.Message, this.Font, mb, new PointF(96, y));

                y += LineHeight;
            }

            if (entries.Count > VisibleLines)
            {
                int trackH = this.Height - 16;
                int thumbH = Math.Max(24, (int)((float)VisibleLines / entries.Count * trackH));
                int thumbY = 8 + (int)((float)scrollOffset / maxScroll * (trackH - thumbH));
                Rectangle thumbRect = new Rectangle(this.Width - 10, thumbY, 6, thumbH);

                using (GraphicsPath tp = Theme.CreateRoundedPath(thumbRect, 3))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(70, 70, 85)))
                    g.FillPath(tb, tp);
            }
        }
    }

    // =========================================================================
    // SECTION 6: GUI - Main Window Application (Standard Native Windows Style)
    // =========================================================================
    public class MainForm : Form
    {
        private Label lblClockTime;
        private Label lblClockDate;
        private Label lblTz;
        private Label lblStatusMode;

        // Navigation Tabs
        private NavTabButton tabDashboard;
        private NavTabButton tabPeers;
        private NavTabButton tabLogs;

        // Tab Content Panels
        private Panel pnlDashboard;
        private Panel pnlPeersView;
        private Panel pnlLogsView;

        // Action Rows
        private SettingsActionRow rowRdr2;
        private SettingsActionRow rowSync;
        private SettingsActionRow rowGlobal;

        // Custom Time Picker Controls
        private DateTimePicker dtpCustomDate;
        private DateTimePicker dtpCustomTime;
        private Button btnNow;
        private Button btnApplyCustom;

        // Peers View Controls
        private Panel pnlPeersList;
        private Label lblPeerCount;
        private Button btnTestPeers;
        private TextBox txtCustomHost;
        private Button btnAddHost;

        // Logs View Controls
        private CustomDarkConsole consoleLog;

        private System.Windows.Forms.Timer clockTimer;

        public MainForm()
        {
            this.DoubleBuffered = true;
            InitializeComponent();
            LoadPeers();
            SwitchTab("dashboard");

            Log("WinTime - Windows Time & NTP Manager launched successfully.");
            Log("Elevation Status: " + (Win32Native.IsAdministrator() ? "Administrator (Full Clock Control)" : "Standard User (Limited - Clock Modification Disabled)"));
            if (!Win32Native.IsAdministrator())
            {
                Log("[!] Notice: Please run this tool as Administrator to change the system time.", Theme.AccentYellow);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnableNativeDarkMode();
        }

        private void EnableNativeDarkMode()
        {
            try
            {
                // DWM Native Dark Mode for standard Windows 10 & 11 title bar
                int dark = 1;
                Win32Native.DwmSetWindowAttribute(this.Handle, Win32Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                Win32Native.DwmSetWindowAttribute(this.Handle, Win32Native.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref dark, sizeof(int));

                // Windows 11 caption color: exact refined dark (#1E1E24)
                int captionColor = ColorTranslator.ToWin32(Color.FromArgb(30, 30, 36));
                Win32Native.DwmSetWindowAttribute(this.Handle, Win32Native.DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

                // Windows 11 title text color: pure crisp white
                int textColor = ColorTranslator.ToWin32(Color.White);
                Win32Native.DwmSetWindowAttribute(this.Handle, Win32Native.DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
            }
            catch { }
        }

        private void InitializeComponent()
        {
            // Standard Native Windows Form Properties
            this.Text = "WinTime v0.1.0 (Alpha) - Windows Time & NTP Manager";
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch {}
            this.ClientSize = new Size(1000, 740);
            this.MinimumSize = new Size(920, 700);
            this.MaximumSize = new Size(1600, 1100);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = Theme.BgApp;
            this.ForeColor = Theme.TextPrimary;
            this.Font = new Font("Segoe UI", 9F);

            // Navigation Bar (Docked Top, directly under native Windows title bar)
            Panel navBar = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(24, 24, 30)
            };

            // Allow dragging window from navigation bar as well
            navBar.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left && e.Clicks == 1)
                {
                    Win32Native.ReleaseCapture();
                    Win32Native.SendMessage(this.Handle, Win32Native.WM_NCLBUTTONDOWN, Win32Native.HTCAPTION, 0);
                }
            };

            tabDashboard = new NavTabButton("Time & Actions", "clock") { Location = new Point(16, 2) };
            tabDashboard.Click += (s, e) => SwitchTab("dashboard");

            tabPeers = new NavTabButton("NTP Servers", "server") { Location = new Point(205, 2) };
            tabPeers.Click += (s, e) => SwitchTab("peers");

            tabLogs = new NavTabButton("Diagnostics", "terminal") { Location = new Point(394, 2) };
            tabLogs.Click += (s, e) => SwitchTab("logs");

            navBar.Controls.AddRange(new Control[] { tabDashboard, tabPeers, tabLogs });
            this.Controls.Add(navBar);

            // Tab 1: Dashboard View
            BuildDashboardView();

            // Tab 2: Peers View
            BuildPeersView();

            // Tab 3: Logs View
            BuildLogsView();

            // Clock Timer
            clockTimer = new System.Windows.Forms.Timer() { Interval = 1000 };
            clockTimer.Tick += (s, e) => UpdateClock();
            clockTimer.Start();
            UpdateClock();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height - 48;
            if (pnlDashboard != null) pnlDashboard.Size = new Size(w, h);
            if (pnlPeersView != null) pnlPeersView.Size = new Size(w, h);
            if (pnlLogsView != null) pnlLogsView.Size = new Size(w, h);
        }

        private void SwitchTab(string tabName)
        {
            tabDashboard.IsSelected = (tabName == "dashboard");
            tabPeers.IsSelected = (tabName == "peers");
            tabLogs.IsSelected = (tabName == "logs");

            pnlDashboard.Visible = (tabName == "dashboard");
            pnlPeersView.Visible = (tabName == "peers");
            pnlLogsView.Visible = (tabName == "logs");
        }

        // =====================================================================
        // TAB 1: Dashboard View (Hero Clock + 3 Wide Action Cards + Custom Time)
        // =====================================================================
        private void BuildDashboardView()
        {
            pnlDashboard = new Panel()
            {
                Location = new Point(0, 48),
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 48),
                BackColor = Color.Transparent
            };

            int currentY = 16;
            int cardW = this.ClientSize.Width - 44;

            // Hero Clock Card
            ModernFluentCard cardHero = new ModernFluentCard()
            {
                Location = new Point(22, currentY),
                Size = new Size(cardW, 110),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblLiveDot = new Label()
            {
                Text = "● LIVE SYSTEM CLOCK",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Theme.AccentGreen,
                BackColor = Color.Transparent,
                Location = new Point(22, 14),
                AutoSize = true
            };

            lblClockTime = new Label()
            {
                Text = "--:--:--",
                Font = new Font("Consolas", 32F, FontStyle.Bold),
                ForeColor = Theme.AccentCyan,
                BackColor = Color.Transparent,
                Location = new Point(18, 34),
                AutoSize = true
            };

            lblClockDate = new Label()
            {
                Text = "...",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(300, 38),
                AutoSize = true
            };

            lblTz = new Label()
            {
                Text = "Time Zone: Detecting...",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(300, 66),
                AutoSize = true
            };

            bool admin = Win32Native.IsAdministrator();
            lblStatusMode = new Label()
            {
                Text = admin ? "🛡️ ADMIN ELEVATED" : "⚠️ STANDARD USER",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = admin ? Theme.AccentGreen : Theme.AccentRed,
                BackColor = admin ? Color.FromArgb(22, 60, 38) : Color.FromArgb(65, 20, 26),
                Location = new Point(cardW - 195, 20),
                Size = new Size(175, 34),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            cardHero.Controls.AddRange(new Control[] { lblLiveDot, lblClockTime, lblClockDate, lblTz, lblStatusMode });
            pnlDashboard.Controls.Add(cardHero);
            currentY += 124;

            // Action 1: RDR2 Fix
            rowRdr2 = new SettingsActionRow(
                "Red Dead Redemption 2 Game Fix",
                "Sets system clock to October 15, 2019 (21:31:00) to bypass launch and activation errors.",
                "target",
                Theme.AccentOrange,
                "⚡ Set RDR2 Time",
                (s, e) => ActionSetRdr2()
            ) { Location = new Point(22, currentY), Size = new Size(cardW, 92) };
            pnlDashboard.Controls.Add(rowRdr2);
            currentY += 104;

            // Action 2: Windows System Sync
            rowSync = new SettingsActionRow(
                "Windows Time Service Resync (w32tm)",
                "Synchronizes your system clock from Windows registered peers using native w32tm /resync.",
                "sync",
                Theme.AccentGreen,
                "🔄 Sync System",
                (s, e) => ActionSyncSystemPeers()
            ) { Location = new Point(22, currentY), Size = new Size(cardW, 92) };
            pnlDashboard.Controls.Add(rowSync);
            currentY += 104;

            // Action 3: Global NTP Sync
            rowGlobal = new SettingsActionRow(
                "Global International NTP Sync (Direct Atomic Clock)",
                "Directly queries Cloudflare & Google Tier-1 NTP servers over port 123 / HTTPS (bypasses .ir).",
                "globe",
                Theme.AccentCyan,
                "🌐 Sync Global NTP",
                (s, e) => ActionSyncGlobal()
            ) { Location = new Point(22, currentY), Size = new Size(cardW, 92) };
            pnlDashboard.Controls.Add(rowGlobal);
            currentY += 104;

            // Custom Date & Time Adjustment Card (Spacious, Elegant Charcoal-Purple Surface, Not Black!)
            ModernFluentCard cardCustom = new ModernFluentCard()
            {
                Location = new Point(22, currentY),
                Size = new Size(cardW, 144),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseCustomBg = true,
                CustomBgStart = Color.FromArgb(46, 40, 58),
                CustomBgEnd = Color.FromArgb(36, 32, 46)
            };

            Label lblCustomTitle = new Label()
            {
                Text = "CUSTOM DATE & TIME ADJUSTMENT",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(192, 132, 252),
                BackColor = Color.Transparent,
                Location = new Point(20, 14),
                AutoSize = true
            };

            Label lblCustomSub = new Label()
            {
                Text = "Pick any custom date and time to apply to Windows:",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(215, 210, 230),
                BackColor = Color.Transparent,
                Location = new Point(20, 38),
                AutoSize = true
            };

            // Spacious Inputs Row (Proper Clearances, Zero Overlaps)
            Label lblDate = new Label() { Text = "Date:", ForeColor = Color.FromArgb(225, 220, 240), Location = new Point(20, 78), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), BackColor = Color.Transparent };
            dtpCustomDate = new DateTimePicker()
            {
                Format = DateTimePickerFormat.Short,
                Location = new Point(70, 74),
                Size = new Size(150, 32),
                Font = new Font("Segoe UI", 10F)
            };

            Label lblTime = new Label() { Text = "Time:", ForeColor = Color.FromArgb(225, 220, 240), Location = new Point(245, 78), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), BackColor = Color.Transparent };
            dtpCustomTime = new DateTimePicker()
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Location = new Point(298, 74),
                Size = new Size(140, 32),
                Font = new Font("Segoe UI", 10F)
            };

            btnNow = new Button()
            {
                Text = "🕒 Current Time",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F),
                Size = new Size(135, 36),
                Location = new Point(460, 72),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 52, 75),
                ForeColor = Color.FromArgb(240, 235, 255),
                Cursor = Cursors.Hand
            };
            btnNow.FlatAppearance.BorderColor = Color.FromArgb(90, 80, 110);
            btnNow.MouseEnter += (s, e) => btnNow.BackColor = Color.FromArgb(75, 65, 95);
            btnNow.MouseLeave += (s, e) => btnNow.BackColor = Color.FromArgb(60, 52, 75);
            btnNow.Click += (s, e) => {
                dtpCustomDate.Value = DateTime.Now;
                dtpCustomTime.Value = DateTime.Now;
                Log("[i] Reset custom pickers to current system time.");
                btnNow.Text = "✓ Reset to Now!";
                btnNow.BackColor = Color.FromArgb(75, 65, 100);
                System.Windows.Forms.Timer tNow = new System.Windows.Forms.Timer();
                tNow.Interval = 1500;
                tNow.Tick += (ts, te) => {
                    tNow.Stop();
                    tNow.Dispose();
                    btnNow.Text = "🕒 Current Time";
                    btnNow.BackColor = Color.FromArgb(60, 52, 75);
                };
                tNow.Start();
            };

            btnApplyCustom = new Button()
            {
                Text = "✓ Apply Custom Time",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(215, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(22, 60, 38),
                ForeColor = Theme.AccentGreen,
                Cursor = Cursors.Hand
            };
            btnApplyCustom.FlatAppearance.BorderColor = Theme.AccentGreen;
            btnApplyCustom.MouseEnter += (s, e) => {
                if (!btnApplyCustom.Text.StartsWith("✓ Time") && !btnApplyCustom.Text.StartsWith("✗"))
                    btnApplyCustom.BackColor = Color.FromArgb(32, 85, 54);
            };
            btnApplyCustom.MouseLeave += (s, e) => {
                if (!btnApplyCustom.Text.StartsWith("✓ Time") && !btnApplyCustom.Text.StartsWith("✗"))
                    btnApplyCustom.BackColor = Color.FromArgb(22, 60, 38);
            };
            btnApplyCustom.Click += (s, e) => ActionSetCustom();

            Action reposApply = () => {
                btnApplyCustom.Location = new Point(cardCustom.Width - btnApplyCustom.Width - 22, 71);
            };
            cardCustom.Resize += (s, e) => reposApply();
            reposApply();

            cardCustom.Controls.AddRange(new Control[] { lblCustomTitle, lblCustomSub, lblDate, dtpCustomDate, lblTime, dtpCustomTime, btnNow, btnApplyCustom });
            pnlDashboard.Controls.Add(cardCustom);

            this.Controls.Add(pnlDashboard);
        }

        // =====================================================================
        // TAB 2: NTP Servers View (Validated, Scrollable, Managed Peer List)
        // =====================================================================
        private void BuildPeersView()
        {
            pnlPeersView = new Panel()
            {
                Location = new Point(0, 48),
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 48),
                BackColor = Color.Transparent,
                Visible = false
            };

            int currentY = 16;
            int cardW = this.ClientSize.Width - 44;

            // Info Notice Card
            ModernFluentCard cardNotice = new ModernFluentCard()
            {
                Location = new Point(22, currentY),
                Size = new Size(cardW, 64),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblNotice = new Label()
            {
                Text = "💡 Windows Time Service (w32time) Peer Management\n" +
                       "Configured servers below are stored directly in your Windows registry. Having both Global and Iran (.ir) servers guarantees clock sync during ISP restrictions.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.AccentCyan,
                BackColor = Color.Transparent,
                Location = new Point(16, 12),
                AutoSize = true
            };
            cardNotice.Controls.Add(lblNotice);
            pnlPeersView.Controls.Add(cardNotice);
            currentY += 76;

            // Presets Bar
            FlowLayoutPanel pnlPresets = new FlowLayoutPanel()
            {
                Location = new Point(22, currentY),
                Size = new Size(cardW, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Button btnAll5 = CreatePresetButton("⭐ All 5 (Iran-Optimized)", Color.FromArgb(50, 40, 20), Theme.AccentYellow, (s, e) => {
                List<string> five = new List<string>() { "time.windows.com,0x8", "pool.ntp.org,0x8", "time.cloudflare.com,0x8", "time.digiboy.ir,0x8", "ntp.iranet.ir,0x8" };
                ApplyPeerList(five);
            });

            Button btnGlobal = CreatePresetButton("🌐 Global Only (3 Servers)", Color.FromArgb(22, 50, 72), Theme.AccentCyan, (s, e) => {
                List<string> three = new List<string>() { "time.windows.com,0x8", "pool.ntp.org,0x8", "time.cloudflare.com,0x8" };
                ApplyPeerList(three);
            });

            Button btnDefault = CreatePresetButton("🪟 Windows Default Only", Theme.BgControl, Theme.TextSecondary, (s, e) => {
                List<string> one = new List<string>() { "time.windows.com,0x8" };
                ApplyPeerList(one);
            });

            pnlPresets.Controls.AddRange(new Control[] { btnAll5, btnGlobal, btnDefault });
            pnlPeersView.Controls.Add(pnlPresets);
            currentY += 50;

            // Servers List Card (Enlarged with AutoScroll support)
            ModernFluentCard cardServers = new ModernFluentCard()
            {
                Location = new Point(22, currentY),
                Size = new Size(cardW, 320),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblPeerCount = new Label()
            {
                Text = "0 peers active",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(20, 16),
                AutoSize = true
            };

            btnTestPeers = new Button()
            {
                Text = "⚡ Test Latency for All Peers",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size = new Size(225, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(22, 50, 72),
                ForeColor = Theme.AccentCyan,
                Cursor = Cursors.Hand
            };
            btnTestPeers.FlatAppearance.BorderColor = Theme.AccentCyan;
            btnTestPeers.MouseEnter += (s, e) => btnTestPeers.BackColor = Color.FromArgb(32, 68, 98);
            btnTestPeers.MouseLeave += (s, e) => btnTestPeers.BackColor = Color.FromArgb(22, 50, 72);
            btnTestPeers.Click += (s, e) => ActionTestPeers();

            Action reposTestPeers = () => {
                btnTestPeers.Location = new Point(cardServers.Width - btnTestPeers.Width - 20, 14);
            };
            cardServers.Resize += (s, e) => reposTestPeers();
            reposTestPeers();

            cardServers.Controls.AddRange(new Control[] { lblPeerCount, btnTestPeers });

            // Scrollable Peer List Panel
            pnlPeersList = new Panel()
            {
                Location = new Point(16, 56),
                Size = new Size(cardW - 32, 248),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.BgInnerDark,
                AutoScroll = true
            };
            cardServers.Controls.Add(pnlPeersList);
            pnlPeersView.Controls.Add(cardServers);
            currentY += 336;

            // Add Custom Server Card
            ModernFluentCard cardAdd = new ModernFluentCard()
            {
                Location = new Point(22, currentY),
                Size = new Size(cardW, 76),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblAddHost = new Label() { Text = "Add NTP Server:", Location = new Point(18, 26), AutoSize = true, ForeColor = Theme.TextPrimary, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            txtCustomHost = new TextBox() { Location = new Point(155, 22), Size = new Size(290, 30), BackColor = Theme.BgInnerDark, ForeColor = Theme.TextPrimary, Font = new Font("Consolas", 10F), BorderStyle = BorderStyle.FixedSingle };
            txtCustomHost.Text = "time.google.com";

            btnAddHost = new Button()
            {
                Text = "+ Add & Test Server",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(185, 36),
                Location = new Point(460, 19),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 56, 36),
                ForeColor = Theme.AccentGreen,
                Cursor = Cursors.Hand
            };
            btnAddHost.FlatAppearance.BorderColor = Theme.AccentGreen;
            btnAddHost.MouseEnter += (s, e) => btnAddHost.BackColor = Color.FromArgb(28, 78, 48);
            btnAddHost.MouseLeave += (s, e) => btnAddHost.BackColor = Color.FromArgb(20, 56, 36);
            btnAddHost.Click += (s, e) => ValidateAndAddCustomPeer();

            cardAdd.Controls.AddRange(new Control[] { lblAddHost, txtCustomHost, btnAddHost });
            pnlPeersView.Controls.Add(cardAdd);

            this.Controls.Add(pnlPeersView);
        }

        private void FlashAddHostButton(string text, Color bg, int durationMs = 2500)
        {
            btnAddHost.Enabled = false;
            btnAddHost.Text = text;
            btnAddHost.BackColor = bg;
            System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
            tm.Interval = durationMs;
            tm.Tick += (s, e) => {
                tm.Stop();
                tm.Dispose();
                btnAddHost.Enabled = true;
                btnAddHost.Text = "+ Add & Test Server";
                btnAddHost.BackColor = Color.FromArgb(20, 56, 36);
            };
            tm.Start();
        }

        // --- Add Server Validation & Testing Logic with Interactive Feedback ---
        private void ValidateAndAddCustomPeer()
        {
            string raw = txtCustomHost.Text.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                Log("[!] Please enter a server hostname or IP address.", Theme.AccentYellow);
                FlashAddHostButton("✗ Enter Hostname or IP", Color.FromArgb(95, 25, 25));
                return;
            }

            // 1. Strict Validation: Non-ASCII characters (e.g. Persian/Arabic) are rejected immediately
            foreach (char c in raw)
            {
                if (c > 127 || char.IsWhiteSpace(c) || c == ',' || c == ';' || c == '/' || c == '\\' || c == '?' || c == '*' || c == '!')
                {
                    Log("[✗] VALIDATION ERROR: Server name contains invalid characters: '" + raw + "'. Only English letters, numbers, hyphens, and dots are allowed.", Theme.AccentRed);
                    FlashAddHostButton("✗ English Only (No Persian)", Color.FromArgb(95, 25, 25));
                    return;
                }
            }

            // 2. Format validation: must be a valid IP or a domain with at least one dot
            IPAddress dummyIp;
            bool isIp = IPAddress.TryParse(raw, out dummyIp);
            bool isDomain = raw.Contains(".") && !raw.StartsWith(".") && !raw.EndsWith(".") && !raw.Contains("..");

            if (!isIp && !isDomain && !raw.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                Log("[✗] VALIDATION ERROR: '" + raw + "' is not a valid domain or IP address format.", Theme.AccentRed);
                FlashAddHostButton("✗ Invalid Address Format", Color.FromArgb(95, 25, 25));
                return;
            }

            // 3. DNS Resolution and Connectivity Test in Background
            btnAddHost.Enabled = false;
            btnAddHost.Text = "⏳ Testing DNS & NTP...";
            btnAddHost.BackColor = Color.FromArgb(35, 45, 60);
            Log("Testing server reachability: " + raw + " ...");

            ThreadPool.QueueUserWorkItem((state) => {
                try
                {
                    // Step A: DNS Resolution Check
                    IPAddress[] addresses = null;
                    try
                    {
                        addresses = Dns.GetHostAddresses(raw);
                    }
                    catch
                    {
                        this.Invoke(new Action(() => {
                            FlashAddHostButton("✗ Host Not Found (DNS Error)", Color.FromArgb(95, 25, 25), 3000);
                            Log("[✗] DNS RESOLUTION FAILED: Hostname '" + raw + "' does not exist or DNS could not resolve it.", Theme.AccentRed);
                        }));
                        return;
                    }

                    if (addresses == null || addresses.Length == 0)
                    {
                        this.Invoke(new Action(() => {
                            FlashAddHostButton("✗ No IP Found", Color.FromArgb(95, 25, 25));
                            Log("[✗] DNS ERROR: No IP address found for '" + raw + "'.", Theme.AccentRed);
                        }));
                        return;
                    }

                    string resolvedIp = addresses[0].ToString();
                    Log("  Resolved " + raw + " -> " + resolvedIp);

                    // Step B: Query NTP Port 123
                    int lat;
                    DateTime utc;
                    bool ntpOk = TimeServiceHelper.QueryNtp(raw, out lat, out utc);

                    this.Invoke(new Action(() => {
                        if (ntpOk)
                        {
                            Log(string.Format("[✓] NTP Test PASSED: {0} replied in {1}ms!", raw, lat), Theme.AccentGreen);
                        }
                        else
                        {
                            Log("[!] Notice: DNS resolved, but UDP 123 timed out (may be blocked by your firewall/ISP).", Theme.AccentYellow);
                        }

                        // Add to current peer list, filtering out any corrupt entries
                        var existingPeers = TimeServiceHelper.GetSystemPeers();
                        List<string> updatedList = new List<string>();
                        bool alreadyExists = false;

                        foreach (var p in existingPeers)
                        {
                            if (p.Key.Contains("?") || string.IsNullOrEmpty(p.Key)) continue;
                            if (p.Key.Equals(raw, StringComparison.OrdinalIgnoreCase))
                                alreadyExists = true;
                            updatedList.Add(p.Key + ",0x8");
                        }

                        if (alreadyExists)
                        {
                            FlashAddHostButton("✗ Already in List", Color.FromArgb(95, 25, 25));
                            Log("[!] Server '" + raw + "' is already in your configured peers list.", Theme.AccentYellow);
                        }
                        else
                        {
                            updatedList.Add(raw + ",0x8");
                            ApplyPeerList(updatedList);
                            txtCustomHost.Clear();
                            FlashAddHostButton("✓ Server Added!", Color.FromArgb(20, 85, 45));
                        }
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => {
                        FlashAddHostButton("✗ Error: " + ex.Message, Color.FromArgb(95, 25, 25));
                        Log("[✗] Error validating server: " + ex.Message, Theme.AccentRed);
                    }));
                }
            });
        }

        private Button CreatePresetButton(string text, Color bg, Color fg, EventHandler onClick)
        {
            Button btn = new Button()
            {
                Text = text,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size = new Size(215, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = fg;
            btn.Click += (s, e) => {
                onClick(s, e);
                string orig = btn.Text;
                btn.Text = "✓ Applied!";
                btn.BackColor = Color.FromArgb(20, 85, 45);
                btn.ForeColor = Color.White;
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 2000;
                t.Tick += (ts, te) => {
                    t.Stop();
                    t.Dispose();
                    btn.Text = orig;
                    btn.BackColor = bg;
                    btn.ForeColor = fg;
                };
                t.Start();
            };
            return btn;
        }

        private void ApplyPeerList(List<string> peers)
        {
            string msg;
            bool ok = TimeServiceHelper.SetSystemPeers(peers, out msg);
            if (ok)
            {
                Log("[✓] " + msg);
                LoadPeers();
            }
            else
            {
                Log("[✗] Error applying peers: " + msg, Theme.AccentRed);
            }
        }

        // =====================================================================
        // TAB 3: Diagnostics & Logs View (Full-Height Clean Console)
        // =====================================================================
        private void BuildLogsView()
        {
            pnlLogsView = new Panel()
            {
                Location = new Point(0, 48),
                Size = new Size(this.ClientSize.Width, this.ClientSize.Height - 48),
                BackColor = Color.Transparent,
                Visible = false
            };

            int currentY = 16;
            int cardW = this.ClientSize.Width - 44;
            int cardH = this.ClientSize.Height - 48 - 32;

            ModernFluentCard cardLog = new ModernFluentCard()
            {
                Location = new Point(22, currentY),
                Size = new Size(cardW, cardH),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblLogTitle = new Label()
            {
                Text = "DIAGNOSTICS & SYSTEM ACTIVITY CONSOLE",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.AccentCyan,
                BackColor = Color.Transparent,
                Location = new Point(18, 16),
                AutoSize = true
            };

            Button btnCopy = new Button()
            {
                Text = "Copy Full Log",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F),
                Size = new Size(130, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgControl,
                ForeColor = Theme.TextPrimary,
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderColor = Theme.BorderCard;
            btnCopy.MouseEnter += (s, e) => btnCopy.BackColor = Theme.BgControlHover;
            btnCopy.MouseLeave += (s, e) => btnCopy.BackColor = Theme.BgControl;
            btnCopy.Click += (s, e) => {
                Clipboard.SetText(consoleLog.GetPlainText());
                Log("[i] Full log copied to clipboard.");
                btnCopy.Text = "✓ Copied!";
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 2000;
                t.Tick += (ts, te) => { t.Stop(); t.Dispose(); btnCopy.Text = "Copy Full Log"; };
                t.Start();
            };

            Button btnClear = new Button()
            {
                Text = "Clear Console",
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F),
                Size = new Size(110, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgControl,
                ForeColor = Theme.TextSecondary,
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderColor = Theme.BorderCard;
            btnClear.MouseEnter += (s, e) => btnClear.BackColor = Theme.BgControlHover;
            btnClear.MouseLeave += (s, e) => btnClear.BackColor = Theme.BgControl;
            btnClear.Click += (s, e) => {
                consoleLog.ClearLog();
                btnClear.Text = "✓ Cleared!";
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 1500;
                t.Tick += (ts, te) => { t.Stop(); t.Dispose(); btnClear.Text = "Clear Console"; };
                t.Start();
            };

            Action reposLogBtns = () => {
                btnClear.Location = new Point(cardLog.Width - btnClear.Width - 20, 12);
                btnCopy.Location = new Point(btnClear.Left - btnCopy.Width - 10, 12);
            };
            cardLog.Resize += (s, e) => reposLogBtns();
            reposLogBtns();

            cardLog.Controls.AddRange(new Control[] { lblLogTitle, btnCopy, btnClear });

            consoleLog = new CustomDarkConsole()
            {
                Location = new Point(16, 52),
                Size = new Size(cardW - 32, cardH - 66),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            cardLog.Controls.Add(consoleLog);

            pnlLogsView.Controls.Add(cardLog);
            this.Controls.Add(pnlLogsView);
        }

        private void UpdateClock()
        {
            DateTime n = DateTime.Now;
            lblClockTime.Text = n.ToString("HH:mm:ss");
            lblClockDate.Text = n.ToString("dddd, MMMM dd, yyyy");
            lblTz.Text = "Time Zone: " + TimeZoneInfo.Local.DisplayName;
        }

        private void LoadPeers()
        {
            pnlPeersList.Controls.Clear();
            var peers = TimeServiceHelper.GetSystemPeers();
            lblPeerCount.Text = string.Format("{0} peer(s) configured in Windows registry", peers.Count);

            if (peers.Count == 0)
            {
                Label lblEmpty = new Label()
                {
                    Text = "No NTP peers configured. Use presets above or add one below.",
                    ForeColor = Theme.TextMuted,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Italic)
                };
                pnlPeersList.Controls.Add(lblEmpty);
                return;
            }

            int idx = 1;
            int rowY = 6;
            // Always subtract 28px for scrollbar clearance so buttons NEVER collide with scrollbar!
            int rowW = pnlPeersList.ClientSize.Width - 28;
            if (rowW < 520) rowW = 520;

            foreach (var p in peers)
            {
                string currentHost = p.Key;
                bool isCorrupt = currentHost.Contains("?");

                Panel row = new Panel()
                {
                    Location = new Point(6, rowY),
                    Size = new Size(rowW, 40),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = idx % 2 == 0 ? Color.FromArgb(32, 32, 40) : Color.FromArgb(24, 24, 30)
                };

                Label lblIdx = new Label()
                {
                    Text = string.Format("#{0}", idx),
                    ForeColor = Theme.TextMuted,
                    BackColor = Color.Transparent,
                    Location = new Point(10, 11),
                    AutoSize = true,
                    Font = new Font("Consolas", 9F, FontStyle.Bold)
                };

                Label lblHost = new Label()
                {
                    Text = currentHost,
                    ForeColor = isCorrupt ? Theme.AccentRed : Theme.TextPrimary,
                    BackColor = Color.Transparent,
                    Location = new Point(48, 10),
                    AutoSize = true,
                    Font = new Font("Consolas", 10.5F, FontStyle.Bold)
                };

                bool isIran = currentHost.EndsWith(".ir");
                string tagText = isCorrupt ? "CORRUPT / INVALID" : (isIran ? "IRAN LOCAL SERVER" : "GLOBAL TIER-1 NTP");
                Color tagBg = isCorrupt ? Color.FromArgb(65, 20, 20) : (isIran ? Color.FromArgb(58, 30, 14) : Color.FromArgb(22, 45, 65));
                Color tagFg = isCorrupt ? Color.FromArgb(255, 120, 120) : (isIran ? Color.FromArgb(253, 186, 116) : Color.FromArgb(125, 211, 252));

                Label lblTag = new Label()
                {
                    Text = tagText,
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    ForeColor = tagFg,
                    BackColor = tagBg,
                    Location = new Point(310, 9),
                    Size = new Size(140, 22),
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblFlag = new Label()
                {
                    Text = string.IsNullOrEmpty(p.Value) ? "Mode: Default" : "Mode: " + p.Value,
                    ForeColor = Theme.TextMuted,
                    BackColor = Color.Transparent,
                    Location = new Point(465, 12),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8F)
                };

                // Delete Button: Pinned to the right of the row
                Button btnDelete = new Button()
                {
                    Text = "✕",
                    UseMnemonic = false,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Size = new Size(34, 28),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 24, 24),
                    ForeColor = Color.FromArgb(255, 140, 140),
                    Cursor = Cursors.Hand
                };
                btnDelete.Location = new Point(row.Width - btnDelete.Width - 10, 6);
                btnDelete.FlatAppearance.BorderSize = 1;
                btnDelete.FlatAppearance.BorderColor = Color.FromArgb(120, 45, 45);
                btnDelete.MouseEnter += (s, e) => { if (btnDelete.Enabled) btnDelete.BackColor = Color.FromArgb(90, 32, 32); };
                btnDelete.MouseLeave += (s, e) => { if (btnDelete.Enabled) btnDelete.BackColor = Color.FromArgb(60, 24, 24); };

                // Status Label: Positioned to the left of the delete button with 160px width
                Label lblStatus = new Label()
                {
                    Text = isCorrupt ? "● Invalid Host" : "○ Ready to Test",
                    Name = "status_" + idx,
                    ForeColor = isCorrupt ? Theme.AccentRed : Theme.TextSecondary,
                    BackColor = Color.Transparent,
                    Size = new Size(160, 22),
                    TextAlign = ContentAlignment.MiddleRight,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Font = new Font("Segoe UI", 9F)
                };
                lblStatus.Location = new Point(btnDelete.Left - lblStatus.Width - 12, 9);

                string hostCapture = currentHost;
                Panel rowCapture = row;
                Button btnDeleteCapture = btnDelete;
                Label lblStatusCapture = lblStatus;
                btnDelete.Click += (s, e) => RemovePeer(hostCapture, rowCapture, btnDeleteCapture, lblStatusCapture);

                row.Controls.AddRange(new Control[] { lblIdx, lblHost, lblTag, lblFlag, lblStatus, btnDelete });
                pnlPeersList.Controls.Add(row);
                rowY += 44;
                idx++;
            }
        }

        private void RemovePeer(string hostToRemove, Panel row, Button btnDelete, Label lblStatus)
        {
            var existing = TimeServiceHelper.GetSystemPeers();
            List<string> newList = new List<string>();
            foreach (var p in existing)
            {
                if (!p.Key.Equals(hostToRemove, StringComparison.OrdinalIgnoreCase))
                {
                    newList.Add(p.Key + (string.IsNullOrEmpty(p.Value) ? ",0x8" : "," + p.Value));
                }
            }

            if (newList.Count == 0)
            {
                btnDelete.Text = "✗";
                btnDelete.BackColor = Color.FromArgb(120, 25, 25);
                btnDelete.ForeColor = Color.White;
                lblStatus.Text = "● Cannot remove last peer";
                lblStatus.ForeColor = Theme.AccentRed;
                Log("[!] Cannot remove '" + hostToRemove + "': Windows requires at least one active NTP peer.", Theme.AccentYellow);

                System.Windows.Forms.Timer resetTimer = new System.Windows.Forms.Timer();
                resetTimer.Interval = 2500;
                resetTimer.Tick += (s, e) => {
                    resetTimer.Stop();
                    resetTimer.Dispose();
                    btnDelete.Text = "✕";
                    btnDelete.BackColor = Color.FromArgb(60, 24, 24);
                    btnDelete.ForeColor = Color.FromArgb(255, 140, 140);
                    lblStatus.Text = "○ Ready to Test";
                    lblStatus.ForeColor = Theme.TextSecondary;
                };
                resetTimer.Start();
                return;
            }

            btnDelete.Enabled = false;
            btnDelete.Text = "✓";
            btnDelete.BackColor = Color.FromArgb(20, 85, 45);
            btnDelete.ForeColor = Color.FromArgb(180, 255, 200);
            lblStatus.Text = "✓ Removed!";
            lblStatus.ForeColor = Theme.AccentGreen;
            row.BackColor = Color.FromArgb(45, 22, 22);

            Log("[✓] Removed peer '" + hostToRemove + "' from Windows registry.");

            System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
            tm.Interval = 350;
            tm.Tick += (s, e) => {
                tm.Stop();
                tm.Dispose();
                ApplyPeerList(newList);
            };
            tm.Start();
        }

        private void Log(string message, Color? color = null)
        {
            if (consoleLog != null)
                consoleLog.AddLog(message, color);
        }

        // =====================================================================
        // Action Handlers with Instant Interactive Visual Feedback!
        // =====================================================================
        private void ActionSetRdr2()
        {
            rowRdr2.SetLoading("⏳ Setting Clock...");
            ThreadPool.QueueUserWorkItem((state) => {
                Log("Applying RDR2 Preset time (2019-10-15 21:31:00)...");
                bool ok = Win32Native.SetSystemClock(2019, 10, 15, 21, 31, 0);
                if (ok)
                {
                    Log("[✓] SUCCESS: RDR2 fixed time (2019-10-15 21:31:00) applied successfully!");
                    rowRdr2.SetResult(true, "✓ RDR2 Time Set!");
                }
                else
                {
                    Log("[✗] ERROR: Failed to apply RDR2 time. Please run with Administrator privileges.");
                    rowRdr2.SetResult(false, "✗ Need Admin!");
                }
            });
        }

        private void ActionSetCustom()
        {
            btnApplyCustom.Enabled = false;
            btnApplyCustom.Text = "⏳ Applying...";
            DateTime d = dtpCustomDate.Value;
            DateTime t = dtpCustomTime.Value;

            ThreadPool.QueueUserWorkItem((state) => {
                string target = string.Format("{0:0000}-{1:00}-{2:00} {3:00}:{4:00}:{5:00}", d.Year, d.Month, d.Day, t.Hour, t.Minute, t.Second);
                Log("Applying custom time: " + target);
                bool ok = Win32Native.SetSystemClock(d.Year, d.Month, d.Day, t.Hour, t.Minute, t.Second);
                this.Invoke(new Action(() => {
                    btnApplyCustom.Enabled = true;
                    if (ok)
                    {
                        Log("[✓] SUCCESS: System time set to " + target);
                        btnApplyCustom.Text = "✓ Time Applied!";
                        btnApplyCustom.BackColor = Color.FromArgb(20, 85, 45);
                    }
                    else
                    {
                        Log("[✗] ERROR: Failed to set custom time. Run as Administrator.");
                        btnApplyCustom.Text = "✗ Failed (Need Admin)!";
                        btnApplyCustom.BackColor = Color.FromArgb(95, 25, 25);
                    }

                    System.Windows.Forms.Timer tmr = new System.Windows.Forms.Timer();
                    tmr.Interval = 2500;
                    tmr.Tick += (ts, te) => {
                        tmr.Stop();
                        tmr.Dispose();
                        btnApplyCustom.Text = "✓ Apply Custom Time";
                        btnApplyCustom.BackColor = Color.FromArgb(22, 60, 38);
                    };
                    tmr.Start();
                }));
            });
        }

        private void ActionSyncSystemPeers()
        {
            rowSync.SetLoading("⏳ Resyncing w32tm...");
            ThreadPool.QueueUserWorkItem((state) => {
                Log("Triggering Windows Time service synchronization (w32tm /resync)...");
                TimeServiceHelper.RunHiddenProcess("net.exe", "start w32time");

                Process p = Process.Start(new ProcessStartInfo("w32tm.exe", "/resync /force")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                string output = p != null ? p.StandardOutput.ReadToEnd() : "";
                if (p != null) p.WaitForExit(6000);

                if (p != null && (p.ExitCode == 0 || output.Contains("completed successfully")))
                {
                    Log("[✓] SUCCESS: Time synchronized via Windows Time service (w32tm)!");
                    rowSync.SetResult(true, "✓ Synchronized!");
                    return;
                }

                Log("[!] w32tm returned notice. Falling back to direct UDP NTP sync against registered peers...");
                var peers = TimeServiceHelper.GetSystemPeers();
                bool synced = false;
                foreach (var peer in peers)
                {
                    if (peer.Key.Contains("?")) continue;

                    Log("  Querying peer: " + peer.Key + " ...");
                    int lat;
                    DateTime utc;
                    if (TimeServiceHelper.QueryNtp(peer.Key, out lat, out utc))
                    {
                        DateTime local = utc.ToLocalTime();
                        Win32Native.SetSystemClock(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second);
                        Log(string.Format("[✓] SUCCESS: Clock synchronized from {0} ({1}ms)", peer.Key, lat));
                        synced = true;
                        break;
                    }
                    else
                    {
                        Log("  " + peer.Key + " unreachable via UDP 123.");
                    }
                }

                if (synced)
                {
                    rowSync.SetResult(true, "✓ Synced (NTP)!");
                }
                else
                {
                    Log("[✗] ERROR: Could not sync from configured peers. Check firewall or ISP filtering.");
                    rowSync.SetResult(false, "✗ Sync Failed!");
                }
            });
        }

        private void ActionSyncGlobal()
        {
            rowGlobal.SetLoading("⏳ Querying Global NTP...");
            ThreadPool.QueueUserWorkItem((state) => {
                Log("Starting Global NTP synchronization (International Servers, No .ir)...");
                string[] globalServers = new string[] {
                    "time.cloudflare.com",
                    "time.google.com",
                    "pool.ntp.org",
                    "time.windows.com",
                    "time.aws.com"
                };

                bool synced = false;
                int bestLat = 0;
                foreach (string srv in globalServers)
                {
                    Log("  Querying Global NTP: " + srv + " ...");
                    int lat;
                    DateTime utc;
                    if (TimeServiceHelper.QueryNtp(srv, out lat, out utc))
                    {
                        DateTime local = utc.ToLocalTime();
                        Win32Native.SetSystemClock(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second);
                        Log(string.Format("[✓] SUCCESS: Clock synchronized from {0} ({1}ms - Tier 1 NTP)", srv, lat));
                        synced = true;
                        bestLat = lat;
                        break;
                    }
                }

                if (synced)
                {
                    rowGlobal.SetResult(true, string.Format("✓ Synced ({0}ms)!", bestLat));
                    return;
                }

                if (!synced)
                {
                    Log("[!] UDP Port 123 appears blocked. Initiating HTTPS Atomic Time Fallback (Port 443)...");
                    KeyValuePair<string, string>[] httpsTargets = new KeyValuePair<string, string>[] {
                        new KeyValuePair<string, string>("https://www.google.com", "Google Global HTTPS"),
                        new KeyValuePair<string, string>("https://cloudflare.com", "Cloudflare Edge HTTPS"),
                        new KeyValuePair<string, string>("https://www.microsoft.com", "Microsoft Global HTTPS")
                    };

                    foreach (var tgt in httpsTargets)
                    {
                        Log("  Connecting to: " + tgt.Value + " ...");
                        int lat;
                        DateTime utc;
                        if (TimeServiceHelper.QueryHttpsTime(tgt.Key, out lat, out utc))
                        {
                            DateTime local = utc.ToLocalTime();
                            Win32Native.SetSystemClock(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second);
                            Log(string.Format("[✓] SUCCESS: Clock synchronized from {0} ({1}ms - HTTPS Port 443)", tgt.Value, lat));
                            synced = true;
                            rowGlobal.SetResult(true, "✓ Synced (HTTPS 443)!");
                            break;
                        }
                    }
                }

                if (!synced)
                {
                    Log("[✗] ERROR: Failed to reach international time servers.");
                    rowGlobal.SetResult(false, "✗ Sync Failed!");
                }
            });
        }

        private void ActionTestPeers()
        {
            btnTestPeers.Enabled = false;
            btnTestPeers.Text = "⏳ Testing Latencies...";

            ThreadPool.QueueUserWorkItem((state) => {
                try
                {
                    Log("Testing NTP connectivity and latency for configured peers...");
                    var peers = TimeServiceHelper.GetSystemPeers();
                    int idx = 1;
                    foreach (var p in peers)
                    {
                        string name = "status_" + idx;

                        if (p.Key.Contains("?"))
                        {
                            this.Invoke(new Action(() => {
                                Control[] matches = pnlPeersList.Controls.Find(name, true);
                                if (matches.Length > 0)
                                {
                                    matches[0].Text = "● Corrupted";
                                    matches[0].ForeColor = Theme.AccentRed;
                                }
                            }));
                            idx++;
                            continue;
                        }

                        int lat;
                        DateTime utc;
                        bool ok = TimeServiceHelper.QueryNtp(p.Key, out lat, out utc);
                        string status = ok ? string.Format("● Online ({0} ms)", lat) : "● Unreachable";
                        Color col = ok ? Theme.AccentGreen : Theme.AccentRed;
                        Log(string.Format("  [{0}] {1} -> {2}", idx, p.Key, status));

                        this.Invoke(new Action(() => {
                            Control[] matches = pnlPeersList.Controls.Find(name, true);
                            if (matches.Length > 0)
                            {
                                matches[0].Text = status;
                                matches[0].ForeColor = col;
                            }
                        }));
                        idx++;
                    }
                }
                finally
                {
                    this.Invoke(new Action(() => {
                        btnTestPeers.Enabled = true;
                        btnTestPeers.Text = "✓ Test Completed!";
                        btnTestPeers.BackColor = Color.FromArgb(20, 85, 45);

                        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                        t.Interval = 2500;
                        t.Tick += (s, e) => {
                            t.Stop();
                            t.Dispose();
                            btnTestPeers.Text = "⚡ Test Latency for All Peers";
                            btnTestPeers.BackColor = Color.FromArgb(22, 50, 72);
                        };
                        t.Start();
                    }));
                }
            });
        }
    }

    // =========================================================================
    // SECTION 7: Program Entry Point
    // =========================================================================
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
