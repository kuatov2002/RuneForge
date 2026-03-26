using UnityEngine;

/// <summary>
/// Fire + Earth = Magma
/// Lava pool that heavily slows enemies and deals DoT.
/// Slow disappears as soon as enemies leave.
/// Balance: radius 2 (was 3), damage 3 (was 5)
/// </summary>
public static class MagmaSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.4f; duration *= 1.5f; damage *= 1.3f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) { SpawnZone(center, damage, radius, duration, charged); return; }

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;

        // Lava glob sphere
        var projGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(projGO.GetComponent<SphereCollider>());
        projGO.name = "MagmaProjectile";
        projGO.transform.position = origin;
        projGO.transform.localScale = Vector3.one * 0.25f;
        projGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0f), 4f);

        var col = projGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;

        var rb = projGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        float dmg = damage; float rad = radius; float dur = duration; bool ch = charged;
        var zp = projGO.AddComponent<ZoneProjectile>();
        zp.Init(dir, 14f, 3f, 16f, new Color(1f, 0.5f, 0f), (pos) => SpawnZone(pos, dmg, rad, dur, ch));
    }

    public static void SpawnZone(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        center.y = 0.05f;

        var poolGO = new GameObject("MagmaPool");
        poolGO.transform.position = center;

        // ── VFX: glowing lava disc that pulses ──
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(poolGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2, 0.04f, radius * 2);
        disc.GetComponent<Renderer>().material = ShaderCache.NewLava(new Color(0.3f, 0.05f, 0f));

        // Bubbling lava particles: continuous emission popping up from surface
        var bubbleGO = new GameObject("MagmaBubbles");
        bubbleGO.transform.SetParent(poolGO.transform, false);
        bubbleGO.transform.localPosition = Vector3.up * 0.1f;
        var bubblePS = bubbleGO.AddComponent<ParticleSystem>();
        bubblePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var bubbleMain = bubblePS.main;
        bubbleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        bubbleMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        bubbleMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        bubbleMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.05f),
            new Color(1f, 0.3f, 0f));
        bubbleMain.simulationSpace = ParticleSystemSimulationSpace.World;
        bubbleMain.gravityModifier = 0.5f;
        bubbleMain.duration = duration;
        bubbleMain.loop = true;
        bubbleMain.maxParticles = 30;

        var bubbleEmission = bubblePS.emission;
        bubbleEmission.rateOverTime = 12;

        var bubbleShape = bubblePS.shape;
        bubbleShape.shapeType = ParticleSystemShapeType.Circle;
        bubbleShape.radius = radius * 0.8f;

        var bubbleSizeOL = bubblePS.sizeOverLifetime;
        bubbleSizeOL.enabled = true;
        bubbleSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var bubbleColorOL = bubblePS.colorOverLifetime;
        bubbleColorOL.enabled = true;
        var bGrad = new Gradient();
        bGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0f), 0.5f),
                new GradientColorKey(new Color(0.4f, 0.1f, 0f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        bubbleColorOL.color = bGrad;

        var bubbleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        bubbleMat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        bubbleMat.SetFloat("_Mode", 1); // Additive
        bubbleGO.GetComponent<ParticleSystemRenderer>().material = bubbleMat;
        bubblePS.Play();

        // Heat shimmer: transparent particles rising
        var shimmerGO = new GameObject("MagmaShimmer");
        shimmerGO.transform.SetParent(poolGO.transform, false);
        shimmerGO.transform.localPosition = Vector3.up * 0.2f;
        var shimmerPS = shimmerGO.AddComponent<ParticleSystem>();
        shimmerPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var shimmerMain = shimmerPS.main;
        shimmerMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        shimmerMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        shimmerMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        shimmerMain.startColor = new Color(1f, 0.7f, 0.3f, 0.08f);
        shimmerMain.simulationSpace = ParticleSystemSimulationSpace.World;
        shimmerMain.gravityModifier = -0.3f;
        shimmerMain.duration = duration;
        shimmerMain.loop = true;

        var shimmerEmission = shimmerPS.emission;
        shimmerEmission.rateOverTime = 6;

        var shimmerShape = shimmerPS.shape;
        shimmerShape.shapeType = ParticleSystemShapeType.Circle;
        shimmerShape.radius = radius * 0.6f;

        var shimmerMat = new Material(Shader.Find("Particles/Standard Unlit"));
        shimmerMat.SetColor("_Color", new Color(1f, 0.7f, 0.3f, 0.1f));
        shimmerGO.GetComponent<ParticleSystemRenderer>().material = shimmerMat;
        shimmerPS.Play();

        // Trigger
        var zoneCol = poolGO.AddComponent<SphereCollider>();
        zoneCol.isTrigger = true;
        zoneCol.radius = radius;

        var zoneRb = poolGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        poolGO.AddComponent<MagmaPoolZone>().Init(damage, radius, duration, disc.GetComponent<Renderer>());

        Object.Destroy(poolGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

public class MagmaPoolZone : MonoBehaviour
{
    float _dps;
    float _radius;
    float _duration;
    float _tickTimer;
    float _pulseTimer;
    Renderer _discRenderer;

    public void Init(float dps, float radius, float duration, Renderer discRenderer)
    {
        _dps = dps;
        _radius = radius;
        _duration = duration;
        _discRenderer = discRenderer;
    }

    void Update()
    {
        // Pulse the disc between dark red and bright orange
        if (_discRenderer != null)
        {
            _pulseTimer += Time.deltaTime * 2f;
            float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            Color darkRed = new Color(0.5f, 0.1f, 0f);
            Color brightOrange = new Color(1f, 0.5f, 0f);
            Color current = Color.Lerp(darkRed, brightOrange, pulse);
            _discRenderer.material.SetColor("_EmissionColor", current * Mathf.Lerp(2f, 5f, pulse));
            _discRenderer.material.SetColor("_BaseColor", current);
            _discRenderer.material.color = current;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Apply slow while in pool
        hp.ApplyMagmaSlow();

        // DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps);
    }
}
