using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using PrimeTween;

/// <summary>
/// Gestor central de habitaciones de cámara con Cinemachine (One Virtual Camera per Room).
/// 
/// Características:
/// - Cada habitación posee su propia CinemachineCamera y Collider2D de sala.
/// - CinemachineBrain gestiona automáticamente el blend nativo y suave entre cámaras al activarse/desactivarse.
/// - Pausa sincronizada de físicas del jugador durante la duración del blend (estilo Celeste).
/// - Control independiente de Freeze para Entrada (Enter) y Salida (Exit) por habitación (ideal para salas combinadas).
/// - Asignación automática del jugador como TrackingTarget/Follow a todas las cámaras de las habitaciones.
/// </summary>
public class CameraRoomManager : MonoBehaviour
{
    public static CameraRoomManager Instance { get; private set; }

    #region Events

    public delegate void RoomChangeHandler(CameraRoom newRoom, CameraRoom previousRoom);
    public event RoomChangeHandler OnRoomChanged;
    public event Action<CameraRoom> OnRoomEntered;
    public event Action<CameraRoom> OnRoomExited;

    #endregion

    #region Inspector Fields

    [Header("--- Target Setup ---")]
    [Tooltip("Transform del jugador que las cámaras deben seguir. Si está vacío, se auto-detecta por tag 'Player'.")]
    public Transform playerTransform;

    [Header("--- Global Fallback & Defaults ---")]
    [Tooltip("Cámara virtual por defecto cuando el jugador no está dentro de ninguna habitación.")]
    public CinemachineCamera defaultCamera;

    [Tooltip("Duración de congelación por defecto si no es especificada por la habitación.")]
    public float defaultFreezeDuration = 0.45f;

    [Tooltip("Modo de freeze global por defecto.")]
    public TransitionFreezeMode defaultFreezeMode = TransitionFreezeMode.FreezePlayerPhysics;

    [Tooltip("Escala de tiempo si se usa el modo SlowMotion.")]
    [Range(0.01f, 1f)] public float slowMoScale = 0.1f;

    #endregion

    #region Internal State

    private readonly List<CameraRoom> _activeRooms = new List<CameraRoom>();
    private CameraRoom _currentRoom;
    private Tween _freezeTween;
    private bool _isFreezing;

    public CameraRoom CurrentRoom => _currentRoom;
    public bool IsFreezing => _isFreezing;

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

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }
    }

    private void Start()
    {
        // Si hay una habitación inicial activa, activarla; de lo contrario, activar la cámara por defecto
        if (_currentRoom != null)
        {
            _currentRoom.SetCameraActive(true);
        }
        else if (defaultCamera != null)
        {
            defaultCamera.gameObject.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (_freezeTween.isAlive) _freezeTween.Stop();
        ApplyFreeze(defaultFreezeMode, false);
    }

    #endregion

    #region Room Management

    public void OnPlayerEnterRoom(CameraRoom room)
    {
        if (room == null) return;

        // Si la sala ya estaba en la lista, moverla al final como la más reciente
        if (_activeRooms.Contains(room))
        {
            _activeRooms.Remove(room);
        }
        _activeRooms.Add(room);

        EvaluateActiveRoom();
    }

    public void OnPlayerExitRoom(CameraRoom room)
    {
        if (room == null) return;

        // Si el jugador se está transformando, ignorar deshabilitaciones momentáneas de colliders
        if (PlayerTransformationManager.Instance != null && PlayerTransformationManager.Instance.IsTransforming)
        {
            return;
        }

        if (_activeRooms.Contains(room))
        {
            _activeRooms.Remove(room);
            room.SetCameraActive(false);
        }

        EvaluateActiveRoom();
    }

    private void EvaluateActiveRoom()
    {
        CameraRoom topRoom = _activeRooms.Count > 0 ? _activeRooms[_activeRooms.Count - 1] : null;

        if (topRoom != _currentRoom)
        {
            CameraRoom previous = _currentRoom;
            _currentRoom = topRoom;

            if (_currentRoom != null)
            {
                // Activar la cámara de la nueva habitación (Cinemachine Brain inicia el blend nativo)
                _currentRoom.SetCameraActive(true);

                if (previous != null)
                {
                    // Desactivar la cámara anterior
                    previous.SetCameraActive(false);

                    // Evaluar si se debe pausar físicas (no pausar si las salas son continuas/seamless)
                    bool isSeamless = previous.IsSeamlessWith(_currentRoom);
                    bool shouldFreeze = !isSeamless && previous.enableFreezeOnExit && _currentRoom.enableFreezeOnEnter;

                    if (shouldFreeze)
                    {
                        float duration = _currentRoom.freezeDuration > 0f ? _currentRoom.freezeDuration : defaultFreezeDuration;
                        TransitionFreezeMode mode = _currentRoom.enterFreezeMode;
                        TriggerPhysicsFreeze(duration, mode);
                    }
                }

                OnRoomEntered?.Invoke(_currentRoom);
            }
            else
            {
                if (defaultCamera != null)
                {
                    defaultCamera.gameObject.SetActive(true);
                }
            }

            if (previous != null)
            {
                OnRoomExited?.Invoke(previous);
            }

            OnRoomChanged?.Invoke(_currentRoom, previous);
        }
    }

    #endregion

    #region Instant Camera Snap & Respawn

    /// <summary>
    /// Reubica inmediatamente la cámara en la habitación del jugador tras una reaparición/teletransporte,
    /// desactivando las demás cámaras y activando la cámara del cuarto sin interpolaciones visibles.
    /// </summary>
    public void SnapToPlayerRoomInstant()
    {
        if (playerTransform == null && PlayerTransformationManager.Instance != null)
        {
            playerTransform = PlayerTransformationManager.Instance.transform;
        }
        if (playerTransform == null) return;

        Vector2 playerPos = playerTransform.position;
        CameraRoom targetRoom = null;

        var allRooms = FindObjectsByType<CameraRoom>(FindObjectsSortMode.None);
        foreach (var room in allRooms)
        {
            if (room != null && room.RoomCollider != null && room.RoomCollider.OverlapPoint(playerPos))
            {
                targetRoom = room;
                break;
            }
        }

        _activeRooms.Clear();

        if (targetRoom != null)
        {
            _activeRooms.Add(targetRoom);
            _currentRoom = targetRoom;

            // Desactivar todas las demás cámaras
            foreach (var room in allRooms)
            {
                if (room != targetRoom)
                {
                    room.SetCameraActive(false);
                }
            }
            if (defaultCamera != null) defaultCamera.gameObject.SetActive(false);

            // Activar la cámara de la habitación objetivo
            targetRoom.SetCameraActive(true);
            OnRoomEntered?.Invoke(targetRoom);
        }
        else
        {
            _currentRoom = null;
            foreach (var room in allRooms)
            {
                room.SetCameraActive(false);
            }
            if (defaultCamera != null)
            {
                defaultCamera.gameObject.SetActive(true);
            }
        }
    }

    #endregion

    #region Physics Freeze Handling

    public void TriggerPhysicsFreeze(float duration, TransitionFreezeMode mode)
    {
        if (duration <= 0.001f || mode == TransitionFreezeMode.None) return;

        if (_freezeTween.isAlive) _freezeTween.Stop();

        _isFreezing = true;
        ApplyFreeze(mode, true);

        _freezeTween = Tween.Delay(duration, () =>
        {
            _isFreezing = false;
            ApplyFreeze(mode, false);
        }, useUnscaledTime: true);
    }

    private void ApplyFreeze(TransitionFreezeMode mode, bool freeze)
    {
        switch (mode)
        {
            case TransitionFreezeMode.FreezePlayerPhysics:
                if (PlayerTransformationManager.Instance != null)
                {
                    PlayerTransformationManager.Instance.SetPhysicsPaused(freeze);
                }
                else if (playerTransform != null)
                {
                    var rb = playerTransform.GetComponent<Rigidbody2D>();
                    if (rb != null && freeze)
                    {
                        rb.linearVelocity = Vector2.zero;
                    }
                }
                break;

            case TransitionFreezeMode.SlowMotion:
                Time.timeScale = freeze ? slowMoScale : 1f;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                break;

            case TransitionFreezeMode.None:
                break;
        }
    }

    #endregion
}
