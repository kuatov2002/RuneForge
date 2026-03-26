using UnityEngine;

/// <summary>
/// Fire + Fire = Inferno
/// Instant massive AoE explosion. Highest burst damage in the game. No DoT.
/// Balance: radius 2.5 (was 3.5)
/// </summary>
public static class InfernoSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        // AoE damage
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                GameFeel.ApplyKnockback(h.transform, center, damage * 0.5f);
            }
        }

        // ── VFX ──

        // 1) Fire particle burst: 30-50 orange-red particles spraying outward
        var burstGO = new GameObject("InfernoBurst");
        burstGO.transform.position = center + Vector3.up * 0.3f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.4f, 0f),
            new Color(1f, 0.15f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;
        main.maxParticles = 60;
        main.duration = 0.6f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        int burstCount = charged ? 50 : 35;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0f), 0.4f),
                new GradientColorKey(new Color(0.3f, 0.05f, 0f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = grad;

        var renderer = burstGO.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial(new Color(1f, 0.4f, 0f));

        ps.Play();
        Object.Destroy(burstGO, 1f);

        // 2) Lingering embers: slow upward drift
        var embersGO = new GameObject("InfernoEmbers");
        embersGO.transform.position = center + Vector3.up * 0.2f;
        var emberPS = embersGO.AddComponent<ParticleSystem>();
        emberPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var emberMain = emberPS.main;
        emberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        emberMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        emberMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        emberMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.7f, 0.1f),
            new Color(1f, 0.3f, 0f));
        emberMain.simulationSpace = ParticleSystemSimulationSpace.World;
        emberMain.gravityModifier = -0.3f; // float upward
        emberMain.duration = 0.3f;
        emberMain.loop = false;

        var emberEmission = emberPS.emission;
        emberEmission.rateOverTime = 0;
        emberEmission.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 12) });

        var emberShape = emberPS.shape;
        emberShape.shapeType = ParticleSystemShapeType.Sphere;
        emberShape.radius = radius * 0.5f;

        var emberSize = emberPS.sizeOverLifetime;
        emberSize.enabled = true;
        emberSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var emberRenderer = embersGO.GetComponent<ParticleSystemRenderer>();
        emberRenderer.material = CreateParticleMaterial(new Color(1f, 0.6f, 0.1f));

        emberPS.Play();
        Object.Destroy(embersGO, 1.8f);

        // 3) Ground scorch decal: dark disc on ground
        var scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(scorch.GetComponent<CapsuleCollider>());
        scorch.transform.position = center + Vector3.up * 0.02f;
        scorch.transform.localScale = new Vector3(radius * 1.8f, 0.01f, radius * 1.8f);
        scorch.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.08f, 0.05f, 0.03f));
        scorch.AddComponent<InfernoScorchFade>().Init(2f);

        // 4) Expanding heat ring (replaces old sphere expand)
        var ringGO = new GameObject("InfernoHeatRing");
        ringGO.transform.position = center + Vector3.up * 0.5f;
        var ringPS = ringGO.AddComponent<ParticleSystem>();
        ringPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var ringMain = ringPS.main;
        ringMain.startLifetime = 0.4f;
        ringMain.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
        ringMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
        ringMain.startColor = new Color(1f, 0.8f, 0.2f, 0.6f);
        ringMain.simulationSpace = ParticleSystemSimulationSpace.World;
        ringMain.gravityModifier = 0f;
        ringMain.duration = 0.1f;
        ringMain.loop = false;

        var ringEmission = ringPS.emission;
        ringEmission.rateOverTime = 0;
        ringEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        var ringShape = ringPS.shape;
        ringShape.shapeType = ParticleSystemShapeType.Circle;
        ringShape.radius = 0.2f;

        var ringColor = ringPS.colorOverLifetime;
        ringColor.enabled = true;
        var ringGrad = new Gradient();
        ringGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        ringColor.color = ringGrad;

        ringGO.GetComponent<ParticleSystemRenderer>().material = CreateParticleMaterial(new Color(1f, 0.7f, 0.2f));
        ringPS.Play();
        Object.Destroy(ringGO, 0.8f);

        // 5) Brief white-hot flash core
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(core.GetComponent<SphereCollider>());
        core.transform.position = center + Vector3.up * 0.5f;
        core.transform.localScale = Vector3.one * 0.6f;
        core.GetComponent<Renderer>().material = ShaderCache.NewEmissive(Color.white, 10f);
        core.AddComponent<FlashShrink>().Init(0.2f);

        // Game feel
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(charged ? 0.5f : 0.35f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(charged ? 0.08f : 0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }

    static Material CreateParticleMaterial(Color color)
    {
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", color);
        mat.SetFloat("_Mode", 1); // Additive
        return mat;
    }
}

/// <summary>Fades ground scorch mark alpha over time then destroys.</summary>
public class InfernoScorchFade : MonoBehaviour
{
    float _duration;
    float _timer;
    Renderer _rend;
    Color _startColor;

    public void Init(float duration)
    {
        _duration = duration;
        _rend = GetComponent<Renderer>();
        _startColor = _rend.material.color;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        // Start fading after 60% of duration
        if (t > 0.6f)
        {
            float fade = (t - 0.6f) / 0.4f;
            Color c = _startColor;
            c.a = 1f - fade;
            _rend.material.color = c;
        }
    }
}
