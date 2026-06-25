#if UNITY_EDITOR
using ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// One-time setup tool.
    /// Run via:  Tools → Memesploding → Create SoundMap Asset
    ///
    /// Creates Assets/Resources/Audio/SoundMap.asset (auto-loaded by SoundManager)
    /// and opens it in the Inspector so you can start wiring AudioCueSOs.
    /// </summary>
    public static class SoundMapSetup
    {
        private const string ResourcesAudioPath = "Assets/Resources/Audio";
        private const string SoundMapAssetPath  = "Assets/Resources/Audio/SoundMap.asset";

        [MenuItem("Tools/Memesploding/Create SoundMap Asset")]
        public static void CreateSoundMap()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Audio");

            if (AssetDatabase.LoadAssetAtPath<SoundMapSO>(SoundMapAssetPath) != null)
            {
                Debug.Log("[Memesploding] SoundMap already exists at " + SoundMapAssetPath);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SoundMapSO>(SoundMapAssetPath);
                return;
            }

            var map = ScriptableObject.CreateInstance<SoundMapSO>();
            AssetDatabase.CreateAsset(map, SoundMapAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = map;
            EditorGUIUtility.PingObject(map);

            Debug.Log("[Memesploding] SoundMap.asset created at " + SoundMapAssetPath);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
