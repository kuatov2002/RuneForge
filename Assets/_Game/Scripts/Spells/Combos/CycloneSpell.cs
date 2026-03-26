using UnityEngine;

/// <summary>
/// Lightning + Air = Cyclone
/// Projectile that creates a moving whirlwind. The cyclone chases the nearest
/// enemy, pulls others toward its center, and deals DoT. On expiry, a final
/// burst pushes enemies outward.
/// </summary>
public static class CycloneSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; duration *= 1.3f; damage *= 1.2f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        // Create projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CycloneProjectile";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.18f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.6f, 0.85f, 1f), 4f);

        // Trail
        var trail = go.AddComponent<TrailRenderer>();
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;
        trail.time = 0.2f;
        trail.material = ShaderCache.NewEmissive(new Color(0.6f, 0.85f, 1f), 3f);
        trail.startColor = new Color(0.6f, 0.9f, 1f);
        trail.endColor = new Color(0.4f, 0.7f, 1f, 0f);

        go.AddComponent<ZoneProjectile>().Init(dir, 12f, 0f, 18f, new Color(0.6f, 0.85f, 1f),
            (impactPos) => { SpawnCycloneZone(impactPos, damage, radius, duration, charged); });

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnCycloneZone(Vector3 pos, float damage, float radius, float duration, bool charged)
    {
        pos.y = 0;

        var zoneGO = new GameObject("CycloneZone");
        zoneGO.transform.position = pos;

        // Spinning cube particles (8 small cubes rotating at different heights)
        for (int i = 0; i < 8; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cube.GetComponent<BoxCollider>());
            cube.name = "CycloneDebris";
            cube.transform.SetParent(zoneGO.transform, false);

            float height = Random.Range(0.3f, 2.5f);
            float angle = (360f / 8f) * i;
            float orbitRadius = radius * Random.Range(0.3f, 0.7f);
            cube.transform.localPosition = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * orbitRadius,
                height,
                Mathf.Sin(angle * Mathf.Deg2Rad) * orbitRadius);

            float s = Random.Range(0.08f, 0.18f);
            cube.transform.localScale = new Vector3(s, s, s);
            cube.transform.rotation = Random.rotation;

            Color cubeCol = new Color(
                Random.Range(0.5f, 0.7f),
                Random.Range(0.7f, 0.9f),
                1f);
            cube.GetComponent<Renderer>().material = ShaderCache.NewEmissive(cubeCol, 2f);
        }

        // Wind particle system
        var windGO = new GameObject("CycloneWind");
        windGO.transform.SetParent(zoneGO.transform, false);
        windGO.transform.localPosition = Vector3.up * 1f;
        var windPS = windGO.AddComponent<ParticleSystem>();
        windPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var windMain = windPS.main;
        windMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        windMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        windMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
        windMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.85f, 1f, 0.6f),
            new Color(0.5f, 0.75f, 1f, 0.4f));
        windMain.simulationSpace = ParticleSystemSimulationSpace.World;
        windMain.gravityModifier = -0.2f;
        windMain.duration = duration;
        windMain.loop = true;
        windMain.maxParticles = 40;

        var windEmission = windPS.emission;
        windEmission.rateOverTime = 20;

        var windShape = windPS.shape;
        windShape.shapeType = ParticleSystemShapeType.Circle;
        windShape.radius = radius * 0.5f;

        var windSizeOL = windPS.sizeOverLifetime;
        windSizeOL.enabled = true;
        windSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var windColorOL = windPS.colorOverLifetime;
        windColorOL.enabled = true;
        var wGrad = new Gradient();
        wGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.7f, 0.85f, 1f), 0f),
                new GradientColorKey(new Color(0.5f, 0.75f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(0.3f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        windColorOL.color = wGrad;

        var windNoise = windPS.noise;
        windNoise.enabled = true;
        windNoise.strength = 0.6f;
        windNoise.frequency = 2f;

        var windMat = new Material(Shader.Find("Particles/Standard Unlit"));
        windMat.SetColor("_Color", new Color(0.7f, 0.85f, 1f, 0.5f));
        windGO.GetComponent<ParticleSystemRenderer>().material = windMat;
        windPS.Play();

        // Core glow
        var coreGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(coreGlow.GetComponent<SphereCollider>());
        coreGlow.transform.SetParent(zoneGO.transform, false);
        coreGlow.transform.localPosition = Vector3.up * 1f;
        coreGlow.transform.localScale = Vector3.one * radius * 0.4f;
        coreGlow.GetComponent<Renderer>().material = ShaderCache.NewMagic(
            new Color(0.6f, 0.85f, 1f), 1.5f);

        // Trigger collider
        var zoneColl = zoneGO.AddComponent<SphereCollider>();
        zoneColl.isTrigger = true;
        zoneColl.radius = radius;

        var zoneRb = zoneGO.AddComponent<Rigidbody>();
        zoneRb.isKinematic = true;
        zoneRb.useGravity = false;

        zoneGO.AddComponent<CycloneZone>().Init(damage, radius, duration, charged);

        Object.Destroy(zoneGO, duration + 0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, pos);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.15f);
    }
}

public class CycloneZone : MonoBehaviour
{
    float _damage;
    float _radius;
    float _duration;
    bool _charged;
    float _tickTimer;
    float _elapsed;
    float _spinAngle;
    bool _expired;

    public void Init(float damage, float radius, float duration, bool charged)
    {
        _damage = damage;
        _radius = radius;
        _duration = duration;
        _charged = charged;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        // Spin the debris cubes around the center
        _spinAngle += Time.deltaTime * 360f; // one full rotation per second
        int childIdx = 0;
        foreach (Transform child in transform)
        {
            if (child.name != "CycloneDebris") continue;
            float baseAngle = (360f / 8f) * childIdx + _spinAngle;
            float height = child.localPosition.y;
            float orbitRadius = _radius * Mathf.Lerp(0.3f, 0.7f, (childIdx % 4) / 3f);
            child.localPosition = new Vector3(
                Mathf.Cos(baseAngle * Mathf.Deg2Rad) * orbitRadius,
                height + Mathf.Sin(_elapsed * 3f + childIdx) * 0.1f,
                Mathf.Sin(baseAngle * Mathf.Deg2Rad) * orbitRadius);
            child.Rotate(Vector3.one * 200f * Time.deltaTime);
            childIdx++;
        }

        // Move toward nearest enemy
        Transform target = FindNearestEnemy();
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0;
            if (toTarget.magnitude > 0.5f)
            {
                transform.position += toTarget.normalized * 3f * Time.deltaTime;
            }
        }

        // Check for expiry and final burst
        if (_elapsed >= _duration && !_expired)
        {
            _expired = true;
            FinalBurst();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (_expired) return;
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Pull toward center
        Vector3 toCenter = transform.position - other.transform.position;
        toCenter.y = 0;
        if (toCenter.magnitude > 0.3f)
        {
            var erb = other.GetComponent<Rigidbody>();
            if (erb != null && !erb.isKinematic)
            {
                erb.AddForce(toCenter.normalized * 4f, ForceMode.Acceleration);
            }
            else
            {
                other.transform.position += toCenter.normalized * 4f * Time.deltaTime;
            }
        }

        // DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;

        hp.TakeDamage(_damage);
    }

    Transform FindNearestEnemy()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, _radius * 3f);
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var c in nearby)
        {
            if (c.GetComponent<PlayerController>() != null) continue;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < bestDist) { bestDist = d; best = c.transform; }
        }
        return best;
    }

    void FinalBurst()
    {
        Vector3 pos = transform.position;
        float burstRadius = _charged ? _radius * 1.5f : _radius;
        float burstForce = _charged ? 12f : 8f;

        // Push enemies outward
        Collider[] hits = Physics.OverlapSphere(pos, burstRadius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            GameFeel.ApplyKnockback(h.transform, pos, burstForce);
            hp.TakeDamage(_damage * 0.5f);
        }

        // Burst VFX: particles spraying outward
        var burstGO = new GameObject("CycloneBurst");
        burstGO.transform.position = pos + Vector3.up * 0.5f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 0.85f, 1f),
            new Color(0.8f, 0.95f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.2f;
        main.duration = 0.2f;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, _charged ? 30 : 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var burstMat = new Material(Shader.Find("Particles/Standard Unlit"));
        burstMat.SetColor("_Color", new Color(0.7f, 0.85f, 1f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = burstMat;
        ps.Play();
        Object.Destroy(burstGO, 0.8f);

        // Flash
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = pos + Vector3.up * 0.5f;
        flash.transform.localScale = Vector3.one * 0.5f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.95f, 1f), 8f);
        flash.AddComponent<FlashShrink>().Init(0.2f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.25f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }
}
