using UnityEngine;

/// <summary>
/// Efecto visual ligero de salpicadura de agua Pixel Art.
/// Instancia pequeñas gotas/partículas cuadradas de píxel que saltan y desaparecen rápidamente.
/// </summary>
public class PixelWaterSplash : MonoBehaviour
{
    private static PixelWaterSplash _instance;
    public static PixelWaterSplash Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("PixelWaterSplashManager");
                _instance = go.AddComponent<PixelWaterSplash>();
            }
            return _instance;
        }
    }

    [Header("--- Splash Configuration ---")]
    [Tooltip("Color de las gotas de salpicadura.")]
    public Color splashColor = new Color(0.9f, 0.98f, 1f, 1f);

    [Tooltip("Cantidad de píxeles/gotas por salpicadura estándar.")]
    public int particleCount = 6;

    [Tooltip("PPU del juego para alinear las partículas a la cuadrícula.")]
    public float pixelsPerUnit = 16f;

    private static Sprite _pixelSprite;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        EnsurePixelSprite();
    }

    private void EnsurePixelSprite()
    {
        if (_pixelSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            _pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }
    }

    /// <summary>
    /// Genera una salpicadura pixelada en una posición dada con cierta intensidad vertical/horizontal.
    /// </summary>
    public void SpawnSplash(Vector2 position, float intensity = 1f)
    {
        EnsurePixelSprite();

        int count = Mathf.Clamp(Mathf.RoundToInt(particleCount * intensity), 3, 16);
        float pixelSize = 1f / Mathf.Max(pixelsPerUnit, 1f);

        for (int i = 0; i < count; i++)
        {
            var particleGO = new GameObject("SplashParticle");
            // Snap starting position to pixel grid
            float startX = Mathf.Floor(position.x * pixelsPerUnit) / pixelsPerUnit;
            float startY = Mathf.Floor(position.y * pixelsPerUnit) / pixelsPerUnit;
            particleGO.transform.position = new Vector3(startX, startY, 0f);

            var sr = particleGO.AddComponent<SpriteRenderer>();
            sr.sprite = _pixelSprite;
            sr.color = splashColor;
            sr.sortingOrder = 10;

            var drop = particleGO.AddComponent<PixelDropBehaviour>();
            float angle = Random.Range(35f, 145f) * Mathf.Deg2Rad;
            float speed = Random.Range(2.5f, 5.5f) * Mathf.Clamp(intensity, 0.6f, 1.8f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            drop.Initialize(velocity, pixelsPerUnit, 0.45f);
        }
    }

    private class PixelDropBehaviour : MonoBehaviour
    {
        private Vector2 _velocity;
        private float _ppu;
        private float _lifetime;
        private float _elapsed;
        private Vector2 _exactPos;
        private SpriteRenderer _sr;

        public void Initialize(Vector2 velocity, float ppu, float lifetime)
        {
            _velocity = velocity;
            _ppu = ppu;
            _lifetime = lifetime;
            _exactPos = transform.position;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Apply gravity
            _velocity.y -= 18f * Time.deltaTime;
            _exactPos += _velocity * Time.deltaTime;

            // Snap rendered position to 16x16 pixel grid
            float snapX = Mathf.Floor(_exactPos.x * _ppu) / _ppu;
            float snapY = Mathf.Floor(_exactPos.y * _ppu) / _ppu;
            transform.position = new Vector3(snapX, snapY, 0f);

            // Fade out
            if (_sr != null)
            {
                float alpha = 1f - (_elapsed / _lifetime);
                Color c = _sr.color;
                c.a = alpha;
                _sr.color = c;
            }
        }
    }
}
