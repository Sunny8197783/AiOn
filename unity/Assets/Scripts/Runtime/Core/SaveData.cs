// SHARED. Save file, checkpoint snapshot (app.js snapshot/readSnap) and G as JSON (state.js save/load).
// save.json lives in Application.persistentDataPath; Store wraps PlayerPrefs for small conveniences
// (localStorage in JS: echoes and the like). Everything read back from disk is validated before use.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gyeongbo.Logic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Gyeongbo.Core
{
    // Runtime state a checkpoint needs besides G (ctx fields), saved with G so a reload resumes the same world.
    public sealed class Snapshot
    {
        public Dictionary<string, Dictionary<string, object>> levelStates = new Dictionary<string, Dictionary<string, object>>();
        public bool? power;
        public string outdoorEnv, indoorEnv;
        public string lightSource;               // flashlight | phone | null
        public List<XZ> trail;
        public string seoyeonAsk;                // yes | no | null

        public static readonly HashSet<string> ENVS = new HashSet<string> { "day", "afternoon", "dusk", "night", "blackout", "interior", "interiorNight", "interiorDark", "underground" };

        // readSnap: keep only known keys with sane values (the file is untrusted).
        public static Snapshot Read(object raw)
        {
            var o = Sv.Map(raw is JToken || raw is IDictionary<string, object> ? raw : raw == null ? null : JToken.FromObject(raw));
            var s = new Snapshot();
            foreach (var kv in Sv.Map(o.TryGetValue("levelStates", out var ls) ? ls : null))
                if (LevelRegistry.Has(kv.Key) && kv.Value is IDictionary<string, object> m) s.levelStates[kv.Key] = new Dictionary<string, object>(m);
            if (o.TryGetValue("power", out var p) && p is bool b) s.power = b;
            if (o.TryGetValue("outdoorEnv", out var oe) && oe is string os && ENVS.Contains(os)) s.outdoorEnv = os;
            if (o.TryGetValue("indoorEnv", out var ie) && ie is string ins && ENVS.Contains(ins)) s.indoorEnv = ins;
            if (o.TryGetValue("lightSource", out var l) && (l as string == "flashlight" || l as string == "phone")) s.lightSource = (string)l;
            if (o.TryGetValue("trail", out var t) && t is List<object> tl)
                s.trail = tl.Select(Sv.Map).Where(q => q.ContainsKey("x") && q.ContainsKey("z") && Sv.IsNum(q["x"]) && Sv.IsNum(q["z"]))
                    .Select(q => new XZ(Sv.Num(q["x"]), Sv.Num(q["z"]))).Where(q => !float.IsNaN(q.x) && !float.IsInfinity(q.x) && !float.IsNaN(q.z) && !float.IsInfinity(q.z))
                    .Reverse().Take(2000).Reverse().ToList();
            if (o.TryGetValue("seoyeonAsk", out var a) && (a as string == "yes" || a as string == "no")) s.seoyeonAsk = (string)a;
            return s;
        }
    }

    public sealed class SaveFile
    {
        public int index;                        // chapter index in the runner
        public Game G;
        public Snapshot snap;
    }

    public static class GameJson
    {
        public static readonly JsonSerializerSettings SETTINGS = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
        };

        public static string Save(Game G) => JsonConvert.SerializeObject(G, SETTINGS);

        // state.js load(json): parse, turn JSON tokens in loose fields into plain data, validate (throws
        // FormatException "invalid save: ..."; "not JSON" when it does not parse).
        public static Game Load(string json)
        {
            Game G;
            try { G = JsonConvert.DeserializeObject<Game>(json, SETTINGS); }
            catch (JsonException) { throw new FormatException("invalid save: not JSON"); }
            if (G == null) throw new FormatException("invalid save: not a v2 game");
            Normalize(G);
            return State.Load(G);
        }

        static void Normalize(Game G)
        {
            foreach (var e in G.events ?? new List<GameEvent>())
                if (e?.details != null) foreach (var k in e.details.Keys.ToList()) e.details[k] = Sv.Plain(e.details[k]);
            foreach (var runs in (G.skill ?? new Dictionary<string, List<SkillRun>>()).Values)
                foreach (var r in runs ?? new List<SkillRun>()) if (r != null) r.result = Sv.Plain(r.result);
            if (G.checkpoint?.state != null) Normalize(G.checkpoint.state);
        }
    }

    public static class SaveStore
    {
        static string PathOf => Path.Combine(Application.persistentDataPath, "save.json");

        // null when there is no save or it does not parse (validation of G happens in GameJson.Load).
        public static JObject ReadRaw()
        {
            try { return File.Exists(PathOf) ? JObject.Parse(File.ReadAllText(PathOf)) : null; }
            catch (Exception e) { Debug.LogWarning("[save] unreadable save.json: " + e.Message); return null; }
        }

        public static void Write(SaveFile f)
        {
            try { WriteAtomic(PathOf, JsonConvert.SerializeObject(f, GameJson.SETTINGS)); }
            catch (Exception e) { Debug.LogError("[save] not saved: " + e.Message); }
        }

        public static void Clear()
        {
            try { if (File.Exists(PathOf)) File.Delete(PathOf); }
            catch (Exception e) { Debug.LogWarning("[save] not cleared: " + e.Message); }
        }

        // Write to a temp file, then replace, so a crash mid-write never leaves half a save.
        public static void WriteAtomic(string path, string text)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }
    }

    // localStorage stand-in (per-machine conveniences). Every access may fail; the game runs without it.
    public static class Store
    {
        public static string Get(string key) { try { return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null; } catch { return null; } }
        public static void Set(string key, string value) { try { PlayerPrefs.SetString(key, value ?? ""); PlayerPrefs.Save(); } catch { /* not saved */ } }
    }
}
