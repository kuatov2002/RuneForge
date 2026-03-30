using UnityEngine;
using System.Collections.Generic;

public class RunebreakerBoss : MonoBehaviour
{
    public float moveSpeed = 4.5f;
    public float meleeRange = 2f;
    public int meleeDamage = 4;
    public Color baseColor = new Color(0.6f, 0.1f, 0.1f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    int phase = 1;
    SpellCaster playerCaster;

    enum State { Hunt, TeleportStrike, SwordCombo, RuneSteal, Shockwave, BoltBarrage, Berserker, Recover }
    State state = State.Hunt;
    float stateTimer;
    float actionCooldown = 0.9f;

    // Teleport strike
    int teleportStrikeCount;
    int teleportStrikeMax;
    float teleportStrikeDelay;
    Vector3 strikeTarget;

    // Sword combo
    int swordComboCount;
    int swordComboMax;
    float swordComboDelay;

    // Rune steal
    float runeStealTimer;
    ElementType stolenElement;
    bool hasStolen;

    // Bolt barrage
    int boltsRemaining;
    float boltDelay;

    // Berserker
    float berserkerTimer;
    float berserkerDashCooldown;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
            playerCaster = player.GetComponent<SpellCaster>();
        }
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        var elemData = gameObject.GetComponent<EnemyElementData>();
        if (elemData == null) elemData = gameObject.AddComponent<EnemyElementData>();
        elemData.AssignBossImmunity(ElementType.Earth);

        if (health != null)
        {
            health.OnDeath += () => { isDead = true; Destroy(gameObject, 0.5f); };
            health.OnHPChanged += OnHPChanged;
        }
    }

    void OnHPChanged(int cur, int max)
    {
        float ratio = (float)cur / max;
        if (ratio < 0.25f && phase < 3)
        {
            phase = 3;
            EnterPhase3();
        }
        else if (ratio < 0.6f && phase < 2)
        {
            phase = 2;
            EnterPhase2();
        }
    }

    void Update()
    {
        if (isDead || target == null) return;
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        switch (state)
        {
            case State.Hunt:
                HuntUpdate(toPlayer, dist, speedMult);
                break;
            case State.TeleportStrike:
                TeleportStrikeUpdate(speedMult);
                break;
            case State.SwordCombo:
                SwordComboUpdate(dist, speedMult);
                break;
            case State.RuneSteal:
                RuneStealUpdate();
                break;
            case State.Shockwave:
                ShockwaveUpdate();
                break;
            case State.BoltBarrage:
                BoltBarrageUpdate();
                break;
            case State.Berserker:
                BerserkerUpdate(toPlayer, dist, speedMult);
                break;
            case State.Recover:
                RecoverUpdate();
                break;
        }
    }

    // --- HUNT: aggressive pursuit, picks attacks ---
    void HuntUpdate(Vector3 toPlayer, float dist, float speedMult)
    {
        Vector3 dir = toPlayer.normalized;
        transform.rotation = Quaternion.LookRotation(dir);

        // Always closing distance
        float speed = moveSpeed * (phase >= 2 ? 1.3f : 1f);
        if (dist > meleeRange)
            rb.MovePosition(transform.position + dir * speed * speedMult * Time.deltaTime);

        actionCooldown -= Time.deltaTime;
        if (actionCooldown > 0) return;

        float r = Random.value;
        if (dist > 6f)
        {
            // Far — teleport strike to close gap
            if (r < 0.6f)
                StartTeleportStrike();
            else
                StartBoltBarrage();
        }
        else if (dist > 3f)
        {
            // Mid range
            if (r < 0.35f)
                StartTeleportStrike();
            else if (r < 0.6f)
                StartSwordCombo();
            else if (r < 0.8f && phase >= 2)
                StartRuneSteal();
            else
                StartBoltBarrage();
        }
        else
        {
            // Melee range
            if (r < 0.5f)
                StartSwordCombo();
            else if (r < 0.7f)
                StartShockwave();
            else if (r < 0.85f && phase >= 2)
                StartRuneSteal();
            else
                StartSwordCombo();
        }
    }

    // --- TELEPORT STRIKE: disappear then reappear behind player with attack ---
    void StartTeleportStrike()
    {
        state = State.TeleportStrike;
        teleportStrikeMax = phase >= 2 ? Random.Range(2, 4) : 1;
        teleportStrikeCount = 0;
        teleportStrikeDelay = 0.3f; // Initial delay (telegraph)

        // Flash — telegraph
        foreach (var r in renderers)
            if (r != null) r.material.color = new Color(1f, 0.3f, 0.3f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);

        // Briefly invisible
        foreach (var r in renderers) if (r != null) r.enabled = false;
    }

    void TeleportStrikeUpdate(float speedMult)
    {
        teleportStrikeDelay -= Time.deltaTime;
        if (teleportStrikeDelay > 0) return;

        // Reappear behind player
        Vector3 behindPlayer = target.position - target.forward * 1.5f;
        behindPlayer.y = 0;

        // Show telegraph at landing spot briefly
        var telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(telegraph.GetComponent<CapsuleCollider>());
        telegraph.transform.position = behindPlayer + Vector3.up * 0.03f;
        telegraph.transform.localScale = new Vector3(3f, 0.03f, 3f);
        telegraph.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.1f, 0.1f, 0.5f), 2f);
        Destroy(telegraph, 0.2f);

        transform.position = behindPlayer;
        foreach (var r in renderers) if (r != null) r.enabled = true;

        // Strike AoE
        float radius = 2.5f;
        DamagePlayerInRadius(radius, meleeDamage);
        SpawnStrikeVFX(transform.position, radius);

        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.35f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.04f);
        SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);

        // Face player
        Vector3 toP = target.position - transform.position; toP.y = 0;
        if (toP.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toP.normalized);

        teleportStrikeCount++;
        if (teleportStrikeCount >= teleportStrikeMax)
        {
            state = State.Recover;
            stateTimer = phase >= 2 ? 0.4f : 0.7f;
            actionCooldown = phase >= 2 ? 0.36f : 0.6f;
            foreach (var r in renderers)
                if (r != null) r.material.color = GetPhaseColor();
        }
        else
        {
            // Chain — disappear again
            teleportStrikeDelay = phase >= 2 ? 0.25f : 0.4f;
            foreach (var r in renderers) if (r != null) r.enabled = false;
            SFXSystem.Play(SFXSystem.SFXType.Dash, transform.position);
        }
    }

    // --- SWORD COMBO: multi-hit melee flurry tracking the player ---
    void StartSwordCombo()
    {
        state = State.SwordCombo;
        swordComboMax = phase switch
        {
            3 => Random.Range(4, 7),
            2 => Random.Range(3, 5),
            _ => Random.Range(2, 4)
        };
        swordComboCount = 0;
        swordComboDelay = 0.15f;

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void SwordComboUpdate(float dist, float speedMult)
    {
        swordComboDelay -= Time.deltaTime;

        // Chase during combo
        Vector3 toP = target.position - transform.position; toP.y = 0;
        transform.rotation = Quaternion.LookRotation(toP.normalized);

        float chaseSpeed = phase >= 2 ? 9f : 6f;
        if (toP.magnitude > 1.5f)
            rb.MovePosition(transform.position + toP.normalized * chaseSpeed * speedMult * Time.deltaTime);

        if (swordComboDelay <= 0)
        {
            // Slash
            float slashRadius = swordComboCount == swordComboMax - 1 ? meleeRange * 1.5f : meleeRange;
            int dmg = swordComboCount == swordComboMax - 1 ? meleeDamage + 2 : meleeDamage;
            DamagePlayerInRadius(slashRadius, dmg);
            SpawnStrikeVFX(transform.position, slashRadius);

            if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.03f);
            SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);

            swordComboCount++;
            if (swordComboCount >= swordComboMax)
            {
                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.4f);
                SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

                state = State.Recover;
                stateTimer = phase >= 3 ? 0.3f : (phase >= 2 ? 0.5f : 0.8f);
                actionCooldown = phase >= 2 ? 0.3f : 0.48f;
            }
            else
            {
                swordComboDelay = phase >= 3 ? 0.12f : (phase >= 2 ? 0.18f : 0.25f);
            }
        }

        // Flash during combo
        float flash = Mathf.PingPong(Time.time * 14f, 1f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = Color.Lerp(GetPhaseColor(), Color.white, flash * 0.4f);
    }

    // --- RUNE STEAL: steal player's active element and use it against them ---
    void StartRuneSteal()
    {
        state = State.RuneSteal;
        runeStealTimer = 0.8f; // Channel time
        hasStolen = false;

        foreach (var r in renderers)
            if (r != null) r.material.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 3f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void RuneStealUpdate()
    {
        runeStealTimer -= Time.deltaTime;

        // Pulsing glow
        float pulse = Mathf.PingPong(Time.time * 8f, 1f);
        foreach (var r in renderers)
            if (r != null)
                r.material.SetColor("_EmissionColor", new Color(1f, 0.2f, 0f) * (1f + pulse * 4f));

        // Face player
        Vector3 toP = target.position - transform.position; toP.y = 0;
        if (toP.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toP.normalized);

        if (runeStealTimer <= 0 && !hasStolen)
        {
            hasStolen = true;

            // Steal: overheat a random slot
            if (playerCaster != null)
                ForceOverheatRandomSlot();

            // Determine stolen element for counter-attack
            stolenElement = (ElementType)Random.Range(0, 4); // Fire, Water, Earth, Air

            // Fire the stolen element back
            FireStolenElement();

            foreach (var r in renderers)
                if (r != null) r.material.SetColor("_EmissionColor", Color.black);

            // Shockwave after steal
            CreateDisableVFX();
            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.4f);
            SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

            state = State.Recover;
            stateTimer = 0.5f;
            actionCooldown = 0.6f;
        }
    }

    void FireStolenElement()
    {
        // Fire a burst of projectiles colored by the stolen element
        Color elemColor = stolenElement switch
        {
            ElementType.Fire => new Color(1f, 0.3f, 0f),
            ElementType.Water => new Color(0.2f, 0.4f, 1f),
            ElementType.Earth => new Color(0.6f, 0.4f, 0.2f),
            ElementType.Air => new Color(0.8f, 0.9f, 1f),
            _ => Color.red
        };

        int count = phase >= 3 ? 8 : 5;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            EnemyProjectile.Create(
                transform.position + Vector3.up * 1f,
                dir, 8f, 3, elemColor, null);
        }
    }

    // --- SHOCKWAVE: ground slam with expanding ring ---
    void StartShockwave()
    {
        state = State.Shockwave;
        stateTimer = 0.4f; // Windup
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void ShockwaveUpdate()
    {
        stateTimer -= Time.deltaTime;

        // Flash during windup
        float flash = Mathf.PingPong(Time.time * 10f, 1f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = Color.Lerp(GetPhaseColor(), Color.white, flash);

        if (stateTimer <= 0)
        {
            // Big AoE slam
            float radius = phase >= 2 ? 3.5f : 2.5f;
            DamagePlayerInRadius(radius, meleeDamage + 1);
            SpawnStrikeVFX(transform.position, radius, new Color(0.8f, 0.2f, 0.1f));

            // Expanding shockwave ring
            var ring = new GameObject("RunebreakerShockwave");
            ring.transform.position = transform.position;
            var sw = ring.AddComponent<ExpandingShockwave>();
            sw.Setup(radius, radius + 2f, 6f, meleeDamage, 0.8f);
            Destroy(ring, 1.2f);

            if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.5f);
            if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
            SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = GetPhaseColor();

            state = State.Recover;
            stateTimer = phase >= 3 ? 0.3f : 0.6f;
            actionCooldown = 0.48f;
        }
    }

    // --- BOLT BARRAGE: rapid-fire ranged attacks ---
    void StartBoltBarrage()
    {
        state = State.BoltBarrage;
        boltsRemaining = phase switch
        {
            3 => Random.Range(6, 10),
            2 => Random.Range(4, 7),
            _ => Random.Range(3, 5)
        };
        boltDelay = 0f;
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void BoltBarrageUpdate()
    {
        boltDelay -= Time.deltaTime;

        // Face player
        Vector3 toP = target.position - transform.position; toP.y = 0;
        if (toP.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toP.normalized), 6f * Time.deltaTime);

        if (boltDelay <= 0 && boltsRemaining > 0)
        {
            Vector3 dir = toP.normalized;
            // Slight randomization for spread
            dir = Quaternion.Euler(0, Random.Range(-8f, 8f), 0) * dir;

            float boltSpeed = phase >= 3 ? 12f : 9f;
            EnemyProjectile.Create(
                transform.position + Vector3.up * 1f + transform.forward * 0.5f,
                dir, boltSpeed, 2, new Color(0.8f, 0.1f, 0.1f), null);

            boltsRemaining--;
            boltDelay = phase >= 3 ? 0.1f : 0.18f;
            SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position, 0.3f);
        }

        if (boltsRemaining <= 0)
        {
            state = State.Recover;
            stateTimer = 0.4f;
            actionCooldown = phase >= 2 ? 0.3f : 0.48f;
        }
    }

    // --- BERSERKER MODE (Phase 3): relentless aggression ---
    void BerserkerUpdate(Vector3 toPlayer, float dist, float speedMult)
    {
        berserkerTimer -= Time.deltaTime;

        Vector3 dir = toPlayer.normalized;
        transform.rotation = Quaternion.LookRotation(dir);

        // Extremely fast pursuit
        float speed = moveSpeed * 2f;
        rb.MovePosition(transform.position + dir * speed * speedMult * Time.deltaTime);

        // Continuous damage trail
        if (Random.value < 0.3f)
        {
            var trail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(trail.GetComponent<SphereCollider>());
            trail.transform.position = transform.position + Vector3.up * 0.1f;
            trail.transform.localScale = Vector3.one * 0.5f;
            trail.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0f, 0.5f), 3f);
            Destroy(trail, 0.3f);
        }

        // Rapid mini-dashes
        berserkerDashCooldown -= Time.deltaTime;
        if (berserkerDashCooldown <= 0 && dist > 2f)
        {
            berserkerDashCooldown = 0.5f;
            // Lunge forward
            transform.position += dir * 3f;
            DamagePlayerInRadius(2f, meleeDamage);
            SpawnStrikeVFX(transform.position, 2f, new Color(1f, 0.2f, 0f));
            SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);

            if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.03f);
        }

        // Continuous melee if close
        if (dist < meleeRange)
        {
            DamagePlayerInRadius(meleeRange, 2); // Rapid light hits
        }

        if (berserkerTimer <= 0)
        {
            // Exhaustion: big recovery window
            state = State.Recover;
            stateTimer = 1.5f;
            actionCooldown = 0.3f;
            moveSpeed /= 2f; // Temporarily slower after berserker

            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = GetPhaseColor();
        }
    }

    // --- PHASE TRANSITIONS ---
    void EnterPhase2()
    {
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.07f);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.5f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

        ForceOverheatRandomSlot();
        CreateDisableVFX();

        baseColor = new Color(0.8f, 0.2f, 0f);
        UpdateVisual();

        moveSpeed *= 1.2f;
    }

    void EnterPhase3()
    {
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.1f);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.7f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

        ForceOverheatRandomSlot();
        ForceOverheatRandomSlot(); // Overheat 2 slots
        CreateDisableVFX();

        baseColor = new Color(1f, 0.1f, 0.1f);
        UpdateVisual();

        moveSpeed *= 1.3f;

        // Enter berserker rage
        state = State.Berserker;
        berserkerTimer = 5f;
        berserkerDashCooldown = 0.3f;

        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
            {
                r.material.color = new Color(1f, 0.15f, 0.05f);
                r.material.SetColor("_EmissionColor", new Color(1f, 0.2f, 0f) * 4f);
            }
    }

    // --- RECOVER ---
    void RecoverUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                {
                    r.material.color = GetPhaseColor();
                    r.material.SetColor("_EmissionColor", Color.black);
                }
            state = State.Hunt;
        }
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

    void SpawnStrikeVFX(Vector3 pos, float radius, Color? color = null)
    {
        Color c = color ?? new Color(0.8f, 0.1f, 0.1f);
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(vfx.GetComponent<CapsuleCollider>());
        vfx.transform.position = pos + Vector3.up * 0.05f;
        vfx.transform.localScale = new Vector3(radius * 2, 0.05f, radius * 2);
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(c, 2f);
        Destroy(vfx, 0.2f);
    }

    Color GetPhaseColor()
    {
        return phase switch
        {
            2 => new Color(0.8f, 0.2f, 0f),
            3 => new Color(1f, 0.1f, 0.1f),
            _ => baseColor
        };
    }

    void UpdateVisual()
    {
        foreach (var r in renderers)
            if (r != null) r.material.color = GetPhaseColor();
    }

    void ForceOverheatRandomSlot()
    {
        if (playerCaster == null) return;
        int slotCount = playerCaster.equippedElements.Length;
        if (slotCount == 0) return;

        int attempts = 0;
        int slot;
        do
        {
            slot = Random.Range(0, slotCount);
            attempts++;
        }
        while (playerCaster.IsOverheated(slot) && attempts < slotCount * 2);

        playerCaster.ForceOverheat(slot);
    }

    void CreateDisableVFX()
    {
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(vfx.GetComponent<SphereCollider>());
        vfx.transform.position = transform.position + Vector3.up * 1f;
        vfx.transform.localScale = Vector3.one * 4f;
        var mat = ShaderCache.NewEmissive(new Color(0.9f, 0.1f, 0.1f, 0.5f), 4f);
        mat.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 4f);
        vfx.GetComponent<Renderer>().material = mat;
        Destroy(vfx, 0.4f);
    }
}
