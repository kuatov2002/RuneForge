using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

public class SwarmQueenBoss : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float teleportCooldown = 5f;
    public float spawnCooldown = 4f;
    public int spawnCount = 3;
    public Color baseColor = new Color(0.7f, 0.5f, 0.1f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    bool phase2;
    BossEnrage enrage;
    List<GameObject> minions = new();

    // State machine
    enum State { Reposition, AcidBarrage, CommandSwarm, BodySlam, Absorb, SummonBurst, Recover }
    State state = State.Reposition;
    float stateTimer;
    float actionCooldown = 1.2f;
    float spawnTimer;
    float teleportTimer;

    // Acid barrage
    int acidShotsRemaining;
    float acidShotDelay;

    // Body slam
    Vector3 slamTarget;
    Vector3 slamStart;
    float slamProgress;

    // Absorb
    float absorbTimer;
    bool hasAbsorbed;

    // Swarm command
    Vector3 commandTarget;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        var elemData = gameObject.GetComponent<EnemyElementData>();
        if (elemData == null) elemData = gameObject.AddComponent<EnemyElementData>();
        elemData.AssignBossImmunity(ElementType.Poison);

        if (health != null)
        {
            health.OnDeath += () => { isDead = true; CleanupMinions(); Destroy(gameObject, 0.5f); };
            health.OnHPChanged += (cur, max) =>
            {
                if (!phase2 && cur < max / 2)
                {
                    phase2 = true;
                    EnterPhase2();
                }
            };
        }

        spawnTimer = 1f;
        teleportTimer = teleportCooldown;
        enrage = GetComponent<BossEnrage>();
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        minions.RemoveAll(m => m == null);

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        switch (state)
        {
            case State.Reposition:
                RepositionUpdate(toPlayer, dist, speedMult);
                break;
            case State.AcidBarrage:
                AcidBarrageUpdate();
                break;
            case State.CommandSwarm:
                CommandSwarmUpdate();
                break;
            case State.BodySlam:
                BodySlamUpdate();
                break;
            case State.Absorb:
                AbsorbUpdate();
                break;
            case State.SummonBurst:
                SummonBurstUpdate();
                break;
            case State.Recover:
                RecoverUpdate();
                break;
        }
    }

    // --- REPOSITION: keep distance, spawn minions, pick attacks ---
    void RepositionUpdate(Vector3 toPlayer, float dist, float speedMult)
    {
        transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Keep at medium range (5-8m)
        float enrageSpeed = enrage != null ? enrage.SpeedMultiplier : 1f;
        float speed = (phase2 ? moveSpeed * 1.3f : moveSpeed) * enrageSpeed;
        if (dist < 4f)
        {
            // Flee
            rb.MovePosition(transform.position - toPlayer.normalized * speed * speedMult * Time.deltaTime);
        }
        else if (dist > 9f)
        {
            // Close in
            rb.MovePosition(transform.position + toPlayer.normalized * speed * 0.5f * speedMult * Time.deltaTime);
        }
        else
        {
            // Strafe
            Vector3 strafe = Vector3.Cross(toPlayer.normalized, Vector3.up);
            rb.MovePosition(transform.position + strafe * speed * 0.7f * speedMult * Time.deltaTime);
        }

        // Spawn minions periodically
        spawnTimer -= Time.deltaTime;
        float cd = phase2 ? spawnCooldown * 0.5f : spawnCooldown;
        if (spawnTimer <= 0 && minions.Count < (phase2 ? 8 : 5))
        {
            spawnTimer = cd;
            SpawnMinions(phase2 ? 2 : spawnCount);
        }

        // Teleport if player gets too close
        teleportTimer -= Time.deltaTime;
        if (dist < 3f && teleportTimer <= 0)
        {
            teleportTimer = phase2 ? teleportCooldown * 0.6f : teleportCooldown;
            DoTeleport();
            return;
        }

        // Pick attacks
        float cdSpeed = enrage != null ? 1f / enrage.CooldownMultiplier : 1f;
        actionCooldown -= Time.deltaTime * cdSpeed;
        if (actionCooldown > 0) return;

        if (dist < 3.5f && Random.value < 0.5f)
        {
            StartBodySlam();
        }
        else if (minions.Count >= 2 && Random.value < 0.4f)
        {
            StartCommandSwarm();
        }
        else if (phase2 && !hasAbsorbed && minions.Count >= 3 && health.currentHP < health.maxHP * 0.3f)
        {
            StartAbsorb();
        }
        else
        {
            StartAcidBarrage();
        }
    }

    // --- ACID BARRAGE: rapid spread of poison projectiles ---
    void StartAcidBarrage()
    {
        state = State.AcidBarrage;
        acidShotsRemaining = phase2 ? 8 : 5;
        acidShotDelay = 0f;
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void AcidBarrageUpdate()
    {
        acidShotDelay -= Time.deltaTime;
        if (acidShotDelay <= 0 && acidShotsRemaining > 0)
        {
            FireAcidSpit();
            acidShotsRemaining--;
            acidShotDelay = phase2 ? 0.12f : 0.18f;
        }

        // Face player
        Vector3 toP = target.position - transform.position; toP.y = 0;
        if (toP.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toP.normalized), 5f * Time.deltaTime);

        if (acidShotsRemaining <= 0)
        {
            state = State.Recover;
            stateTimer = 0.6f;
            actionCooldown = phase2 ? 0.6f : 0.9f;
        }
    }

    void FireAcidSpit()
    {
        Vector3 toP = (target.position + Vector3.up * 0.5f - transform.position).normalized;
        // Add spread
        float spread = phase2 ? 15f : 10f;
        Vector3 dir = Quaternion.Euler(0, Random.Range(-spread, spread), 0) * toP;

        var projGO = EnemyProjectile.Create(
            transform.position + Vector3.up * 1f + transform.forward * 0.5f,
            dir, 7f, 1, new Color(0.3f, 0.7f, 0.1f), null);

        // Acid pools on impact
        if (projGO != null)
            projGO.AddComponent<AcidPoolOnImpact>();
    }

    // --- COMMAND SWARM: direct all minions to charge the player ---
    void StartCommandSwarm()
    {
        state = State.CommandSwarm;
        commandTarget = target.position;
        stateTimer = 0.5f;

        // Visual: queen glows, sends signal
        foreach (var r in renderers)
            if (r != null) r.material.color = new Color(1f, 0.8f, 0.2f);
        SFXSystem.Play(SFXSystem.SFXType.DualCast, transform.position);

        // Command each minion to rush the player
        foreach (var m in minions)
        {
            if (m == null) continue;
            var ai = m.GetComponent<SwarmAI>();
            if (ai != null) ai.CommandCharge(commandTarget);
        }
    }

    void CommandSwarmUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            foreach (var r in renderers)
                if (r != null) r.material.color = baseColor;
            state = State.Reposition;
            actionCooldown = 1.2f;
        }
    }

    // --- BODY SLAM: leap onto player when they get close ---
    void StartBodySlam()
    {
        state = State.BodySlam;
        slamTarget = target.position;
        slamStart = transform.position;
        slamProgress = 0f;
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void BodySlamUpdate()
    {
        slamProgress += Time.deltaTime / 0.35f;
        if (slamProgress >= 1f)
        {
            slamProgress = 1f;
            transform.position = new Vector3(slamTarget.x, 0, slamTarget.z);

            // Damage + spawn acid pool at landing
            float radius = 2.5f;
            float dmgMult = enrage != null ? enrage.DamageMultiplier : 1f;
            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var h in hits)
            {
                var pc = h.GetComponent<PlayerController>();
                if (pc == null || pc.isInvulnerable) continue;
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead) hp.TakeDamage(Mathf.CeilToInt(3 * dmgMult));
            }

            // Acid pool at landing
            SpawnAcidPool(transform.position, 2.5f);

            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.4f);
            if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.04f);
            SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

            state = State.Recover;
            stateTimer = 0.7f;
            actionCooldown = 0.9f;
            return;
        }

        // Parabolic arc
        Vector3 pos = Vector3.Lerp(slamStart, slamTarget, slamProgress);
        pos.y = Mathf.Sin(slamProgress * Mathf.PI) * 3f;
        transform.position = pos;
    }

    // --- ABSORB: consume remaining minions to heal and power up ---
    void StartAbsorb()
    {
        state = State.Absorb;
        absorbTimer = 1.5f;
        hasAbsorbed = true;

        foreach (var r in renderers)
            if (r != null) r.material.color = new Color(0.9f, 0.2f, 0.1f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void AbsorbUpdate()
    {
        absorbTimer -= Time.deltaTime;

        // Pull minions toward self
        foreach (var m in minions)
        {
            if (m == null) continue;
            Vector3 toSelf = transform.position - m.transform.position;
            m.transform.position += toSelf.normalized * 8f * Time.deltaTime;

            if (toSelf.magnitude < 1f)
            {
                // Absorb: heal + destroy minion
                health.Heal(3);
                Destroy(m);
            }
        }
        minions.RemoveAll(m => m == null);

        // Pulsing visual
        float pulse = Mathf.PingPong(Time.time * 8f, 1f);
        foreach (var r in renderers)
            if (r != null)
                r.material.SetColor("_EmissionColor", new Color(0.8f, 0.2f, 0f) * pulse * 3f);

        if (absorbTimer <= 0)
        {
            // Power burst: AoE damage
            float radius = 4f;
            float burstDmgMult = enrage != null ? enrage.DamageMultiplier : 1f;
            Collider[] hits = Physics.OverlapSphere(transform.position, radius);
            foreach (var h in hits)
            {
                var pc = h.GetComponent<PlayerController>();
                if (pc == null || pc.isInvulnerable) continue;
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead) hp.TakeDamage(Mathf.CeilToInt(4 * burstDmgMult));
            }

            // VFX explosion
            var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(vfx.GetComponent<SphereCollider>());
            vfx.transform.position = transform.position + Vector3.up;
            vfx.transform.localScale = Vector3.one * radius * 2;
            vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.8f, 0.3f, 0f, 0.5f), 4f);
            Destroy(vfx, 0.3f);

            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.6f);
            SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

            foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.material.color = new Color(0.9f, 0.4f, 0.1f);
                    r.material.SetColor("_EmissionColor", Color.black);
                }
            }

            // Post-absorb: stronger stats
            moveSpeed *= 1.3f;
            baseColor = new Color(0.9f, 0.4f, 0.1f);

            state = State.Recover;
            stateTimer = 1f;
            actionCooldown = 1f;
        }
    }

    // --- SUMMON BURST (phase 2): spawn a ring of minions around the player ---
    void SummonBurstUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            // Surround player with minions
            int count = 6;
            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3f;
                SpawnSingleMinion(target.position + offset);
            }

            SFXSystem.Play(SFXSystem.SFXType.Explosion, target.position);

            state = State.Reposition;
            actionCooldown = 1.2f;
        }
    }

    // --- RECOVER ---
    void RecoverUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
            state = State.Reposition;
    }

    // --- PHASE 2 ---
    void EnterPhase2()
    {
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.06f);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.5f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

        baseColor = new Color(0.8f, 0.3f, 0.05f);
        foreach (var r in renderers)
            if (r != null) r.material.color = baseColor;

        // Immediate summon burst
        state = State.SummonBurst;
        stateTimer = 0.5f;
    }

    // --- TELEPORT ---
    void DoTeleport()
    {
        Vector3 oldPos = transform.position;
        // Teleport to a flanking position
        Vector3 toP = target.position - transform.position;
        Vector3 perp = Vector3.Cross(toP.normalized, Vector3.up) * (Random.value > 0.5f ? 1 : -1);
        Vector3 newPos = target.position + perp * Random.Range(6f, 9f);
        newPos.y = 0;
        transform.position = newPos;

        // Leave acid pool at old position
        SpawnAcidPool(oldPos, 2f);

        // VFX
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = oldPos + Vector3.up;
        vfx.transform.localScale = Vector3.one * 2f;
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.4f, 0.6f, 0.1f, 0.5f), 2f);
        Destroy(vfx, 0.3f);
    }

    // --- HELPERS ---
    void SpawnMinions(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 2f; offset.y = 0;
            SpawnSingleMinion(transform.position + offset);
        }
    }

    void SpawnSingleMinion(Vector3 pos)
    {
        var minion = new GameObject("QueenMinion");
        var sc = minion.AddComponent<SphereCollider>();
        sc.radius = 0.2f; sc.center = new Vector3(0, 0.2f, 0);
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body"; Object.Destroy(body.GetComponent<SphereCollider>());
        body.transform.parent = minion.transform;
        body.transform.localPosition = new Vector3(0, 0.2f, 0);
        body.transform.localScale = Vector3.one * 0.2f;
        body.GetComponent<Renderer>().material = ShaderCache.NewLit(baseColor);

        pos.y = 0;
        minion.transform.position = pos;

        var mrb = minion.AddComponent<Rigidbody>(); mrb.useGravity = false; mrb.isKinematic = true;
        var mhp = minion.AddComponent<Health>(); mhp.maxHP = 3; mhp.currentHP = 3;
        minion.AddComponent<EnemyHealthBar>();
        var ai = minion.AddComponent<SwarmAI>(); ai.baseColor = baseColor;

        var bootstrap = FindAnyObjectByType<Bootstrap>();
        if (bootstrap != null)
        {
            var enemyRef = minion;
            mhp.OnDeath += () => { if (bootstrap != null) bootstrap.OnMinionDeath(enemyRef); };
        }

        minions.Add(minion);
    }

    void SpawnAcidPool(Vector3 pos, float radius)
    {
        pos.y = 0.05f;
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pool.GetComponent<CapsuleCollider>());
        pool.name = "AcidPool";
        pool.transform.position = pos;
        pool.transform.localScale = new Vector3(radius * 2, 0.04f, radius * 2);
        pool.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.2f, 0.7f, 0.1f, 0.6f), 1.5f);

        var pz = pool.AddComponent<PoisonZone>();
        pz.dps = 1.5f;
        pz.radius = radius;

        Destroy(pool, 5f);
    }

    void CleanupMinions()
    {
        foreach (var m in minions) if (m != null) Destroy(m);
        minions.Clear();
    }

    void OnDestroy() { CleanupMinions(); }
}

/// <summary>Spawns an acid pool where the projectile lands.</summary>
public class AcidPoolOnImpact : MonoBehaviour
{
    void OnDestroy()
    {
        // When the projectile is destroyed (hit or timeout), leave an acid puddle
        Vector3 pos = transform.position; pos.y = 0.05f;
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(pool.GetComponent<CapsuleCollider>());
        pool.name = "AcidPuddle";
        pool.transform.position = pos;
        pool.transform.localScale = new Vector3(1.5f, 0.03f, 1.5f);
        pool.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.2f, 0.6f, 0.1f, 0.5f), 1f);

        var pz = pool.AddComponent<PoisonZone>();
        pz.dps = 1f;
        pz.radius = 0.75f;

        Object.Destroy(pool, 3f);
    }
}
