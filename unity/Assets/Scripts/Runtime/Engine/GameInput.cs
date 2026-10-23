// Keyboard/mouse for the whole game, by JS KeyboardEvent.code names ("KeyW", "ArrowUp", "ShiftLeft",
// "Space", "Enter", "NumpadEnter", "Escape", "Tab", "KeyE", "Digit1"...). The Input System assembly registers
// GameInput.Backend from BeforeSceneLoad; without it the legacy Input manager is used
// (ENABLE_LEGACY_INPUT_MANAGER). PORTING.md "Versions and packages".
using System;
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

        public static bool Held(string code) => throw new NotImplementedException("port: keyboard state (controls.js keys)");
        public static bool Down(string code) => throw new NotImplementedException("port: keydown");
        public static bool Up(string code) => throw new NotImplementedException("port: keyup");
        public static bool MouseHeld(int button) => throw new NotImplementedException("port: pointer buttons");
        public static bool MouseDown(int button) => throw new NotImplementedException("port: pointerdown");
        public static Vector2 MouseDelta => throw new NotImplementedException("port: pointermove movementX/Y");
        public static Vector2 MousePosition => throw new NotImplementedException("port: pointer position");
        // Pointer lock (cursor hidden and confined while looking around).
        public static bool Locked => throw new NotImplementedException("port: document.pointerLockElement");
        public static void SetLocked(bool on) => throw new NotImplementedException("port: requestPointerLock / exitPointerLock");
    }
}
