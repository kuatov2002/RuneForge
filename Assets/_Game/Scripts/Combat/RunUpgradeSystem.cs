using UnityEngine;

/// <summary>
/// Post-room upgrade system (Dead Cells style).
/// Player chooses one of three random stat boosts.
/// Boosts apply globally to all spells.
/// </summary>
public enum UpgradeType
{
    DamageUp,       // +15% damage
    CooldownDown,   // -15% cooldown
    DurationUp,     // +20% effect duration
    RadiusUp,       // +20% radius
    ChargesUp,      // +1 charge per element
    RechargeDown,   // -20% overheat recharge time
}

public static class RunUpgradeSystem
{
    static readonly UpgradeType[] AllUpgrades = {
        UpgradeType.DamageUp,
        UpgradeType.CooldownDown,
        UpgradeType.DurationUp,
        UpgradeType.RadiusUp,
        UpgradeType.ChargesUp,
        UpgradeType.RechargeDown,
    };

    /// <summary>Generate random upgrade choices.</summary>
    public static UpgradeType[] GenerateChoices(int count = 3)
    {
        var choices = new UpgradeType[count];
        var used = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int idx;
            do { idx = Random.Range(0, AllUpgrades.Length); }
            while (used.Contains(idx) && used.Count < AllUpgrades.Length);
            used.Add(idx);
            choices[i] = AllUpgrades[idx];
        }
        return choices;
    }

    /// <summary>Apply an upgrade to the player's SpellCaster.</summary>
    public static void ApplyUpgrade(SpellCaster caster, UpgradeType upgrade)
    {
        switch (upgrade)
        {
            case UpgradeType.DamageUp:
                caster.damageBonusMult += 0.15f;
                break;
            case UpgradeType.CooldownDown:
                caster.cooldownBonusMult = Mathf.Max(0.3f, caster.cooldownBonusMult - 0.15f);
                break;
            case UpgradeType.DurationUp:
                caster.durationBonusMult += 0.20f;
                break;
            case UpgradeType.RadiusUp:
                caster.radiusBonusMult += 0.20f;
                break;
            case UpgradeType.ChargesUp:
                for (int i = 0; i < 4; i++)
                    if (caster.equippedElements[i] != null)
                        caster.equippedElements[i].maxCharges += 1;
                break;
            case UpgradeType.RechargeDown:
                for (int i = 0; i < 4; i++)
                    if (caster.equippedElements[i] != null)
                        caster.equippedElements[i].overheatRechargeTime *= 0.8f;
                break;
        }
    }

    public static string GetName(UpgradeType type) => type switch
    {
        UpgradeType.DamageUp => "Damage +15%",
        UpgradeType.CooldownDown => "Cooldown -15%",
        UpgradeType.DurationUp => "Duration +20%",
        UpgradeType.RadiusUp => "Radius +20%",
        UpgradeType.ChargesUp => "Charges +1",
        UpgradeType.RechargeDown => "Recharge -20%",
        _ => "???"
    };

    public static string GetDescription(UpgradeType type) => type switch
    {
        UpgradeType.DamageUp => "All spells deal 15% more damage",
        UpgradeType.CooldownDown => "All spell cooldowns reduced by 15%",
        UpgradeType.DurationUp => "All spell effects last 20% longer",
        UpgradeType.RadiusUp => "All spell areas increased by 20%",
        UpgradeType.ChargesUp => "Each element gets +1 charge before overheat",
        UpgradeType.RechargeDown => "Overheated elements recharge 20% faster",
        _ => ""
    };

    public static Color GetColor(UpgradeType type) => type switch
    {
        UpgradeType.DamageUp => new Color(1f, 0.4f, 0.2f),
        UpgradeType.CooldownDown => new Color(0.3f, 0.7f, 1f),
        UpgradeType.DurationUp => new Color(0.2f, 0.9f, 0.5f),
        UpgradeType.RadiusUp => new Color(1f, 0.9f, 0.2f),
        UpgradeType.ChargesUp => new Color(0.8f, 0.5f, 1f),
        UpgradeType.RechargeDown => new Color(0.5f, 0.8f, 0.3f),
        _ => Color.white
    };
}
