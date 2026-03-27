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

    // Track extra charges and recharge bonuses per-run (not on ScriptableObject)
    static int runExtraCharges;
    static float runRechargeMultiplier = 1f;

    // Hard caps: prevent overheat system from becoming irrelevant in late runs
    public const int MaxExtraCharges = 2;
    public const float MinRechargeMultiplier = 0.5f;

    public static int RunExtraCharges => runExtraCharges;
    public static float RunRechargeMultiplier => runRechargeMultiplier;
    public static bool IsChargesMaxed => runExtraCharges >= MaxExtraCharges;
    public static bool IsRechargeMaxed => runRechargeMultiplier <= MinRechargeMultiplier + 0.01f;

    public static void ResetRunUpgrades()
    {
        runExtraCharges = 0;
        runRechargeMultiplier = 1f;
        damageCapped = false;
        cooldownCapped = false;
        durationCapped = false;
        radiusCapped = false;
    }

    /// <summary>Generate random upgrade choices (no duplicates, excludes maxed upgrades).</summary>
    public static UpgradeType[] GenerateChoices(int count = 3)
    {
        // Build available pool, excluding upgrades that hit their cap
        var pool = new System.Collections.Generic.List<UpgradeType>();
        foreach (var u in AllUpgrades)
        {
            if (u == UpgradeType.ChargesUp && IsChargesMaxed) continue;
            if (u == UpgradeType.RechargeDown && IsRechargeMaxed) continue;
            if (u == UpgradeType.DamageUp && damageCapped) continue;
            if (u == UpgradeType.CooldownDown && cooldownCapped) continue;
            if (u == UpgradeType.DurationUp && durationCapped) continue;
            if (u == UpgradeType.RadiusUp && radiusCapped) continue;
            pool.Add(u);
        }

        count = Mathf.Min(count, pool.Count);
        var choices = new UpgradeType[count];
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, pool.Count);
            choices[i] = pool[idx];
            pool.RemoveAt(idx);
        }
        return choices;
    }

    // Track which stat upgrades have reached their caps (set during ApplyUpgrade)
    static bool damageCapped, cooldownCapped, durationCapped, radiusCapped;

    /// <summary>Apply an upgrade to the player's SpellCaster.</summary>
    public static void ApplyUpgrade(SpellCaster caster, UpgradeType upgrade)
    {
        switch (upgrade)
        {
            case UpgradeType.DamageUp:
                caster.damageBonusMult = Mathf.Min(caster.damageBonusMult + 0.15f, 3f);
                if (caster.damageBonusMult >= 2.95f) damageCapped = true;
                break;
            case UpgradeType.CooldownDown:
                caster.cooldownBonusMult = Mathf.Max(0.3f, caster.cooldownBonusMult - 0.15f);
                if (caster.cooldownBonusMult <= 0.35f) cooldownCapped = true;
                break;
            case UpgradeType.DurationUp:
                caster.durationBonusMult = Mathf.Min(caster.durationBonusMult + 0.20f, 3f);
                if (caster.durationBonusMult >= 2.95f) durationCapped = true;
                break;
            case UpgradeType.RadiusUp:
                caster.radiusBonusMult = Mathf.Min(caster.radiusBonusMult + 0.20f, 3f);
                if (caster.radiusBonusMult >= 2.95f) radiusCapped = true;
                break;
            case UpgradeType.ChargesUp:
                runExtraCharges = Mathf.Min(runExtraCharges + 1, MaxExtraCharges);
                break;
            case UpgradeType.RechargeDown:
                runRechargeMultiplier = Mathf.Max(MinRechargeMultiplier, runRechargeMultiplier * 0.8f);
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

    public static string GetDescription(UpgradeType type) => GetDescriptionBase(type);

    static string GetDescriptionBase(UpgradeType type) => type switch
    {
        UpgradeType.DamageUp => "All spells deal 15% more damage",
        UpgradeType.CooldownDown => "All spell cooldowns reduced by 15%",
        UpgradeType.DurationUp => "All spell effects last 20% longer",
        UpgradeType.RadiusUp => "All spell areas increased by 20%",
        UpgradeType.ChargesUp => "Each element gets +1 charge before overheat",
        UpgradeType.RechargeDown => "Overheated elements recharge 20% faster",
        _ => ""
    };

    /// <summary>Get description with current stats when a caster is available.</summary>
    public static string GetDetailedDescription(UpgradeType type, SpellCaster caster)
    {
        if (caster == null) return GetDescriptionBase(type);
        return type switch
        {
            UpgradeType.DamageUp => $"All spells deal 15% more damage (current: {caster.damageBonusMult:F2}x)",
            UpgradeType.CooldownDown => $"Cooldowns -15% (current: {caster.cooldownBonusMult:F2}x)",
            UpgradeType.DurationUp => $"Duration +20% (current: {caster.durationBonusMult:F2}x)",
            UpgradeType.RadiusUp => $"Radius +20% (current: {caster.radiusBonusMult:F2}x)",
            UpgradeType.ChargesUp => $"+1 charge per element (current: +{runExtraCharges}/{MaxExtraCharges})",
            UpgradeType.RechargeDown => $"Recharge -20% (current: {runRechargeMultiplier:F2}x, min {MinRechargeMultiplier:F1}x)",
            _ => ""
        };
    }

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
