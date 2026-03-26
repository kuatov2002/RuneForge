using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fire + Air = Wildfire
/// Fire automatically jumps from burning enemies to all nearby ones.
/// Chain reaction across the entire room.
/// Balance: chain radius 3 (was 4), max chains 8 (was 12)
/// </summary>
public static class WildfireSpell
{
    public static void Cast(Vector3 center, float damage, float chainRadius, bool charged)
    {
        if (charged) { chainRadius *= 1.5f; damage *= 1.3f; }

        // Find initial target(s) near cursor
        Collider[] initial = Physics.OverlapSphere(center, 2f);
        var hit = new HashSet<Health>();
        var toProcess = new Queue<Health>();

        foreach (var c in initial)
        {
            if (c.GetComponent<PlayerController>() != null) continue;
            var hp = c.GetComponent<Health>();
            if (hp != null && !hp.IsDead && hit.Add(hp))
            {
                hp.TakeDamage(damage);
                toProcess.Enqueue(hp);

                // Fire burst on each initial hit
                SpawnHitBurst(hp.transform.position);
            }
        }

        // If no direct target, fire a projectile toward cursor
        if (hit.Count == 0)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                Vector3 dir = (center - player.transform.position);
                dir.y = 0;
                dir.Normalize();
                CreateFirebolt(player.transform.position + dir * 0.6f + Vector3.up * 0.5f,
                    dir, damage, chainRadius, charged);
            }
            return;
        }

        // Chain reaction
        int maxChains = charged ? 14 : 8;
        int chains = 0;
        while (toProcess.Count > 0 && chains < maxChains)
        {
            var current = toProcess.Dequeue();
            if (current == null) continue;

            Collider[] nearby = Physics.OverlapSphere(current.transform.position, chainRadius);
            foreach (var n in nearby)
            {
                if (n.GetComponent<PlayerController>() != null) continue;
                var nhp = n.GetComponent<Health>();
                if (nhp != null && !nhp.IsDead && hit.Add(nhp))
                {
                    nhp.TakeDamage(damage * 0.7f);
                    toProcess.Enqueue(nhp);
                    chains++;

                    // Fire chain arc with trailing particles
                    CreateChainArc(current.transform.position, n.transform.position);
                    // Brief fire burst on each chained target
                    SpawnHitBurst(n.transform.position);
                }
            }
        }

        // Origin flash with particles
        var flashGO = new GameObject("WildfireFlash");
        flashGO.transform.position = center + Vector3.up * 0.5f;
        var flashPS = flashGO.AddComponent<ParticleSystem>();
        flashPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var flashMain = flashPS.main;
        flashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        flashMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        flashMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        flashMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.7f, 0.1f),
            new Color(1f, 0.3f, 0f));
        flashMain.simulationSpace = ParticleSystemSimulationSpace.World;
        flashMain.gravityModifier = -0.2f;
        flashMain.duration = 0.1f;
        flashMain.loop = false;

        var flashEmission = flashPS.emission;
        flashEmission.rateOverTime = 0;
        flashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var flashShape = flashPS.shape;
        flashShape.shapeType = ParticleSystemShapeType.Sphere;
        flashShape.radius = 0.2f;

        var flashMat = new Material(Shader.Find("Particles/Standard Unlit"));
        flashMat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        flashMat.SetFloat("_Mode", 1);
        flashGO.GetComponent<ParticleSystemRenderer>().material = flashMat;
        flashPS.Play();
        Object.Destroy(flashGO, 0.6f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }

    /// <summary>Brief fire burst on hit enemy.</summary>
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
            new Color(1f, 0.6f, 0.1f),
            new Color(1f, 0.2f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.2f;
        main.duration = 0.1f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        mat.SetFloat("_Mode", 1);
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.5f);
    }

    static void CreateFirebolt(Vector3 pos, Vector3 dir, float damage, float chainRadius, bool charged)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "WildfireBolt";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.3f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.8f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0.1f), 5f);

        // Trailing fire particles on the projectile
        var trailGO = new GameObject("BoltTrail");
        trailGO.transform.SetParent(go.transform, false);
        trailGO.transform.localPosition = Vector3.zero;
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var trailMain = trailPS.main;
        trailMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        trailMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        trailMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        trailMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.1f),
            new Color(1f, 0.25f, 0f));
        trailMain.simulationSpace = ParticleSystemSimulationSpace.World;
        trailMain.gravityModifier = -0.1f;
        trailMain.loop = true;

        var trailEmission = trailPS.emission;
        trailEmission.rateOverTime = 25;

        var trailShape = trailPS.shape;
        trailShape.shapeType = ParticleSystemShapeType.Sphere;
        trailShape.radius = 0.1f;

        var trailSizeOL = trailPS.sizeOverLifetime;
        trailSizeOL.enabled = true;
        trailSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var trailMat = new Material(Shader.Find("Particles/Standard Unlit"));
        trailMat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        trailMat.SetFloat("_Mode", 1);
        trailGO.GetComponent<ParticleSystemRenderer>().material = trailMat;
        trailPS.Play();

        var proj = go.AddComponent<SpellProjectile>();
        proj.Setup(dir, 14f, damage, null, 15f);
        proj.wildfireChainRadius = chainRadius;
        proj.wildfireCharged = charged;
    }

    static void CreateChainArc(Vector3 from, Vector3 to)
    {
        // Animated fire trail line
        var go = new GameObject("WildfireArc");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 6;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.05f;
        Color fireCol = new Color(1f, 0.5f, 0.1f);
        lr.material = ShaderCache.NewEmissive(fireCol, 4f);
        lr.startColor = new Color(1f, 0.9f, 0.3f);
        lr.endColor = new Color(1f, 0.3f, 0f);

        Vector3 mid = (from + to) * 0.5f;
        Vector3 perp = Vector3.Cross(to - from, Vector3.up).normalized;
        float dist = Vector3.Distance(from, to);

        // More waypoints for a jittery fire-like path
        lr.SetPosition(0, from + Vector3.up * 0.5f);
        for (int i = 1; i < 5; i++)
        {
            float t = i / 5f;
            Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * 0.5f;
            p += perp * Random.Range(-0.2f, 0.2f);
            p.y += Random.Range(-0.1f, 0.15f);
            lr.SetPosition(i, p);
        }
        lr.SetPosition(5, to + Vector3.up * 0.5f);

        // Fire particles along the chain arc
        var arcPS_GO = new GameObject("ArcParticles");
        arcPS_GO.transform.position = mid + Vector3.up * 0.5f;
        var arcPS = arcPS_GO.AddComponent<ParticleSystem>();
        arcPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var arcMain = arcPS.main;
        arcMain.startLifetime = 0.2f;
        arcMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        arcMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        arcMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.7f, 0.2f),
            new Color(1f, 0.3f, 0f));
        arcMain.simulationSpace = ParticleSystemSimulationSpace.World;
        arcMain.gravityModifier = -0.2f;
        arcMain.duration = 0.1f;
        arcMain.loop = false;

        var arcEmission = arcPS.emission;
        arcEmission.rateOverTime = 0;
        arcEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });

        var arcShape = arcPS.shape;
        arcShape.shapeType = ParticleSystemShapeType.Box;
        arcShape.scale = new Vector3(dist * 0.5f, 0.2f, 0.2f);
        arcPS_GO.transform.rotation = Quaternion.LookRotation(to - from);

        var arcMat = new Material(Shader.Find("Particles/Standard Unlit"));
        arcMat.SetColor("_Color", new Color(1f, 0.5f, 0.1f));
        arcMat.SetFloat("_Mode", 1);
        arcPS_GO.GetComponent<ParticleSystemRenderer>().material = arcMat;
        arcPS.Play();

        Object.Destroy(go, 0.25f);
        Object.Destroy(arcPS_GO, 0.4f);
    }
}
