using System;

namespace CleanAimTracker.Services
{
    public class RawInputService
    {
        public event Action<int, int>? MouseMoved;

        public RawInputService()
        {
        }

        public void Register()
        {
            // TODO: Add actual raw input registration later
        }

        public void Unregister()
        {
            // TODO: Add raw input unregistration later
        }

        public void ProcessRawInput(int deltaX, int deltaY)
        {
            MouseMoved?.Invoke(deltaX, deltaY);
        }
    }
}
