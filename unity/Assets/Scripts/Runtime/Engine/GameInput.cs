// Keyboard/mouse for the whole game, by JS KeyboardEvent.code names ("KeyW", "ArrowUp", "ShiftLeft",
// "Space", "Enter", "NumpadEnter", "Escape", "Tab", "KeyE", "Digit1"...). The Input System assembly registers
// GameInput.Backend from BeforeSceneLoad; without it the legacy Input manager is used
// (ENABLE_LEGACY_INPUT_MANAGER). PORTING.md "Versions and packages".
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public interface IInputBackend
    {
        bool Held(string code);
        bool Down(string code);              // pressed this frame
        bool Up(string code);
        bool MouseHeld(int button);          // 0 left, 1 right, 2 middle
        bool MouseDown(int button);
        Vector2 MouseDelta { get; }          // pixels this frame
        Vector2 MousePosition { get; }       // pixels, origin bottom-left (Unity)
        Vector2 Scroll { get; }
        string TypedText { get; }            // characters typed this frame (UI text fields use UI Toolkit instead)
    }

    public static class GameInput
    {
        public static IInputBackend Backend;

        public static bool Held(string code) => Backend != null ? Backend.Held(code) : Legacy(code, 0);
        public static bool Down(string code) => Backend != null ? Backend.Down(code) : Legacy(code, 1);
        public static bool Up(string code) => Backend != null ? Backend.Up(code) : Legacy(code, 2);
        public static bool MouseHeld(int button) => Backend != null ? Backend.MouseHeld(button) : LegacyMouse(button, false);
        public static bool MouseDown(int button) => Backend != null ? Backend.MouseDown(button) : LegacyMouse(button, true);
        // Pixels this frame, y up (Unity screen convention; JS movementY is the negative).
        public static Vector2 MouseDelta => Backend != null ? Backend.MouseDelta : LegacyDelta();
        public static Vector2 MousePosition => Backend != null ? Backend.MousePosition : LegacyPosition();
        // Pointer lock (cursor hidden and confined while looking around).
        public static bool Locked => Cursor.lockState == CursorLockMode.Locked;
        public static void SetLocked(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // JS KeyboardEvent.code -> the Input System Key enum name ("KeyA" -> "A", "ArrowUp" -> "UpArrow"); null if unknown.
        public static string KeyName(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal)) return code.Substring(3);
            if (code.StartsWith("Digit", StringComparison.Ordinal) || (code.Length == 7 && code.StartsWith("Numpad", StringComparison.Ordinal))) return code;
            if (code.Length <= 3 && code[0] == 'F' && code.Length > 1 && char.IsDigit(code[1])) return code;
            return NAMES.TryGetValue(code, out var n) ? n : null;
        }

        static readonly Dictionary<string, string> NAMES = new Dictionary<string, string>
        {
            ["ArrowUp"] = "UpArrow", ["ArrowDown"] = "DownArrow", ["ArrowLeft"] = "LeftArrow", ["ArrowRight"] = "RightArrow",
            ["ShiftLeft"] = "LeftShift", ["ShiftRight"] = "RightShift", ["ControlLeft"] = "LeftCtrl", ["ControlRight"] = "RightCtrl",
            ["AltLeft"] = "LeftAlt", ["AltRight"] = "RightAlt", ["MetaLeft"] = "LeftMeta", ["MetaRight"] = "RightMeta",
            ["Space"] = "Space", ["Enter"] = "Enter", ["Tab"] = "Tab", ["Escape"] = "Escape", ["Backspace"] = "Backspace",
            ["Delete"] = "Delete", ["Insert"] = "Insert", ["Home"] = "Home", ["End"] = "End", ["PageUp"] = "PageUp", ["PageDown"] = "PageDown",
            ["CapsLock"] = "CapsLock", ["Backquote"] = "Backquote", ["Quote"] = "Quote", ["Semicolon"] = "Semicolon", ["Comma"] = "Comma",
            ["Period"] = "Period", ["Slash"] = "Slash", ["Backslash"] = "Backslash", ["Minus"] = "Minus", ["Equal"] = "Equals",
            ["BracketLeft"] = "LeftBracket", ["BracketRight"] = "RightBracket",
            ["NumpadEnter"] = "NumpadEnter", ["NumpadAdd"] = "NumpadPlus", ["NumpadSubtract"] = "NumpadMinus", ["NumpadMultiply"] = "NumpadMultiply",
            ["NumpadDivide"] = "NumpadDivide", ["NumpadDecimal"] = "NumpadPeriod", ["NumpadEqual"] = "NumpadEquals",
        };

#if ENABLE_LEGACY_INPUT_MANAGER
        static readonly Dictionary<string, KeyCode?> codes = new Dictionary<string, KeyCode?>();
        static readonly Dictionary<string, KeyCode> LEGACY = new Dictionary<string, KeyCode>
        {
            ["Enter"] = KeyCode.Return, ["LeftCtrl"] = KeyCode.LeftControl, ["RightCtrl"] = KeyCode.RightControl,
            ["LeftMeta"] = KeyCode.LeftWindows, ["RightMeta"] = KeyCode.RightWindows, ["Backquote"] = KeyCode.BackQuote,
            ["NumpadEnter"] = KeyCode.KeypadEnter, ["NumpadPlus"] = KeyCode.KeypadPlus, ["NumpadMinus"] = KeyCode.KeypadMinus,
            ["NumpadMultiply"] = KeyCode.KeypadMultiply, ["NumpadDivide"] = KeyCode.KeypadDivide, ["NumpadPeriod"] = KeyCode.KeypadPeriod,
            ["NumpadEquals"] = KeyCode.KeypadEquals,
        };
        static KeyCode? CodeOf(string code)
        {
            if (code == null) return null;
            if (codes.TryGetValue(code, out var kc)) return kc;
            var n = KeyName(code);
            KeyCode? r = null;
            if (n != null)
            {
                if (LEGACY.TryGetValue(n, out var k)) r = k;
                else if (n.StartsWith("Digit", StringComparison.Ordinal)) r = KeyCode.Alpha0 + (n[5] - '0');
                else if (n.StartsWith("Numpad", StringComparison.Ordinal) && n.Length == 7) r = KeyCode.Keypad0 + (n[6] - '0');
                else if (Enum.TryParse<KeyCode>(n, out var e)) r = e;
            }
            return codes[code] = r;
        }
        static bool Legacy(string code, int how)
        {
            var k = CodeOf(code); if (k == null) return false;
            return how == 0 ? Input.GetKey(k.Value) : how == 1 ? Input.GetKeyDown(k.Value) : Input.GetKeyUp(k.Value);
        }
        static bool LegacyMouse(int b, bool down) => b >= 0 && b <= 2 && (down ? Input.GetMouseButtonDown(b) : Input.GetMouseButton(b));
        // The default InputManager "Mouse X/Y" axes are pixels * 0.1.
        static Vector2 LegacyDelta()
        {
            try { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f; }
            catch (ArgumentException) { return Vector2.zero; }
        }
        static Vector2 LegacyPosition() => Input.mousePosition;
        public static Vector2 Scroll => Backend != null ? Backend.Scroll : Input.mouseScrollDelta;
        public static string TypedText => Backend != null ? Backend.TypedText : Input.inputString;
#else
        static bool Legacy(string code, int how) => false;
        static bool LegacyMouse(int b, bool down) => false;
        static Vector2 LegacyDelta() => Vector2.zero;
        static Vector2 LegacyPosition() => Vector2.zero;
        public static Vector2 Scroll => Backend != null ? Backend.Scroll : Vector2.zero;
        public static string TypedText => Backend != null ? Backend.TypedText : "";
#endif
    }
}
