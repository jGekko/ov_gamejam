using UnityEngine;

/// <summary>
/// Controlador del Cocodrilo (Game Jam Edition).
/// 
/// Características:
/// - Movimiento terrestre horizontal (A/D). No puede saltar.
/// - Habilidad especial con Espacio: Embestida/Dash rápido hacia adelante con cooldown.
/// - En Agua: Movimiento lateral lento y hundimiento gradual sin capacidad de salto.
/// - Separación visual y sincronización con Animator (Walk bool y Dash trigger).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CrocodileController : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Visual & Orientation ---")]
    [Tooltip("Transform hijo que contiene los sprites/animaciones.")]
    public Transform visualsRoot;
    [Tooltip("Componente Animator asignado al cocodrilo.")]
    public Animator animator;
    [Tooltip("SpriteRenderer opcional.")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Si es true, invierte SpriteRenderer.flipX en lugar de localScale.x.")]
    public bool flipUsingSpriteRenderer = false;

    [Header("--- Ground Movement ---")]
    [Tooltip("Velocidad máxima de movimiento en tierra.")]
    public float walkSpeed = 8f;
    [Tooltip("Aceleración horizontal en tierra.")]
    public float acceleration = 40f;
    [Tooltip("Fricción / desaceleración en tierra.")]
    public float friction = 50f;
    [Tooltip("Gravedad aplicada en tierra.")]
    public float groundGravity = 3f;

    [Header("--- Dash / Embestida (Space) ---")]
    [Tooltip("Velocidad de la embestida.")]
    public float dashSpeed = 22f;
    [Tooltip("Duración en segundos de la embestida.")]
    public float dashDuration = 0.22f;
    [Tooltip("Tiempo de enfriamiento (cooldown) antes de volver a embestir.")]
    public float dashCooldown = 1.2f;

    [Header("--- Water Behavior ---")]
    [Tooltip("Velocidad máxima de movimiento horizontal en el agua.")]
    public float waterWalkSpeed = 3.5f;
    [Tooltip("Velocidad constante de hundimiento en el agua.")]
    public float waterSinkSpeed = 2.2f;

    [Header("--- Ground Detection ---")]
    [Tooltip("Capas consideradas suelo.")]
    public LayerMask groundLayer;
    [Tooltip("Buffer de detección de suelo.")]
    public float groundCheckBuffer = 0.08f;

    [Header("--- Dash VFX ---")]
    [Tooltip("Animator especial asignado a los efectos visuales de la embestida/dash del cocodrilo.")]
    public Animator dashVFXAnimator;
    [Tooltip("Nombre del Trigger para reproducir la animación de dash del cocodrilo.")]
    public string crocDashTrigger = "crocDash";
    [Tooltip("Si es true, sincroniza la posición del VFX con la del cocodrilo y adapta su escala horizontal al encarar.")]
    public bool syncDashVFXTransform = true;

    [Header("--- Dash Ghost Trail ---")]
    [Tooltip("GhostTrail asignado a la embestida del cocodrilo (Gradiente Amarillo a Verde).")]
    public GhostTrail dashGhostTrail;
    [Tooltip("Si es true, auto-configura el GhostTrail para el dash si no está asignado.")]
    public bool autoSetupDashGhostTrail = true;

    [Header("--- Debug & Gizmos ---")]
    public bool showGizmos = true;

    #endregion

    #region Public Properties & State

    public Rigidbody2D RB { get; private set; }
    public Collider2D MainCollider { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsInWater { get; private set; }
    public bool IsDashing { get; private set; }
    public int FacingDirection { get; private set; } = 1; // 1 = Derecha, -1 = Izquierda
    public float CurrentSpeed => RB != null ? RB.linearVelocity.magnitude : 0f;
    public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownTimer);

    // Input virtualizable
    public Vector2 MoveInput { get; set; }
    public bool AbilityPressed { get; set; }

    #endregion

    #region Animation Hashes

    private int _animWalk;
    private int _animDash;

    #endregion

    #region Internal State

    private float _dashTimer;
    private float _dashCooldownTimer;
    private int _dashDirection = 1;
    private Vector2 _currentVelocity;
    private WaterZone _currentWaterZone;
    private float _baseVisualScaleX = 1f;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        RB = GetComponent<Rigidbody2D>();
        MainCollider = GetComponent<Collider2D>();
        CacheVisualReferences();

        if (RB != null)
        {
            RB.bodyType = RigidbodyType2D.Dynamic;
            RB.freezeRotation = true;
            RB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            RB.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        CacheAnimationHashes();
    }

    protected virtual void OnEnable()
    {
        CacheVisualReferences();
        CacheAnimationHashes();

        if (RB != null)
        {
            RB.gravityScale = groundGravity;
        }
        IsDashing = false;
        _dashTimer = 0f;
        ApplyVisualFacing();
    }

    protected virtual void OnDisable()
    {
        if (dashGhostTrail != null)
        {
            dashGhostTrail.SetTrailActive(false);
            dashGhostTrail.ClearAllClones();
        }
    }

    public void CacheVisualReferences()
    {
        if (visualsRoot == null)
        {
            var ptm = GetComponent<PlayerTransformationManager>() ?? GetComponentInParent<PlayerTransformationManager>();
            if (ptm != null && ptm.crocodileVisuals != null)
            {
                visualsRoot = ptm.crocodileVisuals.transform;
            }
            else
            {
                Transform found = transform.Find("CrocodileVisuals") ?? transform.Find("Crocodile") ?? transform.Find("Visuals");
                if (found != null) visualsRoot = found;
                else if (transform.childCount > 0)
                {
                    // Buscar entre hijos un nombre que contenga croc
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var child = transform.GetChild(i);
                        if (child.name.ToLower().Contains("croc"))
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

        // Buscar Animator específicamente en visualsRoot o entre todos los hijos
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
                    if (a.name.ToLower().Contains("croc") ||
                        (a.runtimeAnimatorController != null && a.runtimeAnimatorController.name.ToLower().Contains("croc")))
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

        if (visualsRoot != null && visualsRoot != transform)
        {
            float absX = Mathf.Abs(visualsRoot.localScale.x);
            if (absX > 0.001f) _baseVisualScaleX = absX;
            else _baseVisualScaleX = 1f;

            if (dashGhostTrail == null)
            {
                dashGhostTrail = visualsRoot.GetComponent<GhostTrail>();
                if (dashGhostTrail == null && autoSetupDashGhostTrail)
                {
                    dashGhostTrail = visualsRoot.gameObject.AddComponent<GhostTrail>();
                }
            }

            if (dashGhostTrail != null)
            {
                dashGhostTrail.targetSpriteRenderer = spriteRenderer;
                dashGhostTrail.profile = GhostTrail.GhostTrailProfile.CrocodileDash;
                dashGhostTrail.ApplyProfileDefaults(GhostTrail.GhostTrailProfile.CrocodileDash);
                dashGhostTrail.EnsureTargetSpriteRenderer();
            }
        }
    }

    public void SetInitialVelocity(Vector2 initialVel)
    {
        _currentVelocity = initialVel;
        if (RB != null) RB.linearVelocity = initialVel;
    }

    public void SetInWater(bool inWater, WaterZone waterZone)
    {
        IsInWater = inWater;
        _currentWaterZone = inWater ? waterZone : null;

        if (RB != null)
        {
            RB.gravityScale = inWater ? 0f : groundGravity;
        }
    }

    protected virtual void Update()
    {
        GatherInput();
        UpdateTimers();
        UpdateGroundCheck();
        HandleDashTrigger();
        HandleVisuals();
    }

    protected virtual void FixedUpdate()
    {
        if (IsDashing)
        {
            ExecuteDashPhysics();
        }
        else if (IsInWater)
        {
            ExecuteWaterPhysics();
        }
        else
        {
            ExecuteGroundPhysics();
        }
    }

    #endregion

    #region Input Collection

    protected virtual void GatherInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        MoveInput = new Vector2(h, 0f);

        // Habilidad con Espacio
        AbilityPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump");
    }

    #endregion

    #region State & Ground Checks

    private void UpdateTimers()
    {
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
        if (_dashTimer > 0f)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
            {
                IsDashing = false;
                if (dashGhostTrail != null)
                {
                    dashGhostTrail.SetTrailActive(false);
                }
            }
        }
    }

    private void UpdateGroundCheck()
    {
        if (MainCollider == null)
        {
            IsGrounded = false;
            return;
        }

        // Si groundLayer es 0 (no configurado), usar todas las capas excepto Ignore Raycast y Triggers
        LayerMask mask = groundLayer != 0 ? groundLayer : ~LayerMask.GetMask("Ignore Raycast");

        float skinWidth = 0.02f;
        Vector2 boxSize = new Vector2(MainCollider.bounds.size.x * 0.9f, skinWidth);
        Vector2 origin = (Vector2)MainCollider.bounds.center + Vector2.down * (MainCollider.bounds.extents.y - skinWidth);
        float castDist = skinWidth + groundCheckBuffer;

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, Vector2.down, castDist, mask);
        IsGrounded = hit.collider != null && hit.collider != MainCollider;
    }

    private void HandleDashTrigger()
    {
        // Solo puede embestir en tierra y si el cooldown expiró
        if (AbilityPressed && !IsInWater && !IsDashing && _dashCooldownTimer <= 0f)
        {
            StartDash();
        }
    }

    private void StartDash()
    {
        IsDashing = true;
        _dashTimer = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _dashDirection = FacingDirection;

        TriggerAnimation(_animDash);
        PlayDashVFX();

        if (dashGhostTrail != null)
        {
            dashGhostTrail.SetTrailActive(true);
            dashGhostTrail.SpawnClone();
        }

        // Romper instantáneamente cualquier obstáculo o tronco que esté pegado a la boca del cocodrilo
        CheckAndBreakForwardObstacles();
    }

    private void PlayDashVFX()
    {
        if (dashVFXAnimator != null)
        {
            if (syncDashVFXTransform)
            {
                dashVFXAnimator.transform.position = transform.position;
                Vector3 scale = dashVFXAnimator.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * _dashDirection;
                dashVFXAnimator.transform.localScale = scale;
            }
            if (!string.IsNullOrEmpty(crocDashTrigger))
            {
                dashVFXAnimator.ResetTrigger(crocDashTrigger);
                dashVFXAnimator.SetTrigger(crocDashTrigger);
            }
        }
    }

    #endregion

    #region Physics Movements

    private void ExecuteDashPhysics()
    {
        if (RB == null) return;

        // Movimiento a alta velocidad horizontal durante el dash
        RB.linearVelocity = new Vector2(_dashDirection * dashSpeed, 0f);

        // Comprobación proactiva durante todo el dash para destruir obstáculos a quemarropa
        CheckAndBreakForwardObstacles();
    }

    /// <summary>
    /// Detecta y rompe inmediatamente obstáculos destruibles o troncos que estén frente al cocodrilo.
    /// </summary>
    private void CheckAndBreakForwardObstacles()
    {
        if (MainCollider == null) return;

        Vector2 origin = MainCollider.bounds.center;
        Vector2 size = MainCollider.bounds.size;
        Vector2 dir = Vector2.right * _dashDirection;
        float checkDist = 0.5f;

        RaycastHit2D[] hits = Physics2D.BoxCastAll(origin, size, 0f, dir, checkDist);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider == MainCollider) continue;

            // 1. Romper obstáculos destruibles
            var breakable = hit.collider.GetComponent<BreakableObstacle>() ?? hit.collider.GetComponentInParent<BreakableObstacle>();
            if (breakable != null && !breakable.IsBroken)
            {
                breakable.Break();
            }

            // 2. Tumbado de troncos
            var log = hit.collider.GetComponent<TopplingLog>() ?? hit.collider.GetComponentInParent<TopplingLog>();
            if (log != null && !log.IsToppled)
            {
                log.Topple(_dashDirection > 0);
            }
        }
    }

    private void ExecuteGroundPhysics()
    {
        if (RB == null) return;

        float dt = Time.fixedDeltaTime;
        float targetVx = MoveInput.x * walkSpeed;
        float currentVx = RB.linearVelocity.x;

        float rate = Mathf.Abs(MoveInput.x) > 0.01f ? acceleration : friction;
        float newVx = Mathf.MoveTowards(currentVx, targetVx, rate * dt);

        RB.linearVelocity = new Vector2(newVx, RB.linearVelocity.y);
    }

    private void ExecuteWaterPhysics()
    {
        if (RB == null) return;

        float dt = Time.fixedDeltaTime;
        float targetVx = MoveInput.x * waterWalkSpeed;
        float currentVx = RB.linearVelocity.x;

        float newVx = Mathf.MoveTowards(currentVx, targetVx, acceleration * 0.6f * dt);
        // Hundimiento constante
        float newVy = Mathf.MoveTowards(RB.linearVelocity.y, -waterSinkSpeed, 10f * dt);

        RB.linearVelocity = new Vector2(newVx, newVy);
    }

    #endregion

    #region Visuals & Animation

    private void HandleVisuals()
    {
        float moveX = MoveInput.x;
        float velX = RB != null ? RB.linearVelocity.x : 0f;
        float absVelX = Mathf.Abs(velX);
        bool hasMoveInput = Mathf.Abs(moveX) > 0.05f;

        // 1. Orientación horizontal
        if (hasMoveInput && !IsDashing)
        {
            SetFacingDirection(moveX > 0 ? 1 : -1);
        }
        else if (absVelX > 0.1f && !IsDashing)
        {
            SetFacingDirection(velX > 0 ? 1 : -1);
        }
        else
        {
            ApplyVisualFacing();
        }

        // 2. Animator parameters: Walk bool
        bool isWalking = (hasMoveInput || absVelX > 0.1f) && !IsDashing;
        SetBoolAnimation(_animWalk, isWalking);
    }

    public void SetFacingDirection(int dir)
    {
        if (dir == 0) return;
        FacingDirection = dir;
        ApplyVisualFacing();
    }

    private void ApplyVisualFacing()
    {
        if (visualsRoot != null && visualsRoot != transform)
        {
            Vector3 s = visualsRoot.localScale;
            s.x = Mathf.Abs(_baseVisualScaleX) * FacingDirection;
            visualsRoot.localScale = s;
        }

        if (flipUsingSpriteRenderer || visualsRoot == null || visualsRoot == transform)
        {
            if (spriteRenderer == null && visualsRoot != null)
            {
                spriteRenderer = visualsRoot.GetComponent<SpriteRenderer>() ?? visualsRoot.GetComponentInChildren<SpriteRenderer>(true);
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = (FacingDirection < 0);
            }
        }
    }

    #endregion

    #region Animation Helpers

    private void CacheAnimationHashes()
    {
        _animWalk = Animator.StringToHash("Walk");
        _animDash = Animator.StringToHash("Dash");
    }

    private void TriggerAnimation(int hash)
    {
        if (animator == null)
        {
            CacheVisualReferences();
        }

        if (animator != null)
        {
            if (hash != 0)
            {
                animator.ResetTrigger(hash);
                animator.SetTrigger(hash);
            }
            else
            {
                animator.ResetTrigger("Dash");
                animator.SetTrigger("Dash");
            }
        }
    }

    private void SetBoolAnimation(int hash, bool value)
    {
        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            CacheVisualReferences();
        }

        if (animator != null)
        {
            if (hash != 0)
            {
                animator.SetBool(hash, value);
            }
            else
            {
                animator.SetBool("Walk", value);
            }
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || MainCollider == null) return;

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector2 origin = (Vector2)MainCollider.bounds.center + Vector2.down * MainCollider.bounds.extents.y;
        Gizmos.DrawWireCube(origin, new Vector3(MainCollider.bounds.size.x * 0.9f, 0.05f, 0f));
    }

    #endregion
}
