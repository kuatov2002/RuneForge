using UnityEngine;

/// <summary>
/// Void + Water = Rift
/// Projectile that spawns a time-slow zone. Enemies inside move at 30% speed and take DoT.
/// </summary>
public static class RiftSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; duration *= 1.5f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) { SpawnZone(center, damage, radius, duration, charged); return; }

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        // Blue-purple projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "RiftProjectile";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.3f, 0.1f, 0.8f), 5f);

        // Trail particles
        var trailGO = new GameObject("RiftTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.1f, 0.9f, 0.8f), new Color(0.1f, 0.2f, 0.8f, 0.6f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = 0f;
        tm.loop = true;
        tm.maxParticles = 25;

        var te = trailPS.emission;
        te.rateOverTime = 20;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.08f;

        var tMat = new Material(ShaderCache.ParticleShader);
        tMat.SetColor("_Color", new Color(0.3f, 0.1f, 0.9f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        float dmg = damage; float rad = radius; float dur = duration; bool ch = charged;
        var zp = go.AddComponent<ZoneProjectile>();
        zp.Init(dir, 14f, 0f, 18f, new Color(0.3f, 0.1f, 0.8f),
            (pos) => SpawnZone(pos, dmg, rad, dur, ch));

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnZone(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        center.y = 0.05f;

        var zoneGO = new GameObject("RiftZone");
        zoneGO.transform.position = center;

        // Dark purple-blue disc on the ground
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(zoneGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2, 0.03f, radius * 2);
        disc.GetComponent<Renderer>().material = ShaderCache.NewPlayerZone(new Color(0.4f, 0.15f, 0.8f), 0.5f, 0.3f, 2f, 0.3f);

        // Rotating transparent sphere inside (warped visual)
        var warpSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(warpSphere.GetComponent<SphereCollider>());
        warpSphere.transform.SetParent(zoneGO.transform, false);
        warpSphere.transform.localPosition = Vector3.up * 0.5f;
        warpSphere.transform.localScale = Vector3.one * radius * 1.2f;
        warpSphere.GetComponent<Renderer>().material = ShaderCache.NewMagic(
            new Color(0.2f, 0.05f, 0.5f), 1.5f);

        // Pulsing purple-blue particles
        var particleGO = new GameObject("RiftParticles");
        particleGO.transform.SetParent(zoneGO.transform, false);
        particleGO.transform.localPosition = Vector3.up * 0.3f;
        var ps = particleGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.1f, 0.8f, 0.7f),
            new Color(0.1f, 0.15f, 0.6f, 0.5f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.1f;
        main.duration = duration;
        main.loop = true;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 15;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.8f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var pulseCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.6f, 0.6f),
            new Keyframe(0.8f, 0.9f),
            new Keyframe(1f, 0f));
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, pulseCurve);

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.3f, 0.1f, 0.9f), 0f),
                new GradientColorKey(new Color(0.15f, 0.1f, 0.7f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.05f, 0.5f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOL.color = grad;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 1f;
        noise.scrollSpeed = 0.5f;

        var psMat = new Material(ShaderCache.ParticleShader);
        psMat.SetColor("_Color", new Color(0.3f, 0.1f, 0.8f));
        particleGO.GetComponent<ParticleSystemRenderer>().material = psMat;
        ps.Play();

        // Trigger collider
        var zoneCol = zoneGO.AddComponent<SphereCollider>();
        zoneCol.isTrigger = true;
        zoneCol.radius = radius;

        var zoneRb = zoneGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        zoneGO.AddComponent<RiftZone>().Init(damage, radius, duration, warpSphere.transform,
            disc.GetComponent<Renderer>());

        Object.Destroy(zoneGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

/// <summary>Rift zone: slows enemies and deals DoT.</summary>
public class RiftZone : MonoBehaviour
{
    float _dps;
    float _radius;
    float _duration;
    float _tickTimer;
    float _rotateSpeed = 30f;
    Transform _warpSphere;
    Renderer _discRenderer;
    float _pulseTimer;
    ElementType _spellElement = ElementType.Fire;

    public void Init(float damage, float radius, float duration, Transform warpSphere, Renderer discRenderer)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dps = damage;
        _radius = radius;
        _duration = duration;
        _warpSphere = warpSphere;
        _discRenderer = discRenderer;
    }

    void Update()
    {
        // Rotate the warp sphere slowly
        if (_warpSphere != null)
            _warpSphere.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime);

        // Pulse the disc
        if (_discRenderer != null)
        {
            _pulseTimer += Time.deltaTime * 2f;
            float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            Color darkPurple = new Color(0.1f, 0.02f, 0.3f);
            Color brightPurple = new Color(0.3f, 0.1f, 0.6f);
            Color current = Color.Lerp(darkPurple, brightPurple, pulse);
            _discRenderer.material.SetColor("_EmissionColor", current * Mathf.Lerp(1f, 3f, pulse));
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Slow enemies (reuse magma slow for 30% speed)
        hp.ApplyMagmaSlow();

        // DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps, _spellElement);
    }
}
