using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Gestor de postprocesado dinámico para efectos de tiempo ralentizado / cámara lenta (Slow Motion).
/// 
/// Características:
/// - Desatura la pantalla suavemente al ralentizar el tiempo para transformarse o activar la rueda.
/// - Añade efectos cinemáticos ajustables:
///   * Desaturación / Color Adjustments (enfoque y pérdida de color ambiental).
///   * Aberración Cromática (Warp de dilatación temporal en los bordes).
///   * Viñeta focal (oscurece y enfoca la atención en el jugador).
///   * Realce de Bloom (hace que las estelas, UI y elementos brillantes resalten).
///   * Distorsión de lente sutil.
/// - Completamente Plug-and-Play: Si no existe un Volume en la escena, crea un Volume global en tiempo de ejecución.
/// - Funciona con Time.unscaledDeltaTime para transiciones 100% fluidas incluso a Time.timeScale = 0.05.
/// </summary>
[DisallowMultipleComponent]
public class SlowMotionPostProcessManager : MonoBehaviour
{
    public enum SlowMoPreset
    {
        CinematicDesaturate, // Desaturación cinematográfica + viñeta + aberración cromática (Default)
        EtherealDream,       // Tinte frío cian/azul + desaturación media + bloom elevado
        NoirFocus,           // Blanco y negro dramático (-100 saturación) + viñeta intensa
        CyberWarp,           // Aberración cromática fuerte + contraste alto + tinte místico
        Custom               // Valores personalizados configurados manualmente
    }

    #region Inspector Fields

    [Header("--- Preset de Postprocesado ---")]
    [Tooltip("Preset visual a aplicar durante la cámara lenta.")]
    public SlowMoPreset preset = SlowMoPreset.CinematicDesaturate;

    [Header("--- Volume Reference ---")]
    [Tooltip("Volume de URP a controlar (si es null, busca uno existente o crea uno global automáticamente).")]
    public Volume targetVolume;

    [Header("--- Transición Suave (Unscaled Time) ---")]
    [Tooltip("Velocidad de transición al entrar y salir del slow-mo.")]
    [Range(1f, 30f)] public float transitionSpeed = 14f;

    [Header("--- Valores en Slow Motion (Preset Custom) ---")]
    [Tooltip("Saturación objetivo (-100 = escala de grises completa, 0 = normal).")]
    [Range(-100f, 0f)] public float slowMoSaturation = -75f;

    [Tooltip("Ajuste de contraste en slow-mo.")]
    [Range(0f, 50f)] public float slowMoContrast = 15f;

    [Tooltip("Filtro de color / tinte en slow-mo.")]
    public Color slowMoColorFilter = new Color(0.88f, 0.96f, 1.0f);

    [Tooltip("Intensidad de viñeta en slow-mo.")]
    [Range(0f, 1f)] public float slowMoVignette = 0.38f;

    [Tooltip("Intensidad de aberración cromática en slow-mo (Warp temporal).")]
    [Range(0f, 1f)] public float slowMoChromaticAberration = 0.45f;

    [Tooltip("Intensidad adicional de bloom en slow-mo.")]
    [Range(0f, 3f)] public float slowMoBloomBonus = 0.6f;

    [Tooltip("Distorsión de lente en slow-mo (-0.2 = ligero efecto ojo de pez).")]
    [Range(-0.5f, 0.5f)] public float slowMoLensDistortion = -0.12f;

    [Header("--- Live Testing in Inspector ---")]
    [Tooltip("Activa este checkbox en tiempo de ejecución para previsualizar el efecto de cámara lenta en vivo sin necesidad de abrir la rueda.")]
    public bool previewSlowMoInEditor = false;

    #endregion

    #region Public Properties & State

    public static SlowMotionPostProcessManager Instance { get; private set; }

    public bool IsSlowMoActive { get; private set; }
    public float CurrentWeight => _currentWeight;

    #endregion

    #region Internal State

    private float _targetWeight = 0f;
    private float _currentWeight = 0f;

    // Componentes de URP Volume
    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private ChromaticAberration _chromaticAberration;
    private Bloom _bloom;
    private LensDistortion _lensDistortion;

    private float _baseBloomIntensity = 1f;
    private bool _isInitialized;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeVolumeAndOverrides();
    }

    private void Update()
    {
        if (previewSlowMoInEditor)
        {
            _targetWeight = 1.0f;
        }
        else if (!IsSlowMoActive)
        {
            _targetWeight = 0.0f;
        }

        UpdateTransition();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _isInitialized)
        {
            ApplyPresetValues();
            if (previewSlowMoInEditor)
            {
                _targetWeight = 1.0f;
            }
            else if (!IsSlowMoActive)
            {
                _targetWeight = 0.0f;
            }
        }
    }

    private void OnDestroy()
    {
        if (targetVolume != null && targetVolume.profile != null)
        {
            // Resetear peso
            targetVolume.weight = 0f;
        }
    }

    #endregion

    #region Initialization & Auto-Setup

    public void InitializeVolumeAndOverrides()
    {
        if (_isInitialized) return;

        // 1. Auto-buscar Volume existente o crear uno global dedicado
        if (targetVolume == null)
        {
            // Buscar un Volume que tenga prioridad alta o crear uno nuevo
            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var v in volumes)
            {
                if (v.isGlobal && v.name.Contains("SlowMo"))
                {
                    targetVolume = v;
                    break;
                }
            }

            if (targetVolume == null)
            {
                GameObject volGo = new GameObject("[SlowMoPostProcessVolume]");
                targetVolume = volGo.AddComponent<Volume>();
                targetVolume.isGlobal = true;
                targetVolume.priority = 99f;
                targetVolume.weight = 0f;

                // Crear VolumeProfile runtime
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "SlowMo_RuntimeProfile";
                targetVolume.profile = profile;
            }
        }

        if (targetVolume.profile == null)
        {
            targetVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        VolumeProfile p = targetVolume.profile;

        // 2. Obtener o agregar overrides necesarios
        if (!p.TryGet(out _colorAdjustments))
        {
            _colorAdjustments = p.Add<ColorAdjustments>(true);
        }
        _colorAdjustments.saturation.overrideState = true;
        _colorAdjustments.contrast.overrideState = true;
        _colorAdjustments.colorFilter.overrideState = true;

        if (!p.TryGet(out _vignette))
        {
            _vignette = p.Add<Vignette>(true);
        }
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;
        _vignette.rounded.overrideState = true;
        _vignette.rounded.value = true;
        _vignette.smoothness.value = 0.5f;

        if (!p.TryGet(out _chromaticAberration))
        {
            _chromaticAberration = p.Add<ChromaticAberration>(true);
        }
        _chromaticAberration.intensity.overrideState = true;

        if (!p.TryGet(out _bloom))
        {
            _bloom = p.Add<Bloom>(true);
        }
        if (_bloom != null)
        {
            _baseBloomIntensity = _bloom.intensity.value;
        }

        if (!p.TryGet(out _lensDistortion))
        {
            _lensDistortion = p.Add<LensDistortion>(true);
        }
        _lensDistortion.intensity.overrideState = true;

        ApplyPresetValues();
        targetVolume.weight = 0f;
        _isInitialized = true;
    }

    #endregion

    #region Preset Configuration

    public void ApplyPresetValues()
    {
        switch (preset)
        {
            case SlowMoPreset.CinematicDesaturate:
            default:
                slowMoSaturation = -75f;
                slowMoContrast = 15f;
                slowMoColorFilter = new Color(0.88f, 0.96f, 1.0f);
                slowMoVignette = 0.38f;
                slowMoChromaticAberration = 0.45f;
                slowMoBloomBonus = 0.6f;
                slowMoLensDistortion = -0.12f;
                break;

            case SlowMoPreset.EtherealDream:
                slowMoSaturation = -45f;
                slowMoContrast = 20f;
                slowMoColorFilter = new Color(0.75f, 0.90f, 1.0f);
                slowMoVignette = 0.30f;
                slowMoChromaticAberration = 0.35f;
                slowMoBloomBonus = 1.2f;
                slowMoLensDistortion = -0.08f;
                break;

            case SlowMoPreset.NoirFocus:
                slowMoSaturation = -100f;
                slowMoContrast = 30f;
                slowMoColorFilter = Color.white;
                slowMoVignette = 0.50f;
                slowMoChromaticAberration = 0.20f;
                slowMoBloomBonus = 0.3f;
                slowMoLensDistortion = -0.15f;
                break;

            case SlowMoPreset.CyberWarp:
                slowMoSaturation = -25f;
                slowMoContrast = 25f;
                slowMoColorFilter = new Color(0.95f, 0.80f, 1.0f);
                slowMoVignette = 0.42f;
                slowMoChromaticAberration = 0.85f;
                slowMoBloomBonus = 0.8f;
                slowMoLensDistortion = -0.22f;
                break;

            case SlowMoPreset.Custom:
                // Conserva los valores del Inspector
                break;
        }

        if (_colorAdjustments != null)
        {
            _colorAdjustments.saturation.value = slowMoSaturation;
            _colorAdjustments.contrast.value = slowMoContrast;
            _colorAdjustments.colorFilter.value = slowMoColorFilter;
        }

        if (_vignette != null)
        {
            _vignette.intensity.value = slowMoVignette;
        }

        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = slowMoChromaticAberration;
        }

        if (_lensDistortion != null)
        {
            _lensDistortion.intensity.value = slowMoLensDistortion;
        }
    }

    #endregion

    #region Transitions & Updates

    private void UpdateTransition()
    {
        if (!_isInitialized) InitializeVolumeAndOverrides();
        if (targetVolume == null) return;

        // Interpolar peso con unscaledDeltaTime para total fluidez en slow-mo
        _currentWeight = Mathf.MoveTowards(_currentWeight, _targetWeight, transitionSpeed * Time.unscaledDeltaTime);
        targetVolume.weight = _currentWeight;

        // Actualizar Bloom dinámico si está disponible
        if (_bloom != null && _bloom.intensity.overrideState)
        {
            _bloom.intensity.value = _baseBloomIntensity + (slowMoBloomBonus * _currentWeight);
        }
    }

    /// <summary>
    /// Activa o desactiva el efecto de postprocesado de cámara lenta.
    /// </summary>
    public void SetSlowMoActive(bool active)
    {
        IsSlowMoActive = active;
        _targetWeight = active ? 1.0f : 0.0f;
    }

    /// <summary>
    /// Permite modular directamente el peso (ej. vinculado al valor exacto de Time.timeScale).
    /// </summary>
    public void SetSlowMoWeight(float weight)
    {
        _targetWeight = Mathf.Clamp01(weight);
        IsSlowMoActive = _targetWeight > 0.01f;
    }

    #endregion
}
