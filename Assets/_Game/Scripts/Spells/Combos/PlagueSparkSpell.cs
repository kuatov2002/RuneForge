using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Poison + Lightning = Plague Spark
/// Launches a yellow-green bolt that chains poison to nearby enemies on impact.
/// Each chain deals reduced damage and applies a PoisonDoT component.
/// </summary>
public static class PlagueSparkSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        float finalDamage = charged ? damage * 1.3f : damage;
        float finalRadius = radius;
        int maxChains = charged ? 5 : 4;

        // Create yellow-green bolt projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "PlagueSpark";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.18f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.7f, 0.9f, 0.2f), 5f);

        // Electric trail particles
        var trailGO = new GameObject("PlagueSparkTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 1f, 0.3f), new Color(0.3f, 0.9f, 0.1f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = 0f;
        tm.loop = true;
        tm.maxParticles = 25;

        var te = trailPS.emission;
        te.rateOverTime = 25;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.06f;

        var tSizeOL = trailPS.sizeOverLifetime;
        tSizeOL.enabled = true;
        tSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var tMat = new Material(ShaderCache.ParticleShader);
        tMat.SetColor("_Color", new Color(0.7f, 1f, 0.3f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        go.AddComponent<PlagueSparkProjectile>().Init(dir, finalDamage, finalRadius, maxChains);

        Object.Destroy(go, 5f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    /// <summary>
    /// Spawn a green lightning bolt visual between two points (thin stretched cube, like LightningStrike).
    /// </summary>
    public static void SpawnChainBolt(Vector3 from, Vector3 to)
    {
        Vector3 mid = (from + to) * 0.5f;
        float dist = Vector3.Distance(from, to);

        var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(bolt.GetComponent<BoxCollider>());
        bolt.name = "PlagueChainBolt";
        bolt.transform.position = mid;
        bolt.transform.rotation = Quaternion.LookRotation(to - from);
        bolt.transform.localScale = new Vector3(0.04f, 0.04f, dist);
        bolt.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0.9f, 0.2f), 6f);
        Object.Destroy(bolt, 0.3f);

        // Second bolt slightly offset for thickness
        var bolt2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(bolt2.GetComponent<BoxCollider>());
        bolt2.name = "PlagueChainBolt2";
        bolt2.transform.position = mid + new Vector3(
            Random.Range(-0.05f, 0.05f),
            Random.Range(-0.05f, 0.05f),
            Random.Range(-0.05f, 0.05f));
        bolt2.transform.rotation = Quaternion.LookRotation(to - from);
        bolt2.transform.localScale = new Vector3(0.03f, 0.03f, dist * 0.9f);
        bolt2.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 1f, 0.1f), 8f);
        Object.Destroy(bolt2, 0.25f);

        // Spark burst at target end
        var sparkGO = new GameObject("ChainSpark");
        sparkGO.transform.position = to;
        var sparkPS = sparkGO.AddComponent<ParticleSystem>();
        sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var sMain = sparkPS.main;
        sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        sMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        sMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        sMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 1f, 0.3f), new Color(0.3f, 0.8f, 0.1f));
        sMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sMain.gravityModifier = 0.1f;
        sMain.duration = 0.1f;
        sMain.loop = false;

        var sEmission = sparkPS.emission;
        sEmission.rateOverTime = 0;
        sEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        var sShape = sparkPS.shape;
        sShape.shapeType = ParticleSystemShapeType.Sphere;
        sShape.radius = 0.1f;

        var sMat = new Material(ShaderCache.ParticleShader);
        sMat.SetColor("_Color", new Color(0.6f, 1f, 0.3f));
        sparkGO.GetComponent<ParticleSystemRenderer>().material = sMat;
        sparkPS.Play();
        Object.Destroy(sparkGO, 0.4f);
    }
}

public class PlagueSparkProjectile : MonoBehaviour
{
    Vector3 _dir;
    float _speed = 18f;
    float _damage;
    float _radius;
    int _maxChains;
    float _maxDist = 20f;
    Vector3 _startPos;
    bool _hit;
    ElementType _spellElement = ElementType.Fire;

    public void Init(Vector3 dir, float damage, float radius, int maxChains)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dir = dir.normalized;
        _damage = damage;
        _radius = radius;
        _maxChains = maxChains;
        _startPos = transform.position;
    }

    void Update()
    {
        if (_hit) return;
        transform.position += _dir * _speed * Time.deltaTime;
        if (Vector3.Distance(_startPos, transform.position) > _maxDist)
        {
            // Missed everything — AoE at max range
            AoEImpact(transform.position);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_hit) return;
        if (other.GetComponent<PlayerController>() != null) return;
        if (other.GetComponent<PlagueSparkProjectile>() != null) return;
        if (other.GetComponent<ZoneProjectile>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;

        // Poison zone interactions (steam → toxic mist, cyclone → plague vortex)
        if (SpellInteractionSystem.TryInteraction(ElementType.Poison, other, transform.position, _damage))
        { _hit = true; Destroy(gameObject); return; }

        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
        {
            // Hit an enemy — chain to nearby enemies
            _hit = true;
            ChainImpact(other.transform, hp);
            return;
        }

        if (hp == null)
        {
            // Hit wall/ground — AoE damage at impact
            AoEImpact(transform.position);
        }
    }

    void ChainImpact(Transform firstTarget, Health firstHP)
    {
        Vector3 pos = firstTarget.position;

        // Damage first target
        firstHP.TakeDamage(_damage, _spellElement);
        ApplyPoisonDoT(firstTarget.gameObject);
        SFXSystem.Play(SFXSystem.SFXType.Hit, pos);

        // Flash at impact
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = pos + Vector3.up * 0.5f;
        flash.transform.localScale = Vector3.one * 0.3f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.6f, 1f, 0.3f), 7f);
        flash.AddComponent<FlashShrink>().Init(0.15f);

        // Find chain targets
        HashSet<int> hitIds = new HashSet<int>();
        hitIds.Add(firstTarget.GetInstanceID());

        Transform previousTarget = firstTarget;
        float chainDamage = _damage * 0.7f;

        for (int i = 0; i < _maxChains; i++)
        {
            Collider[] nearby = Physics.OverlapSphere(previousTarget.position, _radius);
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var c in nearby)
            {
                if (c.GetComponent<PlayerController>() != null) continue;
                var cHP = c.GetComponent<Health>();
                if (cHP == null || cHP.IsDead) continue;
                if (hitIds.Contains(c.transform.GetInstanceID())) continue;

                float d = Vector3.Distance(previousTarget.position, c.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c.transform;
                }
            }

            if (best == null) break;

            hitIds.Add(best.GetInstanceID());

            // Deal chain damage
            var bestHP = best.GetComponent<Health>();
            if (bestHP != null && !bestHP.IsDead)
            {
                bestHP.TakeDamage(chainDamage, _spellElement);
                ApplyPoisonDoT(best.gameObject);
            }

            // VFX: green lightning bolt between targets
            PlagueSparkSpell.SpawnChainBolt(
                previousTarget.position + Vector3.up * 0.5f,
                best.position + Vector3.up * 0.5f);

            SFXSystem.Play(SFXSystem.SFXType.Hit, best.position);

            previousTarget = best;
            chainDamage *= 0.7f;
        }

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);

        Destroy(gameObject);
    }

    void AoEImpact(Vector3 pos)
    {
        _hit = true;
        pos.y = 0;

        Collider[] hits = Physics.OverlapSphere(pos, _radius * 0.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(_damage * 0.5f, _spellElement);
            }
        }

        // Burst VFX
        var burstGO = new GameObject("PlagueSparkBurst");
        burstGO.transform.position = pos + Vector3.up * 0.3f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 1f, 0.3f), new Color(0.3f, 0.8f, 0.1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;
        main.duration = 0.1f;
        main.loop = false;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.2f;

        var sOL = ps.sizeOverLifetime;
        sOL.enabled = true;
        sOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", new Color(0.6f, 1f, 0.3f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.5f);

        SFXSystem.Play(SFXSystem.SFXType.Hit, pos);
        Destroy(gameObject);
    }

    void ApplyPoisonDoT(GameObject target)
    {
        // Remove existing PoisonDoT if present to refresh
        var existing = target.GetComponent<PoisonDoT>();
        if (existing != null) Object.Destroy(existing);

        target.AddComponent<PoisonDoT>().Init(2f, 3f);
    }
}

/// <summary>
/// Helper component: applies poison damage over time to the attached entity.
/// 0.5s tick rate, self-removing after duration expires.
/// </summary>
public class PoisonDoT : MonoBehaviour
{
    float _dps, _timer, _tickTimer;
    ElementType _spellElement = ElementType.Fire;

    public void Init(float dps, float duration)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dps = dps;
        _timer = duration;
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0) { Destroy(this); return; }
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        var hp = GetComponent<Health>();
        if (hp != null && !hp.IsDead) hp.TakeDamage(_dps * 0.5f, _spellElement);
    }
}
