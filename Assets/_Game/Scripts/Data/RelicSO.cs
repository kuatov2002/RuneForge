using UnityEngine;

public enum RelicType
{
    SpeedBoost,       // +15% movement speed
    DoubleStrike,     // Every 5th hit deals 2x damage
    DashFire,         // Dash leaves fire trail
    Thorns,           // Retaliation AoE on damage taken
    VampireAura,      // Heal 1 HP per room cleared
    GlassCannon,      // CURSED: +50% damage, -2 max HP
    Shield,           // Block first hit in each room
    Lucky,            // Treasure Hunter: bonus gold on room clear
    Berserker,        // +25% damage when below 50% HP
    Regeneration,     // Vitality: heal 1 HP/20s, overshield at max

    // ─── CURSED RELICS (risk/reward) ───
    CursedPower,      // +75% damage, take +50% damage per hit
    CursedSpeed,      // +40% move speed, -2 max HP
    CursedGold,       // 3x gold drops, enemies have +30% HP
    BloodPact,        // Spells cost 1 HP/3 casts, +60% damage
    Chaos,            // Random element on each cast, +30% damage

    // ─── ELEMENT-SPECIFIC RELICS ───
    EmberHeart,       // Fire spells chain +1 + burning ground
    FrostCrown,       // Freeze duration +50% + chain-freeze on kill
    StoneSkin,        // Fortify: movement stacks reduce damage
    GaleRing,         // Dash distance +30% + post-dash +30% damage
    StormConductor,   // +15% crit for lightning + chain on crit
    VenomSac,         // Poison ticks 1.5x + poison cloud on kill
    VoidLens,         // Void pull radius +40% + Exposed on center
    PrismShard,       // 3+ elements in 10s = +25% damage for 5s

    // ═══ NEW: TRIGGER-BASED RELICS ═══
    Aftershock,       // Shockwave on momentum kill (tier 2+)
    QuickSwap,        // +25% damage 1.5s after element switch
    OverheatSurge,    // Elemental explosion when element overheats
    SoulHarvest,      // Heal 1 HP every 8 kills in streak
    DashRecharge,     // Dash restores 1 charge to overheated element
    LastStand,        // Survive lethal hit once, +50% damage 5s
    ElementalEcho,    // Kill spreads victim's status to nearby (2u)
    DashChain,        // Dash through 2+ enemies = next cast instant
    ReactorCore,      // Each reaction = +6% damage stack (8s, max 5)
    GoldRush,         // Clear room in <15s = +50% gold bonus

    // ═══ NEW: CONDITIONAL PASSIVE RELICS ═══
    LowTide,          // No overheated elements = -15% cooldown
    Underdog,         // Below 30% HP: +20% speed, -1 incoming damage
    FullPower,        // At max HP: +20% damage
    ElementalFocus,   // Same-element combo = +15% damage
    FloorVeteran,     // +5% damage per floor cleared (permanent)
    Minimalist,       // 3 or fewer relics = +40% damage
    MomentumArmor,    // Momentum tier 3+ = -2 incoming damage
    VoidAffinity,     // +30% damage to Exposed enemies
    CrowdedRoom,      // 5+ enemies alive = +20% damage
    ChargeHoarder,    // All elements at max charges = +15% damage

    // ═══ NEW: BUILD-DEFINING RELICS ═══
    Monolith,         // Stand still: +60% damage, +50% radius (6s max)
    Gambler,          // Each cast: 50% triple damage, 50% zero damage
    Siphon,           // 50% damage dealt, steal equal HP
    Cascade,          // Kill = auto-cast last spell at death pos (50% dmg)
    ElementalOverload,// All elements share overheat, +25% damage always
    PhantomCaster,    // Dash spawns phantom that copies last spell (3s)
    Convergence,      // Lock to 2 elements, those combos +40% damage
    EntropyRelic,     // Force overheat every 20s, +30% damage during

    // ═══ NEW: UTILITY RELICS ═══
    Cartographer,     // Reveal adjacent room types on floor enter
    Scavenger,        // 15% chance enemies drop healing pickup (1 HP)
    MagnetField,      // Gold pickup radius x2.5
    Merchant,         // Shop has +1 item, potions -30% price
    TimeDilation,     // Stand 0.5s = slow enemies in 3u by 40%
    EscapeArtist,     // At 1 HP: free emergency dash (10s cooldown)
    RuneRecycler,     // Overheat penalty skipped, lose 1 extra charge
    GoldShield,       // Spend 10 gold to block 1 damage (auto)

    // ═══ NEW: COMBO-SYSTEM RELICS ═══
    ComboMaster,      // Variety bonus thresholds -1 element each
    ReactionCatalyst, // 30% chance reaction preserves element status
    OverheatMastery,  // While any element overheated: +30% damage
    DualElement,      // Spells apply BOTH element statuses

    // ═══ NEW: CURSED RELICS ═══
    CursedVelocity,   // -30% cooldown, stunned 0.5s every 12s
    CursedAbsorption, // Kills stack +3% damage (max 10), -1 HP every 8s
    CursedEcho,       // 25% free recast, 10% recast hits you for 3 dmg
    CursedHunger,     // +35% damage, no kills for 6s = lose 1 HP
    CursedMirror,     // Reactions deal +50% damage, apply 0.3s debuff to you
    CursedFortune,    // Gold pickups heal 1 HP, shop prices +50%
}

public class RelicSO : ScriptableObject
{
    public string relicName;
    public RelicType relicType;
    public string description;
    public Color color;
    public bool isCursed;
}
