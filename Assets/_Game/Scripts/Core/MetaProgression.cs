using UnityEngine;

public static class MetaProgression
{
    const string KEY_CURRENCY = "meta_currency";
    const string KEY_MAX_HP_BONUS = "meta_maxhp_bonus";
    const string KEY_REROLLS = "meta_rerolls";
    const string KEY_RUNS_COMPLETED = "meta_runs_completed";

    public static int Currency
    {
        get => PlayerPrefs.GetInt(KEY_CURRENCY, 0);
        set { PlayerPrefs.SetInt(KEY_CURRENCY, value); PlayerPrefs.Save(); }
    }

    public static int MaxHPBonus
    {
        get => PlayerPrefs.GetInt(KEY_MAX_HP_BONUS, 0);
        set { PlayerPrefs.SetInt(KEY_MAX_HP_BONUS, value); PlayerPrefs.Save(); }
    }

    public static int Rerolls
    {
        get => PlayerPrefs.GetInt(KEY_REROLLS, 0);
        set { PlayerPrefs.SetInt(KEY_REROLLS, value); PlayerPrefs.Save(); }
    }

    public static int RunsCompleted
    {
        get => PlayerPrefs.GetInt(KEY_RUNS_COMPLETED, 0);
        set { PlayerPrefs.SetInt(KEY_RUNS_COMPLETED, value); PlayerPrefs.Save(); }
    }

    // Boss currency drops by floor
    public static int GetBossCurrencyDrop(int floor)
    {
        return floor switch
        {
            1 => 50,
            2 => 80,
            3 => 120,
            4 => 170,
            5 => 250,
            _ => 50
        };
    }

    // Upgrade costs
    public static int MaxHPUpgradeCost => 100 + MaxHPBonus * 50;
    public static int RerollUpgradeCost => 75;

    public static bool TryBuyMaxHP()
    {
        int cost = MaxHPUpgradeCost;
        if (Currency < cost) return false;
        Currency -= cost;
        MaxHPBonus++;
        return true;
    }

    public static bool TryBuyReroll()
    {
        int cost = RerollUpgradeCost;
        if (Currency < cost) return false;
        Currency -= cost;
        Rerolls++;
        return true;
    }

    public static void AwardBossCurrency(int floor)
    {
        Currency += GetBossCurrencyDrop(floor);
    }

    public static void CompleteRun()
    {
        RunsCompleted++;
        Currency += 100; // Bonus for completing a run
    }
}
