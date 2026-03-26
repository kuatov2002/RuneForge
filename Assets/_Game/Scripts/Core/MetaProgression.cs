using UnityEngine;

public static class MetaProgression
{
    // ─── KEYS ───────────────────────────────────────────────────
    const string KEY_CURRENCY = "meta_currency";
    const string KEY_RUNS_COMPLETED = "meta_runs_completed";
    const string KEY_BEST_FLOOR = "meta_best_floor";

    // Upgrade keys
    const string KEY_MAX_HP_BONUS = "meta_maxhp_bonus";          // +1 max HP each
    const string KEY_BASE_DAMAGE = "meta_base_damage";            // +5% damage each
    const string KEY_DASH_CHARGES = "meta_dash_charges";          // extra dash charges
    const string KEY_SPEED_BONUS = "meta_speed_bonus";            // +8% speed each
    const string KEY_STARTING_GOLD = "meta_starting_gold";        // gold at run start
    const string KEY_POTION_SLOT = "meta_potion_slot";            // heal potion per floor
    const string KEY_CRIT_CHANCE = "meta_crit_chance";            // +3% crit chance each
    const string KEY_REROLLS = "meta_rerolls";                    // rune rerolls
    const string KEY_STARTING_RELIC = "meta_starting_relic";      // start with random relic
    const string KEY_ELEMENT_UNLOCK = "meta_elem_unlock_";        // per-element unlock prefix

    // ─── CURRENCY ───────────────────────────────────────────────

    public static int Currency
    {
        get => PlayerPrefs.GetInt(KEY_CURRENCY, 0);
        set { PlayerPrefs.SetInt(KEY_CURRENCY, value); PlayerPrefs.Save(); }
    }

    public static int RunsCompleted
    {
        get => PlayerPrefs.GetInt(KEY_RUNS_COMPLETED, 0);
        set { PlayerPrefs.SetInt(KEY_RUNS_COMPLETED, value); PlayerPrefs.Save(); }
    }

    public static int BestFloor
    {
        get => PlayerPrefs.GetInt(KEY_BEST_FLOOR, 0);
        set
        {
            if (value > PlayerPrefs.GetInt(KEY_BEST_FLOOR, 0))
            { PlayerPrefs.SetInt(KEY_BEST_FLOOR, value); PlayerPrefs.Save(); }
        }
    }

    // ─── UPGRADE LEVELS ─────────────────────────────────────────

    public static int MaxHPBonus
    {
        get => PlayerPrefs.GetInt(KEY_MAX_HP_BONUS, 0);
        set { PlayerPrefs.SetInt(KEY_MAX_HP_BONUS, value); PlayerPrefs.Save(); }
    }

    public static int BaseDamageLevel
    {
        get => PlayerPrefs.GetInt(KEY_BASE_DAMAGE, 0);
        set { PlayerPrefs.SetInt(KEY_BASE_DAMAGE, value); PlayerPrefs.Save(); }
    }

    public static int DashChargesLevel
    {
        get => PlayerPrefs.GetInt(KEY_DASH_CHARGES, 0);
        set { PlayerPrefs.SetInt(KEY_DASH_CHARGES, value); PlayerPrefs.Save(); }
    }

    public static int SpeedBonusLevel
    {
        get => PlayerPrefs.GetInt(KEY_SPEED_BONUS, 0);
        set { PlayerPrefs.SetInt(KEY_SPEED_BONUS, value); PlayerPrefs.Save(); }
    }

    public static int StartingGoldLevel
    {
        get => PlayerPrefs.GetInt(KEY_STARTING_GOLD, 0);
        set { PlayerPrefs.SetInt(KEY_STARTING_GOLD, value); PlayerPrefs.Save(); }
    }

    public static int PotionSlotLevel
    {
        get => PlayerPrefs.GetInt(KEY_POTION_SLOT, 0);
        set { PlayerPrefs.SetInt(KEY_POTION_SLOT, value); PlayerPrefs.Save(); }
    }

    public static int CritChanceLevel
    {
        get => PlayerPrefs.GetInt(KEY_CRIT_CHANCE, 0);
        set { PlayerPrefs.SetInt(KEY_CRIT_CHANCE, value); PlayerPrefs.Save(); }
    }

    public static int Rerolls
    {
        get => PlayerPrefs.GetInt(KEY_REROLLS, 0);
        set { PlayerPrefs.SetInt(KEY_REROLLS, value); PlayerPrefs.Save(); }
    }

    public static int StartingRelicLevel
    {
        get => PlayerPrefs.GetInt(KEY_STARTING_RELIC, 0);
        set { PlayerPrefs.SetInt(KEY_STARTING_RELIC, value); PlayerPrefs.Save(); }
    }

    // ─── COMPUTED VALUES ────────────────────────────────────────

    public static float DamageMultiplier => 1f + BaseDamageLevel * 0.05f;
    public static float SpeedMultiplier => 1f + SpeedBonusLevel * 0.08f;
    public static int ExtraDashCharges => DashChargesLevel;
    public static int StartingGold => StartingGoldLevel * 25;
    public static int PotionsPerFloor => PotionSlotLevel;
    public static float CritChance => CritChanceLevel * 0.03f;
    public static bool HasStartingRelic => StartingRelicLevel > 0;

    // Element unlocks (player starts with Fire+Water+Earth+Air, can unlock rest)
    public static bool IsElementUnlocked(string elemName)
    {
        if (elemName == "Fire" || elemName == "Water" || elemName == "Earth" || elemName == "Air")
            return true; // base elements always available
        return PlayerPrefs.GetInt(KEY_ELEMENT_UNLOCK + elemName, 0) > 0;
    }

    public static void UnlockElement(string elemName)
    {
        PlayerPrefs.SetInt(KEY_ELEMENT_UNLOCK + elemName, 1);
        PlayerPrefs.Save();
    }

    // ─── UPGRADE DEFINITIONS ────────────────────────────────────

    public struct UpgradeDef
    {
        public string id;
        public string name;
        public string description;
        public int maxLevel;
        public Color color;
        public System.Func<int> getLevel;
        public System.Action<int> setLevel;
        public System.Func<int, int> getCost;
    }

    public static UpgradeDef[] AllUpgrades => new UpgradeDef[]
    {
        new() { id = "maxhp", name = "Vitality", description = "+1 Max HP",
            maxLevel = 5, color = new Color(0.9f, 0.2f, 0.2f),
            getLevel = () => MaxHPBonus, setLevel = v => MaxHPBonus = v,
            getCost = lvl => 80 + lvl * 40 },

        new() { id = "damage", name = "Arcane Power", description = "+5% Spell Damage",
            maxLevel = 8, color = new Color(0.9f, 0.5f, 0.1f),
            getLevel = () => BaseDamageLevel, setLevel = v => BaseDamageLevel = v,
            getCost = lvl => 60 + lvl * 30 },

        new() { id = "speed", name = "Swift Feet", description = "+8% Move Speed",
            maxLevel = 5, color = new Color(0.3f, 0.8f, 1f),
            getLevel = () => SpeedBonusLevel, setLevel = v => SpeedBonusLevel = v,
            getCost = lvl => 50 + lvl * 25 },

        new() { id = "dash", name = "Shadow Step", description = "+1 Dash Charge",
            maxLevel = 2, color = new Color(0.5f, 0.3f, 0.8f),
            getLevel = () => DashChargesLevel, setLevel = v => DashChargesLevel = v,
            getCost = lvl => 150 + lvl * 100 },

        new() { id = "gold", name = "Merchant's Favor", description = "+25 Starting Gold",
            maxLevel = 4, color = new Color(1f, 0.85f, 0.2f),
            getLevel = () => StartingGoldLevel, setLevel = v => StartingGoldLevel = v,
            getCost = lvl => 40 + lvl * 20 },

        new() { id = "crit", name = "Precision", description = "+3% Critical Hit Chance",
            maxLevel = 5, color = new Color(1f, 0.3f, 0.5f),
            getLevel = () => CritChanceLevel, setLevel = v => CritChanceLevel = v,
            getCost = lvl => 70 + lvl * 35 },

        new() { id = "potion", name = "Alchemist's Gift", description = "+1 Healing Potion per Floor",
            maxLevel = 3, color = new Color(0.3f, 0.9f, 0.4f),
            getLevel = () => PotionSlotLevel, setLevel = v => PotionSlotLevel = v,
            getCost = lvl => 100 + lvl * 60 },

        new() { id = "reroll", name = "Fate's Hand", description = "+1 Rune Reroll",
            maxLevel = 5, color = new Color(0.6f, 0.4f, 0.9f),
            getLevel = () => Rerolls, setLevel = v => Rerolls = v,
            getCost = lvl => 50 + lvl * 15 },

        new() { id = "relic", name = "Heirloom", description = "Start with a Random Relic",
            maxLevel = 1, color = new Color(0.8f, 0.6f, 0.2f),
            getLevel = () => StartingRelicLevel, setLevel = v => StartingRelicLevel = v,
            getCost = _ => 300 },
    };

    // Element unlock costs
    public static int GetElementUnlockCost(string elemName) => elemName switch
    {
        "Lightning" => 120,
        "Poison" => 150,
        "Void" => 200,
        _ => 100
    };

    public static bool TryBuyUpgrade(UpgradeDef def)
    {
        int level = def.getLevel();
        if (level >= def.maxLevel) return false;
        int cost = def.getCost(level);
        if (Currency < cost) return false;
        Currency -= cost;
        def.setLevel(level + 1);
        return true;
    }

    public static bool TryUnlockElement(string elemName)
    {
        if (IsElementUnlocked(elemName)) return false;
        int cost = GetElementUnlockCost(elemName);
        if (Currency < cost) return false;
        Currency -= cost;
        UnlockElement(elemName);
        return true;
    }

    // ─── CURRENCY REWARDS ───────────────────────────────────────

    public static int GetBossCurrencyDrop(int floor) => floor switch
    {
        1 => 50, 2 => 80, 3 => 120, 4 => 170, 5 => 250, _ => 50
    };

    public static void AwardBossCurrency(int floor)
    {
        Currency += GetBossCurrencyDrop(floor);
    }

    public static void CompleteRun()
    {
        RunsCompleted++;
        Currency += 100;
    }

    public static void RecordFloor(int floor)
    {
        BestFloor = floor;
    }

    /// <summary>Award meta-currency on death based on progress (rooms cleared, floor reached).</summary>
    public static int AwardDeathCurrency(int floor, int room, int enemiesKilled)
    {
        // Base: 10 per floor reached + 2 per room cleared + 1 per 3 enemies killed
        int reward = floor * 10 + room * 2 + enemiesKilled / 3;
        reward = Mathf.Max(reward, 5); // Minimum 5 even for early deaths
        reward = Mathf.CeilToInt(reward * AscensionSystem.CurrencyMultiplier);
        Currency += reward;
        return reward;
    }

    // ─── STARTING LOADOUTS (Aspects) ─────────────────────────

    const string KEY_LOADOUT_UNLOCK = "meta_loadout_";
    const string KEY_SELECTED_LOADOUT = "meta_selected_loadout";

    public struct LoadoutDef
    {
        public string id;
        public string name;
        public string description;
        public string startElement;
        public Color color;
        public int unlockCost;
        public string passiveDesc;
    }

    public static LoadoutDef[] AllLoadouts => new LoadoutDef[]
    {
        new() { id = "default", name = "Apprentice", description = "Fire + Water + Earth + Air. Classic start.",
            startElement = null, color = new Color(0.6f, 0.6f, 0.65f),
            unlockCost = 0, passiveDesc = "No bonus" },
        new() { id = "pyromancer", name = "Pyromancer", description = "Fire damage +15%. Master of flames.",
            startElement = "Fire", color = new Color(1f, 0.4f, 0.1f),
            unlockCost = 100, passiveDesc = "+15% Fire damage" },
        new() { id = "cryomancer", name = "Cryomancer", description = "Water effects last +25% longer. Freeze and slow specialist.",
            startElement = "Water", color = new Color(0.3f, 0.7f, 1f),
            unlockCost = 100, passiveDesc = "+25% Water effect duration" },
        new() { id = "geomancer", name = "Geomancer", description = "Earth spells +20% radius. Shaper of stone.",
            startElement = "Earth", color = new Color(0.6f, 0.4f, 0.2f),
            unlockCost = 100, passiveDesc = "+20% Earth spell radius" },
        new() { id = "windwalker", name = "Windwalker", description = "Air dash distance +25%. Swift as the gale.",
            startElement = "Air", color = new Color(0.7f, 0.9f, 1f),
            unlockCost = 100, passiveDesc = "+25% Air dash distance" },
        new() { id = "stormcaller", name = "Stormcaller", description = "Starts with Lightning unlocked (replaces Air). Storm wielder.",
            startElement = "Lightning", color = new Color(1f, 1f, 0.3f),
            unlockCost = 150, passiveDesc = "Lightning replaces Air" },
    };

    public static bool IsLoadoutUnlocked(string loadoutId)
    {
        if (loadoutId == "default") return true;
        return PlayerPrefs.GetInt(KEY_LOADOUT_UNLOCK + loadoutId, 0) > 0;
    }

    public static bool TryUnlockLoadout(string loadoutId)
    {
        if (IsLoadoutUnlocked(loadoutId)) return false;
        LoadoutDef? def = null;
        foreach (var l in AllLoadouts) if (l.id == loadoutId) { def = l; break; }
        if (def == null) return false;
        if (Currency < def.Value.unlockCost) return false;
        Currency -= def.Value.unlockCost;
        PlayerPrefs.SetInt(KEY_LOADOUT_UNLOCK + loadoutId, 1);
        PlayerPrefs.Save();
        return true;
    }

    public static string SelectedLoadout
    {
        get => PlayerPrefs.GetString(KEY_SELECTED_LOADOUT, "default");
        set { PlayerPrefs.SetString(KEY_SELECTED_LOADOUT, value); PlayerPrefs.Save(); }
    }

    public static LoadoutDef GetSelectedLoadoutDef()
    {
        string sel = SelectedLoadout;
        foreach (var l in AllLoadouts) if (l.id == sel) return l;
        return AllLoadouts[0];
    }

    // ─── COMBO DISCOVERY ───────────────────────────────────────

    const string KEY_COMBO_DISCOVERED = "meta_combo_";

    public static readonly string[] AllComboIds = new[]
    {
        "SteamBurst", "Detonate", "Implode", "Shatter",
        "ToxicFrost", "PlagueSpark", "RiftShock", "Corruption"
    };

    public static readonly (string id, string name, string elem1, string elem2, string desc)[] ComboDefinitions = new[]
    {
        ("SteamBurst", "Steam Burst", "Fire", "Water", "AoE damage cloud (3m, 8 dmg)"),
        ("Detonate", "Detonate", "Fire", "Poison", "Consumes poison stacks, 5 dmg each"),
        ("Implode", "Implode", "Fire", "Void", "Pulls enemies in + 10 dmg"),
        ("Shatter", "Shatter", "Water", "Lightning", "Frozen target takes 15 burst dmg"),
        ("ToxicFrost", "Toxic Frost", "Water", "Poison", "Slow + poison AoE pool (4s)"),
        ("PlagueSpark", "Plague Spark", "Lightning", "Poison", "Chain-detonates poisoned enemies"),
        ("RiftShock", "Rift Shock", "Lightning", "Void", "Stuns all void-marked enemies 2s"),
        ("Corruption", "Corruption", "Poison", "Void", "Spreads 3 poison stacks to nearby"),
    };

    public static bool IsComboDiscovered(string comboId)
    {
        return PlayerPrefs.GetInt(KEY_COMBO_DISCOVERED + comboId, 0) > 0;
    }

    public static void DiscoverCombo(string comboId)
    {
        if (!IsComboDiscovered(comboId))
        {
            PlayerPrefs.SetInt(KEY_COMBO_DISCOVERED + comboId, 1);
            PlayerPrefs.Save();
        }
    }

    public static int DiscoveredComboCount
    {
        get
        {
            int count = 0;
            foreach (var id in AllComboIds)
                if (IsComboDiscovered(id)) count++;
            return count;
        }
    }

    // ─── RESET (debug) ─────────────────────────────────────────

    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
