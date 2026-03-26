using UnityEngine;

/// <summary>
/// Fire + Fire = Inferno
/// Instant massive AoE explosion. Highest burst damage in the game. No DoT.
/// </summary>
public static class InfernoSpell
{
    public static void Cast(Vector3 center, float damage, float radius, bool charged)
    {
        // AoE damage
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                GameFeel.ApplyKnockback(h.transform, center, damage * 0.5f);
            }
        }

        // VFX: expanding fire sphere
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = center + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 0.5f;
        Color fireCol = new Color(1f, 0.3f, 0f);
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(fireCol, 6f);
        vfx.AddComponent<ComboExpandVFX>().Init(radius * 2, 0.4f, fireCol);

        // Inner white-hot core
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(core.GetComponent<SphereCollider>());
        core.transform.position = center + Vector3.up * 0.5f;
        core.transform.localScale = Vector3.one * 0.3f;
        core.GetComponent<Renderer>().material = ShaderCache.NewEmissive(Color.white, 8f);
        core.AddComponent<ComboExpandVFX>().Init(radius, 0.3f, Color.yellow);

        // Ground scorch ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = center + Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0f), 3f);
        ring.AddComponent<DeathRingEffect>().Init(radius * 2, 0.5f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(charged ? 0.5f : 0.35f);
        if (GameFeel.Instance != null)
            GameFeel.Instance.Hitstop(charged ? 0.08f : 0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }
}
