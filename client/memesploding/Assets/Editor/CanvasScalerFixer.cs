using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

public class CanvasScalerFixer : EditorWindow
{
    [MenuItem("Tools/Fix All Canvas Scalers")]
    public static void FixAllCanvasScalers()
    {
        // Find all scene files under Assets/Scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        
        foreach (string guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            Debug.Log($"[CanvasScalerFixer] Opening scene: {scenePath}");
            
            // Open the scene
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool sceneModified = false;
            
            // Find all CanvasScaler components in the scene
            CanvasScaler[] scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();
            foreach (var scaler in scalers)
            {
                // Verify it belongs to the loaded scene (not a project asset or template)
                if (scaler.gameObject.scene.name == null) continue;
                
                Undo.RecordObject(scaler, "Fix Canvas Scaler Setting");
                
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                
                EditorUtility.SetDirty(scaler);
                sceneModified = true;
                Debug.Log($"[CanvasScalerFixer] Fixed CanvasScaler on GameObject '{scaler.gameObject.name}' in scene '{scene.name}'", scaler.gameObject);
            }
            
            if (sceneModified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[CanvasScalerFixer] Saved changes in scene: {scenePath}");
            }
        }
        
        // Fix CanvasScalers inside Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;
            
            CanvasScaler[] scalers = prefab.GetComponentsInChildren<CanvasScaler>(true);
            bool prefabModified = false;
            foreach (var scaler in scalers)
            {
                Undo.RecordObject(scaler, "Fix Canvas Scaler Setting in Prefab");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                
                EditorUtility.SetDirty(scaler);
                prefabModified = true;
                Debug.Log($"[CanvasScalerFixer] Fixed CanvasScaler in prefab: '{prefabPath}' -> GameObject: '{scaler.gameObject.name}'");
            }
            
            if (prefabModified)
            {
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("[CanvasScalerFixer] Completed scanning and fixing all Canvas Scalers!");
    }
}
