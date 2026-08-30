using System.Collections.Generic;
using UnityEngine;
using PrimeTween;

/// <summary>
/// Sistema de rastro de clones / siluetas fantasma (Ghost Trail / Afterimage) para personajes 2D.
/// 
/// Características:
/// - 100% independiente: Utiliza Unity y PrimeTween.
/// - Presets y perfiles integrados:
///   * CrocodileDash: Gradiente de Amarillo a Verde durante la embestida.
///   * FishSwim: Estela continua de tonos grisáceos y azul océano al nadar.
///   * HumanRun: Estela leve y sutil al correr (baja opacidad, no exagerada).
///   * Custom: Control total de gradientes, colores y tiempos.
/// - Modos de color: Color sólido, ciclo de gradiente continuo, gradiente según progreso de acción o arcoíris RGB.
/// - Sorting por defecto: Layer 'Default' en Order in Layer -5 para no tapar al personaje ni interactuables.
/// - Object Pooling optimizado: Cero basura en el Garbage Collector.
/// </summary>
public class GhostTrail : MonoBehaviour
{
    public enum GhostTrailProfile
    {
        Custom,
        CrocodileDash, // Amarillo a Verde
        FishSwim,      // Grisáceo a Azul
        HumanRun       // Leve y sutil (blanco/gris suave, baja opacidad)
    }

    public enum ColorMode
    {
        SingleColor,
        GradientCycle,
        GradientByActionProgress,
        Rainbow
    }

    #region Inspector Fields

    [Header("--- Profile & Presets ---")]
    [Tooltip("Perfil predefinido con la estética y comportamiento calibrado para cada personaje.")]
    public GhostTrailProfile profile = GhostTrailProfile.Custom;

    [Header("--- Target Sprite ---")]
    [Tooltip("SpriteRenderer del personaje a clonar. Si se deja vacío, se auto-detecta en este objeto o en sus hijos inmediatos.")]
    public SpriteRenderer targetSpriteRenderer;

    [Header("--- Color & Appearance ---")]
    [Tooltip("Modo de color para los clones generados.")]
    public ColorMode colorMode = ColorMode.SingleColor;

    [Tooltip("Color base cuando el modo está en SingleColor.")]
    public Color baseColor = new Color(0.4f, 0.7f, 1f, 1f);

    [Tooltip("Gradiente utilizado en los modos GradientCycle y GradientByActionProgress.")]
    public Gradient trailGradient;

    [Tooltip("Velocidad de ciclo del gradiente en modo GradientCycle.")]
    public float gradientCycleSpeed = 1.5f;

    [Tooltip("Transparencia/Alfa inicial del clon (0 = invisible, 1 = completamente opaco).")]
    [Range(0f, 1f)] public float baseAlpha = 0.6f;

    [Tooltip("Tiempo en segundos que tarda el clon en desvanecerse por completo.")]
    public float fadeDuration = 0.35f;

    [Tooltip("Curva de suavizado del desvanecimiento con PrimeTween.")]
    public Ease fadeEase = Ease.OutQuad;

    [Tooltip("Material personalizado opcional para los clones.")]
    public Material customGhostMaterial;

    [Header("--- Rainbow Effect (Opcional) ---")]
    [Tooltip("Velocidad de ciclo del arcoíris si el modo es Rainbow.")]
    public float rainbowCycleSpeed = 2f;
    [Range(0f, 1f)] public float rainbowSaturation = 0.85f;
    [Range(0f, 1f)] public float rainbowValue = 1f;

    [Header("--- Sorting & Rendering ---")]
    [Tooltip("Sorting Layer para los clones. Por defecto 'Default'.")]
    public string sortingLayerName = "Default";

    [Tooltip("Order in Layer para los clones. Por defecto -5 para dibujarse por detrás del personaje.")]
    public int sortingOrder = -5;

    [Header("--- Continuous Emission ---")]
    [Tooltip("Distancia mínima recorrida por el personaje para spawnear el siguiente clon cuando el trail está activo.")]
    public float distanceBetweenClones = 0.25f;

    [Tooltip("Intervalo de tiempo mínimo entre clones (permite emitir a intervalos regulares aunque la distancia varíe).")]
    public float timeBetweenClones = 0.05f;

    #endregion

    #region Public Properties & State

    public bool IsTrailActive => _isTrailActive;

    #endregion

    #region Internal State & Pooling

    private bool _isTrailActive = false;
    private Vector2 _lastSpawnPosition;
    private float _timeSinceLastSpawn;
    private float _actionProgress = 0f;
    private Transform _trailContainer;
    private readonly Queue<SpriteRenderer> _pool = new Queue<SpriteRenderer>();
    private readonly List<SpriteRenderer> _activeClones = new List<SpriteRenderer>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureTargetSpriteRenderer();

        if (profile != GhostTrailProfile.Custom)
        {
            ApplyProfileDefaults(profile);
        }

        EnsureContainer();
    }

    private void Start()
    {
        EnsureTargetSpriteRenderer();
        if (targetSpriteRenderer != null)
        {
            _lastSpawnPosition = targetSpriteRenderer.transform.position;
        }
        else
        {
            _lastSpawnPosition = transform.position;
        }
    }

    private void Update()
    {
        if (!_isTrailActive) return;

        EnsureTargetSpriteRenderer();
        if (targetSpriteRenderer == null) return;

        Vector2 currentPos = targetSpriteRenderer.transform.position;
        _timeSinceLastSpawn += Time.deltaTime;

        bool shouldSpawn = false;

        // 1. Chequeo por distancia
        if (distanceBetweenClones > 0.001f)
        {
            float distance = Vector2.Distance(currentPos, _lastSpawnPosition);
            if (distance >= distanceBetweenClones)
            {
                shouldSpawn = true;
            }
        }

        // 2. Chequeo por tiempo
        if (timeBetweenClones > 0.001f && _timeSinceLastSpawn >= timeBetweenClones)
        {
            shouldSpawn = true;
        }

        if (shouldSpawn)
        {
            SpawnClone();
            _lastSpawnPosition = currentPos;
            _timeSinceLastSpawn = 0f;
        }
    }

    private void OnDisable()
    {
        _isTrailActive = false;
        ClearAllClones();
    }

    private void OnDestroy()
    {
        if (_trailContainer != null)
        {
            Destroy(_trailContainer.gameObject);
        }
    }

    private void OnValidate()
    {
        if (profile != GhostTrailProfile.Custom)
        {
            ApplyProfileDefaults(profile);
        }
    }

    #endregion

    #region Initialization & Helpers

    public void EnsureTargetSpriteRenderer()
    {
        if (targetSpriteRenderer == null)
        {
            // Buscar ÚNICAMENTE en este GameObject o en sus hijos directos
            targetSpriteRenderer = GetComponent<SpriteRenderer>()
                ?? GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void EnsureContainer()
    {
        if (_trailContainer == null)
        {
            GameObject containerGO = new GameObject($"[GhostTrail_Container_{gameObject.name}]");
            _trailContainer = containerGO.transform;
            _trailContainer.SetParent(null);
        }
    }

    #endregion

    #region Profiles & Presets

    public void ApplyProfileDefaults(GhostTrailProfile p)
    {
        switch (p)
        {
            case GhostTrailProfile.CrocodileDash:
                // Gradiente Amarillo Brillante a Verde Intenso
                colorMode = ColorMode.GradientCycle;
                trailGradient = new Gradient();
                trailGradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1.0f, 0.92f, 0.10f), 0.0f),  // Amarillo sol brillante
                        new GradientColorKey(new Color(0.60f, 0.95f, 0.12f), 0.5f),  // Lima
                        new GradientColorKey(new Color(0.12f, 0.88f, 0.28f), 1.0f)   // Verde intenso
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 1.0f)
                    }
                );
                baseAlpha = 0.78f;
                fadeDuration = 0.32f;
                distanceBetweenClones = 0.18f;
                timeBetweenClones = 0.035f;
                gradientCycleSpeed = 4f;
                sortingLayerName = "Default";
                sortingOrder = -5;
                break;

            case GhostTrailProfile.FishSwim:
                // Gradiente de tonos Grisáceos / Pizarra a Azul Océano
                colorMode = ColorMode.GradientCycle;
                trailGradient = new Gradient();
                trailGradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.68f, 0.78f, 0.86f), 0.0f),  // Gris azulado / Pizarra suave
                        new GradientColorKey(new Color(0.38f, 0.68f, 0.92f), 0.5f),  // Celeste oceánico
                        new GradientColorKey(new Color(0.15f, 0.45f, 0.85f), 1.0f)   // Azul marino
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 1.0f)
                    }
                );
                baseAlpha = 0.50f;
                fadeDuration = 0.35f;
                distanceBetweenClones = 0.22f;
                timeBetweenClones = 0.06f;
                gradientCycleSpeed = 2f;
                sortingLayerName = "Default";
                sortingOrder = -5;
                break;

            case GhostTrailProfile.HumanRun:
                // Estela leve y sutil: Blanco/gris suave y baja opacidad
                colorMode = ColorMode.SingleColor;
                baseColor = new Color(0.92f, 0.95f, 0.98f, 1f);
                baseAlpha = 0.32f; // Sutil pero claramente visible
                fadeDuration = 0.22f;
                distanceBetweenClones = 0.28f;
                timeBetweenClones = 0.07f;
                sortingLayerName = "Default";
                sortingOrder = -5;
                break;

            case GhostTrailProfile.Custom:
                break;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Activa o desactiva la emisión continua de clones.
    /// </summary>
    public void SetTrailActive(bool active)
    {
        if (_isTrailActive == active) return;

        _isTrailActive = active;
        if (active)
        {
            EnsureTargetSpriteRenderer();
            if (targetSpriteRenderer != null)
            {
                _lastSpawnPosition = targetSpriteRenderer.transform.position;
                _timeSinceLastSpawn = 0f;
                SpawnClone(); // Spawn de clon inicial instantáneo
            }
        }
    }

    /// <summary>
    /// Establece el progreso actual de una acción (0 a 1) para evaluar el gradiente en modo GradientByActionProgress.
    /// </summary>
    public void SetActionProgress(float progress01)
    {
        _actionProgress = Mathf.Clamp01(progress01);
    }

    /// <summary>
    /// Cambia dinámicamente el SpriteRenderer a clonar.
    /// </summary>
    public void SetTargetSpriteRenderer(SpriteRenderer sr)
    {
        targetSpriteRenderer = sr;
        if (sr != null)
        {
            _lastSpawnPosition = sr.transform.position;
        }
    }

    /// <summary>
    /// Spawnea inmediatamente una silueta fantasma con los ajustes por defecto.
    /// </summary>
    public SpriteRenderer SpawnClone()
    {
        Color spawnColor = GetCurrentColor();
        return SpawnClone(spawnColor, fadeDuration);
    }

    /// <summary>
    /// Spawnea inmediatamente una silueta evaluando el gradiente en un punto específico de progreso (0 a 1).
    /// </summary>
    public SpriteRenderer SpawnCloneAtProgress(float progress01, float customFadeDuration = -1f)
    {
        Color spawnColor = baseColor;
        if (trailGradient != null)
        {
            spawnColor = trailGradient.Evaluate(Mathf.Clamp01(progress01));
        }
        return SpawnClone(spawnColor, customFadeDuration);
    }

    /// <summary>
    /// Spawnea inmediatamente una silueta fantasma con un color y duración de desvanecimiento personalizados.
    /// </summary>
    public SpriteRenderer SpawnClone(Color customColor, float customFadeDuration = -1f)
    {
        EnsureTargetSpriteRenderer();
        if (targetSpriteRenderer == null || targetSpriteRenderer.sprite == null)
        {
            return null;
        }

        EnsureContainer();
        SpriteRenderer clone = GetFromPool();
        if (clone == null) return null;

        // 1. Configuración de Transform
        Transform sourceT = targetSpriteRenderer.transform;
        clone.transform.position = sourceT.position;
        clone.transform.rotation = sourceT.rotation;
        clone.transform.localScale = sourceT.lossyScale;

        // 2. Configuración Visual
        clone.sprite = targetSpriteRenderer.sprite;
        clone.flipX = targetSpriteRenderer.flipX;
        clone.flipY = targetSpriteRenderer.flipY;

        if (customGhostMaterial != null)
        {
            clone.material = customGhostMaterial;
        }
        else if (targetSpriteRenderer.sharedMaterial != null)
        {
            clone.sharedMaterial = targetSpriteRenderer.sharedMaterial;
        }

        // 3. Sorting & Capas (Por defecto Layer Default en Order -5)
        string targetSortingLayer = !string.IsNullOrEmpty(sortingLayerName) ? sortingLayerName : "Default";
        clone.sortingLayerName = targetSortingLayer;
        clone.sortingOrder = sortingOrder;

        // 4. Color y Alfa
        Color initialColor = customColor;
        initialColor.a = baseAlpha;
        clone.color = initialColor;

        clone.gameObject.SetActive(true);
        _activeClones.Add(clone);

        // 5. Animación de desvanecimiento con PrimeTween
        float duration = customFadeDuration > 0f ? customFadeDuration : fadeDuration;
        Color targetFadeColor = initialColor;
        targetFadeColor.a = 0f;

        Tween.Color(clone, targetFadeColor, duration, fadeEase)
            .OnComplete(() => ReturnToPool(clone));

        return clone;
    }

    /// <summary>
    /// Recicla inmediatamente todos los clones activos al pool.
    /// </summary>
    public void ClearAllClones()
    {
        for (int i = _activeClones.Count - 1; i >= 0; i--)
        {
            ReturnToPool(_activeClones[i]);
        }
        _activeClones.Clear();
    }

    #endregion

    #region Internal Helpers & Pooling

    private Color GetCurrentColor()
    {
        switch (colorMode)
        {
            case ColorMode.GradientCycle:
                if (trailGradient != null)
                {
                    float t = Mathf.PingPong(Time.time * gradientCycleSpeed, 1f);
                    return trailGradient.Evaluate(t);
                }
                break;

            case ColorMode.GradientByActionProgress:
                if (trailGradient != null)
                {
                    return trailGradient.Evaluate(_actionProgress);
                }
                break;

            case ColorMode.Rainbow:
                return Color.HSVToRGB((Time.time * rainbowCycleSpeed) % 1f, rainbowSaturation, rainbowValue);

            case ColorMode.SingleColor:
            default:
                return baseColor;
        }

        return baseColor;
    }

    private SpriteRenderer GetFromPool()
    {
        EnsureContainer();

        while (_pool.Count > 0)
        {
            SpriteRenderer sr = _pool.Dequeue();
            if (sr != null && sr.gameObject != null)
            {
                return sr;
            }
        }

        GameObject cloneObj = new GameObject("GhostTrail_Clone");
        cloneObj.transform.SetParent(_trailContainer);
        SpriteRenderer newSr = cloneObj.AddComponent<SpriteRenderer>();
        return newSr;
    }

    private void ReturnToPool(SpriteRenderer clone)
    {
        if (clone == null || clone.gameObject == null) return;

        clone.gameObject.SetActive(false);
        _activeClones.Remove(clone);
        _pool.Enqueue(clone);
    }

    #endregion
}
