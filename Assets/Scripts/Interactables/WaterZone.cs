using UnityEngine;

/// <summary>
/// Componente para cuerpos de agua (lagos, estanques, ríos) en 2D.
/// Provee detección de superficie, nivel de agua y eventos para los controladores del jugador.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WaterZone : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Surface Tuning ---")]
    [Tooltip("Offset vertical respecto al borde superior del collider para definir la línea exacta de flotación/superficie.")]
    public float surfaceOffset = 0f;

    [Header("--- Water Physics Properties ---")]
    [Tooltip("Fuerza de empuje vertical hacia arriba (flotabilidad) que se aplica a cuerpos flotantes.")]
    public float buoyancyForce = 25f;

    [Tooltip("Arrastre / resistencia al movimiento dentro del agua.")]
    public float waterDrag = 3f;

    [Header("--- Debug & Visuals ---")]
    public bool showGizmos = true;
    public Color surfaceGizmoColor = new Color(0f, 0.7f, 1f, 0.8f);

    #endregion

    #region Properties

    public Collider2D WaterCollider { get; private set; }

    /// <summary>
    /// Altura Y de la superficie del agua en coordenadas de mundo.
    /// </summary>
    public float SurfaceY
    {
        get
        {
            if (WaterCollider == null) WaterCollider = GetComponent<Collider2D>();
            return WaterCollider != null ? WaterCollider.bounds.max.y + surfaceOffset : transform.position.y + surfaceOffset;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        WaterCollider = GetComponent<Collider2D>();
        if (WaterCollider != null && !WaterCollider.isTrigger)
        {
            WaterCollider.isTrigger = true;
        }
    }

    #endregion

    #region Public Helper Methods

    /// <summary>
    /// Calcula la distancia desde una posición hasta la superficie del agua.
    /// Retorna un valor positivo si la posición está por debajo de la superficie.
    /// </summary>
    public float GetDistanceToSurface(Vector2 position)
    {
        return SurfaceY - position.y;
    }

    /// <summary>
    /// Determina si una posición dada se encuentra dentro del volumen de agua.
    /// </summary>
    public bool ContainsPoint(Vector2 position)
    {
        if (WaterCollider == null) WaterCollider = GetComponent<Collider2D>();
        return WaterCollider != null && WaterCollider.OverlapPoint(position);
    }

    #endregion

    #region Visuals Integration & Splash

    [Header("--- Visuals Integration ---")]
    [Tooltip("Referencia opcional a PixelWaterVisuals para sincronización visual.")]
    public PixelWaterVisuals waterVisuals;

    [Tooltip("Si es true, genera salpicaduras de partículas al entrar/salir del agua.")]
    public bool enableSplashParticles = true;

    [Tooltip("Velocidad mínima para activar salpicadura.")]
    public float minSplashSpeed = 2f;

    #endregion

    #region Trigger Handling

    private void OnTriggerEnter2D(Collider2D other)
    {
        var transformManager = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
        if (transformManager != null)
        {
            transformManager.OnEnterWater(this);
        }

        if (enableSplashParticles)
        {
            var rb = other.attachedRigidbody;
            float speed = 3f;
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                speed = Mathf.Abs(rb.linearVelocity.y);
#else
                speed = Mathf.Abs(rb.velocity.y);
#endif
            }
            if (speed >= minSplashSpeed)
            {
                float splashX = other.bounds.center.x;
                PixelWaterSplash.Instance.SpawnSplash(new Vector2(splashX, SurfaceY), Mathf.Clamp(speed / 6f, 0.7f, 1.8f));
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var transformManager = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
        if (transformManager != null)
        {
            transformManager.OnExitWater(this);
        }

        if (enableSplashParticles)
        {
            var rb = other.attachedRigidbody;
            float speed = 3f;
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                speed = Mathf.Abs(rb.linearVelocity.y);
#else
                speed = Mathf.Abs(rb.velocity.y);
#endif
            }
            if (speed >= minSplashSpeed)
            {
                float splashX = other.bounds.center.x;
                PixelWaterSplash.Instance.SpawnSplash(new Vector2(splashX, SurfaceY), Mathf.Clamp(speed / 7f, 0.5f, 1.4f));
            }
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Collider2D col = WaterCollider != null ? WaterCollider : GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = surfaceGizmoColor;
        Bounds b = col.bounds;
        float y = b.max.y + surfaceOffset;

        Gizmos.DrawLine(new Vector3(b.min.x, y, 0f), new Vector3(b.max.x, y, 0f));
    }

    #endregion
}
