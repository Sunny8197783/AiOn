// Ports app.js: boot, settings and experience modes (PROFILES), save/continue, the chapter runner with soft-fail
// retries from the last checkpoint, global keys while roaming (F flashlight, V view, Tab notebook, I inventory,
// H controls, X first aid, B chalk, P phone, M map, Esc pause), falls and fights (Actions, Danger), echoes,
// stamina/compass/minimap per frame, footsteps, the wheelchair stair help, the guardian who walks along.
// Split into more App/*.cs files as the port grows (each < 500 lines).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Story;
using UnityEngine;

namespace Gyeongbo.App
{
    public sealed class GameApp : MonoBehaviour
    {
        // The chapter runner's order: [id, chapter]. Every chapter resolves null except 'ending'.
        public static readonly List<KeyValuePair<string, Func<Ctx, Task<string>>>> CHAPTERS = new List<KeyValuePair<string, Func<Ctx, Task<string>>>>
        {
            new KeyValuePair<string, Func<Ctx, Task<string>>>("prologue", Prologue.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("prep", Prep.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("dday", Dday.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("endure", Endure.Run),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("crossroads", Evac.Crossroads),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("evac", Evac.Evacuation),
            new KeyValuePair<string, Func<Ctx, Task<string>>>("ending", Evac.Ending),
        };

        // Experience modes (체험 모드): how the body moves and what the senses get; speed multiplies the story's speed.
        public static readonly Dictionary<string, Profile> PROFILES = new Dictionary<string, Profile>
        {
            ["none"] = new Profile { run = true, jump = true, eye = 1.62f, stairs = true, speed = 1 },
            ["mobility"] = new Profile { run = false, jump = false, eye = 1.62f, stairs = true, speed = 0.62f },
            ["wheelchair"] = new Profile { run = false, jump = false, eye = 1.18f, stairs = false, speed = 0.85f },
            ["lowvision"] = new Profile { run = true, jump = true, eye = 1.62f, stairs = true, speed = 0.8f },
            ["deaf"] = new Profile { run = true, jump = true, eye = 1.62f, stairs = true, speed = 1 },
            ["guardian"] = new Profile { run = false, jump = false, eye = 1.62f, stairs = true, speed = 0.72f },
        };

        // Unity entry point: everything else is awaited inside Run; nothing escapes unlogged.
        async void Start()
        {
            try { await Run(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                try { UI.UiRoot.ShowLoading("문제가 생겨 게임을 계속할 수 없습니다. 다시 시작해 주세요."); } catch (Exception e2) { Debug.LogException(e2); }
            }
        }

        // app.js boot(): landing -> new or continue -> intro -> run chapters -> back to the landing.
        public Task Run() => throw new NotImplementedException("port: app.js:boot");
    }
}
