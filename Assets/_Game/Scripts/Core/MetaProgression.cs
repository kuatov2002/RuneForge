using UnityEngine;

public static class MetaProgression
{
    static SaveData D => SaveSystem.Data;

    // ─── CURRENCY ───────────────────────────────────────────────

    public static int Currency
    {
        get => D.currency;
        set { D.currency = value; SaveSystem.Save(); }
    }

    public static int RunsCompleted
    {
        get => D.runsCompleted;
        set { D.runsCompleted = value; SaveSystem.Save(); }
    }

    public static int BestFloor
    {
        get => D.bestFloor;
        set
        {
            if (value > D.bestFloor)
            { D.bestFloor = value; SaveSystem.Save(); }
        }
    }

    public static int TotalKills
    {
        get => D.totalKills;
        set { D.totalKills = value; SaveSystem.Save(); }
    }

    public static int TotalEssenceSpent
    {
        get => D.totalEssenceSpent;
        set { D.totalEssenceSpent = value; SaveSystem.Save(); }
    }

    // ─── PATH A UPGRADE LEVELS ────────────────────────────────

    public static int MaxHPBonus
    {
        get => D.maxHPBonus;
        set { D.maxHPBonus = value; SaveSystem.Save(); }
    }

    public static int BaseDamageLevel
    {
        get => D.baseDamage;
        set { D.baseDamage = value; SaveSystem.Save(); }
    }

    public static int DashChargesLevel
    {
        get => D.dashCharges;
        set { D.dashCharges = value; SaveSystem.Save(); }
    }

    public static int SpeedBonusLevel
    {
        get => D.speedBonus;
        set { D.speedBonus = value; SaveSystem.Save(); }
    }

    public static int StartingGoldLevel
    {
        get => D.startingGold;
        set { D.startingGold = value; SaveSystem.Save(); }
    }

    public static int PotionSlotLevel
    {
        get => D.potionSlot;
        set { D.potionSlot = value; SaveSystem.Save(); }
    }

    public static int CritChanceLevel
    {
        get => D.critChance;
        set { D.critChance = value; SaveSystem.Save(); }
    }

    public static int Rerolls
    {
        get => D.rerolls;
        set { D.rerolls = value; SaveSystem.Save(); }
    }

    public static int StartingRelicLevel
    {
        get => D.startingRelic;
        set { D.startingRelic = value; SaveSystem.Save(); }
    }

    // ─── PATH B UPGRADE LEVELS ────────────────────────────────

    public static int SecondWindLevel
    {
        get => D.secondWind;
        set { D.secondWind = value; SaveSystem.Save(); }
    }

    public static int SpellMasteryLevel
    {
        get => D.spellMastery;
        set { D.spellMastery = value; SaveSystem.Save(); }
    }

    public static int PhaseStepLevel
    {
        get => D.phaseStep;
        set { D.phaseStep = value; SaveSystem.Save(); }
    }

    public static int BlinkStrikeLevel
    {
        get => D.blinkStrike;
        set { D.blinkStrike = value; SaveSystem.Save(); }
    }

    public static int HagglerLevel
    {
        get => D.haggler;
        set { D.haggler = value; SaveSystem.Save(); }
    }

    public static int ElemMasteryLevel
    {
        get => D.elemMastery;
        set { D.elemMastery = value; SaveSystem.Save(); }
    }

    public static int BloodMageLevel
    {
        get => D.bloodMage;
        set { D.bloodMage = value; SaveSystem.Save(); }
    }

    public static int LuckyFindLevel
    {
        get => D.luckyFind;
        set { D.luckyFind = value; SaveSystem.Save(); }
    }

    public static int CursedHeirloomLevel
    {
        get => D.cursedHeirloom;
        set { D.cursedHeirloom = value; SaveSystem.Save(); }
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

    public static int SecondWindCharges => SecondWindLevel;
    public static float SpellMasteryBonus => 1f + SpellMasteryLevel * 0.08f;
    public static float PhaseStepDuration => PhaseStepLevel * 0.3f;
    public static int BlinkStrikeDamage => BlinkStrikeLevel > 0 ? 1 + BlinkStrikeLevel * 2 : 0;
    public static float HagglerDiscount => 1f - HagglerLevel * 0.08f;
    public static float ReactionDamageBonus => 1f + ElemMasteryLevel * 0.10f;
    public static float BloodMageChance => BloodMageLevel * 0.03f;
    public static float LuckyFindChance => LuckyFindLevel * 0.05f;
    public static bool HasCursedHeirloom => CursedHeirloomLevel > 0;
    public static int CursedHeirloomGold => HasCursedHeirloom ? 50 : 0;

    // ─── PATH CHOICE SYSTEM ──────────────────────────────────

    public static string GetChosenPath(string upgradeId) => D.GetPath(upgradeId);

    public static void ChoosePath(string upgradeId, string path)
    {
        D.SetPath(upgradeId, path);
        SaveSystem.Save();
    }

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
        return D.unlockedElements.Contains(elemName);
    }

    public static void UnlockElement(string elemName)
    {
        if (!D.unlockedElements.Contains(elemName))
        {
            D.unlockedElements.Add(elemName);
            SaveSystem.Save();
        }
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

    public struct UpgradeSlot
    {
        public UpgradeDef pathA;
        public UpgradeDef pathB;
    }

    public static UpgradeSlot[] AllUpgradeSlots => new UpgradeSlot[]
    {
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
        return D.unlockedLoadouts.Contains(loadoutId);
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
        D.unlockedLoadouts.Add(loadoutId);
        SaveSystem.Save();
        return true;
    }

    public static string SelectedLoadout
    {
        get => D.selectedLoadout;
        set { D.selectedLoadout = value; SaveSystem.Save(); }
    }

    public static LoadoutDef GetSelectedLoadoutDef()
    {
        string sel = SelectedLoadout;
        foreach (var l in AllLoadouts) if (l.id == sel) return l;
        return AllLoadouts[0];
    }

    // ─── COMBO DISCOVERY ───────────────────────────────────────

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
        return D.discoveredCombos.Contains(comboId);
    }

    public static void DiscoverCombo(string comboId)
    {
        if (!D.discoveredCombos.Contains(comboId))
        {
            D.discoveredCombos.Add(comboId);
            SaveSystem.Save();
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
        SaveSystem.ResetAll();
    }
}
