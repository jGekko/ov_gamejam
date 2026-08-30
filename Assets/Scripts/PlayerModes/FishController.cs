using UnityEngine;
using PrimeTween;

/// <summary>
/// Controlador del Pez (Game Jam Edition).
/// 
/// Características:
/// - En Agua:
///   * Movimiento 360° en agua (A/D para horizontal, W para subir, S para bajar).
///   * Inclinación visual procedural suave al nadar hacia arriba/abajo.
///   * Habilidad con Espacio:
///     - En lo profundo del agua: pequeño aleteo/hop con squash & stretch procedural.
///     - Cerca de la superficie del agua: gran salto parabólico exagerado para salir del agua hacia otro lago.
/// - Periodo de gracia al salir del agua:
///   * Al saltar del agua, vuela parabólicamente conservando inercia sin penalización hasta tocar el suelo.
/// - En Tierra:
///   * Desplazamiento lateral y rebotes físicos procedurales (coletazos / flop) mientras no puede respirar.
///   * Temporizador de asfixia: descuenta tiempo solo cuando toca el suelo (IsGrounded). Al agotarse, fuerza la transformación a Humano.
///   * Al entrar al agua, el temporizador de asfixia se recupera instantáneamente al 100%.
/// - Sincronización con Animator (Move bool) y PrimeTween para Game Feel.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FishController : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Visual & Orientation ---")]
    [Tooltip("Transform hijo que contiene los sprites/animaciones.")]
    public Transform visualsRoot;
    [Tooltip("Componente Animator asignado al pez.")]
    public Animator animator;
    [Tooltip("SpriteRenderer opcional.")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Si es true, invierte SpriteRenderer.flipX en lugar de localScale.x.")]
    public bool flipUsingSpriteRenderer = false;

    [Header("--- Water Swimming Movement ---")]
    [Tooltip("Velocidad de nado horizontal.")]
    public float swimSpeedHorizontal = 9f;
    [Tooltip("Velocidad de nado hacia arriba (tecla W).")]
    public float swimSpeedUp = 7.5f;
    [Tooltip("Velocidad de nado hacia abajo (tecla S).")]
    public float swimSpeedDown = 7f;
    [Tooltip("Aceleración de nado.")]
    public float swimAcceleration = 35f;
    [Tooltip("Fricción en el agua cuando no hay input.")]
    public float waterFriction = 20f;
    [Tooltip("Factor de amortiguación vertical al entrar al agua desde el aire (0 = frena en seco, 1 = sin freno).")]
    [Range(0f, 1f)]
    public float waterEntryVerticalDamping = 0.35f;
    [Tooltip("Velocidad máxima hacia abajo permitida al entrar al agua (evita hundirse hasta el fondo/hazards).")]
    public float maxWaterEntryDownwardSpeed = 3.5f;
    [Tooltip("Profundidad objetivo natural de flotación por debajo de la superficie del agua.")]
    public float surfaceSubmersionDepth = 0.45f;

    [Header("--- Water Jump / Leap (Space) ---")]
    [Tooltip("Distancia vertical a la superficie considerada 'cerca de la superficie'.")]
    public float surfaceProximityThreshold = 1.0f;
    [Tooltip("Impulso vertical al presionar Espacio en lo profundo del agua.")]
    public float deepHopForce = 6.5f;
    [Tooltip("Fuerza vertical del salto parabólico al estar cerca de la superficie.")]
    public float surfaceLeapVerticalForce = 15f;
    [Tooltip("Fuerza horizontal adicional aplicada en el salto de superficie.")]
    public float surfaceLeapHorizontalForce = 8f;
    [Tooltip("Cooldown entre impulsos en agua.")]
    public float leapCooldown = 0.35f;

    [Header("--- Land Suffocation & Movement ---")]
    [Tooltip("Velocidad de arrastre en tierra.")]
    public float landFlopSpeed = 2.5f;
    [Tooltip("Aceleración en tierra.")]
    public float landAcceleration = 15f;
    [Tooltip("Fricción en tierra.")]
    public float landFriction = 30f;
    [Tooltip("Fuerza de impulso vertical en los rebotes en tierra (coletazos).")]
    public float groundBounceForce = 4.2f;
    [Tooltip("Intervalo mínimo entre rebotes en tierra (en segundos).")]
    public float groundBounceIntervalMin = 0.35f;
    [Tooltip("Intervalo máximo entre rebotes en tierra (en segundos).")]
    public float groundBounceIntervalMax = 0.6f;
    [Tooltip("Gravedad aplicada en el aire y tierra.")]
    public float airGravity = 3.5f;
    [Tooltip("Tiempo máximo en segundos que puede sobrevivir en tierra tocando el suelo antes de forzar regreso a Humano.")]
    public float suffocationDuration = 2f;

    [Header("--- Procedural Juice & Tilting (PrimeTween) ---")]
    [Tooltip("Habilita efectos procedurales de inclinación y squash/stretch.")]
    public bool enableProceduralJuice = true;
    [Tooltip("Ángulo máximo de inclinación al nadar verticalmente.")]
    public float maxSwimTiltAngle = 25f;
    [Tooltip("Velocidad de suavizado para la inclinación visual.")]
    public float tiltSmoothSpeed = 10f;

    [Header("--- Ground Detection ---")]
    [Tooltip("Capas consideradas suelo.")]
    public LayerMask groundLayer;
    [Tooltip("Buffer de detección de suelo.")]
    public float groundCheckBuffer = 0.08f;

    [Header("--- Swim Ghost Trail ---")]
    [Tooltip("GhostTrail asignado al nado continuo del pez (Tonos grisáceos y azul océano).")]
    public GhostTrail swimGhostTrail;
    [Tooltip("Si es true, auto-configura el GhostTrail para el nado si no está asignado.")]
    public bool autoSetupSwimGhostTrail = true;

    [Header("--- Debug & Gizmos ---")]
    public bool showGizmos = true;

    #endregion

    #region Public Properties & State

    public Rigidbody2D RB { get; private set; }
    public Collider2D MainCollider { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsInWater { get; private set; }
    public bool IsInWaterLeapGrace => _isWaterLeapGrace;
    public int FacingDirection { get; private set; } = 1;

    /// <summary>
    /// Tiempo restante de oxígeno en tierra (de 0 a suffocationDuration).
    /// </summary>
    public float SuffocationTimer { get; private set; }

    /// <summary>
    /// Porcentaje de oxígeno restante (1 = lleno, 0 = asfixiado).
    /// </summary>
    public float OxygenPercentage => suffocationDuration > 0f ? Mathf.Clamp01(SuffocationTimer / suffocationDuration) : 1f;

    // Inputs virtualizables
    public Vector2 MoveInput { get; set; }
    public bool JumpPressed { get; set; }

    #endregion

    #region Animation Hashes

    private int _animMove;

    #endregion

    #region Internal State

    private WaterZone _currentWaterZone;
    private PlayerTransformationManager _transformationManager;
    private float _leapCooldownTimer;
    private float _groundBounceTimer;
    private bool _isWaterLeapGrace;
    private float _currentTiltAngle;
    private float _baseVisualScaleX = 1f;
    private float _baseVisualScaleY = 1f;
    private Sequence _squashSequence;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        RB = GetComponent<Rigidbody2D>();
        MainCollider = GetComponent<Collider2D>();
        _transformationManager = GetComponent<PlayerTransformationManager>() ?? GetComponentInParent<PlayerTransformationManager>();
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
        SuffocationTimer = suffocationDuration;
        _leapCooldownTimer = 0f;
        _groundBounceTimer = Random.Range(groundBounceIntervalMin, groundBounceIntervalMax);
        _isWaterLeapGrace = false;
        _currentTiltAngle = 0f;

        if (_squashSequence.isAlive)
        {
            _squashSequence.Stop();
        }

        if (visualsRoot != null && visualsRoot != transform)
        {
            visualsRoot.localScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);
        }

        if (RB != null)
        {
            RB.gravityScale = IsInWater ? 0f : airGravity;
        }
        ApplyVisualFacing();
    }

    private void OnDisable()
    {
        if (_squashSequence.isAlive)
        {
            _squashSequence.Stop();
        }
        if (visualsRoot != null && visualsRoot != transform)
        {
            visualsRoot.localScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);
        }
        if (animator != null)
        {
            animator.speed = 1f;
        }
        if (swimGhostTrail != null)
        {
            swimGhostTrail.SetTrailActive(false);
            swimGhostTrail.ClearAllClones();
        }
    }

    public void CacheVisualReferences()
    {
        if (visualsRoot == null)
        {
            var ptm = GetComponent<PlayerTransformationManager>() ?? GetComponentInParent<PlayerTransformationManager>();
            if (ptm != null && ptm.fishVisuals != null)
            {
                visualsRoot = ptm.fishVisuals.transform;
            }
            else
            {
                Transform found = transform.Find("FishVisuals") ?? transform.Find("Fish") ?? transform.Find("Visuals");
                if (found != null) visualsRoot = found;
                else if (transform.childCount > 0)
                {
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var child = transform.GetChild(i);
                        if (child.name.ToLower().Contains("fish"))
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
                    if (a.name.ToLower().Contains("fish") ||
                        (a.runtimeAnimatorController != null && a.runtimeAnimatorController.name.ToLower().Contains("fish")))
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
            if (_baseVisualScaleX <= 0.001f) _baseVisualScaleX = 1f;
            if (_baseVisualScaleY <= 0.001f) _baseVisualScaleY = 1f;

            if (swimGhostTrail == null)
            {
                swimGhostTrail = visualsRoot.GetComponent<GhostTrail>();
                if (swimGhostTrail == null && autoSetupSwimGhostTrail)
                {
                    swimGhostTrail = visualsRoot.gameObject.AddComponent<GhostTrail>();
                }
            }

            if (swimGhostTrail != null)
            {
                swimGhostTrail.targetSpriteRenderer = spriteRenderer;
                swimGhostTrail.profile = GhostTrail.GhostTrailProfile.FishSwim;
                swimGhostTrail.ApplyProfileDefaults(GhostTrail.GhostTrailProfile.FishSwim);
                swimGhostTrail.EnsureTargetSpriteRenderer();
            }
        }
    }

    public void SetInitialVelocity(Vector2 initialVel)
    {
        if (IsInWater && initialVel.y < 0f)
        {
            initialVel.y = Mathf.Max(initialVel.y * waterEntryVerticalDamping, -maxWaterEntryDownwardSpeed);
        }
        if (RB != null) RB.linearVelocity = initialVel;
    }

    /// <summary>
    /// Aplica un impulso de velocidad (Speed Boost) en una dirección dada.
    /// </summary>
    public void ApplySpeedBoost(Vector2 boostVelocity)
    {
        if (RB != null)
        {
            RB.linearVelocity = boostVelocity;
            if (boostVelocity.x > 0.1f) SetFacingDirection(1);
            else if (boostVelocity.x < -0.1f) SetFacingDirection(-1);
        }
    }

    public void SetInWater(bool inWater, WaterZone waterZone)
    {
        bool wasInWater = IsInWater;
        IsInWater = inWater;
        _currentWaterZone = inWater ? waterZone : null;

        if (inWater)
        {
            _isWaterLeapGrace = false;
            // Se recupera instantáneamente el oxígeno al tocar agua
            SuffocationTimer = suffocationDuration;
            if (RB != null)
            {
                RB.gravityScale = 0f;
                // Al entrar al agua, frenar inmediatamente cualquier velocidad de caída vertical
                Vector2 v = RB.linearVelocity;
                if (v.y < 0f)
                {
                    v.y = Mathf.Max(v.y * waterEntryVerticalDamping, -maxWaterEntryDownwardSpeed);
                }
                v.x *= 0.85f;
                RB.linearVelocity = v;
            }
        }
        else
        {
            // Si venía del agua, entra en periodo de gracia parabólico hasta tocar suelo
            if (wasInWater)
            {
                _isWaterLeapGrace = true;
            }
            if (RB != null) RB.gravityScale = airGravity;
        }
    }

    protected virtual void Update()
    {
        GatherInput();
        UpdateTimers();
        UpdateGroundCheck();
        HandleWaterJumpTrigger();
        HandleGroundBounces();
        HandleVisuals();
        UpdateSwimGhostTrail();
    }

    private void UpdateSwimGhostTrail()
    {
        if (swimGhostTrail != null)
        {
            bool isMoving = RB != null && (RB.linearVelocity.sqrMagnitude > 0.15f || MoveInput.sqrMagnitude > 0.05f);
            swimGhostTrail.SetTrailActive(isMoving && gameObject.activeInHierarchy && enabled);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (IsInWater)
        {
            ExecuteWaterSwimPhysics();
        }
        else
        {
            ExecuteLandPhysics();
        }
    }

    #endregion

    #region Input Collection

    protected virtual void GatherInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        MoveInput = new Vector2(h, v);
        JumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump");
    }

    #endregion

    #region Timers & Ground Checks

    private void UpdateTimers()
    {
        if (_leapCooldownTimer > 0f) _leapCooldownTimer -= Time.deltaTime;

        // Asfixia en tierra: solo descuenta si está en tierra Y tocando el suelo (después de terminar la gracia)
        if (!IsInWater)
        {
            if (IsGrounded)
            {
                _isWaterLeapGrace = false; // Tocar el suelo cancela la gracia parabólica
                SuffocationTimer -= Time.deltaTime;
                if (SuffocationTimer <= 0f)
                {
                    SuffocationTimer = 0f;
                    // Forzar transformación de vuelta a Humano
                    if (_transformationManager != null)
                    {
                        _transformationManager.ForceRevertToHuman();
                    }
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

        LayerMask mask = groundLayer != 0 ? groundLayer : ~LayerMask.GetMask("Ignore Raycast");

        float skinWidth = 0.02f;
        Vector2 boxSize = new Vector2(MainCollider.bounds.size.x * 0.9f, skinWidth);
        Vector2 origin = (Vector2)MainCollider.bounds.center + Vector2.down * (MainCollider.bounds.extents.y - skinWidth);
        float castDist = skinWidth + groundCheckBuffer;

        RaycastHit2D hit = Physics2D.BoxCast(origin, boxSize, 0f, Vector2.down, castDist, mask);
        IsGrounded = hit.collider != null && hit.collider != MainCollider && !hit.collider.isTrigger;
    }

    private void HandleWaterJumpTrigger()
    {
        if (!IsInWater || !JumpPressed || _leapCooldownTimer > 0f) return;

        _leapCooldownTimer = leapCooldown;

        float distanceToSurface = float.MaxValue;
        if (_currentWaterZone != null)
        {
            distanceToSurface = _currentWaterZone.GetDistanceToSurface(transform.position);
        }

        if (distanceToSurface <= surfaceProximityThreshold)
        {
            // Cerca de la superficie: Gran salto parabólico fuera del agua
            float hImpulse = MoveInput.x != 0 ? Mathf.Sign(MoveInput.x) * surfaceLeapHorizontalForce : FacingDirection * (surfaceLeapHorizontalForce * 0.7f);
            Vector2 leapVelocity = new Vector2(hImpulse, surfaceLeapVerticalForce);
            if (RB != null) RB.linearVelocity = leapVelocity;

            _isWaterLeapGrace = true;
            PlaySurfaceLeapJuice();
        }
        else
        {
            // En lo profundo: Pequeño hop vertical hacia arriba
            if (RB != null)
            {
                Vector2 hopVelocity = new Vector2(RB.linearVelocity.x, deepHopForce);
                RB.linearVelocity = hopVelocity;
            }

            PlayDeepHopJuice();
        }
    }

    private void HandleGroundBounces()
    {
        // En tierra tocando el suelo, el pez rebota/coletea periódicamente
        if (!IsInWater && IsGrounded)
        {
            _groundBounceTimer -= Time.deltaTime;
            if (_groundBounceTimer <= 0f)
            {
                _groundBounceTimer = Random.Range(groundBounceIntervalMin, groundBounceIntervalMax);
                ExecuteGroundFlopBounce();
            }
        }
    }

    private void ExecuteGroundFlopBounce()
    {
        if (RB == null) return;

        // Impulso vertical y leve avance horizontal al aletear en tierra
        float hopX = MoveInput.x != 0 ? MoveInput.x * landFlopSpeed : Random.Range(-0.6f, 0.6f);
        RB.linearVelocity = new Vector2(hopX, groundBounceForce);

        PlayLandFlopJuice();
    }

    #endregion

    #region Physics Movements

    private void ExecuteWaterSwimPhysics()
    {
        if (RB == null) return;

        float dt = Time.fixedDeltaTime;

        // Movimiento Horizontal
        float targetVx = MoveInput.x * swimSpeedHorizontal;
        float currentVx = RB.linearVelocity.x;
        float newVx = Mathf.MoveTowards(currentVx, targetVx, (Mathf.Abs(MoveInput.x) > 0.01f ? swimAcceleration : waterFriction) * dt);

        // Movimiento Vertical (W sube, S baja)
        float currentVy = RB.linearVelocity.y;
        float targetVy = 0f;

        if (MoveInput.y > 0.1f)
        {
            targetVy = MoveInput.y * swimSpeedUp;
            currentVy = Mathf.MoveTowards(currentVy, targetVy, swimAcceleration * dt);
        }
        else if (MoveInput.y < -0.1f)
        {
            targetVy = MoveInput.y * swimSpeedDown;
            currentVy = Mathf.MoveTowards(currentVy, targetVy, swimAcceleration * dt);
        }
        else
        {
            // Sin input vertical:
            // Si el pez está por encima o sobresaliendo del agua, sumergirlo suavemente hasta su profundidad natural
            if (_currentWaterZone != null && transform.position.y > _currentWaterZone.SurfaceY - surfaceSubmersionDepth)
            {
                targetVy = -1.8f;
                currentVy = Mathf.MoveTowards(currentVy, targetVy, swimAcceleration * 0.6f * dt);
            }
            else
            {
                // En el cuerpo de agua: Flotabilidad neutra estable
                currentVy = Mathf.MoveTowards(currentVy, 0f, waterFriction * dt);
            }
        }

        RB.linearVelocity = new Vector2(newVx, currentVy);
    }

    private void ExecuteLandPhysics()
    {
        if (RB == null) return;

        float dt = Time.fixedDeltaTime;

        // Si está en el aire en gracia tras saltar del agua, conserva velocidad parabólica
        if (!IsGrounded && _isWaterLeapGrace)
        {
            // Vuelo libre balístico en el aire
            return;
        }

        // En tierra o aire regular
        float targetVx = MoveInput.x * landFlopSpeed;
        float currentVx = RB.linearVelocity.x;

        float rate = Mathf.Abs(MoveInput.x) > 0.01f ? landAcceleration : landFriction;
        float newVx = Mathf.MoveTowards(currentVx, targetVx, rate * dt);

        RB.linearVelocity = new Vector2(newVx, RB.linearVelocity.y);
    }

    #endregion

    #region Visuals & Animation

    private void HandleVisuals()
    {
        float moveX = MoveInput.x;
        float velX = RB != null ? RB.linearVelocity.x : 0f;
        float velY = RB != null ? RB.linearVelocity.y : 0f;
        float absVelX = Mathf.Abs(velX);

        // 1. Orientación horizontal
        if (Mathf.Abs(moveX) > 0.05f)
        {
            SetFacingDirection(moveX > 0 ? 1 : -1);
        }
        else if (absVelX > 0.1f)
        {
            SetFacingDirection(velX > 0 ? 1 : -1);
        }
        else
        {
            ApplyVisualFacing();
        }

        // 2. Inclinación visual procedural al nadar o volar en el salto
        if (enableProceduralJuice && visualsRoot != null)
        {
            float targetTilt = 0f;

            if (IsInWater)
            {
                if (Mathf.Abs(MoveInput.y) > 0.1f || Mathf.Abs(velY) > 0.5f)
                {
                    float verticalSign = MoveInput.y != 0 ? Mathf.Sign(MoveInput.y) : Mathf.Sign(velY);
                    targetTilt = verticalSign * maxSwimTiltAngle * FacingDirection;
                }
            }
            else if (!IsGrounded && _isWaterLeapGrace && absVelX > 0.5f)
            {
                // En el aire durante el salto: inclinar el pez siguiendo la trayectoria balística
                float flightAngle = Mathf.Atan2(velY, absVelX) * Mathf.Rad2Deg * FacingDirection;
                targetTilt = Mathf.Clamp(flightAngle, -45f * FacingDirection, 45f * FacingDirection);
            }

            _currentTiltAngle = Mathf.MoveTowardsAngle(_currentTiltAngle, targetTilt, tiltSmoothSpeed * 10f * Time.deltaTime);
            visualsRoot.localEulerAngles = new Vector3(0f, 0f, _currentTiltAngle);
        }

        // 3. Animator parameters (Move bool)
        if (animator != null)
        {
            // En tierra siempre se activa Move (aleteo rápido de asfixia) y aumenta la velocidad del Animator
            if (!IsInWater)
            {
                animator.speed = 1.35f;
                SetBoolAnimation(_animMove, true);
            }
            else
            {
                animator.speed = 1f;
                bool isMoving = (absVelX > 0.1f || Mathf.Abs(moveX) > 0.1f || Mathf.Abs(MoveInput.y) > 0.1f);
                SetBoolAnimation(_animMove, isMoving);
            }
        }
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
            if (!_squashSequence.isAlive)
            {
                s.y = _baseVisualScaleY;
                s.z = 1f;
            }
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

    #region Procedural Juice (PrimeTween)

    private void PlayDeepHopJuice()
    {
        if (!enableProceduralJuice || visualsRoot == null) return;

        if (_squashSequence.isAlive) _squashSequence.Stop();
        visualsRoot.localScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);

        Vector3 squashedScale = new Vector3(_baseVisualScaleX * FacingDirection * 0.85f, _baseVisualScaleY * 1.18f, 1f);
        Vector3 defaultScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);

        _squashSequence = Sequence.Create()
            .Chain(Tween.Scale(visualsRoot, squashedScale, 0.10f, Ease.OutQuad))
            .Chain(Tween.Scale(visualsRoot, defaultScale, 0.16f, Ease.InQuad));
    }

    private void PlaySurfaceLeapJuice()
    {
        if (!enableProceduralJuice || visualsRoot == null) return;

        if (_squashSequence.isAlive) _squashSequence.Stop();
        visualsRoot.localScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);

        Vector3 stretchedScale = new Vector3(_baseVisualScaleX * FacingDirection * 1.20f, _baseVisualScaleY * 0.85f, 1f);
        Vector3 defaultScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);

        _squashSequence = Sequence.Create()
            .Chain(Tween.Scale(visualsRoot, stretchedScale, 0.12f, Ease.OutQuad))
            .Chain(Tween.Scale(visualsRoot, defaultScale, 0.18f, Ease.InQuad));
    }

    private void PlayLandFlopJuice()
    {
        if (!enableProceduralJuice || visualsRoot == null) return;

        if (_squashSequence.isAlive) _squashSequence.Stop();
        visualsRoot.localScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);

        Vector3 flopScale = new Vector3(_baseVisualScaleX * FacingDirection * 1.15f, _baseVisualScaleY * 0.88f, 1f);
        Vector3 defaultScale = new Vector3(_baseVisualScaleX * FacingDirection, _baseVisualScaleY, 1f);

        _squashSequence = Sequence.Create()
            .Chain(Tween.Scale(visualsRoot, flopScale, 0.08f, Ease.OutQuad))
            .Chain(Tween.Scale(visualsRoot, defaultScale, 0.14f, Ease.InQuad));
    }

    #endregion

    #region Animation Helpers

    private void CacheAnimationHashes()
    {
        _animMove = Animator.StringToHash("Move");
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
                animator.SetBool("Move", value);
            }
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = IsInWater ? Color.blue : (IsGrounded ? Color.yellow : Color.gray);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }

    #endregion
}
