using UnityEngine;

/// <summary>
/// Void + Poison = Corruption
/// Projectile that spawns a debuff zone. Enemies inside take amplified DoT (1.3x DPS).
/// Initial burst damage on impact. Enemies get purple tint while in zone.
/// </summary>
public static class CorruptionSpell
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

        // Dark purple-green projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CorruptionBolt";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.35f, 0.15f, 0.6f), 4f);

        // Trail particles: dark purple-green wisps
        var trailGO = new GameObject("CorruptionTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.15f, 0.7f, 0.8f), new Color(0.2f, 0.5f, 0.15f, 0.6f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = -0.05f;
        tm.loop = true;
        tm.maxParticles = 25;

        var te = trailPS.emission;
        te.rateOverTime = 20;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.08f;

        var tSizeOL = trailPS.sizeOverLifetime;
        tSizeOL.enabled = true;
        tSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var tMat = new Material(ShaderCache.ParticleShader);
        tMat.SetColor("_Color", new Color(0.4f, 0.2f, 0.6f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        float dmg = damage; float rad = radius; float dur = duration; bool ch = charged;
        var zp = go.AddComponent<ZoneProjectile>();
        zp.Init(dir, 14f, 0f, 16f, new Color(0.35f, 0.15f, 0.6f),
            (pos) => SpawnZone(pos, dmg, rad, dur, ch));

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnZone(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        center.y = 0.05f;

        // Initial burst damage
        Collider[] burstHits = Physics.OverlapSphere(center, radius);
        foreach (var h in burstHits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 0.5f);
        }

        var zoneGO = new GameObject("CorruptionZone");
        zoneGO.transform.position = center;

        // ── VFX: dark purple-green disc on ground ──
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(zoneGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2, 0.04f, radius * 2);
        disc.GetComponent<Renderer>().material = ShaderCache.NewMagic(
            new Color(0.25f, 0.1f, 0.4f), 2f);

        // Corrupt particles rising from the ground
        var risingGO = new GameObject("CorruptionRisingParticles");
        risingGO.transform.SetParent(zoneGO.transform, false);
        risingGO.transform.localPosition = Vector3.up * 0.1f;
        var risingPS = risingGO.AddComponent<ParticleSystem>();
        risingPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var rm = risingPS.main;
        rm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        rm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        rm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        rm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.12f, 0.7f, 0.7f),
            new Color(0.15f, 0.5f, 0.15f, 0.5f));
        rm.simulationSpace = ParticleSystemSimulationSpace.World;
        rm.gravityModifier = -0.2f;
        rm.duration = duration;
        rm.loop = true;
        rm.maxParticles = 40;

        var re = risingPS.emission;
        re.rateOverTime = 15;

        var rs = risingPS.shape;
        rs.shapeType = ParticleSystemShapeType.Circle;
        rs.radius = radius * 0.8f;

        var rSizeOL = risingPS.sizeOverLifetime;
        rSizeOL.enabled = true;
        rSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var rColorOL = risingPS.colorOverLifetime;
        rColorOL.enabled = true;
        var rGrad = new Gradient();
        rGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.4f, 0.15f, 0.7f), 0f),
                new GradientColorKey(new Color(0.25f, 0.4f, 0.15f), 0.5f),
                new GradientColorKey(new Color(0.3f, 0.1f, 0.5f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        rColorOL.color = rGrad;

        var rNoise = risingPS.noise;
        rNoise.enabled = true;
        rNoise.strength = 0.3f;
        rNoise.frequency = 1.2f;
        rNoise.scrollSpeed = 0.5f;

        var rMat = new Material(ShaderCache.ParticleShader);
        rMat.SetColor("_Color", new Color(0.4f, 0.2f, 0.6f));
        risingGO.GetComponent<ParticleSystemRenderer>().material = rMat;
        risingPS.Play();

        // Low-lying toxic mist particles
        var mistGO = new GameObject("CorruptionMist");
        mistGO.transform.SetParent(zoneGO.transform, false);
        mistGO.transform.localPosition = Vector3.up * 0.05f;
        var mistPS = mistGO.AddComponent<ParticleSystem>();
        mistPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var mm = mistPS.main;
        mm.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        mm.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        mm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        mm.startColor = new Color(0.2f, 0.4f, 0.1f, 0.08f);
        mm.simulationSpace = ParticleSystemSimulationSpace.World;
        mm.gravityModifier = 0f;
        mm.duration = duration;
        mm.loop = true;

        var me = mistPS.emission;
        me.rateOverTime = 5;

        var ms = mistPS.shape;
        ms.shapeType = ParticleSystemShapeType.Circle;
        ms.radius = radius * 0.7f;

        var mNoise = mistPS.noise;
        mNoise.enabled = true;
        mNoise.strength = 0.2f;
        mNoise.frequency = 0.8f;

        var mColorOL = mistPS.colorOverLifetime;
        mColorOL.enabled = true;
        var mGrad = new Gradient();
        mGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.2f, 0.4f, 0.1f), 0f),
                new GradientColorKey(new Color(0.3f, 0.15f, 0.5f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.1f, 0.3f),
                new GradientAlphaKey(0.06f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        mColorOL.color = mGrad;

        var mMat = new Material(ShaderCache.ParticleShader);
        mMat.SetColor("_Color", new Color(0.2f, 0.4f, 0.1f, 0.1f));
        mistGO.GetComponent<ParticleSystemRenderer>().material = mMat;
        mistPS.Play();

        // Impact burst VFX
        var burstGO = new GameObject("CorruptionImpactBurst");
        burstGO.transform.position = center + Vector3.up * 0.3f;
        var burstPS = burstGO.AddComponent<ParticleSystem>();
        burstPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var bm = burstPS.main;
        bm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        bm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        bm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        bm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.15f, 0.7f), new Color(0.15f, 0.5f, 0.1f));
        bm.simulationSpace = ParticleSystemSimulationSpace.World;
        bm.gravityModifier = 0.2f;
        bm.duration = 0.15f;
        bm.loop = false;

        var be = burstPS.emission;
        be.rateOverTime = 0;
        be.SetBursts(new[] { new ParticleSystem.Burst(0f, charged ? 30 : 20) });

        var bs = burstPS.shape;
        bs.shapeType = ParticleSystemShapeType.Sphere;
        bs.radius = 0.3f;

        var bSizeOL = burstPS.sizeOverLifetime;
        bSizeOL.enabled = true;
        bSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        burstGO.GetComponent<ParticleSystemRenderer>().material = rMat;
        burstPS.Play();
        Object.Destroy(burstGO, 0.6f);

        // Trigger collider
        var zonCol = zoneGO.AddComponent<SphereCollider>();
        zonCol.isTrigger = true;
        zonCol.radius = radius;

        var zonRb = zoneGO.AddComponent<Rigidbody>();
        zonRb.isKinematic = true;
        zonRb.useGravity = false;

        // Zone behavior: enemies take amplified DoT (1.3x base DPS)
        float baseDps = damage / Mathf.Max(duration, 1f);
        float amplifiedDps = baseDps * 1.3f;
        zoneGO.AddComponent<CorruptionZone>().Init(amplifiedDps, radius, disc.GetComponent<Renderer>());

        Object.Destroy(zoneGO, duration);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }
}

/// <summary>Corruption zone: enemies inside take amplified DoT and get purple tint.</summary>
public class CorruptionZone : MonoBehaviour
{
    float _dps;
    float _radius;
    float _tickTimer;
    float _pulseTimer;
    Renderer _discRenderer;

    public void Init(float dps, float radius, Renderer discRenderer)
    {
        _dps = dps;
        _radius = radius;
        _discRenderer = discRenderer;
    }

    void Update()
    {
        // Pulse the disc between dark purple and sickly green
        if (_discRenderer != null)
        {
            _pulseTimer += Time.deltaTime * 1.5f;
            float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            Color darkPurple = new Color(0.2f, 0.05f, 0.4f);
            Color sickGreen = new Color(0.15f, 0.35f, 0.1f);
            Color current = Color.Lerp(darkPurple, sickGreen, pulse);
            _discRenderer.material.SetColor("_EmissionColor", current * Mathf.Lerp(1.5f, 3f, pulse));
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Apply purple tint to enemy while in zone
        var renderers = other.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r != null)
                r.material.SetColor("_EmissionColor", new Color(0.3f, 0.05f, 0.5f) * 1.5f);
        }

        // DoT tick (amplified 1.3x)
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps * 0.5f); // Half DPS per tick at 0.5s intervals
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;

        // Remove purple tint when leaving zone
        var renderers = other.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r != null)
                r.material.SetColor("_EmissionColor", Color.black);
        }
    }
}
