using UnityEngine;

public enum RelicType
{
    SpeedBoost,       // +15% movement speed
    DoubleStrike,     // Every 5th hit deals 2x damage
    DashFire,         // Dash leaves fire trail
    Thorns,           // Reflect 1 damage to melee attackers
    VampireAura,      // Heal 1 HP per room cleared
    GlassCannon,      // +50% damage, -1 max HP
    Shield,           // Block first hit in each room
    Lucky,            // +20% chance for extra rune choice
    Berserker,        // +25% damage when below 50% HP
    Regeneration      // Heal 1 HP every 30 seconds
}

public class RelicSO : ScriptableObject
{
    public string relicName;
    public RelicType relicType;
    public string description;
    public Color color;
}
