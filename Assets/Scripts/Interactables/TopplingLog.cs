using UnityEngine;
using UnityEngine.Events;
using PrimeTween;

/// <summary>
/// Tronco de mangle vertical que se derriba con la embestida de la Babilla (Cocodrilo)
/// para convertirse en un puente horizontal transitable.
/// 
/// Soporta:
/// 1. Pivote automático alrededor de basePivot (sin importar si es hijo, padre o transform externo).
/// 2. Detección robusta de la embestida (IsDashing) del cocodrilo.
/// 3. Reseteo automático al reaparecer el jugador.
/// </summary>
public class TopplingLog : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Pivot & Rotation Setup ---")]
    [Tooltip("Punto de pivote en la base del tronco alrededor del cual rota. Puede ser un GameObject hijo colocado en la base inferior o el mismo transform.")]
    public Transform basePivot;

    [Tooltip("Duración de la caída del tronco.")]
    public float fallDuration = 0.65f;

    [Tooltip("Ángulo final al caer hacia la derecha (en grados).")]
    public float targetAngleRight = -90f;

    [Tooltip("Ángulo final al caer hacia la izquierda (en grados).")]
    public float targetAngleLeft = 90f;

    [Header("--- Reset Settings ---")]
    [Tooltip("Si es true, el tronco vuelve a su posición vertical cuando el jugador muere.")]
    public bool resetOnPlayerRespawn = true;

    [Header("--- Visual & Feedback ---")]
    public ParticleSystem impactParticles;
    public ParticleSystem landingParticles;
    public AudioSource impactAudio;
    public AudioSource landingAudio;

    [Header("--- Events ---")]
    public UnityEvent OnToppled;
    public UnityEvent OnReset;

    #endregion

    #region Public Properties & State

    public bool IsToppled { get; private set; }

    #endregion

    #region Internal State

    private Quaternion _initialRotation;
    private Vector3 _initialPosition;
    private Vector3 _pivotWorldPos;
    private Tween _rotationTween;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _initialRotation = transform.rotation;
        _initialPosition = transform.position;
        _pivotWorldPos = basePivot != null ? basePivot.position : transform.position;
    }

    private void Start()
    {
        if (LevelRespawnManager.Instance != null && resetOnPlayerRespawn)
        {
            LevelRespawnManager.Instance.OnPlayerRespawned += HandlePlayerRespawn;
        }
    }

    private void OnDestroy()
    {
        if (_rotationTween.isAlive) _rotationTween.Stop();
        if (LevelRespawnManager.Instance != null && resetOnPlayerRespawn)
        {
            LevelRespawnManager.Instance.OnPlayerRespawned -= HandlePlayerRespawn;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndTopple(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckAndTopple(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckAndTopple(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CheckAndTopple(other);
    }

    #endregion

    #region Topple Logic

    private void CheckAndTopple(Collider2D other)
    {
        if (IsToppled) return;

        // Detectar si el atacante es el Cocodrilo / Babilla
        CrocodileController croco = other.GetComponent<CrocodileController>() ?? other.GetComponentInParent<CrocodileController>();
        if (croco == null)
        {
            var ptm = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
            if (ptm != null && ptm.currentForm == AnimalForm.Crocodile)
            {
                croco = ptm.crocodileController;
            }
        }

        if (croco != null && croco.IsDashing)
        {
            // Determinar dirección de caída según la dirección de movimiento/impacto
            float hitDirX = croco.FacingDirection != 0 ? croco.FacingDirection : (croco.transform.position.x < transform.position.x ? 1f : -1f);
            Topple(hitDirX > 0);
        }
    }

    public void Topple(bool toppleRight)
    {
        if (IsToppled) return;
        IsToppled = true;

        if (impactParticles != null) impactParticles.Play();
        if (impactAudio != null) impactAudio.Play();

        Vector3 pivotPos = basePivot != null ? basePivot.position : transform.position;
        float targetAngle = toppleRight ? targetAngleRight : targetAngleLeft;

        if (_rotationTween.isAlive) _rotationTween.Stop();

        // Si el pivote coincide con transform o es su padre directo
        if (basePivot == null || basePivot == transform)
        {
            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);
            _rotationTween = Tween.Rotation(transform, targetRot, fallDuration, ease: Ease.OutBounce)
                .OnComplete(FinishLanding);
        }
        else
        {
            // Rotación orbital exacta de transform alrededor de pivotPos
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            _rotationTween = Tween.Custom(0f, targetAngle, fallDuration, ease: Ease.OutBounce, onValueChange: currentAngle =>
            {
                Quaternion rotOffset = Quaternion.Euler(0f, 0f, currentAngle);
                transform.rotation = rotOffset * startRot;
                transform.position = pivotPos + (rotOffset * (startPos - pivotPos));
            })
            .OnComplete(FinishLanding);
        }
    }

    private void FinishLanding()
    {
        if (landingParticles != null) landingParticles.Play();
        if (landingAudio != null) landingAudio.Play();
        OnToppled?.Invoke();
    }

    public void ResetLog()
    {
        if (!IsToppled) return;
        IsToppled = false;

        if (_rotationTween.isAlive) _rotationTween.Stop();

        transform.rotation = _initialRotation;
        transform.position = _initialPosition;

        OnReset?.Invoke();
    }

    private void HandlePlayerRespawn()
    {
        ResetLog();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 pivot = basePivot != null ? basePivot.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pivot, 0.25f);
        Gizmos.DrawLine(pivot + Vector3.left * 0.3f, pivot + Vector3.right * 0.3f);
        Gizmos.DrawLine(pivot + Vector3.down * 0.3f, pivot + Vector3.up * 0.3f);
    }

    #endregion
}
