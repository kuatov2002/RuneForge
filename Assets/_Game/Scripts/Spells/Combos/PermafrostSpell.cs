using UnityEngine;

/// <summary>
/// Water + Earth = Permafrost
/// Ground becomes ice. Enemies on it slide and lose movement control.
/// Balance: radius 3 (was 5)
/// </summary>
public static class PermafrostSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.4f; duration *= 1.5f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) { SpawnZone(center, damage, radius, duration); return; }

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;

        // Ice shard projectile
        var projGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(projGO.GetComponent<SphereCollider>());
        projGO.name = "PermafrostProjectile";
        projGO.transform.position = origin;
        projGO.transform.localScale = Vector3.one * 0.2f;
        projGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.6f, 0.88f, 0.95f), 3f);

        var col = projGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;

        var rb = projGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        float dmg = damage; float rad = radius; float dur = duration;
        var zp = projGO.AddComponent<ZoneProjectile>();
        zp.Init(dir, 15f, 2f, 16f, new Color(0.6f, 0.88f, 0.95f), (pos) => SpawnZone(pos, dmg, rad, dur));
    }

    public static void SpawnZone(Vector3 center, float damage, float radius, float duration)
    {
        center.y = 0.05f;

        var iceGO = new GameObject("PermafrostZone");
        iceGO.transform.position = center;

        // ── VFX: shimmering ice disc ──
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(iceGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2, 0.03f, radius * 2);
        Color iceCol = new Color(0.6f, 0.88f, 0.95f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewIce(iceCol, 0.5f);

        // Sparkle particles: tiny white sparkles above the ice surface
        var sparkleGO = new GameObject("PermafrostSparkles");
        sparkleGO.transform.SetParent(iceGO.transform, false);
        sparkleGO.transform.localPosition = Vector3.up * 0.1f;
        var sparklePS = sparkleGO.AddComponent<ParticleSystem>();
        sparklePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var sparkleMain = sparklePS.main;
        sparkleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        sparkleMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        sparkleMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        sparkleMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.95f, 1f, 0.9f),
            new Color(0.7f, 0.9f, 1f, 0.7f));
        sparkleMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkleMain.gravityModifier = -0.05f;
        sparkleMain.duration = duration;
        sparkleMain.loop = true;
        sparkleMain.maxParticles = 30;

        var sparkleEmission = sparklePS.emission;
        sparkleEmission.rateOverTime = 15;

        var sparkleShape = sparklePS.shape;
        sparkleShape.shapeType = ParticleSystemShapeType.Circle;
        sparkleShape.radius = radius * 0.9f;

        var sparkleSizeOL = sparklePS.sizeOverLifetime;
        sparkleSizeOL.enabled = true;
        // Pulse in and out for sparkle/twinkle effect
        var sparkleCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.5f, 0.3f),
            new Keyframe(0.7f, 0.9f),
            new Keyframe(1f, 0f));
        sparkleSizeOL.size = new ParticleSystem.MinMaxCurve(1f, sparkleCurve);

        var sparkleColorOL = sparklePS.colorOverLifetime;
        sparkleColorOL.enabled = true;
        var sGrad = new Gradient();
        sGrad.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.7f, 0.9f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        sparkleColorOL.color = sGrad;

        var sparkleMat = new Material(ShaderCache.ParticleShader);
        sparkleMat.SetColor("_Color", new Color(0.9f, 0.95f, 1f));
        sparkleGO.GetComponent<ParticleSystemRenderer>().material = sparkleMat;
        sparklePS.Play();

        // Frost mist hugging the ground
        var mistGO = new GameObject("PermafrostMist");
        mistGO.transform.SetParent(iceGO.transform, false);
        mistGO.transform.localPosition = Vector3.up * 0.05f;
        var mistPS = mistGO.AddComponent<ParticleSystem>();
        mistPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var mistMain = mistPS.main;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        mistMain.startColor = new Color(0.7f, 0.85f, 1f, 0.1f);
        mistMain.simulationSpace = ParticleSystemSimulationSpace.World;
        mistMain.gravityModifier = 0f;
        mistMain.duration = duration;
        mistMain.loop = true;

        var mistEmission = mistPS.emission;
        mistEmission.rateOverTime = 5;

        var mistShape = mistPS.shape;
        mistShape.shapeType = ParticleSystemShapeType.Circle;
        mistShape.radius = radius * 0.7f;

        var mistNoise = mistPS.noise;
        mistNoise.enabled = true;
        mistNoise.strength = 0.3f;
        mistNoise.frequency = 0.8f;

        var mistColorOL = mistPS.colorOverLifetime;
        mistColorOL.enabled = true;
        var mGrad = new Gradient();
        mGrad.SetKeys(
            new[] { new GradientColorKey(new Color(0.7f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.12f, 0.3f), new GradientAlphaKey(0.08f, 0.7f), new GradientAlphaKey(0f, 1f) });
        mistColorOL.color = mGrad;

        var mistMat = new Material(ShaderCache.ParticleShader);
        mistMat.SetColor("_Color", new Color(0.7f, 0.85f, 1f, 0.1f));
        mistGO.GetComponent<ParticleSystemRenderer>().material = mistMat;
        mistPS.Play();

        // Trigger
        var zoneCol = iceGO.AddComponent<SphereCollider>();
        zoneCol.isTrigger = true;
        zoneCol.radius = radius;

        var zoneRb = iceGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        iceGO.AddComponent<PermafrostZone>().Init(damage, duration);

        Object.Destroy(iceGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.1f);
    }
}

public class PermafrostZone : MonoBehaviour
{
    float _damage;
    float _tickTimer;

    public void Init(float damage, float duration)
    {
        _damage = damage;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Apply ice slide
        var erb = other.GetComponent<Rigidbody>();
        if (erb != null && !erb.isKinematic)
        {
            Vector3 vel = erb.linearVelocity;
            vel.y = 0;
            if (vel.sqrMagnitude > 0.5f)
            {
                erb.AddForce(vel.normalized * 8f, ForceMode.Acceleration);
            }
        }

        // Small DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 1f;
        hp.TakeDamage(_damage);
    }
}
