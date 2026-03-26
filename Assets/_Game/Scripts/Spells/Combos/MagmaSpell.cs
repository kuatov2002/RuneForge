using UnityEngine;

/// <summary>
/// Fire + Earth = Magma
/// Lava pool that heavily slows enemies and deals DoT.
/// Slow disappears as soon as enemies leave.
/// </summary>
public static class MagmaSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.4f; duration *= 1.5f; damage *= 1.3f; }

        center.y = 0.05f;

        var poolGO = new GameObject("MagmaPool");
        poolGO.transform.position = center;

        // Visual: flat cylinder
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(visual.GetComponent<CapsuleCollider>());
        visual.transform.parent = poolGO.transform;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(radius * 2, 0.04f, radius * 2);
        Color magmaCol = new Color(1f, 0.4f, 0f);
        visual.GetComponent<Renderer>().material = ShaderCache.NewEmissive(magmaCol, 3f);

        // Trigger
        var col = poolGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;

        var rb = poolGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        poolGO.AddComponent<MagmaPoolZone>().Init(damage, radius, duration);

        Object.Destroy(poolGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}

public class MagmaPoolZone : MonoBehaviour
{
    float _dps;
    float _radius;
    float _duration;
    float _tickTimer;

    public void Init(float dps, float radius, float duration)
    {
        _dps = dps;
        _radius = radius;
        _duration = duration;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Apply slow while in pool
        hp.ApplyMagmaSlow();

        // DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        hp.TakeDamage(_dps);
    }
}
