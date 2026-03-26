using UnityEngine;

/// <summary>
/// Void + Fire = Implode
/// Projectile that creates a vortex pulling enemies in, then explodes outward.
/// </summary>
public static class ImplodeSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        if (charged) radius *= 1.3f;

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) { SpawnVortex(center, damage, radius, charged); return; }

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        // Dark purple-red projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ImplodeProjectile";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.5f, 0f, 0.3f), 5f);

        // Trail particles
        var trailGO = new GameObject("ImplodeTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0f, 0.4f), new Color(0.8f, 0.1f, 0.2f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = 0f;
        tm.loop = true;
        tm.maxParticles = 30;

        var te = trailPS.emission;
        te.rateOverTime = 25;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.1f;

        var tSizeOL = trailPS.sizeOverLifetime;
        tSizeOL.enabled = true;
        tSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var tMat = new Material(Shader.Find("Particles/Standard Unlit"));
        tMat.SetColor("_Color", new Color(0.6f, 0f, 0.4f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        float dmg = damage; float rad = radius; bool ch = charged;
        var zp = go.AddComponent<ZoneProjectile>();
        zp.Init(dir, 15f, 0f, 18f, new Color(0.5f, 0f, 0.3f),
            (pos) => SpawnVortex(pos, dmg, rad, ch));

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnVortex(Vector3 center, float damage, float radius, bool charged)
    {
        center.y = 0.05f;

        var vortexGO = new GameObject("ImplodeVortex");
        vortexGO.transform.position = center;
        vortexGO.AddComponent<ImplodeVortex>().Init(center, damage, radius, charged);

        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

/// <summary>Pull phase then explosion.</summary>
public class ImplodeVortex : MonoBehaviour
{
    Vector3 _center;
    float _damage;
    float _radius;
    bool _charged;
    float _pullDuration = 0.5f;
    float _timer;
    bool _exploded;

    // VFX references
    ParticleSystem _swirlPS;
    GameObject _coreGO;

    public void Init(Vector3 center, float damage, float radius, bool charged)
    {
        _center = center;
        _damage = damage;
        _radius = radius;
        _charged = charged;

        // Dark core sphere
        _coreGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(_coreGO.GetComponent<SphereCollider>());
        _coreGO.transform.SetParent(transform, false);
        _coreGO.transform.localPosition = Vector3.up * 0.5f;
        _coreGO.transform.localScale = Vector3.one * 0.4f;
        _coreGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.2f, 0f, 0.15f), 2f);

        // Swirling dark particles during pull
        var swirlGO = new GameObject("ImplodeSwirlParticles");
        swirlGO.transform.SetParent(transform, false);
        swirlGO.transform.localPosition = Vector3.up * 0.5f;
        _swirlPS = swirlGO.AddComponent<ParticleSystem>();
        _swirlPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var sm = _swirlPS.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(-4f, -8f); // Inward
        sm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        sm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0f, 0.3f, 0.8f),
            new Color(0.6f, 0f, 0.2f, 0.6f));
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.gravityModifier = 0f;
        sm.loop = true;
        sm.maxParticles = 60;

        var se = _swirlPS.emission;
        se.rateOverTime = 50;

        var ss = _swirlPS.shape;
        ss.shapeType = ParticleSystemShapeType.Sphere;
        ss.radius = _radius;

        var sSizeOL = _swirlPS.sizeOverLifetime;
        sSizeOL.enabled = true;
        sSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var sColorOL = _swirlPS.colorOverLifetime;
        sColorOL.enabled = true;
        var sGrad = new Gradient();
        sGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.5f, 0f, 0.3f), 0f),
                new GradientColorKey(new Color(0.8f, 0.2f, 0.4f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        sColorOL.color = sGrad;

        var sMat = new Material(Shader.Find("Particles/Standard Unlit"));
        sMat.SetColor("_Color", new Color(0.5f, 0f, 0.3f));
        swirlGO.GetComponent<ParticleSystemRenderer>().material = sMat;
        _swirlPS.Play();
    }

    void Update()
    {
        if (_exploded) return;

        _timer += Time.deltaTime;

        // Pull phase: pull enemies toward center
        float pullStrength = _charged ? 8f : 6f;
        Collider[] pullHits = Physics.OverlapSphere(_center, _radius);
        foreach (var h in pullHits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            Vector3 toCenter = (_center - h.transform.position);
            toCenter.y = 0;
            if (toCenter.sqrMagnitude > 0.1f)
            {
                h.transform.position += toCenter.normalized * pullStrength * Time.deltaTime;
            }
        }

        // Animate core pulsing
        if (_coreGO != null)
        {
            float pulse = (Mathf.Sin(_timer * 12f) + 1f) * 0.5f;
            float scale = Mathf.Lerp(0.3f, 0.6f, pulse);
            _coreGO.transform.localScale = Vector3.one * scale;
        }

        if (_timer >= _pullDuration)
        {
            Explode();
        }
    }

    void Explode()
    {
        _exploded = true;

        // Stop swirl particles
        if (_swirlPS != null) _swirlPS.Stop();

        // Burst damage + knockback outward
        Collider[] hits = Physics.OverlapSphere(_center, _radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(_damage);
                GameFeel.ApplyKnockback(h.transform, _center, _damage * 0.4f);
            }
        }

        // Red-purple explosion burst particles
        var burstGO = new GameObject("ImplodeExplosion");
        burstGO.transform.position = _center + Vector3.up * 0.5f;
        var burstPS = burstGO.AddComponent<ParticleSystem>();
        burstPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var bm = burstPS.main;
        bm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        bm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
        bm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        bm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.1f, 0.3f), new Color(0.6f, 0f, 0.8f));
        bm.simulationSpace = ParticleSystemSimulationSpace.World;
        bm.gravityModifier = 0.2f;
        bm.duration = 0.2f;
        bm.loop = false;

        var be = burstPS.emission;
        be.rateOverTime = 0;
        be.SetBursts(new[] { new ParticleSystem.Burst(0f, _charged ? 50 : 35) });

        var bs = burstPS.shape;
        bs.shapeType = ParticleSystemShapeType.Sphere;
        bs.radius = 0.3f;

        var bSizeOL = burstPS.sizeOverLifetime;
        bSizeOL.enabled = true;
        bSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var bColorOL = burstPS.colorOverLifetime;
        bColorOL.enabled = true;
        var bGrad = new Gradient();
        bGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.3f, 0.5f), 0f),
                new GradientColorKey(new Color(0.5f, 0f, 0.6f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0f, 0.3f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        bColorOL.color = bGrad;

        var bMat = new Material(Shader.Find("Particles/Standard Unlit"));
        bMat.SetColor("_Color", new Color(0.7f, 0.1f, 0.4f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = bMat;
        burstPS.Play();
        Object.Destroy(burstGO, 0.8f);

        // Bright flash at center
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = _center + Vector3.up * 0.5f;
        flash.transform.localScale = Vector3.one * 0.5f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(1f, 0.3f, 0.6f), 10f);
        flash.AddComponent<FlashShrink>().Init(0.25f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.35f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, _center);

        Object.Destroy(gameObject, 0.1f);
    }
}
