// Unlit colour * texture * (optional) vertex colour without fog: the stand-in for three.js MeshBasicMaterial
// { fog: false } (sky shells, hills, beams, the fade overlay, distant backdrops). Blend/ZWrite/ZTest/Cull come
// from Engine.Mat. Plain CG so it runs in URP's unlit pass and in the built-in pipeline.
Shader "Gyeongbo/UnlitNoFog"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _VertexColor ("Use vertex colour", Float) = 0
        _Cutoff ("Alpha cutoff", Float) = 0
        [HideInInspector] _SrcBlend ("Src blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _VertexColor;
            float _Cutoff;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = lerp(float4(1, 1, 1, 1), v.color, _VertexColor) * _Color;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * i.color;
                clip(c.a - _Cutoff);
                return c;
            }
            ENDCG
        }
    }
}
