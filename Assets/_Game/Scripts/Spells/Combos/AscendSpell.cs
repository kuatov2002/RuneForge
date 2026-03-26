using UnityEngine;

/// <summary>
/// Air + Air = Ascend
/// Fast dash toward cursor. Player is invulnerable during the dash.
/// Balance: dash distance 4 (was 5), charged 6 (was 8)
/// </summary>
public static class AscendSpell
{
    public static void Cast(Vector3 origin, Vector3 direction, bool charged)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        float dashDist = charged ? 6f : 4f;
        float dashDuration = 0.2f;

        // Make player invulnerable and dash
        player.isInvulnerable = true;
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction * (dashDist / dashDuration);

        // Schedule end of invulnerability
        player.CancelInvoke(nameof(PlayerController) + "_EndAscend");
        var ascend = player.gameObject.GetComponent<AscendEffect>();
        if (ascend == null) ascend = player.gameObject.AddComponent<AscendEffect>();
        ascend.StartAscend(dashDuration, dashDist);

        // ── VFX ──

        // 1) Wind swirl trail along the dash path
        var trailGO = new GameObject("AscendWindTrail");
        trailGO.transform.position = origin + Vector3.up * 0.5f;
        var trailPS = trailGO.AddComponent<ParticleSystem>();
        trailPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = trailPS.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.9f, 1f, 0.7f),
            new Color(0.6f, 0.8f, 1f, 0.5f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.1f;
        main.duration = 0.3f;
        main.loop = false;
        main.maxParticles = 40;

        var emission = trailPS.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

        var shape = trailPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.5f, 0.5f, dashDist);
        shape.position = new Vector3(0, 0, dashDist * 0.5f);
        trailGO.transform.rotation = Quaternion.LookRotation(direction);

        // Velocity over lifetime for swirl effect
        var vel = trailPS.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, -2f, 1f, 2f));
        vel.y = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 2f, 1f, -2f));
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        var sizeOL = trailPS.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var colorOL = trailPS.colorOverLifetime;
        colorOL.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.85f, 0.93f, 1f), 0f),
                new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOL.color = grad;

        var trailMat = new Material(Shader.Find("Particles/Standard Unlit"));
        trailMat.SetColor("_Color", new Color(0.8f, 0.9f, 1f, 0.6f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = trailMat;
        trailPS.Play();
        Object.Destroy(trailGO, 0.8f);

        // 2) Speed line particles: thin fast particles in dash direction
        var speedGO = new GameObject("AscendSpeedLines");
        speedGO.transform.position = origin + Vector3.up * 0.5f;
        speedGO.transform.rotation = Quaternion.LookRotation(direction);
        var speedPS = speedGO.AddComponent<ParticleSystem>();
        speedPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var speedMain = speedPS.main;
        speedMain.startLifetime = 0.15f;
        speedMain.startSpeed = new ParticleSystem.MinMaxCurve(15f, 25f);
        speedMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        speedMain.startColor = new Color(1f, 1f, 1f, 0.5f);
        speedMain.simulationSpace = ParticleSystemSimulationSpace.World;
        speedMain.duration = 0.1f;
        speedMain.loop = false;

        var speedEmission = speedPS.emission;
        speedEmission.rateOverTime = 0;
        speedEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var speedShape = speedPS.shape;
        speedShape.shapeType = ParticleSystemShapeType.Cone;
        speedShape.angle = 5f;
        speedShape.radius = 0.3f;

        // Stretch particles for speed line look
        var speedRenderer = speedGO.GetComponent<ParticleSystemRenderer>();
        speedRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        speedRenderer.lengthScale = 4f;
        speedRenderer.velocityScale = 0.1f;
        speedRenderer.material = trailMat;

        speedPS.Play();
        Object.Destroy(speedGO, 0.5f);

        // 3) Flash at origin
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flash.GetComponent<SphereCollider>());
        flash.transform.position = origin + Vector3.up * 0.5f;
        flash.transform.localScale = Vector3.one * 0.5f;
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.9f, 0.95f, 1f), 6f);
        flash.AddComponent<FlashShrink>().Init(0.2f);

        SFXSystem.Play(SFXSystem.SFXType.Dash, origin);
        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.1f);
    }
}

public class AscendEffect : MonoBehaviour
{
    float _timer;
    float _duration;
    float _ghostTimer;

    public void StartAscend(float duration, float distance)
    {
        _timer = 0;
        _duration = duration;
        enabled = true;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        // Afterimage ghosts
        _ghostTimer -= Time.deltaTime;
        if (_ghostTimer <= 0)
        {
            _ghostTimer = 0.03f;
            var renderers = GetComponentsInChildren<Renderer>();
            GameFeel.SpawnDashGhost(renderers, transform.position, transform.rotation);
        }

        if (_timer >= _duration)
        {
            var pc = GetComponent<PlayerController>();
            if (pc != null) pc.isInvulnerable = false;
            enabled = false;
        }
    }
}
