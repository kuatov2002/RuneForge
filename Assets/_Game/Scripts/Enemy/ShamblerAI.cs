using UnityEngine;

public class ShamblerAI : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public int attackDamage = 1;
    public Color baseColor = new Color(0.8f, 0.2f, 0.2f);
    public int floorLevel = 1;

    Transform target;
    float attackTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // Void pull
    Vector3 pullTarget;
    float pullTimer;

    // Attack telegraph
    bool isWindingUp;
    float windupTimer;
    const float WindupDuration = 0.35f;

    // Floor 3+: leap attack
    float leapCooldown;
    bool isLeaping;
    float leapTimer;
    Vector3 leapTarget;

    // Floor 5+: slam AoE
    float slamCooldown;

    void Start()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        if (health != null) health.OnDeath += OnDie;
    }

    void OnDie()
    {
        isDead = true;
        foreach (var r in renderers)
            if (r != null) r.material.color = Color.gray;
        Destroy(gameObject, 0.3f);
    }

    public void ApplyPull(Vector3 pullTo, float duration)
    {
        pullTarget = pullTo;
        pullTimer = duration;
    }

    void Update()
    {
        if (isDead || target == null) return;

        UpdateStatusVisual();

        // Being pulled by void
        if (pullTimer > 0)
        {
            pullTimer -= Time.deltaTime;
            Vector3 dir = (pullTarget - transform.position);
            dir.y = 0;
            if (dir.magnitude > 0.3f)
                rb.MovePosition(transform.position + dir.normalized * 6f * Time.deltaTime);
            return;
        }

        // Stunned/frozen - can't act
        if (health.IsStunned) return;

        float speedMult = health.SpeedMultiplier;
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;

        // Wind-up phase
        if (isWindingUp)
        {
            windupTimer -= Time.deltaTime;
            float flash = Mathf.PingPong(Time.time * 10f, 1f);
            foreach (var r in renderers)
                if (r != null && r.gameObject.name != "Eye")
                    r.material.color = Color.Lerp(baseColor, Color.white, flash);

            if (windupTimer <= 0)
            {
                isWindingUp = false;
                var playerHealth = target.GetComponent<Health>();
                var playerCtrl = target.GetComponent<PlayerController>();
                if (playerHealth != null && !playerHealth.IsDead
                    && (playerCtrl == null || !playerCtrl.isInvulnerable)
                    && Vector3.Distance(transform.position, target.position) < attackRange + 0.5f)
                {
                    playerHealth.TakeDamage(attackDamage);
                }
            }
            return;
        }

        // Floor 3+: leap attack when far from player
        if (floorLevel >= 3 && !isLeaping)
        {
            leapCooldown -= Time.deltaTime;
            if (leapCooldown <= 0 && dist > 4f && dist < 10f)
            {
                leapCooldown = 5f - (floorLevel - 3) * 0.5f; // faster on higher floors
                isLeaping = true;
                leapTimer = 0.4f;
                leapTarget = target.position;
                // Telegraph: red circle at landing spot
                var warn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(warn.GetComponent<CapsuleCollider>());
                warn.transform.position = leapTarget + Vector3.up * 0.02f;
                warn.transform.localScale = new Vector3(2f, 0.01f, 2f);
                warn.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.2f, 0.1f, 0.5f), 2f);
                Destroy(warn, 0.5f);
                return;
            }
        }

        // Leap in progress
        if (isLeaping)
        {
            leapTimer -= Time.deltaTime;
            Vector3 toTarget = leapTarget - transform.position;
            toTarget.y = 0;
            rb.MovePosition(transform.position + toTarget.normalized * 18f * Time.deltaTime + Vector3.up * 0.02f);

            if (leapTimer <= 0 || toTarget.magnitude < 0.5f)
            {
                isLeaping = false;
                // Slam damage on landing
                Collider[] hits = Physics.OverlapSphere(transform.position, 1.8f);
                foreach (var h in hits)
                {
                    var pc = h.GetComponent<PlayerController>();
                    if (pc != null && !pc.isInvulnerable)
                    {
                        var php = h.GetComponent<Health>();
                        if (php != null && !php.IsDead) php.TakeDamage(attackDamage + 1);
                    }
                }
                // VFX: impact ring
                GameFeel.SpawnDeathVFX(transform.position, baseColor);
                if (TopDownCamera.Instance != null) TopDownCamera.Instance.AddTrauma(0.15f);
            }
            return;
        }

        // Floor 5+: periodic slam AoE (telegraphed shockwave)
        if (floorLevel >= 5 && dist <= attackRange + 1f)
        {
            slamCooldown -= Time.deltaTime;
            if (slamCooldown <= 0)
            {
                slamCooldown = 4f;
                // AoE slam: damage + knockback in radius
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(ring.GetComponent<CapsuleCollider>());
                ring.transform.position = transform.position + Vector3.up * 0.05f;
                ring.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
                ring.GetComponent<Renderer>().material = ShaderCache.NewEmissive(new Color(1f, 0.3f, 0.1f), 3f);
                ring.AddComponent<DeathRingEffect>().Init(5f, 0.5f);

                Collider[] slamHits = Physics.OverlapSphere(transform.position, 2.5f);
                foreach (var h in slamHits)
                {
                    var pc = h.GetComponent<PlayerController>();
                    if (pc != null && !pc.isInvulnerable)
                    {
                        var php = h.GetComponent<Health>();
                        if (php != null && !php.IsDead) php.TakeDamage(attackDamage);
                    }
                }
            }
        }

        if (dist > attackRange)
        {
            Vector3 moveDir = toPlayer.normalized;
            rb.MovePosition(transform.position + moveDir * moveSpeed * speedMult * Time.deltaTime);
            if (moveDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(moveDir);
        }
        else
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = attackCooldown;
                isWindingUp = true;
                windupTimer = WindupDuration;
            }
        }
    }

    void UpdateStatusVisual()
    {
        if (renderers == null || renderers.Length == 0) return;

        Color col = baseColor;
        if (health.IsStunned)
            col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);

        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = col;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDie;
    }
}
