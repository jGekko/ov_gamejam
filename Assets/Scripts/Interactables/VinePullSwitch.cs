using UnityEngine;
using UnityEngine.Events;
using PrimeTween;

/// <summary>
/// Interruptor/Llave flotante coleccionable interactuable por cualquier forma del jugador (Humano, Ave, Cocodrilo, Pez).
/// Al recogerlo/tocarlo, activa compuertas de raíces (RootGate) o mecanismos vinculados con feedback jugoso de PrimeTween.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class VinePullSwitch : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Target Mechanism ---")]
    [Tooltip("Compuerta de raíces vinculada que se abrirá al recoger esta llave/interruptor.")]
    public RootGate targetGate;

    [Header("--- Activation Settings ---")]
    [Tooltip("Tecla para interactuar manualmente si triggerOnTouch es false.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Si es true, se activa automáticamente al contacto con el jugador sin presionar teclas.")]
    public bool triggerOnTouch = true;

    [Tooltip("Si es true, la compuerta se abre temporalmente durante 'timerDuration' segundos y la llave reaparece.")]
    public bool isTimed = true;

    [Tooltip("Duración en segundos que permanece abierta la compuerta antes de cerrarse y reaparecer la llave.")]
    public float timerDuration = 5f;

    [Tooltip("Cooldown mínimo antes de poder volver a interactuar.")]
    public float pullCooldown = 0.5f;

    [Header("--- Floating & Collectible Visuals (Juice) ---")]
    [Tooltip("Transform del objeto visual de la llave (si es nulo, usa este transform o su primer hijo).")]
    public Transform vineVisual;

    [Tooltip("Distancia de flotación vertical oscilante.")]
    public float floatBobHeight = 0.25f;

    [Tooltip("Duración de cada ciclo de flotación.")]
    public float floatBobDuration = 1.2f;

    [Tooltip("Velocidad de rotación continua opcional (grados por segundo).")]
    public float rotateSpeed = 0f;

    [Header("--- Audio & Particles ---")]
    public AudioSource pullAudio;
    public ParticleSystem pullParticles;

    [Header("--- Events ---")]
    public UnityEvent OnPulled;
    public UnityEvent OnTimerExpired;

    #endregion

    #region Public Properties & State

    public bool IsActive { get; private set; }
    public bool IsCollected { get; private set; }

    #endregion

    #region Internal State

    private Collider2D _collider;
    private bool _playerInRange;
    private float _lastPullTime = -999f;
    private Vector3 _initialLocalPos;
    private Vector3 _initialLocalScale = Vector3.one;
    private Tween _bobTween;
    private Tween _collectTween;
    private Tween _timerTween;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider != null) _collider.isTrigger = true;

        if (vineVisual == null)
        {
            vineVisual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        _initialLocalPos = vineVisual.localPosition;
        _initialLocalScale = vineVisual.localScale;
    }

    private void Start()
    {
        StartFloatingAnimation();
    }

    private void Update()
    {
        if (rotateSpeed != 0f && vineVisual != null && !IsCollected)
        {
            vineVisual.Rotate(Vector3.forward, rotateSpeed * Time.deltaTime, Space.Self);
        }

        if (_playerInRange && !triggerOnTouch && !IsCollected && Input.GetKeyDown(interactKey))
        {
            TryPull();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsCollected) return;

        // Aceptar al jugador en cualquier forma (Humano, Ave, Cocodrilo, Pez)
        if (IsPlayer(other))
        {
            _playerInRange = true;
            if (triggerOnTouch)
            {
                TryPull();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            _playerInRange = false;
        }
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    private void OnDisable()
    {
        KillAllTweens();
    }

    #endregion

    #region Interaction & Collectible Logic

    private bool IsPlayer(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponent<PlayerTransformationManager>() != null || col.GetComponentInParent<PlayerTransformationManager>() != null) return true;
        if (col.GetComponent<HumanController>() != null || col.GetComponentInParent<HumanController>() != null) return true;
        if (col.GetComponent<BirdController>() != null || col.GetComponentInParent<BirdController>() != null) return true;
        if (col.GetComponent<CrocodileController>() != null || col.GetComponentInParent<CrocodileController>() != null) return true;
        if (col.GetComponent<FishController>() != null || col.GetComponentInParent<FishController>() != null) return true;
        return false;
    }

    public void TryPull()
    {
        if (IsCollected) return;
        if (Time.unscaledTime - _lastPullTime < pullCooldown) return;
        _lastPullTime = Time.unscaledTime;

        IsActive = true;
        IsCollected = true;

        // Desactivar collider temporalmente
        if (_collider != null) _collider.enabled = false;

        // Feedback de sonido y partículas
        if (pullAudio != null) pullAudio.Play();
        if (pullParticles != null) pullParticles.Play();

        // Animación de recolección (Pop juice -> encoger a 0)
        StopFloatingAnimation();
        if (vineVisual != null)
        {
            _collectTween.Stop();
            _collectTween = Tween.Scale(vineVisual, _initialLocalScale * 1.35f, 0.12f, Ease.OutBack)
                .OnComplete(() =>
                {
                    _collectTween = Tween.Scale(vineVisual, Vector3.zero, 0.18f, Ease.InBack)
                        .OnComplete(() =>
                        {
                            if (vineVisual != null && !isTimed)
                            {
                                vineVisual.gameObject.SetActive(false);
                            }
                        });
                });
        }

        // Abrir compuerta vinculada
        if (targetGate != null)
        {
            targetGate.Open();
        }

        OnPulled?.Invoke();

        // Si es temporizado, programar cierre de compuerta y reaparición de la llave
        if (isTimed)
        {
            _timerTween.Stop();
            _timerTween = Tween.Delay(timerDuration, () =>
            {
                RespawnKey();
            });
        }
    }

    private void RespawnKey()
    {
        IsActive = false;
        IsCollected = false;

        // Cerrar compuerta
        if (targetGate != null)
        {
            targetGate.Close();
        }

        OnTimerExpired?.Invoke();

        // Reaparecer visual con efecto pop de PrimeTween
        if (vineVisual != null)
        {
            vineVisual.gameObject.SetActive(true);
            vineVisual.localPosition = _initialLocalPos;
            vineVisual.localScale = Vector3.zero;

            _collectTween.Stop();
            _collectTween = Tween.Scale(vineVisual, _initialLocalScale, 0.35f, Ease.OutBack)
                .OnComplete(StartFloatingAnimation);
        }

        // Reactivar collider
        if (_collider != null) _collider.enabled = true;
    }

    #endregion

    #region Floating Animation (PrimeTween)

    private void StartFloatingAnimation()
    {
        if (vineVisual == null) return;

        _bobTween.Stop();
        vineVisual.localPosition = _initialLocalPos;
        Vector3 targetPos = _initialLocalPos + Vector3.up * floatBobHeight;

        _bobTween = Tween.LocalPosition(vineVisual, targetPos, floatBobDuration, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
    }

    private void StopFloatingAnimation()
    {
        _bobTween.Stop();
    }

    private void KillAllTweens()
    {
        _bobTween.Stop();
        _collectTween.Stop();
        _timerTween.Stop();
    }

    #endregion
}
