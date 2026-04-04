using UnityEngine;

/// <summary>
/// Handles interactions between spell projectiles and airborne spell zones.
/// Only zones that physically occupy the projectile's flight path react.
/// Ground pools (permafrost, magma, quicksand) do NOT react with flying projectiles.
///
/// 20 unique reactions across all 7 elements × 3 airborne zone types.
/// </summary>
public static class SpellInteractionSystem
{
    public static event System.Action<string, Vector3, Color> OnReaction;

    // ── UNIFIED DISPATCH ──────────────────────────────────────

    /// <summary>Try zone interaction based on projectile element. Returns true if a reaction occurred.</summary>
    public static bool TryInteraction(ElementType element, Collider zone, Vector3 hitPos, float damage)
    {
        return element switch
        {
            ElementType.Fire => TryFireInteraction(zone, hitPos, damage),
            ElementType.Water => TryWaterInteraction(zone, hitPos, damage),
            ElementType.Lightning => TryLightningInteraction(zone, hitPos, damage),
            ElementType.Earth => TryEarthInteraction(zone, hitPos, damage),
            ElementType.Air => TryAirInteraction(zone, hitPos, damage),
            ElementType.Poison => TryPoisonInteraction(zone, hitPos, damage),
            ElementType.Void => TryVoidInteraction(zone, hitPos, damage),
            _ => false
        };
    }

    // ── FIRE INTERACTIONS ─────────────────────────────────────

    /// <summary>Fire projectile + airborne zone.</summary>
    public static bool TryFireInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Fire + Steam → IGNITE (steam explodes in fire burst)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            steam.Ignite();
            OnReaction?.Invoke("IGNITE", hitPos, new Color(1f, 0.5f, 0.1f));
            return true;
        }

        // Fire + Plague Cloud → TOXIC EXPLOSION (poison ignites)
        var plague = zone.GetComponent<PlagueCloudZone>();
        if (plague != null)
        {
            ToxicExplosion(plague.gameObject, hitPos, plague.Radius, plague.Dps * 3f);
            return true;
        }

        // Fire + Miasma → TOXIC EXPLOSION
        var miasma = zone.GetComponent<MiasmaCloudBehavior>();
        if (miasma != null)
        {
            ToxicExplosion(zone.gameObject, hitPos, 3f, 8f);
            return true;
        }

        // Fire + Cyclone → FIRENADO (fire tornado burst + burn)
        var cyclone = zone.GetComponent<CycloneZone>();
        if (cyclone != null)
        {
            Firenado(cyclone, hitPos, damage);
            return true;
        }

        return false;
    }

    // ── WATER INTERACTIONS ────────────────────────────────────

    static bool TryWaterInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Water + Steam → DOWNPOUR (steam condenses, freeze burst)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            Downpour(steam, hitPos, damage);
            return true;
        }

        // Water + Plague Cloud → ACID RAIN (corrosive condensation)
        var plague = zone.GetComponent<PlagueCloudZone>();
        if (plague != null)
        {
            AcidRain(plague, hitPos, damage);
            return true;
        }

        // Water + Miasma → ACID RAIN
        var miasma = zone.GetComponent<MiasmaCloudBehavior>();
        if (miasma != null)
        {
            AcidRainGeneric(zone.gameObject, hitPos, 3f, damage);
            return true;
        }

        // Water + Cyclone → HURRICANE (amplified knockback burst)
        var cyclone = zone.GetComponent<CycloneZone>();
        if (cyclone != null)
        {
            Hurricane(cyclone, hitPos, damage);
            return true;
        }

        return false;
    }

    // ── LIGHTNING INTERACTIONS ─────────────────────────────────

    static bool TryLightningInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Lightning + Steam → CHAIN LIGHTNING (conductive steam)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            ChainLightning(steam, hitPos, damage);
            return true;
        }

        // Lightning + Plague Cloud → PLAGUE SPARK (chain detonation through poison)
        var plague = zone.GetComponent<PlagueCloudZone>();
        if (plague != null)
        {
            PlagueSpark(plague, hitPos, damage);
            return true;
        }

        // Lightning + Miasma → PLAGUE SPARK
        var miasma = zone.GetComponent<MiasmaCloudBehavior>();
        if (miasma != null)
        {
            PlagueSparkGeneric(zone.gameObject, hitPos, 3f, damage);
            return true;
        }

        // Lightning + Cyclone → SUPERCELL (rapid lightning strikes from cyclone)
        var cyclone = zone.GetComponent<CycloneZone>();
        if (cyclone != null)
        {
            Supercell(cyclone, hitPos, damage);
            return true;
        }

        return false;
    }

    // ── EARTH INTERACTIONS ────────────────────────────────────

    static bool TryEarthInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Earth + Steam → MUDSLIDE (steam condenses with earth, roots enemies)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            Mudslide(steam, hitPos, damage);
            return true;
        }

        // Earth + Plague Cloud → PETRIFY (poison crystallizes, mass stun)
        var plague = zone.GetComponent<PlagueCloudZone>();
        if (plague != null)
        {
            Petrify(plague, hitPos, damage);
            return true;
        }

        // Earth + Cyclone → DEBRIS STORM (rocks in tornado, shrapnel)
        var cyclone = zone.GetComponent<CycloneZone>();
        if (cyclone != null)
        {
            DebrisStorm(cyclone, hitPos, damage);
            return true;
        }

        return false;
    }

    // ── AIR INTERACTIONS ──────────────────────────────────────

    static bool TryAirInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Air + Steam → STEAM BLAST (gust disperses steam, knockback wave)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            SteamBlast(steam, hitPos, damage);
            return true;
        }

        // Air + Plague Cloud → PANDEMIC (wind scatters poison wide)
        var plague = zone.GetComponent<PlagueCloudZone>();
        if (plague != null)
        {
            Pandemic(plague, hitPos, damage);
            return true;
        }

        // Air + Cyclone → UPDRAFT (launch all enemies skyward)
        var cyclone = zone.GetComponent<CycloneZone>();
        if (cyclone != null)
        {
            Updraft(cyclone, hitPos, damage);
            return true;
        }

        return false;
    }

    // ── POISON INTERACTIONS ───────────────────────────────────

    static bool TryPoisonInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Poison + Steam → TOXIC MIST (steam becomes poisonous)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            ToxicMist(steam, hitPos, damage);
            return true;
        }

        // Poison + Cyclone → PLAGUE VORTEX (cyclone spreads poison)
        var cyclone = zone.GetComponent<CycloneZone>();
        if (cyclone != null)
        {
            PlagueVortex(cyclone, hitPos, damage);
            return true;
        }

        return false;
    }

    // ── VOID INTERACTIONS ─────────────────────────────────────

    static bool TryVoidInteraction(Collider zone, Vector3 hitPos, float damage)
    {
        // Void + Steam → VOID MIST (Exposed status on all inside)
        var steam = zone.GetComponent<SteamCloudZone>();
        if (steam != null)
        {
            VoidMist(steam, hitPos, damage);
            return true;
        }

        // Void + Plague Cloud → BLIGHT (void amplifies plague, massive burst)
        var plague = zone.GetComponent<PlagueCloudZone>();
        if (plague != null)
        {
            Blight(plague, hitPos, damage);
            return true;
        }

        // Void + Miasma → BLIGHT
        var miasma = zone.GetComponent<MiasmaCloudBehavior>();
        if (miasma != null)
        {
            BlightGeneric(zone.gameObject, hitPos, 3f, damage);
            return true;
        }

        // Void + Cyclone → SINGULARITY (cyclone collapses inward)
        var cyclone = zone.GetComponent<CycloneZone>();
        if (cyclone != null)
        {
            Singularity(cyclone, hitPos, damage);
            return true;
        }

        return false;
    }

    // ════════════════════════════════════════════════════════════
    // ── REACTION IMPLEMENTATIONS ──
    // ════════════════════════════════════════════════════════════

    static float GetRadius(Collider zone)
    {
        var sc = zone as SphereCollider;
        if (sc != null) return sc.radius * zone.transform.lossyScale.x;
        return Mathf.Max(zone.bounds.extents.x, zone.bounds.extents.z);
    }

    // ── FIRE REACTIONS ────────────────────────────────────────

    /// <summary>Fire + Plague/Miasma: poison cloud ignites in a burst.</summary>
    static void ToxicExplosion(GameObject zone, Vector3 pos, float radius, float damage)
    {
        DamageArea(pos, radius * 1.5f, damage);
        SpawnFlash(pos, radius * 2f, new Color(0.7f, 0.5f, 0.1f, 0.5f), 4f);
        Object.Destroy(zone);
        Shake(0.35f); Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("TOXIC EXPLOSION", pos, new Color(0.7f, 0.5f, 0.1f));
    }

    /// <summary>Fire + Cyclone: fire tornado erupts, burns everything.</summary>
    static void Firenado(CycloneZone cyclone, Vector3 pos, float damage)
    {
        float radius = GetRadius(cyclone.GetComponent<Collider>());

        // Burn damage + apply Burning status via Fire element
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 2f, ElementType.Fire);
                GameFeel.ApplyKnockback(h.transform, pos, 4f);
            }
        }

        // Fire tornado VFX — spiraling fire particles
        SpawnBurst(pos, 40, new Color(1f, 0.4f, 0f), radius, 6f, 0.5f);
        SpawnBurst(pos, 20, new Color(1f, 0.8f, 0.1f), radius * 0.5f, 4f, 0.3f);
        SpawnFlash(pos, radius * 2.5f, new Color(1f, 0.3f, 0f, 0.4f), 5f);

        Object.Destroy(cyclone.gameObject);
        Shake(0.4f); Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("FIRENADO", pos, new Color(1f, 0.4f, 0f));
    }

    // ── WATER REACTIONS ───────────────────────────────────────

    /// <summary>Water + Steam: steam condenses into freezing downpour.</summary>
    static void Downpour(SteamCloudZone steam, Vector3 pos, float damage)
    {
        float radius = GetRadius(steam.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage, ElementType.Water);
                hp.ApplyFreeze(2f);
            }
        }

        SpawnBurst(pos, 30, new Color(0.4f, 0.7f, 1f), radius, 1f, 0.6f, gravityMod: 1.5f);
        SpawnFlash(pos, radius * 1.5f, new Color(0.3f, 0.5f, 0.9f, 0.4f), 3f);

        Object.Destroy(steam.gameObject);
        Shake(0.25f);
        SFXSystem.Play(SFXSystem.SFXType.Freeze, pos);
        OnReaction?.Invoke("DOWNPOUR", pos, new Color(0.4f, 0.7f, 1f));
    }

    /// <summary>Water + Plague: corrosive acid rain.</summary>
    static void AcidRain(PlagueCloudZone plague, Vector3 pos, float damage)
    {
        AcidRainGeneric(plague.gameObject, pos, plague.Radius, damage);
    }

    static void AcidRainGeneric(GameObject zone, Vector3 pos, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.3f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 1.5f, ElementType.Poison);
                hp.ApplyMagmaSlow();
            }
        }

        SpawnBurst(pos, 25, new Color(0.5f, 0.9f, 0.2f), radius, 1.5f, 0.5f, gravityMod: 1.2f);
        SpawnFlash(pos, radius * 1.5f, new Color(0.4f, 0.8f, 0.1f, 0.5f), 3f);

        Object.Destroy(zone);
        Shake(0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("ACID RAIN", pos, new Color(0.4f, 0.8f, 0.1f));
    }

    /// <summary>Water + Cyclone: cyclone becomes hurricane with massive knockback.</summary>
    static void Hurricane(CycloneZone cyclone, Vector3 pos, float damage)
    {
        float radius = GetRadius(cyclone.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 2f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 1.5f, ElementType.Water);
                GameFeel.ApplyKnockback(h.transform, pos, 10f);
                hp.ApplyStun(1.5f);
            }
        }

        SpawnBurst(pos, 35, new Color(0.3f, 0.6f, 1f), radius * 1.5f, 8f, 0.4f);
        SpawnFlash(pos, radius * 3f, new Color(0.2f, 0.4f, 0.9f, 0.3f), 4f);

        Object.Destroy(cyclone.gameObject);
        Shake(0.4f); Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("HURRICANE", pos, new Color(0.3f, 0.6f, 1f));
    }

    // ── LIGHTNING REACTIONS ───────────────────────────────────

    /// <summary>Lightning + Steam: chain lightning through conductive steam.</summary>
    static void ChainLightning(SteamCloudZone steam, Vector3 pos, float damage)
    {
        float radius = GetRadius(steam.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 1.2f, ElementType.Lightning);
                hp.ApplyStun(1f);
            }
        }

        SpawnBurst(pos, 30, new Color(1f, 1f, 0.3f), radius, 8f, 0.2f);
        SpawnFlash(pos, radius * 2f, new Color(1f, 1f, 0.2f, 0.6f), 5f, duration: 0.15f);

        Object.Destroy(steam.gameObject);
        Shake(0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("CHAIN LIGHTNING", pos, new Color(1f, 1f, 0.3f));
    }

    /// <summary>Lightning + Plague: chain detonation through poison.</summary>
    static void PlagueSpark(PlagueCloudZone plague, Vector3 pos, float damage)
    {
        PlagueSparkGeneric(plague.gameObject, pos, plague.Radius, damage);
    }

    static void PlagueSparkGeneric(GameObject zone, Vector3 pos, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 2f, ElementType.Lightning);
                hp.ApplyStun(0.8f);
            }
        }

        SpawnBurst(pos, 20, new Color(0.8f, 1f, 0.2f), radius, 6f, 0.3f);
        SpawnBurst(pos, 15, new Color(1f, 1f, 0.3f), radius * 0.7f, 10f, 0.15f);
        SpawnFlash(pos, radius * 1.8f, new Color(0.7f, 0.9f, 0.1f, 0.5f), 4f);

        Object.Destroy(zone);
        Shake(0.35f); Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("PLAGUE SPARK", pos, new Color(0.8f, 1f, 0.2f));
    }

    /// <summary>Lightning + Cyclone: supercell — rapid lightning strikes.</summary>
    static void Supercell(CycloneZone cyclone, Vector3 pos, float damage)
    {
        float radius = GetRadius(cyclone.GetComponent<Collider>());
        int strikeCount = 5;
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 0.6f * strikeCount, ElementType.Lightning);
                hp.ApplyStun(2f);
            }
        }

        // Multiple lightning bolt VFX
        for (int i = 0; i < strikeCount; i++)
        {
            Vector3 offset = Random.insideUnitSphere * radius;
            offset.y = 0;
            Vector3 strikePos = pos + offset;
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(bolt.GetComponent<CapsuleCollider>());
            bolt.transform.position = strikePos + Vector3.up * 3f;
            bolt.transform.localScale = new Vector3(0.08f, 3f, 0.08f);
            bolt.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 1f, 0.4f), 8f);
            Object.Destroy(bolt, 0.2f);
        }

        SpawnFlash(pos, radius * 2f, new Color(1f, 1f, 0.5f, 0.4f), 6f, duration: 0.15f);

        Object.Destroy(cyclone.gameObject);
        Shake(0.5f); Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("SUPERCELL", pos, new Color(1f, 1f, 0.4f));
    }

    // ── EARTH REACTIONS ──────────────────────────────────────

    /// <summary>Earth + Steam: mud rain — roots enemies in place.</summary>
    static void Mudslide(SteamCloudZone steam, Vector3 pos, float damage)
    {
        float radius = GetRadius(steam.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 0.8f, ElementType.Earth);
                hp.ApplyStun(2.5f);
                hp.ApplyMagmaSlow();
            }
        }

        SpawnBurst(pos, 20, new Color(0.5f, 0.35f, 0.15f), radius, 2f, 0.5f, gravityMod: 2f);
        SpawnFlash(pos, radius * 1.5f, new Color(0.45f, 0.3f, 0.1f, 0.5f), 2f);

        Object.Destroy(steam.gameObject);
        Shake(0.25f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("MUDSLIDE", pos, new Color(0.5f, 0.35f, 0.15f));
    }

    /// <summary>Earth + Plague: poison crystallizes, mass petrification.</summary>
    static void Petrify(PlagueCloudZone plague, Vector3 pos, float damage)
    {
        float radius = plague.Radius;
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.3f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 1.2f, ElementType.Earth);
                hp.ApplyStun(3f);
                hp.ApplyFreeze(2f);
            }
        }

        // Stone shards VFX
        for (int i = 0; i < 8; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(shard.GetComponent<BoxCollider>());
            Vector3 offset = Random.insideUnitSphere * radius;
            offset.y = Mathf.Abs(offset.y) * 0.3f + 0.1f;
            shard.transform.position = pos + offset;
            float s = Random.Range(0.08f, 0.2f);
            shard.transform.localScale = new Vector3(s, s * 1.5f, s);
            shard.transform.rotation = Random.rotation;
            shard.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.4f, 0.35f, 0.5f));
            Object.Destroy(shard, 2f);
        }

        SpawnFlash(pos, radius * 1.5f, new Color(0.5f, 0.4f, 0.55f, 0.5f), 2f);

        Object.Destroy(plague.gameObject);
        Shake(0.35f); Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("PETRIFY", pos, new Color(0.5f, 0.4f, 0.55f));
    }

    /// <summary>Earth + Cyclone: debris storm — rock shrapnel burst.</summary>
    static void DebrisStorm(CycloneZone cyclone, Vector3 pos, float damage)
    {
        float radius = GetRadius(cyclone.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 2f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage * 2.5f, ElementType.Earth);
                GameFeel.ApplyKnockback(h.transform, pos, 6f);
            }
        }

        // Flying rock debris
        for (int i = 0; i < 12; i++)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(rock.GetComponent<BoxCollider>());
            rock.transform.position = pos + Vector3.up * 0.5f;
            float s = Random.Range(0.1f, 0.3f);
            rock.transform.localScale = new Vector3(s, s, s);
            rock.transform.rotation = Random.rotation;
            rock.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.5f, 0.4f, 0.3f));
            var rb = rock.AddComponent<Rigidbody>();
            rb.AddForce(Random.insideUnitSphere * 12f + Vector3.up * 5f, ForceMode.Impulse);
            Object.Destroy(rock, 2f);
        }

        Object.Destroy(cyclone.gameObject);
        Shake(0.45f); Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("DEBRIS STORM", pos, new Color(0.5f, 0.4f, 0.3f));
    }

    // ── AIR REACTIONS ────────────────────────────────────────

    /// <summary>Air + Steam: explosive steam blast — knockback wave.</summary>
    static void SteamBlast(SteamCloudZone steam, Vector3 pos, float damage)
    {
        float radius = GetRadius(steam.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 2f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage, ElementType.Air);
                GameFeel.ApplyKnockback(h.transform, pos, 12f);
                hp.ApplyStun(0.5f);
            }
        }

        SpawnBurst(pos, 40, new Color(0.85f, 0.9f, 1f, 0.6f), radius * 1.5f, 10f, 0.3f);
        SpawnFlash(pos, radius * 3f, new Color(0.8f, 0.85f, 1f, 0.3f), 3f, duration: 0.2f);

        Object.Destroy(steam.gameObject);
        Shake(0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("STEAM BLAST", pos, new Color(0.85f, 0.9f, 1f));
    }

    /// <summary>Air + Plague: wind scatters poison over massive area.</summary>
    static void Pandemic(PlagueCloudZone plague, Vector3 pos, float damage)
    {
        float radius = plague.Radius;
        float spreadRadius = radius * 2.5f;

        Collider[] hits = Physics.OverlapSphere(pos, spreadRadius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 0.8f, ElementType.Poison);
        }

        SpawnBurst(pos, 50, new Color(0.3f, 0.8f, 0.2f, 0.6f), spreadRadius, 6f, 0.6f);
        SpawnFlash(pos, spreadRadius * 1.5f, new Color(0.2f, 0.7f, 0.1f, 0.2f), 2f);

        Object.Destroy(plague.gameObject);
        Shake(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("PANDEMIC", pos, new Color(0.3f, 0.8f, 0.2f));
    }

    /// <summary>Air + Cyclone: updraft — launches all enemies skyward.</summary>
    static void Updraft(CycloneZone cyclone, Vector3 pos, float damage)
    {
        float radius = GetRadius(cyclone.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage, ElementType.Air);
                hp.ApplyStun(2.5f);
                var rb = h.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                    rb.AddForce(Vector3.up * 18f + Random.insideUnitSphere * 3f, ForceMode.Impulse);
            }
        }

        SpawnBurst(pos, 30, new Color(0.7f, 0.9f, 1f), radius, 12f, 0.4f, gravityMod: -1f);
        SpawnFlash(pos, radius * 2f, new Color(0.6f, 0.85f, 1f, 0.3f), 3f);

        Object.Destroy(cyclone.gameObject);
        Shake(0.3f); Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("UPDRAFT", pos, new Color(0.7f, 0.9f, 1f));
    }

    // ── POISON REACTIONS ─────────────────────────────────────

    /// <summary>Poison + Steam: steam becomes toxic, poisons everyone.</summary>
    static void ToxicMist(SteamCloudZone steam, Vector3 pos, float damage)
    {
        float radius = GetRadius(steam.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.3f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 1.5f, ElementType.Poison);
        }

        SpawnBurst(pos, 25, new Color(0.3f, 0.9f, 0.1f), radius, 3f, 0.5f, gravityMod: -0.3f);
        SpawnFlash(pos, radius * 1.8f, new Color(0.2f, 0.8f, 0.1f, 0.4f), 3f);

        Object.Destroy(steam.gameObject);
        Shake(0.2f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("TOXIC MIST", pos, new Color(0.3f, 0.9f, 0.1f));
    }

    /// <summary>Poison + Cyclone: cyclone becomes plague vortex, wide poison spread.</summary>
    static void PlagueVortex(CycloneZone cyclone, Vector3 pos, float damage)
    {
        float radius = GetRadius(cyclone.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 2f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 1.5f, ElementType.Poison);
        }

        SpawnBurst(pos, 35, new Color(0.4f, 0.8f, 0.1f), radius * 1.5f, 5f, 0.5f, gravityMod: -0.5f);
        SpawnFlash(pos, radius * 2.5f, new Color(0.3f, 0.7f, 0.1f, 0.3f), 3f);

        Object.Destroy(cyclone.gameObject);
        Shake(0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("PLAGUE VORTEX", pos, new Color(0.4f, 0.8f, 0.1f));
    }

    // ── VOID REACTIONS ───────────────────────────────────────

    /// <summary>Void + Steam: steam infused with void energy, Exposed on all.</summary>
    static void VoidMist(SteamCloudZone steam, Vector3 pos, float damage)
    {
        float radius = GetRadius(steam.GetComponent<Collider>());
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.3f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(damage, ElementType.Void);
                // Void element applies Exposed status via ElementalStatus
            }
        }

        SpawnBurst(pos, 25, new Color(0.4f, 0.1f, 0.7f), radius, 4f, 0.4f, gravityMod: -0.5f);
        SpawnFlash(pos, radius * 1.8f, new Color(0.3f, 0f, 0.6f, 0.5f), 3f);

        Object.Destroy(steam.gameObject);
        Shake(0.25f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("VOID MIST", pos, new Color(0.4f, 0.1f, 0.7f));
    }

    /// <summary>Void + Plague: void amplifies plague for massive dark burst.</summary>
    static void Blight(PlagueCloudZone plague, Vector3 pos, float damage)
    {
        BlightGeneric(plague.gameObject, pos, plague.Radius, damage);
    }

    static void BlightGeneric(GameObject zone, Vector3 pos, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, radius * 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 3f, ElementType.Void);
        }

        SpawnBurst(pos, 30, new Color(0.3f, 0.1f, 0.5f), radius, 5f, 0.4f);
        SpawnBurst(pos, 15, new Color(0.2f, 0.6f, 0.1f), radius * 0.8f, 3f, 0.3f);
        SpawnFlash(pos, radius * 2f, new Color(0.25f, 0.05f, 0.4f, 0.6f), 4f);

        Object.Destroy(zone);
        Shake(0.4f); Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("BLIGHT", pos, new Color(0.3f, 0.1f, 0.5f));
    }

    /// <summary>Void + Cyclone: cyclone collapses into singularity — pull + concentrated damage.</summary>
    static void Singularity(CycloneZone cyclone, Vector3 pos, float damage)
    {
        float radius = GetRadius(cyclone.GetComponent<Collider>());

        // Pull enemies toward center
        Collider[] nearby = Physics.OverlapSphere(pos, radius * 2f);
        foreach (var n in nearby)
        {
            if (n.GetComponent<PlayerController>() != null) continue;
            var hp = n.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;
            var rb = n.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
                rb.AddForce((pos - n.transform.position).normalized * 12f, ForceMode.Impulse);
        }

        // Concentrated damage in tight radius
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage * 3f, ElementType.Void);
        }

        // Implosion VFX — dark shrinking sphere
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * radius * 2;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.2f, 0f, 0.4f, 0.7f), 5f);
        vfx.AddComponent<FlashShrink>().Init(0.35f);

        SpawnBurst(pos, 20, new Color(0.5f, 0.1f, 0.8f), radius * 0.5f, 2f, 0.3f);

        Object.Destroy(cyclone.gameObject);
        Shake(0.5f); Hitstop(0.08f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, pos);
        OnReaction?.Invoke("SINGULARITY", pos, new Color(0.3f, 0f, 0.5f));
    }

    // ════════════════════════════════════════════════════════════
    // ── VFX HELPERS ──
    // ════════════════════════════════════════════════════════════

    static void DamageArea(Vector3 pos, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, radius);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(damage);
        }
    }

    static void SpawnFlash(Vector3 pos, float size, Color color, float emission, float duration = 0.25f)
    {
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * size;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, emission);
        Object.Destroy(vfx, duration);
    }

    static void SpawnBurst(Vector3 pos, int count, Color color, float radius,
        float speed, float lifetime, float gravityMod = 0f)
    {
        var go = new GameObject("ReactionBurst");
        go.transform.position = pos + Vector3.up * 0.3f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravityMod;
        main.duration = 0.1f;
        main.loop = false;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = radius * 0.3f;

        var mat = new Material(ShaderCache.ParticleShader);
        mat.SetColor("_Color", color);
        go.GetComponent<ParticleSystemRenderer>().material = mat;

        ps.Play();
        Object.Destroy(go, lifetime + 0.2f);
    }

    static void Shake(float amount)
    {
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(amount);
    }

    static void Hitstop(float duration)
    {
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(duration);
    }
}
