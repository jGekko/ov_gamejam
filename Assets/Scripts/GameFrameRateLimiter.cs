using UnityEngine;

/// <summary>
/// Limita y estabiliza automáticamente la tasa de fotogramas del juego a 60 FPS.
/// Se ejecuta automáticamente antes de cargar cualquier escena, tanto en el editor como en la build final,
/// evitando desincronizaciones de físicas y problemas en pantallas de 120Hz/144Hz/240Hz.
/// </summary>
public static class GameFrameRateLimiter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeFrameRate()
    {
        // 1. Desactivar VSync para permitir control exacto de targetFrameRate en cualquier monitor
        QualitySettings.vSyncCount = 0;

        // 2. Fijar tasa de fotogramas objetivo a 60 FPS
        Application.targetFrameRate = 60;

        Debug.Log("[GameFrameRateLimiter] Tasa de fotogramas bloqueada a 60 FPS.");
    }
}
