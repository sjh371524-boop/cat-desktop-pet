using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CodexCat
{
    internal sealed class ScreenPowerTracker : IDisposable
    {
        private const int WmPowerBroadcast = 0x0218;
        private const int PbtPowerSettingChange = 0x8013;
        private const int PbtApmSuspend = 0x0004;
        private const int PbtApmResumeAutomatic = 0x0012;
        private const int DeviceNotifyWindowHandle = 0x00000000;
        private static readonly Guid ConsoleDisplayState = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        private readonly HwndSource source;
        private IntPtr registration;

        public bool IsScreenOn { get; private set; }
        public event EventHandler DisplayStateChanged;

        public ScreenPowerTracker(IntPtr windowHandle)
        {
            IsScreenOn = true;
            source = HwndSource.FromHwnd(windowHandle);
            if (source != null)
            {
                source.AddHook(WndProc);
            }
            Guid setting = ConsoleDisplayState;
            registration = RegisterPowerSettingNotification(windowHandle, ref setting, DeviceNotifyWindowHandle);
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != WmPowerBroadcast) return IntPtr.Zero;
            int eventCode = wParam.ToInt32();
            if (eventCode == PbtPowerSettingChange && lParam != IntPtr.Zero)
            {
                Guid setting = (Guid)Marshal.PtrToStructure(lParam, typeof(Guid));
                if (setting == ConsoleDisplayState)
                {
                    int dataOffset = 20;
                    int displayState = Marshal.ReadInt32(lParam, dataOffset);
                    SetScreenState(displayState != 0);
                }
            }
            else if (eventCode == PbtApmSuspend)
            {
                SetScreenState(false);
            }
            else if (eventCode == PbtApmResumeAutomatic)
            {
                SetScreenState(true);
            }
            return IntPtr.Zero;
        }

        private void SetScreenState(bool value)
        {
            if (IsScreenOn == value) return;
            IsScreenOn = value;
            if (DisplayStateChanged != null) DisplayStateChanged(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (source != null) source.RemoveHook(WndProc);
            if (registration != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(registration);
                registration = IntPtr.Zero;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid powerSettingGuid, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
    }
}

