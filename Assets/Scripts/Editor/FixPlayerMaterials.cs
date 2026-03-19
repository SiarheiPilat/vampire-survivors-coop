using UnityEditor;
using UnityEngine;

public static class FixPlayerMaterials
{
    [MenuItem("Tools/Fix Player Material Shaders")]
    static void Execute()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) { Debug.LogError("URP Unlit shader not found."); return; }

        var mats = new[]
        {
            ("Assets/Materials/PlayerMat_Red.mat",    new Color(1, 0, 0)),
            ("Assets/Materials/PlayerMat_Blue.mat",   new Color(0, 0, 1)),
            ("Assets/Materials/PlayerMat_Green.mat",  new Color(0, 1, 0)),
            ("Assets/Materials/PlayerMat_Yellow.mat", new Color(1, 1, 0)),
        };

        foreach (var (path, color) in mats)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { Debug.LogError($"Material not found: {path}"); continue; }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FixPlayerMaterials] Done.");
    }
}
