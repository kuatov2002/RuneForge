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

        // ── Same element combos (powerful but specialized) ──
        Register(ElementType.Fire, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Inferno, comboName = "Inferno",
            description = "Instant AoE explosion",
            baseDamage = 18, radius = 2.5f, cooldown = 0.8f,
            chargedDamageMultiplier = 2.2f, chargedRadiusMultiplier = 1.4f,
            color = new Color(1f, 0.3f, 0f)
        });
        Register(ElementType.Water, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.DeepFreeze, comboName = "Deep Freeze",
            description = "Freezes all enemies in radius",
            baseDamage = 3, radius = 2.2f, duration = 2f, cooldown = 1f,
            color = new Color(0.3f, 0.7f, 1f)
        });
        Register(ElementType.Earth, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Bulwark, comboName = "Bulwark",
            description = "Stone wall blocking projectiles",
            baseDamage = 0, radius = 0, duration = 4f, cooldown = 1.2f,
            color = new Color(0.6f, 0.4f, 0.2f)
        });
        Register(ElementType.Air, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Ascend, comboName = "Ascend",
            description = "Dash toward cursor, invulnerable",
            baseDamage = 0, radius = 0, cooldown = 0.8f,
            color = new Color(0.8f, 0.9f, 1f)
        });

        // ── Cross element combos ──
        Register(ElementType.Fire, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.Steam, comboName = "Steam",
            description = "Steam cloud, ignitable by Fire",
            baseDamage = 2, radius = 2f, duration = 3.5f, cooldown = 0.7f,
            color = new Color(0.8f, 0.8f, 0.9f)
        });
        Register(ElementType.Fire, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Magma, comboName = "Magma",
            description = "Lava pool: slow + DoT",
            baseDamage = 3, radius = 2f, duration = 4f, cooldown = 0.8f,
            color = new Color(1f, 0.5f, 0f)
        });
        Register(ElementType.Fire, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Wildfire, comboName = "Wildfire",
            description = "Fire chains between enemies",
            baseDamage = 8, radius = 3f, cooldown = 0.7f,
            color = new Color(1f, 0.6f, 0.1f)
        });
        Register(ElementType.Water, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Permafrost, comboName = "Permafrost",
            description = "Ice floor, enemies slide",
            baseDamage = 1, radius = 3f, duration = 3.5f, cooldown = 0.8f,
            color = new Color(0.5f, 0.8f, 0.9f)
        });
        Register(ElementType.Water, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Geyser, comboName = "Geyser",
            description = "Water pillar launches enemies",
            baseDamage = 10, radius = 1.8f, cooldown = 0.7f,
            color = new Color(0.2f, 0.5f, 1f)
        });
        Register(ElementType.Earth, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Rubble, comboName = "Rubble",
            description = "Rock rain with stun",
            baseDamage = 5, radius = 2.5f, duration = 1.5f, cooldown = 0.8f,
            color = new Color(0.5f, 0.4f, 0.3f)
        });

        // ── Lightning combos ──
        Register(ElementType.Lightning, ElementType.Lightning, new ComboSpellDef
        {
            comboType = ComboType.LightningStrike, comboName = "Lightning Strike",
            description = "Bolt from the sky",
            baseDamage = 16, radius = 1.5f, cooldown = 0.7f,
            color = new Color(1f, 1f, 0.3f)
        });
        Register(ElementType.Lightning, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Thunderstorm, comboName = "Thunderstorm",
            description = "Fire and lightning rain",
            baseDamage = 6, radius = 3f, duration = 2f, cooldown = 0.9f,
            color = new Color(1f, 0.7f, 0.2f)
        });
        Register(ElementType.Lightning, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.FrostShock, comboName = "Frost Shock",
            description = "Freezes and stuns",
            baseDamage = 10, radius = 2.5f, duration = 1.5f, cooldown = 0.8f,
            color = new Color(0.5f, 0.8f, 1f)
        });
        Register(ElementType.Lightning, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Earthquake, comboName = "Earthquake",
            description = "Ground tremor with stun",
            baseDamage = 12, radius = 3.5f, duration = 0.8f, cooldown = 1f,
            color = new Color(0.7f, 0.6f, 0.2f)
        });
        Register(ElementType.Lightning, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Cyclone, comboName = "Cyclone",
            description = "Whirlwind pulling enemies",
            baseDamage = 4, radius = 2.5f, duration = 2.5f, cooldown = 0.9f,
            color = new Color(0.7f, 0.9f, 1f)
        });

        // ── Poison combos ──
        Register(ElementType.Poison, ElementType.Poison, new ComboSpellDef
        {
            comboType = ComboType.Plague, comboName = "Plague",
            description = "Spreading poison cloud",
            baseDamage = 2, radius = 3f, duration = 4f, cooldown = 0.9f,
            color = new Color(0.2f, 0.9f, 0.1f)
        });
        Register(ElementType.Poison, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Detonate, comboName = "Detonate",
            description = "Toxic explosion",
            baseDamage = 14, radius = 2f, cooldown = 0.7f,
            color = new Color(0.8f, 0.5f, 0.1f)
        });
        Register(ElementType.Poison, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.ToxicFrost, comboName = "Toxic Frost",
            description = "Slow + poison zone",
            baseDamage = 2, radius = 2.5f, duration = 3f, cooldown = 0.8f,
            color = new Color(0.3f, 0.8f, 0.6f)
        });
        Register(ElementType.Poison, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Quicksand, comboName = "Quicksand",
            description = "Trapping poisonous ground",
            baseDamage = 3, radius = 2.5f, duration = 3f, cooldown = 0.8f,
            color = new Color(0.4f, 0.5f, 0.2f)
        });
        Register(ElementType.Poison, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Miasma, comboName = "Miasma",
            description = "Poisonous wind",
            baseDamage = 3, radius = 3.5f, duration = 2.5f, cooldown = 0.8f,
            color = new Color(0.4f, 0.7f, 0.2f)
        });
        Register(ElementType.Poison, ElementType.Lightning, new ComboSpellDef
        {
            comboType = ComboType.PlagueSpark, comboName = "Plague Spark",
            description = "Lightning chains through enemies",
            baseDamage = 8, radius = 3.5f, cooldown = 0.7f,
            color = new Color(0.5f, 0.9f, 0.2f)
        });

        // ── Void combos ──
        Register(ElementType.Void, ElementType.Void, new ComboSpellDef
        {
            comboType = ComboType.Collapse, comboName = "Collapse",
            description = "Reality collapses inward",
            baseDamage = 20, radius = 2.5f, cooldown = 0.9f,
            color = new Color(0.5f, 0.1f, 0.8f)
        });
        Register(ElementType.Void, ElementType.Fire, new ComboSpellDef
        {
            comboType = ComboType.Implode, comboName = "Implode",
            description = "Pulls enemies in, then damages",
            baseDamage = 12, radius = 2.5f, cooldown = 0.8f,
            color = new Color(0.7f, 0.2f, 0.6f)
        });
        Register(ElementType.Void, ElementType.Water, new ComboSpellDef
        {
            comboType = ComboType.Rift, comboName = "Rift",
            description = "Rift that slows and damages",
            baseDamage = 6, radius = 2f, duration = 2.5f, cooldown = 0.8f,
            color = new Color(0.4f, 0.3f, 0.8f)
        });
        Register(ElementType.Void, ElementType.Earth, new ComboSpellDef
        {
            comboType = ComboType.Gravity, comboName = "Gravity",
            description = "Gravity crush",
            baseDamage = 15, radius = 2f, cooldown = 0.8f,
            color = new Color(0.4f, 0.2f, 0.5f)
        });
        Register(ElementType.Void, ElementType.Air, new ComboSpellDef
        {
            comboType = ComboType.Vacuum, comboName = "Vacuum",
            description = "Pulls enemies toward a point",
            baseDamage = 4, radius = 3.5f, duration = 1.5f, cooldown = 0.8f,
            color = new Color(0.5f, 0.4f, 0.8f)
        });
        Register(ElementType.Void, ElementType.Lightning, new ComboSpellDef
        {
            comboType = ComboType.RiftShock, comboName = "Rift Shock",
            description = "Stuns nearby enemies",
            baseDamage = 9, radius = 3f, duration = 1.5f, cooldown = 0.8f,
            color = new Color(0.7f, 0.3f, 1f)
        });
        Register(ElementType.Void, ElementType.Poison, new ComboSpellDef
        {
            comboType = ComboType.Corruption, comboName = "Corruption",
            description = "Spreading void corruption",
            baseDamage = 5, radius = 3f, duration = 3f, cooldown = 0.8f,
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
