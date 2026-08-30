using System;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using PrimeTween;

/// <summary>
/// Temporizador de partida / Speedrun Timer para UI en Canvas.
/// 
/// Características:
/// - Inicia el conteo automáticamente al detectar el primer input del jugador (teclas, movimiento, saltos, clics).
/// - Se detiene automáticamente al alcanzar el último checkpoint (marcado con 'isFinalCheckpoint = true').
/// - Soporta TextMeshProUGUI y UI.Text estándar.
/// - Formatos de tiempo personalizables (minutos, segundos, centésimas / milisegundos).
/// - Feedback visual de inicio, conteo activo y acabado de nivel (PrimeTween pulse / cambio de color).
/// - Guardado automático del récord / mejor tiempo (Best Time).
/// </summary>
public class SpeedrunTimerUI : MonoBehaviour
{
    public enum TimerState
    {
        WaitingForFirstInput,
        Running,
        Finished
    }

    public static SpeedrunTimerUI Instance { get; private set; }

    #region Inspector Fields

    [Header("--- UI References ---")]
    [Tooltip("Componente TextMeshProUGUI que mostrará el tiempo. Si se deja vacío, se busca automáticamente.")]
    public TextMeshProUGUI timerTextTMP;

    [Tooltip("Componente Text estándar alternativo (Legacy UI).")]
    public UnityEngine.UI.Text timerTextLegacy;

    [Header("--- Timer Settings ---")]
    [Tooltip("Si es true, cuenta el tiempo real de reloj (Time.unscaledDeltaTime) sin ser afectado por la cámara lenta (Slow-Motion). Si es false, cuenta el tiempo de juego.")]
    public bool countUnscaledTime = true;

    [Tooltip("Formato del tiempo. Ejemplos:\n'mm\\:ss\\.ff' -> 01:23.45\n'mm\\:ss\\.fff' -> 01:23.456\n'hh\\:mm\\:ss\\.ff' -> 00:01:23.45")]
    public string timeFormat = @"mm\:ss\.ff";

    [Tooltip("Prefijo del texto (ej. '⏱ ' o 'TIME: ').")]
    public string prefix = "";

    [Tooltip("Sufijo del texto (ej. ' s').")]
    public string suffix = "";

    [Header("--- Colors & Visual Feedback ---")]
    [Tooltip("Color del texto mientras espera el primer input.")]
    public Color waitingColor = new Color(0.75f, 0.75f, 0.75f, 0.85f);

    [Tooltip("Color del texto mientras está contando activamente.")]
    public Color runningColor = Color.white;

    [Tooltip("Color del texto al cruzar la meta / detener el temporizador.")]
    public Color finishedColor = new Color(0.25f, 1f, 0.45f, 1f);

    [Tooltip("Si es true, realiza un pulso/agrandamiento del texto con PrimeTween al terminar la partida.")]
    public bool pulseOnFinish = true;

    [Header("--- Best Time / Records ---")]
    [Tooltip("Si es true, guarda el mejor tiempo en PlayerPrefs.")]
    public bool saveBestTime = true;

    [Tooltip("Clave de PlayerPrefs para guardar el récord de tiempo.")]
    public string bestTimeKey = "Speedrun_BestTime";

    [Header("--- Events ---")]
    public UnityEvent OnTimerStarted;
    public UnityEvent<float, string> OnTimerFinished; // (float seconds, string formattedTime)

    #endregion

    #region Public Properties & State

    public TimerState CurrentState => _state;
    public float ElapsedTime => _elapsedTime;
    public bool IsRunning => _state == TimerState.Running;
    public bool IsFinished => _state == TimerState.Finished;

    public float BestTime => PlayerPrefs.HasKey(bestTimeKey) ? PlayerPrefs.GetFloat(bestTimeKey) : float.MaxValue;
    public bool IsNewBestTime { get; private set; }

    #endregion

    #region Internal State

    private TimerState _state = TimerState.WaitingForFirstInput;
    private float _elapsedTime = 0f;
    private Tween _pulseTween;

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

        CacheTextReferences();
    }

    private void OnEnable()
    {
        Checkpoint.OnAnyCheckpointActivated += HandleCheckpointActivated;
    }

    private void OnDisable()
    {
        Checkpoint.OnAnyCheckpointActivated -= HandleCheckpointActivated;
        if (_pulseTween.isAlive) _pulseTween.Stop();
    }

    private void Start()
    {
        CacheTextReferences();
        ResetTimer();
    }

    private void Update()
    {
        switch (_state)
        {
            case TimerState.WaitingForFirstInput:
                if (HasAnyPlayerInput())
                {
                    StartTimer();
                }
                break;

            case TimerState.Running:
                // Si el juego está pausado (menú de pausa o timeScale cero), no avanzar el cronómetro
                if (IsGamePaused()) break;

                float dt = countUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                _elapsedTime += dt;
                UpdateDisplay(_elapsedTime, runningColor);
                break;

            case TimerState.Finished:
                break;
        }
    }

    /// <summary>
    /// Determina si el juego se encuentra en pausa o en el menú de pausa.
    /// </summary>
    public bool IsGamePaused()
    {
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return true;
        if (Time.timeScale <= 0.0001f) return true;
        return false;
    }

    #endregion

    #region Input Detection

    private bool HasAnyPlayerInput()
    {
        // 1. Teclado o mando (cualquier tecla presionada)
        if (Input.anyKeyDown) return true;

        // 2. Ejes analógicos de movimiento (Joysticks, WASD, Flechas)
        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.12f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.12f)
        {
            return true;
        }

        // 3. Botones del ratón
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            return true;
        }

        return false;
    }

    #endregion

    #region Timer Controls

    /// <summary>
    /// Inicia el temporizador.
    /// </summary>
    public void StartTimer()
    {
        if (_state == TimerState.Running) return;

        _state = TimerState.Running;
        UpdateDisplay(_elapsedTime, runningColor);
        OnTimerStarted?.Invoke();
    }

    /// <summary>
    /// Detiene el temporizador al cruzar la meta final.
    /// </summary>
    public void StopTimer()
    {
        if (_state == TimerState.Finished) return;

        _state = TimerState.Finished;

        // Comprobar y guardar récord
        IsNewBestTime = false;
        if (saveBestTime && _elapsedTime > 0.1f)
        {
            float prevBest = BestTime;
            if (_elapsedTime < prevBest)
            {
                PlayerPrefs.SetFloat(bestTimeKey, _elapsedTime);
                PlayerPrefs.Save();
                IsNewBestTime = true;
            }
        }

        string formatted = FormatTime(_elapsedTime);
        UpdateDisplay(_elapsedTime, finishedColor);

        // Feedback visual de finalización
        if (pulseOnFinish)
        {
            Transform targetT = timerTextTMP != null ? timerTextTMP.transform : (timerTextLegacy != null ? timerTextLegacy.transform : transform);
            if (_pulseTween.isAlive) _pulseTween.Stop();
            _pulseTween = Tween.PunchScale(targetT, Vector3.one * 0.35f, 0.45f);
        }

        OnTimerFinished?.Invoke(_elapsedTime, formatted);
    }

    /// <summary>
    /// Resetea el temporizador a 0 y lo pone en espera del primer input.
    /// </summary>
    public void ResetTimer()
    {
        if (_pulseTween.isAlive) _pulseTween.Stop();
        _state = TimerState.WaitingForFirstInput;
        _elapsedTime = 0f;
        IsNewBestTime = false;
        UpdateDisplay(0f, waitingColor);
    }

    #endregion

    #region Checkpoint Listener

    private void HandleCheckpointActivated(Checkpoint cp)
    {
        if (cp != null && cp.isFinalCheckpoint)
        {
            StopTimer();
        }
    }

    #endregion

    #region UI & Formatting Helpers

    private void CacheTextReferences()
    {
        if (timerTextTMP == null)
        {
            timerTextTMP = GetComponent<TextMeshProUGUI>() ?? GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (timerTextLegacy == null && timerTextTMP == null)
        {
            timerTextLegacy = GetComponent<UnityEngine.UI.Text>() ?? GetComponentInChildren<UnityEngine.UI.Text>(true);
        }
    }

    private void UpdateDisplay(float seconds, Color textColor)
    {
        string text = prefix + FormatTime(seconds) + suffix;

        if (timerTextTMP != null)
        {
            timerTextTMP.text = text;
            timerTextTMP.color = textColor;
        }

        if (timerTextLegacy != null)
        {
            timerTextLegacy.text = text;
            timerTextLegacy.color = textColor;
        }
    }

    public string FormatTime(float timeInSeconds)
    {
        if (timeInSeconds < 0f) timeInSeconds = 0f;

        TimeSpan t = TimeSpan.FromSeconds(timeInSeconds);

        try
        {
            return t.ToString(timeFormat);
        }
        catch
        {
            // Formato de respaldo estándar mm:ss.ff
            return string.Format("{0:00}:{1:00}.{2:00}", (int)t.TotalMinutes, t.Seconds, t.Milliseconds / 10);
        }
    }

    #endregion
}
