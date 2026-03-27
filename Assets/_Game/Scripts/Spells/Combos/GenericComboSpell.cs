using UnityEngine;

/// <summary>
/// Generic fallback implementation for advanced element combos
/// (Lightning, Poison, Void cross-combos).
/// Provides a baseline AoE damage + particle VFX that can be refined later.
/// </summary>
public static class GenericComboSpell
{
    public static void Cast(ComboSpellDef def, Vector3 center, float damage, float radius, bool charged)
    {
        // AoE damage
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage, ComboSpellFactory.CurrentCastElement ?? ElementType.Fire);

                if (def.duration > 0)
                    hp.ApplyStun(Mathf.Min(def.duration, 2f));
            }
        }

        // ── VFX: particle burst in combo color ──

        // 1) Main burst: particles spraying outward in combo color
        var burstGO = new GameObject("GenericComboBurst");
        burstGO.transform.position = center + Vector3.up * 0.5f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        Color brightCol = def.color * 1.2f;
        brightCol.a = 1f;
        main.startColor = new ParticleSystem.MinMaxGradient(def.color, brightCol);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;
        main.duration = 0.3f;
        main.loop = false;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        int burstCount = charged ? 25 : 18;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(def.color, 0.3f),
                new GradientColorKey(def.color * 0.5f, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOL.color = grad;

        var burstMat = new Material(ShaderCache.ParticleShader);
        burstMat.SetColor("_Color", def.color);
        burstMat.SetFloat("_Mode", 1); // Additive
        burstGO.GetComponent<ParticleSystemRenderer>().material = burstMat;
        ps.Play();
        Object.Destroy(burstGO, 0.8f);

        // 2) Ground ring particles expanding outward
        var ringGO = new GameObject("GenericComboRing");
        ringGO.transform.position = center + Vector3.up * 0.1f;
        var ringPS = ringGO.AddComponent<ParticleSystem>();
        ringPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var ringMain = ringPS.main;
        ringMain.startLifetime = 0.4f;
        ringMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
        ringMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        ringMain.startColor = new Color(def.color.r, def.color.g, def.color.b, 0.7f);
        ringMain.simulationSpace = ParticleSystemSimulationSpace.World;
        ringMain.gravityModifier = 0f;
        ringMain.duration = 0.1f;
        ringMain.loop = false;

        var ringEmission = ringPS.emission;
        ringEmission.rateOverTime = 0;
        ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var ringShape = ringPS.shape;
        ringShape.shapeType = ParticleSystemShapeType.Circle;
        ringShape.radius = 0.15f;

        var ringSizeOL = ringPS.sizeOverLifetime;
        ringSizeOL.enabled = true;
        ringSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ringGO.GetComponent<ParticleSystemRenderer>().material = burstMat;
        ringPS.Play();
        Object.Destroy(ringGO, 0.6f);

        // 3) Flash core
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(core.GetComponent<SphereCollider>());
        core.transform.position = center + Vector3.up * 0.5f;
        core.transform.localScale = Vector3.one * 0.4f;
        core.GetComponent<Renderer>().material = ShaderCache.NewEmissive(def.color, 6f);
        core.AddComponent<FlashShrink>().Init(0.25f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(charged ? 0.3f : 0.2f);
        if (GameFeel.Instance != null && damage >= 10)
            GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }
}
