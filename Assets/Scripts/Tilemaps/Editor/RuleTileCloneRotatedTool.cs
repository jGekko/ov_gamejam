#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Herramienta de Editor para duplicar cualquier RuleTile existente y convertirla
/// instantáneamente en una RotatedRuleTile con rotación de 90°, 180° o 270°,
/// preservando intactos todos los arrays de sprites aleatorios y reglas de tiling.
/// </summary>
public static class RuleTileCloneRotatedTool
{
    [MenuItem("Assets/Create/2D/Tiles/Clone as 90° Rotated Rule Tile", true)]
    private static bool ValidateCloneAs90()
    {
        return Selection.activeObject is RuleTile;
    }

    [MenuItem("Assets/Create/2D/Tiles/Clone as 90° Rotated Rule Tile", false, 20)]
    public static void CloneAs90()
    {
        CloneRuleTileWithRotation(RotatedRuleTile.TileRotationAngle.Rotation90);
    }

    [MenuItem("Assets/Create/2D/Tiles/Clone as 180° Rotated Rule Tile", true)]
    private static bool ValidateCloneAs180()
    {
        return Selection.activeObject is RuleTile;
    }

    [MenuItem("Assets/Create/2D/Tiles/Clone as 180° Rotated Rule Tile", false, 21)]
    public static void CloneAs180()
    {
        CloneRuleTileWithRotation(RotatedRuleTile.TileRotationAngle.Rotation180);
    }

    [MenuItem("Assets/Create/2D/Tiles/Clone as 270° Rotated Rule Tile", true)]
    private static bool ValidateCloneAs270()
    {
        return Selection.activeObject is RuleTile;
    }

    [MenuItem("Assets/Create/2D/Tiles/Clone as 270° Rotated Rule Tile", false, 22)]
    public static void CloneAs270()
    {
        CloneRuleTileWithRotation(RotatedRuleTile.TileRotationAngle.Rotation270);
    }

    private static void CloneRuleTileWithRotation(RotatedRuleTile.TileRotationAngle rotation)
    {
        RuleTile source = Selection.activeObject as RuleTile;
        if (source == null) return;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        string dir = Path.GetDirectoryName(sourcePath);
        string filename = Path.GetFileNameWithoutExtension(sourcePath);
        string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{filename}_Rot{(int)rotation}.asset");

        RotatedRuleTile newTile = ScriptableObject.CreateInstance<RotatedRuleTile>();
        EditorUtility.CopySerialized(source, newTile);
        newTile.fixedRotation = rotation;

        AssetDatabase.CreateAsset(newTile, newPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = newTile;
        EditorGUIUtility.PingObject(newTile);
        Debug.Log($"[RotatedRuleTile] Copia con rotación fija de {(int)rotation}° creada exitosamente en: {newPath}");
    }
}
#endif
