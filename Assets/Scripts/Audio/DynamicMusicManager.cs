using UnityEngine;
using System.Collections;
using PrimeTween;

/// <summary>
/// Sistema integral y simple de música dinámica en un solo script:
/// - Reproducción de música en bucle (Loop) con control de volumen maestro y pitch base.
/// - Filtro Low-Pass ("Bajo el Agua" / Lofi Muffled) suave durante el cambio de modo (en vez de ralentizar el pitch).
/// - Efecto de Glitch estilizado, lento y musical al morir (Warp + Stutter rítmico + Tape-Stop analógico), restaurándose al revivir.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
public class DynamicMusicManager : MonoBehaviour
{
    public static DynamicMusicManager Instance { get; private set; }

    #region Inspector Fields

    [Header("--- Music Track & Playback ---")]
    [Tooltip("Canción de fondo que se reproducirá en bucle.")]
    public AudioClip musicTrack;

    [Range(0f, 1f)]
    [Tooltip("Volumen principal de la música.")]
    public float masterVolume = 0.75f;

    [Range(0.5f, 1.5f)]
    [Tooltip("Pitch base normal de la música (por defecto 1.0).")]
    public float basePitch = 1.0f;

    [Tooltip("Si es true, reproduce la música automáticamente al iniciar.")]
    public bool playOnStart = true;

    [Tooltip("Persistir entre cambios de escena.")]
    public bool dontDestroyOnLoad = false;

    [Header("--- Mode Change / Slow Motion Low-Pass (\"Bajo el Agua\") ---")]
    [Tooltip("Si es true, aplica un filtro Low-Pass tipo 'bajo el agua' / muffled mientras la rueda de cambio de modo esté abierta.")]
    public bool enableModeChangeLowPass = true;

    [Range(200f, 3000f)]
    [Tooltip("Frecuencia de corte al estar en la rueda de transformación (800-900Hz suena como bajo el agua / amortiguado).")]
    public float muffledCutoffFrequency = 850f;

    [Tooltip("Frecuencia de corte normal (22000Hz = sonido nítido y claro sin filtro).")]
    public float normalCutoffFrequency = 22000f;

    [Tooltip("Velocidad de transición suave del filtro Low-Pass.")]
    public float lowPassTransitionSpeed = 12f;

    [Range(0f, 1f)]
    [Tooltip("Multiplicador de volumen opcional durante la rueda de transformación.")]
    public float slowMoVolumeMultiplier = 0.9f;

    [Header("--- Stylized Death Glitch Effect ---")]
    [Tooltip("Si es true, aplica una secuencia de glitch musical y tape-stop lento al morir.")]
    public bool enableDeathGlitch = true;

    [Tooltip("Duración total de la secuencia de glitch y tape-stop al morir.")]
    public float glitchDuration = 0.35f;

    [Tooltip("Si es true, hace una caída analógica de pitch y volumen a 0 (Tape-Stop) al final del glitch.")]
    public bool tapeStopOnDeath = true;

    [Header("--- Respawn Recovery ---")]
    [Tooltip("Duración del fade in y apertura del filtro al reaparecer tras morir.")]
    public float respawnRecoveryDuration = 0.35f;

    #endregion

    #region Internal State

    private AudioSource _audioSource;
    private AudioLowPassFilter _lowPassFilter;
    private bool _isDead = false;
    private Coroutine _glitchCoroutine;
    private Tween _recoveryTween;
    private float _targetCutoff;
    private float _currentCutoff;

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
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        _audioSource = GetComponent<AudioSource>();
        _lowPassFilter = GetComponent<AudioLowPassFilter>();
        if (_lowPassFilter == null)
        {
            _lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
        }

        SetupAudioComponents();
    }

    private void OnEnable()
    {
        SubscribeToRespawnEvents();
    }

    private void OnDisable()
    {
        if (LevelRespawnManager.Instance != null)
        {
            LevelRespawnManager.Instance.OnPlayerDied -= HandlePlayerDied;
            LevelRespawnManager.Instance.OnPlayerRespawned -= HandlePlayerRespawned;
        }
    }

    private void Start()
    {
        SubscribeToRespawnEvents();

        if (playOnStart && musicTrack != null && !_audioSource.isPlaying)
        {
            PlayMusic(musicTrack);
        }
    }

    private void Update()
    {
        if (_audioSource == null || !_audioSource.isPlaying) return;

        // Durante el glitch de muerte, Update no interfiere
        if (_isDead) return;

        UpdateModeChangeLowPass();
    }

    #endregion

    #region Setup & Events

    private void SetupAudioComponents()
    {
        _audioSource.clip = musicTrack;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume = masterVolume;
        _audioSource.pitch = basePitch;

        _currentCutoff = normalCutoffFrequency;
        _targetCutoff = normalCutoffFrequency;
        if (_lowPassFilter != null)
        {
            _lowPassFilter.cutoffFrequency = normalCutoffFrequency;
            _lowPassFilter.lowpassResonanceQ = 1.2f;
        }
    }

    private void SubscribeToRespawnEvents()
    {
        if (LevelRespawnManager.Instance != null)
        {
            LevelRespawnManager.Instance.OnPlayerDied -= HandlePlayerDied;
            LevelRespawnManager.Instance.OnPlayerDied += HandlePlayerDied;

            LevelRespawnManager.Instance.OnPlayerRespawned -= HandlePlayerRespawned;
            LevelRespawnManager.Instance.OnPlayerRespawned += HandlePlayerRespawned;
        }
    }

    #endregion

    #region Low-Pass Logic (Bajo el Agua)

    private void UpdateModeChangeLowPass()
    {
        if (!enableModeChangeLowPass || _lowPassFilter == null) return;

        bool isTransformingOrWheelOpen = false;

        // 1. Detectar si la rueda de transformación está abierta
        if (AnimalWheelUI.Instance != null && AnimalWheelUI.Instance.IsWheelOpen)
        {
            isTransformingOrWheelOpen = true;
        }
        // 2. O si Time.timeScale está ralentizado
        else if (Time.timeScale < 0.95f)
        {
            isTransformingOrWheelOpen = true;
        }

        _targetCutoff = isTransformingOrWheelOpen ? muffledCutoffFrequency : normalCutoffFrequency;
        float targetVol = masterVolume * (isTransformingOrWheelOpen ? slowMoVolumeMultiplier : 1f);

        // Suavizado en tiempo no escalado para respuesta inmediata
        float dt = Time.unscaledDeltaTime;
        _currentCutoff = Mathf.Lerp(_currentCutoff, _targetCutoff, dt * lowPassTransitionSpeed);
        _lowPassFilter.cutoffFrequency = _currentCutoff;
        _audioSource.volume = Mathf.Lerp(_audioSource.volume, targetVol, dt * lowPassTransitionSpeed);
        _audioSource.pitch = basePitch; // Mantener pitch limpio y estable
    }

    #endregion

    #region Stylized Death Glitch & Respawn

    private void HandlePlayerDied()
    {
        if (!enableDeathGlitch) return;

        _isDead = true;

        if (_recoveryTween.isAlive) _recoveryTween.Stop();
        if (_glitchCoroutine != null) StopCoroutine(_glitchCoroutine);

        _glitchCoroutine = StartCoroutine(StylizedDeathGlitchRoutine());
    }

    private void HandlePlayerRespawned()
    {
        _isDead = false;

        if (_glitchCoroutine != null) StopCoroutine(_glitchCoroutine);
        if (_recoveryTween.isAlive) _recoveryTween.Stop();

        // Recuperación fluida y suave al revivir
        float startVol = _audioSource.volume;
        float startPitch = _audioSource.pitch;
        float startCutoff = _lowPassFilter != null ? _lowPassFilter.cutoffFrequency : normalCutoffFrequency;

        _recoveryTween = Tween.Custom(0f, 1f, respawnRecoveryDuration, val =>
        {
            _audioSource.volume = Mathf.Lerp(startVol, masterVolume, val);
            _audioSource.pitch = Mathf.Lerp(startPitch, basePitch, val);
            if (_lowPassFilter != null)
            {
                _lowPassFilter.cutoffFrequency = Mathf.Lerp(startCutoff, normalCutoffFrequency, val);
            }
        }, useUnscaledTime: true, ease: Ease.OutQuad);
    }

    /// <summary>
    /// Secuencia de glitch estilizada, rítmica y deliberada (Warp musical + micro-pausa + tape stop).
    /// </summary>
    private IEnumerator StylizedDeathGlitchRoutine()
    {
        // Fase 1: Deformación inicial de Pitch y Scoop del filtro (0.0s - 0.10s)
        _audioSource.pitch = basePitch * 0.72f;
        if (_lowPassFilter != null) _lowPassFilter.cutoffFrequency = 1400f;
        yield return new WaitForSecondsRealtime(0.09f);

        // Fase 2: Micro-stutter rítmico con caída de medio tono (0.09s - 0.18s)
        _audioSource.volume = 0f;
        yield return new WaitForSecondsRealtime(0.04f);

        _audioSource.volume = masterVolume * 0.7f;
        _audioSource.pitch = basePitch * 0.48f;
        if (_lowPassFilter != null) _lowPassFilter.cutoffFrequency = 700f;
        yield return new WaitForSecondsRealtime(0.08f);

        // Fase 3: Tape-Stop final (caída suave y continua de pitch y volumen a 0)
        if (tapeStopOnDeath)
        {
            float stopDuration = Mathf.Max(glitchDuration - 0.21f, 0.14f);
            float stopElapsed = 0f;
            float initialPitch = _audioSource.pitch;
            float initialVol = _audioSource.volume;

            while (stopElapsed < stopDuration)
            {
                stopElapsed += Time.unscaledDeltaTime;
                float t = stopElapsed / stopDuration;
                _audioSource.pitch = Mathf.Lerp(initialPitch, 0.05f, t);
                _audioSource.volume = Mathf.Lerp(initialVol, 0f, t);
                yield return null;
            }

            _audioSource.pitch = 0.01f;
            _audioSource.volume = 0f;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Inicia o cambia la canción de fondo actual con opción de crossfade.
    /// </summary>
    public void PlayMusic(AudioClip clip, float fadeDuration = 0.3f)
    {
        if (clip == null) return;

        musicTrack = clip;
        _isDead = false;

        if (_audioSource.isPlaying && fadeDuration > 0.01f)
        {
            Tween.Custom(_audioSource.volume, 0f, fadeDuration * 0.5f, val => _audioSource.volume = val, useUnscaledTime: true)
                .OnComplete(() =>
                {
                    _audioSource.clip = clip;
                    _audioSource.pitch = basePitch;
                    _audioSource.Play();
                    Tween.Custom(0f, masterVolume, fadeDuration * 0.5f, val => _audioSource.volume = val, useUnscaledTime: true);
                });
        }
        else
        {
            _audioSource.clip = clip;
            _audioSource.pitch = basePitch;
            _audioSource.volume = masterVolume;
            _audioSource.Play();
        }
    }

    /// <summary>
    /// Detiene la música con fade out opcional.
    /// </summary>
    public void StopMusic(float fadeDuration = 0.3f)
    {
        if (fadeDuration > 0.01f)
        {
            Tween.Custom(_audioSource.volume, 0f, fadeDuration, val => _audioSource.volume = val, useUnscaledTime: true)
                .OnComplete(() => _audioSource.Stop());
        }
        else
        {
            _audioSource.Stop();
        }
    }

    /// <summary>
    /// Ajusta el volumen principal en tiempo de ejecución.
    /// </summary>
    public void SetMasterVolume(float newVolume)
    {
        masterVolume = Mathf.Clamp01(newVolume);
        if (!_isDead)
        {
            _audioSource.volume = masterVolume;
        }
    }

    #endregion
}
