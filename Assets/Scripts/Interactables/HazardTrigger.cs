using UnityEngine;

/// <summary>
/// Componente para zonas de peligro letal (pinchos, abismos, trampas, lava).
/// Al entrar en contacto con el jugador, dispara la muerte inmediata y el ciclo de respawn.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HazardTrigger : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Hazard Settings ---")]
    [Tooltip("Nombre descriptivo del peligro (ej. Pinchos, Abismo, Lava).")]
    public string hazardName = "Spikes";

    [Tooltip("Cooldown mínimo antes de volver a registrar impacto.")]
    public float triggerCooldown = 0.5f;

    [Header("--- Immunity ---")]
    [Tooltip("Formas animales inmunes a este peligro (ej. Cocodrilo para espinas).")]
    public System.Collections.Generic.List<AnimalForm> immuneForms = new System.Collections.Generic.List<AnimalForm>() { AnimalForm.Crocodile };

    [Header("--- Visual & Gizmos ---")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.7f);

    #endregion

    #region Internal State

    private float _lastTriggerTime = -999f;
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
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckAndKill(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CheckAndKill(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndKill(collision.collider);
    }

    private void CheckAndKill(Collider2D other)
    {
        if (Time.unscaledTime - _lastTriggerTime < triggerCooldown) return;

        var ptm = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
        if (ptm != null)
        {
            // Comprobar inmunidad de la forma actual
            if (immuneForms != null && immuneForms.Contains(ptm.currentForm))
            {
                return;
            }

            _lastTriggerTime = Time.unscaledTime;

            if (LevelRespawnManager.Instance != null)
            {
                LevelRespawnManager.Instance.KillPlayer(ptm.transform.position);
            }
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
        Gizmos.DrawWireCube(b.center, b.size);
    }

    #endregion
}
