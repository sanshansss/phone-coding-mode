// 快照 / 配置 / 进入与退出手机模式
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PhoneCoding
{
    public static class Store
    {
        public static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhoneCoding");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }
        public static string ConfigFile { get { return Path.Combine(Dir, "config.ini"); } }
        public static string SnapshotFile { get { return Path.Combine(Dir, "snapshot.ini"); } }
        public static string LogFile { get { return Path.Combine(Dir, "log.txt"); } }

        public static void Log(string s)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + "\r\n";
                File.AppendAllText(LogFile, line, Encoding.UTF8);
            }
            catch { }
        }
    }

    public class Settings
    {
        public double Scale = 1.5;          // 期望放大倍数
        public int Rotation = 90;           // 90 或 270
        public string MonitorKey = "";      // 空 = 主屏
        public string Hotkey = "Ctrl+Alt+P";
        public bool HotkeyEnabled = true;
        public bool AutoHideTaskbar = true;
        public bool ShowToast = true;
        public bool ShowPill = true;
        public bool KeepPhoneModeOnExit = false;

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (!File.Exists(Store.ConfigFile)) return s;
                string[] lines = File.ReadAllLines(Store.ConfigFile, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == '[') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    s.Set(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
                }
            }
            catch (Exception e) { Store.Log("config read: " + e.Message); }
            return s;
        }

        public void Set(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "scale":
                    double d;
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out d) && d >= 1.0 && d <= 3.0)
                        Scale = d;
                    break;
                case "rotation": Rotation = value == "270" ? 270 : 90; break;
                case "monitor": MonitorKey = value; break;
                case "hotkey": Hotkey = value; break;
                case "hotkey_enabled": HotkeyEnabled = value == "1"; break;
                case "autohide_taskbar": AutoHideTaskbar = value == "1"; break;
                case "show_toast": ShowToast = value == "1"; break;
                case "show_pill": ShowPill = value == "1"; break;
            }
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# PhoneCoding");
                sb.AppendLine("scale=" + Scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("rotation=" + Rotation);
                sb.AppendLine("monitor=" + MonitorKey);
                sb.AppendLine("hotkey=" + Hotkey);
                sb.AppendLine("hotkey_enabled=" + (HotkeyEnabled ? "1" : "0"));
                sb.AppendLine("autohide_taskbar=" + (AutoHideTaskbar ? "1" : "0"));
                sb.AppendLine("show_toast=" + (ShowToast ? "1" : "0"));
                sb.AppendLine("show_pill=" + (ShowPill ? "1" : "0"));
                File.WriteAllText(Store.ConfigFile, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Store.Log("config write: " + e.Message); }
        }
    }

    /// <summary>进入手机模式前的桌面快照，用来精确还原。</summary>
    public static class Snapshot
    {
        public static void Save(List<DisplayPath> paths)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("count=" + paths.Count);
                for (int i = 0; i < paths.Count; i++)
                {
                    DisplayPath p = paths[i];
                    string n = p.Name.Replace("\r", " ").Replace("\n", " ");
                    sb.AppendLine("p" + i + ".adapter=" + p.AdapterKey);
                    sb.AppendLine("p" + i + ".target=" + p.TargetId);
                    sb.AppendLine("p" + i + ".rotation=" + p.Rotation);
                    sb.AppendLine("p" + i + ".w=" + p.Width);
                    sb.AppendLine("p" + i + ".h=" + p.Height);
                    sb.AppendLine("p" + i + ".x=" + p.X);
                    sb.AppendLine("p" + i + ".y=" + p.Y);
                    sb.AppendLine("p" + i + ".name=" + n);
                }
                File.WriteAllText(Store.SnapshotFile, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Store.Log("snapshot write: " + e.Message); }
        }

        public static List<DisplayPath> Load()
        {
            List<DisplayPath> list = new List<DisplayPath>();
            try
            {
                if (!File.Exists(Store.SnapshotFile)) return list;
                string[] lines = File.ReadAllLines(Store.SnapshotFile, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    if (k == "count" || k.Length < 4 || k[0] != 'p') continue;
                    int dot = k.IndexOf('.');
                    if (dot <= 1) continue;
                    int idx;
                    if (!int.TryParse(k.Substring(1, dot - 1), out idx)) continue;
                    while (list.Count <= idx) list.Add(new DisplayPath());
                    DisplayPath p = list[idx];
                    int iv; uint uv; long lv;
                    switch (k.Substring(dot + 1))
                    {
                        case "adapter": if (long.TryParse(v, out lv)) p.AdapterKey = lv; break;
                        case "target": if (uint.TryParse(v, out uv)) p.TargetId = uv; break;
                        case "rotation": if (uint.TryParse(v, out uv)) p.Rotation = uv; break;
                        case "w": if (int.TryParse(v, out iv)) p.Width = iv; break;
                        case "h": if (int.TryParse(v, out iv)) p.Height = iv; break;
                        case "x": if (int.TryParse(v, out iv)) p.X = iv; break;
                        case "y": if (int.TryParse(v, out iv)) p.Y = iv; break;
                        case "name": p.Name = v; break;
                    }
                }
            }
            catch (Exception e) { Store.Log("snapshot read: " + e.Message); }
            return list;
        }
    }

    public enum SwitchResult { Entered, Exited, Already, Failed }

    public class SwitchOutcome
    {
        public SwitchResult Result;
        public string Message = "";
        public PortraitMode Mode;
    }

    /// <summary>手机模式的核心逻辑，UI 无关。</summary>
    public static class PhoneMode
    {
        public static DisplayPath PickMonitor(Settings cfg, List<DisplayPath> paths)
        {
            if (paths == null || paths.Count == 0) return null;
            if (!string.IsNullOrEmpty(cfg.MonitorKey))
            {
                for (int i = 0; i < paths.Count; i++)
                    if (paths[i].Key == cfg.MonitorKey) return paths[i];
            }
            for (int i = 0; i < paths.Count; i++) if (paths[i].IsPrimary) return paths[i];
            for (int i = 0; i < paths.Count; i++) if (!paths[i].IsPortrait) return paths[i];
            return paths[0];
        }

        public static SwitchOutcome Enter(Settings cfg)
        {
            SwitchOutcome o = new SwitchOutcome();
            List<DisplayPath> cur;
            try { cur = DisplayManager.Query(); }
            catch (Exception e) { o.Result = SwitchResult.Failed; o.Message = e.Message; return o; }

            if (cur.Count == 0) { o.Result = SwitchResult.Failed; o.Message = "没有检测到显示器"; return o; }

            DisplayPath target = PickMonitor(cfg, cur);
            if (target == null) { o.Result = SwitchResult.Failed; o.Message = "找不到目标显示器"; return o; }

            if (target.IsPortrait && target.VisualHeight > target.VisualWidth)
            {
                o.Result = SwitchResult.Already;
                o.Message = "已经是竖屏 " + target.VisualWidth + "x" + target.VisualHeight;
                return o;
            }

            Snapshot.Save(cur);

            // 竖屏原生宽度：横屏时它是屏幕高(1080)，竖屏时它就是当前宽
            int nativeW = target.IsPortrait ? target.VisualWidth : target.Height;
            List<PortraitMode> ladder = DisplayManager.PortraitLadder(nativeW);
            PortraitMode pick = DisplayManager.PickByScale(ladder, cfg.Scale, nativeW);
            if (pick == null)
            {
                o.Result = SwitchResult.Failed;
                o.Message = "这块屏找不到合适的竖屏分辨率";
                return o;
            }

            uint rot = (uint)(cfg.Rotation == 270 ? 4 : 2);
            List<DisplayPath> want = new List<DisplayPath>();
            for (int i = 0; i < cur.Count; i++)
            {
                DisplayPath p = cur[i].Clone();
                if (p.Key == target.Key)
                {
                    p.Rotation = rot;
                    p.Width = pick.SourceWidth;
                    p.Height = pick.SourceHeight;
                    if (p.IsPrimary) { p.X = 0; p.Y = 0; }
                }
                want.Add(p);
            }

            string err;
            if (!DisplayManager.Apply(want, out err))
            {
                o.Result = SwitchResult.Failed;
                o.Message = err;
                return o;
            }

            if (cfg.AutoHideTaskbar) DisplayManager.SetTaskbarAutoHide(true);

            o.Result = SwitchResult.Entered;
            o.Mode = pick;
            o.Message = pick.Width + " x " + pick.Height;
            Store.Log("ENTER " + target.Name + " -> " + o.Message);
            return o;
        }

        public static SwitchOutcome Exit(Settings cfg)
        {
            SwitchOutcome o = new SwitchOutcome();
            List<DisplayPath> snap = Snapshot.Load();
            if (snap.Count == 0)
            {
                o.Result = SwitchResult.Failed;
                o.Message = "没有可还原的快照";
                return o;
            }

            List<DisplayPath> cur;
            try { cur = DisplayManager.Query(); }
            catch (Exception e) { o.Result = SwitchResult.Failed; o.Message = e.Message; return o; }

            List<DisplayPath> want = new List<DisplayPath>();
            for (int i = 0; i < snap.Count; i++)
                for (int j = 0; j < cur.Count; j++)
                    if (cur[j].Key == snap[i].Key) { want.Add(snap[i]); break; }

            if (want.Count == 0)
            {
                o.Result = SwitchResult.Failed;
                o.Message = "原显示器未连接";
                return o;
            }

            string err;
            if (!DisplayManager.Apply(want, out err))
            {
                o.Result = SwitchResult.Failed;
                o.Message = err;
                return o;
            }

            if (cfg.AutoHideTaskbar) DisplayManager.SetTaskbarAutoHide(false);

            o.Result = SwitchResult.Exited;
            o.Message = "已恢复横屏";
            Store.Log("EXIT restored");
            return o;
        }

        /// <summary>当前主目标屏是否已经是竖屏。</summary>
        public static bool IsPhoneMode(Settings cfg)
        {
            try
            {
                List<DisplayPath> cur = DisplayManager.Query();
                DisplayPath m = PickMonitor(cfg, cur);
                return m != null && m.IsPortrait;
            }
            catch { return false; }
        }
    }
}
