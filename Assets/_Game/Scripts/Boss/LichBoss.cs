using UnityEngine;
using System.Collections.Generic;

public class LichBoss : MonoBehaviour
{
    public float teleportCooldown = 2.5f;
    public float orbSpeed = 6f;
    public int orbDamage = 2;
    public float orbHomingTurn = 100f;
    public Color baseColor = new Color(0.3f, 0.1f, 0.5f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    bool phase2;

    enum State { Float, TeleportChain, OrbBarrage, BeamSweep, VoidPull, SummonPillars, Recover }
    State state = State.Float;
    float stateTimer;
    float actionCooldown = 0.9f;
    float passiveOrbTimer;

    // Teleport chain
    int teleportChainCount;
    int teleportChainMax;
    float teleportChainDelay;
    bool isTeleporting;
    float teleportImmuneTimer;

    // Orb barrage
    int orbsRemaining;
    float orbDelay;
    int barragePattern; // 0=spread, 1=spiral, 2=aimed burst

    // Beam sweep
    float beamAngle;
    float beamSweepSpeed;
    float beamSweepDir;
    GameObject beamVisual;
    float beamDamageTimer;

    // Void pull
    float voidPullRadius;
    float voidPullForce;
    GameObject voidZone;

    // Poison zones
    List<GameObject> hazards = new();

    public bool IsImmune => isTeleporting;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        var elemData = gameObject.GetComponent<EnemyElementData>();
        if (elemData == null) elemData = gameObject.AddComponent<EnemyElementData>();
        elemData.AssignBossImmunity(ElementType.Void);

        if (health != null)
        {
            health.OnDeath += () =>
            {
                isDead = true;
                CleanupHazards();
                if (beamVisual != null) Destroy(beamVisual);
                if (voidZone != null) Destroy(voidZone);
                Destroy(gameObject, 0.5f);
            };
            health.OnHPChanged += (cur, max) =>
            {
                if (!phase2 && cur < max / 2)
                {
                    phase2 = true;
                    EnterPhase2();
                }
            };
        }

        passiveOrbTimer = 2f;
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        // Teleport immune window
        if (isTeleporting)
        {
            teleportImmuneTimer -= Time.deltaTime;
            if (teleportImmuneTimer <= 0)
                isTeleporting = false;
            return;
        }

        switch (state)
        {
            case State.Float:
                FloatUpdate(speedMult);
                break;
            case State.TeleportChain:
                TeleportChainUpdate();
                break;
            case State.OrbBarrage:
                OrbBarrageUpdate();
                break;
            case State.BeamSweep:
                BeamSweepUpdate();
                break;
            case State.VoidPull:
                VoidPullUpdate();
                break;
            case State.SummonPillars:
                SummonPillarsUpdate();
                break;
            case State.Recover:
                RecoverUpdate();
                break;
        }
    }

    // --- FLOAT: hover menacingly, fire passive orbs, pick attacks ---
    void FloatUpdate(float speedMult)
    {
        Vector3 toPlayer = target.position - transform.position; toPlayer.y = 0;
        float dist = toPlayer.magnitude;
        transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Keep at range, strafe
        float speed = phase2 ? 4f : 3f;
        if (dist < 5f)
        {
            // Back away while strafing
            Vector3 away = -toPlayer.normalized;
            Vector3 strafe = Vector3.Cross(toPlayer.normalized, Vector3.up);
            rb.MovePosition(transform.position + (away * 0.7f + strafe * 0.5f) * speed * speedMult * Time.deltaTime);
        }
        else if (dist > 10f)
        {
            rb.MovePosition(transform.position + toPlayer.normalized * speed * speedMult * Time.deltaTime);
        }
        else
        {
            // Strafe at comfortable range
            Vector3 strafe = Vector3.Cross(toPlayer.normalized, Vector3.up);
            rb.MovePosition(transform.position + strafe * speed * 0.6f * speedMult * Time.deltaTime);
        }

        // Passive orb fire (constant pressure)
        passiveOrbTimer -= Time.deltaTime;
        float passiveCD = phase2 ? 1.2f : 1.8f;
        if (passiveOrbTimer <= 0)
        {
            passiveOrbTimer = passiveCD;
            FireVoidOrb(toPlayer.normalized);
        }

        // Attack selection
        actionCooldown -= Time.deltaTime;
        if (actionCooldown > 0) return;

        float r = Random.value;
        if (dist < 5f)
        {
            // Too close — teleport chain away
            StartTeleportChain();
        }
        else if (r < 0.3f)
        {
            StartOrbBarrage();
        }
        else if (r < 0.5f)
        {
            StartTeleportChain();
        }
        else if (r < 0.7f && phase2)
        {
            StartBeamSweep();
        }
        else if (r < 0.85f && phase2)
        {
            StartVoidPull();
        }
        else
        {
            StartOrbBarrage();
        }
    }

    // --- TELEPORT CHAIN: rapid multi-teleport leaving hazards ---
    void StartTeleportChain()
    {
        state = State.TeleportChain;
        teleportChainMax = phase2 ? Random.Range(3, 5) : Random.Range(2, 3);
        teleportChainCount = 0;
        teleportChainDelay = 0f;

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void TeleportChainUpdate()
    {
        teleportChainDelay -= Time.deltaTime;
        if (teleportChainDelay <= 0)
        {
            DoTeleport();
            // Fire orb at player from new position
            Vector3 toP = target.position - transform.position; toP.y = 0;
            FireVoidOrb(toP.normalized);

            teleportChainCount++;
            if (teleportChainCount >= teleportChainMax)
            {
                state = State.Float;
                actionCooldown = phase2 ? 0.6f : 0.9f;
            }
            else
            {
                teleportChainDelay = phase2 ? 0.35f : 0.5f;
            }
        }
    }

    void DoTeleport()
    {
        Vector3 oldPos = transform.position;

        // Teleport to flanking position around player
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist = Random.Range(5f, 9f);
        Vector3 newPos = target.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;
        newPos.y = 0;
        transform.position = newPos;

        isTeleporting = true;
        teleportImmuneTimer = 0.3f;

        // VFX at old position
        CreateTeleportVFX(oldPos);
        CreateTeleportVFX(newPos);

        // Leave poison zone at old position
        if (phase2)
            CreatePoisonZone(oldPos);

        SFXSystem.Play(SFXSystem.SFXType.Dash, transform.position);
    }

    // --- ORB BARRAGE: varied projectile patterns ---
    void StartOrbBarrage()
    {
        state = State.OrbBarrage;
        barragePattern = Random.Range(0, 3);
        orbDelay = 0f;

        switch (barragePattern)
        {
            case 0: // Spread fan
                orbsRemaining = phase2 ? 10 : 7;
                break;
            case 1: // Spiral
                orbsRemaining = phase2 ? 16 : 12;
                break;
            case 2: // Aimed burst
                orbsRemaining = phase2 ? 6 : 4;
                break;
        }

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
        // Glow during barrage
        foreach (var r in renderers)
            if (r != null) r.material.SetColor("_EmissionColor", new Color(0.5f, 0f, 1f) * 3f);
    }

    void OrbBarrageUpdate()
    {
        // Face player
        Vector3 toP = target.position - transform.position; toP.y = 0;
        if (toP.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toP.normalized), 4f * Time.deltaTime);

        orbDelay -= Time.deltaTime;
        if (orbDelay <= 0 && orbsRemaining > 0)
        {
            switch (barragePattern)
            {
                case 0: // Spread fan
                    float totalArc = phase2 ? 120f : 90f;
                    float angleStep = totalArc / Mathf.Max(1, (phase2 ? 10 : 7) - 1);
                    int idx = (phase2 ? 10 : 7) - orbsRemaining;
                    float spreadAngle = -totalArc / 2f + idx * angleStep;
                    Vector3 spreadDir = Quaternion.Euler(0, spreadAngle, 0) * toP.normalized;
                    FireVoidOrb(spreadDir, 0.5f); // Reduced homing for spread
                    orbDelay = 0.08f;
                    break;

                case 1: // Spiral
                    float spiralAngle = (16 - orbsRemaining) * 30f;
                    Vector3 spiralDir = Quaternion.Euler(0, spiralAngle, 0) * Vector3.forward;
                    FireVoidOrb(spiralDir, 0.3f);
                    orbDelay = 0.1f;
                    break;

                case 2: // Aimed burst — fast, high-homing
                    FireVoidOrb(toP.normalized, 1.5f);
                    orbDelay = phase2 ? 0.2f : 0.3f;
                    break;
            }

            orbsRemaining--;
            SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position, 0.3f);
        }

        if (orbsRemaining <= 0 && orbDelay <= 0)
        {
            foreach (var r in renderers)
                if (r != null) r.material.SetColor("_EmissionColor", Color.black);

            state = State.Recover;
            stateTimer = phase2 ? 0.5f : 0.8f;
            actionCooldown = phase2 ? 0.48f : 0.72f;
        }
    }

    // --- BEAM SWEEP (phase 2): rotating damage beam ---
    void StartBeamSweep()
    {
        state = State.BeamSweep;
        stateTimer = phase2 ? 2.5f : 2f;
        beamAngle = 0f;
        beamSweepSpeed = 180f;
        beamSweepDir = Random.value > 0.5f ? 1f : -1f;
        beamDamageTimer = 0f;

        // Create beam visual
        beamVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(beamVisual.GetComponent<BoxCollider>());
        beamVisual.transform.localScale = new Vector3(0.3f, 0.3f, 12f);
        beamVisual.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0f, 0.8f, 0.8f), 4f);

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);

        // Glow
        foreach (var r in renderers)
            if (r != null) r.material.SetColor("_EmissionColor", new Color(0.8f, 0f, 1f) * 4f);
    }

    void BeamSweepUpdate()
    {
        stateTimer -= Time.deltaTime;
        beamAngle += beamSweepSpeed * beamSweepDir * Time.deltaTime;

        // Update beam visual
        if (beamVisual != null)
        {
            Vector3 beamDir = Quaternion.Euler(0, beamAngle, 0) * Vector3.forward;
            beamVisual.transform.position = transform.position + beamDir * 6f + Vector3.up * 0.5f;
            beamVisual.transform.rotation = Quaternion.LookRotation(beamDir);
        }

        // Damage check along beam line
        beamDamageTimer -= Time.deltaTime;
        if (beamDamageTimer <= 0)
        {
            beamDamageTimer = 0.15f;
            Vector3 beamDir = Quaternion.Euler(0, beamAngle, 0) * Vector3.forward;

            // Check multiple points along beam
            for (float t = 1f; t <= 12f; t += 1.5f)
            {
                Vector3 point = transform.position + beamDir * t;
                Collider[] hits = Physics.OverlapSphere(point, 1f);
                foreach (var h in hits)
                {
                    var pc = h.GetComponent<PlayerController>();
                    if (pc == null || pc.isInvulnerable) continue;
                    var hp = h.GetComponent<Health>();
                    if (hp != null && !hp.IsDead)
                    {
                        hp.TakeDamage(orbDamage);
                        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.02f);
                    }
                }
            }
        }

        if (stateTimer <= 0)
        {
            if (beamVisual != null) Destroy(beamVisual);
            foreach (var r in renderers)
                if (r != null) r.material.SetColor("_EmissionColor", Color.black);

            state = State.Recover;
            stateTimer = 1f; // Punish window after beam
            actionCooldown = 0.6f;
        }
    }

    // --- VOID PULL (phase 2): gravity well that pulls player in ---
    void StartVoidPull()
    {
        state = State.VoidPull;
        stateTimer = 2.5f;
        voidPullRadius = 8f;
        voidPullForce = 5f;
        voidPullDamageTick = 0.5f;

        // Create void zone at player position
        Vector3 zonePos = target.position;
        zonePos.y = 0.05f;
        voidZone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(voidZone.GetComponent<CapsuleCollider>());
        voidZone.transform.position = zonePos;
        voidZone.transform.localScale = new Vector3(voidPullRadius * 2, 0.05f, voidPullRadius * 2);
        voidZone.GetComponent<Renderer>().material =
            ShaderCache.NewEmissive(new Color(0.2f, 0f, 0.4f, 0.5f), 2f);

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);

        // While pulling, fire orbs
        passiveOrbTimer = 0.5f;
    }

    float voidPullDamageTick;

    void VoidPullUpdate()
    {
        stateTimer -= Time.deltaTime;

        if (voidZone != null)
        {
            Vector3 zoneCenter = voidZone.transform.position;

            // Pull player toward center (use cached target, not FindAnyObjectByType)
            if (target != null)
            {
                var player = target.GetComponent<PlayerController>();
                if (player != null && !player.isInvulnerable)
                {
                    Vector3 toCenter = zoneCenter - player.transform.position;
                    toCenter.y = 0;
                    float dist = toCenter.magnitude;
                    if (dist < voidPullRadius && dist > 0.5f)
                    {
                        float pullStrength = voidPullForce * (1f - dist / voidPullRadius);
                        var prb = player.GetComponent<Rigidbody>();
                        if (prb != null)
                            prb.MovePosition(player.transform.position + toCenter.normalized * pullStrength * Time.deltaTime);
                    }

                    // Tick-based damage if in center (1 damage per 0.5s, not per-frame)
                    if (dist < 1.5f)
                    {
                        voidPullDamageTick -= Time.deltaTime;
                        if (voidPullDamageTick <= 0f)
                        {
                            voidPullDamageTick = 0.5f;
                            var hp = player.GetComponent<Health>();
                            if (hp != null && !hp.IsDead)
                                hp.TakeDamage(orbDamage);
                        }
                    }
                }
            }

            // Pulse visual
            float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.2f;
            voidZone.transform.localScale = new Vector3(voidPullRadius * 2 * pulse, 0.05f, voidPullRadius * 2 * pulse);
        }

        // Fire orbs while pulling
        passiveOrbTimer -= Time.deltaTime;
        if (passiveOrbTimer <= 0)
        {
            passiveOrbTimer = 0.8f;
            Vector3 toP = target.position - transform.position; toP.y = 0;
            FireVoidOrb(toP.normalized);
        }

        if (stateTimer <= 0)
        {
            // Collapse explosion at zone center
            if (voidZone != null)
            {
                Vector3 zonePos = voidZone.transform.position;
                Collider[] hits = Physics.OverlapSphere(zonePos, 3f);
                foreach (var h in hits)
                {
                    var pc = h.GetComponent<PlayerController>();
                    if (pc == null || pc.isInvulnerable) continue;
                    var hp = h.GetComponent<Health>();
                    if (hp != null && !hp.IsDead) hp.TakeDamage(orbDamage + 2);
                }
                Destroy(voidZone);
                voidZone = null;
            }

            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.4f);
            SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

            state = State.Recover;
            stateTimer = 0.8f;
            actionCooldown = 0.9f;
        }
    }

    // --- SUMMON PILLARS: create void pillars that block and fire ---
    void SummonPillarsUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            state = State.Float;
            actionCooldown = 1.5f;
        }
    }

    // --- RECOVER ---
    void RecoverUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
            state = State.Float;
    }

    // --- PHASE 2 ---
    void EnterPhase2()
    {
        // Brief immunity window during phase transition (gives player breathing room)
        isTeleporting = true;
        teleportImmuneTimer = 1.5f;

        // Dramatic hitstop + camera shake
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.15f);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.8f);
        SFXSystem.Play(SFXSystem.SFXType.BossIntro, transform.position);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);
        GameFeel.ScreenFlash(new Color(0.5f, 0f, 0.8f), 0.4f);

        // Color shift
        baseColor = new Color(0.4f, 0.05f, 0.6f);
        foreach (var r in renderers)
            if (r != null)
            {
                r.material.color = baseColor;
                r.material.SetColor("_EmissionColor", new Color(0.6f, 0f, 1f) * 3f);
            }

        // Shockwave VFX ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(ring.GetComponent<CapsuleCollider>());
        ring.transform.position = transform.position + Vector3.up * 0.1f;
        ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
        ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0f, 0.8f), 5f);
        ring.AddComponent<DeathRingEffect>().Init(8f, 0.8f);

        orbSpeed *= 1.3f;
        orbHomingTurn *= 1.2f;

        // After immunity ends, start aggressive teleport chain
        state = State.Recover;
        stateTimer = 1.5f;
        actionCooldown = 0.18f;
    }

    // --- ORB FIRING ---
    void FireVoidOrb(Vector3 dir, float homingMult = 1f)
    {
        var orb = new GameObject("VoidOrb");
        var sc = orb.AddComponent<SphereCollider>();
        sc.radius = 0.2f;
        sc.isTrigger = true;

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(body.GetComponent<SphereCollider>());
        body.transform.parent = orb.transform;
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one * 0.35f;
        var mat = ShaderCache.NewEmissive(new Color(0.4f, 0f, 0.6f));
        mat.SetColor("_EmissionColor", new Color(0.6f, 0f, 1f) * 3f);
        body.GetComponent<Renderer>().material = mat;

        orb.transform.position = transform.position + Vector3.up * 1f;
        var vo = orb.AddComponent<VoidOrbProjectile>();
        vo.Setup(dir, orbSpeed, orbDamage, orbHomingTurn * homingMult, target);

        var trail = orb.AddComponent<TrailRenderer>();
        trail.startWidth = 0.15f;
        trail.endWidth = 0f;
        trail.time = 0.3f;
        trail.material = ShaderCache.NewLit(new Color(0.5f, 0f, 0.8f));
        trail.startColor = new Color(0.5f, 0f, 0.8f);
        trail.endColor = new Color(0.5f, 0f, 0.8f, 0f);

        Destroy(orb, 5f);
    }

    // --- VFX ---
    void CreateTeleportVFX(Vector3 pos)
    {
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = Vector3.one * 1.5f;
        vfx.GetComponent<Renderer>().material =
            ShaderCache.NewEmissive(new Color(0.3f, 0f, 0.5f, 0.5f), 2f);
        Destroy(vfx, 0.3f);
    }

    void CreatePoisonZone(Vector3 pos)
    {
        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(zone.GetComponent<CapsuleCollider>());
        zone.name = "PoisonZone";
        zone.transform.position = pos + Vector3.up * 0.05f;
        float radius = 2f;
        zone.transform.localScale = new Vector3(radius * 2, 0.05f, radius * 2);
        zone.GetComponent<Renderer>().material =
            ShaderCache.NewEmissive(new Color(0.1f, 0.6f, 0.1f, 0.6f), 1.5f);

        var pz = zone.AddComponent<PoisonZone>();
        pz.dps = 1.5f;
        pz.radius = radius;

        hazards.Add(zone);
        Destroy(zone, 4f);
    }

    void CleanupHazards()
    {
        foreach (var h in hazards) if (h != null) Destroy(h);
        hazards.Clear();
    }

    void OnDestroy() { CleanupHazards(); }
}

// VoidOrbProjectile and PoisonZone are defined in the original — keeping them here
public class VoidOrbProjectile : MonoBehaviour
{
    Vector3 direction;
    float speed;
    float damage;
    float homingTurn;
    Transform target;

    public void Setup(Vector3 dir, float spd, float dmg, float turn, Transform tgt)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        homingTurn = turn;
        target = tgt;
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 toTarget = (target.position + Vector3.up * 0.5f - transform.position).normalized;
            direction = Vector3.RotateTowards(direction, toTarget, homingTurn * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
        }
        transform.position += direction * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;
        if (pc.isInvulnerable) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead) hp.TakeDamage(damage);
        Destroy(gameObject);
    }
}

public class PoisonZone : MonoBehaviour
{
    public float dps;
    public float radius;
    float tickTimer;

    void Update()
    {
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0) return;
        tickTimer = 0.5f;

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist > radius) return;
        if (player.isInvulnerable) return;
        var hp = player.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            hp.TakeDamage(dps * 0.5f);
    }
}
