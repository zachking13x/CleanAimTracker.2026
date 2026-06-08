using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CleanAimTracker.Services
{
    public class RawInputService
    {
        public event Action<int, int, long>? MouseMoved;

        private HwndSource? _source;
        private IntPtr _hwnd;
        private bool _isRegistered = false;

        private const int WM_INPUT = 0x00FF;
        private const int RID_INPUT = 0x10000003;
        private const int RIM_TYPEMOUSE = 0;
        private const int RIDEV_INPUTSINK = 0x00000100;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public int dwType;
            public int dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public int dwFlags;
            public IntPtr hwndTarget;
        }

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("User32.dll")]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        // -----------------------------
        // INITIALIZE WITH WINDOW
        // -----------------------------
        public void Initialize(Window window)
        {
            _source = (HwndSource)PresentationSource.FromVisual(window)!;
            _source.AddHook(WndProc);
            _hwnd = _source.Handle;
        }

        // -----------------------------
        // START RAW INPUT
        // -----------------------------
        public void Start()
        {
            if (_isRegistered || _hwnd == IntPtr.Zero)
                return;

            RAWINPUTDEVICE[] rid =
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,
                    usUsage = 0x02,
                    dwFlags = RIDEV_INPUTSINK,
                    hwndTarget = _hwnd
                }
            };

            RegisterRawInputDevices(
                rid,
                (uint)rid.Length,
                (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));

            _isRegistered = true;
        }

        // -----------------------------
        // STOP RAW INPUT
        // -----------------------------
        public void Stop()
        {
            _isRegistered = false;
        }

        // -----------------------------
        // WNDPROC HOOK
        // -----------------------------
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (!_isRegistered)
                return IntPtr.Zero;

            if (msg == WM_INPUT)
                ProcessRawInput(lParam);

            return IntPtr.Zero;
        }

        // -----------------------------
        // PROCESS RAW INPUT
        // -----------------------------
        private void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();

            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);
            if (dwSize < headerSize) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);

            try
            {
                GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, headerSize);
                if (dwSize < headerSize) return;

                RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
                if (header.dwType != RIM_TYPEMOUSE) return;

                IntPtr mousePtr = IntPtr.Add(buffer, (int)headerSize);

                int dx = Marshal.ReadInt32(mousePtr, 12);
                int dy = Marshal.ReadInt32(mousePtr, 16);

                if (dx != 0 || dy != 0)
                    MouseMoved?.Invoke(dx, dy,
                        System.Diagnostics.Stopwatch.GetTimestamp());
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
