using UnityEngine;

/// <summary>
/// Archer — ranged enemy that keeps distance.
/// Mechanics:
/// - Keeps preferred distance, retreats if too close
/// - REPOSITION: teleports (quick dash) to a new spot after taking damage
/// - TRAP ARROW: occasionally shoots a slow arrow that leaves a danger zone on the ground
/// - SNIPER MODE: after standing still for 2s, next shot is instant (no telegraph) and faster
/// - Floor 3+: burst fire
/// - Floor 4+: scatter shot
/// </summary>
public class ArcherAI : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float preferredDist = 7f;
    public float retreatDist = 4f;
    public float shootCooldown = 2f;
    public float projectileSpeed = 8f;
    public int attackDamage = 1;
    public Color baseColor = new Color(0.2f, 0.6f, 0.2f);
    public int floorLevel = 1;

    Transform target;
    float shootTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // Telegraph
    bool isAiming;
    float aimTimer;
    const float AimDuration = 0.5f;
    Vector3 aimDir;
    GameObject aimLine;

    // Burst fire
    int burstShotsRemaining;
    float burstTimer;

    // Scatter
    float scatterCooldown;

    // Reposition dash on damage
    int lastHP;
    float repositionCooldown;
    bool isDashing;
    float dashTimer;
    Vector3 dashDir;
    const float DashDuration = 0.2f;
    const float DashSpeed = 18f;

    // Sniper mode
    float standingStillTimer;
    bool isSniperReady;
    Vector3 lastPos;

    // Trap arrow
    float trapArrowCooldown = 8f;
    float trapArrowTimer;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null)
        {
            health.OnDeath += OnDie;
            lastHP = health.currentHP;
        }
        lastPos = transform.position;
        trapArrowTimer = trapArrowCooldown * 0.5f; // first trap arrow sooner
    }

    void OnDie()
    {
        isDead = true;
        foreach (var r in renderers) if (r != null) r.material.color = Color.gray;
        if (aimLine != null) Destroy(aimLine);
        Destroy(gameObject, 0.3f);
    }

    void Update()
    {
        if (isDead || target == null) return;
        UpdateStatusVisual();
        if (health.IsStunned) return;
        float speedMult = health.SpeedMultiplier;

        // Dash in progress
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            rb.MovePosition(transform.position + dashDir * DashSpeed * Time.deltaTime);
            // Spawn ghost trail during dash
            if (Mathf.FloorToInt(dashTimer * 30f) % 2 == 0)
                GameFeel.SpawnDashGhost(renderers, transform.position, transform.rotation);
            if (dashTimer <= 0) isDashing = false;
            return;
        }

        // Check if took damage — reposition
        if (health.currentHP < lastHP && repositionCooldown <= 0)
        {
            lastHP = health.currentHP;
            repositionCooldown = 3f;
            Reposition();
        }
        lastHP = health.currentHP;
        repositionCooldown -= Time.deltaTime;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Track if standing still for sniper mode
        float moved = Vector3.Distance(transform.position, lastPos);
        lastPos = transform.position;
        if (moved < 0.05f * Time.deltaTime)
            standingStillTimer += Time.deltaTime;
        else
            standingStillTimer = 0;

        isSniperReady = standingStillTimer >= 2f;

        // Sniper visual indicator
        if (isSniperReady)
        {
            // Eyes glow brighter when sniper ready
            foreach (var r in renderers)
                if (r != null && r.gameObject.name == "Eye")
                    r.material.SetColor("_EmissionColor", new Color(1f, 0f, 0f) * 5f);
        }

        // Movement
        Vector3 moveDir = Vector3.zero;
        if (dist < retreatDist) moveDir = -toPlayer.normalized;
        else if (dist > preferredDist + 1f) moveDir = toPlayer.normalized;

        // Add strafing: slowly circle the player
        if (moveDir.sqrMagnitude < 0.01f && dist >= retreatDist && dist <= preferredDist + 1f)
        {
            Vector3 strafe = Vector3.Cross(toPlayer.normalized, Vector3.up);
            moveDir = strafe * (Mathf.Sin(Time.time * 0.5f + GetInstanceID()) > 0 ? 1 : -1);
        }

        if (moveDir.sqrMagnitude > 0.01f)
        {
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);
            standingStillTimer = 0;
        }
        transform.rotation = Quaternion.LookRotation(toPlayer.normalized);

        // Aim telegraph
        if (isAiming)
        {
            aimTimer -= Time.deltaTime;
            float flash = Mathf.PingPong(Time.time * 10f, 1f);
            var bow = transform.Find("Bow");
            if (bow != null)
                bow.GetComponent<Renderer>().material.color = Color.Lerp(new Color(0.4f, 0.25f, 0.1f), Color.white, flash);

            if (aimTimer <= 0)
            {
                isAiming = false;
                if (aimLine != null) Destroy(aimLine);
                // Re-aim at player's current position for leading
                aimDir = (target.position - transform.position).normalized;
                aimDir.y = 0; aimDir.Normalize();
                EnemyProjectile.Create(transform.position, aimDir, projectileSpeed, attackDamage,
                    new Color(0.3f, 0.8f, 0.3f));
                if (floorLevel >= 3) burstShotsRemaining = 2;
            }
            return;
        }

        // Burst fire
        if (burstShotsRemaining > 0)
        {
            burstTimer -= Time.deltaTime;
            if (burstTimer <= 0)
            {
                burstTimer = 0.2f;
                burstShotsRemaining--;
                Vector3 burstDir = (target.position - transform.position).normalized;
                burstDir = Quaternion.Euler(0, Random.Range(-8f, 8f), 0) * burstDir;
                EnemyProjectile.Create(transform.position, burstDir, projectileSpeed * 1.1f, attackDamage,
                    new Color(0.3f, 0.8f, 0.3f));
            }
            return;
        }

        // Floor 4+: scatter
        if (floorLevel >= 4)
        {
            scatterCooldown -= Time.deltaTime;
            if (scatterCooldown <= 0 && dist < 8f)
            {
                scatterCooldown = 6f;
                Vector3 scatterDir = toPlayer.normalized;
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 dir = Quaternion.Euler(0, i * 20f, 0) * scatterDir;
                    EnemyProjectile.Create(transform.position, dir, projectileSpeed * 0.8f, attackDamage,
                        new Color(0.8f, 0.3f, 0.3f));
                }
                SFXSystem.Play(SFXSystem.SFXType.Cast, transform.position, 0.5f);
            }
        }

        // Trap arrow
        trapArrowTimer -= Time.deltaTime;
        if (trapArrowTimer <= 0 && dist < 10f)
        {
            trapArrowTimer = trapArrowCooldown;
            ShootTrapArrow(toPlayer.normalized);
        }

        // Shoot
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0 && dist < 12f)
        {
            shootTimer = shootCooldown;

            if (isSniperReady)
            {
                // Instant shot, no telegraph, faster projectile
                Vector3 sniperDir = toPlayer.normalized;
                EnemyProjectile.Create(transform.position, sniperDir, projectileSpeed * 1.8f, attackDamage + 1,
                    new Color(1f, 0.2f, 0.2f)); // red = sniper shot
                standingStillTimer = 0;
                SFXSystem.Play(SFXSystem.SFXType.CritHit, transform.position, 0.5f);
            }
            else
            {
                // Normal aimed shot with telegraph
                aimDir = toPlayer.normalized;
                isAiming = true;
                aimTimer = AimDuration;

                aimLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(aimLine.GetComponent<BoxCollider>());
                aimLine.name = "AimLine";
                Vector3 mid = transform.position + aimDir * 5f;
                aimLine.transform.position = mid + Vector3.up * 0.05f;
                aimLine.transform.localScale = new Vector3(0.08f, 0.02f, 10f);
                aimLine.transform.rotation = Quaternion.LookRotation(aimDir);
                aimLine.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0.1f), 2f);
                Destroy(aimLine, AimDuration + 0.1f);
            }
        }
    }

    void Reposition()
    {
        // Visible dash away from player (not teleport)
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        Vector3 awayDir = -toPlayer.normalized;
        Vector3 perpendicular = Vector3.Cross(awayDir, Vector3.up);
        dashDir = (awayDir + perpendicular * Random.Range(-0.8f, 0.8f)).normalized;
        dashDir.y = 0;

        isDashing = true;
        dashTimer = DashDuration;
        standingStillTimer = 0;

        // Cancel aim if dashing
        if (isAiming)
        {
            isAiming = false;
            if (aimLine != null) Destroy(aimLine);
        }

        SFXSystem.Play(SFXSystem.SFXType.Dash, transform.position, 0.3f);
    }

    void ShootTrapArrow(Vector3 dir)
    {
        // Slow glowing arrow that creates a danger zone on ground where it lands
        var arrow = EnemyProjectile.Create(transform.position, dir, 4f, 0,
            new Color(1f, 0.6f, 0.1f)); // orange = trap

        // Add trap behavior when it hits ground
        arrow.AddComponent<TrapArrowBehavior>().Init(attackDamage);
    }

    void UpdateStatusVisual()
    {
        if (renderers == null) return;
        Color col = baseColor;
        if (health.IsStunned) col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye") r.material.color = col;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDie;
        if (aimLine != null) Destroy(aimLine);
    }
}

/// <summary>Trap arrow: after 2s creates a danger zone that damages player standing in it.</summary>
public class TrapArrowBehavior : MonoBehaviour
{
    int _damage;
    float _timer = 2f;
    bool _deployed;

    public void Init(int damage) { _damage = damage; }

    void Update()
    {
        if (_deployed) return;
        _timer -= Time.deltaTime;
        if (_timer <= 0)
        {
            Deploy();
        }
    }

    void Deploy()
    {
        _deployed = true;
        Vector3 pos = transform.position;
        pos.y = 0.05f;

        // Create danger zone
        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(zone.GetComponent<CapsuleCollider>());
        zone.transform.position = pos;
        zone.transform.localScale = new Vector3(2f, 0.02f, 2f);
        zone.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.3f, 0.1f), 2f);

        var col = zone.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1f;

        var rb = zone.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        zone.AddComponent<DangerZone>().Init(_damage, 3f);
        Object.Destroy(zone, 3f);
    }
}

/// <summary>Danger zone: damages player standing in it periodically.</summary>
public class DangerZone : MonoBehaviour
{
    int _damage;
    float _tickTimer;

    public void Init(int damage, float duration) { _damage = damage; }

    void OnTriggerStay(Collider other)
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;

        var pc = other.GetComponent<PlayerController>();
        if (pc == null || pc.isInvulnerable) return;
        var hp = other.GetComponent<Health>();
        if (hp != null && !hp.IsDead)
            hp.TakeDamage(_damage);
    }
}
