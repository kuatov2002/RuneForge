using UnityEngine;

/// <summary>
/// Water + Air = Geyser
/// Water pillar launches all enemies in radius upward. Deals damage and interrupts.
/// </summary>
public static class GeyserSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        if (charged) { radius *= 1.4f; damage *= 1.5f; }

        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            hp.TakeDamage(damage);

            // Launch enemy upward
            var erb = h.GetComponent<Rigidbody>();
            if (erb != null && !erb.isKinematic)
            {
                erb.AddForce(Vector3.up * 12f + Random.insideUnitSphere * 2f, ForceMode.Impulse);
            }
            else
            {
                // For kinematic enemies, apply stun instead
                hp.ApplyStun(1.5f);
            }

            // Interrupt: apply stun
            hp.ApplyStun(1f);
        }

        // VFX: water column
        Color waterCol = new Color(0.2f, 0.5f, 1f);
        var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(column.GetComponent<CapsuleCollider>());
        column.transform.position = center;
        column.transform.localScale = new Vector3(radius, 0.1f, radius);
        column.GetComponent<Renderer>().material = ShaderCache.NewEmissive(waterCol, 4f);
        column.AddComponent<GeyserRise>().Init(6f, 0.4f);

        // Water splash particles
        for (int i = 0; i < 10; i++)
        {
            var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(drop.GetComponent<SphereCollider>());
            drop.transform.position = center + Random.insideUnitSphere * radius * 0.5f;
            drop.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);
            drop.GetComponent<Renderer>().material = ShaderCache.NewEmissive(waterCol, 3f);
            var rb = drop.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.AddForce(Vector3.up * Random.Range(5f, 10f) + Random.insideUnitSphere * 2f, ForceMode.Impulse);
            Object.Destroy(drop, 1f);
        }

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.25f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }
}

public class GeyserRise : MonoBehaviour
{
    float _targetHeight;
    float _duration;
    float _timer;
    float _startWidth;

    public void Init(float height, float duration)
    {
        _targetHeight = height;
        _duration = duration;
        _startWidth = transform.localScale.x;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / _duration;

        if (t < 0.5f)
        {
            // Rise
            float h = Mathf.Lerp(0.1f, _targetHeight, t * 2f);
            transform.localScale = new Vector3(_startWidth, h, _startWidth);
            Vector3 pos = transform.position;
            pos.y = h;
            transform.position = pos;
        }
        else
        {
            // Fade
            float fade = (t - 0.5f) * 2f;
            float w = Mathf.Lerp(_startWidth, 0.01f, fade);
            transform.localScale = new Vector3(w, transform.localScale.y, w);
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
