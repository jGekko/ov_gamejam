using UnityEngine;

/// <summary>
/// Componente de soporte para capas de fondo y reflejos con scroll infinito y parallax.
/// Permite controlar la velocidad de desplazamiento y vincular el movimiento con la cámara principal.
/// </summary>
[ExecuteAlways]
public class BackgroundScroller : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Auto Scroll Speed ---")]
    [Tooltip("Velocidad de desplazamiento horizontal continuo.")]
    public float scrollSpeedX = 0.03f;

    [Tooltip("Velocidad de desplazamiento vertical continuo.")]
    public float scrollSpeedY = 0.0f;

    [Header("--- Parallax Tracking ---")]
    [Tooltip("Si es true, añade efecto parallax según el movimiento de la cámara.")]
    public bool enableParallax = false;

    [Tooltip("Referencia a la cámara. Si es null, usa Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Factor de parallax horizontal (0 = fondo estático/lejano, 1 = se mueve con la cámara).")]
    public float parallaxFactorX = 0.05f;

    [Tooltip("Factor de parallax vertical.")]
    public float parallaxFactorY = 0.0f;

    [Header("--- Pixel Art Tuning ---")]
    [Tooltip("Resolución PPU para cuantizar el movimiento.")]
    public float pixelsPerUnit = 16f;

    [Range(0, 30)]
    [Tooltip("Tasa de cuadros para la animación escalonada (0 = suave).")]
    public float animationFPS = 10f;

    #endregion

    #region Private Fields & Shader IDs

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private Vector3 _lastCamPos;
    private Vector2 _currentOffset;

    private static readonly int PropScrollSpeedX = Shader.PropertyToID("_ScrollSpeedX");
    private static readonly int PropScrollSpeedY = Shader.PropertyToID("_ScrollSpeedY");
    private static readonly int PropPPU = Shader.PropertyToID("_PPU");
    private static readonly int PropFPS = Shader.PropertyToID("_FPS");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null) _lastCamPos = targetCamera.transform.position;
    }

    private void Update()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        if (enableParallax)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                Vector3 delta = targetCamera.transform.position - _lastCamPos;
                _currentOffset.x += delta.x * parallaxFactorX;
                _currentOffset.y += delta.y * parallaxFactorY;
                _lastCamPos = targetCamera.transform.position;
            }
        }

        UpdateMaterial();
    }

    private void OnValidate()
    {
        Initialize();
        UpdateMaterial();
    }

    #endregion

    #region Helpers

    private void Initialize()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
    }

    private void UpdateMaterial()
    {
        if (_renderer == null) return;
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(PropScrollSpeedX, scrollSpeedX);
        _propBlock.SetFloat(PropScrollSpeedY, scrollSpeedY);
        _propBlock.SetFloat(PropPPU, pixelsPerUnit);
        _propBlock.SetFloat(PropFPS, animationFPS);
        _renderer.SetPropertyBlock(_propBlock);
    }

    #endregion
}
