// Input System backend for Engine.GameInput: maps JS KeyboardEvent.code names to Keyboard keys, mouse buttons,
// delta and scroll. Registered before the first scene loads. Not compiled by Tools~/check.sh: keep it plain.
using System;
using System.Collections.Generic;
using System.Text;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Gyeongbo.InputSys
{
    public sealed class InputSystemBackend : IInputBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            // Active Input Handling = "Input Manager (Old)" leaves Keyboard.current null: keep the legacy fallback then.
#if ENABLE_INPUT_SYSTEM
            GameInput.Backend = new InputSystemBackend();
#endif
        }

        readonly Dictionary<string, Key> keys = new Dictionary<string, Key>();
        Keyboard typedFrom;
        readonly StringBuilder typed = new StringBuilder();
        int typedFrame = -1;

        KeyControl Control(string code)
        {
            var kb = Keyboard.current;
            if (kb == null || code == null) return null;
            if (!keys.TryGetValue(code, out var k))
            {
                var n = GameInput.KeyName(code);
                k = n != null && Enum.TryParse(n, out Key parsed) ? parsed : Key.None;
                keys[code] = k;
            }
            return k == Key.None ? null : kb[k];
        }

        public bool Held(string code) => Control(code)?.isPressed ?? false;
        public bool Down(string code) => Control(code)?.wasPressedThisFrame ?? false;
        public bool Up(string code) => Control(code)?.wasReleasedThisFrame ?? false;

        static ButtonControl Button(int b)
        {
            var m = Mouse.current;
            if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : b == 2 ? m.middleButton : null;
        }
        public bool MouseHeld(int button) => Button(button)?.isPressed ?? false;
        public bool MouseDown(int button) => Button(button)?.wasPressedThisFrame ?? false;
        public Vector2 MouseDelta => Mouse.current?.delta.ReadValue() ?? Vector2.zero;
        public Vector2 MousePosition => Mouse.current?.position.ReadValue() ?? Vector2.zero;
        // InputSettings.scrollDeltaBehavior defaults to UniformAcrossAllPlatforms: 1 per notch, like Input.mouseScrollDelta.
        public Vector2 Scroll => Mouse.current?.scroll.ReadValue() ?? Vector2.zero;

        public string TypedText
        {
            get
            {
                var kb = Keyboard.current;
                if (kb != typedFrom)
                {
                    if (typedFrom != null) typedFrom.onTextInput -= OnText;
                    typedFrom = kb;
                    if (kb != null) kb.onTextInput += OnText;
                }
                return typedFrame == Time.frameCount ? typed.ToString() : "";
            }
        }

        void OnText(char c)
        {
            if (typedFrame != Time.frameCount) { typed.Clear(); typedFrame = Time.frameCount; }
            typed.Append(c);
        }
    }
}
