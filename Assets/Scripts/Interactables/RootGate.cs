using UnityEngine;
using UnityEngine.Events;
using PrimeTween;

/// <summary>
/// Compuerta o barrera de raíces de mangle entrelazadas.
/// Se abre/retrae al activarse mediante bejucos/lianas (VinePullSwitch) o aros del ave (FloraTargetRing).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RootGate : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Movement & Animation ---")]
    [Tooltip("Desplazamiento relativo al abrirse (ej. hacia abajo (0, -3) para enterrarse en el suelo).")]
    public Vector3 openOffset = new Vector3(0f, -3f, 0f);

    [Tooltip("Duración de la animación de apertura/cierre.")]
    public float transitionDuration = 0.5f;

    [Tooltip("Si es true, la compuerta inicia abierta.")]
    public bool startOpen = false;

    [Header("--- Visual & Feedback ---")]
    public GameObject visualRoot;
    public AudioSource moveAudio;
    public ParticleSystem moveParticles;

    [Header("--- Events ---")]
    public UnityEvent OnGateOpened;
    public UnityEvent OnGateClosed;

    #endregion

    #region Public Properties & State

    public bool IsOpen { get; private set; }

    #endregion

    #region Internal State

    private Collider2D _collider;
    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private Tween _moveTween;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (visualRoot == null) visualRoot = gameObject;

        _closedPosition = visualRoot.transform.position;
        _openPosition = _closedPosition + openOffset;

        if (startOpen)
        {
            SetStateInstant(true);
        }
        else
        {
            SetStateInstant(false);
        }
    }

    private void OnDestroy()
    {
        if (_moveTween.isAlive) _moveTween.Stop();
    }

    #endregion

    #region Gate Actions

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        if (_moveTween.isAlive) _moveTween.Stop();
        if (moveAudio != null) moveAudio.Play();
        if (moveParticles != null) moveParticles.Play();

        _moveTween = Tween.Position(visualRoot.transform, _openPosition, transitionDuration, ease: Ease.OutQuad)
            .OnComplete(() =>
            {
                if (_collider != null) _collider.enabled = false;
                OnGateOpened?.Invoke();
            });
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        if (_collider != null) _collider.enabled = true;
        if (_moveTween.isAlive) _moveTween.Stop();
        if (moveAudio != null) moveAudio.Play();
        if (moveParticles != null) moveParticles.Play();

        _moveTween = Tween.Position(visualRoot.transform, _closedPosition, transitionDuration, ease: Ease.InQuad)
            .OnComplete(() =>
            {
                OnGateClosed?.Invoke();
            });
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void SetStateInstant(bool open)
    {
        IsOpen = open;
        if (_moveTween.isAlive) _moveTween.Stop();

        if (visualRoot != null)
        {
            visualRoot.transform.position = open ? _openPosition : _closedPosition;
        }

        if (_collider != null)
        {
            _collider.enabled = !open;
        }
    }

    #endregion
}
