// Stand-in for the NUnit subset the EditMode tests use, so check.sh can run them on
// plain .NET without Unity. Unity itself uses the real NUnit; keep tests to this subset.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute { }
    public delegate void TestDelegate();
    public sealed class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public static class Assert
    {
        static void Check(bool ok, string what, string msg) { if (!ok) throw new AssertionException(what + (msg == null ? "" : " — " + msg)); }
        static string S(object o) => o == null ? "null" : o is string s ? "\"" + s + "\"" : o.ToString();
        public static void AreEqual(object expected, object actual, string msg = null) =>
            Check(IsNum(expected) && IsNum(actual) ? Convert.ToDouble(expected) == Convert.ToDouble(actual) : Equals(expected, actual), $"expected {S(expected)} but was {S(actual)}", msg);
        public static void AreEqual(double expected, double actual, double delta, string msg = null) =>
            Check(Math.Abs(expected - actual) <= delta, $"expected {expected} ±{delta} but was {actual}", msg);
        public static void AreNotEqual(object a, object b, string msg = null) => Check(!Equals(a, b), $"did not expect {S(a)}", msg);
        public static void IsTrue(bool c, string msg = null) => Check(c, "expected true", msg);
        public static void IsFalse(bool c, string msg = null) => Check(!c, "expected false", msg);
        public static void IsNull(object o, string msg = null) => Check(o == null, $"expected null but was {S(o)}", msg);
        public static void IsNotNull(object o, string msg = null) => Check(o != null, "expected not null", msg);
        public static void Greater(double a, double b, string msg = null) => Check(a > b, $"expected {a} > {b}", msg);
        public static void GreaterOrEqual(double a, double b, string msg = null) => Check(a >= b, $"expected {a} >= {b}", msg);
        public static void Less(double a, double b, string msg = null) => Check(a < b, $"expected {a} < {b}", msg);
        public static void LessOrEqual(double a, double b, string msg = null) => Check(a <= b, $"expected {a} <= {b}", msg);
        public static void Contains(object expected, ICollection actual, string msg = null) => Check(actual.Cast<object>().Contains(expected), $"expected collection to contain {S(expected)}", msg);
        public static void Fail(string msg = null) => throw new AssertionException(msg ?? "failed");
        public static T Throws<T>(TestDelegate code, string msg = null) where T : Exception
        {
            try { code(); } catch (T e) { return e; } catch (Exception e) { Fail($"expected {typeof(T).Name} but got {e.GetType().Name}: {e.Message}"); }
            Fail($"expected {typeof(T).Name}" + (msg == null ? "" : " — " + msg)); return null;
        }
        static bool IsNum(object o) => o is int || o is long || o is float || o is double || o is decimal || o is short || o is byte;
    }

    public static class StringAssert
    {
        public static void Contains(string expected, string actual, string msg = null) =>
            Assert.IsTrue(actual != null && actual.Contains(expected), $"expected \"{actual}\" to contain \"{expected}\"" + (msg == null ? "" : " — " + msg));
    }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable expected, IEnumerable actual, string msg = null)
        {
            var e = expected.Cast<object>().ToList(); var a = actual.Cast<object>().ToList();
            Assert.AreEqual(e.Count, a.Count, "count" + (msg == null ? "" : " — " + msg));
            for (int i = 0; i < e.Count; i++) Assert.AreEqual(e[i], a[i], $"item {i}" + (msg == null ? "" : " — " + msg));
        }
        public static void Contains(IEnumerable collection, object expected, string msg = null) => Assert.Contains(expected, collection.Cast<object>().ToList(), msg);
    }

    static class ShimRunner
    {
        static int Main()
        {
            int pass = 0, fail = 0;
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetMethods().Any(m => m.IsDefined(typeof(TestAttribute)))).OrderBy(t => t.FullName))
            {
                var setUp = t.GetMethods().FirstOrDefault(m => m.IsDefined(typeof(SetUpAttribute)));
                foreach (var m in t.GetMethods().Where(m => m.IsDefined(typeof(TestAttribute))))
                {
                    var inst = m.IsStatic ? null : Activator.CreateInstance(t);
                    try { setUp?.Invoke(inst, null); m.Invoke(inst, null); pass++; }
                    catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {t.Name}.{m.Name}: {e.InnerException?.GetType().Name}: {e.InnerException?.Message}"); }
                }
            }
            Console.WriteLine($"tests: {pass} passed, {fail} failed");
            return fail == 0 ? 0 : 1;
        }
    }
}
