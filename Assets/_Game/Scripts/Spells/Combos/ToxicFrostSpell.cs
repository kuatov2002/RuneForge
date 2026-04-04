using UnityEngine;

/// <summary>
/// Poison + Water = Toxic Frost
/// Launches a teal glob that creates a zone slowing and poisoning enemies.
/// Reuses MagmaSlow mechanic for the slow effect.
/// </summary>
public static class ToxicFrostSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; duration *= 1.5f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        float finalDamage = damage;
        float finalRadius = radius;
        float finalDuration = duration;

        // Create teal glob projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ToxicFrostGlob";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.1f, 0.7f, 0.6f), 4f);

        go.AddComponent<ZoneProjectile>().Init(dir, 14f, 1f, 18f,
            new Color(0.1f, 0.7f, 0.6f),
            (impactPos) => { SpawnToxicFrostZone(impactPos, finalDamage, finalRadius, finalDuration); });

        Object.Destroy(go, 5f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnToxicFrostZone(Vector3 pos, float damage, float radius, float duration)
    {
        pos.y = 0.05f;

        var zoneGO = new GameObject("ToxicFrostZone");
        zoneGO.transform.position = pos;

        // ── VFX: teal/green ice disc on ground ──
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(zoneGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewPlayerZone(new Color(0.2f, 0.7f, 0.6f), 0.5f, 0.3f, 1.5f, 0.3f);

        // Poison bubbles rising
        var bubbleGO = new GameObject("ToxicFrostBubbles");
        bubbleGO.transform.SetParent(zoneGO.transform, false);
        bubbleGO.transform.localPosition = Vector3.up * 0.1f;
        var bubblePS = bubbleGO.AddComponent<ParticleSystem>();
        bubblePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var bMain = bubblePS.main;
        bMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        bMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        bMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        bMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.8f, 0.3f, 0.6f),
            new Color(0.1f, 0.6f, 0.5f, 0.4f));
        bMain.simulationSpace = ParticleSystemSimulationSpace.World;
        bMain.gravityModifier = -0.25f;
        bMain.duration = duration;
        bMain.loop = true;
        bMain.maxParticles = 25;

        var bEmission = bubblePS.emission;
        bEmission.rateOverTime = 10;

        var bShape = bubblePS.shape;
        bShape.shapeType = ParticleSystemShapeType.Circle;
        bShape.radius = radius * 0.7f;

        var bSizeOL = bubblePS.sizeOverLifetime;
        bSizeOL.enabled = true;
        bSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0f));

        var bColorOL = bubblePS.colorOverLifetime;
        bColorOL.enabled = true;
        var bGrad = new Gradient();
        bGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.1f, 0.8f, 0.4f), 0f),
                new GradientColorKey(new Color(0.1f, 0.6f, 0.5f), 0.5f),
                new GradientColorKey(new Color(0.05f, 0.3f, 0.2f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.3f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        bColorOL.color = bGrad;

        var bMat = new Material(ShaderCache.ParticleShader);
        bMat.SetColor("_Color", new Color(0.15f, 0.7f, 0.4f, 0.5f));
        bubbleGO.GetComponent<ParticleSystemRenderer>().material = bMat;
        bubblePS.Play();

        // Frost mist particles hovering at ground level
        var mistGO = new GameObject("ToxicFrostMist");
        mistGO.transform.SetParent(zoneGO.transform, false);
        mistGO.transform.localPosition = Vector3.up * 0.3f;
        var mistPS = mistGO.AddComponent<ParticleSystem>();
        mistPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var mMain = mistPS.main;
        mMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        mMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        mMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        mMain.startColor = new Color(0.3f, 0.8f, 0.7f, 0.1f);
        mMain.simulationSpace = ParticleSystemSimulationSpace.World;
        mMain.gravityModifier = -0.05f;
        mMain.duration = duration;
        mMain.loop = true;
        mMain.maxParticles = 15;

        var mEmission = mistPS.emission;
        mEmission.rateOverTime = 6;

        var mShape = mistPS.shape;
        mShape.shapeType = ParticleSystemShapeType.Circle;
        mShape.radius = radius * 0.6f;

        var mNoise = mistPS.noise;
        mNoise.enabled = true;
        mNoise.strength = 0.3f;
        mNoise.frequency = 1f;
        mNoise.scrollSpeed = 0.3f;

        var mMat = new Material(ShaderCache.ParticleShader);
        mMat.SetColor("_Color", new Color(0.2f, 0.7f, 0.6f, 0.12f));
        mistGO.GetComponent<ParticleSystemRenderer>().material = mMat;
        mistPS.Play();

        // Trigger collider
        var zoneCol = zoneGO.AddComponent<SphereCollider>();
        zoneCol.isTrigger = true;
        zoneCol.radius = radius;

        var zoneRb = zoneGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        zoneGO.AddComponent<ToxicFrostZone>().Init(damage, duration);

        Object.Destroy(zoneGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Freeze, pos);
    }
}

public class ToxicFrostZone : MonoBehaviour
{
    float _dps;
    float _duration;
    float _tickTimer;
    ElementType _spellElement = ElementType.Fire;

    public void Init(float dps, float duration)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dps = dps;
        _duration = duration;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Slow — reuse MagmaSlow mechanic
        hp.ApplyMagmaSlow();

        // DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps * 0.5f, _spellElement);
        hp.ApplyBrittle(2f);
    }
}
