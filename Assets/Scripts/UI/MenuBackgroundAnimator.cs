using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animador dinámico y vivo para el fondo del Menú Principal (Background Artwork / Personajes y Animales):
/// - Parallax interactivo con el cursor del ratón (sensación 2.5D de profundidad).
/// - Respiración orgánica y flotación vertical suave (para que la ilustración no sea estática).
/// - Inclinación sutil (Tilt) dinámica según la posición del cursor.
/// </summary>
public class MenuBackgroundAnimator : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Target RectTransform ---")]
    [Tooltip("RectTransform de la imagen de fondo. Si se deja vacío, se usa este GameObject.")]
    public RectTransform backgroundRect;

    [Header("--- Mouse Parallax (Profundidad 2.5D) ---")]
    [Tooltip("Habilita el desplazamiento suave en contraposición al cursor del ratón.")]
    public bool enableMouseParallax = true;

    [Tooltip("Intensidad del desplazamiento horizontal con el ratón (píxeles).")]
    public float parallaxIntensityX = 22f;

    [Tooltip("Intensidad del desplazamiento vertical con el ratón (píxeles).")]
    public float parallaxIntensityY = 14f;

    [Tooltip("Velocidad de suavizado del parallax.")]
    public float parallaxSmoothSpeed = 4f;

    [Header("--- Organic Breathing & Floating (Vida / Respiración) ---")]
    [Tooltip("Habilita la flotación y respiración periódica suave.")]
    public bool enableBreathing = true;

    [Tooltip("Velocidad de la oscilación vertical.")]
    public float floatingSpeed = 1.2f;

    [Tooltip("Distancia de flotación vertical en píxeles.")]
    public float floatingDistance = 10f;

    [Tooltip("Escala extra máxima durante el ciclo de respiración (ej. 0.025 = 1.025x).")]
    public float breathingScaleAmount = 0.025f;

    [Tooltip("Velocidad del ciclo de respiración.")]
    public float breathingScaleSpeed = 0.9f;

    [Header("--- Tilt / Inclinación Sutil ---")]
    [Tooltip("Habilita una leve inclinación rotacional según la posición del cursor.")]
    public bool enableTilt = true;

    [Tooltip("Ángulo máximo de inclinación en grados.")]
    public float maxTiltAngle = 1.2f;

    [Tooltip("Velocidad de suavizado del tilt.")]
    public float tiltSmoothSpeed = 3.5f;

    #endregion

    #region Internal State

    private Vector2 _initialAnchoredPosition;
    private Vector3 _initialScale;
    private Vector2 _currentParallaxOffset;
    private float _currentTiltZ;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (backgroundRect == null)
        {
            backgroundRect = GetComponent<RectTransform>();
        }

        if (backgroundRect != null)
        {
            _initialAnchoredPosition = backgroundRect.anchoredPosition;
            _initialScale = backgroundRect.localScale;
        }
    }

    private void Update()
    {
        if (backgroundRect == null) return;

        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        // 1. Calcular Parallax del Ratón
        Vector2 targetParallax = Vector2.zero;
        float targetTilt = 0f;

        if (enableMouseParallax || enableTilt)
        {
            Vector2 mousePos = Input.mousePosition;
            float screenW = Mathf.Max(Screen.width, 1);
            float screenH = Mathf.Max(Screen.height, 1);

            // Coordenadas normalizadas centradas (-1 a +1)
            float normX = (mousePos.x / screenW - 0.5f) * 2f;
            float normY = (mousePos.y / screenH - 0.5f) * 2f;

            normX = Mathf.Clamp(normX, -1f, 1f);
            normY = Mathf.Clamp(normY, -1f, 1f);

            targetParallax = new Vector2(-normX * parallaxIntensityX, -normY * parallaxIntensityY);
            targetTilt = -normX * maxTiltAngle;
        }

        if (enableMouseParallax)
        {
            _currentParallaxOffset = Vector2.Lerp(_currentParallaxOffset, targetParallax, dt * parallaxSmoothSpeed);
        }
        else
        {
            _currentParallaxOffset = Vector2.zero;
        }

        // 2. Calcular Flotación y Respiración
        Vector2 breathingOffset = Vector2.zero;
        Vector3 breathingScale = _initialScale;

        if (enableBreathing)
        {
            float floatY = Mathf.Sin(time * floatingSpeed) * floatingDistance;
            breathingOffset = new Vector2(0f, floatY);

            float scaleOscillation = (Mathf.Sin(time * breathingScaleSpeed) * 0.5f + 0.5f) * breathingScaleAmount;
            breathingScale = _initialScale * (1f + scaleOscillation);
        }

        // 3. Aplicar Transformaciones al Fondo
        backgroundRect.anchoredPosition = _initialAnchoredPosition + _currentParallaxOffset + breathingOffset;
        backgroundRect.localScale = breathingScale;

        // 4. Aplicar Tilt
        if (enableTilt)
        {
            _currentTiltZ = Mathf.Lerp(_currentTiltZ, targetTilt, dt * tiltSmoothSpeed);
            backgroundRect.localRotation = Quaternion.Euler(0f, 0f, _currentTiltZ);
        }
    }

    #endregion
}
