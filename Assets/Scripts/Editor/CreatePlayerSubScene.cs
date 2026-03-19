using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the SubScene holder in SampleScene pointing to Players.unity.
/// Menu: Tools > Create Player SubScene
/// </summary>
public static class CreatePlayerSubScene
{
    [MenuItem("Tools/Create Player SubScene")]
    static void Execute()
    {
        const string subScenePath = "Assets/Scenes/Players.unity";

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
        if (sceneAsset == null)
        {
            Debug.LogError($"[CreatePlayerSubScene] Could not load SceneAsset at '{subScenePath}'. Make sure the file exists.");
            return;
        }

        // Make SampleScene the active scene
        var mainScene = SceneManager.GetSceneByName("SampleScene");
        if (mainScene.IsValid())
            EditorSceneManager.SetActiveScene(mainScene);

        // Remove any stale holder
        var stale = GameObject.Find("Players SubScene");
        if (stale != null) Object.DestroyImmediate(stale);

        // Create the holder in SampleScene
        var holder = new GameObject("Players SubScene");
        SceneManager.MoveGameObjectToScene(holder, mainScene);

        var comp = holder.AddComponent<SubScene>();
        comp.SceneAsset = sceneAsset;
        comp.AutoLoadScene = true;
        EditorUtility.SetDirty(holder);

        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[CreatePlayerSubScene] Done. SceneAsset = {sceneAsset.name}");
    }
}
