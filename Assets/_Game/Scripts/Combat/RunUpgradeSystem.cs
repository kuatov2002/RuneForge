using UnityEngine;

/// <summary>
/// Post-room upgrade system (Dead Cells style).
/// Player chooses one of three random upgrades: stat boosts or behavior modifiers.
/// </summary>
public enum UpgradeType
{
    // Stat upgrades (stackable)
    DamageUp,       // +15% damage
    CooldownDown,   // -15% cooldown
    DurationUp,     // +20% effect duration
    RadiusUp,       // +20% radius
    ChargesUp,      // +1 charge per element
    RechargeDown,   // -20% overheat recharge time

    // Behavior upgrades (one-time picks)
    AfterburnCast,  // Spells leave burning zone at impact
    DashStrike,     // Dash through enemies deals damage
    EchoCharge,     // Every 4th cast is free
    SiphonShield,   // Kills grant 1s immunity
    SpellRush,      // +25% move speed for 2s after casting
    Ricochet,       // Spell impacts bounce to 1 nearby enemy
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
        UpgradeType.AfterburnCast,
        UpgradeType.DashStrike,
        UpgradeType.EchoCharge,
        UpgradeType.SiphonShield,
        UpgradeType.SpellRush,
        UpgradeType.Ricochet,
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

    // Behavior upgrade flags (each can only be picked once per run)
    static bool hasAfterburn, hasDashStrike, hasEchoCharge, hasSiphon, hasSpellRush, hasRicochet;
    static int echoCastCounter;

    public static bool HasAfterburn => hasAfterburn;
    public static bool HasDashStrike => hasDashStrike;
    public static bool HasEchoCharge => hasEchoCharge;
    public static bool HasSiphon => hasSiphon;
    public static bool HasSpellRush => hasSpellRush;
    public static bool HasRicochet => hasRicochet;

    /// <summary>Track casts for Echo Charge. Returns true if this cast should be free.</summary>
    public static bool ShouldSkipChargeCost()
    {
        if (!hasEchoCharge) return false;
        echoCastCounter++;
        return echoCastCounter % 4 == 0;
    }

    public static void ResetRunUpgrades()
    {
        runExtraCharges = 0;
        runRechargeMultiplier = 1f;
        damageCapped = false;
        cooldownCapped = false;
        durationCapped = false;
        radiusCapped = false;

        hasAfterburn = false;
        hasDashStrike = false;
        hasEchoCharge = false;
        hasSiphon = false;
        hasSpellRush = false;
        hasRicochet = false;
        echoCastCounter = 0;
    }

    /// <summary>Generate random upgrade choices (no duplicates, excludes maxed/picked upgrades).</summary>
    public static UpgradeType[] GenerateChoices(int count = 3)
    {
        // Build available pool, excluding upgrades that hit their cap or are already picked
        var pool = new System.Collections.Generic.List<UpgradeType>();
        foreach (var u in AllUpgrades)
        {
            if (u == UpgradeType.ChargesUp && IsChargesMaxed) continue;
            if (u == UpgradeType.RechargeDown && IsRechargeMaxed) continue;
            if (u == UpgradeType.DamageUp && damageCapped) continue;
            if (u == UpgradeType.CooldownDown && cooldownCapped) continue;
            if (u == UpgradeType.DurationUp && durationCapped) continue;
            if (u == UpgradeType.RadiusUp && radiusCapped) continue;
            // Behavior upgrades: exclude if already picked
            if (u == UpgradeType.AfterburnCast && hasAfterburn) continue;
            if (u == UpgradeType.DashStrike && hasDashStrike) continue;
            if (u == UpgradeType.EchoCharge && hasEchoCharge) continue;
            if (u == UpgradeType.SiphonShield && hasSiphon) continue;
            if (u == UpgradeType.SpellRush && hasSpellRush) continue;
            if (u == UpgradeType.Ricochet && hasRicochet) continue;
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
            // Behavior upgrades
            case UpgradeType.AfterburnCast: hasAfterburn = true; break;
            case UpgradeType.DashStrike: hasDashStrike = true; break;
            case UpgradeType.EchoCharge: hasEchoCharge = true; echoCastCounter = 0; break;
            case UpgradeType.SiphonShield: hasSiphon = true; break;
            case UpgradeType.SpellRush: hasSpellRush = true; break;
            case UpgradeType.Ricochet: hasRicochet = true; break;
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
        UpgradeType.AfterburnCast => "AFTERBURN",
        UpgradeType.DashStrike => "DASH STRIKE",
        UpgradeType.EchoCharge => "ECHO CHARGE",
        UpgradeType.SiphonShield => "SIPHON",
        UpgradeType.SpellRush => "SPELL RUSH",
        UpgradeType.Ricochet => "RICOCHET",
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
        UpgradeType.AfterburnCast => "Spells leave a burning zone for 2s at impact point",
        UpgradeType.DashStrike => "Dashing through enemies deals 50% of last spell's damage",
        UpgradeType.EchoCharge => "Every 4th spell cast costs no charges",
        UpgradeType.SiphonShield => "Spell kills grant 1s of damage immunity",
        UpgradeType.SpellRush => "+25% movement speed for 2s after casting a spell",
        UpgradeType.Ricochet => "Spell impacts bounce to 1 nearby enemy for 50% damage",
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
            _ => GetDescriptionBase(type)
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
        // Behavior upgrades: distinct warm/cool tones
        UpgradeType.AfterburnCast => new Color(1f, 0.55f, 0.1f),
        UpgradeType.DashStrike => new Color(0.2f, 0.85f, 0.9f),
        UpgradeType.EchoCharge => new Color(0.7f, 0.4f, 1f),
        UpgradeType.SiphonShield => new Color(0.9f, 0.2f, 0.4f),
        UpgradeType.SpellRush => new Color(0.3f, 1f, 0.6f),
        UpgradeType.Ricochet => new Color(1f, 0.8f, 0.3f),
        _ => Color.white
    };

    // ── Afterburn helper ──

    /// <summary>Spawn a burning DoT zone at the target position (2 dmg/s, 1.5m, 2s).</summary>
    public static void SpawnAfterburnZone(Vector3 pos)
    {
        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(zone.GetComponent<Collider>());
        zone.transform.position = pos + Vector3.up * 0.05f;
        zone.transform.localScale = new Vector3(3f, 0.05f, 3f); // 1.5m radius
        zone.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.4f, 0.1f), 3f);
        zone.AddComponent<AfterburnZone>().Init(2f, 1.5f, 2f);
    }

    // ── Ricochet helper ──

    /// <summary>Find nearest enemy within range and deal bounce damage.</summary>
    public static void BounceToNearestEnemy(Vector3 hitPos, float damage, float range, GameObject exclude)
    {
        Collider nearest = null;
        float nearestDist = range * range;

        var hits = Physics.OverlapSphere(hitPos, range);
        foreach (var h in hits)
        {
            if (h.gameObject == exclude) continue;
            if (h.GetComponent<PlayerController>() != null) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null || hp.IsDead) continue;

            float dist = (h.transform.position - hitPos).sqrMagnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = h;
            }
        }

        if (nearest != null)
        {
            var targetHP = nearest.GetComponent<Health>();
            targetHP.TakeDamage(Mathf.RoundToInt(damage));

            // Visual: small line flash from hit to bounce target
            GameFeel.SpawnHitParticles(nearest.transform.position, new Color(1f, 0.8f, 0.3f));
        }
    }
}
