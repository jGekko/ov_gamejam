using UnityEngine;

/// <summary>
/// Corriente de agua rápida en caños y canales de la ciénaga.
/// Otorga un fuerte impulso de velocidad (Speed Boost) al Pez,
/// permitiéndole nadar a alta velocidad y realizar saltos acrobáticos fuera del agua.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WaterCurrentStream : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Flow Settings ---")]
    [Tooltip("Dirección del flujo de la corriente (normalizada).")]
    public Vector2 flowDirection = Vector2.right;

    [Tooltip("Velocidad base de la corriente de agua.")]
    public float flowSpeed = 10f;

    [Tooltip("Multiplicador de velocidad exclusivo para el Pez (Turbo / Speed Boost).")]
    public float fishBoostMultiplier = 1.8f;

    [Tooltip("Fuerza de arrastre suave para las demás formas.")]
    public float otherFormsDriftForce = 4f;

    [Header("--- Visual & Gizmos ---")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(0f, 0.6f, 1f, 0.4f);

    #endregion

    #region Internal State

    private Collider2D _collider;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider != null && !_collider.isTrigger)
        {
            _collider.isTrigger = true;
        }

        if (flowDirection.sqrMagnitude > 0.001f)
        {
            flowDirection.Normalize();
        }
        else
        {
            flowDirection = Vector2.right;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        var ptm = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
        if (ptm != null && ptm.RB != null)
        {
            ApplyCurrentPhysics(ptm);
        }
    }

    #endregion

    #region Current Physics

    private void ApplyCurrentPhysics(PlayerTransformationManager ptm)
    {
        float dt = Time.fixedDeltaTime;
        Vector2 currentVel = ptm.RB.linearVelocity;

        if (ptm.currentForm == AnimalForm.Fish)
        {
            // Boost acelerado para el Pez en la dirección de la corriente
            Vector2 targetVel = flowDirection * (flowSpeed * fishBoostMultiplier);
            Vector2 boostedVel = Vector2.MoveTowards(currentVel, targetVel, 45f * dt);

            // Permitir que el pez conserve su control lateral perpendicular
            ptm.RB.linearVelocity = boostedVel;
        }
        else
        {
            // Arrastre suave para otras formas dentro de la corriente
            ptm.RB.linearVelocity += flowDirection * (otherFormsDriftForce * dt);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        if (_collider == null) _collider = GetComponent<Collider2D>();
        if (_collider == null) return;

        Gizmos.color = gizmoColor;
        Bounds b = _collider.bounds;
        Gizmos.DrawCube(b.center, b.size);

        // Flecha de dirección del flujo
        Gizmos.color = Color.cyan;
        Vector3 dir = (Vector3)flowDirection.normalized;
        Vector3 center = b.center;
        Gizmos.DrawRay(center, dir * 1.5f);
        Gizmos.DrawRay(center + dir * 1.5f, Quaternion.Euler(0, 0, 140) * dir * 0.4f);
        Gizmos.DrawRay(center + dir * 1.5f, Quaternion.Euler(0, 0, -140) * dir * 0.4f);
    }

    #endregion
}
