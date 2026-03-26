using UnityEngine;

/// <summary>
/// Fire + Water = Steam
/// Cloud of steam dealing small DoT. If hit by a Fire combo, the cloud explodes.
/// </summary>
public static class SteamSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.3f; duration *= 1.5f; }

        var cloudGO = new GameObject("SteamCloud");
        cloudGO.transform.position = center;

        // Trigger collider for the cloud zone
        var col = cloudGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;

        var rb = cloudGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var cloud = cloudGO.AddComponent<SteamCloudZone>();
        cloud.Init(damage, radius, duration, charged);

        // Visual: steam cloud spheres
        Color steamCol = new Color(0.85f, 0.85f, 0.9f, 0.5f);
        for (int i = 0; i < 8; i++)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(sphere.GetComponent<SphereCollider>());
            sphere.transform.parent = cloudGO.transform;
            sphere.transform.localPosition = Random.insideUnitSphere * radius * 0.6f;
            sphere.transform.localPosition = new Vector3(
                sphere.transform.localPosition.x,
                Mathf.Abs(sphere.transform.localPosition.y) + 0.3f,
                sphere.transform.localPosition.z);
            float s = Random.Range(0.5f, 1.2f);
            sphere.transform.localScale = Vector3.one * s;
            sphere.GetComponent<Renderer>().material = ShaderCache.NewEmissive(steamCol, 1.5f);
        }

        Object.Destroy(cloudGO, duration);

        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

public class SteamCloudZone : MonoBehaviour
{
    float _dps;
    float _radius;
    float _duration;
    bool _charged;
    float _tickTimer;
    float _explosionDamage;

    public void Init(float dps, float radius, float duration, bool charged)
    {
        _dps = dps;
        _radius = radius;
        _duration = duration;
        _charged = charged;
        _explosionDamage = dps * 5f; // Explosion damage if ignited
    }

    void OnTriggerStay(Collider other)
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;

        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            hp.TakeDamage(_dps);
    }

    /// <summary>Called when a Fire spell hits the steam cloud.</summary>
    public void Ignite()
    {
        Vector3 pos = transform.position;
        float explodeRadius = _radius * 1.5f;

        Collider[] hits = Physics.OverlapSphere(pos, explodeRadius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(_explosionDamage);
        }

        // Explosion VFX
        Color fireCol = new Color(1f, 0.5f, 0.1f);
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 0.5f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(fireCol, 6f);
        vfx.AddComponent<ComboExpandVFX>().Init(explodeRadius * 2, 0.3f, fireCol);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.3f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);

        Destroy(gameObject);
    }
}
