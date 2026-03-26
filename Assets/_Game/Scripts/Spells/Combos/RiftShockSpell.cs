using UnityEngine;

/// <summary>
/// Void + Lightning = Rift Shock
/// Teleport player to cursor position (max 10m) + AoE damage + stun at arrival.
/// Purple flash at origin and destination with lightning sparks.
/// </summary>
public static class RiftShockSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        if (charged) radius *= 1.3f;

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position;
        Vector3 toTarget = center - origin;
        toTarget.y = 0;

        // Clamp teleport distance to 10m
        float maxDist = 10f;
        if (toTarget.magnitude > maxDist)
            center = origin + toTarget.normalized * maxDist;
        center.y = origin.y;

        float stunDur = charged ? 2f : 1.5f;

        // ── VFX at origin ──

        // Purple flash sphere at origin
        var originFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(originFlash.GetComponent<SphereCollider>());
        originFlash.transform.position = origin + Vector3.up * 0.5f;
        originFlash.transform.localScale = Vector3.one * 1.2f;
        originFlash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.5f, 0.1f, 0.8f), 8f);
        originFlash.AddComponent<FlashShrink>().Init(0.2f);

        // Lightning sparks at origin
        SpawnLightningSparks(origin + Vector3.up * 0.5f, charged);

        // Origin particle burst (purple dispersal)
        var originBurstGO = new GameObject("RiftShockOriginBurst");
        originBurstGO.transform.position = origin + Vector3.up * 0.5f;
        var originPS = originBurstGO.AddComponent<ParticleSystem>();
        originPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var obm = originPS.main;
        obm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        obm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        obm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        obm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0.1f, 0.9f), new Color(0.7f, 0.3f, 1f));
        obm.simulationSpace = ParticleSystemSimulationSpace.World;
        obm.gravityModifier = 0.1f;
        obm.duration = 0.1f;
        obm.loop = false;

        var obe = originPS.emission;
        obe.rateOverTime = 0;
        obe.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var obs = originPS.shape;
        obs.shapeType = ParticleSystemShapeType.Sphere;
        obs.radius = 0.2f;

        var obSizeOL = originPS.sizeOverLifetime;
        obSizeOL.enabled = true;
        obSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var obMat = new Material(ShaderCache.ParticleShader);
        obMat.SetColor("_Color", new Color(0.5f, 0.1f, 0.9f));
        originBurstGO.GetComponent<ParticleSystemRenderer>().material = obMat;
        originPS.Play();
        Object.Destroy(originBurstGO, 0.5f);

        // ── Teleport player ──

        player.isInvulnerable = true;
        player.transform.position = center;

        // Brief invulnerability handler
        var invuln = player.gameObject.GetComponent<RiftShockInvuln>();
        if (invuln != null) Object.Destroy(invuln);
        invuln = player.gameObject.AddComponent<RiftShockInvuln>();
        invuln.Init(player, 0.15f);

        // ── VFX at destination ──

        // Purple flash sphere at destination
        var destFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(destFlash.GetComponent<SphereCollider>());
        destFlash.transform.position = center + Vector3.up * 0.5f;
        destFlash.transform.localScale = Vector3.one * 1.5f;
        destFlash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.6f, 0.15f, 1f), 10f);
        destFlash.AddComponent<FlashShrink>().Init(0.25f);

        // Lightning sparks at destination
        SpawnLightningSparks(center + Vector3.up * 0.5f, charged);

        // ── AoE damage + stun at arrival ──

        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                hp.ApplyStun(stunDur);
                GameFeel.ApplyKnockback(h.transform, center, damage * 0.25f);
            }
        }

        // Arrival energy burst particles
        var arrivalBurstGO = new GameObject("RiftShockArrivalBurst");
        arrivalBurstGO.transform.position = center + Vector3.up * 0.5f;
        var arrPS = arrivalBurstGO.AddComponent<ParticleSystem>();
        arrPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var abm = arrPS.main;
        abm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        abm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        abm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        abm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0.15f, 1f), new Color(0.8f, 0.4f, 1f));
        abm.simulationSpace = ParticleSystemSimulationSpace.World;
        abm.gravityModifier = 0.15f;
        abm.duration = 0.15f;
        abm.loop = false;

        var abe = arrPS.emission;
        abe.rateOverTime = 0;
        abe.SetBursts(new[] { new ParticleSystem.Burst(0f, charged ? 45 : 30) });

        var abs2 = arrPS.shape;
        abs2.shapeType = ParticleSystemShapeType.Sphere;
        abs2.radius = 0.3f;

        var abSizeOL = arrPS.sizeOverLifetime;
        abSizeOL.enabled = true;
        abSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var abColorOL = arrPS.colorOverLifetime;
        abColorOL.enabled = true;
        var abGrad = new Gradient();
        abGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.8f, 0.5f, 1f), 0f),
                new GradientColorKey(new Color(0.5f, 0.1f, 0.8f), 0.5f),
                new GradientColorKey(new Color(0.3f, 0f, 0.5f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        abColorOL.color = abGrad;

        var abMat = new Material(ShaderCache.ParticleShader);
        abMat.SetColor("_Color", new Color(0.6f, 0.2f, 1f));
        arrivalBurstGO.GetComponent<ParticleSystemRenderer>().material = abMat;
        arrPS.Play();
        Object.Destroy(arrivalBurstGO, 0.6f);

        // Expanding shockwave ring on ground at destination
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = center + Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.6f, 0.2f, 1f), 5f);
        ring.AddComponent<RiftShockRingExpand>().Init(radius * 2.5f, 0.3f);

        // Screen shake
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.3f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
        SFXSystem.Play(SFXSystem.SFXType.Dash, origin);
    }

    static void SpawnLightningSparks(Vector3 pos, bool charged)
    {
        // Spark particle burst
        var sparksGO = new GameObject("RiftShockSparks");
        sparksGO.transform.position = pos;
        var sparksPS = sparksGO.AddComponent<ParticleSystem>();
        sparksPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var sm = sparksPS.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        sm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.6f, 1f), new Color(1f, 0.9f, 1f));
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.gravityModifier = 0.5f;
        sm.duration = 0.1f;
        sm.loop = false;

        var se = sparksPS.emission;
        se.rateOverTime = 0;
        se.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(charged ? 25 : 15)) });

        var ss = sparksPS.shape;
        ss.shapeType = ParticleSystemShapeType.Sphere;
        ss.radius = 0.15f;

        var sSizeOL = sparksPS.sizeOverLifetime;
        sSizeOL.enabled = true;
        sSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var sparksMat = new Material(ShaderCache.ParticleShader);
        sparksMat.SetColor("_Color", new Color(0.8f, 0.6f, 1f));
        sparksGO.GetComponent<ParticleSystemRenderer>().material = sparksMat;
        sparksPS.Play();
        Object.Destroy(sparksGO, 0.4f);

        // Small lightning bolt lines using thin cubes at both points
        for (int i = 0; i < 4; i++)
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(bolt.GetComponent<BoxCollider>());
            bolt.transform.position = pos + Random.insideUnitSphere * 0.2f;
            bolt.transform.rotation = Random.rotation;
            float length = Random.Range(0.3f, 0.9f);
            bolt.transform.localScale = new Vector3(0.02f, 0.02f, length);
            bolt.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
                new Color(0.7f, 0.4f, 1f), 8f);
            Object.Destroy(bolt, 0.12f);
        }
    }
}

/// <summary>Handles brief invulnerability after rift shock teleport.</summary>
public class RiftShockInvuln : MonoBehaviour
{
    PlayerController _pc;
    float _timer;

    public void Init(PlayerController pc, float duration)
    {
        _pc = pc;
        _timer = duration;
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0)
        {
            if (_pc != null) _pc.isInvulnerable = false;
            Destroy(this);
        }
    }
}

/// <summary>Expanding ground ring that fades out.</summary>
public class RiftShockRingExpand : MonoBehaviour
{
    float _targetRadius;
    float _duration;
    float _timer;
    Renderer _renderer;
    Vector3 _startScale;

    public void Init(float targetRadius, float duration)
    {
        _targetRadius = targetRadius;
        _duration = duration;
        _renderer = GetComponent<Renderer>();
        _startScale = transform.localScale;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;

        float scale = Mathf.Lerp(_startScale.x, _targetRadius * 2f, t);
        transform.localScale = new Vector3(scale, _startScale.y, scale);

        if (_renderer != null)
        {
            Color baseCol = new Color(0.6f, 0.2f, 1f);
            _renderer.material.SetColor("_EmissionColor", baseCol * Mathf.Lerp(5f, 0f, t));
        }

        if (t >= 1f) Object.Destroy(gameObject);
    }
}
