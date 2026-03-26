using UnityEngine;

/// <summary>
/// Water + Water = Deep Freeze
/// Freezes all enemies in radius for 3 seconds. They become statues.
/// </summary>
public static class DeepFreezeSpell
{
    public static void Cast(Vector3 center, float damage, float radius, float duration, bool charged)
    {
        if (charged) duration *= 1.5f;

        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                hp.ApplyFreeze(duration);

                // Ice crystal VFX on each frozen enemy
                var ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(ice.GetComponent<BoxCollider>());
                ice.transform.position = h.transform.position + Vector3.up * 0.5f;
                ice.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
                Color iceCol = new Color(0.5f, 0.8f, 1f, 0.6f);
                ice.GetComponent<Renderer>().material = ShaderCache.NewEmissive(iceCol, 3f);
                Object.Destroy(ice, duration);
            }
        }

        // VFX: expanding ice ring on ground
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = center + Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
        Color blueCol = new Color(0.3f, 0.7f, 1f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(blueCol, 4f);
        ring.AddComponent<DeathRingEffect>().Init(radius * 2, 0.4f);

        // Ice particles
        for (int i = 0; i < 12; i++)
        {
            var frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(frag.GetComponent<BoxCollider>());
            frag.transform.position = center + Random.insideUnitSphere * radius * 0.5f + Vector3.up * 0.5f;
            float s = Random.Range(0.04f, 0.1f);
            frag.transform.localScale = new Vector3(s, s, s);
            frag.GetComponent<Renderer>().material = ShaderCache.NewEmissive(blueCol, 3f);
            var rb = frag.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.AddForce(Random.insideUnitSphere * 2f + Vector3.up * 3f, ForceMode.Impulse);
            Object.Destroy(frag, 1f);
        }

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, center);
    }
}
