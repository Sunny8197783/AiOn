// One-time editor setup (PORTING.md "Boot"): creates Assets/Scenes/Main.unity (an empty scene; Boot builds the
// game from code) with its build settings entry and adds the project shaders (Runtime/Engine/Shaders) plus URP
// Lit / Unlit and Skybox/Procedural to Always Included Shaders, so builds keep the shaders found by name.
// URP assets themselves: Assets/Scripts/URP/Editor/UrpSetup.cs.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Gyeongbo.Editor
{
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string SCENE = "Assets/Scenes/Main.unity", SHADERS = "Assets/Scripts/Runtime/Engine/Shaders";
        static readonly string[] BY_NAME = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Skybox/Procedural" };

        static ProjectSetup() { EditorApplication.delayCall += Run; }

        // Idempotent: does nothing when the scene and the shader entries already exist.
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureScene();
            EnsureBuildEntry();
            EnsureShaders();
        }

        static void EnsureScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE) != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            // A fresh project has only the untitled scene open: replace it. Otherwise add, save and close, so
            // whatever is open stays as it is.
            bool untitled = string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && SceneManager.sceneCount == 1;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitled ? NewSceneMode.Single : NewSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene, SCENE)) { Debug.LogError("[setup] could not save " + SCENE); return; }
            if (!untitled) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[setup] created " + SCENE);
        }

        static void EnsureBuildEntry()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == SCENE)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(SCENE, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureShaders()
        {
            var want = new List<Shader>();
            foreach (var n in BY_NAME) { var s = Shader.Find(n); if (s != null) want.Add(s); else Debug.LogWarning("[setup] shader not found: " + n); }
            if (AssetDatabase.IsValidFolder(SHADERS))
                foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { SHADERS }))
                {
                    var s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                    if (s != null) want.Add(s);
                }
            var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) { Debug.LogError("[setup] GraphicsSettings.m_AlwaysIncludedShaders not found"); return; }
            var have = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++) have.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            bool changed = false;
            foreach (var s in want)
            {
                if (have.Contains(s)) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
                have.Add(s); changed = true;
            }
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
