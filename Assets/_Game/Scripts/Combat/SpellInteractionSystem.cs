using UnityEngine;

/// <summary>
/// Handles interactions between spell zones on the field.
/// When a projectile (Fireball, IceSpike, etc.) enters a spell zone,
/// a reaction occurs.
///
/// Reactions:
/// - Fireball/Wildfire hitting Steam cloud → IGNITE (already in SteamCloudZone)
/// - Fireball hitting Permafrost zone → EVAPORATE (destroys ice, creates steam cloud, damages enemies on ice)
/// - IceSpike hitting Magma pool → OBSIDIAN (creates obsidian shards dealing AoE damage + stun)
/// - Geyser on Magma pool → SOLIDIFY (creates stone wall segment)
/// - Fireball hitting Water (Geyser pool) → STEAM BURST (small steam cloud)
///
/// This is checked by projectile OnTriggerEnter callbacks.
/// Zone components expose methods for interaction.
/// </summary>
public static class SpellInteractionSystem
{
    /// <summary>Check if a fire-element projectile should interact with a zone.</summary>
    public static bool TryFireInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Fire + Steam → Ignite (handled in SteamCloudZone.Ignite)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null) { steam.Ignite(); return true; }

        // Fire + Permafrost → Evaporate
        var frost = zone.GetComponent<PermafrostZone>();
        if (frost != null)
        {
            Evaporate(frost, hitPos, damage);
            return true;
        }

        return false;
    }

    /// <summary>Check if an ice-element projectile should interact with a zone.</summary>
    public static bool TryIceInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Ice + Magma → Obsidian
        var magma = zone.GetComponent<MagmaPoolZone>();
        if (magma != null)
        {
            Obsidian(magma, hitPos, damage);
            return true;
        }

        return false;
    }

    /// <summary>Fire hits Permafrost: ice evaporates into steam, damages enemies on ice.</summary>
    static void Evaporate(PermafrostZone frost, Vector3 pos, float damage)
    {
        // Damage all enemies in the frost zone
        Collider[] hits = Physics.OverlapSphere(pos, 3f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 0.5f);
        }

        // Spawn steam cloud where ice was
        SteamSpell.Cast(pos, 2f, 2f, 3f, false);

        // VFX: evaporation burst
        var burstGO = new GameObject("EvaporateBurst");
        burstGO.transform.position = pos + Vector3.up * 0.3f;
        var ps = burstGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new Color(0.9f, 0.9f, 0.95f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f;
        main.duration = 0.15f; main.loop = false;
        var em = ps.emission; em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(0.9f, 0.9f, 1f, 0.5f));
        burstGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(burstGO, 0.6f);

        // Destroy the frost zone
        Object.Destroy(frost.gameObject);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }

    /// <summary>Ice hits Magma: creates obsidian shards with AoE stun damage.</summary>
    static void Obsidian(MagmaPoolZone magma, Vector3 pos, float damage)
    {
        // Spawn obsidian shards
        int shardCount = 5;
        for (int i = 0; i < shardCount; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(shard.GetComponent<BoxCollider>());
            Vector3 offset = Random.insideUnitSphere * 1.5f;
            offset.y = Mathf.Abs(offset.y) * 0.5f + 0.1f;
            shard.transform.position = pos + offset;
            float s = Random.Range(0.1f, 0.25f);
            shard.transform.localScale = new Vector3(s * 0.5f, s, s * 0.5f);
            shard.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0, 360f), Random.Range(-20f, 20f));
            shard.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.15f, 0.1f, 0.15f));
            Object.Destroy(shard, 3f);
        }

        // AoE damage + stun
        Collider[] hits = Physics.OverlapSphere(pos, 2.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage);
                hp.ApplyStun(1.5f);
            }
        }

        // Destroy the magma pool
        Object.Destroy(magma.gameObject);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.25f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }
}
