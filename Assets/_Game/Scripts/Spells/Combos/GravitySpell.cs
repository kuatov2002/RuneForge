using UnityEngine;

/// <summary>
/// Void + Earth = Gravity
/// Projectile that creates an instant gravity slam. Stuns and damages all enemies in radius.
/// </summary>
public static class GravitySpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        if (charged) radius *= 1.3f;

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) { SpawnSlam(center, damage, radius, charged); return; }

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;
        dir.Normalize();

        // Dark purple-brown projectile
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "GravityProjectile";
        go.transform.position = origin + dir * 0.6f;
        go.transform.localScale = Vector3.one * 0.2f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        go.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.35f, 0.1f, 0.5f), 4f);

        // Trail particles
        var trailGO = new GameObject("GravityTrail");
        trailGO.transform.SetParent(go.transform, false);
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var tm = trailPS.main;
        tm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        tm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        tm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        tm.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.15f, 0.5f), new Color(0.25f, 0.1f, 0.3f));
        tm.simulationSpace = ParticleSystemSimulationSpace.World;
        tm.gravityModifier = 0.3f;
        tm.loop = true;
        tm.maxParticles = 25;

        var te = trailPS.emission;
        te.rateOverTime = 20;

        var ts = trailPS.shape;
        ts.shapeType = ParticleSystemShapeType.Sphere;
        ts.radius = 0.08f;

        var tMat = new Material(Shader.Find("Particles/Standard Unlit"));
        tMat.SetColor("_Color", new Color(0.35f, 0.1f, 0.5f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = tMat;
        trailPS.Play();

        float dmg = damage; float rad = radius; bool ch = charged;
        var zp = go.AddComponent<ZoneProjectile>();
        zp.Init(dir, 14f, 0f, 18f, new Color(0.35f, 0.1f, 0.5f),
            (pos) => SpawnSlam(pos, dmg, rad, ch));

        SFXSystem.Play(SFXSystem.SFXType.Cast, origin);
    }

    static void SpawnSlam(Vector3 center, float damage, float radius, bool charged)
    {
        center.y = 0.05f;

        float stunDuration = charged ? 1.5f : 1f;

        // AoE damage + stun
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                hp.ApplyStun(stunDuration);

                // Visual slam: push enemy down briefly
                var erb = h.GetComponent<Rigidbody>();
                if (erb != null && !erb.isKinematic)
                {
                    erb.AddForce(Vector3.down * 15f, ForceMode.Impulse);
                }
            }
        }

        // VFX: dark purple shockwave disc on ground (expanding)
        var shockDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(shockDisc.GetComponent<CapsuleCollider>());
        shockDisc.transform.position = center;
        shockDisc.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
        shockDisc.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.4f, 0.05f, 0.6f), 5f);
        shockDisc.AddComponent<GravityShockwave>().Init(radius * 3f, 0.35f);

        // Dust cloud burst
        var dustGO = new GameObject("GravitySlamDust");
        dustGO.transform.position = center + Vector3.up * 0.2f;
        var dustPS = dustGO.AddComponent<ParticleSystem>();
        dustPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var dustMain = dustPS.main;
        dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        dustMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        dustMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        dustMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.3f, 0.25f, 0.6f),
            new Color(0.3f, 0.2f, 0.15f, 0.4f));
        dustMain.simulationSpace = ParticleSystemSimulationSpace.World;
        dustMain.gravityModifier = 0.3f;
        dustMain.duration = 0.2f;
        dustMain.loop = false;

        var dustEmission = dustPS.emission;
        dustEmission.rateOverTime = 0;
        dustEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var dustShape = dustPS.shape;
        dustShape.shapeType = ParticleSystemShapeType.Circle;
        dustShape.radius = radius * 0.5f;

        var dustSizeOL = dustPS.sizeOverLifetime;
        dustSizeOL.enabled = true;
        dustSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.3f));

        var dustColorOL = dustPS.colorOverLifetime;
        dustColorOL.enabled = true;
        var dGrad = new Gradient();
        dGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.45f, 0.35f, 0.25f), 0f),
                new GradientColorKey(new Color(0.35f, 0.25f, 0.2f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(0.3f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        dustColorOL.color = dGrad;

        var dustMat = new Material(Shader.Find("Particles/Standard Unlit"));
        dustMat.SetColor("_Color", new Color(0.4f, 0.3f, 0.25f));
        dustGO.GetComponent<ParticleSystemRenderer>().material = dustMat;
        dustPS.Play();
        Object.Destroy(dustGO, 1f);

        // Purple energy particles bursting downward
        var energyGO = new GameObject("GravityEnergyBurst");
        energyGO.transform.position = center + Vector3.up * 2f;
        var energyPS = energyGO.AddComponent<ParticleSystem>();
        energyPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var eMain = energyPS.main;
        eMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        eMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 14f);
        eMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        eMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.5f, 0.1f, 0.8f), new Color(0.3f, 0f, 0.6f));
        eMain.simulationSpace = ParticleSystemSimulationSpace.World;
        eMain.gravityModifier = 2f;
        eMain.duration = 0.15f;
        eMain.loop = false;

        var eEmission = energyPS.emission;
        eEmission.rateOverTime = 0;
        eEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, charged ? 35 : 20) });

        var eShape = energyPS.shape;
        eShape.shapeType = ParticleSystemShapeType.Circle;
        eShape.radius = radius * 0.4f;

        var eVel = energyPS.velocityOverLifetime;
        eVel.enabled = true;
        eVel.space = ParticleSystemSimulationSpace.World;
        eVel.y = new ParticleSystem.MinMaxCurve(-10f, -15f);

        var eSizeOL = energyPS.sizeOverLifetime;
        eSizeOL.enabled = true;
        eSizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var eMat = new Material(Shader.Find("Particles/Standard Unlit"));
        eMat.SetColor("_Color", new Color(0.5f, 0.1f, 0.8f));
        energyGO.GetComponent<ParticleSystemRenderer>().material = eMat;
        energyPS.Play();
        Object.Destroy(energyGO, 0.6f);

        // Ground crack mark
        var crackDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(crackDisc.GetComponent<CapsuleCollider>());
        crackDisc.transform.position = center;
        crackDisc.transform.localScale = new Vector3(radius * 1.8f, 0.01f, radius * 1.8f);
        crackDisc.GetComponent<Renderer>().material = ShaderCache.NewStone(
            new Color(0.15f, 0.08f, 0.05f));
        Object.Destroy(crackDisc, 1.5f);

        // Flash
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = center + Vector3.up * 0.3f;
        flash.transform.localScale = Vector3.one * 0.4f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.6f, 0.15f, 1f), 8f);
        flash.AddComponent<FlashShrink>().Init(0.2f);

        // Screen shake and hitstop
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.4f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }
}

/// <summary>Expanding shockwave disc that fades out.</summary>
public class GravityShockwave : MonoBehaviour
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
            Color c = _renderer.material.GetColor("_EmissionColor");
            _renderer.material.SetColor("_EmissionColor", c * Mathf.Lerp(5f, 0f, t));
        }

        if (t >= 1f) Object.Destroy(gameObject);
    }
}
