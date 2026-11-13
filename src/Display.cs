// 显示器枚举 / 旋转 / 分辨率切换 / 任务栏
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PhoneCoding
{
    /// <summary>一块显示器的一路输出。</summary>
    public class DisplayPath
    {
        public long AdapterKey;
        public uint TargetId;
        public uint SourceId;
        public uint Rotation;             // 1=0 2=90 3=180 4=270
        public int Width, Height;         // 驱动里的「源」尺寸（旋转前）
        public int X, Y;
        public uint Frequency;
        public string Name = "";

        public string Key { get { return AdapterKey + ":" + TargetId; } }
        public bool IsPortrait { get { return Rotation == 2 || Rotation == 4; } }
        public bool IsPrimary { get { return X == 0 && Y == 0; } }

        /// <summary>旋转之后肉眼看到的宽高。</summary>
        public int VisualWidth { get { return IsPortrait ? Height : Width; } }
        public int VisualHeight { get { return IsPortrait ? Width : Height; } }

        public DisplayPath Clone()
        {
            DisplayPath p = new DisplayPath();
            p.AdapterKey = AdapterKey; p.TargetId = TargetId; p.SourceId = SourceId;
            p.Rotation = Rotation; p.Width = Width; p.Height = Height;
            p.X = X; p.Y = Y; p.Frequency = Frequency; p.Name = Name;
            return p;
        }

        public string Describe()
        {
            return string.Format("{0}  {1}x{2}{3}", Name, VisualWidth, VisualHeight,
                IsPrimary ? "  · 主屏" : "");
        }
        public override string ToString() { return Describe(); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2,
                     dmPanningWidth, dmPanningHeight;
        public static DevMode Create()
        {
            DevMode d = new DevMode();
            d.dmSize = (ushort)Marshal.SizeOf(typeof(DevMode));
            return d;
        }
    }

    /// <summary>一个「竖过来像手机」的候选分辨率。</summary>
    public class PortraitMode
    {
        public int SourceWidth, SourceHeight;
        public double Scale;
        public int Width { get { return SourceHeight; } }
        public int Height { get { return SourceWidth; } }

        public string MenuLabel()
        {
            if (Math.Abs(Scale - 1.0) < 0.02)
                return Width + " x " + Height + "     原尺寸";
            return Width + " x " + Height + "     放大 " + Scale.ToString("0.0") + " 倍";
        }
        public override string ToString() { return Width + "x" + Height; }
    }

    public static class DisplayManager
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
        static extern bool EnumDisplaySettingsW(string deviceName, int modeNum, ref DevMode devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
        static extern bool EnumDisplayDevicesW(string device, uint devNum, ref DISPLAY_DEVICE dd, uint flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        static long KeyOf(Native.LUID l)
        {
            return ((long)l.Low & 0xFFFFFFFFL) | (((long)l.High) << 32);
        }

        // ------------------------------------------------ enumerate monitors
        public static List<DisplayPath> Query()
        {
            uint np, nm;
            int rc = Native.GetDisplayConfigBufferSizes(Native.QDC_ONLY_ACTIVE_PATHS, out np, out nm);
            if (rc != 0) throw new InvalidOperationException("GetDisplayConfigBufferSizes rc=" + rc);

            Native.PATH[] paths = new Native.PATH[np];
            Native.MODE[] modes = new Native.MODE[nm];
            uint cnp = np, cnm = nm;
            rc = Native.QueryDisplayConfig(Native.QDC_ONLY_ACTIVE_PATHS, ref cnp, paths, ref cnm, modes, IntPtr.Zero);
            if (rc != 0) throw new InvalidOperationException("QueryDisplayConfig rc=" + rc);

            List<DisplayPath> list = new List<DisplayPath>();
            for (int i = 0; i < cnp; i++)
            {
                DisplayPath p = new DisplayPath();
                p.AdapterKey = KeyOf(paths[i].targetInfo.adapterId);
                p.TargetId = paths[i].targetInfo.id;
                p.SourceId = paths[i].sourceInfo.id;
                p.Rotation = paths[i].targetInfo.rotation;
                if (paths[i].targetInfo.refreshRate.Den != 0)
                    p.Frequency = paths[i].targetInfo.refreshRate.Num / paths[i].targetInfo.refreshRate.Den;

                uint mi = paths[i].sourceInfo.modeInfoIdx;
                if (mi < cnm && modes[mi].infoType == 1)
                {
                    p.Width = (int)modes[mi].modeInfo.sourceMode.width;
                    p.Height = (int)modes[mi].modeInfo.sourceMode.height;
                    p.X = modes[mi].modeInfo.sourceMode.position.x;
                    p.Y = modes[mi].modeInfo.sourceMode.position.y;
                }
                p.Name = FriendlyName(paths[i].targetInfo.adapterId, paths[i].targetInfo.id);
                list.Add(p);
            }
            return list;
        }

        static string FriendlyName(Native.LUID adapterId, uint targetId)
        {
            try
            {
                Native.TARGET_NAME n = new Native.TARGET_NAME();
                n.header.type = 2;
                n.header.size = (uint)Marshal.SizeOf(typeof(Native.TARGET_NAME));
                n.header.adapterId = adapterId;
                n.header.id = targetId;
                if (Native.DisplayConfigGetDeviceInfo(ref n) == 0 &&
                    !string.IsNullOrEmpty(n.monitorFriendlyDeviceName))
                    return n.monitorFriendlyDeviceName;
            }
            catch { }
            return "显示器 " + targetId;
        }

        // ------------------------------------------------ supported resolutions
        /// <summary>面板真正支持的横屏分辨率（去重、从大到小）。</summary>
        public static List<int[]> SupportedLandscapeModes()
        {
            List<int[]> list = new List<int[]>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < 8000; i++)
            {
                DevMode d = DevMode.Create();
                if (!EnumDisplaySettingsW(null, i, ref d)) break;
                int w = (int)d.dmPelsWidth, h = (int)d.dmPelsHeight;
                if (w < 320 || h < 200 || w <= h) continue;
                string k = w + "x" + h;
                if (seen.Contains(k)) continue;
                seen.Add(k);
                list.Add(new int[] { w, h });
            }
            list.Sort(delegate (int[] a, int[] b) { return b[0].CompareTo(a[0]); });
            return list;
        }

        /// <summary>
        /// 把真实支持的分辨率换算成「竖过来像手机」的档位。
        /// 旋转 90 度后 宽=源高、高=源宽，所以 源高/源宽 要落在 0.50~0.68（9:16 ~ 9:20）
        /// </summary>
        /// <param name="nativePortraitWidth">这块屏竖过来时的原生宽度（横屏模式下就是屏幕高，如 1080）</param>
        public static List<PortraitMode> PortraitLadder(int nativePortraitWidth)
        {
            if (nativePortraitWidth < 240) nativePortraitWidth = 1080;
            List<PortraitMode> result = new List<PortraitMode>();
            List<int[]> raw = SupportedLandscapeModes();
            HashSet<int> usedWidth = new HashSet<int>();
            for (int i = 0; i < raw.Count; i++)
            {
                int sw = raw[i][0], sh = raw[i][1];
                double visualAspect = (double)sh / sw;
                if (visualAspect < 0.50 || visualAspect > 0.68) continue;
                if (usedWidth.Contains(sh)) continue;
                usedWidth.Add(sh);
                PortraitMode pm = new PortraitMode();
                pm.SourceWidth = sw;
                pm.SourceHeight = sh;
                // 竖屏宽度 = 源高(sh)。原生竖屏宽度 / 当前竖屏宽度 = 视觉放大倍数
                pm.Scale = (double)nativePortraitWidth / sh;
                result.Add(pm);
            }
            // 放大倍数从 1.0 往上排（1080 -> 720 -> 480），用户一眼能看出「越大越放大」
            result.Sort(delegate (PortraitMode a, PortraitMode b) { return a.Scale.CompareTo(b.Scale); });
            return result;
        }

        /// <summary>按「目标竖直宽度 = 原生横屏宽度 / 倍数」找最接近的真实档位。</summary>
        public static PortraitMode PickByScale(List<PortraitMode> ladder, double scale, int nativePortraitWidth)
        {
            if (ladder == null || ladder.Count == 0) return null;
            if (nativePortraitWidth < 240) nativePortraitWidth = 1080;
            if (scale < 1.0) scale = 1.0;
            int want = (int)Math.Round(nativePortraitWidth / scale);
            PortraitMode best = ladder[0];
            int bestDelta = int.MaxValue;
            for (int i = 0; i < ladder.Count; i++)
            {
                int d = Math.Abs(ladder[i].Width - want);
                if (d < bestDelta) { bestDelta = d; best = ladder[i]; }
            }
            return best;
        }

        // ------------------------------------------------ apply
        /// <summary>只改 want 里列出的显示器，其余保持原样。</summary>
        public static bool Apply(List<DisplayPath> want, out string error)
        {
            error = "";
            uint np, nm;
            if (Native.GetDisplayConfigBufferSizes(Native.QDC_ONLY_ACTIVE_PATHS, out np, out nm) != 0)
            { error = "读取显示配置失败"; return false; }

            Native.PATH[] paths = new Native.PATH[np];
            Native.MODE[] modes = new Native.MODE[nm];
            uint cnp = np, cnm = nm;
            if (Native.QueryDisplayConfig(Native.QDC_ONLY_ACTIVE_PATHS, ref cnp, paths, ref cnm, modes, IntPtr.Zero) != 0)
            { error = "读取显示配置失败"; return false; }

            Native.MODE[] outModes = new Native.MODE[cnm];
            Array.Copy(modes, outModes, (int)cnm);

            for (int i = 0; i < cnp; i++)
            {
                long ak = KeyOf(paths[i].targetInfo.adapterId);
                DisplayPath w = null;
                for (int j = 0; j < want.Count; j++)
                    if (want[j].AdapterKey == ak && want[j].TargetId == paths[i].targetInfo.id) { w = want[j]; break; }
                if (w == null) continue;

                paths[i].targetInfo.rotation = w.Rotation;
                paths[i].flags |= 1;
                paths[i].targetInfo.statusFlags |= 1;
                paths[i].sourceInfo.statusFlags |= 1;

                uint mi = paths[i].sourceInfo.modeInfoIdx;
                if (mi == 0xFFFFFFFF || mi >= cnm || outModes[mi].infoType != 1) continue;

                Native.MODE m = outModes[mi];
                m.modeInfo.sourceMode.width = (uint)w.Width;
                m.modeInfo.sourceMode.height = (uint)w.Height;
                m.modeInfo.sourceMode.position.x = w.X;
                m.modeInfo.sourceMode.position.y = w.Y;
                outModes[mi] = m;
            }

            uint flags = Native.SDC_APPLY | Native.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                       | Native.SDC_SAVE_TO_DATABASE | Native.SDC_ALLOW_CHANGES;
            int rc = Native.SetDisplayConfig(cnp, paths, cnm, outModes, flags);
            if (rc != 0) { error = "SetDisplayConfig 返回 " + rc; return false; }
            return true;
        }

        // ------------------------------------------------ taskbar
        public static void SetTaskbarAutoHide(bool on)
        {
            try
            {
                Native.APPBARDATA d = new Native.APPBARDATA();
                d.cbSize = Marshal.SizeOf(typeof(Native.APPBARDATA));
                d.hWnd = Native.FindWindow("Shell_TrayWnd", null);
                d.lParam = (IntPtr)(on ? Native.ABS_AUTOHIDE : 0);
                Native.SHAppBarMessage(Native.ABM_SETSTATE, ref d);
            }
            catch { }
        }
    }
}
