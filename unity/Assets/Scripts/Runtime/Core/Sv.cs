// SHARED. Reading level-state values (and other loosely typed JSON-ish values) whatever form they arrive in:
// a C# value from story code, or Newtonsoft tokens from a loaded save. Plain() turns tokens into CLR data.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Gyeongbo.Core
{
    public static class Sv
    {
        // JToken -> string / bool / long / double / Dictionary<string, object> / List<object> (recursively); others unchanged.
        public static object Plain(object v)
        {
            switch (v)
            {
                case null: return null;
                case JValue jv: return jv.Value;
                case JObject o: return o.Properties().ToDictionary(p => p.Name, p => Plain(p.Value));
                case JArray a: return a.Select(Plain).ToList();
                case JToken t: return t.ToString();
                default: return v;
            }
        }

        public static string Str(object v) => Plain(v) switch { null => null, string s => s, bool b => b ? "true" : "false", IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), object o => o.ToString() };

        public static bool IsNum(object v) { var p = Plain(v); return p is IConvertible && !(p is string) && !(p is bool); }

        public static float Num(object v, float fallback = 0)
        {
            var p = Plain(v);
            if (p is string s) return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
            if (p is bool) return fallback;
            try { return p is IConvertible c ? Convert.ToSingle(c, CultureInfo.InvariantCulture) : fallback; } catch { return fallback; }
        }

        // A list of strings; a single string becomes a one-item list (JS [].concat(value)); null -> empty.
        public static List<string> StrList(object v)
        {
            var p = Plain(v);
            if (p == null) return new List<string>();
            if (p is string s) return new List<string> { s };
            if (p is IEnumerable e) return e.Cast<object>().Select(Str).ToList();
            return new List<string> { Str(p) };
        }

        public static Dictionary<string, object> Map(object v)
        {
            var p = Plain(v);
            if (p is IDictionary<string, object> d) return new Dictionary<string, object>(d);
            if (p is IDictionary g) { var r = new Dictionary<string, object>(); foreach (DictionaryEntry e in g) r[Str(e.Key)] = Plain(e.Value); return r; }
            return new Dictionary<string, object>();
        }

        public static Dictionary<string, string> StrMap(object v) => Map(v).ToDictionary(k => k.Key, k => Str(k.Value));
        public static Dictionary<string, int> IntMap(object v) => Map(v).ToDictionary(k => k.Key, k => (int)Num(k.Value));
        // { id: lines | null } (town noticeText): a null value stays null (= remove).
        public static Dictionary<string, List<string>> StrListMap(object v) => Map(v).ToDictionary(k => k.Key, k => k.Value == null ? null : StrList(k.Value));
    }
}
