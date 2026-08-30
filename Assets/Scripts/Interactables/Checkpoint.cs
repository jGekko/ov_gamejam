using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using PrimeTween;

/// <summary>
/// Punto de control (Checkpoint) para el nivel.
/// Al ser alcanzado por el jugador, se registra en LevelRespawnManager como el punto activo de reaparición.
/// Dispara el trigger configurado (por defecto 'checked') en su componente Animator al activarse.
/// Si está marcado como 'isFinalCheckpoint', actúa como meta, detiene el SpeedrunTimerUI y puede hacer transición al Menú Principal.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Spawn Setup ---")]
    [Tooltip("Transform exacto donde reaparecerá el jugador. Si está vacío, usa la posición de este GameObject.")]
    public Transform customSpawnPoint;

    [Tooltip("Offset vertical añadido a la posición de reaparición.")]
    public float spawnYOffset = 0.25f;

    [Header("--- Speedrun / Level Goal ---")]
    [Tooltip("Si es true, este checkpoint representa la meta final del nivel y detiene automáticamente el temporizador de la partida (SpeedrunTimer).")]
    public bool isFinalCheckpoint = false;

    [Tooltip("Si es true y este checkpoint es final, tras activarse cargará la escena del Menú Principal tras 'delayBeforeMenuTransition' segundos.")]
    public bool loadMenuOnFinish = true;

    [Tooltip("Nombre de la escena del menú principal a cargar.")]
    public string menuSceneName = "MainMenu";

    [Tooltip("Segundos de espera tras activar el checkpoint final antes de iniciar la transición al menú.")]
    public float delayBeforeMenuTransition = 2.0f;

    [Tooltip("Duración del fade de pantalla hacia negro al ir al menú.")]
    public float menuFadeDuration = 0.5f;

    [Tooltip("Estilo de transición visual hacia el menú.")]
    public TransitionStyle menuTransitionStyle = TransitionStyle.DiamondWave;

    [Header("--- Animation Feedback ---")]
    [Tooltip("Componente Animator asignado al checkpoint. Si se deja vacío, se auto-detecta en este objeto o en sus hijos.")]
    public Animator animator;

    [Tooltip("Nombre del Trigger que se dispara en el Animator cuando el checkpoint se activa.")]
    public string checkedTrigger = "checked";

    [Tooltip("Nombre del parámetro booleano opcional en el Animator para mantener el estado activado (dejar vacío si no se usa).")]
    public string isActivatedBool = "isActivated";

    [Header("--- Visual Feedback (Opcional) ---")]
    public SpriteRenderer spriteRenderer;
    public Sprite inactiveSprite;
    public Sprite activeSprite;
    public Color activeColor = new Color(0.2f, 1f, 0.5f, 1f);
    public Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    public ParticleSystem activationParticles;

    [Header("--- Gizmos ---")]
    public bool showGizmos = true;
    public Color gizmoActiveColor = Color.green;
    public Color gizmoInactiveColor = Color.yellow;

    #endregion

    #region Public Properties & State

    public bool IsActivated { get; private set; }

    public Vector2 SpawnPosition
    {
        get
        {
            Vector2 basePos = customSpawnPoint != null ? (Vector2)customSpawnPoint.position : (Vector2)transform.position;
            return basePos + Vector2.up * spawnYOffset;
        }
    }

    #endregion

    #region Events

    public event Action<Checkpoint> OnCheckpointActivated;
    public static event Action<Checkpoint> OnAnyCheckpointActivated;

    #endregion

    #region Internal State

    private Collider2D _collider;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider != null && !_collider.isTrigger)
        {
            _collider.isTrigger = true;
        }

        CacheReferences();
        SetVisualState(false);
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var ptm = other.GetComponent<PlayerTransformationManager>() ?? other.GetComponentInParent<PlayerTransformationManager>();
        if (ptm != null)
        {
            if (LevelRespawnManager.Instance != null)
            {
                LevelRespawnManager.Instance.RegisterCheckpoint(this);
            }
            else
            {
                ActivateCheckpoint();
            }
        }
    }

    #endregion

    #region Activation

    public void ActivateCheckpoint()
    {
        if (IsActivated) return;

        IsActivated = true;
        SetVisualState(true);

        if (activationParticles != null)
        {
            activationParticles.Play();
        }

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(checkedTrigger))
            {
                animator.ResetTrigger(checkedTrigger);
                animator.SetTrigger(checkedTrigger);
            }

            if (!string.IsNullOrEmpty(isActivatedBool))
            {
                animator.SetBool(isActivatedBool, true);
            }
        }

        OnCheckpointActivated?.Invoke(this);
        OnAnyCheckpointActivated?.Invoke(this);

        // Transición opcional al menú tras alcanzar la meta final
        if (isFinalCheckpoint && loadMenuOnFinish)
        {
            Tween.Delay(delayBeforeMenuTransition, () =>
            {
                if (ScreenTransitionManager.Instance != null)
                {
                    ScreenTransitionManager.Instance.FadeOut(menuFadeDuration, menuTransitionStyle, null, () =>
                    {
                        SceneManager.LoadScene(menuSceneName);
                    });
                }
                else
                {
                    SceneManager.LoadScene(menuSceneName);
                }
            }, useUnscaledTime: true);
        }
    }

    public void DeactivateCheckpoint()
    {
        IsActivated = false;
        SetVisualState(false);

        if (animator != null && !string.IsNullOrEmpty(isActivatedBool))
        {
            animator.SetBool(isActivatedBool, false);
        }
    }

    private void SetVisualState(bool active)
    {
        if (spriteRenderer != null)
        {
            if (active && activeSprite != null)
            {
                spriteRenderer.sprite = activeSprite;
            }
            else if (!active && inactiveSprite != null)
            {
                spriteRenderer.sprite = inactiveSprite;
            }

            spriteRenderer.color = active ? activeColor : inactiveColor;
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = IsActivated ? gizmoActiveColor : (isFinalCheckpoint ? Color.magenta : gizmoInactiveColor);
        Vector3 spawnPos = SpawnPosition;

        // Dibujar marcador de spawn
        Gizmos.DrawWireSphere(spawnPos, isFinalCheckpoint ? 0.5f : 0.35f);
        Gizmos.DrawLine(spawnPos + Vector3.down * 0.35f, spawnPos + Vector3.down * 0.7f);
        Gizmos.DrawLine(spawnPos + Vector3.left * 0.2f, spawnPos + Vector3.right * 0.2f);
    }

    #endregion
}
