using UnityEngine;

/// <summary>
/// Lightning + Water = Frost Shock
/// Projectile that creates an electrified ice zone. Enemies inside get
/// frozen + electric DoT. Frozen enemies take 2x from next non-FrostShock source.
/// </summary>
public static class FrostShockSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; duration *= 1.4f; damage *= 1.2f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        // Create projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "FrostShockProjectile";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.18f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 0.9f, 1f), 5f);

        // Trail
        var trail = go.AddComponent<TrailRenderer>();
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;
        trail.time = 0.25f;
        trail.material = ShaderCache.NewEmissive(new Color(0.3f, 0.8f, 1f), 3f);
        trail.startColor = new Color(0.4f, 0.9f, 1f);
        trail.endColor = new Color(0.2f, 0.6f, 1f, 0f);

        go.AddComponent<ZoneProjectile>().Init(dir, 15f, 0f, 18f, new Color(0.3f, 0.9f, 1f),
            (impactPos) => { SpawnFrostShockZone(impactPos, damage, radius, duration); });

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnFrostShockZone(Vector3 pos, float damage, float radius, float duration)
    {
        pos.y = 0.05f;

        var zoneGO = new GameObject("FrostShockZone");
        zoneGO.transform.position = pos;

        // Ice disc (like Permafrost)
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.SetParent(zoneGO.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2, 0.03f, radius * 2);
        Color iceCol = new Color(0.4f, 0.8f, 0.95f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewIce(iceCol, 0.5f);

        // Sparking yellow particles on top of ice
        var sparkGO = new GameObject("FrostShockSparks");
        sparkGO.transform.SetParent(zoneGO.transform, false);
        sparkGO.transform.localPosition = Vector3.up * 0.15f;
        var sparkPS = sparkGO.AddComponent<ParticleSystem>();
        sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var sparkMain = sparkPS.main;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 0.3f, 0.9f),
            new Color(0.8f, 0.9f, 1f, 0.7f));
        sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkMain.gravityModifier = 0.3f;
        sparkMain.duration = duration;
        sparkMain.loop = true;
        sparkMain.maxParticles = 25;

        var sparkEmission = sparkPS.emission;
        sparkEmission.rateOverTime = 12;

        var sparkShape = sparkPS.shape;
        sparkShape.shapeType = ParticleSystemShapeType.Circle;
        sparkShape.radius = radius * 0.8f;

        var sparkSizeOL = sparkPS.sizeOverLifetime;
        sparkSizeOL.enabled = true;
        sparkSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var sparkMat = new Material(ShaderCache.ParticleShader);
        sparkMat.SetColor("_Color", new Color(1f, 1f, 0.3f));
        sparkGO.GetComponent<ParticleSystemRenderer>().material = sparkMat;
        sparkPS.Play();

        // Frost mist particles
        var mistGO = new GameObject("FrostShockMist");
        mistGO.transform.SetParent(zoneGO.transform, false);
        mistGO.transform.localPosition = Vector3.up * 0.05f;
        var mistPS = mistGO.AddComponent<ParticleSystem>();
        mistPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var mistMain = mistPS.main;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        mistMain.startColor = new Color(0.5f, 0.8f, 1f, 0.08f);
        mistMain.simulationSpace = ParticleSystemSimulationSpace.World;
        mistMain.gravityModifier = -0.05f;
        mistMain.duration = duration;
        mistMain.loop = true;

        var mistEmission = mistPS.emission;
        mistEmission.rateOverTime = 5;

        var mistShape = mistPS.shape;
        mistShape.shapeType = ParticleSystemShapeType.Circle;
        mistShape.radius = radius * 0.6f;

        var mistColorOL = mistPS.colorOverLifetime;
        mistColorOL.enabled = true;
        var mGrad = new Gradient();
        mGrad.SetKeys(
            new[] { new GradientColorKey(new Color(0.5f, 0.8f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.1f, 0.3f), new GradientAlphaKey(0.06f, 0.7f), new GradientAlphaKey(0f, 1f) });
        mistColorOL.color = mGrad;

        var mistMat = new Material(ShaderCache.ParticleShader);
        mistMat.SetColor("_Color", new Color(0.5f, 0.8f, 1f, 0.1f));
        mistGO.GetComponent<ParticleSystemRenderer>().material = mistMat;
        mistPS.Play();

        // Trigger collider
        var zoneColl = zoneGO.AddComponent<SphereCollider>();
        zoneColl.isTrigger = true;
        zoneColl.radius = radius;

        var zoneRb = zoneGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        zoneGO.AddComponent<FrostShockZone>().Init(damage, radius, duration);

        Object.Destroy(zoneGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Freeze, pos);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
    }
}

public class FrostShockZone : MonoBehaviour
{
    float _damage;
    float _radius;
    float _duration;
    float _tickTimer;

    public void Init(float damage, float radius, float duration)
    {
        _damage = damage;
        _radius = radius;
        _duration = duration;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Keep enemy frozen while in zone
        hp.ApplyFreeze(0.8f);

        // Add vulnerability mark
        var mark = other.GetComponent<FrostShockMark>();
        if (mark == null)
            mark = other.gameObject.AddComponent<FrostShockMark>();
        mark.Refresh();

        // Electric DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;

        hp.TakeDamage(_damage);
        SFXSystem.Play(SFXSystem.SFXType.Hit, other.transform.position);
    }
}

/// <summary>
/// Marks an enemy that was frozen by FrostShock.
/// Next non-FrostShock damage source deals 2x damage.
/// Auto-removes after 3 seconds if not refreshed.
/// </summary>
public class FrostShockMark : MonoBehaviour
{
    float _expireTimer = 3f;
    bool _consumed;

    public void Refresh()
    {
        _expireTimer = 3f;
        _consumed = false;
    }

    /// <summary>
    /// Call before applying damage from a non-FrostShock source.
    /// Returns the damage multiplier (2x if mark active, 1x otherwise).
    /// Consumes the mark on use.
    /// </summary>
    public float ConsumeMark()
    {
        if (_consumed) return 1f;
        _consumed = true;
        Destroy(this, 0.1f);
        return 2f;
    }

    void Update()
    {
        _expireTimer -= Time.deltaTime;
        if (_expireTimer <= 0f)
            Destroy(this);
    }
}
