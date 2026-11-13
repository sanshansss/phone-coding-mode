// 托盘菜单 + 程序入口
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PhoneCoding
{
    internal class TrayApp : ApplicationContext
    {
        readonly Settings cfg;
        NotifyIcon tray;
        HotkeyHost hotkey;
        FloatingPill pill;
        ToolStripMenuItem toggleItem;
        ToolStripMenuItem startupItem;
        ToolStripMenuItem pillItem;
        Icon iconH, iconV;
        bool busy;
        bool portraitNow;

        public TrayApp(Settings c)
        {
            cfg = c;
            iconH = MakeIcon(false);
            iconV = MakeIcon(true);

            tray = new NotifyIcon();
            tray.Visible = true;
            tray.DoubleClick += delegate { DoToggle(); };
            BuildMenu();

            hotkey = new HotkeyHost();
            if (cfg.HotkeyEnabled && !hotkey.Register(cfg.Hotkey))
                Store.Log("全局快捷键注册失败: " + cfg.Hotkey);
            hotkey.Pressed += delegate { DoToggle(); };

            portraitNow = PhoneMode.IsPhoneMode(cfg);
            RefreshState();

            pill = new FloatingPill(portraitNow);
            pill.Clicked += delegate { DoToggle(); };
            pill.SnapToEdge();
            if (cfg.ShowPill) pill.Show();
            if (pillItem != null) pillItem.Checked = cfg.ShowPill;
        }

        // ---------------------------------------------------------- menu
        void BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9.2f);
            menu.RenderMode = ToolStripRenderMode.System;

            toggleItem = new ToolStripMenuItem("切换到手机模式");
            toggleItem.Font = new Font("Microsoft YaHei UI", 9.6f, FontStyle.Bold);
            toggleItem.Click += delegate { DoToggle(); };
            menu.Items.Add(toggleItem);

            menu.Items.Add(new ToolStripSeparator());

            // ---- 放大倍数
            ToolStripMenuItem sizeMenu = new ToolStripMenuItem("放大倍数");
            sizeMenu.DropDownOpening += delegate { FillSizeMenu(sizeMenu); };
            menu.Items.Add(sizeMenu);

            // ---- 目标显示器
            ToolStripMenuItem monMenu = new ToolStripMenuItem("作用在哪块屏");
            monMenu.DropDownOpening += delegate { FillMonitorMenu(monMenu); };
            menu.Items.Add(monMenu);

            // ---- 方向
            ToolStripMenuItem dirMenu = new ToolStripMenuItem("竖屏方向");
            ToolStripMenuItem left = new ToolStripMenuItem("向左转 90 度");
            ToolStripMenuItem right = new ToolStripMenuItem("向右转 90 度");
            left.Click += delegate { cfg.Rotation = 90; cfg.Save(); };
            right.Click += delegate { cfg.Rotation = 270; cfg.Save(); };
            dirMenu.DropDownOpening += delegate
            {
                left.Checked = cfg.Rotation == 90;
                right.Checked = cfg.Rotation == 270;
            };
            dirMenu.DropDownItems.Add(left);
            dirMenu.DropDownItems.Add(right);
            menu.Items.Add(dirMenu);

            // ---- 选项
            ToolStripMenuItem optMenu = new ToolStripMenuItem("选项");
            ToolStripMenuItem hideBar = new ToolStripMenuItem("进入时自动隐藏任务栏");
            hideBar.Checked = cfg.AutoHideTaskbar;
            hideBar.Click += delegate
            {
                cfg.AutoHideTaskbar = !cfg.AutoHideTaskbar;
                hideBar.Checked = cfg.AutoHideTaskbar;
                cfg.Save();
            };
            ToolStripMenuItem toastItem = new ToolStripMenuItem("显示切换提示");
            toastItem.Checked = cfg.ShowToast;
            toastItem.Click += delegate
            {
                cfg.ShowToast = !cfg.ShowToast;
                toastItem.Checked = cfg.ShowToast;
                cfg.Save();
            };
            ToolStripMenuItem hkItem = new ToolStripMenuItem("启用全局快捷键");
            hkItem.Checked = cfg.HotkeyEnabled;
            hkItem.Click += delegate
            {
                cfg.HotkeyEnabled = !cfg.HotkeyEnabled;
                hkItem.Checked = cfg.HotkeyEnabled;
                cfg.Save();
                if (cfg.HotkeyEnabled) hotkey.Register(cfg.Hotkey); else hotkey.Unregister();
            };
            pillItem = new ToolStripMenuItem("显示桌面悬浮按钮");
            pillItem.Checked = cfg.ShowPill;
            pillItem.Click += delegate
            {
                cfg.ShowPill = !cfg.ShowPill;
                pillItem.Checked = cfg.ShowPill;
                cfg.Save();
                if (cfg.ShowPill) { pill.Show(); pill.SnapToEdge(); }
                else pill.Hide();
            };
            optMenu.DropDownItems.Add(hideBar);
            optMenu.DropDownItems.Add(toastItem);
            optMenu.DropDownItems.Add(pillItem);
            optMenu.DropDownItems.Add(new ToolStripSeparator());
            optMenu.DropDownItems.Add(hkItem);
            menu.Items.Add(optMenu);

            menu.Items.Add(new ToolStripSeparator());

            startupItem = new ToolStripMenuItem("开机自动启动");
            startupItem.Checked = IsStartupEnabled();
            startupItem.Click += delegate
            {
                SetStartup(!IsStartupEnabled());
                startupItem.Checked = IsStartupEnabled();
            };
            menu.Items.Add(startupItem);

            ToolStripMenuItem showAll = new ToolStripMenuItem("列出所有显示器（诊断）");
            showAll.Click += delegate
            {
                try
                {
                    List<DisplayPath> ps = DisplayManager.Query();
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = 0; i < ps.Count; i++) sb.AppendLine("· " + ps[i].Describe());
                    MessageBox.Show(sb.ToString(), "检测到 " + ps.Count + " 块屏",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception e) { MessageBox.Show(e.Message); }
            };
            menu.Items.Add(showAll);

            ToolStripMenuItem openDir = new ToolStripMenuItem("打开配置文件夹");
            openDir.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", Store.Dir); } catch { }
            };
            menu.Items.Add(openDir);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem quit = new ToolStripMenuItem("退出");
            quit.Click += delegate { Quit(); };
            menu.Items.Add(quit);

            tray.ContextMenuStrip = menu;
        }

        void FillSizeMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            List<PortraitMode> ladder = null;
            try
            {
                List<DisplayPath> ps = DisplayManager.Query();
                DisplayPath t = PhoneMode.PickMonitor(cfg, ps);
                int nativeW = t == null ? 1080 : (t.IsPortrait ? t.VisualWidth : t.Height);
                ladder = DisplayManager.PortraitLadder(nativeW);
            }
            catch { }

            if (ladder == null || ladder.Count == 0) return;
            int nativeW2 = 1080;
            try
            {
                List<DisplayPath> all = DisplayManager.Query();
                DisplayPath t2 = PhoneMode.PickMonitor(cfg, all);
                if (t2 != null) nativeW2 = t2.IsPortrait ? t2.VisualWidth : t2.Height;
            }
            catch { }
            PortraitMode current = DisplayManager.PickByScale(ladder, cfg.Scale, nativeW2);
            for (int i = 0; i < ladder.Count; i++)
            {
                PortraitMode pm = ladder[i];
                ToolStripMenuItem mi = new ToolStripMenuItem(pm.MenuLabel());
                mi.Tag = pm;
                mi.Checked = (current != null && current.Width == pm.Width);
                mi.Click += delegate
                {
                    cfg.Scale = ((PortraitMode)mi.Tag).Scale;
                    cfg.Save();
                    if (portraitNow) { DoExit(); DoEnter(); }
                };
                parent.DropDownItems.Add(mi);
            }
        }

        void FillMonitorMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            List<DisplayPath> ps;
            try { ps = DisplayManager.Query(); } catch { return; }
            for (int i = 0; i < ps.Count; i++)
            {
                DisplayPath p = ps[i];
                ToolStripMenuItem mi = new ToolStripMenuItem(p.Describe());
                mi.Tag = p.Key;
                mi.Checked = cfg.MonitorKey == p.Key || (cfg.MonitorKey == "" && p.IsPrimary);
                mi.Click += delegate
                {
                    cfg.MonitorKey = (string)mi.Tag;
                    cfg.Save();
                };
                parent.DropDownItems.Add(mi);
            }
        }

        // ---------------------------------------------------------- state
        void RefreshState()
        {
            tray.Icon = portraitNow ? iconV : iconH;
            string hkText = (hotkey != null && !string.IsNullOrEmpty(hotkey.ActiveCombo)) ? hotkey.ActiveCombo : cfg.Hotkey;
            tray.Text = portraitNow
                ? "PhoneCoding · 竖屏中（" + hkText + " 回横屏）"
                : "PhoneCoding · " + hkText + " 一键竖屏";
            toggleItem.Text = portraitNow ? "退出手机模式（回到横屏）" : "切换到手机模式";
            if (pill != null) { pill.Portrait = portraitNow; pill.Invalidate(); }
        }

        void DoToggle()
        {
            Store.Log("toggle pressed, portraitNow=" + portraitNow);
            if (portraitNow) DoExit(); else DoEnter();
        }

        void DoEnter()
        {
            if (busy) return;
            busy = true;
            try
            {
                SwitchOutcome o = PhoneMode.Enter(cfg);
                portraitNow = PhoneMode.IsPhoneMode(cfg);
                Store.Log("enter result=" + o.Result + " msg=" + o.Message + " portraitNow=" + portraitNow);
                RefreshState();
                if (cfg.ShowToast)
                {
                    if (o.Result == SwitchResult.Entered) Toast.Show("竖屏模式", o.Message + "  · 放大 " + (o.Mode != null ? o.Mode.Scale.ToString("0.0#") : "?") + " 倍");
                    else if (o.Result == SwitchResult.Already) Toast.Show("已经在竖屏", o.Message);
                    else Toast.Show("切换失败", o.Message);
                }
            }
            catch (Exception e)
            {
                Store.Log("enter: " + e);
                if (cfg.ShowToast) Toast.Show("出错了", e.Message);
            }
            finally { busy = false; }
        }

        void DoExit()
        {
            if (busy) return;
            busy = true;
            try
            {
                SwitchOutcome o = PhoneMode.Exit(cfg);
                portraitNow = PhoneMode.IsPhoneMode(cfg);
                Store.Log("exit result=" + o.Result + " msg=" + o.Message + " portraitNow=" + portraitNow);
                RefreshState();
                if (cfg.ShowToast)
                {
                    if (o.Result == SwitchResult.Exited) Toast.Show("已回到横屏", o.Message);
                    else Toast.Show("还原失败", o.Message);
                }
            }
            catch (Exception e)
            {
                Store.Log("exit: " + e);
                if (cfg.ShowToast) Toast.Show("出错了", e.Message);
            }
            finally { busy = false; }
        }

        // ---------------------------------------------------------- icon
        Icon MakeIcon(bool portrait)
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Color accent = Color.FromArgb(90, 200, 250);
                Rectangle body = portrait ? new Rectangle(9, 3, 14, 26) : new Rectangle(3, 9, 26, 14);
                using (Pen pen = new Pen(accent, 2.2f))
                    Gfx.StrokeRoundRect(g, pen, body, 4);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 120, 170)))
                    g.FillRectangle(b, new Rectangle(body.X + 4, body.Y + 5, body.Width - 8, body.Height - 10));
            }
            IntPtr h = bmp.GetHicon();
            return Icon.FromHandle(h);
        }

        // ---------------------------------------------------------- startup
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (k == null) return false;
                    return !string.IsNullOrEmpty(k.GetValue("PhoneCoding") as string);
                }
            }
            catch { return false; }
        }

        void SetStartup(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("PhoneCoding", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("PhoneCoding", false);
                }
            }
            catch (Exception e) { Store.Log("startup: " + e.Message); }
        }

        // ---------------------------------------------------------- quit
        void Quit()
        {
            try
            {
                if (portraitNow && !cfg.KeepPhoneModeOnExit)
                {
                    SwitchOutcome o = PhoneMode.Exit(cfg);
                    if (o.Result != SwitchResult.Exited)
                        Store.Log("exit on quit failed: " + o.Message);
                }
            }
            catch (Exception e) { Store.Log("quit exit: " + e.Message); }

            try { hotkey.Dispose(); } catch { }
            try { if (pill != null) { pill.Hide(); pill.Dispose(); } } catch { }
            try { tray.Visible = false; tray.Dispose(); } catch { }
            ExitThread();
        }
    }

    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Settings cfg = Settings.Load();
            string cmd = args.Length > 0 ? args[0].TrimStart('-', '/').ToLowerInvariant() : "";

            // ---------- 命令行（给脚本 / Alfred / 快捷方式用）
            if (cmd == "list")
            {
                List<DisplayPath> ps = DisplayManager.Query();
                Console.WriteLine("检测到 " + ps.Count + " 块显示器:");
                for (int i = 0; i < ps.Count; i++)
                    Console.WriteLine("  [" + i + "] " + ps[i].Describe() + "   key=" + ps[i].Key);
                int baseW = 1080;
                try
                {
                    DisplayPath prim = PhoneMode.PickMonitor(cfg, ps);
                    if (prim != null) baseW = prim.IsPortrait ? prim.VisualWidth : prim.Height;
                }
                catch { }
                List<PortraitMode> ladder = DisplayManager.PortraitLadder(baseW);
                Console.WriteLine("可用竖屏档位: " + ladder.Count + " 个");
                for (int i = 0; i < ladder.Count && i < 12; i++)
                    Console.WriteLine("    " + ladder[i].MenuLabel());
                return 0;
            }
            if (cmd == "on") { SwitchOutcome o = PhoneMode.Enter(cfg); Console.WriteLine(o.Result + " " + o.Message); return o.Result == SwitchResult.Entered || o.Result == SwitchResult.Already ? 0 : 1; }
            if (cmd == "off") { SwitchOutcome o = PhoneMode.Exit(cfg); Console.WriteLine(o.Result + " " + o.Message); return o.Result == SwitchResult.Exited ? 0 : 1; }
            if (cmd == "status")
            {
                List<DisplayPath> ps = DisplayManager.Query();
                for (int i = 0; i < ps.Count; i++) Console.WriteLine(ps[i].Describe() + "   " + ps[i].Width + "x" + ps[i].Height + " rot" + ps[i].Rotation);
                Console.WriteLine("phone_mode=" + PhoneMode.IsPhoneMode(cfg));
                return 0;
            }
            if (cmd == "set")
            {
                for (int i = 1; i + 1 < args.Length; i += 2) cfg.Set(args[i], args[i + 1]);
                cfg.Save();
                Console.WriteLine("saved");
                return 0;
            }

            // ---------- 托盘常驻
            bool isFirst;
            using (Mutex mtx = new Mutex(true, "PhoneCoding.SingleInstance", out isFirst))
            {
                if (!isFirst) return 0;
                Store.Log("---- start ----");
                Application.Run(new TrayApp(cfg));
            }
            return 0;
        }
    }
}
