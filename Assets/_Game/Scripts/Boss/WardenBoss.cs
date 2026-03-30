using UnityEngine;
using System.Collections.Generic;

public class WardenBoss : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    public float slamRange = 2.5f;
    public int slamDamage = 3;
    public float wallSpawnCooldown = 5f;
    public Color baseColor = new Color(0.4f, 0.4f, 0.35f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    List<GameObject> walls = new();
    bool phase2;

    // State machine
    enum State { Chase, LeapWindup, Leaping, SlamCombo, ShockwaveAttack, PillarCharge, Recover, ArenaClose }
    State state = State.Chase;
    float stateTimer;
    float actionCooldown;
    float wallTimer;

    // Leap
    Vector3 leapTarget;
    Vector3 leapStart;
    float leapProgress;
    const float LeapDuration = 0.4f;
    const float LeapHeight = 4f;

    // Slam combo
    int comboCount;
    int comboMax;

    // Pillar charge
    Vector3 chargeDir;

    // Shockwave
    int shockwaveRingsSpawned;
    float shockwaveDelay;

    // Phase 2 arena closing
    List<GameObject> arenaWalls = new();
    float arenaTimer;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        var elemData = gameObject.GetComponent<EnemyElementData>();
        if (elemData == null) elemData = gameObject.AddComponent<EnemyElementData>();
        elemData.AssignBossImmunity(ElementType.Earth);

        if (health != null)
        {
            health.OnDeath += () => { isDead = true; CleanupWalls(); Destroy(gameObject, 0.5f); };
            health.OnHPChanged += (cur, max) =>
            {
                if (!phase2 && cur < max / 2)
                {
                    phase2 = true;
                    EnterPhase2();
                }
            };
        }

        actionCooldown = 0.6f;
        wallTimer = wallSpawnCooldown;
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        wallTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Chase:
                ChaseUpdate(toPlayer, dist, speedMult);
                break;
            case State.LeapWindup:
                LeapWindupUpdate();
                break;
            case State.Leaping:
                LeapingUpdate();
                break;
            case State.SlamCombo:
                SlamComboUpdate(dist, speedMult);
                break;
            case State.ShockwaveAttack:
                ShockwaveUpdate();
                break;
            case State.PillarCharge:
                PillarChargeUpdate(speedMult);
                break;
            case State.Recover:
                RecoverUpdate();
                break;
            case State.ArenaClose:
                ArenaCloseUpdate(toPlayer, dist, speedMult);
                break;
        }

        // Spawn walls between attacks
        float wallCD = phase2 ? wallSpawnCooldown * 0.4f : wallSpawnCooldown;
        if (wallTimer <= 0 && state == State.Chase)
        {
            wallTimer = wallCD;
            SpawnWall();
        }
    }

    // --- CHASE: aggressively close distance, pick attacks ---
    void ChaseUpdate(Vector3 toPlayer, float dist, float speedMult)
    {
        Vector3 dir = toPlayer.normalized;
        transform.rotation = Quaternion.LookRotation(dir);

        // Move toward player aggressively
        float speed = phase2 ? moveSpeed * 1.4f : moveSpeed;
        rb.MovePosition(transform.position + dir * speed * speedMult * Time.deltaTime);

        actionCooldown -= Time.deltaTime;
        if (actionCooldown > 0) return;

        // Pick attack based on distance and randomness
        if (dist > 6f)
        {
            // Far away — leap to player
            StartLeap();
        }
        else if (dist > 3.5f)
        {
            // Mid range — pillar charge or leap
            if (Random.value < 0.6f)
                StartPillarCharge(dir);
            else
                StartLeap();
        }
        else
        {
            // Close — slam combo or shockwave
            if (Random.value < 0.5f)
                StartSlamCombo();
            else
                StartShockwave();
        }
    }

    // --- LEAP: jump to player position like Malenia's diving attack ---
    void StartLeap()
    {
        state = State.LeapWindup;
        stateTimer = 0.35f;
        leapTarget = target.position;

        // Telegraph: red circle at landing zone
        var telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(telegraph.GetComponent<CapsuleCollider>());
        telegraph.transform.position = leapTarget + Vector3.up * 0.03f;
        telegraph.transform.localScale = new Vector3(5f, 0.03f, 5f);
        telegraph.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0.1f, 0.5f), 2f);
        Destroy(telegraph, LeapDuration + 0.4f);

        // Flash white during windup
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = Color.white;

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void LeapWindupUpdate()
    {
        stateTimer -= Time.deltaTime;
        // Track player during windup
        leapTarget = Vector3.Lerp(leapTarget, target.position, 2f * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation((leapTarget - transform.position).normalized);

        if (stateTimer <= 0)
        {
            state = State.Leaping;
            leapStart = transform.position;
            leapProgress = 0f;
        }
    }

    void LeapingUpdate()
    {
        leapProgress += Time.deltaTime / LeapDuration;
        if (leapProgress >= 1f)
        {
            leapProgress = 1f;
            transform.position = new Vector3(leapTarget.x, 0, leapTarget.z);
            LeapLand();
            return;
        }

        // Parabolic arc
        Vector3 pos = Vector3.Lerp(leapStart, leapTarget, leapProgress);
        pos.y = Mathf.Sin(leapProgress * Mathf.PI) * LeapHeight;
        transform.position = pos;
    }

    void LeapLand()
    {
        // AoE damage on landing
        float radius = phase2 ? 3.5f : 2.5f;
        int dmg = phase2 ? slamDamage + 1 : slamDamage;
        DamagePlayerInRadius(radius, dmg);

        // Shockwave VFX
        SpawnSlamVFX(transform.position, radius, new Color(0.6f, 0.4f, 0.2f));

        // Screen shake
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.5f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = baseColor;

        // Phase 2: chain into slam combo after leap
        if (phase2 && Random.value < 0.6f)
        {
            StartSlamCombo();
        }
        else
        {
            state = State.Recover;
            stateTimer = 0.5f;
            actionCooldown = 0.48f;
        }
    }

    // --- SLAM COMBO: rapid consecutive slams tracking the player ---
    void StartSlamCombo()
    {
        state = State.SlamCombo;
        comboMax = phase2 ? Random.Range(3, 5) : Random.Range(2, 4);
        comboCount = 0;
        stateTimer = 0.3f; // Delay before first slam
    }

    void SlamComboUpdate(float dist, float speedMult)
    {
        stateTimer -= Time.deltaTime;

        // Track player between slams
        Vector3 toP = target.position - transform.position;
        toP.y = 0;
        transform.rotation = Quaternion.LookRotation(toP.normalized);

        // Slide toward player between slams
        float slideSpeed = phase2 ? 8f : 5f;
        if (toP.magnitude > 1.5f)
            rb.MovePosition(transform.position + toP.normalized * slideSpeed * speedMult * Time.deltaTime);

        if (stateTimer <= 0)
        {
            // Do slam
            DamagePlayerInRadius(slamRange, slamDamage);
            SpawnSlamVFX(transform.position, slamRange, new Color(0.5f, 0.4f, 0.2f));

            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.3f);
            if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.03f);
            SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);

            comboCount++;
            if (comboCount >= comboMax)
            {
                // Combo finished — final big slam
                float bigRadius = phase2 ? 4f : 3f;
                DamagePlayerInRadius(bigRadius, slamDamage + 1);
                SpawnSlamVFX(transform.position, bigRadius, new Color(0.8f, 0.3f, 0.1f));

                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.5f);
                if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
                SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

                state = State.Recover;
                stateTimer = phase2 ? 0.6f : 1f; // Punish window
                actionCooldown = phase2 ? 0.48f : 0.72f;
            }
            else
            {
                // Next slam delay (gets faster)
                stateTimer = phase2 ? 0.25f : 0.35f;
            }
        }

        // Flash during combo
        float flash = Mathf.PingPong(Time.time * 12f, 1f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = Color.Lerp(baseColor, new Color(1f, 0.6f, 0.2f), flash * 0.5f);
    }

    // --- SHOCKWAVE: expanding rings that player must dodge through ---
    void StartShockwave()
    {
        state = State.ShockwaveAttack;
        shockwaveRingsSpawned = 0;
        shockwaveDelay = 0f;

        // Telegraph: boss raises up
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void ShockwaveUpdate()
    {
        shockwaveDelay -= Time.deltaTime;
        int maxRings = phase2 ? 4 : 3;

        if (shockwaveDelay <= 0 && shockwaveRingsSpawned < maxRings)
        {
            SpawnShockwaveRing(shockwaveRingsSpawned);
            shockwaveRingsSpawned++;
            shockwaveDelay = 0.4f;

            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.2f);
            SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);
        }

        if (shockwaveRingsSpawned >= maxRings && shockwaveDelay <= 0)
        {
            state = State.Recover;
            stateTimer = phase2 ? 0.5f : 0.8f;
            actionCooldown = 0.6f;
        }
    }

    void SpawnShockwaveRing(int index)
    {
        float startRadius = 1.5f + index * 2.5f;
        var ring = new GameObject("ShockwaveRing");
        ring.transform.position = transform.position;
        var sw = ring.AddComponent<ExpandingShockwave>();
        sw.Setup(startRadius, startRadius + 1.5f, phase2 ? 6f : 4.5f, slamDamage, 0.8f);
        Destroy(ring, 1.2f);
    }

    // --- PILLAR CHARGE: dash forward leaving stone pillars ---
    void StartPillarCharge(Vector3 dir)
    {
        state = State.PillarCharge;
        chargeDir = dir;
        stateTimer = 0.6f;

        // Telegraph: flash and line indicator
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = new Color(0.8f, 0.6f, 0.2f);

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void PillarChargeUpdate(float speedMult)
    {
        float chargeSpeed = phase2 ? 16f : 12f;
        rb.MovePosition(transform.position + chargeDir * chargeSpeed * speedMult * Time.deltaTime);

        stateTimer -= Time.deltaTime;

        // Spawn pillars along charge path
        if (Random.value < 0.4f)
            SpawnStonePillar(transform.position + Random.insideUnitSphere * 1f);

        // Damage on contact
        DamagePlayerInRadius(1.5f, slamDamage);

        if (stateTimer <= 0)
        {
            // End charge with a slam
            DamagePlayerInRadius(2.5f, slamDamage);
            SpawnSlamVFX(transform.position, 2.5f, new Color(0.5f, 0.4f, 0.2f));

            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.4f);
            SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = baseColor;

            state = State.Recover;
            stateTimer = 0.6f;
            actionCooldown = 0.48f;
        }
    }

    void SpawnStonePillar(Vector3 pos)
    {
        pos.y = 0;
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = "StonePillar";
        pillar.transform.position = pos + Vector3.up * 0.75f;
        pillar.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
        pillar.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.45f, 0.4f, 0.3f));
        walls.Add(pillar);
        Destroy(pillar, 4f);
    }

    // --- RECOVER: brief punish window ---
    void RecoverUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            state = State.Chase;
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = baseColor;
        }
    }

    // --- PHASE 2: arena closing walls ---
    void EnterPhase2()
    {
        // Dramatic pause
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.08f);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.6f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

        // Visual change
        baseColor = new Color(0.6f, 0.3f, 0.15f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = baseColor;

        // Spawn arena walls to shrink the fight area
        SpawnArenaWalls();

        moveSpeed *= 1.3f;
    }

    void SpawnArenaWalls()
    {
        float arenaSize = 8f;
        Vector3 center = (transform.position + target.position) * 0.5f;
        center.y = 0;

        // 4 walls forming a smaller arena
        Vector3[] offsets = {
            new Vector3(arenaSize, 0, 0), new Vector3(-arenaSize, 0, 0),
            new Vector3(0, 0, arenaSize), new Vector3(0, 0, -arenaSize)
        };
        Vector3[] scales = {
            new Vector3(0.5f, 2f, arenaSize * 2), new Vector3(0.5f, 2f, arenaSize * 2),
            new Vector3(arenaSize * 2, 2f, 0.5f), new Vector3(arenaSize * 2, 2f, 0.5f)
        };

        for (int i = 0; i < 4; i++)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "ArenaWall";
            w.transform.position = center + offsets[i] + Vector3.up;
            w.transform.localScale = scales[i];
            w.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.35f, 0.25f, 0.15f));
            arenaWalls.Add(w);
            walls.Add(w);
            Destroy(w, 30f);
        }
    }

    void ArenaCloseUpdate(Vector3 toPlayer, float dist, float speedMult)
    {
        // Not used currently — arena walls are static after spawn
        state = State.Chase;
    }

    // --- UTILITY ---
    void DamagePlayerInRadius(float radius, int damage)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead) hp.TakeDamage(damage);
        }
    }

    void SpawnSlamVFX(Vector3 pos, float radius, Color color)
    {
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(vfx.GetComponent<CapsuleCollider>());
        vfx.transform.position = pos + Vector3.up * 0.05f;
        vfx.transform.localScale = new Vector3(radius * 2, 0.05f, radius * 2);
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(color, 2f);
        Destroy(vfx, 0.25f);
    }

    void SpawnWall()
    {
        if (target == null) return;
        // Spawn wall to cut off player's retreat path
        Vector3 behindPlayer = target.position + (target.position - transform.position).normalized * 2f;
        behindPlayer.y = 0;
        Vector3 perp = Vector3.Cross((target.position - transform.position).normalized, Vector3.up);

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "WardenWall";
        wall.transform.position = behindPlayer + Vector3.up * 0.75f;
        wall.transform.localScale = new Vector3(3.5f, 1.5f, 0.4f);
        wall.transform.rotation = Quaternion.LookRotation(perp);
        wall.GetComponent<Renderer>().material = ShaderCache.NewLit(new Color(0.4f, 0.35f, 0.25f));
        walls.Add(wall);
        Destroy(wall, phase2 ? 6f : 5f);
    }

    void CleanupWalls()
    {
        foreach (var w in walls) if (w != null) Destroy(w);
        walls.Clear();
        foreach (var w in arenaWalls) if (w != null) Destroy(w);
        arenaWalls.Clear();
    }

    void OnDestroy() { CleanupWalls(); }
}

/// <summary>Expanding shockwave ring that damages the player on contact.</summary>
public class ExpandingShockwave : MonoBehaviour
{
    float innerRadius, outerRadius, expandSpeed;
    int damage;
    float currentRadius;
    float lifetime;
    GameObject ringVisual;
    bool hasHit;

    public void Setup(float inner, float outer, float speed, int dmg, float life)
    {
        innerRadius = inner;
        outerRadius = outer;
        expandSpeed = speed;
        damage = dmg;
        lifetime = life;
        currentRadius = 0.1f;

        // Visual ring
        ringVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ringVisual.GetComponent<CapsuleCollider>());
        ringVisual.transform.parent = transform;
        ringVisual.transform.localPosition = Vector3.up * 0.05f;
        ringVisual.GetComponent<Renderer>().material =
            ShaderCache.NewEmissive(new Color(0.7f, 0.4f, 0.15f, 0.7f), 2f);
    }

    void Update()
    {
        currentRadius += expandSpeed * Time.deltaTime;
        lifetime -= Time.deltaTime;

        if (ringVisual != null)
        {
            ringVisual.transform.localScale = new Vector3(currentRadius * 2, 0.04f, currentRadius * 2);
            // Fade out
            var r = ringVisual.GetComponent<Renderer>();
            if (r != null)
            {
                Color c = r.material.color;
                c.a = Mathf.Lerp(0.7f, 0f, 1f - lifetime / 0.8f);
                r.material.color = c;
            }
        }

        // Check player overlap with the ring edge (must dodge through gap)
        if (!hasHit)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                float dist = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(player.transform.position.x, 0, player.transform.position.z));

                // Hit if player is in the ring band
                if (dist >= currentRadius - 0.5f && dist <= currentRadius + 0.5f)
                {
                    if (!player.isInvulnerable)
                    {
                        var hp = player.GetComponent<Health>();
                        if (hp != null && !hp.IsDead)
                        {
                            hp.TakeDamage(damage);
                            hasHit = true;
                        }
                    }
                }
            }
        }

        if (lifetime <= 0)
            Destroy(gameObject);
    }
}
