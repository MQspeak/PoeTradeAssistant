using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Poe2MarketScanner.Core.Configuration;

namespace Poe2MarketScanner.App.Services;

public sealed class WindowsInputAutomationRunner : IInputAutomationRunner
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VkControl = 0x11;
    private const ushort VkA = 0x41;
    private const ushort VkBack = 0x08;

    public async Task ClickAsync(double x, double y, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
        await Task.Delay(80, cancellationToken);

        SendMouse(MouseEventLeftDown);
        await Task.Delay(30, cancellationToken);
        SendMouse(MouseEventLeftUp);
        await Task.Delay(80, cancellationToken);
    }

    public async Task ClickWithModifiersAsync(
        double x,
        double y,
        IReadOnlyCollection<InputModifierKey> modifierKeys,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keys = modifierKeys?
            .Distinct()
            .Select(MapModifierKey)
            .Where(key => key != 0)
            .ToList() ?? new List<ushort>();

        SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
        await Task.Delay(80, cancellationToken);

        foreach (var key in keys)
        {
            SendKeyDown(key);
            await Task.Delay(20, cancellationToken);
        }

        SendMouse(MouseEventLeftDown);
        await Task.Delay(30, cancellationToken);
        SendMouse(MouseEventLeftUp);
        await Task.Delay(30, cancellationToken);

        for (var i = keys.Count - 1; i >= 0; i--)
        {
            SendKeyUp(keys[i]);
            await Task.Delay(20, cancellationToken);
        }

        await Task.Delay(80, cancellationToken);
    }

    public async Task CtrlClickAsync(double x, double y, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
        await Task.Delay(80, cancellationToken);

        SendKeyDown(VkControl);
        await Task.Delay(30, cancellationToken);
        SendMouse(MouseEventLeftDown);
        await Task.Delay(30, cancellationToken);
        SendMouse(MouseEventLeftUp);
        await Task.Delay(30, cancellationToken);
        SendKeyUp(VkControl);
        await Task.Delay(80, cancellationToken);
    }

    public Task SendSelectAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendChord(VkControl, VkA);
        return Task.CompletedTask;
    }

    public Task SendBackspaceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SendKey(VkBack);
        return Task.CompletedTask;
    }

    public Task PasteTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SendUnicodeTextAsync(text ?? string.Empty, cancellationToken);
    }

    public Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
    {
        return Task.Delay(Math.Max(0, milliseconds), cancellationToken);
    }

    private static void SendMouse(uint mouseFlags)
    {
        var input = new INPUT
        {
            type = InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = mouseFlags
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendChord(ushort modifier, ushort key)
    {
        SendKeyDown(modifier);
        SendKeyDown(key);
        SendKeyUp(key);
        SendKeyUp(modifier);
    }

    private static void SendKey(ushort key)
    {
        SendKeyDown(key);
        SendKeyUp(key);
    }

    private static void SendKeyDown(ushort key)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendKeyUp(ushort key)
    {
        var input = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key,
                    dwFlags = KeyEventKeyUp
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static async Task SendUnicodeTextAsync(string text, CancellationToken cancellationToken)
    {
        foreach (var character in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendUnicodeCharacter(character);
            await Task.Delay(12, cancellationToken);
        }
    }

    private static void SendUnicodeCharacter(char character)
    {
        var down = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wScan = character,
                    dwFlags = KeyEventUnicode
                }
            }
        };

        var up = new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wScan = character,
                    dwFlags = KeyEventUnicode | KeyEventKeyUp
                }
            }
        };

        SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
    }

    private static ushort MapModifierKey(InputModifierKey modifierKey)
    {
        return modifierKey switch
        {
            InputModifierKey.Control => VkControl,
            InputModifierKey.Shift => 0x10,
            InputModifierKey.Alt => 0x12,
            _ => 0
        };
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
}

