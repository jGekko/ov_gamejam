using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Custom Rule Tile que permite forzar una rotación fija (90°, 180°, 270°) en todas las tiles colocadas,
/// preservando las reglas de coincidencia y los outputs aleatorios (Random Output).
/// Ideal para clonar RuleTiles con arrays de sprites aleatorios orientados en 90°, 180° o 270°.
/// </summary>
[CreateAssetMenu(fileName = "New Rotated Rule Tile", menuName = "2D/Tiles/Rotated Rule Tile")]
public class RotatedRuleTile : RuleTile
{
    public enum TileRotationAngle
    {
        Rotation0 = 0,
        Rotation90 = 90,
        Rotation180 = 180,
        Rotation270 = 270
    }

    [Header("--- Fixed Output Placement Rotation ---")]
    [Tooltip("Rotación fija en grados aplicada a la tile colocada (90°, 180°, 270°).")]
    public TileRotationAngle fixedRotation = TileRotationAngle.Rotation90;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);

        if (fixedRotation != TileRotationAngle.Rotation0)
        {
            tileData.flags &= ~TileFlags.LockTransform;
            Matrix4x4 rotMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, (float)fixedRotation), Vector3.one);
            tileData.transform = tileData.transform * rotMatrix;
        }
    }
}
