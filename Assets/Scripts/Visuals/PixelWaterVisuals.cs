using UnityEngine;

/// <summary>
/// Controlador visual para cuerpos de agua Pixel Art 2D.
/// Actualiza automáticamente las propiedades del shader (superficie, PPU, colores, oleaje y reflejos)
/// sincronizándose con la posición del objeto o con el Collider de WaterZone.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PixelWaterVisuals : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Color Palette ---")]
    [Tooltip("Color del agua en la superficie / zonas poco profundas.")]
    public Color shallowColor = new Color(0.15f, 0.65f, 0.85f, 0.82f);

    [Tooltip("Color del agua en la profundidad.")]
    public Color deepColor = new Color(0.05f, 0.20f, 0.45f, 0.95f);

    [Tooltip("Color de la espuma y crestas de onda.")]
    public Color foamColor = new Color(0.92f, 0.98f, 1.0f, 1.0f);

    [Range(2, 8)]
    [Tooltip("Cantidad de bandas de color discretas para el gradiente retro.")]
    public int colorBands = 4;

    [Header("--- Pixel Art Tuning ---")]
    [Tooltip("Resolución en Pixels Per Unit (PPU). 16 para estética estándar 16x16.")]
    public float pixelsPerUnit = 16f;

    [Range(1, 30)]
    [Tooltip("Tasa de cuadros para la animación retro escalonada (ej. 8 a 12 FPS).")]
    public float animationFPS = 10f;

    [Header("--- Wave & Foam Dynamics ---")]
    [Tooltip("Velocidad de movimiento horizontal del oleaje.")]
    public float waveSpeed = 2.5f;

    [Tooltip("Frecuencia horizontal de las crestas de ola.")]
    public float waveFrequency = 3.0f;

    [Tooltip("Amplitud de la onda en píxeles enteros.")]
    public float waveAmplitudePixels = 1.0f;

    [Range(0f, 4f)]
    [Tooltip("Grosor de la línea de espuma en píxeles.")]
    public float foamThicknessPixels = 1.5f;

    [Header("--- Reflection Settings ---")]
    [Tooltip("Habilita o deshabilita el reflejo en tiempo real de los elementos sobre el agua.")]
    public bool enableReflection = true;

    [Range(0f, 1f)]
    [Tooltip("Intensidad del reflejo.")]
    public float reflectionIntensity = 0.55f;

    [Range(0f, 4f)]
    [Tooltip("Distorsión horizontal del reflejo en píxeles.")]
    public float reflectionDistortionPixels = 1.0f;

    [Tooltip("Distancia vertical (en unidades de mundo) en la que el reflejo se desvanece con la profundidad.")]
    public float reflectionFadeDistance = 2.5f;

    [Tooltip("Tinte de color aplicado sobre el reflejo reflejado.")]
    public Color reflectionTint = new Color(0.80f, 0.90f, 1.0f, 1.0f);

    [Header("--- Surface Offset ---")]
    [Tooltip("Offset vertical manual respecto a la parte superior del collider/sprite.")]
    public float surfaceOffset = 0f;

    #endregion

    #region Private Fields & Shader Property IDs

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private Collider2D _collider;

    private static readonly int PropShallowColor = Shader.PropertyToID("_ShallowColor");
    private static readonly int PropDeepColor = Shader.PropertyToID("_DeepColor");
    private static readonly int PropFoamColor = Shader.PropertyToID("_FoamColor");
    private static readonly int PropColorBands = Shader.PropertyToID("_ColorBands");
    private static readonly int PropPPU = Shader.PropertyToID("_PPU");
    private static readonly int PropFPS = Shader.PropertyToID("_FPS");
    private static readonly int PropWaveSpeed = Shader.PropertyToID("_WaveSpeed");
    private static readonly int PropWaveFrequency = Shader.PropertyToID("_WaveFrequency");
    private static readonly int PropWaveAmplitude = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int PropFoamThickness = Shader.PropertyToID("_FoamThickness");
    private static readonly int PropEnableReflection = Shader.PropertyToID("_EnableReflection");
    private static readonly int PropReflectionIntensity = Shader.PropertyToID("_ReflectionIntensity");
    private static readonly int PropReflectionDistortion = Shader.PropertyToID("_ReflectionDistortion");
    private static readonly int PropReflectionFadeDistance = Shader.PropertyToID("_ReflectionFadeDistance");
    private static readonly int PropReflectionTint = Shader.PropertyToID("_ReflectionTint");
    private static readonly int PropSurfaceWorldY = Shader.PropertyToID("_SurfaceWorldY");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
        UpdateMaterialProperties();
    }

    private void OnEnable()
    {
        Initialize();
        UpdateMaterialProperties();
    }

    private void Update()
    {
        // En el editor o cuando el agua se mueva, mantener sincronizada la superficie
        UpdateMaterialProperties();
    }

    private void OnValidate()
    {
        Initialize();
        UpdateMaterialProperties();
    }

    #endregion

    #region Initialization & Property Sync

    private void Initialize()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_collider == null) _collider = GetComponent<Collider2D>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Calcula la posición Y de la superficie en coordenadas de mundo.
    /// </summary>
    public float GetCalculatedSurfaceWorldY()
    {
        if (_collider != null)
        {
            return _collider.bounds.max.y + surfaceOffset;
        }

        if (_renderer != null)
        {
            return _renderer.bounds.max.y + surfaceOffset;
        }

        return transform.position.y + surfaceOffset;
    }

    /// <summary>
    /// Aplica todos los parámetros configurados al shader utilizando MaterialPropertyBlock para máximo rendimiento.
    /// </summary>
    public void UpdateMaterialProperties()
    {
        if (_renderer == null) return;
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        _renderer.GetPropertyBlock(_propBlock);

        _propBlock.SetColor(PropShallowColor, shallowColor);
        _propBlock.SetColor(PropDeepColor, deepColor);
        _propBlock.SetColor(PropFoamColor, foamColor);
        _propBlock.SetFloat(PropColorBands, colorBands);

        _propBlock.SetFloat(PropPPU, Mathf.Max(pixelsPerUnit, 1f));
        _propBlock.SetFloat(PropFPS, Mathf.Max(animationFPS, 1f));
        _propBlock.SetFloat(PropWaveSpeed, waveSpeed);
        _propBlock.SetFloat(PropWaveFrequency, waveFrequency);
        _propBlock.SetFloat(PropWaveAmplitude, waveAmplitudePixels);
        _propBlock.SetFloat(PropFoamThickness, foamThicknessPixels);

        _propBlock.SetFloat(PropEnableReflection, enableReflection ? 1f : 0f);
        _propBlock.SetFloat(PropReflectionIntensity, reflectionIntensity);
        _propBlock.SetFloat(PropReflectionDistortion, reflectionDistortionPixels);
        _propBlock.SetFloat(PropReflectionFadeDistance, reflectionFadeDistance);
        _propBlock.SetColor(PropReflectionTint, reflectionTint);

        float surfaceY = GetCalculatedSurfaceWorldY();
        _propBlock.SetFloat(PropSurfaceWorldY, surfaceY);

        _renderer.SetPropertyBlock(_propBlock);
    }

    #endregion
}
