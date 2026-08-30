using UnityEngine;
using Unity.Cinemachine;
using PrimeTween;

/// <summary>
/// Modos de pausa o ralentización de físicas durante la transición entre habitaciones.
/// </summary>
public enum TransitionFreezeMode
{
    /// <summary>
    /// Congela completamente las físicas e inputs del jugador en su pose/inercia actual (estilo Celeste).
    /// </summary>
    FreezePlayerPhysics,

    /// <summary>
    /// Ralentiza el tiempo de juego (Slow-Motion) durante el blend de Cinemachine.
    /// </summary>
    SlowMotion,

    /// <summary>
    /// No altera las físicas ni el tiempo; el juego y movimiento continúan normalmente mientras las cámaras hacen blend.
    /// </summary>
    None
}

/// <summary>
/// Controlador de Habitación con Cámara Virtual Cinemachine independiente.
/// Cada habitación posee su propia CinemachineCamera y Collider2D Trigger.
/// Soporta salas normales (seguimiento del jugador con confiner), salas especiales fijas con Zoom-Out (Overview/Arena)
/// y escalado / offset dinámico del fondo (Background) para evitar espacios vacíos durante el Zoom-Out.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CameraRoom : MonoBehaviour
{
    #region Inspector Fields

    [Header("--- Virtual Camera ---")]
    [Tooltip("Cámara virtual de Cinemachine (CinemachineCamera) para esta habitación. Si está vacía, se auto-detecta en este objeto o hijos.")]
    public CinemachineCamera virtualCamera;

    [Tooltip("Prioridad cuando esta habitación está activa.")]
    public int activePriority = 10;

    [Tooltip("Prioridad cuando esta habitación está inactiva.")]
    public int inactivePriority = 0;

    [Tooltip("Si es true, asigna automáticamente al jugador como TrackingTarget si la cámara no tiene target asignado.")]
    public bool autoAssignPlayerTarget = true;

    [Tooltip("Si es true, asigna automáticamente el Collider2D de esta sala al CinemachineConfiner2D de la cámara.")]
    public bool autoAssignConfiner = true;

    [Header("--- Fixed / Overview Camera (Zoom Out) ---")]
    [Tooltip("Si es true, la cámara de esta habitación se queda estática y no persigue al jugador (ideal para salas gigantes panorámicas, arenas o puzzles).")]
    public bool isFixedCamera = false;

    [Tooltip("Punto fijo donde se posiciona la cámara. Si se deja vacío y 'isFixedCamera' es true, se centra automáticamente en el medio del Collider de la habitación.")]
    public Transform fixedCameraPosition;

    [Tooltip("Tamaño ortográfico (Zoom) personalizado para esta habitación. Si es > 0, sobreescribe la lente de la cámara.")]
    public float customOrthographicSize = 0f;

    [Tooltip("FOV personalizado para cámaras en perspectiva (si aplica). Si es > 0, sobreescribe el Field of View.")]
    public float customFieldOfView = 0f;

    [Tooltip("Si es true, calcula automáticamente el Zoom Out exacto para que el tamaño del Collider de la habitación quepa al 100% en la pantalla.")]
    public bool autoFitToRoomBounds = false;

    [Tooltip("Margen adicional de padding en unidades de mundo al auto-ajustar el tamaño de la pantalla.")]
    public float autoFitPadding = 0.5f;

    [Header("--- Background Scaling & Offset (Zoom Out Compensation) ---")]
    [Tooltip("GameObject o Transform del fondo (Background / Parallax) que se escalará y desplazará mientras esta habitación esté activa.")]
    public Transform backgroundToScale;

    [Tooltip("Escala destino del fondo al entrar a esta habitación (ej. (2, 2, 1) o (2.5, 2.5, 1)).")]
    public Vector3 activeBackgroundScale = new Vector3(2f, 2f, 1f);

    [Tooltip("Offset o desplazamiento en unidades de mundo (X, Y) aplicado a la posición del fondo al entrar a esta habitación.")]
    public Vector2 activeBackgroundOffset = Vector2.zero;

    [Tooltip("Si es true, interpola suavemente la escala y posición usando PrimeTween. Si es false, cambia instantáneamente.")]
    public bool smoothBackgroundScale = true;

    [Tooltip("Duración en segundos de la transición de escala y posición del fondo.")]
    public float backgroundScaleDuration = 0.5f;

    [Header("--- Room Identity ---")]
    [Tooltip("Nombre descriptivo para identificar esta habitación (opcional).")]
    public string roomName = "New Room";

    [Header("--- Seamless Connections (Salas Combinadas) ---")]
    [Tooltip("Nombre de grupo o zona compartida. Las habitaciones que compartan el mismo 'zoneGroup' no pausarán físicas al moverse entre ellas, pero sí al conectar con habitaciones externas.")]
    public string zoneGroup = "";

    [Tooltip("Lista explícita de habitaciones con las que esta sala conecta de forma continua (sin freeze).")]
    public System.Collections.Generic.List<CameraRoom> seamlessRooms = new System.Collections.Generic.List<CameraRoom>();

    [Header("--- Freeze / Transition Settings (Enter & Exit) ---")]
    [Tooltip("Habilita la pausa/ralentización de físicas al ENTRAR a esta habitación.")]
    public bool enableFreezeOnEnter = true;
    [Tooltip("Modo de efecto al ENTRAR a esta habitación.")]
    public TransitionFreezeMode enterFreezeMode = TransitionFreezeMode.FreezePlayerPhysics;

    [Tooltip("Habilita la pausa/ralentización de físicas al SALIR de esta habitación hacia otra.")]
    public bool enableFreezeOnExit = true;
    [Tooltip("Modo de efecto al SALIR de esta habitación.")]
    public TransitionFreezeMode exitFreezeMode = TransitionFreezeMode.FreezePlayerPhysics;

    [Tooltip("Duración en segundos de la pausa de físicas (sincronizar con el Blend Duration de CinemachineBrain).")]
    public float freezeDuration = 0.45f;

    [Header("--- Editor Visuals ---")]
    public Color gizmoColor = new Color(0f, 0.85f, 1f, 0.3f);

    #endregion

    private Collider2D _roomCollider;
    public Collider2D RoomCollider
    {
        get
        {
            if (_roomCollider == null) _roomCollider = GetComponent<Collider2D>();
            return _roomCollider;
        }
    }

    private Vector3 _initialBackgroundScale = Vector3.one;
    private Vector3 _initialBackgroundPosition = Vector3.zero;
    private bool _hasCapturedInitialBackgroundState = false;
    private Tween _bgScaleTween;
    private Tween _bgPosTween;

    /// <summary>
    /// Determina si la transición hacia otra habitación debe ser continua (sin freeze de físicas).
    /// </summary>
    public bool IsSeamlessWith(CameraRoom otherRoom)
    {
        if (otherRoom == null) return false;

        // 1. Mismo grupo de zona compartido (no vacío)
        if (!string.IsNullOrEmpty(zoneGroup) && !string.IsNullOrEmpty(otherRoom.zoneGroup))
        {
            if (string.Equals(zoneGroup, otherRoom.zoneGroup, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 2. Conexión explícita en la lista de salas continuas
        if (seamlessRooms != null && seamlessRooms.Contains(otherRoom))
        {
            return true;
        }

        if (otherRoom.seamlessRooms != null && otherRoom.seamlessRooms.Contains(this))
        {
            return true;
        }

        return false;
    }

    private void Awake()
    {
        _roomCollider = GetComponent<Collider2D>();
        if (_roomCollider != null && !_roomCollider.isTrigger)
        {
            _roomCollider.isTrigger = true;
        }

        if (virtualCamera == null)
        {
            virtualCamera = GetComponentInChildren<CinemachineCamera>(true) ?? GetComponent<CinemachineCamera>();
        }

        if (backgroundToScale != null && !_hasCapturedInitialBackgroundState)
        {
            _initialBackgroundScale = backgroundToScale.localScale;
            _initialBackgroundPosition = backgroundToScale.position;
            _hasCapturedInitialBackgroundState = true;
        }

        AutoSetupConfiner();

        // Iniciar desactivada por defecto (el manager activará la inicial)
        SetCameraActive(false);
    }

    private void OnValidate()
    {
        AutoSetupConfiner();
    }

    private void OnDisable()
    {
        if (_bgScaleTween.isAlive) _bgScaleTween.Stop();
        if (_bgPosTween.isAlive) _bgPosTween.Stop();
        if (backgroundToScale != null && _hasCapturedInitialBackgroundState)
        {
            backgroundToScale.localScale = _initialBackgroundScale;
            backgroundToScale.position = _initialBackgroundPosition;
        }
    }

    private void OnDestroy()
    {
        if (_bgScaleTween.isAlive) _bgScaleTween.Stop();
        if (_bgPosTween.isAlive) _bgPosTween.Stop();
    }

    /// <summary>
    /// Enlaza automáticamente el Collider2D de esta habitación al CinemachineConfiner2D de la cámara.
    /// </summary>
    public void AutoSetupConfiner()
    {
        if (!autoAssignConfiner) return;

        if (_roomCollider == null)
            _roomCollider = GetComponent<Collider2D>();

        if (virtualCamera == null)
        {
            virtualCamera = GetComponentInChildren<CinemachineCamera>(true) ?? GetComponent<CinemachineCamera>();
        }

        if (virtualCamera != null)
        {
            var confiner = virtualCamera.GetComponent<CinemachineConfiner2D>();
            if (confiner != null)
            {
                if (isFixedCamera)
                {
                    confiner.enabled = false;
                }
                else if (_roomCollider != null)
                {
                    confiner.enabled = true;
                    confiner.BoundingShape2D = _roomCollider;
                    confiner.InvalidateBoundingShapeCache();
                }
            }
        }
    }

    public void SetCameraActive(bool active)
    {
        if (virtualCamera != null)
        {
            virtualCamera.Priority = active ? activePriority : inactivePriority;
            virtualCamera.gameObject.SetActive(active);

            if (active)
            {
                // 1. Configuración de Cámara Fija vs Seguimiento
                if (isFixedCamera)
                {
                    virtualCamera.Target.TrackingTarget = null;

                    Vector3 targetPos;
                    if (fixedCameraPosition != null)
                    {
                        targetPos = fixedCameraPosition.position;
                    }
                    else if (RoomCollider != null)
                    {
                        targetPos = RoomCollider.bounds.center;
                    }
                    else
                    {
                        targetPos = transform.position;
                    }

                    targetPos.z = virtualCamera.transform.position.z;
                    virtualCamera.transform.position = targetPos;
                }
                else if (autoAssignPlayerTarget && virtualCamera.Target.TrackingTarget == null)
                {
                    if (CameraRoomManager.Instance != null && CameraRoomManager.Instance.playerTransform != null)
                    {
                        virtualCamera.Target.TrackingTarget = CameraRoomManager.Instance.playerTransform;
                    }
                }

                // 2. Ajuste de Zoom / Orthographic Size / FOV
                if (autoFitToRoomBounds && RoomCollider != null)
                {
                    Bounds b = RoomCollider.bounds;
                    float screenAspect = Camera.main != null ? Camera.main.aspect : (16f / 9f);
                    float halfHeight = (b.size.y * 0.5f) + autoFitPadding;
                    float halfWidthNeeded = ((b.size.x * 0.5f) + autoFitPadding) / screenAspect;
                    float calculatedOrthoSize = Mathf.Max(halfHeight, halfWidthNeeded);

                    var lens = virtualCamera.Lens;
                    lens.OrthographicSize = calculatedOrthoSize;
                    virtualCamera.Lens = lens;
                }
                else if (customOrthographicSize > 0.01f)
                {
                    var lens = virtualCamera.Lens;
                    lens.OrthographicSize = customOrthographicSize;
                    virtualCamera.Lens = lens;
                }

                if (customFieldOfView > 0.01f)
                {
                    var lens = virtualCamera.Lens;
                    lens.FieldOfView = customFieldOfView;
                    virtualCamera.Lens = lens;
                }
            }
        }

        // 3. Escalado y Offset del Fondo (Background Scaling & Offset)
        if (backgroundToScale != null)
        {
            if (!_hasCapturedInitialBackgroundState)
            {
                _initialBackgroundScale = backgroundToScale.localScale;
                _initialBackgroundPosition = backgroundToScale.position;
                _hasCapturedInitialBackgroundState = true;
            }

            Vector3 targetScale = active ? activeBackgroundScale : _initialBackgroundScale;
            Vector3 targetPosition = active 
                ? (_initialBackgroundPosition + new Vector3(activeBackgroundOffset.x, activeBackgroundOffset.y, 0f))
                : _initialBackgroundPosition;

            if (_bgScaleTween.isAlive) _bgScaleTween.Stop();
            if (_bgPosTween.isAlive) _bgPosTween.Stop();

            if (smoothBackgroundScale && backgroundScaleDuration > 0f)
            {
                _bgScaleTween = Tween.Scale(backgroundToScale, targetScale, backgroundScaleDuration, Ease.InOutQuad);
                _bgPosTween = Tween.Position(backgroundToScale, targetPosition, backgroundScaleDuration, Ease.InOutQuad);
            }
            else
            {
                backgroundToScale.localScale = targetScale;
                backgroundToScale.position = targetPosition;
            }
        }
    }

    public Collider2D GetCollider()
    {
        if (_roomCollider == null)
            _roomCollider = GetComponent<Collider2D>();
        return _roomCollider;
    }

    #region Trigger Callbacks

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            if (CameraRoomManager.Instance != null)
            {
                CameraRoomManager.Instance.OnPlayerEnterRoom(this);
            }
            else
            {
                SetCameraActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            if (ShouldIgnoreExit(other)) return;

            if (CameraRoomManager.Instance != null)
            {
                CameraRoomManager.Instance.OnPlayerExitRoom(this);
            }
            else
            {
                SetCameraActive(false);
            }
        }
    }

    private bool ShouldIgnoreExit(Collider2D other)
    {
        if (PlayerTransformationManager.Instance != null && PlayerTransformationManager.Instance.IsTransforming)
        {
            return true;
        }

        return false;
    }

    private bool IsPlayer(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (CameraRoomManager.Instance != null && CameraRoomManager.Instance.playerTransform != null)
        {
            return col.transform == CameraRoomManager.Instance.playerTransform ||
                   col.transform.IsChildOf(CameraRoomManager.Instance.playerTransform);
        }
        return false;
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = isFixedCamera ? new Color(1f, 0.6f, 0.1f, 0.4f) : gizmoColor;
        Bounds b = col.bounds;
        Gizmos.DrawWireCube(b.center, b.size);

        string label = string.IsNullOrEmpty(roomName) ? gameObject.name : roomName;
        if (isFixedCamera) label += " [Fixed Zoom-Out]";
        if (backgroundToScale != null) label += " [Scales/Offsets BG]";
        if (!enableFreezeOnEnter) label += " [NoFreezeIn]";
        if (!enableFreezeOnExit) label += " [NoFreezeOut]";

        UnityEditor.Handles.Label(b.center + Vector3.up * (b.extents.y + 0.5f), label);
    }
#endif
}
