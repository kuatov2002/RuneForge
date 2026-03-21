using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class Bootstrap : MonoBehaviour
{
    // Elements
    ElementSO fireElem, iceElem, lightningElem, poisonElem, voidElem;
    ElementSO[] allElements;

    // Forms
    FormSO boltForm, coneForm, beamForm, auraForm, orbitForm, trapForm;
    FormSO[] allForms;

    // Modifiers
    ModifierSO noneMod, splitMod, pierceMod, bounceMod, leechMod, oversizeMod, volatileMod, homingMod;
    ModifierSO[] allModifiers;

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
    enum NodeType { Combat, EliteCombat, Shop, Event, Rest, Boss }
    struct MapNode { public NodeType type; public int depth; }
    List<MapNode[]> floorMap; // floorMap[depth] = array of 2-3 node choices

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

        // Starting relic from meta-progression
        if (MetaProgression.HasStartingRelic && allRelics.Length > 0)
        {
            var randomRelic = allRelics[Random.Range(0, allRelics.Length)];
            if (!relicMgr.HasRelic(randomRelic.relicType))
            {
                relicMgr.AddRelic(randomRelic);
                hud.RefreshRelics(relicMgr.OwnedRelics);
            }
        }

        // Apply selected loadout (starting spell + passive)
        ApplyStartingLoadout();

        SpawnWave();
    }

    void ApplyMetaProgressionToPlayer()
    {
        // HP bonus
        int baseHP = 5 + MetaProgression.MaxHPBonus;
        playerHealth.maxHP = baseHP;
        playerHealth.currentHP = baseHP;

        // Speed bonus
        playerCtrl.moveSpeed = 6f * MetaProgression.SpeedMultiplier;

        // Extra dash charges
        playerCtrl.SetExtraDashCharges(MetaProgression.ExtraDashCharges);

        // Potions
        playerCtrl.SetPotions(MetaProgression.PotionsPerFloor);
    }

    void ApplyStartingLoadout()
    {
        var loadout = MetaProgression.GetSelectedLoadoutDef();
        if (loadout.startElement == null || loadout.startForm == null) return;

        // Find matching element and form
        ElementSO elem = null;
        foreach (var e in allElements) if (e.elementName == loadout.startElement) { elem = e; break; }
        FormSO form = null;
        foreach (var f in allForms) if (f.formName == loadout.startForm) { form = f; break; }
        if (elem == null || form == null) return;

        // Equip to slot 0
        spellCaster.spellSlots[0] = new SpellData { element = elem, form = form };

        // Apply passive bonus based on aspect
        switch (loadout.id)
        {
            case "pyromancer": // +10% burn damage handled via burn DPS scaling
                fireElem.statusDPS *= 1.1f;
                break;
            case "cryomancer": // +0.5s freeze duration
                iceElem.statusDuration += 0.5f;
                break;
            case "stormcaller": // +1 chain target
                lightningElem.chainCount += 1;
                break;
            case "plaguebringer": // +1 starting poison stack (enemies start with 1)
                poisonElem.baseDamage += 1;
                break;
            case "voidwalker": // -20% dash cooldown
                playerCtrl.dashCooldown *= 0.8f;
                break;
        }
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
    }

    void Update()
    {
        if (isPlayerDead)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                ReturnToHub();
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
    }

    // ─── SPELL DATA ───────────────────────────────────────────────

    void CreateSpellData()
    {
        // Elements
        fireElem = CreateElement("Fire", 10, StatusEffectType.Burn, 3f, 3f, new Color(1f, 0.4f, 0.1f));
        iceElem = CreateElement("Ice", 7, StatusEffectType.Slow, 0, 2f, new Color(0.3f, 0.7f, 1f));
        lightningElem = CreateElement("Lightning", 6, StatusEffectType.Chain, 0, 0.5f, new Color(1f, 1f, 0.3f));
        lightningElem.chainCount = 2;
        lightningElem.chainRadius = 4f;
        poisonElem = CreateElement("Poison", 4, StatusEffectType.Poison, 0, 0, new Color(0.2f, 0.9f, 0.1f));
        voidElem = CreateElement("Void", 8, StatusEffectType.VoidMark, 0, 2f, new Color(0.6f, 0.1f, 0.9f));
        allElements = new[] { fireElem, iceElem, lightningElem, poisonElem, voidElem };

        // Forms
        boltForm = CreateForm("Bolt", FormType.Bolt);
        boltForm.range = 15f; boltForm.projectileSpeed = 14f; boltForm.cooldown = 0.3f;

        coneForm = CreateForm("Cone", FormType.Cone);
        coneForm.coneAngle = 45f; coneForm.coneRange = 3f; coneForm.cooldown = 0.5f;

        beamForm = CreateForm("Beam", FormType.Beam);
        beamForm.beamRange = 20f; beamForm.beamWidth = 0.3f; beamForm.cooldown = 0.6f;

        auraForm = CreateForm("Aura", FormType.Aura);
        auraForm.auraRadius = 2.5f; auraForm.cooldown = 1f;

        orbitForm = CreateForm("Orbit", FormType.Orbit);
        orbitForm.orbitRadius = 1.8f; orbitForm.orbitSpeed = 250f;
        orbitForm.orbitCount = 3; orbitForm.orbitDuration = 4f; orbitForm.cooldown = 5f;

        trapForm = CreateForm("Trap", FormType.Trap);
        trapForm.trapRadius = 1.5f; trapForm.trapArmTime = 0.5f; trapForm.cooldown = 0.8f;

        allForms = new[] { boltForm, coneForm, beamForm, auraForm, orbitForm, trapForm };

        // Modifiers
        noneMod = ScriptableObject.CreateInstance<ModifierSO>();
        noneMod.modifierName = "None"; noneMod.modifierType = ModifierType.None; noneMod.damageMultiplier = 1f;

        splitMod = ScriptableObject.CreateInstance<ModifierSO>();
        splitMod.modifierName = "Split"; splitMod.modifierType = ModifierType.Split;
        splitMod.damageMultiplier = 0.6f; splitMod.splitCount = 3; splitMod.splitSpreadAngle = 15f;

        pierceMod = ScriptableObject.CreateInstance<ModifierSO>();
        pierceMod.modifierName = "Pierce"; pierceMod.modifierType = ModifierType.Pierce;
        pierceMod.damageMultiplier = 1f; pierceMod.pierceCount = 5;

        bounceMod = ScriptableObject.CreateInstance<ModifierSO>();
        bounceMod.modifierName = "Bounce"; bounceMod.modifierType = ModifierType.Bounce;
        bounceMod.damageMultiplier = 1f; bounceMod.bounceCount = 3;

        leechMod = ScriptableObject.CreateInstance<ModifierSO>();
        leechMod.modifierName = "Leech"; leechMod.modifierType = ModifierType.Leech;
        leechMod.damageMultiplier = 1f; leechMod.leechPercent = 0.15f;

        oversizeMod = ScriptableObject.CreateInstance<ModifierSO>();
        oversizeMod.modifierName = "Oversize"; oversizeMod.modifierType = ModifierType.Oversize;
        oversizeMod.damageMultiplier = 1f; oversizeMod.sizeMultiplier = 2f; oversizeMod.speedPenalty = 0.8f;

        volatileMod = ScriptableObject.CreateInstance<ModifierSO>();
        volatileMod.modifierName = "Volatile"; volatileMod.modifierType = ModifierType.Volatile;
        volatileMod.damageMultiplier = 1.5f; volatileMod.volatileMissChance = 0.1f;

        homingMod = ScriptableObject.CreateInstance<ModifierSO>();
        homingMod.modifierName = "Homing"; homingMod.modifierType = ModifierType.Homing;
        homingMod.damageMultiplier = 1f; homingMod.homingTurnSpeed = 180f;
        homingMod.homingDetectRange = 10f; homingMod.homingSpeedMult = 0.7f;

        allModifiers = new[] { noneMod, splitMod, pierceMod, bounceMod, leechMod, oversizeMod, volatileMod, homingMod };

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
            CreateCursedRelic("Cursed Gold", RelicType.CursedGold, "CURSED: 3x gold drops, enemies +30% HP", new Color(0.6f, 0.5f, 0.1f)),
            CreateCursedRelic("Blood Pact", RelicType.BloodPact, "CURSED: Spells cost 1 HP, +100% damage", new Color(0.6f, 0.05f, 0.05f)),
            CreateCursedRelic("Chaos", RelicType.Chaos, "CURSED: Random element each cast, +30% damage", new Color(0.3f, 0.1f, 0.5f)),
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

    static ElementSO CreateElement(string name, int dmg, StatusEffectType effect, float dps, float dur, Color col)
    {
        var e = ScriptableObject.CreateInstance<ElementSO>();
        e.elementName = name; e.baseDamage = dmg; e.statusEffect = effect;
        e.statusDPS = dps; e.statusDuration = dur; e.color = col;
        return e;
    }

    static FormSO CreateForm(string name, FormType type)
    {
        var f = ScriptableObject.CreateInstance<FormSO>();
        f.formName = name; f.formType = type;
        return f;
    }

    // ─── FLOOR MAP ──────────────────────────────────────────────

    void GenerateFloorMap()
    {
        floorMap = new List<MapNode[]>();
        // 10 depths: 0=start combat, 1-3=choices, 4=shop/devilDeal, 5-6=choices, 7=rest, 8=choice, 9=boss
        for (int d = 0; d < roomsPerFloor; d++)
        {
            if (d == 0) // First room: always combat
                floorMap.Add(new[] { new MapNode { type = NodeType.Combat, depth = d } });
            else if (d == roomsPerFloor - 1) // Last room: always boss
                floorMap.Add(new[] { new MapNode { type = NodeType.Boss, depth = d } });
            else if (d == 4) // Depth 4: shop or devil deal
            {
                if (currentFloor >= 3 && Random.value < 0.5f)
                    floorMap.Add(new[] {
                        new MapNode { type = NodeType.Shop, depth = d },
                        new MapNode { type = NodeType.Event, depth = d }
                    });
                else
                    floorMap.Add(new[] {
                        new MapNode { type = NodeType.Shop, depth = d },
                        new MapNode { type = NodeType.Combat, depth = d }
                    });
            }
            else if (d == 7) // Depth 7: rest or event
                floorMap.Add(new[] {
                    new MapNode { type = NodeType.Rest, depth = d },
                    new MapNode { type = NodeType.Event, depth = d }
                });
            else // Choice rooms: 2-3 options
            {
                var options = new List<MapNode>();
                options.Add(new MapNode { type = NodeType.Combat, depth = d });

                // Second option
                float roll = Random.value;
                if (roll < 0.25f)
                    options.Add(new MapNode { type = NodeType.EliteCombat, depth = d });
                else if (roll < 0.5f)
                    options.Add(new MapNode { type = NodeType.Event, depth = d });
                else
                    options.Add(new MapNode { type = NodeType.Combat, depth = d });

                // Third option on later floors (50% chance)
                if (currentFloor >= 2 && Random.value < 0.5f)
                {
                    if (Random.value < 0.4f)
                        options.Add(new MapNode { type = NodeType.EliteCombat, depth = d });
                    else
                        options.Add(new MapNode { type = NodeType.Event, depth = d });
                }

                floorMap.Add(options.ToArray());
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
        NodeType.Shop => ("SHOP", "Buy relics with gold", new Color(1f, 0.85f, 0.2f)),
        NodeType.Event => ("EVENT", "Risk and reward", new Color(0.3f, 0.7f, 0.9f)),
        NodeType.Rest => ("REST", "Heal to full HP", new Color(0.3f, 0.9f, 0.5f)),
        NodeType.Boss => ("BOSS", "Floor guardian", new Color(0.8f, 0.1f, 0.1f)),
        _ => ("???", "Unknown", Color.gray)
    };

    void TransitionToRoom(NodeType nodeType)
    {
        // Clean up
        if (rewardPickup != null) { Destroy(rewardPickup); rewardPickup = null; }
        foreach (var e in enemies) if (e != null) Destroy(e);
        enemies.Clear();
        enemiesAlive = 0;

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
                hud.ShowVictory(wave, currentFloor - 1);
                playerCtrl.enabled = false;
                spellCaster.enabled = false;
                return;
            }

            floorGen = new FloorGenerator();
            floorGen.Generate(roomsPerFloor, currentFloor - 1);
            GenerateFloorMap();

            if (playerCtrl != null)
                playerCtrl.RefillPotions(MetaProgression.PotionsPerFloor);
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
        head.GetComponent<Renderer>().material = MakeLit(new Color(0.85f, 0.7f, 0.55f));

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
        staffTip.GetComponent<Renderer>().material = MakeEmissive(fireElem.color);

        // Components
        playerCtrl = player.AddComponent<PlayerController>();
        spellCaster = player.AddComponent<SpellCaster>();
        playerHealth = player.AddComponent<Health>();
        int baseHP = 5 + MetaProgression.MaxHPBonus;
        playerHealth.maxHP = baseHP;
        playerHealth.currentHP = baseHP;

        relicMgr = player.AddComponent<RelicManager>();
        relicMgr.Init(playerHealth, playerCtrl, allRelics, allElements);

        var dualCast = player.AddComponent<DualCast>();
        dualCast.Init(spellCaster);

        if (!inHub)
        {
            // Starting spells: Fire Bolt, Ice Cone + Split
            spellCaster.spellSlots[0] = new SpellData { element = fireElem, form = boltForm, modifier = noneMod };
            spellCaster.spellSlots[1] = new SpellData { element = iceElem, form = coneForm, modifier = splitMod };

            playerHealth.OnDeath += OnPlayerDeath;

            // Update staff tip color when spell changes
            spellCaster.OnSpellChanged += () =>
            {
                var spell = spellCaster.ActiveSpell;
                if (spell?.element != null && staffTip != null)
                    staffTip.GetComponent<Renderer>().material = MakeEmissive(spell.element.color);
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

        cam = camGO.AddComponent<TopDownCamera>();
        cam.target = player.transform;
        cam.distance = 14f;
        cam.pitch = 60f;
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

        switch (nodeType)
        {
            case NodeType.Boss:
                SpawnBossWave(currentFloor);
                return;
            case NodeType.Shop:
                if (currentFloor >= 3 && Random.value < 0.5f)
                    StartDevilDealRoom();
                else
                    StartShopRoom();
                return;
            case NodeType.Event:
                StartEventRoom();
                return;
            case NodeType.Rest:
                StartRestRoom();
                return;
            case NodeType.EliteCombat:
                SpawnCombatWave(1.5f, true); // 1.5x budget, guaranteed elite
                return;
            default: // Combat
                SpawnCombatWave(1f, false);
                return;
        }
    }

    // Keep SpawnWave as alias for first room
    void SpawnWave()
    {
        SpawnWaveForNodeType(currentRoom == roomsPerFloor ? NodeType.Boss : NodeType.Combat);
    }

    void SpawnCombatWave(float budgetMult, bool forceElite)
    {
        int baseBudget = currentFloor switch { 1 => 10, 2 => 18, 3 => 28, 4 => 40, _ => 55 };
        int totalBudget = Mathf.CeilToInt((baseBudget + (currentRoom - 1) * 2) * budgetMult);

        // Split into 1-3 sub-waves based on floor
        totalSubWaves = currentFloor >= 3 ? Random.Range(2, 4) : (currentFloor >= 2 ? Random.Range(1, 3) : 1);
        subWave = 0;
        reinforcementTimer = -1;

        int waveBudget = totalBudget / totalSubWaves;
        SpawnSubWave(waveBudget, forceElite);
    }

    void SpawnSubWave(int budget, bool forceElite)
    {
        subWave++;

        while (budget > 0)
        {
            int type = PickEnemyType(budget);
            int cost = type switch { 0=>2, 1=>3, 2=>6, 3=>5, 4=>7, 5=>5, _=>2 };
            if (cost > budget) { type = 0; cost = 2; }
            if (cost > budget) break;
            budget -= cost;

            if (type == 3)
            {
                int sc = Random.Range(8, 13);
                for (int s = 0; s < sc; s++) { SpawnEnemy(type); enemiesAlive++; }
            }
            else { SpawnEnemy(type); enemiesAlive++; }
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
                }
            }
            TransitionToNextRoom();
        });
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

        int eventType = Random.Range(0, 4);
        switch (eventType)
        {
            case 0: // Sacrifice: trade HP for a relic
                StartSacrificeEvent();
                break;
            case 1: // Curse choice: pick a curse, gain gold
                StartCurseChoiceEvent();
                break;
            case 2: // Gamble: risk gold for reward
                StartGambleEvent();
                break;
            default: // Mystery: random positive/negative
                StartMysteryEvent();
                break;
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

    int PickEnemyType(int budget)
    {
        if (wave <= 2) return Random.value < 0.7f ? 0 : 1;
        if (wave <= 4)
        {
            float r = Random.value;
            if (r < 0.3f) return 0; if (r < 0.5f) return 1;
            if (r < 0.7f) return 2; return 3;
        }
        return Random.Range(0, 6);
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
        }
    }

    Vector3 RandomSpawnPos()
    {
        Vector3 pos; int safety = 50;
        do { pos = new Vector3(Random.Range(1.5f, 10.5f), 0, Random.Range(1.5f, 10.5f)); safety--; }
        while (Vector3.Distance(pos, player.transform.position) < 4f && safety > 0);
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
        AddCapsuleCol(e, 1.2f, 0.35f, 0.6f); BuildBody(e.transform, c, 1f);
        RegisterEnemy(e, 20 + wave * 5);
        var ai = e.AddComponent<ShamblerAI>(); ai.moveSpeed = 2.5f + wave * 0.2f; ai.baseColor = c; ai.floorLevel = currentFloor;
    }

    void SpawnArcher()
    {
        var e = new GameObject("Archer"); Color c = new(0.2f, 0.6f, 0.2f);
        AddCapsuleCol(e, 1.2f, 0.3f, 0.6f); BuildBody(e.transform, c, 0.9f);
        var bow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bow.name = "Bow"; Destroy(bow.GetComponent<CapsuleCollider>());
        bow.transform.parent = e.transform; bow.transform.localPosition = new Vector3(0.3f, 0.6f, 0.15f);
        bow.transform.localScale = new Vector3(0.04f, 0.25f, 0.04f);
        bow.transform.localRotation = Quaternion.Euler(0, 0, -30);
        bow.GetComponent<Renderer>().material = MakeLit(new Color(0.4f, 0.25f, 0.1f));
        RegisterEnemy(e, 15 + wave * 3);
        var archerAI = e.AddComponent<ArcherAI>(); archerAI.baseColor = c; archerAI.floorLevel = currentFloor;
    }

    void SpawnBrute()
    {
        var e = new GameObject("Brute"); Color c = new(0.5f, 0.2f, 0.15f);
        AddCapsuleCol(e, 1.6f, 0.5f, 0.8f); BuildBody(e.transform, c, 1.4f);
        RegisterEnemy(e, 60 + wave * 15);
        e.AddComponent<BruteAI>().baseColor = c;
    }

    void SpawnSwarmUnit()
    {
        var e = new GameObject("Swarm"); Color c = new(0.6f, 0.5f, 0.1f);
        var sc = e.AddComponent<SphereCollider>(); sc.radius = 0.2f; sc.center = new Vector3(0, 0.2f, 0);
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body"; Destroy(body.GetComponent<SphereCollider>());
        body.transform.parent = e.transform; body.transform.localPosition = new Vector3(0, 0.2f, 0);
        body.transform.localScale = Vector3.one * 0.25f;
        body.GetComponent<Renderer>().material = MakeLit(c);
        CreateEye(e.transform, new Vector3(0, 0.3f, 0.1f), new Color(1f, 0.8f, 0.1f));
        RegisterEnemy(e, 5 + wave);
        e.AddComponent<SwarmAI>().baseColor = c;
    }

    void SpawnMirror()
    {
        var e = new GameObject("Mirror"); Color c = new(0.7f, 0.7f, 0.75f);
        AddCapsuleCol(e, 1.2f, 0.35f, 0.6f); BuildBody(e.transform, c, 1f);
        RegisterEnemy(e, 30 + wave * 5);
        e.AddComponent<MirrorAI>().baseColor = c;
    }

    void SpawnShieldBearer()
    {
        var e = new GameObject("ShieldBearer"); Color c = new(0.3f, 0.35f, 0.5f);
        AddCapsuleCol(e, 1.3f, 0.4f, 0.65f); BuildBody(e.transform, c, 1.1f);
        var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shield.name = "Shield"; Destroy(shield.GetComponent<BoxCollider>());
        shield.transform.parent = e.transform; shield.transform.localPosition = new Vector3(0, 0.5f, 0.35f);
        shield.transform.localScale = new Vector3(0.6f, 0.7f, 0.08f);
        shield.GetComponent<Renderer>().material = MakeLit(new Color(0.5f, 0.5f, 0.6f));
        RegisterEnemy(e, 35 + wave * 5);
        e.AddComponent<ShieldBearerAI>().baseColor = c;
    }

    static void CreateEye(Transform parent, Vector3 localPos, Color color)
    {
        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        Destroy(eye.GetComponent<SphereCollider>());
        eye.transform.parent = parent;
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.08f;
        eye.GetComponent<Renderer>().material = MakeEmissive(color);
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
        AddCapsuleCol(e, 2f, 0.6f, 1f);
        BuildBody(e.transform, c, 1.6f);
        e.transform.position = new Vector3(6, 0, 10);
        var rb = e.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
        var hp = e.AddComponent<Health>(); hp.maxHP = 150; hp.currentHP = 150;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<WardenBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnSwarmQueen()
    {
        var e = new GameObject("SwarmQueenBoss");
        Color c = new(0.7f, 0.5f, 0.1f);
        AddCapsuleCol(e, 1.8f, 0.5f, 0.9f);
        BuildBody(e.transform, c, 1.3f);
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
        var hp = e.AddComponent<Health>(); hp.maxHP = 200; hp.currentHP = 200;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<SwarmQueenBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnMirrorKnight()
    {
        var e = new GameObject("MirrorKnightBoss");
        Color c = new(0.6f, 0.6f, 0.7f);
        AddCapsuleCol(e, 2f, 0.5f, 1f);
        BuildBody(e.transform, c, 1.4f);
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
        var hp = e.AddComponent<Health>(); hp.maxHP = 250; hp.currentHP = 250;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<MirrorKnightBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnLich()
    {
        var e = new GameObject("LichBoss");
        Color c = new(0.3f, 0.1f, 0.5f);
        AddCapsuleCol(e, 1.8f, 0.45f, 0.9f);
        BuildBody(e.transform, c, 1.2f);
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
        var hp = e.AddComponent<Health>(); hp.maxHP = 300; hp.currentHP = 300;
        e.AddComponent<EnemyHealthBar>();
        e.AddComponent<LichBoss>();
        var enemyRef = e;
        hp.OnDeath += () => OnBossDeath(enemyRef);
        currentBoss = e;
    }

    void SpawnRunebreaker()
    {
        var e = new GameObject("RunebreakerBoss");
        Color c = new(0.6f, 0.1f, 0.1f);
        AddCapsuleCol(e, 2.2f, 0.6f, 1.1f);
        BuildBody(e.transform, c, 1.7f);
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
        var hp = e.AddComponent<Health>(); hp.maxHP = 400; hp.currentHP = 400;
        e.AddComponent<EnemyHealthBar>();
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

        // Boss gold drop (CursedGold: 3x)
        int goldDrop = GoldSystem.CalculateEnemyDrop(wave, true);
        if (relicMgr != null && relicMgr.HasRelic(RelicType.CursedGold))
            goldDrop *= 3;
        GoldSystem.SpawnGoldDrop(boss.transform.position, goldDrop);
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
        ShowRuneSelection();
    }

    // ─── GAME EVENTS ──────────────────────────────────────────────

    void OnEnemyDeath(GameObject enemy)
    {
        // Gold drop (CursedGold: 3x)
        int goldDrop = GoldSystem.CalculateEnemyDrop(wave, false);
        if (relicMgr != null && relicMgr.HasRelic(RelicType.CursedGold))
            goldDrop *= 3;
        GoldSystem.SpawnGoldDrop(enemy.transform.position, goldDrop);

        // Void pull effect
        var health = enemy.GetComponent<Health>();
        if (health != null && health.voidMarked)
        {
            Vector3 deathPos = enemy.transform.position;
            Collider[] nearby = Physics.OverlapSphere(deathPos, 4f);
            foreach (var col in nearby)
            {
                if (col.gameObject == enemy) continue;
                var ai = col.GetComponent<ShamblerAI>();
                if (ai != null) { ai.ApplyPull(deathPos, 1.5f); continue; }
                // Other enemy types: push via rigidbody
                var erb = col.GetComponent<Rigidbody>();
                if (erb != null && col.GetComponent<Health>() != null)
                {
                    Vector3 pullDir = (deathPos - col.transform.position).normalized;
                    erb.MovePosition(col.transform.position + pullDir * 3f * Time.deltaTime);
                }
            }
            // Pull visual (expanding void sphere)
            var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(vfx.GetComponent<SphereCollider>());
            vfx.transform.position = deathPos + Vector3.up * 0.5f;
            vfx.transform.localScale = Vector3.one * 0.5f;
            vfx.GetComponent<Renderer>().material = MakeEmissive(voidElem.color);
            Destroy(vfx, 0.4f);
        }

        enemies.Remove(enemy);
        enemiesAlive--;
        enemiesKilledThisRun++;

        if (enemiesAlive <= 0)
        {
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

        // Every 3rd room: offer relic choice instead of rune
        if (currentRoom % 3 == 0 && !bossActive)
        {
            ShowRelicSelection();
            return;
        }

        int count = 3;
        // Lucky relic: 20% chance for 4th choice
        if (relicMgr != null && relicMgr.HasLucky) count = 4;

        var options = new ScriptableObject[count];

        // Weighted rune pools by floor
        float elemW, formW;
        if (currentFloor <= 2) { elemW = 0.6f; formW = 0.3f; }
        else if (currentFloor <= 3) { elemW = 0.3f; formW = 0.5f; }
        else { elemW = 0.1f; formW = 0.3f; }

        // Build unlocked element pool
        var unlockedElements = new List<ElementSO>();
        foreach (var e in allElements)
            if (MetaProgression.IsElementUnlocked(e.elementName)) unlockedElements.Add(e);

        for (int i = 0; i < count; i++)
        {
            options[i] = RollWeightedRune(elemW, formW, unlockedElements);
        }

        // Guarantee one option matches current build element (build direction)
        if (count >= 3 && unlockedElements.Count > 0)
        {
            var currentElem = spellCaster.ActiveSpell?.element;
            if (currentElem != null && Random.value < 0.4f)
                options[0] = currentElem; // 40% chance to offer current element as first option
        }

        int rerolls = MetaProgression.Rerolls;
        hud.ShowRuneSelection(options, rerolls, idx =>
        {
            ApplyRune(options[idx]);
            if (bossActive)
                bossActive = false;
            TransitionToNextRoom();
        }, () =>
        {
            // Reroll callback: regenerate options with weighting
            for (int i = 0; i < count; i++)
                options[i] = RollWeightedRune(elemW, formW, unlockedElements);
        });
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

    // Fallback when all relics are owned
    void ShowRuneSelectionForce()
    {
        var options = new ScriptableObject[3];
        float elemW = 0.3f, formW = 0.3f;
        var unlockedElements = new List<ElementSO>();
        foreach (var e in allElements)
            if (MetaProgression.IsElementUnlocked(e.elementName)) unlockedElements.Add(e);

        for (int i = 0; i < 3; i++)
        {
            float r = Random.value;
            if (r < elemW && unlockedElements.Count > 0) options[i] = unlockedElements[Random.Range(0, unlockedElements.Count)];
            else if (r < elemW + formW) options[i] = allForms[Random.Range(0, allForms.Length)];
            else options[i] = allModifiers[Random.Range(0, allModifiers.Length)];
        }
        hud.ShowRuneSelection(options, 0, idx =>
        {
            ApplyRune(options[idx]);
            TransitionToNextRoom();
        }, null);
    }

    ScriptableObject RollWeightedRune(float elemW, float formW, List<ElementSO> unlockedElements)
    {
        float r = Random.value;
        if (r < elemW && unlockedElements.Count > 0)
        {
            // Weight toward current build's element (2x chance)
            var currentElem = spellCaster.ActiveSpell?.element;
            if (currentElem != null && unlockedElements.Contains(currentElem) && Random.value < 0.35f)
                return currentElem;
            return unlockedElements[Random.Range(0, unlockedElements.Count)];
        }
        if (r < elemW + formW)
            return allForms[Random.Range(0, allForms.Length)];
        return allModifiers[Random.Range(0, allModifiers.Length)];
    }

    void ApplyRune(ScriptableObject rune)
    {
        var spell = spellCaster.spellSlots[spellCaster.activeSlot];
        if (spell == null)
        {
            spell = new SpellData();
            spellCaster.spellSlots[spellCaster.activeSlot] = spell;
        }

        if (rune is ElementSO elem)
        {
            if (spell.element == elem)
                spell.elementTier++; // Same element = upgrade tier
            else
                { spell.element = elem; spell.elementTier = 0; }
        }
        else if (rune is FormSO form)
        {
            if (spell.form == form)
                spell.formTier++; // Same form = cooldown reduction
            else
                { spell.form = form; spell.formTier = 0; }
        }
        else if (rune is ModifierSO mod)
        {
            if (spell.modifier == mod)
                spell.modifierTier++; // Same modifier = stronger effect
            else
                { spell.modifier = mod; spell.modifierTier = 0; }
        }

        SFXSystem.Play(SFXSystem.SFXType.LevelUp, player.transform.position);
        hud.Refresh();
    }

    void OnPlayerDeath()
    {
        isPlayerDead = true;
        MetaProgression.RecordFloor(currentFloor);
        int metaReward = MetaProgression.AwardDeathCurrency(currentFloor, currentRoom, enemiesKilledThisRun);
        hud.ShowDeath(true, metaReward);
        playerCtrl.enabled = false;
        spellCaster.enabled = false;
    }

    void ReturnToHub()
    {
        isPlayerDead = false;
        CleanupRun();
        EnterHub();
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
