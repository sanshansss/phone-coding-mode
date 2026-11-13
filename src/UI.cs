// 托盘、浮动按钮、快捷键、提示气泡、入口
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PhoneCoding
{
    // ------------------------------------------------------------ 快捷键
    internal static class HotkeyParser
    {
        public const uint MOD_ALT = 1, MOD_CTRL = 2, MOD_SHIFT = 4, MOD_WIN = 8;

        public static bool TryParse(string s, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                string u = p.ToUpperInvariant();
                if (u == "CTRL" || u == "CONTROL") { mods |= MOD_CTRL; continue; }
                if (u == "ALT") { mods |= MOD_ALT; continue; }
                if (u == "SHIFT") { mods |= MOD_SHIFT; continue; }
                if (u == "WIN" || u == "WINDOWS") { mods |= MOD_WIN; continue; }
                if (p.Length == 1)
                {
                    char c = char.ToUpperInvariant(p[0]);
                    if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) { vk = (uint)c; continue; }
                }
                if (u.Length >= 2 && u[0] == 'F')
                {
                    int n;
                    if (int.TryParse(u.Substring(1), out n) && n >= 1 && n <= 24) { vk = (uint)(0x70 + n - 1); continue; }
                }
            }
            return mods != 0 && vk != 0;
        }
    }

    internal class HotkeyHost : NativeWindow, IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        const int ID = 0x4A11;
        public event EventHandler Pressed;

        public HotkeyHost()
        {
            CreateHandle(new CreateParams());
        }

        public bool Register(string combo)
        {
            uint mods, vk;
            Unregister();
            if (!HotkeyParser.TryParse(combo, out mods, out vk))
            {
                Store.Log("hotkey parse failed: " + combo);
                return false;
            }
            bool ok = Native.RegisterHotKey(Handle, ID, mods, vk);
            int err = ok ? 0 : Marshal.GetLastWin32Error();
            Store.Log("hotkey " + combo + " registered=" + ok + " err=" + err);
            if (!ok)
            {
                // 1409 = 被别的程序占用，换成一组不容易撞的组合
                string[] fallbacks = new string[] { "Ctrl+Alt+J", "Ctrl+Alt+K", "Ctrl+Shift+F9", "Ctrl+Alt+F9", "Ctrl+Alt+Y" };
                for (int i = 0; i < fallbacks.Length; i++)
                {
                    uint m2, v2;
                    if (!HotkeyParser.TryParse(fallbacks[i], out m2, out v2)) continue;
                    if (Native.RegisterHotKey(Handle, ID, m2, v2))
                    {
                        Store.Log("hotkey fallback -> " + fallbacks[i]);
                        ActiveCombo = fallbacks[i];
                        return true;
                    }
                }
            }
            if (ok) ActiveCombo = combo;
            return ok;
        }

        /// <summary>实际生效的组合键（可能因冲突被自动替换）。</summary>
        public string ActiveCombo = "";

        public void Unregister()
        {
            try { Native.UnregisterHotKey(Handle, ID); } catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == ID && Pressed != null)
                Pressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Unregister();
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }

    // ------------------------------------------------------------ 提示气泡
    internal class Toast : Form
    {
        System.Windows.Forms.Timer timer;

        public Toast(string title, string sub)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(22, 22, 26);
            DoubleBuffered = true;
            Width = 320;
            Height = 78;

            Label t = new Label();
            t.Text = title;
            t.ForeColor = Color.White;
            t.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            t.Bounds = new Rectangle(0, 10, 320, 30);
            t.TextAlign = ContentAlignment.MiddleCenter;
            t.BackColor = Color.Transparent;
            Controls.Add(t);

            Label s = new Label();
            s.Text = sub;
            s.ForeColor = Color.FromArgb(130, 200, 255);
            s.Font = new Font("Consolas", 9.5f);
            s.Bounds = new Rectangle(0, 40, 320, 26);
            s.TextAlign = ContentAlignment.MiddleCenter;
            s.BackColor = Color.Transparent;
            Controls.Add(s);

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + 36);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1400;
            timer.Tick += delegate
            {
                timer.Stop();
                Close();
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(60, 70, 90), 1))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        public static void Show(string title, string sub)
        {
            try { new Toast(title, sub).Show(); } catch { }
        }
    }

    // ------------------------------------------------------------ 悬浮胶囊按钮
    internal class FloatingPill : Form
    {
        bool hot;
        public event EventHandler Clicked;

        public FloatingPill(bool portrait)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Size = new Size(46, 46);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            Portrait = portrait;
        }

        public bool Portrait;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public void SnapToEdge()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 10, wa.Y + wa.Height / 2 - Height / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Color.Magenta);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundRect(r, 23))
            {
                using (SolidBrush bg = new SolidBrush(hot ? Color.FromArgb(38, 44, 58) : Color.FromArgb(26, 30, 40)))
                    e.Graphics.FillPath(bg, path);
                using (Pen border = new Pen(Color.FromArgb(90, 200, 250), hot ? 2.2f : 1.6f))
                    e.Graphics.DrawPath(border, path);
            }

            Color fg = Color.FromArgb(120, 215, 255);
            if (Portrait)
            {
                Rectangle phone = new Rectangle(17, 11, 13, 24);
                using (Pen p = new Pen(fg, 1.9f)) Gfx.StrokeRoundRect(e.Graphics, p, phone, 3);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 130, 175)))
                    e.Graphics.FillRectangle(b, new Rectangle(21, 15, 5, 14));
            }
            else
            {
                Rectangle screen = new Rectangle(10, 17, 26, 14);
                using (Pen p = new Pen(fg, 1.9f)) Gfx.StrokeRoundRect(e.Graphics, p, screen, 2);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 130, 175)))
                    e.Graphics.FillRectangle(b, new Rectangle(14, 20, 18, 8));
            }
        }

        internal static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Clicked != null) Clicked(this, EventArgs.Empty);
            else if (e.Button == MouseButtons.Right) Hide();
        }
    }

    internal static class Gfx
    {
        /// <summary>圆角矩形描边。</summary>
        public static void StrokeRoundRect(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using (GraphicsPath p = FloatingPill.RoundRect(r, radius)) g.DrawPath(pen, p);
        }

        /// <summary>圆角矩形填充。</summary>
        public static void FillRoundRect(Graphics g, Brush b, Rectangle r, int radius)
        {
            using (GraphicsPath p = FloatingPill.RoundRect(r, radius)) g.FillPath(b, p);
        }
    }
}
