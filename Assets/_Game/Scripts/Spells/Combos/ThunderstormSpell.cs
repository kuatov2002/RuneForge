using UnityEngine;

/// <summary>
/// Lightning + Fire = Thunderstorm
/// Projectile that creates a storm zone. Over its duration, random lightning
/// bolts strike within radius dealing damage and stunning enemies.
/// </summary>
public static class ThunderstormSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; duration *= 1.4f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        // Create projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ThunderstormProjectile";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.8f, 0.2f), 5f);

        // Trail
        var trail = go.AddComponent<TrailRenderer>();
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;
        trail.time = 0.2f;
        trail.material = ShaderCache.NewEmissive(new Color(1f, 0.7f, 0.1f), 3f);
        trail.startColor = new Color(1f, 0.8f, 0.2f);
        trail.endColor = new Color(1f, 0.5f, 0f, 0f);

        float boltInterval = charged ? 0.4f : 0.6f;

        go.AddComponent<ZoneProjectile>().Init(dir, 14f, 0f, 18f, new Color(1f, 0.8f, 0.2f),
            (impactPos) => { SpawnThunderstormZone(impactPos, damage, radius, duration, boltInterval); });

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnThunderstormZone(Vector3 pos, float damage, float radius, float duration, float boltInterval)
    {
        pos.y = 0;

        var zoneGO = new GameObject("ThunderstormZone");
        zoneGO.transform.position = pos;

        // Dark cloud disc overhead
        var cloud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(cloud.GetComponent<CapsuleCollider>());
        cloud.transform.SetParent(zoneGO.transform, false);
        cloud.transform.localPosition = Vector3.up * 5f;
        cloud.transform.localScale = new Vector3(radius * 2.2f, 0.15f, radius * 2.2f);
        cloud.GetComponent<Renderer>().material = ShaderCache.NewMagic(new Color(0.2f, 0.2f, 0.25f), 0.5f);

        // Cloud particle system for billowing effect
        var cloudParticlesGO = new GameObject("StormCloudParticles");
        cloudParticlesGO.transform.SetParent(zoneGO.transform, false);
        cloudParticlesGO.transform.localPosition = Vector3.up * 5f;
        var cloudPS = cloudParticlesGO.AddComponent<ParticleSystem>();
        cloudPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var cloudMain = cloudPS.main;
        cloudMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        cloudMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        cloudMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        cloudMain.startColor = new Color(0.25f, 0.25f, 0.3f, 0.15f);
        cloudMain.simulationSpace = ParticleSystemSimulationSpace.World;
        cloudMain.gravityModifier = 0f;
        cloudMain.duration = duration;
        cloudMain.loop = true;

        var cloudEmission = cloudPS.emission;
        cloudEmission.rateOverTime = 8;

        var cloudShape = cloudPS.shape;
        cloudShape.shapeType = ParticleSystemShapeType.Circle;
        cloudShape.radius = radius * 0.8f;

        var cloudNoise = cloudPS.noise;
        cloudNoise.enabled = true;
        cloudNoise.strength = 0.3f;
        cloudNoise.frequency = 0.8f;

        var cloudMat = new Material(ShaderCache.ParticleShader);
        cloudMat.SetColor("_Color", new Color(0.25f, 0.25f, 0.3f, 0.2f));
        cloudParticlesGO.GetComponent<ParticleSystemRenderer>().material = cloudMat;
        cloudPS.Play();

        // Zone behavior
        zoneGO.AddComponent<ThunderstormZone>().Init(pos, damage, radius, duration, boltInterval);

        Object.Destroy(zoneGO, duration + 0.5f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }
}

public class ThunderstormZone : MonoBehaviour
{
    Vector3 _center;
    float _damage;
    float _radius;
    float _duration;
    float _boltInterval;
    float _boltTimer;
    float _elapsed;
    ElementType _spellElement = ElementType.Fire;

    public void Init(Vector3 center, float damage, float radius, float duration, float boltInterval)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _center = center;
        _damage = damage;
        _radius = radius;
        _duration = duration;
        _boltInterval = boltInterval;
        _boltTimer = 0.2f; // First bolt comes quickly
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= _duration) return;

        _boltTimer -= Time.deltaTime;
        if (_boltTimer > 0) return;
        _boltTimer = _boltInterval;

        // Random position within radius
        Vector2 rnd = Random.insideUnitCircle * _radius;
        Vector3 strikePos = _center + new Vector3(rnd.x, 0, rnd.y);

        // Bolt VFX: vertical cylinder flash from y=8 to y=0
        var boltGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(boltGO.GetComponent<CapsuleCollider>());
        boltGO.name = "ThunderstormBolt";
        boltGO.transform.position = strikePos + Vector3.up * 4f;
        boltGO.transform.localScale = new Vector3(0.15f, 4f, 0.15f);
        boltGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 1f, 0.3f), 10f);
        Object.Destroy(boltGO, 0.1f);

        // Ground impact disc
        var impactGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(impactGO.GetComponent<CapsuleCollider>());
        impactGO.name = "ThunderstormImpact";
        impactGO.transform.position = strikePos + Vector3.up * 0.02f;
        impactGO.transform.localScale = new Vector3(1f, 0.02f, 1f);
        impactGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 1f, 0.4f), 6f);
        Object.Destroy(impactGO, 0.2f);

        // Spark particles at strike point
        var sparkGO = new GameObject("ThunderstormSparks");
        sparkGO.transform.position = strikePos + Vector3.up * 0.2f;
        var ps = sparkGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 0.3f), new Color(0.8f, 0.8f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f;
        main.duration = 0.1f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var sparkMat = new Material(ShaderCache.ParticleShader);
        sparkMat.SetColor("_Color", new Color(1f, 1f, 0.3f));
        sparkGO.GetComponent<ParticleSystemRenderer>().material = sparkMat;
        ps.Play();
        Object.Destroy(sparkGO, 0.4f);

        // AoE damage at strike point
        float subRadius = 1.5f;
        Collider[] hits = Physics.OverlapSphere(strikePos, subRadius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(_damage, _spellElement);
                hp.ApplyStun(0.3f);
            }
        }

        // Screen shake per bolt
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, strikePos);
    }
}
