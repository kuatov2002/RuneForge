using UnityEngine;

/// <summary>
/// Fire + Fire = Fireball
/// Launches a large flaming projectile that explodes on contact with an enemy.
/// </summary>
public static class InfernoSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        // center = cursor target, but we fire FROM the player
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        float speed = charged ? 16f : 20f;
        float size = charged ? 0.45f : 0.3f;
        float explodeRadius = charged ? radius * 1.4f : radius;

        // Create fireball projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Fireball";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * size;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.1f), 5f);

        // Fire particle trail on the fireball
        var trailGO = new GameObject("FireballTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.1f), new Color(1f, 0.2f, 0f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = -0.15f;
        tm.loop = true;
        tm.maxParticles = 40;

        var te = trailPS.emission;
        te.rateOverTime = 30;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = size * 0.5f;

        var tSize = trailPS.sizeOverLifetime;
        tSize.enabled = true;
        tSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var tMat = new Material(ShaderCache.ParticleShader);
        tMat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        // Also add a TrailRenderer for the streak
        var trail = go.AddComponent<TrailRenderer>();
        trail.startWidth = size * 1.5f;
        trail.endWidth = 0f;
        trail.time = 0.2f;
        trail.material = ShaderCache.NewEmissive(new Color(1f, 0.4f, 0f), 3f);
        trail.startColor = new Color(1f, 0.6f, 0.1f);
        trail.endColor = new Color(1f, 0.2f, 0f, 0f);

        // Add fireball behavior
        var fb = go.AddComponent<FireballProjectile>();
        fb.Init(dir, speed, damage, explodeRadius, charged);

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }
}

/// <summary>Fireball projectile that explodes on contact.</summary>
public class FireballProjectile : MonoBehaviour
{
    Vector3 _dir;
    float _speed;
    float _damage;
    float _radius;
    bool _charged;
    float _maxDist = 18f;
    Vector3 _startPos;
    bool _exploded;
    ElementType _spellElement = ElementType.Fire;

    public void Init(Vector3 dir, float speed, float damage, float radius, bool charged)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dir = dir;
        _speed = speed;
        _damage = damage;
        _radius = radius;
        _charged = charged;
        _startPos = transform.position;
    }

    void Update()
    {
        transform.position += _dir * _speed * Time.deltaTime;
        if (Vector3.Distance(_startPos, transform.position) > _maxDist)
        {
            Explode();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_exploded) return;
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<FireballProjectile>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;

        // Check spell zone interactions (Fire + Steam, Fire + Permafrost, etc.)
        if (SpellInteractionSystem.TryFireInteraction(other, transform.position, _damage))
        { Explode(); return; }

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            Explode();
        else if (hp == null)
            Explode(); // Hit wall
    }

    void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        Vector3 pos = transform.position;

        // AoE damage
        Collider[] hits = Physics.OverlapSphere(pos, _radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(_damage, _spellElement);
                GameFeel.ApplyKnockback(h.transform, pos, _damage * 0.3f);
            }
        }

        // Explosion VFX
        var burstGO = new GameObject("FireballExplosion");
        burstGO.transform.position = pos;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.5f, 0f), new Color(1f, 0.2f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;
        main.duration = 0.2f;
        main.loop = false;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, _charged ? 40 : 25) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.3f;

        var sOL = ps.sizeOverLifetime;
        sOL.enabled = true;
        sOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.8f);

        // Flash
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = pos;
        flash.transform.localScale = Vector3.one * 0.5f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.9f, 0.5f), 8f);
        flash.AddComponent<FlashShrink>().Init(0.2f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(_charged ? 0.4f : 0.25f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(_charged ? 0.06f : 0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);

        Destroy(gameObject);
    }
}
