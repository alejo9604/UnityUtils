using System.Text;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

public interface IFlashWindow
{
    void Init();
    /// <summary>Flash the Window until it receives focus.</summary>
    bool Flash();
    /// <summary>Flash the window for specified amount of times.</summary>
    bool Flash(uint count);
    bool Start();
    bool Stop();
}


public static class FlashWindow
{
    public static readonly IFlashWindow Instance =
#if UNITY_STANDALONE_WIN
    new FlashWindowWin32();
#else
    new FlashWindowNoOp();
#endif
}

class FlashWindowNoOp : IFlashWindow
{
    public void Init() { }
    public bool Flash() => false;
    public bool Flash(uint count) => false;
    public bool Start() => false;
    public bool Stop() => false;
}

#if UNITY_STANDALONE_WIN

// https://gist.github.com/mattbenic/908483ad0bedbc62ab17
public static class WindowHandle
{
    #region DLL Imports
    const string UnityWindowClassName = "UnityWndClass";

    [DllImport("User32.dll")]
    private static extern IntPtr GetActiveWindow();


    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpEnumFunc, IntPtr lParam);
    #endregion

    private static IntPtr handle;
    public static IntPtr GetHandle()
    {
        if (handle.Equals(IntPtr.Zero))
            TryToGetHandle();

        return handle;
    }    

    public static void TryToGetHandle()
    {
        var h = GetActiveWindow();
        if (h == IntPtr.Zero) {
            var threadId = GetCurrentThreadId();

            EnumThreadWindows(threadId, (hWnd, lParam) => {
                var classTextB = new StringBuilder(1000);
                GetClassName(hWnd, classTextB, classTextB.Capacity);
                var classText = classTextB.ToString();
                if (classText == UnityWindowClassName) {
                    h = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
        }
        Debug.LogErrorFormat("Getting handle: {0}", h);
        handle = h;
    }
}

class FlashWindowWin32 : IFlashWindow
{

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        /// The size of the structure in bytes.
        public uint cbSize;

        /// A Handle to the Window to be Flashed. The window can be either opened or minimized.
        public IntPtr hwnd;

        /// The Flash Status.
        public uint dwFlags;

        /// The number of times to Flash the window.
        public uint uCount;

        /// The rate at which the Window is to be flashed, in milliseconds.
        /// If Zero, the function uses the default cursor blink rate.
        public uint dwTimeout;
    }

    /// Stop flashing. The system restores the window to its original state.
    private const uint FLASHW_STOP = 0;

    /// Flash the window caption.
    private const uint FLASHW_CAPTION = 1;

    /// Flash the taskbar button.
    private const uint FLASHW_TRAY = 2;

    /// Flash both the window caption and taskbar button.
    /// This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
    private const uint FLASHW_ALL = 3;

    /// Flash continuously, until the FLASHW_STOP flag is set.
    private const uint FLASHW_TIMER = 4;

    /// Flash continuously until the window comes to the foreground.
    private const uint FLASHW_TIMERNOFG = 12;

    static FLASHWINFO Create_FLASHWINFO(IntPtr handle, uint flags, uint count, uint timeout)
    {
        var fi = new FLASHWINFO { hwnd = handle, dwFlags = flags, uCount = count, dwTimeout = timeout };
        fi.cbSize = Convert.ToUInt32(Marshal.SizeOf(fi));
        return fi;
    }

    public void Init()
    {
        //On Editor sometimes failes getting the Handle when is no focus.
        //To avoid this I call Init() when app start.
        WindowHandle.TryToGetHandle();
    }

    public bool Flash()
    {
        try {
            var fi = Create_FLASHWINFO(
                WindowHandle.GetHandle(), FLASHW_ALL | FLASHW_TIMERNOFG, uint.MaxValue, 0
            );
            return FlashWindowEx(ref fi);
        }
        catch (Exception e) {
            //print(nameof(Flash), e);
        }
        return false;
    }

    public bool Flash(uint count)
    {
        try {
            var fi = Create_FLASHWINFO(WindowHandle.GetHandle(), FLASHW_ALL, count, 0);
            return FlashWindowEx(ref fi);
        }
        catch (Exception e) {
            //Log.d.error($"{nameof(Flash)}({count})", e);
        }

        return false;
    }

    public bool Start()
    {
        try {
            var fi = Create_FLASHWINFO(WindowHandle.GetHandle(), FLASHW_ALL, uint.MaxValue, 0);
            return FlashWindowEx(ref fi);
        }
        catch (Exception e) {
            //Log.d.error(nameof(Start), e);
        }

        return false;
    }

    public bool Stop()
    {
        try {
            var fi = Create_FLASHWINFO(WindowHandle.GetHandle(), FLASHW_STOP, uint.MaxValue, 0);
            return FlashWindowEx(ref fi);
        }
        catch (Exception e) {
            //Log.d.error(nameof(Stop), e);
        }

        return false;
    }
}

#endif