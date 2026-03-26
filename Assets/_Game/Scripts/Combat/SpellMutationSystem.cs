using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spell Mutation System — provides run-defining spell modifications.
/// Mutations are offered in Spell Forge events and occasionally as upgrade alternatives.
/// Max 3 mutations per run.
/// </summary>
public static class SpellMutationSystem
{
    public enum MutationType
    {
        SplitShot,       // Projectiles split into 2 on first hit (50% dmg each)
        Piercing,        // Projectiles pierce +1 enemy
        Homing,          // Slight homing toward nearest enemy
        LingeringZone,   // Zones last 50% longer
        ChainCast,       // 20% chance to auto-cast same spell again (free)
        Overcharge,      // Charged spells deal 4x instead of 2.5x, cost 3 charges
        GlassSpells,     // +80% damage, -30% radius
        WideSpells,      // +50% radius, -20% damage
        Vampiric,        // Heal 1 HP per 5 kills with spells
        Volatile,        // All kills create small explosions (2 dmg, 1.5u radius)
    }

    public struct MutationDef
    {
        public MutationType type;
        public string name;
        public string description;
        public Color color;
    }

    static readonly MutationDef[] AllMutations = new[]
    {
        new MutationDef { type = MutationType.SplitShot, name = "SPLIT SHOT",
            description = "Projectiles split into 2 on hit (50% dmg each)",
            color = new Color(0.3f, 0.7f, 1f) },
        new MutationDef { type = MutationType.Piercing, name = "PIERCING",
            description = "Projectiles pierce through 1 extra enemy",
            color = new Color(0.9f, 0.9f, 1f) },
        new MutationDef { type = MutationType.Homing, name = "HOMING",
            description = "Projectiles curve slightly toward nearest enemy",
            color = new Color(0.5f, 0.3f, 1f) },
        new MutationDef { type = MutationType.LingeringZone, name = "LINGERING ZONES",
            description = "Zone spells last 50% longer",
            color = new Color(0.2f, 0.8f, 0.5f) },
        new MutationDef { type = MutationType.ChainCast, name = "CHAIN CAST",
            description = "20% chance to auto-cast the same spell again for free",
            color = new Color(1f, 0.85f, 0.2f) },
        new MutationDef { type = MutationType.Overcharge, name = "OVERCHARGE",
            description = "Charged spells deal 4x damage but cost 3 charges",
            color = new Color(1f, 0.3f, 0.1f) },
        new MutationDef { type = MutationType.GlassSpells, name = "GLASS CANNON SPELLS",
            description = "+80% spell damage, -30% spell radius",
            color = new Color(1f, 0.2f, 0.3f) },
        new MutationDef { type = MutationType.WideSpells, name = "WIDE SPELLS",
            description = "+50% spell radius, -20% spell damage",
            color = new Color(0.3f, 0.5f, 1f) },
        new MutationDef { type = MutationType.Vampiric, name = "VAMPIRIC SPELLS",
            description = "Heal 1 HP for every 5 spell kills",
            color = new Color(0.8f, 0.1f, 0.2f) },
        new MutationDef { type = MutationType.Volatile, name = "VOLATILE",
            description = "All spell kills create small explosions (2 dmg, 1.5u)",
            color = new Color(1f, 0.5f, 0f) },
    };

    static List<MutationType> _activeMutations = new();
    static int _vampiricKillCount;
    public const int MaxMutations = 3;

    public static IReadOnlyList<MutationType> ActiveMutations => _activeMutations;
    public static bool HasMutation(MutationType type) => _activeMutations.Contains(type);

    public static void Reset()
    {
        _activeMutations.Clear();
        _vampiricKillCount = 0;
    }

    public static bool AddMutation(MutationType type)
    {
        if (_activeMutations.Count >= MaxMutations) return false;
        if (_activeMutations.Contains(type)) return false;
        _activeMutations.Add(type);
        return true;
    }

    /// <summary>Generate 3 random mutation choices (not already owned)</summary>
    public static MutationDef[] GenerateChoices(int count = 3)
    {
        var available = new List<MutationDef>();
        foreach (var m in AllMutations)
        {
            if (!_activeMutations.Contains(m.type))
                available.Add(m);
        }

        // Shuffle and pick
        for (int i = available.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (available[i], available[j]) = (available[j], available[i]);
        }

        int n = Mathf.Min(count, available.Count);
        var result = new MutationDef[n];
        for (int i = 0; i < n; i++) result[i] = available[i];
        return result;
    }

    /// <summary>Get all mutation definitions (for UI)</summary>
    public static MutationDef GetDef(MutationType type)
    {
        foreach (var m in AllMutations)
            if (m.type == type) return m;
        return AllMutations[0];
    }

    // ─── Mutation effects applied at cast time ───

    /// <summary>Modify damage multiplier based on active mutations</summary>
    public static float ModifyDamage(float dmg)
    {
        if (HasMutation(MutationType.GlassSpells)) dmg *= 1.8f;
        if (HasMutation(MutationType.WideSpells)) dmg *= 0.8f;
        return dmg;
    }

    /// <summary>Modify radius based on active mutations</summary>
    public static float ModifyRadius(float radius)
    {
        if (HasMutation(MutationType.GlassSpells)) radius *= 0.7f;
        if (HasMutation(MutationType.WideSpells)) radius *= 1.5f;
        return radius;
    }

    /// <summary>Modify duration based on active mutations</summary>
    public static float ModifyDuration(float duration)
    {
        if (HasMutation(MutationType.LingeringZone)) duration *= 1.5f;
        return duration;
    }

    /// <summary>Modify charged damage multiplier</summary>
    public static float ModifyChargedMultiplier(float mult)
    {
        if (HasMutation(MutationType.Overcharge)) return 4f;
        return mult;
    }

    /// <summary>Modify charged charge cost</summary>
    public static int ModifyChargedCost(int cost)
    {
        if (HasMutation(MutationType.Overcharge)) return 3;
        return cost;
    }

    /// <summary>Called when an enemy dies from spell damage. Returns true if player should heal.</summary>
    public static bool OnSpellKill(Vector3 deathPos)
    {
        bool shouldHeal = false;

        // Vampiric
        if (HasMutation(MutationType.Vampiric))
        {
            _vampiricKillCount++;
            if (_vampiricKillCount >= 5)
            {
                _vampiricKillCount = 0;
                shouldHeal = true;
            }
        }

        // Volatile — create small explosion
        if (HasMutation(MutationType.Volatile))
        {
            SpawnVolatileExplosion(deathPos);
        }

        return shouldHeal;
    }

    static void SpawnVolatileExplosion(Vector3 pos)
    {
        // Damage nearby enemies
        var hits = Physics.OverlapSphere(pos, 1.5f);
        foreach (var h in hits)
        {
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(2);
        }

        // VFX
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.3f;
        vfx.transform.localScale = Vector3.one * 0.3f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.5f, 0f), 5f);
        vfx.AddComponent<ExpandRingVFX>().Init(3f, 0.3f);

        if (TopDownCamera.Instance != null)
            TopDownCamera.Instance.AddTrauma(0.05f);
    }
}
