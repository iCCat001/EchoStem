using System;

namespace EchoStem.Services.Win32Input
{
    public interface IWin32InputService
    {
        void SetKeyboardHook(IntPtr hwnd);
        void UnhookKeyboard();
    }
}
