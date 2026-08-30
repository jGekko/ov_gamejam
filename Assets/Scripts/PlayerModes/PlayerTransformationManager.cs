using System;
using UnityEngine;

/// <summary>
/// Formas disponibles de transformación.
/// </summary>
public enum AnimalForm
{
    Human = 0,
    Bird = 1,
    Crocodile = 2,
    Fish = 3
}

/// <summary>
/// Administrador central de transformación del jugador.
/// Gestiona la transición seamless entre formas (Humano, Ave, Cocodrilo, Pez),
/// conservando inercia, verificando espacio libre (clearance check) y manejando estados de agua/tierra.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerTransformationManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Current Form ---")]
    [Tooltip("Forma inicial del jugador.")]
    public AnimalForm currentForm = AnimalForm.Human;

    [Header("--- Controllers ---")]
    public HumanController humanController;
    public BirdController birdController;
    public CrocodileController crocodileController;
    public FishController fishController;

    [Header("--- Visuals (GameObjects hijos de cada forma) ---")]
    public GameObject humanVisuals;
    public GameObject birdVisuals;
    public GameObject crocodileVisuals;
    public GameObject fishVisuals;

    [Header("--- Colliders por Forma ---")]
    [Tooltip("Collider para forma Humana.")]
    public Collider2D humanCollider;
    [Tooltip("Collider para forma Ave.")]
    public Collider2D birdCollider;
    [Tooltip("Collider para forma Cocodrilo.")]
    public Collider2D crocodileCollider;
    [Tooltip("Collider para forma Pez.")]
    public Collider2D fishCollider;

    [Header("--- Clearance & Collision Check ---")]
    [Tooltip("Capas de obstáculos/muros a verificar antes de permitir transformación.")]
    public LayerMask obstacleLayer;
    [Tooltip("Si es true, no permite transformar si el nuevo collider chocaría con una pared sólida.")]
    public bool enableClearanceCheck = true;

    [Header("--- Momentum Conservation ---")]
    [Tooltip("Porcentaje de velocidad conservado al transformarse (0 = detenerse, 1 = 100% de inercia).")]
    [Range(0f, 1f)] public float momentumConservation = 0.95f;

    [Header("--- Debug ---")]
    public bool debugLogs = false;

    [Header("--- Transformation & Death VFX ---")]
    [Tooltip("Animator especial asignado a los efectos visuales de transformación y muerte.")]
    public Animator transformVFXAnimator;
    [Tooltip("Nombre del Trigger para reproducir la animación de transformación.")]
    public string transformVFXTrigger = "transform";
    [Tooltip("Nombre del Trigger para reproducir la animación de muerte.")]
    public string deathVFXTrigger = "death";
    [Tooltip("Si es true, sincroniza la posición del VFX con la posición del jugador al transformarse o morir.")]
    public bool syncVFXPosition = true;

    public static PlayerTransformationManager Instance { get; private set; }

    #endregion

    #region Events & Properties

    public event Action<AnimalForm, AnimalForm> OnFormChanged; // (previousForm, newForm)

    public Rigidbody2D RB { get; private set; }
    public bool IsInWater { get; private set; }
    public WaterZone CurrentWaterZone { get; private set; }
    public bool IsTransforming { get; private set; }
    public bool IsPhysicsPaused { get; private set; }

    private Vector2 _savedVelocity;
    private RigidbodyType2D _savedBodyType = RigidbodyType2D.Dynamic;
    private float _savedGravityScale = 1f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null) Instance = this;
        RB = GetComponent<Rigidbody2D>();
        AutoFindReferences();
        InitializeForm(currentForm);
    }

    private void Start()
    {
        CheckInitialWaterOverlap();
    }

    private void AutoFindReferences()
    {
        if (humanController == null) humanController = GetComponent<HumanController>();
        if (birdController == null) birdController = GetComponent<BirdController>();
        if (crocodileController == null) crocodileController = GetComponent<CrocodileController>();
        if (fishController == null) fishController = GetComponent<FishController>();

        // Auto-buscar GameObjects visuales si no están asignados
        if (humanVisuals == null) humanVisuals = transform.Find("HumanVisuals")?.gameObject ?? transform.Find("Human")?.gameObject;
        if (birdVisuals == null) birdVisuals = transform.Find("BirdVisuals")?.gameObject ?? transform.Find("Bird")?.gameObject;
        if (crocodileVisuals == null) crocodileVisuals = transform.Find("CrocodileVisuals")?.gameObject ?? transform.Find("Crocodile")?.gameObject;
        if (fishVisuals == null) fishVisuals = transform.Find("FishVisuals")?.gameObject ?? transform.Find("Fish")?.gameObject;

        // Auto-enlazar visualsRoot en cada controlador
        if (humanController != null && humanVisuals != null && humanController.visualsRoot == null)
            humanController.visualsRoot = humanVisuals.transform;
        if (birdController != null && birdVisuals != null && birdController.visualsRoot == null)
            birdController.visualsRoot = birdVisuals.transform;
        if (crocodileController != null && crocodileVisuals != null && crocodileController.visualsRoot == null)
            crocodileController.visualsRoot = crocodileVisuals.transform;
        if (fishController != null && fishVisuals != null && fishController.visualsRoot == null)
            fishController.visualsRoot = fishVisuals.transform;

        // Si los colliders no están asignados, intentar obtener los de los controladores
        if (humanCollider == null && humanController != null) humanCollider = humanController.GetComponent<Collider2D>();
        if (birdCollider == null && birdController != null) birdCollider = birdController.GetComponent<Collider2D>();
        if (crocodileCollider == null && crocodileController != null) crocodileCollider = crocodileController.GetComponent<Collider2D>();
        if (fishCollider == null && fishController != null) fishCollider = fishController.GetComponent<Collider2D>();
    }

    #endregion

    #region Transformation Logic

    /// <summary>
    /// Intenta cambiar a la forma deseada tras verificar espacio libre y condiciones del entorno.
    /// Si la nueva forma colisionaría con el suelo (ej. Pez a Humano), desplaza al jugador hacia arriba automáticamente para que quede parado.
    /// Si se transforma a Ave, eleva al jugador al aire para iniciar vuelo inmediatamente.
    /// Retorna true si la transformación fue exitosa.
    /// </summary>
    public bool TryChangeForm(AnimalForm newForm)
    {
        if (newForm == currentForm) return true;

        // No permitir transformación si está pausado, en menú o reapareciendo
        if (IsPhysicsPaused || 
            (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) ||
            (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMenuOpen) ||
            (LevelRespawnManager.Instance != null && LevelRespawnManager.Instance.IsRespawning))
        {
            return false;
        }

        // Restricción: El Ave no puede transformarse dentro del agua
        if (newForm == AnimalForm.Bird && IsInWater)
        {
            if (debugLogs) Debug.LogWarning("[PlayerTransformationManager] No puedes transformarte en Ave bajo el agua.");
            return false;
        }

        Vector2 positionOffset = Vector2.zero;

        if (newForm == AnimalForm.Bird)
        {
            // Transformación a Ave: Elevar al jugador hacia el aire (verificando si hay techo)
            positionOffset = GetBirdAirLaunchOffset();
        }
        else if (enableClearanceCheck)
        {
            // Clearance Check con ajuste vertical inteligente (ej. Pez en suelo a Humano)
            if (!TryGetClearanceOffset(newForm, out positionOffset))
            {
                if (debugLogs) Debug.LogWarning($"[PlayerTransformationManager] Sin espacio libre suficiente para transformar a {newForm}.");
                return false;
            }
        }

        // Aplicar el ajuste de posición antes del cambio de forma
        if (positionOffset.sqrMagnitude > 0.0001f)
        {
            transform.position += (Vector3)positionOffset;
        }

        ExecuteFormChange(newForm);
        return true;
    }

    /// <summary>
    /// Fuerza el regreso inmediato a la forma humana (usado por el Ave al tocar agua o el Pez al asfixiarse).
    /// </summary>
    public void ForceRevertToHuman()
    {
        if (currentForm != AnimalForm.Human)
        {
            if (debugLogs) Debug.Log("[PlayerTransformationManager] Reversión forzada a Humano.");

            // Ajustar posición vertical para quedar de pie sobre el suelo sin atravesarlo
            if (TryGetClearanceOffset(AnimalForm.Human, out Vector2 offset))
            {
                if (offset.sqrMagnitude > 0.0001f)
                {
                    transform.position += (Vector3)offset;
                }
            }

            ExecuteFormChange(AnimalForm.Human);
        }
        else
        {
            // Asegurar que los visuales del humano estén encendidos
            SetFormActive(AnimalForm.Human, true);
        }
    }

    /// <summary>
    /// Restaura por completo el estado del jugador tras reaparecer (Respawn).
    /// Garantiza la reactivación limpia de los visuales de la forma activa (Humano por defecto).
    /// </summary>
    public void ResetOnRespawn()
    {
        currentForm = AnimalForm.Human;
        InitializeForm(AnimalForm.Human);

        if (RB != null)
        {
            RB.linearVelocity = Vector2.zero;
            RB.angularVelocity = 0f;
        }
    }

    private void ExecuteFormChange(AnimalForm newForm)
    {
        IsTransforming = true;
        try
        {
            AnimalForm previousForm = currentForm;
            Vector2 preservedVelocity = (RB != null ? RB.linearVelocity : Vector2.zero) * momentumConservation;

            AnimalForm oldForm = currentForm;
            currentForm = newForm;

            // 1. Desactivar forma anterior si era diferente
            if (oldForm != newForm)
            {
                SetFormActive(oldForm, false);
            }

            // 2. Activar nueva forma
            SetFormActive(newForm, true);

            // 3. Pasar inercia y estado de agua a la nueva forma
            ApplyStateToActiveForm(preservedVelocity);

            // 4. Disparar efecto visual de transformación (VFX Animator)
            PlayTransformationVFX();

            if (debugLogs) Debug.Log($"[PlayerTransformationManager] Transformado exitosamente de {previousForm} a {newForm}.");
            OnFormChanged?.Invoke(previousForm, newForm);
        }
        finally
        {
            IsTransforming = false;
        }
    }

    private void PlayTransformationVFX()
    {
        if (transformVFXAnimator != null)
        {
            if (syncVFXPosition)
            {
                transformVFXAnimator.transform.position = transform.position;
            }
            if (!string.IsNullOrEmpty(transformVFXTrigger))
            {
                transformVFXAnimator.ResetTrigger(transformVFXTrigger);
                transformVFXAnimator.SetTrigger(transformVFXTrigger);
            }
        }
    }

    /// <summary>
    /// Dispara el trigger de animación de muerte en el transformVFXAnimator,
    /// desactivando el visual del modo actual y dejando únicamente la animación de muerte activa.
    /// </summary>
    public void PlayDeathVFX()
    {
        // 1. Ocultar los visuales de todas las formas
        if (humanVisuals != null) humanVisuals.SetActive(false);
        if (birdVisuals != null) birdVisuals.SetActive(false);
        if (crocodileVisuals != null) crocodileVisuals.SetActive(false);
        if (fishVisuals != null) fishVisuals.SetActive(false);

        // 2. Activar y disparar el Animator de VFX de muerte (sin pausar su velocidad)
        if (transformVFXAnimator != null)
        {
            transformVFXAnimator.gameObject.SetActive(true);
            transformVFXAnimator.enabled = true;
            transformVFXAnimator.speed = 1f;

            if (syncVFXPosition)
            {
                transformVFXAnimator.transform.position = transform.position;
            }
            if (!string.IsNullOrEmpty(deathVFXTrigger))
            {
                transformVFXAnimator.ResetTrigger(deathVFXTrigger);
                transformVFXAnimator.SetTrigger(deathVFXTrigger);
            }
        }
    }

    private readonly System.Collections.Generic.List<Animator> _pausedAnimators = new System.Collections.Generic.List<Animator>();
    private readonly System.Collections.Generic.List<float> _savedAnimSpeeds = new System.Collections.Generic.List<float>();

    /// <summary>
    /// Congela/descongela las físicas, inputs y animaciones del jugador para transiciones cinemáticas (estilo Celeste).
    /// </summary>
    public void SetPhysicsPaused(bool pause)
    {
        if (IsPhysicsPaused == pause) return;
        IsPhysicsPaused = pause;

        if (RB == null) RB = GetComponent<Rigidbody2D>();
        if (RB == null) return;

        if (pause)
        {
            _savedVelocity = RB.linearVelocity;
            _savedBodyType = RB.bodyType;
            _savedGravityScale = RB.gravityScale;

            RB.linearVelocity = Vector2.zero;
            RB.bodyType = RigidbodyType2D.Kinematic;

            // Desactivar temporalmente el controlador activo para que no procese inputs durante el paneo
            SetCurrentControllerEnabled(false);

            // Pausar animaciones en su frame actual, EXCEPTO el Animator de muerte/VFX
            _pausedAnimators.Clear();
            _savedAnimSpeeds.Clear();
            var animators = GetComponentsInChildren<Animator>(false);
            foreach (var anim in animators)
            {
                if (anim != null && anim.enabled && anim.gameObject.activeInHierarchy)
                {
                    if (anim == transformVFXAnimator) continue;

                    _pausedAnimators.Add(anim);
                    _savedAnimSpeeds.Add(anim.speed);
                    anim.speed = 0f;
                }
            }
        }
        else
        {
            RB.bodyType = _savedBodyType;
            RB.gravityScale = _savedGravityScale;
            RB.linearVelocity = _savedVelocity;

            // Reactivar el controlador y visuales de la forma activa
            SetFormActive(currentForm, true);

            // Reanudar animaciones
            for (int i = 0; i < _pausedAnimators.Count; i++)
            {
                if (_pausedAnimators[i] != null)
                {
                    _pausedAnimators[i].speed = _savedAnimSpeeds[i];
                }
            }
            _pausedAnimators.Clear();
            _savedAnimSpeeds.Clear();
        }
    }

    private void SetCurrentControllerEnabled(bool enabled)
    {
        switch (currentForm)
        {
            case AnimalForm.Human:
                if (humanController != null) humanController.enabled = enabled;
                break;
            case AnimalForm.Bird:
                if (birdController != null) birdController.enabled = enabled;
                break;
            case AnimalForm.Crocodile:
                if (crocodileController != null) crocodileController.enabled = enabled;
                break;
            case AnimalForm.Fish:
                if (fishController != null) fishController.enabled = enabled;
                break;
        }
    }

    private void InitializeForm(AnimalForm startingForm)
    {
        // Desactivar todos los controladores y visuales
        SetFormActive(AnimalForm.Human, false);
        SetFormActive(AnimalForm.Bird, false);
        SetFormActive(AnimalForm.Crocodile, false);
        SetFormActive(AnimalForm.Fish, false);

        // Activar la forma inicial
        currentForm = startingForm;
        SetFormActive(startingForm, true);
        ApplyStateToActiveForm(Vector2.zero);
    }

    private void SetFormActive(AnimalForm form, bool active)
    {
        switch (form)
        {
            case AnimalForm.Human:
                if (humanController != null) humanController.enabled = active;
                if (humanVisuals != null) humanVisuals.SetActive(active);
                if (humanCollider != null) humanCollider.enabled = active;
                break;

            case AnimalForm.Bird:
                if (birdController != null) birdController.enabled = active;
                if (birdVisuals != null) birdVisuals.SetActive(active);
                if (birdCollider != null) birdCollider.enabled = active;
                break;

            case AnimalForm.Crocodile:
                if (crocodileController != null) crocodileController.enabled = active;
                if (crocodileVisuals != null) crocodileVisuals.SetActive(active);
                if (crocodileCollider != null) crocodileCollider.enabled = active;
                break;

            case AnimalForm.Fish:
                if (fishController != null) fishController.enabled = active;
                if (fishVisuals != null) fishVisuals.SetActive(active);
                if (fishCollider != null) fishCollider.enabled = active;
                break;
        }
    }

    private void ApplyStateToActiveForm(Vector2 initialVelocity)
    {
        switch (currentForm)
        {
            case AnimalForm.Human:
                if (humanController != null)
                {
                    humanController.SetInWater(IsInWater, CurrentWaterZone);
                    humanController.SetInitialVelocity(initialVelocity);
                }
                break;

            case AnimalForm.Bird:
                if (birdController != null)
                {
                    birdController.SetInWater(IsInWater, CurrentWaterZone);
                    birdController.SetInitialVelocity(initialVelocity);
                }
                break;

            case AnimalForm.Crocodile:
                if (crocodileController != null)
                {
                    crocodileController.SetInWater(IsInWater, CurrentWaterZone);
                    crocodileController.SetInitialVelocity(initialVelocity);
                }
                break;

            case AnimalForm.Fish:
                if (fishController != null)
                {
                    fishController.SetInWater(IsInWater, CurrentWaterZone);
                    fishController.SetInitialVelocity(initialVelocity);
                }
                break;
        }
    }

    #endregion

    #region Clearance & Offset Calculations

    private Vector2 GetBirdAirLaunchOffset()
    {
        float desiredLift = 0.5f;
        if (obstacleLayer != 0)
        {
            // Verificar si hay techo encima
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.up, desiredLift + 0.3f, obstacleLayer);
            if (hit.collider != null && !hit.transform.IsChildOf(transform) && hit.transform != transform)
            {
                desiredLift = Mathf.Max(0.1f, hit.distance - 0.25f);
            }
        }
        return new Vector2(0f, desiredLift);
    }

    private bool TryGetClearanceOffset(AnimalForm targetForm, out Vector2 requiredOffset)
    {
        requiredOffset = Vector2.zero;
        Collider2D targetCol = GetColliderForForm(targetForm);
        if (targetCol == null || obstacleLayer == 0) return true;

        Vector2 basePos = transform.position;
        Vector2 checkSize = new Vector2(targetCol.bounds.size.x * 0.88f, targetCol.bounds.size.y * 0.88f);

        // 1. Probar en la posición actual
        Vector2 checkCenter0 = basePos + targetCol.offset;
        Collider2D hit0 = Physics2D.OverlapBox(checkCenter0, checkSize, 0f, obstacleLayer);
        if (hit0 == null || hit0.transform.IsChildOf(transform) || hit0.transform == transform)
        {
            requiredOffset = Vector2.zero;
            return true;
        }

        // 2. Si colisiona con el suelo abajo, calcular el offset necesario para apoyar los pies en el piso
        Collider2D currentCol = GetColliderForForm(currentForm);
        float currentBottomY = currentCol != null ? (currentCol.offset.y - currentCol.bounds.extents.y) : 0f;
        float targetBottomY = targetCol.offset.y - targetCol.bounds.extents.y;
        float bottomAlignmentOffset = currentBottomY - targetBottomY;

        // Probar varios desplazamientos hacia arriba (desde la alineación de base hasta +1.2 unidades)
        float[] candidateYOffsets = new float[]
        {
            Mathf.Max(0.1f, bottomAlignmentOffset),
            Mathf.Max(0.1f, bottomAlignmentOffset) + 0.15f,
            0.3f, 0.5f, 0.7f, 0.9f, 1.1f
        };

        for (int i = 0; i < candidateYOffsets.Length; i++)
        {
            float yOff = candidateYOffsets[i];
            Vector2 checkCenter = basePos + targetCol.offset + Vector2.up * yOff;
            Collider2D hit = Physics2D.OverlapBox(checkCenter, checkSize, 0f, obstacleLayer);
            if (hit == null || hit.transform.IsChildOf(transform) || hit.transform == transform)
            {
                requiredOffset = Vector2.up * yOff;
                return true;
            }
        }

        return false;
    }

    private bool HasClearanceForForm(AnimalForm form)
    {
        return TryGetClearanceOffset(form, out _);
    }

    private Collider2D GetColliderForForm(AnimalForm form)
    {
        switch (form)
        {
            case AnimalForm.Human: return humanCollider;
            case AnimalForm.Bird: return birdCollider;
            case AnimalForm.Crocodile: return crocodileCollider;
            case AnimalForm.Fish: return fishCollider;
            default: return null;
        }
    }

    #endregion

    #region Water Handling

    public void OnEnterWater(WaterZone zone)
    {
        IsInWater = true;
        CurrentWaterZone = zone;

        if (currentForm == AnimalForm.Bird)
        {
            // El ave no puede estar en agua -> reversión forzada
            ForceRevertToHuman();
            return;
        }

        // Notificar a la forma activa
        ApplyStateToActiveForm(RB != null ? RB.linearVelocity : Vector2.zero);
    }

    public void OnExitWater(WaterZone zone)
    {
        if (CurrentWaterZone == zone)
        {
            IsInWater = false;
            CurrentWaterZone = null;
            ApplyStateToActiveForm(RB != null ? RB.linearVelocity : Vector2.zero);
        }
    }

    private void CheckInitialWaterOverlap()
    {
        Collider2D activeCol = GetColliderForForm(currentForm);
        if (activeCol == null) activeCol = GetComponent<Collider2D>();
        if (activeCol == null) return;

        Collider2D[] results = new Collider2D[8];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        int count = activeCol.Overlap(filter, results);
        for (int i = 0; i < count; i++)
        {
            if (results[i] != null)
            {
                var zone = results[i].GetComponent<WaterZone>();
                if (zone != null)
                {
                    OnEnterWater(zone);
                    break;
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var zone = other.GetComponent<WaterZone>();
        if (zone != null)
        {
            OnEnterWater(zone);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var zone = other.GetComponent<WaterZone>();
        if (zone != null)
        {
            OnExitWater(zone);
        }
    }

    #endregion
}
