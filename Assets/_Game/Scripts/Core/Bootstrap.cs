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
    ModifierSO noneMod, splitMod;
    ModifierSO[] allModifiers;

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
        CreateRoom();
        CreatePlayer();
        CreateCamera();
        CreateLighting();
        CreateHUD();
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

        allModifiers = new[] { noneMod, splitMod };
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

    void CreateRoom()
    {
        RoomBuilder.Build(12, 12);
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
        playerHealth.maxHP = 5;
        playerHealth.currentHP = 5;

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
        int count = 2 + wave;
        enemiesAlive = count;
        hud.SetWave(wave);
        for (int i = 0; i < count; i++)
            SpawnShambler();
    }

    void SpawnShambler()
    {
        var enemy = new GameObject("Shambler");
        Color enemyColor = new Color(0.75f, 0.15f, 0.15f);

        // Capsule collider
        var col = enemy.AddComponent<CapsuleCollider>();
        col.height = 1.2f;
        col.radius = 0.35f;
        col.center = new Vector3(0, 0.6f, 0);

        // 3D body
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = enemy.transform;
        body.transform.localPosition = new Vector3(0, 0.45f, 0);
        body.transform.localScale = new Vector3(0.6f, 0.35f, 0.55f);
        var bodyMat = MakeLit(enemyColor);
        body.GetComponent<Renderer>().material = bodyMat;

        // Head (slightly larger, menacing)
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<SphereCollider>());
        head.name = "Head";
        head.transform.parent = enemy.transform;
        head.transform.localPosition = new Vector3(0, 0.9f, 0.05f);
        head.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
        head.GetComponent<Renderer>().material = MakeLit(enemyColor * 0.8f);

        // Eyes (emissive)
        CreateEye(enemy.transform, new Vector3(-0.1f, 0.95f, 0.18f), new Color(1f, 0.3f, 0.1f));
        CreateEye(enemy.transform, new Vector3(0.1f, 0.95f, 0.18f), new Color(1f, 0.3f, 0.1f));

        // Arms (cubes at sides)
        CreateArm(enemy.transform, new Vector3(-0.35f, 0.45f, 0.1f), enemyColor * 0.9f);
        CreateArm(enemy.transform, new Vector3(0.35f, 0.45f, 0.1f), enemyColor * 0.9f);

        // Position
        Vector3 pos;
        int safety = 50;
        do
        {
            pos = new Vector3(Random.Range(1.5f, 10.5f), 0, Random.Range(1.5f, 10.5f));
            safety--;
        } while (Vector3.Distance(pos, player.transform.position) < 4f && safety > 0);
        enemy.transform.position = pos;

        // Physics
        var rb = enemy.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // Health
        var health = enemy.AddComponent<Health>();
        health.maxHP = 20 + wave * 5;
        health.currentHP = health.maxHP;

        // AI - use the body renderer for status color changes
        enemy.AddComponent<EnemyHealthBar>();
        var ai = enemy.AddComponent<ShamblerAI>();
        ai.moveSpeed = 2.5f + wave * 0.2f;
        ai.baseColor = enemyColor;

        var enemyRef = enemy;
        health.OnDeath += () => OnEnemyDeath(enemyRef);
        enemies.Add(enemy);
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
                var ai = col.GetComponent<ShamblerAI>();
                if (ai != null && col.gameObject != enemy)
                    ai.ApplyPull(deathPos, 1.5f);
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
            ShowRuneSelection();
    }

    void ShowRuneSelection()
    {
        var options = new ScriptableObject[3];

        for (int i = 0; i < 3; i++)
        {
            int type = Random.Range(0, 3);
            options[i] = type switch
            {
                0 => allElements[Random.Range(0, allElements.Length)],
                1 => allForms[Random.Range(0, allForms.Length)],
                _ => allModifiers[Random.Range(0, allModifiers.Length)]
            };
        }

        hud.ShowRuneSelection(options, idx =>
        {
            ApplyRune(options[idx]);
            wave++;
            SpawnWave();
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

        foreach (var e in enemies)
            if (e != null) Destroy(e);
        enemies.Clear();

        playerHealth.ResetHealth();
        player.transform.position = new Vector3(6, 0, 6);
        playerCtrl.enabled = true;
        spellCaster.enabled = true;

        wave = 1;
        hud.Refresh();
        SpawnWave();
    }

    // ─── MATERIAL HELPERS ─────────────────────────────────────────

    static Material MakeLit(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        return mat;
    }

    static Material MakeEmissive(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 3f);
        return mat;
    }
}
