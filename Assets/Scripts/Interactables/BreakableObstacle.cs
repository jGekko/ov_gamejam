using UnityEngine;
using UnityEngine.Events;
using PrimeTween;

/// <summary>
/// Obstáculo destruible (muros de raíces secas, rocas agrietadas, matorrales densos).
/// Solo se destruye cuando la Babilla (Cocodrilo) lo embiste usando su habilidad de Dash (IsDashing).
/// Soporta SpriteDebrisBurst 2D (tamaño exacto de sprites), ParticleSystem y efectos de sonido.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BreakableObstacle : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Break Settings ---")]
    [Tooltip("Si es true, el obstáculo reaparece cuando el jugador muere y reaparece.")]
    public bool respawnOnDeath = true;

    [Tooltip("Tiempo en segundos de la animación de destrucción/desvanecimiento.")]
    public float breakAnimDuration = 0.25f;

    [Header("--- Visual & Feedback ---")]
    public GameObject visualRoot;

    [Tooltip("Emisor SpriteDebrisBurst para fragmentos 2D. Si no se asigna, busca en el objeto o hijos.")]
    public SpriteDebrisBurst debrisBurst;

    [Tooltip("ParticleSystem opcional de impacto. Si no se asigna, busca en los hijos automáticamente.")]
    public ParticleSystem breakParticles;

    [Tooltip("Audio opcional que se reproduce al romperse.")]
    public AudioSource breakAudio;

    [Header("--- Reusable Debris Prefab (Opcional) ---")]
    [Tooltip("Prefab con SpriteDebrisBurst o ParticleSystem a instanciar al romperse.")]
    public GameObject debrisPrefab;

    [Header("--- Events ---")]
    public UnityEvent OnBroken;
    public UnityEvent OnRestored;

    #endregion

    #region Public Properties & State

    public bool IsBroken { get; private set; }

    #endregion

    #region Internal State

    private Collider2D _collider;
    private Vector3 _initialScale;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (visualRoot == null) visualRoot = gameObject;
        _initialScale = visualRoot.transform.localScale;

        if (debrisBurst == null)
        {
            debrisBurst = GetComponentInChildren<SpriteDebrisBurst>();
        }

        if (breakParticles == null)
        {
            breakParticles = GetComponentInChildren<ParticleSystem>();
        }
    }

    private void Start()
    {
        if (LevelRespawnManager.Instance != null && respawnOnDeath)
        {
            LevelRespawnManager.Instance.OnPlayerRespawned += HandlePlayerRespawn;
        }
    }

    private void OnDestroy()
    {
        if (LevelRespawnManager.Instance != null && respawnOnDeath)
        {
            LevelRespawnManager.Instance.OnPlayerRespawned -= HandlePlayerRespawn;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndBreak(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckAndBreak(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckAndBreak(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CheckAndBreak(other);
    }

    #endregion

    #region Break Logic

    private void CheckAndBreak(Collider2D other)
    {
        if (IsBroken) return;

        var croco = other.GetComponent<CrocodileController>() ?? other.GetComponentInParent<CrocodileController>();
        if (croco != null && croco.IsDashing)
        {
            Break();
        }
    }

    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;

        if (_collider != null) _collider.enabled = false;

        // 1. Disparar SpriteDebrisBurst si está en el objeto o hijos
        if (debrisBurst != null)
        {
            debrisBurst.Play();
        }

        // 2. Instanciar prefab si está configurado
        if (debrisPrefab != null)
        {
            GameObject instance = Instantiate(debrisPrefab, transform.position, Quaternion.identity);
            var spawnedBurst = instance.GetComponent<SpriteDebrisBurst>();
            if (spawnedBurst != null)
            {
                spawnedBurst.Play();
            }
            else
            {
                var ps = instance.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
            }
        }

        // 3. Reproducir ParticleSystem adicional si existe
        if (breakParticles != null)
        {
            breakParticles.Play();
        }

        // 4. Audio
        if (breakAudio != null)
        {
            breakAudio.Play();
        }

        // 5. Animación de impacto y encogimiento con PrimeTween
        if (visualRoot != null)
        {
            Tween.Scale(visualRoot.transform, Vector3.zero, breakAnimDuration, ease: Ease.InBack)
                .OnComplete(() =>
                {
                    if (visualRoot != gameObject) visualRoot.SetActive(false);
                    else visualRoot.transform.localScale = Vector3.zero;
                });
        }

        OnBroken?.Invoke();
    }

    public void Restore()
    {
        if (!IsBroken) return;
        IsBroken = false;

        if (_collider != null) _collider.enabled = true;

        if (visualRoot != null)
        {
            if (visualRoot != gameObject) visualRoot.SetActive(true);
            visualRoot.transform.localScale = _initialScale;
        }

        OnRestored?.Invoke();
    }

    private void HandlePlayerRespawn()
    {
        Restore();
    }

    #endregion
}
