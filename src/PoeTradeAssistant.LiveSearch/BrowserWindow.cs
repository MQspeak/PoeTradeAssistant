using System.Runtime.InteropServices;
using System.Text;

namespace PoeTradeAssistant.LiveSearch;

internal sealed class BrowserWindow
{
    private nint _handle;
    private uint _process;
    public bool Hide(string marker)
    {
        if (!OperatingSystem.IsWindows()) return false;
        EnumWindows((handle, _) =>
        {
            var title = new StringBuilder(1024);
            GetWindowText(handle, title, title.Capacity);
            if (!title.ToString().Contains(marker, StringComparison.Ordinal)) return true;
            _handle = handle;
            GetWindowThreadProcessId(handle, out _process);
            return false;
        }, 0);
        if (_handle == 0) return false;
        ShowWindowAsync(_handle, 0);
        return true;
    }
    public void Show()
    {
        if (_handle == 0) return;
        GetWindowThreadProcessId(_handle, out var process);
        if (process == _process) ShowWindowAsync(_handle, 9);
        _handle = 0;
    }
    private delegate bool WindowCallback(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint handle, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint process);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(nint handle, int command);
}
