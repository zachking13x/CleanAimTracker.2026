using System;

namespace CleanAimTracker.Services
{
    public class RawInputService
    {
        public event Action<int, int>? MouseMoved;

        public RawInputService()
        {
        }

        public void Register(IntPtr hwnd)
        {
            // placeholder for raw input registration
        }

        public void Unregister()
        {
            // placeholder
        }

        public void ProcessRawInput(int dx, int dy)
        {
            MouseMoved?.Invoke(dx, dy);
        }
    }
}
