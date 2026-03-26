using UnityEngine;

/// <summary>
/// Poison + Fire = Detonate
/// Launches an orange-green toxic projectile that explodes on impact,
/// dealing burst damage and leaving a small poison zone.
/// </summary>
public static class DetonateSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        if (charged) { radius *= 1.3f; damage *= 1.3f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        float finalDamage = damage;
        float finalRadius = radius;
        bool wasCharged = charged;

        // Create orange-green glob projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "DetonateGlob";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.22f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.6f, 0.1f), 5f);

        // Fire + poison particle trail
        var trailGO = new GameObject("DetonateTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.5f, 0.1f), new Color(0.3f, 0.8f, 0.1f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = -0.1f;
        tm.loop = true;
        tm.maxParticles = 30;

        var te = trailPS.emission;
        te.rateOverTime = 25;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.06f;

        var tSizeOL = trailPS.sizeOverLifetime;
        tSizeOL.enabled = true;
        tSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var tMat = new Material(Shader.Find("Particles/Standard Unlit"));
        tMat.SetColor("_Color", new Color(0.8f, 0.6f, 0.1f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        go.AddComponent<ZoneProjectile>().Init(dir, 16f, 0f, 20f,
            new Color(0.8f, 0.6f, 0.1f),
            (impactPos) => { SpawnDetonation(impactPos, finalDamage, finalRadius, wasCharged); });

        Object.Destroy(go, 5f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnDetonation(Vector3 pos, float damage, float radius, bool charged)
    {
        pos.y = 0;

        // Immediate AoE burst damage
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                GameFeel.ApplyKnockback(h.transform, pos, damage * 0.25f);
            }
        }

        // Check SpellInteractionSystem for fire interactions with existing zones
        Collider[] zones = Physics.OverlapSphere(pos, radius);
        foreach (var z in zones)
        {
            SpellInteractionSystem.TryFireInteraction(z, pos, damage);
        }

        // ── Explosion VFX: orange-green burst ──
        var burstGO = new GameObject("DetonateExplosion");
        burstGO.transform.position = pos + Vector3.up * 0.3f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.5f, 0f), new Color(0.3f, 0.8f, 0.1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;
        main.duration = 0.2f;
        main.loop = false;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, charged ? 40 : 25) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.3f;

        var sOL = ps.sizeOverLifetime;
        sOL.enabled = true;
        sOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f),
                new GradientColorKey(new Color(0.6f, 0.8f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.1f, 0.4f, 0.05f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.5f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOL.color = grad;

        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(0.8f, 0.6f, 0.1f));
        mat.SetFloat("_Mode", 1);
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.8f);

        // Flash
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = pos + Vector3.up * 0.3f;
        flash.transform.localScale = Vector3.one * 0.5f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.8f, 0.3f), 8f);
        flash.AddComponent<FlashShrink>().Init(0.2f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(charged ? 0.35f : 0.2f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(charged ? 0.05f : 0.03f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);

        // ── Leave a small poison zone at impact ──
        SpawnPoisonResidue(pos);
    }

    static void SpawnPoisonResidue(Vector3 pos)
    {
        float poisonRadius = 1.5f;
        float poisonDps = 3f;
        float poisonDuration = 2f;

        pos.y = 0.05f;

        var poolGO = new GameObject("DetonatePoisonPool");
        poolGO.transform.position = pos;

        // Green poison disc
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(poolGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(poisonRadius * 2f, 0.03f, poisonRadius * 2f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.15f, 0.5f, 0.05f), 2f);

        // Poison bubble particles
        var bubbleGO = new GameObject("PoisonBubbles");
        bubbleGO.transform.SetParent(poolGO.transform, false);
        bubbleGO.transform.localPosition = Vector3.up * 0.1f;
        var bubblePS = bubbleGO.AddComponent<ParticleSystem>();
        bubblePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var bMain = bubblePS.main;
        bMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        bMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        bMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        bMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.2f, 0.8f, 0.1f, 0.7f),
            new Color(0.1f, 0.6f, 0.05f, 0.5f));
        bMain.simulationSpace = ParticleSystemSimulationSpace.World;
        bMain.gravityModifier = -0.2f;
        bMain.duration = poisonDuration;
        bMain.loop = true;
        bMain.maxParticles = 20;

        var bEmission = bubblePS.emission;
        bEmission.rateOverTime = 10;

        var bShape = bubblePS.shape;
        bShape.shapeType = ParticleSystemShapeType.Circle;
        bShape.radius = poisonRadius * 0.6f;

        var bSizeOL = bubblePS.sizeOverLifetime;
        bSizeOL.enabled = true;
        bSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var bMat = new Material(Shader.Find("Particles/Standard Unlit"));
        bMat.SetColor("_Color", new Color(0.2f, 0.7f, 0.1f, 0.6f));
        bubbleGO.GetComponent<ParticleSystemRenderer>().material = bMat;
        bubblePS.Play();

        // Trigger collider
        var col = poolGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = poisonRadius;

        var rb = poolGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        poolGO.AddComponent<DetonatePoisonZone>().Init(poisonDps, poisonDuration);

        Object.Destroy(poolGO, poisonDuration);
    }
}

public class DetonatePoisonZone : MonoBehaviour
{
    float _dps;
    float _duration;
    float _tickTimer;

    public void Init(float dps, float duration)
    {
        _dps = dps;
        _duration = duration;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps * 0.5f);
    }
}
