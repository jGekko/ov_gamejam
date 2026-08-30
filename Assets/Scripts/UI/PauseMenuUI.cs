using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú de Pausa (Game Jam Edition).
/// 
/// Características:
/// - Al presionar Escape: Pausa el juego (Time.timeScale = 0), bloquea inputs de transformación y rueda animal.
/// - Libera y hace visible el cursor del ratón para interactuar con botones.
/// - Opciones clicables: Reanudar, Reiniciar desde Checkpoint y Volver al Menú Principal.
/// - Soporte dual: Canvas UI personalizable + Fallback visual automático en OnGUI con estética retro.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    #region Inspector Fields

    [Header("--- Input Configuration ---")]
    [Tooltip("Tecla para abrir/cerrar el menú de pausa.")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("--- Canvas UI Elements (Opcional) ---")]
    [Tooltip("GameObject raíz del panel del menú de pausa en Canvas.")]
    public GameObject pausePanelRoot;

    public Button resumeButton;
    public Button restartCheckpointButton;
    public Button returnToMenuButton;

    [Header("--- Scene Navigation ---")]
    [Tooltip("Nombre de la escena del Menú Principal si se usan escenas separadas.")]
    public string mainMenuSceneName = "MainMenu";

    [Header("--- Visual Style (OnGUI Fallback) ---")]
    public Color backdropColor = new Color(0f, 0f, 0f, 0.75f);
    public Color panelColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);
    public Color buttonColor = new Color(0.2f, 0.22f, 0.28f, 1f);
    public Color buttonHoverColor = new Color(0.25f, 0.65f, 1f, 1f);
    public Color buttonTextColor = Color.white;

    #endregion

    #region Public Properties & State

    public bool IsPaused { get; private set; }

    #endregion

    #region Events

    public event Action<bool> OnPauseStateChanged; // (isPaused)

    #endregion

    #region Internal State

    private Texture2D _whiteTex;
    private AnimalWheelUI _cachedWheelUI;
    private PlayerTransformationManager _cachedPTM;

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

        CreateWhiteTexture();
        BindCanvasButtons();

        if (pausePanelRoot != null)
        {
            pausePanelRoot.SetActive(false);
        }
    }

    private void Start()
    {
        _cachedWheelUI = AnimalWheelUI.Instance ?? FindFirstObjectByType<AnimalWheelUI>();
        _cachedPTM = PlayerTransformationManager.Instance ?? FindFirstObjectByType<PlayerTransformationManager>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            // Si la rueda de transformación está abierta, primero cerramos la rueda
            if (_cachedWheelUI != null && _cachedWheelUI.IsWheelOpen)
            {
                return;
            }

            TogglePause();
        }
    }

    private void OnDestroy()
    {
        if (IsPaused)
        {
            Time.timeScale = 1f;
        }
    }

    #endregion

    #region Pause / Resume Logic

    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsPaused) return;

        // No pausar si el jugador está en plena secuencia de muerte/respawn
        if (LevelRespawnManager.Instance != null && LevelRespawnManager.Instance.IsRespawning)
        {
            return;
        }

        IsPaused = true;
        Time.timeScale = 0f;

        // 1. Deshabilitar Rueda Animal y Transformación
        if (_cachedWheelUI == null) _cachedWheelUI = AnimalWheelUI.Instance ?? FindFirstObjectByType<AnimalWheelUI>();
        if (_cachedWheelUI != null)
        {
            _cachedWheelUI.enabled = false;
        }

        if (_cachedPTM == null) _cachedPTM = PlayerTransformationManager.Instance ?? FindFirstObjectByType<PlayerTransformationManager>();
        if (_cachedPTM != null)
        {
            _cachedPTM.SetPhysicsPaused(true);
        }

        // 2. Liberar y mostrar cursor para selección por ratón
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Mostrar UI de pausa
        if (pausePanelRoot != null)
        {
            pausePanelRoot.SetActive(true);
        }

        OnPauseStateChanged?.Invoke(true);
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;

        // 1. Ocultar UI de pausa
        if (pausePanelRoot != null)
        {
            pausePanelRoot.SetActive(false);
        }

        // 2. Reactivar Rueda Animal y Transformación
        if (_cachedWheelUI != null)
        {
            _cachedWheelUI.enabled = true;
        }

        if (_cachedPTM != null)
        {
            _cachedPTM.SetPhysicsPaused(false);
        }

        // 3. Bloquear cursor de vuelta al centro para gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnPauseStateChanged?.Invoke(false);
    }

    public void RestartFromCheckpoint()
    {
        ResumeGame();

        if (LevelRespawnManager.Instance != null)
        {
            LevelRespawnManager.Instance.KillPlayer();
        }
    }

    public void ReturnToMainMenu()
    {
        ResumeGame();

        // Si existe MainMenuUI en la misma escena
        if (MainMenuUI.Instance != null)
        {
            if (ScreenTransitionManager.Instance != null)
            {
                ScreenTransitionManager.Instance.Transition(0.35f, 0.35f, () =>
                {
                    MainMenuUI.Instance.ShowMainMenu();
                });
            }
            else
            {
                MainMenuUI.Instance.ShowMainMenu();
            }
            return;
        }

        // Si se usa una escena separada
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            if (ScreenTransitionManager.Instance != null)
            {
                ScreenTransitionManager.Instance.Transition(0.35f, 0.35f, () =>
                {
                    SceneManager.LoadScene(mainMenuSceneName);
                });
            }
            else
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }
    }

    #endregion

    #region Canvas Binding

    private void BindCanvasButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (restartCheckpointButton != null)
        {
            restartCheckpointButton.onClick.RemoveAllListeners();
            restartCheckpointButton.onClick.AddListener(RestartFromCheckpoint);
        }

        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.RemoveAllListeners();
            returnToMenuButton.onClick.AddListener(ReturnToMainMenu);
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
        if (!IsPaused) return;
        // Si hay Canvas configurado y activo, no dibujar GUI legacy
        if (pausePanelRoot != null && pausePanelRoot.activeInHierarchy) return;

        CreateWhiteTexture();

        // 1. Fondo oscurecido de pantalla completa
        GUI.color = backdropColor;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whiteTex);

        // 2. Ventana central
        float panelW = 320f;
        float panelH = 340f;
        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;

        GUI.color = panelColor;
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _whiteTex);

        // Borde decorativo
        GUI.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(panelX - 2, panelY - 2, panelW + 4, 3), _whiteTex);
        GUI.DrawTexture(new Rect(panelX - 2, panelY + panelH - 1, panelW + 4, 3), _whiteTex);
        GUI.DrawTexture(new Rect(panelX - 2, panelY, 3, panelH), _whiteTex);
        GUI.DrawTexture(new Rect(panelX + panelW - 1, panelY, 3, panelH), _whiteTex);

        // 3. Título PAUSA
        GUI.color = Color.white;
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(panelX, panelY + 25, panelW, 35), "PAUSA", titleStyle);

        // 4. Botones
        float btnW = 240f;
        float btnH = 45f;
        float btnX = panelX + (panelW - btnW) * 0.5f;
        float startBtnY = panelY + 85f;
        float spacing = 58f;

        if (DrawCustomButton(new Rect(btnX, startBtnY, btnW, btnH), "REANUDAR"))
        {
            ResumeGame();
        }

        if (DrawCustomButton(new Rect(btnX, startBtnY + spacing, btnW, btnH), "REINICIAR CHECKPOINT"))
        {
            RestartFromCheckpoint();
        }

        if (DrawCustomButton(new Rect(btnX, startBtnY + spacing * 2, btnW, btnH), "MENÚ PRINCIPAL"))
        {
            ReturnToMainMenu();
        }
    }

    private bool DrawCustomButton(Rect rect, string text)
    {
        Vector2 mousePos = Event.current.mousePosition;
        bool isHover = rect.Contains(mousePos);

        GUI.color = isHover ? buttonHoverColor : buttonColor;
        GUI.DrawTexture(rect, _whiteTex);

        GUI.color = buttonTextColor;
        var btnStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(rect, text, btnStyle);

        return isHover && Event.current.type == EventType.MouseDown && Event.current.button == 0;
    }

    #endregion
}
