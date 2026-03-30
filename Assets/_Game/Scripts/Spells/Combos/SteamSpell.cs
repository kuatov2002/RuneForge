using UnityEngine;

/// <summary>
/// Fire + Water = Steam
/// Cloud of steam dealing small DoT. If hit by a Fire combo, the cloud explodes.
/// Balance: radius 2 (was 3), damage 2 (was 3)
/// </summary>
public static class SteamSpell
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

        // Steam puff sphere
        var projGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(projGO.GetComponent<SphereCollider>());
        projGO.name = "SteamProjectile";
        projGO.transform.position = origin;
        projGO.transform.localScale = Vector3.one * 0.2f;
        projGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.9f, 0.9f, 0.95f), 2f);

        var col = projGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;

        var rb = projGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        float dmg = damage; float rad = radius; float dur = duration; bool ch = charged;
        var zp = projGO.AddComponent<ZoneProjectile>();
        zp.Init(dir, 16f, 0f, 16f, new Color(0.9f, 0.9f, 0.95f), (pos) => SpawnZone(pos, dmg, rad, dur, ch));
    }

    public static void SpawnZone(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        var cloudGO = new GameObject("SteamCloud");
        cloudGO.transform.position = center;

        // Trigger collider for the cloud zone
        var col = cloudGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;

        var rb = cloudGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var cloud = cloudGO.AddComponent<SteamCloudZone>();
        cloud.Init(damage, radius, duration, charged);

        // ── VFX: billowing steam particle system ──
        var steamVFXGO = new GameObject("SteamParticles");
        steamVFXGO.transform.SetParent(cloudGO.transform, false);
        steamVFXGO.transform.localPosition = Vector3.up * 0.5f;
        var ps = steamVFXGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.85f, 0.9f, 0.25f),
            new Color(0.9f, 0.9f, 0.95f, 0.15f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.15f; // drift upward slowly
        main.duration = duration;
        main.loop = true;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 10;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius * 0.5f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.9f, 0.9f, 0.95f), 0f),
                new GradientColorKey(new Color(0.8f, 0.8f, 0.85f), 0.5f),
                new GradientColorKey(new Color(0.7f, 0.7f, 0.75f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.25f, 0.2f),
                new GradientAlphaKey(0.2f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        // Noise for billowing movement
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 0.5f;
        noise.octaveCount = 2;

        var psMat = new Material(ShaderCache.ParticleShader);
        psMat.SetColor("_Color", new Color(0.9f, 0.9f, 0.95f, 0.3f));
        steamVFXGO.GetComponent<ParticleSystemRenderer>().material = psMat;
        ps.Play();

        // Subtle white-ish glow core
        var coreGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(coreGlow.GetComponent<SphereCollider>());
        coreGlow.transform.SetParent(cloudGO.transform, false);
        coreGlow.transform.localPosition = Vector3.up * 0.5f;
        coreGlow.transform.localScale = Vector3.one * radius * 0.6f;
        coreGlow.GetComponent<Renderer>().material = ShaderCache.NewMagic(
            new Color(0.9f, 0.9f, 0.95f), 1f);

        Object.Destroy(cloudGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

public class SteamCloudZone : MonoBehaviour
{
    float _dps;
    float _radius;
    float _duration;
    bool _charged;
    float _tickTimer;
    float _explosionDamage;
    ElementType _spellElement = ElementType.Fire;

    public void Init(float dps, float radius, float duration, bool charged)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dps = dps;
        _radius = radius;
        _duration = duration;
        _charged = charged;
        _explosionDamage = dps * 3f;
    }

    void OnTriggerStay(Collider other)
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;

        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            hp.TakeDamage(_dps, _spellElement);
    }

    /// <summary>Called when a Fire spell hits the steam cloud.</summary>
    public void Ignite()
    {
        Vector3 pos = transform.position;
        float explodeRadius = _radius * 1.5f;

        Collider[] hits = Physics.OverlapSphere(pos, explodeRadius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(_explosionDamage, _spellElement);
        }

        // Explosion VFX: orange-red particle burst replacing steam
        var explodeGO = new GameObject("SteamIgnite");
        explodeGO.transform.position = pos + Vector3.up * 0.5f;
        var ps = explodeGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.5f, 0.1f),
            new Color(1f, 0.2f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;
        main.duration = 0.3f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0f), 0.5f),
                new GradientColorKey(new Color(0.3f, 0.05f, 0f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOL.color = grad;

        var explodeMat = new Material(ShaderCache.ParticleShader);
        explodeMat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        explodeMat.SetFloat("_Mode", 1);
        explodeGO.GetComponent<ParticleSystemRenderer>().material = explodeMat;
        ps.Play();

        // Flash core
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(core.GetComponent<SphereCollider>());
        core.transform.position = pos + Vector3.up * 0.5f;
        core.transform.localScale = Vector3.one * 0.5f;
        core.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.8f, 0.3f), 8f);
        core.AddComponent<FlashShrink>().Init(0.2f);

        Object.Destroy(explodeGO, 0.8f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.3f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);

        Destroy(gameObject);
    }
}
