using UnityEngine;

/// <summary>
/// Standalone 2D Platformer Character Controller (Gamejam Edition).
/// 
/// Características:
/// - 100% independiente: Cero librerías externas o dependencias (sin PrimeTween, sin ChromaSynk).
/// - Diseñado para Gamejams: Listo para arrastrar y usar en cualquier proyecto Unity 2D.
/// - Separación visual: 'visualsRoot' separado del Rigidbody para permitir Pixel Aligners / Pixel Perfect.
/// - QoL & Game Feel: Coyote Time, Jump Buffer, Salto Variable (Jump Cut), Slopes y Fricción.
/// - Wall Mechanics: Wall Slide y Wall Jump con control lock y wall coyote time.
/// - Crouch System: Agachado, Crouch Walk, Crouch Turn, ajuste dinámico de collider y Ceiling Check.
/// - Animaciones completas: Idle, Walk, WalkToRun, Run, RunTurn, Jump, JumpTurn, WallSlide, WallJump, Fall, Crouch, CrouchTurn, CrouchWalk.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HumanController : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Visual & Pixel Alignment ---")]
    [Tooltip("Transform hijo que contiene los sprites/animaciones. Separado del Rigidbody para permitir Pixel Aligners / Pixel Perfect.")]
    public Transform visualsRoot;
    [Tooltip("Componente Animator asignado al personaje o a su objeto visual.")]
    public Animator animator;
    [Tooltip("Si es true, invierte SpriteRenderer.flipX en lugar de localScale.x (útil si visualsRoot no debe rotar).")]
    public bool flipUsingSpriteRenderer = false;
    [Tooltip("SpriteRenderer opcional si se usa 'flipUsingSpriteRenderer'.")]
    public SpriteRenderer spriteRenderer;

    [Header("--- Movement Speeds ---")]
    [Tooltip("Velocidad máxima al caminar.")]
    public float walkSpeed = 6.5f;
    [Tooltip("Velocidad máxima al correr.")]
    public float runSpeed = 13.5f;
    [Tooltip("Velocidad máxima horizontal en el aire (independiente de si se salta caminando o corriendo).")]
    public float airSpeed = 11.5f;
    [Tooltip("Tiempo caminando continuamente antes de acelerar a correr (en segundos).")]
    public float timeToStartRunning = 0.4f;
    [Tooltip("Velocidad máxima al desplazarse agachado.")]
    public float crouchSpeed = 4f;
    [Tooltip("Aceleración horizontal en el suelo.")]
    public float acceleration = 35f;
    [Tooltip("Fricción / Deceleración en el suelo al soltar el input.")]
    public float groundFriction = 45f;
    [Tooltip("Deceleración al realizar un giro brusco (Skid).")]
    public float skidDeceleration = 65f;
    [Tooltip("Multiplicador de control de aceleración en el aire (0 = sin control, 1 = control total).")]
    [Range(0f, 1f)] public float airControl = 0.9f;
    [Tooltip("Fricción / Resistencia del aire cuando no hay input.")]
    public float airFriction = 20f;

    [Header("--- Jump & Gravity ---")]
    [Tooltip("Fuerza de impulso vertical del salto.")]
    public float jumpForce = 14f;
    [Tooltip("Duración máxima del salto con botón presionado.")]
    public float jumpDuration = 0.3f;
    [Tooltip("Tiempo mínimo antes de poder cortar el salto (evita micro-saltos accidentales).")]
    public float minJumpTime = 0.1f;
    [Tooltip("Multiplicador de gravedad cuando el jugador suelta el botón de salto antes de tiempo.")]
    public float jumpCutGravityMult = 4f;
    [Tooltip("Multiplicador de gravedad al caer para dar una sensación de peso más ágil.")]
    public float fallGravityMult = 4f;
    [Tooltip("Límite de velocidad máxima de caída.")]
    public float maxFallSpeed = -24f;
    [Tooltip("Escala de gravedad por defecto del Rigidbody2D.")]
    public float defaultGravityScale = 1f;

    [Header("--- Quality of Life (QoL) ---")]
    [Tooltip("Tiempo de gracia para saltar tras abandonar una plataforma.")]
    public float coyoteTime = 0.2f;
    [Tooltip("Ventana de tiempo para almacenar el input de salto antes de tocar el suelo/pared.")]
    public float jumpBufferTime = 0.1f;

    [Header("--- Ground & Slope Detection ---")]
    [Tooltip("Capa(s) consideradas suelo.")]
    public LayerMask groundLayer;
    [Tooltip("Buffer extra para el chequeo de suelo con BoxCast.")]
    public float groundCheckBuffer = 0.05f;
    [Tooltip("Ángulo máximo de pendiente transitable.")]
    public float maxSlopeAngle = 45f;
    [Tooltip("Multiplicador de velocidad al desplazarse por pendientes.")]
    public float slopeSpeedMultiplier = 1f;

    [Header("--- Wall Mechanics ---")]
    [Tooltip("Habilita o deshabilita las mecánicas de pared.")]
    public bool enableWallMechanics = true;
    [Tooltip("Capa(s) consideradas paredes para wall slide/jump.")]
    public LayerMask wallLayer;
    [Tooltip("Velocidad máxima de caída al deslizarse por una pared.")]
    public float wallSlideMaxSpeed = 8f;
    [Tooltip("Fuerza del salto en pared: X = impulso horizontal, Y = impulso vertical.")]
    public Vector2 wallJumpForce = new Vector2(6.4f, 11.2f);
    [Tooltip("Tiempo de coyote para saltar en la pared tras despegarse de ella.")]
    public float wallJumpCoyoteTime = 0.1f;
    [Tooltip("Tiempo durante el cual se bloquea el input horizontal del jugador tras un Wall Jump.")]
    public float wallJumpControlLockDuration = 0.1f;
    [Tooltip("Requiere mantener el input apuntando hacia la pared para iniciar el deslizamiento.")]
    public bool requireInputToWallSlide = true;
    [Tooltip("Dimensiones de la caja de detección de pared.")]
    public Vector2 wallCheckSize = new Vector2(0.1f, 0.4f);
    [Tooltip("Offset respecto al centro del collider para la detección de pared.")]
    public Vector2 wallCheckOffset = new Vector2(0f, 0.75f);
    [Tooltip("Distancia lateral de detección de pared.")]
    public float wallCheckDistance = 0.05f;

    [Header("--- Crouch & Ceiling Check ---")]
    [Tooltip("Habilita o deshabilita la mecánica de agacharse.")]
    public bool enableCrouch = true;
    [Tooltip("Multiplicador de altura del collider al agacharse (ej. 0.5f = mitad de altura).")]
    [Range(0.2f, 1f)] public float crouchColliderHeightMultiplier = 0.55f;
    [Tooltip("Capa(s) consideradas techo para evitar levantarse si hay un obstáculo arriba.")]
    public LayerMask ceilingLayer;
    [Tooltip("Radio del círculo de detección de techo.")]
    public float ceilingCheckRadius = 0.2f;
    [Tooltip("Offset del chequeo de techo relativo al centro del personaje.")]
    public Vector2 ceilingCheckOffset = new Vector2(0f, 0.4f);

    [Header("--- Animation Tuning ---")]
    [Tooltip("Velocidad normalizada para considerar transición de Walk a Run (0 a 1).")]
    public float walkToRunThreshold = 0.65f;
    [Tooltip("Ventana de tiempo para detectar un giro brusco (RunTurn).")]
    public float turnBufferWindow = 0.2f;
    [Tooltip("Duración visual de la animación de giro.")]
    public float turnVisualDuration = 0.3f;

    [Header("--- Water Behavior ---")]
    [Tooltip("Velocidad de movimiento horizontal en el agua.")]
    public float waterMoveSpeed = 3.5f;
    [Tooltip("Velocidad constante de hundimiento lento en el agua (estilo cocodrilo).")]
    public float waterSinkSpeed = 1.8f;
    [Tooltip("Fuerza de salto al impulsarse dentro/fuera del agua.")]
    public float waterJumpForce = 12f;

    [Header("--- Run Ghost Trail ---")]
    [Tooltip("GhostTrail sutil y leve asignado al correr (no exagerado, baja opacidad).")]
    public GhostTrail runGhostTrail;
    [Tooltip("Si es true, auto-configura el GhostTrail sutil si no está asignado.")]
    public bool autoSetupRunGhostTrail = true;

    [Header("--- Debug & Gizmos ---")]
    public bool showGizmos = true;
    public Color gizmoColor = new Color(0f, 1f, 0.5f, 0.6f);

    #endregion

    #region Public Properties & State

    public Rigidbody2D RB { get; private set; }
    public Collider2D MainCollider { get; private set; }
    public BoxCollider2D BoxCol { get; private set; }
    public CapsuleCollider2D CapsuleCol { get; private set; }

    public bool IsGrounded { get; private set; }
    public bool IsWallSliding { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsJumping { get; private set; }
    public bool IsFalling { get; private set; }
    public bool IsInWater { get; private set; }
    public WaterZone CurrentWaterZone { get; private set; }
    public int FacingDirection { get; private set; } = 1; // 1: Derecha, -1: Izquierda
    public Vector2 GroundNormal { get; private set; } = Vector2.up;
    public float CurrentSpeedNormalized { get; private set; }

    // Input virtualizable (puedes sobreescribirlo o alimentarlo desde otros scripts)
    public Vector2 MoveInput { get; set; }
    public bool JumpPressed { get; set; }
    public bool JumpHeld { get; set; }
    public bool JumpReleased { get; set; }
    public bool RunHeld { get; set; }
    public bool CrouchInput { get; set; }

    #endregion

    #region Internal State & Timers

    private float _coyoteTimeCounter;
    private float _jumpBufferCounter;
    private float _wallCoyoteTimeCounter;
    private float _jumpActionTimer;
    private bool _hasCutJump;
    private bool _wantsToCutJump;

    private int _wallSide;        // 1 = Pared a la derecha, -1 = Pared a la izquierda, 0 = Sin pared
    private int _activeWallSide;
    private float _wallJumpControlLockTimer;
    private float _forcedInputX;

    private float _turnLockTimer;
    private float _directionChangeBuffer;
    private int _lastMovingDirection;
    private float _velocityAtTurnStart;
    private float _runToIdleDelayTimer;
    private float _continuousMoveTimer;
    private bool _wasActuallyRunningWhenStopped;
    private bool _wasGrounded;
    private bool _wasFalling;
    private bool _wasMoving;
    private float _baseVisualScaleX = 1f;

    // Collider default dimensions
    private Vector2 _defaultBoxSize;
    private Vector2 _defaultBoxOffset;
    private Vector2 _defaultCapsuleSize;
    private Vector2 _defaultCapsuleOffset;

    #endregion

    #region Animation Hashes

    private int _animSpeed;
    private int _animGrounded;
    private int _animWallSlide;
    private int _animCrouch;
    private int _animCrouchWalk;
    private int _animFallLoop;
    private int _animRun;
    private int _animWalk;
    private int _animJump;
    private int _animWallJump;
    private int _animJumpTurn;
    private int _animRunTurn;
    private int _animCrouchTurn;
    private int _animWalkToRun;
    private int _animFall;
    private int _animRunToIdle;
    private int _animIdleTurn;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        RB = GetComponent<Rigidbody2D>();
        MainCollider = GetComponent<Collider2D>();
        BoxCol = GetComponent<BoxCollider2D>();
        CapsuleCol = GetComponent<CapsuleCollider2D>();

        if (visualsRoot == null)
        {
            // Si no se asignó un visualsRoot separado, buscar un hijo o usar el propio transform
            if (transform.childCount > 0)
                visualsRoot = transform.GetChild(0);
            else
                visualsRoot = transform;
        }

        if (animator == null)
        {
            animator = visualsRoot.GetComponent<Animator>() ?? GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }

        if (spriteRenderer == null && visualsRoot != null)
        {
            spriteRenderer = visualsRoot.GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        }

        // Guardar dimensiones originales del collider
        if (BoxCol != null)
        {
            _defaultBoxSize = BoxCol.size;
            _defaultBoxOffset = BoxCol.offset;
        }
        else if (CapsuleCol != null)
        {
            _defaultCapsuleSize = CapsuleCol.size;
            _defaultCapsuleOffset = CapsuleCol.offset;
        }

        if (visualsRoot != null)
        {
            _baseVisualScaleX = Mathf.Abs(visualsRoot.localScale.x);
            if (_baseVisualScaleX <= 0f) _baseVisualScaleX = 1f;

            if (runGhostTrail == null)
            {
                runGhostTrail = visualsRoot.GetComponent<GhostTrail>();
                if (runGhostTrail == null && autoSetupRunGhostTrail)
                {
                    runGhostTrail = visualsRoot.gameObject.AddComponent<GhostTrail>();
                }
            }

            if (runGhostTrail != null)
            {
                runGhostTrail.targetSpriteRenderer = spriteRenderer;
                runGhostTrail.profile = GhostTrail.GhostTrailProfile.HumanRun;
                runGhostTrail.ApplyProfileDefaults(GhostTrail.GhostTrailProfile.HumanRun);
                runGhostTrail.EnsureTargetSpriteRenderer();
            }
        }

        if (RB != null) RB.gravityScale = defaultGravityScale;
        CacheAnimationHashes();
    }

    protected virtual void OnEnable()
    {
        if (runGhostTrail != null)
        {
            if (spriteRenderer == null && visualsRoot != null)
            {
                spriteRenderer = visualsRoot.GetComponent<SpriteRenderer>() ?? visualsRoot.GetComponentInChildren<SpriteRenderer>(true);
            }
            runGhostTrail.targetSpriteRenderer = spriteRenderer;
            runGhostTrail.EnsureTargetSpriteRenderer();
        }
    }

    protected virtual void OnDisable()
    {
        if (runGhostTrail != null)
        {
            runGhostTrail.SetTrailActive(false);
            runGhostTrail.ClearAllClones();
        }
    }

    protected virtual void Update()
    {
        GatherInput();
        UpdateGroundCheck();
        UpdateWallCheck();
        UpdateCrouchState();
        UpdatePlatformingTimers();
        CheckJumpTriggers();
        HandleVisuals();
    }

    protected virtual void FixedUpdate()
    {
        HandlePhysicsMovement();
        ApplySpeedCap();
    }

    #endregion

    #region Input Collection

    /// <summary>
    /// Recolecta inputs estándar de Unity. Puede sobreescribirse si usas el nuevo Input System.
    /// </summary>
    protected virtual void GatherInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        MoveInput = new Vector2(h, v);

        JumpPressed = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
        JumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.Space);
        JumpReleased = Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.Space);

        CrouchInput = enableCrouch && (v < -0.5f || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));
    }

    #endregion

    #region Checks & State Updates

    private void UpdateGroundCheck()
    {
        if (MainCollider == null)
        {
            IsGrounded = false;
            GroundNormal = Vector2.up;
            return;
        }

        float skinWidth = 0.02f;
        Vector2 boxSize = new Vector2(MainCollider.bounds.size.x * 0.9f, skinWidth);
        Vector2 origin = (Vector2)MainCollider.bounds.center + Vector2.down * (MainCollider.bounds.extents.y - skinWidth);
        float castDist = skinWidth + groundCheckBuffer;

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, Vector2.down, castDist, groundLayer);

        _wasGrounded = IsGrounded;
        IsGrounded = hit.collider != null;

        // Evitar adherirse a plataformas one-way mientras se asciende a través de ellas
        if (IsGrounded && hit.collider.usedByEffector && RB.linearVelocity.y > 0.05f)
        {
            IsGrounded = false;
        }

        GroundNormal = IsGrounded ? hit.normal : Vector2.up;
        float slopeAngle = Vector2.Angle(GroundNormal, Vector2.up);

        if (IsGrounded)
        {
            if (slopeAngle > maxSlopeAngle)
            {
                IsGrounded = false; // Pendiente demasiado inclinada: se trata como caída
            }
        }
    }

    private void UpdateWallCheck()
    {
        if (!enableWallMechanics || MainCollider == null || IsGrounded)
        {
            _wallSide = 0;
            _activeWallSide = 0;
            _wallCoyoteTimeCounter -= Time.deltaTime;
            IsWallSliding = false;
            return;
        }

        Vector2 origin = (Vector2)MainCollider.bounds.center + wallCheckOffset;
        Vector2 size = wallCheckSize;
        float castDist = wallCheckDistance;
        float halfExtentX = MainCollider.bounds.extents.x;

        RaycastHit2D hitRight = Physics2D.BoxCast(origin, size, 0f, Vector2.right, halfExtentX + castDist, wallLayer);
        RaycastHit2D hitLeft = Physics2D.BoxCast(origin, size, 0f, Vector2.left, halfExtentX + castDist, wallLayer);

        if (hitRight.collider != null)
        {
            _wallSide = 1;
            _activeWallSide = 1;
            _wallCoyoteTimeCounter = wallJumpCoyoteTime;
        }
        else if (hitLeft.collider != null)
        {
            _wallSide = -1;
            _activeWallSide = -1;
            _wallCoyoteTimeCounter = wallJumpCoyoteTime;
        }
        else
        {
            _wallSide = 0;
            _wallCoyoteTimeCounter -= Time.deltaTime;
            if (_wallCoyoteTimeCounter <= 0f) _activeWallSide = 0;
        }

        // Lógica de Wall Slide
        if (_wallSide != 0 && !IsGrounded && RB.linearVelocity.y <= 0.1f)
        {
            if (!requireInputToWallSlide)
            {
                IsWallSliding = true;
            }
            else
            {
                // Requiere empujar hacia la pared
                bool pushingIntoWall = (_wallSide == 1 && MoveInput.x > 0.1f) ||
                                       (_wallSide == -1 && MoveInput.x < -0.1f);
                IsWallSliding = pushingIntoWall;
            }
        }
        else
        {
            IsWallSliding = false;
        }
    }

    private void UpdateCrouchState()
    {
        if (!enableCrouch)
        {
            if (IsCrouching) StopCrouch();
            return;
        }

        if (IsGrounded && CrouchInput)
        {
            if (!IsCrouching) StartCrouch();
        }
        else if (IsCrouching)
        {
            // Intentar levantarse: verificar si hay techo encima
            if (!HasCeilingAbove())
            {
                StopCrouch();
            }
        }
    }

    private bool HasCeilingAbove()
    {
        if (MainCollider == null) return false;

        LayerMask mask = ceilingLayer != 0 ? ceilingLayer : groundLayer;
        Vector2 checkPos = (Vector2)MainCollider.bounds.center + ceilingCheckOffset;
        Collider2D hit = Physics2D.OverlapCircle(checkPos, ceilingCheckRadius, mask);
        return hit != null && hit != MainCollider;
    }

    private void StartCrouch()
    {
        IsCrouching = true;
        AdjustColliderForCrouch(true);
    }

    private void StopCrouch()
    {
        IsCrouching = false;
        AdjustColliderForCrouch(false);
    }

    private void AdjustColliderForCrouch(bool crouching)
    {
        if (BoxCol != null)
        {
            if (crouching)
            {
                float newHeight = _defaultBoxSize.y * crouchColliderHeightMultiplier;
                float heightDiff = _defaultBoxSize.y - newHeight;
                BoxCol.size = new Vector2(_defaultBoxSize.x, newHeight);
                BoxCol.offset = new Vector2(_defaultBoxOffset.x, _defaultBoxOffset.y - (heightDiff / 2f));
            }
            else
            {
                BoxCol.size = _defaultBoxSize;
                BoxCol.offset = _defaultBoxOffset;
            }
        }
        else if (CapsuleCol != null)
        {
            if (crouching)
            {
                float newHeight = _defaultCapsuleSize.y * crouchColliderHeightMultiplier;
                float heightDiff = _defaultCapsuleSize.y - newHeight;
                CapsuleCol.size = new Vector2(_defaultCapsuleSize.x, newHeight);
                CapsuleCol.offset = new Vector2(_defaultCapsuleOffset.x, _defaultCapsuleOffset.y - (heightDiff / 2f));
            }
            else
            {
                CapsuleCol.size = _defaultCapsuleSize;
                CapsuleCol.offset = _defaultCapsuleOffset;
            }
        }
    }

    private int _lastInputDirection;

    private void UpdatePlatformingTimers()
    {
        _jumpActionTimer += Time.deltaTime;
        if (_turnLockTimer > 0f) _turnLockTimer -= Time.deltaTime;
        if (_wallJumpControlLockTimer > 0f) _wallJumpControlLockTimer -= Time.deltaTime;
        if (_directionChangeBuffer > 0f) _directionChangeBuffer -= Time.deltaTime;

        // Temporizador continuo para acelerar automáticamente de Caminar a Correr
        float absMoveX = Mathf.Abs(MoveInput.x);
        int inputDir = absMoveX > 0.1f ? (int)Mathf.Sign(MoveInput.x) : 0;

        if (inputDir != 0 && !IsCrouching)
        {
            if (_lastInputDirection != 0 && inputDir != _lastInputDirection)
            {
                // Si cambió de dirección (giro), reiniciar el contador de caminata
                _continuousMoveTimer = 0f;
            }
            else
            {
                _continuousMoveTimer += Time.deltaTime;
            }
            _lastInputDirection = inputDir;
        }
        else
        {
            _continuousMoveTimer = 0f;
            _lastInputDirection = 0;
            _walkToRunTriggered = false;
        }

        RunHeld = _continuousMoveTimer >= timeToStartRunning;

        // Coyote Time
        if (IsGrounded && RB.linearVelocity.y <= 0.1f)
        {
            _coyoteTimeCounter = coyoteTime;
            _hasCutJump = false;
            IsJumping = false;
        }
        else
        {
            _coyoteTimeCounter -= Time.deltaTime;
        }

        // Jump Buffer
        if (JumpPressed)
        {
            _jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            _jumpBufferCounter -= Time.deltaTime;
        }

        // Jump Cut request
        if (JumpReleased || !JumpHeld)
        {
            _wantsToCutJump = true;
        }

        if (_wantsToCutJump && !_hasCutJump && IsJumping && _jumpActionTimer >= minJumpTime && RB.linearVelocity.y > 0)
        {
            _hasCutJump = true;
            RB.linearVelocity = new Vector2(RB.linearVelocity.x, RB.linearVelocity.y * 0.4f);
        }
    }

    #endregion

    #region Jumps & Physics

    public void SetInWater(bool inWater, WaterZone waterZone)
    {
        IsInWater = inWater;
        CurrentWaterZone = inWater ? waterZone : null;

        if (inWater)
        {
            IsWallSliding = false;
            _wallSide = 0;
            if (RB != null)
            {
                RB.gravityScale = 0f;
                // Amortiguación inmediata al caer al agua
                if (RB.linearVelocity.y < -waterSinkSpeed)
                {
                    Vector2 v = RB.linearVelocity;
                    v.x *= 0.7f;
                    v.y = Mathf.Max(v.y * 0.35f, -waterSinkSpeed * 1.5f);
                    RB.linearVelocity = v;
                }
            }
        }
        else
        {
            if (RB != null) RB.gravityScale = defaultGravityScale;
        }
    }

    public void SetInitialVelocity(Vector2 initialVel)
    {
        if (RB != null) RB.linearVelocity = initialVel;
    }

    private void CheckJumpTriggers()
    {
        if (_jumpBufferCounter > 0f)
        {
            // Salto para salir del agua
            if (IsInWater)
            {
                PerformWaterJump();
            }
            // Salto estándar desde suelo / coyote time
            else if (_coyoteTimeCounter > 0f && !IsCrouching)
            {
                PerformJump();
            }
            // Si está agachado pero no hay techo, se levanta y salta
            else if (_coyoteTimeCounter > 0f && IsCrouching && !HasCeilingAbove())
            {
                StopCrouch();
                PerformJump();
            }
            // Salto en pared
            else if (_wallCoyoteTimeCounter > 0f && enableWallMechanics)
            {
                PerformWallJump();
            }
        }
    }

    private void PerformWaterJump()
    {
        _jumpBufferCounter = 0f;
        _jumpActionTimer = 0f;
        IsJumping = true;
        _hasCutJump = false;
        _wantsToCutJump = false;

        Vector2 vel = RB.linearVelocity;
        vel.y = waterJumpForce;
        RB.linearVelocity = vel;

        TriggerAnimation(_animJump);
    }

    private void PerformJump()
    {
        _jumpBufferCounter = 0f;
        _coyoteTimeCounter = 0f;
        _jumpActionTimer = 0f;
        IsJumping = true;
        _hasCutJump = false;
        _wantsToCutJump = false;

        Vector2 vel = RB.linearVelocity;
        vel.y = jumpForce;
        RB.linearVelocity = vel;

        TriggerAnimation(_animJump);
    }

    private void PerformWallJump()
    {
        _jumpBufferCounter = 0f;
        _wallCoyoteTimeCounter = 0f;
        _jumpActionTimer = 0f;
        IsJumping = true;
        _hasCutJump = false;
        _wantsToCutJump = false;

        float forceX = -_activeWallSide * wallJumpForce.x;
        float forceY = wallJumpForce.y;

        _forcedInputX = -_activeWallSide;
        RB.linearVelocity = new Vector2(forceX, forceY);
        _wallJumpControlLockTimer = wallJumpControlLockDuration;

        // Girar de inmediato en dirección contraria a la pared
        SetFacingDirection(-_activeWallSide);

        TriggerAnimation(_animWallJump);

        IsWallSliding = false;
        _wallSide = 0;
    }

    private bool _walkToRunTriggered;

    private void HandlePhysicsMovement()
    {
        // FÍSICA EN AGUA (Hundimiento lento estilo cocodrilo con salto en agua)
        if (IsInWater)
        {
            float dt = Time.fixedDeltaTime;
            RB.gravityScale = 0f;

            // Movimiento horizontal en agua
            float targetVx = MoveInput.x * waterMoveSpeed;
            float currentVx = RB.linearVelocity.x;
            float newVx = Mathf.MoveTowards(currentVx, targetVx, acceleration * 0.6f * dt);

            // Hundimiento gradual y suave hacia -waterSinkSpeed (frenando inmediatamente la velocidad de caída)
            float currentVy = RB.linearVelocity.y;
            float targetVy = -waterSinkSpeed;

            // Si viene cayendo a alta velocidad (ej. desde una gran altura), frenar de inmediato con fuerte resistencia de agua (drag)
            float sinkRate;
            if (currentVy < -waterSinkSpeed)
            {
                sinkRate = 45f;
            }
            else if (currentVy > 0f)
            {
                sinkRate = 14f;
            }
            else
            {
                sinkRate = 8f;
            }

            float newVy = Mathf.MoveTowards(currentVy, targetVy, sinkRate * dt);

            // Límite estricto de velocidad de caída dentro del agua
            if (newVy < -waterSinkSpeed * 1.5f)
            {
                newVy = Mathf.MoveTowards(newVy, -waterSinkSpeed, 50f * dt);
            }

            RB.linearVelocity = new Vector2(newVx, newVy);
            return;
        }

        float rawX = MoveInput.x;
        float inputX = 0f;

        if (_wallJumpControlLockTimer > 0f)
        {
            inputX = _forcedInputX;
        }
        else
        {
            if (rawX > 0.1f) inputX = rawX;
            else if (rawX < -0.1f) inputX = rawX;
        }

        Vector2 currentVelocity = RB.linearVelocity;
        float slopeAngle = Vector2.Angle(GroundNormal, Vector2.up);
        bool isStableOnSlope = IsGrounded && slopeAngle <= maxSlopeAngle && !IsJumping;

        bool hasInput = Mathf.Abs(inputX) > 0.1f;
        float signInput = Mathf.Sign(inputX);

        // Determinar si ya es hora de correr según el tiempo de caminata continua (0.4s)
        bool isRunningTime = _continuousMoveTimer >= timeToStartRunning;

        // Determinar velocidad objetivo según estado (Crouch / Run / Walk)
        float targetMaxSpeed;
        if (IsCrouching)
        {
            targetMaxSpeed = crouchSpeed;
        }
        else if (isRunningTime)
        {
            targetMaxSpeed = runSpeed;
        }
        else
        {
            targetMaxSpeed = walkSpeed;
        }

        if (isStableOnSlope)
        {
            RB.gravityScale = 0f;

            Vector2 slopeForward = new Vector2(GroundNormal.y, -GroundNormal.x);
            float currentSlopeSpeed = Vector2.Dot(RB.linearVelocity, slopeForward);

            float effectiveMaxSpeed = targetMaxSpeed;
            if (slopeAngle > 0.01f) effectiveMaxSpeed *= slopeSpeedMultiplier;

            float targetSlopeSpeed = inputX * effectiveMaxSpeed;

            if (hasInput)
            {
                // Chequear si estamos derrapando/girando en contra de la velocidad actual
                bool isSkidding = Mathf.Abs(currentSlopeSpeed) > 1.5f && Mathf.Sign(currentSlopeSpeed) != signInput;
                float accelRate = isSkidding ? skidDeceleration : acceleration;

                currentSlopeSpeed = Mathf.MoveTowards(currentSlopeSpeed, targetSlopeSpeed, accelRate * Time.fixedDeltaTime);
                RB.linearVelocity = (slopeForward * currentSlopeSpeed) - (GroundNormal * 2f);
            }
            else
            {
                currentSlopeSpeed = Mathf.MoveTowards(currentSlopeSpeed, 0f, groundFriction * Time.fixedDeltaTime);
                if (Mathf.Abs(currentSlopeSpeed) < 0.05f)
                {
                    RB.linearVelocity = Vector2.zero;
                }
                else
                {
                    RB.linearVelocity = (slopeForward * currentSlopeSpeed) - (GroundNormal * 2f);
                }
                return;
            }
        }
        else
        {
            // FÍSICA EN AIRE / CAÍDA
            float currentVelocityY = currentVelocity.y;

            if (currentVelocityY < 0f)
            {
                RB.gravityScale = defaultGravityScale * fallGravityMult;
            }
            else if (currentVelocityY > 0f)
            {
                if (IsJumping)
                {
                    if (_jumpActionTimer >= jumpDuration)
                    {
                        RB.gravityScale = defaultGravityScale * fallGravityMult;
                    }
                    else
                    {
                        RB.gravityScale = _hasCutJump ? defaultGravityScale * jumpCutGravityMult : defaultGravityScale;
                    }
                }
                else
                {
                    RB.gravityScale = defaultGravityScale * fallGravityMult;
                }
            }
            else
            {
                RB.gravityScale = defaultGravityScale;
            }

            // Movimiento horizontal en el aire (velocidad fija independiente)
            if (hasInput)
            {
                float targetVelX = inputX * airSpeed;
                currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, targetVelX, acceleration * airControl * Time.fixedDeltaTime);
            }
            else
            {
                currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0f, airFriction * airControl * Time.fixedDeltaTime);
            }

            RB.linearVelocity = currentVelocity;
        }

        // Límite de velocidad vertical en deslizamiento de pared o caída libre
        float maxFall = IsWallSliding ? -wallSlideMaxSpeed : maxFallSpeed;
        if (!isStableOnSlope && RB.linearVelocity.y < maxFall)
        {
            Vector2 clampedVel = RB.linearVelocity;
            clampedVel.y = maxFall;
            RB.linearVelocity = clampedVel;
        }
    }

    private void ApplySpeedCap()
    {
        CurrentSpeedNormalized = Mathf.Clamp01(Mathf.Abs(RB.linearVelocity.x) / runSpeed);
    }

    #endregion

    #region Visuals & Animation Controller

    private void HandleVisuals()
    {
        float moveX = MoveInput.x;
        float velX = RB.linearVelocity.x;
        float velY = RB.linearVelocity.y;
        float absMoveX = Mathf.Abs(moveX);
        float absVelX = Mathf.Abs(velX);

        int inputDir = absMoveX > 0.1f ? (int)Mathf.Sign(moveX) : 0;
        bool hasMovementInput = inputDir != 0;

        // IsFalling se activa únicamente cuando se tiene velocidad negativa real en el aire
        IsFalling = !IsGrounded && !IsInWater && velY < -0.5f && !IsWallSliding;

        bool isRunningTime = _continuousMoveTimer >= timeToStartRunning;
        bool isActuallyRunning = absVelX > walkSpeed * 0.9f;

        // ---------------------------------------------------------------
        // GIROS (SKID / RUN TURN / JUMP TURN / CROUCH TURN)
        // ---------------------------------------------------------------
        if (hasMovementInput)
        {
            if (IsGrounded && !IsInWater)
            {
                if (IsCrouching)
                {
                    if (inputDir != FacingDirection && _turnLockTimer <= 0f)
                    {
                        TriggerAnimation(_animCrouchTurn);
                        _turnLockTimer = turnVisualDuration;
                        SetFacingDirection(inputDir);
                    }
                }
                else
                {
                    // Giro brusco / Skid: La velocidad actual va en dirección contraria al input presionado
                    bool isSkidding = absVelX > walkSpeed * 0.4f && Mathf.Sign(velX) != inputDir;

                    if (isSkidding && _turnLockTimer <= 0f)
                    {
                        ResetTriggerAnimation(_animRunToIdle);
                        TriggerAnimation(_animRunTurn);
                        _turnLockTimer = turnVisualDuration;
                        _directionChangeBuffer = turnBufferWindow;
                        SetFacingDirection(inputDir);
                    }
                }
            }
            else if (!IsWallSliding)
            {
                // En el aire / agua: JumpTurn al cambiar dirección de input
                if (inputDir != FacingDirection && _turnLockTimer <= 0f && !IsInWater)
                {
                    TriggerAnimation(_animJumpTurn);
                    _turnLockTimer = 0.15f;
                    SetFacingDirection(inputDir);
                }
            }
        }

        // Si no estamos bloqueados por giro, orientar visual hacia el input
        if (hasMovementInput && _turnLockTimer <= 0f)
        {
            SetFacingDirection(inputDir);
        }

        // ---------------------------------------------------------------
        // DETECCIÓN RUN TO IDLE (Frenar en seco tras correr)
        // ---------------------------------------------------------------
        if (_wasMoving && !hasMovementInput && IsGrounded && !IsInWater && _turnLockTimer <= 0f)
        {
            if (_wasActuallyRunningWhenStopped)
            {
                TriggerAnimation(_animRunToIdle);
            }
            _wasActuallyRunningWhenStopped = false;
        }

        _wasActuallyRunningWhenStopped = isActuallyRunning && isRunningTime && !IsInWater;
        _wasMoving = hasMovementInput;

        // ---------------------------------------------------------------
        // FALL TRIGGER (Solo al iniciar caída en el aire)
        // ---------------------------------------------------------------
        bool startedFalling = IsFalling && !_wasFalling;
        if (startedFalling && !IsWallSliding && !IsInWater && _turnLockTimer <= 0f)
        {
            TriggerAnimation(_animFall);
        }
        _wasFalling = IsFalling;

        // ---------------------------------------------------------------
        // ACTUALIZACIÓN DE PARÁMETROS BOOLS Y FLOATS DEL ANIMATOR
        // ---------------------------------------------------------------
        if (animator != null)
        {
            bool isJumpingFrame = IsJumping && velY > 0.1f;
            bool groundedVisual = (IsGrounded || IsInWater) && !isJumpingFrame;
            bool movingGrounded = hasMovementInput && groundedVisual && absVelX > 0.1f;

            // Caminar vs Correr:
            // - Correr se activa tras caminar continuamente el tiempo requerido (0.4s) y alcanzar velocidad
            // - Caminar es el estado natural al empezar a moverse (y el único en agua)
            bool isRunning = movingGrounded && !IsCrouching && isActuallyRunning && isRunningTime && !IsInWater;
            bool isWalking = movingGrounded && !IsCrouching && (!isRunning || IsInWater);
            bool isCrouchWalking = movingGrounded && IsCrouching && absVelX > 0.1f && !IsInWater;

            SetFloatAnimation(_animSpeed, CurrentSpeedNormalized);
            SetBoolAnimation(_animGrounded, groundedVisual);
            SetBoolAnimation(_animWallSlide, IsWallSliding && !groundedVisual);
            SetBoolAnimation(_animFallLoop, IsFalling && !groundedVisual);
            SetBoolAnimation(_animCrouch, IsCrouching && groundedVisual && !IsInWater);
            SetBoolAnimation(_animCrouchWalk, isCrouchWalking);
            SetBoolAnimation(_animWalk, isWalking);
            SetBoolAnimation(_animRun, isRunning);

            if (runGhostTrail != null)
            {
                bool isRunningFast = (isRunning || (movingGrounded && !IsCrouching && (RunHeld || isActuallyRunning || CurrentSpeedNormalized > walkToRunThreshold))) && !IsInWater;
                runGhostTrail.SetTrailActive(isRunningFast && gameObject.activeInHierarchy && enabled);
            }

            // Transición WalkToRun disparada justo al cumplir el tiempo de caminata para acelerar a la carrera
            if (isWalking && movingGrounded && isRunningTime && absVelX > walkSpeed * 0.7f && !_walkToRunTriggered)
            {
                TriggerAnimation(_animWalkToRun);
                _walkToRunTriggered = true;
            }
            else if (!movingGrounded || !isRunningTime || isRunning)
            {
                _walkToRunTriggered = false;
            }
        }

        // Orientación visual
        ApplyVisualOrientation();
    }

    /// <summary>
    /// Aplica la orientación horizontal al transform de visualsRoot o SpriteRenderer,
    /// asegurando que el Rigidbody y sus físicas no sufran rotaciones o desfases.
    /// </summary>
    private void ApplyVisualOrientation()
    {
        if (visualsRoot == null) return;

        bool flipLocked = _turnLockTimer > 0f || _wallJumpControlLockTimer > 0f;

        if (IsWallSliding && _activeWallSide != 0 && !IsGrounded)
        {
            SetVisualFacing(-_activeWallSide);
        }
        else if (FacingDirection != 0 && !flipLocked)
        {
            SetVisualFacing(FacingDirection);
        }
    }

    public void SetFacingDirection(int dir)
    {
        if (dir == 0) return;
        FacingDirection = dir;
        SetVisualFacing(dir);
    }

    private void SetVisualFacing(int dir)
    {
        if (flipUsingSpriteRenderer && spriteRenderer != null)
        {
            spriteRenderer.flipX = (dir < 0);
        }
        else if (visualsRoot != null)
        {
            Vector3 scale = visualsRoot.localScale;
            scale.x = _baseVisualScaleX * dir;
            visualsRoot.localScale = scale;
        }
    }

    #endregion

    #region Animation Helpers

    private void CacheAnimationHashes()
    {
        _animSpeed = Animator.StringToHash("Speed");
        _animGrounded = Animator.StringToHash("Grounded");
        _animWallSlide = Animator.StringToHash("WallSlide");
        _animCrouch = Animator.StringToHash("Crouch");
        _animCrouchWalk = Animator.StringToHash("CrouchWalk");
        _animFallLoop = Animator.StringToHash("FallLoop");
        _animRun = Animator.StringToHash("Run");
        _animWalk = Animator.StringToHash("Walk");
        _animJump = Animator.StringToHash("Jump");
        _animWallJump = Animator.StringToHash("WallJump");
        _animJumpTurn = Animator.StringToHash("JumpTurn");
        _animRunTurn = Animator.StringToHash("RunTurn");
        _animCrouchTurn = Animator.StringToHash("CrouchTurn");
        _animWalkToRun = Animator.StringToHash("WalkToRun");
        _animFall = Animator.StringToHash("Fall");
        _animRunToIdle = Animator.StringToHash("RunToIdle");
        _animIdleTurn = Animator.StringToHash("IdleTurn");
    }

    private void TriggerAnimation(int hash)
    {
        if (animator != null && hash != 0)
        {
            animator.ResetTrigger(hash);
            animator.SetTrigger(hash);
        }
    }

    private void ResetTriggerAnimation(int hash)
    {
        if (animator != null && hash != 0)
        {
            animator.ResetTrigger(hash);
        }
    }

    private void SetBoolAnimation(int hash, bool value)
    {
        if (animator != null && hash != 0)
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetFloatAnimation(int hash, float value)
    {
        if (animator != null && hash != 0)
        {
            animator.SetFloat(hash, value);
        }
    }

    #endregion

    #region Gizmos & Debugging

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Collider2D col = MainCollider != null ? MainCollider : GetComponent<Collider2D>();
        if (col == null) return;

        // 1. Ground Check Box
        Gizmos.color = Color.green;
        float skinWidth = 0.02f;
        Vector2 boxSize = new Vector2(col.bounds.size.x * 0.9f, skinWidth);
        Vector2 origin = (Vector2)col.bounds.center + Vector2.down * (col.bounds.extents.y - skinWidth);
        float castDist = skinWidth + groundCheckBuffer;
        Gizmos.DrawWireCube(origin + Vector2.down * (castDist / 2f), new Vector3(boxSize.x, castDist, 0f));

        // 2. Wall Check Boxes (Right & Left)
        if (enableWallMechanics)
        {
            Gizmos.color = Color.cyan;
            Vector2 wallOrigin = (Vector2)col.bounds.center + wallCheckOffset;
            float halfExtentX = col.bounds.extents.x;
            Vector2 rightCenter = wallOrigin + Vector2.right * (halfExtentX + wallCheckDistance / 2f);
            Vector2 leftCenter = wallOrigin + Vector2.left * (halfExtentX + wallCheckDistance / 2f);

            Gizmos.DrawWireCube(rightCenter, new Vector3(wallCheckSize.x + wallCheckDistance, wallCheckSize.y, 0f));
            Gizmos.DrawWireCube(leftCenter, new Vector3(wallCheckSize.x + wallCheckDistance, wallCheckSize.y, 0f));
        }

        // 3. Ceiling Check Circle
        if (enableCrouch)
        {
            Gizmos.color = Color.yellow;
            Vector2 ceilingOrigin = (Vector2)col.bounds.center + ceilingCheckOffset;
            Gizmos.DrawWireSphere(ceilingOrigin, ceilingCheckRadius);
        }
    }

    #endregion
}
