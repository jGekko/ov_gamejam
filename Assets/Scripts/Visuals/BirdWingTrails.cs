using System;
using UnityEngine;

/// <summary>
/// Sistema de estelas aerodinámicas estilizadas (Wingtip Trails / Contrails) para las alas del Ave.
/// 
/// Características:
/// - Genera dos estelas paralelas continuas (ala izquierda y derecha) como un avión a reacción / caza de combate.
/// - Estilo Pixel Art configurable con cuantización de bandas, grosor y transparencia escalonada.
/// - Control de Opacidad / Alpha general y desvanecimiento suave a 100% transparente over lifetime.
/// - Completamente Plug-and-Play: Auto-instancia y auto-configura los GameObjects TrailRenderer si no existen.
/// - Presets estéticos intercambiables (GoldenPhoenix, AeroVapor, SpiritWisp, SonicWhite, Custom).
/// - Reactividad física dinámica:
///   * El grosor y la longitud crecen con la velocidad de vuelo.
///   * Efecto de viraje/fuerza G (Banking): Al girar bruscamente, el ala exterior intensifica su estela.
///   * Modo Overdrive durante Speed Boosts.
/// - Limpieza automática (Clear) en transformaciones y reapariciones para evitar líneas fantasma.
/// </summary>
[DisallowMultipleComponent]
public class BirdWingTrails : MonoBehaviour
{
    public enum WingTrailPreset
    {
        GoldenPhoenix,  // Fuego dorado, ámbar y destellos solares (Default)
        AeroVapor,      // Cian eléctrico y vapor blanco aerodinámico
        SpiritWisp,     // Degradado místico púrpura, violeta y cian espectral
        SonicWhite,     // Estela pura blanca y plateada de jet supersónico
        Custom          // Gradiente personalizado definido en el Inspector
    }

    #region Inspector Fields

    [Header("--- Estilo Visual & Presets ---")]
    [Tooltip("Preset visual de color y brillo para las estelas.")]
    public WingTrailPreset preset = WingTrailPreset.GoldenPhoenix;

    [Tooltip("Material estilizado para las estelas (si es null, auto-busca Mat_BirdWingTrail o el Shader correspondiente).")]
    public Material trailMaterial;

    [Tooltip("Multiplicador general de opacidad / transparencia para ambas estelas.")]
    [Range(0.0f, 1.0f)] public float overallAlpha = 0.9f;

    [Tooltip("Gradiente personalizado utilizado cuando el Preset está en 'Custom'.")]
    public Gradient customGradient;

    [Header("--- Pixel Art Quantization ---")]
    [Tooltip("Activa la pixelación procedural estilo retro en la textura y bordes de la estela.")]
    public bool enablePixelation = true;

    [Tooltip("Cantidad de bandas/bloques pixelados a lo largo de la estela.")]
    [Range(4f, 64f)] public float pixelStepsLength = 28f;

    [Tooltip("Cantidad de niveles de grosor pixelado a lo ancho de la estela.")]
    [Range(2f, 16f)] public float pixelStepsWidth = 6f;

    [Tooltip("Cantidad de niveles discretos de transparencia pixel art (0 = desvanecimiento suave).")]
    [Range(0f, 16f)] public float alphaQuantizationSteps = 6f;

    [Header("--- Anchors & Offset de las Alas ---")]
    [Tooltip("Transform del ala izquierda (si es null, se crea automáticamente).")]
    public Transform leftWingAnchor;

    [Tooltip("Transform del ala derecha (si es null, se crea automáticamente).")]
    public Transform rightWingAnchor;

    [Tooltip("Offset local respecto al centro del sprite (X = distancia del centro al ala, Y = posición vertical en el sprite).")]
    public Vector2 wingOffset = new Vector2(0.48f, -0.05f);

    [Header("--- Dimensiones & Tapering Aerodinámico ---")]
    [Tooltip("Duración base de la estela en segundos a velocidad de crucero.")]
    [Range(0.1f, 1.5f)] public float baseTrailLifetime = 0.32f;

    [Tooltip("Duración de la estela durante Speed Boost.")]
    [Range(0.2f, 2.5f)] public float boostTrailLifetime = 0.55f;

    [Tooltip("Grosor inicial base en la punta del ala.")]
    [Range(0.05f, 0.6f)] public float baseStartWidth = 0.16f;

    [Tooltip("Grosor inicial durante Speed Boost.")]
    [Range(0.1f, 1.0f)] public float boostStartWidth = 0.28f;

    [Tooltip("Distancia mínima entre vértices de la estela (menor = curva más suave en giros).")]
    [Range(0.01f, 0.1f)] public float minVertexDistance = 0.035f;

    [Header("--- Reactividad de Vuelo ---")]
    [Tooltip("Velocidad mínima requerida para emitir estelas.")]
    public float minSpeedToEmit = 0.8f;

    [Tooltip("Multiplicador de grosor en el ala exterior al realizar virajes cerrados (G-Force).")]
    [Range(1.0f, 2.5f)] public float bankingGForceMultiplier = 1.35f;

    [Tooltip("Ajuste de Sorting Order relativo al SpriteRenderer del ave (ej. -1 para emitir justo detrás del cuerpo).")]
    public int sortingOrderOffset = -1;

    [Header("--- Gizmos ---")]
    public bool showGizmos = true;

    #endregion

    #region Public Properties & References

    public BirdController Bird { get; private set; }
    public TrailRenderer LeftTrail { get; private set; }
    public TrailRenderer RightTrail { get; private set; }

    #endregion

    #region Internal State

    private Vector2 _previousVelocityDir = Vector2.right;
    private float _angularTurnRate;
    private bool _isInitialized;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        InitializeComponents();
        ClearTrails();
    }

    private void OnDisable()
    {
        ClearTrails();
    }

    private void LateUpdate()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }

        UpdateTrailsDynamics();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _isInitialized)
        {
            ApplyGradientPreset();
            ApplyMaterialProperties();
        }
    }

    #endregion

    #region Initialization & Auto-Setup

    public void InitializeComponents()
    {
        if (Bird == null)
        {
            Bird = GetComponent<BirdController>() ?? GetComponentInParent<BirdController>();
        }

        Transform root = Bird != null && Bird.visualsRoot != null ? Bird.visualsRoot : transform;

        // Auto-crear o asignar Anclas de las Alas
        if (leftWingAnchor == null)
        {
            Transform existingLeft = root.Find("LeftWingTrailAnchor");
            if (existingLeft == null)
            {
                var go = new GameObject("LeftWingTrailAnchor");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(-wingOffset.x, wingOffset.y, 0f);
                go.transform.localRotation = Quaternion.identity;
                leftWingAnchor = go.transform;
            }
            else
            {
                leftWingAnchor = existingLeft;
            }
        }

        if (rightWingAnchor == null)
        {
            Transform existingRight = root.Find("RightWingTrailAnchor");
            if (existingRight == null)
            {
                var go = new GameObject("RightWingTrailAnchor");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(wingOffset.x, wingOffset.y, 0f);
                go.transform.localRotation = Quaternion.identity;
                rightWingAnchor = go.transform;
            }
            else
            {
                rightWingAnchor = existingRight;
            }
        }

        // Auto-asignar o cargar Material de estela
        if (trailMaterial == null)
        {
            trailMaterial = Resources.Load<Material>("Mat_BirdWingTrail");
            if (trailMaterial == null)
            {
                Shader shader = Shader.Find("Custom/2D/StylizedWingTrail") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    trailMaterial = new Material(shader);
                    trailMaterial.name = "Runtime_BirdWingTrail";
                }
            }
        }

        // Configurar TrailRenderers
        LeftTrail = SetupTrailRenderer(leftWingAnchor.gameObject, "LeftTrailRenderer");
        RightTrail = SetupTrailRenderer(rightWingAnchor.gameObject, "RightTrailRenderer");

        ApplyGradientPreset();
        ApplyMaterialProperties();
        _isInitialized = true;
    }

    private TrailRenderer SetupTrailRenderer(GameObject targetGo, string trailName)
    {
        TrailRenderer trail = targetGo.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = targetGo.AddComponent<TrailRenderer>();
        }

        trail.name = trailName;
        trail.time = baseTrailLifetime;
        trail.minVertexDistance = minVertexDistance;
        trail.autodestruct = false;
        trail.emitting = true;
        trail.numCornerVertices = 5;
        trail.numCapVertices = 5;
        trail.alignment = LineAlignment.TransformZ;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        // Curva ahusada aerodinámica (Sleek aerodynamic taper)
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(new Keyframe(0.0f, 1.0f, 0.0f, -0.2f));
        widthCurve.AddKey(new Keyframe(0.35f, 0.75f, -0.8f, -0.8f));
        widthCurve.AddKey(new Keyframe(1.0f, 0.0f, -1.2f, 0.0f));
        trail.widthCurve = widthCurve;
        trail.widthMultiplier = baseStartWidth;

        if (trailMaterial != null)
        {
            trail.material = trailMaterial;
        }

        // Sincronizar Sorting Layer con el SpriteRenderer del ave
        SpriteRenderer sr = Bird != null ? Bird.spriteRenderer : GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            trail.sortingLayerID = sr.sortingLayerID;
            trail.sortingOrder = sr.sortingOrder + sortingOrderOffset;
        }

        return trail;
    }

    #endregion

    #region Presets & Gradients

    /// <summary>
    /// Aplica el gradiente de color según el preset seleccionado y el alpha general.
    /// </summary>
    public void ApplyGradientPreset()
    {
        Gradient g = GetPresetGradient(preset);

        if (LeftTrail != null) LeftTrail.colorGradient = g;
        if (RightTrail != null) RightTrail.colorGradient = g;
    }

    public void ApplyMaterialProperties()
    {
        Material mat = trailMaterial;
        if (mat == null && LeftTrail != null) mat = LeftTrail.material;

        if (mat != null)
        {
            mat.SetFloat("_OverallAlpha", overallAlpha);
            mat.SetFloat("_EnablePixelation", enablePixelation ? 1f : 0f);
            mat.SetFloat("_PixelStepsX", pixelStepsLength);
            mat.SetFloat("_PixelStepsY", pixelStepsWidth);
            mat.SetFloat("_AlphaSteps", alphaQuantizationSteps);
        }
    }

    public Gradient GetPresetGradient(WingTrailPreset p)
    {
        Gradient g = new Gradient();

        switch (p)
        {
            case WingTrailPreset.GoldenPhoenix:
            default:
                // Destello solar blanco cálido -> Ámbar dorado brillante -> Naranja fuego -> Rojo profundo -> 0 Alpha (Transparente)
                g.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1.0f, 0.98f, 0.88f), 0.0f),  // Destello solar
                        new GradientColorKey(new Color(1.0f, 0.78f, 0.12f), 0.25f), // Oro brillante
                        new GradientColorKey(new Color(1.0f, 0.42f, 0.05f), 0.60f), // Ámbar / Fuego
                        new GradientColorKey(new Color(0.85f, 0.15f, 0.02f), 1.0f)  // Rescoldo final
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(overallAlpha * 1.0f, 0.0f),
                        new GradientAlphaKey(overallAlpha * 0.85f, 0.28f),
                        new GradientAlphaKey(overallAlpha * 0.35f, 0.65f),
                        new GradientAlphaKey(0.0f, 1.0f) // 100% transparente al final de su vida útil
                    }
                );
                break;

            case WingTrailPreset.AeroVapor:
                // Blanco brillante en el ala -> Cian eléctrico aero -> Faded sky mist -> Transparente
                g.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.95f, 0.99f, 1.0f), 0.0f),
                        new GradientColorKey(new Color(0.25f, 0.85f, 0.98f), 0.25f),
                        new GradientColorKey(new Color(0.12f, 0.55f, 0.88f), 0.65f),
                        new GradientColorKey(new Color(0.08f, 0.32f, 0.65f), 1.0f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(overallAlpha * 0.95f, 0.0f),
                        new GradientAlphaKey(overallAlpha * 0.80f, 0.30f),
                        new GradientAlphaKey(overallAlpha * 0.35f, 0.70f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                break;

            case WingTrailPreset.SpiritWisp:
                // Blanco etéreo -> Violeta místico -> Cian espectral -> Transparente
                g.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1.0f, 0.95f, 1.0f), 0.0f),
                        new GradientColorKey(new Color(0.72f, 0.35f, 0.95f), 0.35f),
                        new GradientColorKey(new Color(0.28f, 0.70f, 0.95f), 0.75f),
                        new GradientColorKey(new Color(0.15f, 0.25f, 0.70f), 1.0f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(overallAlpha * 0.95f, 0.0f),
                        new GradientAlphaKey(overallAlpha * 0.75f, 0.35f),
                        new GradientAlphaKey(overallAlpha * 0.30f, 0.70f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                break;

            case WingTrailPreset.SonicWhite:
                // Estela blanca pura y plateada de jet supersónico -> Transparente
                g.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(Color.white, 0.0f),
                        new GradientColorKey(new Color(0.88f, 0.92f, 0.96f), 0.5f),
                        new GradientColorKey(new Color(0.75f, 0.82f, 0.90f), 1.0f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(overallAlpha * 0.90f, 0.0f),
                        new GradientAlphaKey(overallAlpha * 0.60f, 0.40f),
                        new GradientAlphaKey(overallAlpha * 0.20f, 0.80f),
                        new GradientAlphaKey(0.0f, 1.0f)
                    }
                );
                break;

            case WingTrailPreset.Custom:
                if (customGradient != null && customGradient.colorKeys.Length > 0)
                {
                    return customGradient;
                }
                goto default;
        }

        return g;
    }

    #endregion

    #region Dynamics & Aerodynamics

    private void UpdateTrailsDynamics()
    {
        if (LeftTrail == null || RightTrail == null) return;

        // Posicionar anclas en el offset local
        if (leftWingAnchor != null)
        {
            leftWingAnchor.localPosition = new Vector3(-wingOffset.x, wingOffset.y, 0f);
        }
        if (rightWingAnchor != null)
        {
            rightWingAnchor.localPosition = new Vector3(wingOffset.x, wingOffset.y, 0f);
        }

        float currentSpeed = Bird != null ? Bird.CurrentSpeed : 0f;
        Vector2 currentVel = Bird != null ? Bird.CurrentVelocity : Vector2.zero;
        float maxSpeed = Bird != null ? Bird.maxFlightSpeed : 12f;

        // 1. Control de Emisión por Velocidad
        bool shouldEmit = currentSpeed >= minSpeedToEmit && gameObject.activeInHierarchy;
        LeftTrail.emitting = shouldEmit;
        RightTrail.emitting = shouldEmit;

        if (!shouldEmit) return;

        // 2. Factor de velocidad y boost
        float speedRatio = Mathf.Clamp01(currentSpeed / Mathf.Max(1f, maxSpeed));
        bool isBoosting = currentSpeed > (maxSpeed * 1.05f);

        float targetLifetime = Mathf.Lerp(baseTrailLifetime, boostTrailLifetime, isBoosting ? 1.0f : speedRatio * 0.6f);
        LeftTrail.time = targetLifetime;
        RightTrail.time = targetLifetime;

        // 3. Cálculo de viraje / fuerza G (Banking effect)
        float leftWidthMult = 1.0f;
        float rightWidthMult = 1.0f;

        if (currentVel.sqrMagnitude > 0.01f)
        {
            Vector2 currentDir = currentVel.normalized;
            // Producto cruz 2D para determinar giro a izquierda o derecha
            float turnCross = (currentDir.x * _previousVelocityDir.y) - (currentDir.y * _previousVelocityDir.x);
            _angularTurnRate = Mathf.Lerp(_angularTurnRate, turnCross / Mathf.Max(0.001f, Time.deltaTime), 12f * Time.deltaTime);
            _previousVelocityDir = currentDir;

            // Viraje a la izquierda (_angularTurnRate > 0) -> Ala derecha exterior recibe mayor vórtice
            if (_angularTurnRate > 0.1f)
            {
                float factor = Mathf.Clamp01(_angularTurnRate * 0.25f);
                rightWidthMult = Mathf.Lerp(1.0f, bankingGForceMultiplier, factor);
            }
            // Viraje a la derecha (_angularTurnRate < 0) -> Ala izquierda exterior recibe mayor vórtice
            else if (_angularTurnRate < -0.1f)
            {
                float factor = Mathf.Clamp01(-_angularTurnRate * 0.25f);
                leftWidthMult = Mathf.Lerp(1.0f, bankingGForceMultiplier, factor);
            }
        }

        // 4. Aplicar anchos finales
        float baseWidth = isBoosting ? boostStartWidth : Mathf.Lerp(baseStartWidth * 0.8f, baseStartWidth * 1.2f, speedRatio);
        LeftTrail.widthMultiplier = baseWidth * leftWidthMult;
        RightTrail.widthMultiplier = baseWidth * rightWidthMult;
    }

    #endregion

    #region Public Utility Methods

    /// <summary>
    /// Limpia instantáneamente las estelas activas (útil al transformarse o teletransportarse para evitar líneas cruzadas).
    /// </summary>
    public void ClearTrails()
    {
        if (LeftTrail != null)
        {
            LeftTrail.Clear();
        }
        if (RightTrail != null)
        {
            RightTrail.Clear();
        }
    }

    /// <summary>
    /// Cambia el preset en tiempo de ejecución.
    /// </summary>
    public void SetPreset(WingTrailPreset newPreset)
    {
        preset = newPreset;
        ApplyGradientPreset();
        ApplyMaterialProperties();
    }

    /// <summary>
    /// Modifica la opacidad general de las estelas.
    /// </summary>
    public void SetOverallAlpha(float alpha)
    {
        overallAlpha = Mathf.Clamp01(alpha);
        ApplyGradientPreset();
        ApplyMaterialProperties();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Transform root = Bird != null && Bird.visualsRoot != null ? Bird.visualsRoot : transform;

        Vector3 leftPos = root.TransformPoint(new Vector3(-wingOffset.x, wingOffset.y, 0f));
        Vector3 rightPos = root.TransformPoint(new Vector3(wingOffset.x, wingOffset.y, 0f));

        Gizmos.color = new Color(1.0f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(leftPos, 0.06f);
        Gizmos.DrawWireSphere(rightPos, 0.06f);
        Gizmos.DrawLine(leftPos, rightPos);
    }

    #endregion
}
