using UnityEngine;

public class ShamblerAI : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float attackRange = 1.5f;
    public float attackCooldown = 1f;
    public int attackDamage = 1;
    public Color baseColor = new Color(0.8f, 0.2f, 0.2f);

    Transform target;
    float attackTimer;
    Health health;
    Rigidbody rb;
    Renderer[] renderers;
    bool isDead;

    // Void pull
    Vector3 pullTarget;
    float pullTimer;

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
                var playerHealth = target.GetComponent<Health>();
                var playerCtrl = target.GetComponent<PlayerController>();
                if (playerHealth != null && !playerHealth.IsDead
                    && (playerCtrl == null || !playerCtrl.isInvulnerable))
                {
                    playerHealth.TakeDamage(attackDamage);
                }
            }
        }
    }

    void UpdateStatusVisual()
    {
        if (renderers == null || renderers.Length == 0) return;

        Color col = baseColor;
        if (health.IsStunned)
            col = health.IsFrozen ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 1f, 0.3f);
        else if (health.IsSlowed)
            col = new Color(0.4f, 0.6f, 1f);
        else if (health.PoisonStacks > 0)
            col = Color.Lerp(baseColor, new Color(0.2f, 0.9f, 0.1f), health.PoisonStacks / 5f);

        foreach (var r in renderers)
            if (r != null && r.gameObject.name != "Eye")
                r.material.color = col;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= OnDie;
    }
}
