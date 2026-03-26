using UnityEngine;

/// <summary>
/// Handles interactions between spell zones on the field.
/// When a projectile enters a spell zone, a reaction occurs.
///
/// Reactions:
/// - Fire + Steam → IGNITE (steam explodes)
/// - Fire + Permafrost → EVAPORATE (ice→steam, damage enemies)
/// - Fire + Poison cloud → TOXIC EXPLOSION (3x cloud DPS burst)
/// - Ice + Magma → OBSIDIAN (shards + AoE stun)
/// - Lightning + Water zone → ELECTRIFY (double DPS, chain damage)
/// - Lightning + Magma → SUPERHEAT (magma explodes)
/// - Void + any zone → IMPLODE (instant zone damage, smaller radius)
/// </summary>
public static class SpellInteractionSystem
{
    /// <summary>Check if a fire-element projectile should interact with a zone.</summary>
    public static bool TryFireInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Fire + Steam → Ignite
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null) { steam.Ignite(); return true; }

        // Fire + Permafrost → Evaporate
        var frost = zone.GetComponent<PermafrostZone>();
        if (frost != null)
        {
            Evaporate(frost, hitPos, damage);
            return true;
        }

        // Fire + Poison cloud → Toxic Explosion
        var plague = zone.GetComponent<PlagueCloudZone>();
        if (plague != null)
        {
            ToxicExplosion(plague, hitPos);
            return true;
        }

        // Fire + Miasma → Toxic Explosion
        var miasma = zone.GetComponent<MiasmaCloudBehavior>();
        if (miasma != null)
        {
            ToxicExplosionGeneric(zone.gameObject, hitPos, 3f, 8f);
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

        // Ice + Quicksand → Frozen Quicksand (enemies take 2x damage while stuck+frozen)
        var quicksand = zone.GetComponent<QuicksandZone>();
        if (quicksand != null)
        {
            FrozenQuicksand(quicksand, hitPos);
            return true;
        }

        return false;
    }

    /// <summary>Check if a lightning-element projectile should interact with a zone.</summary>
    public static bool TryLightningInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Lightning + Magma → Superheat explosion
        var magma = zone.GetComponent<MagmaPoolZone>();
        if (magma != null)
        {
            SuperheatMagma(magma, hitPos);
            return true;
        }

        // Lightning + Water/Geyser/Permafrost → Electrify
        var permafrost = zone.GetComponent<PermafrostZone>();
        if (permafrost != null)
        {
            Electrify(zone.gameObject, hitPos, 3f, damage);
            return true;
        }

        // Lightning + Steam → chain lightning through steam
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            Electrify(zone.gameObject, hitPos, 2.5f, damage);
            return true;
        }

        return false;
    }

    /// <summary>Check if a void-element projectile should interact with a zone.</summary>
    public static bool TryVoidInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Void + any damageable zone → Implode (instant concentrated damage)
        // Check for any known zone type
        if (zone.GetComponent<MagmaPoolZone>() != null ||
            zone.GetComponent<SteamCloudZone>() != null ||
            zone.GetComponent<PermafrostZone>() != null ||
            zone.GetComponent<PlagueCloudZone>() != null ||
            zone.GetComponent<MiasmaCloudBehavior>() != null)
        {
            ImplodeZone(zone.gameObject, hitPos, damage);
            return true;
        }

        return false;
    }

    // ── REACTIONS ──

    /// <summary>Fire hits Permafrost: ice evaporates into steam, damages enemies.</summary>
    static void Evaporate(PermafrostZone frost, Vector3 pos, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, 3f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 0.5f);
        }

        SteamSpell.Cast(pos, 2f, 2f, 3f, false);

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

        Object.Destroy(frost.gameObject);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }

    /// <summary>Ice hits Magma: creates obsidian shards with AoE stun damage.</summary>
    static void Obsidian(MagmaPoolZone magma, Vector3 pos, float damage)
    {
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

        Object.Destroy(magma.gameObject);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.25f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }

    /// <summary>Fire hits Poison cloud: toxic explosion (3x DPS burst).</summary>
    static void ToxicExplosion(PlagueCloudZone plague, Vector3 pos)
    {
        ToxicExplosionGeneric(plague.gameObject, pos, plague.Radius, plague.Dps * 3f);
    }

    static void ToxicExplosionGeneric(GameObject zone, Vector3 pos, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage);
        }

        // Green-orange explosion VFX
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * radius * 2;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.7f, 0.5f, 0.1f, 0.5f), 4f);
        Object.Destroy(vfx, 0.25f);

        Object.Destroy(zone);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.35f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }

    /// <summary>Lightning hits water zone: electrify — chain damage to all enemies in zone.</summary>
    static void Electrify(GameObject zone, Vector3 pos, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 0.8f);
                hp.ApplyStun(0.5f);
            }
        }

        // Electric sparks VFX
        var sparkGO = new GameObject("ElectrifyBurst");
        sparkGO.transform.position = pos + Vector3.up * 0.3f;
        var ps = sparkGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = 0.2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new Color(1f, 1f, 0.3f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = 0.1f; main.loop = false;
        var em = ps.emission; em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius * 0.5f;
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(1f, 1f, 0.3f));
        sparkGO.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        Object.Destroy(sparkGO, 0.5f);

        // Yellow flash on zone
        var flash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(flash.GetComponent<CapsuleCollider>());
        flash.transform.position = pos + Vector3.up * 0.05f;
        flash.transform.localScale = new Vector3(radius * 2, 0.04f, radius * 2);
        flash.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 1f, 0.2f, 0.6f), 5f);
        Object.Destroy(flash, 0.15f);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.25f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }

    /// <summary>Lightning hits Magma: superheat → magma explodes for burst damage.</summary>
    static void SuperheatMagma(MagmaPoolZone magma, Vector3 pos)
    {
        float radius = 3f;
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(12);
                GameFeel.ApplyKnockback(h.transform, pos, 5f);
            }
        }

        // Lava explosion VFX
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * radius * 1.5f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.4f, 0f, 0.6f), 6f);
        Object.Destroy(vfx, 0.2f);

        Object.Destroy(magma.gameObject);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.4f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }

    /// <summary>Ice hits Quicksand: enemies take 2x damage while stuck + frozen.</summary>
    static void FrozenQuicksand(QuicksandZone quicksand, Vector3 pos)
    {
        float radius = 2.5f;
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.ApplyFreeze(3f);
                hp.TakeDamage(5);
            }
        }

        // Frozen quicksand visual: change color to blue-brown
        var renderers = quicksand.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            if (r != null) r.material.color = new Color(0.3f, 0.4f, 0.6f);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Freeze, pos);
    }

    /// <summary>Void hits any zone: implode — instant concentrated damage in smaller radius.</summary>
    static void ImplodeZone(GameObject zone, Vector3 pos, float damage)
    {
        float radius = 2f;

        // Pull enemies toward center briefly
        Collider[] nearby = Physics.OverlapSphere(pos, radius * 2f);
        foreach (var n in nearby)
        {
            if (n.GetComponent<PlayerController>() != null) continue;
            var hp = n.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            Vector3 pull = (pos - n.transform.position).normalized;
            var erb = n.GetComponent<Rigidbody>();
            if (erb != null && !erb.isKinematic)
                erb.AddForce(pull * 10f, ForceMode.Impulse);
        }

        // Burst damage
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 1.5f);
        }

        // Implosion VFX: shrinking dark sphere
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * radius * 2;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 0f, 0.5f, 0.6f), 3f);
        vfx.AddComponent<FlashShrink>().Init(0.3f);

        Object.Destroy(zone);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.3f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
    }
}
