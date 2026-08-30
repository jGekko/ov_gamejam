using UnityEngine;
using UnityEngine.Events;
using PrimeTween;

/// <summary>
/// Aro de flores / campanilla de impulso (Speed Boost Ring) en la ciénaga.
/// Al ser atravesado por el Ave o por el Pez, otorga un potente impulso de velocidad en la dirección del aro o movimiento.
/// Incluye una animación de flotación / aleteo suave en Idle (hover) simulando el vuelo del ave que lo sostiene,
/// sin alterar las escalas del Transform para preservar la proporción visual.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FloraTargetRing : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Boost Settings ---")]
    [Tooltip("Velocidad / fuerza del impulso al atravesar el aro.")]
    public float boostSpeed = 24f;

    [Tooltip("Duración en segundos de la inercia/fuerza del impulso antes de volver a la velocidad normal.")]
    public float boostDuration = 1.2f;

    [Tooltip("Si es true, impulsa en la dirección en la que está rotado el aro (transform.right). Si es false, potencia la dirección actual del jugador.")]
    public bool useRingOrientation = true;

    [Tooltip("Cooldown mínimo antes de que este mismo aro vuelva a impulsar al jugador.")]
    public float ringCooldown = 0.5f;

    [Header("--- Idle Hover (Aleteo del Ave) ---")]
    [Tooltip("Activa la flotación/hover suave de arriba a abajo en idle.")]
    public bool enableIdleHover = true;

    [Tooltip("Amplitud del movimiento vertical (cuánto sube y baja).")]
    public float hoverAmplitude = 0.08f;

    [Tooltip("Velocidad/frecuencia del aleteo.")]
    public float hoverFrequency = 2.2f;

    [Tooltip("Distancia del pequeño brinco / retroceso al activarse.")]
    public float boostNudgeDistance = 0.14f;

    [Tooltip("Duración del retorno del brinco de activación.")]
    public float boostNudgeDuration = 0.35f;

    [Header("--- Visual & Feedback ---")]
    [Tooltip("Transform visual que contiene los sprites del aro y el ave.")]
    public Transform ringVisual;
    public ParticleSystem bloomParticles;
    public AudioSource boostAudio;

    [Header("--- Events ---")]
    public UnityEvent OnBoostApplied;

    [Header("--- Gizmos ---")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.6f);

    #endregion

    #region Public Properties & State

    public bool IsActive { get; private set; }

    #endregion

    #region Internal State

    private float _lastBoostTime = -999f;
    private Vector3 _baseLocalPosition;
    private float _randomPhaseOffset;
    private float _activationNudgeOffset = 0f;
    private Tween _nudgeTween;
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

        if (ringVisual == null)
        {
            Transform foundChild = transform.Find("Visual") ?? transform.Find("ringVisual");
            ringVisual = foundChild != null ? foundChild : transform;
        }

        if (ringVisual != null)
        {
            _baseLocalPosition = ringVisual.localPosition;
        }

        _randomPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (!enableIdleHover || ringVisual == null) return;

        // Oscilación vertical sinusoidal suave (aleteo del ave)
        float hoverOffset = Mathf.Sin((Time.time * hoverFrequency) + _randomPhaseOffset) * hoverAmplitude;
        ringVisual.localPosition = _baseLocalPosition + new Vector3(0f, hoverOffset + _activationNudgeOffset, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyBoost(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyBoost(other);
    }

    private void OnDisable()
    {
        if (_nudgeTween.isAlive) _nudgeTween.Stop();
        if (ringVisual != null)
        {
            ringVisual.localPosition = _baseLocalPosition;
        }
    }

    private void OnDestroy()
    {
        if (_nudgeTween.isAlive) _nudgeTween.Stop();
    }

    #endregion

    #region Boost Logic

    private void TryApplyBoost(Collider2D other)
    {
        if (Time.unscaledTime - _lastBoostTime < ringCooldown) return;

        var ptm = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
        if (ptm == null) return;

        // Válido para Ave o Pez
        bool isBird = ptm.currentForm == AnimalForm.Bird;
        bool isFish = ptm.currentForm == AnimalForm.Fish;

        if (!isBird && !isFish) return;

        _lastBoostTime = Time.unscaledTime;
        IsActive = true;

        // Determinar dirección del boost
        Vector2 direction;
        if (useRingOrientation)
        {
            direction = transform.right.normalized;
        }
        else
        {
            Vector2 currentVel = ptm.RB != null ? ptm.RB.linearVelocity : Vector2.zero;
            if (currentVel.sqrMagnitude > 0.1f)
            {
                direction = currentVel.normalized;
            }
            else
            {
                direction = transform.right.normalized;
            }
        }

        Vector2 boostVelocity = direction * boostSpeed;

        // Aplicar impulso según la forma
        if (isBird && ptm.birdController != null)
        {
            ptm.birdController.ApplySpeedBoost(boostVelocity, boostDuration);
        }
        else if (isFish && ptm.fishController != null)
        {
            ptm.fishController.ApplySpeedBoost(boostVelocity);
        }
        else if (ptm.RB != null)
        {
            ptm.RB.linearVelocity = boostVelocity;
        }

        // Feedback audiovisual
        if (bloomParticles != null) bloomParticles.Play();
        if (boostAudio != null) boostAudio.Play();

        // Brinco suave posicional al activarse (sin tocar escala ni deformar)
        if (ringVisual != null && boostNudgeDistance > 0f)
        {
            if (_nudgeTween.isAlive) _nudgeTween.Stop();
            _activationNudgeOffset = boostNudgeDistance;
            _nudgeTween = Tween.Custom(this, boostNudgeDistance, 0f, boostNudgeDuration, (target, val) => target._activationNudgeOffset = val, Ease.OutQuad);
        }

        OnBoostApplied?.Invoke();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.6f);

        // Flecha de dirección de impulso
        Gizmos.color = Color.green;
        Vector3 dir = useRingOrientation ? transform.right.normalized : Vector3.right;
        Vector3 center = transform.position;
        Gizmos.DrawRay(center, dir * 1.6f);
        Gizmos.DrawRay(center + dir * 1.6f, Quaternion.Euler(0, 0, 140) * dir * 0.4f);
        Gizmos.DrawRay(center + dir * 1.6f, Quaternion.Euler(0, 0, -140) * dir * 0.4f);
    }

    #endregion
}
