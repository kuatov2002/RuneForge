using UnityEngine;

/// <summary>
/// Water + Earth = Permafrost
/// Ground becomes ice. Enemies on it slide and lose movement control.
/// </summary>
public static class PermafrostSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) { radius *= 1.4f; duration *= 1.5f; }

        center.y = 0.05f;

        var iceGO = new GameObject("PermafrostZone");
        iceGO.transform.position = center;

        // Visual: flat ice disc
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.parent = iceGO.transform;
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(radius * 2, 0.03f, radius * 2);
        Color iceCol = new Color(0.6f, 0.85f, 0.95f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewEmissive(iceCol, 2f);

        // Trigger
        var col = iceGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;

        var rb = iceGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        iceGO.AddComponent<PermafrostZone>().Init(damage, duration);

        Object.Destroy(iceGO, duration);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.1f);
    }
}

public class PermafrostZone : MonoBehaviour
{
    float _damage;
    float _tickTimer;

    public void Init(float damage, float duration)
    {
        _damage = damage;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null) return;
        var hp = other.GetComponent<Health>();
        if (hp == null || hp.IsDead) return;

        // Apply ice slide — push enemy in their current movement direction
        var erb = other.GetComponent<Rigidbody>();
        if (erb != null && !erb.isKinematic)
        {
            Vector3 vel = erb.linearVelocity;
            vel.y = 0;
            if (vel.sqrMagnitude > 0.5f)
            {
                // Amplify current velocity direction (sliding effect)
                erb.AddForce(vel.normalized * 8f, ForceMode.Acceleration);
            }
        }

        // Small DoT tick
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 1f;
        hp.TakeDamage(_damage);
    }
}
