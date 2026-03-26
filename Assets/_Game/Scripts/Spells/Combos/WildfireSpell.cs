using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fire + Air = Wildfire
/// Fires a fireball at cursor. On hit, spawns a chain fireball that visually
/// flies to the next nearest enemy, then the next, up to 3 targets total.
/// Each chain is a visible projectile with travel time.
/// </summary>
public static class WildfireSpell
{
    public static void Cast(Vector3 center, float damage, float chainRadius, bool charged)
    {
        if (charged) { chainRadius *= 1.3f; damage *= 1.2f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        int maxTargets = charged ? 4 : 3;

        // Create initial fireball projectile
        CreateWildfireBolt(origin + dir * 0.6f, dir, damage, chainRadius, maxTargets);

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    public static void CreateWildfireBolt(Vector3 pos, Vector3 dir, float damage, float chainRadius, int chainsLeft)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "WildfireBolt";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.1f), 5f);

        // Fire particle trail
        var trailGO = new GameObject("WildfireTrail");
        trailGO.transform.SetParent(go.transform, false);
        var ps = trailGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.1f), new Color(1f, 0.2f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.1f;
        main.loop = true;

        var em = ps.emission;
        em.rateOverTime = 20;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.08f;

        var sOL = ps.sizeOverLifetime;
        sOL.enabled = true;
        sOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();

        // Trail renderer
        var trail = go.AddComponent<TrailRenderer>();
        trail.startWidth = 0.15f;
        trail.endWidth = 0f;
        trail.time = 0.15f;
        trail.material = ShaderCache.NewEmissive(new Color(1f, 0.4f, 0f), 3f);
        trail.startColor = new Color(1f, 0.5f, 0.1f);
        trail.endColor = new Color(1f, 0.2f, 0f, 0f);

        var wb = go.AddComponent<WildfireBoltBehavior>();
        wb.Init(dir, damage, chainRadius, chainsLeft);
    }
}

/// <summary>
/// Wildfire bolt: flies forward, on enemy hit deals damage and spawns
/// a new bolt targeting the nearest un-hit enemy. Visual chain effect.
/// </summary>
public class WildfireBoltBehavior : MonoBehaviour
{
    Vector3 _dir;
    float _speed = 16f;
    float _damage;
    float _chainRadius;
    int _chainsLeft;
    float _maxDist = 15f;
    Vector3 _startPos;
    bool _hit;
    Transform _homingTarget;
    float _homingSpeed = 20f;

    // Track all targets hit across the chain
    static HashSet<int> _hitIds = new();
    static float _lastCastTime;

    public void Init(Vector3 dir, float damage, float chainRadius, int chainsLeft)
    {
        _dir = dir.normalized;
        _damage = damage;
        _chainRadius = chainRadius;
        _chainsLeft = chainsLeft;
        _startPos = transform.position;

        // Reset hit tracking if this is a new cast (not a chain)
        if (Time.time - _lastCastTime > 0.5f)
            _hitIds.Clear();
        _lastCastTime = Time.time;
    }

    /// <summary>For chain bolts: home toward a specific target.</summary>
    public void SetHomingTarget(Transform target)
    {
        _homingTarget = target;
        _homingSpeed = 22f;
    }

    void Update()
    {
        if (_hit) return;

        // Homing toward chain target
        if (_homingTarget != null && _homingTarget.gameObject.activeSelf)
        {
            Vector3 toTarget = (_homingTarget.position + Vector3.up * 0.5f - transform.position);
            if (toTarget.magnitude < 0.3f)
            {
                // Arrived at target
                HitTarget(_homingTarget.GetComponent<Health>(), _homingTarget);
                return;
            }
            _dir = toTarget.normalized;
            transform.position += _dir * _homingSpeed * Time.deltaTime;
        }
        else
        {
            transform.position += _dir * _speed * Time.deltaTime;
        }

        if (Vector3.Distance(_startPos, transform.position) > _maxDist)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_hit) return;
        if (_homingTarget != null) return; // homing bolts don't use trigger
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<WildfireBoltBehavior>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;
        if (other.GetComponent<FireballProjectile>() != null) return;

        // Fire zone interactions
        if (SpellInteractionSystem.TryFireInteraction(other, transform.position, _damage))
        { _hit = true; SpawnHitBurst(transform.position); Destroy(gameObject); return; }

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            HitTarget(hp, other.transform);
            return;
        }

        // Hit wall — just explode
        if (hp == null && other.GetComponent<Health>() == null)
        {
            SpawnHitBurst(transform.position);
            _hit = true;
            Destroy(gameObject);
        }
    }

    void HitTarget(Health hp, Transform target)
    {
        if (hp == null || hp.IsDead) { Destroy(gameObject); return; }

        _hit = true;
        int id = target.GetInstanceID();

        if (!_hitIds.Contains(id))
        {
            hp.TakeDamage(_damage);
            _hitIds.Add(id);
        }

        SpawnHitBurst(target.position);
        SFXSystem.Play(SFXSystem.SFXType.Hit, target.position);

        // Chain to next target
        if (_chainsLeft > 1)
        {
            Transform nextTarget = FindNextTarget(target);
            if (nextTarget != null)
            {
                // Spawn a new bolt that homes toward the next target
                WildfireSpell.CreateWildfireBolt(
                    target.position + Vector3.up * 0.5f,
                    (nextTarget.position - target.position).normalized,
                    _damage * 0.8f,
                    _chainRadius,
                    _chainsLeft - 1);

                // Make the new bolt home toward the target
                var newBolts = Object.FindObjectsByType<WildfireBoltBehavior>(FindObjectsSortMode.None);
                foreach (var b in newBolts)
                {
                    if (b != this && b._homingTarget == null && !b._hit)
                    {
                        b.SetHomingTarget(nextTarget);
                        break;
                    }
                }
            }
        }

        Destroy(gameObject);
    }

    Transform FindNextTarget(Transform current)
    {
        Collider[] nearby = Physics.OverlapSphere(current.position, _chainRadius);
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var c in nearby)
        {
            if (c.transform == current) continue;
            if (c.GetComponent<PlayerController>() != null) continue;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;
            if (_hitIds.Contains(c.transform.GetInstanceID())) continue;

            float d = Vector3.Distance(current.position, c.transform.position);
            if (d < bestDist) { bestDist = d; best = c.transform; }
        }
        return best;
    }

    static void SpawnHitBurst(Vector3 pos)
    {
        var burstGO = new GameObject("WildfireHitBurst");
        burstGO.transform.position = pos + Vector3.up * 0.5f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.1f), new Color(1f, 0.2f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.2f;
        main.duration = 0.1f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.5f);
    }
}
