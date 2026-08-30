using UnityEngine;

/// <summary>
/// Standalone 2D Sphere / Free Flight Controller (Gamejam Edition).
/// 
/// Características:
/// - Vuelo 360° Cinemático en Gravedad Cero: Aceleración suave y respuesta rápida de giro (Turn Responsiveness).
/// - Vuelo continuo hacia adelante: El ave nunca se detiene en seco ni cae en idle; cuando no hay input,
///   sigue volando hacia la última dirección ingresada, obligando al jugador a maniobrar constantemente.
/// - Separación visual: 'visualsRoot' separado del Rigidbody para permitir Pixel Aligners / Pixel Perfect.
/// - Animación continua de vuelo.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BirdController : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Visual & Orientation (360° Flight) ---")]
    [Tooltip("Transform hijo que contiene los sprites del ave.")]
    public Transform visualsRoot;
    [Tooltip("Componente Animator asignado al ave.")]
    public Animator animator;
    [Tooltip("SpriteRenderer opcional en el objeto visual.")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Velocidad de rotación del visual hacia la dirección de vuelo (ej. 12 = suave, 0 = rotación instantánea hacia el movimiento).")]
    public float rotationSpeed = 12f;
    [Tooltip("Offset angular en grados. Por defecto -90° porque el sprite del ave apunta hacia arriba.")]
    public float angleOffset = -90f;

    [Header("--- 360° Free Flight Movement ---")]
    [Tooltip("Velocidad de vuelo continuo hacia adelante en espacio 360°.")]
    public float maxFlightSpeed = 18f;
    [Tooltip("Límite absoluto de velocidad (para impulsos/boosts).")]
    public float maxAbsoluteSpeed = 50f;
    [Tooltip("Tasa de aceleración hacia la dirección del input.")]
    public float acceleration = 45f;
    [Tooltip("Multiplicador de respuesta al girar en dirección opuesta a la velocidad actual.")]
    public float turnResponsiveness = 4f;

    [Header("--- Debug & Gizmos ---")]
    public bool showGizmos = true;
    public Color gizmoVelocityColor = Color.cyan;

    [Header("--- Wing Trails (Airplane FX) ---")]
    [Tooltip("Controlador de estelas aerodinámicas estilizadas en las puntas de las alas.")]
    public BirdWingTrails wingTrails;
    [Tooltip("Si es true, auto-añade y configura BirdWingTrails si no está presente.")]
    public bool autoSetupWingTrails = true;

    #endregion

    #region Public Properties & State

    public Rigidbody2D RB { get; private set; }
    public Collider2D MainCollider { get; private set; }
    public CircleCollider2D CircleCol { get; private set; }

    public Vector2 CurrentVelocity => _currentVelocity;
    public float CurrentSpeed => _currentVelocity.magnitude;
    public Vector2 LastFlightDirection => _lastFlightDirection;
    public int FacingDirection { get; private set; } = 1;

    // Inputs virtualizables (puedes sobreescribirlos o alimentarlos externamente)
    public Vector2 MoveInput { get; set; }

    #endregion

    #region Internal State

    private Vector2 _currentVelocity;
    private Vector2 _lastFlightDirection = Vector2.right;
    private PlayerTransformationManager _transformationManager;
    private float _controlLockTimer;
    private float _boostTimer;
    private float _boostDuration = 1.2f;
    private float _boostInitialSpeed;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        RB = GetComponent<Rigidbody2D>();
        MainCollider = GetComponent<Collider2D>();
        CircleCol = GetComponent<CircleCollider2D>();
        _transformationManager = GetComponent<PlayerTransformationManager>() ?? GetComponentInParent<PlayerTransformationManager>();
        CacheVisualReferences();

        if (RB != null)
        {
            RB.bodyType = RigidbodyType2D.Dynamic;
            RB.gravityScale = 0f;
            RB.freezeRotation = true;
            RB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            RB.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    protected virtual void OnEnable()
    {
        CacheVisualReferences();
        if (RB != null)
        {
            RB.gravityScale = 0f;
        }
        _controlLockTimer = 0f;
        _boostTimer = 0f;

        // Iniciar dirección de vuelo hacia la derecha o hacia la velocidad inicial
        if (_currentVelocity.sqrMagnitude > 0.1f)
        {
            _lastFlightDirection = _currentVelocity.normalized;
        }
        else
        {
            _lastFlightDirection = Vector2.right;
            _currentVelocity = _lastFlightDirection * maxFlightSpeed;
        }

        if (wingTrails != null)
        {
            wingTrails.ClearTrails();
        }
    }

    protected virtual void OnDisable()
    {
        if (wingTrails != null)
        {
            wingTrails.ClearTrails();
        }
    }

    public void CacheVisualReferences()
    {
        if (visualsRoot == null)
        {
            var ptm = GetComponent<PlayerTransformationManager>() ?? GetComponentInParent<PlayerTransformationManager>();
            if (ptm != null && ptm.birdVisuals != null)
            {
                visualsRoot = ptm.birdVisuals.transform;
            }
            else
            {
                Transform found = transform.Find("BirdVisuals") ?? transform.Find("Bird") ?? transform.Find("Visuals");
                if (found != null) visualsRoot = found;
                else if (transform.childCount > 0)
                {
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var child = transform.GetChild(i);
                        if (child.name.ToLower().Contains("bird") || child.name.ToLower().Contains("birb"))
                        {
                            visualsRoot = child;
                            break;
                        }
                    }
                    if (visualsRoot == null) visualsRoot = transform.GetChild(0);
                }
                else visualsRoot = transform;
            }
        }

        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            if (visualsRoot != null)
            {
                animator = visualsRoot.GetComponent<Animator>() ?? visualsRoot.GetComponentInChildren<Animator>(true);
            }

            if (animator == null)
            {
                var allAnimators = GetComponentsInChildren<Animator>(true);
                foreach (var a in allAnimators)
                {
                    if (a.name.ToLower().Contains("bird") || a.name.ToLower().Contains("birb") ||
                        (a.runtimeAnimatorController != null && (a.runtimeAnimatorController.name.ToLower().Contains("bird") || a.runtimeAnimatorController.name.ToLower().Contains("birb"))))
                    {
                        animator = a;
                        if (visualsRoot == null || visualsRoot == transform) visualsRoot = a.transform;
                        break;
                    }
                }
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
            }
        }

        if (spriteRenderer == null && visualsRoot != null)
        {
            spriteRenderer = visualsRoot.GetComponent<SpriteRenderer>() ?? visualsRoot.GetComponentInChildren<SpriteRenderer>(true);
        }
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (wingTrails == null)
        {
            wingTrails = GetComponent<BirdWingTrails>() ?? GetComponentInChildren<BirdWingTrails>(true);
            if (wingTrails == null && autoSetupWingTrails)
            {
                Transform targetHost = visualsRoot != null ? visualsRoot : transform;
                wingTrails = targetHost.GetComponent<BirdWingTrails>() ?? targetHost.gameObject.AddComponent<BirdWingTrails>();
            }
        }
    }

    public void SetInitialVelocity(Vector2 initialVel)
    {
        _currentVelocity = initialVel;
        if (initialVel.sqrMagnitude > 0.1f)
        {
            _lastFlightDirection = initialVel.normalized;
        }
        if (RB != null) RB.linearVelocity = initialVel;
    }

    /// <summary>
    /// Aplica un impulso de velocidad (Speed Boost) dinámico con desaceleración suave.
    /// </summary>
    public void ApplySpeedBoost(Vector2 boostVelocity, float boostDuration = 1.2f)
    {
        _boostDuration = Mathf.Max(0.1f, boostDuration);
        _boostTimer = _boostDuration;
        _boostInitialSpeed = boostVelocity.magnitude;
        _currentVelocity = boostVelocity;

        if (boostVelocity.sqrMagnitude > 0.1f)
        {
            _lastFlightDirection = boostVelocity.normalized;
        }
        if (RB != null) RB.linearVelocity = boostVelocity;
    }

    /// <summary>
    /// Notificación de entrada/salida de agua. El ave no puede estar en el agua y fuerza transformación a Humano.
    /// </summary>
    public void SetInWater(bool inWater, WaterZone waterZone)
    {
        if (inWater && gameObject.activeInHierarchy && enabled)
        {
            if (_transformationManager != null)
            {
                _transformationManager.ForceRevertToHuman();
            }
        }
    }

    protected virtual void Update()
    {
        GatherInput();
        UpdateTimers();
        HandleVisualOrientation();
    }

    protected virtual void FixedUpdate()
    {
        Handle360FlightPhysics();
    }

    #endregion

    #region Input Collection

    protected virtual void GatherInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        MoveInput = new Vector2(h, v);
    }

    #endregion

    #region 360° Free Flight Physics

    private void UpdateTimers()
    {
        if (_controlLockTimer > 0f) _controlLockTimer -= Time.deltaTime;
    }

    private void Handle360FlightPhysics()
    {
        if (RB == null) return;

        float dt = Time.fixedDeltaTime;
        Vector2 input = _controlLockTimer <= 0f ? MoveInput : Vector2.zero;

        Vector2 targetDir = _lastFlightDirection;

        // Si hay input, actualizar la dirección objetivo
        if (input.sqrMagnitude > 0.01f)
        {
            targetDir = input.normalized;
            _lastFlightDirection = targetDir;
        }

        // Determinar velocidad objetivo y tasa de aceleración según el estado del boost
        float targetSpeed = maxFlightSpeed;
        float accelRate = acceleration;

        if (_boostTimer > 0f)
        {
            _boostTimer -= dt;
            float t = Mathf.Clamp01(_boostTimer / _boostDuration);
            // Curva suave de decaimiento desde _boostInitialSpeed hasta maxFlightSpeed
            targetSpeed = Mathf.Lerp(maxFlightSpeed, _boostInitialSpeed, t);
            // Suavizar la tasa de frenado para que la inercia del boost se sienta prolongada y fluida
            accelRate = Mathf.Lerp(acceleration, 12f, t);
        }

        Vector2 targetVelocity = targetDir * targetSpeed;
        float currentSpeed = _currentVelocity.magnitude;

        if (currentSpeed > 0.1f)
        {
            float alignment = Vector2.Dot(_currentVelocity.normalized, targetDir);
            float turnFactor = Mathf.Lerp(Mathf.Max(1f, turnResponsiveness), 1f, (alignment + 1f) * 0.5f);
            accelRate *= turnFactor;
        }

        _currentVelocity = Vector2.MoveTowards(_currentVelocity, targetVelocity, accelRate * dt);

        // Permitir que la velocidad supere maxAbsoluteSpeed durante el impulso del boost
        float currentMaxLimit = Mathf.Max(maxAbsoluteSpeed, _boostInitialSpeed);
        if (_currentVelocity.magnitude > currentMaxLimit)
        {
            _currentVelocity = _currentVelocity.normalized * currentMaxLimit;
        }

        // Movimiento por Física Dinámica (respeta colisiones)
        RB.linearVelocity = _currentVelocity;
        RB.rotation = 0f;
    }

    #endregion

    #region Visuals & Orientation

    private void HandleVisualOrientation()
    {
        if (visualsRoot == null) return;

        // Mantener escala visual positiva sin deformar
        Vector3 currentScale = visualsRoot.localScale;
        if (currentScale.x < 0f)
        {
            currentScale.x = Mathf.Abs(currentScale.x);
            visualsRoot.localScale = currentScale;
        }

        Vector2 dir = _lastFlightDirection;
        if (_currentVelocity.sqrMagnitude > 0.1f)
        {
            dir = _currentVelocity.normalized;
        }

        if (dir.sqrMagnitude > 0.01f)
        {
            float targetAngle = (Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg) + angleOffset;
            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);

            if (rotationSpeed > 0.01f)
            {
                visualsRoot.rotation = Quaternion.RotateTowards(visualsRoot.rotation, targetRot, rotationSpeed * 100f * Time.deltaTime);
            }
            else
            {
                visualsRoot.rotation = targetRot;
            }
        }
    }

    public Vector2 GetPlayerCenter()
    {
        if (MainCollider != null) return MainCollider.bounds.center;
        if (RB != null) return RB.position;
        return (Vector2)transform.position;
    }

    #endregion

    #region Impulses & Boosts

    /// <summary>
    /// Aplica un impulso forzado a la esfera (útil para resortes, knockbacks, boost pads).
    /// </summary>
    public void ApplyImpulse(Vector2 impulse, float controlLockDuration = 0.2f)
    {
        _currentVelocity = impulse;
        if (impulse.sqrMagnitude > 0.1f)
        {
            _lastFlightDirection = impulse.normalized;
        }
        _controlLockTimer = Mathf.Max(_controlLockTimer, controlLockDuration);
        if (RB != null)
        {
            RB.linearVelocity = impulse;
        }
    }

    public void ApplyControlLock(float duration)
    {
        _controlLockTimer = duration;
    }

    #endregion

    #region Debug & Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Vector2 center = GetPlayerCenter();

        // Vector de velocidad actual
        if (Application.isPlaying && _currentVelocity.sqrMagnitude > 0.01f)
        {
            Gizmos.color = gizmoVelocityColor;
            Gizmos.DrawLine(center, center + _currentVelocity * 0.25f);
        }
    }

    #endregion
}
