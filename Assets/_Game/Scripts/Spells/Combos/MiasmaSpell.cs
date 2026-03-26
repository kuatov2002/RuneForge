using UnityEngine;

/// <summary>
/// Poison + Air = Miasma
/// Launches a green-white projectile that spawns a drifting poison cloud.
/// The cloud seeks the nearest enemy at 3 m/s, dealing DoT to all in radius.
/// </summary>
public static class MiasmaSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        float moveSpeed = charged ? 4f : 3f;
        if (charged) { radius *= 1.3f; }

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
        float finalSpeed = moveSpeed;

        // Create green-white projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MiasmaGlob";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.18f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0.9f, 0.5f), 4f);

        go.AddComponent<ZoneProjectile>().Init(dir, 14f, 0f, 18f,
            new Color(0.5f, 0.9f, 0.5f),
            (impactPos) => { SpawnMiasmaCloud(impactPos, finalDamage, finalRadius, finalDuration, finalSpeed); });

        Object.Destroy(go, 5f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnMiasmaCloud(Vector3 pos, float damage, float radius, float duration, float moveSpeed)
    {
        pos.y = 0.5f;

        var cloudGO = new GameObject("MiasmaCloud");
        cloudGO.transform.position = pos;

        // ── VFX: transparent green sphere core ──
        var coreSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(coreSphere.GetComponent<SphereCollider>());
        coreSphere.transform.SetParent(cloudGO.transform, false);
        coreSphere.transform.localPosition = Vector3.zero;
        coreSphere.transform.localScale = Vector3.one * radius * 0.8f;
        coreSphere.GetComponent<Renderer>().material = ShaderCache.NewMagic(
            new Color(0.15f, 0.6f, 0.1f), 1.5f);

        // Green fog particles with noise movement
        var fogGO = new GameObject("MiasmaFog");
        fogGO.transform.SetParent(cloudGO.transform, false);
        fogGO.transform.localPosition = Vector3.zero;
        var fogPS = fogGO.AddComponent<ParticleSystem>();
        fogPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var fMain = fogPS.main;
        fMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        fMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        fMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        fMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.7f, 0.1f, 0.2f),
            new Color(0.2f, 0.5f, 0.08f, 0.12f));
        fMain.simulationSpace = ParticleSystemSimulationSpace.World;
        fMain.gravityModifier = -0.08f;
        fMain.duration = duration;
        fMain.loop = true;
        fMain.maxParticles = 35;

        var fEmission = fogPS.emission;
        fEmission.rateOverTime = 12;

        var fShape = fogPS.shape;
        fShape.shapeType = ParticleSystemShapeType.Sphere;
        fShape.radius = radius * 0.4f;

        var fSizeOL = fogPS.sizeOverLifetime;
        fSizeOL.enabled = true;
        fSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.3f));

        var fColorOL = fogPS.colorOverLifetime;
        fColorOL.enabled = true;
        var fGrad = new Gradient();
        fGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.2f, 0.8f, 0.15f), 0f),
                new GradientColorKey(new Color(0.15f, 0.6f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.1f, 0.4f, 0.05f), 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.2f, 0.15f),
                new GradientAlphaKey(0.15f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        fColorOL.color = fGrad;

        // Noise for billowing movement
        var fNoise = fogPS.noise;
        fNoise.enabled = true;
        fNoise.strength = 0.6f;
        fNoise.frequency = 1.5f;
        fNoise.scrollSpeed = 0.6f;
        fNoise.octaveCount = 2;

        var fMat = new Material(ShaderCache.ParticleShader);
        fMat.SetColor("_Color", new Color(0.2f, 0.7f, 0.1f, 0.2f));
        fogGO.GetComponent<ParticleSystemRenderer>().material = fMat;
        fogPS.Play();

        // Wispy trailing particles
        var wispGO = new GameObject("MiasmaWisp");
        wispGO.transform.SetParent(cloudGO.transform, false);
        wispGO.transform.localPosition = Vector3.zero;
        var wispPS = wispGO.AddComponent<ParticleSystem>();
        wispPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var wMain = wispPS.main;
        wMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        wMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        wMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        wMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.9f, 0.2f, 0.5f),
            new Color(0.8f, 1f, 0.7f, 0.3f));
        wMain.simulationSpace = ParticleSystemSimulationSpace.World;
        wMain.gravityModifier = -0.15f;
        wMain.duration = duration;
        wMain.loop = true;
        wMain.maxParticles = 15;

        var wEmission = wispPS.emission;
        wEmission.rateOverTime = 8;

        var wShape = wispPS.shape;
        wShape.shapeType = ParticleSystemShapeType.Sphere;
        wShape.radius = radius * 0.3f;

        var wSizeOL = wispPS.sizeOverLifetime;
        wSizeOL.enabled = true;
        wSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var wMat = new Material(ShaderCache.ParticleShader);
        wMat.SetColor("_Color", new Color(0.4f, 0.9f, 0.3f, 0.4f));
        wispGO.GetComponent<ParticleSystemRenderer>().material = wMat;
        wispPS.Play();

        // Trigger collider
        var cloudCol = cloudGO.AddComponent<SphereCollider>();
        cloudCol.isTrigger = true;
        cloudCol.radius = radius;

        var cloudRb = cloudGO.AddComponent<Rigidbody>();
        cloudRb.isKinematic = true;
        cloudRb.useGravity = false;

        cloudGO.AddComponent<MiasmaCloudBehavior>().Init(damage, radius, duration, moveSpeed);

        Object.Destroy(cloudGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, pos);
    }
}

public class MiasmaCloudBehavior : MonoBehaviour
{
    float _dps;
    float _radius;
    float _duration;
    float _moveSpeed;
    float _tickTimer;
    float _seekTimer;
    Transform _target;

    public void Init(float dps, float radius, float duration, float moveSpeed)
    {
        _dps = dps;
        _radius = radius;
        _duration = duration;
        _moveSpeed = moveSpeed;
    }

    void Update()
    {
        // Seek new target every 1 second
        _seekTimer -= Time.deltaTime;
        if (_seekTimer <= 0)
        {
            _seekTimer = 1f;
            FindNearestEnemy();
        }

        // Drift toward target
        if (_target != null && _target.gameObject.activeSelf)
        {
            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude > 0.1f)
            {
                transform.position += toTarget.normalized * _moveSpeed * Time.deltaTime;
            }
        }

        // Keep cloud at consistent height
        Vector3 pos = transform.position;
        pos.y = 0.5f;
        transform.position = pos;
    }

    void FindNearestEnemy()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, 15f);
        float bestDist = float.MaxValue;
        Transform best = null;

        foreach (var c in nearby)
        {
            if (c.GetComponent<PlayerController>() != null) continue;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = c.transform;
            }
        }

        _target = best;
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
