using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Peligro con superficie sólida (ej. Lecho de espinas, zarzas con espinas, cactus).
/// 
/// Características:
/// - Posee un Collider2D sólido (NO trigger), permitiendo que actúe como suelo físico.
/// - La Babilla (Cocodrilo) es inmune por defecto gracias a sus escamas blindadas y puede caminar encima.
/// - Si el Humano, Pez o Ave colisionan con la superficie, mueren de inmediato.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SolidHazard : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Hazard Settings ---")]
    [Tooltip("Nombre descriptivo del peligro sólido.")]
    public string hazardName = "Solid Thorns";

    [Tooltip("Formas animales inmunes que pueden caminar/pararse sobre este peligro.")]
    public List<AnimalForm> immuneForms = new List<AnimalForm>() { AnimalForm.Crocodile };

    [Tooltip("Cooldown mínimo antes de volver a registrar impacto.")]
    public float triggerCooldown = 0.5f;

    [Header("--- Visual & Gizmos ---")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(1f, 0.4f, 0f, 0.7f);

    #endregion

    #region Internal State

    private float _lastTriggerTime = -999f;
    private Collider2D _collider;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        // Asegurar que sea collider físico sólido para que la babilla pueda caminar encima
        if (_collider != null && _collider.isTrigger)
        {
            _collider.isTrigger = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndKill(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckAndKill(collision.collider);
    }

    private void CheckAndKill(Collider2D other)
    {
        if (Time.unscaledTime - _lastTriggerTime < triggerCooldown) return;

        var ptm = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
        if (ptm != null)
        {
            // Si la forma actual es inmune (ej. Cocodrilo), permitirle caminar y pararse encima
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
