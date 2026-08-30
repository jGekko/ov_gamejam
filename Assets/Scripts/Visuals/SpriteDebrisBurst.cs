using UnityEngine;

/// <summary>
/// Emisor de fragmentos/escombros (debris) 2D pixel-art perfecto.
/// Permite desprender sprites en su tamaño y proporción nativa exacta con física 2D real (sin distorsión 3D ni reescalados extraños),
/// o configurar un ParticleSystem nativo 100% alineado al plano 2D.
/// </summary>
public class SpriteDebrisBurst : MonoBehaviour
{
    public enum BurstMode
    {
        [Tooltip("Recomendado para Pixel Art: Usa SpriteRenderers 2D directos. Tamaño 100% exacto del sprite original y cero distorsión 3D.")]
        PixelPerfect2DSprites,

        [Tooltip("Configura y usa el ParticleSystem nativo de Unity.")]
        UnityParticleSystem
    }

    #region Inspector Fields

    [Header("--- Modo de Emisión ---")]
    [Tooltip("PixelPerfect2DSprites garantiza tamaño exacto de cada sprite original sin distorsión 3D.")]
    public BurstMode mode = BurstMode.PixelPerfect2DSprites;

    [Header("--- Sprites a Desprender ---")]
    [Tooltip("Lista de sprites de fragmentos (rocas, madera, hojas, cristales, etc.).")]
    public Sprite[] debrisSprites;

    [Header("--- Tamaño & Escala (Size Tuning) ---")]
    [Tooltip("Multiplicador de escala sobre el tamaño nativo del sprite (1.0 = tamaño exacto del sprite original en píxeles).")]
    [Range(0.1f, 4f)] public float scaleMultiplier = 1.0f;

    [Tooltip("Variación aleatoria de tamaño (+/-). Ej. 0.25 = variación entre 0.75x y 1.25x.")]
    [Range(0f, 0.8f)] public float sizeRandomness = 0.2f;

    [Header("--- Cantidad & Dispersión ---")]
    [Tooltip("Cantidad de fragmentos a desprender en el impacto.")]
    [Range(1, 60)] public int burstCount = 14;

    [Tooltip("Radio del área inicial de desprendimiento.")]
    public float spreadRadius = 0.2f;

    [Tooltip("Si es true, dispersa en 360° todas las direcciones. Si es false, usa angleRange (ej. arco hacia arriba).")]
    public bool fullCircleBurst = true;

    [Tooltip("Rango de ángulo de expulsión en grados si fullCircleBurst es false (0° = Derecha, 90° = Arriba, 180° = Izquierda).")]
    public Vector2 angleRange = new Vector2(30f, 150f);

    [Header("--- Física 2D & Movimiento ---")]
    [Tooltip("Velocidad mínima y máxima de expulsión.")]
    public Vector2 speedRange = new Vector2(4f, 8.5f);

    [Tooltip("Gravedad 2D que atrae los fragmentos hacia abajo.")]
    public float gravity = 22f;

    [Tooltip("Velocidad máxima de rotación en 2D (grados/segundo).")]
    public float spinSpeed = 450f;

    [Tooltip("Tiempo de vida promedio en segundos antes de desaparecer.")]
    public float lifetime = 0.8f;

    [Header("--- Render & Apariencia ---")]
    [Tooltip("Sorting Layer para los fragmentos.")]
    public string sortingLayerName = "Default";

    [Tooltip("Order in Layer para los fragmentos.")]
    public int orderInLayer = 10;

    [Tooltip("Tinte de color aplicado a los fragmentos.")]
    public Color tint = Color.white;

    [Tooltip("Material personalizado opcional (si es null, usa Sprites/Default).")]
    public Material customMaterial;

    [Header("--- Opciones de Ejecución ---")]
    [Tooltip("Si es true, reproduce la ráfaga automáticamente al activarse en Awake/Start.")]
    public bool playOnStart = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    #endregion

    #region Burst Playback

    /// <summary>
    /// Dispara la ráfaga de fragmentos.
    /// </summary>
    [ContextMenu("▶ Disparar Burst (Play)")]
    public void Play()
    {
        PlayAt(transform.position);
    }

    /// <summary>
    /// Dispara la ráfaga de fragmentos en una posición del mundo dada.
    /// </summary>
    public void PlayAt(Vector3 position)
    {
        if (debrisSprites == null || debrisSprites.Length == 0)
        {
            Debug.LogWarning($"[SpriteDebrisBurst] No hay sprites asignados en '{gameObject.name}'.");
            return;
        }

        if (mode == BurstMode.PixelPerfect2DSprites)
        {
            SpawnPixelPerfectSprites(position);
        }
        else
        {
            PlayParticleSystem(position);
        }
    }

    private void SpawnPixelPerfectSprites(Vector3 origin)
    {
        for (int i = 0; i < burstCount; i++)
        {
            Sprite chosenSprite = debrisSprites[Random.Range(0, debrisSprites.Length)];
            if (chosenSprite == null) continue;

            GameObject pieceGO = new GameObject($"Debris_{chosenSprite.name}");

            // 1. Posición inicial con spread en plano 2D
            Vector2 offset = Random.insideUnitCircle * spreadRadius;
            pieceGO.transform.position = new Vector3(origin.x + offset.x, origin.y + offset.y, origin.z);

            // 2. Escala respetando tamaño nativo del sprite
            float sizeMod = 1f + Random.Range(-sizeRandomness, sizeRandomness);
            pieceGO.transform.localScale = Vector3.one * (scaleMultiplier * sizeMod);

            // 3. SpriteRenderer 2D estricto
            SpriteRenderer sr = pieceGO.AddComponent<SpriteRenderer>();
            sr.sprite = chosenSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = orderInLayer;
            sr.color = tint;
            if (customMaterial != null) sr.material = customMaterial;

            // 4. Vector de velocidad 2D
            float angleDeg = fullCircleBurst ? Random.Range(0f, 360f) : Random.Range(angleRange.x, angleRange.y);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            float speed = Random.Range(speedRange.x, speedRange.y);
            Vector2 velocity = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * speed;

            float rotSpeed = Random.Range(-spinSpeed, spinSpeed);
            float pieceLifetime = lifetime * Random.Range(0.85f, 1.15f);

            // 5. Comportamiento de física 2D
            var behaviour = pieceGO.AddComponent<PixelDebrisPiece>();
            behaviour.Initialize(velocity, gravity, rotSpeed, pieceLifetime);
        }
    }

    private void PlayParticleSystem(Vector3 origin)
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            SetupNativeParticleSystem();
            ps = GetComponent<ParticleSystem>();
        }

        if (ps != null)
        {
            transform.position = origin;
            ps.Clear();
            ps.Play();
        }
    }

    #endregion

    #region Particle System Setup (2D Flat Alignment)

    /// <summary>
    /// Configura el ParticleSystem nativo de Unity asegurando que sea 100% plano 2D (sin rotaciones 3D).
    /// </summary>
    [ContextMenu("⚡ Configurar Particle System 2D")]
    public void SetupNativeParticleSystem()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();

        ParticleSystemRenderer psr = GetComponent<ParticleSystemRenderer>();
        if (psr == null) psr = gameObject.AddComponent<ParticleSystemRenderer>();

        // Material 2D
        if (customMaterial != null)
        {
            psr.sharedMaterial = customMaterial;
        }
        else if (psr.sharedMaterial == null || psr.sharedMaterial.shader == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                psr.sharedMaterial = new Material(shader);
            }
        }

        // Renderer 2D Alignment: Facing garantiza que no se incline en 3D
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.alignment = ParticleSystemRenderSpace.Facing;
        psr.sortMode = ParticleSystemSortMode.Distance;
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder = orderInLayer;

        // Calcular tamaño promedio aproximado en unidades de mundo según el primer sprite
        float avgWorldSize = 0.5f * scaleMultiplier;
        if (debrisSprites != null && debrisSprites.Length > 0 && debrisSprites[0] != null)
        {
            avgWorldSize = (debrisSprites[0].rect.width / debrisSprites[0].pixelsPerUnit) * scaleMultiplier;
        }

        // 1. Módulo Principal (Main)
        var main = ps.main;
        main.duration = lifetime;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime * 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(avgWorldSize * (1f - sizeRandomness), avgWorldSize * (1f + sizeRandomness));
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotation3D = false; // ESTRICTO 2D
        main.gravityModifier = gravity / 9.81f; // Normalizar gravedad para particle system
        main.startColor = tint;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 500;

        // 2. Módulo de Emisión (Burst)
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)burstCount)
        });

        // 3. Módulo de Forma (Shape) - Plano 2D XY estricto
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = spreadRadius;
        shape.arc = 360f;
        shape.rotation = Vector3.zero; // Sin rotación 3D en plano Z
        shape.position = Vector3.zero;
        shape.radiusThickness = 1f;

        // 4. Módulo de Rotación 2D sobre eje Z
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(0f);
        rot.y = new ParticleSystem.MinMaxCurve(0f);
        rot.z = new ParticleSystem.MinMaxCurve(-spinSpeed * Mathf.Deg2Rad, spinSpeed * Mathf.Deg2Rad);

        // 5. Módulo de Color sobre tiempo
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        // 6. Texture Sheet Animation
        if (debrisSprites != null && debrisSprites.Length > 0)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            tsa.animation = ParticleSystemAnimationType.SingleRow;
            tsa.timeMode = ParticleSystemAnimationTimeMode.Lifetime;

            int count = debrisSprites.Length;
            while (tsa.spriteCount < count)
            {
                tsa.AddSprite(debrisSprites[tsa.spriteCount]);
            }
            while (tsa.spriteCount > count)
            {
                tsa.RemoveSprite(tsa.spriteCount - 1);
            }
            for (int i = 0; i < count; i++)
            {
                tsa.SetSprite(i, debrisSprites[i]);
            }

            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, count);
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        }

        Debug.Log($"[SpriteDebrisBurst] ParticleSystem 2D configurado correctamente en '{gameObject.name}'.");
    }

    #endregion

    #region Internal 2D Debris Piece Behaviour

    /// <summary>
    /// Comportamiento ligero de física 2D para cada fragmento de sprite.
    /// </summary>
    private class PixelDebrisPiece : MonoBehaviour
    {
        private Vector2 _velocity;
        private float _gravity;
        private float _spinSpeed;
        private float _lifetime;
        private float _elapsed;
        private SpriteRenderer _sr;
        private Vector3 _baseScale;

        public void Initialize(Vector2 velocity, float gravity, float spinSpeed, float lifetime)
        {
            _velocity = velocity;
            _gravity = gravity;
            _spinSpeed = spinSpeed;
            _lifetime = lifetime;
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // 1. Movimiento 2D estricto en plano XY
            _velocity.y -= _gravity * Time.deltaTime;
            transform.position += (Vector3)(_velocity * Time.deltaTime);

            // 2. Rotación 2D plana en Z
            transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime, Space.Self);

            // 3. Desvanecimiento suave y encogimiento al final
            float progress = _elapsed / _lifetime;
            if (progress > 0.65f)
            {
                float fade = (progress - 0.65f) / 0.35f;
                if (_sr != null)
                {
                    Color c = _sr.color;
                    c.a = Mathf.Lerp(1f, 0f, fade);
                    _sr.color = c;
                }
                transform.localScale = Vector3.Lerp(_baseScale, Vector3.zero, fade * 0.6f);
            }
        }
    }

    #endregion
}
