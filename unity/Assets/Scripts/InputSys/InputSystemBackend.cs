// Input System backend for Engine.GameInput: maps JS KeyboardEvent.code names to Keyboard keys, mouse buttons,
// delta and scroll. Registered before the first scene loads. Not compiled by Tools~/check.sh: keep it plain.
using System;
using Gyeongbo.Engine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gyeongbo.InputSys
{
    public sealed class InputSystemBackend : IInputBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => GameInput.Backend = new InputSystemBackend();

        public bool Held(string code) => throw new NotImplementedException("port: Keyboard.current[key].isPressed");
        public bool Down(string code) => throw new NotImplementedException("port: wasPressedThisFrame");
        public bool Up(string code) => throw new NotImplementedException("port: wasReleasedThisFrame");
        public bool MouseHeld(int button) => throw new NotImplementedException("port: Mouse.current buttons");
        public bool MouseDown(int button) => throw new NotImplementedException("port: Mouse.current buttons");
        public Vector2 MouseDelta => throw new NotImplementedException("port: Mouse.current.delta");
        public Vector2 MousePosition => throw new NotImplementedException("port: Mouse.current.position");
        public Vector2 Scroll => throw new NotImplementedException("port: Mouse.current.scroll");
        public string TypedText => throw new NotImplementedException("port: Keyboard.onTextInput");
    }
}
