using System;
using System.Runtime.InteropServices;

namespace CleanAimTracker.Services
{
    public class RawInputService
    {
        public event Action<int, int>? MouseMoved;

        // ─────────────────────────────────────────────────────────
        //  RAWINPUT structures
        // ─────────────────────────────────────────────────────────
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
        private struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        // ─────────────────────────────────────────────────────────
        //  RAWINPUTDEVICE registration
        // ─────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public int dwFlags;
            public IntPtr hwndTarget;
        }

        private const int RIM_TYPEMOUSE = 0;
        private const int RID_INPUT = 0x10000003;
        private const int RIDEV_INPUTSINK = 0x00000100;

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

        // ─────────────────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────────────────
        public RawInputService() { }

        // ─────────────────────────────────────────────────────────
        //  Register for raw mouse input
        // ─────────────────────────────────────────────────────────
        public void Register(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,   // Generic desktop controls
                    usUsage = 0x02,       // Mouse
                    dwFlags = RIDEV_INPUTSINK,
                    hwndTarget = hwnd
                }
            };

            RegisterRawInputDevices(
                rid,
                (uint)rid.Length,
                (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))
            );
        }

        public void Unregister() { }

        // ─────────────────────────────────────────────────────────
        //  Parse WM_INPUT → return dx/dy
        // ─────────────────────────────────────────────────────────
        public (int dx, int dy) ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;

            // First call: get size
            GetRawInputData(
                lParam,
                RID_INPUT,
                IntPtr.Zero,
                ref dwSize,
                (uint)Marshal.SizeOf<RAWINPUTHEADER>()
            );

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);

            try
            {
                // Second call: get actual data
                if (GetRawInputData(
                        lParam,
                        RID_INPUT,
                        buffer,
                        ref dwSize,
                        (uint)Marshal.SizeOf<RAWINPUTHEADER>()
                    ) != dwSize)
                {
                    return (0, 0);
                }

                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                if (raw.header.dwType == RIM_TYPEMOUSE)
                {
                    int dx = raw.mouse.lLastX;
                    int dy = raw.mouse.lLastY;

                    MouseMoved?.Invoke(dx, dy);
                    return (dx, dy);
                }

                return (0, 0);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
