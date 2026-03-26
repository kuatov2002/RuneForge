using UnityEngine;

/// <summary>
/// Air + Air = Ascend
/// Fast dash toward cursor. Player is invulnerable during the dash.
/// </summary>
public static class AscendSpell
{
    public static void Cast(Vector3 origin, Vector3 direction, bool charged)
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        float dashDist = charged ? 8f : 5f;
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

        // Wind trail VFX
        Color airCol = new Color(0.8f, 0.9f, 1f);
        for (int i = 0; i < 5; i++)
        {
            var trail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(trail.GetComponent<SphereCollider>());
            float t = i / 5f;
            trail.transform.position = origin + direction * dashDist * t + Vector3.up * 0.5f +
                                       Random.insideUnitSphere * 0.3f;
            trail.transform.localScale = Vector3.one * 0.2f;
            trail.GetComponent<Renderer>().material = ShaderCache.NewEmissive(airCol, 3f);
            trail.AddComponent<FlashShrink>().Init(0.4f);
        }

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
