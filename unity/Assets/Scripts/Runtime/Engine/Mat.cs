// Materials (PORTING.md "Boot"): three.js MeshStandardMaterial / MeshBasicMaterial params -> URP Lit / Unlit
// (falling back to Standard / Unlit/Color outside URP). Owned by the engine group; signatures are shared.
// Render state is set the way URP's BaseShaderGUI does it in the editor (surface, blend, zwrite, queue, keywords),
// since runtime-made materials never pass through the inspector. Colours are sRGB like Unity's Color; three.js
// keeps them linear, so emissive colour * intensity is computed in linear and handed back as sRGB.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gyeongbo.Engine
{
    // The three.js material params the levels use. null = three.js default. Colours are sRGB (J.Hex / J.Css).
    public sealed class MatSpec
    {
        public Color? color, emissive;
        public float? roughness, metalness, opacity, emissiveIntensity, alphaTest, envMapIntensity;
        public float? polygonOffsetFactor, polygonOffsetUnits;
        public Texture map, normalMap, emissiveMap, roughnessMap, alphaMap;
        public Vector2? normalScale;
        public bool? transparent, depthWrite, depthTest, polygonOffset, vertexColors, fog, flatShading, toneMapped;
        public string side;          // "front" (default) | "back" | "double"
        public string blending;      // "normal" (default) | "additive"
        public Vector2? repeat, offset;   // texture repeat/offset (three.js sets them on the texture; here on the material)
        public float? unlitColor;    // kit userData.unlitColor: a backlit panel also darkens its colour when unpowered
        public bool? wet;            // kit userData.wet: joins the rain wet look
        public MatSpec Clone() => (MatSpec)MemberwiseClone();
    }

    public static class Mat
    {
        public const string NOFOG = "Gyeongbo/UnlitNoFog";

        // What three.js keeps on the material and URP does not: the emissive base, roughness, userData flags.
        internal sealed class Info
        {
            public Color emissiveLin = Color.black;   // linear
            public float emissiveI = 1, roughness = 1, envMapIntensity = 1;
            public float? unlitColor;
            public bool wet, standard;
        }
        static readonly ConditionalWeakTable<Material, Info> infos = new ConditionalWeakTable<Material, Info>();
        internal static Info InfoOf(Material m) => infos.GetOrCreateValue(m);

        static readonly Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();
        internal static Shader Find(params string[] names)
        {
            foreach (var n in names)
            {
                if (!shaders.TryGetValue(n, out var s)) { s = Shader.Find(n); shaders[n] = s; }
                if (s != null) return s;
            }
            Debug.LogWarning("[engine/mat] none of the shaders found: " + string.Join(", ", names));
            return Shader.Find("Hidden/InternalErrorShader");
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), ColorP = Shader.PropertyToID("_Color"),
            BaseMap = Shader.PropertyToID("_BaseMap"), MainTex = Shader.PropertyToID("_MainTex"),
            Smoothness = Shader.PropertyToID("_Smoothness"), Glossiness = Shader.PropertyToID("_Glossiness"),
            Metallic = Shader.PropertyToID("_Metallic"), BumpMap = Shader.PropertyToID("_BumpMap"), BumpScale = Shader.PropertyToID("_BumpScale"),
            EmissionColor = Shader.PropertyToID("_EmissionColor"), EmissionMap = Shader.PropertyToID("_EmissionMap"),
            Surface = Shader.PropertyToID("_Surface"), Blend = Shader.PropertyToID("_Blend"), Cull = Shader.PropertyToID("_Cull"),
            AlphaClip = Shader.PropertyToID("_AlphaClip"), Cutoff = Shader.PropertyToID("_Cutoff"),
            SrcBlend = Shader.PropertyToID("_SrcBlend"), DstBlend = Shader.PropertyToID("_DstBlend"),
            SrcBlendA = Shader.PropertyToID("_SrcBlendAlpha"), DstBlendA = Shader.PropertyToID("_DstBlendAlpha"),
            ZWrite = Shader.PropertyToID("_ZWrite"), ZTest = Shader.PropertyToID("_ZTest"), ModeP = Shader.PropertyToID("_Mode"),
            PreserveSpec = Shader.PropertyToID("_BlendModePreserveSpecular"), VertexColor = Shader.PropertyToID("_VertexColor"),
            AlphaToMask = Shader.PropertyToID("_AlphaToMask");

        // MeshStandardMaterial -> "Universal Render Pipeline/Lit" (Standard fallback).
        public static Material Lit(MatSpec s)
        {
            s = s ?? new MatSpec();
            var sh = s.vertexColors == true ? Find("Universal Render Pipeline/Particles/Lit", "Universal Render Pipeline/Lit", "Standard") : Find("Universal Render Pipeline/Lit", "Standard");
            var m = new Material(sh) { name = "lit" };
            var inf = InfoOf(m);
            inf.standard = true;
            var c = s.color ?? Color.white; c.a = s.opacity ?? 1;
            SetBaseColor(m, c);
            if (s.map != null) SetMap(m, s.map);
            SetTiling(m, s.repeat ?? Vector2.one, s.offset ?? Vector2.zero);
            SetRoughness(m, s.roughness ?? 1);
            if (m.HasProperty(Metallic)) m.SetFloat(Metallic, s.metalness ?? 0);
            if (s.normalMap != null && m.HasProperty(BumpMap))
            {
                m.SetTexture(BumpMap, s.normalMap);
                if (m.HasProperty(BumpScale)) m.SetFloat(BumpScale, s.normalScale?.x ?? 1);
                m.EnableKeyword("_NORMALMAP");
            }
            if (s.emissiveMap != null && m.HasProperty(EmissionMap)) m.SetTexture(EmissionMap, s.emissiveMap);
            inf.emissiveLin = (s.emissive ?? Color.black).linear;
            inf.emissiveI = s.emissiveIntensity ?? 1;
            ApplyEmission(m, inf);
            inf.unlitColor = s.unlitColor; inf.wet = s.wet == true; inf.envMapIntensity = s.envMapIntensity ?? 1;
            // ponytail: roughnessMap, alphaMap, polygonOffset, depthTest:false, flatShading and envMapIntensity have no
            // URP Lit equivalent here; the quality pass moves those materials to Shader Graph.
            State(m, s);
            return m;
        }

        // MeshBasicMaterial -> "Universal Render Pipeline/Unlit" (vertex colours: Particles/Unlit; fog: false: Gyeongbo/UnlitNoFog).
        public static Material Unlit(MatSpec s)
        {
            s = s ?? new MatSpec();
            var sh = s.fog == false ? Find(NOFOG, "Universal Render Pipeline/Unlit", "Unlit/Texture")
                : s.vertexColors == true ? Find("Universal Render Pipeline/Particles/Unlit", NOFOG, "Unlit/Texture")
                : Find("Universal Render Pipeline/Unlit", "Unlit/Texture", "Unlit/Color");
            var m = new Material(sh) { name = "unlit" };
            var c = s.color ?? Color.white; c.a = s.opacity ?? 1;
            SetBaseColor(m, c);
            if (s.map != null) SetMap(m, s.map);
            SetTiling(m, s.repeat ?? Vector2.one, s.offset ?? Vector2.zero);
            if (m.HasProperty(VertexColor)) m.SetFloat(VertexColor, s.vertexColors == true ? 1 : 0);
            InfoOf(m).unlitColor = s.unlitColor;
            State(m, s);
            return m;
        }

        // side -> cull, transparent / additive -> surface + blend, alphaTest -> clip, depthWrite / depthTest, queue.
        static void State(Material m, MatSpec s)
        {
            bool additive = s.blending == "additive", blended = s.transparent == true || additive;
            bool clip = (s.alphaTest ?? 0) > 0, zwrite = s.depthWrite ?? true;   // three.js writes depth even when transparent
            if (m.HasProperty(Cull)) m.SetFloat(Cull, s.side == "double" ? 0 : s.side == "back" ? 1 : 2);
            if (m.HasProperty(Cutoff)) m.SetFloat(Cutoff, clip ? s.alphaTest.Value : m.shader.name == NOFOG ? 0 : 0.5f);
            if (m.HasProperty(AlphaClip)) m.SetFloat(AlphaClip, clip ? 1 : 0);
            SetKeyword(m, "_ALPHATEST_ON", clip);
            var src = BlendMode.One; var dst = BlendMode.Zero; var srcA = BlendMode.One; var dstA = BlendMode.Zero;
            if (blended)
            {
                src = BlendMode.SrcAlpha; dst = additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha; dstA = dst;
                bool preserve = m.HasProperty(PreserveSpec) && m.GetFloat(PreserveSpec) > 0;
                if (preserve) src = BlendMode.One;
                SetKeyword(m, "_ALPHAPREMULTIPLY_ON", preserve);
                m.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                SetKeyword(m, "_ALPHAPREMULTIPLY_ON", false);
                m.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
            }
            if (m.HasProperty(Surface)) { m.SetFloat(Surface, blended ? 1 : 0); m.SetFloat(Blend, additive ? 2 : 0); }
            else if (m.HasProperty(ModeP)) m.SetFloat(ModeP, blended ? 3 : clip ? 1 : 0);   // Standard fallback
            SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", blended);
            SetKeyword(m, "_ALPHAMODULATE_ON", false);
            if (m.HasProperty(SrcBlend)) { m.SetFloat(SrcBlend, (float)src); m.SetFloat(DstBlend, (float)dst); }
            if (m.HasProperty(SrcBlendA)) { m.SetFloat(SrcBlendA, (float)srcA); m.SetFloat(DstBlendA, (float)dstA); }
            if (m.HasProperty(ZWrite)) m.SetFloat(ZWrite, zwrite ? 1 : 0);
            if (m.HasProperty(ZTest)) m.SetFloat(ZTest, s.depthTest == false ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
            if (m.HasProperty(AlphaToMask)) m.SetFloat(AlphaToMask, 0);
            m.SetShaderPassEnabled("DepthOnly", zwrite && !blended);
            m.renderQueue = blended ? (int)RenderQueue.Transparent : clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
        }

        static void SetKeyword(Material m, string k, bool on) { if (on) m.EnableKeyword(k); else m.DisableKeyword(k); }

        static void SetBaseColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColor)) m.SetColor(BaseColor, c);
            if (m.HasProperty(ColorP)) m.SetColor(ColorP, c);
        }

        static Color BaseColorOf(Material m) => m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : m.HasProperty(ColorP) ? m.GetColor(ColorP) : Color.white;

        static void ApplyEmission(Material m, Info inf)
        {
            if (!m.HasProperty(EmissionColor)) return;
            bool on = inf.emissiveLin.maxColorComponent > 0;
            SetKeyword(m, "_EMISSION", on);
            m.globalIlluminationFlags = on ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            var lin = inf.emissiveLin * Mathf.Max(0, inf.emissiveI); lin.a = 1;
            m.SetColor(EmissionColor, lin.gamma);
        }

        // Runtime changes the JS makes on materials (material.color, .emissiveIntensity, .opacity, map offset).
        public static Color GetColor(Material m) { var c = BaseColorOf(m); c.a = 1; return c; }

        // material.color.set(c): the colour only, opacity stays.
        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            c.a = BaseColorOf(m).a;
            SetBaseColor(m, c);
        }

        public static void SetEmissive(Material m, Color c, float intensity)
        {
            if (m == null) return;
            var inf = InfoOf(m); inf.emissiveLin = c.linear; inf.emissiveI = intensity;
            ApplyEmission(m, inf);
        }

        // Scales the material's own emissive colour (kept from creation); 0 = dark.
        public static void SetEmissiveIntensity(Material m, float intensity)
        {
            if (m == null) return;
            var inf = InfoOf(m);
            if (inf.emissiveI == intensity) return;
            inf.emissiveI = intensity;
            ApplyEmission(m, inf);
        }

        public static float GetEmissiveIntensity(Material m) => m == null ? 0 : InfoOf(m).emissiveI;

        public static void SetOpacity(Material m, float opacity)
        {
            if (m == null) return;
            var c = BaseColorOf(m); c.a = opacity;
            SetBaseColor(m, c);
        }

        public static void SetMap(Material m, Texture map)
        {
            if (m == null) return;
            if (m.HasProperty(BaseMap)) m.SetTexture(BaseMap, map);
            if (m.HasProperty(MainTex)) m.SetTexture(MainTex, map);
        }

        public static void SetTiling(Material m, Vector2 repeat, Vector2 offset)
        {
            if (m == null) return;
            foreach (var p in new[] { BaseMap, MainTex })
                if (m.HasProperty(p)) { m.SetTextureScale(p, repeat); m.SetTextureOffset(p, offset); }
        }

        public static void SetRoughness(Material m, float roughness)
        {
            if (m == null) return;
            InfoOf(m).roughness = roughness;
            if (m.HasProperty(Smoothness)) m.SetFloat(Smoothness, Mathf.Clamp01(1 - roughness));
            if (m.HasProperty(Glossiness)) m.SetFloat(Glossiness, Mathf.Clamp01(1 - roughness));
        }

        internal static float GetRoughness(Material m) => InfoOf(m).roughness;
        internal static Texture GetMap(Material m) => m.HasProperty(BaseMap) ? m.GetTexture(BaseMap) : m.HasProperty(MainTex) ? m.GetTexture(MainTex) : null;
        internal static bool IsStandard(Material m) => m != null && InfoOf(m).standard;

        // three.js color.setScalar / multiplyScalar work on the linear value; keep the opacity.
        internal static Color GetLinearColor(Material m) => GetColor(m).linear;
        internal static void SetLinearColor(Material m, Color lin) { lin.a = 1; SetColor(m, lin.gamma); }
    }
}
