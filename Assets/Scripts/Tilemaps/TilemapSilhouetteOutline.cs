using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Generador automático de Outline continuo para Tilemaps (Contorno exterior sin costuras internas).
/// 
/// Cómo funciona (técnica clásica de juegos como Celeste / Hollow Knight):
/// - Genera una silueta 4-direccional subyacente (debajo del Tilemap principal en sortingOrder - 1).
/// - El Tilemap principal se dibuja encima tapando todas las uniones interiores.
/// - Solo el contorno perimetral exterior sobresale (1 píxel o configurable),
///   garantizando 0 líneas divisorias entre tiles y 0 píxeles fantasma en el aire.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(TilemapRenderer))]
public class TilemapSilhouetteOutline : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Outline Style ---")]
    [Tooltip("Color del contorno exterior.")]
    public Color outlineColor = new Color(0.95f, 0.15f, 0.1f, 1f); // Rojo peligro brillante

    [Range(1, 3)]
    [Tooltip("Grosor del contorno en píxeles.")]
    public int pixelThickness = 1;

    [Tooltip("Pixels Per Unit del sprite (por defecto 16).")]
    public float pixelsPerUnit = 16f;

    [Tooltip("Offset del Sorting Order (debe ser menor al del Tilemap principal, ej. -1).")]
    public int sortingOrderOffset = -1;

    [Header("--- Material ---")]
    [Tooltip("Material para la silueta. Si se deja vacío, se usa un material sólido con tinte.")]
    public Material customSilhouetteMaterial;

    [Header("--- Auto-Update ---")]
    [Tooltip("Si es true, regenera las siluetas en el Editor al modificar el Tilemap.")]
    public bool updateInEditor = true;

    #endregion

    #region Internal State

    private Tilemap _mainTilemap;
    private TilemapRenderer _mainRenderer;
    private GameObject _outlineRoot;
    private readonly System.Collections.Generic.List<Tilemap> _outlineTilemaps = new System.Collections.Generic.List<Tilemap>();
    private Material _solidColorMaterial;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        GenerateOutline();
    }

    private void OnEnable()
    {
        Initialize();
        GenerateOutline();
    }

    private void OnValidate()
    {
        if (updateInEditor && !Application.isPlaying)
        {
            Initialize();
            GenerateOutline();
        }
    }

    #endregion

    #region Outline Generation

    public void Initialize()
    {
        _mainTilemap = GetComponent<Tilemap>();
        _mainRenderer = GetComponent<TilemapRenderer>();
    }

    [ContextMenu("⚡ Regenerar Outline")]
    public void GenerateOutline()
    {
        if (_mainTilemap == null || _mainRenderer == null)
        {
            Initialize();
        }

        if (_mainTilemap == null) return;

        ClearOldOutlines();

        // 1. Crear contenedor de siluetas
        _outlineRoot = new GameObject("_Auto_Outline_Silhouettes");
        _outlineRoot.transform.SetParent(transform, false);
        _outlineRoot.transform.localPosition = Vector3.zero;
        _outlineRoot.transform.localRotation = Quaternion.identity;
        _outlineRoot.transform.localScale = Vector3.one;
        _outlineRoot.hideFlags = HideFlags.DontSave;

        // 2. Crear material de color plano
        Material mat = GetSilhouetteMaterial();

        // 3. Offset en unidades de mundo para 1 píxel
        float pixelOffset = (1f / Mathf.Max(pixelsPerUnit, 1f)) * pixelThickness;

        // 4 direcciones cardinales (y diagonales si pixelThickness >= 2)
        Vector2[] directions;
        if (pixelThickness >= 2)
        {
            directions = new Vector2[]
            {
                new Vector2(pixelOffset, 0),
                new Vector2(-pixelOffset, 0),
                new Vector2(0, pixelOffset),
                new Vector2(0, -pixelOffset),
                new Vector2(pixelOffset, pixelOffset),
                new Vector2(-pixelOffset, pixelOffset),
                new Vector2(pixelOffset, -pixelOffset),
                new Vector2(-pixelOffset, -pixelOffset)
            };
        }
        else
        {
            directions = new Vector2[]
            {
                new Vector2(pixelOffset, 0),
                new Vector2(-pixelOffset, 0),
                new Vector2(0, pixelOffset),
                new Vector2(0, -pixelOffset)
            };
        }

        _outlineTilemaps.Clear();

        // 4. Clonar el Tilemap en cada dirección
        for (int i = 0; i < directions.Length; i++)
        {
            var dirObj = new GameObject($"Silhouette_{i}");
            dirObj.transform.SetParent(_outlineRoot.transform, false);
            dirObj.transform.localPosition = new Vector3(directions[i].x, directions[i].y, 0.05f); // Leve Z-offset hacia atrás
            dirObj.transform.localRotation = Quaternion.identity;
            dirObj.transform.localScale = Vector3.one;

            var copyTilemap = dirObj.AddComponent<Tilemap>();
            var copyRenderer = dirObj.AddComponent<TilemapRenderer>();

            // Copiar configuración de render
            copyRenderer.sortingLayerID = _mainRenderer.sortingLayerID;
            copyRenderer.sortingOrder = _mainRenderer.sortingOrder + sortingOrderOffset;
            copyRenderer.material = mat;

            // Copiar todos los tiles del tilemap principal
            CopyTilemapTiles(_mainTilemap, copyTilemap);

            // Color del contorno
            copyTilemap.color = outlineColor;

            _outlineTilemaps.Add(copyTilemap);
        }
    }

    private void CopyTilemapTiles(Tilemap source, Tilemap dest)
    {
        dest.ClearAllTiles();

        BoundsInt bounds = source.cellBounds;
        TileBase[] allTiles = source.GetTilesBlock(bounds);

        dest.SetTilesBlock(bounds, allTiles);

        // Copiar matrices de transformación / rotación de cada tile
        foreach (var pos in bounds.allPositionsWithin)
        {
            if (source.HasTile(pos))
            {
                dest.SetTransformMatrix(pos, source.GetTransformMatrix(pos));
                dest.SetColor(pos, outlineColor);
            }
        }
    }

    private Material GetSilhouetteMaterial()
    {
        if (customSilhouetteMaterial != null) return customSilhouetteMaterial;

        if (_solidColorMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            _solidColorMaterial = new Material(spriteShader)
            {
                hideFlags = HideFlags.DontSave
            };
        }

        return _solidColorMaterial;
    }

    private void ClearOldOutlines()
    {
        // Limpiar hijos existentes
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("_Auto_Outline"))
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    private void OnDestroy()
    {
        ClearOldOutlines();
        if (_solidColorMaterial != null) DestroyImmediate(_solidColorMaterial);
    }

    #endregion
}
