// Editor-only: creates the URP pipeline asset, its renderer (with the SSAO feature UrpPost toggles by quality)
// and the default volume profile if missing and assigns them in Graphics/Quality settings. Not compiled by
// Tools~/check.sh. UrpPost builds its own runtime profile, so the asset profile only holds URP's defaults
// (UrpPost's global Volume at priority 10 overrides everything it grades).
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gyeongbo.URP.Editor
{
    [InitializeOnLoad]
    static class UrpSetup
    {
        const string DIR = "Assets/Settings", PIPE = DIR + "/URP.asset", RENDERER = DIR + "/URP_Renderer.asset", PROFILE = DIR + "/URP_VolumeProfile.asset";

        static UrpSetup() { EditorApplication.delayCall += Run; }

        // Idempotent.
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(DIR)) AssetDatabase.CreateFolder("Assets", "Settings");

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RENDERER);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                // As URP's own "URP Asset (with Universal Renderer)" menu does (PostProcessData.GetDefaultPostProcessData is internal).
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, RENDERER);
                AddSsao(renderer);
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PROFILE);
            }

            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PIPE);
            if (pipe == null)
            {
                pipe = UniversalRenderPipelineAsset.Create(renderer);
                pipe.supportsHDR = true;
                AssetDatabase.CreateAsset(pipe, PIPE);
            }
            // The asset's default volume profile (URP 17 per-quality defaults).
            if (pipe.volumeProfile == null) { pipe.volumeProfile = profile; EditorUtility.SetDirty(pipe); }

            if (GraphicsSettings.defaultRenderPipeline != pipe) GraphicsSettings.defaultRenderPipeline = pipe;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.count; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) == pipe) continue;
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipe;
            }
            if (QualitySettings.GetQualityLevel() != current) QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
        }

        // UrpPost.SetQuality turns ScreenSpaceAmbientOcclusion on for "high" only.
        static void AddSsao(UniversalRendererData renderer)
        {
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            renderer.rendererFeatures.Add(ssao);
            // Keep the feature map (local file ids) in step with the list, as the renderer inspector does.
            var so = new SerializedObject(renderer);
            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long id))
            {
                map.InsertArrayElementAtIndex(map.arraySize);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
    }
}
