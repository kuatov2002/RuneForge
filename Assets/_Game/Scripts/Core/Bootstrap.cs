using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

public class Bootstrap : MonoBehaviour
{
    // Elements
    ElementSO fireElem, waterElem, earthElem, airElem;
    ElementSO lightningElem, poisonElem, voidElem;
    ElementSO[] baseElements;   // Fire, Water, Earth, Air
    ElementSO[] allElements;    // All 7

    // Synergy system
    SynergySystem synergySystem;

    // Relics
    RelicSO[] allRelics;
    RelicManager relicMgr;

    // Runtime
    GameObject player;
    PlayerController playerCtrl;
    SpellCaster spellCaster;
    Health playerHealth;
    GameHUD hud;
    TopDownCamera cam;

    List<GameObject> enemies = new();
    int wave = 1;
    int enemiesAlive;
    int enemiesKilledThisRun;
    float runStartTime;
    bool isPlayerDead;
    bool bossActive;
    GameObject currentBoss;
    GameObject rewardPickup;

    // Multi-wave system
    int subWave;
    int totalSubWaves;
    float reinforcementTimer;

    // Floor/Room system
    int currentFloor = 1; // 1-5
    int currentRoom = 1;  // 1-10
    int roomsPerFloor = 10;
    int totalFloors = 5;
    FloorGenerator floorGen;
    GameObject currentRoomGO;
    bool roomCleared;

    // Branching path system
    enum NodeType { Combat, EliteCombat, Shop, Event, Rest, Boss, Treasure, Altar, Challenge }
    struct MapNode { public NodeType type; public int depth; }
    List<MapNode[]> floorMap; // floorMap[depth] = array of 2-3 node choices

    // Encounter system
    EncounterSystem currentEncounter;
    float encounterSpawnTimer;
    bool encounterBonusRerollFlag;

    // Combat pressure timer: reinforcements if room takes too long
    const float CombatPressureFirstWave = 45f;
    const float CombatPressureInterval = 30f;
    const float CombatPressureWarning = 5f;
    const int CombatPressureSwarmCount = 3;
    float combatPressureTimer;
    bool combatPressureActive;
    bool combatPressureWarningShown;
    List<GameObject> pressurePortals = new();

    // Boss intro
    float bossIntroTimer;
    bool bossIntroActive;

    // Lore texts per floor
    static readonly string[][] loreTexts = new[]
    {
        new[] { "Ancient runes flicker on the walls...", "The air smells of dust and old magic.", "Footsteps echo in the forgotten halls." },
        new[] { "Water drips from crystalline stalactites.", "The walls shimmer with frost runes.", "A cold wind whispers through the corridors." },
        new[] { "Crimson light pulses from deep cracks.", "The stone is warm to the touch here.", "Embers float like fireflies in the dark." },
        new[] { "Vines twist through corrupted stone.", "Green fog clings to the ground.", "Something grows in the shadows." },
        new[] { "Reality bends at the edges of vision.", "The air crackles with unstable energy.", "This place should not exist." },
    };

    // Hub
    bool inHub;
    GameObject hubGO;
    HubUI hubUI;
    GoldSystem goldSystem;

    static Material litMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (FindAnyObjectByType<Bootstrap>() != null) return;
        new GameObject("Bootstrap").AddComponent<Bootstrap>();
    }

    void Start()
    {
        CreateSpellData();
        CreateLighting();
        CreateGameFeel();
        CreateGoldSystem();
        CreateSFXSystem();
        DoorTrigger.OnDoorEntered += OnDoorEntered;
        EnterHub();
    }

    void EnterHub()
    {
        inHub = true;
        CleanupRun();

        hubGO = HubBuilder.Build();
        CreatePlayer();
        player.transform.position = new Vector3(10, 0, 10); // Hub center
        CreateCamera();

        // Hub UI
        var hubUIGO = new GameObject("HubUI");
        hubUI = hubUIGO.AddComponent<HubUI>();
        hubUI.Init(StartRunFromHub, allElements);
    }

    void StartRunFromHub()
    {
        inHub = false;

        // Cleanup hub
        if (hubGO != null) { Destroy(hubGO); hubGO = null; }
        if (hubUI != null) { Destroy(hubUI.gameObject); hubUI = null; }
        if (player != null) { Destroy(player); player = null; }
        if (cam != null) { Destroy(cam.gameObject); cam = null; }

        // Start run
        wave = 1;
        currentFloor = 1;
        currentRoom = 1;
        roomCleared = false;
        bossActive = false;
        enemiesKilledThisRun = 0;
        runStartTime = Time.time;
        SpellMutationSystem.Reset();

        floorGen = new FloorGenerator();
        floorGen.Generate(roomsPerFloor, currentFloor - 1);
        GenerateFloorMap();
        BuildCurrentRoom();
        CreatePlayer();
        ApplyMetaProgressionToPlayer();
        CreateCamera();
        CreateHUD();
        hud.RefreshRelics(relicMgr.OwnedRelics);

        // Gold: starting gold from meta-progression
        if (goldSystem != null)
            goldSystem.Init(MetaProgression.StartingGold);
        hud.SetGold(goldSystem != null ? goldSystem.Gold : 0);

        // Starting relic from meta-progression (Path A: random relic, Path B: cursed relic + gold)
        if (MetaProgression.HasStartingRelic && allRelics.Length > 0)
        {
            var candidates = new System.Collections.Generic.List<RelicSO>();
            foreach (var r in allRelics) if (!r.isCursed && !relicMgr.HasRelic(r.relicType)) candidates.Add(r);
            if (candidates.Count > 0)
            {
                relicMgr.AddRelic(candidates[Random.Range(0, candidates.Count)]);
                hud.RefreshRelics(relicMgr.OwnedRelics);
            }
        }
        else if (MetaProgression.HasCursedHeirloom && allRelics.Length > 0)
        {
            var cursedCandidates = new System.Collections.Generic.List<RelicSO>();
            foreach (var r in allRelics) if (r.isCursed && !relicMgr.HasRelic(r.relicType)) cursedCandidates.Add(r);
            if (cursedCandidates.Count > 0)
            {
                relicMgr.AddRelic(cursedCandidates[Random.Range(0, cursedCandidates.Count)]);
                hud.RefreshRelics(relicMgr.OwnedRelics);
            }
            // Bonus gold from Cursed Heirloom
            if (goldSystem != null)
                goldSystem.AddGold(MetaProgression.CursedHeirloomGold);
        }

        // Apply selected loadout (starting spell + passive)
        ApplyStartingLoadout();

        SpawnWave();

        // Start ambient music
        if (SFXSystem.Instance != null) SFXSystem.Instance.StartMusic();

        // Codex: load discovery tracking
        Codex.Load();

        // Subscribe combo discovery
        spellCaster.OnComboNameChanged += Codex.DiscoverCombo;

        // Subscribe spell reaction feedback
        SpellInteractionSystem.OnReaction += OnSpellReaction;
        ElementalStatus.OnReaction += OnSpellReaction;

        // Tutorial hints (first run only)
        if (!SaveSystem.Data.tutorialShown)
        {
            SaveSystem.Data.tutorialShown = true;
            SaveSystem.Save();
            ShowTutorialHints();
        }
    }

    void ShowTutorialHints()
    {
        if (hud == null) return;
        var runner = hud.gameObject.AddComponent<TutorialHintRunner>();
        runner.Run(hud);
    }

    void ApplyMetaProgressionToPlayer()
    {
        // Path A: HP bonus
        int baseHP = 8 + MetaProgression.MaxHPBonus;
        playerHealth.maxHP = baseHP;
        playerHealth.currentHP = baseHP;

        // Path B: Second Wind (survive lethal hit)
        playerHealth.secondWindCharges = MetaProgression.SecondWindCharges;

        // Path A: Speed bonus
        playerCtrl.moveSpeed = 6f * MetaProgression.SpeedMultiplier;

        // Path A: Extra dash charges
        playerCtrl.SetExtraDashCharges(MetaProgression.ExtraDashCharges);

        // Path B: Phase Step (extra dash i-frames)
        playerCtrl.phaseStepBonus = MetaProgression.PhaseStepDuration;

        // Path B: Blink Strike (dash through enemies deals damage)
        playerCtrl.blinkStrikeDamage = MetaProgression.BlinkStrikeDamage;

        // Path A: Potions
        playerCtrl.SetPotions(MetaProgression.PotionsPerFloor);
    }

    void ApplyStartingLoadout()
    {
        // New system: player always starts with base 4 elements
        // SpellCaster.Init already sets up Fire, Water, Earth, Air
        // No loadout customization needed — elements are always the same at start
    }

    void CleanupRun()
    {
        if (player != null) { Destroy(player); player = null; }
        if (cam != null) { Destroy(cam.gameObject); cam = null; }
        if (hud != null) { Destroy(hud.gameObject); hud = null; }
        if (currentRoomGO != null) { Destroy(currentRoomGO); currentRoomGO = null; }
        if (currentBoss != null) { Destroy(currentBoss); currentBoss = null; }
        if (rewardPickup != null) { Destroy(rewardPickup); rewardPickup = null; }
        foreach (var e in enemies) if (e != null) Destroy(e);
        enemies.Clear();
        enemiesAlive = 0;
        bossActive = false;
        CleanupSpellEffects();
    }

    /// <summary>Destroy all lingering spell effect objects between rooms.</summary>
    static void CleanupSpellEffects()
    {
        // Destroy named spell effect objects
        string[] spellTags = {
            "SteamCloud", "MagmaPool", "PermafrostZone", "BulwarkWall",
            "RubbleStorm", "WildfireBolt", "Bolt", "ReactiveShot",
            "ComboLabel", "ComboArc", "WildfireArc", "ChainArc",
            "ToxicFrostPool", "GeyserColumn", "Fireball", "IceSpike"
        };
        foreach (var tag in spellTags)
        {
            var objs = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in objs)
                if (obj != null && obj.name == tag) Object.Destroy(obj);
        }

        // Destroy all SpellProjectiles
        foreach (var proj in Object.FindObjectsByType<SpellProjectile>(FindObjectsSortMode.None))
            if (proj != null) Object.Destroy(proj.gameObject);
        foreach (var fb in Object.FindObjectsByType<FireballProjectile>(FindObjectsSortMode.None))
            if (fb != null) Object.Destroy(fb.gameObject);
        foreach (var ice in Object.FindObjectsByType<IceSpikeProjectile>(FindObjectsSortMode.None))
            if (ice != null) Object.Destroy(ice.gameObject);

        // Destroy all Bulwark walls
        foreach (var wall in Object.FindObjectsByType<BulwarkFortress>(FindObjectsSortMode.None))
            if (wall != null) Object.Destroy(wall.gameObject);
        BulwarkSpell._activeWall = null;

        // Destroy all zone effects
        foreach (var zone in Object.FindObjectsByType<SteamCloudZone>(FindObjectsSortMode.None))
            if (zone != null) Object.Destroy(zone.gameObject);
        foreach (var zone in Object.FindObjectsByType<MagmaPoolZone>(FindObjectsSortMode.None))
            if (zone != null) Object.Destroy(zone.gameObject);
        foreach (var zone in Object.FindObjectsByType<PermafrostZone>(FindObjectsSortMode.None))
            if (zone != null) Object.Destroy(zone.gameObject);
        foreach (var storm in Object.FindObjectsByType<RubbleStorm>(FindObjectsSortMode.None))
            if (storm != null) Object.Destroy(storm.gameObject);
        foreach (var ascend in Object.FindObjectsByType<AscendEffect>(FindObjectsSortMode.None))
            if (ascend != null) ascend.enabled = false;

        // Destroy lingering VFX primitives
        foreach (var vfx in Object.FindObjectsByType<ComboExpandVFX>(FindObjectsSortMode.None))
            if (vfx != null) Object.Destroy(vfx.gameObject);
        foreach (var vfx in Object.FindObjectsByType<ComboShrinkVFX>(FindObjectsSortMode.None))
            if (vfx != null) Object.Destroy(vfx.gameObject);
        foreach (var vfx in Object.FindObjectsByType<FlashShrink>(FindObjectsSortMode.None))
            if (vfx != null) Object.Destroy(vfx.gameObject);
        // GeyserRise removed (legacy stub deleted)

        // Destroy any standalone particle systems not parented to player/camera
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (ps == null) continue;
            var root = ps.transform.root;
            if (root.GetComponent<PlayerController>() != null) continue;
            if (root.GetComponent<Camera>() != null) continue;
            if (root.GetComponent<Bootstrap>() != null) continue;
            // Only destroy if it's a spell effect (has no Health = not an enemy)
            if (root.GetComponent<Health>() == null && ps.transform.parent == null)
                Object.Destroy(ps.gameObject);
        }
    }

    void Update()
    {
        if (isPlayerDead)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                ReturnToHub();
        }

        // Boss intro sequence
        if (bossIntroActive)
        {
            bossIntroTimer -= Time.deltaTime;
            if (bossIntroTimer <= 0)
            {
                bossIntroActive = false;
                hud.HideBossIntro();
                if (cam != null) cam.ZoomTo(10f, 1f);
                SpawnBossWave(currentFloor);
            }
            return; // Don't process anything else during boss intro
        }

        // Encounter timers (Survival, TimedChallenge)
        if (currentEncounter != null && !currentEncounter.IsComplete)
        {
            if (currentEncounter.Type == EncounterSystem.EncounterType.Survival ||
                currentEncounter.Type == EncounterSystem.EncounterType.TimedChallenge)
            {
                currentEncounter.Timer -= Time.deltaTime;
                if (hud != null) hud.SetObjective(currentEncounter.GetObjectiveText());

                if (currentEncounter.Type == EncounterSystem.EncounterType.Survival && currentEncounter.Timer <= 0)
                {
                    currentEncounter.IsComplete = true;
                    hud.SetObjective("");
                    // Survival complete — clear remaining enemies, room done
                    foreach (var e in enemies) if (e != null) Destroy(e);
                    enemies.Clear();
                    enemiesAlive = 0;
                    if (goldSystem != null) goldSystem.AddGold(currentEncounter.BonusGold);
                    SpawnRewardPickup();
                }
                else if (currentEncounter.Type == EncounterSystem.EncounterType.TimedChallenge && currentEncounter.Timer <= 0)
                {
                    // Timer expired — no bonus, but room continues as KillAll
                    currentEncounter.IsComplete = true;
                    hud.SetObjective("Time's up! No bonus.");
                }

                // Survival: continuous spawning
                if (currentEncounter.Type == EncounterSystem.EncounterType.Survival && !currentEncounter.IsComplete)
                {
                    encounterSpawnTimer -= Time.deltaTime;
                    if (encounterSpawnTimer <= 0 && enemiesAlive < 8)
                    {
                        encounterSpawnTimer = 3f;
                        int type = PickEnemyType(10);
                        SpawnEnemy(type);
                        enemiesAlive++;
                    }
                }
            }
        }

        // Reinforcement timer for multi-wave rooms
        if (reinforcementTimer > 0)
        {
            reinforcementTimer -= Time.deltaTime;
            if (reinforcementTimer <= 0)
            {
                reinforcementTimer = -1;
                int baseBudget = currentFloor switch { 1 => 10, 2 => 18, 3 => 28, 4 => 40, _ => 55 };
                float budgetMult = currentNodeType == NodeType.EliteCombat ? 1.5f : 1f;
                int totalBudget = Mathf.CeilToInt((baseBudget + (currentRoom - 1) * 2) * budgetMult);
                int waveBudget = totalBudget / totalSubWaves;
                SpawnSubWave(waveBudget, currentNodeType == NodeType.EliteCombat);
            }
        }

        // Combat pressure: spawn reinforcement Swarm if room takes too long
        if (combatPressureActive && enemiesAlive > 0 && !bossActive && !roomCleared)
        {
            combatPressureTimer -= Time.deltaTime;

            // Warning portals 5s before spawn
            if (!combatPressureWarningShown && combatPressureTimer <= CombatPressureWarning)
            {
                combatPressureWarningShown = true;
                SpawnPressureWarningPortals();
            }

            if (combatPressureTimer <= 0)
            {
                // Spawn reinforcement swarm
                ClearPressurePortals();
                for (int i = 0; i < CombatPressureSwarmCount; i++)
                {
                    SpawnEnemy(3); // Swarm
                    enemiesAlive++;
                }
                SFXSystem.Play(SFXSystem.SFXType.Explosion, player.transform.position, 0.3f);

                // Reset for next wave
                combatPressureTimer = CombatPressureInterval;
                combatPressureWarningShown = false;
            }
        }
    }

    // ─── SPELL DATA ───────────────────────────────────────────────

    void CreateSpellData()
    {
        // Base elements (unlocked from start) — each has unique charges & recharge rhythm
        fireElem = CreateElement("Fire", ElementType.Fire, 6, new Color(1f, 0.4f, 0.1f),
            charges: 3, rechargeTime: 3.5f);   // Fast burst, frequent overheat
        waterElem = CreateElement("Water", ElementType.Water, 4, new Color(0.3f, 0.7f, 1f),
            charges: 5, rechargeTime: 6f);      // Deep pool, slow recovery
        earthElem = CreateElement("Earth", ElementType.Earth, 5, new Color(0.6f, 0.4f, 0.2f),
            charges: 4, rechargeTime: 5f);      // Balanced baseline
        airElem = CreateElement("Air", ElementType.Air, 3, new Color(0.8f, 0.9f, 1f),
            charges: 6, rechargeTime: 4f);      // Many light casts, quick recovery
        baseElements = new[] { fireElem, waterElem, earthElem, airElem };

        // Advanced elements (unlockable mid-run) — powerful but resource-hungry
        lightningElem = CreateElement("Lightning", ElementType.Lightning, 7, new Color(1f, 1f, 0.3f),
            charges: 2, rechargeTime: 4f);      // Very limited, devastating per-cast
        poisonElem = CreateElement("Poison", ElementType.Poison, 3, new Color(0.2f, 0.9f, 0.1f),
            charges: 4, rechargeTime: 7f);      // Normal pool, punishing overheat
        voidElem = CreateElement("Void", ElementType.Void, 8, new Color(0.6f, 0.1f, 0.9f),
            charges: 2, rechargeTime: 8f);      // Extremely limited, each cast is a commitment

        allElements = new[] { fireElem, waterElem, earthElem, airElem, lightningElem, poisonElem, voidElem };

        // Init combo spell registry
        ComboSpellRegistry.Init();

        // Relics
        allRelics = new RelicSO[]
        {
            CreateRelic("Swift Boots", RelicType.SpeedBoost, "+15% movement speed", new Color(0.3f, 0.8f, 1f)),
            CreateRelic("Double Strike", RelicType.DoubleStrike, "Every 5th hit deals 2x damage", new Color(1f, 0.6f, 0.1f)),
            CreateRelic("Blazing Trail", RelicType.DashFire, "Dash leaves a fire trail", new Color(1f, 0.3f, 0.1f)),
            CreateRelic("Thorns", RelicType.Thorns, "Reflect 1 damage to melee attackers", new Color(0.5f, 0.8f, 0.2f)),
            CreateRelic("Vampire Aura", RelicType.VampireAura, "Heal 1 HP per room cleared", new Color(0.8f, 0.1f, 0.2f)),
            CreateRelic("Glass Cannon", RelicType.GlassCannon, "+50% damage, -1 max HP", new Color(0.9f, 0.9f, 0.3f)),
            CreateRelic("Aegis", RelicType.Shield, "Block the first hit in each room", new Color(0.4f, 0.6f, 0.9f)),
            CreateRelic("Lucky Charm", RelicType.Lucky, "+20% chance for 4th rune choice", new Color(0.2f, 0.9f, 0.4f)),
            CreateRelic("Berserker Rage", RelicType.Berserker, "+25% damage when below 50% HP", new Color(0.9f, 0.2f, 0.1f)),
            CreateRelic("Regeneration", RelicType.Regeneration, "Heal 1 HP every 30 seconds", new Color(0.3f, 0.9f, 0.5f)),

            // Cursed relics
            CreateCursedRelic("Cursed Power", RelicType.CursedPower, "CURSED: +75% damage, take 1 extra damage per hit", new Color(0.5f, 0.1f, 0.3f)),
            CreateCursedRelic("Cursed Speed", RelicType.CursedSpeed, "CURSED: +40% move speed, -2 max HP", new Color(0.4f, 0.1f, 0.4f)),
            CreateCursedRelic("Cursed Gold", RelicType.CursedGold, "CURSED: 2x gold drops, enemies +30% HP", new Color(0.6f, 0.5f, 0.1f)),
            CreateCursedRelic("Blood Pact", RelicType.BloodPact, "CURSED: Spells cost 1 HP, +60% damage", new Color(0.6f, 0.05f, 0.05f)),
            CreateCursedRelic("Chaos", RelicType.Chaos, "CURSED: Random element each cast, +30% damage", new Color(0.3f, 0.1f, 0.5f)),

            // Element-specific relics
            CreateRelic("Ember Heart", RelicType.EmberHeart, "Fire spells chain to 1 extra target", new Color(1f, 0.4f, 0.1f)),
            CreateRelic("Frost Crown", RelicType.FrostCrown, "Freeze duration +50%", new Color(0.4f, 0.75f, 1f)),
            CreateRelic("Stone Skin", RelicType.StoneSkin, "Take 1 less damage when standing still", new Color(0.5f, 0.4f, 0.3f)),
            CreateRelic("Gale Ring", RelicType.GaleRing, "Dash distance +30%, air zone on dash", new Color(0.7f, 0.9f, 1f)),
            CreateRelic("Storm Conductor", RelicType.StormConductor, "+10% crit for lightning spells", new Color(1f, 1f, 0.3f)),
            CreateRelic("Venom Sac", RelicType.VenomSac, "Poison ticks deal 1.5x damage", new Color(0.2f, 0.8f, 0.1f)),
            CreateRelic("Void Lens", RelicType.VoidLens, "Void pull radius +40%", new Color(0.5f, 0.1f, 0.8f)),
            CreateRelic("Prism Shard", RelicType.PrismShard, "3+ elements in 10s = +25% damage", new Color(0.9f, 0.5f, 0.9f)),
        };
    }

    static RelicSO CreateRelic(string name, RelicType type, string desc, Color col)
    {
        var r = ScriptableObject.CreateInstance<RelicSO>();
        r.relicName = name; r.relicType = type; r.description = desc; r.color = col;
        return r;
    }

    static RelicSO CreateCursedRelic(string name, RelicType type, string desc, Color col)
    {
        var r = ScriptableObject.CreateInstance<RelicSO>();
        r.relicName = name; r.relicType = type; r.description = desc; r.color = col;
        r.isCursed = true;
        return r;
    }

    static ElementSO CreateElement(string name, ElementType type, int dmg, Color col,
        int charges = 4, float rechargeTime = 5f)
    {
        var e = ScriptableObject.CreateInstance<ElementSO>();
        e.elementName = name; e.elementType = type; e.baseDamage = dmg; e.color = col;
        e.maxCharges = charges; e.overheatRechargeTime = rechargeTime;
        return e;
    }

    // ─── FLOOR MAP ──────────────────────────────────────────────

    void GenerateFloorMap()
    {
        floorMap = new List<MapNode[]>();
        for (int d = 0; d < roomsPerFloor; d++)
        {
            if (d == 0)
                floorMap.Add(new[] { new MapNode { type = NodeType.Combat, depth = d } });
            else if (d == roomsPerFloor - 1)
                floorMap.Add(new[] { new MapNode { type = NodeType.Boss, depth = d } });
            else if (d == 4) // Shop depth
            {
                var opts = new List<MapNode> { new() { type = NodeType.Shop, depth = d } };
                opts.Add(new MapNode { type = currentFloor >= 3 ? NodeType.Altar : NodeType.Event, depth = d });
                floorMap.Add(opts.ToArray());
            }
            else if (d == 7) // Rest depth
            {
                var opts = new List<MapNode>
                {
                    new() { type = NodeType.Rest, depth = d },
                    new() { type = currentFloor >= 2 ? NodeType.Altar : NodeType.Event, depth = d }
                };
                floorMap.Add(opts.ToArray());
            }
            else // All other depths: 2-3 branching choices
            {
                var opts = new List<MapNode>();
                opts.Add(new MapNode { type = NodeType.Combat, depth = d });

                if (d >= 5 && d <= 6 && currentFloor >= 2)
                {
                    // Mid-floor: offer Treasure or Challenge
                    float roll = Random.value;
                    if (roll < 0.3f)
                        opts.Add(new MapNode { type = NodeType.Treasure, depth = d });
                    else if (roll < 0.55f)
                        opts.Add(new MapNode { type = NodeType.Challenge, depth = d });
                    else
                        opts.Add(new MapNode { type = NodeType.EliteCombat, depth = d });
                }
                else
                {
                    float roll = Random.value;
                    if (roll < 0.25f)
                        opts.Add(new MapNode { type = NodeType.EliteCombat, depth = d });
                    else if (roll < 0.5f)
                        opts.Add(new MapNode { type = NodeType.Event, depth = d });
                    else
                        opts.Add(new MapNode { type = NodeType.Combat, depth = d });
                }

                // Third option on later floors
                if (currentFloor >= 2 && Random.value < 0.5f)
                {
                    float roll2 = Random.value;
                    if (roll2 < 0.3f)
                        opts.Add(new MapNode { type = NodeType.EliteCombat, depth = d });
                    else if (roll2 < 0.5f && currentFloor >= 3)
                        opts.Add(new MapNode { type = NodeType.Treasure, depth = d });
                    else
                        opts.Add(new MapNode { type = NodeType.Event, depth = d });
                }

                floorMap.Add(opts.ToArray());
            }
        }
    }

    NodeType currentNodeType = NodeType.Combat;

    void ShowPathChoice()
    {
        int nextDepth = currentRoom; // currentRoom is 1-indexed, next depth = currentRoom (0-indexed is currentRoom-1, so next = currentRoom)
        if (nextDepth >= roomsPerFloor)
        {
            // Boss or end of floor
            TransitionToRoom(NodeType.Boss);
            return;
        }

        var nodes = floorMap[nextDepth];
        if (nodes.Length <= 1)
        {
            TransitionToRoom(nodes[0].type);
            return;
        }

        // Show choice UI
        string[] labels = new string[nodes.Length];
        string[] descs = new string[nodes.Length];
        Color[] colors = new Color[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            (labels[i], descs[i], colors[i]) = GetNodeDisplay(nodes[i].type);
        }

        hud.ShowEventRoom("CHOOSE YOUR PATH", $"Floor {currentFloor} — Depth {nextDepth + 1}/{roomsPerFloor}",
            new Color(0.7f, 0.7f, 0.8f), labels, descs, colors, choice =>
            {
                TransitionToRoom(nodes[choice].type);
            });
    }

    (string label, string desc, Color color) GetNodeDisplay(NodeType type) => type switch
    {
        NodeType.Combat => ("COMBAT", "Standard enemies", new Color(0.8f, 0.3f, 0.2f)),
        NodeType.EliteCombat => ("ELITE COMBAT", "Harder enemies, better rewards", new Color(0.9f, 0.6f, 0.1f)),
        NodeType.Shop => ("SHOP", "Buy relics and items with gold", new Color(1f, 0.85f, 0.2f)),
        NodeType.Event => ("EVENT", "Risk and reward", new Color(0.3f, 0.7f, 0.9f)),
        NodeType.Rest => ("REST", "Recover and prepare", new Color(0.3f, 0.9f, 0.5f)),
        NodeType.Boss => ("BOSS", "Floor guardian", new Color(0.8f, 0.1f, 0.1f)),
        NodeType.Treasure => ("TREASURE", "Hard fight, guaranteed relic", new Color(1f, 0.7f, 0.1f)),
        NodeType.Altar => ("BLOOD ALTAR", "Sacrifice HP for power", new Color(0.7f, 0.1f, 0.2f)),
        NodeType.Challenge => ("CHALLENGE", "Timed fight for bonus reward", new Color(0.9f, 0.4f, 0.9f)),
        _ => ("???", "Unknown", Color.gray)
    };

    void TransitionToRoom(NodeType nodeType)
    {
        // Clean up
        if (rewardPickup != null) { Destroy(rewardPickup); rewardPickup = null; }
        foreach (var e in enemies) if (e != null) Destroy(e);
        enemies.Clear();
        enemiesAlive = 0;
        combatPressureActive = false;
        ClearPressurePortals();

        // Destroy all lingering spell effects between rooms
        CleanupSpellEffects();

        currentRoom++;
        currentNodeType = nodeType;

        if (currentRoom > roomsPerFloor)
        {
            // Floor complete
            currentFloor++;
            currentRoom = 1;

            if (currentFloor > totalFloors)
            {
                MetaProgression.CompleteRun();
                AscensionSystem.OnRunComplete();
                isPlayerDead = true;
                int ownedRelicCount = relicMgr != null ? relicMgr.OwnedRelics.Count : 0;
                hud.ShowVictory(wave, currentFloor - 1, enemiesKilledThisRun,
                    runTime: Time.time - runStartTime,
                    relicsCollected: ownedRelicCount,
                    combosDiscovered: Codex.DiscoveredComboCount,
                    essenceEarned: 100);
                playerCtrl.enabled = false;
                spellCaster.enabled = false;
                return;
            }

            floorGen = new FloorGenerator();
            floorGen.Generate(roomsPerFloor, currentFloor - 1);
            GenerateFloorMap();

            if (playerCtrl != null)
                playerCtrl.RefillPotions(MetaProgression.PotionsPerFloor);

            // Second Wind resets each floor
            if (playerHealth != null)
                playerHealth.secondWindCharges = MetaProgression.SecondWindCharges;
        }

        BuildCurrentRoom();
        player.transform.position = new Vector3(
            currentRoomGO.transform.position.x + 6, 0,
            currentRoomGO.transform.position.z + 2);

        wave++;
        hud.SetFloorRoom(currentFloor, currentRoom);
        if (relicMgr != null) relicMgr.OnRoomEnter();
        SpawnWaveForNodeType(nodeType);
    }

    // ─── ROOM ─────────────────────────────────────────────────────

    void BuildCurrentRoom()
    {
        if (currentRoomGO != null) Destroy(currentRoomGO);

        int roomIdx = currentRoom - 1;
        bool isBossRoom = currentRoom == roomsPerFloor;
        bool isStart = currentRoom == 1;

        // Determine room size
        int w, h;
        if (isBossRoom) { w = 16; h = 16; }
        else if (currentRoom % 3 == 0) { w = 14; h = 14; }
        else { w = 12; h = 12; }

        // Doors: south door if not first room, north door if not last room
        bool doorN = !isBossRoom || false; // Boss rooms have no exit until boss dies
        bool doorS = !isStart;
        doorN = currentRoom < roomsPerFloor; // Has next room

        currentRoomGO = RoomBuilder.Build(w, h, doorN, doorS, false, false, currentFloor - 1);

        // Add environmental hazards to combat rooms (not start, shop, rest, or boss)
        bool isCombatRoom = !isStart && !isBossRoom && currentRoom != 5 && currentRoom != 8;
        if (isCombatRoom && w >= 10)
            RoomHazards.Populate(currentRoomGO.transform, w, h, currentFloor - 1);

        // Floor-specific mechanics
        if (isCombatRoom && currentFloor >= 2 && player != null)
            FloorMechanics.Apply(currentRoomGO, currentFloor - 1, player.transform);

        // Lore fragment: 20% chance in non-combat rooms
        if (!isCombatRoom && !isStart && !isBossRoom && Random.value < 0.2f)
            SpawnLoreStone(w, h);
    }

    void SpawnLoreStone(int roomW, int roomH)
    {
        var stone = new GameObject("LoreStone");
        stone.transform.parent = currentRoomGO.transform;
        Vector3 pos = new Vector3(Random.Range(2f, roomW - 2f), 0, Random.Range(2f, roomH - 2f));
        stone.transform.localPosition = pos;

        // Glowing stone visual
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<SphereCollider>());
        sphere.transform.parent = stone.transform;
        sphere.transform.localPosition = new Vector3(0, 0.4f, 0);
        sphere.transform.localScale = Vector3.one * 0.3f;
        sphere.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 0.6f, 1f), 3f);

        var col = stone.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1f;
        col.center = new Vector3(0, 0.4f, 0);

        int floorIdx = Mathf.Clamp(currentFloor - 1, 0, loreTexts.Length - 1);
        string text = loreTexts[floorIdx][Random.Range(0, loreTexts[floorIdx].Length)];

        var lore = stone.AddComponent<LoreStonePickup>();
        lore.loreText = text;
    }

    void TransitionToNextRoom()
    {
        ShowPathChoice();
    }

    void OnDoorEntered(string doorName)
    {
        if (!roomCleared) return;
        roomCleared = false;
        TransitionToNextRoom();
    }

    // ─── PLAYER (3D composite) ────────────────────────────────────

    void CreatePlayer()
    {
        player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(6, 0, 6);

        // Main collider (capsule)
        var capsuleCol = player.AddComponent<CapsuleCollider>();
        capsuleCol.height = 1.4f;
        capsuleCol.radius = 0.3f;
        capsuleCol.center = new Vector3(0, 0.7f, 0);

        // Rigidbody
        var rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var bodyColor = new Color(0.15f, 0.55f, 0.3f);
        var robeColor = new Color(0.1f, 0.35f, 0.2f);

        // Body (capsule torso)
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = player.transform;
        body.transform.localPosition = new Vector3(0, 0.5f, 0);
        body.transform.localScale = new Vector3(0.5f, 0.35f, 0.45f);
        body.GetComponent<Renderer>().material = MakeLit(bodyColor);

        // Robe bottom (wider cylinder)
        var robe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        robe.name = "Robe";
        Destroy(robe.GetComponent<CapsuleCollider>());
        robe.transform.parent = player.transform;
        robe.transform.localPosition = new Vector3(0, 0.2f, 0);
        robe.transform.localScale = new Vector3(0.55f, 0.2f, 0.55f);
        robe.GetComponent<Renderer>().material = MakeLit(robeColor);

        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        Destroy(head.GetComponent<SphereCollider>());
        head.transform.parent = player.transform;
        head.transform.localPosition = new Vector3(0, 1.05f, 0);
        head.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        head.GetComponent<Renderer>().material = ShaderCache.NewSkin(new Color(0.85f, 0.7f, 0.55f));

        // Staff
        var staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        staff.name = "Staff";
        Destroy(staff.GetComponent<CapsuleCollider>());
        staff.transform.parent = player.transform;
        staff.transform.localPosition = new Vector3(0.25f, 0.7f, 0.2f);
        staff.transform.localScale = new Vector3(0.06f, 0.5f, 0.06f);
        staff.transform.localRotation = Quaternion.Euler(15, 0, -10);
        staff.GetComponent<Renderer>().material = MakeLit(new Color(0.45f, 0.3f, 0.15f));

        // Staff tip (glowing)
        var staffTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        staffTip.name = "StaffTip";
        Destroy(staffTip.GetComponent<SphereCollider>());
        staffTip.transform.parent = staff.transform;
        staffTip.transform.localPosition = new Vector3(0, 1.1f, 0);
        staffTip.transform.localScale = new Vector3(2.5f, 0.4f, 2.5f);
        staffTip.GetComponent<Renderer>().material = ShaderCache.NewMagic(fireElem != null ? fireElem.color : Color.white, 4f);

        // Components
        playerCtrl = player.AddComponent<PlayerController>();
        spellCaster = player.AddComponent<SpellCaster>();
        playerHealth = player.AddComponent<Health>();
        int baseHP = 8 + MetaProgression.MaxHPBonus;
        playerHealth.maxHP = baseHP;
        playerHealth.currentHP = baseHP;

        relicMgr = player.AddComponent<RelicManager>();
        relicMgr.Init(playerHealth, playerCtrl, allRelics, allElements);

        // Synergy system
        synergySystem = player.AddComponent<SynergySystem>();

        // Momentum system
        player.AddComponent<MomentumSystem>();

        if (!inHub)
        {
            // Initialize with base elements
            spellCaster.Init(baseElements);

            playerHealth.OnDeath += OnPlayerDeath;

            // Update staff tip color when orbs change
            spellCaster.OnOrbsChanged += () =>
            {
                if (spellCaster.rightOrb != null && staffTip != null)
                    staffTip.GetComponent<Renderer>().material = ShaderCache.NewMagic(spellCaster.rightOrb.color, 4f);
            };
        }
        else
        {
            // Hub: disable spell casting
            spellCaster.enabled = false;
        }
    }

    // ─── CAMERA ───────────────────────────────────────────────────

    void CreateCamera()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            Destroy(c.gameObject);

        var camGO = new GameObject("MainCamera");
        camGO.tag = "MainCamera";
        var camera = camGO.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7;
        camera.backgroundColor = new Color(0.04f, 0.04f, 0.08f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 60f;
        camGO.AddComponent<AudioListener>();

        // Enable post-processing on URP camera
        var cameraData = camGO.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            cameraData = camGO.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;

        cam = camGO.AddComponent<TopDownCamera>();
        cam.target = player.transform;
        cam.distance = 14f;
        cam.pitch = 60f;

        // Global Volume for post-processing
        var volumeGO = new GameObject("GlobalVolume");
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = volume.profile;

        var tonemapping = profile.Add<Tonemapping>();
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;

        var bloom = profile.Add<Bloom>();
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.25f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.5f;

        var vignette = profile.Add<Vignette>();
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.2f;
    }

    // ─── LIGHTING ─────────────────────────────────────────────────

    void CreateLighting()
    {
        // Main directional light
        var lightGO = new GameObject("DirectionalLight");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.8f, 0.85f, 1f);
        light.intensity = 0.8f;
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(55, -30, 0);

        // Fill light
        var fillGO = new GameObject("FillLight");
        var fill = fillGO.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.3f, 0.3f, 0.5f);
        fill.intensity = 0.3f;
        fill.shadows = LightShadows.None;
        fillGO.transform.rotation = Quaternion.Euler(30, 150, 0);
    }

    // ─── HUD ──────────────────────────────────────────────────────

    // Cached delegate so we can unsubscribe on next run
    Action<int> goldUICallback;

    void CreateHUD()
    {
        var hudGO = new GameObject("HUD");
        hud = hudGO.AddComponent<GameHUD>();
        hud.Init(spellCaster, playerHealth);
        hud.OnReturnToHub = ReturnToHub;

        // Wire gold UI — remove old sub, add new
        if (goldSystem != null)
        {
            if (goldUICallback != null)
                goldSystem.OnGoldChanged -= goldUICallback;
            var currentHud = hud;
            goldUICallback = g => { if (currentHud != null) currentHud.SetGold(g); };
            goldSystem.OnGoldChanged += goldUICallback;
        }
    }

    // ─── GAME FEEL ──────────────────────────────────────────────

    void CreateGameFeel()
    {
        if (GameFeel.Instance != null) return;
        var go = new GameObject("GameFeel");
        go.AddComponent<GameFeel>();
    }

    void CreateSFXSystem()
    {
        if (SFXSystem.Instance != null) return;
        var go = new GameObject("SFXSystem");
        go.AddComponent<SFXSystem>();
    }

    void CreateGoldSystem()
    {
        if (GoldSystem.Instance != null) return;
        var go = new GameObject("GoldSystem");
        goldSystem = go.AddComponent<GoldSystem>();
        goldSystem.Init(0);
    }

    // ─── ENEMIES ──────────────────────────────────────────────────

    void SpawnWaveForNodeType(NodeType nodeType)
    {
        hud.SetWave(wave);
        hud.SetFloorRoom(currentFloor, currentRoom);
        roomCleared = false;
        currentEncounter = null;

        switch (nodeType)
        {
            case NodeType.Boss:
                StartPreBossSequence();
                return;
            case NodeType.Shop:
                if (currentFloor >= 3 && Random.value < 0.5f)
                    StartDevilDealRoom();
                else
                    StartShopRoomNew();
                return;
            case NodeType.Event:
                StartEventRoom();
                return;
            case NodeType.Rest:
                StartRestRoomNew();
                return;
            case NodeType.EliteCombat:
                SpawnCombatWaveWithEncounter(1.5f, true);
                return;
            case NodeType.Treasure:
                StartTreasureRoom();
                return;
            case NodeType.Altar:
                StartAltarRoom();
                return;
            case NodeType.Challenge:
                StartTimedChallengeRoom();
                return;
            default: // Combat
                SpawnCombatWaveWithEncounter(1f, false);
                return;
        }
    }

    // ─── NEW ROOM TYPES ────────────────────────────────────────

    void SpawnCombatWaveWithEncounter(float budgetMult, bool forceElite)
    {
        // Roll encounter type for variety
        currentEncounter = new EncounterSystem(EncounterSystem.Roll(currentFloor, currentRoom));

        if (currentEncounter.Type == EncounterSystem.EncounterType.KillAll)
        {
            SpawnCombatWave(budgetMult, forceElite);
            return;
        }

        SpawnCombatWave(budgetMult, forceElite);

        switch (currentEncounter.Type)
        {
            case EncounterSystem.EncounterType.Survival:
                hud.SetObjective(currentEncounter.GetObjectiveText());
                break;
            case EncounterSystem.EncounterType.TimedChallenge:
                hud.SetObjective(currentEncounter.GetObjectiveText());
                break;
            case EncounterSystem.EncounterType.PriorityTarget:
                // Find a support enemy (Healer/Buffer) as priority, fallback to first enemy
                GameObject priorityTarget = null;
                foreach (var e in enemies)
                {
                    if (e == null) continue;
                    if (e.GetComponent<HealerAI>() != null || e.GetComponent<BufferAI>() != null)
                    {
                        priorityTarget = e;
                        break;
                    }
                }
                // Fallback: mark the first enemy if no support enemy exists
                if (priorityTarget == null)
                {
                    foreach (var e in enemies)
                    {
                        if (e != null) { priorityTarget = e; break; }
                    }
                }

                if (priorityTarget != null)
                {
                    // Pulsing ring mark at the enemy's feet
                    var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(mark.GetComponent<CapsuleCollider>());
                    mark.name = "PriorityMark";
                    mark.transform.parent = priorityTarget.transform;
                    mark.transform.localPosition = new Vector3(0, 0.05f, 0);
                    mark.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
                    mark.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0.2f), 5f);
                    var pulse = mark.AddComponent<PriorityMarkPulse>();
                    pulse.rotateSpeed = 90f;

                    // Vertical beacon pillar — tall, thin, semi-transparent red
                    var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(beacon.GetComponent<CapsuleCollider>());
                    beacon.name = "PriorityBeacon";
                    beacon.transform.parent = priorityTarget.transform;
                    beacon.transform.localPosition = new Vector3(0, 4f, 0);
                    beacon.transform.localScale = new Vector3(0.15f, 4f, 0.15f);
                    var beaconMat = ShaderCache.NewEmissive(new Color(1f, 0.15f, 0.15f, 0.4f), 6f);
                    beaconMat.SetFloat("_Surface", 1); // transparent
                    beaconMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    beaconMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    beaconMat.renderQueue = 3000;
                    beacon.GetComponent<Renderer>().material = beaconMat;
                    var beaconPulse = beacon.AddComponent<PriorityMarkPulse>();
                    beaconPulse.minIntensity = 2f;
                    beaconPulse.maxIntensity = 8f;
                    beaconPulse.pulseSpeed = 2f;
                    beaconPulse.rotateSpeed = 0f;

                    // Track priority target death to complete the encounter
                    var targetHealth = priorityTarget.GetComponent<Health>();
                    if (targetHealth != null)
                    {
                        var encounter = currentEncounter;
                        targetHealth.OnDeath += () =>
                        {
                            if (encounter != null && !encounter.IsComplete)
                            {
                                encounter.CompleteObjective();
                                if (goldSystem != null) goldSystem.AddGold(encounter.BonusGold);
                                if (hud != null) hud.SetObjective(encounter.GetObjectiveText());
                            }
                        };
                    }
                }
                hud.SetObjective(currentEncounter.GetObjectiveText());
                break;
            case EncounterSystem.EncounterType.Gauntlet:
                hud.SetObjective(currentEncounter.GetObjectiveText());
                break;
        }
    }

    void StartTreasureRoom()
    {
        SpawnCombatWave(1.8f, true); // Hard fight
        // Reward override happens in SpawnRewardPickup — treasure rooms always give relic
    }

    void StartAltarRoom()
    {
        roomCleared = true;
        hud.ShowEventRoom(
            "BLOOD ALTAR", "Ancient power courses through this altar. What will you sacrifice?",
            new Color(0.7f, 0.1f, 0.2f),
            new[] { "SACRIFICE 2 HP", "SACRIFICE 1 HP", "SACRIFICE 3 HP", "LEAVE" },
            new[] { "Gain a random relic", "Gain a spell mutation", "Heal to full + permanent +1 max HP", "Continue safely" },
            new[] { new Color(0.8f, 0.1f, 0.15f), new Color(0.6f, 0.3f, 1f), new Color(0.3f, 0.9f, 0.4f), new Color(0.5f, 0.5f, 0.5f) },
            choice =>
            {
                switch (choice)
                {
                    case 0:
                        playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 2);
                        if (playerHealth.currentHP > playerHealth.maxHP)
                            playerHealth.currentHP = playerHealth.maxHP;
                        playerHealth.InvokeHPChanged();
                        var available = new List<RelicSO>();
                        foreach (var r in allRelics) if (!relicMgr.HasRelic(r.relicType)) available.Add(r);
                        if (available.Count > 0)
                        {
                            var relic = available[Random.Range(0, available.Count)];
                            relicMgr.AddRelic(relic);
                            hud.RefreshRelics(relicMgr.OwnedRelics);
                            Codex.DiscoverRelic(relic.relicName);
                        }
                        break;
                    case 1:
                        if (SpellMutationSystem.ActiveMutations.Count < SpellMutationSystem.MaxMutations)
                        {
                            playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 1);
                            if (playerHealth.currentHP > playerHealth.maxHP)
                                playerHealth.currentHP = playerHealth.maxHP;
                            playerHealth.InvokeHPChanged();
                            var mutations = SpellMutationSystem.GenerateChoices(1);
                            if (mutations.Length > 0) SpellMutationSystem.AddMutation(mutations[0].type);
                        }
                        break;
                    case 2:
                        playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 3);
                        if (playerHealth.currentHP > playerHealth.maxHP)
                            playerHealth.currentHP = playerHealth.maxHP;
                        playerHealth.maxHP += 1;
                        playerHealth.Heal(playerHealth.maxHP);
                        break;
                }
                SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                hud.Refresh();
                TransitionToNextRoom();
            });
    }

    void StartTimedChallengeRoom()
    {
        currentEncounter = new EncounterSystem(EncounterSystem.EncounterType.TimedChallenge);
        SpawnCombatWave(1f, false);
        hud.SetObjective(currentEncounter.GetObjectiveText());
    }

    void StartPreBossSequence()
    {
        bossIntroActive = true;
        bossIntroTimer = 2.5f;

        string bossName = GetBossName(currentFloor);
        int loreIdx = Mathf.Clamp(currentFloor - 1, 0, loreTexts.Length - 1);
        string lore = loreTexts[loreIdx][Random.Range(0, loreTexts[loreIdx].Length)];

        hud.ShowBossIntro(bossName, lore);
        SFXSystem.Play(SFXSystem.SFXType.BossIntro, player.transform.position);

        if (cam != null) cam.ZoomTo(8f, 0.5f);
    }

    // Keep SpawnWave as alias for first room
    void SpawnWave()
    {
        SpawnWaveForNodeType(currentRoom == roomsPerFloor ? NodeType.Boss : NodeType.Combat);
    }

    // ─── ENCOUNTER TEMPLATES ─────────────────────────────────────

    struct EncounterTemplate
    {
        public string name;
        public int[] enemies; // enemy type IDs
        public int cost;
        public int minFloor;
    }

    static readonly EncounterTemplate[] encounterTemplates = new[]
    {
        new EncounterTemplate { name = "Shield Wall",    enemies = new[]{5,1,1},     cost = 11, minFloor = 1 },
        new EncounterTemplate { name = "Ambush",         enemies = new[]{2,0,0,0,0}, cost = 14, minFloor = 1 },
        new EncounterTemplate { name = "Chaos Pack",     enemies = new[]{4,0,1},      cost = 12, minFloor = 2 },
        new EncounterTemplate { name = "Swarm Tide",     enemies = new[]{3,6},        cost = 11, minFloor = 2 },
        new EncounterTemplate { name = "Artillery Line", enemies = new[]{1,1,1,7},    cost = 14, minFloor = 2 },
        new EncounterTemplate { name = "Juggernaut",     enemies = new[]{2,6},        cost = 12, minFloor = 2 },
        new EncounterTemplate { name = "Mirror Match",   enemies = new[]{4,4,5},      cost = 19, minFloor = 3 },
        new EncounterTemplate { name = "Twin Brutes",    enemies = new[]{2,2},        cost = 12, minFloor = 3 },
        new EncounterTemplate { name = "Shield Brothers",enemies = new[]{5,5,7},      cost = 15, minFloor = 3 },
        new EncounterTemplate { name = "Death Squad",    enemies = new[]{2,4,4,6},    cost = 26, minFloor = 4 },
        new EncounterTemplate { name = "Fortress",       enemies = new[]{5,5,1,1,1},  cost = 19, minFloor = 4 },
        new EncounterTemplate { name = "Apocalypse",     enemies = new[]{3,2,2,7},    cost = 22, minFloor = 5 },
    };

    bool TrySpawnEncounter(int budget)
    {
        // Collect valid templates
        var valid = new List<EncounterTemplate>();
        foreach (var t in encounterTemplates)
            if (t.minFloor <= currentFloor && t.cost <= budget) valid.Add(t);

        if (valid.Count == 0) return false;

        var template = valid[Random.Range(0, valid.Count)];
        foreach (int type in template.enemies)
        {
            if (type == 3) // Swarm spawns pack
            {
                int sc = Random.Range(4, 7);
                for (int s = 0; s < sc; s++) { SpawnEnemy(type); enemiesAlive++; }
            }
            else { SpawnEnemy(type); enemiesAlive++; }
        }
        return true;
    }

    // ─── WAVE SPAWNING ───────────────────────────────────────────

    void SpawnCombatWave(float budgetMult, bool forceElite)
    {
        int baseBudget = currentFloor switch { 1 => 10, 2 => 18, 3 => 28, 4 => 40, _ => 55 };
        int totalBudget = Mathf.CeilToInt((baseBudget + (currentRoom - 1) * 2) * budgetMult);

        // Split into 1-3 sub-waves based on floor
        totalSubWaves = currentFloor >= 3 ? Random.Range(2, 4) : (currentFloor >= 2 ? Random.Range(1, 3) : 1);
        subWave = 0;
        reinforcementTimer = -1;

        // Combat pressure: start timer for anti-kiting reinforcements
        combatPressureTimer = CombatPressureFirstWave;
        combatPressureActive = true;
        combatPressureWarningShown = false;

        int waveBudget = totalBudget / totalSubWaves;
        SpawnSubWave(waveBudget, forceElite);
    }

    void SpawnSubWave(int budget, bool forceElite)
    {
        subWave++;
        flankSide = Random.Range(0, 4); // Randomize starting quadrant each sub-wave

        // 60% chance to use encounter template, 40% random
        bool usedTemplate = false;
        if (Random.value < 0.6f && budget >= 10)
            usedTemplate = TrySpawnEncounter(budget);

        if (!usedTemplate)
        {
            while (budget > 0)
            {
                int type = PickEnemyType(budget);
                int cost = type switch { 0=>2, 1=>3, 2=>6, 3=>5, 4=>7, 5=>5, 6=>6, 7=>5, _=>2 };
                if (cost > budget) { type = 0; cost = 2; }
                if (cost > budget) break;
                budget -= cost;

                if (type == 3)
                {
                    int sc = Random.Range(4, 7);
                    for (int s = 0; s < sc; s++) { SpawnEnemy(type); enemiesAlive++; }
                }
                else { SpawnEnemy(type); enemiesAlive++; }
            }
        }

        if (enemiesAlive == 0) { SpawnEnemy(0); enemiesAlive = 1; }

        if (forceElite)
        {
            foreach (var e in enemies)
            {
                if (e == null) continue;
                var affix = e.GetComponent<EnemyAffix>();
                if (affix == null)
                {
                    affix = e.AddComponent<EnemyAffix>();
                    AffixType rollType = EnemyAffix.RollAffix(5);
                    if (rollType == AffixType.None) rollType = AffixType.Berserker;
                    affix.Init(rollType);
                }
            }
        }

        // Show sub-wave indicator
        if (totalSubWaves > 1 && hud != null)
            hud.SetWave(wave, subWave, totalSubWaves);
    }

    // ─── COMBAT PRESSURE PORTALS ────────────────────────────────

    void SpawnPressureWarningPortals()
    {
        ClearPressurePortals();
        // Spawn 2-3 glowing portals at room edges
        int count = CombatPressureSwarmCount;
        for (int i = 0; i < count; i++)
        {
            var portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(portal.GetComponent<CapsuleCollider>());
            portal.name = "PressurePortal";
            portal.transform.position = FlankSpawnPos(i) + Vector3.up * 0.1f;
            portal.transform.localScale = new Vector3(1.2f, 0.03f, 1.2f);
            portal.GetComponent<Renderer>().material = ShaderCache.NewDanger(new Color(1f, 0.1f, 0.1f), 0.2f, 0.5f, 6f, 2f);
            portal.AddComponent<PressurePortalVFX>();
            pressurePortals.Add(portal);
        }
        if (hud != null) hud.SetObjective("Reinforcements incoming!");
    }

    void ClearPressurePortals()
    {
        foreach (var p in pressurePortals) if (p != null) Destroy(p);
        pressurePortals.Clear();
    }

    void StartShopRoom()
    {
        int price = 30 + currentFloor * 5;
        int gold = goldSystem != null ? goldSystem.Gold : 0;

        hud.ShowShopRoom(allRelics, relicMgr, price, gold, relic =>
        {
            if (relic != null)
            {
                if (goldSystem != null && goldSystem.TrySpend(price))
                {
                    relicMgr.AddRelic(relic);
                    hud.RefreshRelics(relicMgr.OwnedRelics);
                    Codex.DiscoverRelic(relic.relicName);
                }
            }
            TransitionToNextRoom();
        });
    }

    void StartShopRoomNew()
    {
        roomCleared = true;
        var items = ShopSystem.GenerateInventory(currentFloor, allRelics, relicMgr,
            SpellMutationSystem.ActiveMutations.Count);

        hud.ShowShopRoomNew(items, goldSystem != null ? goldSystem.Gold : 0, (item, idx) =>
        {
            if (goldSystem == null || !goldSystem.TrySpend(item.price)) return false;

            switch (item.type)
            {
                case ShopSystem.ShopItemType.Relic:
                case ShopSystem.ShopItemType.CursedRelic:
                    if (item.relic != null)
                    {
                        relicMgr.AddRelic(item.relic);
                        hud.RefreshRelics(relicMgr.OwnedRelics);
                        Codex.DiscoverRelic(item.relic.relicName);
                    }
                    break;
                case ShopSystem.ShopItemType.Mutation:
                    SpellMutationSystem.AddMutation(item.mutation.type);
                    break;
                case ShopSystem.ShopItemType.Potion:
                    if (playerCtrl != null) playerCtrl.AddPotion(1);
                    break;
                case ShopSystem.ShopItemType.Reroll:
                    encounterBonusRerollFlag = true;
                    break;
            }
            SFXSystem.Play(SFXSystem.SFXType.ShopBuy, player.transform.position);
            return true;
        }, () => TransitionToNextRoom());
    }

    void StartDevilDealRoom()
    {
        // Offer 2 cursed relics — powerful but with drawbacks, costs HP instead of gold
        var cursedRelics = new List<RelicSO>();
        foreach (var r in allRelics)
            if (r.isCursed && !relicMgr.HasRelic(r.relicType)) cursedRelics.Add(r);

        if (cursedRelics.Count == 0) { StartShopRoom(); return; }

        runeOverlayActive = true;
        hud.ShowDevilDeal(cursedRelics, relicMgr, playerHealth, relic =>
        {
            runeOverlayActive = false;
            if (relic != null)
            {
                // Cost: 1 max HP
                playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 1);
                if (playerHealth.currentHP > playerHealth.maxHP)
                    playerHealth.currentHP = playerHealth.maxHP;

                relicMgr.AddRelic(relic);
                hud.RefreshRelics(relicMgr.OwnedRelics);
                hud.Refresh();
            }
            TransitionToNextRoom();
        });
    }

    bool runeOverlayActive;

    void StartEventRoom()
    {
        roomCleared = true; // Allow door transition after event

        int eventType = Random.Range(0, 7);
        switch (eventType)
        {
            case 0: StartSacrificeEvent(); break;
            case 1: StartCurseChoiceEvent(); break;
            case 2: StartGambleEvent(); break;
            case 3: StartMysteryEvent(); break;
            case 4: StartChallengeEvent(); break;
            case 5: StartSpellForgeEvent(); break;
            default: StartMerchantEvent(); break;
        }
    }

    void StartSacrificeEvent()
    {
        hud.ShowEventRoom(
            "BLOOD ALTAR", "A dark altar pulses with power. Sacrifice your vitality?",
            new Color(0.8f, 0.1f, 0.15f),
            new[] { "SACRIFICE 2 HP", "SACRIFICE 1 HP", "LEAVE" },
            new[] { "Gain a random relic", "Heal 3 HP + 20 gold", "Continue safely" },
            new[] { new Color(0.8f, 0.1f, 0.15f), new Color(0.8f, 0.4f, 0.1f), new Color(0.5f, 0.5f, 0.5f) },
            choice =>
            {
                switch (choice)
                {
                    case 0:
                        playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 2);
                        if (playerHealth.currentHP > playerHealth.maxHP)
                            playerHealth.currentHP = playerHealth.maxHP;
                        playerHealth.InvokeHPChanged();
                        // Give random relic
                        var available = new List<RelicSO>();
                        foreach (var r in allRelics) if (!relicMgr.HasRelic(r.relicType)) available.Add(r);
                        if (available.Count > 0)
                        {
                            var relic = available[Random.Range(0, available.Count)];
                            relicMgr.AddRelic(relic);
                            hud.RefreshRelics(relicMgr.OwnedRelics);
                        }
                        break;
                    case 1:
                        playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 1);
                        if (playerHealth.currentHP > playerHealth.maxHP)
                            playerHealth.currentHP = playerHealth.maxHP;
                        playerHealth.Heal(3);
                        if (goldSystem != null) goldSystem.AddGold(20);
                        break;
                }
                hud.Refresh();
                TransitionToNextRoom();
            });
    }

    void StartCurseChoiceEvent()
    {
        hud.ShowEventRoom(
            "WITCH'S BARGAIN", "A cackling witch offers forbidden power...",
            new Color(0.5f, 0.1f, 0.6f),
            new[] { "ACCEPT CURSE", "STEAL GOLD", "REFUSE" },
            new[] { "Random curse relic + 50 gold", "Gain 30 gold, lose 1 HP", "Walk away" },
            new[] { new Color(0.5f, 0.1f, 0.6f), new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.5f, 0.5f) },
            choice =>
            {
                switch (choice)
                {
                    case 0:
                        var cursed = new List<RelicSO>();
                        foreach (var r in allRelics) if (r.isCursed && !relicMgr.HasRelic(r.relicType)) cursed.Add(r);
                        if (cursed.Count > 0)
                        {
                            var relic = cursed[Random.Range(0, cursed.Count)];
                            relicMgr.AddRelic(relic);
                            hud.RefreshRelics(relicMgr.OwnedRelics);
                        }
                        if (goldSystem != null) goldSystem.AddGold(50);
                        break;
                    case 1:
                        if (goldSystem != null) goldSystem.AddGold(30);
                        playerHealth.TakeDamage(1);
                        break;
                }
                hud.Refresh();
                TransitionToNextRoom();
            });
    }

    void StartGambleEvent()
    {
        int bet = 25;
        int currentGold = goldSystem != null ? goldSystem.Gold : 0;
        bool canBet = currentGold >= bet;

        hud.ShowEventRoom(
            "FORTUNE'S WHEEL", "A mysterious gambler beckons you to try your luck.",
            new Color(1f, 0.85f, 0.2f),
            new[] { canBet ? "BET 25 GOLD" : "NOT ENOUGH GOLD", "BET 1 MAX HP", "LEAVE" },
            new[] { "50% chance: win 75 gold or lose bet", "50% chance: +2 max HP or lose 2 max HP", "Continue safely" },
            new[] { new Color(1f, 0.85f, 0.2f), new Color(0.8f, 0.2f, 0.2f), new Color(0.5f, 0.5f, 0.5f) },
            choice =>
            {
                switch (choice)
                {
                    case 0:
                        if (canBet && goldSystem != null)
                        {
                            if (Random.value < 0.5f)
                            {
                                goldSystem.AddGold(75);
                                SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                            }
                            else
                            {
                                goldSystem.TrySpend(bet);
                                SFXSystem.Play(SFXSystem.SFXType.PlayerHit, player.transform.position);
                            }
                        }
                        break;
                    case 1:
                        if (Random.value < 0.5f)
                        {
                            playerHealth.maxHP += 2;
                            playerHealth.Heal(2);
                            SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                        }
                        else
                        {
                            playerHealth.maxHP = Mathf.Max(1, playerHealth.maxHP - 2);
                            if (playerHealth.currentHP > playerHealth.maxHP)
                                playerHealth.currentHP = playerHealth.maxHP;
                            playerHealth.InvokeHPChanged();
                            SFXSystem.Play(SFXSystem.SFXType.PlayerHit, player.transform.position);
                        }
                        break;
                }
                hud.Refresh();
                TransitionToNextRoom();
            });
    }

    void StartMysteryEvent()
    {
        hud.ShowEventRoom(
            "STRANGE SHRINE", "An ancient shrine hums with unknown energy.",
            new Color(0.3f, 0.7f, 0.9f),
            new[] { "PRAY", "SMASH IT", "IGNORE" },
            new[] { "Random blessing or curse", "Guaranteed 40 gold + enemies may appear", "Leave undisturbed" },
            new[] { new Color(0.3f, 0.7f, 0.9f), new Color(0.9f, 0.3f, 0.1f), new Color(0.5f, 0.5f, 0.5f) },
            choice =>
            {
                switch (choice)
                {
                    case 0: // Pray: random effect
                        int roll = Random.Range(0, 4);
                        if (roll == 0) { playerHealth.Heal(playerHealth.maxHP); } // Full heal
                        else if (roll == 1) { playerHealth.maxHP += 1; playerHealth.Heal(1); } // +1 max HP
                        else if (roll == 2) { playerHealth.TakeDamage(2); } // Damage
                        else { if (goldSystem != null) goldSystem.AddGold(40); } // Gold
                        break;
                    case 1: // Smash: guaranteed gold + possible fight
                        if (goldSystem != null) goldSystem.AddGold(40);
                        // 40% chance to spawn enemies
                        if (Random.value < 0.4f)
                        {
                            roomCleared = false;
                            enemiesAlive = 0;
                            for (int i = 0; i < 3; i++) { SpawnEnemy(0); enemiesAlive++; }
                        }
                        break;
                }
                hud.Refresh();
                if (roomCleared)
                    TransitionToNextRoom();
                // If enemies spawned, room will transition after they're killed
            });
    }

    void StartRestRoom()
    {
        // Rest: heal to full, then advance
        if (playerHealth != null)
            playerHealth.Heal(playerHealth.maxHP);
        hud.ShowRestRoom(() => TransitionToNextRoom());
    }

    void StartRestRoomNew()
    {
        roomCleared = true;
        // Check if player has any cursed relics
        bool hasCurse = false;
        if (relicMgr != null)
            foreach (var r in relicMgr.OwnedRelics)
                if (r.isCursed) { hasCurse = true; break; }

        // Check if player has mutations for option 4
        bool hasMutations = SpellMutationSystem.ActiveMutations.Count > 0;

        string[] labels = hasCurse
            ? new[] { "REST & HEAL", "UPGRADE POTIONS", "PURIFY CURSE",
                      hasMutations ? "REFORGE SPELL" : "SHARPEN FOCUS",
                      "SCOUT AHEAD" }
            : new[] { "REST & HEAL", "UPGRADE POTIONS", "MEDITATE",
                      hasMutations ? "REFORGE SPELL" : "SHARPEN FOCUS",
                      "SCOUT AHEAD" };
        string[] descs = hasCurse
            ? new[] { "Heal to full HP", "+1 potion capacity", "Remove a cursed relic",
                      hasMutations ? "Replace a random mutation" : "+10% spell damage this floor",
                      "See what lies ahead" }
            : new[] { "Heal to full HP", "+1 potion capacity", "+1 max HP",
                      hasMutations ? "Replace a random mutation" : "+10% spell damage this floor",
                      "See what lies ahead" };
        Color[] cols = new[] {
            new Color(0.3f, 0.9f, 0.4f), new Color(0.3f, 0.7f, 1f), new Color(0.8f, 0.6f, 1f),
            new Color(1f, 0.6f, 0.2f), new Color(0.2f, 0.8f, 0.8f)
        };

        hud.ShowEventRoom("REST SITE", "A safe haven to recover...", new Color(0.3f, 0.9f, 0.5f),
            labels, descs, cols, choice =>
            {
                switch (choice)
                {
                    case 0:
                        if (playerHealth != null) playerHealth.Heal(playerHealth.maxHP);
                        break;
                    case 1:
                        if (playerCtrl != null) playerCtrl.AddPotion(1);
                        break;
                    case 2:
                        if (hasCurse && relicMgr != null)
                        {
                            foreach (var r in relicMgr.OwnedRelics)
                            {
                                if (r.isCursed) { relicMgr.RemoveRelic(r); break; }
                            }
                            hud.RefreshRelics(relicMgr.OwnedRelics);
                        }
                        else
                        {
                            if (playerHealth != null) { playerHealth.maxHP += 1; playerHealth.Heal(1); }
                        }
                        break;
                    case 3:
                        if (hasMutations)
                        {
                            // Reforge: remove a random mutation, then add a new random one
                            var active = new List<SpellMutationSystem.MutationType>(SpellMutationSystem.ActiveMutations);
                            var toRemove = active[Random.Range(0, active.Count)];
                            SpellMutationSystem.Reset();
                            // Re-add all except the removed one
                            foreach (var m in active)
                                if (m != toRemove) SpellMutationSystem.AddMutation(m);
                            // Add a random new mutation
                            var mutChoices = SpellMutationSystem.GenerateChoices(1);
                            if (mutChoices.Length > 0) SpellMutationSystem.AddMutation(mutChoices[0].type);
                        }
                        else
                        {
                            // Sharpen Focus: +10% spell damage
                            if (spellCaster != null) spellCaster.damageBonusMult += 0.1f;
                        }
                        break;
                    case 4:
                        // Scout Ahead: +15% damage for the next room
                        if (spellCaster != null) spellCaster.damageBonusMult += 0.15f;
                        break;
                }
                SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                hud.Refresh();
                TransitionToNextRoom();
            });
    }

    // ─── NEW EVENT ROOMS ─────────────────────────────────────────

    void StartChallengeEvent()
    {
        hud.ShowEventRoom(
            "ARENA CHALLENGE", "A spectral arena beckons. Survive 3 rapid waves for a reward!",
            new Color(0.9f, 0.2f, 0.2f),
            new[] { "ACCEPT CHALLENGE", "WALK AWAY" },
            new[] { "Survive 3 fast waves → earn a relic", "Continue safely" },
            new[] { new Color(0.9f, 0.2f, 0.2f), new Color(0.5f, 0.5f, 0.5f) },
            choice =>
            {
                if (choice == 0)
                {
                    roomCleared = false;
                    challengeWavesLeft = 3;
                    isChallenge = true;
                    SpawnCombatWave(0.6f, false);
                }
                else
                {
                    TransitionToNextRoom();
                }
            });
    }

    int challengeWavesLeft;
    bool isChallenge;

    void OnChallengeWaveComplete()
    {
        challengeWavesLeft--;
        if (challengeWavesLeft > 0)
        {
            // Spawn next wave after brief delay
            reinforcementTimer = 1f;
            totalSubWaves = 1;
            subWave = 0;
        }
        else
        {
            // Challenge complete! Award relic
            isChallenge = false;
            roomCleared = true;
            var available = new System.Collections.Generic.List<RelicSO>();
            foreach (var r in allRelics) if (!relicMgr.HasRelic(r.relicType)) available.Add(r);
            if (available.Count > 0)
            {
                var relic = available[Random.Range(0, available.Count)];
                relicMgr.AddRelic(relic);
                hud.RefreshRelics(relicMgr.OwnedRelics);
            }
            SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
            hud.Refresh();
            TransitionToNextRoom();
        }
    }

    void StartSpellForgeEvent()
    {
        if (SpellMutationSystem.ActiveMutations.Count >= SpellMutationSystem.MaxMutations)
        {
            // Already at max mutations, fall back to mystery
            StartMysteryEvent();
            return;
        }

        var choices = SpellMutationSystem.GenerateChoices(3);
        if (choices.Length == 0) { StartMysteryEvent(); return; }

        string[] labels = new string[choices.Length];
        string[] descs = new string[choices.Length];
        Color[] colors = new Color[choices.Length];
        for (int i = 0; i < choices.Length; i++)
        {
            labels[i] = choices[i].name;
            descs[i] = choices[i].description;
            colors[i] = choices[i].color;
        }

        hud.ShowEventRoom(
            "SPELL FORGE", "Ancient runes shimmer with power. Choose a mutation for your spells.",
            new Color(0.6f, 0.3f, 1f), labels, descs, colors,
            choice =>
            {
                if (choice >= 0 && choice < choices.Length)
                {
                    SpellMutationSystem.AddMutation(choices[choice].type);
                    SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                }
                hud.Refresh();
                TransitionToNextRoom();
            });
    }

    void StartMerchantEvent()
    {
        int gold = goldSystem != null ? goldSystem.Gold : 0;
        int hpPrice = 40;
        int dashPrice = 60;
        int rerollPrice = 30;

        hud.ShowEventRoom(
            "WANDERING MERCHANT", "A hooded figure spreads wares on a tattered cloth.",
            new Color(0.8f, 0.7f, 0.2f),
            new[] {
                gold >= hpPrice ? $"BUY +1 MAX HP ({hpPrice}g)" : $"NOT ENOUGH ({hpPrice}g)",
                gold >= dashPrice ? $"BUY +1 DASH ({dashPrice}g)" : $"NOT ENOUGH ({dashPrice}g)",
                gold >= rerollPrice ? $"REROLL ELEMENT ({rerollPrice}g)" : $"NOT ENOUGH ({rerollPrice}g)"
            },
            new[] { "+1 max HP and heal 1", "+1 dash charge", "Replace a random equipped element" },
            new[] { new Color(0.3f, 0.9f, 0.4f), new Color(0.3f, 0.7f, 1f), new Color(0.9f, 0.5f, 0.2f) },
            choice =>
            {
                switch (choice)
                {
                    case 0:
                        if (gold >= hpPrice && goldSystem != null)
                        {
                            goldSystem.TrySpend(hpPrice);
                            playerHealth.maxHP += 1;
                            playerHealth.Heal(1);
                            SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                        }
                        break;
                    case 1:
                        if (gold >= dashPrice && goldSystem != null)
                        {
                            goldSystem.TrySpend(dashPrice);
                            if (playerCtrl != null) playerCtrl.SetExtraDashCharges(playerCtrl.MaxDashCharges);
                            SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                        }
                        break;
                    case 2:
                        if (gold >= rerollPrice && goldSystem != null && spellCaster != null)
                        {
                            goldSystem.TrySpend(rerollPrice);
                            // Replace random equipped element with random unlocked one
                            int slot = Random.Range(0, 4);
                            var current = spellCaster.equippedElements[slot];
                            var candidates = new System.Collections.Generic.List<ElementSO>();
                            foreach (var el in allElements)
                                if (el != current && System.Array.IndexOf(spellCaster.equippedElements, el) < 0)
                                    candidates.Add(el);
                            if (candidates.Count > 0)
                            {
                                spellCaster.equippedElements[slot] = candidates[Random.Range(0, candidates.Count)];
                                SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                            }
                        }
                        break;
                }
                hud.Refresh();
                TransitionToNextRoom();
            });
    }

    int PickEnemyType(int budget)
    {
        if (wave <= 2) return Random.value < 0.7f ? 0 : 1;
        if (wave <= 4)
        {
            float r = Random.value;
            if (r < 0.3f) return 0; if (r < 0.5f) return 1;
            if (r < 0.7f) return 2; return 3;
        }
        // Wave 5+: all types including Healer(6) and Buffer(7)
        return Random.Range(0, 8);
    }

    void SpawnEnemy(int type)
    {
        switch (type)
        {
            case 0: SpawnShambler(); break;
            case 1: SpawnArcher(); break;
            case 2: SpawnBrute(); break;
            case 3: SpawnSwarmUnit(); break;
            case 4: SpawnMirror(); break;
            case 5: SpawnShieldBearer(); break;
            case 6: SpawnHealer(); break;
            case 7: SpawnBuffer(); break;
        }
    }

    // Flank spawning: alternate sides so melee enemies surround the player
    int flankSide; // cycles 0-3 for four quadrants

    Vector3 RandomSpawnPos()
    {
        return FlankSpawnPos(flankSide++);
    }

    Vector3 FlankSpawnPos(int side)
    {
        int w = RoomBuilder.LastWidth;
        int h = RoomBuilder.LastHeight;
        RoomShape shape = RoomBuilder.LastBuiltShape;
        float halfW = w * 0.5f;
        float halfH = h * 0.5f;

        // Usable area: 1.5 inset from walls
        float minBound = 1.5f;
        float maxBoundX = w - 1.5f;
        float maxBoundZ = h - 1.5f;
        float midX = w * 0.5f;
        float midZ = h * 0.5f;

        // Split into 4 quadrants to force flanking
        float qMinX, qMaxX, qMinZ, qMaxZ;
        switch (side % 4)
        {
            case 0: qMinX = minBound; qMaxX = midX - 1f; qMinZ = minBound; qMaxZ = midZ - 1f; break;
            case 1: qMinX = midX + 1f; qMaxX = maxBoundX; qMinZ = midZ + 1f; qMaxZ = maxBoundZ; break;
            case 2: qMinX = midX + 1f; qMaxX = maxBoundX; qMinZ = minBound; qMaxZ = midZ - 1f; break;
            default: qMinX = minBound; qMaxX = midX - 1f; qMinZ = midZ + 1f; qMaxZ = maxBoundZ; break;
        }

        Vector3 pos;
        int safety = 50;
        bool valid;
        do {
            pos = new Vector3(Random.Range(qMinX, qMaxX), 0, Random.Range(qMinZ, qMaxZ));
            int tileX = Mathf.FloorToInt(pos.x);
            int tileZ = Mathf.FloorToInt(pos.z);
            valid = RoomBuilder.ShouldPlaceTile(shape, tileX, tileZ, w, h, halfW, halfH)
                    && Vector3.Distance(pos, player.transform.position) >= 4f;
            safety--;
        } while (!valid && safety > 0);

        // Fallback: if quadrant has no valid tiles, try any valid tile in the room
        if (!valid)
        {
            safety = 50;
            do {
                pos = new Vector3(Random.Range(minBound, maxBoundX), 0, Random.Range(minBound, maxBoundZ));
                int tileX = Mathf.FloorToInt(pos.x);
                int tileZ = Mathf.FloorToInt(pos.z);
                valid = RoomBuilder.ShouldPlaceTile(shape, tileX, tileZ, w, h, halfW, halfH)
                        && Vector3.Distance(pos, player.transform.position) >= 3f;
                safety--;
            } while (!valid && safety > 0);
        }

        return pos;
    }

    void RegisterEnemy(GameObject enemy, int hp)
    {
        enemy.transform.position = RandomSpawnPos();
        var rb = enemy.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;

        // Ascension scaling
        int scaledHP = Mathf.CeilToInt(hp * AscensionSystem.EnemyHPMultiplier);

        // Cursed Gold relic: enemies +30% HP
        if (relicMgr != null && relicMgr.HasRelic(RelicType.CursedGold))
            scaledHP = Mathf.CeilToInt(scaledHP * 1.3f);

        var health = enemy.AddComponent<Health>(); health.maxHP = scaledHP; health.currentHP = scaledHP;
        health.ApplyStun(1.2f); // spawn grace period — enemies wait before attacking
        enemy.AddComponent<EnemyHealthBar>();
        var enemyRef = enemy;
        health.OnDeath += () => OnEnemyDeath(enemyRef);
        enemies.Add(enemy);

        // Elite affixes on floor 3+
        float extraEliteChance = AscensionSystem.ExtraEliteChance;
        AffixType affix = EnemyAffix.RollAffix(currentFloor);
        if (affix == AffixType.None && extraEliteChance > 0 && Random.value < extraEliteChance)
            affix = EnemyAffix.RollAffix(5); // Force a roll as if floor 5

        if (affix != AffixType.None)
        {
            var affixComp = enemy.AddComponent<EnemyAffix>();
            affixComp.Init(affix);
        }

        // Element weakness system
        var elemData = enemy.AddComponent<EnemyElementData>();
        elemData.AssignRandomWeakness(baseElements);

        // Elemental status system
        enemy.AddComponent<ElementalStatus>();
        enemy.AddComponent<ElementalStatusVisuals>();

        // Track damage numbers
        if (hud != null) hud.TrackEnemyDamage(health);
    }

    static void AddCapsuleCol(GameObject go, float h, float r, float cy)
    {
        var c = go.AddComponent<CapsuleCollider>(); c.height = h; c.radius = r; c.center = new Vector3(0, cy, 0);
    }

    void BuildBody(Transform p, Color c, float s)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body"; Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = p; body.transform.localPosition = new Vector3(0, 0.45f*s, 0);
        body.transform.localScale = new Vector3(0.6f*s, 0.35f*s, 0.55f*s);
        body.GetComponent<Renderer>().material = MakeLit(c);
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head"; Destroy(head.GetComponent<SphereCollider>());
        head.transform.parent = p; head.transform.localPosition = new Vector3(0, 0.9f*s, 0.05f);
        head.transform.localScale = new Vector3(0.4f*s, 0.35f*s, 0.4f*s);
        head.GetComponent<Renderer>().material = MakeLit(c * 0.8f);
        CreateEye(p, new Vector3(-0.1f*s, 0.95f*s, 0.18f*s), new Color(1f, 0.3f, 0.1f));
        CreateEye(p, new Vector3(0.1f*s, 0.95f*s, 0.18f*s), new Color(1f, 0.3f, 0.1f));
        CreateArm(p, new Vector3(-0.35f*s, 0.45f*s, 0.1f), c * 0.9f);
        CreateArm(p, new Vector3(0.35f*s, 0.45f*s, 0.1f), c * 0.9f);
    }

    void SpawnShambler()
    {
        var e = new GameObject("Shambler"); Color c = new(0.75f, 0.15f, 0.15f);
        AddCapsuleCol(e, 1.2f, 0.35f, 0.6f);
        // Hunched body - heavily tilted torso, gorilla-like posture
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body"; Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = e.transform; body.transform.localPosition = new Vector3(0, 0.35f, 0.15f);
        body.transform.localScale = new Vector3(0.55f, 0.32f, 0.5f);
        body.transform.localRotation = Quaternion.Euler(25, 0, 0); // heavily hunched forward
        body.GetComponent<Renderer>().material = MakeLit(c);
        // Small head tucked very low and forward — almost touching ground
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head"; Destroy(head.GetComponent<SphereCollider>());
        head.transform.parent = e.transform; head.transform.localPosition = new Vector3(0, 0.7f, 0.25f);
        head.transform.localScale = new Vector3(0.3f, 0.25f, 0.28f);
        head.GetComponent<Renderer>().material = MakeLit(c * 0.8f);
        CreateEye(e.transform, new Vector3(-0.08f, 0.75f, 0.38f), new Color(1f, 0.3f, 0.1f));
        CreateEye(e.transform, new Vector3(0.08f, 0.75f, 0.38f), new Color(1f, 0.3f, 0.1f));
        // Asymmetric arms — one dragging on ground (gorilla), one shorter
        CreateArm(e.transform, new Vector3(-0.32f, 0.3f, 0.15f), c * 0.9f);
        var longArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        longArm.name = "LongArm"; Destroy(longArm.GetComponent<BoxCollider>());
        longArm.transform.parent = e.transform; longArm.transform.localPosition = new Vector3(0.32f, 0.2f, 0.2f);
        longArm.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
        longArm.GetComponent<Renderer>().material = MakeLit(c * 0.9f);
        // Spines on back — distinct silhouette feature
        for (int i = 0; i < 3; i++)
        {
            var spine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spine.name = "Spine"; Destroy(spine.GetComponent<BoxCollider>());
            spine.transform.parent = e.transform;
            spine.transform.localPosition = new Vector3(0, 0.5f + i * 0.12f, -0.1f - i * 0.04f);
            spine.transform.localScale = new Vector3(0.06f, 0.15f, 0.04f);
            spine.transform.localRotation = Quaternion.Euler(-30, 0, 0);
            spine.GetComponent<Renderer>().material = MakeLit(c * 0.6f);
        }
        RegisterEnemy(e, 12 + wave * 3);
        var ai = e.AddComponent<ShamblerAI>(); ai.moveSpeed = 2.5f + wave * 0.15f; ai.baseColor = c; ai.floorLevel = currentFloor;
    }

    void SpawnArcher()
    {
        var e = new GameObject("Archer"); Color c = new(0.2f, 0.6f, 0.2f);
        AddCapsuleCol(e, 1.2f, 0.25f, 0.6f);
        // Tall, thin body — slender silhouette distinct from BuildBody capsule
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body"; Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = e.transform; body.transform.localPosition = new Vector3(0, 0.45f, 0);
        body.transform.localScale = new Vector3(0.35f, 0.38f, 0.3f);
        body.GetComponent<Renderer>().material = MakeLit(c);
        // Small hooded head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head"; Destroy(head.GetComponent<SphereCollider>());
        head.transform.parent = e.transform; head.transform.localPosition = new Vector3(0, 0.9f, 0.05f);
        head.transform.localScale = new Vector3(0.28f, 0.26f, 0.3f);
        head.GetComponent<Renderer>().material = MakeLit(c * 0.6f);
        CreateEye(e.transform, new Vector3(-0.07f, 0.93f, 0.14f), new Color(1f, 0.3f, 0.1f));
        CreateEye(e.transform, new Vector3(0.07f, 0.93f, 0.14f), new Color(1f, 0.3f, 0.1f));
        // Bow arm — tall curved look
        var bow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bow.name = "Bow"; Destroy(bow.GetComponent<CapsuleCollider>());
        bow.transform.parent = e.transform; bow.transform.localPosition = new Vector3(0.28f, 0.6f, 0.15f);
        bow.transform.localScale = new Vector3(0.04f, 0.3f, 0.04f);
        bow.transform.localRotation = Quaternion.Euler(0, 0, -30);
        bow.GetComponent<Renderer>().material = MakeLit(new Color(0.4f, 0.25f, 0.1f));
        // Quiver on back
        var quiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
        quiver.name = "Quiver"; Destroy(quiver.GetComponent<BoxCollider>());
        quiver.transform.parent = e.transform; quiver.transform.localPosition = new Vector3(-0.15f, 0.55f, -0.15f);
        quiver.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
        quiver.transform.localRotation = Quaternion.Euler(-15, 0, 0);
        quiver.GetComponent<Renderer>().material = MakeLit(new Color(0.4f, 0.25f, 0.1f));
        RegisterEnemy(e, 10 + wave * 2);
        var archerAI = e.AddComponent<ArcherAI>(); archerAI.baseColor = c; archerAI.floorLevel = currentFloor;
    }

    void SpawnBrute()
    {
        var e = new GameObject("Brute"); Color c = new(0.5f, 0.2f, 0.15f);
        AddCapsuleCol(e, 1.6f, 0.5f, 0.8f);
        // Wide body
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body"; Destroy(body.GetComponent<BoxCollider>());
        body.transform.parent = e.transform; body.transform.localPosition = new Vector3(0, 0.6f, 0);
        body.transform.localScale = new Vector3(0.9f, 0.5f, 0.6f);
        body.GetComponent<Renderer>().material = MakeLit(c);
        // Helmet
        var helmet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        helmet.name = "Helmet"; Destroy(helmet.GetComponent<BoxCollider>());
        helmet.transform.parent = e.transform; helmet.transform.localPosition = new Vector3(0, 1.15f, 0);
        helmet.transform.localScale = new Vector3(0.45f, 0.35f, 0.4f);
        helmet.GetComponent<Renderer>().material = MakeLit(c * 0.7f);
        // Big arm cubes
        var armL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armL.name = "ArmL"; Destroy(armL.GetComponent<BoxCollider>());
        armL.transform.parent = e.transform; armL.transform.localPosition = new Vector3(-0.55f, 0.5f, 0.1f);
        armL.transform.localScale = new Vector3(0.22f, 0.55f, 0.2f);
        armL.GetComponent<Renderer>().material = MakeLit(c * 0.85f);
        var armR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armR.name = "ArmR"; Destroy(armR.GetComponent<BoxCollider>());
        armR.transform.parent = e.transform; armR.transform.localPosition = new Vector3(0.55f, 0.5f, 0.1f);
        armR.transform.localScale = new Vector3(0.22f, 0.55f, 0.2f);
        armR.GetComponent<Renderer>().material = MakeLit(c * 0.85f);
        CreateEye(e.transform, new Vector3(-0.12f, 1.2f, 0.18f), new Color(1f, 0.3f, 0.1f));
        CreateEye(e.transform, new Vector3(0.12f, 1.2f, 0.18f), new Color(1f, 0.3f, 0.1f));
        RegisterEnemy(e, 30 + wave * 8);
        e.AddComponent<BruteAI>().baseColor = c;
    }

    void SpawnSwarmUnit()
    {
        var e = new GameObject("Swarm"); Color c = new(0.6f, 0.5f, 0.1f);
        var sc = e.AddComponent<SphereCollider>(); sc.radius = 0.2f; sc.center = new Vector3(0, 0.2f, 0);
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body"; Destroy(body.GetComponent<SphereCollider>());
        body.transform.parent = e.transform; body.transform.localPosition = new Vector3(0, 0.2f, 0);
        body.transform.localScale = Vector3.one * 0.22f;
        body.GetComponent<Renderer>().material = MakeLit(c);
        // Orbiting mini-spheres
        for (int i = 0; i < 3; i++)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "SwarmOrb"; Destroy(orb.GetComponent<SphereCollider>());
            orb.transform.parent = e.transform;
            float angle = i * 120f * Mathf.Deg2Rad;
            orb.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.18f, 0.2f, Mathf.Sin(angle) * 0.18f);
            orb.transform.localScale = Vector3.one * 0.07f;
            orb.GetComponent<Renderer>().material = ShaderCache.NewEmissive(c * 1.5f, 2f);
        }
        CreateEye(e.transform, new Vector3(0, 0.3f, 0.1f), new Color(1f, 0.8f, 0.1f));
        RegisterEnemy(e, 3 + wave);
        e.AddComponent<SwarmAI>().baseColor = c;
    }

    void SpawnMirror()
    {
        var e = new GameObject("Mirror"); Color c = new(0.7f, 0.7f, 0.75f);
        AddCapsuleCol(e, 1.2f, 0.35f, 0.6f);
        // Diamond-shaped body (rotated cube)
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body"; Destroy(body.GetComponent<BoxCollider>());
        body.transform.parent = e.transform; body.transform.localPosition = new Vector3(0, 0.55f, 0);
        body.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
        body.transform.localRotation = Quaternion.Euler(0, 45, 45); // diamond rotation
        body.GetComponent<Renderer>().material = ShaderCache.NewMetal(c);
        // Reflective head prism
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head"; Destroy(head.GetComponent<BoxCollider>());
        head.transform.parent = e.transform; head.transform.localPosition = new Vector3(0, 0.95f, 0);
        head.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        head.transform.localRotation = Quaternion.Euler(0, 45, 0);
        head.GetComponent<Renderer>().material = ShaderCache.NewMetal(new Color(0.85f, 0.85f, 0.9f));
        CreateEye(e.transform, new Vector3(-0.08f, 1.0f, 0.12f), new Color(0.8f, 0.8f, 1f));
        CreateEye(e.transform, new Vector3(0.08f, 1.0f, 0.12f), new Color(0.8f, 0.8f, 1f));
        RegisterEnemy(e, 18 + wave * 3);
        e.AddComponent<MirrorAI>().baseColor = c;
    }

    void SpawnShieldBearer()
    {
        var e = new GameObject("ShieldBearer"); Color c = new(0.3f, 0.35f, 0.5f);
        AddCapsuleCol(e, 1.5f, 0.45f, 0.75f);
        // Stocky wide body — wider than Brute, shorter
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body"; Destroy(body.GetComponent<BoxCollider>());
        body.transform.parent = e.transform; body.transform.localPosition = new Vector3(0, 0.55f, 0);
        body.transform.localScale = new Vector3(0.7f, 0.55f, 0.5f);
        body.GetComponent<Renderer>().material = MakeLit(c);
        // Flat helmet
        var helmet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        helmet.name = "Helmet"; Destroy(helmet.GetComponent<BoxCollider>());
        helmet.transform.parent = e.transform; helmet.transform.localPosition = new Vector3(0, 1.0f, 0);
        helmet.transform.localScale = new Vector3(0.4f, 0.2f, 0.38f);
        helmet.GetComponent<Renderer>().material = ShaderCache.NewMetal(c * 0.7f);
        CreateEye(e.transform, new Vector3(-0.1f, 1.0f, 0.17f), new Color(0.3f, 0.5f, 1f));
        CreateEye(e.transform, new Vector3(0.1f, 1.0f, 0.17f), new Color(0.3f, 0.5f, 1f));
        // Large shield — prominent front feature
        var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shield.name = "Shield"; Destroy(shield.GetComponent<BoxCollider>());
        shield.transform.parent = e.transform; shield.transform.localPosition = new Vector3(0, 0.55f, 0.4f);
        shield.transform.localScale = new Vector3(0.75f, 0.8f, 0.08f);
        shield.GetComponent<Renderer>().material = ShaderCache.NewMetal(new Color(0.5f, 0.5f, 0.65f));
        // Shield boss (center decoration)
        var boss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        boss.name = "ShieldBoss"; Destroy(boss.GetComponent<SphereCollider>());
        boss.transform.parent = e.transform; boss.transform.localPosition = new Vector3(0, 0.55f, 0.46f);
        boss.transform.localScale = Vector3.one * 0.12f;
        boss.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.4f, 0.5f, 1f), 2f);
        RegisterEnemy(e, 22 + wave * 3);
        e.AddComponent<ShieldBearerAI>().baseColor = c;
    }

    void SpawnHealer()
    {
        var e = new GameObject("Healer"); Color c = new(0.2f, 0.8f, 0.3f);
        AddCapsuleCol(e, 1.1f, 0.3f, 0.55f);
        // Robed body (cylinder)
        var robe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        robe.name = "Robe"; Destroy(robe.GetComponent<CapsuleCollider>());
        robe.transform.parent = e.transform; robe.transform.localPosition = new Vector3(0, 0.35f, 0);
        robe.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
        robe.GetComponent<Renderer>().material = MakeLit(c * 0.7f);
        // Hooded head
        var hood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hood.name = "Hood"; Destroy(hood.GetComponent<SphereCollider>());
        hood.transform.parent = e.transform; hood.transform.localPosition = new Vector3(0, 0.8f, 0);
        hood.transform.localScale = new Vector3(0.3f, 0.28f, 0.32f);
        hood.GetComponent<Renderer>().material = MakeLit(c * 0.5f);
        // Glowing healing orb above head
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "HealOrb"; Destroy(orb.GetComponent<SphereCollider>());
        orb.transform.parent = e.transform; orb.transform.localPosition = new Vector3(0, 1.15f, 0);
        orb.transform.localScale = Vector3.one * 0.15f;
        orb.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 1f, 0.4f), 4f);
        // Staff
        var staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        staff.name = "Staff"; Destroy(staff.GetComponent<CapsuleCollider>());
        staff.transform.parent = e.transform; staff.transform.localPosition = new Vector3(0.25f, 0.5f, 0.1f);
        staff.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
        staff.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.3f, 1f, 0.4f), 2f);
        CreateEye(e.transform, new Vector3(-0.06f, 0.83f, 0.14f), new Color(0.3f, 1f, 0.4f));
        CreateEye(e.transform, new Vector3(0.06f, 0.83f, 0.14f), new Color(0.3f, 1f, 0.4f));
        RegisterEnemy(e, 8 + wave * 2);
        e.AddComponent<HealerAI>().baseColor = c;
    }

    void SpawnBuffer()
    {
        var e = new GameObject("Buffer"); Color c = new(0.9f, 0.55f, 0.1f);
        AddCapsuleCol(e, 1.3f, 0.38f, 0.63f);
        // Drum-shaped body (wide cylinder)
        var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        drum.name = "Drum"; Destroy(drum.GetComponent<CapsuleCollider>());
        drum.transform.parent = e.transform; drum.transform.localPosition = new Vector3(0, 0.4f, 0);
        drum.transform.localScale = new Vector3(0.55f, 0.3f, 0.55f);
        drum.GetComponent<Renderer>().material = MakeLit(c);
        // Symbol disc on top
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Symbol"; Destroy(disc.GetComponent<CapsuleCollider>());
        disc.transform.parent = e.transform; disc.transform.localPosition = new Vector3(0, 0.72f, 0);
        disc.transform.localScale = new Vector3(0.35f, 0.02f, 0.35f);
        disc.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.8f, 0.1f), 3f);
        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head"; Destroy(head.GetComponent<SphereCollider>());
        head.transform.parent = e.transform; head.transform.localPosition = new Vector3(0, 0.95f, 0.05f);
        head.transform.localScale = new Vector3(0.32f, 0.3f, 0.32f);
        head.GetComponent<Renderer>().material = MakeLit(c * 0.8f);
        CreateEye(e.transform, new Vector3(-0.08f, 1.0f, 0.15f), new Color(1f, 0.8f, 0.1f));
        CreateEye(e.transform, new Vector3(0.08f, 1.0f, 0.15f), new Color(1f, 0.8f, 0.1f));
        // War horn
        var horn = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        horn.name = "Horn"; Destroy(horn.GetComponent<CapsuleCollider>());
        horn.transform.parent = e.transform; horn.transform.localPosition = new Vector3(0.3f, 0.6f, 0.2f);
        horn.transform.localScale = new Vector3(0.06f, 0.15f, 0.06f);
        horn.transform.localRotation = Quaternion.Euler(0, 0, -45);
        horn.GetComponent<Renderer>().material = MakeLit(new Color(0.7f, 0.5f, 0.1f));
        RegisterEnemy(e, 15 + wave * 2);
        e.AddComponent<BufferAI>().baseColor = c;
    }

    static void CreateEye(Transform parent, Vector3 localPos, Color color)
    {
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        Destroy(eye.GetComponent<SphereCollider>());
        eye.transform.parent = parent;
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.08f;
        eye.GetComponent<Renderer>().material = ShaderCache.NewMagic(color, 3f);
    }

    static void CreateArm(Transform parent, Vector3 localPos, Color color)
    {
        var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm.name = "Arm";
        Destroy(arm.GetComponent<BoxCollider>());
        arm.transform.parent = parent;
        arm.transform.localPosition = localPos;
        arm.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
        arm.transform.localRotation = Quaternion.Euler(0, 0, 0);
        arm.GetComponent<Renderer>().material = MakeLit(color);
    }

    // ─── BOSSES ─────────────────────────────────────────────────

    void SpawnBossWave(int bossIndex)
    {
        bossActive = true;
        enemiesAlive = 1;
        switch (bossIndex)
        {
            case 1: SpawnWarden(); break;
            case 2: SpawnSwarmQueen(); break;
            case 3: SpawnMirrorKnight(); break;
            case 4: SpawnLich(); break;
            default: SpawnRunebreaker(); break;
        }
        if (currentBoss != null)
        {
            var bossHP = currentBoss.GetComponent<Health>();
            if (bossHP != null)
            {
                hud.ShowBossHP(bossHP, GetBossName(bossIndex));
                hud.TrackEnemyDamage(bossHP);
            }
        }
    }

    string GetBossName(int idx) => idx switch
    {
        1 => "THE WARDEN",
        2 => "THE SWARM QUEEN",
        3 => "THE MIRROR KNIGHT",
        4 => "THE LICH",
        _ => "THE RUNEBREAKER"
    };

    void SpawnWarden()
    {
        var e = new GameObject("WardenBoss");
        Color c = new(0.4f, 0.4f, 0.35f);
        AddCapsuleCol(e, 3.2f, 0.9f, 1.6f);
        BuildBody(e.transform, c, 2.5f);
        e.transform.position = new Vector3(6, 0, 10);
        var rb = e.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
        var hp = e.AddComponent<Health>(); hp.maxHP = 320; hp.currentHP = 320;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<ElementalStatus>();
        e.AddComponent<ElementalStatusVisuals>();
        e.AddComponent<WardenBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnSwarmQueen()
    {
        var e = new GameObject("SwarmQueenBoss");
        Color c = new(0.7f, 0.5f, 0.1f);
        AddCapsuleCol(e, 3.2f, 0.8f, 1.6f);
        BuildBody(e.transform, c, 2.5f);
        // Crown spikes
        for (int i = 0; i < 4; i++)
        {
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spike.name = "Crown"; Destroy(spike.GetComponent<CapsuleCollider>());
            spike.transform.parent = e.transform;
            float angle = i * 90f * Mathf.Deg2Rad;
            spike.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.15f, 1.35f, Mathf.Sin(angle) * 0.15f);
            spike.transform.localScale = new Vector3(0.04f, 0.12f, 0.04f);
            spike.GetComponent<Renderer>().material = MakeEmissive(new Color(1f, 0.8f, 0.1f));
        }
        e.transform.position = new Vector3(6, 0, 10);
        var rb = e.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
        var hp = e.AddComponent<Health>(); hp.maxHP = 440; hp.currentHP = 440;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<ElementalStatus>();
        e.AddComponent<ElementalStatusVisuals>();
        e.AddComponent<SwarmQueenBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnMirrorKnight()
    {
        var e = new GameObject("MirrorKnightBoss");
        Color c = new(0.6f, 0.6f, 0.7f);
        AddCapsuleCol(e, 3.2f, 0.8f, 1.6f);
        BuildBody(e.transform, c, 2.5f);
        // Sword
        var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sword.name = "Sword"; Destroy(sword.GetComponent<BoxCollider>());
        sword.transform.parent = e.transform;
        sword.transform.localPosition = new Vector3(0.4f, 0.7f, 0.2f);
        sword.transform.localScale = new Vector3(0.06f, 0.5f, 0.06f);
        sword.transform.localRotation = Quaternion.Euler(15, 0, -20);
        sword.GetComponent<Renderer>().material = MakeLit(new Color(0.8f, 0.8f, 0.9f));
        // Shield visual
        var shieldVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shieldVis.name = "ShieldVis"; Destroy(shieldVis.GetComponent<BoxCollider>());
        shieldVis.transform.parent = e.transform;
        shieldVis.transform.localPosition = new Vector3(-0.35f, 0.6f, 0.25f);
        shieldVis.transform.localScale = new Vector3(0.5f, 0.6f, 0.08f);
        shieldVis.GetComponent<Renderer>().material = MakeEmissive(new Color(0.5f, 0.5f, 0.8f));
        e.transform.position = new Vector3(6, 0, 10);
        var rb = e.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
        var hp = e.AddComponent<Health>(); hp.maxHP = 560; hp.currentHP = 560;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<ElementalStatus>();
        e.AddComponent<ElementalStatusVisuals>();
        e.AddComponent<MirrorKnightBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnLich()
    {
        var e = new GameObject("LichBoss");
        Color c = new(0.3f, 0.1f, 0.5f);
        AddCapsuleCol(e, 3.2f, 0.75f, 1.6f);
        BuildBody(e.transform, c, 2.5f);
        // Glowing staff
        var staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        staff.name = "Staff"; Destroy(staff.GetComponent<CapsuleCollider>());
        staff.transform.parent = e.transform;
        staff.transform.localPosition = new Vector3(0.3f, 0.8f, 0.15f);
        staff.transform.localScale = new Vector3(0.05f, 0.45f, 0.05f);
        staff.GetComponent<Renderer>().material = MakeLit(new Color(0.2f, 0.05f, 0.3f));
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "StaffTip"; Destroy(tip.GetComponent<SphereCollider>());
        tip.transform.parent = staff.transform;
        tip.transform.localPosition = new Vector3(0, 1.1f, 0);
        tip.transform.localScale = new Vector3(2.5f, 0.4f, 2.5f);
        tip.GetComponent<Renderer>().material = MakeEmissive(new Color(0.6f, 0f, 1f));
        e.transform.position = new Vector3(6, 0, 10);
        var rb = e.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
        var hp = e.AddComponent<Health>(); hp.maxHP = 720; hp.currentHP = 720;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<ElementalStatus>();
        e.AddComponent<ElementalStatusVisuals>();
        e.AddComponent<LichBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnRunebreaker()
    {
        var e = new GameObject("RunebreakerBoss");
        Color c = new(0.6f, 0.1f, 0.1f);
        AddCapsuleCol(e, 3.5f, 0.95f, 1.75f);
        BuildBody(e.transform, c, 2.7f);
        // Rune markings (glowing cubes on body)
        for (int i = 0; i < 3; i++)
        {
            var rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rune.name = "RuneMark"; Destroy(rune.GetComponent<BoxCollider>());
            rune.transform.parent = e.transform;
            rune.transform.localPosition = new Vector3(0, 0.6f + i * 0.3f, 0.3f);
            rune.transform.localScale = new Vector3(0.15f, 0.08f, 0.02f);
            rune.GetComponent<Renderer>().material = MakeEmissive(new Color(1f, 0.2f, 0.1f));
        }
        e.transform.position = new Vector3(6, 0, 10);
        var rb = e.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
        var hp = e.AddComponent<Health>(); hp.maxHP = 880; hp.currentHP = 880;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<ElementalStatus>();
        e.AddComponent<ElementalStatusVisuals>();
        e.AddComponent<RunebreakerBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void OnBossDeath(GameObject boss)
    {
        currentBoss = null;
        hud.HideBossHP();
        enemies.Remove(boss);
        enemiesAlive = 0;
        MetaProgression.AwardBossCurrency(currentFloor);
        MetaProgression.RecordFloor(currentFloor);

        // Boss gold drop (CursedGold: 2x)
        int goldDrop = GoldSystem.CalculateEnemyDrop(wave, true, currentFloor);
        if (relicMgr != null && relicMgr.HasRelic(RelicType.CursedGold))
            goldDrop *= 2;
        GoldSystem.SpawnGoldDrop(boss.transform.position, goldDrop);

        // Post-boss victory splash
        string bossName = GetBossName(currentFloor);
        hud.ShowBossVictorySplash(bossName, playerHealth.currentHP, playerHealth.maxHP);

        // bossActive stays true so ShowRuneSelection knows to advance floor
        SpawnRewardPickup();
    }

    // Called by SwarmQueenBoss for minion tracking
    public void OnMinionDeath(GameObject minion)
    {
        // Minion deaths don't count toward wave clearing while boss is alive
        if (bossActive) return;
        enemies.Remove(minion);
        enemiesAlive--;
        if (enemiesAlive <= 0) SpawnRewardPickup();
    }

    // ─── REWARD PICKUP ──────────────────────────────────────────

    void SpawnRewardPickup()
    {
        roomCleared = true;
        combatPressureActive = false;
        ClearPressurePortals();

        // Spawn glowing pickup in center of room
        rewardPickup = new GameObject("RewardPickup");
        var center = new Vector3(6, 0, 6);
        if (currentRoomGO != null)
            center = currentRoomGO.transform.position + new Vector3(6, 0, 6);
        rewardPickup.transform.position = center;

        // Visual: glowing orb floating above ground
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(orb.GetComponent<SphereCollider>());
        orb.name = "RewardOrb";
        orb.transform.parent = rewardPickup.transform;
        orb.transform.localPosition = new Vector3(0, 0.8f, 0);
        orb.transform.localScale = Vector3.one * 0.4f;

        // Color based on what type of reward
        Color orbColor;
        if (currentRoom % 3 == 0 && !bossActive)
            orbColor = new Color(1f, 0.85f, 0.2f); // relic = gold
        else
            orbColor = new Color(0.3f, 0.6f, 1f); // rune = blue

        orb.GetComponent<Renderer>().material = MakeEmissive(orbColor);

        // Pillar of light
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(beam.GetComponent<CapsuleCollider>());
        beam.name = "RewardBeam";
        beam.transform.parent = rewardPickup.transform;
        beam.transform.localPosition = new Vector3(0, 2f, 0);
        beam.transform.localScale = new Vector3(0.15f, 2f, 0.15f);
        Color beamCol = orbColor; beamCol.a = 0.4f;
        var beamMat = ShaderCache.NewEmissive(beamCol, 2f);
        beam.GetComponent<Renderer>().material = beamMat;

        // Trigger collider for pickup
        var col = rewardPickup.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.2f;
        col.center = new Vector3(0, 0.5f, 0);

        var pickup = rewardPickup.AddComponent<RewardPickup>();
        pickup.onPickedUp = OnRewardPickedUp;

        // Float animation
        rewardPickup.AddComponent<FloatBob>();
    }

    void OnRewardPickedUp()
    {
        if (rewardPickup != null)
        {
            Destroy(rewardPickup);
            rewardPickup = null;
        }

        // Lucky Find: chance to grant a bonus relic on room clear
        if (MetaProgression.LuckyFindChance > 0 && Random.value < MetaProgression.LuckyFindChance
            && relicMgr != null && allRelics != null)
        {
            var candidates = new System.Collections.Generic.List<RelicSO>();
            foreach (var r in allRelics)
                if (!r.isCursed && !relicMgr.HasRelic(r.relicType)) candidates.Add(r);
            if (candidates.Count > 0)
            {
                relicMgr.AddRelic(candidates[Random.Range(0, candidates.Count)]);
                if (hud != null) hud.RefreshRelics(relicMgr.OwnedRelics);
            }
        }

        ShowRuneSelection();
    }

    // ─── GAME EVENTS ──────────────────────────────────────────────

    void OnEnemyDeath(GameObject enemy)
    {
        // Gold drop (CursedGold: 2x)
        int goldDrop = GoldSystem.CalculateEnemyDrop(wave, false, currentFloor);
        if (relicMgr != null && relicMgr.HasRelic(RelicType.CursedGold))
            goldDrop *= 2;
        GoldSystem.SpawnGoldDrop(enemy.transform.position, goldDrop);

        // Synergy effects on enemy death
        if (synergySystem != null && spellCaster != null)
        {
            Vector3 deathPos = enemy.transform.position;
            var killElem = spellCaster.rightOrb != null ? spellCaster.rightOrb.elementType : ElementType.Fire;
            synergySystem.OnEnemyKilled(deathPos, killElem);
        }

        // Momentum system: track kill streak
        var momentum = player != null ? player.GetComponent<MomentumSystem>() : null;
        if (momentum != null) momentum.OnEnemyKilled();

        // Blood Mage: chance to heal 1 HP on kill
        if (MetaProgression.BloodMageChance > 0 && playerHealth != null && !playerHealth.IsDead)
        {
            if (Random.value < MetaProgression.BloodMageChance)
                playerHealth.Heal(1);
        }

        enemies.Remove(enemy);
        enemiesAlive--;
        enemiesKilledThisRun++;

        // Spell mutation: Vampiric heal + Volatile explosion
        if (SpellMutationSystem.OnSpellKill(enemy.transform.position))
        {
            if (playerHealth != null && !playerHealth.IsDead)
                playerHealth.Heal(1);
        }

        // Siphon Shield upgrade: kills grant 1s immunity
        if (RunUpgradeSystem.HasSiphon && playerHealth != null && !playerHealth.IsDead)
        {
            playerHealth.GrantImmunity(1f);
        }

        if (enemiesAlive <= 0)
        {
            // Challenge room: wave-by-wave
            if (isChallenge)
            {
                OnChallengeWaveComplete();
                return;
            }

            // Check for more sub-waves
            if (subWave < totalSubWaves && !bossActive)
            {
                int baseBudget = currentFloor switch { 1 => 10, 2 => 18, 3 => 28, 4 => 40, _ => 55 };
                float budgetMult = currentNodeType == NodeType.EliteCombat ? 1.5f : 1f;
                int totalBudget = Mathf.CeilToInt((baseBudget + (currentRoom - 1) * 2) * budgetMult);
                int waveBudget = totalBudget / totalSubWaves;
                // Brief delay before reinforcements
                reinforcementTimer = 1.5f;
                return;
            }
            SpawnRewardPickup();
        }
    }

    void ShowRuneSelection()
    {
        roomCleared = true;

        // Treasure rooms: always offer relic
        if (currentNodeType == NodeType.Treasure)
        {
            ShowRelicSelection();
            return;
        }

        // Every 3rd room: offer relic choice instead of upgrade
        if (currentRoom % 3 == 0 && !bossActive)
        {
            ShowRelicSelection();
            return;
        }

        // Brief slow-motion when upgrade appears
        Time.timeScale = 0.15f;

        // Dead Cells style: choose one of three stat upgrades
        var choices = RunUpgradeSystem.GenerateChoices(3);

        hud.ShowUpgradeSelection(choices, idx =>
        {
            Time.timeScale = 1f;
            RunUpgradeSystem.ApplyUpgrade(spellCaster, choices[idx]);
            SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);

            // Timed challenge: award bonus if completed in time
            if (currentEncounter != null &&
                currentEncounter.Type == EncounterSystem.EncounterType.TimedChallenge &&
                !currentEncounter.BonusAwarded && currentEncounter.Timer > 0)
            {
                currentEncounter.BonusAwarded = true;
                if (goldSystem != null) goldSystem.AddGold(currentEncounter.BonusGold);
                hud.SetObjective($"BONUS: +{currentEncounter.BonusGold} gold!");
            }

            if (bossActive)
            {
                // After boss: also offer synergy + element unlock
                bossActive = false;
                ShowPostBossRewards();
                return;
            }
            hud.SetObjective("");
            TransitionToNextRoom();
        });
    }

    void ShowPostBossRewards()
    {
        // Offer synergy choice
        var allSyn = SynergySystem.AllSynergies;
        var available = new List<SynergyDef>();
        foreach (var s in allSyn)
            if (!synergySystem.HasSynergy(s.type)) available.Add(s);

        if (available.Count > 0)
        {
            int count = Mathf.Min(3, available.Count);
            var synChoices = new SynergyDef[count];
            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, available.Count);
                synChoices[i] = available[idx];
                available.RemoveAt(idx);
            }

            hud.ShowSynergySelection(synChoices, idx =>
            {
                synergySystem.AddSynergy(synChoices[idx].type);
                SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);

                // Then offer element unlock if advanced elements available
                ShowElementUnlockIfAvailable();
            });
        }
        else
        {
            ShowElementUnlockIfAvailable();
        }
    }

    void ShowElementUnlockIfAvailable()
    {
        // Check if there are unlockable elements not yet equipped
        ElementSO[] unlockable = { lightningElem, poisonElem, voidElem };
        ElementSO toUnlock = null;

        foreach (var elem in unlockable)
        {
            bool alreadyEquipped = false;
            for (int i = 0; i < 4; i++)
                if (spellCaster.equippedElements[i] == elem) { alreadyEquipped = true; break; }
            if (!alreadyEquipped) { toUnlock = elem; break; }
        }

        if (toUnlock != null)
        {
            hud.ShowElementUnlock(toUnlock, spellCaster.equippedElements, slotIdx =>
            {
                spellCaster.ReplaceElement(slotIdx, toUnlock);
                SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
                TransitionToNextRoom();
            });
        }
        else
        {
            TransitionToNextRoom();
        }
    }

    void ShowRelicSelection()
    {
        // Pick 3 random relics the player doesn't have yet
        var available = new List<RelicSO>();
        foreach (var r in allRelics)
            if (!relicMgr.HasRelic(r.relicType)) available.Add(r);

        if (available.Count == 0)
        {
            // All relics owned, show normal rune selection instead
            ShowRuneSelectionForce();
            return;
        }

        int count = Mathf.Min(3, available.Count);
        var options = new RelicSO[count];
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, available.Count);
            options[i] = available[idx];
            available.RemoveAt(idx);
        }

        hud.ShowRelicSelection(options, idx =>
        {
            relicMgr.AddRelic(options[idx]);
            hud.RefreshRelics(relicMgr.OwnedRelics);
            TransitionToNextRoom();
        });
    }

    // Fallback when all relics are owned — offer stat upgrades instead
    void ShowRuneSelectionForce()
    {
        var choices = RunUpgradeSystem.GenerateChoices(3);
        hud.ShowUpgradeSelection(choices, idx =>
        {
            RunUpgradeSystem.ApplyUpgrade(spellCaster, choices[idx]);
            SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
            TransitionToNextRoom();
        });
    }

    void OnPlayerDeath()
    {
        isPlayerDead = true;
        MetaProgression.RecordFloor(currentFloor);
        int metaReward = MetaProgression.AwardDeathCurrency(currentFloor, currentRoom, enemiesKilledThisRun);

        // Death VFX: scatter player model + screen effects
        GameFeel.PlayerDeathVFX(player.transform);

        // Brief slow-mo then show death screen
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.1f);

        int ownedRelicCount = relicMgr != null ? relicMgr.OwnedRelics.Count : 0;
        hud.ShowDeath(true, metaReward, currentFloor, currentRoom, enemiesKilledThisRun, ownedRelicCount);
        playerCtrl.enabled = false;
        spellCaster.enabled = false;

        // Hide player renderers
        foreach (var r in player.GetComponentsInChildren<Renderer>())
            if (r != null) r.enabled = false;
    }

    void ReturnToHub()
    {
        isPlayerDead = false;
        if (SFXSystem.Instance != null) SFXSystem.Instance.StopMusic();
        SpellInteractionSystem.OnReaction -= OnSpellReaction;
        ElementalStatus.OnReaction -= OnSpellReaction;
        CleanupRun();
        EnterHub();
    }

    // ─── SPELL REACTION HANDLER ──────────────────────────────────

    void OnSpellReaction(string name, Vector3 pos, Color color)
    {
        // Reaction popup label in HUD
        if (hud != null) hud.SpawnReactionLabel(name, pos, color);

        // Codex discovery
        Codex.DiscoverReaction(name, pos, color);

        // Shockwave ring at reaction point (localized, not full-screen)
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = pos + Vector3.up * 0.1f;
        ring.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 5f);
        ring.AddComponent<DeathRingEffect>().Init(4f, 0.3f);

        // Hit particles at reaction point
        GameFeel.SpawnHitParticles(pos + Vector3.up * 0.3f, color, 2f);

        // Sound
        SFXSystem.Play(SFXSystem.SFXType.Reaction, pos);
    }

    // ─── MATERIAL HELPERS ─────────────────────────────────────────

    static Material MakeLit(Color color)
    {
        return ShaderCache.NewLit(color);
    }

    static Material MakeEmissive(Color color)
    {
        return ShaderCache.NewEmissive(color);
    }
}
