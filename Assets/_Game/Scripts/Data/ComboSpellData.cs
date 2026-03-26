using UnityEngine;
using System.Collections.Generic;

public enum ComboType
{
    // Same element
    Inferno,      // Fire + Fire
    DeepFreeze,   // Water + Water
    Bulwark,      // Earth + Earth
    Ascend,       // Air + Air

    // Cross element
    Steam,        // Fire + Water
    Magma,        // Fire + Earth
    Wildfire,     // Fire + Air
    Permafrost,   // Water + Earth
    Geyser,       // Water + Air
    Rubble,       // Earth + Air

    // Advanced (unlockable elements)
    // Lightning combos
    LightningStrike,  // Lightning + Lightning
    Thunderstorm,     // Lightning + Fire
    FrostShock,       // Lightning + Water
    Earthquake,       // Lightning + Earth
    Cyclone,          // Lightning + Air

    // Poison combos
    Plague,           // Poison + Poison
    Detonate,         // Poison + Fire
    ToxicFrost,       // Poison + Water
    Quicksand,        // Poison + Earth
    Miasma,           // Poison + Air
    PlagueSpark,      // Poison + Lightning

    // Void combos
    Collapse,         // Void + Void
    Implode,          // Void + Fire
    Rift,             // Void + Water
    Gravity,          // Void + Earth
    Vacuum,           // Void + Air
    RiftShock,        // Void + Lightning
    Corruption,       // Void + Poison
}

[System.Serializable]
public class ComboSpellDef
{
    public ComboType comboType;
    public string comboName;
    public string description;
    public float baseDamage;
    public float radius = 3f;
    public float duration;
    public float cooldown = 0.5f;
    public Color color;

    // Charged shot multiplier
    public float chargedDamageMultiplier = 2.5f;
    public float chargedRadiusMultiplier = 1.5f;
}

/// <summary>
/// Registry of all combo spells. Lookup by element pair.
/// </summary>
public static class ComboSpellRegistry
{
    static Dictionary<(ElementType, ElementType), ComboSpellDef> _registry;

    public static void Init()
    {
        _registry = new Dictionary<(ElementType, ElementType), ComboSpellDef>();

        // ── Same element combos ──
        Register(ElementType.Fire, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Inferno,
            comboName = "Inferno",
            description = "Massive instant AoE explosion",
            baseDamage = 30,
            radius = 5f,
            cooldown = 0.5f,
            color = new Color(1f, 0.3f, 0f)
        });

        Register(ElementType.Water, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.DeepFreeze,
            comboName = "Deep Freeze",
            description = "Freezes all enemies in radius for 3s",
            baseDamage = 5,
            radius = 5f,
            duration = 3f,
            cooldown = 0.6f,
            color = new Color(0.3f, 0.7f, 1f)
        });

        Register(ElementType.Earth, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Bulwark,
            comboName = "Bulwark",
            description = "Stone wall blocking projectiles and enemies",
            baseDamage = 0,
            radius = 0,
            duration = 5f,
            cooldown = 0.8f,
            color = new Color(0.6f, 0.4f, 0.2f)
        });

        Register(ElementType.Air, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Ascend,
            comboName = "Ascend",
            description = "Dash toward cursor, invulnerable",
            baseDamage = 0,
            radius = 0,
            cooldown = 0.6f,
            color = new Color(0.8f, 0.9f, 1f)
        });

        // ── Cross element combos ──
        Register(ElementType.Fire, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.Steam,
            comboName = "Steam",
            description = "Cloud of steam dealing small damage, explodes with Fire",
            baseDamage = 3,
            radius = 3f,
            duration = 4f,
            cooldown = 0.5f,
            color = new Color(0.8f, 0.8f, 0.9f)
        });

        Register(ElementType.Fire, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Magma,
            comboName = "Magma",
            description = "Lava pool slowing and burning enemies",
            baseDamage = 5,
            radius = 3f,
            duration = 5f,
            cooldown = 0.6f,
            color = new Color(1f, 0.5f, 0f)
        });

        Register(ElementType.Fire, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Wildfire,
            comboName = "Wildfire",
            description = "Fire chains between enemies",
            baseDamage = 12,
            radius = 4f,
            cooldown = 0.5f,
            color = new Color(1f, 0.6f, 0.1f)
        });

        Register(ElementType.Water, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Permafrost,
            comboName = "Permafrost",
            description = "Ice floor making enemies slide uncontrollably",
            baseDamage = 2,
            radius = 5f,
            duration = 4f,
            cooldown = 0.6f,
            color = new Color(0.5f, 0.8f, 0.9f)
        });

        Register(ElementType.Water, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Geyser,
            comboName = "Geyser",
            description = "Water pillar launches and damages enemies",
            baseDamage = 15,
            radius = 2.5f,
            cooldown = 0.5f,
            color = new Color(0.2f, 0.5f, 1f)
        });

        Register(ElementType.Earth, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Rubble,
            comboName = "Rubble",
            description = "Rock rain with multi-hit waves and stun",
            baseDamage = 8,
            radius = 4f,
            duration = 1.5f,
            cooldown = 0.6f,
            color = new Color(0.5f, 0.4f, 0.3f)
        });

        // ── Lightning combos ──
        Register(ElementType.Lightning, ElementType.Lightning, new ComboSpellDef
        {
            comboType = ComboType.LightningStrike,
            comboName = "Lightning Strike",
            description = "Massive bolt from the sky",
            baseDamage = 25,
            radius = 2f,
            cooldown = 0.5f,
            color = new Color(1f, 1f, 0.3f)
        });

        Register(ElementType.Lightning, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Thunderstorm,
            comboName = "Thunderstorm",
            description = "Fire and lightning rain from above",
            baseDamage = 10,
            radius = 5f,
            duration = 2f,
            cooldown = 0.6f,
            color = new Color(1f, 0.7f, 0.2f)
        });

        Register(ElementType.Lightning, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.FrostShock,
            comboName = "Frost Shock",
            description = "Freezes and stuns enemies",
            baseDamage = 15,
            radius = 4f,
            duration = 2f,
            cooldown = 0.5f,
            color = new Color(0.5f, 0.8f, 1f)
        });

        Register(ElementType.Lightning, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Earthquake,
            comboName = "Earthquake",
            description = "Ground tremor stunning nearby enemies",
            baseDamage = 18,
            radius = 6f,
            duration = 1f,
            cooldown = 0.7f,
            color = new Color(0.7f, 0.6f, 0.2f)
        });

        Register(ElementType.Lightning, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Cyclone,
            comboName = "Cyclone",
            description = "Whirlwind pulling enemies in",
            baseDamage = 8,
            radius = 4f,
            duration = 3f,
            cooldown = 0.6f,
            color = new Color(0.7f, 0.9f, 1f)
        });

        // ── Poison combos ──
        Register(ElementType.Poison, ElementType.Poison, new ComboSpellDef
        {
            comboType = ComboType.Plague,
            comboName = "Plague",
            description = "Spreading poison cloud",
            baseDamage = 4,
            radius = 5f,
            duration = 5f,
            cooldown = 0.6f,
            color = new Color(0.2f, 0.9f, 0.1f)
        });

        Register(ElementType.Poison, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Detonate,
            comboName = "Detonate",
            description = "Explodes poisoned enemies",
            baseDamage = 20,
            radius = 3f,
            cooldown = 0.5f,
            color = new Color(0.8f, 0.5f, 0.1f)
        });

        Register(ElementType.Poison, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.ToxicFrost,
            comboName = "Toxic Frost",
            description = "Slow + poison zone on ground",
            baseDamage = 3,
            radius = 4f,
            duration = 4f,
            cooldown = 0.6f,
            color = new Color(0.3f, 0.8f, 0.6f)
        });

        Register(ElementType.Poison, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Quicksand,
            comboName = "Quicksand",
            description = "Trapping enemies in poisonous ground",
            baseDamage = 6,
            radius = 4f,
            duration = 4f,
            cooldown = 0.6f,
            color = new Color(0.4f, 0.5f, 0.2f)
        });

        Register(ElementType.Poison, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Miasma,
            comboName = "Miasma",
            description = "Poisonous wind spreading across room",
            baseDamage = 5,
            radius = 6f,
            duration = 3f,
            cooldown = 0.6f,
            color = new Color(0.4f, 0.7f, 0.2f)
        });

        Register(ElementType.Poison, ElementType.Lightning, new ComboSpellDef
        {
            comboType = ComboType.PlagueSpark,
            comboName = "Plague Spark",
            description = "Lightning chains through poisoned enemies",
            baseDamage = 12,
            radius = 6f,
            cooldown = 0.5f,
            color = new Color(0.5f, 0.9f, 0.2f)
        });

        // ── Void combos ──
        Register(ElementType.Void, ElementType.Void, new ComboSpellDef
        {
            comboType = ComboType.Collapse,
            comboName = "Collapse",
            description = "Reality collapses inward dealing massive damage",
            baseDamage = 28,
            radius = 4f,
            cooldown = 0.6f,
            color = new Color(0.5f, 0.1f, 0.8f)
        });

        Register(ElementType.Void, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Implode,
            comboName = "Implode",
            description = "Pulls enemies in then damages",
            baseDamage = 18,
            radius = 4f,
            cooldown = 0.6f,
            color = new Color(0.7f, 0.2f, 0.6f)
        });

        Register(ElementType.Void, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.Rift,
            comboName = "Rift",
            description = "Opens a rift that slows and damages",
            baseDamage = 10,
            radius = 3f,
            duration = 3f,
            cooldown = 0.6f,
            color = new Color(0.4f, 0.3f, 0.8f)
        });

        Register(ElementType.Void, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Gravity,
            comboName = "Gravity",
            description = "Crushes enemies with gravity force",
            baseDamage = 22,
            radius = 3f,
            cooldown = 0.6f,
            color = new Color(0.4f, 0.2f, 0.5f)
        });

        Register(ElementType.Void, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Vacuum,
            comboName = "Vacuum",
            description = "Pulls all enemies toward a point",
            baseDamage = 8,
            radius = 6f,
            duration = 2f,
            cooldown = 0.6f,
            color = new Color(0.5f, 0.4f, 0.8f)
        });

        Register(ElementType.Void, ElementType.Lightning, new ComboSpellDef
        {
            comboType = ComboType.RiftShock,
            comboName = "Rift Shock",
            description = "Stuns all nearby enemies",
            baseDamage = 14,
            radius = 5f,
            duration = 2f,
            cooldown = 0.6f,
            color = new Color(0.7f, 0.3f, 1f)
        });

        Register(ElementType.Void, ElementType.Poison, new ComboSpellDef
        {
            comboType = ComboType.Corruption,
            comboName = "Corruption",
            description = "Spreading void corruption",
            baseDamage = 10,
            radius = 5f,
            duration = 4f,
            cooldown = 0.6f,
            color = new Color(0.4f, 0.1f, 0.7f)
        });
    }

    static void Register(ElementType a, ElementType b, ComboSpellDef def)
    {
        // Store both orderings for easy lookup
        var key1 = (a, b);
        var key2 = (b, a);
        _registry[key1] = def;
        if (a != b) _registry[key2] = def;
    }

    /// <summary>Get the combo spell for two elements. Returns null if no combo defined.</summary>
    public static ComboSpellDef GetCombo(ElementType a, ElementType b)
    {
        if (_registry == null) Init();
        _registry.TryGetValue((a, b), out var def);
        return def;
    }
}
