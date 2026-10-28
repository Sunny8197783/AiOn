// ui/dom.js icon(): 24x24 stroke icons (fill none, stroke currentColor, width 2, round caps and joins), drawn
// from the same SVG path data with Painter2D. Size = em * the inherited font size (.icon { width: 1.15em }).
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public sealed class UiIcon : VisualElement
    {
        public static readonly Dictionary<string, string> P = new Dictionary<string, string>
        {
            ["heart"] = "M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z",
            ["drop"] = "M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z",
            ["moon"] = "M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z",
            ["pulse"] = "M3 12h4l2-5 4 10 2-5h6",
            ["battery"] = "M3 8h14a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1zM21 11v2",
            ["bag"] = "M8 8V6a4 4 0 0 1 8 0v2M5 10a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2zM9 14h6",
            ["clock"] = "M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0zM12 7v5l3 2",
            ["check"] = "M5 12.5l4.5 4.5L19 7.5",
            ["circle"] = "M17 12a5 5 0 1 1-10 0 5 5 0 0 1 10 0z",
            ["official"] = "M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6zM8.5 12l2.5 2.5 4.5-5",
            ["site"] = "M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z",
            ["neighbor"] = "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21v-1a6 6 0 0 1 12 0v1M16 3.5a4 4 0 0 1 0 7.5M22 21v-1a6 6 0 0 0-4-5.6",
            ["rumor"] = "M21 12a8 8 0 0 1-11.6 7.1L4 20l1.1-4.6A8 8 0 1 1 21 12zM9.8 9.5a2.3 2.3 0 0 1 4.4.8c0 1.5-2.2 2-2.2 2M12 15.5h.01",
            ["self"] = "M4 20h4L19 9l-4-4L4 16zM13.5 6.5l4 4",
            ["alert"] = "M12 3l10 18H2zM12 10v5M12 18h.01",
            ["info"] = "M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0zM12 11v6M12 7.5h.01",
            ["news"] = "M4 5h13v14H6a2 2 0 0 1-2-2zM17 9h3v8a2 2 0 0 1-2 2M8 9h5M8 13h5M8 16h3",
            ["notice"] = "M9 3h6v3H9zM7 4.5H5V21h14V4.5h-2M8 11h8M8 15h6",
            ["sms"] = "M4 5h16v11H9l-5 4z",
            ["signal"] = "M4 20v-3M9 20v-7M14 20V9M19 20V5",
            ["map"] = "M9 4L3 6v14l6-2 6 2 6-2V4l-6 2zM9 4v14M15 6v14",
            ["calendar"] = "M4 6h16v15H4zM4 10h16M8 3v4M16 3v4",
            ["book"] = "M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3zM5 17a3 3 0 0 1 3-3h11",
            ["home"] = "M3 11l9-7 9 7M5 10v10h14V10",
            ["play"] = "M7 4l13 8-13 8z",
            ["sliders"] = "M4 6h9M17 6h3M15 4v4M4 12h3M11 12h9M9 10v4M4 18h11M19 18h1M17 16v4",
            ["save"] = "M5 4h11l3 3v13H5zM8 4v5h7M8 20v-6h8v6",
            ["exit"] = "M14 4h5v16h-5M10 8l-4 4 4 4M6 12h10",
            ["retry"] = "M20 11a8 8 0 1 0-2.3 5.7M20 4v7h-7",
            ["sound"] = "M4 9h4l5-4v14l-5-4H4zM16.5 8.5a5 5 0 0 1 0 7",
            ["print"] = "M7 9V3h10v6M7 17H4v-7h16v7h-3M7 14h10v7H7z",
            ["close"] = "M6 6l12 12M18 6L6 18",
            ["arrow"] = "M5 12h14M13 6l6 6-6 6",
            ["lock"] = "M6 11h12v10H6zM8 11V8a4 4 0 0 1 8 0v3",
            ["spark"] = "M12 3l2.2 6.8L21 12l-6.8 2.2L12 21l-2.2-6.8L3 12l6.8-2.2z",
            ["siren"] = "M7 18v-6a5 5 0 0 1 10 0v6M4 18h16v3H4zM12 3v2M4.5 6.5L6 8M19.5 6.5L18 8",
            ["hourglass"] = "M6 3h12M6 21h12M7 3c0 5 10 5 10 9s-10 4-10 9M17 3c0 5-10 5-10 9",
            ["people"] = "M8 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM16.5 11a3 3 0 1 0 0-6M2 20a6 6 0 0 1 12 0M15 14.5a5 5 0 0 1 7 4.5",
            ["box"] = "M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10",
            ["eye"] = "M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z",
            ["flame"] = "M12 21a6 6 0 0 0 6-6c0-4-3-6-4-10-2 2-3 4-3 6-1-1-1.5-2-1.5-3C7 10 6 12.5 6 15a6 6 0 0 0 6 6z",
        };

        static readonly Dictionary<string, List<List<Vector2>>> parsed = new Dictionary<string, List<List<Vector2>>>();

        public readonly string iconName;
        public float em = 1.15f;
        readonly List<List<Vector2>> lines;

        public UiIcon(string name)
        {
            iconName = name;
            pickingMode = PickingMode.Ignore;
            style.flexShrink = 0;
            lines = Polylines(name != null && P.ContainsKey(name) ? name : "circle");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => Resize());
            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Resize));
        }

        // width = height = em * font size.
        public void Resize()
        {
            float fs = resolvedStyle.fontSize;
            if (!(fs > 0)) fs = Css.Rem(1);
            float px = Mathf.Round(em * fs);
            if (Mathf.Abs(resolvedStyle.width - px) > 0.5f || style.width.value.value != px) { style.width = px; style.height = px; }
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            float k = Mathf.Min(r.width, r.height) / 24f;
            var p = ctx.painter2D;
            p.strokeColor = resolvedStyle.color;
            p.lineWidth = 2 * k;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            foreach (var line in lines)
            {
                if (line.Count < 2) continue;
                p.BeginPath();
                p.MoveTo(line[0] * k);
                for (int i = 1; i < line.Count; i++) p.LineTo(line[i] * k);
                p.Stroke();
            }
        }

        // ---- SVG path data -> polylines (curves and arcs flattened) ----
        static List<List<Vector2>> Polylines(string name)
        {
            if (parsed.TryGetValue(name, out var cached)) return cached;
            var outp = new List<List<Vector2>>();
            List<Vector2> cur = null;
            var toks = Tokens(P[name]);
            int i = 0; char cmd = 'M';
            Vector2 pos = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero; char lastCmd = ' ';
            float N() => toks[i++].num;
            while (i < toks.Count)
            {
                if (toks[i].cmd != '\0') cmd = toks[i++].cmd;
                bool rel = char.IsLower(cmd);
                Vector2 o = rel ? pos : Vector2.zero;
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M':
                        pos = o + new Vector2(N(), N()); start = pos;
                        cur = new List<Vector2> { pos }; outp.Add(cur);
                        cmd = rel ? 'l' : 'L';
                        continue;
                    case 'L': pos = o + new Vector2(N(), N()); cur.Add(pos); break;
                    case 'H': pos = new Vector2((rel ? pos.x : 0) + N(), pos.y); cur.Add(pos); break;
                    case 'V': pos = new Vector2(pos.x, (rel ? pos.y : 0) + N()); cur.Add(pos); break;
                    case 'Z':
                        cur.Add(start); pos = start;
                        cur = new List<Vector2> { pos }; outp.Add(cur);
                        lastCmd = 'Z';
                        continue;
                    case 'C':
                    {
                        var c1 = o + new Vector2(N(), N()); var c2 = o + new Vector2(N(), N()); var e = o + new Vector2(N(), N());
                        Cubic(cur, pos, c1, c2, e); lastCtrl = c2; pos = e; lastCmd = 'C';
                        continue;
                    }
                    case 'S':
                    {
                        var c1 = lastCmd == 'C' ? 2 * pos - lastCtrl : pos;
                        var c2 = o + new Vector2(N(), N()); var e = o + new Vector2(N(), N());
                        Cubic(cur, pos, c1, c2, e); lastCtrl = c2; pos = e; lastCmd = 'C';
                        continue;
                    }
                    case 'A':
                    {
                        float rx = N(), ry = N(), rot = N(); bool large = N() != 0, sweep = N() != 0;
                        var e = o + new Vector2(N(), N());
                        Arc(cur, pos, rx, ry, rot, large, sweep, e); pos = e;
                        break;
                    }
                    default: i++; break;
                }
                lastCmd = char.ToUpperInvariant(cmd);
            }
            outp.RemoveAll(l => l.Count < 2);
            parsed[name] = outp;
            return outp;
        }

        static void Cubic(List<Vector2> o, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            for (int s = 1; s <= 12; s++)
            {
                float t = s / 12f, u = 1 - t;
                o.Add(u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3);
            }
        }

        // SVG endpoint arc -> centre parametrisation (SVG 1.1 F.6.5), sampled.
        static void Arc(List<Vector2> o, Vector2 p1, float rx, float ry, float rotDeg, bool large, bool sweep, Vector2 p2)
        {
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            if (rx < 1e-5f || ry < 1e-5f) { o.Add(p2); return; }
            float phi = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(phi), sn = Mathf.Sin(phi);
            var d = (p1 - p2) / 2;
            float x1 = cs * d.x + sn * d.y, y1 = -sn * d.x + cs * d.y;
            float lam = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lam > 1) { float s = Mathf.Sqrt(lam); rx *= s; ry *= s; }
            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float co = Mathf.Sqrt(Mathf.Max(0, num / den)) * (large == sweep ? -1 : 1);
            float cxp = co * rx * y1 / ry, cyp = -co * ry * x1 / rx;
            var c = new Vector2(cs * cxp - sn * cyp, sn * cxp + cs * cyp) + (p1 + p2) / 2;
            float Ang(Vector2 u, Vector2 v) { float a = Mathf.Atan2(u.x * v.y - u.y * v.x, Vector2.Dot(u, v)); return a; }
            var v1 = new Vector2((x1 - cxp) / rx, (y1 - cyp) / ry);
            var v2 = new Vector2((-x1 - cxp) / rx, (-y1 - cyp) / ry);
            float th = Ang(Vector2.right, v1), dth = Ang(v1, v2);
            if (!sweep && dth > 0) dth -= 2 * Mathf.PI;
            else if (sweep && dth < 0) dth += 2 * Mathf.PI;
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(dth) / (Mathf.PI / 16)));
            for (int s = 1; s <= n; s++)
            {
                float a = th + dth * s / n;
                float px = rx * Mathf.Cos(a), py = ry * Mathf.Sin(a);
                o.Add(new Vector2(cs * px - sn * py, sn * px + cs * py) + c);
            }
        }

        struct Tok { public char cmd; public float num; }
        static List<Tok> Tokens(string d)
        {
            var list = new List<Tok>();
            int i = 0;
            while (i < d.Length)
            {
                char ch = d[i];
                if (char.IsLetter(ch) && ch != 'e') { list.Add(new Tok { cmd = ch }); i++; continue; }
                if (ch == ' ' || ch == ',') { i++; continue; }
                int s = i; bool dot = false;
                if (d[i] == '-' || d[i] == '+') i++;
                while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot)))
                {
                    if (d[i] == '.') dot = true;
                    i++;
                }
                if (i == s) { i++; continue; }
                list.Add(new Tok { num = float.Parse(d.Substring(s, i - s), CultureInfo.InvariantCulture) });
            }
            return list;
        }
    }
}
