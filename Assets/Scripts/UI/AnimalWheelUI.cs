using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;

/// <summary>
/// Rueda de selección radial de animales con 4 sprites independientes, animaciones PrimeTween (juice),
/// shader de outline pixel-art, pulsación arcade para el icono de Shift, soporte de cancelación al centro
/// y calibración de apuntado/ángulos.
/// </summary>
public class AnimalWheelUI : MonoBehaviour
{
    [Serializable]
    public class WheelSlice
    {
        [Tooltip("Forma correspondiente a este pedazo.")]
        public AnimalForm form;

        [Tooltip("Nombre a mostrar en la UI cuando se apunta a este pedazo.")]
        public string displayName = "MODO";

        [Tooltip("RectTransform del pedazo (para escalar con PrimeTween).")]
        public RectTransform sliceTransform;

        [Tooltip("Image del pedazo (para color y material con outline).")]
        public Image sliceImage;

        [Tooltip("Si es true, usa targetAngleDegrees en lugar de la posición o dirección vectorial.")]
        public bool useTargetAngle = false;

        [Tooltip("Ángulo central en grados de este pedazo (0° = Derecha, 90° = Arriba, 180° = Izquierda, 270° = Abajo. Diagonales: 45°, 135°, 225°, 315°).")]
        public float targetAngleDegrees = 0f;

        [Tooltip("Dirección personalizada opcional (si no se usa ángulo ni auto-detección por posición).")]
        public Vector2 customAimDirection = Vector2.zero;

        [NonSerialized] public Material instanceMaterial;
        [NonSerialized] public Tween scaleTween;
        [NonSerialized] public Tween colorTween;
    }

    #region Inspector Fields

    [Header("--- Target Transformation Manager ---")]
    [Tooltip("Referencia al PlayerTransformationManager del jugador.")]
    public PlayerTransformationManager transformationManager;

    [Header("--- Wheel Root & Scaling (PrimeTween) ---")]
    [Tooltip("RectTransform raíz de la rueda para escalar abierta/cerrada.")]
    public RectTransform wheelRootTransform;

    [Tooltip("Escala reducida por defecto cuando la rueda está cerrada.")]
    public Vector3 closedScale = new Vector3(0.55f, 0.55f, 1f);

    [Tooltip("Escala grande cuando la rueda está abierta.")]
    public Vector3 openScale = new Vector3(1f, 1f, 1f);

    [Tooltip("Duración de apertura de la rueda.")]
    public float openTweenDuration = 0.22f;
    public Ease openEase = Ease.OutBack;

    [Tooltip("Duración de cierre de la rueda.")]
    public float closeTweenDuration = 0.18f;
    public Ease closeEase = Ease.OutQuad;

    [Header("--- Shift Prompt (Arcade Pulse) ---")]
    [Tooltip("RectTransform o GameObject del icono/prompt de Shift separado.")]
    public RectTransform shiftPromptTransform;

    [Tooltip("Escala normal del icono de Shift.")]
    public Vector3 shiftNormalScale = Vector3.one;

    [Tooltip("Escala agrandada del icono de Shift durante el pulso arcade.")]
    public Vector3 shiftPulseScale = new Vector3(1.25f, 1.25f, 1f);

    [Tooltip("Intervalo en segundos para alternar tamaño (estilo arcade discreto).")]
    public float shiftPulseInterval = 0.35f;

    [Tooltip("Si es true, oculta el icono de Shift mientras la rueda está abierta.")]
    public bool hideShiftWhileOpen = true;

    [Header("--- Custom Mode Display Names ---")]
    [Tooltip("Nombre mostrado en UI para el Humano.")]
    public string humanDisplayName = "HUMANO";

    [Tooltip("Nombre mostrado en UI para el Ave (Garza Morena).")]
    public string birdDisplayName = "GARZA MORENA";

    [Tooltip("Nombre mostrado en UI para el Cocodrilo (Babilla).")]
    public string crocodileDisplayName = "BABILLA";

    [Tooltip("Nombre mostrado en UI para el Pez (Bocachico).")]
    public string fishDisplayName = "BOCACHICO";

    [Header("--- 4 Wheel Slices (Human, Bird, Crocodile, Fish) ---")]
    [Tooltip("Configuración de los 4 pedazos de la rueda.")]
    public WheelSlice[] slices = new WheelSlice[4]
    {
        new WheelSlice { form = AnimalForm.Human, displayName = "HUMANO", targetAngleDegrees = 180f },
        new WheelSlice { form = AnimalForm.Bird, displayName = "GARZA MORENA", targetAngleDegrees = 90f },
        new WheelSlice { form = AnimalForm.Crocodile, displayName = "BABILLA", targetAngleDegrees = 0f },
        new WheelSlice { form = AnimalForm.Fish, displayName = "BOCACHICO", targetAngleDegrees = 270f }
    };

    [Header("--- Slices Visual Juice ---")]
    [Tooltip("Color del pedazo seleccionado o activo (Color original de la imagen).")]
    public Color activeSliceColor = Color.white;

    [Tooltip("Color grisáceo / atenuado para los pedazos inactivos.")]
    public Color dimmedSliceColor = new Color(0.38f, 0.38f, 0.38f, 0.95f);

    [Tooltip("Escala aumentada del pedazo cuando se le apunta con la rueda abierta.")]
    public float sliceHoverScale = 1.28f;

    [Tooltip("Duración de la animación de escala del pedazo (PrimeTween).")]
    public float sliceScaleDuration = 0.15f;

    [Tooltip("Duración de la transición de color del pedazo.")]
    public float sliceColorDuration = 0.12f;

    [Header("--- Outline Shader (Pixel Art) ---")]
    [Tooltip("Shader de outline para pixel art (Custom/UI/PixelArtOutline).")]
    public Shader pixelOutlineShader;

    [Tooltip("Color de la línea de contorno.")]
    public Color outlineColor = Color.white;

    [Tooltip("Grosor de la línea en píxeles.")]
    [Range(1f, 4f)] public float outlineWidth = 1.5f;

    [Header("--- Aiming & Angle Offset Tuning ---")]
    [Tooltip("Compensación de ángulo global en grados para calibrar la detección si los sprites están rotados o desplazados (ej. -45°, 45°, -30°, etc.).")]
    [Range(-180f, 180f)] public float globalAngleOffset = 0f;

    [Tooltip("Si es true, calcula la dirección de cada porción automáticamente desde la posición de su RectTransform en el Canvas.")]
    public bool autoDetectFromSlicePosition = true;

    [Tooltip("Desplazamiento del centro del cursor (offset en X/Y en píxeles si el cursor o rueda no están centrados).")]
    public Vector2 cursorCenterOffset = Vector2.zero;

    [Tooltip("Sensibilidad de movimiento del cursor en X y Y.")]
    public Vector2 cursorSensitivity = new Vector2(24f, 24f);

    [Header("--- Center Deadzone & Cancel State ---")]
    [Tooltip("Radio interior central. Si el cursor está dentro de este radio, se cancela la selección.")]
    public float centerDeadzoneRadius = 45f;

    [Tooltip("Radio exterior máximo que puede alcanzar el cursor virtual.")]
    public float outerRadius = 160f;

    [Tooltip("Texto mostrado cuando el cursor está en el centro.")]
    public string cancelText = "CANCELAR";

    [Header("--- Text & Cursor UI ---")]
    [Tooltip("Texto TextMeshPro para mostrar el nombre seleccionado o 'CANCELAR'.")]
    public TMP_Text selectedNameTMPText;

    [Tooltip("Texto UI estándar opcional.")]
    public Text selectedNameText;

    [Tooltip("Si es true, borra el texto cuando la rueda está cerrada.")]
    public bool hideTextWhenClosed = true;

    [Tooltip("GameObject / RectTransform del cursor virtual en la UI.")]
    public RectTransform cursorIndicator;

    [Header("--- Input Configuration ---")]
    [Tooltip("Tecla para abrir y mantener la rueda.")]
    public KeyCode wheelKey = KeyCode.LeftShift;

    [Tooltip("Tecla secundaria opcional para abrir la rueda.")]
    public KeyCode alternateWheelKey = KeyCode.RightShift;

    [Header("--- Slow Motion Tuning ---")]
    [Tooltip("Escala de tiempo mientras la rueda está activa (ej. 0.1 = 10% de velocidad).")]
    [Range(0.01f, 1f)] public float slowMoTimeScale = 0.1f;

    [Tooltip("Velocidad de transición suave al ralentizar/restaurar el tiempo.")]
    public float timeScaleTransitionSpeed = 15f;

    [Header("--- Slow Motion Post Processing ---")]
    [Tooltip("Controlador de postprocesado dinámico (desaturación, viñeta, aberración cromática).")]
    public SlowMotionPostProcessManager slowMoPostProcessing;
    [Tooltip("Si es true, auto-activa los efectos de postprocesado al abrir la rueda.")]
    public bool enableSlowMoPostProcessing = true;

    [Header("--- Optional Canvas Root ---")]
    [Tooltip("GameObject Canvas raíz de la UI (si se asigna, se mantiene activo para ver la rueda cerrada).")]
    public GameObject wheelCanvasRoot;

    #endregion

    #region Public Properties & State

    public static AnimalWheelUI Instance { get; private set; }

    public bool IsWheelOpen { get; private set; }
    public AnimalForm? HoveredForm { get; private set; } // Null si está en el centro ("CANCELAR")
    public Vector2 VirtualCursorPosition { get; private set; } // En píxeles relativos al centro

    #endregion

    #region Internal State

    private float _targetTimeScale = 1f;
    private float _fixedDeltaTimeDefault = 0.02f;
    private Tween _wheelRootScaleTween;

    // Shift Arcade Pulse
    private float _shiftPulseTimer = 0f;
    private bool _shiftPulseState = false;

    // Shader Property IDs
    private static readonly int PropOutlineEnabled = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int PropOutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _fixedDeltaTimeDefault = Time.fixedDeltaTime;

        if (transformationManager == null)
        {
            transformationManager = FindFirstObjectByType<PlayerTransformationManager>();
        }

        if (wheelRootTransform == null && wheelCanvasRoot != null)
        {
            wheelRootTransform = wheelCanvasRoot.GetComponent<RectTransform>();
        }
        if (wheelRootTransform == null)
        {
            wheelRootTransform = GetComponent<RectTransform>();
        }

        InitializeOutlineShaderAndMaterials();
        EnsureSliceDefaults();
        SetupInitialClosedState();

        if (slowMoPostProcessing == null)
        {
            slowMoPostProcessing = FindFirstObjectByType<SlowMotionPostProcessManager>();
            if (slowMoPostProcessing == null && enableSlowMoPostProcessing)
            {
                GameObject postGo = new GameObject("[SlowMotionPostProcessManager]");
                slowMoPostProcessing = postGo.AddComponent<SlowMotionPostProcessManager>();
            }
        }
    }

    private void OnEnable()
    {
        if (transformationManager != null)
        {
            transformationManager.OnFormChanged += HandleFormChanged;
        }
    }

    private void OnDisable()
    {
        if (transformationManager != null)
        {
            transformationManager.OnFormChanged -= HandleFormChanged;
        }

        if (IsWheelOpen)
        {
            CloseWheel(instant: true);
        }
    }

    private void Start()
    {
        LockCursorToCenter();
        UpdateClosedVisuals(instant: true);
    }

    private void Update()
    {
        HandleWheelInput();
        UpdateCursorPosition();
        DetermineHoveredOption();
        UpdateSmoothTimeScale();
        UpdateShiftArcadePulse();
        UpdateUIElements();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _fixedDeltaTimeDefault;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Limpiar instancias de materiales creadas
        if (slices != null)
        {
            foreach (var slice in slices)
            {
                if (slice != null && slice.instanceMaterial != null)
                {
                    Destroy(slice.instanceMaterial);
                }
            }
        }
    }

    #endregion

    #region Initialization & Material Setup

    private void InitializeOutlineShaderAndMaterials()
    {
        if (pixelOutlineShader == null)
        {
            pixelOutlineShader = Shader.Find("Custom/UI/PixelArtOutline");
            if (pixelOutlineShader == null)
            {
                pixelOutlineShader = Shader.Find("UI/Default");
            }
        }

        if (slices == null) return;

        foreach (var slice in slices)
        {
            if (slice == null || slice.sliceImage == null) continue;

            Material baseMat = slice.sliceImage.material != null && slice.sliceImage.material.shader == pixelOutlineShader
                ? slice.sliceImage.material
                : new Material(pixelOutlineShader);

            slice.instanceMaterial = new Material(baseMat);
            slice.instanceMaterial.SetColor(PropOutlineColor, outlineColor);
            slice.instanceMaterial.SetFloat(PropOutlineWidth, outlineWidth);
            slice.instanceMaterial.SetFloat(PropOutlineEnabled, 0f);

            slice.sliceImage.material = slice.instanceMaterial;
        }
    }

    private void EnsureSliceDefaults()
    {
        if (slices == null || slices.Length == 0)
        {
            slices = new WheelSlice[4]
            {
                new WheelSlice { form = AnimalForm.Human, displayName = humanDisplayName, targetAngleDegrees = 180f },
                new WheelSlice { form = AnimalForm.Bird, displayName = birdDisplayName, targetAngleDegrees = 90f },
                new WheelSlice { form = AnimalForm.Crocodile, displayName = crocodileDisplayName, targetAngleDegrees = 0f },
                new WheelSlice { form = AnimalForm.Fish, displayName = fishDisplayName, targetAngleDegrees = 270f }
            };
        }
    }

    private void SetupInitialClosedState()
    {
        IsWheelOpen = false;
        _targetTimeScale = 1f;

        if (wheelCanvasRoot != null)
        {
            wheelCanvasRoot.SetActive(true);
        }

        if (wheelRootTransform != null)
        {
            wheelRootTransform.localScale = closedScale;
        }

        if (shiftPromptTransform != null)
        {
            shiftPromptTransform.localScale = shiftNormalScale;
            shiftPromptTransform.gameObject.SetActive(true);
        }

        if (cursorIndicator != null)
        {
            cursorIndicator.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Input & State Transitions

    private void HandleWheelInput()
    {
        // Si el juego está pausado, en menú o reapareciendo, cerrar rueda forzadamente
        if ((PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) ||
            (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMenuOpen) ||
            (LevelRespawnManager.Instance != null && LevelRespawnManager.Instance.IsRespawning))
        {
            if (IsWheelOpen)
            {
                CloseWheel(instant: true);
            }
            return;
        }

        bool isHoldingKey = Input.GetKey(wheelKey) || Input.GetKey(alternateWheelKey);

        if (isHoldingKey && !IsWheelOpen)
        {
            OpenWheel();
        }
        else if (!isHoldingKey && IsWheelOpen)
        {
            ConfirmSelectionAndClose();
        }
    }

    private void OpenWheel()
    {
        IsWheelOpen = true;
        _targetTimeScale = slowMoTimeScale;

        // Animar escala de la rueda con PrimeTween (Juice)
        if (wheelRootTransform != null)
        {
            _wheelRootScaleTween.Stop();
            _wheelRootScaleTween = Tween.Scale(wheelRootTransform, openScale, openTweenDuration, openEase, useUnscaledTime: true);
        }

        // Manejo del indicador Shift
        if (shiftPromptTransform != null)
        {
            _shiftPulseTimer = 0f;
            _shiftPulseState = false;
            shiftPromptTransform.localScale = shiftNormalScale;
            if (hideShiftWhileOpen)
            {
                shiftPromptTransform.gameObject.SetActive(false);
            }
        }

        // Activar cursor virtual
        if (cursorIndicator != null)
        {
            cursorIndicator.gameObject.SetActive(true);
            cursorIndicator.anchoredPosition = Vector2.zero;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        VirtualCursorPosition = Vector2.zero;

        // Al abrir en el centro, el estado inicial es CANCELAR
        HoveredForm = null;
        UpdateOpenHoverVisuals(null, instant: true);

        if (enableSlowMoPostProcessing && slowMoPostProcessing != null)
        {
            slowMoPostProcessing.SetSlowMoActive(true);
        }
    }

    private void ConfirmSelectionAndClose()
    {
        if (HoveredForm.HasValue && transformationManager != null)
        {
            transformationManager.TryChangeForm(HoveredForm.Value);
        }

        CloseWheel(instant: false);
    }

    private void CloseWheel(bool instant)
    {
        IsWheelOpen = false;
        _targetTimeScale = 1f;
        HoveredForm = null;

        if (slowMoPostProcessing != null)
        {
            slowMoPostProcessing.SetSlowMoActive(false);
        }

        if (instant)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _fixedDeltaTimeDefault;
            if (wheelRootTransform != null)
            {
                _wheelRootScaleTween.Stop();
                wheelRootTransform.localScale = closedScale;
            }
        }
        else
        {
            if (wheelRootTransform != null)
            {
                _wheelRootScaleTween.Stop();
                _wheelRootScaleTween = Tween.Scale(wheelRootTransform, closedScale, closeTweenDuration, closeEase, useUnscaledTime: true);
            }
        }

        // Reactivar Shift Prompt
        if (shiftPromptTransform != null)
        {
            shiftPromptTransform.gameObject.SetActive(true);
            shiftPromptTransform.localScale = shiftNormalScale;
            _shiftPulseTimer = 0f;
            _shiftPulseState = false;
        }

        // Ocultar cursor virtual
        if (cursorIndicator != null)
        {
            cursorIndicator.gameObject.SetActive(false);
        }

        LockCursorToCenter();
        UpdateClosedVisuals(instant);
    }

    private void LockCursorToCenter()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        VirtualCursorPosition = Vector2.zero;
    }

    private void HandleFormChanged(AnimalForm oldForm, AnimalForm newForm)
    {
        if (!IsWheelOpen)
        {
            UpdateClosedVisuals(instant: false);
        }
    }

    #endregion

    #region Cursor & Aiming Math

    private void UpdateCursorPosition()
    {
        if (!IsWheelOpen) return;

        // Leer movimiento de ratón con delta no escalado y sensibilidad configurable
        float mouseX = Input.GetAxisRaw("Mouse X") * cursorSensitivity.x;
        float mouseY = Input.GetAxisRaw("Mouse Y") * cursorSensitivity.y;

        Vector2 newPos = VirtualCursorPosition + new Vector2(mouseX, mouseY);

        // Clampear dentro del radio exterior
        if (newPos.magnitude > outerRadius)
        {
            newPos = newPos.normalized * outerRadius;
        }

        VirtualCursorPosition = newPos;
    }

    private void DetermineHoveredOption()
    {
        if (!IsWheelOpen) return;

        Vector2 effectiveCursorPos = VirtualCursorPosition - cursorCenterOffset;
        float distance = effectiveCursorPos.magnitude;

        // 1. Centro / Deadzone -> CANCELAR
        if (distance < centerDeadzoneRadius)
        {
            if (HoveredForm != null)
            {
                HoveredForm = null;
                UpdateOpenHoverVisuals(null, instant: false);
            }
            return;
        }

        // 2. Determinar vector de cursor compensado por globalAngleOffset
        Vector2 cursorDir = effectiveCursorPos.normalized;
        if (Mathf.Abs(globalAngleOffset) > 0.001f)
        {
            float rad = -globalAngleOffset * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            cursorDir = new Vector2(cursorDir.x * cos - cursorDir.y * sin, cursorDir.x * sin + cursorDir.y * cos);
        }

        WheelSlice bestSlice = null;
        float bestDot = -999f;

        foreach (var slice in slices)
        {
            if (slice == null) continue;

            Vector2 sliceDir = GetSliceDirection(slice);
            float dot = Vector2.Dot(cursorDir, sliceDir);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestSlice = slice;
            }
        }

        AnimalForm? targetForm = bestSlice != null ? bestSlice.form : (AnimalForm?)null;

        if (HoveredForm != targetForm)
        {
            HoveredForm = targetForm;
            UpdateOpenHoverVisuals(bestSlice, instant: false);
        }
    }

    public Vector2 GetSliceDirection(WheelSlice slice)
    {
        if (slice == null) return Vector2.up;

        // 1. Si tiene useTargetAngle activado o ángulo explícito
        if (slice.useTargetAngle)
        {
            float rad = slice.targetAngleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }

        // 2. Si autoDetectFromSlicePosition está activo y sliceTransform está posicionado fuera del centro
        if (autoDetectFromSlicePosition && slice.sliceTransform != null && slice.sliceTransform.anchoredPosition.sqrMagnitude > 1f)
        {
            return slice.sliceTransform.anchoredPosition.normalized;
        }

        // 3. Si tiene customAimDirection asignado manualmente
        if (slice.customAimDirection.sqrMagnitude > 0.001f)
        {
            return slice.customAimDirection.normalized;
        }

        // 4. Si tiene targetAngleDegrees != 0
        if (Mathf.Abs(slice.targetAngleDegrees) > 0.001f)
        {
            float rad = slice.targetAngleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }

        // 5. Fallback por defecto según AnimalForm
        switch (slice.form)
        {
            case AnimalForm.Bird: return Vector2.up;          // 90°
            case AnimalForm.Crocodile: return Vector2.right;  // 0°
            case AnimalForm.Fish: return Vector2.down;        // 270° (-90°)
            case AnimalForm.Human:
            default:
                return Vector2.left;                          // 180°
        }
    }

    #endregion

    #region Visual Juicing (PrimeTween & Outlines)

    private void UpdateOpenHoverVisuals(WheelSlice hoveredSlice, bool instant)
    {
        if (slices == null) return;

        foreach (var slice in slices)
        {
            if (slice == null) continue;

            bool isSelected = hoveredSlice != null && slice == hoveredSlice;

            Vector3 targetScale = isSelected ? Vector3.one * sliceHoverScale : Vector3.one;
            Color targetColor = isSelected ? activeSliceColor : dimmedSliceColor;
            float outlineEnabled = isSelected ? 1f : 0f;

            // Escala del pedazo con PrimeTween
            if (slice.sliceTransform != null)
            {
                slice.scaleTween.Stop();
                if (instant)
                {
                    slice.sliceTransform.localScale = targetScale;
                }
                else
                {
                    Ease ease = isSelected ? Ease.OutBack : Ease.OutQuad;
                    slice.scaleTween = Tween.Scale(slice.sliceTransform, targetScale, sliceScaleDuration, ease, useUnscaledTime: true);
                }
            }

            // Color del pedazo con PrimeTween
            if (slice.sliceImage != null)
            {
                slice.colorTween.Stop();
                if (instant)
                {
                    slice.sliceImage.color = targetColor;
                }
                else
                {
                    slice.colorTween = Tween.Color(slice.sliceImage, targetColor, sliceColorDuration, useUnscaledTime: true);
                }
            }

            // Outline en el material
            if (slice.instanceMaterial != null)
            {
                slice.instanceMaterial.SetFloat(PropOutlineEnabled, outlineEnabled);
                slice.instanceMaterial.SetColor(PropOutlineColor, outlineColor);
                slice.instanceMaterial.SetFloat(PropOutlineWidth, outlineWidth);
            }
        }
    }

    private void UpdateClosedVisuals(bool instant)
    {
        if (slices == null) return;

        AnimalForm currentActive = transformationManager != null ? transformationManager.currentForm : AnimalForm.Human;

        foreach (var slice in slices)
        {
            if (slice == null) continue;

            bool isActiveForm = slice.form == currentActive;
            Color targetColor = isActiveForm ? activeSliceColor : dimmedSliceColor;

            // Escala normal
            if (slice.sliceTransform != null)
            {
                slice.scaleTween.Stop();
                if (instant)
                {
                    slice.sliceTransform.localScale = Vector3.one;
                }
                else
                {
                    slice.scaleTween = Tween.Scale(slice.sliceTransform, Vector3.one, sliceScaleDuration, Ease.OutQuad, useUnscaledTime: true);
                }
            }

            // Color
            if (slice.sliceImage != null)
            {
                slice.colorTween.Stop();
                if (instant)
                {
                    slice.sliceImage.color = targetColor;
                }
                else
                {
                    slice.colorTween = Tween.Color(slice.sliceImage, targetColor, sliceColorDuration, useUnscaledTime: true);
                }
            }

            // Outline apagado en modo cerrado
            if (slice.instanceMaterial != null)
            {
                slice.instanceMaterial.SetFloat(PropOutlineEnabled, 0f);
            }
        }
    }

    #endregion

    #region Shift Arcade Pulse & Smooth TimeScale

    private void UpdateShiftArcadePulse()
    {
        if (IsWheelOpen || shiftPromptTransform == null) return;

        _shiftPulseTimer += Time.unscaledDeltaTime;
        if (_shiftPulseTimer >= shiftPulseInterval)
        {
            _shiftPulseTimer -= shiftPulseInterval;
            _shiftPulseState = !_shiftPulseState;
            shiftPromptTransform.localScale = _shiftPulseState ? shiftPulseScale : shiftNormalScale;
        }
    }

    private void UpdateSmoothTimeScale()
    {
        Time.timeScale = Mathf.MoveTowards(Time.timeScale, _targetTimeScale, timeScaleTransitionSpeed * Time.unscaledDeltaTime);
        Time.fixedDeltaTime = _fixedDeltaTimeDefault * Time.timeScale;
    }

    private void UpdateUIElements()
    {
        if (cursorIndicator != null && IsWheelOpen)
        {
            cursorIndicator.anchoredPosition = VirtualCursorPosition;
        }

        string textToDisplay = "";
        if (IsWheelOpen)
        {
            if (HoveredForm.HasValue)
            {
                textToDisplay = GetSliceDisplayName(HoveredForm.Value);
            }
            else
            {
                textToDisplay = cancelText;
            }
        }
        else if (!hideTextWhenClosed && transformationManager != null)
        {
            textToDisplay = GetSliceDisplayName(transformationManager.currentForm);
        }

        if (selectedNameTMPText != null)
        {
            selectedNameTMPText.text = textToDisplay;
        }

        if (selectedNameText != null)
        {
            selectedNameText.text = textToDisplay;
        }
    }

    private string GetSliceDisplayName(AnimalForm form)
    {
        switch (form)
        {
            case AnimalForm.Human:
                if (!string.IsNullOrEmpty(humanDisplayName)) return humanDisplayName;
                break;
            case AnimalForm.Bird:
                if (!string.IsNullOrEmpty(birdDisplayName)) return birdDisplayName;
                break;
            case AnimalForm.Crocodile:
                if (!string.IsNullOrEmpty(crocodileDisplayName)) return crocodileDisplayName;
                break;
            case AnimalForm.Fish:
                if (!string.IsNullOrEmpty(fishDisplayName)) return fishDisplayName;
                break;
        }

        if (slices != null)
        {
            foreach (var slice in slices)
            {
                if (slice != null && slice.form == form && !string.IsNullOrEmpty(slice.displayName))
                {
                    return slice.displayName;
                }
            }
        }

        switch (form)
        {
            case AnimalForm.Human: return "HUMANO";
            case AnimalForm.Bird: return "GARZA MORENA";
            case AnimalForm.Crocodile: return "BABILLA";
            case AnimalForm.Fish: return "BOCACHICO";
            default: return "";
        }
    }

    private void OnValidate()
    {
        if (slices != null)
        {
            foreach (var slice in slices)
            {
                if (slice == null) continue;
                switch (slice.form)
                {
                    case AnimalForm.Human:
                        if (!string.IsNullOrEmpty(humanDisplayName)) slice.displayName = humanDisplayName;
                        break;
                    case AnimalForm.Bird:
                        if (!string.IsNullOrEmpty(birdDisplayName)) slice.displayName = birdDisplayName;
                        break;
                    case AnimalForm.Crocodile:
                        if (!string.IsNullOrEmpty(crocodileDisplayName)) slice.displayName = crocodileDisplayName;
                        break;
                    case AnimalForm.Fish:
                        if (!string.IsNullOrEmpty(fishDisplayName)) slice.displayName = fishDisplayName;
                        break;
                }
            }
        }
    }

    #endregion

    #region Editor Debug Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector3 center = wheelRootTransform != null ? wheelRootTransform.position : transform.position;

        // Dibujar radio de Deadzone
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, centerDeadzoneRadius);

        // Dibujar radio exterior
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, outerRadius);

        // Dibujar direcciones de cada slice
        if (slices != null)
        {
            foreach (var slice in slices)
            {
                if (slice == null) continue;
                Vector2 dir = GetSliceDirection(slice);

                // Aplicar offset si existe
                if (Mathf.Abs(globalAngleOffset) > 0.001f)
                {
                    float rad = globalAngleOffset * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    dir = new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
                }

                Gizmos.color = slice.form == AnimalForm.Human ? Color.white :
                               slice.form == AnimalForm.Bird ? Color.green :
                               slice.form == AnimalForm.Crocodile ? Color.red : Color.blue;

                Gizmos.DrawRay(center, (Vector3)dir * outerRadius);
            }
        }
    }

    #endregion
}
