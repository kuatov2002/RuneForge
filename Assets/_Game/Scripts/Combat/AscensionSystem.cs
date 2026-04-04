using UnityEngine;

/// <summary>
/// Heat/Ascension system for post-victory replayability.
/// Each heat level adds modifiers that increase difficulty but also rewards.
/// </summary>
public static class AscensionSystem
{
    static SaveData D => SaveSystem.Data;

    public static int CurrentHeat
    {
        get => D.ascensionHeat;
        set { D.ascensionHeat = Mathf.Max(0, value); SaveSystem.Save(); }
    }

    public static int MaxHeatUnlocked
    {
        get => D.ascensionMaxHeat;
        set { D.ascensionMaxHeat = value; SaveSystem.Save(); }
    }

    /// <summary>Call after a successful run to unlock next heat level.</summary>
    public static void OnRunComplete()
    {
        if (CurrentHeat >= MaxHeatUnlocked)
            MaxHeatUnlocked = CurrentHeat + 1;
    }

    // ─── HEAT MODIFIERS ────────────────────────────────────────

    /// <summary>Enemy HP multiplier. Heat 1: +15% per level.</summary>
    public static float EnemyHPMultiplier => 1f + CurrentHeat * 0.15f;

    /// <summary>Enemy damage multiplier. Heat 2+: +10% per level.</summary>
    public static float EnemyDamageMultiplier => 1f + Mathf.Max(0, CurrentHeat - 1) * 0.1f;

    /// <summary>Enemy speed multiplier. Heat 4+: +8% per level.</summary>
    public static float EnemySpeedMultiplier => 1f + Mathf.Max(0, CurrentHeat - 3) * 0.08f;

    /// <summary>Extra elite chance. Heat 3+: +10% per level.</summary>
    public static float ExtraEliteChance => Mathf.Max(0, CurrentHeat - 2) * 0.10f;

    /// <summary>Shop price multiplier. Heat 5+: +20% per level.</summary>
    public static float ShopPriceMultiplier => 1f + Mathf.Max(0, CurrentHeat - 4) * 0.2f;

    /// <summary>Healing reduction. Heat 6+: heals reduced by 25% per level.</summary>
    public static float HealingMultiplier => Mathf.Max(0.25f, 1f - Mathf.Max(0, CurrentHeat - 5) * 0.25f);

    /// <summary>Meta-currency reward multiplier for running at heat.</summary>
    public static float CurrencyMultiplier => 1f + CurrentHeat * 0.1f;

    /// <summary>Get human-readable description of active modifiers.</summary>
    public static string GetHeatDescription()
    {
        if (CurrentHeat == 0) return "No modifiers active.";

        string desc = "";
        if (CurrentHeat >= 1) desc += $"Enemy HP +{CurrentHeat * 15}%\n";
        if (CurrentHeat >= 2) desc += $"Enemy Damage +{(CurrentHeat - 1) * 10}%\n";
        if (CurrentHeat >= 3) desc += $"More Elite enemies\n";
        if (CurrentHeat >= 4) desc += $"Enemy Speed +{(CurrentHeat - 3) * 8}%\n";
        if (CurrentHeat >= 5) desc += $"Shop prices increased\n";
        if (CurrentHeat >= 6) desc += $"Healing reduced\n";
        desc += $"\nReward bonus: +{CurrentHeat * 10}%";
        return desc.Trim();
    }
}
