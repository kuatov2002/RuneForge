using UnityEngine;

/// <summary>
/// Void + Air = Vacuum
/// Projectile that spawns a persistent vortex pulling enemies toward center with DoT.
/// </summary>
public static class VacuumSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) radius *= 1.3f;

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) { SpawnVortex(center, damage, radius, duration, charged); return; }

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        // Purple-white projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "VacuumProjectile";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.6f, 0.3f, 0.9f), 5f);

        // Trail particles
        var trailGO = new GameObject("VacuumTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.4f, 1f, 0.8f), new Color(0.9f, 0.8f, 1f, 0.6f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = -0.1f;
        tm.loop = true;
        tm.maxParticles = 25;

        var te = trailPS.emission;
        te.rateOverTime = 20;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.08f;

        var tMat = new Material(ShaderCache.ParticleShader);
        tMat.SetColor("_Color", new Color(0.7f, 0.4f, 1f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        float dmg = damage; float rad = radius; float dur = duration; bool ch = charged;
        var zp = go.AddComponent<ZoneProjectile>();
        zp.Init(dir, 14f, 0f, 18f, new Color(0.6f, 0.3f, 0.9f),
            (pos) => SpawnVortex(pos, dmg, rad, dur, ch));

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnVortex(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        center.y = 0.05f;

        var vortexGO = new GameObject("VacuumVortex");
        vortexGO.transform.position = center;

        // Dark core sphere
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(core.GetComponent<SphereCollider>());
        core.transform.SetParent(vortexGO.transform, false);
        core.transform.localPosition = Vector3.up * 0.5f;
        core.transform.localScale = Vector3.one * 0.35f;
        core.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.15f, 0f, 0.2f), 2f);

        // Swirling particles spiraling inward
        var swirlGO = new GameObject("VacuumSwirlParticles");
        swirlGO.transform.SetParent(vortexGO.transform, false);
        swirlGO.transform.localPosition = Vector3.up * 0.5f;
        var swirlPS = swirlGO.AddComponent<ParticleSystem>();
        swirlPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var sm = swirlPS.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(-3f, -6f); // Inward
        sm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        sm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0.3f, 1f, 0.8f),
            new Color(0.8f, 0.6f, 1f, 0.6f));
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.gravityModifier = 0f;
        sm.duration = duration;
        sm.loop = true;
        sm.maxParticles = 60;

        var se = swirlPS.emission;
        se.rateOverTime = 35;

        var ss = swirlPS.shape;
        ss.shapeType = ParticleSystemShapeType.Sphere;
        ss.radius = radius;

        var sSizeOL = swirlPS.sizeOverLifetime;
        sSizeOL.enabled = true;
        sSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        var sColorOL = swirlPS.colorOverLifetime;
        sColorOL.enabled = true;
        var sGrad = new Gradient();
        sGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.7f, 0.4f, 1f), 0f),
                new GradientColorKey(new Color(0.5f, 0.2f, 0.8f), 0.5f),
                new GradientColorKey(new Color(0.9f, 0.7f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.5f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        sColorOL.color = sGrad;

        var sNoise = swirlPS.noise;
        sNoise.enabled = true;
        sNoise.strength = 0.4f;
        sNoise.frequency = 2f;
        sNoise.scrollSpeed = 1f;

        var sMat = new Material(ShaderCache.ParticleShader);
        sMat.SetColor("_Color", new Color(0.6f, 0.3f, 1f));
        swirlGO.GetComponent<ParticleSystemRenderer>().material = sMat;
        swirlPS.Play();

        // Ground disc indicator
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(vortexGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2, 0.02f, radius * 2);
        disc.GetComponent<Renderer>().material = ShaderCache.NewPlayerZone(new Color(0.5f, 0.2f, 0.8f), 0.5f, 0.3f, 2f, 0.3f);

        // Trigger collider
        var zoneCol = vortexGO.AddComponent<SphereCollider>();
        zoneCol.isTrigger = true;
        zoneCol.radius = radius;

        var zoneRb = vortexGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        vortexGO.AddComponent<VacuumVortexZone>().Init(damage, radius, duration, charged,
            core.transform);

        Object.Destroy(vortexGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

/// <summary>Persistent vortex pulling enemies toward center with DoT.</summary>
public class VacuumVortexZone : MonoBehaviour
{
    float _dps;
    float _radius;
    float _duration;
    bool _charged;
    float _tickTimer;
    float _pullStrength;
    Transform _core;
    float _pulseTimer;
    ElementType _spellElement = ElementType.Fire;

    public void Init(float damage, float radius, float duration, bool charged, Transform core)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dps = damage;
        _radius = radius;
        _duration = duration;
        _charged = charged;
        _pullStrength = charged ? 10f : 8f;
        _core = core;
    }

    void Update()
    {
        // Pulse the core
        if (_core != null)
        {
            _pulseTimer += Time.deltaTime * 6f;
            float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            float scale = Mathf.Lerp(0.25f, 0.45f, pulse);
            _core.localScale = Vector3.one * scale;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Pull toward center
        Vector3 toCenter = (transform.position - other.transform.position);
        toCenter.y = 0;
        float dist = toCenter.magnitude;
        if (toCenter.sqrMagnitude > 0.1f)
        {
            other.transform.position += toCenter.normalized * _pullStrength * Time.deltaTime;
        }

        // Enemies near center get Exposed (Void status)
        if (dist < 1.5f)
        {
            var elemStatus = hp.GetComponent<ElementalStatus>();
            if (elemStatus != null)
                elemStatus.ApplyElement(ElementType.Void, 0);
        }

        // DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps, _spellElement);
    }
}
