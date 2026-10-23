// Materials (PORTING.md "Boot"): three.js MeshStandardMaterial / MeshBasicMaterial params -> URP Lit / Unlit
// (falling back to Standard / Unlit/Color outside URP). Owned by the engine group; signatures are shared.
using System;
using UnityEngine;

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
        // MeshStandardMaterial -> "Universal Render Pipeline/Lit" (Standard fallback).
        public static Material Lit(MatSpec s) => throw new NotImplementedException("port: three.js MeshStandardMaterial -> URP Lit");
        // MeshBasicMaterial -> "Universal Render Pipeline/Unlit".
        public static Material Unlit(MatSpec s) => throw new NotImplementedException("port: three.js MeshBasicMaterial -> URP Unlit");

        // Runtime changes the JS makes on materials (material.color, .emissiveIntensity, .opacity, map offset).
        public static Color GetColor(Material m) => throw new NotImplementedException("port: material.color");
        public static void SetColor(Material m, Color c) => throw new NotImplementedException("port: material.color.set");
        public static void SetEmissive(Material m, Color c, float intensity) => throw new NotImplementedException("port: material.emissive");
        // Scales the material's own emissive colour (kept from creation); 0 = dark.
        public static void SetEmissiveIntensity(Material m, float intensity) => throw new NotImplementedException("port: material.emissiveIntensity");
        public static float GetEmissiveIntensity(Material m) => throw new NotImplementedException("port: material.emissiveIntensity");
        public static void SetOpacity(Material m, float opacity) => throw new NotImplementedException("port: material.opacity");
        public static void SetMap(Material m, Texture map) => throw new NotImplementedException("port: material.map");
        public static void SetTiling(Material m, Vector2 repeat, Vector2 offset) => throw new NotImplementedException("port: texture.repeat/offset");
        public static void SetRoughness(Material m, float roughness) => throw new NotImplementedException("port: material.roughness");
    }
}
