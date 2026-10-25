// Small helpers shared by the storyB chapters (Dday, Seoyeon, Endure, Evac): JS literals as C# objects.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.Story
{
    static class SB
    {
        // A JS number in a template string (64, 12.5).
        public static string N(double n) => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        public static List<string> L(params string[] a) => new List<string>(a);
        public static Choice C(string id, string label, bool primary = false) => new Choice(id, label, primary);
        public static List<Choice> Cs(params Choice[] a) => new List<Choice>(a);
        // fact(id).text
        public static string F(string id) => Facts.Fact(id)?.text;
        // G.items[id]?.home ?? 0 / ?.bag ?? 0
        public static int Home(Game G, string id) => G.items.TryGetValue(id, out var e) && e != null ? e.home : 0;
        public static int Bag(Game G, string id) => G.items.TryGetValue(id, out var e) && e != null ? e.bag : 0;
        // G.items[id] ??= { home: 0, bag: 0, bought: 0, checked: false, used: 0 }
        public static ItemEntry Entry(Game G, string id)
        {
            if (!G.items.TryGetValue(id, out var e) || e == null) G.items[id] = e = new ItemEntry();
            return e;
        }
        // JS truthiness of G.rel[who].contact
        public static bool Contact(Game G, string who) => G.rel.TryGetValue(who, out var r) && r != null && !string.IsNullOrEmpty(r.contact);

        public static Task<string> Talk(Ctx ctx, string speaker, string role, List<string> lines, List<Choice> choices = null) =>
            ctx.ui.Talk(new TalkOptions { speaker = speaker, role = role, lines = lines, choices = choices });

        public static CardOptions Card(string kind, string title, string meta, List<string> body, List<Choice> actions) =>
            new CardOptions { kind = kind, title = title, meta = meta, body = body, actions = actions };

        // A roam handler with no answer (JS async () => { ...; } resolves undefined).
        public static Func<Point, Task<object>> H(Func<Point, Task> fn) => async p => { await fn(p); return null; };
    }
}
