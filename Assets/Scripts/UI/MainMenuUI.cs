using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using PrimeTween;

/// <summary>
/// Menú Principal (Game Jam Edition).
/// 
/// Características:
/// - Control exclusivo mediante ratón (cursor libre y visible).
/// - Botones: Jugar y Salir con soporte Canvas UI + animaciones.
/// - Pantalla de Prólogo e Introducción de Personajes en 2 Páginas:
///   * Página 1: Historia y lore (El Don de la Ciénaga).
///   * Página 2: Guía rápida de habilidades de cada criatura (Humano, Babilla, Garza y Bocachico).
/// - Soporte de máquina de escribir, salto con Espacio/Clic y transición fluida al juego.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance { get; private set; }

    #region Inspector Fields

    [Header("--- Scene Navigation ---")]
    [Tooltip("Nombre de la escena de gameplay si se usan escenas separadas.")]
    public string gameplaySceneName = "game";

    [Tooltip("Si es true, el menú principal inicia activo al cargar la escena.")]
    public bool startActive = true;

    [Header("--- Canvas UI Elements ---")]
    [Tooltip("GameObject raíz del panel del menú principal en Canvas.")]
    public GameObject mainMenuRoot;

    public Button playButton;
    public Button quitButton;

    [Header("--- Story & Character Intro (2 Páginas) ---")]
    [Tooltip("Si es true, muestra la pantalla de prólogo/historia antes de iniciar el juego.")]
    public bool showStoryIntro = true;

    [Tooltip("Panel de la historia en Canvas (se auto-crea automáticamente si está vacío).")]
    public GameObject storyPanel;

    [Tooltip("Fuente de TextMeshPro (TMP_FontAsset) que se usará para el texto (opcional).")]
    public TMP_FontAsset storyFontAsset;

    [Tooltip("Color de fondo del panel de historia / monólogo.")]
    public Color storyBackgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.96f);

    [Tooltip("Componente TextMeshProUGUI para el título.")]
    public TextMeshProUGUI storyTitleTMP;

    [Tooltip("Componente TextMeshProUGUI para el texto de la historia.")]
    public TextMeshProUGUI storyTextTMP;

    [Tooltip("Componente TextMeshProUGUI para el prompt de salto.")]
    public TextMeshProUGUI skipPromptTMP;

    [Header("--- Página 1: Historia & Lore ---")]
    public string page1Title = "EL DON DE LA CIÉNAGA";
    [TextArea(5, 10)]
    public string page1Text = "En un paseo por el humedal, a Jacinto se le apareció la Madremonte, quien le dio el \"Don de la Ciénaga\", con la misión de llegar a un árbol ancestral que ayudaría a restaurar el agua de la Ciénaga San Silvestre y volverla a purificar.\n\nCon el Don de la Ciénaga, Jacinto puede aprovechar la biodiversidad de su región y transformarse en criaturas del propio humedal.\n\nTú eres Jacinto, y tu misión ya fue entregada.";
    public float page1Duration = 12f;

    [Header("--- Página 2: Guía de Criaturas & Habilidades ---")]
    public string page2Title = "CRIATURAS DEL HUMEDAL";
    [TextArea(6, 12)]
    public string page2Text = "• JACINTO (Humano): Ágil en tierra firme, salta y trepa plataformas.\n\n• BABILLA (Cocodrilo): Embestida con [ESPACIO] para romper rocas y derribar troncos.\n\n• GARZA MORENA (Ave): Vuelo omnidireccional con [WASD] para cruzar abismos y alturas.\n\n• BOCACHICO (Pez): Nado veloz en el agua y gran salto acrobático con [ESPACIO] hacia tierra.";
    public float page2Duration = 12f;

    [Header("--- Text Animation Settings ---")]
    [Tooltip("Permite saltar / pasar a la siguiente página pulsando Espacio, Enter o Clic.")]
    public bool allowSkipStory = true;

    [Tooltip("Efecto máquina de escribir para el texto.")]
    public bool useTypewriterEffect = true;

    [Tooltip("Velocidad de escritura (caracteres por segundo).")]
    public float typewriterSpeed = 52f;

    [Header("--- Visual Style (OnGUI Fallback) ---")]
    public string gameTitle = "El Don de la Ciénaga";
    public string gameSubtitle = "OV GAMEJAM 2026";
    public Color backdropColor = new Color(0.06f, 0.08f, 0.12f, 1f);
    public Color titleColor = new Color(0.3f, 0.8f, 1f, 1f);
    public Color buttonColor = new Color(0.18f, 0.22f, 0.3f, 1f);
    public Color buttonHoverColor = new Color(0.2f, 0.7f, 0.4f, 1f);
    public Color quitHoverColor = new Color(0.9f, 0.3f, 0.3f, 1f);
    public Color textColor = Color.white;

    #endregion

    #region Public Properties & State

    public bool IsMenuOpen { get; private set; }
    public bool IsStoryShowing { get; private set; }

    #endregion

    #region Events

    public event Action OnGameStarted;

    #endregion

    #region Internal State

    private Texture2D _whiteTex;
    private PlayerTransformationManager _cachedPTM;
    private Coroutine _storyCoroutine;
    private bool _storySkipped = false;

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

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        CreateWhiteTexture();
        BindCanvasButtons();

        if (startActive)
        {
            ShowMainMenu();
        }
        else
        {
            HideMainMenu();
        }
    }

    private void Start()
    {
        _cachedPTM = PlayerTransformationManager.Instance ?? FindFirstObjectByType<PlayerTransformationManager>();

        if (startActive)
        {
            ShowMainMenu();
        }
    }

    private void Update()
    {
        // Detectar salto o avance de página con input
        if (IsStoryShowing && allowSkipStory)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                _storySkipped = true;
            }
        }
    }

    #endregion

    #region Menu API

    public void ShowMainMenu()
    {
        IsMenuOpen = true;
        IsStoryShowing = false;

        // Liberar y mostrar cursor para mouse-only
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(true);
        }

        if (storyPanel != null)
        {
            storyPanel.SetActive(false);
        }

        if (_cachedPTM == null) _cachedPTM = PlayerTransformationManager.Instance ?? FindFirstObjectByType<PlayerTransformationManager>();
        if (_cachedPTM != null)
        {
            _cachedPTM.SetPhysicsPaused(true);
        }
    }

    public void HideMainMenu()
    {
        IsMenuOpen = false;

        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(false);
        }
    }

    public void PlayGame()
    {
        if (showStoryIntro)
        {
            StartStoryIntro();
        }
        else
        {
            TransitionToGame();
        }
    }

    private void StartStoryIntro()
    {
        HideMainMenu();
        EnsureStoryUIExists();

        if (_storyCoroutine != null) StopCoroutine(_storyCoroutine);
        _storyCoroutine = StartCoroutine(StoryIntroSequence());
    }

    private IEnumerator StoryIntroSequence()
    {
        IsStoryShowing = true;

        if (storyPanel != null)
        {
            storyPanel.SetActive(true);
        }

        // ==================== PÁGINA 1: HISTORIA ====================
        yield return StartCoroutine(PlayStoryPage(page1Title, page1Text, page1Duration, 1, 2, "[ Pulsa ESPACIO o CLIC para continuar • {0}s • (1/2) ]"));

        // ==================== PÁGINA 2: PERSONAJES ====================
        yield return StartCoroutine(PlayStoryPage(page2Title, page2Text, page2Duration, 2, 2, "[ Pulsa ESPACIO o CLIC para comenzar • {0}s • (2/2) ]"));

        IsStoryShowing = false;
        TransitionToGame();
    }

    private IEnumerator PlayStoryPage(string title, string text, float duration, int pageNum, int totalPages, string promptFormat)
    {
        _storySkipped = false;

        if (storyTitleTMP != null)
        {
            storyTitleTMP.text = title;
        }

        // Mostrar texto con typewriter o instantáneo
        if (storyTextTMP != null)
        {
            if (useTypewriterEffect && typewriterSpeed > 0)
            {
                storyTextTMP.text = "";
                float charDelay = 1f / typewriterSpeed;

                for (int i = 0; i < text.Length; i++)
                {
                    if (_storySkipped)
                    {
                        storyTextTMP.text = text;
                        break;
                    }

                    storyTextTMP.text += text[i];
                    yield return new WaitForSecondsRealtime(charDelay);
                }
            }
            else
            {
                storyTextTMP.text = text;
            }
        }

        // Esperar tiempo restante o click/espacio para avanzar
        float elapsed = 0f;
        _storySkipped = false; // Reset para permitir pasar página tras escribir

        // Breve ventana de seguridad para evitar saltos accidentales de doble clic
        yield return new WaitForSecondsRealtime(0.15f);

        while (elapsed < duration && !_storySkipped)
        {
            elapsed += Time.unscaledDeltaTime;

            if (skipPromptTMP != null)
            {
                int remainingSeconds = Mathf.CeilToInt(duration - elapsed);
                skipPromptTMP.text = string.Format(promptFormat, remainingSeconds);
            }

            yield return null;
        }
    }

    private void TransitionToGame()
    {
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.FadeOut(0.4f, TransitionStyle.DiamondWave, null, () =>
            {
                ExecuteStartGame();
            });
        }
        else
        {
            ExecuteStartGame();
        }
    }

    private void ExecuteStartGame()
    {
        HideMainMenu();

        if (storyPanel != null)
        {
            storyPanel.SetActive(false);
        }

        // Si estamos en la misma escena de juego, iniciar gameplay directamente
        if (_cachedPTM != null)
        {
            _cachedPTM.SetPhysicsPaused(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            OnGameStarted?.Invoke();

            if (ScreenTransitionManager.Instance != null)
            {
                ScreenTransitionManager.Instance.FadeIn(0.4f);
            }
            return;
        }

        // Si se usa una escena separada
        if (!string.IsNullOrEmpty(gameplaySceneName) && SceneManager.GetActiveScene().name != gameplaySceneName)
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            OnGameStarted?.Invoke();

            if (ScreenTransitionManager.Instance != null)
            {
                ScreenTransitionManager.Instance.FadeIn(0.4f);
            }
        }
    }

    public void QuitGame()
    {
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.FadeOut(0.3f, null, null, () =>
            {
                DoQuit();
            });
        }
        else
        {
            DoQuit();
        }
    }

    private void DoQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Auto-Setup Story UI

    /// <summary>
    /// Genera o personaliza automáticamente el panel de historia con la fuente y colores elegidos.
    /// </summary>
    private void EnsureStoryUIExists()
    {
        if (storyPanel != null)
        {
            // Actualizar colores y fuentes si el panel ya existía
            var existingImg = storyPanel.GetComponent<Image>();
            if (existingImg != null) existingImg.color = storyBackgroundColor;

            if (storyFontAsset != null)
            {
                var tmps = storyPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in tmps)
                {
                    if (tmp != null) tmp.font = storyFontAsset;
                }
            }
            return;
        }

        // Buscar un Canvas en la escena
        Canvas targetCanvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            var canvasObj = new GameObject("StoryCanvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 500;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 1. Panel de Fondo
        var panelObj = new GameObject("StoryPanel");
        panelObj.transform.SetParent(targetCanvas.transform, false);

        var rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        var img = panelObj.AddComponent<Image>();
        img.color = storyBackgroundColor;

        // 2. Título de la Historia
        var titleObj = new GameObject("StoryTitle");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.85f);
        titleRect.anchorMax = new Vector2(0.5f, 0.85f);
        titleRect.sizeDelta = new Vector2(1000, 60);
        titleRect.anchoredPosition = Vector2.zero;

        storyTitleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        if (storyFontAsset != null) storyTitleTMP.font = storyFontAsset;
        storyTitleTMP.text = page1Title;
        storyTitleTMP.fontSize = 32;
        storyTitleTMP.fontStyle = FontStyles.Bold;
        storyTitleTMP.alignment = TextAlignmentOptions.Center;
        storyTitleTMP.color = new Color(1f, 0.85f, 0.35f, 1f); // Dorado brillante

        // 3. Texto del Prólogo
        var textObj = new GameObject("StoryText");
        textObj.transform.SetParent(panelObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.52f);
        textRect.anchorMax = new Vector2(0.5f, 0.52f);
        textRect.sizeDelta = new Vector2(1100, 440);
        textRect.anchoredPosition = Vector2.zero;

        storyTextTMP = textObj.AddComponent<TextMeshProUGUI>();
        if (storyFontAsset != null) storyTextTMP.font = storyFontAsset;
        storyTextTMP.fontSize = 23;
        storyTextTMP.lineSpacing = 24f;
        storyTextTMP.alignment = TextAlignmentOptions.Center;
        storyTextTMP.color = new Color(0.92f, 0.95f, 1f, 1f); // Blanco suave
        storyTextTMP.enableWordWrapping = true;

        // 4. Prompt para Continuar / Saltar
        var promptObj = new GameObject("SkipPrompt");
        promptObj.transform.SetParent(panelObj.transform, false);
        var promptRect = promptObj.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.10f);
        promptRect.anchorMax = new Vector2(0.5f, 0.10f);
        promptRect.sizeDelta = new Vector2(900, 40);
        promptRect.anchoredPosition = Vector2.zero;

        skipPromptTMP = promptObj.AddComponent<TextMeshProUGUI>();
        if (storyFontAsset != null) skipPromptTMP.font = storyFontAsset;
        skipPromptTMP.fontSize = 18;
        skipPromptTMP.alignment = TextAlignmentOptions.Center;
        skipPromptTMP.color = new Color(0.55f, 0.65f, 0.78f, 0.85f);
        skipPromptTMP.text = "[ Pulsa ESPACIO o CLIC para continuar • (1/2) ]";

        storyPanel = panelObj;
    }

    #endregion

    #region Canvas Binding

    private void BindCanvasButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(PlayGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    #endregion

    #region OnGUI Visual Fallback

    private void CreateWhiteTexture()
    {
        if (_whiteTex == null)
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }
    }

    private void OnGUI()
    {
        if (!IsMenuOpen || IsStoryShowing) return;
        if (mainMenuRoot != null && mainMenuRoot.activeInHierarchy) return;

        CreateWhiteTexture();

        // 1. Fondo completo
        GUI.color = backdropColor;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTex);

        // 2. Título & Subtítulo con leve oscilación
        float floatOffset = Mathf.Sin(Time.unscaledTime * 2f) * 6f;
        float titleY = Screen.height * 0.22f + floatOffset;

        GUI.color = titleColor;
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.065f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(new Rect(0, titleY, Screen.width, Screen.height * 0.08f), gameTitle, titleStyle);

        GUI.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.025f),
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(new Rect(0, titleY + Screen.height * 0.07f, Screen.width, Screen.height * 0.04f), gameSubtitle, subStyle);

        // 3. Botones Centrados
        float btnW = Mathf.Min(Screen.width * 0.28f, 320f);
        float btnH = Mathf.Min(Screen.height * 0.075f, 60f);
        float btnX = (Screen.width - btnW) * 0.5f;
        float startBtnY = Screen.height * 0.52f;
        float spacing = btnH + 18f;

        if (DrawCustomMenuButton(new Rect(btnX, startBtnY, btnW, btnH), "JUGAR", buttonHoverColor))
        {
            PlayGame();
        }

        if (DrawCustomMenuButton(new Rect(btnX, startBtnY + spacing, btnW, btnH), "SALIR", quitHoverColor))
        {
            QuitGame();
        }
    }

    private bool DrawCustomMenuButton(Rect rect, string text, Color hoverCol)
    {
        Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        bool isHover = rect.Contains(mousePos);

        // Fondo del botón
        GUI.color = isHover ? hoverCol : buttonColor;
        GUI.DrawTexture(rect, _whiteTex);

        // Texto
        GUI.color = isHover ? Color.white : new Color(textColor.r, textColor.g, textColor.b, 0.9f);
        var btnStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(rect.height * 0.42f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(rect, text, btnStyle);

        // Clic
        if (isHover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Event.current.Use();
            return true;
        }

        return false;
    }

    #endregion
}
