using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PrimeTween;

/// <summary>
/// Componente de estilizado y jugo visual (Juice) para botones de menú pixel art:
/// - Animación de Hover: Punch scale, desplazamiento horizontal sutil a la derecha (+X nudge) y cambio de color.
/// - Indicador opcional de selección (flecha/cursor '▶' o icono pixel).
/// - Animación de Click: Squash & stretch inmediato y satisfactorio.
/// - Pulso sutil en Idle para el botón principal (ej. JUGAR).
/// - Soporte para Ratón (Hover/Click) y Teclado/Gamepad (Select/Submit).
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    #region Inspector Fields

    [Header("--- Target Elements ---")]
    [Tooltip("RectTransform a animar. Si se deja vacío, se usa el RectTransform de este botón.")]
    public RectTransform targetRect;

    [Tooltip("Componente TextMeshProUGUI del botón. Si se deja vacío, se auto-detecta.")]
    public TextMeshProUGUI labelTMP;

    [Tooltip("Componente Text estándar de UI (si no se usa TextMeshPro).")]
    public Text labelText;

    [Header("--- Selection Indicator (Opcional) ---")]
    [Tooltip("GameObject de flecha o cursor (ej. '▶' o icono pixel) que aparece al seleccionar o pasar el cursor.")]
    public GameObject selectionIndicator;

    [Header("--- Hover / Focus Animation ---")]
    [Tooltip("Escala al pasar el ratón o seleccionar (ej. 1.08x).")]
    public float hoverScale = 1.08f;

    [Tooltip("Desplazamiento horizontal en píxeles al estar enfocado (estilo clásico retro hacia la derecha).")]
    public float hoverNudgeX = 12f;

    [Tooltip("Color del texto al estar enfocado.")]
    public Color hoverTextColor = new Color(1f, 0.92f, 0.35f, 1f); // Dorado brillante

    [Tooltip("Duración de la animación de hover.")]
    public float hoverDuration = 0.15f;

    [Header("--- Click / Press Animation ---")]
    [Tooltip("Escala de compresión al hacer clic (ej. 0.92x).")]
    public float clickScale = 0.92f;

    [Tooltip("Duración de la animación de clic.")]
    public float clickDuration = 0.1f;

    [Header("--- Primary Button Pulse (Idle) ---")]
    [Tooltip("Si es true, este botón tiene un sutil pulso continuo para invitar a interactuar (ideal para 'JUGAR').")]
    public bool enableIdlePulse = false;

    [Tooltip("Intensidad del pulso en idle.")]
    public float idlePulseAmount = 0.04f;

    [Tooltip("Velocidad del pulso en idle.")]
    public float idlePulseSpeed = 2.5f;

    [Header("--- Audio Feedback (Opcional) ---")]
    public AudioSource audioSource;
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0.8f, 1.2f)] public float soundPitchVariation = 0.05f;

    #endregion

    #region Internal State

    private Button _button;
    private Vector2 _initialAnchoredPos;
    private Vector3 _initialScale;
    private Color _initialTextColor = Color.white;
    private bool _isHovered = false;
    private bool _isPressed = false;
    private Tween _scaleTween;
    private Tween _posTween;
    private Tween _colorTween;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (targetRect == null)
        {
            targetRect = GetComponent<RectTransform>();
        }

        if (targetRect != null)
        {
            _initialAnchoredPos = targetRect.anchoredPosition;
            _initialScale = targetRect.localScale;
        }

        AutoFindText();

        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(false);
        }
    }

    private void OnEnable()
    {
        ResetToNormalInstant();
    }

    private void OnDisable()
    {
        KillTweens();
        ResetToNormalInstant();
    }

    private void Update()
    {
        if (enableIdlePulse && !_isHovered && !_isPressed && targetRect != null)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * idlePulseSpeed) * idlePulseAmount;
            targetRect.localScale = _initialScale * (1f + pulse);
        }
    }

    #endregion

    #region Text & Setup

    private void AutoFindText()
    {
        if (labelTMP == null)
        {
            labelTMP = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (labelTMP != null)
        {
            _initialTextColor = labelTMP.color;
            return;
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<Text>(true);
        }

        if (labelText != null)
        {
            _initialTextColor = labelText.color;
        }
    }

    #endregion

    #region Event Handlers

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        ApplyHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_isPressed) return;
        ApplyHover(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        ApplyHover(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        ApplyHover(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        _isPressed = true;
        PlayClickJuice();
        PlaySound(clickSound);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        if (_isHovered)
        {
            ApplyHover(true);
        }
        else
        {
            ApplyHover(false);
        }
    }

    #endregion

    #region Animations

    private void ApplyHover(bool hover)
    {
        _isHovered = hover;

        KillTweens();

        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(hover);
            if (hover)
            {
                // Punch sutil al indicador
                var indRect = selectionIndicator.GetComponent<RectTransform>();
                if (indRect != null)
                {
                    Tween.Scale(indRect, Vector3.one * 1.2f, 0.12f, Ease.OutBack, useUnscaledTime: true)
                        .OnComplete(() => Tween.Scale(indRect, Vector3.one, 0.08f, useUnscaledTime: true));
                }
            }
        }

        if (hover)
        {
            PlaySound(hoverSound);

            Vector3 targetScale = _initialScale * hoverScale;
            Vector2 targetPos = _initialAnchoredPos + new Vector2(hoverNudgeX, 0f);

            if (targetRect != null)
            {
                _scaleTween = Tween.Scale(targetRect, targetScale, hoverDuration, Ease.OutBack, useUnscaledTime: true);
                _posTween = Tween.UIAnchoredPosition(targetRect, targetPos, hoverDuration, Ease.OutQuad, useUnscaledTime: true);
            }

            SetTextColor(hoverTextColor, hoverDuration);
        }
        else
        {
            if (targetRect != null)
            {
                _scaleTween = Tween.Scale(targetRect, _initialScale, hoverDuration, Ease.OutQuad, useUnscaledTime: true);
                _posTween = Tween.UIAnchoredPosition(targetRect, _initialAnchoredPos, hoverDuration, Ease.OutQuad, useUnscaledTime: true);
            }

            SetTextColor(_initialTextColor, hoverDuration);
        }
    }

    private void PlayClickJuice()
    {
        KillTweens();

        if (targetRect != null)
        {
            Vector3 compressedScale = _initialScale * clickScale;
            _scaleTween = Tween.Scale(targetRect, compressedScale, clickDuration, Ease.OutQuad, useUnscaledTime: true);
        }
    }

    private void SetTextColor(Color targetCol, float duration)
    {
        if (labelTMP != null)
        {
            _colorTween = Tween.Color(labelTMP, targetCol, duration, useUnscaledTime: true);
        }
        else if (labelText != null)
        {
            _colorTween = Tween.Color(labelText, targetCol, duration, useUnscaledTime: true);
        }
    }

    private void ResetToNormalInstant()
    {
        _isHovered = false;
        _isPressed = false;

        if (targetRect != null)
        {
            targetRect.anchoredPosition = _initialAnchoredPos;
            targetRect.localScale = _initialScale;
        }

        if (labelTMP != null) labelTMP.color = _initialTextColor;
        if (labelText != null) labelText.color = _initialTextColor;

        if (selectionIndicator != null) selectionIndicator.SetActive(false);
    }

    private void KillTweens()
    {
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_posTween.isAlive) _posTween.Stop();
        if (_colorTween.isAlive) _colorTween.Stop();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        audioSource.pitch = 1.0f + Random.Range(-soundPitchVariation, soundPitchVariation);
        audioSource.PlayOneShot(clip);
    }

    #endregion
}
