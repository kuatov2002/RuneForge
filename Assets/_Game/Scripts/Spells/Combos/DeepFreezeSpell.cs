using UnityEngine;

/// <summary>
/// Water + Water = Deep Freeze
/// Freezes all enemies in radius. They become statues.
/// Balance: radius 2.2 (was 3.5), freeze duration 2s (was 3s)
/// </summary>
public static class DeepFreezeSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) duration *= 1.5f;

        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                hp.ApplyFreeze(duration);

                // Ice crystal overlay on each frozen enemy
                SpawnIceOverlay(h.transform, duration);
            }
        }

        // ── VFX ──

        // 1) Ice particle burst: 20-30 ice-blue particles bursting up then falling
        var burstGO = new GameObject("DeepFreezeBurst");
        burstGO.transform.position = center + Vector3.up * 0.2f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0.8f, 1f),
            new Color(0.7f, 0.9f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.6f; // fall like snow
        main.duration = 0.2f;
        main.loop = false;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.3f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.4f, 0.7f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        burstGO.GetComponent<ParticleSystemRenderer>().material = CreateIceParticleMat();
        ps.Play();
        Object.Destroy(burstGO, 1.5f);

        // 2) Ground ice ring expanding outward with frost particles
        var ringGO = new GameObject("DeepFreezeRing");
        ringGO.transform.position = center + Vector3.up * 0.05f;
        var ringPS = ringGO.AddComponent<ParticleSystem>();
        ringPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var ringMain = ringPS.main;
        ringMain.startLifetime = 0.5f;
        ringMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        ringMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        ringMain.startColor = new Color(0.6f, 0.85f, 1f, 0.8f);
        ringMain.simulationSpace = ParticleSystemSimulationSpace.World;
        ringMain.gravityModifier = 0f;
        ringMain.duration = 0.1f;
        ringMain.loop = false;
        ringMain.startRotation3D = false;

        var ringEmission = ringPS.emission;
        ringEmission.rateOverTime = 0;
        ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var ringShape = ringPS.shape;
        ringShape.shapeType = ParticleSystemShapeType.Circle;
        ringShape.radius = 0.2f;
        ringShape.arc = 360f;

        var ringSizeOL = ringPS.sizeOverLifetime;
        ringSizeOL.enabled = true;
        ringSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ringGO.GetComponent<ParticleSystemRenderer>().material = CreateIceParticleMat();
        ringPS.Play();
        Object.Destroy(ringGO, 0.8f);

        // 3) Crystal shards: small cubes with ice-blue emissive scattered around
        Color shardCol = new Color(0.5f, 0.85f, 1f);
        for (int i = 0; i < 8; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(shard.GetComponent<BoxCollider>());
            Vector3 offset = Random.insideUnitSphere * radius * 0.7f;
            offset.y = Mathf.Abs(offset.y) * 0.3f;
            shard.transform.position = center + offset;
            float s = Random.Range(0.05f, 0.12f);
            shard.transform.localScale = new Vector3(s * 0.6f, s, s * 0.6f);
            shard.transform.rotation = Random.rotation;
            shard.GetComponent<Renderer>().material = ShaderCache.NewIce(shardCol, 0.8f);
            Object.Destroy(shard, duration * 0.8f);
        }

        // 4) Ice disc on ground (persistent for duration)
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.position = center + Vector3.up * 0.02f;
        disc.transform.localScale = new Vector3(radius * 1.6f, 0.015f, radius * 1.6f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewIce(new Color(0.6f, 0.88f, 1f), 0.5f);
        Object.Destroy(disc, duration);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }

    static void SpawnIceOverlay(Transform enemy, float duration)
    {
        // Translucent ice cube overlay on enemy
        var ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(ice.GetComponent<BoxCollider>());
        ice.transform.SetParent(enemy, false);
        ice.transform.localPosition = Vector3.up * 0.5f;
        ice.transform.localScale = new Vector3(0.7f, 1.1f, 0.7f);
        Color iceCol = new Color(0.5f, 0.8f, 1f, 0.6f);
        ice.GetComponent<Renderer>().material = ShaderCache.NewIce(iceCol, 0.6f);

        // Frost particle effect on the frozen enemy
        var frostGO = new GameObject("FrostParticles");
        frostGO.transform.SetParent(enemy, false);
        frostGO.transform.localPosition = Vector3.up * 0.8f;
        var frostPS = frostGO.AddComponent<ParticleSystem>();
        frostPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var fm = frostPS.main;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        fm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        fm.startColor = new Color(0.7f, 0.9f, 1f, 0.7f);
        fm.simulationSpace = ParticleSystemSimulationSpace.World;
        fm.gravityModifier = -0.1f;
        fm.duration = duration;
        fm.loop = true;

        var fe = frostPS.emission;
        fe.rateOverTime = 8;

        var fs = frostPS.shape;
        fs.shapeType = ParticleSystemShapeType.Box;
        fs.scale = new Vector3(0.5f, 0.8f, 0.5f);

        frostGO.GetComponent<ParticleSystemRenderer>().material = CreateIceParticleMat();
        frostPS.Play();

        Object.Destroy(ice, duration);
        Object.Destroy(frostGO, duration);
    }

    static Material CreateIceParticleMat()
    {
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(0.6f, 0.9f, 1f, 0.8f));
        return mat;
    }
}
