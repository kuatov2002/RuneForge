using UnityEngine;

/// <summary>
/// Generic fallback implementation for advanced element combos
/// (Lightning, Poison, Void cross-combos).
/// Provides a baseline AoE damage + VFX that can be refined later.
/// </summary>
public static class GenericComboSpell
{
    public static void Cast(ComboSpellDef def, Vector3 center, float damage, float radius, bool charged)
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

                // Duration-based: apply stun
                if (def.duration > 0)
                    hp.ApplyStun(Mathf.Min(def.duration, 2f));
            }
        }

        // VFX: expanding sphere in combo color
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = center + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 0.4f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(def.color, 5f);
        vfx.AddComponent<ComboExpandVFX>().Init(radius * 2, 0.4f, def.color);

        // Ground ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = center + Vector3.up * 0.05f;
        ring.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(def.color, 3f);
        ring.AddComponent<DeathRingEffect>().Init(radius * 2, 0.4f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(charged ? 0.3f : 0.2f);
        if (GameFeel.Instance != null && damage >= 10)
            GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, center);
    }
}
