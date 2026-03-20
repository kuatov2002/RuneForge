using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
    bool isPlayerDead;
    bool bossActive;
    GameObject currentBoss;
    GameObject rewardPickup;

    // Floor/Room system
    int currentFloor = 1; // 1-5
    int currentRoom = 1;  // 1-10
    int roomsPerFloor = 10;
    int totalFloors = 5;
    FloorGenerator floorGen;
    GameObject currentRoomGO;
    bool roomCleared;

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
        floorGen = new FloorGenerator();
        floorGen.Generate(roomsPerFloor, currentFloor - 1);
        BuildCurrentRoom();
        CreatePlayer();
        CreateCamera();
        CreateLighting();
        CreateHUD();
        hud.RefreshRelics(relicMgr.OwnedRelics);
        DoorTrigger.OnDoorEntered += OnDoorEntered;
        SpawnWave();
    }

    void Update()
    {
        if (isPlayerDead)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                Restart();
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
        };
    }

    static RelicSO CreateRelic(string name, RelicType type, string desc, Color col)
    {
        var r = ScriptableObject.CreateInstance<RelicSO>();
        r.relicName = name; r.relicType = type; r.description = desc; r.color = col;
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
    }

    void TransitionToNextRoom()
    {
        // Clean up
        if (rewardPickup != null) { Destroy(rewardPickup); rewardPickup = null; }
        foreach (var e in enemies) if (e != null) Destroy(e);
        enemies.Clear();
        enemiesAlive = 0;

        currentRoom++;

        if (currentRoom > roomsPerFloor)
        {
            // Floor complete — transition to next floor
            currentFloor++;
            currentRoom = 1;

            if (currentFloor > totalFloors)
            {
                MetaProgression.CompleteRun();
                isPlayerDead = true; // Allow R to restart
                hud.ShowVictory(wave, currentFloor - 1);
                playerCtrl.enabled = false;
                spellCaster.enabled = false;
                return;
            }

            floorGen = new FloorGenerator();
            floorGen.Generate(roomsPerFloor, currentFloor - 1);
        }

        BuildCurrentRoom();
        player.transform.position = new Vector3(
            currentRoomGO.transform.position.x + 6,
            0,
            currentRoomGO.transform.position.z + 2);

        wave++;
        hud.SetFloorRoom(currentFloor, currentRoom);
        if (relicMgr != null) relicMgr.OnRoomEnter();
        SpawnWave();
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

        // Starting spells: Fire Bolt, Ice Cone + Split
        spellCaster.spellSlots[0] = new SpellData { element = fireElem, form = boltForm, modifier = noneMod };
        spellCaster.spellSlots[1] = new SpellData { element = iceElem, form = coneForm, modifier = splitMod };

        relicMgr = player.AddComponent<RelicManager>();
        relicMgr.Init(playerHealth, playerCtrl, allRelics);

        playerHealth.OnDeath += OnPlayerDeath;

        // Update staff tip color when spell changes
        spellCaster.OnSpellChanged += () =>
        {
            var spell = spellCaster.ActiveSpell;
            if (spell?.element != null && staffTip != null)
                staffTip.GetComponent<Renderer>().material = MakeEmissive(spell.element.color);
        };
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

    void CreateHUD()
    {
        var hudGO = new GameObject("HUD");
        hud = hudGO.AddComponent<GameHUD>();
        hud.Init(spellCaster, playerHealth);
    }

    // ─── ENEMIES ──────────────────────────────────────────────────

    void SpawnWave()
    {
        hud.SetWave(wave);
        hud.SetFloorRoom(currentFloor, currentRoom);
        roomCleared = false;

        // Boss room: last room of each floor
        if (currentRoom == roomsPerFloor)
        {
            SpawnBossWave(currentFloor);
            return;
        }

        // Shop room: room 5
        if (currentRoom == 5)
        {
            StartShopRoom();
            return;
        }

        // Rest room: room 8
        if (currentRoom == 8)
        {
            StartRestRoom();
            return;
        }

        // Threat budget scales with floor: Floor 1=10, 2=18, 3=28, 4=40, 5=55
        int baseBudget = currentFloor switch { 1 => 10, 2 => 18, 3 => 28, 4 => 40, _ => 55 };
        int budget = baseBudget + (currentRoom - 1) * 2;
        enemiesAlive = 0;

        while (budget > 0)
        {
            int type = PickEnemyType(budget);
            int cost = type switch { 0=>2, 1=>3, 2=>6, 3=>5, 4=>7, 5=>5, _=>2 };
            if (cost > budget) { type = 0; cost = 2; }
            if (cost > budget) break;
            budget -= cost;

            if (type == 3) // Swarm group
            {
                int sc = Random.Range(8, 13);
                for (int s = 0; s < sc; s++) { SpawnEnemy(type); enemiesAlive++; }
            }
            else { SpawnEnemy(type); enemiesAlive++; }
        }
        if (enemiesAlive == 0) { SpawnEnemy(0); enemiesAlive = 1; }
    }

    void StartShopRoom()
    {
        // Shop: show 3 relics to buy (auto-offer, no combat)
        hud.ShowShopRoom(allRelics, relicMgr, relic =>
        {
            if (relic != null)
            {
                relicMgr.AddRelic(relic);
                hud.RefreshRelics(relicMgr.OwnedRelics);
            }
            TransitionToNextRoom();
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
        var health = enemy.AddComponent<Health>(); health.maxHP = hp; health.currentHP = hp;
        enemy.AddComponent<EnemyHealthBar>();
        var enemyRef = enemy;
        health.OnDeath += () => OnEnemyDeath(enemyRef);
        enemies.Add(enemy);
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
        var ai = e.AddComponent<ShamblerAI>(); ai.moveSpeed = 2.5f + wave * 0.2f; ai.baseColor = c;
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
        e.AddComponent<ArcherAI>().baseColor = c;
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
            if (bossHP != null) hud.ShowBossHP(bossHP, GetBossName(bossIndex));
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

        if (enemiesAlive <= 0)
            SpawnRewardPickup();
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

        for (int i = 0; i < count; i++)
        {
            float r = Random.value;
            if (r < elemW)
                options[i] = allElements[Random.Range(0, allElements.Length)];
            else if (r < elemW + formW)
                options[i] = allForms[Random.Range(0, allForms.Length)];
            else
                options[i] = allModifiers[Random.Range(0, allModifiers.Length)];
        }

        hud.ShowRuneSelection(options, idx =>
        {
            ApplyRune(options[idx]);
            if (bossActive)
                bossActive = false;
            TransitionToNextRoom();
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
        for (int i = 0; i < 3; i++)
        {
            float r = Random.value;
            if (r < elemW) options[i] = allElements[Random.Range(0, allElements.Length)];
            else if (r < elemW + formW) options[i] = allForms[Random.Range(0, allForms.Length)];
            else options[i] = allModifiers[Random.Range(0, allModifiers.Length)];
        }
        hud.ShowRuneSelection(options, idx =>
        {
            ApplyRune(options[idx]);
            TransitionToNextRoom();
        });
    }

    void ApplyRune(ScriptableObject rune)
    {
        var spell = spellCaster.spellSlots[spellCaster.activeSlot];
        if (spell == null)
        {
            spell = new SpellData();
            spellCaster.spellSlots[spellCaster.activeSlot] = spell;
        }

        if (rune is ElementSO elem) spell.element = elem;
        else if (rune is FormSO form) spell.form = form;
        else if (rune is ModifierSO mod) spell.modifier = mod;

        hud.Refresh();
    }

    void OnPlayerDeath()
    {
        isPlayerDead = true;
        hud.ShowDeath(true);
        playerCtrl.enabled = false;
        spellCaster.enabled = false;
    }

    void Restart()
    {
        isPlayerDead = false;
        hud.ShowDeath(false);
        hud.HideBossHP();
        bossActive = false;
        if (currentBoss != null) { Destroy(currentBoss); currentBoss = null; }
        if (rewardPickup != null) { Destroy(rewardPickup); rewardPickup = null; }

        foreach (var e in enemies)
            if (e != null) Destroy(e);
        enemies.Clear();

        playerHealth.ResetHealth();
        playerCtrl.enabled = true;
        spellCaster.enabled = true;

        // Clear relics on death — roguelite reset
        if (relicMgr != null) relicMgr.ClearRelics();
        hud.RefreshRelics(relicMgr.OwnedRelics);

        // Reset base stats that relics may have modified
        playerCtrl.moveSpeed = 6f;
        int baseHP = 5 + MetaProgression.MaxHPBonus;
        playerHealth.maxHP = baseHP;
        playerHealth.currentHP = baseHP;

        wave = 1;
        currentFloor = 1;
        currentRoom = 1;
        roomCleared = false;
        floorGen = new FloorGenerator();
        floorGen.Generate(roomsPerFloor, 0);
        BuildCurrentRoom();
        player.transform.position = new Vector3(6, 0, 6);
        hud.Refresh();
        SpawnWave();
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
