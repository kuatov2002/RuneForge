using UnityEngine;
using System.Collections.Generic;

// ─── STATUS TYPES ───────────────────────────────────────────

public enum ElementalStatusType
{
    Burning,    // Fire   — DoT
    Wet,        // Water  — enables reactions
    Weighted,   // Earth  — slow
    Windswept,  // Air    — amplifies knockback
    Charged,    // Lightning — next different-element hit chains
    Poisoned,   // Poison — DoT
    Exposed     // Void   — takes more damage
}

public enum ReactionType
{
    Vaporize,       // Wet + Fire
    Electrocute,    // Wet + Lightning
    Shatter,        // Wet + Water (second hit)
    Extinguish,     // Burning + Water
    ToxicExplosion, // Burning + Poison
    Firestorm,      // Burning + Air
    Detonate,       // Poisoned + Fire
    PlagueSpark,    // Poisoned + Lightning
    ShortCircuit,   // Charged + Water
    Overload,       // Charged + Earth
    Shockwave,      // Weighted + Lightning
    Launch,         // Weighted + Air
    Exploit         // Exposed + any
}

// ─── STATUS PARAMS ──────────────────────────────────────────

public struct StatusParams
{
    public float dotPerSecond;
    public float duration;
    public float speedMultiplier;   // 1.0 = normal
    public float damageAmplifier;   // 1.0 = normal

    public StatusParams(float dot, float dur, float speed, float amp)
    {
        dotPerSecond = dot;
        duration = dur;
        speedMultiplier = speed;
        damageAmplifier = amp;
    }
}

// ─── REACTION RESULT ────────────────────────────────────────

public struct ReactionResult
{
    public ReactionType type;
    public string displayName;
    public float damageMultiplier;
    public float aoeRadius;         // 0 = single target
    public Color color;
}

// ─── STATIC DEFINITIONS ─────────────────────────────────────

public static class ElementalStatusDefs
{
    // ── Status params ──

    static readonly Dictionary<ElementalStatusType, StatusParams> _params = new()
    {
        { ElementalStatusType.Burning,   new StatusParams(4f,   4f, 1f,  1f) },
        { ElementalStatusType.Wet,       new StatusParams(0f,   6f, 1f,  1f) },
        { ElementalStatusType.Weighted,  new StatusParams(0f,   4f, 0.7f,1f) },
        { ElementalStatusType.Windswept, new StatusParams(0f,   4f, 1f,  1f) },
        { ElementalStatusType.Charged,   new StatusParams(0f,   5f, 1f,  1f) },
        { ElementalStatusType.Poisoned,  new StatusParams(3f,   5f, 1f,  1f) },
        { ElementalStatusType.Exposed,   new StatusParams(0f,   5f, 1f,  1.25f) },
    };

    public static StatusParams GetParams(ElementalStatusType type) => _params[type];

    // ── Status colors ──

    public static readonly Dictionary<ElementalStatusType, Color> StatusColors = new()
    {
        { ElementalStatusType.Burning,   new Color(1f,   0.5f, 0f) },
        { ElementalStatusType.Wet,       new Color(0.3f, 0.6f, 1f) },
        { ElementalStatusType.Weighted,  new Color(0.6f, 0.4f, 0.2f) },
        { ElementalStatusType.Windswept, new Color(0.8f, 0.9f, 1f) },
        { ElementalStatusType.Charged,   new Color(1f,   1f,   0.3f) },
        { ElementalStatusType.Poisoned,  new Color(0.2f, 0.9f, 0.1f) },
        { ElementalStatusType.Exposed,   new Color(0.5f, 0.1f, 0.8f) },
    };

    // ── Element → Status mapping ──

    static readonly Dictionary<ElementType, ElementalStatusType> _elementToStatus = new()
    {
        { ElementType.Fire,      ElementalStatusType.Burning },
        { ElementType.Water,     ElementalStatusType.Wet },
        { ElementType.Earth,     ElementalStatusType.Weighted },
        { ElementType.Air,       ElementalStatusType.Windswept },
        { ElementType.Lightning, ElementalStatusType.Charged },
        { ElementType.Poison,    ElementalStatusType.Poisoned },
        { ElementType.Void,      ElementalStatusType.Exposed },
    };

    public static ElementalStatusType GetStatus(ElementType element) => _elementToStatus[element];

    // ── ComboType → Primary Element ──

    static readonly Dictionary<ComboType, ElementType> _comboToElement = new()
    {
        // Same element
        { ComboType.Inferno,        ElementType.Fire },
        { ComboType.DeepFreeze,     ElementType.Water },
        { ComboType.Bulwark,        ElementType.Earth },
        { ComboType.Ascend,         ElementType.Air },

        // Cross base: dominant element
        { ComboType.Steam,          ElementType.Water },
        { ComboType.Magma,          ElementType.Fire },
        { ComboType.Wildfire,       ElementType.Fire },
        { ComboType.Permafrost,     ElementType.Water },
        { ComboType.Geyser,         ElementType.Water },
        { ComboType.Rubble,         ElementType.Earth },

        // Lightning always dominates
        { ComboType.LightningStrike, ElementType.Lightning },
        { ComboType.Thunderstorm,    ElementType.Lightning },
        { ComboType.FrostShock,      ElementType.Lightning },
        { ComboType.Earthquake,      ElementType.Lightning },
        { ComboType.Cyclone,         ElementType.Lightning },

        // Poison always dominates
        { ComboType.Plague,          ElementType.Poison },
        { ComboType.Detonate,        ElementType.Poison },
        { ComboType.ToxicFrost,      ElementType.Poison },
        { ComboType.Quicksand,       ElementType.Poison },
        { ComboType.Miasma,          ElementType.Poison },
        { ComboType.PlagueSpark,     ElementType.Poison },

        // Void always dominates
        { ComboType.Collapse,        ElementType.Void },
        { ComboType.Implode,         ElementType.Void },
        { ComboType.Rift,            ElementType.Void },
        { ComboType.Gravity,         ElementType.Void },
        { ComboType.Vacuum,          ElementType.Void },
        { ComboType.RiftShock,       ElementType.Void },
        { ComboType.Corruption,      ElementType.Void },
    };

    public static ElementType GetPrimaryElement(ComboType combo)
    {
        return _comboToElement.TryGetValue(combo, out var e) ? e : ElementType.Fire;
    }

    // ── Reaction table ──

    // Key: (existing status, incoming status)
    static Dictionary<(ElementalStatusType, ElementalStatusType), ReactionResult> _reactions;

    public static ReactionResult? GetReaction(ElementalStatusType existing, ElementalStatusType incoming)
    {
        EnsureReactions();

        // Exposed + anything = Exploit
        if (existing == ElementalStatusType.Exposed && incoming != ElementalStatusType.Exposed)
            return new ReactionResult
            {
                type = ReactionType.Exploit,
                displayName = "EXPLOIT",
                damageMultiplier = 2f,
                aoeRadius = 0f,
                color = ElementalStatusDefs.StatusColors[ElementalStatusType.Exposed]
            };

        if (_reactions.TryGetValue((existing, incoming), out var r))
            return r;

        return null;
    }

    static void EnsureReactions()
    {
        if (_reactions != null) return;
        _reactions = new Dictionary<(ElementalStatusType, ElementalStatusType), ReactionResult>
        {
            {
                (ElementalStatusType.Wet, ElementalStatusType.Burning),
                new ReactionResult { type = ReactionType.Vaporize, displayName = "VAPORIZE",
                    damageMultiplier = 2f, aoeRadius = 0f,
                    color = new Color(0.8f, 0.8f, 0.9f) }
            },
            {
                (ElementalStatusType.Wet, ElementalStatusType.Charged),
                new ReactionResult { type = ReactionType.Electrocute, displayName = "ELECTROCUTE",
                    damageMultiplier = 1.5f, aoeRadius = 5f,
                    color = new Color(0.5f, 0.8f, 1f) }
            },
            {
                (ElementalStatusType.Wet, ElementalStatusType.Wet),
                new ReactionResult { type = ReactionType.Shatter, displayName = "SHATTER",
                    damageMultiplier = 1.5f, aoeRadius = 0f,
                    color = new Color(0.5f, 0.85f, 1f) }
            },
            {
                (ElementalStatusType.Burning, ElementalStatusType.Wet),
                new ReactionResult { type = ReactionType.Extinguish, displayName = "EXTINGUISH",
                    damageMultiplier = 1.5f, aoeRadius = 3f,
                    color = new Color(0.8f, 0.8f, 0.9f) }
            },
            {
                (ElementalStatusType.Burning, ElementalStatusType.Poisoned),
                new ReactionResult { type = ReactionType.ToxicExplosion, displayName = "TOXIC EXPLOSION",
                    damageMultiplier = 2.5f, aoeRadius = 3f,
                    color = new Color(0.7f, 0.5f, 0.1f) }
            },
            {
                (ElementalStatusType.Burning, ElementalStatusType.Windswept),
                new ReactionResult { type = ReactionType.Firestorm, displayName = "FIRESTORM",
                    damageMultiplier = 1f, aoeRadius = 4f,
                    color = new Color(1f, 0.4f, 0f) }
            },
            {
                (ElementalStatusType.Poisoned, ElementalStatusType.Burning),
                new ReactionResult { type = ReactionType.Detonate, displayName = "DETONATE",
                    damageMultiplier = 2f, aoeRadius = 2.5f,
                    color = new Color(0.8f, 0.6f, 0.1f) }
            },
            {
                (ElementalStatusType.Poisoned, ElementalStatusType.Charged),
                new ReactionResult { type = ReactionType.PlagueSpark, displayName = "PLAGUE SPARK",
                    damageMultiplier = 1.5f, aoeRadius = 4f,
                    color = new Color(0.4f, 0.9f, 0.3f) }
            },
            {
                (ElementalStatusType.Charged, ElementalStatusType.Wet),
                new ReactionResult { type = ReactionType.ShortCircuit, displayName = "SHORT CIRCUIT",
                    damageMultiplier = 1.5f, aoeRadius = 3f,
                    color = new Color(0.6f, 0.8f, 1f) }
            },
            {
                (ElementalStatusType.Charged, ElementalStatusType.Weighted),
                new ReactionResult { type = ReactionType.Overload, displayName = "OVERLOAD",
                    damageMultiplier = 2f, aoeRadius = 0f,
                    color = new Color(1f, 0.8f, 0.2f) }
            },
            {
                (ElementalStatusType.Weighted, ElementalStatusType.Charged),
                new ReactionResult { type = ReactionType.Shockwave, displayName = "SHOCKWAVE",
                    damageMultiplier = 1.5f, aoeRadius = 3.5f,
                    color = new Color(0.7f, 0.5f, 0.2f) }
            },
            {
                (ElementalStatusType.Weighted, ElementalStatusType.Windswept),
                new ReactionResult { type = ReactionType.Launch, displayName = "LAUNCH",
                    damageMultiplier = 1.5f, aoeRadius = 0f,
                    color = new Color(0.7f, 0.8f, 0.9f) }
            },
        };
    }
}
