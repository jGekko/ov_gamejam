using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using PrimeTween;

/// <summary>
/// Administrador central de reaparición (Respawn) y puntos de control del nivel.
/// - Controla la muerte del jugador, pausa de físicas, animación y transiciones de pantalla.
/// - Asegura que el jugador nunca reaparezca incrustado dentro del suelo (Safe Clearance Check).
/// - Atajo con tecla 'R' para reiniciar la escena al instante con transición retro.
/// </summary>
public class LevelRespawnManager : MonoBehaviour
{
    public static LevelRespawnManager Instance { get; private set; }

    #region Inspector Fields

    [Header("--- Player & Spawn Points ---")]
    [Tooltip("Transform del jugador. Si está vacío, se auto-detecta.")]
    public PlayerTransformationManager player;

    [Tooltip("Punto de reaparición inicial por defecto.")]
    public Transform defaultSpawnPoint;

    [Header("--- Current Checkpoint ---")]
    [Tooltip("Punto de control activo actualmente.")]
    public Checkpoint currentCheckpoint;

    [Header("--- Ground & Clearance Safety (Evitar reaparecer en el suelo) ---")]
    [Tooltip("Capas de terreno sólido para verificar el suelo.")]
    public LayerMask groundLayer = ~0;

    [Tooltip("Offset vertical extra de seguridad al calcular el spawn.")]
    public float spawnSafetyYOffset = 0.15f;

    [Header("--- Restart Shortcut (Tecla R) ---")]
    [Tooltip("Si es true, permite reiniciar la escena completa al presionar la tecla R.")]
    public bool enableRestartKey = true;

    [Tooltip("Tecla asignada para reiniciar la escena.")]
    public KeyCode restartKey = KeyCode.R;

    [Header("--- Transition Settings ---")]
    [Tooltip("Retraso en segundos tras morir antes de iniciar la transición/fade out (permite ver la animación de muerte).")]
    public float deathTransitionDelay = 0.25f;

    [Tooltip("Duración del fade hacia negro al morir.")]
    public float fadeOutDuration = 0.35f;

    [Tooltip("Duración de la pantalla oculta en negro antes de reaparecer.")]
    public float blackoutWaitDuration = 0.08f;

    [Tooltip("Duración del fade de entrada tras reaparecer.")]
    public float fadeInDuration = 0.35f;

    [Header("--- Events ---")]
    public bool debugLogs = false;

    #endregion

    #region Public Properties & State

    public bool IsRespawning { get; private set; }
    public Vector2 ActiveSpawnPosition
    {
        get
        {
            Vector2 rawPos = currentCheckpoint != null 
                ? currentCheckpoint.SpawnPosition 
                : (defaultSpawnPoint != null ? (Vector2)defaultSpawnPoint.position : _initialSpawnPosition);

            return CalculateSafeSpawnPosition(rawPos);
        }
    }

    #endregion

    #region Events

    public event Action OnPlayerDied;
    public event Action OnPlayerRespawned;

    #endregion

    #region Internal State

    private Vector2 _initialSpawnPosition;

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

        AutoFindPlayer();

        if (defaultSpawnPoint != null)
        {
            _initialSpawnPosition = defaultSpawnPoint.position;
        }
        else if (player != null)
        {
            _initialSpawnPosition = player.transform.position;
        }
    }

    private void Start()
    {
        AutoFindPlayer();
    }

    private void Update()
    {
        // Atajo de reinicio de escena con tecla 'R'
        if (enableRestartKey && Input.GetKeyDown(restartKey))
        {
            RestartLevel();
        }
    }

    private void AutoFindPlayer()
    {
        if (player == null)
        {
            player = PlayerTransformationManager.Instance ?? FindFirstObjectByType<PlayerTransformationManager>();
        }
    }

    #endregion

    #region Checkpoint Registration

    /// <summary>
    /// Registra un nuevo punto de control como el activo.
    /// </summary>
    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null || checkpoint == currentCheckpoint) return;

        if (currentCheckpoint != null)
        {
            currentCheckpoint.DeactivateCheckpoint();
        }

        currentCheckpoint = checkpoint;
        currentCheckpoint.ActivateCheckpoint();

        if (debugLogs) Debug.Log($"[LevelRespawnManager] Checkpoint activado en: {checkpoint.SpawnPosition}");
    }

    #endregion

    #region Death & Respawn Flow

    /// <summary>
    /// Inicia la secuencia de muerte y reaparición en el checkpoint activo.
    /// </summary>
    public void KillPlayer(Vector2? deathLocation = null)
    {
        if (IsRespawning) return;

        AutoFindPlayer();
        if (player == null) return;

        IsRespawning = true;
        OnPlayerDied?.Invoke();

        if (debugLogs) Debug.Log("[LevelRespawnManager] Jugador derrotado. Iniciando secuencia de respawn...");

        // 1. Congelar físicas y control del jugador de inmediato
        player.SetPhysicsPaused(true);

        // 2. Disparar animación de muerte en el VFX Animator
        player.PlayDeathVFX();

        // 3. Calcular posición UV en pantalla de la muerte para que la onda de rombos (DiamondWave) se origine allí
        Vector2 deathPos = deathLocation ?? (Vector2)player.transform.position;
        Vector2 screenFocus = CalculateViewportPoint(deathPos);

        // 4. Esperar el delay configurado (0.25s) antes de iniciar el Fade Out para ver la animación de muerte
        if (deathTransitionDelay > 0.001f)
        {
            Tween.Delay(deathTransitionDelay, () =>
            {
                StartFadeOutTransition(screenFocus);
            }, useUnscaledTime: true);
        }
        else
        {
            StartFadeOutTransition(screenFocus);
        }
    }

    private void StartFadeOutTransition(Vector2 screenFocus)
    {
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.FadeOut(fadeOutDuration, TransitionStyle.DiamondWave, screenFocus, () =>
            {
                ExecuteRespawnHidden();
            });
        }
        else
        {
            ExecuteRespawnHidden();
        }
    }

    private void ExecuteRespawnHidden()
    {
        // 5. Restaurar jugador: Forma Humana, visuales activos, velocidad cero, teletransporte a Checkpoint
        Vector2 spawnPos = ActiveSpawnPosition;

        if (player != null)
        {
            player.ResetOnRespawn();
            player.transform.position = spawnPos;
        }

        // 6. Sincronizar cámara instantáneamente a la habitación del spawn
        if (CameraRoomManager.Instance != null)
        {
            CameraRoomManager.Instance.SnapToPlayerRoomInstant();
        }

        // 7. Esperar el breve tiempo de blackout
        Tween.Delay(blackoutWaitDuration, () =>
        {
            // 8. Fade In usando ÚNICAMENTE CircleIris originado en el punto de respawn
            Vector2 respawnScreenFocus = CalculateViewportPoint(spawnPos);

            if (ScreenTransitionManager.Instance != null)
            {
                ScreenTransitionManager.Instance.FadeIn(fadeInDuration, TransitionStyle.CircleIris, respawnScreenFocus, () =>
                {
                    FinishRespawn();
                });
            }
            else
            {
                FinishRespawn();
            }
        }, useUnscaledTime: true);
    }

    private void FinishRespawn()
    {
        // 9. Reanudar físicas y control del jugador
        if (player != null)
        {
            player.SetPhysicsPaused(false);
        }

        IsRespawning = false;
        OnPlayerRespawned?.Invoke();

        if (debugLogs) Debug.Log("[LevelRespawnManager] Respawn completado con éxito.");
    }

    private Vector2 CalculateViewportPoint(Vector2 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector2(0.5f, 0.5f);

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
    }

    /// <summary>
    /// Calcula una posición segura por encima del suelo para evitar quedar incrustado en colliders.
    /// </summary>
    private Vector2 CalculateSafeSpawnPosition(Vector2 rawPos)
    {
        Vector2 safePos = rawPos;

        // 1. Raycast hacia abajo desde arriba del punto para encontrar el suelo exacto
        Vector2 rayOrigin = rawPos + Vector2.up * 1.0f;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 2.5f, groundLayer);

        if (hit.collider != null && !hit.collider.isTrigger)
        {
            float halfHeight = 0.5f;
            if (player != null && player.humanCollider != null)
            {
                halfHeight = player.humanCollider.bounds.extents.y;
            }
            safePos.y = hit.point.y + halfHeight + spawnSafetyYOffset;
        }
        else
        {
            safePos.y += spawnSafetyYOffset;
        }

        // 2. Si todavía solapa con algún collider sólido, empujar progresivamente hacia arriba
        Collider2D col = (player != null && player.humanCollider != null) ? player.humanCollider : null;
        if (col != null)
        {
            Collider2D[] results = new Collider2D[5];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            if (groundLayer.value != 0) filter.SetLayerMask(groundLayer);

            for (int step = 0; step < 8; step++)
            {
                int count = Physics2D.OverlapBox(safePos, col.bounds.size * 0.85f, 0f, filter, results);
                if (count == 0) break;
                safePos += Vector2.up * 0.15f;
            }
        }

        return safePos;
    }

    #endregion

    #region Scene Restart API

    /// <summary>
    /// Reinicia la escena actual con una transición suave (Fade Out -> Load -> Fade In).
    /// </summary>
    public void RestartLevel()
    {
        if (debugLogs) Debug.Log("[LevelRespawnManager] Reiniciando nivel...");

        string currentScene = SceneManager.GetActiveScene().name;

        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.FadeOut(0.25f, TransitionStyle.DiamondWave, null, () =>
            {
                SceneManager.LoadScene(currentScene);
            });
        }
        else
        {
            SceneManager.LoadScene(currentScene);
        }
    }

    #endregion
}
