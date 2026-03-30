using UnityEngine;

/// <summary>
/// Water + Air = Geyser
/// Water pillar launches all enemies in radius upward. Deals damage and interrupts.
/// Balance: radius 1.8 (was 2.5), damage 10 (was 15)
/// </summary>
public static class GeyserSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        if (charged) { radius *= 1.4f; damage *= 1.5f; }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) { DoGeyser(center, damage, radius); return; }

        Vector3 origin = player.transform.position + Vector3.up * 0.5f;
        Vector3 dir = (center - player.transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = player.transform.forward;

        // Water bomb sphere
        var projGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(projGO.GetComponent<SphereCollider>());
        projGO.name = "GeyserProjectile";
        projGO.transform.position = origin;
        projGO.transform.localScale = Vector3.one * 0.25f;
        projGO.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 0.6f, 1f), 3f);

        var col = projGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;

        var rb = projGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        float dmg = damage; float rad = radius;
        var zp = projGO.AddComponent<ZoneProjectile>();
        zp.Init(dir, 12f, 5f, 16f, new Color(0.3f, 0.6f, 1f), (pos) => DoGeyser(pos, dmg, rad));
    }

    static void DoGeyser(Vector3 center, float damage, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            hp.TakeDamage(damage, ComboSpellFactory.CurrentCastElement ?? ElementType.Fire);

            // Launch enemy upward
            var erb = h.GetComponent<Rigidbody>();
            if (erb != null && !erb.isKinematic)
            {
                erb.AddForce(Vector3.up * 12f + Random.insideUnitSphere * 2f, ForceMode.Impulse);
            }
            else
            {
                hp.ApplyStun(1.5f);
            }

            hp.ApplyStun(1f);
        }

        // ── VFX ──

        // 1) Water column: many small blue particles moving rapidly upward in cylinder shape
        var columnGO = new GameObject("GeyserColumn");
        columnGO.transform.position = center;
        var columnPS = columnGO.AddComponent<ParticleSystem>();
        columnPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var colMain = columnPS.main;
        colMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        colMain.startSpeed = new ParticleSystem.MinMaxCurve(10f, 18f);
        colMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        colMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.6f, 1f, 0.8f),
            new Color(0.5f, 0.8f, 1f, 0.9f));
        colMain.simulationSpace = ParticleSystemSimulationSpace.World;
        colMain.gravityModifier = 0f;
        colMain.duration = 0.25f;
        colMain.loop = false;
        colMain.maxParticles = 80;

        var colEmission = columnPS.emission;
        colEmission.rateOverTime = 0;
        colEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

        var colShape = columnPS.shape;
        colShape.shapeType = ParticleSystemShapeType.Circle;
        colShape.radius = radius * 0.4f;

        // Velocity: straight up
        var colVel = columnPS.velocityOverLifetime;
        colVel.enabled = true;
        colVel.space = ParticleSystemSimulationSpace.World;
        colVel.y = new ParticleSystem.MinMaxCurve(8f, 15f);

        var colSizeOL = columnPS.sizeOverLifetime;
        colSizeOL.enabled = true;
        colSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var colColorOL = columnPS.colorOverLifetime;
        colColorOL.enabled = true;
        var cGrad = new Gradient();
        cGrad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.5f, 0.8f, 1f), 0f),
                new GradientColorKey(new Color(0.2f, 0.5f, 1f), 0.6f),
                new GradientColorKey(new Color(0.6f, 0.85f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.6f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colColorOL.color = cGrad;

        var colMat = new Material(ShaderCache.ParticleShader);
        colMat.SetColor("_Color", new Color(0.3f, 0.6f, 1f));
        columnGO.GetComponent<ParticleSystemRenderer>().material = colMat;
        columnPS.Play();
        Object.Destroy(columnGO, 1f);

        // 2) Splash at top: particles spraying outward at the peak
        var splashGO = new GameObject("GeyserSplash");
        splashGO.transform.position = center + Vector3.up * 5f;
        var splashPS = splashGO.AddComponent<ParticleSystem>();
        splashPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var splashMain = splashPS.main;
        splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        splashMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        splashMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        splashMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.7f, 1f, 0.7f),
            new Color(0.7f, 0.9f, 1f, 0.8f));
        splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
        splashMain.gravityModifier = 1f; // fall back down
        splashMain.duration = 0.1f;
        splashMain.loop = false;
        // Delay the splash to match the column reaching the top
        splashMain.startDelay = 0.15f;

        var splashEmission = splashPS.emission;
        splashEmission.rateOverTime = 0;
        splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var splashShape = splashPS.shape;
        splashShape.shapeType = ParticleSystemShapeType.Sphere;
        splashShape.radius = 0.3f;

        var splashSizeOL = splashPS.sizeOverLifetime;
        splashSizeOL.enabled = true;
        splashSizeOL.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        splashGO.GetComponent<ParticleSystemRenderer>().material = colMat;
        splashPS.Play();
        Object.Destroy(splashGO, 1.2f);

        // 3) Mist settling down after
        var mistGO = new GameObject("GeyserMist");
        mistGO.transform.position = center + Vector3.up * 2f;
        var mistPS = mistGO.AddComponent<ParticleSystem>();
        mistPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var mistMain = mistPS.main;
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        mistMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        mistMain.startColor = new Color(0.7f, 0.85f, 1f, 0.12f);
        mistMain.simulationSpace = ParticleSystemSimulationSpace.World;
        mistMain.gravityModifier = 0.3f; // settle down
        mistMain.duration = 0.3f;
        mistMain.loop = false;
        mistMain.startDelay = 0.2f;

        var mistEmission = mistPS.emission;
        mistEmission.rateOverTime = 0;
        mistEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        var mistShape = mistPS.shape;
        mistShape.shapeType = ParticleSystemShapeType.Sphere;
        mistShape.radius = radius;

        var mistColorOL = mistPS.colorOverLifetime;
        mistColorOL.enabled = true;
        var mGrad = new Gradient();
        mGrad.SetKeys(
            new[] { new GradientColorKey(new Color(0.7f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.15f, 0f), new GradientAlphaKey(0.08f, 0.5f), new GradientAlphaKey(0f, 1f) });
        mistColorOL.color = mGrad;

        var mistMat = new Material(ShaderCache.ParticleShader);
        mistMat.SetColor("_Color", new Color(0.7f, 0.85f, 1f, 0.15f));
        mistGO.GetComponent<ParticleSystemRenderer>().material = mistMat;
        mistPS.Play();
        Object.Destroy(mistGO, 2f);

        // 4) Water column cylinder — visible animated water pillar
        var waterPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(waterPillar.GetComponent<CapsuleCollider>());
        waterPillar.transform.position = center + Vector3.up * 2f;
        waterPillar.transform.localScale = new Vector3(radius * 0.8f, 3f, radius * 0.8f);
        waterPillar.GetComponent<Renderer>().material = ShaderCache.NewWater(new Color(0.2f, 0.5f, 1f), 0.65f);
        Object.Destroy(waterPillar, 0.6f);

        // 5) Base water pool disc
        var waterPool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(waterPool.GetComponent<CapsuleCollider>());
        waterPool.transform.position = center + Vector3.up * 0.03f;
        waterPool.transform.localScale = new Vector3(radius * 2.5f, 0.02f, radius * 2.5f);
        waterPool.GetComponent<Renderer>().material = ShaderCache.NewWater(new Color(0.15f, 0.4f, 0.8f), 0.5f);
        Object.Destroy(waterPool, 1.2f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.25f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }
}
