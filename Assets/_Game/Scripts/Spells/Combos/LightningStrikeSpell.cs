using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lightning + Lightning = Lightning Strike
/// Instant bolt at cursor position, chains to nearby enemies.
/// Each chain target takes 60% of previous damage.
/// </summary>
public static class LightningStrikeSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        if (charged) { damage *= 1.3f; radius *= 1.2f; }

        int maxChains = charged ? 4 : 3;

        // Find closest enemy to center within radius for main bolt
        Collider[] nearby = Physics.OverlapSphere(center, radius);
        Transform mainTarget = null;
        float bestDist = float.MaxValue;

        foreach (var c in nearby)
        {
            if (c.GetComponent<PlayerController>() != null) continue;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float d = Vector3.Distance(center, c.transform.position);
            if (d < bestDist) { bestDist = d; mainTarget = c.transform; }
        }

        if (mainTarget == null)
        {
            // No enemies found — just VFX at cursor
            SpawnBoltSegment(center + Vector3.up * 6f, center, 0.15f);
            SpawnImpactFlash(center);

            if (TopDownCamera.Instance != null)
                TopDownCamera.Instance.AddTrauma(0.3f);
            SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
            return;
        }

        // Main bolt hits
        var mainHP = mainTarget.GetComponent<Health>();
        if (mainHP != null && !mainHP.IsDead)
        {
            mainHP.TakeDamage(damage);
            GameFeel.ApplyKnockback(mainTarget, center, damage * 0.2f);
        }

        // Spawn bolt VFX from sky to target
        Vector3 mainPos = mainTarget.position;
        SpawnBoltSegment(mainPos + Vector3.up * 6f, mainPos, 0.15f);
        SpawnImpactFlash(mainPos);

        // Chain lightning
        HashSet<int> hitIds = new HashSet<int>();
        hitIds.Add(mainTarget.GetInstanceID());

        Transform prevTarget = mainTarget;
        float chainDamage = damage * 0.6f;

        for (int i = 0; i < maxChains; i++)
        {
            Transform nextTarget = FindNearestUnhit(prevTarget.position, 5f, hitIds);
            if (nextTarget == null) break;

            var nextHP = nextTarget.GetComponent<Health>();
            if (nextHP != null && !nextHP.IsDead)
            {
                nextHP.TakeDamage(chainDamage);
                GameFeel.ApplyKnockback(nextTarget, prevTarget.position, chainDamage * 0.15f);
            }

            // Bolt VFX between chain targets
            SpawnBoltSegment(prevTarget.position + Vector3.up * 0.8f,
                             nextTarget.position + Vector3.up * 0.8f, 0.15f);
            SpawnImpactFlash(nextTarget.position);

            hitIds.Add(nextTarget.GetInstanceID());
            prevTarget = nextTarget;
            chainDamage *= 0.6f;
        }

        // Game feel
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.3f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }

    static Transform FindNearestUnhit(Vector3 from, float searchRadius, HashSet<int> hitIds)
    {
        Collider[] nearby = Physics.OverlapSphere(from, searchRadius);
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var c in nearby)
        {
            if (c.GetComponent<PlayerController>() != null) continue;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;
            if (hitIds.Contains(c.transform.GetInstanceID())) continue;

            float d = Vector3.Distance(from, c.transform.position);
            if (d < bestDist) { bestDist = d; best = c.transform; }
        }
        return best;
    }

    /// <summary>
    /// Spawns a stretched thin cube "bolt" between two points.
    /// Yellow emissive (1, 1, 0.3), very thin, destroyed after lifetime.
    /// </summary>
    static void SpawnBoltSegment(Vector3 from, Vector3 to, float lifetime)
    {
        Vector3 mid = (from + to) * 0.5f;
        float dist = Vector3.Distance(from, to);
        Vector3 dir = (to - from).normalized;

        var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(bolt.GetComponent<BoxCollider>());
        bolt.name = "LightningBolt";
        bolt.transform.position = mid;
        bolt.transform.localScale = new Vector3(0.05f, 0.05f, dist);
        bolt.transform.rotation = Quaternion.LookRotation(dir);
        bolt.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 1f, 0.3f), 8f);

        Object.Destroy(bolt, lifetime);

        // Secondary thinner bolt offset for visual noise
        var bolt2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(bolt2.GetComponent<BoxCollider>());
        bolt2.name = "LightningBoltSub";
        bolt2.transform.position = mid + Random.insideUnitSphere * 0.1f;
        bolt2.transform.localScale = new Vector3(0.03f, 0.03f, dist * 0.9f);
        bolt2.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(-10f, 10f), 0);
        bolt2.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.8f, 1f), 10f);

        Object.Destroy(bolt2, lifetime);
    }

    static void SpawnImpactFlash(Vector3 pos)
    {
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = pos + Vector3.up * 0.3f;
        flash.transform.localScale = Vector3.one * 0.4f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 1f, 0.5f), 10f);
        flash.AddComponent<FlashShrink>().Init(0.15f);

        // Spark particles
        var sparkGO = new GameObject("LightningImpactSparks");
        sparkGO.transform.position = pos + Vector3.up * 0.3f;
        var ps = sparkGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 0.3f), new Color(0.8f, 0.8f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f;
        main.duration = 0.1f;
        main.loop = false;
        main.maxParticles = 15;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(1f, 1f, 0.3f));
        sparkGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(sparkGO, 0.5f);
    }
}
