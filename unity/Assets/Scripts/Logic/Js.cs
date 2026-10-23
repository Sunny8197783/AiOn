// JS semantics the ported game/*.js code relies on: Math.round, number-to-string, truthiness,
// reading a key off a loose value (eval object, loaded JSON dictionary) and structuredClone of plain data.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Gyeongbo.Logic
{
    internal static class Js
    {
        // Math.round: halves round up (C# Math.Round is banker's rounding).
        public static double Round(double x) => Math.Floor(x + 0.5);

        // String(n) / `${n}` for a number.
        public static string Num(double n) =>
            double.IsNaN(n) ? "NaN" : double.IsPositiveInfinity(n) ? "Infinity" : double.IsNegativeInfinity(n) ? "-Infinity"
            : n.ToString("R", CultureInfo.InvariantCulture);

        // `${v}` for a loose value (null renders empty rather than "undefined" in player text).
        public static string Str(object v) => v switch
        {
            null => "",
            string s => s,
            bool b => b ? "true" : "false",
            _ when IsNumber(v) => Num(Convert.ToDouble(v, CultureInfo.InvariantCulture)),
            _ => v.ToString(),
        };

        public static bool Truthy(object v) => v switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            _ when IsNumber(v) => Convert.ToDouble(v, CultureInfo.InvariantCulture) is var d && d != 0 && !double.IsNaN(d),
            _ => true,
        };

        public static bool IsNumber(object v) => v is int || v is long || v is double || v is float || v is decimal
            || v is short || v is byte || v is sbyte || v is ushort || v is uint || v is ulong;

        // v?.[key]: a dictionary entry or a public field/property of an object; null when absent.
        public static object Get(object v, string key)
        {
            switch (v)
            {
                case null: return null;
                case string _: return null;
                case IDictionary<string, object> d: return d.TryGetValue(key, out var x) ? x : null;
                case IDictionary nd: return nd.Contains(key) ? nd[key] : null;
            }
            var t = v.GetType();
            var f = t.GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(v);
            var p = t.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
            return p != null && p.GetIndexParameters().Length == 0 ? p.GetValue(v) : null;
        }

        // structuredClone for the game's plain data: public fields, lists, dictionaries and arrays are copied
        // deeply; strings and value types are immutable. Anything else (no parameterless constructor, other
        // collections) is shared, which game data never holds.
        public static object Copy(object o)
        {
            if (o == null || o is string || o is ValueType) return o;
            var t = o.GetType();
            if (o is Array a)
            {
                var r = (Array)a.Clone();
                for (int i = 0; i < r.Length; i++) r.SetValue(Copy(a.GetValue(i)), i);
                return r;
            }
            if (t.GetConstructor(Type.EmptyTypes) == null) return o;
            if (o is IDictionary d)
            {
                var r = (IDictionary)Activator.CreateInstance(t);
                foreach (DictionaryEntry e in d) r[e.Key] = Copy(e.Value);
                return r;
            }
            if (o is IList l)
            {
                var r = (IList)Activator.CreateInstance(t);
                foreach (var x in l) r.Add(Copy(x));
                return r;
            }
            if (o is IEnumerable) return o;
            var c = Activator.CreateInstance(t);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) f.SetValue(c, Copy(f.GetValue(o)));
            return c;
        }
    }
}
