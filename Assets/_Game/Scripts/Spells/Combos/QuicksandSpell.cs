using UnityEngine;

/// <summary>
/// Poison + Earth = Quicksand
/// Launches a mud-brown glob that creates a root zone.
/// Enemies inside are repeatedly stunned (effectively rooted) and take DoT.
/// </summary>
public static class QuicksandSpell
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

        // Create mud-brown glob projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "QuicksandGlob";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.22f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.45f, 0.3f, 0.1f), 3f);

        go.AddComponent<ZoneProjectile>().Init(dir, 12f, 3f, 14f,
            new Color(0.45f, 0.3f, 0.1f),
            (impactPos) => { SpawnQuicksandZone(impactPos, finalDamage, finalRadius, finalDuration); });

        Object.Destroy(go, 5f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnQuicksandZone(Vector3 pos, float damage, float radius, float duration)
    {
        pos.y = 0.03f;

        var zoneGO = new GameObject("QuicksandZone");
        zoneGO.transform.position = pos;

        // ── VFX: brown-green disc on ground ──
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(zoneGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewLava(new Color(0.3f, 0.2f, 0.05f));

        // Sinking particles — particles moving downward with decreasing scale.y
        var sinkGO = new GameObject("QuicksandSink");
        sinkGO.transform.SetParent(zoneGO.transform, false);
        sinkGO.transform.localPosition = Vector3.up * 0.15f;
        var sinkPS = sinkGO.AddComponent<ParticleSystem>();
        sinkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var sMain = sinkPS.main;
        sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        sMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        sMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.3f, 0.1f, 0.7f),
            new Color(0.25f, 0.2f, 0.08f, 0.5f));
        sMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sMain.gravityModifier = 0.3f; // particles sink down
        sMain.duration = duration;
        sMain.loop = true;
        sMain.maxParticles = 25;

        var sEmission = sinkPS.emission;
        sEmission.rateOverTime = 10;

        var sShape = sinkPS.shape;
        sShape.shapeType = ParticleSystemShapeType.Circle;
        sShape.radius = radius * 0.7f;

        var sSizeOL = sinkPS.sizeOverLifetime;
        sSizeOL.enabled = true;
        sSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var sColorOL = sinkPS.colorOverLifetime;
        sColorOL.enabled = true;
        var sGrad = new Gradient();
        sGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.45f, 0.35f, 0.15f), 0f),
                new GradientColorKey(new Color(0.3f, 0.25f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.15f, 0.12f, 0.05f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.4f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        sColorOL.color = sGrad;

        var sMat = new Material(ShaderCache.ParticleShader);
        sMat.SetColor("_Color", new Color(0.4f, 0.3f, 0.1f, 0.6f));
        sinkGO.GetComponent<ParticleSystemRenderer>().material = sMat;
        sinkPS.Play();

        // Green poison bubbles emerging from the mud
        var poisonGO = new GameObject("QuicksandPoison");
        poisonGO.transform.SetParent(zoneGO.transform, false);
        poisonGO.transform.localPosition = Vector3.up * 0.05f;
        var poisonPS = poisonGO.AddComponent<ParticleSystem>();
        poisonPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var pMain = poisonPS.main;
        pMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        pMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        pMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        pMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.2f, 0.6f, 0.1f, 0.5f),
            new Color(0.15f, 0.4f, 0.08f, 0.3f));
        pMain.simulationSpace = ParticleSystemSimulationSpace.World;
        pMain.gravityModifier = -0.15f;
        pMain.duration = duration;
        pMain.loop = true;
        pMain.maxParticles = 15;

        var pEmission = poisonPS.emission;
        pEmission.rateOverTime = 6;

        var pShape = poisonPS.shape;
        pShape.shapeType = ParticleSystemShapeType.Circle;
        pShape.radius = radius * 0.5f;

        var pMat = new Material(ShaderCache.ParticleShader);
        pMat.SetColor("_Color", new Color(0.2f, 0.6f, 0.1f, 0.4f));
        poisonGO.GetComponent<ParticleSystemRenderer>().material = pMat;
        poisonPS.Play();

        // Mud bubble surface animation — subtle pulsing disc
        var pulseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(pulseDisc.GetComponent<CapsuleCollider>());
        pulseDisc.transform.SetParent(zoneGO.transform, false);
        pulseDisc.transform.localPosition = Vector3.up * 0.05f;
        pulseDisc.transform.localScale = new Vector3(radius * 1.6f, 0.02f, radius * 1.6f);
        var pulseRend = pulseDisc.GetComponent<Renderer>();
        pulseRend.material = ShaderCache.NewEmissive(new Color(0.3f, 0.25f, 0.08f), 1.5f);

        // Trigger collider
        var zoneCol = zoneGO.AddComponent<SphereCollider>();
        zoneCol.isTrigger = true;
        zoneCol.radius = radius;

        var zoneRb = zoneGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        zoneGO.AddComponent<QuicksandZone>().Init(damage, duration, pulseRend);

        Object.Destroy(zoneGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, pos);
    }
}

public class QuicksandZone : MonoBehaviour
{
    float _dps;
    float _duration;
    float _tickTimer;
    float _stunTimer;
    float _pulseTimer;
    Renderer _pulseRenderer;
    ElementType _spellElement = ElementType.Fire;

    public void Init(float dps, float duration, Renderer pulseRenderer)
    {
        _spellElement = ComboSpellFactory.CurrentCastElement ?? ElementType.Fire;
        _dps = dps;
        _duration = duration;
        _pulseRenderer = pulseRenderer;
    }

    void Update()
    {
        // Pulse the mud disc between dark brown and lighter brown
        if (_pulseRenderer != null)
        {
            _pulseTimer += Time.deltaTime * 2.5f;
            float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
            Color dark = new Color(0.2f, 0.15f, 0.05f);
            Color light = new Color(0.45f, 0.35f, 0.15f);
            Color current = Color.Lerp(dark, light, pulse);
            _pulseRenderer.material.SetColor("_EmissionColor", current * Mathf.Lerp(1f, 3f, pulse));
            _pulseRenderer.material.color = current;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Root: refresh stun every 0.3s to keep enemies locked in place
        _stunTimer -= Time.deltaTime;
        if (_stunTimer <= 0)
        {
            _stunTimer = 0.3f;
            hp.ApplyStun(0.4f);
        }

        // Brown tint on enemies inside
        var rend = other.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = Color.Lerp(rend.material.color, new Color(0.4f, 0.3f, 0.15f), 0.1f);
        }

        // DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps * 0.5f, _spellElement);
    }
}
