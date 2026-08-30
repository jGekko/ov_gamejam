using System.Collections.Generic;
using UnityEngine;
using PrimeTween;

/// <summary>
/// Controla la transición suave de transparencia (Alpha) de todos los Renderers
/// hijos de un fondo (ej. Atardecer/Cielo) al entrar en una zona Trigger.
/// 
/// Características:
/// - Compatible con SpriteRenderers estándar y materiales con shader PixelInfiniteScroller2D.
/// - Control dual: Modifica vertex color (SpriteRenderer.color) y MaterialPropertyBlock (_GlobalAlpha y _Color).
/// - Transición fluida con PrimeTween (Fade In al entrar, y opcionalmente Fade Out al salir).
/// - Desactiva automáticamente el componente Renderer cuando Alpha es 0 para garantizar invisibilidad total.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BackgroundFadeTrigger : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Target Background ---")]
    [Tooltip("Transform raíz que contiene las capas del fondo. Si se deja vacío, usa este mismo GameObject y todos sus hijos.")]
    public Transform targetBackgroundRoot;

    [Header("--- Alpha Settings ---")]
    [Range(0f, 1f)]
    [Tooltip("Alpha inicial al comenzar la escena (por defecto 0 = invisible).")]
    public float initialAlpha = 0f;

    [Range(0f, 1f)]
    [Tooltip("Alpha objetivo al activarse el trigger (por defecto 1 = totalmente visible).")]
    public float targetAlpha = 1f;

    [Tooltip("Duración en segundos de la transición de fade.")]
    public float fadeDuration = 2.5f;

    [Tooltip("Curva de transición suave.")]
    public Ease fadeEase = Ease.InOutSine;

    [Header("--- Trigger Settings ---")]
    [Tooltip("Si es true, vuelve al initialAlpha cuando el jugador sale de la zona del trigger.")]
    public bool fadeOutOnExit = false;

    [Tooltip("Duración del fade out al salir (si fadeOutOnExit es true).")]
    public float exitFadeDuration = 2.0f;

    [Tooltip("Si es true, solo se activa una única vez.")]
    public bool triggerOnce = false;

    #endregion

    #region Internal State

    private struct RendererAlphaData
    {
        public Renderer renderer;
        public SpriteRenderer spriteRenderer;
        public Color baseColor;
        public MaterialPropertyBlock propBlock;
    }

    private readonly List<RendererAlphaData> _cachedRenderers = new List<RendererAlphaData>();
    private Collider2D _triggerCollider;
    private Tween _fadeTween;
    private float _currentGlobalAlpha = 0f;
    private bool _hasTriggered = false;

    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private static readonly int PropGlobalAlpha = Shader.PropertyToID("_GlobalAlpha");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider2D>();
        if (_triggerCollider != null)
        {
            _triggerCollider.isTrigger = true;
        }

        CacheRenderers();
        SetGlobalAlphaInstant(initialAlpha);
    }

    private void OnDestroy()
    {
        if (_fadeTween.isAlive) _fadeTween.Stop();
    }

    #endregion

    #region Sprite Caching & Alpha Control

    public void CacheRenderers()
    {
        _cachedRenderers.Clear();

        Transform root = targetBackgroundRoot != null ? targetBackgroundRoot : transform;
        Renderer[] allRenderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (var rend in allRenderers)
        {
            if (rend != null)
            {
                var sr = rend as SpriteRenderer;
                Color baseCol = sr != null ? sr.color : (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(PropColor) ? rend.sharedMaterial.GetColor(PropColor) : Color.white);
                
                var pb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(pb);

                _cachedRenderers.Add(new RendererAlphaData
                {
                    renderer = rend,
                    spriteRenderer = sr,
                    baseColor = baseCol,
                    propBlock = pb
                });
            }
        }
    }

    public void SetGlobalAlphaInstant(float alpha)
    {
        _currentGlobalAlpha = Mathf.Clamp01(alpha);

        if (_cachedRenderers.Count == 0)
        {
            CacheRenderers();
        }

        bool isCompletelyHidden = _currentGlobalAlpha <= 0.001f;

        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            var data = _cachedRenderers[i];
            if (data.renderer != null)
            {
                // 1. Control de visibilidad del renderer
                data.renderer.enabled = !isCompletelyHidden;

                if (!isCompletelyHidden)
                {
                    Color c = data.baseColor;
                    c.a = data.baseColor.a * _currentGlobalAlpha;

                    // 2. Modificar SpriteRenderer.color si aplica
                    if (data.spriteRenderer != null)
                    {
                        data.spriteRenderer.color = c;
                    }

                    // 3. Modificar MaterialPropertyBlock (_Color y _GlobalAlpha)
                    data.renderer.GetPropertyBlock(data.propBlock);
                    data.propBlock.SetColor(PropColor, c);
                    data.propBlock.SetFloat(PropGlobalAlpha, _currentGlobalAlpha);
                    data.renderer.SetPropertyBlock(data.propBlock);
                }
            }
        }
    }

    public void FadeTo(float target, float duration, System.Action onComplete = null)
    {
        if (_fadeTween.isAlive) _fadeTween.Stop();

        float startAlpha = _currentGlobalAlpha;

        _fadeTween = Tween.Custom(startAlpha, target, duration, onValueChange: val =>
        {
            SetGlobalAlphaInstant(val);
        }, ease: fadeEase, useUnscaledTime: true).OnComplete(onComplete);
    }

    #endregion

    #region Trigger Events

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasTriggered && triggerOnce) return;

        if (IsPlayer(other))
        {
            _hasTriggered = true;
            FadeTo(targetAlpha, fadeDuration);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (fadeOutOnExit && IsPlayer(other))
        {
            FadeTo(initialAlpha, exitFadeDuration);
        }
    }

    private bool IsPlayer(Collider2D col)
    {
        if (col.CompareTag("Player")) return true;
        if (col.GetComponent<PlayerTransformationManager>() != null || col.GetComponentInParent<PlayerTransformationManager>() != null) return true;
        return false;
    }

    #endregion

    #region Editor Context Menus

    [ContextMenu("👁️ Probar: Hacer Visible (Alpha 1)")]
    private void EditorMakeVisible()
    {
        CacheRenderers();
        SetGlobalAlphaInstant(1f);
    }

    [ContextMenu("🕶️ Probar: Hacer Invisible (Alpha 0)")]
    private void EditorMakeInvisible()
    {
        CacheRenderers();
        SetGlobalAlphaInstant(0f);
    }

    #endregion
}
