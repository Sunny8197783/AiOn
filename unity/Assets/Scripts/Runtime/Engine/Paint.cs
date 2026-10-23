// A CPU canvas for the procedural textures (PORTING.md "Procedural textures"): the Canvas 2D subset the JS
// draws with, over Color32[] with canvas coordinates (origin top-left, y down). Member names are the Canvas
// API's, so `g.fillStyle = "#3f3e3c"; g.fillRect(0, 0, w, h);` ports as is. Text: PaintText.cs.
// Not drawn: shadows (shadowBlur/shadowColor are stored only), drawImage, patterns, filters.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Engine
{
    public sealed partial class Paint
    {
        public readonly int width, height;
        public readonly Color32[] Pixels;                // row 0 = top, like the canvas

        sealed class St
        {
            public PaintStyle fill = "#000", stroke = "#000", shadowColor = "rgba(0,0,0,0)";
            public float lineWidth = 1, globalAlpha = 1, dashOffset, shadowBlur, miterLimit = 10;
            public string lineCap = "butt", lineJoin = "miter", comp = "source-over", font = "10px sans-serif", textAlign = "start", textBaseline = "alphabetic";
            public float[] dash = new float[0];
            public float a = 1, b, c, d = 1, e, f;       // transform: x' = a x + c y + e, y' = b x + d y + f
            public float[] clip;                         // per-pixel coverage, null = no clip
            public St Clone() => (St)MemberwiseClone();
        }

        St s = new St();
        readonly List<St> stack = new List<St>();
        readonly List<List<Vector2>> subs = new List<List<Vector2>>();   // device space
        readonly List<bool> shut = new List<bool>();
        List<Vector2> cur;
        float lx, ly;                                    // last point in user space (curves start there)

        public Paint(int width, int height)
        {
            this.width = Mathf.Max(1, width); this.height = Mathf.Max(1, height);
            Pixels = new Color32[this.width * this.height];
        }

        // ---- state ----
        public PaintStyle fillStyle { get => s.fill; set => s.fill = value ?? "#000"; }
        public PaintStyle strokeStyle { get => s.stroke; set => s.stroke = value ?? "#000"; }
        public float lineWidth { get => s.lineWidth; set { if (value > 0 && !float.IsInfinity(value)) s.lineWidth = value; } }
        public string lineCap { get => s.lineCap; set => s.lineCap = value ?? "butt"; }
        public string lineJoin { get => s.lineJoin; set => s.lineJoin = value ?? "miter"; }
        public float miterLimit { get => s.miterLimit; set { if (value > 0) s.miterLimit = value; } }
        public float globalAlpha { get => s.globalAlpha; set { if (value >= 0 && value <= 1) s.globalAlpha = value; } }
        public string globalCompositeOperation { get => s.comp; set => s.comp = value ?? "source-over"; }
        public string font { get => s.font; set => s.font = value ?? s.font; }
        public string textAlign { get => s.textAlign; set => s.textAlign = value ?? "start"; }
        public string textBaseline { get => s.textBaseline; set => s.textBaseline = value ?? "alphabetic"; }
        public float lineDashOffset { get => s.dashOffset; set => s.dashOffset = value; }
        public float shadowBlur { get => s.shadowBlur; set => s.shadowBlur = value; }          // ponytail: not drawn
        public PaintStyle shadowColor { get => s.shadowColor; set => s.shadowColor = value; }
        public bool imageSmoothingEnabled { get; set; } = true;

        public void setLineDash(params float[] segments)
        {
            if (segments == null || segments.Length == 0) { s.dash = new float[0]; return; }
            var d = new List<float>(segments); if (d.Count % 2 == 1) d.AddRange(segments);
            s.dash = d.ToArray();
        }
        public float[] getLineDash() => (float[])s.dash.Clone();
        public void save() => stack.Add(s.Clone());
        public void restore() { if (stack.Count == 0) return; s = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1); }

        // ---- transform ----
        public void translate(float x, float y) { s.e += s.a * x + s.c * y; s.f += s.b * x + s.d * y; }
        public void scale(float x, float y) { s.a *= x; s.b *= x; s.c *= y; s.d *= y; }
        public void rotate(float r)
        {
            float cs = Mathf.Cos(r), sn = Mathf.Sin(r), a = s.a, b = s.b, c = s.c, d = s.d;
            s.a = a * cs + c * sn; s.b = b * cs + d * sn; s.c = c * cs - a * sn; s.d = d * cs - b * sn;
        }
        public void setTransform(float a, float b, float c, float d, float e, float f) { s.a = a; s.b = b; s.c = c; s.d = d; s.e = e; s.f = f; }
        public void resetTransform() => setTransform(1, 0, 0, 1, 0, 0);
        Vector2 T(float x, float y) => new Vector2(s.a * x + s.c * y + s.e, s.b * x + s.d * y + s.f);
        float Det => s.a * s.d - s.b * s.c;
        float Sc => Mathf.Sqrt(Mathf.Abs(Det));
        // device -> user (gradients are defined in user space)
        Vector2 Inv(float px, float py)
        {
            float det = Det; if (Mathf.Abs(det) < 1e-12f) return new Vector2(px, py);
            float dx = px - s.e, dy = py - s.f;
            return new Vector2((s.d * dx - s.c * dy) / det, (-s.b * dx + s.a * dy) / det);
        }

        // ---- path ----
        public void beginPath() { subs.Clear(); shut.Clear(); cur = null; }
        public void moveTo(float x, float y) { cur = new List<Vector2> { T(x, y) }; subs.Add(cur); shut.Add(false); lx = x; ly = y; }
        public void lineTo(float x, float y) { if (cur == null) { moveTo(x, y); return; } cur.Add(T(x, y)); lx = x; ly = y; }
        public void closePath()
        {
            if (cur == null || cur.Count == 0) return;
            shut[shut.Count - 1] = true;
            var first = cur[0]; cur = new List<Vector2> { first }; subs.Add(cur); shut.Add(false);
        }
        public void rect(float x, float y, float w, float h) { moveTo(x, y); lineTo(x + w, y); lineTo(x + w, y + h); lineTo(x, y + h); closePath(); }
        public void roundRect(float x, float y, float w, float h, float r = 0)
        {
            r = Mathf.Max(0, Mathf.Min(r, Mathf.Abs(w) / 2, Mathf.Abs(h) / 2));
            if (r <= 0) { rect(x, y, w, h); return; }
            moveTo(x + r, y); lineTo(x + w - r, y); arc(x + w - r, y + r, r, -Mathf.PI / 2, 0);
            lineTo(x + w, y + h - r); arc(x + w - r, y + h - r, r, 0, Mathf.PI / 2);
            lineTo(x + r, y + h); arc(x + r, y + h - r, r, Mathf.PI / 2, Mathf.PI);
            lineTo(x, y + r); arc(x + r, y + r, r, Mathf.PI, Mathf.PI * 1.5f); closePath();
        }
        public void roundRect(float x, float y, float w, float h, float[] radii) => roundRect(x, y, w, h, radii != null && radii.Length > 0 ? radii[0] : 0);

        public void arc(float x, float y, float r, float a0, float a1, bool ccw = false) => ellipse(x, y, r, r, 0, a0, a1, ccw);

        public void ellipse(float x, float y, float rx, float ry, float rot, float a0, float a1, bool ccw = false)
        {
            const float TAU = Mathf.PI * 2;
            float sweep = a1 - a0;
            if (!ccw) sweep = sweep >= TAU ? TAU : ((sweep % TAU) + TAU) % TAU;
            else sweep = sweep <= -TAU ? -TAU : -((((a0 - a1) % TAU) + TAU) % TAU);
            float rr = Mathf.Max(Mathf.Abs(rx), Mathf.Abs(ry)) * Sc;
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweep) * Mathf.Max(rr, 1) / 2.5f), Mathf.Abs(sweep) >= TAU ? 12 : 1, 720);
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            for (int i = 0; i <= n; i++)
            {
                float t = a0 + sweep * i / n, ex = rx * Mathf.Cos(t), ey = ry * Mathf.Sin(t);
                float px = x + ex * cr - ey * sr, py = y + ex * sr + ey * cr;
                if (i == 0 && cur == null) moveTo(px, py); else lineTo(px, py);
            }
        }

        public void quadraticCurveTo(float cx, float cy, float x, float y)
        {
            float x0 = lx, y0 = ly; if (cur == null) moveTo(cx, cy);
            int n = CurveSteps(x0, y0, x, y);
            for (int i = 1; i <= n; i++) { float t = (float)i / n, u = 1 - t; lineTo(u * u * x0 + 2 * u * t * cx + t * t * x, u * u * y0 + 2 * u * t * cy + t * t * y); }
        }

        public void bezierCurveTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            float x0 = lx, y0 = ly; if (cur == null) moveTo(c1x, c1y);
            int n = CurveSteps(x0, y0, x, y) + 4;
            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n, u = 1 - t, a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
                lineTo(a * x0 + b * c1x + c * c2x + d * x, a * y0 + b * c1y + c * c2y + d * y);
            }
        }

        int CurveSteps(float x0, float y0, float x1, float y1) => Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * Sc / 4), 6, 64);

        // ---- drawing ----
        public void fill(string rule = "nonzero") => Raster(Polys(subs), rule == "evenodd", s.fill, null);
        public void stroke() => Raster(StrokePolys(subs, shut), false, s.stroke, null);

        // Intersects the clip with the current path.
        public void clip(string rule = "nonzero")
        {
            var m = new float[width * height];
            Raster(Polys(subs), rule == "evenodd", null, m);
            if (s.clip != null) for (int i = 0; i < m.Length; i++) m[i] *= s.clip[i];
            s.clip = m;
        }

        public void fillRect(float x, float y, float w, float h)
        {
            if (w == 0 || h == 0) return;
            if (Mathf.Abs(s.b) < 1e-6f && Mathf.Abs(s.c) < 1e-6f) { var p = T(x, y); var q = T(x + w, y + h); FillAxisRect(p.x, p.y, q.x, q.y, s.fill); return; }
            Raster(new List<List<Vector2>> { new List<Vector2> { T(x, y), T(x + w, y), T(x + w, y + h), T(x, y + h) } }, false, s.fill, null);
        }

        public void strokeRect(float x, float y, float w, float h)
        {
            var poly = new List<Vector2> { T(x, y), T(x + w, y), T(x + w, y + h), T(x, y + h) };
            Raster(StrokePolys(new List<List<Vector2>> { poly }, new List<bool> { true }), false, s.stroke, null);
        }

        public void clearRect(float x, float y, float w, float h)
        {
            var p = T(x, y); var q = T(x + w, y + h);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(p.x, q.x)), 0, width), x1 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(p.x, q.x)), 0, width);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(p.y, q.y)), 0, height), y1 = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(p.y, q.y)), 0, height);
            for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) Pixels[yy * width + xx] = new Color32(0, 0, 0, 0);
        }

        void FillAxisRect(float ax, float ay, float bx, float by, PaintStyle style)
        {
            float x0 = Mathf.Min(ax, bx), x1 = Mathf.Max(ax, bx), y0 = Mathf.Min(ay, by), y1 = Mathf.Max(ay, by);
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), ix1 = Mathf.Min(width - 1, Mathf.CeilToInt(x1) - 1);
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0)), iy1 = Mathf.Min(height - 1, Mathf.CeilToInt(y1) - 1);
            for (int py = iy0; py <= iy1; py++)
            {
                float cy = Mathf.Min(py + 1, y1) - Mathf.Max(py, y0); if (cy <= 0) continue;
                for (int px = ix0; px <= ix1; px++)
                {
                    float cx = Mathf.Min(px + 1, x1) - Mathf.Max(px, x0); if (cx <= 0) continue;
                    Plot(px, py, style, cx * cy);
                }
            }
        }

        static List<List<Vector2>> Polys(List<List<Vector2>> paths)
        {
            var o = new List<List<Vector2>>();
            foreach (var p in paths) if (p.Count >= 3) o.Add(p);
            return o;
        }

        // ---- scanline raster: 4 sub-rows per pixel, exact horizontal coverage, nonzero or even-odd ----
        struct EdgeR { public float y0, y1, x0, dxdy; public int dir; }

        void Raster(List<List<Vector2>> polys, bool evenOdd, PaintStyle style, float[] coverOut)
        {
            var edges = new List<EdgeR>();
            float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            foreach (var p in polys)
                for (int i = 0; i < p.Count; i++)
                {
                    Vector2 a = p[i], b = p[(i + 1) % p.Count];
                    minX = Mathf.Min(minX, a.x); maxX = Mathf.Max(maxX, a.x); minY = Mathf.Min(minY, a.y); maxY = Mathf.Max(maxY, a.y);
                    if (Mathf.Abs(a.y - b.y) < 1e-6f) continue;
                    if (a.y < b.y) edges.Add(new EdgeR { y0 = a.y, y1 = b.y, x0 = a.x, dxdy = (b.x - a.x) / (b.y - a.y), dir = 1 });
                    else edges.Add(new EdgeR { y0 = b.y, y1 = a.y, x0 = b.x, dxdy = (a.x - b.x) / (a.y - b.y), dir = -1 });
                }
            if (edges.Count == 0) return;
            edges.Sort((m, n) => m.y0.CompareTo(n.y0));
            int py0 = Mathf.Max(0, Mathf.FloorToInt(minY)), py1 = Mathf.Min(height - 1, Mathf.CeilToInt(maxY));
            int px0 = Mathf.Max(0, Mathf.FloorToInt(minX)), px1 = Mathf.Min(width - 1, Mathf.CeilToInt(maxX));
            if (px0 > px1 || py0 > py1) return;
            var cov = new float[px1 - px0 + 2];
            var active = new List<EdgeR>(); var xs = new List<KeyValuePair<float, int>>();
            int next = 0;
            for (int py = py0; py <= py1; py++)
            {
                Array.Clear(cov, 0, cov.Length);
                bool any = false;
                for (int k = 0; k < 4; k++)
                {
                    float sy = py + (k + 0.5f) / 4f;
                    while (next < edges.Count && edges[next].y0 <= sy) active.Add(edges[next++]);
                    active.RemoveAll(e => e.y1 <= sy);
                    xs.Clear();
                    foreach (var e in active) if (e.y0 <= sy) xs.Add(new KeyValuePair<float, int>(e.x0 + (sy - e.y0) * e.dxdy, e.dir));
                    if (xs.Count < 2) continue;
                    xs.Sort((m, n) => m.Key.CompareTo(n.Key));
                    int wind = 0;
                    for (int i = 0; i < xs.Count - 1; i++)
                    {
                        wind += evenOdd ? 1 : xs[i].Value;
                        bool inside = evenOdd ? (wind & 1) == 1 : wind != 0;
                        if (inside) { Span(cov, px0, px1, xs[i].Key, xs[i + 1].Key, 0.25f); any = true; }
                    }
                }
                if (!any) continue;
                for (int px = px0; px <= px1; px++)
                {
                    float c = Mathf.Min(1f, cov[px - px0]); if (c <= 0) continue;
                    if (coverOut != null) coverOut[py * width + px] = c; else Plot(px, py, style, c);
                }
            }
        }

        static void Span(float[] cov, int px0, int px1, float xa, float xb, float w)
        {
            xa = Mathf.Max(xa, px0); xb = Mathf.Min(xb, px1 + 1); if (xb <= xa) return;
            int ia = Mathf.FloorToInt(xa), ib = Mathf.FloorToInt(xb);
            if (ia == ib) { cov[ia - px0] += (xb - xa) * w; return; }
            cov[ia - px0] += (ia + 1 - xa) * w;
            for (int i = ia + 1; i < ib; i++) cov[i - px0] += w;
            if (ib <= px1) cov[ib - px0] += (xb - ib) * w;
        }

        // Blend one pixel with the style at its centre (source-over, or 'lighter' = additive).
        void Plot(int px, int py, PaintStyle style, float coverage)
        {
            int idx = py * width + px;
            if (s.clip != null) coverage *= s.clip[idx];
            Color src = style.color;
            if (style.gradient != null) { var u = Inv(px + 0.5f, py + 0.5f); src = style.At(u.x, u.y); }
            float a = src.a * s.globalAlpha * coverage; if (a <= 0) return;
            var d = Pixels[idx]; float da = d.a / 255f, dr = d.r / 255f, dg = d.g / 255f, db = d.b / 255f;
            float oa, or, og, ob;
            if (s.comp == "lighter")
            {
                oa = Mathf.Min(1, a + da);
                or = src.r * a + dr * da; og = src.g * a + dg * da; ob = src.b * a + db * da;
            }
            else
            {
                oa = a + da * (1 - a);
                or = src.r * a + dr * da * (1 - a); og = src.g * a + dg * da * (1 - a); ob = src.b * a + db * da * (1 - a);
            }
            if (oa <= 0) { Pixels[idx] = new Color32(0, 0, 0, 0); return; }
            Pixels[idx] = new Color32(B(or / oa), B(og / oa), B(ob / oa), B(oa));
        }

        static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        // ---- stroke outline: one quad per segment, joins and caps, all wound the same way (nonzero = union) ----
        List<List<Vector2>> StrokePolys(List<List<Vector2>> paths, List<bool> closedFlags)
        {
            var o = new List<List<Vector2>>();
            float hw = Mathf.Max(0.35f, s.lineWidth * Sc / 2);
            for (int pi = 0; pi < paths.Count; pi++)
            {
                var pts = new List<Vector2>();
                foreach (var q in paths[pi]) if (pts.Count == 0 || (q - pts[pts.Count - 1]).sqrMagnitude > 1e-8f) pts.Add(q);
                bool closed = pi < closedFlags.Count && closedFlags[pi] && pts.Count > 2;
                if (closed) pts.Add(pts[0]);
                if (pts.Count < 2) continue;
                foreach (var piece in Dash(pts)) Outline(piece, closed && s.dash.Length == 0, hw, o);
            }
            return o;
        }

        List<List<Vector2>> Dash(List<Vector2> pts)
        {
            var res = new List<List<Vector2>>();
            if (s.dash.Length == 0) { res.Add(pts); return res; }
            float sc = Sc, total = 0; foreach (var v in s.dash) total += v * sc;
            if (total <= 0) { res.Add(pts); return res; }
            float pos = ((s.dashOffset * sc) % total + total) % total; int di = 0;
            while (pos >= s.dash[di] * sc) { pos -= s.dash[di] * sc; di = (di + 1) % s.dash.Length; }
            float left = s.dash[di] * sc - pos; bool on = di % 2 == 0;
            var cur2 = on ? new List<Vector2> { pts[0] } : null;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i]; float len = (b - a).magnitude, t = 0;
                while (len - t > left)
                {
                    t += left; var m = a + (b - a) * (t / len);
                    if (on) { cur2.Add(m); res.Add(cur2); cur2 = null; } else cur2 = new List<Vector2> { m };
                    on = !on; di = (di + 1) % s.dash.Length; left = s.dash[di] * sc;
                }
                left -= len - t;
                if (on) cur2.Add(b);
            }
            if (on && cur2 != null && cur2.Count > 1) res.Add(cur2);
            return res;
        }

        void Outline(List<Vector2> pts, bool closed, float hw, List<List<Vector2>> o)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i], dv = (b - a).normalized, n = new Vector2(-dv.y, dv.x) * hw;
                if (!closed && s.lineCap == "square") { if (i == 1) a -= dv * hw; if (i == pts.Count - 1) b += dv * hw; }
                Add(o, new List<Vector2> { a + n, b + n, b - n, a - n });
            }
            int last = pts.Count - 1;
            for (int i = 1; i <= (closed ? last : last - 1); i++)
            {
                Vector2 v = pts[i], p = pts[i - 1], q = pts[i == last ? 1 : i + 1];
                Join(o, v, (v - p).normalized, (q - v).normalized, hw);
            }
            if (!closed && s.lineCap == "round") { Add(o, Disc(pts[0], hw)); Add(o, Disc(pts[last], hw)); }
        }

        void Join(List<List<Vector2>> o, Vector2 v, Vector2 d1, Vector2 d2, float hw)
        {
            float cross = d1.x * d2.y - d1.y * d2.x; if (Mathf.Abs(cross) < 1e-5f && Vector2.Dot(d1, d2) > 0) return;
            if (s.lineJoin == "round" || hw <= 0.75f) { Add(o, Disc(v, hw)); return; }
            float side = cross > 0 ? -1 : 1;                // the outer side of the turn
            Vector2 n1 = new Vector2(-d1.y, d1.x) * hw * side, n2 = new Vector2(-d2.y, d2.x) * hw * side;
            Vector2 mid = (n1 + n2); float cosHalf = Mathf.Sqrt(Mathf.Max(0, (1 + Vector2.Dot(d1, d2)) / 2));
            if (s.lineJoin == "miter" && cosHalf > 1e-3f && 1 / cosHalf <= s.miterLimit)
                Add(o, new List<Vector2> { v, v + n1, v + mid.normalized * (hw / cosHalf), v + n2 });
            else Add(o, new List<Vector2> { v, v + n1, v + n2 });
        }

        static List<Vector2> Disc(Vector2 c, float r)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(r * 2), 8, 64); var d = new List<Vector2>(n);
            for (int i = 0; i < n; i++) { float t = Mathf.PI * 2 * i / n; d.Add(c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r); }
            return d;
        }

        static void Add(List<List<Vector2>> o, List<Vector2> poly)
        {
            float area = 0; for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Count]; area += a.x * b.y - b.x * a.y; }
            if (area < 0) poly.Reverse();
            if (Mathf.Abs(area) > 1e-6f) o.Add(poly);
        }

        // ---- pixels ----
        public ImageData createImageData(int w, int h) => new ImageData(w, h);

        public ImageData getImageData(int x, int y, int w, int h)
        {
            var img = new ImageData(w, h);
            for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++)
            {
                int sx = x + xx, sy = y + yy; if (sx < 0 || sy < 0 || sx >= width || sy >= height) continue;
                var c = Pixels[sy * width + sx]; int i = (yy * w + xx) * 4;
                img.data[i] = c.r; img.data[i + 1] = c.g; img.data[i + 2] = c.b; img.data[i + 3] = c.a;
            }
            return img;
        }

        public void putImageData(ImageData img, int x, int y)
        {
            for (int yy = 0; yy < img.height; yy++) for (int xx = 0; xx < img.width; xx++)
            {
                int dx = x + xx, dy = y + yy; if (dx < 0 || dy < 0 || dx >= width || dy >= height) continue;
                int i = (yy * img.width + xx) * 4;
                Pixels[dy * width + dx] = new Color32(img.data[i], img.data[i + 1], img.data[i + 2], img.data[i + 3]);
            }
        }

        // ---- kit.js helpers ----
        // Grayscale height (red channel) -> tangent-space normal map, wrapping at the edges (kit normalFrom).
        public Paint NormalFrom(float strength = 2)
        {
            var o = new Paint(width, height);
            float H(int x, int y) => Pixels[(((y + height) % height) * width) + ((x + width) % width)].r / 255f;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float dx = (H(x - 1, y) - H(x + 1, y)) * strength, dy = (H(x, y + 1) - H(x, y - 1)) * strength, len = Mathf.Sqrt(dx * dx + dy * dy + 1);
                o.Pixels[y * width + x] = new Color32(B(dx / len * 0.5f + 0.5f), B(dy / len * 0.5f + 0.5f), B(1 / len * 0.5f + 0.5f), 255);
            }
            return o;
        }

        // n random squares of size s0..s1 in random colours (kit.js speckle).
        public void Speckle(Func<float> r, int n, string[] colors, float s0 = 0.5f, float s1 = 1.6f)
        {
            for (int i = 0; i < n; i++)
            {
                fillStyle = colors[(int)(r() * colors.Length) % colors.Length];
                float sz = s0 + r() * (s1 - s0);
                fillRect(r() * width, r() * height, sz, sz);
            }
        }

        // ---- textures (rows flipped like three.js CanvasTexture flipY, so v = 1 is the canvas top) ----
        public Texture2D ToTexture(bool srgb = true, bool mipmaps = true)
        {
            var t = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps, !srgb) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            Upload(t);
            return t;
        }

        // Re-upload after drawing again (three.js texture.needsUpdate = true).
        public void Upload(Texture2D t)
        {
            if (t == null || t.width != width || t.height != height) return;
            var flipped = new Color32[Pixels.Length];
            for (int y = 0; y < height; y++) Array.Copy(Pixels, y * width, flipped, (height - 1 - y) * width, width);
            t.SetPixels32(flipped);
            t.Apply(t.mipmapCount > 1);
        }
    }
}
