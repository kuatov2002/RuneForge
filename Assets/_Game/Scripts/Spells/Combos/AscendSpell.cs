using UnityEngine;

/// <summary>
/// Air + Air = Ascend
/// Fast dash toward cursor. Player is invulnerable during the dash.
/// Uses the PlayerController's own movement system so velocity isn't overridden.
/// </summary>
public static class AscendSpell
{
    public static void Cast(Vector3 origin, Vector3 direction, bool charged)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        float dashDist = charged ? 6f : 4f;
        float dashDuration = 0.18f;

        // Use AscendEffect which takes over movement directly
        var ascend = player.gameObject.GetComponent<AscendEffect>();
        if (ascend == null) ascend = player.gameObject.AddComponent<AscendEffect>();
        ascend.StartAscend(direction, dashDist, dashDuration);

        // ── VFX ──

        // Wind swirl trail
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

        var sizeOL = trailPS.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var trailMat = new Material(Shader.Find("Particles/Standard Unlit"));
        trailMat.SetColor("_Color", new Color(0.8f, 0.9f, 1f, 0.6f));
        trailGO.GetComponent<ParticleSystemRenderer>().material = trailMat;
        trailPS.Play();
        Object.Destroy(trailGO, 0.8f);

        // Flash at origin
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

/// <summary>
/// Handles the Ascend dash by directly moving the transform.
/// This bypasses Rigidbody velocity which gets overridden by PlayerController.
/// </summary>
public class AscendEffect : MonoBehaviour
{
    float _timer;
    float _duration;
    float _ghostTimer;
    Vector3 _direction;
    float _speed;
    bool _active;

    public void StartAscend(Vector3 direction, float distance, float duration)
    {
        _direction = direction.normalized;
        _duration = duration;
        _speed = distance / duration;
        _timer = 0;
        _active = true;

        // Make invulnerable
        var pc = GetComponent<PlayerController>();
        if (pc != null) pc.isInvulnerable = true;
    }

    void Update()
    {
        if (!_active) return;

        _timer += Time.deltaTime;

        // Move directly via transform (bypasses rigidbody)
        transform.position += _direction * _speed * Time.deltaTime;

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
            _active = false;
            var pc = GetComponent<PlayerController>();
            if (pc != null) pc.isInvulnerable = false;

            // Stop rigidbody velocity that might have built up
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
        }
    }
}
