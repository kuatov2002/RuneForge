using UnityEngine;

/// <summary>
/// Poison + Poison = Plague
/// Launches a green glob projectile that spawns an expanding poison cloud on impact.
/// The cloud grows from 60% radius to full radius over duration, dealing DoT.
/// </summary>
public static class PlagueSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; duration *= 1.5f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        float finalRadius = radius;
        float finalDuration = duration;
        float finalDamage = damage;

        // Create green glob projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PlagueGlob";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.2f, 0.8f, 0.1f), 4f);

        go.AddComponent<ZoneProjectile>().Init(dir, 13f, 2f, 16f,
            new Color(0.2f, 0.8f, 0.1f),
            (impactPos) => { SpawnPlagueCloud(impactPos, finalDamage, finalRadius, finalDuration); });

        Object.Destroy(go, 5f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnPlagueCloud(Vector3 pos, float damage, float radius, float duration)
    {
        pos.y = 0.05f;

        var cloudGO = new GameObject("PlagueCloud");
        cloudGO.transform.position = pos;

        // Trigger collider — starts small, zone component will expand it
        var col = cloudGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius * 0.6f;

        var rb = cloudGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // ── VFX: green semi-transparent expanding sphere ──
        var coreSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(coreSphere.GetComponent<SphereCollider>());
        coreSphere.transform.SetParent(cloudGO.transform, false);
        coreSphere.transform.localPosition = Vector3.up * 0.5f;
        coreSphere.transform.localScale = Vector3.one * radius * 0.6f;
        coreSphere.GetComponent<Renderer>().material = ShaderCache.NewMagic(
            new Color(0.1f, 0.7f, 0.05f), 2f);

        // Green rising particles
        var particleGO = new GameObject("PlagueParticles");
        particleGO.transform.SetParent(cloudGO.transform, false);
        particleGO.transform.localPosition = Vector3.up * 0.2f;
        var ps = particleGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.1f, 0.8f, 0.1f, 0.6f),
            new Color(0.2f, 0.6f, 0.05f, 0.4f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.2f;
        main.duration = duration;
        main.loop = true;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 15;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.5f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.2f, 0.9f, 0.1f), 0f),
                new GradientColorKey(new Color(0.1f, 0.6f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.05f, 0.3f, 0.02f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.4f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOL.color = grad;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 0.5f;
        noise.octaveCount = 2;

        var psMat = new Material(ShaderCache.ParticleShader);
        psMat.SetColor("_Color", new Color(0.2f, 0.8f, 0.1f, 0.5f));
        particleGO.GetComponent<ParticleSystemRenderer>().material = psMat;
        ps.Play();

        cloudGO.AddComponent<PlagueCloudZone>().Init(damage, radius, duration,
            col, coreSphere.transform);

        Object.Destroy(cloudGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, pos);
    }
}

public class PlagueCloudZone : MonoBehaviour
{
    float _dps;
    public float Dps => _dps;
    public float Radius => _endRadius;
    float _startRadius;
    float _endRadius;
    float _duration;
    float _elapsed;
    float _tickTimer;
    SphereCollider _collider;
    Transform _visual;

    public void Init(float dps, float endRadius, float duration, SphereCollider col, Transform visual)
    {
        _dps = dps;
        _startRadius = endRadius * 0.6f;
        _endRadius = endRadius;
        _duration = duration;
        _collider = col;
        _visual = visual;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        float currentRadius = Mathf.Lerp(_startRadius, _endRadius, t);

        // Expand collider and visual
        if (_collider != null) _collider.radius = currentRadius;
        if (_visual != null) _visual.localScale = Vector3.one * currentRadius * 2f;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Green tint on enemies inside
        var rend = other.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = Color.Lerp(rend.material.color, new Color(0.3f, 0.8f, 0.2f), 0.1f);
        }

        // DoT tick: 2 DPS, tick every 0.5s = 1 damage per tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps * 0.5f);
    }
}
