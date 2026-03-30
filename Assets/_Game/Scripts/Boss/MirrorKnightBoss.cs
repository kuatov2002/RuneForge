using UnityEngine;
using System.Collections.Generic;

public class MirrorKnightBoss : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float chargeSpeed = 14f;
    public int attackDamage = 3;
    public Color baseColor = new Color(0.6f, 0.6f, 0.7f);

    Transform target;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;
    bool phase2;

    enum State { Stalk, DashCombo, Parry, ShieldBash, MirrorClones, SweepSlash, Recover }
    State state = State.Stalk;
    float stateTimer;
    float actionCooldown = 0.9f;

    // Dash combo
    int dashComboCount;
    int dashComboMax;
    Vector3 dashDir;
    float dashTimer;

    // Parry
    float parryWindow;
    bool parryTriggered;

    // Mirror clones
    List<GameObject> clones = new();

    // Sweep slash
    float sweepAngle;
    float sweepDir;

    // Shield reflects projectiles when not vulnerable
    bool isVulnerable;
    public bool IsShielded => !isVulnerable;

    public bool ShouldBlockDamage() => !isVulnerable;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        var elemData = gameObject.GetComponent<EnemyElementData>();
        if (elemData == null) elemData = gameObject.AddComponent<EnemyElementData>();
        elemData.AssignBossImmunity(ElementType.Air);

        if (health != null)
        {
            health.OnDeath += () => { isDead = true; CleanupClones(); Destroy(gameObject, 0.5f); };
            health.OnHPChanged += (cur, max) =>
            {
                if (!phase2 && cur < max / 2)
                {
                    phase2 = true;
                    EnterPhase2();
                }
            };
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
            case State.Stalk:
                StalkUpdate(toPlayer, dist, speedMult);
                break;
            case State.DashCombo:
                DashComboUpdate(speedMult);
                break;
            case State.Parry:
                ParryUpdate();
                break;
            case State.ShieldBash:
                ShieldBashUpdate(speedMult);
                break;
            case State.MirrorClones:
                MirrorClonesUpdate();
                break;
            case State.SweepSlash:
                SweepSlashUpdate(speedMult);
                break;
            case State.Recover:
                RecoverUpdate();
                break;
        }
    }

    // --- STALK: circle the player like a predator, pick attacks ---
    void StalkUpdate(Vector3 toPlayer, float dist, float speedMult)
    {
        Vector3 dir = toPlayer.normalized;
        transform.rotation = Quaternion.LookRotation(dir);

        // Circle the player at medium distance
        float speed = phase2 ? moveSpeed * 1.3f : moveSpeed;
        if (dist > 5f)
        {
            rb.MovePosition(transform.position + dir * speed * speedMult * Time.deltaTime);
        }
        else if (dist < 3f)
        {
            // Strafe and slightly back
            Vector3 strafe = Vector3.Cross(dir, Vector3.up);
            rb.MovePosition(transform.position + (strafe - dir * 0.3f) * speed * speedMult * Time.deltaTime);
        }
        else
        {
            // Aggressive circling
            Vector3 strafe = Vector3.Cross(dir, Vector3.up);
            rb.MovePosition(transform.position + strafe * speed * speedMult * Time.deltaTime);
        }

        actionCooldown -= Time.deltaTime;
        if (actionCooldown > 0) return;

        // Attack selection — always aggressive
        float r = Random.value;
        if (dist < 4f && r < 0.3f)
        {
            StartShieldBash(dir);
        }
        else if (r < 0.55f)
        {
            StartDashCombo(dir);
        }
        else if (r < 0.7f)
        {
            StartParry();
        }
        else if (r < 0.85f)
        {
            StartSweepSlash();
        }
        else if (phase2 && clones.Count == 0)
        {
            StartMirrorClones();
        }
        else
        {
            StartDashCombo(dir);
        }
    }

    // --- DASH COMBO: Malenia-style multi-slash dashes ---
    void StartDashCombo(Vector3 dir)
    {
        state = State.DashCombo;
        dashComboMax = phase2 ? Random.Range(3, 6) : Random.Range(2, 4);
        dashComboCount = 0;
        isVulnerable = false;
        StartNextDash();

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void StartNextDash()
    {
        // Aim at player with slight variation for unpredictability
        Vector3 toP = target.position - transform.position; toP.y = 0;
        float variance = dashComboCount == dashComboMax - 1 ? 0f : Random.Range(-25f, 25f);
        dashDir = Quaternion.Euler(0, variance, 0) * toP.normalized;
        dashTimer = 0.2f; // Each dash lasts 0.2s

        // Trail VFX
        var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(trail.GetComponent<BoxCollider>());
        trail.transform.position = transform.position + Vector3.up * 0.5f;
        trail.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
        trail.transform.rotation = transform.rotation;
        trail.GetComponent<Renderer>().material = ShaderCache.NewEmissive(
            new Color(0.7f, 0.7f, 0.9f, 0.4f), 2f);
        Destroy(trail, 0.3f);
    }

    void DashComboUpdate(float speedMult)
    {
        dashTimer -= Time.deltaTime;

        // Move fast
        float speed = phase2 ? chargeSpeed * 1.2f : chargeSpeed;
        rb.MovePosition(transform.position + dashDir * speed * speedMult * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(dashDir);

        // Damage on contact during dash
        Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead)
            {
                hp.TakeDamage(attackDamage);
                if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.04f);
            }
        }

        if (dashTimer <= 0)
        {
            dashComboCount++;
            SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);

            // Slash VFX at current position
            SpawnSlashVFX(transform.position);

            if (dashComboCount >= dashComboMax)
            {
                // Final slash: bigger AoE
                float finalRadius = phase2 ? 3f : 2.5f;
                Collider[] finalHits = Physics.OverlapSphere(transform.position, finalRadius);
                foreach (var h in finalHits)
                {
                    var pc = h.GetComponent<PlayerController>();
                    if (pc == null || pc.isInvulnerable) continue;
                    var hp = h.GetComponent<Health>();
                    if (hp != null && !hp.IsDead) hp.TakeDamage(attackDamage + 1);
                }

                SpawnSlashVFX(transform.position, finalRadius);
                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.4f);
                if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.05f);
                SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

                // Vulnerable window after combo
                isVulnerable = true;
                foreach (var r in renderers)
                    if (r != null) r.material.color = new Color(0.9f, 0.3f, 0.3f);

                state = State.Recover;
                stateTimer = phase2 ? 0.8f : 1.2f;
                actionCooldown = phase2 ? 0.36f : 0.6f;
            }
            else
            {
                // Brief pause between dashes (player can dodge)
                dashTimer = phase2 ? 0.15f : 0.25f;
                StartNextDash();
            }
        }
    }

    // --- PARRY: stand ready, if hit during window reflects damage back ---
    void StartParry()
    {
        state = State.Parry;
        parryWindow = phase2 ? 1.2f : 1f;
        parryTriggered = false;
        isVulnerable = false;

        // Visual: shield glow
        foreach (var r in renderers)
            if (r != null)
                r.material.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 1f) * 3f);

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void ParryUpdate()
    {
        parryWindow -= Time.deltaTime;

        // Pulsing shield visual
        float pulse = Mathf.PingPong(Time.time * 6f, 1f);
        foreach (var r in renderers)
            if (r != null)
                r.material.SetColor("_EmissionColor", new Color(0.3f, 0.3f, 1f) * (1f + pulse * 3f));

        // Face player
        Vector3 toP = target.position - transform.position; toP.y = 0;
        if (toP.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toP.normalized);

        if (parryWindow <= 0)
        {
            // Parry failed (not hit) — counter attack anyway with a dash
            foreach (var r in renderers)
                if (r != null) r.material.SetColor("_EmissionColor", Color.black);

            Vector3 dir = toP.normalized;
            StartDashCombo(dir);
        }
    }

    /// <summary>Called from external damage system when boss takes damage during parry.</summary>
    public void OnParryHit()
    {
        if (state != State.Parry || parryTriggered) return;
        parryTriggered = true;

        // Counter-attack: instant dash to player + AoE
        Vector3 toP = target.position - transform.position; toP.y = 0;
        transform.position = target.position - toP.normalized * 1.5f;

        float radius = 3f;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead) hp.TakeDamage(attackDamage + 2);
        }

        SpawnSlashVFX(transform.position, radius);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.5f);
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.06f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

        foreach (var r in renderers)
            if (r != null) r.material.SetColor("_EmissionColor", Color.black);

        state = State.Recover;
        stateTimer = 0.5f;
        actionCooldown = 0.6f;
    }

    // --- SHIELD BASH: short dash + stun ---
    void StartShieldBash(Vector3 dir)
    {
        state = State.ShieldBash;
        dashDir = dir;
        stateTimer = 0.25f;
        isVulnerable = false;

        foreach (var r in renderers)
            if (r != null) r.material.color = new Color(0.8f, 0.8f, 0.9f);
        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void ShieldBashUpdate(float speedMult)
    {
        rb.MovePosition(transform.position + dashDir * chargeSpeed * 0.8f * speedMult * Time.deltaTime);
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
        {
            // Bash AoE
            Collider[] hits = Physics.OverlapSphere(transform.position, 2f);
            foreach (var h in hits)
            {
                var pc = h.GetComponent<PlayerController>();
                if (pc == null || pc.isInvulnerable) continue;
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                {
                    hp.TakeDamage(2);
                    // Knockback player
                    GameFeel.ApplyKnockback(h.transform, h.transform.position + dashDir, 4f);
                }
            }

            SpawnSlashVFX(transform.position, 2f, new Color(0.8f, 0.8f, 1f));
            SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);

            foreach (var r in renderers)
                if (r != null) r.material.color = baseColor;

            // Chain into dash combo
            if (phase2 && Random.value < 0.5f)
            {
                StartDashCombo(dashDir);
            }
            else
            {
                state = State.Stalk;
                actionCooldown = 0.48f;
            }
        }
    }

    // --- SWEEP SLASH: circular attack around self ---
    void StartSweepSlash()
    {
        state = State.SweepSlash;
        sweepAngle = 0f;
        sweepDir = Random.value > 0.5f ? 1f : -1f;
        stateTimer = phase2 ? 0.6f : 0.8f;
        isVulnerable = false;

        SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position);
    }

    void SweepSlashUpdate(float speedMult)
    {
        stateTimer -= Time.deltaTime;

        // Spin and move toward player
        float spinSpeed = phase2 ? 900f : 720f;
        sweepAngle += spinSpeed * Time.deltaTime;
        transform.Rotate(0, spinSpeed * sweepDir * Time.deltaTime, 0);

        // Move toward player during spin
        Vector3 toP = target.position - transform.position; toP.y = 0;
        rb.MovePosition(transform.position + toP.normalized * moveSpeed * 1.5f * speedMult * Time.deltaTime);

        // Continuous damage during spin
        Collider[] hits = Physics.OverlapSphere(transform.position, 2.5f);
        foreach (var h in hits)
        {
            var pc = h.GetComponent<PlayerController>();
            if (pc == null || pc.isInvulnerable) continue;
            var hp = h.GetComponent<Health>();
            if (hp != null && !hp.IsDead) hp.TakeDamage(attackDamage);
        }

        if (stateTimer <= 0)
        {
            SpawnSlashVFX(transform.position, 2.5f);
            SFXSystem.Play(SFXSystem.SFXType.Hit, transform.position);

            isVulnerable = true;
            foreach (var r in renderers)
                if (r != null) r.material.color = new Color(0.9f, 0.3f, 0.3f);

            state = State.Recover;
            stateTimer = phase2 ? 0.6f : 1f;
            actionCooldown = 0.48f;
        }
    }

    // --- MIRROR CLONES (phase 2): spawn 2 clones that dash-attack ---
    void StartMirrorClones()
    {
        state = State.MirrorClones;
        stateTimer = 0.5f;

        SFXSystem.Play(SFXSystem.SFXType.DualCast, transform.position);
    }

    void MirrorClonesUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            // Spawn 2 mirror clones at flanking positions
            for (int i = 0; i < 2; i++)
            {
                Vector3 toP = target.position - transform.position; toP.y = 0;
                Vector3 perp = Vector3.Cross(toP.normalized, Vector3.up) * (i == 0 ? 1 : -1);
                Vector3 pos = target.position + perp * 4f;
                pos.y = 0;
                SpawnClone(pos);
            }

            state = State.Stalk;
            actionCooldown = 1.8f;
        }
    }

    void SpawnClone(Vector3 pos)
    {
        var clone = new GameObject("MirrorClone");
        clone.transform.position = pos;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<CapsuleCollider>());
        body.transform.parent = clone.transform;
        body.transform.localPosition = Vector3.up;
        body.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
        // Translucent silver
        var mat = ShaderCache.NewEmissive(new Color(0.5f, 0.5f, 0.6f, 0.5f), 1.5f);
        body.GetComponent<Renderer>().material = mat;

        var cloneRb = clone.AddComponent<Rigidbody>();
        cloneRb.useGravity = false; cloneRb.isKinematic = true;

        var ai = clone.AddComponent<MirrorCloneAI>();
        ai.Setup(target, attackDamage, chargeSpeed);

        clones.Add(clone);
        Destroy(clone, 6f);
    }

    // --- RECOVER ---
    void RecoverUpdate()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            isVulnerable = false;
            foreach (var r in renderers)
                if (r != null)
                {
                    r.material.color = baseColor;
                    r.material.SetColor("_EmissionColor", Color.black);
                }
            state = State.Stalk;
        }
    }

    // --- PHASE 2 ---
    void EnterPhase2()
    {
        if (GameFeel.Instance != null) GameFeel.Instance.Hitstop(0.07f);
        if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.5f);
        SFXSystem.Play(SFXSystem.SFXType.Explosion, transform.position);

        baseColor = new Color(0.4f, 0.4f, 0.55f);
        moveSpeed *= 1.2f;
        chargeSpeed *= 1.15f;
    }

    void SpawnSlashVFX(Vector3 pos, float radius = 1.5f, Color? color = null)
    {
        Color c = color ?? new Color(0.7f, 0.7f, 0.9f);
        var vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(vfx.GetComponent<CapsuleCollider>());
        vfx.transform.position = pos + Vector3.up * 0.5f;
        vfx.transform.localScale = new Vector3(radius * 2, 0.08f, radius * 2);
        vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(c, 2f);
        Destroy(vfx, 0.2f);
    }

    void CleanupClones()
    {
        foreach (var c in clones) if (c != null) Destroy(c);
        clones.Clear();
    }

    void OnDestroy() { CleanupClones(); }
}

/// <summary>Mirror clone AI — dashes at player then disappears.</summary>
public class MirrorCloneAI : MonoBehaviour
{
    Transform target;
    int damage;
    float speed;
    float dashDelay = 0.8f;
    float dashDuration;
    Vector3 dashDir;
    bool dashing;
    Rigidbody rb;
    int dashesLeft = 2;

    public void Setup(Transform tgt, int dmg, float spd)
    {
        target = tgt;
        damage = dmg;
        speed = spd;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

        if (!dashing)
        {
            // Face player
            Vector3 toP = target.position - transform.position; toP.y = 0;
            if (toP.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(toP.normalized);

            dashDelay -= Time.deltaTime;
            if (dashDelay <= 0)
            {
                dashing = true;
                dashDir = toP.normalized;
                dashDuration = 0.25f;
                SFXSystem.Play(SFXSystem.SFXType.Dash, transform.position);
            }
        }
        else
        {
            rb.MovePosition(transform.position + dashDir * speed * Time.deltaTime);
            dashDuration -= Time.deltaTime;

            // Damage on contact
            Collider[] hits = Physics.OverlapSphere(transform.position, 1.2f);
            foreach (var h in hits)
            {
                var pc = h.GetComponent<PlayerController>();
                if (pc == null || pc.isInvulnerable) continue;
                var hp = h.GetComponent<Health>();
                if (hp != null && !hp.IsDead) hp.TakeDamage(damage);
            }

            if (dashDuration <= 0)
            {
                dashesLeft--;
                if (dashesLeft <= 0)
                {
                    // Despawn VFX
                    var vfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(vfx.GetComponent<SphereCollider>());
                    vfx.transform.position = transform.position + Vector3.up;
                    vfx.transform.localScale = Vector3.one * 1.5f;
                    vfx.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(0.5f, 0.5f, 0.7f, 0.4f), 2f);
                    Destroy(vfx, 0.2f);
                    Destroy(gameObject);
                }
                else
                {
                    dashing = false;
                    dashDelay = 0.5f;
                }
            }
        }
    }
}
