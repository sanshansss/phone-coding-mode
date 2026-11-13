// PhoneCoding — 一键竖屏「手机编程模式」
// 单目录多文件 WinForms 托盘程序，目标 .NET Framework 4.x / C# 5
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PhoneCoding
{
    // ---------------------------------------------------------------- Win32
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct LUID { public uint Low; public int High; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SRC { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAT { public uint Num; public uint Den; }

        [StructLayout(LayoutKind.Sequential)]
        public struct TGT
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx;
            public uint outputTechnology; public uint rotation; public uint scaling;
            public RAT refreshRate; public uint scanLineOrdering;
            public int targetAvailable; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PATH { public SRC sourceInfo; public TGT targetInfo; public uint flags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct REGION2 { public uint cx; public uint cy; }

        [StructLayout(LayoutKind.Sequential)]
        public struct VSIG
        {
            public ulong pixelRate; public RAT hSync; public RAT vSync;
            public REGION2 activeSize; public REGION2 totalSize;
            public uint videoStandard; public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TMODE { public VSIG targetVideoSignalInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct PT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SMODE { public uint width; public uint height; public uint pixelFormat; public PT position; }

        [StructLayout(LayoutKind.Explicit)]
        public struct MODEUNION
        {
            [FieldOffset(0)] public TMODE targetMode;
            [FieldOffset(0)] public SMODE sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MODE { public uint infoType; public uint id; public LUID adapterId; public MODEUNION modeInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVINFO_HEADER { public uint type; public uint size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TARGET_NAME
        {
            public DEVINFO_HEADER header;
            public uint flags; public uint outputTechnology;
            public ushort edidManufactureId; public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        public const uint QDC_ONLY_ACTIVE_PATHS = 2;
        public const uint SDC_APPLY = 0x80;
        public const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x20;
        public const uint SDC_SAVE_TO_DATABASE = 0x200;
        public const uint SDC_ALLOW_CHANGES = 0x400;

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPath, out uint numMode);
        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(uint flags, ref uint numPath, [Out] PATH[] paths,
            ref uint numMode, [Out] MODE[] modes, IntPtr currentTopologyId);
        [DllImport("user32.dll")]
        public static extern int SetDisplayConfig(uint numPath, [In] PATH[] paths,
            uint numMode, [In] MODE[] modes, uint flags);
        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref TARGET_NAME info);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize; public IntPtr hWnd; public uint uCallbackMessage;
            public uint uEdge; public RECT rc; public IntPtr lParam;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string cls, string win);

        public const uint ABM_SETSTATE = 10;
        public const uint ABS_AUTOHIDE = 0x1;

        // -------- window styles for the floating pill (never steal focus)
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
