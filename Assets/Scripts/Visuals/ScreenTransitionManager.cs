using System;
using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

/// <summary>
/// Estilos de transición retro disponibles.
/// </summary>
public enum TransitionStyle
{
    DiamondWave = 0,
    CircleIris = 1
}

/// <summary>
/// Gestor global de transiciones de pantalla estilizadas (Game Jam Edition).
/// 
/// Características:
/// - 2 Estilos seleccionables: Diamond Grid Wave (expansión desde el origen) y Circle Spotlight Iris (foco circular).
/// - Funciona con PrimeTween en tiempo no escalado (useUnscaledTime: true) para permitir transiciones en pausa o respawn.
/// - Auto-generación o auto-detección de Canvas y Material si ya están en la escena o asignados en el Inspector.
/// - Soporte para punto de enfoque en coordenadas normalizadas (Viewport 0..1) o píxeles de pantalla.
/// </summary>
public class ScreenTransitionManager : MonoBehaviour
{
    public static ScreenTransitionManager Instance { get; private set; }

    #region Inspector Fields

    [Header("--- Transition Settings ---")]
    [Tooltip("Estilo de transición por defecto.")]
    public TransitionStyle defaultStyle = TransitionStyle.DiamondWave;

    [Tooltip("Color de la transición.")]
    public Color transitionColor = Color.black;

    [Tooltip("Tamaño de los rombos / bloques pixelados.")]
    public float pixelSize = 24f;

    [Header("--- UI References (Opcional - Se auto-detectan/generan si están vacíos) ---")]
    public Canvas transitionCanvas;
    public Image transitionImage;
    public Material transitionMaterial;

    [Header("--- Shader Reference ---")]
    public Shader transitionShader;

    #endregion

    #region Public Properties & State

    public bool IsTransitioning { get; private set; }
    public float Progress { get; private set; } = 0f;
    public TransitionStyle CurrentStyle { get; private set; }

    #endregion

    #region Internal State

    private Material _runtimeMaterial;
    private Tween _currentTween;
    private static readonly int _propProgress = Shader.PropertyToID("_Progress");
    private static readonly int _propMode = Shader.PropertyToID("_Mode");
    private static readonly int _propColor = Shader.PropertyToID("_Color");
    private static readonly int _propPixelSize = Shader.PropertyToID("_PixelSize");
    private static readonly int _propCenter = Shader.PropertyToID("_Center");
    private static readonly int _propAspectRatio = Shader.PropertyToID("_AspectRatio");

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
        DontDestroyOnLoad(gameObject);

        EnsureSetup();
        SetProgressInstant(0f);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Al cargar una nueva escena, si la pantalla quedó en negro/fade out, revelar suavemente el juego
        if (Progress > 0.02f)
        {
            FadeIn(0.45f, defaultStyle, null);
        }
    }

    private void OnDestroy()
    {
        if (_currentTween.isAlive) _currentTween.Stop();
        if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
    }

    #endregion

    #region Setup

    public void EnsureSetup()
    {
        CurrentStyle = defaultStyle;

        if (transitionShader == null)
        {
            transitionShader = Shader.Find("Custom/ScreenTransition");
        }

        // 1. Resolver Canvas
        if (transitionCanvas == null)
        {
            transitionCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (transitionCanvas == null)
        {
            var canvasObj = new GameObject("ScreenTransitionCanvas");
            canvasObj.transform.SetParent(transform);
            transitionCanvas = canvasObj.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 9999;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 9999;
        }

        // 2. Resolver Image
        if (transitionImage == null && transitionCanvas != null)
        {
            transitionImage = transitionCanvas.GetComponentInChildren<Image>(true);
        }

        if (transitionImage == null && transitionCanvas != null)
        {
            var imageObj = new GameObject("TransitionImage");
            imageObj.transform.SetParent(transitionCanvas.transform, false);

            transitionImage = imageObj.AddComponent<Image>();
            transitionImage.color = Color.white;

            var rect = transitionImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        // 3. Resolver Material y asignar a Image
        if (_runtimeMaterial == null)
        {
            if (transitionMaterial != null)
            {
                _runtimeMaterial = new Material(transitionMaterial);
            }
            else if (transitionImage != null && transitionImage.material != null && transitionImage.material.shader != null && transitionImage.material.shader.name == "Custom/ScreenTransition")
            {
                _runtimeMaterial = new Material(transitionImage.material);
            }
            else if (transitionShader != null)
            {
                _runtimeMaterial = new Material(transitionShader);
            }
        }

        if (transitionImage != null)
        {
            if (_runtimeMaterial != null)
            {
                transitionImage.material = _runtimeMaterial;
            }
            transitionImage.raycastTarget = false;
            transitionImage.gameObject.SetActive(true);
            transitionImage.enabled = true;
        }

        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(true);
            transitionCanvas.enabled = true;
        }

        UpdateMaterialProperties(CurrentStyle, new Vector2(0.5f, 0.5f));
    }

    private void UpdateMaterialProperties(TransitionStyle style, Vector2 focusUV)
    {
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.777778f;

        if (_runtimeMaterial != null)
        {
            _runtimeMaterial.SetFloat(_propMode, (float)style);
            _runtimeMaterial.SetColor(_propColor, transitionColor);
            _runtimeMaterial.SetFloat(_propPixelSize, pixelSize);
            _runtimeMaterial.SetVector(_propCenter, new Vector4(focusUV.x, focusUV.y, 0f, 0f));
            _runtimeMaterial.SetFloat(_propAspectRatio, aspect);
        }

        if (transitionMaterial != null)
        {
            transitionMaterial.SetFloat(_propMode, (float)style);
            transitionMaterial.SetColor(_propColor, transitionColor);
            transitionMaterial.SetFloat(_propPixelSize, pixelSize);
            transitionMaterial.SetVector(_propCenter, new Vector4(focusUV.x, focusUV.y, 0f, 0f));
            transitionMaterial.SetFloat(_propAspectRatio, aspect);
        }
    }

    #endregion

    #region Public Transition API

    /// <summary>
    /// Oscurece la pantalla hacia negro con el estilo seleccionado (Fade Out).
    /// </summary>
    public void FadeOut(float duration, TransitionStyle? style = null, Vector2? screenFocusPos = null, Action onComplete = null)
    {
        EnsureSetup();

        TransitionStyle targetStyle = style ?? defaultStyle;
        Vector2 focusUV = NormalizeFocusUV(screenFocusPos);

        CurrentStyle = targetStyle;
        UpdateMaterialProperties(targetStyle, focusUV);

        if (_currentTween.isAlive) _currentTween.Stop();

        IsTransitioning = true;
        SetProgressInstant(Mathf.Max(Progress, 0.001f));

        _currentTween = Tween.Custom(Progress, 1f, duration, onValueChange: val =>
        {
            SetProgressInstant(val);
        }, useUnscaledTime: true, ease: Ease.OutQuad)
        .OnComplete(() =>
        {
            IsTransitioning = false;
            SetProgressInstant(1f);
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Revela la pantalla desde negro hacia el gameplay (Fade In).
    /// </summary>
    public void FadeIn(float duration, TransitionStyle? style = null, Vector2? screenFocusPos = null, Action onComplete = null)
    {
        EnsureSetup();

        TransitionStyle targetStyle = style ?? defaultStyle;
        Vector2 focusUV = NormalizeFocusUV(screenFocusPos);

        CurrentStyle = targetStyle;
        UpdateMaterialProperties(targetStyle, focusUV);

        if (_currentTween.isAlive) _currentTween.Stop();

        IsTransitioning = true;
        SetProgressInstant(Mathf.Min(Progress, 1f));

        _currentTween = Tween.Custom(Progress, 0f, duration, onValueChange: val =>
        {
            SetProgressInstant(val);
        }, useUnscaledTime: true, ease: Ease.InQuad)
        .OnComplete(() =>
        {
            IsTransitioning = false;
            SetProgressInstant(0f);
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Ejecuta una transición completa: FadeOut -> Acción con pantalla oculta -> FadeIn.
    /// </summary>
    public void Transition(float fadeOutDuration, float fadeInDuration, Action onHidden, TransitionStyle? style = null, Vector2? screenFocusPos = null, Action onComplete = null)
    {
        FadeOut(fadeOutDuration, style, screenFocusPos, () =>
        {
            onHidden?.Invoke();
            FadeIn(fadeInDuration, style, screenFocusPos, onComplete);
        });
    }

    /// <summary>
    /// Establece inmediatamente el nivel de progreso (0 = transparente, 1 = negro completo).
    /// </summary>
    public void SetProgressInstant(float progress)
    {
        Progress = Mathf.Clamp01(progress);

        if (_runtimeMaterial != null)
        {
            _runtimeMaterial.SetFloat(_propProgress, Progress);
        }

        if (transitionMaterial != null)
        {
            transitionMaterial.SetFloat(_propProgress, Progress);
        }

        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(true);
            transitionCanvas.enabled = true;
        }

        if (transitionImage != null)
        {
            transitionImage.enabled = Progress > 0.0001f;
        }
    }

    public Vector2 NormalizeFocusUV(Vector2? focusPos)
    {
        if (!focusPos.HasValue) return new Vector2(0.5f, 0.5f);

        Vector2 pos = focusPos.Value;

        // Si ya está normalizado en rango 0..1 (Viewport coordinates)
        if (pos.x >= 0f && pos.x <= 1.001f && pos.y >= 0f && pos.y <= 1.001f)
        {
            return new Vector2(Mathf.Clamp01(pos.x), Mathf.Clamp01(pos.y));
        }

        // Si está en coordenadas de pantalla en píxeles (Screen space)
        float uvX = Screen.width > 0 ? Mathf.Clamp01(pos.x / Screen.width) : 0.5f;
        float uvY = Screen.height > 0 ? Mathf.Clamp01(pos.y / Screen.height) : 0.5f;
        return new Vector2(uvX, uvY);
    }

    #endregion
}
