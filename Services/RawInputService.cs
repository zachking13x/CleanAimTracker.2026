using System;
using System.Runtime.InteropServices;

namespace CleanAimTracker.Services
{
    public class RawInputService
    {
        public event Action<int, int>? MouseMoved;

        // -----------------------------
        // RAWINPUT STRUCTS
        // -----------------------------
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

        // -----------------------------
        // RAWINPUTDEVICE STRUCT
        // -----------------------------
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public int dwFlags;
            public IntPtr hwndTarget;
        }

        // -----------------------------
        // CONSTANTS
        // -----------------------------
        private const int RIM_TYPEMOUSE = 0;
        private const int RID_INPUT = 0x10000003;
        private const int RIDEV_INPUTSINK = 0x00000100;

        // -----------------------------
        // P/INVOKE
        // -----------------------------
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
        // REGISTER
        // -----------------------------
        public void Register(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] rid =
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
                (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        // -----------------------------
        // PROCESS RAW INPUT (SAFE)
        // -----------------------------
        public (int dx, int dy) ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            uint headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();

            // 1) Ask Windows how big the RAWINPUT packet is
            uint res = GetRawInputData(
                lParam,
                RID_INPUT,
                IntPtr.Zero,
                ref dwSize,
                headerSize);

            if (dwSize < headerSize)
                return (0, 0);

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);

            try
            {
                // 2) Read the actual data
                res = GetRawInputData(
                    lParam,
                    RID_INPUT,
                    buffer,
                    ref dwSize,
                    headerSize);

                if (dwSize < headerSize)
                    return (0, 0);

                // 3) Read header
                RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);

                if (header.dwType != RIM_TYPEMOUSE)
                    return (0, 0);

                // 4) Compute pointer to mouse data
                IntPtr mousePtr = IntPtr.Add(buffer, (int)headerSize);

                // 5) SAFELY read only lLastX and lLastY
                // RAWMOUSE layout:
                // offset 0:  usFlags (2 bytes)
                // offset 2:  padding (2 bytes)
                // offset 4:  ulButtons (4 bytes)
                // offset 8:  usButtonFlags (2 bytes)
                // offset 10: usButtonData (2 bytes)
                // offset 12: lLastX (4 bytes)
                // offset 16: lLastY (4 bytes)

                int lLastX = Marshal.ReadInt32(mousePtr, 12);
                int lLastY = Marshal.ReadInt32(mousePtr, 16);

                MouseMoved?.Invoke(lLastX, lLastY);
                return (lLastX, lLastY);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
