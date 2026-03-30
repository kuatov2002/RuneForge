using UnityEngine;

public static class MetaProgression
{
    // ─── KEYS ───────────────────────────────────────────────────
    const string KEY_CURRENCY = "meta_currency";
    const string KEY_RUNS_COMPLETED = "meta_runs_completed";
    const string KEY_BEST_FLOOR = "meta_best_floor";
    const string KEY_TOTAL_KILLS = "meta_total_kills";
    const string KEY_TOTAL_ESSENCE_SPENT = "meta_total_essence_spent";

    // Path A upgrade keys (original)
    const string KEY_MAX_HP_BONUS = "meta_maxhp_bonus";
    const string KEY_BASE_DAMAGE = "meta_base_damage";
    const string KEY_DASH_CHARGES = "meta_dash_charges";
    const string KEY_SPEED_BONUS = "meta_speed_bonus";
    const string KEY_STARTING_GOLD = "meta_starting_gold";
    const string KEY_POTION_SLOT = "meta_potion_slot";
    const string KEY_CRIT_CHANCE = "meta_crit_chance";
    const string KEY_REROLLS = "meta_rerolls";
    const string KEY_STARTING_RELIC = "meta_starting_relic";
    const string KEY_ELEMENT_UNLOCK = "meta_elem_unlock_";

    // Path B upgrade keys
    const string KEY_SECOND_WIND = "meta_second_wind";
    const string KEY_SPELL_MASTERY = "meta_spell_mastery";
    const string KEY_PHASE_STEP = "meta_phase_step";
    const string KEY_BLINK_STRIKE = "meta_blink_strike";
    const string KEY_HAGGLER = "meta_haggler";
    const string KEY_ELEM_MASTERY = "meta_elem_mastery";
    const string KEY_BLOOD_MAGE = "meta_blood_mage";
    const string KEY_LUCKY_FIND = "meta_lucky_find";
    const string KEY_CURSED_HEIRLOOM = "meta_cursed_heirloom";

    // Path choice key prefix: stores "A" or "B"
    const string KEY_UPGRADE_PATH = "meta_upgrade_path_";

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

    public static int TotalKills
    {
        get => PlayerPrefs.GetInt(KEY_TOTAL_KILLS, 0);
        set { PlayerPrefs.SetInt(KEY_TOTAL_KILLS, value); PlayerPrefs.Save(); }
    }

    public static int TotalEssenceSpent
    {
        get => PlayerPrefs.GetInt(KEY_TOTAL_ESSENCE_SPENT, 0);
        set { PlayerPrefs.SetInt(KEY_TOTAL_ESSENCE_SPENT, value); PlayerPrefs.Save(); }
    }

    // ─── PATH A UPGRADE LEVELS ────────────────────────────────

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

    // ─── PATH B UPGRADE LEVELS ────────────────────────────────

    public static int SecondWindLevel
    {
        get => PlayerPrefs.GetInt(KEY_SECOND_WIND, 0);
        set { PlayerPrefs.SetInt(KEY_SECOND_WIND, value); PlayerPrefs.Save(); }
    }

    public static int SpellMasteryLevel
    {
        get => PlayerPrefs.GetInt(KEY_SPELL_MASTERY, 0);
        set { PlayerPrefs.SetInt(KEY_SPELL_MASTERY, value); PlayerPrefs.Save(); }
    }

    public static int PhaseStepLevel
    {
        get => PlayerPrefs.GetInt(KEY_PHASE_STEP, 0);
        set { PlayerPrefs.SetInt(KEY_PHASE_STEP, value); PlayerPrefs.Save(); }
    }

    public static int BlinkStrikeLevel
    {
        get => PlayerPrefs.GetInt(KEY_BLINK_STRIKE, 0);
        set { PlayerPrefs.SetInt(KEY_BLINK_STRIKE, value); PlayerPrefs.Save(); }
    }

    public static int HagglerLevel
    {
        get => PlayerPrefs.GetInt(KEY_HAGGLER, 0);
        set { PlayerPrefs.SetInt(KEY_HAGGLER, value); PlayerPrefs.Save(); }
    }

    public static int ElemMasteryLevel
    {
        get => PlayerPrefs.GetInt(KEY_ELEM_MASTERY, 0);
        set { PlayerPrefs.SetInt(KEY_ELEM_MASTERY, value); PlayerPrefs.Save(); }
    }

    public static int BloodMageLevel
    {
        get => PlayerPrefs.GetInt(KEY_BLOOD_MAGE, 0);
        set { PlayerPrefs.SetInt(KEY_BLOOD_MAGE, value); PlayerPrefs.Save(); }
    }

    public static int LuckyFindLevel
    {
        get => PlayerPrefs.GetInt(KEY_LUCKY_FIND, 0);
        set { PlayerPrefs.SetInt(KEY_LUCKY_FIND, value); PlayerPrefs.Save(); }
    }

    public static int CursedHeirloomLevel
    {
        get => PlayerPrefs.GetInt(KEY_CURSED_HEIRLOOM, 0);
        set { PlayerPrefs.SetInt(KEY_CURSED_HEIRLOOM, value); PlayerPrefs.Save(); }
    }

    // ─── COMPUTED VALUES (Path A) ─────────────────────────────

    public static float DamageMultiplier => 1f + BaseDamageLevel * 0.05f;
    public static float SpeedMultiplier => 1f + SpeedBonusLevel * 0.08f;
    public static int ExtraDashCharges => DashChargesLevel;
    public static int StartingGold => StartingGoldLevel * 25;
    public static int PotionsPerFloor => PotionSlotLevel;
    public static float CritChance => CritChanceLevel * 0.03f;
    public static bool HasStartingRelic => StartingRelicLevel > 0;

    // ─── COMPUTED VALUES (Path B) ─────────────────────────────

    /// <summary>Number of floors where player survives a lethal hit (1 HP instead of death).</summary>
    public static int SecondWindCharges => SecondWindLevel;

    /// <summary>Bonus damage multiplier for same-element combos (e.g. Fire+Fire).</summary>
    public static float SpellMasteryBonus => 1f + SpellMasteryLevel * 0.08f;

    /// <summary>Extra invulnerability duration on dash (seconds).</summary>
    public static float PhaseStepDuration => PhaseStepLevel * 0.3f;

    /// <summary>Damage dealt when dashing through enemies.</summary>
    public static int BlinkStrikeDamage => BlinkStrikeLevel > 0 ? 1 + BlinkStrikeLevel * 2 : 0;

    /// <summary>Shop price discount multiplier (e.g. 0.68 = 32% off).</summary>
    public static float HagglerDiscount => 1f - HagglerLevel * 0.08f;

    /// <summary>Bonus multiplier for elemental reactions.</summary>
    public static float ReactionDamageBonus => 1f + ElemMasteryLevel * 0.10f;

    /// <summary>Chance to heal 1 HP on enemy kill.</summary>
    public static float BloodMageChance => BloodMageLevel * 0.03f;

    /// <summary>Chance for a bonus relic drop in rooms.</summary>
    public static float LuckyFindChance => LuckyFindLevel * 0.05f;

    /// <summary>Start run with a cursed relic + bonus gold.</summary>
    public static bool HasCursedHeirloom => CursedHeirloomLevel > 0;
    public static int CursedHeirloomGold => HasCursedHeirloom ? 50 : 0;

    // ─── PATH CHOICE SYSTEM ──────────────────────────────────

    /// <summary>Get active path for upgrade slot: "A" or "B". Defaults to "A".</summary>
    public static string GetChosenPath(string upgradeId)
    {
        return PlayerPrefs.GetString(KEY_UPGRADE_PATH + upgradeId, "A");
    }

    /// <summary>Set the active path for an upgrade slot.</summary>
    public static void ChoosePath(string upgradeId, string path)
    {
        PlayerPrefs.SetString(KEY_UPGRADE_PATH + upgradeId, path);
        PlayerPrefs.Save();
    }

    /// <summary>Toggle active path A↔B for free. Each path keeps its own independent levels.</summary>
    public static void SwitchPath(UpgradeSlot slot)
    {
        string current = GetChosenPath(slot.pathA.id);
        ChoosePath(slot.pathA.id, current == "A" ? "B" : "A");
    }

    // Element unlocks
    public static bool IsElementUnlocked(string elemName)
    {
        if (elemName == "Fire" || elemName == "Water" || elemName == "Earth" || elemName == "Air")
            return true;
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

    /// <summary>Paired upgrade slot: Path A and Path B are mutually exclusive.</summary>
    public struct UpgradeSlot
    {
        public UpgradeDef pathA;
        public UpgradeDef pathB;
    }

    public static UpgradeSlot[] AllUpgradeSlots => new UpgradeSlot[]
    {
        // Slot 1: Vitality vs Second Wind
        new() {
            pathA = new() { id = "maxhp", name = "Resilience", description = "+1 Max HP",
                maxLevel = 5, color = new Color(0.9f, 0.2f, 0.2f),
                getLevel = () => MaxHPBonus, setLevel = v => MaxHPBonus = v,
                getCost = lvl => 80 + lvl * 40 },
            pathB = new() { id = "maxhp", name = "Second Wind", description = "Survive lethal hit once per floor",
                maxLevel = 3, color = new Color(0.9f, 0.4f, 0.4f),
                getLevel = () => SecondWindLevel, setLevel = v => SecondWindLevel = v,
                getCost = lvl => 100 + lvl * 50 },
        },
        // Slot 2: Arcane Power vs Spell Mastery
        new() {
            pathA = new() { id = "damage", name = "Arcane Power", description = "+5% Spell Damage",
                maxLevel = 8, color = new Color(0.9f, 0.5f, 0.1f),
                getLevel = () => BaseDamageLevel, setLevel = v => BaseDamageLevel = v,
                getCost = lvl => 60 + lvl * 30 },
            pathB = new() { id = "damage", name = "Spell Mastery", description = "+8% same-element combo damage",
                maxLevel = 6, color = new Color(1f, 0.7f, 0.2f),
                getLevel = () => SpellMasteryLevel, setLevel = v => SpellMasteryLevel = v,
                getCost = lvl => 70 + lvl * 35 },
        },
        // Slot 3: Swift Feet vs Phase Step
        new() {
            pathA = new() { id = "speed", name = "Swift Feet", description = "+8% Move Speed",
                maxLevel = 5, color = new Color(0.3f, 0.8f, 1f),
                getLevel = () => SpeedBonusLevel, setLevel = v => SpeedBonusLevel = v,
                getCost = lvl => 50 + lvl * 25 },
            pathB = new() { id = "speed", name = "Phase Step", description = "+0.3s dash invulnerability",
                maxLevel = 3, color = new Color(0.4f, 0.6f, 1f),
                getLevel = () => PhaseStepLevel, setLevel = v => PhaseStepLevel = v,
                getCost = lvl => 80 + lvl * 40 },
        },
        // Slot 4: Shadow Step vs Blink Strike
        new() {
            pathA = new() { id = "dash", name = "Shadow Step", description = "+1 Dash Charge",
                maxLevel = 2, color = new Color(0.5f, 0.3f, 0.8f),
                getLevel = () => DashChargesLevel, setLevel = v => DashChargesLevel = v,
                getCost = lvl => 150 + lvl * 100 },
            pathB = new() { id = "dash", name = "Blink Strike", description = "Dash through enemies deals damage",
                maxLevel = 2, color = new Color(0.7f, 0.3f, 0.9f),
                getLevel = () => BlinkStrikeLevel, setLevel = v => BlinkStrikeLevel = v,
                getCost = lvl => 120 + lvl * 80 },
        },
        // Slot 5: Merchant's Favor vs Haggler
        new() {
            pathA = new() { id = "gold", name = "Merchant's Favor", description = "+25 Starting Gold",
                maxLevel = 4, color = new Color(1f, 0.85f, 0.2f),
                getLevel = () => StartingGoldLevel, setLevel = v => StartingGoldLevel = v,
                getCost = lvl => 40 + lvl * 20 },
            pathB = new() { id = "gold", name = "Haggler", description = "Shop prices -8%",
                maxLevel = 4, color = new Color(0.9f, 0.75f, 0.3f),
                getLevel = () => HagglerLevel, setLevel = v => HagglerLevel = v,
                getCost = lvl => 50 + lvl * 25 },
        },
        // Slot 6: Precision vs Elemental Mastery
        new() {
            pathA = new() { id = "crit", name = "Precision", description = "+3% Critical Hit Chance",
                maxLevel = 5, color = new Color(1f, 0.3f, 0.5f),
                getLevel = () => CritChanceLevel, setLevel = v => CritChanceLevel = v,
                getCost = lvl => 70 + lvl * 35 },
            pathB = new() { id = "crit", name = "Elemental Mastery", description = "+10% reaction damage",
                maxLevel = 5, color = new Color(1f, 0.5f, 0.7f),
                getLevel = () => ElemMasteryLevel, setLevel = v => ElemMasteryLevel = v,
                getCost = lvl => 60 + lvl * 30 },
        },
        // Slot 7: Alchemist's Gift vs Blood Mage
        new() {
            pathA = new() { id = "potion", name = "Alchemist's Gift", description = "+1 Healing Potion per Floor",
                maxLevel = 3, color = new Color(0.3f, 0.9f, 0.4f),
                getLevel = () => PotionSlotLevel, setLevel = v => PotionSlotLevel = v,
                getCost = lvl => 100 + lvl * 60 },
            pathB = new() { id = "potion", name = "Blood Mage", description = "3% kill chance to heal 1 HP",
                maxLevel = 3, color = new Color(0.5f, 0.9f, 0.3f),
                getLevel = () => BloodMageLevel, setLevel = v => BloodMageLevel = v,
                getCost = lvl => 80 + lvl * 50 },
        },
        // Slot 8: Fate's Hand vs Lucky Find
        new() {
            pathA = new() { id = "reroll", name = "Fate's Hand", description = "+1 Rune Reroll",
                maxLevel = 5, color = new Color(0.6f, 0.4f, 0.9f),
                getLevel = () => Rerolls, setLevel = v => Rerolls = v,
                getCost = lvl => 50 + lvl * 15 },
            pathB = new() { id = "reroll", name = "Lucky Find", description = "+5% bonus relic chance in rooms",
                maxLevel = 4, color = new Color(0.7f, 0.5f, 1f),
                getLevel = () => LuckyFindLevel, setLevel = v => LuckyFindLevel = v,
                getCost = lvl => 60 + lvl * 25 },
        },
        // Slot 9: Heirloom vs Cursed Heirloom
        new() {
            pathA = new() { id = "relic", name = "Heirloom", description = "Start with a Random Relic",
                maxLevel = 1, color = new Color(0.8f, 0.6f, 0.2f),
                getLevel = () => StartingRelicLevel, setLevel = v => StartingRelicLevel = v,
                getCost = _ => 300 },
            pathB = new() { id = "relic", name = "Cursed Heirloom", description = "Start with Cursed Relic + 50 gold",
                maxLevel = 1, color = new Color(0.6f, 0.3f, 0.5f),
                getLevel = () => CursedHeirloomLevel, setLevel = v => CursedHeirloomLevel = v,
                getCost = _ => 200 },
        },
    };

    /// <summary>Flat list of all Path A upgrades for backward compatibility.</summary>
    public static UpgradeDef[] AllUpgrades
    {
        get
        {
            var slots = AllUpgradeSlots;
            var result = new UpgradeDef[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                string path = GetChosenPath(slots[i].pathA.id);
                result[i] = path == "B" ? slots[i].pathB : slots[i].pathA;
            }
            return result;
        }
    }

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
        TotalEssenceSpent += cost;
        def.setLevel(level + 1);
        return true;
    }

    public static bool TryUnlockElement(string elemName)
    {
        if (IsElementUnlocked(elemName)) return false;
        int cost = GetElementUnlockCost(elemName);
        if (Currency < cost) return false;
        Currency -= cost;
        TotalEssenceSpent += cost;
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

    public static int AwardDeathCurrency(int floor, int room, int enemiesKilled)
    {
        int reward = floor * 15 + room * 3 + enemiesKilled / 2;
        reward = Mathf.Max(reward, 15);
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
        TotalEssenceSpent += def.Value.unlockCost;
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

    static readonly string[] AllKeys = {
        KEY_CURRENCY, KEY_RUNS_COMPLETED, KEY_BEST_FLOOR,
        KEY_TOTAL_KILLS, KEY_TOTAL_ESSENCE_SPENT,
        KEY_MAX_HP_BONUS, KEY_BASE_DAMAGE, KEY_DASH_CHARGES,
        KEY_SPEED_BONUS, KEY_STARTING_GOLD, KEY_POTION_SLOT,
        KEY_CRIT_CHANCE, KEY_REROLLS, KEY_STARTING_RELIC,
        KEY_SELECTED_LOADOUT,
        // Path B keys
        KEY_SECOND_WIND, KEY_SPELL_MASTERY, KEY_PHASE_STEP,
        KEY_BLINK_STRIKE, KEY_HAGGLER, KEY_ELEM_MASTERY,
        KEY_BLOOD_MAGE, KEY_LUCKY_FIND, KEY_CURSED_HEIRLOOM,
    };

    // Upgrade IDs for path choice cleanup
    static readonly string[] UpgradeIds = {
        "maxhp", "damage", "speed", "dash", "gold", "crit", "potion", "reroll", "relic"
    };

    public static void ResetAll()
    {
        foreach (var key in AllKeys)
            PlayerPrefs.DeleteKey(key);

        // Delete path choices
        foreach (var id in UpgradeIds)
            PlayerPrefs.DeleteKey(KEY_UPGRADE_PATH + id);

        // Delete element unlocks
        foreach (var name in new[] { "Lightning", "Poison", "Void" })
            PlayerPrefs.DeleteKey(KEY_ELEMENT_UNLOCK + name);

        // Delete loadout unlocks
        foreach (var loadout in AllLoadouts)
            PlayerPrefs.DeleteKey(KEY_LOADOUT_UNLOCK + loadout.id);

        // Delete combo discoveries
        foreach (var id in AllComboIds)
            PlayerPrefs.DeleteKey(KEY_COMBO_DISCOVERED + id);

        PlayerPrefs.Save();
    }
}
