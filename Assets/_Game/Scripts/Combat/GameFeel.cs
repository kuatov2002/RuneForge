using UnityEngine;

/// <summary>
/// Centralized juice/game-feel effects: hitstop, knockback, death VFX, dash afterimage.
/// Singleton — auto-created by Bootstrap or first access.
/// </summary>
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    float _hitstopTimer;
    float _savedTimeScale = 1f;
    bool _inHitstop;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (_inHitstop)
        {
            _hitstopTimer -= Time.unscaledDeltaTime;
            if (_hitstopTimer <= 0f)
            {
                Time.timeScale = _savedTimeScale;
                _inHitstop = false;
            }
        }
    }

    // ─── HITSTOP ────────────────────────────────────────────────

    /// <summary>Freeze time for a brief moment (30-50ms typical). Longer hitstop wins.</summary>
    public void Hitstop(float duration = 0.04f)
    {
        if (_inHitstop)
        {
            // Keep the longer hitstop
            if (duration > _hitstopTimer)
                _hitstopTimer = duration;
            return;
        }
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        _hitstopTimer = duration;
        _inHitstop = true;
    }

    // ─── KNOCKBACK ──────────────────────────────────────────────

    /// <summary>Push an enemy away from a point.</summary>
    public static void ApplyKnockback(Transform target, Vector3 fromPoint, float force)
    {
        if (target == null) return;
        var rb = target.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 dir = (target.position - fromPoint);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        dir.Normalize();

        // Swarm enemies are lighter — fly further
        var swarm = target.GetComponent<SwarmAI>();
        if (swarm != null) force *= 1.5f;

        // Brief kinematic override to apply displacement
        if (rb.isKinematic)
        {
            // For kinematic enemies, lerp position directly
            var kb = target.gameObject.AddComponent<KnockbackEffect>();
            kb.Init(dir * force, 0.2f);
        }
        else
        {
            rb.AddForce(dir * force, ForceMode.Impulse);
        }
    }

    // ─── DEATH VFX ──────────────────────────────────────────────

    /// <summary>Spawn burst particles when an enemy dies.</summary>
    public static void SpawnDeathVFX(Vector3 position, Color color)
    {
        // Expanding ring on ground
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = position + Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 4f);
        ring.AddComponent<DeathRingEffect>().Init(2.5f, 0.4f);

        // Scatter fragments
        int fragmentCount = Random.Range(5, 9);
        for (int i = 0; i < fragmentCount; i++)
        {
            var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(frag.GetComponent<BoxCollider>());
            frag.transform.position = position + Vector3.up * 0.5f;
            float s = Random.Range(0.06f, 0.15f);
            frag.transform.localScale = new Vector3(s, s, s);
            frag.transform.rotation = Random.rotation;
            frag.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 2f);

            var fragRb = frag.AddComponent<Rigidbody>();
            fragRb.useGravity = true;
            fragRb.mass = 0.1f;
            Vector3 burst = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(1f, 3f),
                Random.Range(-1f, 1f)
            ).normalized * Random.Range(3f, 6f);
            fragRb.AddForce(burst, ForceMode.Impulse);
            fragRb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);

            Object.Destroy(frag, Random.Range(0.5f, 1f));
        }

        // Flash sphere (brief bright flash at death point)
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = position + Vector3.up * 0.5f;
        flash.transform.localScale = Vector3.one * 0.8f;
        Color flashColor = Color.Lerp(color, Color.white, 0.5f);
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(flashColor, 6f);
        flash.AddComponent<FlashShrink>().Init(0.2f);
    }

    // ─── DASH AFTERIMAGE ────────────────────────────────────────

    /// <summary>Spawn a translucent ghost copy of renderers at current position.</summary>
    public static void SpawnDashGhost(Renderer[] renderers, Vector3 position, Quaternion rotation)
    {
        var ghost = new GameObject("DashGhost");
        ghost.transform.position = position;
        ghost.transform.rotation = rotation;

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var copy = new GameObject("GhostPart");
            copy.transform.parent = ghost.transform;
            copy.transform.position = r.transform.position;
            copy.transform.rotation = r.transform.rotation;
            copy.transform.localScale = r.transform.lossyScale;

            var copyMF = copy.AddComponent<MeshFilter>();
            copyMF.sharedMesh = mf.sharedMesh;

            var copyR = copy.AddComponent<MeshRenderer>();
            Color ghostColor = new Color(0.3f, 0.8f, 1f, 0.4f);
            var mat = ShaderCache.NewEmissive(ghostColor, 2f);
            copyR.material = mat;
        }

        ghost.AddComponent<GhostFade>().Init(0.25f);
    }

    // ─── DODGE VFX ─────────────────────────────────────────────

    /// <summary>Visual feedback for successful dodge (expanding white ring).</summary>
    public static void SpawnDodgeVFX(Vector3 position)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = position + Vector3.up * 0.1f;
        ring.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 1f, 1f, 0.7f), 3f);
        ring.AddComponent<DeathRingEffect>().Init(2.5f, 0.3f);
    }

    // ─── SCREEN FLASH ──────────────────────────────────────────

    /// <summary>Flash a color overlay on screen (used for spell reactions).
    /// Shows a small camera-space quad that fades out instead of covering the whole screen.</summary>
    public static void ScreenFlash(Color color, float duration = 0.15f)
    {
        if (Camera.main == null) return;
        var flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(flash.GetComponent<MeshCollider>());
        flash.name = "ScreenFlash";
        flash.transform.SetParent(Camera.main.transform, false);
        flash.transform.localPosition = new Vector3(0, 0, 0.3f);
        // Much smaller — noticeable tint, not a blinding full-screen overlay
        flash.transform.localScale = new Vector3(8f, 8f, 1f);
        flash.transform.localRotation = Quaternion.identity;
        Color flashCol = color;
        flashCol.a = Mathf.Clamp(color.a, 0.1f, 0.15f);
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(flashCol, 1f);
        flash.AddComponent<FlashShrink>().Init(duration);
    }

    // ─── HIT IMPACT PARTICLES ──────────────────────────────────

    /// <summary>Spawn small element-colored sparks on every hit.</summary>
    public static void SpawnHitParticles(Vector3 position, Color color, float scale = 1f, float damage = 0f)
    {
        // White impact flash at hit point
        var hitFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(hitFlash.GetComponent<SphereCollider>());
        hitFlash.name = "HitFlash";
        hitFlash.transform.position = position;
        hitFlash.transform.localScale = Vector3.one * 0.3f;
        hitFlash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(Color.white, 6f);
        Object.Destroy(hitFlash, 0.1f);

        // Scale particle size by damage
        float baseSize = 0.05f;
        if (damage > 20f) baseSize = 0.12f;
        else if (damage > 10f) baseSize = 0.08f;

        int count = Random.Range(8, 14);
        for (int i = 0; i < count; i++)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(spark.GetComponent<BoxCollider>());
            spark.name = "HitSpark";
            float s = Random.Range(baseSize * 0.6f, baseSize * 1.4f) * scale;
            spark.transform.localScale = new Vector3(s, s, s);
            spark.transform.position = position + Random.insideUnitSphere * 0.2f;
            spark.transform.rotation = Random.rotation;
            spark.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 3f);

            var rb = spark.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 0.02f;
            Vector3 burst = Random.insideUnitSphere.normalized * Random.Range(2f, 5f);
            burst.y = Mathf.Abs(burst.y);
            rb.AddForce(burst, ForceMode.Impulse);

            Object.Destroy(spark, Random.Range(0.2f, 0.4f));
        }
    }

    // ─── PICKUP BURST VFX ──────────────────────────────────────

    /// <summary>Brief scale-up burst when picking up items.</summary>
    public static void SpawnPickupBurst(Vector3 position, Color color)
    {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = position + Vector3.up * 0.1f;
        ring.transform.localScale = new Vector3(0.2f, 0.02f, 0.2f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 4f);
        ring.AddComponent<DeathRingEffect>().Init(1.5f, 0.25f);
    }

    // ─── PLAYER DEATH VFX ──────────────────────────────────────

    /// <summary>Scatter player primitives on death.</summary>
    public static void PlayerDeathVFX(Transform player)
    {
        if (player == null) return;

        var renderers = player.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var frag = new GameObject("DeathFrag");
            frag.transform.position = r.transform.position;
            frag.transform.rotation = r.transform.rotation;
            frag.transform.localScale = r.transform.lossyScale;

            var fragMF = frag.AddComponent<MeshFilter>();
            fragMF.sharedMesh = mf.sharedMesh;
            var fragR = frag.AddComponent<MeshRenderer>();
            fragR.material = r.material;

            var rb = frag.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            Vector3 burst = Random.insideUnitSphere.normalized * Random.Range(3f, 7f);
            burst.y = Mathf.Abs(burst.y) * 1.5f;
            rb.AddForce(burst, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

            Object.Destroy(frag, Random.Range(1.5f, 2.5f));
        }

        SFXSystem.Play(SFXSystem.SFXType.PlayerDeath, player.position);
        if (Instance != null) Instance.Hitstop(0.08f);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.7f);
    }
}

// ─── HELPER COMPONENTS ──────────────────────────────────────────

/// <summary>Displaces a kinematic rigidbody over time for knockback.</summary>
public class KnockbackEffect : MonoBehaviour
{
    Vector3 _velocity;
    float _duration;
    float _timer;

    public void Init(Vector3 velocity, float duration)
    {
        _velocity = velocity;
        _duration = duration;
        _timer = duration;
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0)
        {
            Destroy(this);
            return;
        }
        float t = _timer / _duration; // 1→0 ease out
        transform.position += _velocity * t * Time.deltaTime;
    }
}

/// <summary>Expanding ring that fades out (death VFX).</summary>
public class DeathRingEffect : MonoBehaviour
{
    float _targetScale;
    float _duration;
    float _timer;
    Renderer _renderer;

    public void Init(float targetScale, float duration)
    {
        _targetScale = targetScale;
        _duration = duration;
        _timer = 0;
        _renderer = GetComponent<Renderer>();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        float scale = Mathf.Lerp(0.5f, _targetScale, t);
        transform.localScale = new Vector3(scale, 0.02f, scale);
        if (_renderer != null && _renderer.material != null)
        {
            Color c = _renderer.material.GetColor("_Color");
            c.a = 1f - t;
            _renderer.material.SetColor("_Color", c * (1f - t));
        }
    }
}

/// <summary>Quick bright flash that shrinks to nothing.</summary>
public class FlashShrink : MonoBehaviour
{
    float _duration;
    float _timer;
    Vector3 _startScale;

    public void Init(float duration)
    {
        _duration = duration;
        _timer = 0;
        _startScale = transform.localScale;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.localScale = _startScale * (1f - t * t);
    }
}

/// <summary>Fades dash ghost renderers then destroys.</summary>
public class GhostFade : MonoBehaviour
{
    float _duration;
    float _timer;
    Renderer[] _renderers;

    public void Init(float duration)
    {
        _duration = duration;
        _timer = 0;
        _renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        float fade = 1f - t;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            Color c = r.material.GetColor("_EmissionColor");
            r.material.SetColor("_EmissionColor", c * fade);
        }
    }
}

/// <summary>Pulsing red portal VFX for combat pressure warnings. Animates danger shader fill.</summary>
public class PressurePortalVFX : MonoBehaviour
{
    Renderer _renderer;
    float _fillTimer;
    const float FillDuration = 5f;

    void Start() { _renderer = GetComponent<Renderer>(); }
    void Update()
    {
        if (_renderer == null) return;
        _fillTimer += Time.deltaTime;
        float fill = Mathf.Clamp01(_fillTimer / FillDuration);
        _renderer.material.SetFloat("_FillProgress", fill);
        float pulse = 0.9f + Mathf.PingPong(Time.time * 3f, 0.2f);
        transform.localScale = new Vector3(1.2f, 0.03f, 1.2f) * pulse;
    }
}
